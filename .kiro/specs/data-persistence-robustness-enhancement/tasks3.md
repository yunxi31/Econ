# Implementation Plan: 数据持久化鲁棒性增强

## Overview

本实现计划将 MotorTestSystem 工业上位机系统的数据持久化层进行全面改造，修复 10 个已识别风险点（R1-R10），确保在网络波动、磁盘 IO 饱和、断电等极端场景下的数据完整性和系统实时性。改造将分阶段进行，优先实现 P0/P1 关键功能，P2/P3 性能优化和云端集成作为后续迭代。

核心改造包括：
1. Repository 事务保护（P0）
2. 通知服务异步解耦（P0）
3. 批量写入重试与死信队列（P0）
4. 写入通道监控与告警（P1）
5. 全异步接口优化（P1）
6. SQLite 连接与性能优化（P2）
7. MES/云端同步机制（P3）

## Tasks

- [ ] 1. 创建核心基础设施和数据模型
  - [ ] 1.1 创建死信队列数据模型和序列化组件
    - 在 `MotorTestSystem.Models` 中创建 `DeadLetterMetadata.cs`
    - 在 `MotorTestSystem.Services` 中创建 `DeadLetterParser.cs` 和 `DeadLetterSerializer.cs`
    - 实现 JSON 序列化/反序列化，处理 `double?`、`DateTime` 和 `null` 值的边缘情况
    - _Requirements: 14.1, 14.2, 14.3_

  - [ ] 1.2 为死信队列序列化编写属性测试
    - **Property 5: 死信队列序列化的往返属性**
    - **Validates: Requirements 14.4**
    - 使用 FsCheck 生成 100 个随机 `List<StageTestData>` 批次
    - 验证 `Parse(Serialize(data)) ≡ data`
    - _Requirements: 14.5_

  - [ ] 1.3 创建 SyncQueue 数据模型（P3 云端同步）
    - 在 `MotorTestSystem.Models` 中创建 `SyncQueueEntity.cs`
    - 定义字段：Id, RecordId, SyncStatus, RetryCount, LastAttempt, CreatedAt
    - 在 `SqlSugarDbContext` 中添加数据库迁移脚本
    - _Requirements: 10.1_

- [ ] 2. 实现 Repository 层事务保护（P0 - R1）
  - [ ] 2.1 重构 UpsertStageResultAsync 增加事务包装
    - 在 `SqlSugarMotorTestRepository.cs` 中修改 `UpsertStageResultAsync` 方法
    - 使用 `UseTranAsync` 包装 SELECT + INSERT/UPDATE 操作
    - 确保并发场景下不会重复插入相同 Barcode
    - _Requirements: 1.1, 1.2, 1.4_

  - [ ] 2.2 为并发 Upsert 编写属性测试
    - **Property 1: 并发 Upsert 操作的最终一致性**
    - **Validates: Requirements 1.1, 1.2**
    - 生成 10-100 个并发 Task 对同一 Barcode 执行 Upsert
    - 验证最终只有一条记录存在
    - _Requirements: 1.2_

  - [ ] 2.3 为事务回滚编写属性测试
    - **Property 2: 事务失败后的状态回滚**
    - **Validates: Requirements 1.3**
    - 生成随机初始状态和事务序列，注入故障
    - 验证失败后数据库状态与初始状态一致
    - _Requirements: 1.3_

- [ ] 3. 实现死信队列机制（P0 - R3）
  - [ ] 3.1 实现 DeadLetterQueue 核心功能
    - 在 `MotorTestSystem.Services` 中创建 `IDeadLetterQueue.cs` 接口
    - 创建 `DeadLetterQueue.cs` 实现类
    - 实现 `EnqueueAsync`：序列化失败批次为 JSON 文件（格式：`{timestamp}_{guid}.json`）
    - 实现 `ScanAsync`：扫描目录并按时间戳排序
    - 实现 `RetryAsync`：反序列化并重试写入
    - 实现 `DeleteAsync` 和 `MarkAsFailedAsync`
    - _Requirements: 3.2, 3.3, 3.4, 3.7_

  - [ ] 3.2 为死信队列时间顺序性编写属性测试
    - **Property 4: 死信队列文件处理的时间顺序性**
    - **Validates: Requirements 6.2**
    - 生成 5-20 个随机时间戳的文件
    - 验证扫描结果按时间戳升序排列
    - _Requirements: 6.2_

  - [ ] 3.3 集成死信队列到 BatchWriteService
    - 修改 `BatchWriteService.cs` 中的 `ProcessQueueAsync` 方法
    - 实现 `TryBulkUpsertWithRetryAsync`：指数退避重试（1s, 2s, 4s）
    - 失败 3 次后调用 `DeadLetterQueue.EnqueueAsync`
    - _Requirements: 3.1, 3.5_

  - [ ] 3.4 实现启动时自动补传死信队列
    - 在 `BackendRuntime.cs` 初始化流程中添加死信队列扫描
    - 在启动 `BatchWriteService` 前调用 `DeadLetterQueue.ScanAsync` 和 `RetryAsync`
    - 记录补传统计日志（成功/失败文件数、记录数）
    - _Requirements: 6.1, 6.3, 6.4, 6.5_

