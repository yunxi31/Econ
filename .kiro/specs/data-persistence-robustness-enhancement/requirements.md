# Requirements Document

## Introduction

本文档定义了 MotorTestSystem 工业上位机系统数据持久化层改造的功能需求。系统当前采用 WPF + SqlSugar ORM + SQLite 架构，从 6 个工位的 PLC（Modbus TCP/S7/MC 协议）采集电机测试数据并存储到本地数据库。

根据 `review-report-data-persistence.md` 审查报告，现有实现存在 10 个风险点（R1-R10），分为 P0（必须修复）、P1（应修复）、P2（可改进）、P3（中远期规划）四个优先级。本改造旨在修复所有已识别风险，确保工业现场网络波动、磁盘 IO 饱和、断电等极端场景下的数据完整性和系统实时性。

**关键约束：**
- 必须保持 PLC 轮询周期 ≤1 秒，不能被 DB 操作阻塞
- 必须向后兼容现有的 PLC 轮询、EventChannel、BatchWriteService 架构
- 关键测试数据不能丢失，需要持久化重试机制
- 所有修改必须在不停产的情况下平滑部署

## Glossary

- **System**: MotorTestSystem 上位机应用程序
- **PLC_Polling_Service**: 负责从 PLC 采集数据的后台服务，每个工位一个独立的 Task，轮询周期 1 秒
- **Event_Channel_Service**: 内存通道服务，包含 `_writeChannel`（有界通道，容量 500）和 `_snapshotChannel`（无界通道）
- **Batch_Write_Service**: 后台批量写入服务，从 `_writeChannel` 读取数据并聚合（100ms 或 50 条触发）后写入数据库
- **Notification_Service**: 通知服务，负责将测试完成事件写入 Notifications 表
- **User_Service**: 用户管理服务，负责用户 CRUD 操作
- **Repository**: 数据访问层，封装 SqlSugar ORM 的数据库操作
- **Dead_Letter_Queue**: 死信队列，持久化存储写入失败的数据批次，用于后续重试
- **Sync_Queue**: 同步队列表，记录需要上传到 MES/云端的数据及其同步状态
- **MES**: Manufacturing Execution System，制造执行系统
- **Round_Trip_Property**: 往返属性，指序列化后再反序列化应得到等价对象的测试属性
- **SQLite_Connection_Pool**: SQLite 连接池，管理长连接以避免频繁创建/销毁连接

## Requirements

### Requirement 1: 事务保护单条 Upsert 操作（修复 R1 - P0）

**User Story:** 作为系统管理员，我希望单条测试结果的 Upsert 操作具有原子性保护，以便在并发写入场景下避免数据重复或主键冲突。

#### Acceptance Criteria

1. WHEN `UpsertStageResultAsync` 执行时，THE Repository SHALL 在事务中包装 select 和 insert/update 操作
2. THE Repository SHALL 确保同一 Barcode 的并发 Upsert 操作串行化，避免两个线程同时判断"数据不存在"并重复插入
3. IF 事务执行失败，THEN THE Repository SHALL 回滚所有变更并抛出异常
4. THE Repository SHALL 使用 `UseTranAsync` API 实现事务包装

### Requirement 2: 解耦通知服务的同步数据库写入（修复 R2 - P0）

**User Story:** 作为 PLC 轮询线程，我希望通知服务的数据库写入不阻塞我的轮询周期，以便保持 1 秒的实时数据采集频率。

#### Acceptance Criteria

1. THE Notification_Service SHALL 提供异步队列写入接口，接收通知项后立即返回
2. WHEN `OnSnapshotReceivedForNotification` 事件触发时，THE System SHALL 将通知项写入无界内存通道而非直接调用同步 `Add()` 方法
3. THE System SHALL 启动独立的后台消费者 Task，从通知队列中批量读取并写入数据库
4. THE Notification_Service SHALL 使用 `System.Threading.Channels.Channel<NotificationItem>` 实现异步队列
5. WHEN 后台消费者写入数据库时，THE System SHALL 批量插入（每批最多 50 条或间隔 100ms）
6. IF 通知队列写入失败，THEN THE System SHALL 记录错误日志但不阻塞 PLC 轮询线程

### Requirement 3: 批量写入增加重试与死信队列（修复 R3 - P0）

