using System;
using System.Globalization;
using System.Windows.Data;

namespace MotorTestSystem.Converters;

public class ViewTypeToBoolConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value == null || parameter == null)
		{
			return false;
		}
		string name = value.GetType().Name;
		string value2 = parameter.ToString();
		if (string.IsNullOrWhiteSpace(value2))
		{
			return false;
		}
		return name.StartsWith(value2, StringComparison.OrdinalIgnoreCase);
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return Binding.DoNothing;
	}
}
