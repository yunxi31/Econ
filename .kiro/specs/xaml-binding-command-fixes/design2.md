# XAML 绑定与 ICommand 缺陷修复设计文档

## Overview

本设计文档针对 MotorTestSystem 项目中 XAML 绑定与 ICommand 的 5 个缺陷进行修复。这些缺陷分为三类：

1. **性能问题**：DashboardView 高频刷新导致 LiveCharts 完整重绘，引发 UI 卡顿
2. **并发安全问题**：ConfigViewModel.TestConnectionAsync 和 HistoryViewModel.PrintTraceAsync 缺少并发保护，可能导致资源阻塞
3. **数据绑定问题**：MonitorView 大量硬编码导致 UI 无法反映实际数据，HistoryView 分页按钮缺少边界检查

修复策略采用最小化改动原则，确保现有功能不受影响。

## Glossary

- **Bug_Condition (C)**: 触发缺陷的条件集合（高频刷新、并发点击、硬编码值、边界翻页）
- **Property (P)**: 修复后的期望行为（增量更新、并发保护、动态绑定、边界防呆）
- **Preservation**: 必须保持不变的现有行为（图表数据正确性、命令功能、现有绑定、其他命令）
- **LiveCharts ISeries**: LiveCharts 图表库的数据系列对象，包含 Values 属性用于存储图表数据
- **AsyncRelayCommand**: CommunityToolkit.Mvvm 提供的异步命令类型，支持 CanExecute 状态控制
- **StationState**: 工位状态数据模型，包含 Barcode、Result、Progress 等属性
- **ObservableCollection**: WPF 数据绑定的集合类型，变更时自动通知 UI 更新

## Bug Details

### Bug Condition

#### Bug 1: DashboardView 高频刷新导致 LiveCharts 重绘风暴

缺陷发生在 `DashboardViewModel.RefreshSummary()` 每 5 秒执行时，重新创建整个 `ISeries[]` 数组并赋值给 `OutputSeries`、`PassRateSeries`、`DefectDistributionSeries`，导致 LiveCharts 检测到引用变化后触发完整图表重绘。

**Formal Specification:**
```
FUNCTION isBugCondition_ChartRefresh(input)
  INPUT: input of type (ISeries[] oldSeries, ISeries[] newSeries)
  OUTPUT: boolean
  
  RETURN oldSeries != newSeries  // 引用不同
         AND oldSeries.Length == newSeries.Length
         AND ForAll i IN [0..Length-1]:
               oldSeries[i].Values == newSeries[i].Values  // 数据值相同
         AND RefreshInterval <= 5 seconds  // 高频刷新
END FUNCTION
```

**Examples:**
- **场景 1**: 定时器触发 `RefreshSummary()` → 创建新 `ISeries[]` → LiveCharts 检测引用变化 → 触发完整重绘 → UI 卡顿 200ms-1s
- **场景 2**: 5 个图表同时刷新 → 累积卡顿时间达 1-5 秒 → 生产看板失去实时性
- **场景 3**: 数据未变化（产量统计相同）→ 仍然触发重绘 → 无效性能开销

#### Bug 2: ConfigViewModel.TestConnectionAsync 无并发保护

缺陷发生在用户在 PLC 连接测试进行中再次点击"测试连接"按钮时，系统允许同时发起多个并发 TCP 连接请求到同一 PLC 端口。

**Formal Specification:**
```
FUNCTION isBugCondition_ConcurrentTest(input)
  INPUT: input of type (ButtonClickEvent event, bool isTestingConnection)
  OUTPUT: boolean
  
  RETURN event.ButtonName == "TestConnection"
         AND isTestingConnection == true  // 已有测试在进行中
         AND TestConnectionCommand.CanExecute == true  // 命令仍可执行
END FUNCTION
```


**Examples:**
- **场景 1**: 用户点击"测试连接" → `TestConnectionAsync` 开始执行 → 用户再次点击 → 第二个请求并发执行 → PLC 端口阻塞
- **场景 2**: 网络延迟 5 秒 → 用户连续点击 3 次 → 3 个并发 TCP 连接 → 产生大量诊断日志、多次弹窗
- **场景 3**: 连接测试超时 30 秒 → 用户不知道是否在执行 → 多次点击尝试 → 资源耗尽

#### Bug 3: HistoryViewModel.PrintTraceAsync 缺少 CanExecute

缺陷发生在用户在打印追溯单进行中再次点击"打印"按钮时，系统允许同时调用多个 `PrintTraceAsync` 方法，可能导致 XPS 写入器冲突。

**Formal Specification:**
```
FUNCTION isBugCondition_ConcurrentPrint(input)
  INPUT: input of type (ButtonClickEvent event, bool isPrinting)
  OUTPUT: boolean
  
  RETURN event.ButtonName == "PrintTrace"
         AND isPrinting == true  // 已有打印任务在进行中
         AND PrintTraceCommand.CanExecute == true  // 命令仍可执行
END FUNCTION
```

