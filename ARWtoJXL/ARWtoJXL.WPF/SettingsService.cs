using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ARWtoJXL.WPF
{
    public class AppSettings
    {
        [JsonPropertyName("useSubfolder")]
        public bool UseSubfolder { get; set; } = true;

        [JsonPropertyName("subfolderName")]
        public string SubfolderName { get; set; } = "jxl_output";

        [JsonPropertyName("qualityPreset")]
        public int QualityPreset { get; set; } = 90;
    }

    public static class SettingsService
    {
        private static readonly string SettingsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ARWtoJXL");
        private static readonly string SettingsPath = Path.Combine(SettingsDirectory, "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (!File.Exists(SettingsPath))
                    return new AppSettings();

                var json = File.ReadAllText(SettingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                return settings ?? new AppSettings();
            }
            catch
            {
                return new AppSettings();
            }
        }

        public static void Save(AppSettings settings)
        {
            try
            {
                Directory.CreateDirectory(SettingsDirectory);
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsPath, json);
            }
            catch
            {
                // Silently ignore save failures
            }
        }
    }
}
