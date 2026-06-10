using CommunityToolkit.Mvvm.ComponentModel;

namespace MotorTestSystem.Models
{
    /// <summary>
    /// 用户状态
    /// </summary>
    public enum UserStatus
    {
        Active,    // 启用
        Disabled   // 禁用
    }

    /// <summary>
    /// 系统角色
    /// </summary>
    public enum AppRole
    {
        Admin,      // 管理员
        Operator,   // 操作员
        Maintainer  // 维护员
    }

    /// <summary>
    /// 系统权限枚举
    /// </summary>
    public enum AppPermission
    {
        /// <summary>生产监控</summary>
        Monitor,
        /// <summary>设备控制（启动/停止测试）</summary>
        DeviceControl,
        /// <summary>生产看板</summary>
        Dashboard,
        /// <summary>数据追溯</summary>
        History,
        /// <summary>诊断功能</summary>
        Diagnosis,
        /// <summary>设备校准/复位</summary>
        Calibration,
        /// <summary>系统配置</summary>
        SystemConfig,
        /// <summary>用户管理</summary>
        UserManagement,
        /// <summary>报警管理</summary>
        AlarmManagement,
        /// <summary>数据导出</summary>
        DataExport,
    }

    /// <summary>
    /// 用户实体
    /// </summary>
    public partial class AppUser : ObservableObject
    {
        [ObservableProperty]
        private string _id = string.Empty;

        [ObservableProperty]
        private string _account = string.Empty;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _passwordHash = string.Empty;

        [ObservableProperty]
        private AppRole _role = AppRole.Operator;

        [ObservableProperty]
        private UserStatus _status = UserStatus.Active;

        [ObservableProperty]
        private DateTime? _lastLoginTime;

        [ObservableProperty]
        private DateTime _createdAt = DateTime.Now;

        [ObservableProperty]
        private DateTime _updatedAt = DateTime.Now;

        // ===== 显示用属性 =====

        /// <summary>角色中文显示名</summary>
        public string RoleDisplayName => Role switch
        {
            AppRole.Admin => "管理员",
            AppRole.Operator => "操作员",
            AppRole.Maintainer => "维护员",
            _ => Role.ToString()
        };

        /// <summary>状态中文显示名</summary>
        public string StatusDisplayName => Status switch
        {
            UserStatus.Active => "在线",
            UserStatus.Disabled => "禁用",
            _ => Status.ToString()
        };

        /// <summary>账号前缀（OP- / MT- / AD-）</summary>
        public string AccountPrefix => Role switch
        {
            AppRole.Admin => "AD",
            AppRole.Operator => "OP",
            AppRole.Maintainer => "MT",
            _ => "UK"
        };
    }

    /// <summary>
    /// 角色权限映射（静态配置，定义每个角色拥有的权限集合）
    /// </summary>
    public static class RolePermissions
    {
        private static readonly Dictionary<AppRole, HashSet<AppPermission>> _permissions = new()
        {
            [AppRole.Admin] = new HashSet<AppPermission>
            {
                AppPermission.Monitor,
                AppPermission.DeviceControl,
                AppPermission.Dashboard,
                AppPermission.History,
                AppPermission.Diagnosis,
                AppPermission.Calibration,
                AppPermission.SystemConfig,
                AppPermission.UserManagement,
                AppPermission.AlarmManagement,
                AppPermission.DataExport,
            },
            [AppRole.Operator] = new HashSet<AppPermission>
            {
                AppPermission.Monitor,
                AppPermission.DeviceControl,
                AppPermission.Dashboard,
                AppPermission.History,
                AppPermission.DataExport,
            },
            [AppRole.Maintainer] = new HashSet<AppPermission>
            {
                AppPermission.Monitor,
                AppPermission.Dashboard,
                AppPermission.History,
                AppPermission.Diagnosis,
                AppPermission.Calibration,
                AppPermission.AlarmManagement,
                AppPermission.DataExport,
            },
        };

        /// <summary>
        /// 获取指定角色的所有权限
        /// </summary>
        public static IReadOnlySet<AppPermission> GetPermissions(AppRole role)
            => _permissions.GetValueOrDefault(role) ?? new HashSet<AppPermission>();

        /// <summary>
        /// 判断角色是否拥有指定权限
        /// </summary>
        public static bool HasPermission(AppRole role, AppPermission permission)
            => _permissions.GetValueOrDefault(role)?.Contains(permission) == true;

        /// <summary>
        /// 获取权限的中文显示名
        /// </summary>
        public static string GetPermissionDisplayName(AppPermission permission) => permission switch
        {
            AppPermission.Monitor => "生产监控",
            AppPermission.DeviceControl => "设备控制",
            AppPermission.Dashboard => "生产看板",
            AppPermission.History => "数据追溯",
            AppPermission.Diagnosis => "诊断功能",
            AppPermission.Calibration => "设备校准",
            AppPermission.SystemConfig => "系统配置",
            AppPermission.UserManagement => "用户管理",
            AppPermission.AlarmManagement => "报警管理",
            AppPermission.DataExport => "数据导出",
            _ => permission.ToString()
        };

        /// <summary>
        /// 获取权限的图标样式类别（用于 UI 标签高亮）
        /// </summary>
        public static bool IsHighlightPermission(AppRole role, AppPermission permission)
        {
            // 管理员：系统权限类高亮
            if (role == AppRole.Admin)
                return permission is AppPermission.SystemConfig or AppPermission.UserManagement;

            // 操作员：设备控制高亮
            if (role == AppRole.Operator)
                return permission == AppPermission.DeviceControl;

            // 维护员：诊断/校准高亮
            if (role == AppRole.Maintainer)
                return permission is AppPermission.Diagnosis or AppPermission.Calibration;

            return false;
        }
    }
}