**Examples:**
- **场景 1**: 用户点击"打印追溯单" → `PrintTraceAsync` 开始执行 → 用户再次点击 → 第二个打印任务并发执行 → `InvalidOperationException: XPS 写入器正在使用中`
- **场景 2**: 打印对话框加载慢 → 用户以为没响应再次点击 → 出现多个打印对话框
- **场景 3**: 打印队列阻塞 → 用户多次点击 → 产生重复的打印任务和混乱日志

#### Bug 4: MonitorView 大量硬编码导致数据不更新

缺陷发生在 MonitorView.xaml 中大量使用硬编码值（如 `Value="60"`、`Text="SN-9948x-XA1"`、`Text="-- V"`），导致 PLC 轮询服务更新数据后 UI 不会反映变化。


**Formal Specification:**
```
FUNCTION isBugCondition_HardcodedValue(input)
  INPUT: input of type (UIElement element, StationState actualData)
  OUTPUT: boolean
  
  RETURN element.HasHardcodedValue == true  // 硬编码值（非绑定）
         AND actualData.PropertyValue != element.DisplayedValue  // 实际数据已变化
         AND actualData.LastUpdateTime > Application.StartTime  // 数据已更新
END FUNCTION
```

**Examples:**
- **场景 1**: PLC 轮询服务更新 `StationState.Progress = 75` → MonitorView 中的 ProgressBar 保持 `Value="60"` → 用户看到错误进度
- **场景 2**: 系统产生新检测条码 "SN-12345-XA2" → MonitorView 显示硬编码的 "SN-9948x-XA1" → 无法追踪当前检测
- **场景 3**: A2 工位参数变化（电压 220V、电流 5A）→ UI 显示 `"-- V"`, `"-- A"` → 无法监控实际参数
- **场景 4**: 系统警报触发 "A5: 温度过高" → UI 显示硬编码的 "A4: 噪音超标" → 错过关键警报

#### Bug 5: HistoryView 分页按钮缺少 CanExecute

缺陷发生在用户在第 1 页或最后一页时点击"上一页"/"下一页"按钮，系统允许执行命令导致 `CurrentPage` 超出有效范围。

**Formal Specification:**
```
FUNCTION isBugCondition_InvalidPagination(input)
  INPUT: input of type (ButtonClickEvent event, int currentPage, int totalPages)
  OUTPUT: boolean
  
  RETURN (event.ButtonName == "PreviousPage" AND currentPage <= 1)
         OR (event.ButtonName == "NextPage" AND currentPage >= totalPages)
         AND Command.CanExecute == true  // 命令仍可执行
END FUNCTION
```

**Examples:**
- **场景 1**: `CurrentPage = 1` → 用户点击"上一页" → `PreviousPage` 命令执行 → `CurrentPage` 可能变为 0 → 数据查询失败
- **场景 2**: `CurrentPage = TotalPages = 5` → 用户点击"下一页" → `NextPage` 命令执行 → `CurrentPage = 6` → 空数据页

- **场景 3**: `TotalPages` 重新计算后减小 → `CurrentPage` 超出新范围 → 但按钮仍可点击 → 越界访问

## Expected Behavior

### Preservation Requirements

**Unchanged Behaviors:**
- **图表数据正确性**: 修改为增量更新后，产量统计、合格率趋势、缺陷分布等图表数据必须与修改前完全一致
- **图表动画效果**: LiveCharts 的动画效果（柱状图增长动画、折线图平滑过渡）必须继续工作
- **连接测试功能**: 单次点击"测试连接"按钮时，PLC 连接测试必须正确执行并显示成功/失败结果和详细错误信息
- **打印功能**: 单次点击"打印追溯单"按钮时，XPS 文档生成和打印对话框调用必须正常工作，取消操作必须正确处理
- **现有绑定功能**: MonitorView 中现有的正确绑定（如 `NoLoadStations[0].Barcode`、`NoiseStations[0].FwdNoise`）必须继续正常工作
- **分页核心功能**: 用户在有效范围内翻页时，`PreviousPage` 和 `NextPage` 命令必须正确加载对应页数据，`PageSize` 变化时必须正确重新计算 `TotalPages`
- **其他命令功能**: 其他 ICommand（如 `Search`、`Export`、`ViewReport`、`AddUser`、`ResetPassword` 等）必须不受本次修复影响

**Scope:**
所有未明确列入修复范围的功能应完全不受影响。具体包括：
- 非图表相关的 UI 更新（文本、按钮、输入框等）
- 非 TestConnection 和 PrintTrace 的其他异步命令
- MonitorView 之外的其他视图的硬编码值（如果存在）
- 非分页相关的 HistoryViewModel 功能（搜索、导出、重置等）

## Hypothesized Root Cause

基于代码分析和缺陷描述，5 个缺陷的根本原因如下：

### Bug 1: DashboardViewModel 构造函数中直接赋值数组


**根本原因**: `DashboardViewModel` 构造函数中使用 `OutputSeries = (ISeries[])(object)new ISeries[2] { ... }` 创建固定数组。由于 `OutputSeries` 是普通属性（非 ObservableCollection），每次数据更新需要重新赋值整个数组，LiveCharts 检测到引用变化后触发完整重绘。

**为什么不使用 ObservableCollection**: 开发者可能不知道 LiveCharts 支持 ObservableCollection，或者误以为数组赋值更简单。

