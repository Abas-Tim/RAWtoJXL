using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ARWtoJXL.Core.Interfaces;

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

        [JsonPropertyName("searchRecursive")]
        public bool SearchRecursive { get; set; } = false;

        [JsonPropertyName("recentFiles")]
        public List<string> RecentFiles { get; set; } = new List<string>();

        [JsonPropertyName("outputFormat")]
        public OutputFormat OutputFormat { get; set; } = OutputFormat.Jxl;

        [JsonPropertyName("conflictResolution")]
        public ConflictResolution ConflictResolution { get; set; } = ConflictResolution.Overwrite;

        [JsonPropertyName("confirmOverwrite")]
        public bool ConfirmOverwrite { get; set; } = true;
    }

    public static class SettingsService
    {
        private static readonly string SettingsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ARWtoJXL");
        private static readonly string SettingsPath = Path.Combine(SettingsDirectory, "settings.json");
        private const int MaxRecentFiles = 50;

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
            }
        }

        public static void AddRecentFile(string filePath)
        {
            var settings = Load();
            settings.RecentFiles.RemoveAll(p => p == filePath);
            settings.RecentFiles.Insert(0, Path.GetFullPath(filePath));
            while (settings.RecentFiles.Count > MaxRecentFiles)
            {
                settings.RecentFiles.RemoveAt(settings.RecentFiles.Count - 1);
            }
            Save(settings);
        }
    }
}
