using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Ui.Controls;
using ARWtoJXL.WPF.ViewModels;
using ARWtoJXL.Core.Interfaces;
using ARWtoJXL.Core;
namespace ARWtoJXL.WPF
{
    public partial class MainWindow : Window
    {
        private SettingsWindow? _settingsWindow;
        private readonly IImageService _imageService;

        public MainWindow()
        {
            InitializeComponent();

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddCoreServices();
            var serviceProvider = serviceCollection.BuildServiceProvider();

            _imageService = serviceProvider.GetRequiredService<IImageService>();
            var viewModel = new MainViewModel(_imageService);

            var saved = SettingsService.Load();
            viewModel.UseSubfolder = saved.UseSubfolder;
            viewModel.SubfolderName = saved.SubfolderName;
            viewModel.QualityPreset = saved.QualityPreset;
            viewModel.SearchRecursive = saved.SearchRecursive;
            viewModel.OutputFormat = saved.OutputFormat;
            viewModel.ConflictResolution = saved.ConflictResolution;
                viewModel.ConfirmOverwrite = saved.ConfirmOverwrite;

            DataContext = viewModel;
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files != null)
                {
                    var viewModel = (MainViewModel)DataContext!;

                    var allFiles = new List<string>();
                    foreach (var path in files)
                    {
                        if (System.IO.Directory.Exists(path))
                        {
                            var option = viewModel.SearchRecursive
                                ? System.IO.SearchOption.AllDirectories
                                : System.IO.SearchOption.TopDirectoryOnly;
                            allFiles.AddRange(System.IO.Directory.GetFiles(path, "*.*", option));
                        }
                        else
                        {
                            allFiles.Add(path);
                        }
                    }

                    _ = viewModel.AddFilesAsync(allFiles);
                }
            }
            e.Handled = true;
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_settingsWindow == null || !_settingsWindow.IsVisible)
            {
                _settingsWindow = new SettingsWindow
                {
                    Owner = this
                };

                var viewModel = (MainViewModel)DataContext!;
                _settingsWindow.Settings.UseSubfolder = viewModel.UseSubfolder;
                _settingsWindow.Settings.SubfolderName = viewModel.SubfolderName;
                _settingsWindow.Settings.QualityPreset = viewModel.QualityPreset;
                _settingsWindow.Settings.SearchRecursive = viewModel.SearchRecursive;
                _settingsWindow.Settings.OutputFormat = viewModel.OutputFormat;
                _settingsWindow.Settings.ConflictResolution = viewModel.ConflictResolution;
                _settingsWindow.Settings.ConfirmOverwrite = viewModel.ConfirmOverwrite;
                _settingsWindow.Closed += (s, args) =>
                {
                    viewModel.ApplySettings(_settingsWindow!.Settings);
                    _settingsWindow = null;
                };

                _settingsWindow.ShowDialog();
            }
            else
            {
                _settingsWindow.Activate();
            }
        }

        private async void RecentFile_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBlock tb && !string.IsNullOrEmpty(tb.Text))
            {
                var viewModel = (MainViewModel)DataContext!;
                await viewModel.AddFilesAsync(new[] { tb.Text });
            }
        }

        private void QualityTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !IsNumeric(e.Text);
        }

        private static bool IsNumeric(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            return int.TryParse(value, out _);
        }
    }
}
