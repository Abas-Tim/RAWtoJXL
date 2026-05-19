using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace RAWtoJXL.Avalonia.Converters
{
    public class NullableIntConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null || value is int q && q == 0)
                return "";
            if (value is int v)
                return v.ToString();
            return "";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string s)
            {
                s = s.Trim();
                if (string.IsNullOrEmpty(s))
                    return null;
                if (int.TryParse(s, out int result))
                {
                    if (result < 0 || result > 100)
                        return null;
                    return result;
                }
                return null;
            }
            return null;
        }
    }
}
