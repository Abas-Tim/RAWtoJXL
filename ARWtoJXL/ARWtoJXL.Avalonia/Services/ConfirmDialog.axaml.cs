using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace ARWtoJXL.Avalonia.Services
{
    public partial class ConfirmDialog : Window, INotifyPropertyChanged
    {
        public new event PropertyChangedEventHandler? PropertyChanged;

        private string _messageText = string.Empty;
        public string MessageText
        {
            get => _messageText;
            set { _messageText = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MessageText))); }
        }

        private string _titleText = string.Empty;
        public string TitleText
        {
            get => _titleText;
            set { _titleText = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TitleText))); }
        }

        public ConfirmDialog()
        {
            AvaloniaXamlLoader.Load(this);
            DataContext = this;
        }

        private void YesButton_Click(object? sender, RoutedEventArgs e)
        {
            Close(true);
        }

        private void NoButton_Click(object? sender, RoutedEventArgs e)
        {
            Close(false);
        }
    }
}
