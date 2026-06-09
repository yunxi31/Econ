using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace MotorTestSystem.Converters;

public class ResultToBrushConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		string text = value?.ToString() ?? "WAIT";
		string text2 = text.ToUpper();
		if (1 == 0)
		{
		}
		SolidColorBrush result = text2 switch
		{
			"OK" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00FFB2")), 
			"NG" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF3366")), 
			"WAIT" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A0AAB2")), 
			_ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A0AAB2")), 
		};
		if (1 == 0)
		{
		}
		return result;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
