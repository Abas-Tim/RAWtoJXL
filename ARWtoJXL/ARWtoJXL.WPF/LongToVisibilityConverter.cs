using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ARWtoJXL.WPF
{
    public class LongToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            long longValue = value is long l ? l : (value is int i ? i : 0);
            return longValue > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
