# Bugfix Requirements Document

## Introduction

本次修复针对 MotorTestSystem 硬件通讯层的多个性能问题，这些问题导致 UI 线程阻塞、轮询周期抖动、应用启动延迟以及潜在的线程安全风险。问题源于：
- **DashboardViewModel** 每秒触发 6-12 次 UI 线程全量数据库查询
- **BackendRuntime** 启动时同步阻塞嵌套导致 UI 冻结 2-15 秒
- **PlcPollingService** 轮询循环内同步等待数据库写入，导致轮询周期抖动
- **非线程安全的 Dictionary** 在多线程环境下存在数据竞争风险

影响：UI 卡顿 200-500ms、应用启动"未响应"、偶发超时、潜在崩溃风险。

---

## Bug Analysis

### Current Behavior (Defect)

#### 1. UI 线程洪泛问题

1.1 WHEN 任意工位产生快照事件（6工位 × 1-2次/秒 = 6-12次/秒）THEN DashboardViewModel 通过 `Dispatcher.InvokeAsync` 在 UI 线程执行完整的数据库查询（`RefreshAllDataAsync` 包含 KPI聚合、时序查询、分组统计）

1.2 WHEN 数据库查询的 await 延续默认回到 UI 线程的 SynchronizationContext THEN UI 线程被大量异步延续阻塞，消息泵响应变慢

1.3 WHEN UI 线程负载过高（每秒 6-12 次数据库查询）THEN 产生 UI 卡顿 200-500ms，窗口响应延迟，Dispatcher 队列处理效率下降

#### 2. 启动时同步阻塞问题

2.1 WHEN `BackendRuntime.CreateDefault()` 首次被访问（应用启动时）THEN `Task.Run(() => SeedRepositoryIfEmptyAsync(...).GetAwaiter().GetResult()).Wait()` 产生双重同步阻塞

2.2 WHEN 历史数据播种需要处理 3000+ 条记录（每次 `UpsertStageResultAsync` 含数据库读写）THEN 耗时 2-15 秒期间线程池线程被 `.GetAwaiter().GetResult()` 同步阻塞

2.3 WHEN 外层 `.Wait()` 在 UI 线程上被调用 THEN UI 线程被卡住数秒，用户看到"未响应"白屏

#### 3. 轮询循环阻塞问题

3.1 WHEN `PollStationAsync` 轮询循环检测到完成信号 THEN `await _repository.UpsertStageResultAsync(data)` 同步等待 SQLite 写入完成

3.2 WHEN SQLite 写入因锁竞争或 WAL checkpoint 变慢（1-50ms）THEN 整个 500ms 轮询周期被拉长

3.3 WHEN 6个工位全部满负荷运行，每秒产生 6 条完成记录 THEN 写入延迟累积（50ms×6=300ms）+ 事件传播（30ms）+ PLC通信（60ms）导致理论负载 ≈ 39% CPU 时间投入 IO 等待

3.4 WHEN SQLite 发生 checkpoint（默认每 1000 页）THEN 产生瞬间毛刺，轮询延迟抖动 100-200ms，可能导致下一轮周期到达时来不及处理完上一轮数据

#### 4. 线程安全问题

4.1 WHEN 6 个独立的 `PollStationAsync` 任务同时读写 `Dictionary<string, int> _consecutiveFailures` THEN 尽管每个工位只读写自己的 key，但 `Dictionary<TKey, TValue>` 在多线程并发写入不同 key 时仍存在数据竞争

4.2 WHEN Dictionary 内部数据结构（bucket 数组）被并发修改 THEN 可能导致数据结构破坏

4.3 WHEN 扩容操作与并发读取冲突 THEN 可能导致无限循环（.NET 已知问题）

4.4 WHEN 执行 `if (!ContainsKey(...)) ++_consecutiveFailures[id]` THEN 非原子性操作存在竞态条件

#### 5. 冗余线程池调度问题

5.1 WHEN `PlcPollingService.Start()` 使用 `Task.Run(() => PollStationAsync(...))` 包装 async 方法 THEN lambda 被投递到线程池并同步执行到第一个 await 之前

5.2 WHEN `PollStationAsync` 方法体第一行即为 `await Task.Delay(...)` THEN 在第一个 await 后控制权立即返回，`Task.Run` 的线程分派毫无意义

5.3 WHEN 每个工位启动时都经过这个冗余包装 THEN 产生 6 次不必要的线程池上下文切换（每次约 2-15μs）

