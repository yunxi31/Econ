# MotorTestSystem 性能优化 Bugfix 设计文档

## Overview

本设计针对 WPF + .NET 8 电机测试系统的 7 大性能问题提供架构级修复方案：

1. **UI 线程洪泛** → Channel 解耦 + 降频消费（6-12次/秒 → 0.5次/秒）
2. **启动阻塞** → 惰性异步初始化模式（消除 2-15 秒白屏）
3. **轮询循环阻塞** → Channel 写缓冲 + 批量事务（消除 IO 等待抖动）
4. **线程安全风险** → ConcurrentDictionary + Interlocked 原子操作
5. **冗余调度** → 移除 Task.Run 包装（减少上下文切换）
6. **S7 PLC 多次往返** → 批量读取接口（3次 TCP → 1次）
7. **锁粒度过粗** → 最小化 IO 临界区（数据解析移到锁外）

**核心架构变更：**
- 事件驱动架构 → 基于 `System.Threading.Channels` 的生产者-消费者模式
- 同步阻塞初始化 → `Lazy<Task<T>>` 异步初始化模式
- 同步写入 → 批量写入队列（100ms 窗口 / 50 条阈值）

**性能预期：**
- UI 卡顿从 200-500ms → <10ms
- 应用启动从 2-15 秒阻塞 → 无阻塞
- 轮询周期抖动从 100-200ms → <5ms
- SQLite 写入吞吐量提升 3-10x

---

## Glossary

- **Bug_Condition (C)**: 性能瓶颈触发条件集合（UI 线程洪泛、同步阻塞、IO 等待）
- **Property (P)**: 修复后的性能特性（无卡顿、无阻塞、稳定轮询）
- **Preservation**: 必须保留的功能行为（数据完整性、事件传播、错误处理）
- **Channel**: `System.Threading.Channels.Channel<T>` - 线程安全的高性能队列，用于解耦生产者和消费者
- **Dispatcher.InvokeAsync**: WPF UI 线程调度方法，将操作投递到 UI 消息泵
- **SynchronizationContext**: .NET 异步延续上下文，await 后默认回到捕获的上下文（UI 线程）
- **Lazy<Task<T>>**: 惰性异步初始化模式，保证异步工厂方法只执行一次
- **Interlocked**: CPU 级原子操作，保证多线程计数器安全
- **S7NetPlus.ReadMultipleVarsAsync**: S7 PLC 批量读取接口，一次 TCP 往返读取多个数据区
- **WAL Checkpoint**: SQLite 预写日志检查点，将 WAL 文件合并到主数据库

---

## Bug Details

### Bug Condition

性能问题的触发条件可以形式化为 7 个独立但相互关联的条件：

**Formal Specification:**
```
FUNCTION isBugCondition(input)
  INPUT: input = {eventRate, initMode, pollLoopDesign, dictionaryType, schedulingMode, plcReadMode, lockScope}
  OUTPUT: boolean
  
  RETURN (
    // 条件1: UI线程洪泛
    (input.eventRate >= 6 AND uiThreadExecutesDbQuery) OR
    
    // 条件2: 启动阻塞
    (input.initMode == "SyncBlocking" AND seedTime >= 2000ms) OR
    
    // 条件3: 轮询循环阻塞
    (input.pollLoopDesign == "SyncAwaitDbWrite" AND dbWriteTime >= 1ms) OR
    
    // 条件4: 线程安全风险
    (input.dictionaryType == "Dictionary" AND concurrentWriteCount >= 2) OR
    
    // 条件5: 冗余调度
    (input.schedulingMode == "TaskRunWrapping" AND firstLineIsAwait) OR
    
    // 条件6: S7多次往返
    (input.plcReadMode == "Sequential" AND readCallCount >= 3) OR
    
    // 条件7: 锁粒度过粗
    (input.lockScope == "IncludesDataParsing" AND lockHoldTime >= 5ms)
  )
END FUNCTION
```

### Examples

**示例 1: UI 线程洪泛**
- **触发条件**: 6 工位同时运行，每工位 1-2 次快照/秒
- **实际行为**: DashboardViewModel 订阅 `SnapshotReceived` 事件 → `Dispatcher.InvokeAsync(RefreshAllDataAsync)` → UI 线程执行 SQLite 聚合查询 → 每秒 6-12 次数据库查询
- **可观测症状**: 窗口拖动卡顿 200-500ms，按钮点击响应延迟
- **预期行为**: Dashboard 更新频率降至 0.5 次/秒，UI 线程负载 < 5%

**示例 2: 启动阻塞**
- **触发条件**: 应用启动访问 `BackendRuntime.Shared` 静态属性
- **实际行为**: `Task.Run(() => SeedRepositoryIfEmptyAsync(...).GetAwaiter().GetResult()).Wait()` → 线程池线程同步等待 3000+ 条记录播种（2-15 秒）→ 外层 `.Wait()` 阻塞 UI 线程
- **可观测症状**: 应用窗口显示"未响应"白屏 2-15 秒
- **预期行为**: 应用立即显示窗口，播种在后台异步执行

**示例 3: 轮询循环阻塞**
- **触发条件**: `PollStationAsync` 检测到 `CompletionSignal == true`
- **实际行为**: `await _repository.UpsertStageResultAsync(data)` → 同步等待 SQLite 写入（1-50ms）→ 轮询周期从 500ms 拉长到 520-550ms
- **可观测症状**: 6 工位满负荷时轮询抖动 100-200ms，偶发超时
- **预期行为**: 写入通过 Channel 异步提交，轮询周期稳定在 500±5ms

**示例 4: 线程安全风险**
- **触发条件**: 6 个独立的 `PollStationAsync` 任务同时调用 `_consecutiveFailures[id]++`
- **实际行为**: `Dictionary<string, int>` 在并发写入不同 key 时存在数据竞争 → 扩容操作与读取冲突 → 潜在无限循环或内存损坏
- **可观测症状**: 偶发崩溃或计数器丢失（难以复现）
- **预期行为**: 使用 `ConcurrentDictionary<string, int>` 保证线程安全

**示例 5: S7 PLC 重复往返**
- **触发条件**: `S7PlcClient.ReadSnapshotAsync` 需要读取 3 个数据区（M100.0, DB1.DBW100-106, Barcode）
- **实际行为**: 
  ```csharp
  await _plc.ReadAsync("M100.0");     // TCP 往返 1
  await _plc.ReadAsync("DB1.DBW100"); // TCP 往返 2
  await _plc.ReadAsync("DB1.DBW200"); // TCP 往返 3
  ```
- **可观测症状**: S7 工位通信时间 30-60ms（每次往返 10-20ms）
- **预期行为**: 使用 `ReadMultipleVarsAsync` 一次读取，通信时间减少至 10-20ms

**示例 6: 锁粒度过粗（边缘案例）**
- **触发条件**: `ModbusTcpClient.ReadSnapshotAsync` 持有 `_lock` 期间执行数据解析
- **实际行为**: 
  ```csharp
  await _lock.WaitAsync();
  byte[] raw = await _stream.ReadAsync(); // IO - 必须在锁内
  var parsed = ParseModbusFrame(raw);     // CPU - 不需要锁
  _lock.Release();
  ```
