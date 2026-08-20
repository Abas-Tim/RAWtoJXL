using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace RAWtoJXL.Avalonia.Controls
{
    public sealed class ViewportChangedEventArgs : EventArgs
    {
        public CompareViewport Viewport { get; }

        public ViewportChangedEventArgs(CompareViewport viewport)
        {
            Viewport = viewport;
        }
    }

    public partial class ZoomPanImageViewer : Grid
    {
        public const double FullResZoomThreshold = 0.75;
        private const double WheelZoomFactor = 1.1;
        private const int FullResDebounceMs = 150;

        public static readonly StyledProperty<IImage?> ImageSourceProperty =
            AvaloniaProperty.Register<ZoomPanImageViewer, IImage?>(nameof(ImageSource));

        public static readonly StyledProperty<string?> FullResPathProperty =
            AvaloniaProperty.Register<ZoomPanImageViewer, string?>(nameof(FullResPath));

        private readonly Image _image;
        private readonly ScaleTransform _scale;
        private readonly TranslateTransform _translate;

        private Bitmap? _preview;
        private Bitmap? _fullRes;
        private string? _fullResPath;
        private bool _fullResLoaded;
        private bool _fullResRequestAttempted;
        private bool _fullResRequestInFlight;
        private bool _pendingInitialFit = true;
        private bool _isPanning;
        private Point _lastPointer;
        private int _imageWidth;
        private int _imageHeight;
        private int _imageGeneration;
        private CompareViewport _viewport = new(1.0, 0.5, 0.5);
        private DispatcherTimer? _fullResDebounce;

        public event EventHandler<ViewportChangedEventArgs>? ViewportChanged;

        public event Func<Task<string?>>? FullResRequested;

        public IImage? ImageSource
        {
            get => GetValue(ImageSourceProperty);
            set => SetValue(ImageSourceProperty, value);
        }

        public string? FullResPath
        {
            get => GetValue(FullResPathProperty);
            set => SetValue(FullResPathProperty, value);
        }

        public CompareViewport Viewport => _viewport;

        public ZoomPanImageViewer()
        {
            Background = Brushes.Black;
            ClipToBounds = true;
            Focusable = true;

            _scale = new ScaleTransform();
            _translate = new TranslateTransform();

            _image = new Image
            {
                RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Relative),
                RenderTransform = new TransformGroup
                {
                    Children = { _scale, _translate }
                },
                Stretch = Stretch.Fill,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };

            Children.Add(_image);

            PointerWheelChanged += OnPointerWheelChanged;
            PointerPressed += OnPointerPressed;
            PointerMoved += OnPointerMoved;
            PointerReleased += OnPointerReleased;
            DoubleTapped += OnDoubleTapped;
            SizeChanged += OnSizeChanged;
        }

        static ZoomPanImageViewer()
        {
            ImageSourceProperty.Changed.AddClassHandler<ZoomPanImageViewer>((viewer, e) => viewer.OnImageSourceChanged(e.NewValue as Bitmap));
            FullResPathProperty.Changed.AddClassHandler<ZoomPanImageViewer>((viewer, e) => viewer.OnFullResPathChanged(e.NewValue as string));
        }

        public void SetViewport(in CompareViewport viewport, bool raiseEvent = false)
        {
            _viewport = viewport;
            ApplyViewport();
            if (raiseEvent)
            {
                RaiseViewportChanged();
            }
        }

        public void FitToView()
        {
            _viewport = ComputeFitViewport();
            ApplyViewport();
            RaiseViewportChanged();
        }

        public void DisposeImages()
        {
            _image.Source = null;
            _preview?.Dispose();
            _preview = null;
            _fullRes?.Dispose();
            _fullRes = null;
            _fullResLoaded = false;
            _fullResPath = null;
            _fullResRequestAttempted = false;
            _imageGeneration++;
            _fullResDebounce?.Stop();
            _fullResDebounce = null;
        }

        private void OnImageSourceChanged(Bitmap? bitmap)
        {
            _image.Source = null;
            _preview?.Dispose();
            _preview = null;
            _fullRes?.Dispose();
            _fullRes = null;
            _fullResLoaded = false;
            _fullResPath = null;
            _fullResRequestAttempted = false;
            _imageGeneration++;

            _preview = bitmap;
            if (bitmap != null)
            {
                _imageWidth = bitmap.PixelSize.Width;
                _imageHeight = bitmap.PixelSize.Height;
                _image.Source = bitmap;
                if (Bounds.Width > 0 && Bounds.Height > 0)
                {
                    _pendingInitialFit = false;
                    _viewport = ComputeFitViewport();
                }
                else
                {
                    _pendingInitialFit = true;
                }
            }
            else
            {
                _imageWidth = 0;
                _imageHeight = 0;
                _image.Width = double.NaN;
                _image.Height = double.NaN;
                _pendingInitialFit = true;
            }

            if (bitmap != null)
            {
                _image.Width = _imageWidth;
                _image.Height = _imageHeight;
            }

            ApplyViewport();
        }

        private void OnFullResPathChanged(string? path)
        {
            _fullResPath = path;
            _fullResLoaded = false;
            if (string.IsNullOrEmpty(path))
            {
                _fullResRequestAttempted = false;
            }
            ScheduleFullResLoad();
        }

        private CompareViewport ComputeFitViewport()
        {
            return CompareViewport.Fit(_imageWidth, _imageHeight, Bounds.Width, Bounds.Height);
        }

        private void ApplyViewport()
        {
            if (_imageWidth <= 0 || _imageHeight <= 0)
            {
                _scale.ScaleX = _scale.ScaleY = _viewport.Zoom;
                _translate.X = 0;
                _translate.Y = 0;
                return;
            }

            var (tx, ty) = _viewport.GetTranslate(Bounds.Width, Bounds.Height, _imageWidth, _imageHeight);
            _scale.ScaleX = _scale.ScaleY = _viewport.Zoom;
            _translate.X = tx;
            _translate.Y = ty;

            ScheduleFullResLoad();
        }

        private void RaiseViewportChanged()
        {
            ViewportChanged?.Invoke(this, new ViewportChangedEventArgs(_viewport));
        }

        private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            double factor = e.Delta.Y > 0 ? WheelZoomFactor : 1.0 / WheelZoomFactor;
            var position = e.GetPosition(this);
            _viewport = CompareViewport.ZoomAt(
                _viewport, position.X, position.Y,
                Bounds.Width, Bounds.Height,
                _imageWidth, _imageHeight,
                factor);
            ApplyViewport();
            RaiseViewportChanged();
            e.Handled = true;
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                _isPanning = true;
                _lastPointer = e.GetPosition(this);
                e.Pointer.Capture(this);
                e.Handled = true;
            }
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_isPanning)
            {
                return;
            }

            var position = e.GetPosition(this);
            double dx = position.X - _lastPointer.X;
            double dy = position.Y - _lastPointer.Y;
            _lastPointer = position;

            _viewport = CompareViewport.Pan(
                _viewport, dx, dy,
                Bounds.Width, Bounds.Height,
                _imageWidth, _imageHeight);
            ApplyViewport();
            RaiseViewportChanged();
            e.Handled = true;
        }

        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_isPanning)
            {
                _isPanning = false;
                e.Pointer.Capture(null);
                e.Handled = true;
            }
        }

        private void OnDoubleTapped(object? sender, TappedEventArgs e)
        {
            FitToView();
            e.Handled = true;
        }

        private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            if (_pendingInitialFit && _imageWidth > 0 && Bounds.Width > 0 && Bounds.Height > 0)
            {
                _pendingInitialFit = false;
                _viewport = ComputeFitViewport();
                ApplyViewport();
                return;
            }

            ApplyViewport();
        }

        private void ScheduleFullResLoad()
        {
            if (_viewport.Zoom < FullResZoomThreshold)
            {
                _fullResDebounce?.Stop();
                return;
            }

            if (_fullResLoaded)
            {
                return;
            }

            if (string.IsNullOrEmpty(_fullResPath))
            {
                if (!_fullResRequestAttempted && !_fullResRequestInFlight)
                {
                    _fullResRequestAttempted = true;
                    _ = RequestFullResAsync(_imageGeneration);
                }
                return;
            }

            if (_fullResDebounce == null)
            {
                _fullResDebounce = new DispatcherTimer(
                    TimeSpan.FromMilliseconds(FullResDebounceMs),
                    DispatcherPriority.Background,
                    OnFullResDebounceTick);
            }
            else
            {
                _fullResDebounce.Stop();
            }

            _fullResDebounce.Start();
        }

        private async Task RequestFullResAsync(int generation)
        {
            var request = FullResRequested;
            if (request == null)
            {
                return;
            }

            _fullResRequestInFlight = true;
            try
            {
                string? path = await request();
                if (generation == _imageGeneration && !string.IsNullOrEmpty(path))
                {
                    _fullResPath = path;
                    ScheduleFullResLoad();
                }
            }
            catch
            {
            }
            finally
            {
                _fullResRequestInFlight = false;
            }
        }

        private void OnFullResDebounceTick(object? sender, EventArgs e)
        {
            _fullResDebounce?.Stop();
            if (_viewport.Zoom < FullResZoomThreshold)
            {
                return;
            }

            _ = LoadFullResAsync();
        }

        private async Task LoadFullResAsync()
        {
            if (_fullResLoaded || string.IsNullOrEmpty(_fullResPath))
            {
                return;
            }

            string path = _fullResPath;
            try
            {
                var bitmap = await Task.Run(() => new Bitmap(path));
                if (_fullResPath != path || _fullResLoaded)
                {
                    bitmap.Dispose();
                    return;
                }

                _fullRes = bitmap;
                _fullResLoaded = true;
                if (_image.Source == _preview)
                {
                    _image.Source = bitmap;
                    _preview?.Dispose();
                    _preview = null;
                }
            }
            catch
            {
            }
        }
    }
}
