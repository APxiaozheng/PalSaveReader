using System;
using System.Globalization;
using System.Windows.Data;

namespace PalSearch.UI.Converters
{
    /// <summary>
    /// 将 0.0-1.0 的百分比值转换为宽度（像素），乘以 ConverterParameter 指定的最大值
    /// </summary>
    [ValueConversion(typeof(double), typeof(double))]
    public class PercentToWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double percent = 0;
            if (value is double d)
                percent = d;
            else if (value is float f)
                percent = f;
            else if (value is int i)
                percent = i / 100.0;

            double maxWidth = 100;
            if (parameter is double p)
                maxWidth = p;
            else if (parameter is string s && double.TryParse(s, out var parsed))
                maxWidth = parsed;
            else if (parameter is int ip)
                maxWidth = ip;

            return Math.Max(0, Math.Min(maxWidth, percent * maxWidth));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}