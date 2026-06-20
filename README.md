# 电机电性能测试系统上位机

> 基于 **WPF (.NET 8) + MVVM** 架构的工业级产线监控软件，支持三菱 / 西门子 / 汇川三品牌 PLC 并发通信、实时数据看板、历史追溯与 RBAC 权限控制。

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)
![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11%20x64-0078D6?logo=windows)
![License](https://img.shields.io/badge/license-MIT-blue)

---

## 项目概述

本系统为某电机制造企业定制开发的**产线上位机监控软件**，覆盖电机三阶段电性能测试全流程：

| 阶段 | 测试类型 | 协议 / PLC | 关键参数 |
|------|----------|------------|----------|
| 阶段 1 | 空载测试 | MC 协议 / 三菱 FX5U · Q 系列 | 空载电流、空载转速、轴伸长度、滚花直径 |
| 阶段 2 | 噪音测试 | S7Comm / 西门子 S7-1200 · 1500 | 正转噪音、反转噪音、噪音差值 |
| 阶段 3 | 负载测试 | ModbusTCP / 汇川 H5U · Easy521 | 负载电流、负载转速 |

---

## 仓库结构

```
Econ/
├── MotorTestSystem/          # 主程序（WPF 应用）
│   ├── Business/
│   │   ├── Interfaces/       # 业务接口定义
│   │   ├── PLC/              # PLC 驱动（Factory / Strategy 模式）
│   │   │   ├── MelsecMcClient.cs     # 三菱 MC 协议客户端
│   │   │   ├── S7PlcClient.cs        # 西门子 S7Comm 客户端
│   │   │   ├── ModbusTcpClient.cs    # ModbusTCP 客户端
│   │   │   └── PlcClientFactory.cs   # 驱动工厂
│   │   └── Services/
│   │       ├── BackendRuntime.cs         # 异步懒初始化运行时单例
│   │       ├── BatchWriteService.cs      # Channel 式异步批量写入
│   │       ├── DeadLetterQueue.cs        # 写入失败持久化补偿队列
│   │       ├── EventChannelService.cs    # 解耦事件总线
│   │       ├── PlcPollingService.cs      # 6 台 PLC 并发轮询调度
│   │       ├── HikvisionSdkService.cs    # 海康威视 SDK 封装
│   │       ├── AuthService.cs            # RBAC 权限控制
│   │       ├── LanguageManager.cs        # 多语言资源管理
│   │       └── CloudSyncService.cs       # 云同步（可选）
│   ├── Data/
│   │   ├── DbContext/        # SqlSugar 数据库上下文
│   │   ├── Entities/         # 数据库实体
│   │   ├── Repositories/     # 仓储层
│   │   └── Services/         # 数据服务
│   ├── Domain/
│   │   └── Models/           # 领域模型（StationState / MotorTestResult 等）
│   ├── Infrastructure/       # 基础设施（程序集信息等）
│   ├── Presentation/
│   │   ├── Converters/       # IValueConverter（状态 / 颜色 / 可见性转换）
│   │   ├── Services/         # IDialogService / IDispatcherService
│   │   ├── ViewModels/       # 10 个 ViewModel（MVVM 核心）
│   │   ├── Views/            # XAML 视图（含图表、历史、通知等）
│   │   └── Windows/          # App.xaml / MainWindow
│   ├── Resources/
│   │   ├── Images/
│   │   └── Styles/           # 全局样式 / MaterialDesign 主题覆盖
│   ├── docs/                 # 补充文档（code-review、协议说明等）
│   ├── appsettings.json
│   └── global.json
├── MotorTestSystem.Tests/    # 测试项目（xUnit）
│   ├── Benchmarks/
│   ├── IntegrationTests/
│   ├── Performance/
│   ├── PropertyTests/
│   └── StressTests/
├── docs/                     # 仓库级文档（审查报告、协议文档）

```

---

## 技术栈

| 层次 | 选型 | 版本 |
|------|------|------|
| 运行时 | .NET | 8.0 |
| UI 框架 | WPF | 内置 |
| MVVM | CommunityToolkit.Mvvm | 8.4.2 |
| 图表 | LiveChartsCore.SkiaSharpView.WPF | 2.0.4 |
| UI 主题 | MaterialDesignThemes | 5.3.2 |
| PLC 通信 | S7NetPlus | 0.20.0 |
| ORM / 数据库 | SqlSugarCore + SQLite | 5.1.4.214 |
| 视频集成 | 海康威视 HCNetSDK（本地 DLL） | — |
| 测试 | xUnit + BenchmarkDotNet | — |

---

## 快速启动

### 前置要求

- **Windows 10/11 x64**
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Visual Studio 2022 或 VS Code + C# Dev Kit
- 海康威视 SDK 库文件（需单独获取，放置于 `../海康库文件/`）

### 克隆 & 运行

```bash
git clone <repo-url>
cd Econ/MotorTestSystem
dotnet run
```

或在 Visual Studio 中打开 `MotorTestSystem.csproj`，按 `F5`。

> **注意**：海康 SDK DLL 在 csproj 中通过 `ContentWithTargetPath` 引用 `../海康库文件/`，若路径不存在则构建会报错。  
> 无需视频功能时可在 csproj 中注释掉对应 `<ItemGroup>` 块。

### 运行测试

```bash
cd Econ/MotorTestSystem.Tests
dotnet test
```

---

## 核心设计

### 异步懒初始化运行时

`BackendRuntime` 通过 `Lazy<Task<BackendRuntime>>` 实现异步单例，启动不阻塞 UI 线程：

```csharp
var runtime = await BackendRuntime.SharedAsync;
```

### 节流 Channel 驱动 Dashboard

`DashboardViewModel` 通过有界 `Channel<T>` 接收 PLC 遥测事件，后台线程聚合后批量推送 UI，轮询频率与渲染频率完全解耦。

### Dead Letter Queue 补偿

SQLite 写入失败时序列化到本地 DLQ 文件，重启后自动回放，数据零丢失。

### RBAC 权限控制

`AuthService` 提供操作员 / 管理员两级权限，命令 `CanExecute` 直接绑定权限状态，配置修改和用户管理对操作员不可见。

### PLC 多协议适配

`PlcClientFactory` 根据 `StationConfig.Protocol` 字段动态创建对应驱动实例（Strategy 模式），新增协议只需实现 `IPlcClient` 接口并注册工厂即可。

---

## appsettings.json 说明

```json
{
  "DataPersistence": {
    "WriteChannelCapacity": 500,
    "MaxWriteRetryCount": 3,
    "FlushTimeoutSeconds": 10,
    "SQLiteSyncMode": "NORMAL",
    "SQLiteLongConnection": false,
    "CloudSyncEnabled": false,
    "DeadLetterQueuePath": ""
  }
}
```

---

## Roadmap

- [x] MVVM 四层架构（Domain / Business / Data / Presentation）
- [x] 三品牌 PLC 多协议驱动（三菱 MC / 西门子 S7 / ModbusTCP）
- [x] SQLite 异步批量写入 + DLQ 补偿
- [x] 节流 Channel 驱动 DashboardViewModel
- [x] RBAC 权限控制（操作员 / 管理员）
- [x] 海康威视 SDK 集成
- [x] 多语言支持（LanguageManager）
- [ ] 云同步端点实现
- [x] Excel 报表导出（MiniExcel）
- [ ] 全局未处理异常捕获与结构化日志

---

## License

[MIT](LICENSE) © 2026
