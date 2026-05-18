using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ARWtoJXL.Core.Interfaces;
using ARWtoJXL.Avalonia;
using ARWtoJXL.Avalonia.Services;

namespace ARWtoJXL.Avalonia.ViewModels
{
    public partial class SettingsViewModel : ObservableObject, IDisposable
    {
        public event EventHandler? RequestClose;

        private readonly IFilePickerService _filePickerService;
        private readonly System.Timers.Timer _persistTimer;
        private readonly List<string> _recentFiles;

        private static readonly HashSet<string> _noPersistProperties = new()
        {
            nameof(IsSaving),
            nameof(HasSelectedPreset)
        };

        public SettingsViewModel(IFilePickerService filePickerService)
        {
            _filePickerService = filePickerService;
            _persistTimer = new System.Timers.Timer(500) { AutoReset = false };
            _persistTimer.Elapsed += (_, _) => Persist();
            var saved = SettingsService.Load();
            _recentFiles = saved.RecentFiles;
            UseSubfolder = saved.UseSubfolder;
            SubfolderName = saved.SubfolderName;
            QualityPreset = saved.QualityPreset;
            SearchRecursive = saved.SearchRecursive;
            OutputFormat = saved.OutputFormat;
            ConflictResolution = saved.ConflictResolution;
            ConfirmOverwrite = saved.ConfirmOverwrite;
            UseCustomOutputDirectory = saved.UseCustomOutputDirectory;
            CustomOutputDirectory = saved.CustomOutputDirectory;
            Presets = new ObservableCollection<ConversionPreset>(saved.Presets);
            SkipMetadata = saved.SkipMetadata;
            CjxlEffort = saved.CjxlEffort;
            CjxlThreads = saved.CjxlThreads;

            // Initialize selected options after loading to handle the case where
            // the saved value equals the field default (which wouldn't trigger OnChanged)
            SelectedEffortOption = CjxlEffortOptions.FirstOrDefault(e => e.Value == CjxlEffort);
            SelectedThreadsOption = CjxlThreadsOptions.FirstOrDefault(e => e.Value == CjxlThreads);
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if (!string.IsNullOrEmpty(e.PropertyName) && !_noPersistProperties.Contains(e.PropertyName))
            {
                _persistTimer.Stop();
                _persistTimer.Start();
            }
        }

         public void Persist()
        {
            SettingsService.Save(new AppSettings
            {
                UseSubfolder = UseSubfolder,
                SubfolderName = SubfolderName,
                QualityPreset = QualityPreset,
                SearchRecursive = SearchRecursive,
                OutputFormat = OutputFormat,
                ConflictResolution = ConflictResolution,
                ConfirmOverwrite = ConfirmOverwrite,
                UseCustomOutputDirectory = UseCustomOutputDirectory,
                CustomOutputDirectory = CustomOutputDirectory,
                Presets = Presets.ToList(),
                RecentFiles = _recentFiles,
                SkipMetadata = SkipMetadata,
                CjxlEffort = CjxlEffort,
                CjxlThreads = CjxlThreads,
            });
        }

        public void Dispose()
        {
            _persistTimer.Stop();
            Persist();
            _persistTimer.Dispose();
        }

        [ObservableProperty]
        private bool _useSubfolder;

        [ObservableProperty]
        private string _subfolderName = string.Empty;

        [ObservableProperty]
        private int _qualityPreset;

        [ObservableProperty]
        private bool _searchRecursive;

        [ObservableProperty]
        private OutputFormat _outputFormat = OutputFormat.Jxl;

        [ObservableProperty]
        private ConflictResolution _conflictResolution = ConflictResolution.Overwrite;

        [ObservableProperty]
        private bool _confirmOverwrite = true;

        [ObservableProperty]
        private bool _useCustomOutputDirectory;

        [ObservableProperty]
        private string _customOutputDirectory = string.Empty;

        [ObservableProperty]
        private bool _isSaving;

        public Array OutputFormatOptions => Enum.GetValues(typeof(OutputFormat));
        public Array ConflictResolutionOptions => Enum.GetValues(typeof(ConflictResolution));

        public class EffortOption
        {
            public string Display { get; }
            public int Value { get; }
            public EffortOption(string display, int value) { Display = display; Value = value; }
        }

