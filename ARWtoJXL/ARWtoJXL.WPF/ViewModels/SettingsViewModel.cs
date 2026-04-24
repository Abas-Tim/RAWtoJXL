using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ARWtoJXL.Core.Interfaces;
using ARWtoJXL.WPF;

namespace ARWtoJXL.WPF.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        public event EventHandler? RequestClose;

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

        public SettingsViewModel()
        {
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
        private void BrowseOutputDirectory()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                ValidateNames = false,
                FileName = " ",
                InitialDirectory = string.IsNullOrWhiteSpace(CustomOutputDirectory)
                    ? Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
                    : CustomOutputDirectory
            };
            if (dialog.ShowDialog() == true)
            {
                var path = Path.GetDirectoryName(dialog.FileName);
                if (!string.IsNullOrEmpty(path))
                {
                    CustomOutputDirectory = path;
                }
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
                ConfirmOverwrite = ConfirmOverwrite
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
                Presets = Presets.ToList()
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
