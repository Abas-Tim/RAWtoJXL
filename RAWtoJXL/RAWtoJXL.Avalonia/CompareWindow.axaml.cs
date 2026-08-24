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
            GpuPrototypeSurface.CapabilityChanged += OnGpuCapabilityChanged;
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
                viewer.ViewportChanged += (_, args) => vm.OnPaneViewportChanged(
                    pane,
                    args.Viewport,
                    args.VisibleRegion,
                    args.PixelWidth,
                    args.PixelHeight,
                    args.ImagePixelWidth,
                    args.ImagePixelHeight);
                viewer.DisplayStateChanged += (_, args) => vm.OnPaneDisplayStateChanged(pane, args.State);
                viewer.FullResRequested += async () => (string?)await vm.EnsureFullResolutionAsync(pane).ConfigureAwait(false);
                pane.RequestSetViewport += (viewport, sourcePixelWidth) => viewer.SetViewport(
                    NormalizeViewport(viewer, viewport, sourcePixelWidth),
                    raiseEvent: true);
                pane.RequestFit += viewer.FitToView;
                pane.RequestSetDifferenceOverlay += viewer.SetDifferenceOverlay;
            }

            if (GpuPrototypeSurface.Capability != null)
            {
                OnGpuCapabilityChanged(GpuPrototypeSurface, EventArgs.Empty);
            }

            Closed += (_, _) =>
            {
                GpuPrototypeSurface.CapabilityChanged -= OnGpuCapabilityChanged;
                foreach (var viewer in viewers)
                {
                    viewer.DisposeImages();
                }
                vm.Dispose();
            };
        }

        private static CompareViewport NormalizeViewport(
            ZoomPanImageViewer viewer,
            CompareViewport viewport,
            int sourcePixelWidth)
        {
            int targetPixelWidth = viewer.ImagePixelWidth;
            if (targetPixelWidth <= 0 || sourcePixelWidth <= 0)
            {
                return viewport;
            }

            double scale = (double)sourcePixelWidth / targetPixelWidth;
            return new CompareViewport(
                Math.Clamp(viewport.Zoom * scale, CompareViewport.MinZoom, CompareViewport.MaxZoom),
                viewport.CenterX,
                viewport.CenterY);
        }

        private void OnGpuCapabilityChanged(object? sender, EventArgs e)
        {
            if (DataContext is not CompareViewModel vm)
            {
                return;
            }

            bool supported = GpuPrototypeSurface.Capability?.SupportsRenderPrototype == true &&
                             GpuPrototypeSurface.Capability.FailureReason == null;
            vm.SetGpuPrototypeAvailability(supported);
        }
    }
}
