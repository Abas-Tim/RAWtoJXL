using System;
using System.Globalization;
using System.Windows.Data;
using ARWtoJXL.Core.Services;

namespace ARWtoJXL.WPF
{
    public class OutputFormatToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is OutputFormat format)
            {
                return format == OutputFormat.Jxl;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isChecked && isChecked)
            {
                return OutputFormat.Jxl;
            }
            return Binding.DoNothing;
        }
    }
}