- [ ] 4. Checkpoint - 验证 P0 关键功能
  - 确保所有测试通过，运行集成测试验证 Repository 事务保护和死信队列功能
  - 询问用户是否有问题或需要调整

- [ ] 5. 实现通知服务异步解耦（P0 - R2）
  - [ ] 5.1 在 EventChannelService 中新增通知通道
    - 修改 `EventChannelService.cs`，新增 `_notificationChannel`（无界通道）
    - 暴露 `NotificationReader` 和 `NotificationWriter` 属性
    - _Requirements: 2.1, 2.4_

  - [ ] 5.2 创建 NotificationWriter 后台消费者
    - 在 `MotorTestSystem.Services` 中创建 `NotificationWriter.cs`
    - 实现 `ConsumeAsync`：批量读取通知项（100ms 或 50 条触发）
    - 调用 `INotificationService.AddRangeAsync` 批量写入
    - 捕获异常并记录日志，不影响后续批次
    - _Requirements: 2.2, 2.5, 2.6_

  - [ ] 5.3 重构 PlcPollingService 解耦通知写入
    - 修改 `PlcPollingService.cs` 中的 `OnSnapshotReceivedForNotification` 事件处理
    - 将通知项写入 `_notificationChannel` 而非直接调用同步 `Add()` 方法
    - 确保写入立即返回，不阻塞 PLC 轮询线程
    - _Requirements: 2.2_

  - [ ] 5.4 集成 NotificationWriter 到 BackendRuntime
    - 在 `BackendRuntime.cs` 中启动 `NotificationWriter` 后台 Task
    - 确保 Dispose 时正确停止消费者
    - _Requirements: 2.2_

- [ ] 6. 实现通知服务和用户服务全异步接口（P1 - R8）
  - [ ] 6.1 重构 NotificationService 为异步接口
    - 修改 `SqlSugarNotificationService.cs` 所有方法为 `Async` 后缀
    - 将所有 `ExecuteCommand()` 改为 `ExecuteCommandAsync()`
    - 将所有 `dispatcher.Invoke()` 改为 `await dispatcher.InvokeAsync()`
    - 正确传播 `CancellationToken`
    - 实现 `AddRangeAsync` 批量写入方法
    - _Requirements: 5.1, 5.2, 5.4, 5.5_

  - [ ] 6.2 重构 UserService 为异步接口
    - 修改 `UserService.cs` 所有 CRUD 方法为 `Async` 后缀
    - 将同步数据库操作改为异步
    - _Requirements: 5.2, 5.3_

- [ ] 7. 实现写入通道监控与告警（P1 - R4）
  - [ ] 7.1 在 EventChannelService 中实现水位查询接口
    - 添加原子计数器 `_writeChannelCount`（使用 `Interlocked`）
    - 实现 `GetWriteChannelUtilization()` 方法：返回占用率（0.0-1.0）
    - 实现 `GetWriteChannelCount()` 和 `GetNotificationChannelCount()` 方法
    - _Requirements: 4.1_

  - [ ] 7.2 为水位计算编写属性测试
    - **Property 3: 写入通道水位计算的正确性**
    - **Validates: Requirements 4.1**
    - 生成随机容量和队列长度，验证占用率计算正确
    - _Requirements: 4.1_

  - [ ] 7.3 实现后台监控线程和日志告警
    - 在 `BatchWriteService.cs` 中新增监控 Task
    - 每秒查询通道占用率，≥80% 时每 5 秒记录警告日志
    - 跟踪丢弃计数器（DropOldest 策略触发时递增）
    - _Requirements: 4.2, 4.4_

  - [ ] 7.4 实现 UI 监控面板和告警指示器
    - 在 `DashboardViewModel.cs` 中新增监控属性（通道占用率、死信队列数量、丢弃计数）
    - 创建 `DataPersistenceMonitorControl.xaml` 控件显示监控指标
    - 占用率 ≥95% 时显示红色告警
    - 提供"手动触发死信队列补传"按钮
    - _Requirements: 4.3, 13.1, 13.2, 13.3, 13.4_

  - [ ] 7.5 添加 WriteChannelCapacity 配置项
    - 在 `appsettings.json` 中新增 `DataPersistence.WriteChannelCapacity` 配置项（默认 2000）
    - 修改 `EventChannelService` 构造函数读取配置
    - _Requirements: 4.5_

