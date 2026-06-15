# Implementation Plan: MVVM 架构重构

## Overview

将 MotorTestSystem 从当前存在严重架构问题的代码库重构为符合 MVVM 最佳实践的标准架构。采用分阶段增量重构策略，每个阶段保持系统可编译、可运行、可回滚。

重构分为三个阶段：
- **P0 (立即修复)**: 解除 ViewModel-View 反向依赖，创建对话框和调度服务抽象
- **P1 (短期优化)**: 手动依赖注入，引入事件聚合器，解耦 ViewModel
- **P2 (长期演进)**: 迁移到 DI 容器，移除 BackendRuntime

## Tasks

### P0 阶段：立即修复 (解除 ViewModel-View 反向依赖)

- [ ] 1. 创建服务接口和实现
  - [ ] 1.1 创建 IDialogService 接口
    - 在 `MotorTestSystem.Services` 命名空间中创建 `IDialogService.cs`
    - 定义 `ShowMessageAsync` 方法（支持标题、按钮类型、图标）
    - 定义 `ShowSaveFileDialog` 方法（返回文件路径或 null）
    - 定义 `ShowPrintDialog` 方法（接受 FlowDocument 参数）
    - 定义 `ShowReportWindow` 方法（接受 MotorTestRecordModel 参数）
    - 定义 `ShowUserEditDialog` 方法（返回 UserEditResult 或 null）
    - 定义 `SetClipboardText` 方法
    - _Requirements: 1.7, 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7_
  
  - [ ] 1.2 创建 IDispatcherService 接口
    - 在 `MotorTestSystem.Services` 命名空间中创建 `IDispatcherService.cs`
    - 定义 `Invoke(Action)` 同步执行方法
    - 定义 `Invoke<TResult>(Func<TResult>)` 同步返回结果方法
    - 定义 `InvokeAsync(Action)` 异步执行方法
    - 定义 `InvokeAsync<TResult>(Func<TResult>)` 异步返回结果方法
    - 定义 `CheckAccess()` 方法检查是否为 UI 线程
    - _Requirements: 4.1, 4.2, 4.3, 4.4_

  - [ ] 1.3 创建 UserEditResult 数据传输对象
    - 在 `MotorTestSystem.Services` 命名空间中创建 `UserEditResult.cs`
    - 定义为 record 类型，包含 Account, Name, Role, IsEnabled 属性
    - _Requirements: 2.5_

  - [ ] 1.4 实现 WpfDispatcherService
    - 在 `MotorTestSystem.Services` 命名空间中创建 `WpfDispatcherService.cs`
    - 构造函数中获取 `Application.Current.Dispatcher`
    - 实现 `Invoke` 方法，检查线程后同步执行或调度
    - 实现 `InvokeAsync` 方法，检查线程后异步执行或调度
    - 实现 `CheckAccess` 方法，返回 `_dispatcher.CheckAccess()`
    - _Requirements: 4.5, 4.6, 2.10_

  - [ ] 1.5 实现 WpfDialogService
    - 在 `MotorTestSystem.Services` 命名空间中创建 `WpfDialogService.cs`
    - 构造函数注入 `IDispatcherService`
    - 实现 `ShowMessageAsync` 使用 `MessageBox.Show`，通过 Dispatcher 调度
    - 实现 `ShowSaveFileDialog` 使用 `SaveFileDialog`，通过 Dispatcher 调度
    - 实现 `ShowPrintDialog` 使用 `PrintDialog`，通过 Dispatcher 调度
    - 实现 `ShowReportWindow` 创建 `MotorReportWindow` 实例并设置 DataContext
    - 实现 `ShowUserEditDialog` 创建 `UserEditWindow` 和 `UserEditDialogViewModel`
    - 实现 `SetClipboardText` 使用 `Clipboard.SetText`，通过 Dispatcher 调度
    - 所有方法添加 try-catch 异常处理，记录日志但不抛出
    - _Requirements: 2.8, 2.9, 2.10_

  - [ ] 1.6 创建测试用 Mock 实现
    - 创建 `SyncDispatcherService` 类，所有方法同步执行，`CheckAccess` 返回 true
    - 创建 `MockDialogService` 类，包含可配置的返回值属性
    - 添加 `ShownMessages` 列表记录所有消息框调用
    - _Requirements: 4.11, 11.2_

