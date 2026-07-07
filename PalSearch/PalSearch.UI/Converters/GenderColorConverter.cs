using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace PalSearch.UI.Converters
{
    /// <summary>
    /// 将性别字符串转换为对应的颜色画刷
    /// </summary>
    [ValueConversion(typeof(string), typeof(Brush))]
    public class GenderColorConverter : IValueConverter
    {
        private static readonly Brush MaleBrush = new SolidColorBrush(Color.FromRgb(0x4F, 0xA8, 0xED));  // Blue
        private static readonly Brush FemaleBrush = new SolidColorBrush(Color.FromRgb(0xED, 0x4F, 0x8A)); // Pink
        private static readonly Brush UnknownBrush = new SolidColorBrush(Color.FromRgb(0x6C, 0x70, 0x86)); // Gray

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value?.ToString() switch
            {
                "Male" => MaleBrush,
                "Female" => FemaleBrush,
                _ => UnknownBrush
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}