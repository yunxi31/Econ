# 海康SDK集成使用说明

## 概述

已成功将海康威视SDK集成到生产监控系统中，实现了摄像头连接、预览和抓图功能。

## 功能特性

### 1. 核心功能
- ✅ 摄像头登录/登出
- ✅ 实时预览（需要配合显示窗口）
- ✅ 抓图保存
- ✅ 多摄像头管理
- ✅ 错误处理和状态反馈

### 2. UI集成
- 在Dashboard视图右侧新增"视频监控"面板
- 支持IP地址、端口、用户名、密码配置
- 实时显示摄像头连接状态
- 提供连接/断开/抓图按钮

## 使用方法

### 1. 连接摄像头

1. 打开应用程序，进入"生产监控数据看板"页面
2. 在右侧"视频监控"面板中输入摄像头信息：
   - IP地址：例如 `192.168.1.64`
   - 端口：默认 `8000`
   - 用户名：摄像头登录用户名
   - 密码：摄像头登录密码
3. 点击"连接"按钮
4. 连接成功后，状态会显示"已连接 (X 通道)"

### 2. 抓图

1. 确保摄像头已连接
2. 点击"📷 抓图保存"按钮
3. 图片将保存到系统的"图片"文件夹中

### 3. 断开连接

1. 点击"断开"按钮
2. 状态会更新为"已断开"

## 技术架构

### 核心服务类

#### HikvisionSdkService
- 封装海康SDK的P/Invoke调用
- 提供登录、预览、抓图等核心功能
- 自动管理SDK生命周期

#### BackendRuntime
- 全局单例，管理所有后端服务
- 集成HikvisionSdkService实例
- 统一资源管理

### API接口

```csharp
// 登录设备
Task<LoginResult> LoginAsync(string ip, int port, string username, string password)

// 登出设备
bool Logout(string ip)

// 开始预览
int StartPreview(string ip, int channel, IntPtr windowHandle)

// 停止预览
bool StopPreview(int channel)

// 抓图
bool CapturePicture(int channel, string filePath)
```

## 配置说明

### SDK文件配置

所有海康SDK的DLL文件已在项目文件中配置为自动复制到输出目录：

```xml
<ContentWithTargetPath Include="..\海康库文件\HCNetSDK.dll">
  <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  <TargetPath>HCNetSDK.dll</TargetPath>
</ContentWithTargetPath>
```

### 文件结构

```
海康库文件/
├── HCNetSDK.dll          # 主SDK
├── HCCore.dll            # 核心库
├── PlayCtrl.dll          # 播放控制
├── AudioRender.dll       # 音频渲染
├── HCNetSDKCom/          # 扩展组件
│   ├── HCAlarm.dll
│   ├── HCPreview.dll
│   └── ...
└── ...
```

## 注意事项

### 1. 运行环境
- 确保所有DLL文件都存在于应用程序同一目录
- 需要Windows x64环境
- 需要安装Visual C++运行库

### 2. 网络配置
- 确保摄像头与应用程序在同一网络
- 检查防火墙设置，确保端口8000（或自定义端口）开放
- 验证摄像头IP地址可访问

### 3. 摄像头配置
- 确认摄像头已启用SDK访问
- 确认用户名和密码正确
- 建议使用管理员账户登录

### 4. 性能优化
- SDK已设置2秒连接超时，避免长时间等待
- 预览功能需要配合显示窗口使用（当前版本未完全集成）
- 建议在实际部署时根据需求调整超时参数

## 后续扩展

### 1. 视频预览窗口
- 可添加独立的视频显示窗口
- 集成WPF的WindowsFormsHost或HwndHost
- 实现实时视频流显示

### 2. 多摄像头管理
- 支持同时连接多个摄像头
- 提供摄像头切换功能
- 实现轮巡显示

### 3. 录像功能
- 添加录像控制
- 支持计划录像
- 实现录像回放

### 4. 云台控制
- PTZ控制接口
- 预置位管理
- 巡航功能

## 故障排查

### 连接失败
1. 检查网络连接：`ping 摄像头IP`
2. 验证端口是否开放：`telnet 摄像头IP 端口`
3. 确认用户名密码正确
4. 检查摄像头SDK是否启用

### SDK初始化失败
1. 确认所有DLL文件存在
2. 检查是否为x64环境
3. 安装Visual C++运行库

### 抓图失败
1. 确认摄像头已连接
2. 检查存储路径权限
3. 验证摄像头支持抓图功能

## 开发信息

- 开发时间：2026年6月10日
- SDK版本：海康威视 HCNetSDK
- 目标框架：.NET 8.0 Windows
- UI框架：WPF + MVVM

## 相关文件

- 服务类：`MotorTestSystem/Services/HikvisionSdkService.cs`
- 集成：`MotorTestSystem/Services/BackendRuntime.cs`
- 视图模型：`MotorTestSystem/ViewModels/DashboardViewModel.cs`
- 视图：`MotorTestSystem/Views/DashboardView.xaml`
- 项目配置：`MotorTestSystem/MotorTestSystem.csproj`
