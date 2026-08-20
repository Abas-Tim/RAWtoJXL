using System.IO;
using ImageMagick;
using Moq;
using RAWtoJXL.Core.Interfaces;
using RAWtoJXL.Core.Models;
using RAWtoJXL.Core.Services;

namespace RAWtoJXL.Tests;

public class CompareConversionServiceTests
{
    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "RAWtoJXL_Compare_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string CreateTestJpeg(string dir, string name = "input.jpg", int width = 64, int height = 48)
    {
        var path = Path.Combine(dir, name);
        using var image = new MagickImage(MagickColors.SteelBlue, (uint)width, (uint)height);
        image.Quality = 90;
        image.Write(path);
        return path;
    }

    private static CompareConversionService CreateService(
        Mock<ICjxlEncoder>? cjxl = null,
        Mock<IJxlDecoder>? djxl = null,
        Mock<IRawRenderer>? rawRenderer = null)
    {
        cjxl ??= new Mock<ICjxlEncoder>();
        djxl ??= new Mock<IJxlDecoder>();
        rawRenderer ??= new Mock<IRawRenderer>();
        rawRenderer.Setup(x => x.RenderToPngAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FileNotFoundException("rawtherapee-cli.exe not found"));
        var exif = new Mock<IExiftoolService>();
        var logger = new Mock<ILogger>();
        var fileService = new FileService(logger.Object);
        var imageConverter = new ImageConverterService(exif.Object, fileService, logger.Object, djxl.Object);
        return new CompareConversionService(
            imageConverter, cjxl.Object, djxl.Object, rawRenderer.Object, fileService, logger.Object);
    }

