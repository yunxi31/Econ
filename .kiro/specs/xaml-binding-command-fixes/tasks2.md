# 实现任务列表

## Bug 1: DashboardView 高频刷新导致 LiveCharts 重绘风暴

- [ ] 1. 编写 Bug Condition 探索测试 - 图表重绘性能
  - **Property 1: Bug Condition** - 图表增量更新
  - **关键**: 此测试在未修复代码上必须失败 - 失败证明缺陷存在
  - **不要尝试修复测试或代码**
  - **目标**: 发现反例证明缺陷存在
  - **Scoped PBT 方法**: 针对确定性缺陷，将属性范围限定为具体失败案例以确保可重现性
  - 测试实现细节来自设计文档中的 Bug Condition
  - 测试断言应匹配设计文档中的 Expected Behavior Properties
  - 在未修复代码上运行测试
  - **预期结果**: 测试失败（正确 - 证明缺陷存在）
  - 记录发现的反例以理解根本原因
  - 当测试编写、运行并记录失败后标记任务完成
  - _Requirements: 1.1, 1.2, 1.3_
  - **测试用例**:
    - 高频刷新测试: 每 5 秒触发 RefreshSummary，使用 WPF Performance Profiler 监控 UI 线程阻塞时间
    - 预期反例: UI 线程阻塞 200ms-1s，CPU 峰值 40-60%
    - 引用变化检测: 在 RefreshSummary 中断点检查 OutputSeries 引用地址
    - 预期反例: 每次刷新引用地址变化
    - 无效刷新检测: Mock repository 返回相同数据，观察是否仍触发重绘
    - 预期反例: 数据未变化但仍触发完整重绘

- [ ] 2. 编写 Preservation 属性测试 - 图表数据正确性（修复前）
  - **Property 2: Preservation** - 图表数据正确性
  - **重要**: 遵循观察优先方法论
  - 在未修复代码上用非缺陷输入观察行为（isBugCondition 返回 false 的情况）
  - 编写属性测试捕获设计文档 Preservation Requirements 中观察到的行为模式
  - 属性测试生成大量测试用例提供更强保证
  - 在未修复代码上运行测试
  - **预期结果**: 测试通过（确认要保持的基线行为）
  - 当测试编写、运行并在未修复代码上通过时标记任务完成
  - _Requirements: 3.1, 3.2_
  - **测试用例**:
    - 产量统计准确性: 验证 OutputSeries 数据与 repository 查询结果一致
    - 合格率趋势准确性: 验证 PassRateSeries 数据计算正确
    - 缺陷分布准确性: 验证 DefectDistributionSeries 百分比正确
    - 动画效果: 验证柱状图增长动画、折线图平滑过渡正常工作
    - 使用属性测试覆盖多种日期范围和数据分布场景

