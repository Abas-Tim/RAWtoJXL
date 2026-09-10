using System.IO;
using Moq;
using RAWtoJXL.Core.Interfaces;
using RAWtoJXL.Core.Services;

namespace RAWtoJXL.Tests;

public sealed class ImageProcessingServiceThreadBudgetTests
{
    [Fact]
    public async Task JxlPipeline_ForwardsThreadBudgetToEncoderAndPpmWriter()
    {
        var directory = CreateDirectory();
        try
        {
            var input = Path.Combine(directory, "input.arw");
            var output = Path.Combine(directory, "output.jxl");
            File.WriteAllText(input, "input");

            int? streamThreads = null;
            var converter = new Mock<IImageConverterService>();
            converter.Setup(service => service.StreamPpmToAsync(
                    It.IsAny<string>(),
                    It.IsAny<Stream>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<int?>()))
                .Callback<string, Stream, CancellationToken, int?>((_, _, _, threads) => streamThreads = threads)
                .Returns(Task.CompletedTask);

            var encoder = new CapturingEncoder();
            var service = CreateService(converter.Object, encoder);

            await service.ConvertToJxlAsync(
                input,
                output,
                _ => { },
                90,
                OutputFormat.Jxl,
                skipMetadata: true,
                threads: 5);

            Assert.Equal(5, encoder.Threads);
            Assert.Equal(5, streamThreads);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData(OutputFormat.Jpeg)]
    [InlineData(OutputFormat.Avif)]
    public async Task RasterPipeline_ForwardsThreadBudgetToSelectedConverter(OutputFormat format)
    {
        var directory = CreateDirectory();
        try
        {
            var input = Path.Combine(directory, "input.arw");
            var output = Path.Combine(directory, format == OutputFormat.Jpeg ? "output.jpg" : "output.avif");
            File.WriteAllText(input, "input");
            int? capturedThreads = null;

            var converter = new Mock<IImageConverterService>();
            converter.Setup(service => service.ConvertToJpegAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(),
                    It.IsAny<CancellationToken>(), It.IsAny<int?>()))
                .Callback<string, string, int, CancellationToken, int?>((_, _, _, _, threads) => capturedThreads = threads)
                .Returns(Task.CompletedTask);
            converter.Setup(service => service.ConvertToAvifAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(),
                    It.IsAny<CancellationToken>(), It.IsAny<int?>()))
                .Callback<string, string, int, CancellationToken, int?>((_, _, _, _, threads) => capturedThreads = threads)
                .Returns(Task.CompletedTask);

            var service = CreateService(converter.Object, new Mock<ICjxlEncoder>().Object);

            await service.ConvertToJxlAsync(
                input,
                output,
                _ => { },
                90,
                format,
                skipMetadata: true,
                threads: 5);

            Assert.Equal(5, capturedThreads);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static ImageProcessingService CreateService(
        IImageConverterService converter,
        ICjxlEncoder encoder)
    {
        var logger = new FileLogger();
        return new ImageProcessingService(
            converter,
            encoder,
            new FileService(logger),
            logger,
            new Mock<IExiftoolService>().Object,
            new Mock<IJxlDecoder>().Object);
    }

    private static string CreateDirectory()
    {
        var directory = Path.Combine(
            Path.GetTempPath(), "RAWtoJXL_ThreadBudget_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed class CapturingEncoder : ICjxlEncoder
    {
        public int? Threads { get; private set; }

        public async Task EncodeFromStreamAsync(
            string inputPath,
            string outputPath,
            int quality,
            Func<Stream, CancellationToken, Task> ppmWriter,
            CancellationToken cancellationToken,
            int timeoutSeconds,
            Action<double>? progress,
            int? effort,
            int? threads = null)
        {
            Threads = threads;
            using var stream = new MemoryStream();
            await ppmWriter(stream, cancellationToken);
        }

        public Task EncodeFromFileAsync(
            string inputPath,
            string outputPath,
            int quality,
            CancellationToken cancellationToken,
            int timeoutSeconds = 300,
            Action<double>? progress = null,
            int? effort = null,
            int? threads = null)
        {
            return Task.CompletedTask;
        }
    }
}
