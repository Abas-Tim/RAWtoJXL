using Avalonia.Controls;
using ARWtoJXL.Avalonia.Services;
using ARWtoJXL.Avalonia.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace ARWtoJXL.Avalonia
{
    public partial class SettingsWindow : Window
    {
        public SettingsViewModel Settings { get; }

        public SettingsWindow()
        {
            InitializeComponent();
            var filePicker = App.Services!.GetRequiredService<IFilePickerService>();
            Settings = new SettingsViewModel(filePicker);
            Settings.RequestClose += (s, e) => this.Close();
            DataContext = Settings;
            Closing += (_, _) => Settings.Dispose();
        }
    }
}
