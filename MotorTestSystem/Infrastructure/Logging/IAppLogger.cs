using System;

namespace MotorTestSystem.Infrastructure.Logging
{
    /// <summary>
    /// 应用日志接口。服务层依赖此接口而非 Serilog 具体实现，保持可测试性。
    /// </summary>
    public interface IAppLogger
    {
        void Debug(string messageTemplate, params object?[] args);
        void Info(string messageTemplate, params object?[] args);
        /// <summary>Info 的别名，方便从 Serilog 习惯迁移的调用方使用。</summary>
        void Information(string messageTemplate, params object?[] args);
        void Warning(string messageTemplate, params object?[] args);
        void Warning(Exception ex, string messageTemplate, params object?[] args);
        void Error(string messageTemplate, params object?[] args);
        void Error(Exception ex, string messageTemplate, params object?[] args);
        void Fatal(Exception ex, string messageTemplate, params object?[] args);
    }
}
