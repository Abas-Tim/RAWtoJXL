using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace ARWtoJXL.Avalonia.Converters
{
    public class StringToVisibilityConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            bool hasValue = !string.IsNullOrEmpty(value as string);
            return targetType == typeof(double) ? (hasValue ? 1.0 : 0.0) : hasValue;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
