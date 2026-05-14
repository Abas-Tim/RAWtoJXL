using System;
using System.IO;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;

using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ARWtoJXL.Avalonia.ViewModels;

namespace ARWtoJXL.Avalonia
{
    public partial class MainWindow : Window
    {
        private SettingsWindow? _settingsWindow;
        private DispatcherTimer? _recentCloseTimer;
        private bool _isRecentHovered;
        private bool _isPopupHovered;

        public MainWindow()
        {
            InitializeComponent();
            if (DataContext is MainViewModel vm)
            {
                vm.RequestRefreshLayout += () =>
                {
                    var repeater = this.GetControl<ItemsRepeater>("ImagesRepeater");
                    repeater.UpdateLayout();
                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        repeater.Layout = new UniformGridLayout
                        {
                            MinItemWidth = 200,
                            MinColumnSpacing = 8,
                            MinRowSpacing = 8
                        };
                        repeater.UpdateLayout();
                    });
                };
            }
        }

        private void RecentPointerEntered(object? sender, PointerEventArgs e)
        {
            _isRecentHovered = true;
            CancelRecentClose();
            UpdateIsRecentHovered();
        }

        private void RecentPointerExited(object? sender, PointerEventArgs e)
        {
            _isRecentHovered = false;
            ScheduleRecentClose();
        }

        private void RecentPopupPointerEntered(object? sender, PointerEventArgs e)
        {
            _isPopupHovered = true;
            CancelRecentClose();
            UpdateIsRecentHovered();
        }

        private void RecentPopupPointerExited(object? sender, PointerEventArgs e)
        {
            _isPopupHovered = false;
            ScheduleRecentClose();
        }

        private void RecentMenuItemClicked(object? sender, RoutedEventArgs e)
        {
            e.Handled = true;
        }

        private void CancelRecentClose()
        {
            _recentCloseTimer?.Stop();
            _recentCloseTimer = null;
        }

        private void ScheduleRecentClose()
        {
            CancelRecentClose();
            _recentCloseTimer = new DispatcherTimer(
                TimeSpan.FromMilliseconds(200),
                DispatcherPriority.Background,
                OnRecentCloseTimer);
            _recentCloseTimer.Start();
        }

        private void OnRecentCloseTimer(object? sender, EventArgs e)
        {
            _recentCloseTimer?.Stop();
            _recentCloseTimer = null;
            if (!_isRecentHovered && !_isPopupHovered && DataContext is MainViewModel vm)
            {
                vm.IsRecentHovered = false;
            }
        }

        private void UpdateIsRecentHovered()
        {
            if (DataContext is MainViewModel vm)
            {
                vm.IsRecentHovered = _isRecentHovered || _isPopupHovered;
            }
        }

        private async void RecentFileClicked(object? sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button btn && btn.Tag is string filePath && DataContext is MainViewModel vm)
                {
                    if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                    {
                        await vm.AddFilesAsync(new[] { filePath });
                    }
                    _isRecentHovered = false;
                    _isPopupHovered = false;
                    vm.IsRecentHovered = false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RecentFileClicked error: {ex}");
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
