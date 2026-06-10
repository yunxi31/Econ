using System;
using System.Globalization;
using System.Windows.Data;

namespace MotorTestSystem.Converters
{
    public class EnumToBoolConverter : IValueConverter
    {
        private static EnumToBoolConverter? _instance;
        public static EnumToBoolConverter Instance => _instance ??= new EnumToBoolConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return false;

            string parameterString = parameter.ToString()!;
            if (!Enum.IsDefined(value.GetType(), value))
                return false;

            string valueString = value.ToString()!;
            return string.Equals(valueString, parameterString, StringComparison.OrdinalIgnoreCase);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isChecked && isChecked && parameter != null)
            {
                try
                {
                    Type actualType = Nullable.GetUnderlyingType(targetType) ?? targetType;
                    return Enum.Parse(actualType, parameter.ToString()!, true);
                }
                catch
                {
                    return Binding.DoNothing;
                }
            }
            return Binding.DoNothing;
        }
    }
}