- [ ] 2. Checkpoint - 验证服务接口创建
  - 确保所有接口和实现类编译通过
  - 确认 WpfDialogService 和 WpfDispatcherService 可以实例化
  - 如有问题，询问用户

- [ ] 3. 重构数据模型（移除 UI 类型依赖）
  - [ ] 3.1 重构 DefectItem 类
    - 打开 `DefectItem.cs`，移除 `ColorBrush` 属性
    - 移除 `Color` setter 中的 `BrushConverter` 调用
    - 确保 `Color` 属性仅存储字符串值
    - _Requirements: 3.1, 3.2_

  - [ ] 3.2 重构 FaultReason 类
    - 打开 `FaultReason.cs`，移除 `ColorBrush` 属性（如果存在）
    - 确保 `Color` 属性仅存储字符串值
    - _Requirements: 3.1, 3.2_

  - [ ] 3.3 创建 StringToColorBrushConverter
    - 在 `MotorTestSystem.Converters` 命名空间中创建 `StringToColorBrushConverter.cs`
    - 实现 `IValueConverter` 接口
    - `Convert` 方法：将颜色字符串转换为 Brush 对象
    - 使用 `BrushConverter` 处理转换，异常时返回 `Brushes.Gray`
    - `ConvertBack` 方法抛出 `NotImplementedException`
    - _Requirements: 3.3, 3.4_

  - [ ] 3.4 更新 DashboardView.xaml 绑定
    - 在 `DashboardView.xaml` 中添加 `StringToColorBrushConverter` 资源
    - 将所有 `{Binding ColorBrush}` 改为 `{Binding Color, Converter={StaticResource ColorConverter}}`
    - _Requirements: 3.5_

- [ ] 4. Checkpoint - 验证数据模型重构
  - 编译项目，确保 Models 项目不引用 PresentationCore
  - 运行应用，确认 Dashboard 缺陷列表颜色显示正常
  - 如有问题，询问用户

- [ ] 5. 重构 HistoryViewModel（解除 View 依赖）
  - [ ] 5.1 添加 IDialogService 构造函数参数
    - 修改 `HistoryViewModel` 构造函数，添加 `IDialogService dialogService` 参数
    - 添加私有字段 `_dialogService` 并在构造函数中赋值
    - 保留现有的 `IMotorTestRepository` 参数
    - 暂时保留无参构造函数，调用 `BackendRuntime.Shared` (向后兼容)
    - _Requirements: 5.3, 1.7_

  - [ ] 5.2 重构 Export 命令
    - 修改 `Export()` 方法，调用 `_dialogService.ShowSaveFileDialog`
    - 处理用户取消场景（返回 null）
    - 成功导出后调用 `_dialogService.ShowMessageAsync` 显示成功消息
    - 捕获 IOException 和 Exception，调用 `ShowMessageAsync` 显示错误
    - 移除所有 `MessageBox.Show` 调用
    - _Requirements: 1.8, 1.9_

  - [ ] 5.3 重构 PrintMotorReport 方法
    - 修改 `PrintMotorReport` 方法，调用 `_dialogService.ShowPrintDialog`
    - 移除 `new PrintDialog()` 创建代码
    - _Requirements: 1.10_

  - [ ] 5.4 重构 ShowReportWindow 方法
    - 修改相关方法，调用 `_dialogService.ShowReportWindow`
    - 移除 `new MotorReportWindow()` 创建代码
    - 移除 `Application.Current.MainWindow` 访问
    - _Requirements: 1.3, 1.5_

  - [ ] 5.5 重构剪贴板操作
    - 找到所有 `Clipboard.SetText` 调用
    - 替换为 `_dialogService.SetClipboardText`
    - _Requirements: 2.7_

