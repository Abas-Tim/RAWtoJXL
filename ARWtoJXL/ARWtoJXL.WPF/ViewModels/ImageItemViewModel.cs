using ARWtoJXL.Core.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ARWtoJXL.WPF.ViewModels
{
    public partial class ImageItemViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _filePath = string.Empty;

        [ObservableProperty]
        private string _fileName = string.Empty;

        [ObservableProperty]
        private ImageStatus _status;

        partial void OnStatusChanged(ImageStatus value)
        {
            OnPropertyChanged(nameof(SizeInfoText));
        }

        [ObservableProperty]
        private System.Windows.Media.Imaging.BitmapImage? _thumbnail;

        [ObservableProperty]
        private string? _errorMessage;

        [ObservableProperty]
        private bool _isSelected;

        [ObservableProperty]
        private int? _qualityOverride;

        [ObservableProperty]
        private bool _isRemoved;

        [ObservableProperty]
        private long _sourceFileSize;

        partial void OnSourceFileSizeChanged(long value)
        {
            OnPropertyChanged(nameof(SizeInfoText));
        }

        [ObservableProperty]
        private long _outputFileSize;

        partial void OnOutputFileSizeChanged(long value)
        {
            OnPropertyChanged(nameof(SizeInfoText));
        }

        [ObservableProperty]
        private string _outputPath = string.Empty;

        public string SizeInfoText
        {
            get
            {
                if (Status != ImageStatus.Converted || SourceFileSize == 0 || OutputFileSize == 0)
                    return string.Empty;

                var saved = SourceFileSize - OutputFileSize;
                var pct = (saved * 100.0) / SourceFileSize;
                return $"{FormatBytes(OutputFileSize)} ({(pct >= 0 ? "-" : "+")}{Math.Abs(pct):F0}%)";
            }
        }

        public int EffectiveQuality(int globalQuality)
        {
            return QualityOverride ?? globalQuality;
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
