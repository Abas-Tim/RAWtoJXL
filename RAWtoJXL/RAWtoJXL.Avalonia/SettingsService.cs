using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using RAWtoJXL.Core.Interfaces;

namespace RAWtoJXL.Avalonia
{
    public enum ConflictResolution
    {
        Overwrite,
        Skip,
        AppendNumber
    }
    public class ConversionPreset
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("quality")]
        public int Quality { get; set; } = 90;

        [JsonPropertyName("outputFormat")]
        public OutputFormat OutputFormat { get; set; } = OutputFormat.Jxl;

        [JsonPropertyName("conflictResolution")]
        public ConflictResolution ConflictResolution { get; set; } = ConflictResolution.Overwrite;

        [JsonPropertyName("useSubfolder")]
        public bool UseSubfolder { get; set; } = true;

        [JsonPropertyName("subfolderName")]
        public string SubfolderName { get; set; } = "jxl_output";

        [JsonPropertyName("useCustomOutputDirectory")]
        public bool UseCustomOutputDirectory { get; set; } = false;

        [JsonPropertyName("customOutputDirectory")]
        public string CustomOutputDirectory { get; set; } = string.Empty;

        [JsonPropertyName("confirmOverwrite")]
        public bool ConfirmOverwrite { get; set; } = true;

        [JsonPropertyName("skipMetadata")]
        public bool SkipMetadata { get; set; } = false;

        [JsonPropertyName("cjxlEffort")]
        public int CjxlEffort { get; set; } = 7;

        [JsonPropertyName("cjxlThreads")]
        public int CjxlThreads { get; set; } = -1;
    }

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

        [JsonPropertyName("useCustomOutputDirectory")]
        public bool UseCustomOutputDirectory { get; set; } = false;

        [JsonPropertyName("customOutputDirectory")]
        public string CustomOutputDirectory { get; set; } = string.Empty;

        [JsonPropertyName("presets")]
        public List<ConversionPreset> Presets { get; set; } = new List<ConversionPreset>();

        [JsonPropertyName("skipMetadata")]
        public bool SkipMetadata { get; set; } = false;

        [JsonPropertyName("cjxlEffort")]
        public int CjxlEffort { get; set; } = 7;

        [JsonPropertyName("cjxlThreads")]
        public int CjxlThreads { get; set; } = -1;

        public AppSettings Clone()
        {
            return new AppSettings
            {
                UseSubfolder = UseSubfolder,
                SubfolderName = SubfolderName,
                QualityPreset = QualityPreset,
                SearchRecursive = SearchRecursive,
                RecentFiles = new List<string>(RecentFiles),
                OutputFormat = OutputFormat,
                ConflictResolution = ConflictResolution,
                ConfirmOverwrite = ConfirmOverwrite,
                UseCustomOutputDirectory = UseCustomOutputDirectory,
                CustomOutputDirectory = CustomOutputDirectory,
                Presets = Presets.Select(p => new ConversionPreset
                {
                    Name = p.Name,
                    Quality = p.Quality,
                    OutputFormat = p.OutputFormat,
                    ConflictResolution = p.ConflictResolution,
                    UseSubfolder = p.UseSubfolder,
                    SubfolderName = p.SubfolderName,
                    UseCustomOutputDirectory = p.UseCustomOutputDirectory,
                    CustomOutputDirectory = p.CustomOutputDirectory,
                    ConfirmOverwrite = p.ConfirmOverwrite,
                    SkipMetadata = p.SkipMetadata,
                    CjxlEffort = p.CjxlEffort,
                    CjxlThreads = p.CjxlThreads
                }).ToList(),
                SkipMetadata = SkipMetadata,
                CjxlEffort = CjxlEffort,
                CjxlThreads = CjxlThreads
            };
        }
    }

    public static class SettingsService
    {
        internal static string SettingsDirectory { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RAWtoJXL");
        internal static string SettingsPath => Path.Combine(SettingsDirectory, "settings.json");
        private const int MaxRecentFiles = 50;
        private const int PersistDebounceMs = 500;
        private static readonly object Gate = new();
        private static AppSettings? _current;
        private static string? _currentDirectory;
        private static readonly System.Timers.Timer SaveTimer = new(PersistDebounceMs) { AutoReset = false };

        public static event EventHandler<Exception>? ErrorOccurred;

        static SettingsService()
        {
            SaveTimer.Elapsed += (_, _) => Flush();
        }

        internal static void Reset()
        {
            lock (Gate)
            {
                SaveTimer.Stop();
                _current = null;
                _currentDirectory = null;
            }
        }

        public static AppSettings Load()
        {
            lock (Gate)
            {
                if (_current != null && _currentDirectory == SettingsDirectory)
                {
                    return _current;
                }

                _currentDirectory = SettingsDirectory;
                try
                {
                    if (!File.Exists(SettingsPath))
                    {
                        _current = new AppSettings();
                        return _current;
                    }

                    var json = File.ReadAllText(SettingsPath);
                    _current = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
                catch (Exception ex)
                {
                    _current = new AppSettings();
                    OnError(ex);
                }

                return _current;
            }
        }

        public static void Save()
        {
            Load();
            lock (Gate)
            {
                SaveTimer.Stop();
                SaveTimer.Start();
            }
        }

        public static void Save(AppSettings settings)
        {
            lock (Gate)
            {
                _current = settings;
                _currentDirectory = SettingsDirectory;
                Persist(settings);
            }
        }

        public static void Flush()
        {
            lock (Gate)
            {
                if (_current != null && _currentDirectory == SettingsDirectory)
                {
                    Persist(_current);
                }
            }
        }

        public static void AddRecentFile(string filePath)
        {
            var settings = Load();
            lock (Gate)
            {
                settings.RecentFiles.RemoveAll(p => p == filePath);
                settings.RecentFiles.Insert(0, Path.GetFullPath(filePath));
                while (settings.RecentFiles.Count > MaxRecentFiles)
                {
                    settings.RecentFiles.RemoveAt(settings.RecentFiles.Count - 1);
                }
            }
            Save();
        }

        private static void Persist(AppSettings settings)
        {
            try
            {
                Directory.CreateDirectory(SettingsDirectory);
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                var tempPath = SettingsPath + ".tmp";
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, SettingsPath, overwrite: true);
            }
            catch (Exception ex)
            {
                OnError(ex);
            }
        }

        private static void OnError(Exception ex)
        {
            ErrorOccurred?.Invoke(null, ex);
        }
    }
}