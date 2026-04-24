using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ARWtoJXL.WPF.ViewModels;

namespace ARWtoJXL.WPF.Behaviors
{
    public static class DragDropBehavior
    {
        public static readonly DependencyProperty EnableDragDropProperty =
            DependencyProperty.RegisterAttached(
                "EnableDragDrop",
                typeof(bool),
                typeof(DragDropBehavior),
                new PropertyMetadata(false, OnEnableDragDropChanged));

        public static void SetEnableDragDrop(DependencyObject element, bool value)
            => element.SetValue(EnableDragDropProperty, value);

        public static bool GetEnableDragDrop(DependencyObject element)
            => (bool)element.GetValue(EnableDragDropProperty);

        private static void OnEnableDragDropChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is UIElement uiElement)
            {
                if ((bool)e.NewValue)
                {
                    uiElement.AllowDrop = true;
                    uiElement.Drop += OnDrop;
                    uiElement.DragOver += OnDragOver;
                }
                else
                {
                    uiElement.Drop -= OnDrop;
                    uiElement.DragOver -= OnDragOver;
                }
            }
        }

        private static async void OnDrop(object sender, DragEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is MainViewModel viewModel)
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    var files = e.Data.GetData(DataFormats.FileDrop) as string[];
                    if (files != null)
                    {
                        var allFiles = new List<string>();
                        foreach (var path in files)
                        {
                            if (Directory.Exists(path))
                            {
                                var option = viewModel.SearchRecursive
                                    ? SearchOption.AllDirectories
                                    : SearchOption.TopDirectoryOnly;
                                allFiles.AddRange(Directory.GetFiles(path, "*.*", option));
                            }
                            else
                            {
                                allFiles.Add(path);
                            }
                        }
                        await viewModel.AddFilesAsync(allFiles);
                    }
                }
            }
            e.Handled = true;
        }

        private static void OnDragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
    }
}
