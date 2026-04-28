using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ARWtoJXL.Avalonia.ViewModels;

namespace ARWtoJXL.Avalonia
{
    public partial class MainWindow : Window
    {
        private SettingsWindow? _settingsWindow;

        public MainWindow()
        {
            InitializeComponent();
            var listBox = this.FindControl<ListBox>("ImagesListBox");
            if (listBox != null)
            {
                listBox.SelectionMode = SelectionMode.Multiple;
            }
        }

        public void OpenSettings()
        {
            if (_settingsWindow == null || !_settingsWindow.IsVisible)
            {
                var viewModel = DataContext as MainViewModel;
                if (viewModel == null) return;

                _settingsWindow = new SettingsWindow();
                _settingsWindow.Closed += (s, args) =>
                {
                    viewModel.RefreshSettings();
                    _settingsWindow = null;
                };

                _settingsWindow.ShowDialog(this);
            }
            else
            {
                _settingsWindow.Activate();
            }
        }
    }
}
