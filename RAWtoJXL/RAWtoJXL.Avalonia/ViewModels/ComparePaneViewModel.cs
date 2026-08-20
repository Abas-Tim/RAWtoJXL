using System;
using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using RAWtoJXL.Avalonia.Controls;
using RAWtoJXL.Core.Interfaces;

namespace RAWtoJXL.Avalonia.ViewModels
{
    public enum PaneStatus
    {
        Loading,
        Rendering,
        Converting,
        Ready,
        Error
    }

    public partial class ComparePaneViewModel : ObservableObject
    {
        [ObservableProperty]
        private OutputFormat? _format;

        [ObservableProperty]
        private string _label = string.Empty;

        [ObservableProperty]
        private ObservableCollection<OutputFormat> _availableFormats = new();

        [ObservableProperty]
        private PaneStatus _status = PaneStatus.Loading;

        [ObservableProperty]
        private string? _errorMessage;

        [ObservableProperty]
        private string _fileSizeText = string.Empty;

        [ObservableProperty]
        private string? _savingsText;

        [ObservableProperty]
        private string? _dimensionsText;

        [ObservableProperty]
        private Bitmap? _preview;

        [ObservableProperty]
        private string? _fullResPath;

        [ObservableProperty]
        private CompareDisplayState _displayState = CompareDisplayState.Preview;

        [ObservableProperty]
        private double? _viewportSsim;

        [ObservableProperty]
        private bool _isAnalyzing;

        partial void OnStatusChanged(PaneStatus value)
        {
            OnPropertyChanged(nameof(IsProcessing));
            OnPropertyChanged(nameof(IsConverting));
            OnPropertyChanged(nameof(IsError));
        }

        partial void OnFormatChanged(OutputFormat? value)
        {
            if (!IsOriginal)
            {
                Label = value?.ToString().ToUpperInvariant() ?? string.Empty;
            }

            FormatChanged?.Invoke(this, value);
        }

        partial void OnDisplayStateChanged(CompareDisplayState value)
        {
            OnPropertyChanged(nameof(DisplayStateText));
        }

        partial void OnViewportSsimChanged(double? value)
        {
            OnPropertyChanged(nameof(SsimText));
        }

        partial void OnIsAnalyzingChanged(bool value)
        {
            OnPropertyChanged(nameof(SsimText));
        }

        public bool IsOriginal { get; }

        public bool IsConverting => Status == PaneStatus.Converting;

        public bool IsProcessing => Status == PaneStatus.Rendering || Status == PaneStatus.Converting;

        public string ProcessingText => IsOriginal ? "Rendering original..." : "Converting...";

        public bool IsError => Status == PaneStatus.Error;

        public string DisplayStateText => DisplayState == CompareDisplayState.Full ? "Full" : "Preview";

        public string SsimText => IsOriginal
            ? string.Empty
            : IsAnalyzing
                ? "SSIM..."
                : ViewportSsim is double value
                    ? $"SSIM {value:F4}"
                    : "SSIM --";

        public event EventHandler<OutputFormat?>? FormatChanged;

        public event Action<CompareViewport>? RequestSetViewport;

        public event Action? RequestFit;

        public event Action<Bitmap?, CompareImageRegion>? RequestSetDifferenceOverlay;

        public ComparePaneViewModel(bool isOriginal)
        {
            IsOriginal = isOriginal;
        }

        public void RaiseSetViewport(CompareViewport viewport)
        {
            RequestSetViewport?.Invoke(viewport);
        }

        public void RaiseFit()
        {
            RequestFit?.Invoke();
        }

        public void RaiseSetDifferenceOverlay(Bitmap? bitmap, CompareImageRegion region)
        {
            var request = RequestSetDifferenceOverlay;
            if (request == null)
            {
                bitmap?.Dispose();
                return;
            }

            request(bitmap, region);
        }

        public void SetFileSizes(long bytes, long? originalBytes)
        {
            FileSizeText = FormatBytes(bytes);
            if (IsOriginal || originalBytes == null || originalBytes.Value <= 0)
            {
                SavingsText = null;
                return;
            }

            var saved = originalBytes.Value - bytes;
            var pct = saved * 100.0 / originalBytes.Value;
            SavingsText = $"({(pct >= 0 ? "-" : "+")}{Math.Abs(pct):F0}%)";
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes >= 1_000_000)
                return $"{bytes / 1000_000.0:F1} MB";
            if (bytes >= 1_000)
                return $"{bytes / 1000.0:F1} KB";
            return $"{bytes} B";
        }
    }
}
