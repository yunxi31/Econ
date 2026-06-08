using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace MotorTestSystem.Converters
{
    public class ValueToBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush RedBrush = new((Color)ColorConverter.ConvertFromString("#FF3366"));
        private static readonly SolidColorBrush WhiteBrush = new(Colors.White);
        private static readonly SolidColorBrush GreyBrush = new((Color)ColorConverter.ConvertFromString("#8E9AA7"));

        public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
            {
                return GreyBrush;
            }

            string colType = parameter?.ToString() ?? string.Empty;
            if (double.TryParse(value.ToString(), out double val))
            {
                switch (colType)
                {
                    case "NoLoadCurrent":
                        if (val > 1.5) return RedBrush;
                        break;
                    case "NoLoadSpeed":
                        if (val < 2900 || val > 3100) return RedBrush;
                        break;
                    case "Noise":
                        if (val > 60.0) return RedBrush;
                        break;
                    case "LoadCurrent":
                        if (val > 4.5) return RedBrush;
                        break;
                }
            }

            return WhiteBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
