using System.Resources;
using System.Globalization;

namespace PalSearch.UI.Localization
{
    public enum TranslationLocale
    {
        en,
        zhHans,
        zhHant,
        ja,
        ko,
        de,
        fr,
        es,
        ru
    }

    public static class Translator
    {
        private static readonly TranslationLocale FallbackLocale = TranslationLocale.en;
        private static readonly object initLock = new();
        private static bool didInit = false;

        private static Dictionary<TranslationLocale, Dictionary<string, string>> translations;
        private static TranslationLocale currentLocale = TranslationLocale.en;

        public static TranslationLocale CurrentLocale
        {
            get => currentLocale;
            set
            {
                currentLocale = value;
                LocaleChanged?.Invoke();
            }
        }

        public static event Action LocaleChanged;

        public static void Init()
        {
            if (didInit) return;

            lock (initLock)
            {
                if (didInit) return;

                DoInit();
                didInit = true;
            }
        }

        private static void DoInit()
        {
            translations = new();

            foreach (TranslationLocale locale in Enum.GetValues<TranslationLocale>())
            {
                var resxName = locale.ToFormalName();
                var rm = new ResourceManager(
                    "PalSearch.UI.Localization.Localizations." + resxName,
                    typeof(Translator).Assembly);

                try
                {
                    var rs = rm.GetResourceSet(CultureInfo.InvariantCulture, true, true);
                    if (rs == null) continue;

                    var dict = new Dictionary<string, string>();
                    var enumerator = rs.GetEnumerator();
                    while (enumerator.MoveNext())
                    {
                        dict.Add(enumerator.Key.ToString()!, enumerator.Value?.ToString() ?? "");
                    }
                    translations[locale] = dict;
                }
                catch
                {
                    // Resource file not found for this locale, skip
                }
            }

            // Fill missing translations with fallback locale values
            if (translations.TryGetValue(FallbackLocale, out var fallbackDict))
            {
                foreach (var locale in translations.Keys)
                {
                    if (locale == FallbackLocale) continue;
                    var localeDict = translations[locale];
                    foreach (var kvp in fallbackDict)
                    {
                        if (!localeDict.ContainsKey(kvp.Key))
                            localeDict[kvp.Key] = kvp.Value;
                    }
                }
            }
        }

        public static string Get(string key, params object[] args)
        {
            Init();

            if (translations.TryGetValue(currentLocale, out var dict) && dict.TryGetValue(key, out var value))
                return args.Length > 0 ? string.Format(value, args) : value;

            return key;
        }
    }

    public static class TranslationLocaleExtensions
    {
        public static string ToFormalName(this TranslationLocale locale)
        {
            return locale switch
            {
                TranslationLocale.en => "en",
                TranslationLocale.zhHans => "zh-Hans",
                TranslationLocale.zhHant => "zh-Hant",
                TranslationLocale.ja => "ja",
                TranslationLocale.ko => "ko",
                TranslationLocale.de => "de",
                TranslationLocale.fr => "fr",
                TranslationLocale.es => "es",
                TranslationLocale.ru => "ru",
                _ => locale.ToString()
            };
        }
    }
}