        public class ThreadOption
        {
            public string Display { get; }
            public int Value { get; }
            public ThreadOption(string display, int value) { Display = display; Value = value; }

            public override bool Equals(object? obj)
            {
                return obj is ThreadOption other && Value == other.Value;
            }

            public override int GetHashCode()
            {
                return Value.GetHashCode();
            }
        }

       public static readonly EffortOption[] DefaultCjxlEffortOptions = new[]
        {
            new EffortOption("1", 1),
            new EffortOption("2", 2),
            new EffortOption("3", 3),
            new EffortOption("4", 4),
            new EffortOption("5", 5),
            new EffortOption("6", 6),
            new EffortOption("7", 7),
            new EffortOption("8", 8),
            new EffortOption("9", 9),
        };

        public EffortOption[] CjxlEffortOptions => DefaultCjxlEffortOptions;

        [ObservableProperty]
        private string? _subfolderNameValidationResult;

        [ObservableProperty]
        private ObservableCollection<ConversionPreset> _presets = new();

        [ObservableProperty]
        private ConversionPreset? _selectedPreset;

        partial void OnSelectedPresetChanged(ConversionPreset? value)
        {
            HasSelectedPreset = value != null;
        }

        [ObservableProperty]
        private bool _hasSelectedPreset;

        [ObservableProperty]
        private string _newPresetName = string.Empty;

        [ObservableProperty]
        private bool _skipMetadata;

        [ObservableProperty]
        private int _cjxlEffort = 7;

        [ObservableProperty]
        private EffortOption? _selectedEffortOption;

        private bool _syncingEffort;

        partial void OnCjxlEffortChanged(int value)
        {
            if (value < 1 || value > 9)
            {
                CjxlEffort = 7;
            }
            if (_syncingEffort) return;
            _syncingEffort = true;
            var match = CjxlEffortOptions.FirstOrDefault(e => e.Value == CjxlEffort);
            SelectedEffortOption = match;
            _syncingEffort = false;
        }

        [ObservableProperty]
        private int _cjxlThreads = -1;

        [ObservableProperty]
        private ThreadOption? _selectedThreadsOption;

        private bool _syncingThreads;
        private ThreadOption[]? _cachedThreadsOptions;

        public ThreadOption[] CjxlThreadsOptions
        {
            get
            {
                if (_cachedThreadsOptions == null)
                {
                    int maxThreads = Environment.ProcessorCount;
                    _cachedThreadsOptions = new ThreadOption[maxThreads + 1];
                    _cachedThreadsOptions[0] = new ThreadOption("Auto", -1);
                    for (int i = 1; i <= maxThreads; i++)
                    {
                        _cachedThreadsOptions[i] = new ThreadOption(i.ToString(), i);
                    }
                }
                return _cachedThreadsOptions;
            }
        }

        partial void OnSelectedThreadsOptionChanged(ThreadOption? value)
        {
            if (_syncingThreads || value == null) return;
            _syncingThreads = true;
            CjxlThreads = value.Value;
            _syncingThreads = false;
        }

        partial void OnCjxlThreadsChanged(int value)
        {
            if (value < -1 || value > Environment.ProcessorCount)
            {
                CjxlThreads = -1;
            }
            if (_syncingThreads) return;
            _syncingThreads = true;
            var match = CjxlThreadsOptions.FirstOrDefault(e => e.Value == CjxlThreads);
            SelectedThreadsOption = match;
            _syncingThreads = false;
        }

        partial void OnSelectedEffortOptionChanged(EffortOption? value)
        {
            if (_syncingEffort || value == null) return;
            _syncingEffort = true;
            CjxlEffort = value.Value;
            _syncingEffort = false;
        }

        partial void OnSubfolderNameChanged(string value)
        {
            SubfolderNameValidationResult = ValidateSubfolderName(value);
        }

        internal static string? ValidateSubfolderName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            foreach (char c in Path.GetInvalidPathChars())
            {
                if (value.Contains(c))
                    return $"Invalid character: '{c}'";
            }

            if (value.Trim() != value)
                return "Leading or trailing whitespace is not allowed.";

            if (value.Length > 255)
                return "Folder name must be 255 characters or fewer.";

