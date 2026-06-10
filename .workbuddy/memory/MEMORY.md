# 项目记忆 - MotorTestSystem

## 角色体系 & RBAC
- 3 个角色：管理员 (Admin)、操作员 (Operator)、维护员 (Maintainer)
- RBAC 权限模型已实现：AppPermission 枚举 + RolePermissions 静态映射
- 权限矩阵：
  - Admin: 全部权限（含用户管理、系统配置）
  - Operator: 监控、设备控制、看板、追溯、数据导出
  - Maintainer: 监控、看板、追溯、诊断、校准、报警、数据导出
- 导航项根据权限动态显示/隐藏（MainViewModel.IsXxxVisible + BoolToVisibilityConverter）
- 认证流程：LoginWindow → AuthService.Login() → App.SetAuthenticatedUser() → MainViewModel 刷新权限
- 密码存储：SHA256 哈希（InMemoryUserService）
- 种子用户 10 个（InMemoryUserService.SeedDefaultUsers）

## 技术栈
- WPF (.NET 8/10)，MVVM 架构（CommunityToolkit.Mvvm）
- 后端运行时单例：BackendRuntime.Shared（包含 Repository + UserService + AuthService + PollingService）
- 用户管理已对接 IUserService，右侧权限面板动态绑定

## Dashboard 后端
- 数据全部从 IMotorTestRepository 动态获取，无硬编码 Mock 数据
- 订阅 PlcPollingService.SnapshotReceived 事件 + 5秒定时器双驱动刷新
- 环形进度条通过 DashboardView.xaml.cs code-behind + PropertyChanged 监听更新（因 WPF DoubleCollection 类型限制无法直接绑定）
- 故障分类规则：空载电流>2.5→电流超限，噪音差>15→噪声过大，负载电流>3.0→电流超限 等
- 日产量目标常量：DailyTarget = 2000，良率目标：TargetPassRate = 98%