- [ ] 6. 重构 UserViewModel（解除 View 依赖）
  - [ ] 6.1 添加 IDialogService 构造函数参数
    - 修改 `UserViewModel` 构造函数，添加 `IDialogService dialogService` 参数
    - 添加私有字段 `_dialogService` 并在构造函数中赋值
    - 暂时保留无参构造函数（向后兼容）
    - _Requirements: 5.4, 1.7_

  - [ ] 6.2 重构 AddUser 命令
    - 修改 `AddUser()` 方法，调用 `_dialogService.ShowUserEditDialog`
    - 传入空字符串作为初始值，role 默认为 "操作员"
    - 处理用户取消场景（返回 null）
    - 使用返回的 `UserEditResult` 创建 `UserItem`
    - 移除 `new UserEditWindow()` 创建代码
    - 移除 `Application.Current.MainWindow` 访问
    - _Requirements: 1.4, 1.6_

  - [ ] 6.3 重构 EditUser 命令
    - 修改 `EditUser()` 方法，调用 `_dialogService.ShowUserEditDialog`
    - 传入当前用户数据作为初始值
    - 处理用户取消场景
    - 使用返回结果更新 UserItem 属性
    - 移除对话框创建代码
    - _Requirements: 1.4, 1.6_

  - [ ] 6.4 重构 ResetPassword 命令
    - 修改 `ResetPassword()` 方法，调用 `_dialogService.ShowMessageAsync`
    - 移除 `MessageBox.Show` 调用
    - _Requirements: 1.8_

- [ ] 7. Checkpoint - 验证 P0 阶段完成
  - 编译项目，确保 HistoryViewModel 和 UserViewModel 不引用 Views 命名空间
  - 手动测试：历史记录导出 CSV 功能
  - 手动测试：历史记录打印功能
  - 手动测试：用户新增/编辑对话框
  - 手动测试：用户密码重置消息框
  - 手动测试：Dashboard 缺陷列表颜色显示
  - 确认所有功能正常后，询问用户是否继续 P1 阶段

### P1 阶段：短期优化 (手动依赖注入 + 事件聚合器)

- [ ] 8. 引入事件聚合器（WeakReferenceMessenger）
  - [ ] 8.1 创建消息类型定义
    - 创建 `MotorTestSystem.Messages` 命名空间
    - 创建 `StationSnapshotMessage.cs` 作为 record 类型，包含 `StationSnapshot Snapshot` 属性
    - 创建 `PlcLogMessage.cs` 作为 record 类型，包含 `string Message` 属性
    - _Requirements: 7.2, 7.3_

  - [ ] 8.2 修改 PlcPollingService 发布消息
    - 打开 `PlcPollingService.cs`
    - 构造函数添加可选参数 `IMessenger? messenger`，默认使用 `WeakReferenceMessenger.Default`
    - 添加私有字段 `_messenger` 并在构造函数中赋值
    - 在 `HandleSnapshot` 方法中，保留现有 `SnapshotReceived?.Invoke` 事件
    - 在 `HandleSnapshot` 方法中，添加 `_messenger.Send(new StationSnapshotMessage(snapshot))`
    - 在 `HandleLog` 方法中，保留现有 `LogReceived?.Invoke` 事件
    - 在 `HandleLog` 方法中，添加 `_messenger.Send(new PlcLogMessage(message))`
    - _Requirements: 7.4, 7.5_

