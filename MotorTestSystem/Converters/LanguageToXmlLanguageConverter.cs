using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace MotorTestSystem.Converters
{
    public class LanguageToXmlLanguageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string lang)
            {
                if (lang.Equals("ZH", StringComparison.OrdinalIgnoreCase))
                {
                    return XmlLanguage.GetLanguage("zh-CN");
                }
            }
            return XmlLanguage.GetLanguage("en-US");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
