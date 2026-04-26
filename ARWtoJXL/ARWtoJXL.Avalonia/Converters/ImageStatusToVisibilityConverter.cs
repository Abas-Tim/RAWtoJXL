using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia;
using Avalonia.Controls;
using ARWtoJXL.Core.Interfaces;

namespace ARWtoJXL.Avalonia.Converters
{
    public class ImageStatusToVisibilityConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not ImageStatus status)
                return false;

            if (parameter?.ToString() == "Converting")
                return status == ImageStatus.Converting;

            return false;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