**User Story:** 作为数据完整性保障机制，我希望批量写入失败时自动重试，重试失败后持久化到死信队列，以便在磁盘 IO 饱和或数据库锁定场景下不丢失关键测试数据。

#### Acceptance Criteria

1. WHEN `BulkUpsertAsync` 抛出异常时，THE Batch_Write_Service SHALL 使用指数退避策略重试（延迟：1s, 2s, 4s）最多 3 次
2. IF 重试 3 次后仍失败，THEN THE Batch_Write_Service SHALL 将批次数据序列化为 JSON 文件并写入死信队列目录
3. THE Dead_Letter_Queue SHALL 存储在 `{AppDomain.BaseDirectory}/Data/DeadLetters/{timestamp}_{guid}.json` 路径
4. THE Batch_Write_Service SHALL 为每个死信文件记录元数据（时间戳、异常信息、数据条数）
5. WHEN 系统启动时，THE System SHALL 扫描死信队列目录并自动补传所有待处理文件
6. WHEN 死信文件补传成功后，THE System SHALL 删除该文件
7. IF 死信文件连续补传失败 5 次，THEN THE System SHALL 将其重命名为 `.failed` 后缀并记录警告日志

### Requirement 4: 监控写入通道容量并告警（修复 R4 - P1）

**User Story:** 作为系统运维人员，我希望在写入通道缓冲区接近满载时收到告警，以便及时发现数据库写入瓶颈并避免静默丢弃数据。

#### Acceptance Criteria

1. THE Event_Channel_Service SHALL 提供实时缓冲区水位查询接口 `GetWriteChannelUtilization()`，返回当前占用百分比
2. WHEN `_writeChannel` 占用率 ≥ 80% 时，THE System SHALL 每 5 秒记录一次警告日志
3. WHEN `_writeChannel` 占用率 ≥ 95% 时，THE System SHALL 触发 UI 告警通知（红色指示器 + 文本提示）
4. WHEN `_writeChannel` 因 DropOldest 策略丢弃数据时，THE System SHALL 增加丢弃计数器并在 UI 上显示累计丢弃数量
5. THE System SHALL 提供配置项 `WriteChannelCapacity`，默认值从 500 调整为 2000

### Requirement 5: 优化通知服务和用户服务为全异步接口（修复 R8 - P1）

**User Story:** 作为 UI 线程，我希望通知服务和用户服务提供完全异步的数据库操作接口，以便避免长时间阻塞导致界面卡顿。

#### Acceptance Criteria

1. THE Notification_Service SHALL 将所有 `ExecuteCommand()` 调用替换为 `await ExecuteCommandAsync()`
2. THE User_Service SHALL 将所有同步数据库操作方法改为异步方法（`CreateAsync`, `UpdateAsync`, `DeleteAsync`）
3. WHEN UI 线程调用服务方法时，THE System SHALL 使用 `await dispatcher.InvokeAsync()` 配合异步路径
4. THE System SHALL 确保所有异步方法正确传播 `CancellationToken`
5. THE System SHALL 移除所有 `dispatcher.Invoke()` 同步调用（改为 `InvokeAsync`）

### Requirement 6: 实现死信队列启动自动补传（P1 补充）

**User Story:** 作为数据完整性保障机制，我希望系统启动时自动扫描并补传死信队列中的历史失败数据，以便恢复因临时故障丢失的测试记录。

#### Acceptance Criteria

1. WHEN `BackendRuntime` 初始化时，THE System SHALL 在启动 `BatchWriteService` 之前先执行死信队列扫描
2. THE System SHALL 按文件时间戳顺序从旧到新处理死信文件
3. WHEN 补传单个文件时，THE System SHALL 反序列化 JSON 并调用 `BulkUpsertAsync`
4. IF 补传失败，THEN THE System SHALL 保留该文件并递增失败计数器（存储在文件名或元数据中）
5. THE System SHALL 在启动日志中记录补传统计（成功 X 个文件，失败 Y 个文件，共 Z 条记录）
6. THE System SHALL 提供配置项 `MaxDeadLetterRetries`（默认值 5）和 `DeadLetterScanOnStartup`（默认值 true）

### Requirement 7: 调整 SQLite 连接策略为长连接（修复 R6 - P2）

**User Story:** 作为数据库性能优化机制，我希望 SqlSugar 使用长连接而非每次操作都创建新连接，以便减少连接创建/销毁开销并提升预编译语句缓存效益。

#### Acceptance Criteria

