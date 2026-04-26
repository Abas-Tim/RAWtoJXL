using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ARWtoJXL.Core.Interfaces;
using ARWtoJXL.Avalonia;
using ARWtoJXL.Avalonia.Services;

namespace ARWtoJXL.Avalonia.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        public event EventHandler? RequestClose;

        private readonly IFilePickerService _filePickerService;

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
        private int _cjxlEffort = -1;

        [ObservableProperty]
        private string _cjxlRawDistance = string.Empty;

        [ObservableProperty]
        private string? _cjxlRawDistanceValidationResult;

        partial void OnCjxlRawDistanceChanged(string value)
        {
            CjxlRawDistanceValidationResult = ValidateRawDistance(value);
        }

        partial void OnCjxlEffortChanged(int value)
        {
            if (value < -1 || value > 9)
            {
                CjxlEffort = -1;
            }
        }

        public SettingsViewModel(IFilePickerService filePickerService)
        {
            _filePickerService = filePickerService;
            var saved = SettingsService.Load();
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
            CjxlRawDistance = saved.CjxlRawDistance;
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

        internal static string? ValidateRawDistance(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            if (!float.TryParse(value, out float distance))
                return "Must be a valid number.";
            if (distance < 0.0 || distance > 150.0)
                return "Must be between 0.0 and 150.0.";
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
                CjxlRawDistance = CjxlRawDistance
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
            CjxlRawDistance = SelectedPreset.CjxlRawDistance;
        }

        [RelayCommand]
        private void Save()
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
                SkipMetadata = SkipMetadata,
                CjxlEffort = CjxlEffort,
                CjxlRawDistance = CjxlRawDistance
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
