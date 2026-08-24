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
        private readonly Dictionary<ComparePaneViewModel, CancellationTokenSource> _paneCts = new();
        private readonly Dictionary<ComparePaneViewModel, CancellationTokenSource> _analysisCts = new();
        private readonly Dictionary<ComparePaneViewModel, ViewportSnapshot> _viewportSnapshots = new();
    private ComparePaneViewModel? _lastViewportSource;
        private readonly object _mirrorGuard = new();
        private const int AnalysisDebounceMs = 300;

        private bool _initializing = true;
        private bool _applyingMirror;
        private bool _qualityPending;
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
        private int _quality;

        partial void OnQualityChanged(int value)
        {
            if (_initializing || _disposed)
            {
                return;
            }

            _qualityPending = true;
            ScheduleReconvert();
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

            var sourceSnapshot = _viewportSnapshots.Values.FirstOrDefault();
            if (sourceSnapshot.PixelWidth <= 0)
            {
                foreach (var pane in Panes)
                {
                    pane.RaiseFit();
                }

                return;
            }

            foreach (var pane in Panes)
            {
                pane.RaiseSetViewport(NormalizeViewportForPane(pane, sourceSnapshot.Viewport, sourceSnapshot.PixelWidth));
            }
        }

        [ObservableProperty]
        private bool _isDifferenceOverlayEnabled;

        [ObservableProperty]
        private bool _isGpuPrototypeVisible = true;

        [ObservableProperty]
        private bool _isGpuPrototypeAvailable = true;

        internal void SetGpuPrototypeAvailability(bool available)
        {
            IsGpuPrototypeAvailable = available;
            if (!available)
            {
                IsGpuPrototypeVisible = false;
            }
        }

        partial void OnIsDifferenceOverlayEnabledChanged(bool value)
        {
            if (_disposed)
            {
                return;
            }

            foreach (var pane in Panes.Where(p => !p.IsOriginal))
            {
                CancelAnalysis(pane);
                pane.RaiseSetDifferenceOverlay(null, default);
                ScheduleAnalysis(pane);
            }
        }

        [ObservableProperty]
        private string _sourceFileName = string.Empty;

        [ObservableProperty]
        private string _statusMessage = AppStrings.ComparePreparing;

        public CompareViewModel(
            string filePath,
            int quality,
            ICompareConversionService conversionService,
            IDispatcherService dispatcherService)
        {
            SourceFilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            SourceFileBytes = GetFileSize(filePath);
            _conversionService = conversionService ?? throw new ArgumentNullException(nameof(conversionService));
            _dispatcherService = dispatcherService ?? throw new ArgumentNullException(nameof(dispatcherService));
            _orchestrator = new ComparePipelineOrchestrator(_conversionService);
            _quality = quality;
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

            AssignDefaultFormats();
            RecomputeAvailableFormats();
            _initializing = false;
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
                Quality,
                JxlEffort,
                _lifetimeCts.Token);
        }

        public void OnPaneViewportChanged(
            ComparePaneViewModel source,
            CompareViewport viewport,
            CompareImageRegion visibleRegion,
            int pixelWidth,
            int pixelHeight)
        {
            if (_disposed)
            {
                return;
            }

            _lastViewportSource = source;
            _viewportSnapshots[source] = new ViewportSnapshot(
                viewport,
                visibleRegion,
                Math.Max(1, pixelWidth),
                Math.Max(1, pixelHeight));
            if (!source.IsOriginal)
            {
                CancelAnalysis(source);
                source.RaiseSetDifferenceOverlay(null, default);
                ScheduleAnalysis(source);
            }

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
                            pane.RaiseSetViewport(NormalizeViewportForPane(pane, viewport, pixelWidth));
                        }
                    }
                }
                finally
                {
                    _applyingMirror = false;
                }
            }
        }

        private CompareViewport NormalizeViewportForPane(
            ComparePaneViewModel pane,
            CompareViewport viewport,
            int sourcePixelWidth)
        {
            int targetPixelWidth = pane.Preview?.PixelSize.Width ?? sourcePixelWidth;
            if (targetPixelWidth <= 0 || sourcePixelWidth <= 0)
            {
                return viewport;
            }

            double scale = (double)sourcePixelWidth / targetPixelWidth;
            return new CompareViewport(
                Math.Clamp(viewport.Zoom * scale, CompareViewport.MinZoom, CompareViewport.MaxZoom),
                viewport.CenterX,
                viewport.CenterY);
        }

        public void OnPaneDisplayStateChanged(ComparePaneViewModel pane, CompareDisplayState state)
        {
            if (_disposed)
            {
                return;
            }

            pane.DisplayState = state;
            if (!pane.IsOriginal)
            {
                CancelAnalysis(pane);
                pane.RaiseSetDifferenceOverlay(null, default);
                ScheduleAnalysis(pane);
            }
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

            foreach (var cts in _paneCts.Values)
            {
                cts.Cancel();
                cts.Dispose();
            }
            _paneCts.Clear();

            foreach (var cts in _analysisCts.Values)
            {
                cts.Cancel();
                cts.Dispose();
            }
            _analysisCts.Clear();
            _viewportSnapshots.Clear();
            foreach (var pane in Panes)
            {
                pane.IsAnalyzing = false;
            }
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
                    pane.AvailableFormats = new ObservableCollection<OutputFormat>(options);

                    if (pane.Format != null && !options.Contains(pane.Format.Value) && options.Count > 0)
                    {
                        pane.Format = options[0];
                    }
                }
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
            CancelAnalysis(pane);

            await _dispatcherService.InvokeAsync(() =>
            {
                pane.ViewportSsim = null;
                pane.IsAnalyzing = false;
                pane.RaiseSetDifferenceOverlay(null, default);
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
                        Quality,
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
                        SourceFilePath, pane.Format, Quality, JxlEffort, ct, threads).ConfigureAwait(false);
                    ct.ThrowIfCancellationRequested();

                    if (!pane.IsOriginal && pane.Format != null)
                    {
                        var target = await _conversionService.EnsureTargetFileAsync(
                            SourceFilePath, pane.Format.Value, Quality, JxlEffort, ct, threads).ConfigureAwait(false);
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
                    if (IsMirroring &&
                        _lastViewportSource != null &&
                        !ReferenceEquals(_lastViewportSource, pane) &&
                        _viewportSnapshots.TryGetValue(_lastViewportSource, out var mirrorSnapshot) &&
                        mirrorSnapshot.PixelWidth > 0)
                    {
                        pane.RaiseSetViewport(NormalizeViewportForPane(pane, mirrorSnapshot.Viewport, mirrorSnapshot.PixelWidth));
                    }

                    ScheduleAnalysis(pane);
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
                        _paneCts.Remove(pane);
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

        private void ScheduleAnalysis(ComparePaneViewModel pane)
        {
            if (_disposed || pane.IsOriginal || pane.Status != PaneStatus.Ready || pane.Format == null ||
                !_viewportSnapshots.TryGetValue(pane, out var snapshot))
            {
                return;
            }

            CancelAnalysis(pane);
            pane.ViewportSsim = null;
            pane.IsAnalyzing = true;
            pane.RaiseSetDifferenceOverlay(null, default);

            var cts = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCts.Token);
            _analysisCts[pane] = cts;
            var request = new AnalysisRequest(
                pane.Format.Value,
                Quality,
                JxlEffort,
                pane.DisplayState == CompareDisplayState.Full,
                IsDifferenceOverlayEnabled,
                snapshot);
            _ = AnalyzeAfterDelayAsync(pane, request, cts);
        }

        private async Task AnalyzeAfterDelayAsync(
            ComparePaneViewModel pane,
            AnalysisRequest request,
            CancellationTokenSource requestCts)
        {
            Bitmap? overlay = null;
            CancellationToken cancellationToken = requestCts.Token;
            try
            {
                await Task.Delay(AnalysisDebounceMs, cancellationToken).ConfigureAwait(false);
                var result = await _conversionService.AnalyzeViewportAsync(
                    SourceFilePath,
                    request.Format,
                    request.Quality,
                    request.Effort,
                    request.Snapshot.Region,
                    request.UseFullResolution,
                    request.Snapshot.PixelWidth,
                    request.Snapshot.PixelHeight,
                    request.IncludeDifference,
                    cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();

                if (request.IncludeDifference && result.DifferencePng is { Length: > 0 })
                {
                    overlay = await Task.Run(() =>
                    {
                        using var stream = new MemoryStream(result.DifferencePng, writable: false);
                        return new Bitmap(stream);
                    }, cancellationToken).ConfigureAwait(false);
                }

                await _dispatcherService.InvokeAsync(() =>
                {
                    if (_disposed || !_analysisCts.TryGetValue(pane, out var current) ||
                        !ReferenceEquals(current, requestCts) || cancellationToken.IsCancellationRequested)
                    {
                        overlay?.Dispose();
                        overlay = null;
                        return;
                    }

                    pane.ViewportSsim = result.Ssim;
                    pane.IsAnalyzing = false;
                    pane.RaiseSetDifferenceOverlay(overlay, result.Region);
                    overlay = null;
                    _analysisCts.Remove(pane);
                    requestCts.Dispose();
                }).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                overlay?.Dispose();
                await _dispatcherService.InvokeAsync(() =>
                {
                    if (_analysisCts.TryGetValue(pane, out var current) && ReferenceEquals(current, requestCts))
                    {
                        pane.IsAnalyzing = false;
                        pane.RaiseSetDifferenceOverlay(null, default);
                        _analysisCts.Remove(pane);
                        requestCts.Dispose();
                    }
                }).ConfigureAwait(false);
            }
            catch
            {
                overlay?.Dispose();
                await _dispatcherService.InvokeAsync(() =>
                {
                    if (_analysisCts.TryGetValue(pane, out var current) && ReferenceEquals(current, requestCts))
                    {
                        pane.ViewportSsim = null;
                        pane.IsAnalyzing = false;
                        pane.RaiseSetDifferenceOverlay(null, default);
                        _analysisCts.Remove(pane);
                        requestCts.Dispose();
                    }
                }).ConfigureAwait(false);
            }
        }

        private void CancelAnalysis(ComparePaneViewModel pane)
        {
            if (_analysisCts.Remove(pane, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
            }

            pane.IsAnalyzing = false;
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
            bool quality = _qualityPending;
            bool effort = _effortPending;
            _qualityPending = false;
            _effortPending = false;

            var affected = Panes
                .Where(pane => !pane.IsOriginal && (quality || (effort && pane.Format == OutputFormat.Jxl)))
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
            int PixelHeight);

        private readonly record struct AnalysisRequest(
            OutputFormat Format,
            int Quality,
            int Effort,
            bool UseFullResolution,
            bool IncludeDifference,
            ViewportSnapshot Snapshot);
    }
}