- [ ] 9. 重构 MainViewModel（依赖注入 + 消息订阅）
  - [ ] 9.1 重构构造函数为完全依赖注入
    - 移除无参构造函数
    - 修改构造函数签名，接受以下参数：
      - `DashboardViewModel dashboardVM`
      - `MonitorViewModel monitorVM`
      - `HistoryViewModel historyVM`
      - `ConfigViewModel configVM`
      - `UserViewModel userVM`
      - `IDispatcherService dispatcher`
      - `IMessenger messenger`
    - 添加私有字段 `_dispatcher` 和 `_messenger` 并赋值
    - 保存所有 ViewModel 引用到属性
    - _Requirements: 5.2, 4.6_

  - [ ] 9.2 订阅 StationSnapshotMessage
    - 在构造函数末尾调用 `_messenger.Register<StationSnapshotMessage>(this, OnSnapshotMessage)`
    - 创建 `OnSnapshotMessage` 方法，接受 `object recipient, StationSnapshotMessage msg` 参数
    - 在方法中调用 `_dispatcher.InvokeAsync(() => ApplyOnlineState(msg.Snapshot))`
    - 添加 try-catch 防止单个订阅者异常影响其他订阅者
    - _Requirements: 7.6, 7.9_

  - [ ] 9.3 移除直接事件订阅
    - 移除 `PlcPollingService.SnapshotReceived` 事件订阅代码
    - 移除所有 `Application.Current.Dispatcher` 访问
    - 移除 `BackendRuntime` 依赖
    - _Requirements: 4.7, 7.9_

- [ ] 10. 重构 MonitorViewModel（依赖注入 + 消息订阅）
  - [ ] 10.1 重构构造函数为完全依赖注入
    - 移除无参构造函数
    - 修改构造函数签名，接受以下参数：
      - `ObservableCollection<StationConfig> stationConfigs`
      - `IDispatcherService dispatcher`
      - `IMessenger messenger`
    - 添加私有字段 `_dispatcher` 和 `_messenger` 并赋值
    - 调用 `BuildStationStates(stationConfigs)` 初始化工站状态
    - _Requirements: 5.2, 4.8_

  - [ ] 10.2 订阅 StationSnapshotMessage 和 PlcLogMessage
    - 在构造函数末尾订阅 `StationSnapshotMessage`，调用 `OnSnapshotMessage`
    - 在构造函数末尾订阅 `PlcLogMessage`，调用 `OnLogMessage`
    - 创建 `OnSnapshotMessage` 方法，通过 `_dispatcher.InvokeAsync` 调用 `ApplySnapshot`
    - 创建 `OnLogMessage` 方法，通过 `_dispatcher.InvokeAsync` 更新 `SystemLogs` 集合
    - 添加 try-catch 异常隔离
    - _Requirements: 7.8, 7.11_

  - [ ] 10.3 添加 AllStations 属性
    - 创建 `AllStations` 公共属性，返回 `IEnumerable<StationState>`
    - 实现为连接 `NoLoadStations`、`NoiseStations` 和 `LoadStations` 集合
    - _Requirements: 8.4, 8.6_

  - [ ] 10.4 移除直接事件订阅
    - 移除 `PlcPollingService.SnapshotReceived` 和 `LogReceived` 事件订阅
    - 移除所有 `Application.Current.Dispatcher` 访问
    - 移除 `BackendRuntime` 依赖
    - _Requirements: 4.8, 7.11_

- [ ] 11. 重构 DashboardViewModel（依赖注入 + 调度服务）
  - [ ] 11.1 添加 IDispatcherService 构造函数参数
    - 修改构造函数，添加 `IDispatcherService dispatcher` 参数
    - 添加私有字段 `_dispatcher` 并赋值
    - _Requirements: 5.5, 4.7_

  - [ ] 11.2 移除定时器中的阻塞调用
    - 找到定时器回调中的 `GetAwaiter().GetResult()` 调用
    - 将定时器回调改为 async 方法
    - 移除阻塞调用，改为 await 异步调用
    - _Requirements: 4.7_

  - [ ] 11.3 移除 Application.Current.Dispatcher 访问
    - 查找所有 `Application.Current.Dispatcher` 访问
    - 替换为 `_dispatcher.InvokeAsync`
    - _Requirements: 4.7_

- [ ] 12. 重构 ConfigViewModel（依赖注入）
  - [ ] 12.1 添加 IDialogService 构造函数参数
    - 修改构造函数，添加 `IDialogService dialogService` 参数
    - 添加私有字段 `_dialogService` 并赋值
    - _Requirements: 5.7_

  - [ ] 12.2 重构 TestConnectionAsync 方法
    - 修改方法，使用 `_dialogService.ShowMessageAsync` 显示测试结果
    - 移除 `MessageBox.Show` 调用
    - _Requirements: 1.8_

  - [ ] 12.3 重构 SaveAll 方法
    - 修改方法，使用 `_dialogService.ShowMessageAsync` 显示保存结果
    - 移除 `MessageBox.Show` 调用
    - _Requirements: 1.8_

