# 数据持久化层审查报告

**审查日期：** 2026-06-16  
**审查范围：** MotorTestSystem 上位机系统 — 数据库访问层、日志写入逻辑、数据同步机制  
**审查目标：** 评估工业现场网络波动/瞬时断开场景下的数据安全性、IO 性能与容错能力

---

## 目录

1. [总体架构概览](#1-总体架构概览)
2. [审查一：批量插入、事务与预编译语句](#2-审查一批量插入事务与预编译语句)
3. [审查二：磁盘 IO 瓶颈对业务/通讯线程的反向阻塞](#3-审查二磁盘-io-瓶颈对业务通讯线程的反向阻塞)
4. [审查三：离线断网与数据补传机制](#4-审查三离线断网与数据补传机制)
5. [风险汇总表](#5-风险汇总表)
6. [改进建议](#6-改进建议)

---

## 1. 总体架构概览

### 数据流主线

```
PLC A1~A6 ──[Modbus TCP / S7 / MC]──▶ PlcPollingService (per-station Task, 1s interval)
                                            │
                                            ▼
                                  EventChannelService
                      ┌──────────────────────────┬────────────────────┐
                      │ _writeChannel            │ _snapshotChannel   │
                      │ Bounded(500, DropOldest) │ Unbounded          │
                      └────────────┬─────────────┴────────────────────┘
                                   ▼
                         BatchWriteService
                         (后台 Task, 100ms/50条 聚合)
                                   ▼
                      ┌──────────────────────────┐
                      │ BulkUpsertAsync          │
                      │ (UseTranAsync 事务包装)    │
                      └────────────┬─────────────┘
                                   ▼
                          SQLite (MotorTest.db)
```

### 数据库栈

| 项 | 内容 |
|---|------|
| 数据库 | SQLite 本地文件 |
| ORM | SqlSugarCore 5.1.4.214 |
| 连接策略 | `IsAutoCloseConnection = true`（每次操作自动开/关连接） |
| 实体表 | MotorTestRecords / Users / StationConfigs / Notifications |
| 存储路径 | `AppDomain.BaseDirectory/Data/MotorTest.db` |

---

## 2. 审查一：批量插入、事务与预编译语句

### 2.1 批量插入

**结论：部分使用，但 SQLite 下行不充分。**

| 写入点 | 批量/单条 | 详情 |
|--------|----------|------|
| `BulkUpsertAsync` | ✅ 批量 | `Insertable(toInsert)` + `Updateable(toUpdate)` — SqlSugar ORM 批量 API |
| `UpsertStageResultAsync` | ❌ 单条 | `Insertable(entity)` 或 `Updateable(entity)` 分别执行 |
| `SqlSugarNotificationService.Add` | ❌ 单条 | 同步 `Insertable(entity).ExecuteCommand()` |
| `SqlSugarNotificationService.AddRange` | ✅ 批量 | `Insertable(entities).ExecuteCommand()` 批量插入 |
| `SqlSugarUserService.Create/Update/Delete` | ❌ 单条 | 全部单条同步执行 |
| 种子数据 | ✅ 批量 | `Insertable(users).ExecuteCommand()` |

> ⚠️ **SqlSugar 的 `Insertable(list)` 在 SQLite 驱动下不是真正的 SQL 批量 INSERT。** 查看 SqlSugar SQLite 实现源码可知，`Insertable(list)` 默认生成 N 条独立的 `INSERT INTO ... VALUES(...)` 语句，每条自动包裹在隐式事务中（`IsAutoCloseConnection = true` 时）。这不会比手动循环插入节省多少 IO 开销。SqlSugar 5.x 的 SQLite 提供者**不支持真正的批量 INSERT**（即 `INSERT INTO t VALUES(...),(...),(...)` 多行语法），因为 SQLite 的 Microsoft.Data.Sqlite 驱动对批量命令的支持有限。

### 2.2 事务使用

**结论：事务覆盖率极低。**

| 位置 | 使用事务？ | 说明 |
|------|-----------|------|
| `BulkUpsertAsync` (L69) | ✅ | `UseTranAsync` 包裹整个 select+insert+update |
| `UpsertStageResultAsync` | ❌ | 没有事务 — select 和 insert/update 是两个独立操作 |
| `SqlSugarNotificationService.Add` | ❌ | 单条无事务 |
| `SqlSugarUserService` | ❌ | 所有操作无事务 |
| 种子数据 | ❌ | `Insertable(list).ExecuteCommand()` 无显式事务 |

**并发风险：** `UpsertStageResultAsync` 的 select-then-insert 模式在没有事务保护的情况下，两个并发线程可能同时判断"数据不存在"并都执行 `Insertable`，导致主键冲突（Barcode 虽非数据库主键但代码逻辑视其为业务唯一键）。

### 2.3 预编译语句

**结论：隐式使用，效果有限。**

SqlSugar ORM 自动生成参数化 SQL（`@` 前缀参数），这提供了 SQL 注入防护和基本的查询计划缓存。但在 SQLite 中，由于 `IsAutoCloseConnection = true`，每次操作都会重新创建数据库连接，导致预编译语句的缓存效果大打折扣。

```
典型生成的 SQL（调试日志可见）：
[SQL] SELECT * FROM MotorTestRecords WHERE Barcode = @barcode  -- 每次都新建连接
[SQL] INSERT INTO MotorTestRecords (...) VALUES (...)            -- 语句未被缓存
```

### 2.4 评估总结

| 评估维度 | 评分 (1-5) | 说明 |
|---------|-----------|------|
| 批量插入使用 | 2/5 | BulkUpsertAsync 使用了批量 API，但 SqlSugar + SQLite 无法转化为真正的批量 SQL |
| 事务覆盖 | 2/5 | 仅一处使用事务，其余写入点均无事务保护 |
| 预编译语句 | 3/5 | ORM 层自动参数化，但 SQLite 连接频繁关闭导致缓存效益低 |

---

## 3. 审查二：磁盘 IO 瓶颈对业务/通讯线程的反向阻塞

### 3.1 主线数据路径分析

系统在 `BackendRuntime` 中正确装配了 `EventChannelService`：

```csharp
// BackendRuntime.cs L40-41
PollingService = new PlcPollingService(..., eventChannel: eventChannel);
BatchWriter = new BatchWriteService(Repository, eventChannel.WriteReader);
```

**正向结论：主线路径已做到 IO 解耦。**

```
PLC 轮询线程 (PollStationAsync)
    │
    ▼ await eventChannel.WriteWriter.WriteAsync(data)
    │  └── 仅写入内存 Channel，O(1)，非阻塞
    ▼ await Task.Delay(1000)  ← 轮询间隔
```

```
后台 BatchWriteService 线程
    │
    ▼ await channelReader.WaitToReadAsync()
    │  └── 100ms/50条 聚合
    ▼ await repository.BulkUpsertAsync(batch)
       └── 真正耗时的 DB 写入在这里，但不阻塞轮询
```

**关键代码确认：** `PlcPollingService.PollStationAsync` (L135-138)：

```csharp
if (_eventChannel != null)
{
    await _eventChannel.WriteWriter.WriteAsync(snapshot.CompletedData, cancellationToken);
}
```

**但是存在一个严重的阻塞路径（备选路径）：**

```csharp
// 当 _eventChannel == null 时（L140-141）：
else
{
    await _repository.UpsertStageResultAsync(snapshot.CompletedData, cancellationToken);
}
```

如果没有 EventChannel，PLC 轮询将**直接等待 DB 写入完成**，此时若磁盘 IO 饱和，轮询周期将从 1s 急剧恶化到数秒甚至数十秒，整条产线的数据采集都会延迟。

### 3.2 ⚠️ 严重问题：通知服务同步阻塞轮询线程

`BackendRuntime` 中订阅了 `PollingService.SnapshotReceived` 事件：

```csharp
// BackendRuntime.cs L45
PollingService.SnapshotReceived += OnSnapshotReceivedForNotification;
```

而 `OnSnapshotReceivedForNotification` (L67-113) 是一个**同步事件处理器**，其中调用了：

```csharp
_notificationService.Add(new NotificationItem { ... });
```

`SqlSugarNotificationService.Add` (L97-118) 是**完全同步**的：

```csharp
public void Add(NotificationItem notification)
{
    var dispatcher = Application.Current?.Dispatcher;
    if (dispatcher != null && !dispatcher.CheckAccess())
    {
        dispatcher.Invoke(() => Add(notification));  // ← 同步阻塞在当前线程！
        return;
    }

    _dbContext.Db.Insertable(entity).ExecuteCommand();  // ← 同步 DB 写入
    // ...
}
```

**阻塞链：** `PollStationAsync` → `Publish()` → `SnapshotReceived` 事件 → `OnSnapshotReceivedForNotification` → `_notificationService.Add()` → `Insertable().ExecuteCommand()` ⸺ **同步磁盘写入阻塞在 PLC 轮询线程上**。

同理，`OnLogReceivedForNotification` 也存在同样问题。

### 3.3 BatchWriteService 失败处理

当 `BulkUpsertAsync` 抛出异常时（如数据库锁定、磁盘满），**仅有一条 Trace 日志**，然后继续：

```csharp
// BatchWriteService.cs L76-79
catch (Exception ex)
{
    System.Diagnostics.Trace.WriteLine($"Error during bulk upsert: {ex.Message}");
    // 没有重试！数据静默丢失！没有进入死信队列！
}
finally
{
    batch.Clear();  // 清空 batch，这批数据彻底丢失
}
```

### 3.4 通道容量瓶颈

`_writeChannel` 容量仅 **500** 条，使用 `DropOldest` 策略：

```csharp
_writeChannel = Channel.CreateBounded<StageTestData>(
    new BoundedChannelOptions(500)
    {
        FullMode = BoundedChannelFullMode.DropOldest  // 满了就丢最旧数据
    });
```

假设每个电机测试有 3 个阶段（空载→噪音→负载），日产量 2000 台，每个阶段约产生 1 条记录。峰值速率如果达到每秒 10 条以上，而 DB 写入延迟上升到 50s（磁盘 IO 瓶颈场景），缓冲区在 50 秒内就会装满并开始丢弃数据。

### 3.5 评估总结

| 评估维度 | 评分 | 说明 |
|---------|------|------|
| 主线 IO 解耦 | 4/5 | EventChannel 正确解耦了轮询与 DB 写入 |
| 备选路径阻塞风险 | 1/5 | 无 EventChannel 时轮询直接等待 DB |
| 通知服务阻塞 | ❌ **1/5** | **同步 DB 写入阻塞在 PLC 轮询线程上，风险极高** |
| 失败重试机制 | 1/5 | 无重试，数据静默丢失 |
| 背压保护 | 3/5 | DropOldest 防止 OOM，但未告警数据丢弃 |

---

## 4. 审查三：离线断网与数据补传机制

### 4.1 PLC 级断网重连

`PlcPollingService` 实现了指数退避重连：

```
1s → 2s → 4s → 8s → 16s → 32s → 60s (上限)
```

连接恢复后，下一个轮询周期即可正常采集数据。**但没有离线数据缓存** — PLC 断开期间产生的测试数据全部丢失，上位机没有任何机制要求 PLC 重传丢失的记录。

### 4.2 ❌ MES / 云端同步 — 完全不存在

在 28 个相关代码文件中搜索以下关键词结果：

| 关键词 | 匹配数 | 说明 |
|--------|-------|------|
| `MES` | 0 | 没有任何 MES 接口 |
| `Upload` | 0 | 没有数据上传功能 |
| `Sync` | 0 | 没有同步服务 |
| `HttpClient` | 0 | 没有 HTTP 请求 |
| `Cloud` / `云端` | 0 | 无云端集成 |
| `Backlog` / `积压` | 0 | 无积压队列 |
| `Retry` | 1 | 仅在 PLC 连接重试中出现 |
| `Offline` | 2 | 仅用于工位在线/离线状态检测 |

**结论：系统完全是一个本地单机应用程序，所有数据仅存储在本地 SQLite 文件中。** 不存在"断网恢复后补传"的概念——因为没有远程目标可传。数据从 PLC 采集到本地数据库的流程是单向的，一旦写入失败即永久丢失。

### 4.3 PLC ↔ 上位机断网场景的数据完整性

在工业现场可能的断网场景：

| 场景 | 影响 | 恢复是否平滑 |
|------|------|-------------|
| PLC 通信电缆松动，<br>1s 后恢复 | 该秒的测试数据丢失（如果有完成信号） | ⚠️ 丢失数据，但大多数情况下无测试完成 |
| PLC 断网 2 小时 | 2 小时内所有完成信号和测试数据丢失 | ❌ 全部丢失，无补传机制 |
| 上位机程序重启 | EventChannel 缓冲区清空，队列中的待写入数据丢失 | ❌ BatchWriteService 的未写入数据在 Dispose 时可能来不及 Flush |
| 磁盘写满 | BulkUpsertAsync 抛异常，batch 数据被 Clear() 丢弃 | ❌ 直接丢失 |

### 4.4 评估总结

| 评估维度 | 评分 | 说明 |
|---------|------|------|
| PLC 重连机制 | 4/5 | 指数退避设计合理，上限 60s 有保护 |
| PLC 断网数据补传 | ❌ **1/5** | 无离线数据缓存或 PLC 重传机制 |
| MES/云端同步 | ❌ 0/5 | 完全不存在 |
| 数据库写失败重试 | ❌ 1/5 | 无重试、无死信队列、无日志持久化 |
| 优雅关闭/数据完整性 | 2/5 | Dispose 时有 FlushRemaining 但仅 2s 超时 |

---

## 5. 风险汇总表

| 风险编号 | 级别 | 描述 | 所属路径 | 当前缓解措施 |
|---------|------|------|---------|------------|
| R1 | 🔴 高危 | `UpsertStageResultAsync` 无事务 — 并发 select-then-insert 导致数据重复/主键冲突 | 单条写入路径 | 无 |
| R2 | 🔴 高危 | 通知服务 `Add()` 同步 DB 写入阻塞 PLC 轮询线程 | `PollStationAsync` → `SnapshotReceived` → `NotificationService.Add` | 无 |
| R3 | 🔴 高危 | BatchWriteService 写失败即丢弃，无重试机制 | `BulkUpsertAsync` 异常 → `batch.Clear()` | 仅 Trace 日志 |
| R4 | 🟡 中危 | `_writeChannel` 容量仅 500，`DropOldest` 不告警 | IO 瓶颈导致缓冲区满 | 内存保护（防 OOM） |
| R5 | 🟡 中危 | SqlSugar + SQLite 无法实现真正批量 INSERT | `BulkUpsertAsync` 生成 N 条独立 INSERT | 无 |
| R6 | 🟢 低危 | `IsAutoCloseConnection = true` 使预编译缓存失效 | 每次操作重新连接 | 无 |
| R7 | 🔴 高危 | 无 MES/云端同步 — 无法远程上传数据 | 整个数据流 | 无 |
| R8 | 🔴 高危 | 第三方服务（UserService、NotificationService）使用同步 `ExecuteCommand`，可能卡 UI | UI 线程调用路径 | 无 |
| R9 | 🟡 中危 | `Dispose()` 时 2s 超时，未写入数据丢失 | 程序关闭 | FlushRemaining (2s 硬超时) |
| R10 | 🟡 中危 | PLC 断网 2 小时恢复后，丢失所有离线期间生成的数据 | PLC → 上位机通信 | 指数退避重连（仅保证连接恢复） |

---

## 6. 改进建议

### P0 — 必须修复（生产阻塞）

#### 1. 解耦通知服务的同步 DB 写入（修复 R2）

**当前问题：** `OnSnapshotReceivedForNotification` 是同步事件处理器，调用 `NotificationService.Add()` 同步写 DB，阻塞 PLC 轮询线程。

**建议方案：** 将通知写入改为**异步队列 + 后台消费者**模式：

```
PLC 轮询线程
  → Publish() 触发事件
  → OnSnapshotReceivedForNotification 
     → 仅将通知写入 Channel<NotificationItem> (Unbounded)
     → 立即返回 ← 不再阻塞
       └── 后台消费者（独立 Task）
            → 从 Channel 批量读取
            → 同步写入 DB（批次写入）
```

或者使用 `System.Threading.Channels` 创建一个优先级通知通道，同样由后台消费者处理 DB 写入。

#### 2. 批量写入增加重试与死信队列（修复 R3）

**当前问题：** `BatchWriteService` 异常后直接丢弃数据。

**建议方案：**

```
catch (Exception ex)
{
    // 1. 记录日志到结构化日志文件
    Logger.Error("BulkUpsert failed", ex, batch.Count);
    
    // 2. 重试（指数退避，最多 3 次）
    bool success = await RetryWithBackoffAsync(batch, 3);
    
    // 3. 重试失败 → 写入死信队列（JSON 文件）
    if (!success)
        await DeadLetterQueue.WriteAsync(batch);
}

// 启动时扫描死信队列，自动补传
```

#### 3. Upsert 增加事务保护（修复 R1）

**建议方案：** 将 `UpsertStageResultAsync` 也包装在 `UseTranAsync` 中，使其 select 和 insert/update 成为原子操作：

```csharp
public async Task UpsertStageResultAsync(...)
{
    await _ctx.Db.Ado.UseTranAsync(async () =>
    {
        var existing = await _ctx.Db.Queryable<...>().FirstAsync(...);
        if (existing == null)
            await _ctx.Db.Insertable(entity).ExecuteCommandAsync(ct);
        else
            await _ctx.Db.Updateable(entity).ExecuteCommandAsync(ct);
    });
}
```

### P1 — 应修复（重要改进）

#### 4. 第三方服务全面异步化（修复 R8）

`SqlSugarNotificationService` 和 `SqlSugarUserService` 的全部 `ExecuteCommand()` 改为 `await ExecuteCommandAsync()`，并暴露 async 接口。UI 线程调用时使用 `await dispatcher.InvokeAsync()` 配合 async 路径。

#### 5. 增加 DropOldest 告警（修复 R4）

当 `_writeChannel` 触发 DropOldest 时，当前 `System.Threading.Channels` 不提供内置回调。建议：
- 定期监控 `ChannelReader.Count`（需要 `BoundedChannel` 支持）
- 或封装一层包装器，在 TryWrite 返回 false 时触发告警
- 在 UI 上显示"写入缓冲区满"的通知

#### 6. 增加启动时死信队列补传

在 `BackendRuntime` 初始化时，增加一步：扫描死信队列目录，如果有残存文件，自动读取并调用 `BulkUpsertAsync` 补传，补传成功后删除文件。

### P2 — 可以改进

#### 7. 调整 SqlSugar 连接策略

将 `IsAutoCloseConnection = false`（保持长连接），减少 SQLite 连接创建/销毁的开销。SQLite 在单文件模式下不必每次操作都开关连接。

#### 8. 增加 File flush 策略

SQLite 默认的同步模式是 `NORMAL`（每检查点刷新一次）。对于高频率写入，建议评估是否需要改为 `PRAGMA synchronous = OFF`（由 SQLite journal 机制保障 crash safety）：

```sql
PRAGMA synchronous = NORMAL;   -- 默认
PRAGMA synchronous = FULL;     -- 最安全，但最慢
PRAGMA synchronous = OFF;      -- 最快，crash 时可能丢失最后几条
```

#### 9. 写 Channel 容量根据生产节拍调整

评估产线峰值数据生成速率。以日产量 2000 台、3 个测试阶段/台计算，日均约 6000 条记录。峰值短期内（如换型后的连续测试）可能达到 50 条/秒。500 条通道容量仅能缓冲 10 秒峰值。建议根据实测调整到 2000-5000。

### P3 — 中远期规划

#### 10. MES/云端同步（填补 R7 空白）

如果系统需要对接 MES 或云平台，建议：
- **增量同步队列：** 在本地 SQLite 中增加 `SyncQueue` 表，记录已写入但未同步的记录
- **断点续传：** 每条记录有同步状态（Pending / Syncing / Synced / Failed）
- **离线积压管理：** 断网时数据在 SyncQueue 中累积，重连后按时间顺序补传
- **限速保护：** 重连后补传时加入速率限制，避免压垮 MES/云端接口

---

## 附录：文件审查清单

| 文件路径 | 行数 | 审查状态 |
|---------|------|---------|
| `Data/Repositories/SqlMotorTestRepository.cs` | 341 | ✅ 已审查 |
| `Data/DbContext/SqlSugarDbContext.cs` | 307 | ✅ 已审查 |
| `Data/Services/SqlSugarNotificationService.cs` | 258 | ✅ 已审查 |
| `Data/Services/SqlSugarUserService.cs` | 158 | ✅ 已审查 |
| `Business/Services/BatchWriteService.cs` | 154 | ✅ 已审查 |
| `Business/Services/PlcPollingService.cs` | 200 | ✅ 已审查 |
| `Business/Services/EventChannelService.cs` | 59 | ✅ 已审查 |
| `Business/Services/BackendRuntime.cs` | 429 | ✅ 已审查 |
| `Business/Interfaces/IMotorTestRepository.cs` | 28 | ✅ 已审查 |

---

*报告结束。本报告基于代码本身的静态分析及架构推导，部分建议可能需要结合现场实际负载验证后实施。*
