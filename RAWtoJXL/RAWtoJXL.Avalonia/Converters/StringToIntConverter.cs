using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace RAWtoJXL.Avalonia.Converters
{
    public class StringToIntConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string s && int.TryParse(s, out int result))
                return result;
            return null;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is int i)
                return i.ToString();
            return null;
        }
    }
}
