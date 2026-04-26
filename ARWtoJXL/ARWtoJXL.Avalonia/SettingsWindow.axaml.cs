using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ARWtoJXL.Avalonia.Services;
using ARWtoJXL.Avalonia.ViewModels;

namespace ARWtoJXL.Avalonia
{
    public partial class SettingsWindow : Window
    {
        public SettingsViewModel Settings { get; }

        public SettingsWindow()
        {
            AvaloniaXamlLoader.Load(this);
            var filePicker = new FilePickerService();
            Settings = new SettingsViewModel(filePicker);
            Settings.RequestClose += (s, e) => this.Close();
            DataContext = Settings;
        }
    }
}