- [ ] 3. 修复 Bug 1: DashboardViewModel 图表增量更新

  - [ ] 3.1 将 ISeries[] 改为 ObservableCollection\<ISeries\>
    - 修改 `decompiled_src/MotorTestSystem.ViewModels/DashboardViewModel.cs`
    - 将 `public ISeries[] OutputSeries` 改为 `public ObservableCollection<ISeries> OutputSeries`
    - 同样修改 `PassRateSeries`、`DefectDistributionSeries`
    - _Bug_Condition: isBugCondition_ChartRefresh(input) where input.oldSeries != newSeries but data same_
    - _Expected_Behavior: 仅更新 ISeries.Values 属性，保持 ISeries 对象引用不变_
    - _Preservation: 图表数据正确性（Requirements 3.1, 3.2）_
    - _Requirements: 1.1, 1.2, 1.3, 2.1, 2.2, 2.3_

  - [ ] 3.2 构造函数中初始化 ObservableCollection
    - 在 DashboardViewModel 构造函数中初始化 ObservableCollection
    - 示例: `OutputSeries = new ObservableCollection<ISeries> { new StackedColumnSeries<int> { ... }, ... }`
    - _Requirements: 2.1_

  - [ ] 3.3 添加 RefreshHourlyChartsAsync 方法实现增量更新
    - 添加 `RefreshHourlyChartsAsync()` 方法
    - 从 repository 获取每小时产量数据
    - 增量更新 `OutputSeries[0].Values`（合格数据）
    - 增量更新 `OutputSeries[1].Values`（不合格数据）
    - 类似方式更新 PassRateSeries 和 DefectDistributionSeries
    - 添加 `UpdateValues<T>` 辅助方法：清空 ObservableCollection 并添加新数据
    - _Requirements: 2.2, 2.3_

  - [ ] 3.4 修改定时器回调使用增量更新
    - 修改 `_refreshTimer.Tick` 事件处理器
    - 调用 `await RefreshHourlyChartsAsync()` 替代直接赋值
    - _Requirements: 2.1, 2.2_

  - [ ] 3.5 验证 Bug Condition 探索测试现在通过
    - **Property 1: Expected Behavior** - 图表增量更新
    - **重要**: 重新运行步骤 1 中的相同测试 - 不要编写新测试
    - 步骤 1 的测试编码了期望行为
    - 当此测试通过时，确认期望行为已满足
    - 重新运行步骤 1 的 Bug Condition 探索测试
    - **预期结果**: 测试通过（确认缺陷已修复）
    - _Requirements: Expected Behavior Properties from design_

  - [ ] 3.6 验证 Preservation 测试仍然通过
    - **Property 2: Preservation** - 图表数据正确性
    - **重要**: 重新运行步骤 2 中的相同测试 - 不要编写新测试
    - 重新运行步骤 2 的 Preservation 属性测试
    - **预期结果**: 测试通过（确认无回归）
    - 确认所有测试在修复后仍通过（无回归）

- [ ] 4. Checkpoint - Bug 1 所有测试通过
  - 确保所有测试通过，如有疑问询问用户

## Bug 2: ConfigViewModel.TestConnectionAsync 无并发保护

- [ ] 5. 编写 Bug Condition 探索测试 - 并发测试保护
  - **Property 1: Bug Condition** - 命令并发保护（TestConnection）
  - **关键**: 此测试在未修复代码上必须失败
  - **目标**: 发现反例证明缺陷存在
  - 在未修复代码上运行测试
  - **预期结果**: 测试失败（正确 - 证明缺陷存在）
  - _Requirements: 2.1_
  - **测试用例**:
    - 并发点击测试: Mock TestConnectionAsync 延迟 5 秒，快速连续点击"测试连接"3 次
    - 预期反例: 3 个并发请求同时执行，DiagnosticLogs 中出现 3 条测试日志
    - 按钮状态检测: 点击按钮后检查 IsEnabled 属性
    - 预期反例: 按钮保持 IsEnabled = true，可继续点击

- [ ] 6. 编写 Preservation 属性测试 - 连接测试功能（修复前）
  - **Property 2: Preservation** - 命令功能完整性（TestConnection）
  - **重要**: 遵循观察优先方法论
  - 在未修复代码上观察单次点击行为（非并发场景）
  - 编写属性测试捕获观察到的行为模式
  - 在未修复代码上运行测试
  - **预期结果**: 测试通过
  - _Requirements: 3.3, 3.4_
  - **测试用例**:
    - 连接成功场景: 验证返回"连接正常"消息，DiagnosticLogs 正确记录
    - 连接失败场景: 验证返回详细错误信息（如"连接超时"、"端口占用"）
    - 使用属性测试覆盖多种 StationConfig 配置

