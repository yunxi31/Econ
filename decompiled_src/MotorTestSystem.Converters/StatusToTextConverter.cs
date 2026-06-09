using System;
using System.Globalization;
using System.Windows.Data;

namespace MotorTestSystem.Converters;

public class StatusToTextConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value is int num)
		{
			if (1 == 0)
			{
			}
			string result = num switch
			{
				0 => "待机", 
				1 => "运行中", 
				2 => "故障", 
				_ => "未知", 
			};
			if (1 == 0)
			{
			}
			return result;
		}
		return "未知";
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
