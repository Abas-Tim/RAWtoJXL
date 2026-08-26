using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using RAWtoJXL.Core.Interfaces;
using RAWtoJXL.Core.Models;
using RAWtoJXL.Core.Services;
using RAWtoJXL.Core.Settings;
using RAWtoJXL.Avalonia.Services;
using RAWtoJXL.Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace RAWtoJXL.Avalonia.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly IImageService _imageService;
        private readonly IDialogService _dialogService;
        private readonly IDispatcherService _dispatcherService;
        private readonly IFilePickerService _filePickerService;
        private readonly bool _generateThumbnails;
        private readonly ObservableCollection<ImageItemViewModel> _selectedImages = new();
        private readonly HashSet<string> _addedFilePaths = new(StringComparer.OrdinalIgnoreCase);
        private CancellationTokenSource? _cancellationTokenSource;

        [ObservableProperty]
        private ObservableCollection<ImageItemViewModel> _images = new();

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
            CompareSelectedCommand.NotifyCanExecuteChanged();
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
            foreach (var item in Images)
            {
                item.GlobalQualityPreset = value;
            }
        }

        [ObservableProperty]
        private bool _searchRecursive;

        partial void OnSearchRecursiveChanged(bool value)
        {
            SaveSettings();
        }

        [ObservableProperty]
        private OutputFormat _outputFormat = OutputFormat.Jxl;

        partial void OnOutputFormatChanged(OutputFormat value)
        {
            SaveSettings();
        }

        [ObservableProperty]
        private ConflictResolution _conflictResolution = ConflictResolution.Overwrite;

        partial void OnConflictResolutionChanged(ConflictResolution value)
        {
            SaveSettings();
        }

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

        [ObservableProperty]
        private bool _isRecentHovered;

        public bool HasRecentFiles => RecentFiles.Count > 0;

        [ObservableProperty]
        private bool _skipMetadata;

        partial void OnSkipMetadataChanged(bool value)
        {
            SaveSettings();
        }

        [ObservableProperty]
        private int _cjxlEffort = -1;

        partial void OnCjxlEffortChanged(int value)
        {
            SaveSettings();
        }

        [ObservableProperty]
        private int _cjxlThreads = -1;

        partial void OnCjxlThreadsChanged(int value)
        {
            SaveSettings();
        }

         public void RefreshSettings()
        {
            var saved = SettingsService.Load().Clone();
            UseSubfolder = saved.UseSubfolder;
            SubfolderName = saved.SubfolderName;
            QualityPreset = saved.QualityPreset;
            SearchRecursive = saved.SearchRecursive;
            OutputFormat = saved.OutputFormat;
            ConflictResolution = saved.ConflictResolution;
            ConfirmOverwrite = saved.ConfirmOverwrite;
            UseCustomOutputDirectory = saved.UseCustomOutputDirectory;
            CustomOutputDirectory = saved.CustomOutputDirectory;
            SkipMetadata = saved.SkipMetadata;
            CjxlEffort = saved.CjxlEffort;
            CjxlThreads = saved.CjxlThreads;
        }

        [ObservableProperty]
        private bool _isAnySelected;

        [ObservableProperty]
        private bool _isExactlyOneSelected;

        partial void OnIsExactlyOneSelectedChanged(bool value)
        {
            if (Dispatcher.UIThread.CheckAccess())
            {
                CompareSelectedCommand.NotifyCanExecuteChanged();
            }
            else
            {
                Dispatcher.UIThread.Post(CompareSelectedCommand.NotifyCanExecuteChanged);
            }
        }

        [ObservableProperty]
        private int _completedCount;

        [ObservableProperty]
        private int _totalCount = 0;

        public event Action? RequestOpenSettings;
        public event Action<string>? RequestOpenCompare;
        public event Action? RequestRefreshLayout;

        public MainViewModel(IImageService imageService, IDialogService dialogService, IDispatcherService dispatcherService, IFilePickerService filePickerService, bool generateThumbnails = true)
        {
            _imageService = imageService;
            _dialogService = dialogService;
            _dispatcherService = dispatcherService;
            _filePickerService = filePickerService;
            _generateThumbnails = generateThumbnails;
            SettingsService.ErrorOccurred += OnSettingsError;
            LoadRecentFilesFromSettings();
        }

        private void OnSettingsError(object? sender, Exception ex)
        {
            var message = $"{AppStrings.SettingsErrorPrefix}{ex.Message}";
            Dispatcher.UIThread.Post(() => StatusMessage = message);
        }

        private void LoadRecentFilesFromSettings()
        {
            var saved = SettingsService.Load().Clone();
            RecentFiles = new ObservableCollection<string>(saved.RecentFiles);
            QualityPreset = saved.QualityPreset;
            UseSubfolder = saved.UseSubfolder;
            SubfolderName = saved.SubfolderName;
            SearchRecursive = saved.SearchRecursive;
            OutputFormat = saved.OutputFormat;
            ConflictResolution = saved.ConflictResolution;
            ConfirmOverwrite = saved.ConfirmOverwrite;
            UseCustomOutputDirectory = saved.UseCustomOutputDirectory;
            CustomOutputDirectory = saved.CustomOutputDirectory;
            SkipMetadata = saved.SkipMetadata;
            CjxlEffort = saved.CjxlEffort;
            CjxlThreads = saved.CjxlThreads;
        }

        [RelayCommand(CanExecute = nameof(CanExecuteConvertSelected))]
        private async Task ConvertSelectedAsync()
        {
            var readySelected = _selectedImages.Where(i => i.Status == ImageStatus.Ready || i.Status == ImageStatus.Converted || i.Status == ImageStatus.Failed).ToList();
            if (!readySelected.Any()) return;

            _cancellationTokenSource = new CancellationTokenSource();
            _completedCountField = 0;
            _currentFileProgress = 0;
            CompletedCount = 0;
            TotalCount = readySelected.Count;
            int convertedCount = 0, skippedCount = 0, failedCount = 0, cancelledCount = 0;
            StatusMessage = $"{AppStrings.ConvertingProgress}{0}{AppStrings.OfSuffix}{readySelected.Count} (0%)";
            IsConverting = true;
            RefreshAllCommands();

           foreach (var item in readySelected)
            {
                if (_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    break;
                }

                _currentFileProgress = 0;
                item.Status = ImageStatus.Converting;

                string? outputPath = ResolveOutputPath(item.FilePath);

                if (outputPath == null)
                {
                    await OnUiAsync(() =>
                    {
                        item.Status = ImageStatus.Failed;
                        item.ErrorMessage = AppStrings.FileSkipped;
                    });
                    skippedCount++;
                    UpdateProgress(readySelected.Count);
                    continue;
                }

                if (File.Exists(outputPath) && ConfirmOverwrite)
                {
                    bool confirm = await _dialogService.ShowConfirmAsync(
                        $"Overwrite existing file?\n\n{Path.GetFileName(outputPath)}",
                        "Confirm Overwrite");
                    if (!confirm)
                    {
                        await OnUiAsync(() =>
                        {
                            item.Status = ImageStatus.Failed;
                            item.ErrorMessage = AppStrings.FileSkippedByUser;
                        });
                        skippedCount++;
                        UpdateProgress(readySelected.Count);
                        continue;
                    }
                }

                int quality = item.EffectiveQuality(QualityPreset);

                try
                {
                    long sourceSize = 0;
                    try { sourceSize = new FileInfo(item.FilePath).Length; } catch { }

                    await _imageService.ConvertToJxlAsync(
                        item.FilePath,
                        outputPath,
                        p => _ = OnUiAsync(() => OnFileProgress(p)),
                        quality,
                        OutputFormat,
                        _cancellationTokenSource.Token,
                        SkipMetadata,
                        CjxlEffort >= 0 ? CjxlEffort : null,
                        CjxlThreads > 0 ? CjxlThreads : null);

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

                        if (string.IsNullOrEmpty(OutputDirectory))
                        {
                            OutputDirectory = Path.GetDirectoryName(outputPath) ?? string.Empty;
                        }
                    });
                    convertedCount++;
                }
                catch (OperationCanceledException)
                {
                    await OnUiAsync(() =>
                    {
                        item.Status = ImageStatus.Pending;
                        item.ErrorMessage = AppStrings.Cancelled;
                    });
                    cancelledCount++;
                }
                catch (FileLockedException ex)
                {
                    await OnUiAsync(() =>
                    {
                        item.Status = ImageStatus.Failed;
                        item.ErrorMessage = $"{AppStrings.FileLockedPrefix}{ex.Message}";
                    });
                    failedCount++;
                }
                catch (Exception ex)
                {
                    await OnUiAsync(() =>
                    {
                        item.Status = ImageStatus.Failed;
                        item.ErrorMessage = ex.Message;
                    });
                    failedCount++;
                }

                UpdateProgress(readySelected.Count);
            }

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
                IsConverting = false;
                _cancellationTokenSource = null;
                StatusMessage = BuildCompletionMessage(convertedCount, skippedCount, failedCount, cancelledCount, readySelected.Count);
                CompletedCount = 0;
                TotalCount = 0;
                RefreshAllCommands();
                RequestRefreshLayout?.Invoke();
            });
        }

        private static string BuildCompletionMessage(int converted, int skipped, int failed, int cancelled, int total)
        {
            if (cancelled > 0)
            {
                return $"{AppStrings.ConversionCancelled} {string.Format(AppStrings.ConversionSummary, converted, skipped, failed)}";
            }

            if (converted == total && skipped == 0 && failed == 0)
            {
                return AppStrings.ConversionComplete;
            }

            return $"{AppStrings.ConversionComplete} {string.Format(AppStrings.ConversionSummary, converted, skipped, failed)}";
        }

        private bool CanExecuteConvertSelected() =>
            !IsConverting && _selectedImages.Any(i => i.Status == ImageStatus.Ready || i.Status == ImageStatus.Converted || i.Status == ImageStatus.Failed);

        [RelayCommand(CanExecute = nameof(CanExecuteRemoveSelected))]
        private void RemoveSelected()
        {
            var itemsToRemove = _selectedImages.ToList();
            foreach (var item in itemsToRemove)
            {
                item.Thumbnail?.Dispose();
                item.IsRemoved = true;
                item.PropertyChanged -= Item_PropertyChanged;
                _addedFilePaths.Remove(item.FilePath);
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

        private bool CanExecuteCancel() => IsConverting;

        [RelayCommand]
        private void OpenSettings()
        {
            RequestOpenSettings?.Invoke();
        }

        [RelayCommand(CanExecute = nameof(CanExecuteCompareSelected))]
        private void CompareSelected()
        {
            if (_selectedImages.Count != 1)
            {
                return;
            }

            RequestOpenCompare?.Invoke(_selectedImages[0].FilePath);
        }

        private bool CanExecuteCompareSelected() => !IsConverting && _selectedImages.Count == 1;

        [RelayCommand(CanExecute = nameof(CanExecuteSelectAll))]
        private async Task OpenFile()
        {
            var files = await _filePickerService.PickFilesAsync(
                AppStrings.OpenFileDialogTitle,
                AppStrings.OpenFileDialogFilter,
                multiselect: true);

            if (files.Any())
            {
                await AddFilesAsync(files);
            }
        }

        [RelayCommand(CanExecute = nameof(CanExecuteSelectAll))]
        private async Task OpenFolder()
        {
            var folder = await _filePickerService.PickFolderAsync(string.Empty);
            if (!string.IsNullOrEmpty(folder))
            {
                var files = await Task.Run(() => ImageFileEnumerator.Enumerate(
                    new[] { folder },
                    SearchRecursive,
                    SupportedFormats.AllInputExtensions)).ConfigureAwait(false);
                if (files.Any())
                {
                    await AddFilesAsync(files);
                }
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
        private async Task LoadRecentFiles()
        {
            var existing = RecentFiles.ToList();
            if (existing.Count > 0)
            {
                await AddFilesAsync(existing);
            }
        }

        [RelayCommand]
        private async Task LoadSingleRecentFile(string filePath)
        {
            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                await AddFilesAsync(new[] { filePath });
            }
        }

        [RelayCommand]
        private void ClearRecentFiles()
        {
            var settings = SettingsService.Load();
            settings.RecentFiles.Clear();
            SettingsService.Save();
            RecentFiles = new ObservableCollection<string>();
        }

       private int _completedCountField;
        private double _currentFileProgress;

        private void UpdateProgress(int total)
        {
            int completed = Interlocked.Increment(ref _completedCountField);
            CompletedCount = completed;
            _currentFileProgress = 0;
            StatusMessage = FormatProgressMessage(completed, total);
        }

        private void UpdateProgressDisplay(int total)
        {
            int completed = Volatile.Read(ref _completedCountField);
            StatusMessage = FormatProgressMessage(completed, total);
        }

        private string FormatProgressMessage(int completed, int total)
        {
            double overallPercent = total > 0 ? (completed + _currentFileProgress) / total * 100 : 0;
            return $"{AppStrings.ConvertingProgress}{completed}{AppStrings.OfSuffix}{total} ({overallPercent:F0}%)";
        }

        private void OnFileProgress(double progress)
        {
            _currentFileProgress = progress;
            UpdateProgressDisplay(TotalCount);
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
            if (Dispatcher.UIThread.CheckAccess())
            {
                ConvertSelectedCommand.NotifyCanExecuteChanged();
                RemoveSelectedCommand.NotifyCanExecuteChanged();
                SelectAllCommand.NotifyCanExecuteChanged();
                CompareSelectedCommand.NotifyCanExecuteChanged();
            }
            else
            {
                Dispatcher.UIThread.Post(() =>
                {
                    ConvertSelectedCommand.NotifyCanExecuteChanged();
                    RemoveSelectedCommand.NotifyCanExecuteChanged();
                    SelectAllCommand.NotifyCanExecuteChanged();
                    CompareSelectedCommand.NotifyCanExecuteChanged();
                });
            }
        }

        private void RefreshAllCommands()
        {
            if (Dispatcher.UIThread.CheckAccess())
            {
                ConvertSelectedCommand.NotifyCanExecuteChanged();
                RemoveSelectedCommand.NotifyCanExecuteChanged();
                SelectAllCommand.NotifyCanExecuteChanged();
                CancelCommand.NotifyCanExecuteChanged();
                OpenOutputFolderCommand.NotifyCanExecuteChanged();
                CompareSelectedCommand.NotifyCanExecuteChanged();
            }
            else
            {
                Dispatcher.UIThread.Post(() =>
                {
                    ConvertSelectedCommand.NotifyCanExecuteChanged();
                    RemoveSelectedCommand.NotifyCanExecuteChanged();
                    SelectAllCommand.NotifyCanExecuteChanged();
                    CancelCommand.NotifyCanExecuteChanged();
                    OpenOutputFolderCommand.NotifyCanExecuteChanged();
                    CompareSelectedCommand.NotifyCanExecuteChanged();
                });
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
            if (IsExactlyOneSelected != (_selectedImages.Count == 1))
                IsExactlyOneSelected = _selectedImages.Count == 1;
        }

        private void RefreshRecentFiles()
        {
            var saved = SettingsService.Load();
            RecentFiles = new ObservableCollection<string>(saved.RecentFiles);
        }
        public async Task AddFilesAsync(IEnumerable<string> filePaths)
        {
            var normalizedPaths = filePaths.Select(p => Path.GetFullPath(p)).Distinct().ToList();

            var validPaths = new List<string>();
            foreach (var path in normalizedPaths)
            {
                if (_addedFilePaths.Contains(path)) continue;
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) continue;
                if (!IsSupportedFile(Path.GetExtension(path))) continue;
                validPaths.Add(path);
            }

            foreach (var p in validPaths)
            {
                _addedFilePaths.Add(p);
            }

            var newItems = new List<ImageItemViewModel>(validPaths.Count);
            foreach (var path in validPaths)
            {
                long fileSize = 0;
                try { fileSize = new FileInfo(path).Length; } catch { }
                newItems.Add(new ImageItemViewModel
                {
                    FilePath = path,
                    FileName = Path.GetFileName(path),
                    Status = ImageStatus.Ready,
                    SourceFileSize = fileSize,
                    GlobalQualityPreset = QualityPreset
                });
            }

            await OnUiAsync(() =>
            {
                foreach (var item in newItems)
                {
                    Images.Add(item);
                    item.PropertyChanged += Item_PropertyChanged;
                }
            });

            if (_generateThumbnails)
            {
                var thumbnailTask = Task.Run(() => GenerateThumbnailsAsync(newItems));
                _ = thumbnailTask.ContinueWith(
                    t => StatusMessage = $"{AppStrings.ThumbnailFailedPrefix}{t.Exception!.GetBaseException().Message}",
                    CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted,
                    TaskScheduler.Default);
            }

            UpdateSelectionState();
            RefreshViewCommands();
        }

        private async Task GenerateThumbnailsAsync(List<ImageItemViewModel> items)
        {
            var semaphore = new SemaphoreSlim(Math.Max(4, Environment.ProcessorCount / 2));
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
                        var bitmap = new Bitmap(ms);
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
            return OutputPathResolver.Resolve(
                inputPath,
                OutputFormat,
                ConflictResolution,
                UseCustomOutputDirectory,
                CustomOutputDirectory,
                UseSubfolder,
                SubfolderName);
        }

        private Task OnUiAsync(Action action)
        {
            return _dispatcherService.InvokeAsync(action);
        }

        private void SaveSettings()
        {
            var settings = SettingsService.Load();
            settings.UseSubfolder = UseSubfolder;
            settings.SubfolderName = SubfolderName;
            settings.QualityPreset = QualityPreset;
            settings.SearchRecursive = SearchRecursive;
            settings.OutputFormat = OutputFormat;
            settings.ConflictResolution = ConflictResolution;
            settings.ConfirmOverwrite = ConfirmOverwrite;
            settings.UseCustomOutputDirectory = UseCustomOutputDirectory;
            settings.CustomOutputDirectory = CustomOutputDirectory;
            settings.SkipMetadata = SkipMetadata;
            settings.CjxlEffort = CjxlEffort;
            settings.CjxlThreads = CjxlThreads;
            SettingsService.Save();
        }

        private static bool IsSupportedFile(string extension)
        {
            return SupportedFormats.IsSupportedInput(extension);
        }
    }
}
