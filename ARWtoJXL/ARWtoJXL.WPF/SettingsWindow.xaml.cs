using System.Windows;
using ARWtoJXL.WPF.ViewModels;

namespace ARWtoJXL.WPF
{
    public partial class SettingsWindow : Window
    {
        public SettingsViewModel Settings { get; }

        public SettingsWindow()
        {
            InitializeComponent();
            Settings = new SettingsViewModel();
            Settings.RequestClose += (s, e) => this.Close();
            DataContext = Settings;
        }
    }
}
