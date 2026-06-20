using System;
using System.Globalization;
using System.Windows.Data;

namespace MotorTestSystem.Converters
{
    /// <summary>
    /// 将 0~1 的进度值转换为环形进度的填充比例。
    /// 此 Converter 暂不用于 StrokeDashArray 直接绑定（因 DoubleCollection 类型限制），
    /// 改由 code-behind 处理。保留此文件以备其他用途。
    /// </summary>
    public class ProgressToDashArrayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value; // passthrough，实际绑定由 code-behind 处理
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