- [ ] 13. 重构 NotificationCenterViewModel 和 LogCenterViewModel（如果存在）
  - [ ] 13.1 添加 IDispatcherService 构造函数参数
    - 修改 `NotificationCenterViewModel` 构造函数（如果存在）
    - 修改 `LogCenterViewModel` 构造函数（如果存在）
    - 添加 `IDispatcherService dispatcher` 参数
    - _Requirements: 4.9, 4.10_

  - [ ] 13.2 替换 Application.Current.Dispatcher 访问
    - 查找所有 `Application.Current.Dispatcher` 访问
    - 替换为 `_dispatcher.InvokeAsync` 或 `_dispatcher.Invoke`
    - _Requirements: 4.9, 4.10_

- [ ] 14. 更新 MainViewModel 使用 MonitorViewModel.AllStations
  - [ ] 14.1 修改 GetAllStations 方法
    - 打开 `MainViewModel.cs`，找到 `GetAllStations` 方法
    - 修改实现为 `return MonitorVM.AllStations;`
    - 移除直接访问 `NoLoadStations`、`NoiseStations`、`LoadStations` 的代码
    - _Requirements: 8.1, 8.2, 8.3, 8.5_

- [ ] 15. 更新 MainWindow 手动构造依赖图
  - [ ] 15.1 修改 MainWindow.xaml.cs 构造函数
    - 创建 `WpfDispatcherService` 实例
    - 创建 `WpfDialogService` 实例，传入 dispatcher
    - 获取 `WeakReferenceMessenger.Default` 实例
    - 从 `BackendRuntime.Shared` 获取 repository 和其他服务（暂时）
    - 创建所有 ViewModel 实例，按依赖顺序：
      1. HistoryViewModel(repository, dialogService)
      2. UserViewModel(dialogService)
      3. ConfigViewModel(dialogService)
      4. DashboardViewModel(dispatcher)
      5. MonitorViewModel(stationConfigs, dispatcher, messenger)
      6. MainViewModel(dashboardVM, monitorVM, historyVM, configVM, userVM, dispatcher, messenger)
    - 设置 `DataContext = mainViewModel`
    - _Requirements: 5.2, 5.11_

- [ ] 16. 移除 EventChannel 闲置代码
  - [ ] 16.1 清理 EventChannelService
    - 打开 `EventChannelService.cs`（如果存在）
    - 移除 `_snapshotChannel` 字段定义
    - 移除 `SnapshotChannel` 属性
    - 移除所有写入 `_snapshotChannel` 的代码
    - 保留 `_writeChannel` 用于 BatchWriteService
    - _Requirements: 9.1, 9.2, 9.3, 9.4_

- [ ] 17. Checkpoint - 验证 P1 阶段完成
  - 编译项目，确保所有 ViewModel 都有明确的构造函数依赖
  - 手动测试：PLC 快照更新是否实时反映在 Dashboard
  - 手动测试：PLC 快照更新是否实时反映在 Monitor
  - 手动测试：在线工站数量统计是否正确
  - 手动测试：Monitor 日志列表是否实时更新
  - 确认所有功能正常后，询问用户是否继续 P2 阶段

### P2 阶段：长期演进 (DI 容器迁移)