- [ ] 8. Checkpoint - 验证 P1 功能和监控能力
  - 运行压力测试验证通道监控、死信队列、异步接口工作正常
  - 验证 UI 监控面板显示正确
  - 询问用户是否有问题或需要调整

- [ ] 9. 实现 SQLite 连接和性能优化（P2 - R6, R5, R9）
  - [ ] 9.1 调整 SQLite 连接策略为长连接
    - 修改 `SqlSugarDbContext.cs` 初始化时设置 `IsAutoCloseConnection = false`
    - 在 `BackendRuntime.Dispose()` 中显式调用 `Db.Dispose()` 关闭连接
    - _Requirements: 7.1, 7.3, 7.4_

  - [ ] 9.2 评估并实现 SQLite 批量 INSERT 优化
    - 在 `SqlSugarRepository.cs` 中新增 `BulkUpsertWithRawSqlAsync` 方法
    - 使用原生 ADO.NET 实现多行 `INSERT INTO ... VALUES (...), (...), (...)` 语法
    - 实现性能基准测试对比（BenchmarkDotNet）
    - 如果性能提升 >30%，则替换 `BulkUpsertAsync` 默认实现
    - _Requirements: 8.1, 8.2, 8.3, 8.4, 8.5_

  - [ ] 9.3 增加 SQLite 同步策略可配置
    - 在 `appsettings.json` 中新增 `DataPersistence.SQLiteSyncMode` 配置项（默认 NORMAL）
    - 在 `SqlSugarDbContext` 初始化时执行 `PRAGMA synchronous = {value}`
    - 添加配置注释说明 NORMAL/FULL/OFF 三种模式的权衡
    - _Requirements: 9.1, 9.2, 9.3, 9.4_

  - [ ] 9.4 增加优雅关闭超时可配置
    - 在 `appsettings.json` 中新增 `DataPersistence.FlushTimeoutSeconds` 配置项（默认 10）
    - 修改 `BatchWriteService.Dispose()` 中的 `FlushRemaining()` 使用配置超时
    - 超时后将未写入数据持久化到死信队列
    - 记录 Flush 统计日志
    - _Requirements: 12.1, 12.2, 12.3, 12.4_

  - [ ] 9.5 在 UI 关闭时显示数据保存进度提示
    - 修改 `MainWindow.xaml` 关闭事件处理
    - 显示"正在保存数据..."进度对话框
    - _Requirements: 12.5_

- [ ] 10. 实现 MES/云端同步机制（P3 - R7）
  - [ ] 10.1 实现 CloudSyncService 核心功能
    - 在 `MotorTestSystem.Services` 中创建 `ICloudSyncService.cs` 接口
    - 创建 `CloudSyncService.cs` 实现类
    - 实现 `SyncLoopAsync`：扫描 SyncQueue 表的 Pending/Failed 记录
    - 实现 `SyncOneAsync`：序列化记录并 POST 到 MES API
    - 使用 `SemaphoreSlim` 实现限速（每秒 50 条）
    - 失败后递增 RetryCount，最多重试 10 次
    - _Requirements: 10.3, 10.4, 10.5, 10.6, 10.7_

  - [ ] 10.2 在 BatchWriteService 中自动创建 SyncQueue 记录
    - 修改 `BatchWriteService.ProcessQueueAsync` 方法
    - 测试数据写入成功后，在 SyncQueue 中插入待同步记录（SyncStatus=Pending）
    - _Requirements: 10.2_

  - [ ] 10.3 集成 CloudSyncService 到 BackendRuntime
    - 在 `BackendRuntime.cs` 中根据 `CloudSyncEnabled` 配置启动 `CloudSyncService`
    - 确保 Dispose 时正确停止同步服务
    - _Requirements: 10.8_

  - [ ] 10.4 在 UI 监控面板中显示云端同步状态
    - 在 `DashboardViewModel.cs` 中新增同步队列积压数量和最后同步时间属性
    - 在监控面板中显示同步指标
    - _Requirements: 10.9_

  - [ ] 10.5 添加云端同步配置项
    - 在 `appsettings.json` 中新增 `DataPersistence.CloudSyncEnabled`（默认 false）
    - 新增 `DataPersistence.CloudSyncEndpoint` 配置项
    - _Requirements: 10.8_

