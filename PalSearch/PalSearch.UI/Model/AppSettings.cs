using Newtonsoft.Json;
using PalSearch.UI.Localization;
using System.IO;

namespace PalSearch.UI.Model
{
    public class AppSettings
    {
        public TranslationLocale Locale { get; set; } = TranslationLocale.zhHans;
        public string LastSaveId { get; set; }

        private static string SettingsPath => Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
            "PalSearch", "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                    return JsonConvert.DeserializeObject<AppSettings>(File.ReadAllText(SettingsPath)) ?? new AppSettings();
            }
            catch { }
            return new AppSettings();
        }

        public void Save()
        {
            var dir = Path.GetDirectoryName(SettingsPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(SettingsPath, JsonConvert.SerializeObject(this));
        }
    }
}