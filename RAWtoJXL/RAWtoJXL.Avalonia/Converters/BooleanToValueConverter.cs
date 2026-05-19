using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace RAWtoJXL.Avalonia.Converters
{
    public class BooleanToValueConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            bool boolValue = value is bool b && b;
            string? converterParam = parameter as string;

            if (converterParam == "Invert" || converterParam == "InvertContent")
            {
                return boolValue ? AppStrings.DeselectAll : AppStrings.SelectAll;
            }

            if (converterParam == "DefaultIfZero")
            {
                int intValue = value is int i ? i : 0;
                return intValue > 0 ? intValue : 100;
            }

            return boolValue ? "True" : "False";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
