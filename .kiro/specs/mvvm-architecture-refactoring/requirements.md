# Requirements Document

## Introduction

MotorTestSystem 是一个基于 WPF + CommunityToolkit.Mvvm 的电机测试系统,当前存在严重的架构问题:ViewModel 直接操作 UI 控件、反向依赖 View 层、使用 Service Locator 反模式等。这些问题导致代码难以测试、模块耦合严重、维护困难。

本重构项目旨在通过分阶段修复这些架构问题,将系统演进为标准的 MVVM 架构,同时保证业务逻辑不变、支持单元测试、可回滚。

## Glossary

- **System**: MotorTestSystem WPF 应用程序
- **ViewModel**: 视图模型层,负责展示逻辑和数据绑定
- **View**: 视图层,负责 UI 展示
- **Service**: 服务层,负责业务逻辑和外部依赖封装
- **DialogService**: 对话框服务接口,封装窗口创建、消息框、文件选择等 UI 交互
- **DispatcherService**: 线程调度服务接口,封装 UI 线程调度逻辑
- **EventAggregator**: 事件聚合器,用于模块间松耦合通信
- **DI_Container**: 依赖注入容器 (Microsoft.Extensions.DependencyInjection)
- **BackendRuntime**: 当前的服务定位器类,手动管理服务实例
- **PlcPollingService**: PLC 轮询服务,产生 SnapshotReceived 和 LogReceived 事件
- **HistoryViewModel**: 历史记录视图模型,存在最严重的 View 反向依赖
- **UserViewModel**: 用户管理视图模型,存在 View 反向依赖
- **DefectItem**: 缺陷数据模型,当前混入了 UI 类型 (BrushConverter)
- **Refactoring_Phase**: 重构阶段,分为 P0(立即修复)、P1(短期优化)、P2(架构演进)

## Requirements

### 需求 1: 移除 ViewModel 到 View 的反向依赖

**用户故事:** 作为开发者,我希望 ViewModel 不直接引用 View 类型,以便可以独立测试 ViewModel 而不依赖 UI 框架

#### 验收标准

1. THE HistoryViewModel SHALL NOT reference the MotorTestSystem.Views namespace
2. THE UserViewModel SHALL NOT reference the MotorTestSystem.Views namespace
3. THE HistoryViewModel SHALL NOT create Window instances directly (MotorReportWindow)
4. THE UserViewModel SHALL NOT create Window instances directly (UserEditWindow)
5. THE HistoryViewModel SHALL NOT access Application.Current.MainWindow
6. THE UserViewModel SHALL NOT access Application.Current.MainWindow
7. WHEN a ViewModel needs to show a dialog, THE System SHALL use IDialogService interface
8. WHEN a ViewModel needs to show a message box, THE System SHALL use IDialogService.ShowMessageAsync
9. WHEN a ViewModel needs to show a save file dialog, THE System SHALL use IDialogService.ShowSaveFileDialog
10. WHEN a ViewModel needs to print a report, THE System SHALL use IDialogService.ShowPrintDialog
11. FOR ALL ViewModel classes, compiling the ViewModel project WITHOUT referencing the View project SHALL succeed

### 需求 2: 创建对话框服务抽象

**用户故事:** 作为开发者,我希望有一个标准的对话框服务接口,以便在测试时可以 mock UI 交互

#### 验收标准

1. THE System SHALL define an IDialogService interface in the Services namespace
2. THE IDialogService SHALL provide a ShowMessageAsync method accepting message, title, and button parameters
3. THE IDialogService SHALL provide a ShowSaveFileDialog method accepting filter and default filename parameters
4. THE IDialogService SHALL provide a ShowPrintDialog method returning boolean success indicator
5. THE IDialogService SHALL provide a ShowReportWindow method accepting report data parameters
6. THE IDialogService SHALL provide a ShowUserEditDialog method accepting user data parameters
7. THE IDialogService SHALL provide a SetClipboardText method for clipboard operations
8. THE System SHALL provide a WpfDialogService implementation of IDialogService
9. THE WpfDialogService implementation SHALL handle all actual UI control creation (PrintDialog, SaveFileDialog, Window instances)
10. WHEN IDialogService methods are called on background threads, THE implementation SHALL automatically marshal to UI thread

