# 🔌 电机电性能测试系统上位机

> 基于 WPF (.NET 8) + MVVM 架构的工业级电机电性能测试上位机软件，支持 6 台 PLC 多协议并发通信、实时数据看板、历史数据追溯与导出。

![Build Status](https://img.shields.io/badge/build-passing-brightgreen)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)
![Platform](https://img.shields.io/badge/platform-Windows-0078D6?logo=windows)
![License](https://img.shields.io/badge/license-MIT-blue)

---

## 📋 项目概述

本项目为某电机制造企业定制开发的**产线上位机监控软件**，覆盖电机三阶段电性能测试全流程（空载测试 → 噪音测试 → 负载测试），具备：

- **实时监控**：2 秒轮询 6 台 PLC，刷新转速、电流、位移等关键参数
- **生产看板**：日产量趋势图、综合良率实时计算
- **视频集成**：预留海康威视 SDK 接入接口（`WindowsFormsHost` 承载）
- **历史追溯**：按条码/日期/结果多条件筛选，支持 CSV 导出
- **通信配置**：图形化界面管理 6 台 PLC 的 IP/端口/协议/站号参数

---

## 🏗️ 系统架构

```
MotorTestSystem/
├── Models/
│   ├── StationState.cs       # PLC 实时遥测数据模型（ObservableObject）
│   ├── StationConfig.cs      # PLC 通信配置（IP/端口/协议）
│   └── MotorTestResult.cs    # 数据库记录模型（对应 SQL 表结构）
│
├── ViewModels/
│   ├── MainViewModel.cs      # 导航控制器、系统时钟、全局状态
│   ├── DashboardViewModel.cs # 看板数据：产量统计、良率图表（LiveCharts2）
│   ├── MonitorViewModel.cs   # 6 机台实时遥测轮询与模拟数据流
│   ├── HistoryViewModel.cs   # 历史查询、筛选、CSV 导出
│   ├── ConfigViewModel.cs    # PLC 配置管理与连接测试
│   └── ViewModelBase.cs      # ObservableObject 基类
│
├── Views/
│   ├── MainWindow.xaml       # Shell 窗口：左侧导航栏 + 动态路由
│   ├── DashboardView.xaml    # 生产看板 UI（Charts + 摄像头区域）
│   ├── MonitorView.xaml      # 3 列 Bento 卡片布局（6 机台实时数据）
│   ├── HistoryView.xaml      # 深色主题 DataGrid + 筛选栏
│   └── ConfigView.xaml       # 2×3 配置卡片网格
│
└── Converters/
    ├── StatusToColorConverter.cs              # 状态码 → 颜色（待机/运行/故障）
    ├── StatusToTextConverter.cs               # 状态码 → 中文文字
    ├── ResultToBrushConverter.cs              # OK/NG/WAIT → 颜色徽章
    ├── OnlineToBrushConverter.cs             # 在线/离线 → 颜色
    └── InverseBooleanToVisibilityConverter.cs # 取反可见性
```

---

## 🖥️ 功能截图预览

| 模块 | 说明 |
|------|------|
| **生产看板** | 日产量柱状图 + 良率折线图 + 海康 SDK 视频区 |
| **工位监视** | 3 阶段 × 2 机台 = 6 卡片实时参数展示，2 秒刷新 |
| **历史追溯** | 条码/时间/结果多维筛选，DataGrid 显示，一键导出 CSV |
| **通信配置** | 卡片式 PLC 配置，支持异步连通性测试 |

---

## 🔧 PLC 接入规格

| 机台 | PLC 型号 | 通信协议 | 测试阶段 |
|------|----------|----------|----------|
| A1 | 三菱 FX5U | MC Protocol / TCP | 空载测试 |
| A2 | 三菱 Q系列 | MC Protocol / TCP | 空载测试 |
| A3 | 西门子 S7-1200 | S7 Protocol / TCP | 噪音测试 |
| A4 | 西门子 S7-1500 | S7 Protocol / TCP | 噪音测试 |
| A5 | 汇川 H5U | ModbusTCP | 负载测试 |
| A6 | 汇川 Easy521 | ModbusTCP | 负载测试 |

---

## 📡 采集参数清单

### 阶段 1：空载测试（三菱）
| 参数 | 单位 | 换算系数 |
|------|------|----------|
| 空载电流 | A | ÷1000（PLC 整数 → 浮点） |
| 空载转速 | r/min | 直接读取 |
| 轴伸长度 | mm | ÷1000 |
| 滚花直径 | mm | ÷1000 |

### 阶段 2：噪音测试（西门子）
| 参数 | 单位 |
|------|------|
| 正转噪音 | dB |
| 反转噪音 | dB |
| 噪音差值 | dB |

### 阶段 3：负载测试（汇川）
| 参数 | 单位 |
|------|------|
| 负载电流 | A |
| 负载转速 | r/min |

---

## 🚀 技术栈

| 层次 | 技术选型 | 版本 |
|------|----------|------|
| 运行时 | .NET | 8.0 |
| UI 框架 | WPF | .NET 8 内置 |
| MVVM | CommunityToolkit.Mvvm | 8.4.2 |
| 图表 | LiveChartsCore.SkiaSharpView.WPF | 2.0.4 |
| PLC 通信（待接入） | HslCommunication / S7NetPlus | — |
| 数据库（待接入） | Microsoft.Data.SqlClient | — |
| 视频（待接入） | 海康威视 SDK（HCNetSDK.dll） | — |

---

## 📦 快速启动

### 前置要求

- Windows 10/11 x64
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Visual Studio 2022（推荐）或 VS Code + C# Dev Kit

### 克隆 & 运行

```bash
git clone https://github.com/your-username/MotorTestSystem.git
cd MotorTestSystem
dotnet run
```

或在 Visual Studio 中直接打开 `MotorTestSystem.csproj` 并按 `F5`。

---

## 🗄️ 数据库设计（SQL Server）

```sql
CREATE TABLE MotorTestResult (
    Id            INT IDENTITY PRIMARY KEY,
    Barcode       NVARCHAR(50)  NOT NULL UNIQUE,  -- 电机条码（唯一键，支持 UPSERT）
    TestTime      DATETIME      DEFAULT GETDATE(),

    -- 阶段1：空载
    NoLoadCurrent FLOAT         NULL,
    NoLoadSpeed   INT           NULL,
    ShaftLength   FLOAT         NULL,
    KnurlDiameter FLOAT         NULL,
    Stage1Result  NVARCHAR(5)   NULL,   -- OK / NG / NULL（未测）

    -- 阶段2：噪音
    FwdNoise      FLOAT         NULL,
    RevNoise      FLOAT         NULL,
    NoiseDiff     FLOAT         NULL,
    Stage2Result  NVARCHAR(5)   NULL,

    -- 阶段3：负载
    LoadCurrent   FLOAT         NULL,
    LoadSpeed     INT           NULL,
    Stage3Result  NVARCHAR(5)   NULL,

    FinalResult   NVARCHAR(5)   NULL    -- 三阶段综合判定
);
```

> **UPSERT 策略**：同一条码重测时通过 `MERGE` 语句覆盖旧数据，`NULL` 字段保留未测状态以支持跨机台分阶段采集。

---

## 🧩 核心设计决策

### 1. MVVM 导航模式
`MainWindow` 中通过 `DataTemplate` 将 ViewModel 类型自动路由到对应 View，无需手动实例化，完全解耦：
```xml
<DataTemplate DataType="{x:Type vm:DashboardViewModel}">
    <views:DashboardView/>
</DataTemplate>
```

### 2. 实时数据线程安全
`DispatcherTimer` 驱动模拟轮询，所有 UI 属性更新在 WPF 主线程执行，避免跨线程异常；接入真实 PLC 驱动后需将阻塞 I/O 移至 `Task.Run`，结果通过 `Application.Current.Dispatcher.InvokeAsync` 回到 UI 线程。

### 3. 海康 SDK 集成策略
采用 `WindowsFormsHost` 承载 Win32 窗口句柄，避免 WPF `HwndSource` 的 airspace 问题。实际接入时调用 `NET_DVR_RealPlay_V40` 并传入 `Handle`。

### 4. 数据 NULL 安全
`MotorTestResult` 所有测试参数字段类型均为 `double?` / `int?`，DataGrid 绑定使用 `TargetNullValue='未测'` 展示友好提示。

---

## 📝 待办 / Roadmap

- [ ] 接入真实 PLC 驱动（S7NetPlus / HslCommunication）
- [ ] 接入 SQL Server，实现异步 UPSERT 入库
- [ ] 接入海康威视 SDK，实现真实视频预览
- [ ] 增加全局 `UnhandledException` 捕获与日志记录
- [ ] 实现用户登录与权限管理（操作员 / 管理员）
- [ ] 报表导出为 Excel（使用 ClosedXML）

---

## 🤝 贡献

欢迎提交 Issue 和 PR！请遵循以下规范：

1. Fork 本仓库
2. 创建 feature 分支：`git checkout -b feature/your-feature`
3. 提交变更：`git commit -m 'feat: add your feature'`
4. Push：`git push origin feature/your-feature`
5. 创建 Pull Request

---

## 📄 License

[MIT](LICENSE) © 2026
