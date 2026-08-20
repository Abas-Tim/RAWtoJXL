using System;
using RAWtoJXL.Core.Interfaces;

namespace RAWtoJXL.Avalonia.Controls
{
    public readonly struct CompareViewport
    {
        public const double MinZoom = 0.01;
        public const double MaxZoom = 16.0;

        public double Zoom { get; }
        public double CenterX { get; }
        public double CenterY { get; }

        public CompareViewport(double zoom, double centerX, double centerY)
        {
            Zoom = Math.Clamp(zoom, MinZoom, MaxZoom);
            CenterX = Math.Clamp(centerX, 0.0, 1.0);
            CenterY = Math.Clamp(centerY, 0.0, 1.0);
        }

        public static CompareViewport Fit(int imageWidth, int imageHeight, double viewWidth, double viewHeight)
        {
            if (imageWidth <= 0 || imageHeight <= 0 || viewWidth <= 0 || viewHeight <= 0)
            {
                return new CompareViewport(1.0, 0.5, 0.5);
            }

            double zoom = Math.Min(viewWidth / imageWidth, viewHeight / imageHeight);
            zoom = Math.Clamp(zoom, MinZoom, MaxZoom);
            return new CompareViewport(zoom, 0.5, 0.5);
        }

        public static CompareViewport ZoomAt(
            in CompareViewport vp,
            double pointerX,
            double pointerY,
            double viewWidth,
            double viewHeight,
            int imageWidth,
            int imageHeight,
            double factor)
        {
            double newZoom = Math.Clamp(vp.Zoom * factor, MinZoom, MaxZoom);
            if (Math.Abs(newZoom - vp.Zoom) < 1e-9)
            {
                return vp;
            }

            double imgX = (pointerX - TranslateX(vp, viewWidth, imageWidth)) / vp.Zoom;
            double imgY = (pointerY - TranslateY(vp, viewHeight, imageHeight)) / vp.Zoom;

            double centerX = (viewWidth / 2 - pointerX + imgX * newZoom) / newZoom;
            double centerY = (viewHeight / 2 - pointerY + imgY * newZoom) / newZoom;

            centerX = ClampCenter(centerX, viewWidth, imageWidth, newZoom);
            centerY = ClampCenter(centerY, viewHeight, imageHeight, newZoom);

            return new CompareViewport(newZoom, centerX / imageWidth, centerY / imageHeight);
        }

        public static CompareViewport Pan(
            in CompareViewport vp,
            double deltaX,
            double deltaY,
            double viewWidth,
            double viewHeight,
            int imageWidth,
            int imageHeight)
        {
            double centerX = vp.CenterX * imageWidth - deltaX / vp.Zoom;
            double centerY = vp.CenterY * imageHeight - deltaY / vp.Zoom;

            centerX = ClampCenter(centerX, viewWidth, imageWidth, vp.Zoom);
            centerY = ClampCenter(centerY, viewHeight, imageHeight, vp.Zoom);

            return new CompareViewport(vp.Zoom, centerX / imageWidth, centerY / imageHeight);
        }

        public (double X, double Y) GetTranslate(double viewWidth, double viewHeight, int imageWidth, int imageHeight)
        {
            double x = viewWidth / 2 - CenterX * imageWidth * Zoom;
            double y = viewHeight / 2 - CenterY * imageHeight * Zoom;
            return (x, y);
        }

        public CompareImageRegion GetVisibleImageRegion(
            double viewWidth,
            double viewHeight,
            int imageWidth,
            int imageHeight)
        {
            if (viewWidth <= 0 || viewHeight <= 0 || imageWidth <= 0 || imageHeight <= 0)
            {
                return new CompareImageRegion(0, 0, 1, 1);
            }

            var (tx, ty) = GetTranslate(viewWidth, viewHeight, imageWidth, imageHeight);
            double scaledWidth = imageWidth * Zoom;
            double scaledHeight = imageHeight * Zoom;
            double left = Math.Clamp(-tx / scaledWidth, 0, 1);
            double top = Math.Clamp(-ty / scaledHeight, 0, 1);
            double right = Math.Clamp((viewWidth - tx) / scaledWidth, 0, 1);
            double bottom = Math.Clamp((viewHeight - ty) / scaledHeight, 0, 1);
            return new CompareImageRegion(left, top, right, bottom);
        }

        public bool Equals(in CompareViewport other)
        {
            return Math.Abs(Zoom - other.Zoom) < 1e-9 &&
                   Math.Abs(CenterX - other.CenterX) < 1e-9 &&
                   Math.Abs(CenterY - other.CenterY) < 1e-9;
        }

        private static double TranslateX(in CompareViewport vp, double viewWidth, int imageWidth)
        {
            return viewWidth / 2 - vp.CenterX * imageWidth * vp.Zoom;
        }

        private static double TranslateY(in CompareViewport vp, double viewHeight, int imageHeight)
        {
            return viewHeight / 2 - vp.CenterY * imageHeight * vp.Zoom;
        }

        private static double ClampCenter(double centerPx, double viewSize, double imageSize, double zoom)
        {
            if (imageSize * zoom <= viewSize)
            {
                return imageSize / 2;
            }

            double halfView = viewSize / (2 * zoom);
            return Math.Clamp(centerPx, halfView, imageSize - halfView);
        }
    }
}
