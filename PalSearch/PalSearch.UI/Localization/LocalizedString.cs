using System.Windows.Data;
using System.Windows.Markup;

namespace PalSearch.UI.Localization
{
    public class LocExtension : MarkupExtension
    {
        public string? Key { get; set; }

        public LocExtension() { }

        public LocExtension(string key)
        {
            Key = key;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            var binding = new Binding(nameof(Translator.CurrentLocale))
            {
                Source = this,
                Mode = BindingMode.OneWay,
                Converter = new LocConverter(Key ?? "")
            };

            return binding.ProvideValue(serviceProvider);
        }

        private class LocConverter : System.Windows.Data.IValueConverter
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
}