- [ ] 7. 修复 Bug 2: ConfigViewModel 添加并发保护

  - [ ] 7.1 添加 IsTestingConnection 属性
    - 修改 `decompiled_src/MotorTestSystem.ViewModels/ConfigViewModel.cs`
    - 添加 `[ObservableProperty] private bool _isTestingConnection;`
    - _Bug_Condition: isBugCondition_ConcurrentTest(input) where isTestingConnection == true but CanExecute == true_
    - _Expected_Behavior: CanExecute = false when isTestingConnection == true_
    - _Preservation: 连接测试功能（Requirements 3.3, 3.4）_
    - _Requirements: 2.1, 2.4, 2.5, 2.6_

  - [ ] 7.2 修改 RelayCommand 特性添加 CanExecute
    - 修改 TestConnectionAsync 方法的 `[RelayCommand]` 特性
    - 添加 `CanExecute = nameof(CanTestConnection)` 参数
    - 在 TestConnectionAsync 开始时设置 `IsTestingConnection = true`
    - 在 finally 块中设置 `IsTestingConnection = false`
    - _Requirements: 2.4, 2.5, 2.6_

  - [ ] 7.3 添加 CanTestConnection 方法
    - 添加 `private bool CanTestConnection(StationConfig config)` 方法
    - 返回 `!IsTestingConnection && config != null`
    - _Requirements: 2.5_

  - [ ] 7.4 验证 Bug Condition 探索测试现在通过
    - **Property 1: Expected Behavior** - 命令并发保护（TestConnection）
    - 重新运行步骤 5 的相同测试
    - **预期结果**: 测试通过（确认缺陷已修复）

  - [ ] 7.5 验证 Preservation 测试仍然通过
    - **Property 2: Preservation** - 命令功能完整性（TestConnection）
    - 重新运行步骤 6 的相同测试
    - **预期结果**: 测试通过（确认无回归）

- [ ] 8. Checkpoint - Bug 2 所有测试通过

## Bug 3: HistoryViewModel.PrintTraceAsync 缺少 CanExecute

- [ ] 9. 编写 Bug Condition 探索测试 - 并发打印保护
  - **Property 1: Bug Condition** - 命令并发保护（PrintTrace）
  - **关键**: 此测试在未修复代码上必须失败
  - **目标**: 发现反例证明缺陷存在
  - 在未修复代码上运行测试
  - **预期结果**: 测试失败（抛出 InvalidOperationException）
  - _Requirements: 3.1, 3.2_
  - **测试用例**:
    - 并发打印测试: Mock PrintTraceAsync 延迟 3 秒，快速连续点击"打印"2 次
    - 预期反例: 抛出 InvalidOperationException: XPS writer is already in use

- [ ] 10. 编写 Preservation 属性测试 - 打印功能（修复前）
  - **Property 2: Preservation** - 命令功能完整性（PrintTrace）
  - **重要**: 遵循观察优先方法论
  - 在未修复代码上观察单次打印行为
  - 编写属性测试捕获观察到的行为模式
  - 在未修复代码上运行测试
  - **预期结果**: 测试通过
  - _Requirements: 3.5, 3.6_
  - **测试用例**:
    - 打印成功场景: 验证生成正确的 XPS 文档并调用打印对话框
    - 打印取消场景: 验证正确处理 CancellationToken，恢复 UI 状态

