using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using RAWtoJXL.Avalonia.Services;
using RAWtoJXL.Avalonia.ViewModels;

namespace RAWtoJXL.Avalonia.Controls
{
    public partial class SettingsPanelView : UserControl
    {
        public SettingsViewModel Settings { get; }

        public event EventHandler? RequestClose;

        public SettingsPanelView()
        {
            InitializeComponent();
            var filePicker = App.Services!.GetRequiredService<IFilePickerService>();
            Settings = new SettingsViewModel(filePicker);
            Settings.RequestClose += (_, _) => RequestClose?.Invoke(this, EventArgs.Empty);
            DataContext = Settings;
        }

        private void OnCloseSettingsClicked(object? sender, RoutedEventArgs e)
        {
            RequestClose?.Invoke(this, EventArgs.Empty);
        }
    }
}
