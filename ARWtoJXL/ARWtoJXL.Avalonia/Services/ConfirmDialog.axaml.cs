using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ARWtoJXL.Avalonia.Services
{
    public partial class ConfirmDialog : Window
    {
        private readonly ConfirmDialogViewModel _viewModel;

        public string MessageText
        {
            get => _viewModel.MessageText;
            set => _viewModel.MessageText = value;
        }

        public string TitleText
        {
            get => _viewModel.TitleText;
            set
            {
                _viewModel.TitleText = value;
                Title = value;
            }
        }

        public ConfirmDialog()
        {
            _viewModel = new ConfirmDialogViewModel();
            AvaloniaXamlLoader.Load(this);
            DataContext = _viewModel;
        }

        private void YesButton_Click(object? sender, RoutedEventArgs e)
        {
            Close(true);
        }

        private void NoButton_Click(object? sender, RoutedEventArgs e)
        {
            Close(false);
        }

        public partial class ConfirmDialogViewModel : ObservableObject
        {
            [ObservableProperty]
            private string _messageText = string.Empty;

            [ObservableProperty]
            private string _titleText = string.Empty;
        }
    }
}