- [ ] 11. 修复 Bug 3: HistoryViewModel 添加并发保护

  - [ ] 11.1 添加 IsPrinting 属性
    - 修改 `decompiled_src/MotorTestSystem.ViewModels/HistoryViewModel.cs`
    - 添加 `[ObservableProperty] private bool _isPrinting;`
    - _Bug_Condition: isBugCondition_ConcurrentPrint(input) where isPrinting == true but CanExecute == true_
    - _Expected_Behavior: CanExecute = false when isPrinting == true_
    - _Preservation: 打印功能（Requirements 3.5, 3.6）_
    - _Requirements: 3.1, 3.2, 2.7, 2.8, 2.9_

  - [ ] 11.2 添加或修改 PrintTraceAsync 命令
    - 添加 `[RelayCommand(CanExecute = nameof(CanPrintTrace))]` 特性
    - 在 PrintTraceAsync 开始时设置 `IsPrinting = true`
    - 在 finally 块中设置 `IsPrinting = false`
    - 实现 XPS 文档生成和打印逻辑
    - _Requirements: 2.7, 2.8, 2.9_

  - [ ] 11.3 添加 CanPrintTrace 方法
    - 添加 `private bool CanPrintTrace(MotorTestRecordModel record)` 方法
    - 返回 `!IsPrinting && record != null`
    - _Requirements: 2.8_

  - [ ] 11.4 验证 Bug Condition 探索测试现在通过
    - **Property 1: Expected Behavior** - 命令并发保护（PrintTrace）
    - 重新运行步骤 9 的相同测试
    - **预期结果**: 测试通过（确认缺陷已修复）

  - [ ] 11.5 验证 Preservation 测试仍然通过
    - **Property 2: Preservation** - 命令功能完整性（PrintTrace）
    - 重新运行步骤 10 的相同测试
    - **预期结果**: 测试通过（确认无回归）

- [ ] 12. Checkpoint - Bug 3 所有测试通过

## Bug 4: MonitorView 大量硬编码导致数据不更新

- [ ] 13. 编写 Bug Condition 探索测试 - 硬编码数据更新
  - **Property 1: Bug Condition** - 硬编码数据绑定化
  - **关键**: 此测试在未修复代码上必须失败
  - **目标**: 发现反例证明缺陷存在
  - 在未修复代码上运行测试
  - **预期结果**: 测试失败（UI 不更新）
  - _Requirements: 4.1, 4.2, 4.3_
  - **测试用例**:
    - ProgressBar 更新测试: Mock PLC 推送 Progress=85，检查 UI ProgressBar 值
    - 预期反例: ProgressBar 保持 Value="60"，不更新为 85
    - 条码显示测试: Mock PLC 推送新条码 "SN-12345-XA2"，检查条码显示
    - 预期反例: 显示硬编码的 "SN-9948x-XA1"
    - 工位参数显示测试: Mock PLC 推送 Voltage=220, Current=5, RPM=3000
    - 预期反例: 显示硬编码的 "-- V", "-- A", "-- RPM"

- [ ] 14. 编写 Preservation 属性测试 - 现有绑定功能（修复前）
  - **Property 2: Preservation** - 现有绑定和其他功能
  - **重要**: 遵循观察优先方法论
  - 在未修复代码上观察现有正确绑定的行为
  - 编写属性测试捕获观察到的行为模式
  - 在未修复代码上运行测试
  - **预期结果**: 测试通过
  - _Requirements: 3.7, 3.8_
  - **测试用例**:
    - 验证现有绑定 NoLoadStations[0].Barcode 正常工作
    - 验证现有绑定 NoiseStations[0].FwdNoise 正常工作
    - 验证 PLC 轮询频率 1 秒/工位时低延迟更新

