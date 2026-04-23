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
using ARWtoJXL.Core.Models;
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
        private readonly SemaphoreSlim _confirmationSemaphore = new(1, 1);
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

        [ObservableProperty]
        private bool _searchRecursive;

        [ObservableProperty]
        private OutputFormat _outputFormat = OutputFormat.Jxl;

        [ObservableProperty]
        private ConflictResolution _conflictResolution = ConflictResolution.Overwrite;

        [ObservableProperty]
        private bool _confirmOverwrite = true;

        partial void OnConfirmOverwriteChanged(bool value)
        {
            SaveSettings();
        }

        [ObservableProperty]
        private bool _useCustomOutputDirectory;

        partial void OnUseCustomOutputDirectoryChanged(bool value)
        {
            SaveSettings();
        }

        [ObservableProperty]
        private string _customOutputDirectory = string.Empty;

        partial void OnCustomOutputDirectoryChanged(string value)
        {
            SaveSettings();
        }

        [ObservableProperty]
        private ObservableCollection<string> _recentFiles = new();

        public void ApplySettings(SettingsViewModel settings)
        {
            UseSubfolder = settings.UseSubfolder;
            SubfolderName = settings.SubfolderName;
            QualityPreset = settings.QualityPreset;
            SearchRecursive = settings.SearchRecursive;
            OutputFormat = settings.OutputFormat;
            ConflictResolution = settings.ConflictResolution;
            ConfirmOverwrite = settings.ConfirmOverwrite;
            UseCustomOutputDirectory = settings.UseCustomOutputDirectory;
            CustomOutputDirectory = settings.CustomOutputDirectory;
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
            LoadRecentFiles();
        }

        private void LoadRecentFiles()
        {
            var saved = SettingsService.Load();
            RecentFiles = new ObservableCollection<string>(saved.RecentFiles);
            SearchRecursive = saved.SearchRecursive;
            OutputFormat = saved.OutputFormat;
            ConflictResolution = saved.ConflictResolution;
            ConfirmOverwrite = saved.ConfirmOverwrite;
            UseCustomOutputDirectory = saved.UseCustomOutputDirectory;
            CustomOutputDirectory = saved.CustomOutputDirectory;
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
            IsCancelRequested = false;
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

                    string outputPath = ResolveOutputPath(item.FilePath);

                 if (outputPath == null)
                        {
                            await OnUiAsync(() =>
                            {
                                item.Status = ImageStatus.Pending;
                                item.ErrorMessage = AppStrings.FileSkipped;
                            });
                            return;
                        }

                        if (File.Exists(outputPath) && ConfirmOverwrite)
                            {
                                await _confirmationSemaphore.WaitAsync(_cancellationTokenSource.Token);
                                MessageBoxResult result = MessageBoxResult.No;
                                try
                                {
                                    await OnUiAsync(() =>
                                    {
                                        result = MessageBox.Show(
                                            $"Overwrite existing file?\n\n{Path.GetFileName(outputPath)}",
                                            "Confirm Overwrite",
                                            MessageBoxButton.YesNo,
                                            MessageBoxImage.Question);
                                    });
                                }
                                finally
                                {
                                    _confirmationSemaphore.Release();
                                }
                                if (result != MessageBoxResult.Yes)
                                {
                                    await OnUiAsync(() =>
                                    {
                                        item.Status = ImageStatus.Pending;
                                        item.ErrorMessage = AppStrings.FileSkippedByUser;
                                    });
                                    return;
                                }
                            }

                        int quality = item.EffectiveQuality(QualityPreset);

                    try
                    {
                        long sourceSize = 0;
                        try { sourceSize = new FileInfo(item.FilePath).Length; } catch { }

                        await _imageService.ConvertArwToJxlAsync(
                            item.FilePath,
                            outputPath,
                            _ => { },
                            quality,
                            OutputFormat,
                            _cancellationTokenSource.Token);

                        long outputSize = 0;
                        try { outputSize = new FileInfo(outputPath).Length; } catch { }

                        await OnUiAsync(() =>
                        {
                            item.Status = ImageStatus.Converted;
                            item.SourceFileSize = sourceSize;
                            item.OutputFileSize = outputSize;
                            item.OutputPath = outputPath;
                            SettingsService.AddRecentFile(item.FilePath);
                            RefreshRecentFiles();
                        });
                    }
                  catch (OperationCanceledException)
                        {
                            await OnUiAsync(() =>
                            {
                                item.Status = ImageStatus.Pending;
                                item.ErrorMessage = AppStrings.Cancelled;
                            });
                        }
                        catch (FileLockedException ex)
                        {
                            await OnUiAsync(() =>
                            {
                                item.Status = ImageStatus.Failed;
                                item.ErrorMessage = $"{AppStrings.FileLockedPrefix}{ex.Message}";
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
                var resolved = ResolveOutputPath(readySelected.First().FilePath);
                if (!string.IsNullOrEmpty(resolved))
                {
                    lastOutputDir = Path.GetDirectoryName(resolved) ?? string.Empty;
                }
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
                item.IsRemoved = true;
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

        [RelayCommand]
        private async Task LoadRecentFilesCommand()
        {
            var existing = RecentFiles.ToList();
            if (existing.Count > 0)
            {
                await AddFilesAsync(existing);
            }
        }

        [RelayCommand]
        private void ClearRecentFiles()
        {
            var settings = SettingsService.Load();
            settings.RecentFiles.Clear();
            SettingsService.Save(settings);
            RecentFiles = new ObservableCollection<string>();
        }

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

        private void RefreshRecentFiles()
        {
            var saved = SettingsService.Load();
            RecentFiles = new ObservableCollection<string>(saved.RecentFiles);
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

            var validPaths = new List<string>();
            foreach (var path in newPaths)
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) continue;
                var extension = Path.GetExtension(path).ToLower();
                if (extension != ".arw" && extension != ".jxl") continue;
                validPaths.Add(path);
            }

            var newItems = validPaths.Select(path => new ImageItemViewModel
            {
                FilePath = path,
                FileName = Path.GetFileName(path),
                Status = ImageStatus.Ready
            }).ToList();

            await OnUiAsync(() =>
            {
                foreach (var item in newItems)
                {
                    Images.Add(item);
                    item.PropertyChanged += Item_PropertyChanged;
                }
            });

            _ = Task.Run(() => GenerateThumbnailsAsync(newItems));

            string msg = skipped > 0
                ? $"{AppStrings.FilesLoaded}{Images.Count}{AppStrings.ItemsSingular}{AppStrings.OfSuffix}{skipped}{AppStrings.DuplicatesSkipped}"
                : $"{AppStrings.FilesLoaded}{Images.Count}{AppStrings.ItemsSingular}";
            StatusMessage = msg;
            UpdateSelectionState();
            RefreshViewCommands();
        }

        private async Task GenerateThumbnailsAsync(List<ImageItemViewModel> items)
        {
            var semaphore = new SemaphoreSlim(Math.Min(4, Environment.ProcessorCount));
            var tasks = items.Select(async item =>
            {
                await semaphore.WaitAsync();
                try
                {
                    if (item.IsRemoved) return;
                    try
                    {
                        var thumbnailBytes = await _imageService.GetThumbnailAsync(item.FilePath);
                        using var ms = new MemoryStream(thumbnailBytes);
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.StreamSource = ms;
                        bitmap.EndInit();
                        bitmap.Freeze();
                        if (item.IsRemoved) return;
                        await OnUiAsync(() => item.Thumbnail = bitmap);
                    }
                    catch (Exception ex)
                    {
                        if (!item.IsRemoved)
                            await OnUiAsync(() => item.ErrorMessage = $"{AppStrings.ThumbnailFailedPrefix}{ex.Message}");
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            });
            await Task.WhenAll(tasks);
        }

        private string? ResolveOutputPath(string inputPath)
        {
            string directory;
            if (UseCustomOutputDirectory && !string.IsNullOrEmpty(CustomOutputDirectory))
            {
                directory = CustomOutputDirectory;
            }
            else
            {
                directory = UseSubfolder
                    ? Path.Combine(Path.GetDirectoryName(inputPath)!, SubfolderName)
                    : Path.GetDirectoryName(inputPath)!;
            }
            Directory.CreateDirectory(directory);

            string baseName = Path.GetFileNameWithoutExtension(inputPath);
            string extension = GetOutputExtension();
            string outputPath = Path.Combine(directory, baseName + extension);

            return ResolveConflict(outputPath);
        }

        private string GetOutputExtension()
        {
            return OutputFormat switch
            {
                OutputFormat.Jxl => ".jxl",
                OutputFormat.Jpeg => ".jpg",
                OutputFormat.Png => ".png",
                _ => ".jxl"
            };
        }

        private string? ResolveConflict(string outputPath)
        {
            if (!File.Exists(outputPath))
                return outputPath;

            switch (ConflictResolution)
            {
                case ConflictResolution.Skip:
                    return null;
                case ConflictResolution.Overwrite:
                    return outputPath;
                case ConflictResolution.AppendNumber:
                    int counter = 1;
                    string directory = Path.GetDirectoryName(outputPath)!;
                    string baseName = Path.GetFileNameWithoutExtension(outputPath);
                    string extension = Path.GetExtension(outputPath);
                    string candidate;
                    do
                    {
                        candidate = Path.Combine(directory, $"{baseName}_{counter}{extension}");
                        counter++;
                    } while (File.Exists(candidate));
                    return candidate;
                default:
                    return outputPath;
            }
        }

        private async Task OnUiAsync(Action action)
        {
            await App.Current.Dispatcher.InvokeAsync(action);
        }

        private void SaveSettings()
        {
            var saved = SettingsService.Load();
            SettingsService.Save(new AppSettings
            {
                UseSubfolder = UseSubfolder,
                SubfolderName = SubfolderName,
                QualityPreset = QualityPreset,
                SearchRecursive = SearchRecursive,
                OutputFormat = OutputFormat,
                ConflictResolution = ConflictResolution,
                ConfirmOverwrite = ConfirmOverwrite,
                UseCustomOutputDirectory = UseCustomOutputDirectory,
                CustomOutputDirectory = CustomOutputDirectory,
                Presets = saved.Presets
            });
        }
    }
}
