using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ARWtoJXL.WPF
{
    public class BooleanToBrushConverter : IValueConverter
    {
        private static readonly Brush SelectedBrush = new SolidColorBrush(Color.FromArgb(30, 0, 120, 215));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isSelected && isSelected)
            {
                return SelectedBrush;
            }
            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}