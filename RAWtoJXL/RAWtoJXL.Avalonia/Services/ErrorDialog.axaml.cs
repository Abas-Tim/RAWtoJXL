using Avalonia.Controls;
using Avalonia.Interactivity;

namespace RAWtoJXL.Avalonia.Services
{
    public partial class ErrorDialog : Window
    {
        public string MessageText
        {
            get => _messageText;
            set { _messageText = value; Title = value; }
        }
        private string _messageText = string.Empty;

        public string TitleText
        {
            get => Title;
            set => Title = value;
        }

        public ErrorDialog()
        {
            InitializeComponent();
        }

        private void OkButton_Click(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
