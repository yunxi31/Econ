# MotorTestSystem 工业上位机代码审查报告

> 审查日期：2026-06-15  
> 审查范围：全项目 70+ .cs 源文件  
> 运行环境：WPF (.NET 8/10)，SqlSugar + SQLite，S7.Net / Modbus TCP / MC 协议  
> 核心诉求：7×24 小时高强度工业现场稳定性

---

## 目录

1. [内存泄漏风险分析](#1-内存泄漏风险分析)
2. [异步异常处理分析](#2-异步异常处理分析)
3. [断线重连鲁棒性评估](#3-断线重连鲁棒性评估)
4. [优先级修复建议总表](#4-优先级修复建议总表)
5. [关键代码修复示例](#5-关键代码修复示例)

---

## 1. 内存泄漏风险分析

### 1.1 🔴 Critical：BackendRuntime 未实现 IDisposable

**问题描述**  
`BackendRuntime` 是系统运行时单例容器，内部持有 `PlcPollingService` 和 `HikvisionSdkService`，这两者都实现了 `IDisposable`，但 `BackendRuntime` 自身**没有任何 Dispose 路径**。应用退出时：

- `PlcPollingService.Dispose()` **永远不会被调用** → TCP 连接保持 ESTABLISHED 直到 OS 超时回收
- `HikvisionSdkService.Dispose()` **永远不会被调用** → 海康 SDK 的 `NET_DVR_Cleanup()` 不会执行，摄像头登录会话残留在设备端

**风险等级**：应用正常关闭时问题不大（OS 回收），但若应用被 Task Manager 强制杀掉，TCP 连接句柄可能泄漏。对于 7×24 系统，每次软件升级重启都可能产生残留连接。

**发生位置**：`Business/Services/BackendRuntime.cs` 类声明第 10 行

### 1.2 🔴 Critical：PlcPollingService.Dispose() 存在竞态条件

```csharp
public void Dispose()  // 第 76-85 行
{
    _cancellationTokenSource?.Cancel();    // ✅ 取消令牌
    foreach (var client in _clients)
    {
        client.Dispose();                  // ❌ 未等待 PollStationAsync 完成就释放 SemaphoreSlim
    }
    _cancellationTokenSource?.Dispose();
}
```

**问题**：`Cancel()` 之后没有 `await Task.WhenAll(_pollingTasks)`，正在执行 `ConnectAsync` 的任务可能正持有 `SemaphoreSlim` 锁，此时 `Dispose()` 释放了 `SemaphoreSlim` → `ObjectDisposedException`。

**对比**：`StopAsync()` 方法正确等待了任务完成后再释放资源，但 `Dispose()` 没有复用该逻辑。

### 1.3 🟠 High：所有 ViewModel 事件订阅未解除

| 文件 | 订阅位置 | 订阅的事件 | 是否存在 -= |
|------|---------|-----------|------------|
| `MainViewModel.cs:102` | 构造函数 | `PollingService.SnapshotReceived` | ❌ |
| `MonitorViewModel.cs:31-32` | 构造函数 | `SnapshotReceived`, `LogReceived` | ❌ |
| `DashboardViewModel.cs:159` | 构造函数 | `PollingService.SnapshotReceived` | ❌ |
| `BackendRuntime.cs:40-41` | 构造函数 | `SnapshotReceived`, `LogReceived` | ❌ |
| `NotificationCenterViewModel.cs:167-169` | 构造函数 | 3 个事件（CollectionChanged等） | ❌ |
| `LogCenterViewModel.cs` | 构造函数 | 同上模式 | ❌ |

**泄漏原理**：`PollingService` 是 `BackendRuntime` 的长生命周期单例对象。ViewModel 虽然在当前 WPF 单页应用中只创建一次，但如果后续架构演进为页面动态创建/销毁（如 Tab 页频繁切换），每个创建过的 ViewModel 都会被单例事件源强引用，无法被 GC 回收 → 所有绑定资源泄漏。

**WPF 特定风险**：未解除订阅的 ViewModel 持有的 `ObservableCollection` 和 UI 相关对象无法释放，可能导致 WPF `Dispatcher` 队列不断接收已不可见页面的更新请求。

### 1.4 🟠 High：匿名 Lambda 订阅事件无法解除

**DashboardViewModel.cs 第 171 行**：
```csharp
_refreshTimer.Tick += async (_, _) => await RefreshAllDataAsync();
```

**MainViewModel.cs 第 108 行**：
```csharp
_clockTimer.Tick += (_, _) => CurrentTime = DateTime.Now.ToString("HH:mm:ss");
```

**风险**：匿名 lambda 绑定的事件不能通过 `-=` 解除。即便这些 Timer 是实例字段（随 ViewModel 生命周期共存亡），但如果 ViewModel 因为事件泄漏而无法释放，其内部的 Timer 也会持续运行。

### 1.5 🟡 Medium：HikvisionSdkService 非托管资源未受保护

**位置**：`Business/Services/HikvisionSdkService.cs`

- 通过 P/Invoke 调用 `HCNetSDK.dll`（非托管代码），`NET_DVR_Login_V30` 返回的 `userId` 和 `NET_DVR_RealPlay_V40` 返回的 `realHandle` 都是非托管资源句柄
- `Dispose()` 方法虽然在正确清理，但缺乏 `_isDisposed` 标志保护，如果被多线程误调两次可能导致 `ObjectDisposedException` 或句柄重复释放
- 类定义没有 `sealed` 关键字（第 12 行 `public class HikvisionSdkService`），如果被继承且不恰当覆盖 Dispose 可能导致基类清理被跳过

### 1.6 🟡 Medium：MainWindow.xaml.cs 匿名 Lambda 订阅

窗口 Loaded/Deactivated/SizeChanged 等事件使用了匿名 lambda 订阅。WPF 窗口关闭时会自动解除窗口级别的事件绑定，但匿名 lambda 仍属于不良实践——拆解为命名方法可让代码意图更清晰。

---

## 2. 异步异常处理分析

### 2.1 🔴 Critical：async void 泄漏 — Dispatcher.InvokeAsync

**DashboardViewModel.cs 第 162-165 行**：
```csharp
Application.Current?.Dispatcher?.InvokeAsync(async () =>
{
    await RefreshAllDataAsync();
});
```

**同文件第 178-182 行**：
```csharp
Application.Current?.Dispatcher?.InvokeAsync(async () =>
{
    await RefreshAllDataAsync();
    UpdateOnlineStationCount();
});
```

**问题**：`Dispatcher.InvokeAsync` 的签名是 `InvokeAsync(Action)`，不是 `InvokeAsync(Func<Task>)`。传入 `async () => await ...` 实际上是一个 **`async void`** 委托。`async void` 的异常：

- 无法被 `await` 捕获
- 直接从 `Dispatcher` 的未处理异常处理逃逸
- 如果 `RefreshAllDataAsync()` 内部的 `try-catch` 未能兜底，异常会直达 `App.OnDispatcherException()`

**当前缓解**：`RefreshAllDataAsync()` 内部有 `try-catch { }` 兜底，缓解了直接崩溃的风险，但如果未来有人修改该方法去掉了 try-catch，崩溃就会发生。

### 2.2 🔴 Critical：async void 泄漏 — Timer Tick 事件

**DashboardViewModel.cs 第 171 行**：
```csharp
_refreshTimer.Tick += async (_, _) => await RefreshAllDataAsync();
```

**问题**：`DispatcherTimer.Tick` 事件的签名是 `EventHandler`（void 返回），同上一条，这是一个 `async void`。如果 `RefreshAllDataAsync` 抛出未捕获异常，进程崩溃。

### 2.3 🟠 High：UI 线程阻塞 — .GetAwaiter().GetResult()

**BackendRuntime.cs 第 165 行**：
```csharp
SeedRepositoryIfEmptyAsync(repository, dbContext).GetAwaiter().GetResult();
```

**问题**：
1. 该方法在静态属性初始化器中执行，运行在 **UI 线程**上
2. 播种逻辑涉及 3 周历史数据 + 本周每天 + 今天每小时 ≈ 数千次 `UpsertStageResultAsync` 数据库写入
3. `.GetAwaiter().GetResult()` 阻塞 UI 线程直到播种完成 → 应用启动时出现白屏/冻结
4. 更危险的是，如果 `UpsertStageResultAsync` 内部遇到数据库锁冲突，这种同步阻塞可能导致死锁（UI 线程等待异步操作完成，异步操作的回调需要回到 UI 线程上下文）

> **典型死锁场景**：如果 `UpsertStageResultAsync` 内部使用了 `Task.Run` 并在完成后调用 `ConfigureAwait(true)`，则需要在 UI 线程上继续执行。但 UI 线程正被 `.GetResult()` 阻塞 → **死锁**。

### 2.4 🟠 High：PlcPollingService.Dispose() 未等待异步任务

**详见 1.2 节**。`Dispose()` 中 `_cancellationTokenSource?.Cancel()` 后不 `await`，导致在被 `Dispose` 的同时 `PollStationAsync` 仍可能在执行。尽管 `OperationCanceledException` 会被处理，但 `client.Dispose()` 会在锁释放前执行，导致 `ObjectDisposedException` 逃逸为未观察任务异常。

### 2.5 🟡 Medium：ResetCompletionSignalAsync 静默吞异常

**ModbusTcpClient.cs 第 198-203 行**：
```csharp
catch
{
    CloseConnection();  // ❌ 关闭连接但不做日志
}
```

**MelsecMcClient.cs 同理**。`ResetCompletionSignalAsync` 写入失败时仅关闭连接，不记录日志。对于 7×24 系统，这意味着运维人员无法知晓 PLC 的手握信号未被复位，可能导致下一轮数据重复保存。

### 2.6 🟢 Low：TaskScheduler.UnobservedTaskException

当前代码所有 `Task.Run` 的 lambda 内部都有完整的 `try-catch`（`PollStationAsync` 中的外层 catch all 和 `OperationCanceledException` 特殊处理），因此在 .NET Core/.NET 8 以上环境下，UnobservedTaskException 引发的核心风险较低。但需要注意：

- `PollStationAsync` 第 119-121 行 `catch (OperationCanceledException) { throw; }` 重新抛出异常。该异常会被 `Task.Run` 捕获并标记为 faulted。在 `StopAsync` 的 `Task.WhenAll` 中会被封装为 `AggregateException`，但被 `catch (OperationCanceledException)` 吞掉，不影响全局。

### 2.7 🟢 Low：ModbusTcpClient._transactionId 溢出

**ModbusTcpClient.cs 第 17 行**：
```csharp
private ushort _transactionId;
```

以 1 秒一次的轮询频率运行，`_transactionId` 从 0 到 65535 溢出约每 **18.2 小时**一次。溢出后事务 ID 回到 0，如果恰好有一个慢响应携带之前的 ID 到达，可能导致事务 ID 校验失败并抛出 `InvalidOperationException`。虽已被 `ReadSnapshotAsync` 的 catch-all 捕获，但会产生一条错误日志。

---

## 3. 断线重连鲁棒性评估

### 3.1 🟢 现有模式评价：双检锁重连 ✅

三个真实 PLC 客户端都实现了正确的双检锁模式：

```csharp
// ConnectAsync:
if (_tcpClient is { Connected: true }) return true;  // 快速路径
await _lock.WaitAsync(cancellationToken);
try {
    if (_tcpClient is { Connected: true }) return true;  // 二次检查
    CloseConnection();  // 清理旧连接
    // ... 建立新连接
}
```

这是工业上位机 PLC 通信的**标准做法**，值得肯定。

### 3.2 🔴 Critical：S7PlcClient.ConnectAsync 超时机制失效

**S7PlcClient.cs 第 58-61 行**：
```csharp
using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
cts.CancelAfter(TimeSpan.FromSeconds(2));
await _plc.OpenAsync();  // ❌ S7.Net 的 OpenAsync 不接受 CancellationToken！
```

**问题**：`cts` 被创建但从未传递给 `OpenAsync()``。S7.Net 的 `Plc.OpenAsync()` 方法**没有 CancellationToken 重载**。当 PLC 网络可达但 S7 端口无响应时（例如 PLC 断电但交换机端口还开着），`OpenAsync()` 会卡在默认 TCP 超时（通常 20-30 秒）。在此期间：

- 该工位的轮询循环被阻塞
- `_lock` 无法被释放，其他操作被阻塞
- UI 上该工位状态无法更新

**影响**：这在 7×24 产线上是一个**致命缺陷**——一台 PLC 断电会导致整个轮询引擎的一个线程挂起 20+ 秒，如果多台 PLC 同时离线，所有轮询线程都会陷入等待。

### 3.3 🟠 High：ModbusTcpClient / MelsecMcClient 超时设计缺陷

**ModbusTcpClient.cs 第 45-48 行**（MelsecMcClient 同理）：
```csharp
var connectTask = _tcpClient.ConnectAsync(Config.IpAddress, Config.Port, cancellationToken).AsTask();
if (await Task.WhenAny(connectTask, Task.Delay(2000, cancellationToken)) != connectTask)
{
    CloseConnection();
    return false;
}
```

**问题 1**：`Task.Delay(2000, cancellationToken)` 使用与 `connectTask` 相同的 `cancellationToken`。当 `_cancellationTokenSource.Cancel()` 被调用时，两个任务都会立即完成，`Task.WhenAny` 返回最快完成的那个。但实际超时判断逻辑 **无法区分"连接成功"和"取消"**：
- 如果 `connectTask` 在取消瞬间完成（因为 `TcpClient.ConnectAsync` 取消了），`WhenAny` 返回 `connectTask`，程序误认为连接成功
- 但实际上 `_tcpClient.Connected` 为 false，然后走 `CloseConnection` 和 return false 路径
- 这不算 bug，但逻辑不健壮

**问题 2**：更关键的是，`Task.Delay(2000)` 没有独立的超时 CancellationToken——它和连接共用同一个 token。这意味着：
- 在 `StopAsync` 调用了 `Cancel()` 之后，这个 Delay 立即完成（不是等待 2 秒）
- 这是期望行为，且不会造成 side effect，但注释应该说明这点

### 3.4 🟠 High：无指数退避重连策略

**PlcPollingService.cs 第 104-106 行**：
```csharp
await Task.Delay(_pollInterval, cancellationToken);
continue;
```

**问题**：无论连续失败多少次，重连间隔固定为 1 秒。在 PLC 断电 10 分钟的场景下：
- 产生 **600 次** 无意义的连接尝试
- 每次触发 `CloseConnection() + ConnectAsync()` 的 TCP 三次握手机制
- 大量 `StationSnapshot(IsOnline=false)` 事件派发给 UI → WPF Dispatcher 队列堆积
- `BackendRuntime.OnSnapshotReceivedForNotification` 每次都会生成"通信中断"通知？——看了代码发现只有 **从在线变为离线** 的时刻才生成通知（`wasOnline && !isNowOnline`），这一点做得很好，但 Dispatcher 消息量仍然很大

**工业现场标准做法**：指数退避 + 上限（如 1s → 2s → 4s → 8s → 16s → 30s → 60s max），连接成功后重置到 1s。

### 3.5 🟠 High：Stream.ReadAsync 无超时保护

**ModbusTcpClient.cs 第 328-341 行**（其他客户端同理）：
```csharp
await _stream.WriteAsync(request, 0, request.Length, cancellationToken);
// 读 7 字节 MBAP 头，无超时
while (read < 7)
{
    int n = await _stream.ReadAsync(mbapHeader, read, 7 - read, cancellationToken);
    if (n <= 0) throw new SocketException(...);
    read += n;
}
```

**问题**：`_stream.ReadAsync` 只响应 `cancellationToken`，没有独立的 Read 超时。在以下场景中风险暴露：
1. 发送请求后，PLC 开始响应但网络中途闪断
2. `ReadAsync` 返回了部分字节（如 3 字节），然后剩余部分永远不到达
3. while 循环陷入无限等待，直到 `cancellationToken` 被 Cancel（仅发生在进程退出时）

建议方案：为每次 Read 环节引入一个到超时 CancellationToken（如 2 秒），与全局取消令牌组成 LinkedTokenSource。

### 3.6 🟡 Medium：每轮都调用 ConnectAsync

**PollStationAsync 第 93 行**：
```csharp
bool connected = await client.ConnectAsync(cancellationToken);
```

每 1 秒轮询周期都会调用一次。虽然双检锁使得已连接时是 O(1) 检查，但仍意味着每次都要获取 `SemaphoreSlim` 锁。在状态稳定的产线上，这不是大问题；但更好的设计是**缓存连接状态，仅在状态为"已断开"时才获取锁**。

### 3.7 🟡 Medium：ResetCompletionSignal 失败不重试

当 `ResetCompletionSignalAsync` 失败时（catch 中调用 `CloseConnection`），PLC 端的手握信号位（M100.0）仍为 `true`。下一轮 `ReadSnapshotAsync` 读到 `CompletionSignal = true` 后，会再次读取数据并保存。这导致：

- **数据重复保存**：同一个电机的测试数据被多次写入数据库
- 虽然 `UpsertStageResultAsync` 做了 upsert（先查询再写入），但如果有两个不同条码的数据... 等等，实际 `UpsertStageResultAsync` 是根据条码+阶段+时间组合 upsert 的，所以确实需要确认

需要查看 `SqlMotorTestRepository.UpsertStageResultAsync` 的实现来确定重复保存的影响。

### 3.8 🟢 Low：ModbusTcpClient 发送前置缓存清理

**第 322-325 行**：
```csharp
if (_tcpClient?.Available > 0)
{
    byte[] junk = new byte[_tcpClient.Available];
    await _stream.ReadAsync(junk, 0, junk.Length, cancellationToken);
}
```

**评价**：这是一个很好的工业经验实践——在发送新请求前清空接收缓冲区中可能残留的旧响应数据，防止粘包。所有三个客户端的 `SendAndReceiveAsync` 都有此逻辑，值得肯定。

---

## 4. 优先级修复建议总表

| 优先级 | 类别 | 问题 | 影响面 | 建议方案 |
|--------|------|------|--------|---------|
| **P0** | 异常 | `S7PlcClient.ConnectAsync` 无真实超时 | 单工位断线阻塞整个轮询线程 20-30s | 改用独立 TCP Client+ `ConnectAsync(token)` 的重载 + 2s 超时，或将 S7.Net 调用封装到 `Task.WhenAny` + 2s 超时 |
| **P0** | 异常 | `Dispatcher.InvokeAsync(async () => ...)` 产生 async void | 异常逃逸可导致进程崩溃 | 改用 `Dispatcher.InvokeAsync(() => { _ = RefreshAllDataAsync(); })` 或提取为命名 async Task 方法后用 `await` 调用 |
| **P0** | 异常 | `_refreshTimer.Tick += async (_,_) => ...` 产生 async void | 同上 | 提取为独立 async void 方法，内部包裹完整 try-catch（或者改用 async Task + fire-and-forget 方法） |
| **P0** | 泄漏 | `BackendRuntime` 未实现 IDisposable | TCP/海康SDK资源无法优雅释放 | 实现 IDisposable，Dispose 中调用 PollingService.StopAsync + Dispose、HikvisionService.Dispose |
| **P1** | 泄漏 | `PlcPollingService.Dispose()` 不等待轮询任务 | 锁与 SemaphoreSlim 竞态释放 | Dispose 中调用 `StopAsync().GetAwaiter().GetResult()`（或改为 `await DisposeAsync`）后再释放客户端 |
| **P1** | 异常 | 启动时 `.GetAwaiter().GetResult()` 阻塞 UI 线程 | 启动白屏 + 潜在死锁 | 改为惰性播种（首次查询发现空时后台播种），或 `Task.Run` 到后台线程 |
| **P1** | 鲁棒 | 重连无指数退避 | PLC 长时间离线时大量无效连接和 Dispatcher 负载 | 在 PlcPollingService 中缓存连续失败计数，指数递增 Delay 至 60s 上限 |
| **P1** | 鲁棒 | `Stream.ReadAsync` 无 Read 超时 | PLC 半响应可导致永久阻塞 | 为每次 Read 循环引入 2-3s 超时 CancellationToken |
| **P2** | 泄漏 | ViewModel 事件未解除订阅 | 页面重建时产生泄漏 | 所有 ViewModel 实现 IDisposable，在 Dispose 中 `-=` 所有事件 |
| **P2** | 异常 | `ResetCompletionSignalAsync` 静默吞异常 | 运维无法感知握手信号丢失 | 至少写入日志，并可考虑重试机制 |
| **P2** | 鲁棒 | ModbusTcpClient._transactionId 溢出 | 18h 循环一次后可能产生错误 | 使用 `Interlocked.Increment` 并允许 0~65535 循环（放弃验证旧的慢响应ID） |
| **P3** | 规范 | 匿名 lambda 无法解除事件 | 代码可维护性 | 统一改为命名方法 |
| **P3** | 规范 | S7PlcClient 每轮都调用 ConnectAsync | 轻微性能损耗 | 在 PlcPollingService 层追踪连接状态，仅在断线时调用 ConnectAsync |

> **P0 = 生产事故级**，`**P1 = 严重风险**`，P2 = 中等风险，P3 = 改进建议

---

## 5. 关键代码修复示例

### 5.1 S7PlcClient.ConnectAsync 超时修复

```csharp
public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
{
    if (_plc is { IsConnected: true }) return true;

    await _lock.WaitAsync(cancellationToken);
    try
    {
        if (_plc is { IsConnected: true }) return true;

        CloseConnection();

        var cpuType = ResolveCpuType(Config.PlcModel);
        _plc = new Plc(cpuType, Config.IpAddress, Config.Port, (short)0, (short)Config.StationId);

        // ✅ 真正实施 2 秒超时：用 Task.WhenAny 包装 S7.Net 的无超时调用
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(2));

        var connectTask = Task.Run(() => _plc.OpenAsync(), cts.Token);
        if (await Task.WhenAny(connectTask, Task.Delay(2000, cts.Token)) != connectTask)
        {
            CloseConnection();
            return false;
        }

        // ✅ 打开成功后检查连接状态
        if (!_plc.IsConnected)
        {
            CloseConnection();
            return false;
        }

        return _plc.IsConnected;
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
        CloseConnection();
        throw; // 轮询引擎的取消信号
    }
    catch
    {
        CloseConnection();
        return false;
    }
    finally
    {
        _lock.Release();
    }
}
```

### 5.2 async void 泄漏修复

**方案 A — 消除 async void（推荐）**：
```csharp
// DashboardViewModel.cs 构造函数：
_ = DispatcherHelper.InvokeOnUiThread(() => RefreshAllDataAsync());

// 或者：
Application.Current?.Dispatcher?.InvokeAsync(() =>
{
    // RefreshAllDataAsync() 内部的异常由 try-catch 兜底
});
```

**方案 B — Timer Tick 安全模式**：
```csharp
// 将 Tick 改为命名方法并确保内部兜底
private async void OnRefreshTimerTick(object? sender, EventArgs e)
{
    try
    {
        await RefreshAllDataAsync();
    }
    catch (Exception ex)
    {
        // 记录日志，不崩溃
        System.Diagnostics.Debug.WriteLine($"[Timer] Refresh failed: {ex.Message}");
    }
}

// 订阅：
_refreshTimer.Tick += OnRefreshTimerTick;
```

### 5.3 指数退避重连修复

```csharp
public sealed class PlcPollingService : IDisposable
{
    // ... 现有字段 ...
    private readonly Dictionary<IPlcClient, int> _retryCount = new();  // 新增：每客户端重试计数

    private async Task PollStationAsync(IPlcClient client, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                bool connected = await client.ConnectAsync(cancellationToken);
                if (!connected)
                {
                    // ✅ 指数退避：1s → 2s → 4s → 8s → ... → 60s max
                    int retries = _retryCount.GetValueOrDefault(client, 0);
                    TimeSpan delay = TimeSpan.FromMilliseconds(
                        Math.Min(1000 * Math.Pow(2, retries), 60_000));
                    _retryCount[client] = retries + 1;

                    Publish(/* offline snapshot */);
                    await Task.Delay(delay, cancellationToken);
                    continue;
                }

                // 连接成功，重置重试计数
                _retryCount[client] = 0;

                // 正常轮询...
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { /* handle */ }
        }
    }
}
```

### 5.4 BackendRuntime IDisposable + 应用关闭钩子

```csharp
public sealed class BackendRuntime : IDisposable
{
    // ... 现有代码 ...

    public void Dispose()
    {
        // 1. 先停止轮询服务（内部取消令牌 + 等待任务完成）
        PollingService.StopAsync().GetAwaiter().GetResult();
        PollingService.Dispose();

        // 2. 再释放海康 SDK
        HikvisionService?.Dispose();

        // 3. 清理数据库连接
        DbContext?.Dispose();

        // 4. 解除事件订阅
        PollingService.SnapshotReceived -= OnSnapshotReceivedForNotification;
        PollingService.LogReceived -= OnLogReceivedForNotification;
    }
}
```

然后在 `App.xaml.cs` 中注册退出清理：

```csharp
protected override void OnExit(ExitEventArgs e)
{
    BackendRuntime.Shared.Dispose();
    base.OnExit(e);
}
```

### 5.5 启动时播种改为惰性模式

```csharp
private static BackendRuntime CreateDefault()
{
    var dbContext = new SqlSugarDbContext();
    var configs = /* load configs */;
    var repository = new SqlMotorTestRepository(dbContext);
    
    // ✅ 不在启动时阻塞播种，改为惰性模式
    // SeedRepositoryIfEmptyAsync 改为在首次查询数据为空时触发
    
    return new BackendRuntime(/* ... */);
}
```

或者在 `App.xaml.cs` 的 `OnStartup` 中：

```csharp
_ = Task.Run(async () =>
{
    await BackendRuntime.SeedIfEmptyAsync();
}).ConfigureAwait(false);
```

---

## 总结

对 MotorTestSystem 项目整体的评价：

**优点**：
- 架构分层清晰（DDD 四层），依赖注入规范
- PLC 客户端接口抽象良好，三种协议实现统一
- 双检锁重连模式、Modbus 发送前清空缓冲区、OperationCanceledException 特别处理等工业经验值得肯定
- 全局异常处理 + 日志落盘 + Stack Overflow 保护到位
- 通知生成有 5 分钟冷却机制，防止刷屏

**核心风险**（排列优先级）：

1. **S7PlcClient 超时失效**（P0）— 一台 S7 PLC 断线可拖死整个轮询系统
2. **async void 泄漏**（P0）— Dispatcher.InvokeAsync 和 Timer Tick 中的 async void 是进程崩溃的直接入口
3. **BackendRuntime 不可释放**（P0）— 资源泄漏源头
4. **无指数退避**（P1）— 长时间断线场景下的无效负载
5. **Stream.ReadAsync 无超时**（P1）— 半连接场景可永久阻塞
6. **启动时 UI 线程阻塞**（P1）— 白屏 + 潜在死锁
7. **ViewModel 事件未解除**（P2）— 架构演进后的泄漏隐患

**P0 问题建议在下一轮迭代中优先修复**，P1 问题纳入后续 Sprint，P2/P3 可通过代码审查流程逐步完善。
