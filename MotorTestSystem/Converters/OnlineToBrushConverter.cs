using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace MotorTestSystem.Converters
{
    public class OnlineToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isOnline)
            {
                return isOnline 
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00FFB2"))  // Green (Online)
                    : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF3366")); // Red (Offline)
            }
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A0AAB2"));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