- [ ] 15. 修复 Bug 4: MonitorView 硬编码数据绑定化

  - [ ] 15.1 添加缺失属性到 StationState 模型
    - 修改 `decompiled_src/MotorTestSystem.Models/StationState.cs`
    - 添加 `public double Progress { get; set; }`（进度百分比 0-100）
    - 添加 `public double Voltage { get; set; }`（电压）
    - 添加 `public double Current { get; set; }`（电流）
    - 添加 `public int RPM { get; set; }`（转速）
    - _Bug_Condition: isBugCondition_HardcodedValue(input) where element.HasHardcodedValue == true_
    - _Expected_Behavior: UI 绑定到 StationState 属性并实时更新_
    - _Preservation: 现有绑定功能（Requirements 3.7, 3.8）_
    - _Requirements: 4.1, 4.2, 4.3, 2.10, 2.11, 2.12_

  - [ ] 15.2 添加 RecentBarcodes 和 Alerts 集合
    - 修改 `decompiled_src/MotorTestSystem.ViewModels/MonitorViewModel.cs`
    - 添加 `public ObservableCollection<string> RecentBarcodes { get; } = new();`
    - 添加 `public ObservableCollection<string> Alerts { get; } = new();`
    - _Requirements: 2.11_

  - [ ] 15.3 在 ApplySnapshot 中更新新增属性
    - 修改 MonitorViewModel.ApplySnapshot 方法
    - 更新 Progress, Voltage, Current, RPM 属性
    - 更新 RecentBarcodes 集合（最多 5 条）
    - _Requirements: 2.10, 2.11_

  - [ ] 15.4 添加警报管理逻辑
    - 修改 MonitorViewModel.OnLogReceived 方法
    - 检测警报消息（包含"警报"、"超标"、"异常"）
    - 添加到 Alerts 集合（最多 5 条）
    - _Requirements: 2.11_

  - [ ] 15.5 修改 MonitorView.xaml 绑定 ProgressBar
    - 找到并修改所有 MonitorView.xaml 中的 ProgressBar 硬编码值
    - 将 `Value="60"` 等改为 `Value="{Binding NoLoadStations[0].Progress}"`
    - 根据实际工位调整索引和集合名称
    - _Requirements: 2.10_

  - [ ] 15.6 修改 MonitorView.xaml 绑定条码显示
    - 找到并修改条码硬编码值
    - 将 `Text="SN-9948x-XA1"` 等改为 `ItemsSource="{Binding RecentBarcodes}"`
    - 使用 ItemsControl 和 DataTemplate 显示列表
    - _Requirements: 2.11_

  - [ ] 15.7 修改 MonitorView.xaml 绑定工位参数
    - 找到并修改工位参数硬编码值
    - 将 `Text="-- V"` 改为 `Text="{Binding NoLoadStations[1].Voltage, StringFormat={}{0} V}"`
    - 将 `Text="-- A"` 改为 `Text="{Binding NoLoadStations[1].Current, StringFormat={}{0} A}"`
    - 将 `Text="-- RPM"` 改为 `Text="{Binding NoLoadStations[1].RPM, StringFormat={}{0} RPM}"`
    - _Requirements: 2.12_

  - [ ] 15.8 修改 MonitorView.xaml 绑定系统警报
    - 找到并修改系统警报硬编码值
    - 将 `Text="A4: 噪音超标"` 等改为 `ItemsSource="{Binding Alerts}"`
    - 使用 ItemsControl 和 DataTemplate 显示列表
    - _Requirements: 2.11_

  - [ ] 15.9 验证 Bug Condition 探索测试现在通过
    - **Property 1: Expected Behavior** - 硬编码数据绑定化
    - 重新运行步骤 13 的相同测试
    - **预期结果**: 测试通过（确认缺陷已修复）

  - [ ] 15.10 验证 Preservation 测试仍然通过
    - **Property 2: Preservation** - 现有绑定和其他功能
    - 重新运行步骤 14 的相同测试
    - **预期结果**: 测试通过（确认无回归）

- [ ] 16. Checkpoint - Bug 4 所有测试通过

## Bug 5: HistoryView 分页按钮缺少 CanExecute

- [ ] 17. 编写 Bug Condition 探索测试 - 分页边界防呆
  - **Property 1: Bug Condition** - 分页边界防呆
  - **关键**: 此测试在未修复代码上必须失败
  - **目标**: 发现反例证明缺陷存在
  - 在未修复代码上运行测试
  - **预期结果**: 测试失败（按钮未禁用）
  - _Requirements: 5.1, 5.2_
  - **测试用例**:
    - 上一页边界测试: CurrentPage = 1，检查"上一页"按钮 IsEnabled 属性
    - 预期反例: 按钮 IsEnabled = true，点击后 CurrentPage 可能变为 0
    - 下一页边界测试: CurrentPage = TotalPages = 5，检查"下一页"按钮 IsEnabled
    - 预期反例: 按钮 IsEnabled = true，点击后 CurrentPage 变为 6

