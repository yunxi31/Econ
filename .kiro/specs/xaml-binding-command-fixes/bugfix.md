# Bugfix Requirements Document

## Introduction

本文档针对 MotorTestSystem 项目中 XAML 绑定与 ICommand 的 5 个缺陷进行修复需求定义。这些缺陷包括：高频 UI 刷新导致的性能问题、命令缺少并发保护导致的资源阻塞风险、以及 UI 控件缺少状态防呆逻辑。修复这些问题将显著提升生产看板流畅度、防止设备端口阻塞、改善用户体验。

## Bug Analysis

### Current Behavior (Defect)

#### 1. DashboardView 高频刷新导致 LiveCharts 重绘风暴

1.1 WHEN `DashboardViewModel.RefreshHourlyChartsAsync()` 每 5 秒执行时 THEN 系统创建全新的 `ISeries[]` 数组对象并赋值给 `OutputSeries`、`PassRateSeries`、`DefectDistributionSeries` 等属性

1.2 WHEN LiveCharts 检测到 `ISeries[]` 引用变化时 THEN 系统触发完整图表重绘（包括布局计算、渲染管线、动画），导致 UI 线程阻塞 200ms-1s

1.3 WHEN 5 个 LiveCharts 图表同时重建时 THEN 系统产生累积卡顿，影响生产看板的实时性

#### 2. ConfigViewModel.TestConnectionAsync 无并发保护

2.1 WHEN 用户在 PLC 连接测试进行中再次点击"测试连接"按钮时 THEN 系统同时发起多个并发 `TestConnectionAsync` 请求

2.2 WHEN 多个并发 TCP 连接请求发送到同一 PLC 端口时 THEN 系统可能造成 PLC 端口阻塞、产生大量诊断日志、界面响应混乱（多次 ShowMessage 弹窗）

#### 3. HistoryViewModel.PrintTraceAsync 缺少 CanExecute

3.1 WHEN 用户在打印追溯单进行中再次点击"打印"按钮时 THEN 系统再次调用 `PrintTraceAsync` 方法

3.2 WHEN 多个打印任务尝试同时写入 XPS 文档时 THEN 系统可能抛出 `InvalidOperationException`（XPS 写入器正在使用中）或打印日志混乱

#### 4. MonitorView 大量硬编码导致数据不更新

4.1 WHEN PLC 轮询服务更新工位状态数据时 THEN MonitorView 中的 ProgressBar（`Value="60"`, `Value="85"` 等）保持硬编码值不变，无法反映真实进度

4.2 WHEN 系统产生新的检测条码或系统警报时 THEN MonitorView 中的"最近检测条码"面板（硬编码 `SN-9948x-XA1` 等）和"系统警报"面板（硬编码"A4: 噪音超标"等）不会更新

4.3 WHEN 工位参数变化时 THEN A2 工位参数区域显示的 `"-- V"`, `"-- A"`, `"-- RPM"` 等硬编码占位符不会更新为实际数值

#### 5. HistoryView 分页按钮缺少 CanExecute

5.1 WHEN 用户在第 1 页时点击"上一页"按钮 THEN 系统允许触发 `PreviousPage` 命令，可能导致 `CurrentPage` 变为 0 或负数

5.2 WHEN 用户在最后一页时点击"下一页"按钮 THEN 系统允许触发 `NextPage` 命令，可能导致 `CurrentPage` 超出 `TotalPages` 范围

### Expected Behavior (Correct)

#### 1. DashboardView 高频刷新 → LiveCharts 增量更新

2.1 WHEN `DashboardViewModel.RefreshHourlyChartsAsync()` 每 5 秒执行时 THEN 系统应使用 `ObservableCollection<ISeries>` 保持 Series 对象不变，仅更新 `Values` 属性

2.2 WHEN LiveCharts 检测到 `Values` 数据变化时 THEN 系统应仅触发数据层更新和局部重绘，而不重建整个图表对象

2.3 WHEN 数据值与上次刷新相同时 THEN 系统应跳过属性赋值，避免触发无效的 PropertyChanged 通知

#### 2. ConfigViewModel.TestConnectionAsync 添加并发保护

2.4 WHEN 用户点击"测试连接"按钮时 THEN 系统应设置 `IsTestingConnection = true` 并禁用按钮（通过 CanExecute）

