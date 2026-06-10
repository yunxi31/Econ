# 项目记忆 - MotorTestSystem

## 角色体系
- 统一为 3 个角色：管理员 (Admin)、操作员 (Operator)、维护员 (Maintainer)
- 登录默认密码：操作员(无需密码/123)、维护员(maint123/456)、管理员(admin123/789)
- 维护员权限范围：诊断功能、设备校准、数据查看、报警管理
- 注意：当前权限仅为 UI 展示，后端尚无实际访问控制逻辑

## 技术栈
- WPF (.NET 8/10)，MVVM 架构（CommunityToolkit.Mvvm）
- 用户认证为硬编码明文密码，无数据库/Token 机制

## Dashboard 后端
- 数据全部从 IMotorTestRepository 动态获取，无硬编码 Mock 数据
- 订阅 PlcPollingService.SnapshotReceived 事件 + 5秒定时器双驱动刷新
- 环形进度条通过 DashboardView.xaml.cs code-behind + PropertyChanged 监听更新（因 WPF DoubleCollection 类型限制无法直接绑定）
- 故障分类规则：空载电流>2.5→电流超限，噪音差>15→噪声过大，负载电流>3.0→电流超限 等
- 日产量目标常量：DailyTarget = 2000，良率目标：TargetPassRate = 98%

