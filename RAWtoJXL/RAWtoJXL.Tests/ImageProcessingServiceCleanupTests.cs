using System.IO;
using Moq;
using RAWtoJXL.Core.Interfaces;
using RAWtoJXL.Core.Services;

namespace RAWtoJXL.Tests;

public class ImageProcessingServiceCleanupTests
{
    private sealed class ThrowingEncoder : ICjxlEncoder
    {
        public Task EncodeFromStreamAsync(string inputPath, string outputPath, int quality, Func<Stream, CancellationToken, Task> ppmWriter, CancellationToken cancellationToken, int timeoutSeconds, Action<double>? progress, int? effort, int? threads = null)
        {
            File.WriteAllText(outputPath, "partial");
            return Task.FromException(new IOException("simulated encoding failure"));
        }
    }

    private static ImageProcessingService CreateService(
        ICjxlEncoder? encoder = null,
        IImageConverterService? converter = null,
        IExiftoolService? exiftool = null,
        IJxlDecoder? jxlDecoder = null)
    {
        var logger = new FileLogger();
        return new ImageProcessingService(
            converter ?? new Mock<IImageConverterService>().Object,
            encoder ?? new Mock<ICjxlEncoder>().Object,
            new FileService(logger),
            logger,
            exiftool ?? new Mock<IExiftoolService>().Object,
            jxlDecoder ?? new Mock<IJxlDecoder>().Object);
    }

    [Fact]
    public async Task ConvertToJxlAsync_EncoderFailsAfterWritingOutput_DeletesPartialOutput()
    {
        var dir = Path.Combine(Path.GetTempPath(), "RAWtoJXL_Cleanup_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var output = Path.Combine(dir, "out.jxl");
        try
        {
            var svc = CreateService(encoder: new ThrowingEncoder());

            await Assert.ThrowsAsync<IOException>(() =>
                svc.ConvertToJxlAsync(Path.Combine(dir, "in.arw"), output, _ => { }, 90));

            Assert.False(File.Exists(output), "partial output should have been deleted");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task ConvertToJxlAsync_EncoderFailsButOutputPreExisted_KeepsExistingFile()
    {
        var dir = Path.Combine(Path.GetTempPath(), "RAWtoJXL_Cleanup_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var output = Path.Combine(dir, "out.jxl");
        File.WriteAllText(output, "original");
        try
        {
            var svc = CreateService(encoder: new ThrowingEncoder());

            await Assert.ThrowsAsync<IOException>(() =>
                svc.ConvertToJxlAsync(Path.Combine(dir, "in.arw"), output, _ => { }, 90));

            Assert.True(File.Exists(output), "pre-existing output should be kept");
            Assert.Equal("partial", File.ReadAllText(output));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task ConvertToJpegAsync_ConverterFailsAfterWritingOutput_DeletesPartialOutput()
    {
        var dir = Path.Combine(Path.GetTempPath(), "RAWtoJXL_Cleanup_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var output = Path.Combine(dir, "out.jpg");
        try
        {
            var converter = new Mock<IImageConverterService>();
            converter.Setup(x => x.ConvertToJpegAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Callback<string, string, int, CancellationToken>((_, outPath, _, _) => File.WriteAllText(outPath, "partial"))
                .ThrowsAsync(new IOException("simulated JPEG failure"));
            var svc = CreateService(converter: converter.Object);

            await Assert.ThrowsAsync<IOException>(() =>
                svc.ConvertToJxlAsync(Path.Combine(dir, "in.arw"), output, _ => { }, 90, OutputFormat.Jpeg));

            Assert.False(File.Exists(output), "partial output should have been deleted");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task ConvertToAvifOutputAsync_ConverterFailsAfterWritingOutput_DeletesPartialOutput()
    {
        var dir = Path.Combine(Path.GetTempPath(), "RAWtoJXL_Cleanup_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var output = Path.Combine(dir, "out.avif");
        try
        {
            var converter = new Mock<IImageConverterService>();
            converter.Setup(x => x.ConvertToAvifAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Callback<string, string, int, CancellationToken>((_, outPath, _, _) => File.WriteAllText(outPath, "partial"))
                .ThrowsAsync(new IOException("simulated AVIF failure"));
            var svc = CreateService(converter: converter.Object);

            await Assert.ThrowsAsync<IOException>(() =>
                svc.ConvertToJxlAsync(Path.Combine(dir, "in.arw"), output, _ => { }, 90, OutputFormat.Avif));

            Assert.False(File.Exists(output), "partial output should have been deleted");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task ConvertToJpegOutputAsync_JxlInputDecoderFails_DeletesPartialOutput()
    {
        var dir = Path.Combine(Path.GetTempPath(), "RAWtoJXL_Cleanup_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var output = Path.Combine(dir, "out.jpg");
        try
        {
            var decoder = new Mock<IJxlDecoder>();
            decoder.Setup(x => x.DecodeToPngAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<int>()))
                .Callback<string, string, CancellationToken, int>((_, outPath, _, _) => File.WriteAllText(outPath, "partial"))
                .ThrowsAsync(new IOException("simulated djxl failure"));
            var svc = CreateService(jxlDecoder: decoder.Object);

            await Assert.ThrowsAsync<IOException>(() =>
                svc.ConvertToJxlAsync(Path.Combine(dir, "in.jxl"), output, _ => { }, 90, OutputFormat.Jpeg));

            Assert.False(File.Exists(output), "partial output should have been deleted");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task ConvertToJpegOutputAsync_JxlInput_DecodesTempPngThenConverts()
    {
        var dir = Path.Combine(Path.GetTempPath(), "RAWtoJXL_Cleanup_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var output = Path.Combine(dir, "out.jpg");
        try
        {
            string? decodedTemp = null;
            var decoder = new Mock<IJxlDecoder>();
            decoder.Setup(x => x.DecodeToPngAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<int>()))
                .Callback<string, string, CancellationToken, int>((_, outPath, _, _) =>
                {
                    decodedTemp = outPath;
                    File.WriteAllText(outPath, "png");
                })
                .Returns(Task.CompletedTask);

            string? converterInput = null;
            var converter = new Mock<IImageConverterService>();
            converter.Setup(x => x.ConvertToJpegAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Callback<string, string, int, CancellationToken>((inPath, outPath, _, _) =>
                {
                    converterInput = inPath;
                    File.WriteAllText(outPath, "jpg");
                })
                .Returns(Task.CompletedTask);

            var exiftool = new Mock<IExiftoolService>();

            var svc = CreateService(converter: converter.Object, jxlDecoder: decoder.Object, exiftool: exiftool.Object);

            await svc.ConvertToJxlAsync(Path.Combine(dir, "in.jxl"), output, _ => { }, 90, OutputFormat.Jpeg);

            Assert.True(File.Exists(output));
            Assert.NotNull(decodedTemp);
            Assert.Equal(decodedTemp, converterInput);
            Assert.True(File.Exists(decodedTemp) == false, "temp PNG should be cleaned up");
            exiftool.Verify(x => x.EmbedMetadataAsync(Path.Combine(dir, "in.jxl"), output, It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
