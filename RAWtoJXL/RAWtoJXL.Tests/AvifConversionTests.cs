using System.IO;
using ImageMagick;
using Microsoft.Extensions.DependencyInjection;
using RAWtoJXL.Core.Interfaces;

namespace RAWtoJXL.Tests;

[Collection("Conversion")]
public class AvifConversionTests : Startup
{
    private readonly IImageService _imageService;

    public AvifConversionTests()
    {
        _imageService = Services.GetRequiredService<IImageService>();
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "RAWtoJXL_Avif_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string CreateJpegFixture(string dir, int width = 96, int height = 64)
    {
        var path = Path.Combine(dir, $"fixture_{Guid.NewGuid():N}.jpg");
        using var image = new MagickImage(MagickColors.Orange, (uint)width, (uint)height);
        image.Format = MagickFormat.Jpg;
        image.Quality = 90;
        image.Write(path);
        return path;
    }

    private static string CreateAvifFixture(string dir, int width = 96, int height = 64)
    {
        var path = Path.Combine(dir, $"fixture_{Guid.NewGuid():N}.avif");
        using var image = new MagickImage(MagickColors.Teal, (uint)width, (uint)height);
        image.Format = MagickFormat.Avif;
        image.Quality = 80;
        image.Write(path);
        return path;
    }

    private static (int Width, int Height, MagickFormat Format) ReadBack(string path)
    {
        using var image = new MagickImage(path);
        return ((int)image.Width, (int)image.Height, image.Format);
    }

    [Fact]
    public async Task Convert_JpegToAvif_CreatesReadableAvif()
    {
        var dir = CreateTempDir();
        try
        {
            var input = CreateJpegFixture(dir);
            var output = Path.Combine(dir, "out.avif");

            await _imageService.ConvertToJxlAsync(input, output, _ => { }, 80, OutputFormat.Avif, TestContext.Current.CancellationToken);

            var (width, height, format) = ReadBack(output);
            Assert.True(File.Exists(output) && new FileInfo(output).Length > 0);
            Assert.Equal(MagickFormat.Avif, format);
            Assert.Equal(96, width);
            Assert.Equal(64, height);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task Convert_ArwToAvif_CreatesReadableAvif()
    {
        var dir = CreateTempDir();
        try
        {
            var output = Path.Combine(dir, "out.avif");

            await _imageService.ConvertToJxlAsync(TestArwPath, output, _ => { }, 70, OutputFormat.Avif, TestContext.Current.CancellationToken);

            var (width, height, format) = ReadBack(output);
            Assert.True(File.Exists(output) && new FileInfo(output).Length > 0);
            Assert.Equal(MagickFormat.Avif, format);
            Assert.True(width > 0 && height > 0);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task Convert_AvifToJpeg_CreatesReadableJpeg()
    {
        var dir = CreateTempDir();
        try
        {
            var input = CreateAvifFixture(dir);
            var output = Path.Combine(dir, "out.jpg");

            await _imageService.ConvertToJxlAsync(input, output, _ => { }, 85, OutputFormat.Jpeg, TestContext.Current.CancellationToken);

            var (width, height, format) = ReadBack(output);
            Assert.True(File.Exists(output) && new FileInfo(output).Length > 0);
            Assert.Equal(MagickFormat.Jpeg, format);
            Assert.Equal(96, width);
            Assert.Equal(64, height);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task Convert_AvifToJxl_CreatesJxlFile()
    {
        var dir = CreateTempDir();
        try
        {
            var input = CreateAvifFixture(dir);
            var output = Path.Combine(dir, "out.jxl");

            await _imageService.ConvertToJxlAsync(input, output, _ => { }, 80, OutputFormat.Jxl, TestContext.Current.CancellationToken);

            Assert.True(File.Exists(output) && new FileInfo(output).Length > 0);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task GetThumbnailAsync_AvifInput_ReturnsBytes()
    {
        var dir = CreateTempDir();
        try
        {
            var input = CreateAvifFixture(dir, 640, 480);

            var thumbnail = await _imageService.GetThumbnailAsync(input, TestContext.Current.CancellationToken);

            Assert.NotEmpty(thumbnail);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}

