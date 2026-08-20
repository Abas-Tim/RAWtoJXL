using RAWtoJXL.Avalonia.Controls;

namespace RAWtoJXL.Tests;

public class CompareViewportTests
{
    [Fact]
    public void Fit_UniformlyScalesAndCenters()
    {
        var vp = CompareViewport.Fit(1000, 500, 500, 500);

        Assert.Equal(0.5, vp.Zoom, 9);
        Assert.Equal(0.5, vp.CenterX, 9);
        Assert.Equal(0.5, vp.CenterY, 9);
    }

    [Fact]
    public void Fit_ClampsToMinimumZoom()
    {
        var vp = CompareViewport.Fit(1_000_000, 1_000_000, 500, 500);

        Assert.Equal(CompareViewport.MinZoom, vp.Zoom, 9);
    }

    [Fact]
    public void ZoomAt_KeepsImagePointUnderPointerFixed()
    {
        var vp = CompareViewport.Fit(1000, 1000, 1000, 1000);

        var zoomed = CompareViewport.ZoomAt(vp, 600, 400, 1000, 1000, 1000, 1000, 2.0);

        Assert.Equal(2.0, zoomed.Zoom, 9);
        Assert.Equal(0.55, zoomed.CenterX, 9);
        Assert.Equal(0.45, zoomed.CenterY, 9);

        var (tx, ty) = zoomed.GetTranslate(1000, 1000, 1000, 1000);
        Assert.Equal(-600, tx, 9);
        Assert.Equal(-400, ty, 9);
    }

    [Fact]
    public void ZoomAt_ClampsZoomToMax()
    {
        var vp = CompareViewport.Fit(1000, 1000, 1000, 1000);

        var zoomed = CompareViewport.ZoomAt(vp, 500, 500, 1000, 1000, 1000, 1000, 1000.0);

        Assert.Equal(CompareViewport.MaxZoom, zoomed.Zoom, 9);
    }

    [Fact]
    public void ZoomOut_ClampsZoomToMin()
    {
        var vp = new CompareViewport(0.05, 0.5, 0.5);

        var zoomed = CompareViewport.ZoomAt(vp, 100, 100, 1000, 1000, 1000, 1000, 0.001);

        Assert.Equal(CompareViewport.MinZoom, zoomed.Zoom, 9);
    }

    [Fact]
    public void Pan_ClampsCenterToImageBounds()
    {
        var vp = new CompareViewport(1.0, 0.5, 0.5);

        var panned = CompareViewport.Pan(vp, 100000, 0, 200, 200, 1000, 1000);

        Assert.Equal(0.1, panned.CenterX, 9);
    }

    [Fact]
    public void Pan_ImageSmallerThanView_StaysCentered()
    {
        var vp = new CompareViewport(0.1, 0.5, 0.5);

        var panned = CompareViewport.Pan(vp, 300, 300, 1000, 1000, 100, 100);

        Assert.Equal(0.5, panned.CenterX, 9);
        Assert.Equal(0.5, panned.CenterY, 9);
    }

    [Fact]
    public void ZoomAt_AtEdge_ClampsCenterSoNoGapAppears()
    {
        var vp = new CompareViewport(2.0, 0.9, 0.9);

        var zoomed = CompareViewport.ZoomAt(vp, 990, 990, 1000, 1000, 1000, 1000, 2.0);

        Assert.Equal(4.0, zoomed.Zoom, 9);
        Assert.Equal(0.875, zoomed.CenterX, 9);
        Assert.Equal(0.875, zoomed.CenterY, 9);
    }

    [Fact]
    public void Equals_ComparesWithTolerance()
    {
        var a = new CompareViewport(2.0, 0.5, 0.5);
        var b = new CompareViewport(2.0 + 1e-12, 0.5, 0.5);

        Assert.True(a.Equals(b));
    }

    [Fact]
    public void GetVisibleImageRegion_FittedLetterboxedImage_ReturnsWholeImage()
    {
        var viewport = CompareViewport.Fit(1000, 500, 500, 500);

        var region = viewport.GetVisibleImageRegion(500, 500, 1000, 500);

        Assert.Equal(0, region.Left, 9);
        Assert.Equal(0, region.Top, 9);
        Assert.Equal(1, region.Right, 9);
        Assert.Equal(1, region.Bottom, 9);
    }

    [Fact]
    public void GetVisibleImageRegion_ZoomedCenter_ReturnsVisibleCrop()
    {
        var viewport = new CompareViewport(1.0, 0.5, 0.5);

        var region = viewport.GetVisibleImageRegion(500, 500, 1000, 1000);

        Assert.Equal(0.25, region.Left, 9);
        Assert.Equal(0.25, region.Top, 9);
        Assert.Equal(0.75, region.Right, 9);
        Assert.Equal(0.75, region.Bottom, 9);
    }

    [Fact]
    public void GetVisibleImageRegion_PannedToEdge_ClampsToImage()
    {
        var viewport = new CompareViewport(1.0, 0.25, 0.5);

        var region = viewport.GetVisibleImageRegion(500, 500, 1000, 1000);

        Assert.Equal(0, region.Left, 9);
        Assert.Equal(0.25, region.Top, 9);
        Assert.Equal(0.5, region.Right, 9);
        Assert.Equal(0.75, region.Bottom, 9);
    }
}