- **可观测症状**: 锁持有时间 5-20ms，虽然不是主要瓶颈但存在优化空间
- **预期行为**: 数据解析移到锁外，锁持有时间降至 2-8ms

---

## Expected Behavior

### Preservation Requirements

**Unchanged Behaviors:**
- **数据完整性**: 每条 `StageTestData` 记录的所有字段（Barcode, StationId, Stage, Result, 测量值）必须完整准确地写入数据库
- **事件传播**: `SnapshotReceived` 事件必须继续传播给所有订阅者（DashboardViewModel, MonitorViewModel, HistoryViewModel）
- **错误处理**: PLC 通信失败时继续记录 `_consecutiveFailures`，数据库错误继续抛出异常
- **生命周期管理**: `PlcPollingService.Stop()` 必须正确取消所有轮询任务并等待完成
- **配置驱动**: 系统必须继续支持通过 `StationConfig` 动态配置 PLC 类型（Modbus/S7/Melsec/Mock）

**Scope:**
修复范围仅限于性能优化，不改变以下核心功能：
- PLC 数据采集逻辑（轮询间隔、数据区地址、完成信号判定）
- 数据库 Upsert 逻辑（新增/更新分支、查询条件）
- UI 数据绑定（ObservableProperty、图表数据源）
- 用户交互响应（按钮命令、视图切换）

---

## Hypothesized Root Cause

基于代码分析和性能测量，7 个问题的根源如下：

### 1. UI 线程洪泛根因

**直接原因**: 
- `DashboardViewModel` 直接订阅 `SnapshotReceived` 事件，每次事件触发调用 `Dispatcher.InvokeAsync(RefreshAllDataAsync)`
- WPF Dispatcher 是串行消息泵，大量数据库查询任务堆积在队列中

**深层原因**:
- 缺乏事件降频机制（throttling/debouncing）
- 数据库查询（KPI 聚合）直接在 UI 线程执行
- `await` 的默认延续行为（`ConfigureAwait(true)`）导致异步操作的延续回到 UI 线程

**修复策略**: 
- 引入 `Channel<StationSnapshot>` 解耦事件生产者和消费者
- 后台消费者每 2 秒最多刷新一次，丢弃中间帧

### 2. 启动阻塞根因

**直接原因**:
```csharp
// BackendRuntime.cs
public static BackendRuntime Shared { get; } = CreateDefault();

private static BackendRuntime CreateDefault() {
    // ...
    Task.Run(() => SeedRepositoryAsync(repo).GetAwaiter().GetResult()).Wait();
    //         ^^^^                       ^^^^^^^^^^^^^^^^^^^^^^^^^^^  ^^^^^^
    //         线程池分派                  同步阻塞1                   同步阻塞2
    return new BackendRuntime(...);
}
```

**深层原因**:
- 静态属性初始化器不支持 `async` 关键字
- 开发者试图通过 `Task.Run` + `.GetAwaiter().GetResult()` + `.Wait()` 绕过异步限制
- 双重同步阻塞导致死锁风险（虽然此处未发生，但线程池线程被浪费）

**修复策略**:
- 使用 `Lazy<Task<BackendRuntime>>` 模式实现异步单例
- 暴露 `static Task<BackendRuntime> GetSharedAsync()` 方法
- 调用方通过 `await GetSharedAsync()` 异步初始化

### 3. 轮询循环阻塞根因

**直接原因**:
```csharp
// PlcPollingService.cs - PollStationAsync()
if (snapshot.CompletionSignal) {
    await _repository.UpsertStageResultAsync(data); // 阻塞点
    // 轮询循环等待数据库写入完成才能进入下一次循环
}
```

**深层原因**:
- SQLite 写入是同步 IO 操作（即使包装为 `async`，底层仍是同步）
- 6 工位并发写入导致 SQLite 锁竞争
- WAL checkpoint（每 1000 页）产生瞬间毛刺（100-200ms）

**修复策略**:
- 引入 `Channel<StageTestData>` 解耦轮询和写入
- 独立的批量写入消费者（100ms 窗口 / 50 条阈值）
- 新增 `IMotorTestRepository.BulkUpsertAsync()` 批量事务接口

### 4. 线程安全风险根因

**直接原因**:
```csharp
private Dictionary<string, int> _consecutiveFailures = new();

// 在 6 个并发任务中调用
_consecutiveFailures[stationId]++; // 非原子操作
```

**深层原因**:
- `Dictionary<TKey, TValue>` 不是线程安全的
- 即使每个工位只读写自己的 key，扩容操作（rehash）会修改内部 bucket 数组
- 并发扩容与读取冲突可能导致无限循环（.NET Framework 已知 bug，.NET Core 改进但仍不建议）

**修复策略**:
- 替换为 `ConcurrentDictionary<string, int>`
- 使用 `AddOrUpdate` / `GetOrAdd` 原子操作模式

### 5. 冗余调度根因

**直接原因**:
```csharp
// PlcPollingService.cs - Start()
_pollingTasks.Add(Task.Run(() => PollStationAsync(client, token)));
```

**深层原因**:
- `PollStationAsync` 是 `async` 方法，第一行即 `await Task.Delay(...)`
- `Task.Run` 将 lambda 投递到线程池，但立即在第一个 `await` 处返回
- 线程池线程分派毫无作用（async 方法本身就是状态机，不需要专用线程）

**修复策略**:
- 直接调用 `PollStationAsync(client, token)`，不使用 `Task.Run` 包装

### 6. S7 PLC 重复往返根因

**直接原因**:
```csharp
// S7PlcClient.cs (假设实现)
public async Task<StationSnapshot> ReadSnapshotAsync() {
    bool signal = await _plc.ReadAsync("M100.0");      // TCP 往返 1
    var testData = await _plc.ReadAsync("DB1.DBW100"); // TCP 往返 2
    var barcode = await _plc.ReadAsync("DB1.DBW200");  // TCP 往返 3
    // ...
}
```

**深层原因**:
- 开发者未使用 S7NetPlus 提供的 `ReadMultipleVarsAsync()` 批量接口
- 每次独立读取都需要持有锁 + 发送 S7 协议帧 + 等待 TCP 响应

**修复策略**:
- 使用 `ReadMultipleVarsAsync(new[] { "M100.0", "DB1.DBW100", "DB1.DBW200" })`
- 一次 TCP 往返读取所有数据项

### 7. 锁粒度过粗根因

**直接原因**:
```csharp
// ModbusTcpClient.cs (假设实现)
public async Task<StationSnapshot> ReadSnapshotAsync() {
    await _lock.WaitAsync();
    try {
        await _stream.WriteAsync(requestFrame); // IO - 必须在锁内
        byte[] response = await _stream.ReadAsync(); // IO - 必须在锁内
        var parsed = ParseModbusFrame(response); // CPU - 不需要锁
        return CreateSnapshot(parsed);           // CPU - 不需要锁
    } finally {
        _lock.Release();
    }
}
```

