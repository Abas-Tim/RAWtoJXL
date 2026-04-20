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
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Media.Imaging;
using ARWtoJXL.Core.Interfaces;
using ARWtoJXL.Core.Services;
using ARWtoJXL.WPF.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ARWtoJXL.WPF.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly IImageService _imageService;
        private readonly ObservableCollection<ImageItem> _selectedImages = new();
        private readonly HashSet<string> _addedFilePaths = new(StringComparer.OrdinalIgnoreCase);
        private CancellationTokenSource? _cancellationTokenSource;

        [ObservableProperty]
        private ObservableCollection<ImageItem> _images = new();

        private RelayCommand _convertSelectedCommand;
        private RelayCommand _removeSelectedCommand;
        private RelayCommand _selectAllCommand;
        private RelayCommand _cancelCommand;
        private RelayCommand _openFileCommand;
        private RelayCommand _openOutputFolderCommand;

        [ObservableProperty]
        private bool _isCancelRequested;

        [ObservableProperty]
        private string _statusMessage = AppStrings.Ready;

        [ObservableProperty]
        private bool _isConverting;

        [ObservableProperty]
        private string _outputPath = string.Empty;

        [ObservableProperty]
        private string _subfolderName = AppStrings.SubfolderNameDefault;

        [ObservableProperty]
        private bool _isAllSelected;

        [ObservableProperty]
        private string _outputDirectory = string.Empty;

        [ObservableProperty]
        private bool _useSubfolder = true;

        [ObservableProperty]
        private int _qualityPreset = 90;

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
            _convertSelectedCommand = new RelayCommand(async () => await ConvertSelectedAsync(), CanExecuteConvertSelected);
            _removeSelectedCommand = new RelayCommand(RemoveSelected, CanExecuteRemoveSelected);
            _selectAllCommand = new RelayCommand(ToggleSelectAll, CanExecuteSelectAll);
            _cancelCommand = new RelayCommand(CancelConversion, CanExecuteCancel);
            _openFileCommand = new RelayCommand(OpenFile, CanExecuteSelectAll);
            _openOutputFolderCommand = new RelayCommand(OpenOutputFolder, CanExecuteOpenOutputFolder);
            PropertyChanging += (s, e) =>
            {
                if (e.PropertyName == nameof(IsCancelRequested))
                {
                    _cancelCommand.NotifyCanExecuteChanged();
                }
                else if (e.PropertyName == nameof(IsConverting))
                {
                    _openOutputFolderCommand.NotifyCanExecuteChanged();
                }
            };
        }

        public ICommand ConvertSelectedCommand => _convertSelectedCommand;
        public ICommand RemoveSelectedCommand => _removeSelectedCommand;
        public ICommand SelectAllCommand => _selectAllCommand;
        public ICommand CancelCommand => _cancelCommand;
        public ICommand OpenFileCommand => _openFileCommand;
        public ICommand OpenOutputFolderCommand => _openOutputFolderCommand;

        private bool CanExecuteCancel() => IsCancelRequested;

        private bool CanExecuteConvertSelected() =>
            !IsConverting && _selectedImages.Any(i => i.Status == ImageStatus.Ready || i.Status == ImageStatus.Converted || i.Status == ImageStatus.Failed);

        private bool CanExecuteRemoveSelected() => !IsConverting && IsAnySelected;

        private bool CanExecuteSelectAll() => !IsConverting;

        private bool CanExecuteOpenOutputFolder() =>
            !IsConverting && !string.IsNullOrEmpty(OutputDirectory) && Directory.Exists(OutputDirectory);

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

        private void ToggleSelectAll()
        {
            foreach (var item in Images)
            {
                item.IsSelected = !IsAllSelected;
            }
        }

        private async void OpenFile()
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
                _openOutputFolderCommand.NotifyCanExecuteChanged();
            });

            IsConverting = false;
            IsCancelRequested = false;
            _cancellationTokenSource = null;
            StatusMessage = AppStrings.ConversionComplete;
            CompletedCount = 0;
            TotalCount = 0;
            RefreshAllCommands();
        }

        private void CancelConversion()
        {
            _cancellationTokenSource?.Cancel();
            StatusMessage = AppStrings.Cancelling;
        }

        private int _completedCountField;

        private void UpdateProgress(int total)
        {
            int completed = Interlocked.Increment(ref _completedCountField);
            CompletedCount = completed;
            StatusMessage = $"{AppStrings.ConvertingProgress}{completed}{AppStrings.OfSuffix}{total}...";
        }

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

        private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ImageItem.Status))
            {
                _convertSelectedCommand.NotifyCanExecuteChanged();
            }
            else if (e.PropertyName == nameof(ImageItem.IsSelected))
            {
                if (sender is ImageItem item)
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
            _convertSelectedCommand.NotifyCanExecuteChanged();
            _removeSelectedCommand.NotifyCanExecuteChanged();
            _selectAllCommand.NotifyCanExecuteChanged();
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

                var item = new ImageItem
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

                try
                {
                    if (extension == ".arw")
                    {
                        item.EstimatedSize = await _imageService.EstimateSizeAsync(path, QualityPreset);
                    }
                    else
                    {
                        var fileInfo = new FileInfo(path);
                        item.EstimatedSize = fileInfo.Length;
                    }
                }
                catch
                {
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

        private void RefreshAllCommands()
        {
            _convertSelectedCommand.NotifyCanExecuteChanged();
            _removeSelectedCommand.NotifyCanExecuteChanged();
            _selectAllCommand.NotifyCanExecuteChanged();
            _cancelCommand.NotifyCanExecuteChanged();
        }
    }
}
