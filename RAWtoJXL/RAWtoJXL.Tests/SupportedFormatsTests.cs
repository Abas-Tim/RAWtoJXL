using RAWtoJXL.Core.Models;

namespace RAWtoJXL.Tests;

public class SupportedFormatsTests
{
    [Theory]
    [InlineData(".arw", true)]
    [InlineData(".dng", true)]
    [InlineData(".cr3", true)]
    [InlineData(".jxl", false)]
    [InlineData(".jpg", false)]
    [InlineData(".avif", false)]
    [InlineData(".txt", false)]
    [InlineData("", false)]
    public void IsRawFile_ClassifiesExtensions(string extension, bool expected)
    {
        Assert.Equal(expected, SupportedFormats.IsRawFile(extension));
    }

    [Theory]
    [InlineData(".jpg", true)]
    [InlineData(".jpeg", true)]
    [InlineData(".JPG", true)]
    [InlineData(".jxl", true)]
    [InlineData(".avif", true)]
    [InlineData(".AVIF", true)]
    [InlineData(".arw", false)]
    [InlineData(".png", false)]
    [InlineData(".txt", false)]
    public void IsRasterInput_ClassifiesExtensions(string extension, bool expected)
    {
        Assert.Equal(expected, SupportedFormats.IsRasterInput(extension));
    }

    [Theory]
    [InlineData(".arw", true)]
    [InlineData(".jpg", true)]
    [InlineData(".jpeg", true)]
    [InlineData(".jxl", true)]
    [InlineData(".avif", true)]
    [InlineData(".png", false)]
    [InlineData(".txt", false)]
    public void IsSupportedInput_ClassifiesExtensions(string extension, bool expected)
    {
        Assert.Equal(expected, SupportedFormats.IsSupportedInput(extension));
    }

    [Theory]
    [InlineData(".jxl", true)]
    [InlineData(".JXL", true)]
    [InlineData(".jpg", false)]
    [InlineData(".avif", false)]
    public void IsJxlFile_ClassifiesExtensions(string extension, bool expected)
    {
        Assert.Equal(expected, SupportedFormats.IsJxlFile(extension));
    }

    [Fact]
    public void AllInputExtensions_ContainsRawAndRaster_WithoutDuplicates()
    {
        Assert.Equal(
            SupportedFormats.RawExtensions.Length + SupportedFormats.RasterInputExtensions.Length,
            SupportedFormats.AllInputExtensions.Length);

        Assert.All(SupportedFormats.RawExtensions, e => Assert.Contains(e, SupportedFormats.AllInputExtensions));
        Assert.All(SupportedFormats.RasterInputExtensions, e => Assert.Contains(e, SupportedFormats.AllInputExtensions));
    }

    [Fact]
    public void OutputExtensions_AreJxlJpgAvifOnly()
    {
        Assert.Equal(new[] { ".jxl", ".jpg", ".avif" }, SupportedFormats.OutputExtensions);
        Assert.DoesNotContain(".png", SupportedFormats.OutputExtensions);
    }

    [Fact]
    public void ToFileFilter_BuildsExpectedPattern()
    {
        var filter = SupportedFormats.ToFileFilter("AVIF Files", new[] { ".avif" });

        Assert.Equal("AVIF Files|*.avif", filter);
    }
}