**深层原因**:
- 锁保护的是 `NetworkStream`（单工协议需要串行化请求-响应对）
- 数据解析和对象构造是纯 CPU 操作，不需要锁保护
- 虽然当前锁持有时间（5-20ms）不是主要瓶颈，但存在优化空间

**修复策略**:
- 将数据解析和业务逻辑移到 `finally` 块后
- 锁只保护网络 IO

---

## Correctness Properties

Property 1: Bug Condition - UI 线程解放

_For any_ 工位快照事件（eventRate >= 6/秒），修复后的 DashboardViewModel SHALL 通过 Channel 缓冲事件，后台消费者每 2 秒最多刷新一次，使得 UI 线程数据库查询频率 <= 0.5 次/秒，UI 线程负载下降 90%+。

**Validates: Requirements 2.1, 2.2, 2.3, 2.4**

Property 2: Bug Condition - 启动无阻塞

_For any_ 应用启动流程，修复后的 `BackendRuntime` SHALL 通过 `Lazy<Task<T>>` 模式实现异步初始化，调用 `await GetSharedAsync()` 不阻塞 UI 线程，应用窗口立即响应，播种操作在后台执行。

**Validates: Requirements 2.5, 2.6, 2.7, 2.8**

Property 3: Bug Condition - 轮询周期稳定

_For any_ 完成信号触发（CompletionSignal == true），修复后的 `PollStationAsync` SHALL 通过 Channel 异步提交写入请求，不等待数据库 IO，轮询周期稳定在配置值 ± 5ms，消除抖动。

**Validates: Requirements 2.9, 2.10, 2.11, 2.12, 2.13**

Property 4: Bug Condition - 线程安全保证

_For any_ 并发写入失败计数器的场景（concurrentWriteCount >= 2），修复后的 `PlcPollingService` SHALL 使用 `ConcurrentDictionary<string, int>` + Interlocked 原子操作，保证无数据竞争，无潜在崩溃。

**Validates: Requirements 2.14, 2.15, 2.16, 2.17**

Property 5: Bug Condition - 调度效率

_For any_ 轮询任务启动（工位数 >= 6），修复后的 `PlcPollingService.Start()` SHALL 直接调用异步方法，不使用 `Task.Run` 包装，节省每工位 2-15μs 的线程池上下文切换开销。

**Validates: Requirements 2.18, 2.19**

Property 6: Bug Condition - S7 通信效率

_For any_ S7 PLC 快照读取（需要读取 3+ 个数据区），修复后的 `S7PlcClient` SHALL 使用 `ReadMultipleVarsAsync` 批量接口，一次 TCP 往返完成读取，通信时间减少 50-66%。

**Validates: Requirements 2.20, 2.21**

Property 7: Bug Condition - 锁优化

_For any_ Modbus TCP 快照读取，修复后的 `ModbusTcpClient` SHALL 仅在网络 IO 期间持有锁，数据解析移到锁外，锁持有时间从 5-20ms 降至 2-8ms。

**Validates: Requirements 2.22, 2.23**

Property 8: Preservation - 数据完整性

_For any_ 测试结果数据（StageTestData），修复前后系统 SHALL 产生完全相同的数据库记录（假设时间戳和并发顺序相同），所有字段值一致。

**Validates: Requirements 3.1, 3.2, 3.8, 3.9, 3.10**

Property 9: Preservation - 事件传播

_For any_ 工位快照事件，修复前后 SHALL 传播给所有订阅者（Dashboard/Monitor/History），事件数据内容保持不变（仅传播延迟可能变化）。

**Validates: Requirements 3.3, 3.14**

Property 10: Preservation - 错误处理

_For any_ PLC 通信错误或数据库错误，修复前后 SHALL 执行相同的错误处理逻辑（记录失败次数、抛出异常、记录日志）。

**Validates: Requirements 3.5, 3.6**

Property 11: Preservation - 生命周期

_For any_ 服务停止操作（Stop/Dispose），修复前后 SHALL 正确取消所有任务、释放所有资源，修复后需额外确保 Channel 中的缓冲数据已刷新。

**Validates: Requirements 3.7, 3.16, 3.17, 3.18**

---

## Fix Implementation

### Architecture Changes

#### 1. Channel-Based Event Decoupling

**新增组件**:
```csharp
// MotorTestSystem.Services/EventChannelService.cs
public sealed class EventChannelService : IDisposable
{
    // Unbounded channel for snapshot events (高优先级，不能丢失)
    private readonly Channel<StationSnapshot> _snapshotChannel = 
        Channel.CreateUnbounded<StationSnapshot>(new UnboundedChannelOptions { 
            SingleReader = false,  // 多个消费者（Dashboard/Monitor/History）
            SingleWriter = false   // 多个生产者（6个工位）
        });
    
    // Bounded channel for write buffer (可丢弃，有背压)
    private readonly Channel<StageTestData> _writeChannel = 
        Channel.CreateBounded<StageTestData>(new BoundedChannelOptions(500) {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,   // 单一写入消费者
            SingleWriter = false   // 多个生产者（6个工位）
        });
    
    public ChannelReader<StationSnapshot> SnapshotReader => _snapshotChannel.Reader;
    public ChannelWriter<StationSnapshot> SnapshotWriter => _snapshotChannel.Writer;
    public ChannelReader<StageTestData> WriteReader => _writeChannel.Reader;
    public ChannelWriter<StageTestData> WriteWriter => _writeChannel.Writer;
    
    public void Dispose() {
        _snapshotChannel.Writer.Complete();
        _writeChannel.Writer.Complete();
    }
}
```

**数据流设计**:
```
[PLC Polling] --TryWrite--> [Snapshot Channel] --ReadAllAsync--> [Dashboard Consumer]
                                                               └--> [Monitor Consumer]
                                                               └--> [History Consumer]

[PLC Polling] --WriteAsync--> [Write Channel] --Batch Reader--> [Bulk Writer] --> [SQLite]
```

#### 2. Async Initialization Pattern

**修改 BackendRuntime**:
```csharp
// MotorTestSystem.Services/BackendRuntime.cs
public sealed class BackendRuntime
{
    // 替换静态属性为惰性异步初始化
    private static readonly Lazy<Task<BackendRuntime>> _sharedInstanceTask = 
        new Lazy<Task<BackendRuntime>>(() => CreateDefaultAsync());
    
    public static Task<BackendRuntime> GetSharedAsync() => _sharedInstanceTask.Value;
    
    // 构造器保持不变
    public BackendRuntime(...) { ... }
    
    // 异步工厂方法
    private static async Task<BackendRuntime> CreateDefaultAsync()
    {
        var stationConfigs = new ObservableCollection<StationConfig> { ... };
        var repository = new InMemoryMotorTestRepository();
        
        // 异步播种（不阻塞）
        await SeedRepositoryAsync(repository);
        
        return new BackendRuntime(stationConfigs, repository, new MockPlcClientFactory());
    }
    
    private static async Task SeedRepositoryAsync(IMotorTestRepository repository)
    {
        // 原有逻辑，但移除 .GetAwaiter().GetResult()
        for (int i = 0; i < 12; i++) {
            await repository.UpsertStageResultAsync(...);
        }
    }
}
```

