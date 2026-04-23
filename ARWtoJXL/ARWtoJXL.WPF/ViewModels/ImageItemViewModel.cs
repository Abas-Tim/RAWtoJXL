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

        [ObservableProperty]
        private System.Windows.Media.Imaging.BitmapImage? _thumbnail;

        [ObservableProperty]
        private string? _errorMessage;

        [ObservableProperty]
        private bool _isSelected;

        [ObservableProperty]
        private int? _qualityOverride;

        public int EffectiveQuality(int globalQuality)
        {
            return QualityOverride ?? globalQuality;
        }
    }
}