**为什么触发完整重绘**: LiveCharts 内部通过引用比较检测 Series 变化，引用不同时认为是全新图表，触发布局计算、渲染管线、动画重建。

### Bug 2: AsyncRelayCommand 未指定 CanExecute 参数

**根本原因**: `ConfigViewModel.TestConnectionAsync` 使用 `[RelayCommand]` 特性生成命令，未提供 `CanExecute` 参数。CommunityToolkit.Mvvm 生成的命令默认 `CanExecute = true`，无法根据 `IsTestingConnection` 状态动态禁用。

**为什么缺少并发保护**: 开发者可能未考虑用户快速点击场景，或者不熟悉 `AsyncRelayCommand` 的 CanExecute 机制。

**为什么会阻塞端口**: TCP 连接建立需要三次握手，多个并发连接同时占用 PLC 端口的连接队列，超过队列长度后新连接被拒绝或超时。

### Bug 3: AsyncRelayCommand 未指定 CanExecute 参数（与 Bug 2 同根源）

**根本原因**: `HistoryViewModel.PrintTraceAsync` 同样使用 `[RelayCommand]` 特性，未提供 `CanExecute` 参数。打印任务涉及 XPS 写入器的独占访问，并发调用会触发 `InvalidOperationException`。

**为什么 XPS 写入器冲突**: XPS 文档生成使用 `XpsDocumentWriter`，内部维护文件句柄和写入状态，不支持并发写入同一文档。

### Bug 4: MonitorView.xaml 使用硬编码值而非 Binding

**根本原因**: MonitorView.xaml 中大量使用 `Value="60"`、`Text="SN-9948x-XA1"` 等硬编码值，未使用 `{Binding}` 语法绑定到 `StationState` 属性。


**为什么使用硬编码**: 可能原因包括：
- 原型开发阶段使用的 Mock 数据，忘记替换为绑定
- 开发者不熟悉 XAML 数据绑定机制
- `StationState` 模型缺少某些属性（如 `Progress`、`Voltage`、`Current`），导致无法绑定

**为什么数据不更新**: WPF 数据绑定基于属性路径解析，硬编码值不参与绑定系统，`PropertyChanged` 事件无法触发 UI 更新。

### Bug 5: RelayCommand 未指定 CanExecute 参数

**根本原因**: `HistoryViewModel.PreviousPage` 和 `NextPage` 使用 `[RelayCommand]` 特性，未提供 `CanExecute` 参数。虽然命令实现中包含 `if (CurrentPage > 1)` 边界检查，但按钮不会根据状态自动禁用。

**为什么需要 CanExecute**: WPF 命令系统支持通过 `CanExecute` 返回值自动禁用按钮（`IsEnabled = false`），提供更好的用户体验和防呆保护。

**为什么当前检查不够**: 命令内部的 `if` 检查只能阻止越界，但按钮仍然可点击（视觉上无禁用状态），用户无法感知边界。

## Correctness Properties

Property 1: Bug Condition - 图表增量更新

_For any_ 图表刷新事件，当数据值发生变化时，修复后的 `DashboardViewModel` SHALL 仅更新 `ISeries.Values` 属性而不重新创建 `ISeries` 对象，触发 LiveCharts 增量重绘而非完整重绘，UI 卡顿时间从 200ms-1s 降低到 <50ms。

**Validates: Requirements 2.1, 2.2, 2.3**

Property 2: Bug Condition - 命令并发保护

_For any_ `TestConnectionAsync` 或 `PrintTraceAsync` 命令执行期间的按钮点击事件，修复后的命令 SHALL 通过 `CanExecute = false` 阻止并发执行，确保单次操作完成前按钮被禁用。

**Validates: Requirements 2.4, 2.5, 2.6, 2.7, 2.8, 2.9**


Property 3: Bug Condition - 硬编码数据绑定化

_For any_ PLC 轮询服务更新 `StationState` 数据时，修复后的 `MonitorView` SHALL 通过数据绑定实时更新 ProgressBar、条码显示、参数显示、警报显示，确保 UI 与实际数据一致。

**Validates: Requirements 2.10, 2.11, 2.12**

Property 4: Bug Condition - 分页边界防呆

_For any_ 分页按钮点击事件，当 `CurrentPage` 处于边界时（第 1 页或最后一页），修复后的命令 SHALL 通过 `CanExecute = false` 禁用对应按钮，阻止越界操作。

**Validates: Requirements 2.13, 2.14, 2.15**

Property 5: Preservation - 图表数据正确性

_For any_ 图表数据更新事件，修复后的 `DashboardViewModel` SHALL 产生与修改前完全一致的图表数据值和动画效果，确保图表显示不受增量更新影响。

**Validates: Requirements 3.1, 3.2**

Property 6: Preservation - 命令功能完整性

_For any_ 单次命令执行（无并发场景），修复后的 `TestConnectionAsync` 和 `PrintTraceAsync` SHALL 产生与修改前完全一致的结果（连接测试结果、错误信息、打印文档、取消处理）。

**Validates: Requirements 3.3, 3.4, 3.5, 3.6**

Property 7: Preservation - 现有绑定和其他功能