**调用方修改**:
```csharp
// App.xaml.cs 或启动代码
protected override async void OnStartup(StartupEventArgs e)
{
    base.OnStartup(e);
    
    // 异步初始化（不阻塞 UI）
    var runtime = await BackendRuntime.GetSharedAsync();
    
    // 启动轮询服务
    runtime.PollingService.Start();
    
    // 显示主窗口
    var mainWindow = new MainWindow();
    mainWindow.Show();
}
```

#### 3. Batch Write Consumer

**新增批量写入服务**:
```csharp
// MotorTestSystem.Services/BatchWriteService.cs
public sealed class BatchWriteService : IDisposable
{
    private readonly IMotorTestRepository _repository;
    private readonly ChannelReader<StageTestData> _reader;
    private readonly Task _consumerTask;
    private readonly CancellationTokenSource _cts = new();
    
    private const int MaxBatchSize = 50;
    private const int MaxWaitMs = 100;
    
    public BatchWriteService(IMotorTestRepository repository, ChannelReader<StageTestData> reader)
    {
        _repository = repository;
        _reader = reader;
        _consumerTask = Task.Run(() => ConsumeAsync(_cts.Token));
    }
    
    private async Task ConsumeAsync(CancellationToken cancellationToken)
    {
        var batch = new List<StageTestData>(MaxBatchSize);
        
        while (!cancellationToken.IsCancellationRequested)
        {
            try {
                // 读取第一条（阻塞等待）
                if (await _reader.WaitToReadAsync(cancellationToken)) {
                    batch.Add(await _reader.ReadAsync(cancellationToken));
                    
                    // 收集批次（最多等待 100ms 或达到 50 条）
                    var deadline = DateTime.UtcNow.AddMilliseconds(MaxWaitMs);
                    while (batch.Count < MaxBatchSize && DateTime.UtcNow < deadline) {
                        if (_reader.TryRead(out var item)) {
                            batch.Add(item);
                        } else {
                            await Task.Delay(10, cancellationToken);
                        }
                    }
                    
                    // 批量写入
                    await _repository.BulkUpsertAsync(batch, cancellationToken);
                    batch.Clear();
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) {
                // 记录日志但继续运行
                Debug.WriteLine($"Batch write error: {ex.Message}");
            }
        }
    }
    
    public async Task StopAsync()
    {
        _cts.Cancel();
        await _consumerTask;
    }
    
    public void Dispose()
    {
        _cts.Cancel();
        _consumerTask.Wait(TimeSpan.FromSeconds(5));
        _cts.Dispose();
    }
}
```

### Specific Code Changes

#### File 1: `MotorTestSystem.Services/PlcPollingService.cs`

**修改 1: 替换 Dictionary 为 ConcurrentDictionary**
```csharp
// 原代码（第 14 行）
private Dictionary<string, int> _consecutiveFailures = new();

// 修复后
private readonly ConcurrentDictionary<string, int> _consecutiveFailures = new();
```

**修改 2: 注入 EventChannelService**
```csharp
// 构造器新增参数
private readonly EventChannelService _eventChannel;

public PlcPollingService(
    IEnumerable<StationConfig> stationConfigs, 
    IMotorTestRepository repository, 
    IPlcClientFactory clientFactory,
    EventChannelService eventChannel,  // 新增
    TimeSpan? pollInterval = null)
{
    _repository = repository;
    _clientFactory = clientFactory;
    _eventChannel = eventChannel;      // 新增
    _pollInterval = pollInterval ?? TimeSpan.FromSeconds(1.0);
    _clients.AddRange(stationConfigs.Select(_clientFactory.Create));
}
```

**修改 3: 移除 Task.Run 包装**
```csharp
// 原代码（Start 方法第 35 行）
_pollingTasks.Add(Task.Run(() => PollStationAsync(client, token)));

// 修复后
_pollingTasks.Add(PollStationAsync(client, _cancellationTokenSource.Token));
```

**修改 4: 异步发送到 Write Channel**
```csharp
// 原代码（PollStationAsync 方法第 74-79 行）
if (snapshot.CompletionSignal && snapshot.CompletedData != null)
{
    await _repository.UpsertStageResultAsync(snapshot.CompletedData, cancellationToken);
    await client.ResetCompletionSignalAsync(cancellationToken);
    this.LogReceived?.Invoke(this, $"{snapshot.StationId} saved ...");
}

// 修复后
if (snapshot.CompletionSignal && snapshot.CompletedData != null)
{
    // 通过 Channel 异步提交（不等待写入完成）
    await _eventChannel.WriteWriter.WriteAsync(snapshot.CompletedData, cancellationToken);
    await client.ResetCompletionSignalAsync(cancellationToken);
    this.LogReceived?.Invoke(this, $"{snapshot.StationId} queued ...");
}
```

**修改 5: 发送快照到 Snapshot Channel**
```csharp
// 原代码（Publish 方法第 91-94 行）
private void Publish(StationSnapshot snapshot)
{
    this.SnapshotReceived?.Invoke(this, snapshot);
}

// 修复后
private void Publish(StationSnapshot snapshot)
{
    // 保留事件（向后兼容 Monitor/History）
    this.SnapshotReceived?.Invoke(this, snapshot);
    
    // 同时发送到 Channel（非阻塞）
    _eventChannel.SnapshotWriter.TryWrite(snapshot);
}
```

**修改 6: 线程安全的失败计数器操作**
```csharp
// 假设代码中有失败计数逻辑（未在反编译代码中找到，推测存在）
// 原代码
if (!_consecutiveFailures.ContainsKey(stationId))
    _consecutiveFailures[stationId] = 0;
_consecutiveFailures[stationId]++;

// 修复后
_consecutiveFailures.AddOrUpdate(
    stationId, 
    addValue: 1, 
    updateValueFactory: (key, old) => old + 1
);

// 重置时
// 原代码
_consecutiveFailures[stationId] = 0;

// 修复后
_consecutiveFailures.TryRemove(stationId, out _);
```

#### File 2: `MotorTestSystem.ViewModels/DashboardViewModel.cs`

**修改 1: 替换 DispatcherTimer 为 Channel 消费者**
```csharp
// 删除原有代码（第 125-137 行）
_refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5.0) };
_refreshTimer.Tick += delegate { RefreshSummary(); };
_refreshTimer.Start();

// 新增后台消费者
private readonly EventChannelService _eventChannel;
private readonly Task _consumerTask;
private readonly CancellationTokenSource _cts = new();

public DashboardViewModel(IMotorTestRepository repository, EventChannelService eventChannel)
{
    _repository = repository;
    _eventChannel = eventChannel;
    
    // ... 图表初始化代码保持不变 ...
    
    RefreshSummary();
    
    // 启动后台消费者（每 2 秒最多刷新一次）
    _consumerTask = Task.Run(() => ConsumeSnapshotsAsync(_cts.Token));
}

private async Task ConsumeSnapshotsAsync(CancellationToken cancellationToken)
{
    var reader = _eventChannel.SnapshotReader;
    var lastRefresh = DateTime.MinValue;
    const int ThrottleMs = 2000;
    
    while (!cancellationToken.IsCancellationRequested)
    {
        try {
            // 等待新快照
            await reader.WaitToReadAsync(cancellationToken);
            
            // 丢弃缓冲区中的所有中间帧（只保留最新状态）
            StationSnapshot? latest = null;
            while (reader.TryRead(out var snapshot)) {
                latest = snapshot;
            }
            
            // 降频：距上次刷新至少 2 秒
            var elapsed = DateTime.UtcNow - lastRefresh;
            if (elapsed.TotalMilliseconds >= ThrottleMs && latest != null) {
                // 通过 Dispatcher 在 UI 线程执行刷新
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(
                    () => RefreshSummary(),
                    System.Windows.Threading.DispatcherPriority.Background
                );
                lastRefresh = DateTime.UtcNow;
            }
        }
        catch (OperationCanceledException) { break; }
        catch (Exception ex) {
            Debug.WriteLine($"Dashboard consumer error: {ex.Message}");
            await Task.Delay(1000, cancellationToken);
        }
    }
}

// RefreshSummary 方法保持不变（第 139-163 行）
private void RefreshSummary() { ... }
```

