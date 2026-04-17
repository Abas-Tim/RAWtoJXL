using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Collections.Generic;
using ARWtoJXL.WPF.ViewModels;
using ARWtoJXL.Core.Services;
using ARWtoJXL.WPF.Models;

namespace ARWtoJXL.WPF
{
    public partial class MainWindow : Window
    {
       public MainWindow()
        {
            InitializeComponent();
            var magickService = new MagickService();
            var pathResolver = new PathResolverService();
            var cjxlEncoder = new CjxlEncoderService(pathResolver);
            var fileService = new FileService();
            var imageService = new ImageProcessingService(magickService, cjxlEncoder, fileService, pathResolver);
            DataContext = new MainViewModel(imageService);
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
    }
}