            if (value.Equals(".", StringComparison.Ordinal) || value.Equals("..", StringComparison.Ordinal))
                return "Invalid folder name.";

            var reservedNames = new[]
            {
                "CON", "PRN", "AUX", "NUL",
                "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
                "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
            };

            if (reservedNames.Contains(value.ToUpperInvariant()))
                return $"Folder name '{value}' is reserved.";

            return null;
        }

        [RelayCommand]
        private async Task BrowseOutputDirectory()
        {
            var folder = await _filePickerService.PickFolderAsync(CustomOutputDirectory);
            if (!string.IsNullOrEmpty(folder))
            {
                CustomOutputDirectory = folder;
            }
        }

        [RelayCommand]
        private void AddPreset()
        {
            if (string.IsNullOrWhiteSpace(NewPresetName))
                return;

            var preset = new ConversionPreset
            {
                Name = NewPresetName.Trim(),
                Quality = QualityPreset,
                OutputFormat = OutputFormat,
                ConflictResolution = ConflictResolution,
                UseSubfolder = UseSubfolder,
                SubfolderName = SubfolderName,
                UseCustomOutputDirectory = UseCustomOutputDirectory,
                CustomOutputDirectory = CustomOutputDirectory,
                ConfirmOverwrite = ConfirmOverwrite,
                SkipMetadata = SkipMetadata,
                CjxlEffort = CjxlEffort,
                CjxlThreads = CjxlThreads,
            };

            if (Presets.Any(p => p.Name.Equals(preset.Name, StringComparison.OrdinalIgnoreCase)))
            {
                var existing = Presets.First(p => p.Name.Equals(preset.Name, StringComparison.OrdinalIgnoreCase));
                var idx = Presets.IndexOf(existing);
                Presets[idx] = preset;
            }
            else
            {
                Presets.Add(preset);
            }

            SelectedPreset = preset;
            NewPresetName = string.Empty;
        }

        [RelayCommand]
        private void RemovePreset()
        {
            if (SelectedPreset == null)
                return;
            Presets.Remove(SelectedPreset);
            SelectedPreset = null;
        }

        [RelayCommand]
        private void LoadPreset()
        {
            if (SelectedPreset == null)
                return;

            QualityPreset = SelectedPreset.Quality;
            OutputFormat = SelectedPreset.OutputFormat;
            ConflictResolution = SelectedPreset.ConflictResolution;
            UseSubfolder = SelectedPreset.UseSubfolder;
            SubfolderName = SelectedPreset.SubfolderName;
            UseCustomOutputDirectory = SelectedPreset.UseCustomOutputDirectory;
            CustomOutputDirectory = SelectedPreset.CustomOutputDirectory;
            ConfirmOverwrite = SelectedPreset.ConfirmOverwrite;
            SkipMetadata = SelectedPreset.SkipMetadata;
            CjxlEffort = SelectedPreset.CjxlEffort;
            CjxlThreads = SelectedPreset.CjxlThreads;

            // Sync selected options in case values match current ones (which wouldn't trigger OnChanged)
            SelectedEffortOption = CjxlEffortOptions.FirstOrDefault(e => e.Value == CjxlEffort);
            SelectedThreadsOption = CjxlThreadsOptions.FirstOrDefault(e => e.Value == CjxlThreads);
        }

        [RelayCommand]
        private void Save()
        {
            var saved = SettingsService.Load();
            SettingsService.Save(new AppSettings
            {
                UseSubfolder = UseSubfolder,
                SubfolderName = SubfolderName,
                QualityPreset = QualityPreset,
                SearchRecursive = SearchRecursive,
                OutputFormat = OutputFormat,
                ConflictResolution = ConflictResolution,
                ConfirmOverwrite = ConfirmOverwrite,
                UseCustomOutputDirectory = UseCustomOutputDirectory,
                CustomOutputDirectory = CustomOutputDirectory,
                Presets = Presets.ToList(),
                RecentFiles = saved.RecentFiles,
                SkipMetadata = SkipMetadata,
                CjxlEffort = CjxlEffort,
                CjxlThreads = CjxlThreads,
            });
            IsSaving = false;
            RequestClose?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        private void Cancel()
        {
            IsSaving = false;
            RequestClose?.Invoke(this, EventArgs.Empty);
        }
    }
}
