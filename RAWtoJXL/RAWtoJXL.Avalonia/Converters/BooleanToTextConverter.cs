using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace RAWtoJXL.Avalonia.Converters
{
    public class BooleanToTextConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isAllSelected)
            {
                return isAllSelected ? parameter?.ToString() ?? "Deselect All" : "Select All";
            }
            return "Select All";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
