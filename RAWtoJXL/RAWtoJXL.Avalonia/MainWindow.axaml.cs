using System;
using System.IO;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;

using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using RAWtoJXL.Avalonia.Services;
using RAWtoJXL.Avalonia.ViewModels;
using RAWtoJXL.Core.Interfaces;

namespace RAWtoJXL.Avalonia
{
    public partial class MainWindow : Window
    {
        private SettingsWindow? _settingsWindow;
        private CompareWindow? _compareWindow;
        private MainViewModel? _wiredViewModel;
        private DispatcherTimer? _recentCloseTimer;
        private bool _isRecentHovered;
        private bool _isPopupHovered;
        private bool _isPopupClickInProgress;

        public MainWindow()
        {
            InitializeComponent();
        }

        internal CompareWindow? CompareToolWindow => _compareWindow;

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            WireViewModel();
        }

        private void WireViewModel()
        {
            var vm = DataContext as MainViewModel;
            if (ReferenceEquals(vm, _wiredViewModel))
            {
                return;
            }

            if (_wiredViewModel != null)
            {
                _wiredViewModel.RequestOpenCompare -= OpenCompareWindow;
                _wiredViewModel.RequestRefreshLayout -= RefreshImagesLayout;
            }

            _wiredViewModel = vm;
            if (vm == null)
            {
                return;
            }

            vm.RequestOpenCompare += OpenCompareWindow;
            vm.RequestRefreshLayout += RefreshImagesLayout;
        }

        private void RefreshImagesLayout()
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

        private void RecentPopupPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            _isPopupClickInProgress = true;
        }

        private void RecentPopupPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            _isPopupClickInProgress = false;
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
            if (_isPopupClickInProgress)
                return;
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

        public void OpenCompareWindow(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                return;
            }

            if (_compareWindow != null && _compareWindow.IsVisible)
            {
                _compareWindow.Activate();
                return;
            }

            var conversionService = App.Services?.GetService<ICompareConversionService>();
            var dispatcherService = App.Services?.GetService<IDispatcherService>();
            if (conversionService == null || dispatcherService == null)
            {
                return;
            }

            var viewModel = new CompareViewModel(filePath, conversionService, dispatcherService);
            _compareWindow = new CompareWindow
            {
                DataContext = viewModel
            };
            _compareWindow.Closed += (_, _) => _compareWindow = null;
            _compareWindow.Show();
        }
    }
}
