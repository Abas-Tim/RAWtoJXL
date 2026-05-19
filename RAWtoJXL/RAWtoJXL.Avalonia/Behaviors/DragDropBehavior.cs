using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using RAWtoJXL.Avalonia.ViewModels;

namespace RAWtoJXL.Avalonia.Behaviors
{
    public static class DragDropBehavior
    {
        public static readonly AvaloniaProperty<bool> EnableDragDropProperty =
            AvaloniaProperty.RegisterAttached<Control, bool>("EnableDragDrop", typeof(DragDropBehavior));

        public static void SetEnableDragDrop(Control control, bool value)
            => control.SetValue(EnableDragDropProperty, value);

        public static bool GetEnableDragDrop(Control control)
            => control.GetValue(EnableDragDropProperty) is true;

        static DragDropBehavior()
        {
            EnableDragDropProperty.Changed.AddClassHandler<Control>(OnEnableDragDropChanged);
        }

        private static async void OnEnableDragDropChanged(Control control, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.NewValue is bool enabled && enabled)
            {
                DragDrop.SetAllowDrop(control, true);
                DragDrop.AddDragEnterHandler(control, OnDragEnter);
                DragDrop.AddDragOverHandler(control, OnDragOver);
                DragDrop.AddDropHandler(control, OnDrop);
            }
        }

        private static async void OnDrop(object? sender, DragEventArgs e)
        {
            if (sender is Control ctrl)
            {
                var viewModel = FindAncestorViewModel(ctrl);
                if (viewModel == null)
                {
                    e.Handled = true;
                    return;
                }

                var allFiles = new List<string>();

                // Try structured file data first
                if (e.DataTransfer.Formats.Contains(DataFormat.File))
                {
                    var files = e.DataTransfer.TryGetFiles();
                    if (files != null && files.Length > 0)
                    {
                        foreach (var file in files)
                        {
                            var path = file.Path.LocalPath;
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
                    }
                }

                // Fallback: try plain text (Windows Explorer sometimes provides paths as text)
                if (allFiles.Count == 0 && e.DataTransfer.Formats.Contains(DataFormat.Text))
                {
                    var text = e.DataTransfer.TryGetText();
                    if (!string.IsNullOrEmpty(text))
                    {
                        foreach (var line in text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
                        {
                            var path = line.Trim();
                            if (string.IsNullOrEmpty(path)) continue;

                            if (Directory.Exists(path))
                            {
                                var option = viewModel.SearchRecursive
                                    ? SearchOption.AllDirectories
                                    : SearchOption.TopDirectoryOnly;
                                allFiles.AddRange(Directory.GetFiles(path, "*.*", option));
                            }
                            else if (File.Exists(path))
                            {
                                allFiles.Add(path);
                            }
                        }
                    }
                }

                if (allFiles.Count > 0)
                {
                    await viewModel.AddFilesAsync(allFiles);
                }
            }
            e.Handled = true;
        }

        private static void OnDragOver(object? sender, DragEventArgs e)
        {
            if (HasFileData(e.DataTransfer))
            {
                e.DragEffects = DragDropEffects.Copy;
            }
            else
            {
                e.DragEffects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private static void OnDragEnter(object? sender, DragEventArgs e)
        {
            if (HasFileData(e.DataTransfer))
            {
                e.DragEffects = DragDropEffects.Copy;
            }
            else
            {
                e.DragEffects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private static bool HasFileData(IDataTransfer dataTransfer)
        {
            return dataTransfer.TryGetFiles() is { Length: > 0 }
                || !string.IsNullOrEmpty(dataTransfer.TryGetText());
        }

        private static MainViewModel? FindAncestorViewModel(Control control)
        {
            var current = control;
            while (current != null)
            {
                if (current.DataContext is MainViewModel vm)
                    return vm;
                current = current.Parent as Control;
            }
            return null;
        }
    }
}