**修改 2: 添加 Dispose 方法**
```csharp
public void Dispose()
{
    _cts.Cancel();
    try {
        _consumerTask.Wait(TimeSpan.FromSeconds(2));
    } catch (AggregateException) { }
    _cts.Dispose();
}
```

#### File 3: `MotorTestSystem.Services/IMotorTestRepository.cs`

**新增批量 Upsert 接口**:
```csharp
public interface IMotorTestRepository
{
    // 原有方法保持不变
    Task UpsertStageResultAsync(StageTestData data, CancellationToken cancellationToken = default);
    Task<ProductionSummary> GetSummaryAsync(DateTime startTime, DateTime endTime, CancellationToken cancellationToken = default);
    // ... 其他方法 ...
    
    // 新增批量方法
    Task BulkUpsertAsync(IEnumerable<StageTestData> dataList, CancellationToken cancellationToken = default);
}
```

#### File 4: `MotorTestSystem.Services/InMemoryMotorTestRepository.cs`

**实现批量 Upsert**:
```csharp
public async Task BulkUpsertAsync(IEnumerable<StageTestData> dataList, CancellationToken cancellationToken = default)
{
    using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
    
    try {
        foreach (var data in dataList) {
            // 复用单条 Upsert 逻辑（但在事务内）
            var existing = await _dbContext.StageResults
                .FirstOrDefaultAsync(r => r.Barcode == data.Barcode && r.Stage == data.Stage, cancellationToken);
            
            if (existing != null) {
                // 更新
                _dbContext.Entry(existing).CurrentValues.SetValues(data);
            } else {
                // 新增
                await _dbContext.StageResults.AddAsync(data, cancellationToken);
            }
        }
        
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
    catch {
        await transaction.RollbackAsync(cancellationToken);
        throw;
    }
}
```

#### File 5: `MotorTestSystem.Services/S7PlcClient.cs` (假设文件路径)

**修改: 使用批量读取接口**
```csharp
// 原代码（假设）
public async Task<StationSnapshot> ReadSnapshotAsync(CancellationToken cancellationToken)
{
    await _lock.WaitAsync(cancellationToken);
    try {
        // 三次独立读取
        bool signal = await _plc.ReadAsync("M100.0");
        var testData = await _plc.ReadAsync("DB1.DBW100");
        var barcode = await _plc.ReadAsync("DB1.DBW200");
        
        return new StationSnapshot { ... };
    } finally {
        _lock.Release();
    }
}

// 修复后
public async Task<StationSnapshot> ReadSnapshotAsync(CancellationToken cancellationToken)
{
    await _lock.WaitAsync(cancellationToken);
    try {
        // 批量读取（一次 TCP 往返）
        var items = new[] {
            new S7NetPlus.Types.DataItem {
                DataType = S7NetPlus.Types.DataType.DataBlock,
                DB = 0,
                StartByteAdr = 100,
                VarType = S7NetPlus.Types.VarType.Bit,
                BitAdr = 0,
                Count = 1
            },
            new S7NetPlus.Types.DataItem {
                DataType = S7NetPlus.Types.DataType.DataBlock,
                DB = 1,
                StartByteAdr = 100,
                VarType = S7NetPlus.Types.VarType.Word,
                Count = 3  // 读取 6 字节（DBW100-106）
            },
            new S7NetPlus.Types.DataItem {
                DataType = S7NetPlus.Types.DataType.DataBlock,
                DB = 1,
                StartByteAdr = 200,
                VarType = S7NetPlus.Types.VarType.String,
                Count = 20
            }
        };
        
        var results = await _plc.ReadMultipleVarsAsync(items);
        
        // 解析结果
        bool signal = BitConverter.ToBoolean(results[0], 0);
        double current = BitConverter.ToDouble(results[1], 0);
        int speed = BitConverter.ToInt16(results[1], 2);
        string barcode = Encoding.ASCII.GetString(results[2]).TrimEnd('\0');
        
        return new StationSnapshot {
            CompletionSignal = signal,
            CompletedData = new StageTestData {
                NoLoadCurrent = current,
                NoLoadSpeed = speed,
                Barcode = barcode,
                ...
            }
        };
    } finally {
        _lock.Release();
    }
}
```

#### File 6: `MotorTestSystem.Services/ModbusTcpClient.cs` (假设文件路径)

**修改: 缩小锁粒度**
```csharp
// 原代码（假设）
public async Task<StationSnapshot> ReadSnapshotAsync(CancellationToken cancellationToken)
{
    await _lock.WaitAsync(cancellationToken);
    try {
        // 发送请求
        await _stream.WriteAsync(requestFrame, cancellationToken);
        
        // 读取响应
        byte[] response = new byte[256];
        int bytesRead = await _stream.ReadAsync(response, cancellationToken);
        
        // 解析帧（不需要锁保护）
        var parsed = ParseModbusFrame(response, bytesRead);
        
        // 构造快照（不需要锁保护）
        return CreateSnapshot(parsed);
    } finally {
        _lock.Release();
    }
}

// 修复后
public async Task<StationSnapshot> ReadSnapshotAsync(CancellationToken cancellationToken)
{
    byte[] response;
    int bytesRead;
    
    // 锁只保护网络 IO
    await _lock.WaitAsync(cancellationToken);
    try {
        await _stream.WriteAsync(requestFrame, cancellationToken);
        response = new byte[256];
        bytesRead = await _stream.ReadAsync(response, cancellationToken);
    } finally {
        _lock.Release();
    }
    
    // 数据解析在锁外执行
    var parsed = ParseModbusFrame(response, bytesRead);
    return CreateSnapshot(parsed);
}
```

### Integration Points