    [Fact]
    public async Task EnsureMasterPngAsync_JpegInput_CreatesMasterAndCachesSecondCall()
    {
        var dir = CreateTempDir();
        var input = CreateTestJpeg(dir);
        var service = CreateService();

        try
        {
            var master = await service.EnsureMasterPngAsync(input);
            Assert.True(File.Exists(master));
            Assert.EndsWith("master.png", master);

            using (var image = new MagickImage(master))
            {
                Assert.Equal(64, (int)image.Width);
                Assert.Equal(48, (int)image.Height);
            }

            var second = await service.EnsureMasterPngAsync(input);
            Assert.Equal(master, second);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task EnsureMasterPngAsync_JxlInput_DelegatesToDecoderOnlyOnce()
    {
        var dir = CreateTempDir();
        var input = Path.Combine(dir, "input.jxl");
        File.WriteAllText(input, "fake jxl");
        var djxl = new Mock<IJxlDecoder>();
        djxl.Setup(x => x.DecodeToPngAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<int>()))
            .Callback<string, string, CancellationToken, int>((_, output, _, _) => File.WriteAllText(output, "png"))
            .Returns(Task.CompletedTask);
        var service = CreateService(djxl: djxl);

        try
        {
            var master = await service.EnsureMasterPngAsync(input);
            Assert.True(File.Exists(master));

            await service.EnsureMasterPngAsync(input);

            djxl.Verify(x => x.DecodeToPngAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<int>()), Times.Once);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task EnsureMasterPngAsync_RawInput_UsesRawRenderer()
    {
        var dir = CreateTempDir();
        var input = Path.Combine(dir, "input.dng");
        File.Copy(TestAssetGenerator.AssetPath, input);
        var rawRenderer = new Mock<IRawRenderer>();
        rawRenderer.Setup(x => x.RenderToPngAsync(
                It.IsAny<string>(), It.IsAny<string>(), CompareDefaults.JxlThreads, It.IsAny<CancellationToken>()))
            .Callback<string, string, int, CancellationToken>((_, output, _, _) => File.WriteAllText(output, "png"))
            .Returns(Task.CompletedTask);
        var service = CreateService(rawRenderer: rawRenderer);

        try
        {
            var master = await service.EnsureMasterPngAsync(input);

            Assert.True(File.Exists(master));
            rawRenderer.Verify(x => x.RenderToPngAsync(
                input, master, CompareDefaults.JxlThreads, It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task EnsureQuickPreviewAsync_UsesEmbeddedPreviewAndCachesIt()
    {
        var dir = CreateTempDir();
        var input = CreateTestJpeg(dir);
        var logger = new Mock<ILogger>();
        var fileService = new FileService(logger.Object);
        var converter = new Mock<IImageConverterService>();
        converter.Setup(x => x.ExtractEmbeddedPreviewAsync(input, It.IsAny<CancellationToken>()))
            .ReturnsAsync(File.ReadAllBytes(input));
        var service = new CompareConversionService(
            converter.Object,
            new Mock<ICjxlEncoder>().Object,
            new Mock<IJxlDecoder>().Object,
            new Mock<IRawRenderer>().Object,
            fileService,
            logger.Object);

        try
        {
            var first = await service.EnsureQuickPreviewAsync(input);
            var second = await service.EnsureQuickPreviewAsync(input);

            Assert.NotNull(first);
            Assert.Equal(first, second);
            Assert.True(File.Exists(first));
            converter.Verify(x => x.ExtractEmbeddedPreviewAsync(input, It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task EnsureTargetFileAsync_JxlFormat_UsesMasterPngAsInput()
    {
        var dir = CreateTempDir();
        var input = CreateTestJpeg(dir);
        var cjxl = new Mock<ICjxlEncoder>();
        string? cjxlInput = null;
        int? cjxlThreads = null;
        cjxl.Setup(x => x.EncodeFromFileAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>(),
                It.IsAny<int>(), It.IsAny<Action<double>?>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .Callback<string, string, int, CancellationToken, int, Action<double>?, int?, int?>((i, o, _, _, _, _, _, threads) =>
            {
                cjxlInput = i;
                cjxlThreads = threads;
                File.WriteAllText(o, "encoded");
            })
            .Returns(Task.CompletedTask);
        var service = CreateService(cjxl: cjxl);

        try
        {
            var target = await service.EnsureTargetFileAsync(input, OutputFormat.Jxl, 90, 4);

            Assert.True(File.Exists(target));
            Assert.EndsWith(".jxl", target);
            Assert.NotNull(cjxlInput);
            Assert.EndsWith("master.png", cjxlInput);
            Assert.Equal(CompareDefaults.JxlThreads, cjxlThreads);

            var second = await service.EnsureTargetFileAsync(input, OutputFormat.Jxl, 90, 4);
            Assert.Equal(target, second);
            cjxl.Verify(x => x.EncodeFromFileAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>(),
                It.IsAny<int>(), It.IsAny<Action<double>?>(), It.IsAny<int?>(), It.IsAny<int?>()), Times.Once);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task EnsureDisplayPngsAsync_WhenJxlToolsAreMissing_UsesMagickFallback()
    {
        var dir = CreateTempDir();
        var input = CreateTestJpeg(dir);
        var cjxl = new Mock<ICjxlEncoder>();
        cjxl.Setup(x => x.EncodeFromFileAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>(),
                It.IsAny<int>(), It.IsAny<Action<double>?>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ThrowsAsync(new FileNotFoundException("cjxl executable not found"));
        var djxl = new Mock<IJxlDecoder>();
        djxl.Setup(x => x.DecodeToPngAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<int>()))
            .ThrowsAsync(new FileNotFoundException("djxl executable not found"));
        var service = CreateService(cjxl, djxl);

        try
        {
            var display = await service.EnsureDisplayPngsAsync(input, OutputFormat.Jxl, 90, 4);
            var full = await service.EnsureDisplayFullPngAsync(input, OutputFormat.Jxl, 90, 4);

            Assert.True(File.Exists(display.PreviewPath));
            Assert.True(File.Exists(full));
            Assert.Equal(64, display.Width);
            Assert.Equal(48, display.Height);

            using var previewImage = new MagickImage(display.PreviewPath);
            using var fullImage = new MagickImage(full);
            Assert.Equal(8, (int)previewImage.Depth);
            Assert.Equal(8, (int)fullImage.Depth);
            Assert.Equal(ColorSpace.sRGB, previewImage.ColorSpace);
            Assert.Equal(ColorSpace.sRGB, fullImage.ColorSpace);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task EnsureTargetFileAsync_MagickJxlFallback_PreservesFullSourceDimensions()
    {
        var dir = CreateTempDir();
        var cjxl = new Mock<ICjxlEncoder>();
        cjxl.Setup(x => x.EncodeFromFileAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>(),
                It.IsAny<int>(), It.IsAny<Action<double>?>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ThrowsAsync(new FileNotFoundException("cjxl executable not found"));
        var service = CreateService(cjxl: cjxl);

        try
        {
            var target = await service.EnsureTargetFileAsync(
                TestAssetGenerator.AssetPath,
                OutputFormat.Jxl,
                90,
                4);

            using var image = new MagickImage(target);
            Assert.Equal(1600, (int)image.Width);
            Assert.Equal(1200, (int)image.Height);
            Assert.True(new FileInfo(target).Length > 10_000);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task EnsureTargetFileAsync_MagickJxlFallback_IsLosslessWhenCjxlIsMissing()
    {
        var dir = CreateTempDir();
        var input = Path.Combine(dir, "input.dng");
        File.Copy(TestAssetGenerator.AssetPath, input);
        var cjxl = new Mock<ICjxlEncoder>();
        cjxl.Setup(x => x.EncodeFromFileAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>(),
                It.IsAny<int>(), It.IsAny<Action<double>?>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ThrowsAsync(new FileNotFoundException("cjxl executable not found"));
        var service = CreateService(cjxl: cjxl);

        try
        {
            var master = await service.EnsureMasterPngAsync(input);
            var target = await service.EnsureTargetFileAsync(input, OutputFormat.Jxl, 90, 4);

            using var sourceImage = new MagickImage(master);
            using var decodedImage = new MagickImage(target);
            double error = sourceImage.Compare(decodedImage, ErrorMetric.RootMeanSquared);

            Assert.True(
                error < 0.000001,
                $"Magick.NET lossless JXL fallback RMSE was {error:F6}; target bytes={new FileInfo(target).Length}, source depth={sourceImage.Depth}, decoded depth={decodedImage.Depth}.");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task EnsureTargetFileAsync_AvifMaximumQuality_ProducesReadableTarget()
    {
        var dir = CreateTempDir();
        var service = CreateService();

        try
        {
            var target = await service.EnsureTargetFileAsync(
                TestAssetGenerator.AssetPath,
                OutputFormat.Avif,
                100,
                null);

            using var image = new MagickImage(target);
            Assert.Equal(1600, (int)image.Width);
            Assert.Equal(1200, (int)image.Height);
            Assert.True(new FileInfo(target).Length > 0);

            var display = await service.EnsureDisplayPngsAsync(
                TestAssetGenerator.AssetPath,
                OutputFormat.Avif,
                100,
                null);
            var full = await service.EnsureDisplayFullPngAsync(
                TestAssetGenerator.AssetPath,
                OutputFormat.Avif,
                100,
                null);

            using var fullDisplay = new MagickImage(full);
            Assert.True(File.Exists(display.PreviewPath));
            Assert.Equal(1600, (int)fullDisplay.Width);
            Assert.Equal(1200, (int)fullDisplay.Height);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task EnsureDisplayPngsAsync_ProductionToolServicesMissing_UsesMagickFallback()
    {
        var dir = CreateTempDir();
        var input = CreateTestJpeg(dir);
        var logger = new Mock<ILogger>();
        var fileService = new FileService(logger.Object);
        var pathResolver = new Mock<IPathResolver>();
        pathResolver.Setup(x => x.ResolveCjxlPath()).Returns("cjxl.exe");
        pathResolver.Setup(x => x.ResolveDjxlPath()).Returns("djxl.exe");
        var processRunner = new SystemProcessRunner(logger.Object);
        var encoder = new CjxlEncoderService(pathResolver.Object, logger.Object, processRunner);
        var decoder = new DjxlDecoderService(pathResolver.Object, logger.Object, processRunner);
        var imageConverter = new ImageConverterService(
            new Mock<IExiftoolService>().Object,
            fileService,
            logger.Object,
            decoder);
        var rawRenderer = new Mock<IRawRenderer>();
        rawRenderer.Setup(x => x.RenderToPngAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FileNotFoundException("rawtherapee-cli.exe not found"));
        var service = new CompareConversionService(
            imageConverter, encoder, decoder, rawRenderer.Object, fileService, logger.Object);

        try
        {
            var display = await service.EnsureDisplayPngsAsync(input, OutputFormat.Jxl, 90, 4);
            var full = await service.EnsureDisplayFullPngAsync(input, OutputFormat.Jxl, 90, 4);

            Assert.True(File.Exists(display.PreviewPath));
            Assert.True(File.Exists(full));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task EnsureTargetFileAsync_StaleSource_RegeneratesIntoNewDirectory()
    {
        var dir = CreateTempDir();
        var input = CreateTestJpeg(dir);
        var service = CreateService();

        try
        {
            var first = await service.EnsureTargetFileAsync(input, OutputFormat.Jpeg, 90, null);

            using (var image = new MagickImage(MagickColors.OrangeRed, 32, 32))
            {
                image.Quality = 90;
                image.Write(input);
            }

            var second = await service.EnsureTargetFileAsync(input, OutputFormat.Jpeg, 90, null);

            Assert.True(File.Exists(first));
            Assert.True(File.Exists(second));
            Assert.NotEqual(first, second);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task EnsureDisplayPngsAsync_JpegTarget_ReturnsPreviewAndFullWithDimensions()
    {
        var dir = CreateTempDir();
        var input = CreateTestJpeg(dir);
        var service = CreateService();

        try
        {
            var display = await service.EnsureDisplayPngsAsync(input, OutputFormat.Jpeg, 90, null);
            var full = await service.EnsureDisplayFullPngAsync(input, OutputFormat.Jpeg, 90, null);

            Assert.True(File.Exists(display.PreviewPath));
            Assert.Empty(display.FullPath);
            Assert.True(File.Exists(full));
            Assert.Equal(64, display.Width);
            Assert.Equal(48, display.Height);

            using (var preview = new MagickImage(display.PreviewPath))
            {
                Assert.Equal(64, (int)preview.Width);
                Assert.Equal(48, (int)preview.Height);
            }
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task EnsureDisplayPngsAsync_OriginalFormat_ReturnsDimensions()
    {
        var dir = CreateTempDir();
        var input = CreateTestJpeg(dir);
        var service = CreateService();

        try
        {
            var display = await service.EnsureDisplayPngsAsync(input, null, 90, null);
            var full = await service.EnsureDisplayFullPngAsync(input, null, 90, null);

            Assert.True(File.Exists(display.PreviewPath));
            Assert.True(File.Exists(full));
            Assert.Equal(64, display.Width);
            Assert.Equal(48, display.Height);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task EnsureDisplayPngsAsync_RawInput_RendersOriginalWithoutTargetConversion()
    {
        var service = CreateService();

        var display = await service.EnsureDisplayPngsAsync(
            TestAssetGenerator.AssetPath,
            null,
            90,
            null);
        var full = await service.EnsureDisplayFullPngAsync(
            TestAssetGenerator.AssetPath,
            null,
            90,
            null);

        Assert.True(File.Exists(display.PreviewPath));
        Assert.True(File.Exists(full));
        Assert.Equal(1600, display.Width);
        Assert.Equal(1200, display.Height);
    }

    [Fact]
    public async Task PurgeStaleEntries_RemovesDirectoriesForDeletedSources()
    {
        var dir = CreateTempDir();
        var input = CreateTestJpeg(dir);
        var service = CreateService();

        try
        {
            var master = await service.EnsureMasterPngAsync(input);
            string masterDir = Path.GetDirectoryName(master)!;
            Assert.True(Directory.Exists(masterDir));

            File.Delete(input);

            service.PurgeStaleEntries();

            Assert.False(Directory.Exists(masterDir));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task EnsureMasterPngAsync_MissingInput_ThrowsFileNotFoundException()
    {
        var dir = CreateTempDir();
        var service = CreateService();

        try
        {
            await Assert.ThrowsAsync<FileNotFoundException>(() =>
                service.EnsureMasterPngAsync(Path.Combine(dir, "missing.jpg")));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task EnsureTargetFileAsync_PreCancelled_ThrowsOperationCanceled()
    {
        var dir = CreateTempDir();
        var input = CreateTestJpeg(dir);
        var service = CreateService();

        try
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                service.EnsureTargetFileAsync(input, OutputFormat.Jpeg, 90, null, cts.Token));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task EnsureTargetFileAsync_DifferentQuality_ProducesDifferentCacheDirectories()
    {
        var dir = CreateTempDir();
        var input = CreateTestJpeg(dir);
        var service = CreateService();

        try
        {
            var q90 = await service.EnsureTargetFileAsync(input, OutputFormat.Jpeg, 90, null);
            var q50 = await service.EnsureTargetFileAsync(input, OutputFormat.Jpeg, 50, null);

            Assert.NotEqual(q90, q50);
            Assert.True(File.Exists(q90));
            Assert.True(File.Exists(q50));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