- [ ] 18. 编写 Preservation 属性测试 - 分页核心功能（修复前）
  - **Property 2: Preservation** - 现有绑定和其他功能（分页）
  - **重要**: 遵循观察优先方法论
  - 在未修复代码上观察有效范围内翻页行为
  - 编写属性测试捕获观察到的行为模式
  - 在未修复代码上运行测试
  - **预期结果**: 测试通过
  - _Requirements: 3.9, 3.10_
  - **测试用例**:
    - 有效范围翻页: 验证 PreviousPage 和 NextPage 正确加载数据
    - PageSize 变化: 验证正确重新计算 TotalPages，CurrentPage 保持在有效范围

- [ ] 19. 修复 Bug 5: HistoryViewModel 分页按钮添加 CanExecute

  - [ ] 19.1 修改 PreviousPage 命令添加 CanExecute
    - 修改 `decompiled_src/MotorTestSystem.ViewModels/HistoryViewModel.cs`
    - 修改 `[RelayCommand]` 为 `[RelayCommand(CanExecute = nameof(CanPreviousPage))]`
    - 简化 PreviousPage 方法为 `CurrentPage--`（CanExecute 已确保边界）
    - _Bug_Condition: isBugCondition_InvalidPagination(input) where currentPage <= 1 but CanExecute == true_
    - _Expected_Behavior: CanExecute = false when currentPage <= 1_
    - _Preservation: 分页核心功能（Requirements 3.9, 3.10）_
    - _Requirements: 5.1, 2.13_

  - [ ] 19.2 添加 CanPreviousPage 方法
    - 添加 `private bool CanPreviousPage()` 方法
    - 返回 `CurrentPage > 1`
    - _Requirements: 2.13_

  - [ ] 19.3 修改 NextPage 命令添加 CanExecute
    - 修改 `[RelayCommand]` 为 `[RelayCommand(CanExecute = nameof(CanNextPage))]`
    - 简化 NextPage 方法为 `CurrentPage++`（CanExecute 已确保边界）
    - _Requirements: 5.2, 2.14_

  - [ ] 19.4 添加 CanNextPage 方法
    - 添加 `private bool CanNextPage()` 方法
    - 返回 `CurrentPage < TotalPages`
    - _Requirements: 2.14_

  - [ ] 19.5 添加属性变化时的命令状态更新
    - 添加 `partial void OnCurrentPageChanged(int value)` 方法
    - 调用 `PreviousPageCommand.NotifyCanExecuteChanged()` 和 `NextPageCommand.NotifyCanExecuteChanged()`
    - 添加 `partial void OnTotalPagesChanged(int value)` 方法
    - 调用 `PreviousPageCommand.NotifyCanExecuteChanged()` 和 `NextPageCommand.NotifyCanExecuteChanged()`
    - _Requirements: 2.13, 2.14, 2.15_

  - [ ] 19.6 验证 Bug Condition 探索测试现在通过
    - **Property 1: Expected Behavior** - 分页边界防呆
    - 重新运行步骤 17 的相同测试
    - **预期结果**: 测试通过（确认缺陷已修复）

  - [ ] 19.7 验证 Preservation 测试仍然通过
    - **Property 2: Preservation** - 现有绑定和其他功能（分页）
    - 重新运行步骤 18 的相同测试
    - **预期结果**: 测试通过（确认无回归）

- [ ] 20. Checkpoint - Bug 5 所有测试通过

## 全局验证

- [ ] 21. 编写 Preservation 属性测试 - 其他命令功能
  - **Property 2: Preservation** - 现有绑定和其他功能（其他命令）
  - 验证非修复范围的功能不受影响
  - _Requirements: 3.11, 3.12_
  - **测试用例**:
    - 验证其他 ICommand（Search、Export、ViewReport、AddUser、ResetPassword）正常工作
    - 验证 UserViewModel 命令的权限检查逻辑（基于 AppPermission.UserManagement）

