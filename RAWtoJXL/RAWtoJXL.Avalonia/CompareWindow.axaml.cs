using System;
using Avalonia.Controls;
using RAWtoJXL.Avalonia.Controls;
using RAWtoJXL.Avalonia.ViewModels;

namespace RAWtoJXL.Avalonia
{
    public partial class CompareWindow : Window
    {
        private bool _wired;

        public CompareWindow()
        {
            InitializeComponent();
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            WireViewModels();
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            if (DataContext is CompareViewModel vm)
            {
                _ = vm.InitializeAsync();
            }
        }

        private void WireViewModels()
        {
            if (_wired || DataContext is not CompareViewModel vm)
            {
                return;
            }

            _wired = true;

            var viewers = new[] { Viewer0, Viewer1, Viewer2 };
            for (int i = 0; i < viewers.Length && i < vm.Panes.Count; i++)
            {
                var viewer = viewers[i];
                var pane = vm.Panes[i];
                viewer.ViewportChanged += (_, args) => vm.OnPaneViewportChanged(pane, args.Viewport);
                viewer.FullResRequested += async () => (string?)await vm.EnsureFullResolutionAsync(pane).ConfigureAwait(false);
                pane.RequestSetViewport += viewport => viewer.SetViewport(viewport, raiseEvent: false);
                pane.RequestFit += viewer.FitToView;
            }

            Closed += (_, _) =>
            {
                foreach (var viewer in viewers)
                {
                    viewer.DisposeImages();
                }
                vm.Dispose();
            };
        }
    }
}
