using System;
using System.Globalization;
using System.Windows.Data;

namespace RaceConfig.GUI.Converters
{
    public class BoolStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str)
            {
                return bool.TryParse(str, out var result) && result;
            }
            if (value is bool b)
            {
                return b;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
            {
                return b.ToString();
            }
            return "False";
        }
    }
}