- [ ] 18. 引入依赖注入容器
  - [ ] 18.1 添加 NuGet 包
    - 在项目中添加 `Microsoft.Extensions.DependencyInjection` NuGet 包
    - _Requirements: 6.1_

  - [ ] 18.2 创建 ServiceCollectionExtensions
    - 创建 `ServiceCollectionExtensions.cs` 文件
    - 定义静态扩展方法 `AddMotorTestSystemServices(this IServiceCollection services)`
    - _Requirements: 6.2_

  - [ ] 18.3 注册基础设施服务
    - 在扩展方法中注册 `IDialogService` 为 Singleton，实现为 `WpfDialogService`
    - 注册 `IDispatcherService` 为 Singleton，实现为 `WpfDispatcherService`
    - 注册 `IMessenger` 为 Singleton，使用 `WeakReferenceMessenger.Default`
    - _Requirements: 6.4, 6.5_

  - [ ] 18.4 注册数据访问服务
    - 注册 `IMotorTestRepository` 为 Singleton，实现为 `InMemoryMotorTestRepository`
    - 注册 `IUserService` 为 Singleton，实现为 `MockUserService`
    - 注册 `IAuthService` 为 Singleton，实现为 `AuthService`
    - _Requirements: 6.6, 6.7, 6.8_

  - [ ] 18.5 注册业务服务
    - 注册 `IPlcClientFactory` 为 Transient，实现为 `MockPlcClientFactory`
    - 创建 `CreateDefaultStationConfigs` 方法返回默认工站配置
    - 注册 `ObservableCollection<StationConfig>` 为 Singleton，使用工厂方法
    - _Requirements: 6.11_

  - [ ] 18.6 注册 PlcPollingService
    - 使用工厂方法注册 `PlcPollingService` 为 Singleton
    - 工厂方法中从 `IServiceProvider` 解析所有依赖
    - 创建实例后调用 `Start()` 方法自动启动
    - _Requirements: 6.10_

  - [ ] 18.7 注册所有 ViewModel
    - 注册 `DashboardViewModel` 为 Singleton
    - 注册 `MonitorViewModel` 为 Singleton
    - 注册 `HistoryViewModel` 为 Singleton
    - 注册 `ConfigViewModel` 为 Singleton
    - 注册 `UserViewModel` 为 Singleton
    - 注册 `MainViewModel` 为 Singleton
    - _Requirements: 6.12_

- [ ] 19. 修改应用启动流程
  - [ ] 19.1 重构 App.xaml.cs OnStartup
    - 添加私有字段 `_serviceProvider`
    - 在 `OnStartup` 方法中创建 `ServiceCollection`
    - 调用 `services.AddMotorTestSystemServices()`
    - 调用 `services.BuildServiceProvider()` 构建容器
    - 保存到 `_serviceProvider` 字段
    - _Requirements: 6.2, 6.15_

  - [ ] 19.2 修改 LoginWindow 显示逻辑
    - 在 `OnStartup` 中显示 `LoginWindow`
    - 如果登录成功，从 `_serviceProvider` 解析 `MainViewModel`
    - 创建 `MainWindow` 实例，设置 `DataContext = mainViewModel`
    - 调用 `mainWindow.Show()`
    - 如果登录失败，调用 `Shutdown()`
    - _Requirements: 6.13_

  - [ ] 19.3 添加资源释放逻辑
    - 重写 `OnExit` 方法
    - 检查 `_serviceProvider` 是否实现 `IDisposable`
    - 如果实现，调用 `Dispose()` 释放资源
    - _Requirements: 6.15_

- [ ] 20. 简化 MainWindow 构造函数
  - [ ] 20.1 修改 MainWindow.xaml.cs
    - 打开 `MainWindow.xaml.cs`
    - 移除所有手动依赖构造代码
    - 保留无参构造函数，仅调用 `InitializeComponent()`
    - DataContext 由 `App.xaml.cs` 设置，不在构造函数中设置
    - _Requirements: 6.13, 6.14_

- [ ] 21. 移除 BackendRuntime 静态服务定位器
  - [ ] 21.1 删除 BackendRuntime.Shared 属性
    - 打开 `BackendRuntime.cs`
    - 移除 `Shared` 静态属性
    - 移除 `CreateDefault()` 静态方法
    - _Requirements: 5.1, 6.14_

  - [ ] 21.2 移除 ViewModel 中的 BackendRuntime 引用
    - 搜索所有 `BackendRuntime.Shared` 访问
    - 确认所有 ViewModel 已通过构造函数注入依赖
    - 移除所有 `BackendRuntime` 引用
    - 移除 ViewModel 中的无参构造函数（如果还保留）
    - _Requirements: 5.1, 5.10_

  - [ ] 21.3 验证编译
    - 编译项目，确保没有 `BackendRuntime.Shared` 访问
    - 确认所有服务通过 DI 容器解析
    - _Requirements: 6.14_

