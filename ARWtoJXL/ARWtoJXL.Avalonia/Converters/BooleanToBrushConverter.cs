using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace ARWtoJXL.Avalonia.Converters
{
    public class BooleanToBrushConverter : IValueConverter
    {
        private static readonly Brush SelectedBrush = new SolidColorBrush(Color.Parse("#1E0078D7"));

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isSelected && isSelected)
            {
                return SelectedBrush;
            }
            return Brushes.Transparent;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
