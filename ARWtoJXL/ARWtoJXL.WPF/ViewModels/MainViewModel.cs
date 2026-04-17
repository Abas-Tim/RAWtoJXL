using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Media.Imaging;
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
            private CancellationTokenSource? _cancellationTokenSource;

            [ObservableProperty]
            private ObservableCollection<ImageItem> _images = new();

            private RelayCommand _convertSelectedCommand;
            private RelayCommand _removeSelectedCommand;
            private RelayCommand _selectAllCommand;
            private RelayCommand _cancelCommand;

            [ObservableProperty]
            private bool _isCancelRequested;

        [ObservableProperty]
        private string _statusMessage = "Ready";

        [ObservableProperty]
        private bool _isConverting;

        [ObservableProperty]
        private string _outputPath = string.Empty;

        [ObservableProperty]
        private string _subfolderName = "jxl_output";

        [ObservableProperty]
        private bool _isAllSelected;

        [ObservableProperty]
        private bool _useSubfolder = true;

        [ObservableProperty]
        private int _qualityPreset = 90;

        [ObservableProperty]
        private OutputFormat _outputFormat = OutputFormat.Jxl;

        public List<OutputFormat> OutputFormats { get; } = new() { OutputFormat.Jxl };

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
            PropertyChanging += (s, e) =>
            {
                if (e.PropertyName == nameof(IsCancelRequested))
                {
                    _cancelCommand.NotifyCanExecuteChanged();
                }
            };
        }

        public ICommand ConvertSelectedCommand => _convertSelectedCommand;
        public ICommand RemoveSelectedCommand => _removeSelectedCommand;
        public ICommand SelectAllCommand => _selectAllCommand;
        public ICommand CancelCommand => _cancelCommand;

        private bool CanExecuteCancel()
        {
            return IsCancelRequested;
        }

        private bool CanExecuteConvertSelected()
        {
            return !IsConverting && _selectedImages.Any(i => i.Status == ImageStatus.Ready || i.Status == ImageStatus.Converted || i.Status == ImageStatus.Failed);
        }

        private bool CanExecuteRemoveSelected()
        {
            return !IsConverting && IsAnySelected;
        }

        private bool CanExecuteSelectAll()
        {
            return !IsConverting;
        }

        private void ToggleSelectAll()
        {
            if (IsAllSelected)
            {
                foreach (var item in Images)
                {
                    item.IsSelected = false;
                }
            }
            else
            {
                foreach (var item in Images)
                {
                    item.IsSelected = true;
                }
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
            StatusMessage = $"Converting 0 of {readySelected.Count}...";
            IsConverting = true;
            IsCancelRequested = true;
            _convertSelectedCommand.NotifyCanExecuteChanged();
            _removeSelectedCommand.NotifyCanExecuteChanged();
            _selectAllCommand.NotifyCanExecuteChanged();
            _cancelCommand.NotifyCanExecuteChanged();

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
                             OutputFormat,
                             _cancellationTokenSource.Token);

                        await App.Current.Dispatcher.InvokeAsync(() =>
                        {
                            item.Status = ImageStatus.Converted;
                        });
                    }
                    catch (OperationCanceledException)
                    {
                        await App.Current.Dispatcher.InvokeAsync(() =>
                        {
                            item.Status = ImageStatus.Pending;
                            item.ErrorMessage = "Cancelled";
                        });
                    }
                    catch (Exception ex)
                    {
                        await App.Current.Dispatcher.InvokeAsync(() =>
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

            IsConverting = false;
            IsCancelRequested = false;
            _cancellationTokenSource = null;
            StatusMessage = "Conversion complete.";
            CompletedCount = 0;
            TotalCount = 0;
            _convertSelectedCommand.NotifyCanExecuteChanged();
            _removeSelectedCommand.NotifyCanExecuteChanged();
            _selectAllCommand.NotifyCanExecuteChanged();
            _cancelCommand.NotifyCanExecuteChanged();
        }

        private void CancelConversion()
        {
            _cancellationTokenSource?.Cancel();
            StatusMessage = "Cancelling...";
        }

        private int _completedCountField;

        private void UpdateProgress(int total)
        {
            int completed = Interlocked.Increment(ref _completedCountField);
            CompletedCount = completed;
            StatusMessage = $"Converting {completed} of {total}...";
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
            StatusMessage = $"Removed {itemsToRemove.Count} item(s).";
            _convertSelectedCommand.NotifyCanExecuteChanged();
            _removeSelectedCommand.NotifyCanExecuteChanged();
            _selectAllCommand.NotifyCanExecuteChanged();
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
                    _convertSelectedCommand.NotifyCanExecuteChanged();
                    _removeSelectedCommand.NotifyCanExecuteChanged();
                    _selectAllCommand.NotifyCanExecuteChanged();
                }
            }
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
            var loadTasks = filePaths.Select(async path =>
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
                    item.ErrorMessage = $"Thumbnail failed: {ex.Message}";
                }

                await App.Current.Dispatcher.InvokeAsync(() =>
                {
                    Images.Add(item);
                    item.PropertyChanged += Item_PropertyChanged;
                });
            });

            await Task.WhenAll(loadTasks);

            StatusMessage = $"Files loaded. {Images.Count} item(s).";
            UpdateSelectionState();
            _convertSelectedCommand.NotifyCanExecuteChanged();
            _removeSelectedCommand.NotifyCanExecuteChanged();
            _selectAllCommand.NotifyCanExecuteChanged();
        }

        private string GetOutputPath(string inputPath)
        {
            string directory = UseSubfolder ? Path.Combine(Path.GetDirectoryName(inputPath)!, SubfolderName) : Path.GetDirectoryName(inputPath)!;
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, Path.GetFileNameWithoutExtension(inputPath) + ".jxl");
        }
    }
}