- [ ] 22. Checkpoint - 验证 P2 阶段完成
  - 编译项目，确保应用启动时自动构建依赖图
  - 手动测试：应用启动流程，确认 LoginWindow → MainWindow 正常
  - 手动测试：所有 Tab 切换正常
  - 手动测试：Dashboard 实时数据更新正常
  - 手动测试：Monitor 工站状态更新正常
  - 手动测试：History 查询/导出/打印功能正常
  - 手动测试：User 增删改功能正常
  - 手动测试：Config 连接测试功能正常
  - 性能验证：应用启动时间 < 3 秒
  - 性能验证：PLC 快照处理延迟 < 100ms
  - 确认所有功能正常后，询问用户

### 可选：单元测试任务

- [ ]* 23. 编写 HistoryViewModel 单元测试
  - [ ]* 23.1 测试导出命令 - 用户取消场景
    - 创建 `HistoryViewModelTests.cs` 测试类
    - Mock `IDialogService`，设置 `NextSaveFilePath = null`
    - 执行 `ExportCommand`
    - 验证没有显示成功消息
    - _Requirements: 11.2_

  - [ ]* 23.2 测试导出命令 - 成功场景
    - Mock `IDialogService`，设置 `NextSaveFilePath = "test.csv"`
    - 执行 `ExportCommand`
    - 验证显示了成功消息
    - 验证消息内容包含 "成功导出"
    - _Requirements: 11.2_

- [ ]* 24. 编写 UserViewModel 单元测试
  - [ ]* 24.1 测试添加用户 - 用户确认场景
    - 创建 `UserViewModelTests.cs` 测试类
    - Mock `IDialogService`，设置 `NextUserEditResult` 返回有效数据
    - 执行 `AddUserCommand`
    - 验证用户集合增加了一条记录
    - 验证新用户的 Account 匹配
    - _Requirements: 11.3_

  - [ ]* 24.2 测试添加用户 - 用户取消场景
    - Mock `IDialogService`，设置 `NextUserEditResult = null`
    - 执行 `AddUserCommand`
    - 验证用户集合数量不变
    - _Requirements: 11.3_

- [ ]* 25. 编写 MainViewModel 单元测试
  - [ ]* 25.1 测试快照消息订阅
    - 创建 `MainViewModelTests.cs` 测试类
    - 使用 `SyncDispatcherService` 和 `WeakReferenceMessenger.Default`
    - 创建 `MainViewModel` 实例
    - 发送 `StationSnapshotMessage`
    - 验证 `OnlineStationCount` 更新正确
    - _Requirements: 11.4_

- [ ]* 26. 编写 MonitorViewModel 单元测试
  - [ ]* 26.1 测试快照消息订阅
    - 创建 `MonitorViewModelTests.cs` 测试类
    - 使用 `SyncDispatcherService` 模拟同步调度
    - 创建 `MonitorViewModel` 实例
    - 发送 `StationSnapshotMessage`
    - 验证对应工站状态更新
    - _Requirements: 11.5_

  - [ ]* 26.2 测试日志消息订阅
    - 发送 `PlcLogMessage`
    - 验证 `SystemLogs` 集合新增了日志条目
    - 验证日志格式正确（包含时间戳）
    - _Requirements: 11.5_

- [ ]* 27. 编写 DashboardViewModel 单元测试
  - [ ]* 27.1 测试调度服务使用
    - 创建 `DashboardViewModelTests.cs` 测试类
    - Mock `IDispatcherService`，验证 `InvokeAsync` 调用
    - _Requirements: 11.4_