1. THE System SHALL 在 `SqlSugarDbContext` 初始化时设置 `IsAutoCloseConnection = false`
2. THE System SHALL 在应用程序关闭时显式调用 `Db.Dispose()` 关闭连接
3. THE System SHALL 确保长连接模式下不会因连接泄漏导致 SQLite 文件锁定
4. THE System SHALL 在 `BackendRuntime.Dispose()` 中正确释放所有数据库连接

### Requirement 8: 评估并实现真正的 SQL 批量 INSERT（修复 R5 - P2）

**User Story:** 作为批量写入性能优化机制，我希望评估 SqlSugar + SQLite 是否可以改进为真正的多行批量 INSERT，以便减少 IO 次数并提升吞吐量。

#### Acceptance Criteria

1. THE System SHALL 调研 SqlSugar 5.x 是否支持 SQLite 的多行 `INSERT INTO ... VALUES (...), (...), (...)` 语法
2. IF SqlSugar 不支持，THEN THE System SHALL 评估直接使用 ADO.NET 实现批量 INSERT 的可行性
3. THE System SHALL 实现性能对比测试：当前 N 条独立 INSERT vs 真正的批量 INSERT（如果可行）
4. IF 批量 INSERT 带来显著性能提升（>30%），THEN THE System SHALL 在 `BulkUpsertAsync` 中使用新实现
5. THE System SHALL 在设计文档中记录评估结果和最终决策

### Requirement 9: 增加 SQLite 同步策略可配置（修复 P2 第 8 项）

**User Story:** 作为数据库性能调优机制，我希望 SQLite 的 flush 策略可配置，以便根据数据安全需求和性能需求权衡 `PRAGMA synchronous` 设置。

#### Acceptance Criteria

1. THE System SHALL 提供配置项 `SQLiteSyncMode`，可选值为 `NORMAL`（默认）、`FULL`、`OFF`
2. WHEN `SqlSugarDbContext` 初始化时，THE System SHALL 根据配置执行 `PRAGMA synchronous = {value}`
3. THE System SHALL 在配置文件中添加注释说明三种模式的权衡：
   - `NORMAL`: 默认，检查点时刷新，平衡性能与安全
   - `FULL`: 每次事务刷新，最安全但最慢
   - `OFF`: 关闭同步，最快但 crash 时可能丢失最后几秒数据（journal 机制仍提供基本保障）
4. THE System SHALL 在生产环境默认使用 `NORMAL` 模式

### Requirement 10: 增加 MES/云端同步机制（修复 R7 - P3）

**User Story:** 作为生产管理人员，我希望系统支持将本地测试数据同步到 MES 或云平台，以便实现远程监控和数据分析。

#### Acceptance Criteria

1. THE System SHALL 在数据库中创建 `SyncQueue` 表，字段包含：RecordId, SyncStatus（Pending/Syncing/Synced/Failed）, RetryCount, LastAttempt, CreatedAt
2. WHEN 测试数据写入 MotorTestRecords 后，THE System SHALL 自动在 SyncQueue 中创建对应的待同步记录
3. THE System SHALL 启动独立的后台同步服务 `CloudSyncService`，按时间顺序扫描 Pending 状态的记录
4. WHEN 网络正常时，THE Cloud_Sync_Service SHALL 批量上传记录到配置的 MES/云端 HTTP API
5. IF 上传成功，THEN THE Cloud_Sync_Service SHALL 更新 SyncStatus 为 Synced
6. IF 上传失败，THEN THE Cloud_Sync_Service SHALL 递增 RetryCount 并使用指数退避重试（最多 10 次）
7. THE Cloud_Sync_Service SHALL 提供限速保护，每秒最多上传 50 条记录，避免压垮远程接口
8. THE System SHALL 提供配置项 `CloudSyncEnabled`（默认 false）和 `CloudSyncEndpoint`
9. THE System SHALL 在 UI 上显示同步队列积压数量和最后同步时间

### Requirement 11: 增加 PLC 断网数据完整性检测（修复 R10 - P3）

**User Story:** 作为数据完整性保障机制，我希望系统能检测 PLC 断网期间可能丢失的数据，并在 PLC 支持的情况下请求补传。

#### Acceptance Criteria

