using System;
using System.Globalization;
using System.Windows.Data;

namespace MotorTestSystem.Converters
{
    public class ViewTypeToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return false;

            string currentViewName = value.GetType().Name; // e.g., "DashboardViewModel"
            string? targetView = parameter.ToString(); // e.g., "Dashboard"

            if (string.IsNullOrWhiteSpace(targetView))
            {
                return false;
            }

            if (currentViewName.Equals("NotificationCenterViewModel", StringComparison.OrdinalIgnoreCase))
            {
                var pageTitleProp = value.GetType().GetProperty("PageTitle");
                if (pageTitleProp != null)
                {
                    string? pageTitle = pageTitleProp.GetValue(value) as string;
                    if (targetView.Equals("LogCenter", StringComparison.OrdinalIgnoreCase))
                    {
                        return pageTitle == "日志中心";
                    }
                    if (targetView.Equals("Notification", StringComparison.OrdinalIgnoreCase))
                    {
                        return pageTitle == "通知中心";
                    }
                }
            }

            return currentViewName.StartsWith(targetView, StringComparison.OrdinalIgnoreCase);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