### 需求 3: 移除数据模型中的 UI 类型依赖

**用户故事:** 作为开发者,我希望数据模型不包含 UI 框架类型,以便模型可以在非 UI 环境中使用

#### 验收标准

1. THE DefectItem class SHALL NOT use System.Windows.Media.BrushConverter
2. THE DefectItem class SHALL store color as string property
3. WHEN DefectItem.Color is bound to XAML, THE System SHALL use an IValueConverter to convert string to Brush
4. THE System SHALL define a StringToColorBrushConverter implementing IValueConverter
5. THE DashboardView XAML SHALL use StringToColorBrushConverter for DefectItem color binding
6. FOR ALL data model classes in MotorTestSystem.Models namespace, compiling WITHOUT referencing PresentationCore SHALL succeed

### 需求 4: 抽象线程调度服务

**用户故事:** 作为开发者,我希望 ViewModel 不直接依赖 Application.Current.Dispatcher,以便可以在单元测试中控制线程调度

#### 验收标准

1. THE System SHALL define an IDispatcherService interface in the Services namespace
2. THE IDispatcherService SHALL provide an Invoke method accepting Action parameter
3. THE IDispatcherService SHALL provide an InvokeAsync method accepting Action parameter and returning Task
4. THE IDispatcherService SHALL provide a CheckAccess method returning boolean
5. THE System SHALL provide a WpfDispatcherService implementation using Application.Current.Dispatcher
6. THE MainViewModel SHALL inject IDispatcherService instead of accessing Application.Current.Dispatcher
7. THE DashboardViewModel SHALL inject IDispatcherService instead of accessing Application.Current.Dispatcher
8. THE MonitorViewModel SHALL inject IDispatcherService instead of accessing Application.Current.Dispatcher
9. THE NotificationCenterViewModel SHALL inject IDispatcherService instead of accessing Application.Current.Dispatcher
10. THE LogCenterViewModel SHALL inject IDispatcherService instead of accessing Application.Current.Dispatcher
11. WHEN unit testing a ViewModel, THE test SHALL be able to provide a synchronous IDispatcherService mock

### 需求 5: 替换 Service Locator 为依赖注入

**用户故事:** 作为开发者,我希望通过构造函数注入依赖,以便明确依赖关系并支持单元测试

#### 验收标准

1. THE System SHALL NOT call BackendRuntime.GetSharedAsync().GetAwaiter().GetResult() in ViewModel constructors
2. WHEN a ViewModel is created, THE System SHALL inject all dependencies through constructor parameters
3. THE HistoryViewModel constructor SHALL accept IMotorTestRepository, IDialogService, and IDispatcherService parameters
4. THE UserViewModel constructor SHALL accept IUserService, IDialogService, and IDispatcherService parameters
5. THE DashboardViewModel constructor SHALL accept IHikvisionSdkService, IDialogService, and IDispatcherService parameters
6. THE MonitorViewModel constructor SHALL accept IPlcPollingService and IDispatcherService parameters
7. THE ConfigViewModel constructor SHALL accept IStationConfigService and IDialogService parameters
8. THE NotificationCenterViewModel constructor SHALL accept INotificationService and IDispatcherService parameters
9. THE LogCenterViewModel constructor SHALL accept INotificationService and IDispatcherService parameters
10. FOR ALL ViewModel classes, the parameterless constructor SHALL be marked as obsolete or removed
11. WHEN unit testing a ViewModel, THE test SHALL be able to pass mock dependencies to constructor

### 需求 6: 引入依赖注入容器

**用户故事:** 作为开发者,我希望使用标准的 DI 容器管理服务生命周期,以便自动处理依赖解析和释放

#### 验收标准