- [ ] 11. 实现 PLC 断网数据完整性检测（P3 - R10）
  - [ ] 11.1 在 StationSnapshot 中增加序列号字段
    - 修改 `StationSnapshot.cs` 增加 `SequenceNumber` 可空字段
    - 在 PlcPollingService 中从 PLC 读取序列号（如果协议支持）
    - _Requirements: 11.1_

  - [ ] 11.2 实现序列号跳跃检测
    - 在 `PlcPollingService.cs` 中跟踪每个工位的上一次序列号
    - PLC 重连后检测序列号是否连续（gap > 1）
    - 检测到跳跃时记录警告日志并标记数据丢失
    - _Requirements: 11.2, 11.3_

  - [ ] 11.3 在 UI 中显示数据丢失告警
    - 在 `MonitorViewModel.cs` 中新增数据丢失告警属性
    - 在监控面板中显示黄色指示器和丢失数量
    - _Requirements: 11.4_

  - [ ] 11.4 实现 PLC 历史数据补传（S7 协议可选）
    - 为 S7 Protocol 实现历史数据查询接口（如果 PLC 支持）
    - 检测到序列号跳跃时尝试请求补传
    - 在设计文档中记录各协议的支持情况
    - _Requirements: 11.5, 11.6_

- [ ] 12. 最终集成测试和性能验证
  - [ ] 12.1 运行完整的属性测试套件
    - 执行所有 5 个属性测试（每个 100 次迭代）
    - 验证所有属性测试通过

  - [ ] 12.2 运行集成测试
    - 端到端数据流测试（6 工位并发轮询 → 批量写入 → 数据库）
    - 故障注入测试（磁盘满、数据库锁定、PLC 断网）
    - 验证数据完整性和顺序

  - [ ] 12.3 运行压力测试
    - 6 工位满负载运行 1 小时
    - 验证 P99 轮询周期 ≤1.05 秒
    - 验证写入通道占用率 <80%
    - 验证内存稳定性（增量 ≤50MB）

  - [ ] 12.4 运行性能基准测试
    - 使用 BenchmarkDotNet 验证关键操作性能
    - 单条 Upsert、批量 Upsert、通知写入等场景
    - 确认性能符合目标值

- [ ] 13. 最终 Checkpoint - 完整验证和交付准备
  - 确保所有测试通过（单元测试、属性测试、集成测试、压力测试）
  - 验证配置文件完整（appsettings.json 包含所有新增配置项）
  - 验证 UI 监控面板功能完整
  - 确认死信队列启动补传工作正常
  - 询问用户是否准备好进行生产部署

## Notes

- **优先级说明**：
  - 任务 1-4: P0 关键数据完整性保障（必须完成）
  - 任务 5-8: P1 可观测性和运维能力（应该完成）
  - 任务 9: P2 性能优化（可改进）
  - 任务 10-11: P3 云端集成和长期规划（中远期）

- **测试任务说明**：
  - 标记为 `*` 的子任务为可选测试任务，可根据时间和资源决定是否执行
  - 属性测试（Property-Based Testing）使用 FsCheck 库，每个属性至少 100 次迭代
  - 集成测试和压力测试对于验证系统鲁棒性至关重要，建议优先执行

- **向后兼容性**：
  - 所有改造保持现有架构不变（EventChannelService、BatchWriteService、PlcPollingService）
  - PLC 轮询周期必须维持 ≤1 秒（P99 ≤1.05 秒）
  - 现有 UI 视图无需大规模重构

- **分阶段部署**：
  - 每完成一个 Checkpoint，建议进行灰度部署和生产验证
  - 保留上一版本可执行文件支持快速回滚
  - 监控关键指标：轮询周期、通道占用率、死信队列数量、内存占用

- **需求溯源**：
  - 每个任务都标注了对应的 Requirements（如 `_Requirements: 1.1, 1.2_`）
  - 便于追溯需求覆盖率和变更影响分析

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.3"] },
    { "id": 1, "tasks": ["1.2", "2.1", "3.1"] },
    { "id": 2, "tasks": ["2.2", "2.3", "3.2"] },
    { "id": 3, "tasks": ["3.3", "5.1"] },
    { "id": 4, "tasks": ["3.4", "5.2", "6.1"] },
    { "id": 5, "tasks": ["5.3", "6.2", "7.1"] },
    { "id": 6, "tasks": ["5.4", "7.2", "9.1"] },
    { "id": 7, "tasks": ["7.3", "9.2", "10.1"] },
    { "id": 8, "tasks": ["7.4", "9.3", "10.2", "11.1"] },
    { "id": 9, "tasks": ["7.5", "9.4", "10.3", "11.2"] },
    { "id": 10, "tasks": ["9.5", "10.4", "11.3"] },
    { "id": 11, "tasks": ["10.5", "11.4"] },
    { "id": 12, "tasks": ["12.1", "12.2", "12.3", "12.4"] }
  ]
}
```
