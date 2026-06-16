# MotorTestSystem — XAML 绑定与 ICommand 审查报告

> 审查日期：2026-06-15  
> 审查范围：14 个 XAML 文件、9 个 ViewModel、10 个 code-behind  
> 重点关注：高频更新集合/属性、INotifyPropertyChanged 冗余、ICommand CanExecute 防呆

---

## 目录

1. [XAML 绑定有效性 & 高频数据更新](#1-xaml-绑定有效性--高频数据更新)
2. [INotifyPropertyChanged 触发频率与冗余审查](#2-inotifypropertychanged-触发频率与冗余审查)
3. [ICommand CanExecute 防呆逻辑审查](#3-icommand-canexecute-防呆逻辑审查)
4. [总体评分与修复建议](#4-总体评分与修复建议)

---

## 1. XAML 绑定有效性 & 高频数据更新

### 1.1 高频更新场景识别

项目中有 **3 个高频数据更新源**：

| 数据源 | 刷新频率 | 影响的 ViewModel/View | 数据量级 |
|--------|---------|----------------------|---------|
| **PLC 轮询快照** (`PlcPollingService.SnapshotReceived`) | 每 1 秒 1 次/每工位 | DashboardVM, MonitorVM, MainVM | 6 个工位 × 1 次/秒 |
| **Dispatcher 定时器** (`_refreshTimer`) | 每 5 秒 | DashboardVM | 全量刷新 KPI + 图表 |
| **时钟更新** (`_clockTimer`) | 每 1 秒 | MainVM → MainWindow 标题 | 1 个字符串属性 |

### 1.2 高频集合更新分析

#### 1.2.1 DashboardView — 高频 refresh 场景

```xml
<!-- DashboardView.xaml -->
<ItemsControl ItemsSource="{Binding DefectList}">                <!-- 5 秒刷新一次 -->
<ItemsControl ItemsSource="{Binding TopFaultList}">              <!-- 5 秒刷新一次 -->
<lvc:CartesianChart Series="{Binding OutputSeries}" .../>       <!-- 5 秒刷新 → 新数组 -->
<lvc:CartesianChart Series="{Binding PassRateSeries}" .../>     <!-- 5 秒刷新 → 新数组 -->
<lvc:PieChart Series="{Binding DefectDistributionSeries}" .../> <!-- 5 秒刷新 → 新数组 -->
```

**DashboardViewModel** 每 5 秒执行 `RefreshAllDataAsync()`，其中：
- `RefreshKpiCardsAsync()` → 更新 8 个 `[ObservableProperty]` 标量属性
- `RefreshHourlyChartsAsync()` → 重建 `OutputSeries` / `XAxes` / `YAxes` / `PassRateSeries` / `PassRateXAxes` / `PassRateYAxes` **12 个数组类型属性**
- `RefreshDefectDataAsync()` → 清空并重建 `DefectList`、重建 `DefectDistributionSeries`
- `RefreshFaultRankingAsync()` → 清空并重建 `TopFaultList`

**问题 | 🔴 高风险：高频刷新导致 LiveCharts 重绘风暴**

`OutputSeries = new ISeries[] { ... }` 每次刷新都会创建全新的 `ISeries[]` 数组，LiveCharts 因此触发完全重绘。

当前 5 秒间隔下，**仅 1 次刷新就触发了 5 个 LiveCharts 图表的完整重建**。每个 `ISeries[]` 包含了 `StackedColumnSeries<int>`、`LineSeries<double>`、`PieSeries<double>` 等复杂对象，LiveCharts 的重新布局+渲染时间在 50-300ms 之间。5 个同时重建可能导致 UI 线程阻塞 200ms-1s 的卡顿。

**不推荐使用`AsyncObservableCollection<T>`**，因为 `ISeries[]` 不是集合类型。应使用**增量更新**策略。

**建议**：
- `OutputSeries` / `PassRateSeries` 改用 `ObservableCollection<ISeries>`，仅替换 `Values` 而不重建整个数组
- 或在 ViewModel 中实现**数据比较**，仅在数值变化时才赋值新数组

#### 1.2.2 MonitorView — 硬编码失控

**问题 | 🟠 高风险：MonitorView 中大量数据未使用绑定，而是硬编码值**

审查发现 MonitorView.xaml 中以下数据为硬编码（未绑定）：

| 位置 | 硬编码值 | 行号 |
|------|---------|------|
| 最近检测条码面板 | 4 个硬编码条码 SN-9948x-XA1, 结果 OK/NG | 127-161 |
| 系统警报面板 | "A4: 噪音超标"、"Cam-2 离线" 硬编码文本 | 197-213 |
| 所有 ProgressBar.Value | `Value="60"`, `Value="85"`, `Value="0"`, `Value="20"` | 411, 461, 537, 617 |
| 视频监控区域 | 全部为静态 UI（点位、角标、"LIVE" 标签） | 228-330 |
| A2 工位参数 | 全部为 `"-- V"`, `"-- A"`, `"-- RPM"` | 524-533 |
| A1/A5 部分参数 | "电压" 标签旁的 `220.1 V`（硬编码） | 391-392 |
| 工位名称映射 | "A1: FX5U", "A3: NV-X", "A5: LD-Max" | 378, 428, 474... |

**影响**：MonitorView 虽然通过 `NoLoadStations[0].Barcode` 等绑定了条码和数值，但大量辅助元素（状态标签、ProgressBar 值、视频区域）都是静态的，导致：
1. ProgressBar 无法反映真实进度
2. "最近检测条码" 区域不会更新
3. "系统警报" 区域无绑定，报警信息不会显示

#### 1.2.3 MainWindow.HeaderStations — 高频集合更新

```xml
<ItemsControl ItemsSource="{Binding HeaderStations}">
    <Ellipse Style="{StaticResource StatusDotStyle}" ToolTip="{Binding DisplayToolTip}"/>
</ItemsControl>
```

`HeaderStations` 是 `MainViewModel` 中的 `ObservableCollection<StationState>`，在 `MainViewModel` 构造函数中从 `MonitorVM` 的 3 组集合 `Concat` 构建。根据 `OnSnapshotReceived` 事件更新。

**风险 | 🟡 中风险**：
- `HeaderStations` 是**静态快照**（只在构造函数中构建一次），即使底层 `StationState` 属性变更（如 `Status`），`HeaderStations` 中的项不会自动更新。
- 但 `StationState` 继承了 `ObservableObject`，属性变更会触发 UI 刷新，所以 StatusDotStyle 的 DataTrigger 绑定 `Status` 数值可以工作。
- 索引依赖（`MonitorVM.NoLoadStations`）是高耦合的跨 ViewModel 访问。

### 1.3 异步可观察集合分析

**未使用异步可观察集合（如 `AsyncObservableCollection` 或 `SynchronizationContextCollection`）**。

当前情况：

| 集合 | 使用方式 | 线程安全 |
|------|---------|---------|
| `_allNotificationVms` (NotificationCenterVM) | `CollectionChanged` 事件从后台线程触发 → 手动 `Dispatcher.Invoke` 同步 | ✅ 手动同步 |
| `_allNotificationVms` (LogCenterVM) | 同上 | ✅ 手动同步 |
| `MonitorViewModel.StationState` 集合 | 仅 UI 线程更新（`RunOnUiThread`） | ✅ 手动同步 |
| `DashboardViewModel.DefectList` / `TopFaultList` | 仅在 `RefreshDefectDataAsync` 中更新（UI 线程触发） | ✅ |
| `HistoryViewModel.TestResults` | 仅在 `RenderCurrentPage` 中更新（UI 线程中调用） | ✅ |
| `UserViewModel.Users` | 仅在 `LoadUsers` / `FilterUsers` 中更新 | ✅ |
| `NotificationService.Notifications` | 后台线程 `CollectionChanged` → VM 拦截 + Dispatch | ✅ |

**结论**：所有 ViewModel 都确保了集合更新在 UI 线程执行，暂无跨线程冲突。但不建议引入 `AsyncObservableCollection`，因为：
1. 当前手动 Dispatch 模式已覆盖所有跨线程场景
2. 引入第三方异步集合会增加复杂度，且 CommunityToolkit.Mvvm 不提供此功能
3. 真正的问题不在线程安全，而在**高频刷新导致的 UI 重绘风暴**

### 1.4 数据节流 (Throttling/Debouncing) 需求

| 场景 | 原始频率 | 是否需要节流 | 现有保护 |
|------|---------|------------|---------|
| DashboardVM `RefreshAllDataAsync` | 5 秒 + event 驱动 | 🟡 建议 `OutputSeries` 增量更新 | 无比较 → 每次都全量重建 |
| MonitorVM `OnSnapshotReceived` | 1 次/秒/工位 | ✅ 已够用（仅 UI 属性赋值） | `RunOnUiThread` |
| MainVM `OnSnapshotReceived` | 1 次/秒/工位 | ✅ 仅做 `_onlineStations` 字典更新 | `InvokeAsync` |
| NotificationCenterVM 事件处理 | 低频 | ✅ 不需要 | 无 |
| HistoryVM 搜索 | 用户触发 | ✅ 不需要 | 无 |

---

## 2. INotifyPropertyChanged 触发频率与冗余审查

### 2.1 重复值保护检查

#### 2.1.1 CommunityToolkit.Mvvm 内置保护 ✅

所有 `[ObservableProperty]` 自动生成的 setter 都带有值比较保护：

```csharp
// 自动生成：
public int TotalChecked
{
    get => _totalChecked;
    set
    {
        if (!EqualityComparer<int>.Default.Equals(_totalChecked, value)) // ✅ 相同值不触发
        {
            _totalChecked = value;
            OnPropertyChanged(...);
        }
    }
}
```

因此**不存在相同值重复触发 PropertyChanged 的问题**。

#### 2.1.2 手动属性 setter 审查

部分 ViewModel 使用了手动属性 setter（如 `HistoryViewModel.StartDate` / `EndDate`）。

```csharp
// HistoryViewModel.cs:91
if (SetProperty(ref _startDate, value))  // ✅ SetProperty 包含值比较
```

全部 4 个手动属性都使用了 `SetProperty` 或 `SetProperty(ref ...)`，同样具有值比较保护。

**结论 | 🟢 无风险**：项目中不存在相同值重复触发 PropertyChanged 的问题。

### 2.2 计算属性的链式通知

使用 `[NotifyPropertyChangedFor(nameof(...))]` 的情况：

| 声明源 | 被触发的计算属性 | 触发条件 |
|--------|----------------|---------|
| `SelectedMotor` | `HasSelectedMotor`, `SelectedMotorResult` | 选中行切换 |
| `IsPrinting` | `IsNotPrinting` | 打印状态变化 |
| `CurrentPage` | `TotalPages`, `CurrentPageStart`, `CurrentPageEnd` | 翻页 |
| `PageSize` | `TotalPages`, `CurrentPageStart`, `CurrentPageEnd` | 页大小变化 |
| `TotalRecords` | `TotalPages`, `CurrentPageStart`, `CurrentPageEnd` | 查询结果变化 |

**这些链式通知都是必要的**，没有产生冗余通知链。

### 2.3 DashboardVM 高频属性分析

DashboardViewModel 每 5 秒全量刷新 8 个 KPI 属性 + 5 个图表数组属性 + 2 个 ObservableCollection。

**潜在问题**：即使数据没有变化（例如数据库返回相同的 TotalChecked），CommunityToolkit setter 的保护机制（2.1.1）会阻止相同值触发通知。但 `RefreshHourlyChartsAsync` 每次都创建**全新的 `ISeries[]` 数组对象**，即使数值完全相同，新数组引用 ≠ 旧数组引用，会触发 LiveCharts 的全量重绘。

**建议**：在创建新数组前，比较数值是否变化，只有变化时才赋值新数组。

### 2.4 只读计算属性重复评估

`MotorTestRecordModel` 中的辅助属性（如 `IsNoLoadCurrentAbnormal => NoLoadCurrent > 2.5`）是纯计算属性，没有存储值。它们依赖于 `NoLoadCurrent` 属性的变化来驱动 UI 更新。

在 `HistoryViewModel` 中，`SelectedMotor` 是整个 `MotorTestRecordModel` 对象引用。当 `SelectedMotor` 更改时，框架会重新评估所有绑定到该对象的属性（包括计算属性）。这是 WPF 的标准行为，**没有无效开销**。

**结论 | 🟢 无风险**：所有计算属性都是必要的，且触发时机正确。

---

## 3. ICommand CanExecute 防呆逻辑审查

### 3.1 危险操作识别

| 操作 | 所在 ViewModel | 命令方法 | 危险等级 |
|------|---------------|---------|---------|
| **PLC 连接测试** | ConfigVM | `TestConnectionAsync` | 🟠 中等（设备通信） |
| **保存全部配置** | ConfigVM | `SaveAll` | 🟡 低（数据库写入） |
| **连接海康摄像头** | DashboardVM | `ConnectCameraAsync` | 🟡 低（新增设备连接） |
| **断开摄像头** | DashboardVM | `DisconnectCamera` | 🟢 无风险 |
| **打印追溯单** | HistoryVM | `PrintTraceAsync` | 🟢 无风险（打印） |
| **导出数据** | HistoryVM | `Export` | 🟢 无风险（文件） |
| **查看报告** | HistoryVM | `ViewReport` | 🟢 无风险 |
| **新增用户** | UserVM | `AddUser` | 🟡 低（用户管理） |
| **编辑用户** | UserVM | `EditUser` | 🟡 低 |
| **重置密码** | UserVM | `ResetPassword` | 🟡 低 |
| **全标记已读/清空** | NotificationCenterVM | `MarkAllAsRead` / `ClearAll` | 🟢 无风险 |
| **复制条码** | HistoryVM | `CopyBarcode` | 🟢 无风险 |

### 3.2 CanExecute 实现审查

#### 3.2.1 有 CanExecute 保护的命令

```csharp
// UserViewModel.cs:216
[RelayCommand(CanExecute = nameof(CanAddUser))]    // ✅ CanExecute = CanAddUser (boolean property)
private void AddUser() ...

// UserViewModel.cs:253
[RelayCommand(CanExecute = nameof(CanEditUser))]   // ✅ CanExecute = CanEditUser
private void EditUser(UserItem user) ...

// UserViewModel.cs:290
[RelayCommand(CanExecute = nameof(CanResetPassword))] // ✅ CanExecute = CanResetPassword
private void ResetPassword(UserItem user) ...
```

这 3 个命令在新增/编辑用户和重置密码时检查了**权限级别**的 CanExecute。`CanAddUser`/`CanEditUser`/`CanResetPassword` 都是 `[ObservableProperty]`，在 `RefreshPermissions()` 中基于 `_authService.HasPermission(AppPermission.UserManagement)` 设置。这是**正确的**。

#### 3.2.2 无 CanExecute 保护的命令 — **严重违规**

以下危险操作**完全没有 CanExecute 保护**：

| 命令 | ViewModel | 风险 |
|------|----------|------|
| `TestConnectionAsync` | ConfigVM | 🟠 当连接测试正在进行时，用户可**重复点击**，同时进行多个并发连接测试 |
| `ConnectCameraAsync` | DashboardVM | 🟡 有 `IsCameraLoading` 保护（方法内拦截），但 CanExecute 未绑定 → 按钮可点击但无效 |
| `DisconnectCamera` | DashboardVM | 🟢 断开操作相对安全 |
| `PrintTraceAsync` | HistoryVM | 🟡 有 `IsPrinting` 属性 + `cancellationToken` 支持，但按钮未在打印中禁用 → 用户可重复触发 |
| `Export` | HistoryVM | 🟢 文件导出，重复快速点击只消耗资源但无破坏 |
| `SaveAll` | ConfigVM | 🟡 可重复保存，无操作中状态保护 |

### 3.3 具体问题

#### 问题 1 | 🟠 高：ConfigViewModel.TestConnectionAsync — 无并发保护

```csharp
// ConfigViewModel.cs:43
[RelayCommand]
private async Task TestConnectionAsync(StationConfig config)
{
    // 无 CanExecute！没有 IsTesting 状态保护
    bool isSuccess = await _runtime.PollingService.TestConnectionAsync(config);
    // ...
}
```

**问题**：如果用户快速点击"测试连接"按钮多次，会同时发起多个并发的 `TestConnectionAsync` 请求。虽然 `TestConnectionAsync` 内部创建了临时 `IPlcClient`，但大量并发 TCP 连接请求可能：
- 造成 PLC 端口阻塞
- 产生大量诊断日志
- 界面响应混乱（多次 ShowMessage）

**修复建议**：
```csharp
[ObservableProperty] private bool _isTestingConnection;
[RelayCommand(CanExecute = nameof(CanTestConnection))]
private async Task TestConnectionAsync(StationConfig config) { ... }
private bool CanTestConnection => !IsTestingConnection && SelectedConfig != null;
```

#### 问题 2 | 🟡 中：HistoryViewModel.PrintTraceAsync — 无按钮禁用

```csharp
// HistoryViewModel.cs:655
[RelayCommand]
private async Task PrintTraceAsync(CancellationToken cancellationToken)
{
    if (SelectedMotor == null) return;  // ✅ 有前置检查
    // ... 打印逻辑
}
```

虽然有 `IsPrinting` / `IsNotPrinting` 属性（`[NotifyPropertyChangedFor(nameof(IsNotPrinting))]`），但没有在 `[RelayCommand(CanExecute = nameof(IsNotPrinting))]` 中使用。

当前状态：打印中用户再次点击按钮 → 方法被调用但 `IsPrinting` 属性被重用 → 会导致打印日志混乱。实际多次 `WriteAsync` 调用可能导致 `InvalidOperationException`（XPS 写入器正在使用）。

**修复建议**：
```csharp
[RelayCommand(CanExecute = nameof(IsNotPrinting))]
private async Task PrintTraceAsync(CancellationToken cancellationToken) { ... }
```

#### 问题 3 | 🟡 中：DashboardViewModel.ConnectCameraAsync — 无 CanExecute

```csharp
// DashboardViewModel.cs:538
[RelayCommand]
private async Task ConnectCameraAsync()
{
    if (IsCameraLoading) return;  // 方法内有 Guard，但 CanExecute 未绑定
```

有 Guard 逻辑但无 CanExecute。`IsCameraLoading` 是 `[ObservableProperty]`，应绑定为 CanExecute。

### 3.4 全部命令 CanExecute 检查清单

| Command | ViewModel | CanExecute | 评价 |
|---------|-----------|-----------|------|
| `Navigate` | MainVM | ❌ 无 | ✅ 不需要（导航命令） |
| `SelectTimeDimension` | DashboardVM | ❌ 无 | ✅ OK（切换标签） |
| `ConnectCamera` | DashboardVM | ❌ 无 | 🟡 `IsCameraLoading` 做方法内拦截，按钮应禁用 |
| `DisconnectCamera` | DashboardVM | ❌ 无 | 🟢 安全操作 |
| `CaptureImage` | DashboardVM | ❌ 无 | 🟡 应禁用当 `!IsCameraConnected` |
| `Search` | HistoryVM | ❌ 无 | ✅ OK（用户主动查询） |
| `Reset` | HistoryVM | ❌ 无 | ✅ OK |
| `PreviousPage` / `NextPage` | HistoryVM | ❌ 无 | 🟡 应有 Page > 1 / Page < TotalPages 检查 |
| **`PrintTrace`** | **HistoryVM** | **❌ 无** | **🟠 打印中应禁用** |
| `ViewReport` | HistoryVM | ❌ 无 | ✅ OK |
| `CopyBarcode` | HistoryVM | ❌ 无 | ✅ OK |
| `Export` | HistoryVM | ❌ 无 | ✅ OK |
| **`TestConnection`** | **ConfigVM** | **❌ 无** | **🔴 应防止重复触发** |
| `ClearLogs` | ConfigVM | ❌ 无 | ✅ OK |
| `SaveAll` | ConfigVM | ❌ 无 | 🟡 应防重复 |
| `AddUser` | UserVM | ✅ `CanAddUser` | ✅ 正确 |
| `EditUser` | UserVM | ✅ `CanEditUser` | ✅ 正确 |
| `ResetPassword` | UserVM | ✅ `CanResetPassword` | ✅ 正确 |
| `MarkAllAsRead` | NotificationCenterVM | ❌ 无 | ✅ OK |
| `ClearAll` | NotificationCenterVM | ❌ 无 | ✅ OK |
| `ToggleReadStatus` | NotificationCenterVM | ❌ 无 | ✅ OK |
| `ViewDetails` | NotificationCenterVM | ❌ 无 | ✅ OK |
| `DiagnoseConnection` | NotificationCenterVM | ❌ 无 | ✅ OK |
| `NextPage` / `PreviousPage` | NotificationCenterVM | ❌ 无 | 🟡 应有分页边界检查 |
| `ExportCsv` | NotificationCenterVM | ❌ 无 | ✅ OK |

---

## 4. 总体评分与修复建议

### 4.1 评分矩阵

| 审查维度 | 评分 | 关键发现 |
|---------|------|---------|
| **XAML 绑定完整性** | ⭐⭐ 2.5/5 | MonitorView 大量硬编码；Dashboard 无增量更新 |
| **高频集合节流** | ⭐⭐ 2/5 | OutputSeries 5 秒全量重建导致 LiveCharts 重绘风暴 |
| **INotifyPropertyChanged 效率** | ⭐⭐⭐⭐ 4/5 | CommunityToolkit 内置值保护，无重复触发，但链式通知链可优化 |
| **CanExecute 防呆** | ⭐⭐ 2.5/5 | 仅 UserVM 3 个命令有 CanExecute；7 个危险操作无保护 |
| **整体** | ⭐⭐⭐ 2.75/5 | 基础绑定架构正确，但在高频刷新和命令防呆上有明显短板 |

### 4.2 P0 — 立即修复

#### 1. DashboardView 高频刷新 → LiveCharts 增量更新

```csharp
// 当前：每次全量重建
OutputSeries = new ISeries[] { new StackedColumnSeries<int> { Values = newValues } };

// 建议1：使用 ObservableCollection<ISeries>，仅更新 Values 属性
private readonly ObservableCollection<ISeries> _outputSeries = new();
public ObservableCollection<ISeries> OutputSeries => _outputSeries;

// 且在 RefreshHourlyChartsAsync 中仅替换 Values（保留 Series 对象）
```

**估算**：1 天（涉及 LiveCharts 的 5 个图表）

#### 2. ConfigViewModel.TestConnectionAsync — 添加 CanExecute

```csharp
[ObservableProperty] private bool _isTestingConnection;
[RelayCommand(CanExecute = nameof(CanTestConnection))]
private async Task TestConnectionAsync(StationConfig config) { ... }
private bool CanTestConnection => !IsTestingConnection;
```
**估算**：30 分钟

#### 3. HistoryViewModel.PrintTraceAsync — 添加 CanExecute

```csharp
[RelayCommand(CanExecute = nameof(IsNotPrinting))]
private async Task PrintTraceAsync(CancellationToken cancellationToken) { ... }
```
**估算**：15 分钟

### 4.3 P1 — 短期优化

4. **MonitorView 硬编码数据绑定化**
   - ProgressBar.Value → 绑定到 `StationState.Progress` 属性
   - "最近检测条码" → 绑定到 `MonitorViewModel.RecentBarcodes` 集合
   - "系统警报" → 绑定到 `MonitorViewModel.Alerts` 集合
   - 估算：2-3 天

5. **HistoryView 分页按钮 CanExecute**
   ```csharp
   [RelayCommand(CanExecute = nameof(CanGoPrevious))]
   private void PreviousPage() { ... }
   private bool CanGoPrevious => CurrentPage > 1;
   ```
   - 估算：15 分钟

### 4.4 P2 — 架构改进

6. **引入弱引用事件订阅**（避免 ViewModel 未 Disposed 时的泄漏风险）
7. **MainWindow.HeaderStations 改用消息驱动更新**（解耦对 MonitorVM 的直接集合索引）

### 4.5 做得好的部分

| 好的实践 | 说明 |
|----------|------|
| ✅ `VirtualizingStackPanel.IsVirtualizing="True"` | HistoryView DataGrid 虚拟化，大数据集不卡 UI |
| ✅ 所有 `[ObservableProperty]` 使用值比较 | 避免无效 PropertyChanged |
| ✅ UserVM 3 个操作正确使用 CanExecute | RBAC 权限检查通过 CanExecute 拦截 |
| ✅ DashboardView 环形进度条 code-behind | 正确的 View 层 UI 编码（WPF DoubleCollection 限制） |
| ✅ MainWindow 使用 DataTemplate 匹配 ViewModel 类型 | 标准的 MVVM ContentControl 导航模式 |

---

## 附录：全部 XAML 绑定表达式分类清单

### 绑定到 `ObservableCollection`（按类）

| XAML 文件 | 绑定表达式 | 源属性 | 更新频率 |
|-----------|-----------|--------|---------|
| MainWindow | `{Binding HeaderStations}` | `MainVM.HeaderStations`（OC） | 低频 |
| DashboardView | `{Binding DefectList}` | `DashboardVM.DefectList`（OC） | 5 秒 |
| DashboardView | `{Binding TopFaultList}` | `DashboardVM.TopFaultList`（OC） | 5 秒 |
| HistoryView | `{Binding TestResults}` | `HistoryVM.TestResults`（OC） | 查询触发 |
| MonitorView | `{Binding NoLoadStations[0].Barcode}` | `MonitorVM.NoLoadStations[...]`（OC） | 1 秒/事件 |
| MonitorView | `{Binding NoiseStations[0].FwdNoise}` | `MonitorVM.NoiseStations[...]`（OC） | 1 秒/事件 |
| MonitorView | `{Binding LoadStations[0].LoadCurrent}` | `MonitorVM.LoadStations[...]`（OC） | 1 秒/事件 |

### 绑定到 `ISeries[]` 的高频属性

| XAML 文件 | 绑定表达式 | 更新频率 | 风险 |
|-----------|-----------|---------|------|
| DashboardView | `{Binding OutputSeries}` | 5 秒 | 🔴 全量重建 |
| DashboardView | `{Binding XAxes}` | 5 秒 | 🔴 全量重建 |
| DashboardView | `{Binding PassRateSeries}` | 5 秒 | 🔴 全量重建 |
| DashboardView | `{Binding DefectDistributionSeries}` | 5 秒 | 🟡 中等 |
| HistoryView | `{Binding NoLoadWaveformSeries}` | 选中行切换 | 🟢 低频 |
| HistoryView | `{Binding NoiseWaveformSeries}` | 选中行切换 | 🟢 低频 |

---

*报告结束。建议优先修复 P0 的 3 个问题，可显著提升生产看板流畅度和危险操作防护。*
