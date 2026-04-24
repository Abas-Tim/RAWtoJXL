using System.Windows;

namespace ARWtoJXL.WPF.Services
{
    public partial class ConfirmDialog : Window
    {
        public string MessageText
        {
            get => (string)GetValue(MessageTextProperty);
            set => SetValue(MessageTextProperty, value);
        }
        public static readonly DependencyProperty MessageTextProperty =
            DependencyProperty.Register(nameof(MessageText), typeof(string), typeof(ConfirmDialog));

        public string TitleText
        {
            get => (string)GetValue(TitleTextProperty);
            set => SetValue(TitleTextProperty, value);
        }
        public static readonly DependencyProperty TitleTextProperty =
            DependencyProperty.Register(nameof(TitleText), typeof(string), typeof(ConfirmDialog));

        public ConfirmDialog()
        {
            InitializeComponent();
        }

        private void YesButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void NoButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