1. THE System SHALL reference Microsoft.Extensions.DependencyInjection NuGet package
2. THE System SHALL create an IServiceCollection in App.xaml.cs startup
3. THE System SHALL register all services with appropriate lifetimes (Singleton, Transient, Scoped)
4. THE System SHALL register IDialogService as Singleton with WpfDialogService implementation
5. THE System SHALL register IDispatcherService as Singleton with WpfDispatcherService implementation
6. THE System SHALL register IMotorTestRepository as Singleton with SqlMotorTestRepository implementation
7. THE System SHALL register IUserService as Singleton with SqlSugarUserService implementation
8. THE System SHALL register IAuthService as Singleton with AuthService implementation
9. THE System SHALL register INotificationService as Singleton with SqlSugarNotificationService implementation
10. THE System SHALL register IPlcPollingService as Singleton with PlcPollingService implementation
11. THE System SHALL register IPlcClientFactory as Transient with PlcClientFactory implementation
12. THE System SHALL register all ViewModel classes as Singleton
13. THE MainWindow constructor SHALL accept IServiceProvider and resolve ViewModels from it
14. THE BackendRuntime.CreateDefaultAsync method SHALL be replaced with IServiceCollection extension methods
15. WHEN the application starts, THE DI_Container SHALL automatically construct the dependency graph

### 需求 7: 引入轻量事件聚合器

**用户故事:** 作为开发者,我希望 ViewModel 之间通过事件聚合器通信,以便解除对 PlcPollingService 的直接依赖

#### 验收标准

1. THE System SHALL use CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger as event aggregator
2. THE System SHALL define a StationSnapshotMessage record containing StationSnapshot data
3. THE System SHALL define a PlcLogMessage record containing log entry data
4. WHEN PlcPollingService receives a snapshot, THE PlcPollingService SHALL publish StationSnapshotMessage
5. WHEN PlcPollingService receives a log, THE PlcPollingService SHALL publish PlcLogMessage
6. THE MainViewModel SHALL subscribe to StationSnapshotMessage using WeakReferenceMessenger
7. THE DashboardViewModel SHALL subscribe to StationSnapshotMessage using WeakReferenceMessenger
8. THE MonitorViewModel SHALL subscribe to StationSnapshotMessage and PlcLogMessage using WeakReferenceMessenger
9. THE MainViewModel SHALL NOT directly reference PlcPollingService.SnapshotReceived event
10. THE DashboardViewModel SHALL NOT directly reference PlcPollingService.SnapshotReceived event
11. THE MonitorViewModel SHALL NOT directly reference PlcPollingService.SnapshotReceived or LogReceived events
12. WHEN a ViewModel is disposed, THE WeakReferenceMessenger SHALL automatically remove subscriptions
13. FOR ALL message subscriptions, memory profiler SHALL NOT show memory leaks after ViewModel disposal

### 需求 8: 解除 ViewModel 之间的强耦合

**用户故事:** 作为开发者,我希望 ViewModel 之间不直接访问彼此的内部集合,以便独立修改每个 ViewModel 的实现

#### 验收标准

1. THE MainViewModel SHALL NOT directly access MonitorViewModel.NoLoadStations collection
2. THE MainViewModel SHALL NOT directly access MonitorViewModel.NoiseStations collection
3. THE MainViewModel SHALL NOT directly access MonitorViewModel.LoadStations collection
4. THE MonitorViewModel SHALL expose an IEnumerable<StationState> AllStations property
5. WHEN MainViewModel needs station data, THE MainViewModel SHALL read MonitorViewModel.AllStations property
6. THE MonitorViewModel.AllStations property SHALL return concatenated and ordered stations
7. WHEN MonitorViewModel internal collections change, THE AllStations property SHALL reflect the changes automatically

### 需求 9: 移除未使用的 EventChannel

**用户故事:** 作为开发者,我希望移除闲置的 _snapshotChannel,以便减少资源浪费和代码复杂度

#### 验收标准

1. THE EventChannelService SHALL NOT define _snapshotChannel field
2. THE EventChannelService SHALL NOT write to _snapshotChannel
3. THE EventChannelService.SnapshotChannel property SHALL be removed
4. THE EventChannelService SHALL only maintain _writeChannel for BatchWriteService consumption
5. WHEN a snapshot is received, THE System SHALL only use C# events or WeakReferenceMessenger for distribution

### 需求 10: 确保业务逻辑不变

**用户故事:** 作为质量保证人员,我希望重构后系统行为完全一致,以便用户无感知升级

#### 验收标准

