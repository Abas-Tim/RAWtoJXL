using System;
using System.Globalization;
using Avalonia.Data.Converters;
using RAWtoJXL.Core.Interfaces;

namespace RAWtoJXL.Avalonia.Converters
{
    public class ImageStatusToStringConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is ImageStatus status)
            {
                if (parameter?.ToString() == "Converting")
                {
                    return status == ImageStatus.Converting;
                }

                return status switch
                {
                    ImageStatus.Pending => "Pending",
                    ImageStatus.Ready => "Ready to convert",
                    ImageStatus.Converting => "Converting...",
                    ImageStatus.Converted => "Converted",
                    ImageStatus.Failed => "Failed",
                    _ => status.ToString()
                };
            }
            return value?.ToString() ?? string.Empty;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