_For any_ 非修复范围的功能（现有绑定、分页核心功能、其他命令、MonitorView 外的视图），修复后的代码 SHALL 产生与修改前完全一致的行为。

**Validates: Requirements 3.7, 3.8, 3.9, 3.10, 3.11, 3.12**

## Fix Implementation

### Changes Required

修复策略采用最小化改动原则，针对 5 个缺陷分别实施：

#### Fix 1: DashboardViewModel - 图表增量更新


**File**: `decompiled_src/MotorTestSystem.ViewModels/DashboardViewModel.cs`

**Specific Changes**:

1. **将 ISeries[] 改为 ObservableCollection<ISeries>**:
   - `public ISeries[] OutputSeries` → `public ObservableCollection<ISeries> OutputSeries`
   - `public ISeries[] PassRateSeries` → `public ObservableCollection<ISeries> PassRateSeries`
   - `public ISeries[] DefectDistributionSeries` → `public ObservableCollection<ISeries> DefectDistributionSeries`

2. **构造函数中初始化 ObservableCollection**:
   ```csharp
   OutputSeries = new ObservableCollection<ISeries>
   {
       new StackedColumnSeries<int> { Name = "合格", Values = new ObservableCollection<int> { ... }, ... },
       new StackedColumnSeries<int> { Name = "不合格", Values = new ObservableCollection<int> { ... }, ... }
   };
   ```

3. **添加 RefreshHourlyChartsAsync 方法**（替代直接赋值）:
   ```csharp
   private async Task RefreshHourlyChartsAsync()
   {
       // 从 repository 获取每小时产量数据
       var hourlyData = await _repository.GetHourlyProductionAsync(...);
       
       // 增量更新 OutputSeries[0].Values (合格数据)
       var okSeries = (StackedColumnSeries<int>)OutputSeries[0];
       UpdateValues(okSeries.Values, hourlyData.OkCounts);
       
       // 增量更新 OutputSeries[1].Values (不合格数据)
       var ngSeries = (StackedColumnSeries<int>)OutputSeries[1];
       UpdateValues(ngSeries.Values, hourlyData.NgCounts);
       
       // 类似方式更新 PassRateSeries 和 DefectDistributionSeries
   }
   
   private void UpdateValues<T>(IEnumerable<T> target, IEnumerable<T> source)
   {
       if (target is ObservableCollection<T> collection)
       {
           collection.Clear();
           foreach (var item in source) collection.Add(item);
       }
   }
   ```

4. **修改定时器回调**:
   ```csharp
   _refreshTimer.Tick += async (s, e) =>
   {
       RefreshSummary();
       await RefreshHourlyChartsAsync();  // 增量更新图表
   };
   ```

#### Fix 2: ConfigViewModel - TestConnectionAsync 并发保护


**File**: `decompiled_src/MotorTestSystem.ViewModels/ConfigViewModel.cs`

**Specific Changes**:

1. **添加 IsTestingConnection 属性**:
   ```csharp
   [ObservableProperty]
   private bool _isTestingConnection;
   ```

2. **修改 RelayCommand 特性，添加 CanExecute 参数**:
   ```csharp
   [RelayCommand(CanExecute = nameof(CanTestConnection))]
   private async Task TestConnectionAsync(StationConfig config)
   {
       IsTestingConnection = true;
       try
       {
           // 现有测试连接逻辑
           ...
       }
       finally
       {
           IsTestingConnection = false;
       }
   }
   
   private bool CanTestConnection(StationConfig config)
   {
       return !IsTestingConnection && config != null;
   }
   ```

3. **确保 PropertyChanged 触发命令状态更新**:
   - `[ObservableProperty]` 自动生成 `PropertyChanged` 事件
   - CommunityToolkit.Mvvm 自动调用 `TestConnectionCommand.NotifyCanExecuteChanged()`

#### Fix 3: HistoryViewModel - PrintTraceAsync 并发保护

**File**: `decompiled_src/MotorTestSystem.ViewModels/HistoryViewModel.cs`

**Specific Changes**:

1. **添加 IsPrinting 属性**:
   ```csharp
   [ObservableProperty]
   private bool _isPrinting;
   ```

2. **添加 PrintTraceAsync 命令**（假设原代码缺少此命令）:
   ```csharp
   [RelayCommand(CanExecute = nameof(CanPrintTrace))]
   private async Task PrintTraceAsync(MotorTestRecordModel record)
   {
       IsPrinting = true;
       try
       {
           // XPS 文档生成和打印逻辑
           await Task.Run(() => {
               // 生成 XPS 文档
               // 调用打印对话框
           });
       }
       finally
       {
           IsPrinting = false;
       }
   }
   
   private bool CanPrintTrace(MotorTestRecordModel record)
   {
       return !IsPrinting && record != null;
   }
   ```


#### Fix 4: MonitorView - 硬编码数据绑定化

**File**: `decompiled_src/MotorTestSystem.ViewModels/MonitorViewModel.cs`

**Specific Changes**:

1. **添加缺失的属性到 StationState 模型**（如果不存在）:
   ```csharp
   // 在 MotorTestSystem.Models.StationState 中添加
   public double Progress { get; set; }  // 进度百分比 0-100
   public double Voltage { get; set; }   // 电压
   public double Current { get; set; }   // 电流
   public int RPM { get; set; }          // 转速
   ```

