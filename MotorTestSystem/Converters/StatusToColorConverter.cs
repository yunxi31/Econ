using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace MotorTestSystem.Converters
{
    public class StatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int status)
            {
                return status switch
                {
                    0 => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFB800")), // Standby (Amber)
                    1 => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00FFB2")), // Running (Mint Green)
                    2 => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF3366")), // Fault (Neon Red)
                    _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A0AAB2"))  // Default Grey
                };
            }
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A0AAB2"));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
