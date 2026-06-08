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
            string targetView = parameter.ToString(); // e.g., "Dashboard"

            return currentViewName.StartsWith(targetView, StringComparison.OrdinalIgnoreCase);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
