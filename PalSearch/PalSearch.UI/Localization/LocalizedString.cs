using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace PalSearch.UI.Localization
{
    public class LocExtension : MarkupExtension
    {
        public string Key { get; set; }

        public LocExtension() { }

        public LocExtension(string key)
        {
            Key = key;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            var binding = new Binding(nameof(LocaleBindingSource.CurrentLocale))
            {
                Source = LocaleBindingSource.Instance,
                Mode = BindingMode.OneWay,
                Converter = new LocConverter(Key ?? "")
            };

            return binding.ProvideValue(serviceProvider);
        }

        private class LocConverter : IValueConverter
        {
            private readonly string key;

            public LocConverter(string key)
            {
                this.key = key;
            }

            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                return Translator.Get(key);
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                throw new NotSupportedException();
            }
        }
    }

    internal class LocaleBindingSource : INotifyPropertyChanged
    {
        public static LocaleBindingSource Instance { get; } = new();

        static LocaleBindingSource()
        {
            Translator.LocaleChanged += () =>
                Instance.PropertyChanged?.Invoke(Instance, new PropertyChangedEventArgs(nameof(CurrentLocale)));
        }

        public TranslationLocale CurrentLocale => Translator.CurrentLocale;

        public event PropertyChangedEventHandler PropertyChanged;
    }
}