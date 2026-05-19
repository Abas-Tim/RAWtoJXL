using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace RAWtoJXL.Avalonia.Converters
{
    public class IntToDoubleConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is int v ? (double)v : 90.0;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is double d)
            {
                int result = Math.Max(0, Math.Min(100, (int)Math.Round(d)));
                return result;
            }
            return null;
        }
    }
}
