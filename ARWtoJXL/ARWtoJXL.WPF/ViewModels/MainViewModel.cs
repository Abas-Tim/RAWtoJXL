using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Media.Imaging;
using ARWtoJXL.Core.Interfaces;
using ARWtoJXL.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ARWtoJXL.WPF.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly IImageService _imageService;
        private readonly ObservableCollection<ImageItemViewModel> _selectedImages = new();
        private readonly HashSet<string> _addedFilePaths = new(StringComparer.OrdinalIgnoreCase);
        private CancellationTokenSource? _cancellationTokenSource;

        [ObservableProperty]
        private ObservableCollection<ImageItemViewModel> _images = new();

        [ObservableProperty]
        private bool _isCancelRequested;

        partial void OnIsCancelRequestedChanged(bool value)
        {
            CancelCommand.NotifyCanExecuteChanged();
        }

        [ObservableProperty]
        private string _statusMessage = AppStrings.Ready;

        [ObservableProperty]
        private bool _isConverting;

        partial void OnIsConvertingChanged(bool value)
        {
            ConvertSelectedCommand.NotifyCanExecuteChanged();
            RemoveSelectedCommand.NotifyCanExecuteChanged();
            SelectAllCommand.NotifyCanExecuteChanged();
            OpenOutputFolderCommand.NotifyCanExecuteChanged();
        }

        [ObservableProperty]
        private string _outputPath = string.Empty;

        [ObservableProperty]
        private string _subfolderName = AppStrings.SubfolderNameDefault;

        partial void OnSubfolderNameChanged(string value)
        {
            SaveSettings();
        }

        [ObservableProperty]
        private bool _isAllSelected;

        [ObservableProperty]
        private string _outputDirectory = string.Empty;

        partial void OnOutputDirectoryChanged(string value)
        {
            OpenOutputFolderCommand.NotifyCanExecuteChanged();
        }

        [ObservableProperty]
        private bool _useSubfolder = true;

        partial void OnUseSubfolderChanged(bool value)
        {
            SaveSettings();
        }

        [ObservableProperty]
        private int _qualityPreset = 90;

        partial void OnQualityPresetChanged(int value)
        {
            SaveSettings();
        }

        public void ApplySettings(SettingsViewModel settings)
        {
            UseSubfolder = settings.UseSubfolder;
            SubfolderName = settings.SubfolderName;
            QualityPreset = settings.QualityPreset;
        }

        [ObservableProperty]
        private bool _isAnySelected;

        [ObservableProperty]
        private int _completedCount;

        [ObservableProperty]
        private int _totalCount = 0;

        public MainViewModel(IImageService imageService)
        {
            _imageService = imageService;
        }

        [RelayCommand(CanExecute = nameof(CanExecuteConvertSelected))]
        private async Task ConvertSelectedAsync()
        {
            var readySelected = _selectedImages.Where(i => i.Status == ImageStatus.Ready || i.Status == ImageStatus.Converted || i.Status == ImageStatus.Failed).ToList();
            if (!readySelected.Any()) return;

            _cancellationTokenSource = new CancellationTokenSource();
            _completedCountField = 0;
            CompletedCount = 0;
            TotalCount = readySelected.Count;
            StatusMessage = $"{AppStrings.ConvertingProgress}{0}{AppStrings.OfSuffix}{readySelected.Count}...";
            IsConverting = true;
            IsCancelRequested = true;
            RefreshAllCommands();

            int maxConcurrency = Environment.ProcessorCount;
            using var semaphore = new SemaphoreSlim(maxConcurrency);
            var tasks = readySelected.Select(async item =>
            {
                await semaphore.WaitAsync(_cancellationTokenSource.Token);
                try
                {
                    if (_cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        return;
                    }

                    item.Status = ImageStatus.Converting;

                    string outputPath = GetOutputPath(item.FilePath);

                    try
                    {
                        await _imageService.ConvertArwToJxlAsync(
                            item.FilePath,
                            outputPath,
                            _ => { },
                            QualityPreset,
                            _cancellationTokenSource.Token);

                        await OnUiAsync(() => item.Status = ImageStatus.Converted);
                    }
                    catch (OperationCanceledException)
                    {
                        await OnUiAsync(() =>
                        {
                            item.Status = ImageStatus.Pending;
                            item.ErrorMessage = AppStrings.Cancelled;
                        });
                    }
                    catch (Exception ex)
                    {
                        await OnUiAsync(() =>
                        {
                            item.Status = ImageStatus.Failed;
                            item.ErrorMessage = ex.Message;
                        });
                    }
                }
                finally
                {
                    semaphore.Release();
                    UpdateProgress(readySelected.Count);
                }
            });

            await Task.WhenAll(tasks);

            string lastOutputDir = string.Empty;
            if (readySelected.Any())
            {
                lastOutputDir = Path.GetDirectoryName(GetOutputPath(readySelected.First().FilePath))!;
            }

            await OnUiAsync(() =>
            {
                OutputDirectory = lastOutputDir;
            });

            IsConverting = false;
            IsCancelRequested = false;
            _cancellationTokenSource = null;
            StatusMessage = AppStrings.ConversionComplete;
            CompletedCount = 0;
            TotalCount = 0;
            RefreshAllCommands();
        }

        private bool CanExecuteConvertSelected() =>
            !IsConverting && _selectedImages.Any(i => i.Status == ImageStatus.Ready || i.Status == ImageStatus.Converted || i.Status == ImageStatus.Failed);

        [RelayCommand(CanExecute = nameof(CanExecuteRemoveSelected))]
        private void RemoveSelected()
        {
            var itemsToRemove = _selectedImages.ToList();
            foreach (var item in itemsToRemove)
            {
                item.PropertyChanged -= Item_PropertyChanged;
                Images.Remove(item);
            }
            _selectedImages.Clear();
            UpdateSelectionState();
            StatusMessage = $"{AppStrings.ItemsRemoved}{itemsToRemove.Count}{AppStrings.ItemsSuffix}";
            RefreshViewCommands();
        }

        private bool CanExecuteRemoveSelected() => !IsConverting && IsAnySelected;

        [RelayCommand(CanExecute = nameof(CanExecuteSelectAll))]
        private void SelectAll()
        {
            foreach (var item in Images)
            {
                item.IsSelected = !IsAllSelected;
            }
        }

        private bool CanExecuteSelectAll() => !IsConverting;

        [RelayCommand(CanExecute = nameof(CanExecuteCancel))]
        private void Cancel()
        {
            _cancellationTokenSource?.Cancel();
            StatusMessage = AppStrings.Cancelling;
        }

        private bool CanExecuteCancel() => IsCancelRequested;

        [RelayCommand(CanExecute = nameof(CanExecuteSelectAll))]
        private async Task OpenFile()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = AppStrings.OpenFileDialogFilter,
                Multiselect = true,
                Title = AppStrings.OpenFileDialogTitle
            };

            if (dialog.ShowDialog() == true)
            {
                await AddFilesAsync(dialog.FileNames);
            }
        }

        [RelayCommand(CanExecute = nameof(CanExecuteOpenOutputFolder))]
        private void OpenOutputFolder()
        {
            if (!string.IsNullOrEmpty(OutputDirectory) && Directory.Exists(OutputDirectory))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = OutputDirectory,
                        UseShellExecute = true
                    });
                }
                catch
                {
                    StatusMessage = AppStrings.FailedToOpenOutputFolder;
                }
            }
        }

        private bool CanExecuteOpenOutputFolder() =>
            !IsConverting && !string.IsNullOrEmpty(OutputDirectory) && Directory.Exists(OutputDirectory);

        private int _completedCountField;

        private void UpdateProgress(int total)
        {
            int completed = Interlocked.Increment(ref _completedCountField);
            CompletedCount = completed;
            StatusMessage = $"{AppStrings.ConvertingProgress}{completed}{AppStrings.OfSuffix}{total}...";
        }

        private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ImageItemViewModel.Status))
            {
                ConvertSelectedCommand.NotifyCanExecuteChanged();
            }
            else if (e.PropertyName == nameof(ImageItemViewModel.IsSelected))
            {
                if (sender is ImageItemViewModel item)
                {
                    if (item.IsSelected)
                    {
                        if (!_selectedImages.Contains(item))
                            _selectedImages.Add(item);
                    }
                    else
                    {
                        _selectedImages.Remove(item);
                    }

                    UpdateSelectionState();
                    RefreshViewCommands();
                }
            }
        }

        private void RefreshViewCommands()
        {
            ConvertSelectedCommand.NotifyCanExecuteChanged();
            RemoveSelectedCommand.NotifyCanExecuteChanged();
            SelectAllCommand.NotifyCanExecuteChanged();
        }

        private void RefreshAllCommands()
        {
            ConvertSelectedCommand.NotifyCanExecuteChanged();
            RemoveSelectedCommand.NotifyCanExecuteChanged();
            SelectAllCommand.NotifyCanExecuteChanged();
            CancelCommand.NotifyCanExecuteChanged();
            OpenOutputFolderCommand.NotifyCanExecuteChanged();
        }

        private void UpdateSelectionState()
        {
            bool allSelected = Images.Any() && Images.All(i => i.IsSelected);
            bool anySelected = _selectedImages.Any();

            if (IsAllSelected != allSelected)
                IsAllSelected = allSelected;
            if (IsAnySelected != anySelected)
                IsAnySelected = anySelected;
        }

        public async Task AddFilesAsync(IEnumerable<string> filePaths)
        {
            var normalizedPaths = filePaths.Select(p => Path.GetFullPath(p)).Distinct().ToList();
            var newPaths = normalizedPaths.Where(p => !_addedFilePaths.Contains(p)).ToList();
            int skipped = normalizedPaths.Count - newPaths.Count;

            foreach (var p in newPaths)
            {
                _addedFilePaths.Add(p);
            }

            var loadTasks = newPaths.Select(async path =>
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

                var extension = Path.GetExtension(path).ToLower();
                if (extension != ".arw" && extension != ".jxl") return;

                var item = new ImageItemViewModel
                {
                    FilePath = path,
                    FileName = Path.GetFileName(path),
                    Status = ImageStatus.Ready
                };

                try
                {
                    var thumbnailBytes = await _imageService.GetThumbnailAsync(path);
                    using var ms = new MemoryStream(thumbnailBytes);
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = ms;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    item.Thumbnail = bitmap;
                }
                catch (Exception ex)
                {
                    item.ErrorMessage = $"{AppStrings.ThumbnailFailedPrefix}{ex.Message}";
                }

                await OnUiAsync(() =>
                {
                    Images.Add(item);
                    item.PropertyChanged += Item_PropertyChanged;
                });
            });

            await Task.WhenAll(loadTasks);

            string msg = skipped > 0
                ? $"{AppStrings.FilesLoaded}{Images.Count}{AppStrings.ItemsSingular}{AppStrings.OfSuffix}{skipped}{AppStrings.DuplicatesSkipped}"
                : $"{AppStrings.FilesLoaded}{Images.Count}{AppStrings.ItemsSingular}";
            StatusMessage = msg;
            UpdateSelectionState();
            RefreshViewCommands();
        }

        private string GetOutputPath(string inputPath)
        {
            string directory = UseSubfolder ? Path.Combine(Path.GetDirectoryName(inputPath)!, SubfolderName) : Path.GetDirectoryName(inputPath)!;
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, Path.GetFileNameWithoutExtension(inputPath) + ".jxl");
        }

        private async Task OnUiAsync(Action action)
        {
            await App.Current.Dispatcher.InvokeAsync(action);
        }

        private void SaveSettings()
        {
            SettingsService.Save(new AppSettings
            {
                UseSubfolder = UseSubfolder,
                SubfolderName = SubfolderName,
                QualityPreset = QualityPreset
            });
        }
    }
}
