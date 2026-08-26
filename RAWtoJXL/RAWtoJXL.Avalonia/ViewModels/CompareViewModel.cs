using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using RAWtoJXL.Avalonia.Services;
using RAWtoJXL.Avalonia.Controls;
using RAWtoJXL.Core.Interfaces;
using RAWtoJXL.Core.Models;
using RAWtoJXL.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace RAWtoJXL.Avalonia.ViewModels
{
    public partial class CompareViewModel : ObservableObject, IDisposable
    {
        private static readonly OutputFormat[] AllFormats = { OutputFormat.Jxl, OutputFormat.Avif, OutputFormat.Jpeg };

    private readonly ICompareConversionService _conversionService;
    private readonly ComparePipelineOrchestrator _orchestrator;
    private readonly IDispatcherService _dispatcherService;
        private readonly CancellationTokenSource _lifetimeCts = new();
        private readonly System.Collections.Concurrent.ConcurrentDictionary<ComparePaneViewModel, CancellationTokenSource> _paneCts = new();
        private readonly Dictionary<ComparePaneViewModel, ViewportSnapshot> _viewportSnapshots = new();
    private ComparePaneViewModel? _lastViewportSource;
        private readonly object _mirrorGuard = new();

        private bool _initializing = true;
        private bool _applyingMirror;
        private readonly HashSet<OutputFormat> _pendingQualityFormats = new();
        private bool _effortPending;
        private bool _disposed;
        private bool _recomputingFormats;
        private DispatcherTimer? _reconvertTimer;

        public string SourceFilePath { get; }
        public long SourceFileBytes { get; }
        public OutputFormat? OriginalFormat { get; }

        public ObservableCollection<ComparePaneViewModel> Panes { get; } = new();

        public ComparePaneViewModel LeftPane => Panes[0];
        public ComparePaneViewModel MiddlePane => Panes[1];
        public ComparePaneViewModel RightPane => Panes[2];

        public IReadOnlyList<int> EffortOptions { get; } = Enumerable.Range(1, 9).ToList();

        [ObservableProperty]
        private int _jxlQuality = 90;

        [ObservableProperty]
        private int _avifQuality = 90;

        [ObservableProperty]
        private int _jpegQuality = 90;

        [ObservableProperty]
        private bool _isSettingsPanelOpen;

        [ObservableProperty]
        private int _middleQuality = 90;

        [ObservableProperty]
        private int _rightQuality = 90;

        [ObservableProperty]
        private bool _middleIsJxl;

        [ObservableProperty]
        private bool _rightIsJxl;

        public string MiddleFormatText => MiddlePane.Format?.ToString().ToUpperInvariant() ?? "—";

        public string RightFormatText => RightPane.Format?.ToString().ToUpperInvariant() ?? "—";

        public void CycleMiddleFormat(int delta)
        {
            CyclePaneFormat(MiddlePane, delta);
        }

        public void CycleRightFormat(int delta)
        {
            CyclePaneFormat(RightPane, delta);
        }

        private void CyclePaneFormat(ComparePaneViewModel pane, int delta)
        {
            if (pane.Format is not { } current || pane.AvailableFormats.Count == 0)
            {
                return;
            }

            int index = pane.AvailableFormats.IndexOf(current);
            int next = index < 0 ? 0 : (index + delta + pane.AvailableFormats.Count) % pane.AvailableFormats.Count;
            pane.Format = pane.AvailableFormats[next];
        }

        partial void OnMiddleQualityChanged(int value)
        {
            if (MiddlePane.Format is { } format && value != QualityFor(format))
            {
                SetFormatQuality(format, value);
            }
        }

        partial void OnRightQualityChanged(int value)
        {
            if (RightPane.Format is { } format && value != QualityFor(format))
            {
                SetFormatQuality(format, value);
            }
        }

        private void SetFormatQuality(OutputFormat format, int value)
        {
            switch (format)
            {
                case OutputFormat.Jxl:
                    JxlQuality = value;
                    break;
                case OutputFormat.Avif:
                    AvifQuality = value;
                    break;
                case OutputFormat.Jpeg:
                    JpegQuality = value;
                    break;
            }
        }

        private void SyncPaneQualityProperties()
        {
            MiddleQuality = QualityFor(MiddlePane.Format);
            RightQuality = QualityFor(RightPane.Format);
            MiddleIsJxl = MiddlePane.Format == OutputFormat.Jxl;
            RightIsJxl = RightPane.Format == OutputFormat.Jxl;
        }

        partial void OnJxlQualityChanged(int value)
        {
            MarkQualityPending(OutputFormat.Jxl);
            SyncPaneQualityProperties();
        }

        partial void OnAvifQualityChanged(int value)
        {
            MarkQualityPending(OutputFormat.Avif);
            SyncPaneQualityProperties();
        }

        partial void OnJpegQualityChanged(int value)
        {
            MarkQualityPending(OutputFormat.Jpeg);
            SyncPaneQualityProperties();
        }

        private void MarkQualityPending(OutputFormat format)
        {
            if (_initializing || _disposed)
            {
                return;
            }

            _pendingQualityFormats.Add(format);
            ScheduleReconvert();
        }

        public int QualityFor(OutputFormat? format)
        {
            return format switch
            {
                OutputFormat.Jxl => JxlQuality,
                OutputFormat.Avif => AvifQuality,
                OutputFormat.Jpeg => JpegQuality,
                _ => JxlQuality
            };
        }

        [ObservableProperty]
        private int _jxlEffort = CompareDefaults.JxlEffort;

        partial void OnJxlEffortChanged(int value)
        {
            if (_initializing || _disposed)
            {
                return;
            }

            _effortPending = true;
            ScheduleReconvert();
        }

        [ObservableProperty]
        private bool _isMirroring;

        partial void OnIsMirroringChanged(bool value)
        {
            if (!value)
            {
                return;
            }

            foreach (var pane in Panes)
            {
                pane.RaiseFit();
            }
        }

        private bool TryGetMirrorSource(out ComparePaneViewModel source, out ViewportSnapshot snapshot)
        {
            var original = Panes.FirstOrDefault(p => p.IsOriginal);
            if (original != null && _viewportSnapshots.TryGetValue(original, out snapshot))
            {
                source = original;
                return true;
            }

            if (_lastViewportSource != null && _viewportSnapshots.TryGetValue(_lastViewportSource, out snapshot))
            {
                source = _lastViewportSource;
                return true;
            }

            source = original ?? _lastViewportSource!;
            snapshot = default;
            return false;
        }

        [ObservableProperty]
        private string _sourceFileName = string.Empty;

        [ObservableProperty]
        private string _statusMessage = AppStrings.ComparePreparing;

        public CompareViewModel(
            string filePath,
            ICompareConversionService conversionService,
            IDispatcherService dispatcherService)
        {
            SourceFilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            SourceFileBytes = GetFileSize(filePath);
            _conversionService = conversionService ?? throw new ArgumentNullException(nameof(conversionService));
            _dispatcherService = dispatcherService ?? throw new ArgumentNullException(nameof(dispatcherService));
            _orchestrator = new ComparePipelineOrchestrator(_conversionService);
            SourceFileName = Path.GetFileName(filePath);
            OriginalFormat = GetOriginalFormat(filePath);

            var left = new ComparePaneViewModel(isOriginal: true)
            {
                Label = BuildOriginalLabel(filePath)
            };
            var middle = new ComparePaneViewModel(isOriginal: false);
            var right = new ComparePaneViewModel(isOriginal: false);
            left.FormatChanged += OnPaneFormatChanged;
            middle.FormatChanged += OnPaneFormatChanged;
            right.FormatChanged += OnPaneFormatChanged;
            Panes.Add(left);
            Panes.Add(middle);
            Panes.Add(right);
        }

        public async Task InitializeAsync()
        {
            if (_disposed)
            {
                return;
            }

            await Task.Run(() => _conversionService.ClearCompareCache()).ConfigureAwait(false);

            AssignDefaultFormats();
            RecomputeAvailableFormats();
            _initializing = false;
            SyncPaneQualityProperties();
            LeftPane.SetFileSizes(SourceFileBytes, null);
            _ = LoadQuickOriginalPreviewAsync();

            int threadsPerJob = CompareDefaults.JxlThreads;
            await Task.WhenAll(Panes.Select(pane =>
                RunPaneAsync(pane, pane.IsOriginal ? null : (int?)threadsPerJob))).ConfigureAwait(false);
        }

        public Task<string> EnsureFullResolutionAsync(ComparePaneViewModel pane)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(CompareViewModel));
            }

            return _conversionService.EnsureDisplayFullPngAsync(
                SourceFilePath,
                pane.Format,
                QualityFor(pane.Format),
                JxlEffort,
                _lifetimeCts.Token);
        }

        public void OnPaneViewportChanged(
            ComparePaneViewModel source,
            CompareViewport viewport,
            CompareImageRegion visibleRegion,
            int pixelWidth,
            int pixelHeight,
            int imagePixelWidth,
            int imagePixelHeight)
        {
            if (_disposed || imagePixelWidth <= 0 || imagePixelHeight <= 0)
            {
                return;
            }

            _lastViewportSource = source;
            _viewportSnapshots[source] = new ViewportSnapshot(
                viewport,
                visibleRegion,
                Math.Max(1, pixelWidth),
                Math.Max(1, pixelHeight),
                Math.Max(1, imagePixelWidth),
                Math.Max(1, imagePixelHeight));

            if (!IsMirroring || _applyingMirror)
            {
                return;
            }

            lock (_mirrorGuard)
            {
                if (_applyingMirror)
                {
                    return;
                }

                _applyingMirror = true;
                try
                {
                    foreach (var pane in Panes)
                    {
                        if (!ReferenceEquals(pane, source))
                        {
                            pane.RaiseSetViewport(viewport, imagePixelWidth);
                        }
                    }
                }
                finally
                {
                    _applyingMirror = false;
                }
            }
        }

        public void OnPaneDisplayStateChanged(ComparePaneViewModel pane, CompareDisplayState state)
        {
            if (_disposed)
            {
                return;
            }

            pane.DisplayState = state;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _reconvertTimer?.Stop();
            _reconvertTimer = null;
            _lifetimeCts.Cancel();
            _lifetimeCts.Dispose();

            _ = Task.Run(() => _conversionService.ClearCompareCache());

            foreach (var cts in _paneCts.Values)
            {
                cts.Cancel();
                cts.Dispose();
            }
            _paneCts.Clear();

            _viewportSnapshots.Clear();
        }

        private void OnPaneFormatChanged(object? sender, OutputFormat? format)
        {
            if (_initializing || _disposed || sender is not ComparePaneViewModel pane || format == null)
            {
                return;
            }

            var other = Panes.First(p => !p.IsOriginal && !ReferenceEquals(p, pane));
            if (other.Format == format)
            {
                var candidates = AllFormats.Where(f => f != format && f != OriginalFormat).ToList();
                if (candidates.Count > 0)
                {
                    other.Format = candidates[0];
                }
            }

            RecomputeAvailableFormats();
            SyncPaneQualityProperties();
            OnPropertyChanged(nameof(MiddleFormatText));
            OnPropertyChanged(nameof(RightFormatText));
            _ = RunPaneAsync(pane);
        }

        private void AssignDefaultFormats()
        {
            var candidates = AllFormats.Where(f => f != OriginalFormat).ToList();
            MiddlePane.Format = candidates[0];
            RightPane.Format = candidates[1];
        }

        private void RecomputeAvailableFormats()
        {
            if (_recomputingFormats)
            {
                return;
            }

            _recomputingFormats = true;
            try
            {
                foreach (var pane in Panes)
                {
                    if (pane.IsOriginal)
                    {
                        continue;
                    }

                    var other = Panes.First(p => !p.IsOriginal && !ReferenceEquals(p, pane));
                    var taken = new HashSet<OutputFormat>();
                    if (OriginalFormat != null)
                    {
                        taken.Add(OriginalFormat.Value);
                    }
                    if (other.Format != null)
                    {
                        taken.Add(other.Format.Value);
                    }

                    var options = AllFormats.Where(f => !taken.Contains(f)).ToList();
                    pane.AvailableFormats.Clear();
                    foreach (OutputFormat option in options)
                    {
                        pane.AvailableFormats.Add(option);
                    }

                    if (pane.Format != null && !options.Contains(pane.Format.Value) && options.Count > 0)
                    {
                        pane.Format = options[0];
                    }
                }

                SyncPaneQualityProperties();
                OnPropertyChanged(nameof(MiddleFormatText));
                OnPropertyChanged(nameof(RightFormatText));
            }
            finally
            {
                _recomputingFormats = false;
            }
        }

        private async Task RunPaneAsync(ComparePaneViewModel pane, int? threads = null)
        {
            var cts = CreatePaneCts(pane);
            var ct = cts.Token;

            await _dispatcherService.InvokeAsync(() =>
            {
                pane.ErrorMessage = null;
                pane.Status = pane.IsOriginal ? PaneStatus.Rendering : PaneStatus.Converting;
            });

            Bitmap? bitmap = null;
            try
            {
                CompareDisplayPngs display;
                long fileBytes = SourceFileBytes;

                if (ComparePipelineOrchestrator.IsParallelRenderEnabled)
                {
                    OutputFormat? pipelineFormat = pane.IsOriginal ? null : pane.Format;
                    int? pipelineThreads = pane.IsOriginal ? null : threads;
                    var result = await _orchestrator.RunPaneAsync(
                        SourceFilePath,
                        pipelineFormat,
                        QualityFor(pipelineFormat),
                        JxlEffort,
                        allowParallelRender: true,
                        ct,
                        pipelineThreads).ConfigureAwait(false);
                    ct.ThrowIfCancellationRequested();

                    display = result.Preview;
                    if (result.TargetPath != null)
                    {
                        fileBytes = GetFileSize(result.TargetPath);
                    }
                }
                else
                {
                    display = await _conversionService.EnsureDisplayPngsAsync(
                        SourceFilePath, pane.Format, QualityFor(pane.Format), JxlEffort, ct, threads).ConfigureAwait(false);
                    ct.ThrowIfCancellationRequested();

                    if (!pane.IsOriginal && pane.Format != null)
                    {
                        var target = await _conversionService.EnsureTargetFileAsync(
                            SourceFilePath, pane.Format.Value, QualityFor(pane.Format.Value), JxlEffort, ct, threads).ConfigureAwait(false);
                        ct.ThrowIfCancellationRequested();
                        fileBytes = GetFileSize(target);
                    }
                }

                bitmap = await Task.Run(() => new Bitmap(display.PreviewPath), ct).ConfigureAwait(false);
                ct.ThrowIfCancellationRequested();

                await _dispatcherService.InvokeAsync(() =>
                {
                    if (_disposed || ct.IsCancellationRequested ||
                        !_paneCts.TryGetValue(pane, out var current) || !ReferenceEquals(current, cts))
                    {
                        bitmap?.Dispose();
                        bitmap = null;
                        return;
                    }

                    pane.Preview = bitmap;
                    bitmap = null;
                    pane.FullResPath = display.FullPath;
                    pane.DimensionsText = $"{display.Width}×{display.Height}";
                    pane.SetFileSizes(fileBytes, pane.IsOriginal ? null : SourceFileBytes);
                    pane.Status = PaneStatus.Ready;
                    if (IsMirroring && TryGetMirrorSource(out var mirrorSource, out var mirrorSnapshot))
                    {
                        if (!ReferenceEquals(mirrorSource, pane))
                        {
                            pane.RaiseSetViewport(mirrorSnapshot.Viewport, mirrorSnapshot.ImagePixelWidth);
                        }
                    }
                }).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                bitmap?.Dispose();
            }
            catch (Exception ex)
            {
                bitmap?.Dispose();
                await _dispatcherService.InvokeAsync(() =>
                {
                    if (_disposed || ct.IsCancellationRequested ||
                        !_paneCts.TryGetValue(pane, out var current) || !ReferenceEquals(current, cts))
                    {
                        return;
                    }

                    pane.ErrorMessage = FormatCompareError(ex);
                    pane.Status = PaneStatus.Error;
                }).ConfigureAwait(false);
            }
            finally
            {
                await _dispatcherService.InvokeAsync(() =>
                {
                    if (!_disposed)
                    {
                        UpdateStatusMessage();
                    }
                    if (_paneCts.TryGetValue(pane, out var current) && ReferenceEquals(current, cts))
                    {
                        _paneCts.TryRemove(new KeyValuePair<ComparePaneViewModel, CancellationTokenSource>(pane, cts));
                        cts.Dispose();
                    }
                }).ConfigureAwait(false);
            }
        }

        private async Task LoadQuickOriginalPreviewAsync()
        {
            Bitmap? bitmap = null;
            try
            {
                string? path = await _conversionService
                    .EnsureQuickPreviewAsync(SourceFilePath, _lifetimeCts.Token)
                    .ConfigureAwait(false);
                if (string.IsNullOrEmpty(path))
                {
                    return;
                }

                bitmap = await Task.Run(() => new Bitmap(path), _lifetimeCts.Token).ConfigureAwait(false);
                await _dispatcherService.InvokeAsync(() =>
                {
                    if (_disposed || LeftPane.Preview != null)
                    {
                        bitmap?.Dispose();
                        bitmap = null;
                        return;
                    }

                    LeftPane.Preview = bitmap;
                    bitmap = null;
                }).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                bitmap?.Dispose();
            }
            catch
            {
                bitmap?.Dispose();
            }
        }

        private static string FormatCompareError(Exception exception)
        {
            if (IsMissingCompareTool(exception))
            {
                return AppStrings.CompareToolMissing;
            }

            return $"{AppStrings.CompareErrorPrefix}{exception.Message}";
        }

        private static bool IsMissingCompareTool(Exception exception)
        {
            for (Exception? current = exception; current != null; current = current.InnerException)
            {
                if (current is FileNotFoundException &&
                    (current.Message.Contains("cjxl", StringComparison.OrdinalIgnoreCase) ||
                     current.Message.Contains("djxl", StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }

            return false;
        }

        private CancellationTokenSource CreatePaneCts(ComparePaneViewModel pane)
        {
            if (_paneCts.TryGetValue(pane, out var old))
            {
                old.Cancel();
                old.Dispose();
            }

            var linked = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCts.Token);
            _paneCts[pane] = linked;
            return linked;
        }

        private void ScheduleReconvert()
        {
            if (_reconvertTimer == null)
            {
                _reconvertTimer = new DispatcherTimer(
                    TimeSpan.FromMilliseconds(300),
                    DispatcherPriority.Background,
                    OnReconvertTick);
            }
            else
            {
                _reconvertTimer.Stop();
            }

            _reconvertTimer.Start();
        }

        private void OnReconvertTick(object? sender, EventArgs e)
        {
            _reconvertTimer?.Stop();
            bool effort = _effortPending;
            _effortPending = false;
            var pendingFormats = new HashSet<OutputFormat>(_pendingQualityFormats);
            _pendingQualityFormats.Clear();

            var affected = Panes
                .Where(pane => !pane.IsOriginal &&
                               ((effort && pane.Format == OutputFormat.Jxl) ||
                                pane.Format is { } format && pendingFormats.Contains(format)))
                .ToList();
            int threadsPerJob = CompareDefaults.JxlThreads;
            foreach (var pane in affected)
            {
                _ = RunPaneAsync(pane, threadsPerJob);
            }
        }

        internal void TriggerReconvertTick()
        {
            OnReconvertTick(null, EventArgs.Empty);
        }

        private void UpdateStatusMessage()
        {
            int ready = Panes.Count(p => p.Status == PaneStatus.Ready);
            int failed = Panes.Count(p => p.Status == PaneStatus.Error);
            if (failed > 0)
            {
                StatusMessage = string.Format(AppStrings.CompareReadyWithErrors, ready, Panes.Count, failed);
            }
            else
            {
                StatusMessage = string.Format(AppStrings.CompareReadyProgress, ready, Panes.Count);
            }
        }

        private static OutputFormat? GetOriginalFormat(string filePath)
        {
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            if (SupportedFormats.IsJxlFile(ext))
            {
                return OutputFormat.Jxl;
            }
            if (ext == ".jpg" || ext == ".jpeg")
            {
                return OutputFormat.Jpeg;
            }
            if (ext == ".avif")
            {
                return OutputFormat.Avif;
            }
            return null;
        }

        private static string BuildOriginalLabel(string filePath)
        {
            string ext = Path.GetExtension(filePath).TrimStart('.').ToUpperInvariant();
            return string.IsNullOrEmpty(ext)
                ? AppStrings.CompareOriginal
                : $"{AppStrings.CompareOriginal} ({ext})";
        }

        private static long GetFileSize(string path)
        {
            try
            {
                return new FileInfo(path).Length;
            }
            catch
            {
                return 0;
            }
        }

        private readonly record struct ViewportSnapshot(
            CompareViewport Viewport,
            CompareImageRegion Region,
            int PixelWidth,
            int PixelHeight,
            int ImagePixelWidth,
            int ImagePixelHeight);
    }
}