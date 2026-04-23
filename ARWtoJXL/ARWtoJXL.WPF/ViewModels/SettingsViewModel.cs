using System;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ARWtoJXL.Core.Interfaces;

namespace ARWtoJXL.WPF.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
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
        private bool _isSaving;

        [ObservableProperty]
        private string? _subfolderNameValidationResult;

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
        }

        partial void OnSubfolderNameChanged(string value)
        {
            SubfolderNameValidationResult = ValidateSubfolderName(value);
        }

        private static string? ValidateSubfolderName(string value)
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
                ConfirmOverwrite = ConfirmOverwrite
            });
            IsSaving = false;
        }

        [RelayCommand]
        private void Cancel()
        {
            IsSaving = false;
        }
    }
}
