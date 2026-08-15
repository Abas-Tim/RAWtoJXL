using System.Diagnostics;
using System.IO;
using ImageMagick;
using Microsoft.Extensions.DependencyInjection;
using RAWtoJXL.Core.Interfaces;

namespace RAWtoJXL.Tests;

[Collection("Conversion")]
public class JxlInputConversionTests : Startup
{
    private readonly IImageService _imageService;

    public JxlInputConversionTests()
    {
        _imageService = Services.GetRequiredService<IImageService>();
    }

    private static string? GetToolPath(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, fileName);
        return File.Exists(path) ? path : null;
    }

    private static string CreateJxlFixture(string dir)
    {
        var png = Path.Combine(dir, $"fixture_{Guid.NewGuid():N}.png");
        using (var image = new MagickImage(MagickColors.SeaGreen, (uint)96, (uint)64))
        {
            image.Format = MagickFormat.Png;
            image.Write(png);
        }

        var jxl = Path.ChangeExtension(png, ".jxl");
        var cjxl = GetToolPath("cjxl.exe")
            ?? throw new InvalidOperationException("cjxl.exe missing from test output directory");

        var psi = new ProcessStartInfo
        {
            FileName = cjxl,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        psi.ArgumentList.Add(png);
        psi.ArgumentList.Add(jxl);
        psi.ArgumentList.Add("-d");
        psi.ArgumentList.Add("1");
        psi.ArgumentList.Add("-e");
        psi.ArgumentList.Add("3");
        psi.ArgumentList.Add("--container=1");

        using var process = Process.Start(psi)!;
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"cjxl fixture generation failed: {process.StandardError.ReadToEnd()}");
        }

        return jxl;
    }

    private static bool ToolsAvailable()
    {
        return GetToolPath("djxl.exe") != null && GetToolPath("cjxl.exe") != null;
    }

    [Fact]
    public async Task Convert_JxlToJpeg_CreatesReadableJpeg()
    {
        if (!ToolsAvailable()) Assert.Skip("djxl.exe not available");

        var dir = Path.Combine(Path.GetTempPath(), "RAWtoJXL_JxlIn_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var input = CreateJxlFixture(dir);
            var output = Path.Combine(dir, "out.jpg");

            await _imageService.ConvertToJxlAsync(input, output, _ => { }, 85, OutputFormat.Jpeg, TestContext.Current.CancellationToken, skipMetadata: true);

            using var image = new MagickImage(output);
            Assert.True(File.Exists(output) && new FileInfo(output).Length > 0);
            Assert.Equal(MagickFormat.Jpeg, image.Format);
            Assert.Equal(96, (int)image.Width);
            Assert.Equal(64, (int)image.Height);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task Convert_JxlToAvif_CreatesReadableAvif()
    {
        if (!ToolsAvailable()) Assert.Skip("djxl.exe not available");

        var dir = Path.Combine(Path.GetTempPath(), "RAWtoJXL_JxlIn_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var input = CreateJxlFixture(dir);
            var output = Path.Combine(dir, "out.avif");

            await _imageService.ConvertToJxlAsync(input, output, _ => { }, 80, OutputFormat.Avif, TestContext.Current.CancellationToken, skipMetadata: true);

            using var image = new MagickImage(output);
            Assert.True(File.Exists(output) && new FileInfo(output).Length > 0);
            Assert.Equal(MagickFormat.Avif, image.Format);
            Assert.Equal(96, (int)image.Width);
            Assert.Equal(64, (int)image.Height);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task Convert_JxlToJpeg_WithMetadata_EmbedsSourceMetadata()
    {
        if (!ToolsAvailable()) Assert.Skip("djxl.exe not available");

        var dir = Path.Combine(Path.GetTempPath(), "RAWtoJXL_JxlIn_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var input = CreateJxlFixture(dir);
            EmbedArtistIntoJxl(input, "Test Artist");
            var output = Path.Combine(dir, "out.jpg");

            await _imageService.ConvertToJxlAsync(input, output, _ => { }, 85, OutputFormat.Jpeg, TestContext.Current.CancellationToken);

            Assert.Equal("Test Artist", ReadArtist(output));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task GetThumbnailAsync_JxlInput_ReturnsBytes()
    {
        if (!ToolsAvailable()) Assert.Skip("djxl.exe not available");

        var dir = Path.Combine(Path.GetTempPath(), "RAWtoJXL_JxlIn_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var input = CreateJxlFixture(dir);

            var thumbnail = await _imageService.GetThumbnailAsync(input, TestContext.Current.CancellationToken);

            Assert.NotEmpty(thumbnail);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    private static void EmbedArtistIntoJxl(string jxlPath, string artist)
    {
        var exiftool = GetToolPath("exiftool.exe")
            ?? throw new InvalidOperationException("exiftool.exe missing from test output directory");

        var psi = new ProcessStartInfo
        {
            FileName = exiftool,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        psi.ArgumentList.Add("-overwrite_original");
        psi.ArgumentList.Add($"-EXIF:Artist={artist}");
        psi.ArgumentList.Add(jxlPath);

        using var process = Process.Start(psi)!;
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"exiftool artist embedding failed: {process.StandardError.ReadToEnd()}");
        }
    }

    private static string ReadArtist(string path)
    {
        var exiftool = GetToolPath("exiftool.exe")
            ?? throw new InvalidOperationException("exiftool.exe missing from test output directory");

        var psi = new ProcessStartInfo
        {
            FileName = exiftool,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        psi.ArgumentList.Add("-s3");
        psi.ArgumentList.Add("-Artist");
        psi.ArgumentList.Add(path);

        using var process = Process.Start(psi)!;
        var stdout = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();
        return stdout;
    }
}