1. THE System SHALL 在 StationSnapshot 中增加序列号字段 `SequenceNumber`（如果 PLC 协议支持）
2. WHEN PLC 重连后，THE PLC_Polling_Service SHALL 检测序列号是否连续
3. IF 检测到序列号跳跃（gap > 1），THEN THE System SHALL 记录警告日志并标记可能的数据丢失
4. THE System SHALL 在 UI 上显示每个工位的数据丢失告警（黄色指示器 + 丢失数量）
5. WHERE PLC 支持历史数据查询接口，THE System SHALL 尝试请求补传丢失的序列号范围
6. THE System SHALL 在设计文档中说明当前 PLC 协议（Modbus TCP/S7/MC）对离线数据补传的支持情况

### Requirement 12: 增加优雅关闭超时时间可配置（修复 R9 - P3）

**User Story:** 作为系统管理员，我希望应用程序关闭时的数据 Flush 超时时间可配置，以便在关键场景下等待更长时间以避免数据丢失。

#### Acceptance Criteria

1. THE System SHALL 提供配置项 `FlushTimeoutSeconds`，默认值从 2 秒调整为 10 秒
2. WHEN `BatchWriteService.Dispose()` 调用 `FlushRemaining()` 时，THE System SHALL 使用配置的超时时间
3. IF Flush 超时，THEN THE System SHALL 将未写入的数据持久化到死信队列
4. THE System SHALL 在关闭日志中记录 Flush 统计（成功写入 X 条，超时遗留 Y 条，已保存到死信队列）
5. THE System SHALL 在 UI 关闭对话框中显示"正在保存数据..."进度提示

### Requirement 13: 增加数据持久化监控仪表盘（P1 补充）

**User Story:** 作为系统运维人员，我希望在 UI 上查看数据持久化层的关键指标，以便实时监控系统健康状态。

#### Acceptance Criteria

1. THE System SHALL 在 UI 上增加"数据持久化监控"面板，显示以下指标：
   - 写入通道占用率（百分比 + 实时曲线图）
   - 批量写入成功/失败计数
   - 死信队列文件数量
   - 通知队列积压数量
   - 云端同步队列积压数量（如果启用）
   - 最后写入时间戳
2. THE System SHALL 每 1 秒刷新一次监控面板数据
3. WHEN 任一指标异常时（通道占用率 > 80%、死信队列 > 10 个文件、同步积压 > 1000 条），THE System SHALL 在面板上显示红色告警标识
4. THE System SHALL 提供"手动触发死信队列补传"按钮，运维人员可点击立即执行补传

### Requirement 14: Parser 和 Serializer 需求（通用模式）

**User Story:** 作为数据序列化/反序列化机制，我希望系统正确解析和格式化死信队列的 JSON 文件，并保证往返一致性。

#### Acceptance Criteria

1. THE System SHALL 实现 `DeadLetterParser` 类，解析死信队列的 JSON 文件为 `List<StageTestData>` 对象
2. WHEN JSON 文件格式无效时，THE Dead_Letter_Parser SHALL 返回描述性错误（包含文件路径和解析位置）
3. THE System SHALL 实现 `DeadLetterSerializer` 类，将 `List<StageTestData>` 对象格式化为标准 JSON 文件
4. FOR ALL 有效的 `List<StageTestData>` 对象，THE System SHALL 保证往返属性：`Parse(Serialize(data)) == data`（Round-Trip Property）
5. THE System SHALL 在死信队列的单元测试中包含往返属性测试，使用 100 个随机生成的测试数据批次

## 补充说明

### 向后兼容性要求

所有改造必须确保：
1. 不破坏现有的 `EventChannelService` 和 `BatchWriteService` 架构
2. 不影响 PLC 轮询线程的 1 秒周期（允许 ±5% 抖动）
3. 现有的 UI 视图（Dashboard、Monitor、History）无需大规模重构

### 性能基准

改造完成后，系统应满足以下性能基准：
- PLC 轮询周期：≤1.05 秒（P99）
- 批量写入吞吐量：≥100 条/秒（正常负载）
- 死信队列补传速度：≥50 条/秒
- UI 响应时间：≤200ms（P95）
- 内存占用增量：≤50MB（相比改造前）

### 测试覆盖要求

所有 P0 和 P1 需求必须包含：
- 单元测试（覆盖率 ≥80%）
- 集成测试（覆盖主要数据流路径）
- 压力测试（模拟 6 工位同时满负载运行 1 小时）
- 故障注入测试（磁盘满、网络断开、数据库锁定）
