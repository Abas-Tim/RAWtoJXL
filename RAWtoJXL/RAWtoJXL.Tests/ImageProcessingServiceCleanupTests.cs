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
        IExiftoolService? exiftool = null)
    {
        var logger = new FileLogger();
        return new ImageProcessingService(
            converter ?? new Mock<IImageConverterService>().Object,
            encoder ?? new Mock<ICjxlEncoder>().Object,
            new FileService(logger),
            logger,
            exiftool ?? new Mock<IExiftoolService>().Object);
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
    public async Task ConvertToPngOutputAsync_ConverterFailsAfterWritingOutput_DeletesPartialOutput()
    {
        var dir = Path.Combine(Path.GetTempPath(), "RAWtoJXL_Cleanup_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var output = Path.Combine(dir, "out.png");
        try
        {
            var converter = new Mock<IImageConverterService>();
            converter.Setup(x => x.ConvertToPngAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback<string, string, CancellationToken>((_, outPath, _) => File.WriteAllText(outPath, "partial"))
                .ThrowsAsync(new IOException("simulated PNG failure"));
            var svc = CreateService(converter: converter.Object);

            await Assert.ThrowsAsync<IOException>(() =>
                svc.ConvertToJxlAsync(Path.Combine(dir, "in.arw"), output, _ => { }, 90, OutputFormat.Png));

            Assert.False(File.Exists(output), "partial output should have been deleted");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
