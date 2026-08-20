using System;
using System.Globalization;
using Avalonia.Data.Converters;
using RAWtoJXL.Core.Interfaces;

namespace RAWtoJXL.Avalonia.Converters
{
    public class OutputFormatToTextConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is OutputFormat format ? format.ToString().ToLowerInvariant() : null;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