2.5 WHEN PLC 连接测试进行中时 THEN 系统应通过 `CanExecute = false` 阻止用户再次点击按钮

2.6 WHEN PLC 连接测试完成（成功或失败）时 THEN 系统应设置 `IsTestingConnection = false` 并重新启用按钮

#### 3. HistoryViewModel.PrintTraceAsync 添加 CanExecute

2.7 WHEN 用户点击"打印追溯单"按钮时 THEN 系统应设置 `IsPrinting = true` 并禁用按钮（通过 CanExecute）

2.8 WHEN 打印任务进行中时 THEN 系统应通过 `CanExecute = false` 阻止用户再次点击按钮

2.9 WHEN 打印任务完成时 THEN 系统应设置 `IsPrinting = false` 并重新启用按钮

#### 4. MonitorView 硬编码数据绑定化

2.10 WHEN PLC 轮询服务更新工位状态数据时 THEN MonitorView 中的 ProgressBar 应绑定到 `StationState.Progress` 属性并实时更新

2.11 WHEN 系统产生新的检测条码或系统警报时 THEN MonitorView 应通过数据绑定到 `MonitorViewModel.RecentBarcodes` 和 `MonitorViewModel.Alerts` 集合来动态显示

2.12 WHEN 工位参数变化时 THEN A2 工位参数区域应绑定到 `StationState.Voltage`、`StationState.Current` 等属性并显示实际数值

#### 5. HistoryView 分页按钮添加 CanExecute

2.13 WHEN 用户在第 1 页时 THEN "上一页"按钮应通过 `CanExecute = false` 禁用，阻止 `PreviousPage` 命令触发

2.14 WHEN 用户在最后一页时 THEN "下一页"按钮应通过 `CanExecute = false` 禁用，阻止 `NextPage` 命令触发

2.15 WHEN 用户在中间页时 THEN "上一页"和"下一页"按钮应通过 `CanExecute = true` 启用

### Unchanged Behavior (Regression Prevention)

#### 1. DashboardView 图表数据正确性

3.1 WHEN 修改为增量更新后 THEN 系统应继续正确显示产量统计、合格率趋势、缺陷分布等图表数据，数值与修改前一致

3.2 WHEN 图表数据更新时 THEN 系统应继续触发 LiveCharts 的动画效果（如柱状图增长动画）

#### 2. ConfigViewModel 测试连接功能

3.3 WHEN 单次点击"测试连接"按钮时 THEN 系统应继续正确执行 PLC 连接测试并显示成功/失败结果

3.4 WHEN 连接测试失败时 THEN 系统应继续显示详细的错误信息（如"连接超时"、"端口占用"）

#### 3. HistoryViewModel 打印功能

3.5 WHEN 单次点击"打印追溯单"按钮时 THEN 系统应继续正确生成 XPS 文档并调用打印对话框

3.6 WHEN 打印任务取消时 THEN 系统应继续正确处理 `CancellationToken` 并恢复 UI 状态

#### 4. MonitorView 现有绑定功能

3.7 WHEN 添加新的数据绑定后 THEN 系统应继续保持现有的条码绑定（`NoLoadStations[0].Barcode`）、数值绑定（`NoiseStations[0].FwdNoise`）等正常工作

3.8 WHEN PLC 轮询频率为 1 秒/工位时 THEN 系统应继续保持低延迟更新，不产生额外性能开销

#### 5. HistoryView 分页功能

3.9 WHEN 用户在有效范围内翻页时 THEN 系统应继续正确执行 `PreviousPage` 和 `NextPage` 命令，加载对应页的数据

3.10 WHEN 用户修改 `PageSize` 时 THEN 系统应继续正确重新计算 `TotalPages` 并保持 `CurrentPage` 在有效范围内

#### 6. 其他 ICommand 功能

3.11 WHEN 用户执行其他命令（如 `Search`、`Export`、`ViewReport` 等）时 THEN 系统应继续正常工作，不受本次修复影响

3.12 WHEN UserViewModel 中的 `AddUser`、`EditUser`、`ResetPassword` 命令执行时 THEN 系统应继续保持现有的权限检查逻辑（`CanExecute` 基于 `AppPermission.UserManagement`）
