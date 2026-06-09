using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace MotorTestSystem.Converters;

public class StatusToColorConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value is int num)
		{
			if (1 == 0)
			{
			}
			SolidColorBrush result = num switch
			{
				0 => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFB800")), 
				1 => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00FFB2")), 
				2 => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF3366")), 
				_ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A0AAB2")), 
			};
			if (1 == 0)
			{
			}
			return result;
		}
		return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A0AAB2"));
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
