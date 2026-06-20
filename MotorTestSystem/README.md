# 🔌 电机电性能测试系统上位机

> 基于 **WPF (.NET 8) + MVVM** 架构的工业级电机电性能测试上位机软件，支持 6 台 PLC 多协议并发通信、实时数据看板、历史数据追溯与导出、RBAC 权限控制。

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)
![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11%20x64-0078D6?logo=windows)
![License](https://img.shields.io/badge/license-MIT-blue)
![Branch](https://img.shields.io/badge/branch-v1.0--stable-success)

---

## 📋 项目概述

本项目为某电机制造企业定制开发的**产线上位机监控软件**，覆盖电机三阶段电性能测试全流程：

| 阶段 | 测试类型 | PLC 品牌 | 关键参数 |
|------|----------|----------|----------|
| 阶段 1 | 空载测试 | 三菱 FX5U / Q系列 | 空载电流、空载转速、轴伸长度、滚花直径 |
| 阶段 2 | 噪音测试 | 西门子 S7-1200/1500 | 正转噪音、反转噪音、噪音差值 |
| 阶段 3 | 负载测试 | 汇川 H5U / Easy521 | 负载电流、负载转速 |

---

## 🚀 技术栈

| 层次 | 技术选型 | 版本 |
|------|----------|------|
| 运行时 | .NET | 8.0 |
| UI 框架 | WPF | 内置 |
| MVVM | CommunityToolkit.Mvvm | 8.4.2 |
| 图表 | LiveChartsCore.SkiaSharpView.WPF | 2.0.4 |
| UI 主题 | MaterialDesignThemes | 5.3.2 |
| PLC 通信 | S7NetPlus | 0.20.0 |
| ORM / 数据库 | SqlSugarCore + SQLite | 5.1.4.214 |
| 视频集成 | 海康威视 HCNetSDK（本地 DLL） | — |

---

## 🏗️ 项目结构

```
MotorTestSystem/
├── Business/
│   ├── Interfaces/          # 业务接口定义
│   ├── PLC/                 # PLC 驱动适配（Factory/Strategy 模式）
│   └── Services/
│       ├── BackendRuntime.cs          # 异步懒初始化运行时单例
│       ├── BatchWriteService.cs       # 通道式异步批量写入
│       ├── DeadLetterQueue.cs         # 写入失败持久化补偿队列
│       ├── EventChannelService.cs     # 解耦事件总线（Channel<T>）
│       ├── PlcPollingService.cs       # 6 台 PLC 并发轮询调度
│       ├── HikvisionSdkService.cs     # 海康威视 SDK 封装
│       ├── AuthService.cs             # RBAC 权限控制
│       ├── LanguageManager.cs         # 多语言资源管理
│       └── CloudSyncService.cs        # 云同步（可选）
│
├── Domain/
│   └── Models/              # 领域模型（StationState / MotorTestResult 等）
│
├── Infrastructure/          # 基础设施（数据库迁移、序列化等）
│
├── Presentation/
│   ├── Converters/          # IValueConverter 集合（状态/颜色/可见性转换）
│   ├── Services/            # IDialogService / IDispatcherService（UI 服务接口）
│   ├── ViewModels/
│   │   ├── MainViewModel.cs           # 导航控制器、全局状态
│   │   ├── DashboardViewModel.cs      # 生产看板（节流 Channel 驱动，无 UI 阻塞）
│   │   ├── MonitorViewModel.cs        # 6 工位实时遥测
│   │   ├── HistoryViewModel.cs        # 历史查询 / 分页 / CSV 导出
│   │   ├── ConfigViewModel.cs         # PLC 通信配置与测试
│   │   ├── UserViewModel.cs           # 用户管理
│   │   └── NotificationCenterViewModel.cs
│   ├── Views/               # XAML 视图（对应 ViewModel 自动路由）
│   └── Windows/             # App.xaml / MainWindow
│
├── Resources/
│   ├── Images/
│   └── Styles/              # 全局样式、MaterialDesign 主题覆盖
│
├── docs/                    # 补充文档（海康SDK集成说明、贡献指南等）
├── appsettings.json         # 运行时配置
└── global.json              # SDK 版本锁定
```

---

## ⚙️ appsettings.json 说明

```json
{
  "DataPersistence": {
    "WriteChannelCapacity": 500,      // 写入 Channel 容量
    "MaxWriteRetryCount": 3,          // 写入失败最大重试次数
    "FlushTimeoutSeconds": 10,        // 优雅关闭等待上限
    "SQLiteSyncMode": "NORMAL",       // WAL 日志模式（NORMAL / FULL）
    "SQLiteLongConnection": false,    // 长连接模式（高频写场景开启）
    "CloudSyncEnabled": false,        // 云同步开关
    "DeadLetterQueuePath": ""         // 补偿队列落盘路径（空=内存模式）
  }
}
```

---

## 📦 快速启动

### 前置要求

- **Windows 10/11 x64**
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Visual Studio 2022（推荐）或 VS Code + C# Dev Kit
- 海康威视 SDK 库文件（`../海康库文件/`，不含于仓库，需单独获取）

### 克隆 & 运行

```bash
git clone https://github.com/yunxi31/Econ.git
cd Econ/MotorTestSystem
dotnet run
```

或在 Visual Studio 中打开 `MotorTestSystem.csproj` 按 `F5`。

> **注意**：海康 SDK DLL 路径在 csproj 中引用为 `../海康库文件/`，请在父目录放置对应文件，或修改 csproj 中的 `ContentWithTargetPath` 路径。详见 [docs/海康SDK集成说明.md](docs/海康SDK集成说明.md)。

---

## 🧩 核心设计决策

### 异步懒初始化运行时
`BackendRuntime` 通过 `Lazy<Task<BackendRuntime>>` 实现异步单例，消除启动阻塞：

```csharp
// 不阻塞 UI 线程
var runtime = await BackendRuntime.SharedAsync;
```

### 节流 Channel 驱动 Dashboard
`DashboardViewModel` 通过有界 `Channel<T>` 接收 PLC 遥测事件，消费侧在后台线程聚合后批量推送 UI，彻底解耦轮询频率与渲染频率。

### Dead Letter Queue 补偿
SQLite 写入失败时序列化到本地 DLQ 文件，重启后自动回放，确保数据零丢失。

### RBAC 权限控制
`AuthService` 提供操作员 / 管理员两级权限，`CanExecute` 绑定命令可用性，配置修改和用户管理功能对操作员不可见。

### MVVM 自动路由
```xml
<!-- MainWindow.xaml：ViewModel 类型 → View 自动解析，零手动实例化 -->
<DataTemplate DataType="{x:Type vm:DashboardViewModel}">
    <views:DashboardView/>
</DataTemplate>
```

---

## 🗄️ 数据库设计（SQLite）

```sql
CREATE TABLE MotorTestResult (
    Id            INTEGER PRIMARY KEY AUTOINCREMENT,
    Barcode       TEXT    NOT NULL UNIQUE,  -- 唯一键，支持 UPSERT
    TestTime      TEXT    DEFAULT (datetime('now')),

    -- 阶段1：空载
    NoLoadCurrent REAL,  NoLoadSpeed INTEGER,
    ShaftLength   REAL,  KnurlDiameter REAL,
    Stage1Result  TEXT,  -- OK / NG / NULL

    -- 阶段2：噪音
    FwdNoise REAL, RevNoise REAL, NoiseDiff REAL,
    Stage2Result TEXT,

    -- 阶段3：负载
    LoadCurrent REAL, LoadSpeed INTEGER,
    Stage3Result TEXT,

    FinalResult TEXT     -- 三阶段综合判定
);
```

---

## 📝 Roadmap

- [x] MVVM 分层架构（Domain / Business / Infrastructure / Presentation）
- [x] 6 台 PLC 并发轮询（S7NetPlus + Strategy 模式）
- [x] SQLite 异步批量写入 + DLQ 补偿
- [x] 节流 Channel 驱动 DashboardViewModel（无 UI 线程阻塞）
- [x] RBAC 权限控制（操作员 / 管理员）
- [x] 海康威视 SDK 集成预留
- [ ] ModbusTCP 汇川驱动接入
- [ ] 云同步端点实现
- [x] Excel 报表导出（MiniExcel）
- [ ] 全局未处理异常捕获与结构化日志

---

## 🤝 贡献

详见 [docs/CONTRIBUTING.md](docs/CONTRIBUTING.md)。

---

## 📄 License

[MIT](LICENSE) © 2026