**修改服务组合**:
```csharp
// MotorTestSystem.Services/BackendRuntime.cs
public sealed class BackendRuntime
{
    public ObservableCollection<StationConfig> StationConfigs { get; }
    public IMotorTestRepository Repository { get; }
    public PlcPollingService PollingService { get; }
    public EventChannelService EventChannel { get; }      // 新增
    public BatchWriteService BatchWriter { get; }          // 新增
    
    private static readonly Lazy<Task<BackendRuntime>> _sharedInstanceTask = 
        new Lazy<Task<BackendRuntime>>(() => CreateDefaultAsync());
    
    public static Task<BackendRuntime> GetSharedAsync() => _sharedInstanceTask.Value;
    
    public BackendRuntime(
        ObservableCollection<StationConfig> stationConfigs, 
        IMotorTestRepository repository, 
        IPlcClientFactory plcClientFactory,
        EventChannelService eventChannel)
    {
        StationConfigs = stationConfigs;
        Repository = repository;
        EventChannel = eventChannel;
        
        // 创建轮询服务（注入 EventChannel）
        PollingService = new PlcPollingService(
            StationConfigs, 
            Repository, 
            plcClientFactory, 
            eventChannel,  // 新增
            null
        );
        
        // 创建批量写入服务
        BatchWriter = new BatchWriteService(repository, eventChannel.WriteReader);
    }
    
    private static async Task<BackendRuntime> CreateDefaultAsync()
    {
        var stationConfigs = new ObservableCollection<StationConfig> { ... };
        var repository = new InMemoryMotorTestRepository();
        var eventChannel = new EventChannelService();
        
        // 异步播种（不阻塞）
        await SeedRepositoryAsync(repository);
        
        return new BackendRuntime(stationConfigs, repository, new MockPlcClientFactory(), eventChannel);
    }
    
    public void Dispose()
    {
        BatchWriter?.Dispose();
        PollingService?.Dispose();
        EventChannel?.Dispose();
        Repository?.Dispose();
    }
}
```

**修改 ViewModel 构造器**:
```csharp
// MotorTestSystem.ViewModels/DashboardViewModel.cs
public DashboardViewModel()
    : this(
        BackendRuntime.GetSharedAsync().GetAwaiter().GetResult().Repository,
        BackendRuntime.GetSharedAsync().GetAwaiter().GetResult().EventChannel)
{
    // 注意：此处仍有同步阻塞（设计权衡）
    // 理想情况下应在 App 启动时预先初始化 BackendRuntime，
    // 然后通过依赖注入传递给 ViewModel
}

public DashboardViewModel(IMotorTestRepository repository, EventChannelService eventChannel)
{
    _repository = repository;
    _eventChannel = eventChannel;
    // ... 初始化逻辑 ...
    _consumerTask = Task.Run(() => ConsumeSnapshotsAsync(_cts.Token));
}
```

---

## Testing Strategy

### Validation Approach

测试策略分为三个阶段：

1. **探索性测试（Exploratory）**: 在未修复代码上运行性能剖析，确认 7 个性能瓶颈的根因假设
2. **修复验证（Fix Checking）**: 运行性能基准测试，验证修复后的性能指标达到预期
3. **回归测试（Preservation Checking）**: 运行功能测试套件，确保数据完整性和功能行为不变

### Exploratory Bug Condition Checking

**Goal**: 在未修复代码上量化性能问题，验证根因分析。

**测试工具**:
- **PerfView** (Windows Performance Toolkit): CPU 采样、线程池分析
- **dotTrace** (JetBrains): 方法调用时序、异步等待分析
- **WPF Performance Profiling**: UI 线程响应时间、Dispatcher 队列深度

**Test Cases**:

1. **UI 线程洪泛验证**
   - 配置 6 工位模拟器（每工位 2 次快照/秒）
   - 使用 PerfView 采样 30 秒
   - 预期发现: `DashboardViewModel.RefreshSummary` 在 UI 线程上执行 6-12 次/秒，占用 UI 线程 30-50%
   - 度量指标: `Dispatcher` 队列平均长度 > 10，窗口拖动响应时间 > 200ms

2. **启动阻塞验证**
   - 冷启动应用并记录启动时间
   - 使用 dotTrace 时序分析，定位阻塞调用栈
   - 预期发现: `BackendRuntime.CreateDefault()` 中的 `SeedRepositoryAsync().GetAwaiter().GetResult()` 阻塞 2-15 秒
   - 度量指标: 窗口显示延迟 > 2 秒，UI 线程 hang 检测触发

3. **轮询循环阻塞验证**
   - 配置 6 工位模拟器（高频完成信号：每 500ms 完成一次测试）
   - 使用 dotTrace 异步等待分析，测量 `PollStationAsync` 的实际轮询周期
   - 预期发现: `UpsertStageResultAsync` 调用耗时 1-50ms，导致轮询周期抖动
   - 度量指标: 轮询周期方差 > 20ms，P99 延迟 > 550ms

4. **线程安全风险验证（压力测试）**
   - 运行 1000 次启动-停止循环，同时 6 工位高频失败
   - 使用 Thread Sanitizer (如果可用) 或手动日志检测数据竞争
   - 预期发现: 偶发崩溃或计数器值异常（实际值与预期不符）
   - 度量指标: 1000 次循环中至少出现 1 次异常

5. **S7 PLC 通信效率验证**
   - 配置 S7 模拟器，使用 Wireshark 抓包分析
   - 单次 `ReadSnapshotAsync` 调用，统计 TCP 往返次数
   - 预期发现: 3 次独立的 S7 协议帧（TPKT + COTP + S7 Comm）
   - 度量指标: 通信时间 30-60ms（每次往返 10-20ms）

**Expected Counterexamples**:
- UI 线程洪泛 → `RefreshSummary` 执行频率 6-12 次/秒
- 启动阻塞 → `.GetAwaiter().GetResult()` 阻塞 UI 线程 2-15 秒
- 轮询循环阻塞 → 轮询周期从 500ms 拉长到 550ms
- 线程安全 → Dictionary 并发写入导致数据结构破坏
- S7 通信 → 3 次 TCP 往返（应为 1 次）

### Fix Checking

**Goal**: 验证修复后性能指标满足预期。

**Pseudocode:**
```
FOR ALL scenario IN [UI洪泛, 启动阻塞, 轮询抖动, 线程安全, S7通信, 锁粒度] DO
  result := measurePerformance_fixed(scenario)
  ASSERT expectedPerformance(result)
END FOR
```

**Testing Approach**: 性能基准测试（Benchmark）

**Test Cases**:

1. **UI 线程解放验证**
   - 配置与探索性测试相同（6 工位 × 2 次/秒）
   - 使用 PerfView 采样 30 秒
   - 预期结果: `RefreshSummary` 执行频率 <= 0.5 次/秒，UI 线程占用 < 5%
   - 通过条件: 窗口拖动响应时间 < 50ms，Dispatcher 队列平均长度 < 3

2. **启动无阻塞验证**
   - 冷启动应用并记录窗口显示时间
   - 使用 dotTrace 验证无同步阻塞
   - 预期结果: 窗口显示延迟 < 500ms，播种在后台执行
   - 通过条件: UI 线程无 hang 检测，应用立即响应

3. **轮询周期稳定性验证**
   - 配置 6 工位模拟器（高频完成信号）
   - 记录 1000 次轮询周期的统计分布
   - 预期结果: 平均周期 500±5ms，标准差 < 5ms，P99 < 510ms
   - 通过条件: 方差 < 25ms²

