using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace MotorTestSystem.Converters;

public class ValueToBrushConverter : IValueConverter
{
	private static readonly SolidColorBrush RedBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF3366"));

	private static readonly SolidColorBrush WhiteBrush = new SolidColorBrush(Colors.White);

	private static readonly SolidColorBrush GreyBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8E9AA7"));

	public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value == null)
		{
			return GreyBrush;
		}
		string text = parameter?.ToString() ?? string.Empty;
		if (double.TryParse(value.ToString(), out var result))
		{
			switch (text)
			{
			case "NoLoadCurrent":
				if (result > 1.5)
				{
					return RedBrush;
				}
				break;
			case "NoLoadSpeed":
				if (result < 2900.0 || result > 3100.0)
				{
					return RedBrush;
				}
				break;
			case "Noise":
				if (result > 60.0)
				{
					return RedBrush;
				}
				break;
			case "LoadCurrent":
				if (result > 4.5)
				{
					return RedBrush;
				}
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