2. **添加 RecentBarcodes 和 Alerts 集合到 MonitorViewModel**:
   ```csharp
   public ObservableCollection<string> RecentBarcodes { get; } = new ObservableCollection<string>();
   public ObservableCollection<string> Alerts { get; } = new ObservableCollection<string>();
   ```

3. **在 ApplySnapshot 中更新新增属性**:
   ```csharp
   private void ApplySnapshot(StationSnapshot snapshot)
   {
       if (_stationsById.TryGetValue(snapshot.StationId, out StationState value))
       {
           // 现有逻辑
           ...
           
           // 新增属性更新
           value.Progress = snapshot.CompletedData?.Progress ?? value.Progress;
           value.Voltage = snapshot.CompletedData?.Voltage ?? value.Voltage;
           value.Current = snapshot.CompletedData?.Current ?? value.Current;
           value.RPM = snapshot.CompletedData?.RPM ?? value.RPM;
           
           // 更新最近条码列表
           if (!string.IsNullOrEmpty(value.Barcode))
           {
               RecentBarcodes.Insert(0, $"{snapshot.StationId}: {value.Barcode}");
               while (RecentBarcodes.Count > 5) RecentBarcodes.RemoveAt(5);
           }
       }
   }
   ```

4. **添加警报管理逻辑**:
   ```csharp
   private void OnLogReceived(object? sender, string message)
   {
       RunOnUiThread(delegate
       {
           SystemLogs.Insert(0, $"{DateTime.Now:HH:mm:ss} {message}");
           while (SystemLogs.Count > 10) SystemLogs.RemoveAt(SystemLogs.Count - 1);
           
           // 如果是警报消息，添加到 Alerts 集合
           if (message.Contains("警报") || message.Contains("超标") || message.Contains("异常"))
           {
               Alerts.Insert(0, message);
               while (Alerts.Count > 5) Alerts.RemoveAt(5);
           }
       });
   }
   ```


**File**: `decompiled_src/MotorTestSystem.Views/MonitorView.xaml` (假设路径)

**Specific Changes**:

1. **ProgressBar 硬编码值改为绑定**:
   ```xml
   <!-- 修改前 -->
   <ProgressBar Value="60" Maximum="100" />
   
   <!-- 修改后 -->
   <ProgressBar Value="{Binding NoLoadStations[0].Progress}" Maximum="100" />
   ```

2. **条码硬编码值改为绑定**:
   ```xml
   <!-- 修改前 -->
   <TextBlock Text="SN-9948x-XA1" />
   
   <!-- 修改后 -->
   <ItemsControl ItemsSource="{Binding RecentBarcodes}">
       <ItemsControl.ItemTemplate>
           <DataTemplate>
               <TextBlock Text="{Binding}" />
           </DataTemplate>
       </ItemsControl.ItemTemplate>
   </ItemsControl>
   ```

3. **工位参数硬编码值改为绑定**:
   ```xml
   <!-- 修改前 -->
   <TextBlock Text="-- V" />
   <TextBlock Text="-- A" />
   <TextBlock Text="-- RPM" />
   
   <!-- 修改后 -->
   <TextBlock Text="{Binding NoLoadStations[1].Voltage, StringFormat={}{0} V}" />
   <TextBlock Text="{Binding NoLoadStations[1].Current, StringFormat={}{0} A}" />
   <TextBlock Text="{Binding NoLoadStations[1].RPM, StringFormat={}{0} RPM}" />
   ```

4. **系统警报硬编码值改为绑定**:
   ```xml
   <!-- 修改前 -->
   <TextBlock Text="A4: 噪音超标" />
   
   <!-- 修改后 -->
   <ItemsControl ItemsSource="{Binding Alerts}">
       <ItemsControl.ItemTemplate>
           <DataTemplate>
               <TextBlock Text="{Binding}" />
           </DataTemplate>
       </ItemsControl.ItemTemplate>
   </ItemsControl>
   ```

#### Fix 5: HistoryViewModel - 分页按钮 CanExecute

**File**: `decompiled_src/MotorTestSystem.ViewModels/HistoryViewModel.cs`

**Specific Changes**:

1. **修改 PreviousPage 命令，添加 CanExecute**:
   ```csharp
   [RelayCommand(CanExecute = nameof(CanPreviousPage))]
   private void PreviousPage()
   {
       CurrentPage--;  // 简化：CanExecute 已确保 CurrentPage > 1
   }
   
   private bool CanPreviousPage()
   {
       return CurrentPage > 1;
   }
   ```


2. **修改 NextPage 命令，添加 CanExecute**:
   ```csharp
   [RelayCommand(CanExecute = nameof(CanNextPage))]
   private void NextPage()
   {
       CurrentPage++;  // 简化：CanExecute 已确保 CurrentPage < TotalPages
   }
   
   private bool CanNextPage()
   {
       return CurrentPage < TotalPages;
   }
   ```