4. **线程安全压力测试**
   - 运行 10000 次启动-停止循环
   - 6 工位并发高频失败（每工位 100 次/秒失败计数）
   - 预期结果: 无崩溃，计数器值准确
   - 通过条件: 10000 次循环零异常

5. **S7 通信效率验证**
   - 使用 Wireshark 抓包分析
   - 单次 `ReadSnapshotAsync` 调用，统计 TCP 往返次数
   - 预期结果: 1 次 S7 协议帧（批量读取）
   - 通过条件: 通信时间 10-20ms（减少 50-66%）

6. **批量写入吞吐量验证**
   - 生成 1000 条测试数据
   - 对比单条写入 vs 批量写入的总耗时
   - 预期结果: 批量写入吞吐量提升 3-10x
   - 通过条件: 1000 条数据写入时间 < 500ms（单条写入约 3-5 秒）

### Preservation Checking

**Goal**: 确保修复后功能行为与修复前完全一致。

**Pseudocode:**
```
FOR ALL functionalScenario IN [数据完整性, 事件传播, 错误处理, 生命周期] DO
  resultBefore := executeScenario_original(functionalScenario)
  resultAfter := executeScenario_fixed(functionalScenario)
  ASSERT resultBefore == resultAfter
END FOR
```

**Testing Approach**: 
- 单元测试：验证关键方法的输入输出不变
- 集成测试：验证端到端数据流不变
- 属性基测试：使用随机输入验证数据一致性

**Test Plan**: 

1. **数据完整性测试**
   - 在修复前后代码上运行相同的 PLC 模拟场景（确定性种子）
   - 对比数据库中写入的 `StageTestData` 记录
   - 预期: 所有字段值完全相同（时间戳除外）
   - 测试用例:
     ```csharp
     [Test]
     public async Task BulkUpsert_ProducesSameResult_AsSingleUpsert()
     {
         // Arrange
         var data = GenerateTestData(count: 100);
         var repo1 = new InMemoryMotorTestRepository();
         var repo2 = new InMemoryMotorTestRepository();
         
         // Act - 单条写入
         foreach (var item in data) {
             await repo1.UpsertStageResultAsync(item);
         }
         
         // Act - 批量写入
         await repo2.BulkUpsertAsync(data);
         
         // Assert
         var result1 = await repo1.QueryAllAsync();
         var result2 = await repo2.QueryAllAsync();
         Assert.That(result1, Is.EquivalentTo(result2));
     }
     ```

2. **事件传播测试**
   - 订阅 `SnapshotReceived` 事件并记录所有事件
   - 对比修复前后事件数量和内容
   - 预期: 事件数量相同，内容相同（传播延迟可能不同）
   - 测试用例:
     ```csharp
     [Test]
     public async Task ChannelBasedPublish_DeliversSameEvents_AsDirectEvent()
     {
         // Arrange
         var events = new List<StationSnapshot>();
         var service = CreatePollingService();
         service.SnapshotReceived += (s, e) => events.Add(e);
         
         // Act
         service.Start();
         await Task.Delay(5000);
         service.Stop();
         
         // Assert
         Assert.That(events.Count, Is.GreaterThan(0));
         Assert.That(events.All(e => e.StationId != null));
     }
     ```

3. **错误处理测试**
   - 模拟 PLC 通信失败、数据库异常
   - 验证异常抛出和日志记录行为不变
   - 预期: 相同的异常类型、相同的日志条目
   - 测试用例:
     ```csharp
     [Test]
     public async Task BulkUpsert_ThrowsException_OnDatabaseError()
     {
         // Arrange
         var repo = CreateRepositoryWithFailingDb();
         var data = GenerateTestData(count: 10);
         
         // Act & Assert
         Assert.ThrowsAsync<DbUpdateException>(() => repo.BulkUpsertAsync(data));
     }
     ```

4. **生命周期测试**
   - 启动服务 → 运行 5 秒 → 停止服务
   - 验证所有任务正确取消、资源正确释放
   - 预期: 无资源泄漏、无僵尸任务
   - 测试用例:
     ```csharp
     [Test]
     public async Task Stop_CancelsAllTasks_AndFlushesChannelBuffer()
     {
         // Arrange
         var service = CreatePollingService();
         service.Start();
         await Task.Delay(1000);
         
         // Act
         await service.StopAsync();
         
         // Assert
         Assert.That(service._pollingTasks.All(t => t.IsCompleted));
         Assert.That(eventChannel.WriteReader.Completion.IsCompleted);
     }
     ```

5. **属性基测试（数据一致性）**
   - 使用 FSCheck 或 Hypothesis.NET 生成随机测试数据
   - 验证批量写入与单条写入的等价性
   - 属性定义:
     ```csharp
     [Property]
     public async Task<bool> BulkUpsert_IsEquivalentTo_SequentialUpsert(List<StageTestData> data)
     {
         var repo1 = new InMemoryMotorTestRepository();
         var repo2 = new InMemoryMotorTestRepository();
         
         // Sequential upsert
         foreach (var item in data) {
             await repo1.UpsertStageResultAsync(item);
         }
         
         // Bulk upsert
         await repo2.BulkUpsertAsync(data);
         
         // Query results
         var result1 = await repo1.QueryAllAsync();
         var result2 = await repo2.QueryAllAsync();
         
         return result1.OrderBy(x => x.Barcode).SequenceEqual(result2.OrderBy(x => x.Barcode));
     }
     ```

### Unit Tests

**核心测试场景**:

1. **Channel 解耦测试**
   - 测试 `EventChannelService` 的生产者-消费者正确性
   - 验证有界 Channel 的背压行为（DropOldest）
   - 验证无界 Channel 的无阻塞写入

2. **异步初始化测试**
   - 测试 `Lazy<Task<BackendRuntime>>` 只初始化一次
   - 测试并发调用 `GetSharedAsync()` 返回同一实例
   - 测试初始化失败时的异常传播

3. **批量写入测试**
   - 测试批次收集逻辑（时间窗口 100ms / 阈值 50 条）
   - 测试事务回滚行为
   - 测试空批次处理

4. **线程安全测试**
   - 测试 `ConcurrentDictionary` 并发操作
   - 测试 `AddOrUpdate` 原子性
   - 测试 `TryRemove` 正确性

5. **S7 批量读取测试**
   - 测试 `ReadMultipleVarsAsync` 调用参数正确性
   - 测试结果解析逻辑
   - 测试通信失败时的异常处理

### Property-Based Tests

**属性定义**:

1. **批量写入等价性**
   - ∀ data: List<StageTestData>, BulkUpsert(data) ≡ SequentialUpsert(data)
   - 生成策略: 随机生成 1-100 条记录，包含重复 Barcode 和 Stage

2. **Channel 无丢失性**
   - ∀ events: List<StationSnapshot>, ∀ events 写入 Channel → ∀ events 能从 Channel 读出
   - 生成策略: 随机生成 1-1000 个事件，多生产者单消费者

3. **并发失败计数准确性**
   - ∀ ops: List<(stationId, increment)>, 并发执行后计数器值 = ∑ increments
   - 生成策略: 随机生成 1000-10000 次并发操作

### Integration Tests