- [ ] 22. 运行所有单元测试
  - 运行所有 DashboardViewModel 单元测试
  - 运行所有 ConfigViewModel 单元测试
  - 运行所有 HistoryViewModel 单元测试
  - 运行所有 MonitorViewModel 单元测试
  - 确保所有测试通过

- [ ] 23. 运行所有属性测试
  - 运行图表数据准确性属性测试
  - 运行命令功能完整性属性测试
  - 运行现有绑定保持属性测试
  - 确保所有属性测试通过

- [ ] 24. 端到端集成测试
  - 运行端到端图表刷新测试（完整应用，Dashboard 视图，3 个刷新周期）
  - 运行端到端并发保护测试（完整应用，Config 视图，UI Automation）
  - 运行端到端数据绑定测试（完整应用，Monitor 视图，Mock PLC）
  - 运行端到端分页测试（完整应用，History 视图，UI Automation）

- [ ] 25. 最终 Checkpoint - 确保所有测试通过
  - 确保所有测试通过，如有疑问询问用户


---

## 任务依赖关系图

```mermaid
graph TD
    %% Bug 1: DashboardView 图表重绘
    T1[1. Bug Condition 探索测试 - 图表重绘]
    T2[2. Preservation 测试 - 图表数据正确性]
    T3[3. 修复 Bug 1: 图表增量更新]
    T3_1[3.1 ISeries[] → ObservableCollection]
    T3_2[3.2 构造函数初始化]
    T3_3[3.3 RefreshHourlyChartsAsync]
    T3_4[3.4 修改定时器回调]
    T3_5[3.5 验证 Bug Condition 测试通过]
    T3_6[3.6 验证 Preservation 测试通过]
    T4[4. Checkpoint - Bug 1 完成]

    %% Bug 2: ConfigViewModel 并发保护
    T5[5. Bug Condition 探索测试 - 并发测试]
    T6[6. Preservation 测试 - 连接测试功能]
    T7[7. 修复 Bug 2: 并发保护]
    T7_1[7.1 添加 IsTestingConnection]
    T7_2[7.2 修改 RelayCommand 特性]
    T7_3[7.3 添加 CanTestConnection]
    T7_4[7.4 验证 Bug Condition 测试通过]
    T7_5[7.5 验证 Preservation 测试通过]
    T8[8. Checkpoint - Bug 2 完成]

    %% Bug 3: HistoryViewModel 打印并发
    T9[9. Bug Condition 探索测试 - 并发打印]
    T10[10. Preservation 测试 - 打印功能]
    T11[11. 修复 Bug 3: 打印并发保护]
    T11_1[11.1 添加 IsPrinting]
    T11_2[11.2 修改 PrintTraceAsync]
    T11_3[11.3 添加 CanPrintTrace]
    T11_4[11.4 验证 Bug Condition 测试通过]
    T11_5[11.5 验证 Preservation 测试通过]
    T12[12. Checkpoint - Bug 3 完成]

    %% Bug 4: MonitorView 硬编码
    T13[13. Bug Condition 探索测试 - 硬编码]
    T14[14. Preservation 测试 - 现有绑定]
    T15[15. 修复 Bug 4: 数据绑定化]
    T15_1[15.1 添加属性到 StationState]
    T15_2[15.2 添加 RecentBarcodes/Alerts]
    T15_3[15.3 更新 ApplySnapshot]
    T15_4[15.4 添加警报管理]
    T15_5[15.5 绑定 ProgressBar]
    T15_6[15.6 绑定条码显示]
    T15_7[15.7 绑定工位参数]
    T15_8[15.8 绑定系统警报]
    T15_9[15.9 验证 Bug Condition 测试通过]
    T15_10[15.10 验证 Preservation 测试通过]
    T16[16. Checkpoint - Bug 4 完成]

    %% Bug 5: HistoryView 分页边界
    T17[17. Bug Condition 探索测试 - 分页边界]
    T18[18. Preservation 测试 - 分页核心]
    T19[19. 修复 Bug 5: 分页边界防呆]
    T19_1[19.1 修改 PreviousPage CanExecute]
    T19_2[19.2 添加 CanPreviousPage]
    T19_3[19.3 修改 NextPage CanExecute]
    T19_4[19.4 添加 CanNextPage]
    T19_5[19.5 属性变化更新命令状态]
    T19_6[19.6 验证 Bug Condition 测试通过]
    T19_7[19.7 验证 Preservation 测试通过]
    T20[20. Checkpoint - Bug 5 完成]

    %% 全局验证
    T21[21. Preservation 测试 - 其他命令]
    T22[22. 运行所有单元测试]
    T23[23. 运行所有属性测试]
    T24[24. 端到端集成测试]
    T25[25. 最终 Checkpoint]

    %% Bug 1 依赖关系
    T1 --> T2
    T2 --> T3
    T3 --> T3_1
    T3_1 --> T3_2
    T3_2 --> T3_3
    T3_3 --> T3_4
    T3_4 --> T3_5
    T3_5 --> T3_6
    T3_6 --> T4

    %% Bug 2 依赖关系
    T5 --> T6
    T6 --> T7
    T7 --> T7_1
    T7_1 --> T7_2
    T7_2 --> T7_3
    T7_3 --> T7_4
    T7_4 --> T7_5
    T7_5 --> T8

    %% Bug 3 依赖关系
    T9 --> T10
    T10 --> T11
    T11 --> T11_1
    T11_1 --> T11_2
    T11_2 --> T11_3
    T11_3 --> T11_4
    T11_4 --> T11_5
    T11_5 --> T12

    %% Bug 4 依赖关系
    T13 --> T14
    T14 --> T15
    T15 --> T15_1
    T15_1 --> T15_2
    T15_2 --> T15_3
    T15_3 --> T15_4
    T15_1 --> T15_5
    T15_2 --> T15_6
    T15_1 --> T15_7
    T15_2 --> T15_8
    T15_4 --> T15_9
    T15_5 --> T15_9
    T15_6 --> T15_9
    T15_7 --> T15_9
    T15_8 --> T15_9
    T15_9 --> T15_10
    T15_10 --> T16

    %% Bug 5 依赖关系
    T17 --> T18
    T18 --> T19
    T19 --> T19_1
    T19_1 --> T19_2
    T19_2 --> T19_3
    T19_3 --> T19_4
    T19_4 --> T19_5
    T19_5 --> T19_6
    T19_6 --> T19_7
    T19_7 --> T20

    %% 全局验证依赖（所有 Bug 修复完成后）
    T4 --> T21
    T8 --> T21
    T12 --> T21
    T16 --> T21
    T20 --> T21
    T21 --> T22
    T22 --> T23
    T23 --> T24
    T24 --> T25

    %% Bug 可并行处理（无相互依赖）
    T1 -.并行.- T5
    T1 -.并行.- T9
    T1 -.并行.- T13
    T1 -.并行.- T17
```

### 依赖关系说明

**并行执行机会**:
- Bug 1-5 的探索测试和 Preservation 测试可以并行编写和运行
- 各个 Bug 的修复可以独立进行，互不依赖
- 建议顺序: Bug 1 → Bug 2/3 (并行) → Bug 4 → Bug 5

**关键路径**:
1. 每个 Bug 必须先编写探索测试 → 再编写 Preservation 测试 → 然后才能开始修复
2. 修复实现完成后必须验证两类测试都通过
3. 所有 5 个 Bug 修复完成后才能进行全局验证

**测试依赖**:
- 探索测试必须在未修复代码上运行并失败
- Preservation 测试必须在未修复代码上运行并通过
- 修复后必须重新运行相同的测试验证结果

**验证门控**:
- 每个 Bug 有独立的 Checkpoint（任务 4, 8, 12, 16, 20）
- 最终 Checkpoint（任务 25）确保所有测试通过
