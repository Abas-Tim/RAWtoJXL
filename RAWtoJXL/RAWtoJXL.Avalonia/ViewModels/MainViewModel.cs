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
        private readonly IBatchConversionService _batchConversionService;
        private readonly bool _generateThumbnails;
        private readonly ObservableCollection<ImageItemViewModel> _selectedImages = new();
        private readonly HashSet<string> _addedFilePaths = new(StringComparer.OrdinalIgnoreCase);
        private CancellationTokenSource? _cancellationTokenSource;
        private long _batchGeneration;
        private readonly object _batchProgressGate = new();
        private (long Generation, BatchConversionProgress Progress)? _pendingBatchProgress;
        private bool _batchProgressUpdateScheduled;

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

        [ObservableProperty]
        private int _batchJobs = -1;

        partial void OnBatchJobsChanged(int value)
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
            BatchJobs = saved.BatchJobs;
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

        public event Action<string>? RequestOpenCompare;
        public event Action? RequestRefreshLayout;

        public MainViewModel(
            IImageService imageService,
            IDialogService dialogService,
            IDispatcherService dispatcherService,
            IFilePickerService filePickerService,
            bool generateThumbnails = true,
            IBatchConversionService? batchConversionService = null)
        {
            _imageService = imageService ?? throw new ArgumentNullException(nameof(imageService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _dispatcherService = dispatcherService ?? throw new ArgumentNullException(nameof(dispatcherService));
            _filePickerService = filePickerService ?? throw new ArgumentNullException(nameof(filePickerService));
            _batchConversionService = batchConversionService ?? new BatchConversionService(imageService);
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
            BatchJobs = saved.BatchJobs;
        }

        [RelayCommand(CanExecute = nameof(CanExecuteConvertSelected))]
        private async Task ConvertSelectedAsync()
        {
            var readySelected = _selectedImages.Where(i => i.Status == ImageStatus.Ready || i.Status == ImageStatus.Converted || i.Status == ImageStatus.Failed).ToList();
            if (!readySelected.Any()) return;

            using var cancellationTokenSource = new CancellationTokenSource();
            _cancellationTokenSource = cancellationTokenSource;
            var generation = Interlocked.Increment(ref _batchGeneration);
            CompletedCount = 0;
            TotalCount = readySelected.Count;
            StatusMessage = $"{AppStrings.ConvertingProgress}{0}{AppStrings.OfSuffix}{readySelected.Count} (0%)";
            IsConverting = true;
            RefreshAllCommands();

            BatchConversionBatchResult? batch = null;
            try
            {
                var settings = new BatchSettingsSnapshot(
                    QualityPreset,
                    OutputFormat,
                    ConflictResolution,
                    ConfirmOverwrite,
                    UseCustomOutputDirectory,
                    CustomOutputDirectory,
                    UseSubfolder,
                    SubfolderName,
                    SkipMetadata,
                    CjxlEffort >= 1 ? CjxlEffort : null,
                    CjxlThreads,
                    BatchJobs);

                var jobs = BatchParallelismPolicy.ResolveJobs(settings.BatchJobs, readySelected.Count);
                var encoderThreads = BatchParallelismPolicy.ResolveEncoderThreads(settings.CjxlThreads, jobs);
                var inputs = readySelected.Select((item, index) => new BatchConversionInput(
                    $"{generation}:{index}",
                    item.FilePath,
                    item.EffectiveQuality(settings.QualityPreset))).ToList();
                var requests = BatchConversionPlanner.CreateRequests(
                    inputs,
                    settings.OutputFormat,
                    settings.Conflict,
                    settings.UseCustomOutputDirectory,
                    settings.CustomOutputDirectory,
                    settings.UseSubfolder,
                    settings.SubfolderName,
                    settings.SkipMetadata,
                    settings.Effort,
                    encoderThreads).ToList();

                await OnUiAsync(() =>
                {
                    foreach (var item in readySelected)
                    {
                        item.Status = ImageStatus.Converting;
                        item.ErrorMessage = null;
                    }
                });

                // All conflict checks and overwrite prompts happen before any
                // worker starts, so dialogs are serial and every request keeps
                // the settings that were selected at batch start.
                for (var index = 0; index < requests.Count; index++)
                {
                    var request = requests[index];
                    if (cancellationTokenSource.IsCancellationRequested)
                    {
                        requests[index] = request with
                        {
                            InitialStatus = BatchConversionStatus.Cancelled,
                            InitialError = AppStrings.Cancelled
                        };
                        continue;
                    }

                    if (request.InitialStatus.HasValue)
                    {
                        continue;
                    }

                    if (request.OutputPath != null &&
                        request.Conflict == ConflictResolution.Overwrite &&
                        settings.ConfirmOverwrite &&
                        File.Exists(request.OutputPath))
                    {
                        bool confirm;
                        try
                        {
                            confirm = await _dialogService.ShowConfirmAsync(
                                $"Overwrite existing file?\n\n{Path.GetFileName(request.OutputPath)}",
                                "Confirm Overwrite");
                        }
                        catch (Exception ex)
                        {
                            requests[index] = request with
                            {
                                InitialStatus = BatchConversionStatus.Failed,
                                InitialError = ex.GetBaseException().Message
                            };
                            continue;
                        }

                        if (!confirm)
                        {
                            requests[index] = request with
                            {
                                InitialStatus = BatchConversionStatus.Skipped,
                                InitialError = AppStrings.FileSkippedByUser
                            };
                        }
                    }
                }

                var itemById = inputs.Select((input, index) => (input.Id, Item: readySelected[index]))
                    .ToDictionary(pair => pair.Id, pair => pair.Item, StringComparer.Ordinal);

                batch = await _batchConversionService.RunAsync(
                    requests,
                    jobs,
                    progress: value => OnBatchProgress(generation, value),
                    fileCompleted: async (result, _) =>
                    {
                        if (!IsCurrentBatch(generation) || !itemById.TryGetValue(result.Id, out var item))
                        {
                            return;
                        }
                        await OnUiAsync(() => ApplyBatchResult(item, result));
                    },
                    cancellationToken: cancellationTokenSource.Token);

                // The service intentionally isolates completion-callback
                // failures. Reconcile the complete result set once more so a
                // dispatcher or item callback failure cannot leave an item in
                // Converting after the batch has finished.
                await OnUiAsync(() =>
                {
                    foreach (var result in batch.Files)
                    {
                        if (itemById.TryGetValue(result.Id, out var item))
                        {
                            ApplyBatchResult(item, result);
                        }
                    }
                });

                var recentFiles = batch.Files
                    .Where(result => result.Status == BatchConversionStatus.Converted)
                    .Select(result => result.InputPath)
                    .ToList();
                if (recentFiles.Count > 0)
                {
                    SettingsService.AddRecentFiles(recentFiles);
                    await OnUiAsync(RefreshRecentFiles);
                }
            }
            catch (Exception ex)
            {
                await OnUiAsync(() =>
                {
                    foreach (var item in readySelected.Where(item => item.Status == ImageStatus.Converting))
                    {
                        item.Status = ImageStatus.Failed;
                        item.ErrorMessage = ex.GetBaseException().Message;
                    }
                });
            }
            finally
            {
                Interlocked.Increment(ref _batchGeneration);
                lock (_batchProgressGate)
                {
                    _pendingBatchProgress = null;
                }
                var finalBatch = batch;
                await OnUiAsync(() =>
                {
                    var lastOutput = finalBatch?.Files
                        .FirstOrDefault(result => result.Status == BatchConversionStatus.Converted && !string.IsNullOrEmpty(result.OutputPath))
                        ?.OutputPath;
                    if (!string.IsNullOrEmpty(lastOutput))
                    {
                        OutputDirectory = Path.GetDirectoryName(lastOutput) ?? string.Empty;
                    }

                    IsConverting = false;
                    if (finalBatch != null)
                    {
                        StatusMessage = BuildCompletionMessage(
                            finalBatch.Converted,
                            finalBatch.Skipped,
                            finalBatch.Failed,
                            finalBatch.Cancelled ? finalBatch.Total - finalBatch.Converted - finalBatch.Skipped - finalBatch.Failed : 0,
                            finalBatch.Total);
                    }
                    else if (cancellationTokenSource.IsCancellationRequested)
                    {
                        StatusMessage = AppStrings.ConversionCancelled;
                    }

                    CompletedCount = 0;
                    TotalCount = 0;
                    RefreshAllCommands();
                    RequestRefreshLayout?.Invoke();
                });
                if (ReferenceEquals(_cancellationTokenSource, cancellationTokenSource))
                {
                    _cancellationTokenSource = null;
                }
            }
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

        [ObservableProperty]
        private bool _isSettingsOpen;

        partial void OnIsSettingsOpenChanged(bool value)
        {
            if (!value)
            {
                RefreshSettings();
            }
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

        private void OnBatchProgress(long generation, BatchConversionProgress progress)
        {
            if (!IsCurrentBatch(generation))
            {
                return;
            }

            if (Dispatcher.UIThread.CheckAccess())
            {
                ApplyBatchProgress(generation, progress);
                return;
            }

            lock (_batchProgressGate)
            {
                if (!IsCurrentBatch(generation))
                {
                    return;
                }

                _pendingBatchProgress = (generation, progress);
                if (_batchProgressUpdateScheduled)
                {
                    return;
                }

                _batchProgressUpdateScheduled = true;
            }

            // Progress callbacks can be much more frequent than the UI can
            // render. Keep only the newest snapshot per dispatcher turn.
            Dispatcher.UIThread.Post(FlushPendingBatchProgress);
        }

        private void FlushPendingBatchProgress()
        {
            (long Generation, BatchConversionProgress Progress)? pending;
            lock (_batchProgressGate)
            {
                pending = _pendingBatchProgress;
                _pendingBatchProgress = null;
                _batchProgressUpdateScheduled = false;
            }

            if (pending.HasValue)
            {
                ApplyBatchProgress(pending.Value.Generation, pending.Value.Progress);
            }
        }

        private void ApplyBatchProgress(long generation, BatchConversionProgress progress)
        {
            if (!IsCurrentBatch(generation))
            {
                return;
            }

            CompletedCount = progress.CompletedCount;
            TotalCount = progress.TotalCount;
            var percent = progress.OverallFraction * 100;
            StatusMessage = $"{AppStrings.ConvertingProgress}{progress.CompletedCount}{AppStrings.OfSuffix}{progress.TotalCount} ({percent:F0}%)";
        }

        private bool IsCurrentBatch(long generation) =>
            IsConverting && Volatile.Read(ref _batchGeneration) == generation;

        private static void ApplyBatchResult(
            ImageItemViewModel item,
            BatchConversionFileResult result)
        {
            switch (result.Status)
            {
                case BatchConversionStatus.Converted:
                    item.Status = ImageStatus.Converted;
                    item.SourceFileSize = result.InputBytes;
                    item.OutputFileSize = result.OutputBytes;
                    item.OutputPath = result.OutputPath ?? string.Empty;
                    item.ErrorMessage = null;
                    break;

                case BatchConversionStatus.Cancelled:
                    item.Status = ImageStatus.Pending;
                    item.ErrorMessage = AppStrings.Cancelled;
                    break;

                case BatchConversionStatus.Skipped:
                    item.Status = ImageStatus.Failed;
                    item.ErrorMessage = result.Error ?? AppStrings.FileSkipped;
                    break;

                default:
                    item.Status = ImageStatus.Failed;
                    item.ErrorMessage = result.Error ?? "conversion failed";
                    break;
            }
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
            settings.BatchJobs = BatchJobs;
            SettingsService.Save();
        }

        private sealed record BatchSettingsSnapshot(
            int QualityPreset,
            OutputFormat OutputFormat,
            ConflictResolution Conflict,
            bool ConfirmOverwrite,
            bool UseCustomOutputDirectory,
            string CustomOutputDirectory,
            bool UseSubfolder,
            string SubfolderName,
            bool SkipMetadata,
            int? Effort,
            int CjxlThreads,
            int BatchJobs);

        private static bool IsSupportedFile(string extension)
        {
            return SupportedFormats.IsSupportedInput(extension);
        }
    }
}