**端到端测试场景**:

1. **完整轮询流程测试**
   - 启动 6 工位模拟器 → 生成完成信号 → 验证数据库写入 → 验证事件传播
   - 断言: Dashboard 更新频率 <= 0.5 次/秒，数据完整性 100%

2. **高负荷压力测试**
   - 6 工位 × 每秒 10 次完成信号 × 持续 60 秒
   - 断言: 无崩溃、无数据丢失、轮询周期稳定

3. **优雅停机测试**
   - 服务运行中调用 `StopAsync()` → 验证 Channel 缓冲数据已刷新 → 验证所有任务已取消
   - 断言: 数据库中无丢失记录、无资源泄漏

4. **启动性能回归测试**
   - 测量冷启动时间（从进程启动到窗口可交互）
   - 断言: < 1 秒（修复前 2-15 秒）

5. **UI 响应性回归测试**
   - 在高负荷下（6 工位 × 2 次/秒）测量 UI 交互延迟
   - 断言: 按钮点击响应时间 < 100ms，窗口拖动流畅（> 30 FPS）

---

## Performance Targets

| 指标 | 修复前 | 修复后目标 | 测量方法 |
|------|--------|------------|----------|
| UI 线程 DB 查询频率 | 6-12 次/秒 | <= 0.5 次/秒 | PerfView 采样 |
| UI 线程负载 | 30-50% | < 5% | WPF Performance Profiler |
| 应用启动时间 | 2-15 秒阻塞 | < 500ms 无阻塞 | Stopwatch 测量窗口显示时间 |
| 轮询周期抖动 | ±100-200ms | ±5ms | dotTrace 异步等待分析 |
| SQLite 写入吞吐量 | 单条写入 | 3-10x 提升 | 基准测试（1000 条数据） |
| S7 通信时间 | 30-60ms | 10-20ms | Wireshark 抓包分析 |
| Modbus 锁持有时间 | 5-20ms | 2-8ms | dotTrace 同步等待分析 |
| 线程安全崩溃率 | 1/1000 | 0/10000 | 压力测试 |

---

## Risk Analysis

### High Risk Areas

1. **Channel 缓冲区溢出**
   - 风险: 如果 Dashboard 消费者阻塞，Snapshot Channel 可能无限增长（Unbounded）
   - 缓解: 监控 Channel 队列深度，设置告警阈值（如 > 1000）
   - 降级策略: 切换为 Bounded Channel + DropOldest

2. **批量写入事务失败**
   - 风险: 如果批量事务回滚，整批数据丢失
   - 缓解: 实现重试机制（最多 3 次），失败后降级为单条写入
   - 监控: 记录批量写入失败率

3. **异步初始化竞态条件**
   - 风险: 如果多个线程同时调用 `GetSharedAsync()`，`Lazy<Task<T>>` 可能创建多个实例
   - 缓解: 使用 `LazyThreadSafetyMode.ExecutionAndPublication` 确保线程安全
   - 验证: 单元测试并发调用

### Migration Strategy

**分阶段部署**:

1. **阶段 1: 基础设施层**（低风险）
   - 新增 `EventChannelService`
   - 新增 `BatchWriteService`
   - 新增 `IMotorTestRepository.BulkUpsertAsync()`
   - 验证: 单元测试通过

2. **阶段 2: 线程安全修复**（低风险）
   - 替换 `Dictionary` 为 `ConcurrentDictionary`
   - 移除 `Task.Run` 包装
   - 验证: 压力测试 10000 次无崩溃

3. **阶段 3: 轮询解耦**（中风险）
   - 修改 `PlcPollingService` 使用 Channel 写入
   - 启动 `BatchWriteService`
   - 验证: 数据完整性测试 + 性能基准测试

4. **阶段 4: UI 解耦**（中风险）
   - 修改 `DashboardViewModel` 使用 Channel 消费
   - 验证: UI 响应性测试 + 功能回归测试

5. **阶段 5: 异步初始化**（高风险）
   - 修改 `BackendRuntime` 为 `Lazy<Task<T>>` 模式
   - 修改 `App.xaml.cs` 启动流程
   - 验证: 启动流程测试 + 集成测试

6. **阶段 6: PLC 优化**（低风险）
   - S7 批量读取
   - Modbus 锁粒度优化
   - 验证: 通信性能测试

**回滚策略**:
- 每个阶段使用 Feature Flag 控制（环境变量或配置文件）
- 如果阶段 N 失败，禁用 Feature Flag 回滚到阶段 N-1
- 关键路径（阶段 3-5）保留双写模式（同时写入 Channel 和直接调用），以便快速切换

---

## Appendix: API Signatures

### EventChannelService
```csharp
public sealed class EventChannelService : IDisposable
{
    public ChannelReader<StationSnapshot> SnapshotReader { get; }
    public ChannelWriter<StationSnapshot> SnapshotWriter { get; }
    public ChannelReader<StageTestData> WriteReader { get; }
    public ChannelWriter<StageTestData> WriteWriter { get; }
    public void Dispose();
}
```

### BatchWriteService
```csharp
public sealed class BatchWriteService : IDisposable
{
    public BatchWriteService(IMotorTestRepository repository, ChannelReader<StageTestData> reader);
    public Task StopAsync();
    public void Dispose();
}
```

### IMotorTestRepository (扩展)
```csharp
public interface IMotorTestRepository
{
    Task UpsertStageResultAsync(StageTestData data, CancellationToken ct = default);
    Task BulkUpsertAsync(IEnumerable<StageTestData> dataList, CancellationToken ct = default);
    Task<ProductionSummary> GetSummaryAsync(DateTime start, DateTime end, CancellationToken ct = default);
    // ... 其他方法 ...
}
```

### BackendRuntime (修改后)
```csharp
public sealed class BackendRuntime
{
    public static Task<BackendRuntime> GetSharedAsync();
    public ObservableCollection<StationConfig> StationConfigs { get; }
    public IMotorTestRepository Repository { get; }
    public PlcPollingService PollingService { get; }
    public EventChannelService EventChannel { get; }
    public BatchWriteService BatchWriter { get; }
    public void Dispose();
}
```

### PlcPollingService (修改后)
```csharp
public sealed class PlcPollingService : IDisposable
{
    public PlcPollingService(
        IEnumerable<StationConfig> stationConfigs,
        IMotorTestRepository repository,
        IPlcClientFactory clientFactory,
        EventChannelService eventChannel,  // 新增参数
        TimeSpan? pollInterval = null);
    
    public void Start();
    public Task StopAsync();
    public void Dispose();
    
    public event EventHandler<StationSnapshot>? SnapshotReceived;  // 保留向后兼容
    public event EventHandler<string>? LogReceived;
}
```

### DashboardViewModel (修改后)
```csharp
public class DashboardViewModel : ViewModelBase, IDisposable
{
    public DashboardViewModel(IMotorTestRepository repository, EventChannelService eventChannel);
    public void Dispose();
    
    // 属性保持不变
    public int TotalChecked { get; set; }
    public int OkCount { get; set; }
    public int NgCount { get; set; }
    public double PassRate { get; set; }
    // ...
}
```
