using System;
using System.Globalization;
using System.Windows.Data;

namespace MotorTestSystem.Converters
{
    public class LocMultiConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length == 0 || values[0] == null)
                return string.Empty;

            string key = values[0].ToString();
            return LanguageManager.Instance[key];
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