3. **确保 CurrentPage 和 TotalPages 变化时触发命令状态更新**:
   ```csharp
   partial void OnCurrentPageChanged(int value)
   {
       PreviousPageCommand.NotifyCanExecuteChanged();
       NextPageCommand.NotifyCanExecuteChanged();
   }
   
   partial void OnTotalPagesChanged(int value)
   {
       PreviousPageCommand.NotifyCanExecuteChanged();
       NextPageCommand.NotifyCanExecuteChanged();
   }
   ```

## Testing Strategy

### Validation Approach

测试策略分为三阶段：

1. **探索性 Bug Condition 检查**：在未修复代码上运行测试，确认缺陷存在并表征根本原因
2. **Fix Checking**：在修复后代码上运行测试，验证 Bug Condition 不再触发
3.**Preservation Checking**：对比修复前后行为，确保非 Bug 场景完全一致

### Exploratory Bug Condition Checking

**Goal**: 在未修复代码上表征 5 个缺陷，确认根本原因分析正确。

#### Test Plan 1: DashboardView 图表重绘性能

**Test Cases**:
1. **高频刷新测试** (期望在未修复代码上失败):
   - 启动应用并导航到 Dashboard 视图
   - 使用性能分析工具（WPF Performance Profiler）监控 UI 线程
   - 观察每 5 秒的刷新周期，测量 UI 线程阻塞时间
   - **Expected Counterexample**: UI 线程阻塞 200ms-1s，CPU 使用率峰值 40-60%

2. **引用变化检测测试** (期望在未修复代码上失败):
   - 在 `RefreshSummary()` 方法中添加断点
   - 检查 `OutputSeries` 对象的引用地址（使用 Debugger）
   - 确认每次刷新创建新的 `ISeries[]` 对象
   - **Expected Counterexample**: 每次刷新 `OutputSeries` 引用地址变化

3. **无效刷新检测测试** (期望在未修复代码上失败):
   - Mock `_repository.GetSummaryAsync()` 返回相同数据
   - 观察 LiveCharts 是否仍触发重绘
   - **Expected Counterexample**: 数据未变化但仍触发完整重绘


#### Test Plan 2: ConfigViewModel 并发测试保护

**Test Cases**:
1. **并发点击测试** (期望在未修复代码上失败):
   - 启动应用并导航到 Config 视图
   - Mock `TestConnectionAsync()` 延迟 5 秒（使用 `Task.Delay(5000)`）
   - 快速连续点击"测试连接"按钮 3 次
   - **Expected Counterexample**: 3 个并发请求同时执行，DiagnosticLogs 中出现 3 条测试日志

2. **按钮状态检测测试** (期望在未修复代码上失败):
   - 点击"测试连接"按钮后，检查按钮的 `IsEnabled` 属性
   - **Expected Counterexample**: 按钮保持 `IsEnabled = true`，可继续点击

#### Test Plan 3: HistoryViewModel 打印并发保护

**Test Cases**:
1. **并发打印测试** (期望在未修复代码上失败):
   - 启动应用并导航到 History 视图
   - Mock `PrintTraceAsync()` 延迟 3 秒
   - 快速连续点击"打印追溯单"按钮 2 次
   - **Expected Counterexample**: 抛出 `InvalidOperationException: XPS writer is already in use`

#### Test Plan 4: MonitorView 硬编码数据更新

**Test Cases**:
1. **ProgressBar 更新测试** (期望在未修复代码上失败):
   - 启动应用并导航到 Monitor 视图
   - Mock PLC 轮询服务，推送 `StationSnapshot` with `Progress = 85`
   - 检查 UI 中的 ProgressBar 显示值
   - **Expected Counterexample**: ProgressBar 保持硬编码值 `Value="60"`，不更新为 85

2. **条码显示测试** (期望在未修复代码上失败):
   - Mock PLC 推送新条码 "SN-12345-XA2"
   - 检查"最近检测条码"面板显示内容
   - **Expected Counterexample**: 显示硬编码的 "SN-9948x-XA1"，不更新为新条码

3. **工位参数显示测试** (期望在未修复代码上失败):
   - Mock PLC 推送 A2 工位参数 `Voltage=220, Current=5, RPM=3000`
   - 检查 A2 工位参数区域显示内容
   - **Expected Counterexample**: 显示硬编码的 `"-- V"`, `"-- A"`, `"-- RPM"`，不更新为实际值


#### Test Plan 5: HistoryView 分页边界检查

**Test Cases**:
1. **上一页边界测试** (期望在未修复代码上失败):
   - 启动应用并导航到 History 视图
   - 设置 `CurrentPage = 1`
   - 检查"上一页"按钮的 `IsEnabled` 属性
   - **Expected Counterexample**: 按钮保持 `IsEnabled = true`，点击后 `CurrentPage` 可能变为 0

2. **下一页边界测试** (期望在未修复代码上失败):
   - 设置 `CurrentPage = TotalPages = 5`
   - 检查"下一页"按钮的 `IsEnabled` 属性
   - **Expected Counterexample**: 按钮保持 `IsEnabled = true`，点击后 `CurrentPage` 变为 6

### Fix Checking

**Goal**: 验证修复后的代码在 Bug Condition 下产生期望行为。

#### Fix Check 1: 图表增量更新

