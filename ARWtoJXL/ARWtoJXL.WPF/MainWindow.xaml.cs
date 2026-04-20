using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Collections.Generic;
using Wpf.Ui.Controls;
using ARWtoJXL.WPF.ViewModels;
using ARWtoJXL.Core.Interfaces;
using ARWtoJXL.Core.Services;
using ARWtoJXL.WPF.Models;

namespace ARWtoJXL.WPF
{
    public partial class MainWindow : Window
    {
        private SettingsWindow? _settingsWindow;

        public MainWindow()
        {
            InitializeComponent();
            var exiftoolService = new ExiftoolService();
            var magickService = new MagickService(exiftoolService);
            var pathResolver = new PathResolverService();
            var cjxlEncoder = new CjxlEncoderService(pathResolver, exiftoolService);
            var fileService = new FileService();
            var sizeEstimator = new SizeEstimatorService();
            var imageService = new ImageProcessingService(magickService, cjxlEncoder, fileService, pathResolver, sizeEstimator);
            var viewModel = new MainViewModel(imageService);

            var saved = SettingsService.Load();
            viewModel.UseSubfolder = saved.UseSubfolder;
            viewModel.SubfolderName = saved.SubfolderName;
            viewModel.QualityPreset = saved.QualityPreset;

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
                            allFiles.AddRange(System.IO.Directory.GetFiles(path, "*.*", System.IO.SearchOption.TopDirectoryOnly));
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
    }
}
