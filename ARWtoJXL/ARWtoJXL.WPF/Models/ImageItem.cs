using System.ComponentModel;
using ARWtoJXL.Core.Interfaces;
using ARWtoJXL.Core.Services;

namespace ARWtoJXL.WPF.Models
{
    public class ImageItem : INotifyPropertyChanged
    {
        private string _filePath = string.Empty;
        private string _fileName = string.Empty;
        private ImageStatus _status;
        private System.Windows.Media.Imaging.BitmapImage? _thumbnail;
        private string? _errorMessage;
        private bool _isSelected;

        public string FilePath { get => _filePath; set { _filePath = value; OnPropertyChanged(nameof(FilePath)); } }
        public string FileName { get => _fileName; set { _fileName = value; OnPropertyChanged(nameof(FileName)); } }
        public ImageStatus Status { get => _status; set { _status = value; OnPropertyChanged(nameof(Status)); } }
        public System.Windows.Media.Imaging.BitmapImage? Thumbnail { get => _thumbnail; set { _thumbnail = value; OnPropertyChanged(nameof(Thumbnail)); } }
        public string? ErrorMessage { get => _errorMessage; set { _errorMessage = value; OnPropertyChanged(nameof(ErrorMessage)); } }
        public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); } }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
