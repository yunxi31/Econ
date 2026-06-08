using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace MotorTestSystem.Converters
{
    public class ResultToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string result = value?.ToString() ?? "WAIT";
            return result.ToUpper() switch
            {
                "OK" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00FFB2")), // Mint Green
                "NG" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF3366")), // Neon Red
                "WAIT" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A0AAB2")), // Grey
                _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A0AAB2"))
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