#### 6. S7 PLC 重复 TCP 往返问题

6.1 WHEN `S7PlcClient.ReadSnapshotAsync` 执行三次独立的 `await _plc.ReadAsync()` 调用 THEN 产生 3 次独立的 TCP 协议交互，每次都是完整的请求-响应 round-trip

6.2 WHEN 每次读取都需要持有 `_lock` (SemaphoreSlim) 并等待网络 IO THEN 锁争用 × 3，S7 工位通信时间被放大

6.3 WHEN S7NetPlus 支持 `ReadMultipleVarsAsync()` 批量读取接口 THEN 当前实现未使用该优化，导致通信效率降低 50-66%

#### 7. 锁持有时间过长问题

7.1 WHEN `ModbusTcpClient.ReadSnapshotAsync` 在 `_lock.WaitAsync()` 保护范围内执行网络 IO + 数据解析 + 业务判定 THEN 锁持有时间约 5-20ms

7.2 WHEN 轮询间隔压缩到 500ms 且锁持有时间占比 1-4% THEN 虽然单连接瓶颈不高，但存在低效设计的优化空间

---

### Expected Behavior (Correct)

#### 1. UI 线程洪泛修复

2.1 WHEN 任意工位产生快照事件 THEN DashboardViewModel SHALL 将事件通过无阻塞的 `Channel<T>.Writer.TryWrite()` 发送到后台消费者

2.2 WHEN 后台消费者处理 Dashboard 更新 THEN SHALL 每 2 秒最多刷新一次，丢弃中间帧，只保留最新状态

2.3 WHEN 需要刷新 UI 时 THEN SHALL 通过 `Dispatcher.InvokeAsync` 调用 `RefreshAllDataAsync`，但频率从 6-12次/秒 降至 0.5次/秒

2.4 WHEN Dashboard 刷新频率降低后 THEN SHALL 使 UI 线程负载下降 90%+，消除 200-500ms 卡顿

#### 2. 启动阻塞修复

2.5 WHEN 应用启动需要访问 `BackendRuntime` 单例 THEN SHALL 使用惰性异步初始化 `Lazy<Task<BackendRuntime>>` 替代静态构造器

2.6 WHEN 调用 `BackendRuntime.GetSharedAsync()` THEN SHALL 返回 `Task<BackendRuntime>` 而非同步阻塞等待

2.7 WHEN `App.xaml.cs` 启动流程执行 THEN SHALL `await GetSharedAsync()` 以异步方式完成初始化，彻底消除同步阻塞

2.8 WHEN 历史数据播种执行 THEN SHALL 不阻塞 UI 线程，应用启动保持响应

#### 3. 轮询循环解耦修复

2.9 WHEN `PollStationAsync` 检测到完成信号 THEN SHALL 将 `StageTestData` 通过 `Channel<T>.Writer.WriteAsync()` 发送到写缓冲区

2.10 WHEN 写缓冲区接收到数据 THEN SHALL 由独立的写消费者线程处理，采用批量化写入（最多等待 100ms 收集最多 50 条记录）

2.11 WHEN 写消费者调用数据库写入 THEN SHALL 使用新的 `IMotorTestRepository.BulkUpsertAsync()` 方法执行批量事务

2.12 WHEN 轮询循环不再等待 DB IO THEN SHALL 缩短到纯 PLC 通信时间（~10ms），消除轮询延迟抖动

2.13 WHEN 使用批量事务写入 THEN SHALL 使 SQLite 写入吞吐量提升 3-10 倍

#### 4. 线程安全修复

2.14 WHEN 多个 `PollStationAsync` 任务并发访问失败计数器 THEN SHALL 使用 `ConcurrentDictionary<string, int>` 替代 `Dictionary<string, int>`

2.15 WHEN 执行失败计数递增 THEN SHALL 使用 `Interlocked.Increment(ref _consecutiveFailures.GetOrAdd(id, 0))` 或 `TryGetValue + TryUpdate` 模式确保原子性

2.16 WHEN 执行失败计数重置 THEN SHALL 使用 `_consecutiveFailures.TryRemove(id, out _)` 确保线程安全

2.17 WHEN 使用 ConcurrentDictionary 后 THEN SHALL 消除多线程数据竞争风险，防止潜在崩溃

#### 5. 冗余调度修复

2.18 WHEN `PlcPollingService.Start()` 启动轮询任务 THEN SHALL 直接调用 `_pollingTasks.Add(PollStationAsync(client, token))` 而非使用 `Task.Run` 包装