- [ ]* 28. 编写集成测试
  - [ ]* 28.1 测试 DI 容器依赖解析
    - 创建 `IntegrationTests.cs` 测试类
    - 创建 `ServiceCollection` 并调用 `AddMotorTestSystemServices`
    - 构建 `IServiceProvider`
    - 解析 `MainViewModel`
    - 验证所有依赖都正确注入
    - 验证 ViewModel 可以正常工作
    - _Requirements: 6.15_

## Notes

### 任务说明

- **标记 `*` 的任务为可选任务**：主要是单元测试任务，可以根据项目时间和优先级决定是否执行
- **每个阶段有 Checkpoint 任务**：用于验证阶段性成果，确保系统可编译、可运行、可回滚
- **任务按依赖顺序排列**：每个任务都可以在前置任务完成后立即开始
- **所有任务都包含需求引用**：便于追溯到原始需求和验收标准

### 测试策略

- **P0/P1 阶段**：重点是功能回归测试，确保重构不改变业务行为
- **P2 阶段**：重点是启动流程和依赖图验证
- **单元测试**：可选任务，用于验证 ViewModel 逻辑的正确性，目标覆盖率 60%+

### 实施建议

1. **分支策略**：每个阶段在独立分支开发
   - P0: `refactor/p0-view-decoupling`
   - P1: `refactor/p1-di-messaging`
   - P2: `refactor/p2-di-container`

2. **提交粒度**：每完成一个子任务提交一次，便于回滚和代码审查

3. **测试优先级**：
   - 必须：P0/P1/P2 的 Checkpoint 手动测试
   - 推荐：HistoryViewModel 和 UserViewModel 单元测试
   - 可选：其他 ViewModel 单元测试

4. **风险控制**：每个阶段合并前需要完整回归测试，如果出现问题可快速回滚

### 预计工作量

- **P0 阶段**：1-2 天（7 个主任务，约 20 个子任务）
- **P1 阶段**：2-3 天（10 个主任务，约 25 个子任务）
- **P2 阶段**：2-3 天（5 个主任务，约 15 个子任务）
- **单元测试**：1-2 天（可选，6 个测试任务）

**总计**：5-8 天（不含单元测试）或 6-10 天（含单元测试）

## Task Dependency Graph

```json
{
  "waves": [
    {
      "id": 0,
      "tasks": ["1.1", "1.2", "1.3"]
    },
    {
      "id": 1,
      "tasks": ["1.4", "1.5", "1.6"]
    },
    {
      "id": 2,
      "tasks": ["3.1", "3.2", "3.3"]
    },
    {
      "id": 3,
      "tasks": ["3.4", "5.1", "6.1"]
    },
    {
      "id": 4,
      "tasks": ["5.2", "5.3", "5.4", "5.5", "6.2", "6.3", "6.4"]
    },
    {
      "id": 5,
      "tasks": ["8.1"]
    },
    {
      "id": 6,
      "tasks": ["8.2", "9.1", "10.1", "11.1", "12.1", "13.1"]
    },
    {
      "id": 7,
      "tasks": ["9.2", "10.2", "11.2", "12.2", "13.2"]
    },
    {
      "id": 8,
      "tasks": ["9.3", "10.3", "10.4", "11.3", "12.3", "14.1", "16.1"]
    },
    {
      "id": 9,
      "tasks": ["15.1"]
    },
    {
      "id": 10,
      "tasks": ["18.1", "18.2"]
    },
    {
      "id": 11,
      "tasks": ["18.3", "18.4", "18.5"]
    },
    {
      "id": 12,
      "tasks": ["18.6", "18.7"]
    },
    {
      "id": 13,
      "tasks": ["19.1", "20.1"]
    },
    {
      "id": 14,
      "tasks": ["19.2", "19.3"]
    },
    {
      "id": 15,
      "tasks": ["21.1"]
    },
    {
      "id": 16,
      "tasks": ["21.2", "21.3"]
    },
    {
      "id": 17,
      "tasks": ["23.1", "23.2", "24.1", "24.2", "25.1", "26.1", "26.2", "27.1", "28.1"]
    }
  ]
}
```
