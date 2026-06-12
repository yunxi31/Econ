using System;
using System.Globalization;
using System.Windows.Data;

namespace MotorTestSystem.Converters
{
    public class StringEqualsToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return false;

            return value.ToString() == parameter.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isChecked && isChecked)
            {
                return parameter?.ToString() ?? string.Empty;
            }
            return Binding.DoNothing;
        }
    }
}