1. WHEN a user clicks print button in HistoryView, THE System SHALL show the same print dialog and produce the same output
2. WHEN a user edits a user record in UserView, THE System SHALL show the same edit window with same validation
3. WHEN a snapshot is received from PLC, THE Dashboard SHALL update KPI cards with same timing
4. WHEN a log entry is generated, THE MonitorView SHALL append it to the log list with same formatting
5. WHEN a defect is displayed in Dashboard, THE defect color SHALL match the original color scheme
6. WHEN user copies barcode in HistoryView, THE clipboard SHALL contain the same barcode string
7. WHEN user exports CSV in HistoryView, THE System SHALL show the same save file dialog with same filters
8. FOR ALL user-facing features, manual regression test suite SHALL pass 100%

### 需求 11: 支持单元测试能力

**用户故事:** 作为开发者,我希望可以对 ViewModel 进行单元测试,以便验证业务逻辑正确性

#### 验收标准

1. THE System SHALL be able to construct any ViewModel with mocked dependencies
2. WHEN testing HistoryViewModel, THE test SHALL mock IDialogService and verify ShowMessageAsync calls
3. WHEN testing UserViewModel, THE test SHALL mock IUserService and IDialogService
4. WHEN testing DashboardViewModel, THE test SHALL mock IDispatcherService to run synchronously
5. WHEN testing MonitorViewModel snapshot handling, THE test SHALL send StationSnapshotMessage and verify state updates
6. THE test project SHALL NOT reference WPF UI assemblies (PresentationCore, PresentationFramework)
7. THE test project SHALL use xUnit or NUnit framework
8. THE test project SHALL use Moq or NSubstitute for mocking
9. FOR ALL critical ViewModel commands (PrintCommand, SaveCommand, DeleteCommand), unit tests SHALL exist and pass
10. THE test suite SHALL achieve at least 60% code coverage on ViewModel layer after refactoring

### 需求 12: 分阶段实施和回滚能力

**用户故事:** 作为项目经理,我希望重构可以分阶段实施,以便降低风险并在出现问题时快速回滚

#### 验收标准

1. THE System SHALL define three refactoring phases: P0 (critical), P1 (short-term), P2 (long-term)
2. THE P0 phase SHALL include Requirements 1, 2, 3 (ViewModel-View decoupling)
3. THE P1 phase SHALL include Requirements 4, 5, 7, 8 (DI and EventAggregator)
4. THE P2 phase SHALL include Requirements 6, 9 (DI Container migration)
5. WHEN P0 phase is completed, THE System SHALL compile and run with P0 changes only
6. WHEN P1 phase is completed, THE System SHALL compile and run with P0+P1 changes
7. WHEN any phase is completed, THE code changes SHALL be in a separate Git branch
8. WHEN a phase introduces breaking changes, THE System SHALL maintain a compatibility layer for one release cycle
9. THE P0 branch SHALL be named "refactor/p0-view-decoupling"
10. THE P1 branch SHALL be named "refactor/p1-di-messaging"
11. THE P2 branch SHALL be named "refactor/p2-di-container"
12. FOR ALL phases, Git commit messages SHALL reference the requirement number being implemented

### 需求 13: 保持现有功能完整性

**用户故事:** 作为用户,我希望重构后所有功能仍然可用,以便继续正常使用系统

#### 验收标准

1. WHEN the application starts, THE LoginWindow SHALL appear and accept user credentials
2. WHEN user logs in successfully, THE MainWindow SHALL show with Dashboard tab selected
3. WHEN user navigates between tabs, THE corresponding ViewModel and View SHALL activate
4. WHEN PLC data arrives, THE MonitorView SHALL update station status in real-time
5. WHEN user queries history records, THE HistoryView SHALL display matching records in the grid
6. WHEN user clicks export in HistoryView, THE System SHALL generate CSV file with all selected records
7. WHEN user adds/edits/deletes a user, THE UserView SHALL reflect the change in the user list
8. WHEN user modifies station configuration, THE ConfigView SHALL save changes to database
9. WHEN a test completes, THE BatchWriteService SHALL write test results to database within 100ms window
10. FOR ALL existing features in the system, functional test checklist SHALL be 100% verified after each phase
