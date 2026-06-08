# 📐 Git 提交规范

本文档定义了 **MotorTestSystem** 仓库的 Git 提交消息规范，基于 [Conventional Commits 1.0.0](https://www.conventionalcommits.org/zh-hans/v1.0.0/) 标准，并结合工业软件项目的实际需求进行了扩展。

---

## 📌 提交消息格式

```
<type>(<scope>): <subject>

[body]

[footer]
```

### 必填字段

| 字段 | 说明 |
|------|------|
| `type` | 提交类型（见下表） |
| `scope` | 影响范围，可选但推荐填写 |
| `subject` | 简短描述，**中文或英文均可**，不超过 72 字符，**不加句号** |

### 可选字段

| 字段 | 说明 |
|------|------|
| `body` | 详细说明，解释「为什么」而非「做了什么」 |
| `footer` | 关联 Issue（`Closes #12`）或破坏性变更说明（`BREAKING CHANGE:`） |

---

## 🏷️ Type 类型说明

| Type | 适用场景 | 示例 |
|------|----------|------|
| `feat` | 新增功能 | `feat(monitor): 新增 A4 机台西门子 S7-1500 噪音测试接入` |
| `fix` | Bug 修复 | `fix(dashboard): 修复良率计算在 0 产量时除零异常` |
| `refactor` | 代码重构（不改变功能） | `refactor(viewmodel): 将 MainViewModel 导航逻辑抽象为 NavigationService` |
| `perf` | 性能优化 | `perf(monitor): 将 DispatcherTimer 替换为 Task.Run 异步轮询，降低 UI 阻塞` |
| `style` | 代码格式、命名（不影响逻辑） | `style(xaml): 统一 Margin 写法为四值简写形式` |
| `docs` | 文档变更 | `docs(readme): 补充 PLC 接入配置说明` |
| `test` | 测试相关 | `test(history): 补充 CSV 导出边界条件单元测试` |
| `build` | 构建/依赖变更 | `build(deps): 升级 LiveChartsCore 至 2.0.5` |
| `ci` | CI/CD 流程变更 | `ci: 添加 GitHub Actions 自动构建流水线` |
| `chore` | 杂项（不影响源码） | `chore: 更新 .gitignore 忽略 bin/obj 目录` |
| `revert` | 回滚某次提交 | `revert: revert "feat(plc): 接入汇川 ModbusTCP 驱动"` |

---

## 🎯 Scope 范围参考

根据本项目模块划分，推荐使用以下 scope：

| Scope | 对应模块 |
|-------|----------|
| `dashboard` | 生产看板（DashboardView / DashboardViewModel） |
| `monitor` | 工位监视（MonitorView / MonitorViewModel） |
| `history` | 历史追溯（HistoryView / HistoryViewModel） |
| `config` | 通信配置（ConfigView / ConfigViewModel） |
| `plc` | PLC 通信驱动层（三菱 / 西门子 / 汇川） |
| `db` | 数据库访问层（SQL Server / UPSERT 逻辑） |
| `model` | 数据模型（Models 目录） |
| `converter` | WPF 值转换器（Converters 目录） |
| `shell` | 主窗口 Shell（MainWindow / MainViewModel） |
| `sdk` | 海康威视 SDK 集成 |
| `deps` | 依赖包（NuGet） |

---

## ✅ 规范示例

### feat — 新增功能

```
feat(plc): 接入三菱 FX5U MC Protocol 实时数据采集

使用 HslCommunication MelsecMcNet 客户端替换模拟数据源。
轮询周期 500ms，通过 Task.Run + Dispatcher.InvokeAsync 保证 UI 线程安全。

Closes #8
```

---

### fix — Bug 修复

```
fix(history): 修复日期筛选在跨月查询时返回空结果

原因：EndDate 未包含当天 23:59:59，导致精确到日期的数据被截断。
修复：EndDate 统一追加 TimeSpan(23, 59, 59)。

Closes #15
```

---

### refactor — 重构

```
refactor(monitor): 将 6 机台模拟逻辑拆分为独立 StationSimulator 类

原 MonitorViewModel 构造函数超过 160 行，职责过重。
拆分后 ViewModel 仅负责数据绑定，模拟器可单独替换为真实驱动。
```

---

### build — 依赖变更

```
build(deps): 将 TargetFramework 从 net10.0 回退至 net8.0

net10.0 下 SkiaSharp.Views.WPF 3.119.0 存在兼容性警告，
net8.0 已有稳定 WindowsDesktop 运行时，优先保障生产环境稳定性。
```

---

### docs — 文档

```
docs(readme): 新增数据库 DDL 与 UPSERT 策略说明
```

---

## ❌ 不规范示例（避免）

```bash
# ❌ 含义不明
git commit -m "update"
git commit -m "fix bug"
git commit -m "修改了一些东西"

# ❌ 缺少 type
git commit -m "添加导出 CSV 功能"

# ❌ subject 过长（超 72 字符）
git commit -m "feat(history): 在历史数据查询页面中添加了按条码、按日期范围、按测试结果进行多条件组合筛选的功能以及将结果导出为 CSV 文件的按钮"
```

正确写法：

```bash
# ✅ 简洁明了
git commit -m "feat(history): 新增多条件组合筛选与 CSV 导出功能"
```

---

## 🔀 分支命名规范

| 前缀 | 适用场景 | 示例 |
|------|----------|------|
| `feature/` | 新功能开发 | `feature/plc-mitsubishi-driver` |
| `fix/` | Bug 修复 | `fix/dashboard-zero-division` |
| `refactor/` | 重构 | `refactor/navigation-service` |
| `docs/` | 文档 | `docs/readme-update` |
| `release/` | 版本发布 | `release/v1.0.0` |

> 分支名全部小写，单词间使用 `-` 连接，**禁止使用下划线或空格**。

---

## 🔖 版本号规范（语义化版本）

格式：`MAJOR.MINOR.PATCH`

| 变更类型 | 版本号递增 | 触发条件 |
|----------|------------|----------|
| 破坏性变更 | `MAJOR` +1 | 数据库结构变更、协议切换等不兼容变更 |
| 新增功能 | `MINOR` +1 | 新增模块、新增 PLC 接入 |
| Bug 修复 | `PATCH` +1 | 缺陷修复、小优化 |

---

## 🛠️ 推荐工具

| 工具 | 用途 |
|------|------|
| [Commitizen](https://github.com/commitizen/cz-cli) | 交互式生成规范提交消息 |
| [commitlint](https://commitlint.js.org/) | 提交消息格式自动校验 |
| [git-cliff](https://github.com/orhun/git-cliff) | 根据提交记录自动生成 CHANGELOG |

---

## 📋 提交前检查清单

在执行 `git commit` 前，请确认以下几点：

- [ ] `dotnet build` 无错误
- [ ] 新增功能已在对应 ViewModel 写注释
- [ ] 敏感信息（IP 地址、账号密码）未硬编码进源码
- [ ] `.gitignore` 已包含 `bin/`、`obj/`、`*.user`、`appsettings.local.json`
- [ ] 提交消息符合本规范格式