2.19 WHEN 移除 Task.Run 包装后 THEN SHALL 每工位省去一次线程池上下文切换（约 2-15μs），同时保持 `_pollingTasks` 跟踪以确保 dispose 时能正确 await

#### 6. S7 批量读取修复

2.20 WHEN `S7PlcClient.ReadSnapshotAsync` 需要读取多个数据区 THEN SHALL 使用 `_plc.ReadMultipleVarsAsync(items)` 一次性读取所有数据项（M100.0, DB1.DBW100-106, Barcode）

2.21 WHEN 使用批量读取后 THEN SHALL 一次 TCP 往返替代三次独立读取，S7 工位通信时间减少 50-66%

#### 7. 锁粒度优化修复

2.22 WHEN `ModbusTcpClient.ReadSnapshotAsync` 执行 THEN SHALL 锁只保护 `NetworkStream` 的 `ReadAsync/WriteAsync` 调用，数据解析和业务判定逻辑移到锁外

2.23 WHEN 锁粒度缩小后 THEN SHALL 减少锁持有时间，降低锁争用概率

---

### Unchanged Behavior (Regression Prevention)

#### 1. 核心功能保持不变

3.1 WHEN 工位正常轮询运行 THEN 系统 SHALL CONTINUE TO 按照配置的轮询间隔（500ms-1000ms）采集 PLC 数据

3.2 WHEN 检测到完成信号 THEN 系统 SHALL CONTINUE TO 正确写入测试结果到数据库并触发 `SnapshotReceived` 事件

3.3 WHEN Dashboard、Monitor、History 视图订阅事件 THEN 系统 SHALL CONTINUE TO 正确传播快照数据到各个 ViewModel

3.4 WHEN 用户在 UI 上查看实时数据 THEN 系统 SHALL CONTINUE TO 显示正确的工位状态、KPI 统计、故障分布等信息

#### 2. 错误处理保持不变

3.5 WHEN PLC 通信失败 THEN 系统 SHALL CONTINUE TO 记录连续失败次数并在达到阈值时触发断线事件

3.6 WHEN 数据库操作失败 THEN 系统 SHALL CONTINUE TO 抛出异常或记录日志，不静默吞掉错误

3.7 WHEN 取消令牌被触发 THEN 系统 SHALL CONTINUE TO 正确停止轮询任务并释放资源

#### 3. 数据一致性保持不变

3.8 WHEN 多个工位并发写入数据 THEN 系统 SHALL CONTINUE TO 保证每条测试结果的完整性和正确性

3.9 WHEN 查询历史数据 THEN 系统 SHALL CONTINUE TO 返回与修复前相同的数据集（假设时间范围和过滤条件相同）

3.10 WHEN 执行 Upsert 操作 THEN 系统 SHALL CONTINUE TO 正确处理新增和更新的逻辑分支

#### 4. 配置和扩展性保持不变

3.11 WHEN 系统配置了不同的 PLC 客户端类型（Modbus/Melsec/S7/Mock）THEN 系统 SHALL CONTINUE TO 通过工厂模式创建对应的客户端实例

3.12 WHEN 添加新的工位配置 THEN 系统 SHALL CONTINUE TO 支持动态启动和停止轮询任务

3.13 WHEN 用户自定义轮询间隔 THEN 系统 SHALL CONTINUE TO 按照配置的间隔执行轮询

#### 5. UI 响应性（非降频场景）

3.14 WHEN MonitorViewModel 或其他 ViewModel 处理快照事件且未采用降频策略 THEN 系统 SHALL CONTINUE TO 保持实时更新特性

3.15 WHEN 用户操作 UI 控件（按钮点击、输入框输入）THEN 系统 SHALL CONTINUE TO 立即响应（修复后响应性应更好，但至少不能变差）

#### 6. 生命周期管理保持不变

3.16 WHEN `PlcPollingService.Stop()` 被调用 THEN 系统 SHALL CONTINUE TO 正确取消所有轮询任务并等待它们完成

3.17 WHEN `BackendRuntime` 被 Dispose THEN 系统 SHALL CONTINUE TO 正确释放 PlcPollingService、Repository、DbContext 等资源

3.18 WHEN 应用关闭时 THEN 系统 SHALL CONTINUE TO 确保所有待写入数据已刷新到数据库（修复后需特别注意 Channel 中的缓冲数据）