**Pseudocode:**
```
FOR ALL chartRefreshEvent WHERE isBugCondition_ChartRefresh(event) DO
  result := RefreshHourlyChartsAsync_fixed()
  ASSERT OutputSeries reference unchanged
  ASSERT OutputSeries[0].Values updated
  ASSERT UI blocking time < 50ms
END FOR
```

**Test Cases**:
1. **引用保持测试**: 验证 `OutputSeries` 引用地址在多次刷新后保持不变
2. **Values 更新测试**: 验证 `OutputSeries[0].Values` 数据正确更新
3. **性能测试**: 验证 UI 线程阻塞时间 <50ms（使用 WPF Performance Profiler）

#### Fix Check 2: 命令并发保护

**Pseudocode:**
```
FOR ALL buttonClickEvent WHERE isBugCondition_ConcurrentTest(event) DO
  result := TestConnectionAsync_fixed()
  ASSERT IsTestingConnection == true during execution
  ASSERT Button.IsEnabled == false during execution
  ASSERT No concurrent execution
END FOR
```

**Test Cases**:
1. **按钮禁用测试**: 验证点击按钮后 `IsEnabled = false`
2. **并发阻止测试**: 验证快速连续点击只执行一次
3. **状态恢复测试**: 验证完成后 `IsEnabled = true`


#### Fix Check 3: 硬编码数据绑定化

**Pseudocode:**
```
FOR ALL plcUpdateEvent WHERE isBugCondition_HardcodedValue(event) DO
  result := ApplySnapshot_fixed(snapshot)
  ASSERT UI.ProgressBar.Value == snapshot.Progress
  ASSERT UI.BarcodeDisplay contains snapshot.Barcode
  ASSERT UI.ParameterDisplay == snapshot.Voltage, Current, RPM
END FOR
```

**Test Cases**:
1. **ProgressBar 绑定测试**: 验证 UI 更新为实际进度
2. **条码绑定测试**: 验证 UI 显示实际条码
3. **参数绑定测试**: 验证 UI 显示实际参数值

#### Fix Check 4: 分页边界防呆

**Pseudocode:**
```
FOR ALL paginationEvent WHERE isBugCondition_InvalidPagination(event) DO
  result := CanPreviousPage_fixed() OR CanNextPage_fixed()
  ASSERT result == false when at boundary
  ASSERT Button.IsEnabled == false when at boundary
END FOR
```

**Test Cases**:
1. **上一页边界测试**: 验证 `CurrentPage = 1` 时按钮禁用
2. **下一页边界测试**: 验证 `CurrentPage = TotalPages` 时按钮禁用
3. **中间页测试**: 验证 `1 < CurrentPage < TotalPages` 时两个按钮均启用

### Preservation Checking

**Goal**: 验证修复后的代码在非 Bug 场景下与原代码行为完全一致。

#### Preservation Check 1: 图表数据正确性

**Pseudocode:**
```
FOR ALL chartDataValue IN {产量统计, 合格率趋势, 缺陷分布} DO
  ASSERT fixed_code(dataValue) == original_code(dataValue)
  ASSERT chart.AnimationEnabled == true
END FOR
```

**Testing Approach**: 
- 对比修复前后的图表数据值（使用单元测试 Mock 相同输入）
- 验证 LiveCharts 动画效果仍然存在（通过 UI Automation 检测动画属性）

**Test Cases**:
1. **产量统计准确性**: 验证 `OutputSeries` 数据与 repository 查询结果一致
2. **合格率趋势准确性**: 验证 `PassRateSeries` 数据计算正确
3. **缺陷分布准确性**: 验证 `DefectDistributionSeries` 百分比正确
4. **动画效果**: 验证柱状图增长动画、折线图平滑过渡正常工作


#### Preservation Check 2: 命令功能完整性

**Pseudocode:**
```
FOR ALL singleCommandExecution (non-concurrent) DO
  ASSERT fixed_TestConnectionAsync(config) == original_TestConnectionAsync(config)
  ASSERT fixed_PrintTraceAsync(record) == original_PrintTraceAsync(record)
END FOR
```

**Testing Approach**:
- 对比单次命令执行的结果（连接测试结果、错误信息、打印文档内容）
- 验证取消操作的 CancellationToken 处理逻辑

**Test Cases**:
1. **连接成功场景**: 验证修复后返回"连接正常"消息，DiagnosticLogs 正确记录
2. **连接失败场景**: 验证修复后返回详细错误信息（如"连接超时"、"端口占用"）
3. **打印成功场景**: 验证修复后生成正确的 XPS 文档并调用打印对话框
4. **打印取消场景**: 验证修复后正确处理 CancellationToken，恢复 UI 状态

#### Preservation Check 3: 现有绑定和其他功能

**Pseudocode:**
```
FOR ALL nonBugFeature IN {现有绑定, 分页核心, 其他命令} DO
  ASSERT fixed_code(feature) == original_code(feature)
END FOR
```

**Testing Approach**:
- Property-Based Testing 推荐用于覆盖大量输入场景
- 对比修复前后的行为（使用 Golden Master Testing）

