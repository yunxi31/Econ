# 项目记忆 - MotorTestSystem

## 角色体系
- 统一为 3 个角色：管理员 (Admin)、操作员 (Operator)、维护员 (Maintainer)
- 登录默认密码：操作员(无需密码/123)、维护员(maint123/456)、管理员(admin123/789)
- 维护员权限范围：诊断功能、设备校准、数据查看、报警管理
- 注意：当前权限仅为 UI 展示，后端尚无实际访问控制逻辑

## 技术栈
- WPF (.NET 8/10)，MVVM 架构（CommunityToolkit.Mvvm）
- 用户认证为硬编码明文密码，无数据库/Token 机制
