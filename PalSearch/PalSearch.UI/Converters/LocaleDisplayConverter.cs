using PalSearch.UI.Localization;
using System;
using System.Globalization;
using System.Windows.Data;

namespace PalSearch.UI.Converters
{
    public class LocaleDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TranslationLocale locale)
            {
                return locale switch
                {
                    TranslationLocale.en => "English",
                    TranslationLocale.zhHans => "简体中文",
                    TranslationLocale.zhHant => "繁體中文",
                    TranslationLocale.ja => "日本語",
                    TranslationLocale.ko => "한국어",
                    TranslationLocale.de => "Deutsch",
                    TranslationLocale.fr => "Français",
                    TranslationLocale.es => "Español",
                    TranslationLocale.ru => "Русский",
                    _ => locale.ToString()
                };
            }
            return value?.ToString() ?? "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}