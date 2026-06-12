# 项目记忆 - MotorTestSystem

## 角色体系 & RBAC
- 3 个角色：管理员 (Admin)、操作员 (Operator)、维护员 (Maintainer)
- RBAC 权限模型已实现：AppPermission 枚举 + RolePermissions 静态映射
- 权限矩阵：
  - Admin: 全部权限（含用户管理、系统配置）
  - Operator: 监控、设备控制、看板、追溯、数据导出
  - Maintainer: 监控、看板、追溯、诊断、校准、报警、数据导出
- 导航项根据权限动态显示/隐藏（MainViewModel.IsXxxVisible + BoolToVisibilityConverter）
- 认证流程：LoginWindow → AuthService.Login() → App.SetAuthenticatedUser() → MainViewModel 刷新权限
- 密码存储：SHA256 哈希（SqlSugarUserService.HashPassword + InMemoryUserService.HashPassword）
- 种子用户 16 个（管理员3 + 操作员8 + 维护员5）

## 技术栈
- WPF (.NET 8/10)，MVVM 架构（CommunityToolkit.Mvvm）
- 后端运行时单例：BackendRuntime.Shared（包含 DbContext + Repository + UserService + AuthService + PollingService）
- 用户管理已对接 IUserService，右侧权限面板动态绑定

## 数据层 — SQLite + SqlSugar
- ORM：SqlSugarCore 5.1.4.214，数据库：SQLite（Data/MotorTest.db）
- SqlSugarDbContext：管理连接 + CodeFirst 建表 + 种子数据（仅在表为空时插入）
- 数据库实体类（Models/Entities/）：
  - MotorTestRecordEntity（自增主键 + 条码业务键）
  - UserEntity（枚举 Role/Status 存为 int）
  - StationConfigEntity
  - NotificationEntity
- SqlMotorTestRepository：实现 IMotorTestRepository，Entity↔Model 转换
  - ⚠️ 已修复：UpsertStageResultAsync 中删除了多余的空 Updateable（会清空已有数据）
  - Upsert 流程：先查询 → 存在则更新 → 不存在则插入
- SqlSugarUserService：实现 IUserService，枚举 int 存储，ID 自增序列从数据库推导
- 工位配置从数据库加载（非硬编码），BackendRuntime 构造函数新增 DbContext 参数
- 旧 InMemory 实现保留但不再使用（InMemoryMotorTestRepository / InMemoryUserService）
- 密码存储：SHA256 哈希（HashPassword 方法在 InMemoryUserService 和 SqlSugarUserService 均有实现）

## Dashboard 后端
- 数据全部从 IMotorTestRepository 动态获取，无硬编码 Mock 数据
- 订阅 PlcPollingService.SnapshotReceived 事件 + 5秒定时器双驱动刷新
- 环形进度条通过 DashboardView.xaml.cs code-behind + PropertyChanged 监听更新（因 WPF DoubleCollection 类型限制无法直接绑定）
- 故障分类规则：空载电流>2.5→电流超限，噪音差>15→噪声过大，负载电流>3.0→电流超限 等
- 日产量目标常量：DailyTarget = 2000，良率目标：TargetPassRate = 98%

## 数据追溯 - 报告与打印
- 打印追溯单：异步打印（XpsDocumentWriter.WriteAsync），TraceDocumentBuilder 构建白底打印友好格式
- 打印超时30秒自动取消，进度浮层（IsPrinting/PrintStatus 属性驱动）
- `PrintQueue.CreateXpsDocumentWriter(printQueue)` 是静态方法，不是实例方法
- 查看完整报告：MotorReportWindow 暗色主题窗口，8章节（基本信息/空载/噪音/负载/阈值判定/波形图/结论/签章）
- 阈值标准：空载电流≤1.5A，空载转速2900~3100r/min，噪音≤60dB，负载电流≤4.5A，负载转速2900~3100r/min
- MotorTestRecordModel 已扩展：含 ShaftLength/KnurlDiameter/NoiseDiff 及各阶段判定结果
- LiveCharts 波形图数据在 HistoryViewModel 中生成，通过 SetWaveformData() 传递给报告窗口

## PLC 通信协议
- IPlcClient 接口：ConnectAsync / ReadSnapshotAsync / ResetCompletionSignalAsync
- PlcClientFactory 根据协议字符串创建对应客户端
- 已实现协议：
  - ModbusTCP → ModbusTcpClient（手写 TCP 帧通信）
  - MelsecMC / MC Protocol → MelsecMcClient（三菱 MC 协议帧）
  - S7 Protocol (TCP) → S7PlcClient（S7netPlus 0.20.0 库）
- S7PlcClient 地址映射：M100.0 完成信号，DB1.DBW100+ 测试数据，DB1.DBD200 条码
- S7netPlus 构造函数：`Plc(CpuType, ip, port, short rack, short slot)`
- S7netPlus ReadAsync 返回 object?，需安全拆箱处理
- 工位 A2 (S7-1200) 和 A5 (S7-1500) 使用 S7 协议

## 通知中心
- INotificationService 接口 + SqlSugarNotificationService 数据库实现
- NotificationItem 模型（NotificationType枚举：Alarm/Maintenance/System，NotificationSeverity枚举：Info/Warning/Critical）
- NotificationItemViewModel UI包装类（XAML绑定用）
- BackendRuntime 订阅 PlcPollingService 事件自动生成通知：工位离线→报警，NG结果→报警（5min冷却），PLC错误日志→报警
- XAML DataTrigger 绑定 TypeDisplay（中文），非 Type（枚举）
- 种子数据12条（3报警+4维护+5系统），通过 SqlSugarDbContext.SeedNotifications() 首次建表时播种
- SqlSugarNotificationService：内存 ObservableCollection + 数据库双向同步，Add/Remove/MarkAsRead/MarkAllAsRead/ClearAll 均同步写入 DB
- DashEffect 用法：`new DashEffect(new float[] { 6, 4 }, 0)`，需 using `LiveChartsCore.SkiaSharpView.Painting.Effects`
- 不要用 `SKPathEffect.CreateDash()`——那是 SkiaSharp 原生类型，与 LiveCharts 的 PathEffect 不兼容
