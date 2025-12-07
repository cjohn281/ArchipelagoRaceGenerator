using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;

namespace RaceConfig.GUI.Converters
{
    public sealed class FileNameConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is string s ? Path.GetFileName(s) : value;

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value; // not used
    }
}