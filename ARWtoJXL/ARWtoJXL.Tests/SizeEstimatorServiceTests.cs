using ARWtoJXL.Core.Services;
using Xunit;

namespace ARWtoJXL.Tests;

public class SizeEstimatorServiceTests
{
    private readonly SizeEstimatorService _sut = new();

    [Fact]
    public void Estimate_ZeroPngSize_ReturnsZero()
    {
        long result = _sut.Estimate(0, 90);
        Assert.Equal(0L, result);
    }

    [Fact]
    public void Estimate_NegativeQuality_ClampsToZero()
    {
        long result = _sut.Estimate(10_000_000, -10);
        Assert.True(result > 0);
    }

    [Fact]
    public void Estimate_QualityOver100_ClampsTo100()
    {
        long result = _sut.Estimate(10_000_000, 150);
        Assert.True(result > 0);
    }

    [Fact]
    public void Estimate_Lossless_Quality100_ReturnsLargerThanLossy()
    {
        long lossless = _sut.Estimate(20_000_000, 100);
        long lossy = _sut.Estimate(20_000_000, 90);
        Assert.True(lossless > lossy);
    }

    [Fact]
    public void Estimate_LowerQuality_ReturnsSmallerSize()
    {
        long size90 = _sut.Estimate(20_000_000, 90);
        long size70 = _sut.Estimate(20_000_000, 70);
        long size50 = _sut.Estimate(20_000_000, 50);
        Assert.True(size90 > size70);
        Assert.True(size70 > size50);
    }

    [Fact]
    public void Estimate_Quality90_GivesApproximateRatio()
    {
        // Quality 90 should produce ~40-60% of PNG size
        long pngSize = 30_000_000L;
        long estimated = _sut.Estimate(pngSize, 90);
        double ratio = (double)estimated / pngSize;
        Assert.InRange(ratio, 0.35, 0.65);
    }

    [Fact]
    public void Estimate_Quality70_GivesApproximateRatio()
    {
        // Quality 70 should produce ~15-30% of PNG size
        long pngSize = 30_000_000L;
        long estimated = _sut.Estimate(pngSize, 70);
        double ratio = (double)estimated / pngSize;
        Assert.InRange(ratio, 0.10, 0.35);
    }

    [Fact]
    public void Estimate_Quality100_Lossless_SimilarSize()
    {
        // Lossless should be roughly similar or slightly larger (metadata overhead)
        long pngSize = 25_000_000L;
        long estimated = _sut.Estimate(pngSize, 100);
        double ratio = (double)estimated / pngSize;
        Assert.InRange(ratio, 0.80, 1.20);
    }

    [Fact]
    public async Task Estimate_FromPngAsync_InvalidPath_ThrowsFileNotFoundException()
    {
        var ex = await Assert.ThrowsAsync<System.IO.FileNotFoundException>(() =>
            _sut.EstimateFromPngAsync("nonexistent.png", 90));
        Assert.Contains("nonexistent.png", ex.Message);
    }

    [Fact]
    public void Estimate_LargerImage_HigherAbsoluteSize()
    {
        long small = _sut.Estimate(10_000_000, 90);
        long large = _sut.Estimate(50_000_000, 90);
        Assert.True(large > small);
    }
}