**Test Cases**:
1. **MonitorView 现有绑定**: 验证 `NoLoadStations[0].Barcode`、`NoiseStations[0].FwdNoise` 等现有绑定继续工作
2. **分页核心功能**: 验证有效范围内翻页正确加载数据，`PageSize` 变化正确重新计算 `TotalPages`
3. **其他命令**: 验证 `Search`、`Export`、`Reset`、`AddUser` 等命令不受影响
4. **权限检查**: 验证 UserViewModel 的命令 CanExecute 基于 `AppPermission.UserManagement` 仍然正常工作

### Unit Tests

#### DashboardViewModel 单元测试
- `RefreshHourlyChartsAsync_UpdatesSeriesValues_WithoutChangingReference`: 验证图表数据更新但引用不变
- `RefreshHourlyChartsAsync_SkipsUpdate_WhenDataUnchanged`: 验证数据未变化时跳过更新
- `RefreshHourlyChartsAsync_MaintainsChartAnimation`: 验证动画效果保持

#### ConfigViewModel 单元测试
- `TestConnectionAsync_DisablesButton_DuringExecution`: 验证按钮在执行期间禁用
- `TestConnectionAsync_EnablesButton_AfterCompletion`: 验证完成后按钮重新启用
- `TestConnectionAsync_BlocksConcurrentExecution`: 验证阻止并发执行


#### HistoryViewModel 单元测试
- `PrintTraceAsync_DisablesButton_DuringExecution`: 验证打印按钮在执行期间禁用
- `PrintTraceAsync_HandlesXpsWriterConflict`: 验证避免 XPS 写入器冲突
- `PreviousPage_Disabled_AtFirstPage`: 验证第 1 页时"上一页"按钮禁用
- `NextPage_Disabled_AtLastPage`: 验证最后一页时"下一页"按钮禁用
- `PaginationButtons_Enabled_AtMiddlePage`: 验证中间页时两个按钮均启用

#### MonitorViewModel 单元测试
- `ApplySnapshot_UpdatesProgressBar`: 验证 ProgressBar 数据更新
- `ApplySnapshot_UpdatesRecentBarcodes`: 验证条码列表更新
- `ApplySnapshot_UpdatesStationParameters`: 验证工位参数更新
- `OnLogReceived_UpdatesAlerts`: 验证警报列表更新

### Property-Based Tests

#### 图表数据准确性 (Property 5)
```csharp
[Property]
public Property ChartData_MatchesRepositoryData(DateTime startDate, DateTime endDate)
{
    return Prop.ForAll(
        Arb.From(GenValidDateRange()),
        (dates) => {
            var (start, end) = dates;
            var expected = _repository.GetHourlyProductionAsync(start, end).Result;
            var viewModel = new DashboardViewModel(_repository);
            
            // 验证 OutputSeries 数据与 expected 一致
            var okSeries = (StackedColumnSeries<int>)viewModel.OutputSeries[0];
            return okSeries.Values.SequenceEqual(expected.OkCounts);
        }
    );
}
```

#### 命令功能完整性 (Property 6)
```csharp
[Property]
public Property TestConnection_ProducesConsistentResults(StationConfig config)
{
    return Prop.ForAll(
        Arb.From(GenValidStationConfig()),
        (cfg) => {
            var originalResult = Original_TestConnectionAsync(cfg).Result;
            var fixedResult = Fixed_TestConnectionAsync(cfg).Result;
            
            return originalResult.IsSuccess == fixedResult.IsSuccess
                && originalResult.Message == fixedResult.Message;
        }
    );
}
```

#### 现有绑定保持 (Property 7)
```csharp
[Property]
public Property ExistingBindings_RemainUnchanged(StationSnapshot snapshot)
{
    return Prop.ForAll(
        Arb.From(GenValidSnapshot()),
        (snap) => {
            var originalViewModel = new Original_MonitorViewModel();
            var fixedViewModel = new Fixed_MonitorViewModel();
            
            originalViewModel.ApplySnapshot(snap);
            fixedViewModel.ApplySnapshot(snap);
            
            // 验证现有绑定属性值一致
            return originalViewModel.NoLoadStations[0].Barcode == fixedViewModel.NoLoadStations[0].Barcode
                && originalViewModel.NoiseStations[0].FwdNoise == fixedViewModel.NoiseStations[0].FwdNoise;
        }
    );
}
```

### Integration Tests

#### 端到端图表刷新测试
- 启动完整应用，导航到 Dashboard 视图
- 等待多个刷新周期（至少 3 次 5 秒刷新）
- 验证 UI 流畅度（帧率 >30fps）、数据正确性、动画效果

#### 端到端并发保护测试
- 启动完整应用，导航到 Config 视图
- 使用 UI Automation 模拟快速连续点击"测试连接"按钮
- 验证按钮状态变化、DiagnosticLogs 记录数量、无并发请求

#### 端到端数据绑定测试
- 启动完整应用，导航到 Monitor 视图
- Mock PLC 轮询服务推送多个 `StationSnapshot` 更新
- 验证 UI 所有绑定元素（ProgressBar、条码、参数、警报）实时更新

#### 端到端分页测试
- 启动完整应用，导航到 History 视图
- 使用 UI Automation 测试分页边界（第 1 页、最后一页、中间页）
- 验证按钮禁用状态、数据加载正确性
 