using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ARWtoJXL.Avalonia.ViewModels;

namespace ARWtoJXL.Avalonia
{
    public partial class MainWindow : Window
    {
        private SettingsWindow? _settingsWindow;

        public MainWindow()
        {
            InitializeComponent();
            var listBox = this.FindControl<ListBox>("ImagesListBox");
            if (listBox != null)
            {
                listBox.SelectionMode = SelectionMode.Multiple;
            }
        }

        public void OpenSettings()
        {
            if (_settingsWindow == null || !_settingsWindow.IsVisible)
            {
                var viewModel = DataContext as MainViewModel;
                if (viewModel == null) return;

                _settingsWindow = new SettingsWindow();

                _settingsWindow.Settings.UseSubfolder = viewModel.UseSubfolder;
                _settingsWindow.Settings.SubfolderName = viewModel.SubfolderName;
                _settingsWindow.Settings.QualityPreset = viewModel.QualityPreset;
                _settingsWindow.Settings.SearchRecursive = viewModel.SearchRecursive;
                _settingsWindow.Settings.OutputFormat = viewModel.OutputFormat;
                _settingsWindow.Settings.ConflictResolution = viewModel.ConflictResolution;
                _settingsWindow.Settings.ConfirmOverwrite = viewModel.ConfirmOverwrite;
                _settingsWindow.Settings.UseCustomOutputDirectory = viewModel.UseCustomOutputDirectory;
                _settingsWindow.Settings.CustomOutputDirectory = viewModel.CustomOutputDirectory;
                _settingsWindow.Settings.SkipMetadata = viewModel.SkipMetadata;
                _settingsWindow.Settings.CjxlEffort = viewModel.CjxlEffort;

                _settingsWindow.Closed += (s, args) =>
                {
                    viewModel.ApplySettings(_settingsWindow!.Settings);
                    _settingsWindow = null;
                };

                _settingsWindow.ShowDialog(this);
            }
            else
            {
                _settingsWindow.Activate();
            }
        }
    }
}
