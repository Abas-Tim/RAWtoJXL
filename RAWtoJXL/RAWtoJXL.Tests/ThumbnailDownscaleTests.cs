using System.IO;
using ImageMagick;
using RAWtoJXL.Core.Services;

namespace RAWtoJXL.Tests;

public class ThumbnailDownscaleTests
{
    private readonly FileLogger _logger = new(
        Path.Combine(Path.GetTempPath(), $"RAWtoJXL-tests-{Guid.NewGuid():N}.log"));

    [Fact]
    public void DownscalePreview_ResizesLargeImageToMaxDimension()
    {
        var large = CreateJpeg(2000, 1500);
        var result = ImageConverterService.DownscalePreview(large, 300, _logger);

        using var image = new MagickImage(result);
        Assert.Equal(MagickFormat.Jpeg, image.Format);
        Assert.True(image.Width <= 300);
        Assert.True(image.Height <= 300);
    }

    [Fact]
    public void DownscalePreview_PreservesAspectRatio()
    {
        var large = CreateJpeg(2000, 1000);
        var result = ImageConverterService.DownscalePreview(large, 300, _logger);

        using var image = new MagickImage(result);
        Assert.InRange((uint)image.Width, image.Height * 2 - 2, image.Height * 2 + 2);
    }

    [Fact]
    public void DownscalePreview_ReturnsOriginalBytesWhenAlreadySmall()
    {
        var small = CreateJpeg(150, 100);
        var result = ImageConverterService.DownscalePreview(small, 300, _logger);

        Assert.Same(small, result);
    }

    [Fact]
    public void DownscalePreview_ReturnsOriginalBytesWhenDecodeFails()
    {
        var garbage = new byte[] { 1, 2, 3, 4, 5 };
        var result = ImageConverterService.DownscalePreview(garbage, 300, _logger);

        Assert.Same(garbage, result);
    }

    private static byte[] CreateJpeg(int width, int height)
    {
        using var image = new MagickImage(MagickColors.SteelBlue, (uint)width, (uint)height);
        image.Format = MagickFormat.Jpg;
        using var ms = new MemoryStream();
        image.Write(ms);
        return ms.ToArray();
    }
}
