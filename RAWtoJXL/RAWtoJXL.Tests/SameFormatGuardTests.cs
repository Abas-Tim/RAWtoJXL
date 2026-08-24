using System.IO;
using Moq;
using RAWtoJXL.Core.Interfaces;
using RAWtoJXL.Core.Services;

namespace RAWtoJXL.Tests;

public class SameFormatGuardTests
{
    private static ImageProcessingService CreateService(
        Mock<ICjxlEncoder>? encoder = null,
        Mock<IImageConverterService>? converter = null,
        Mock<IJxlDecoder>? decoder = null)
    {
        var logger = new FileLogger();
        return new ImageProcessingService(
            (converter ?? new Mock<IImageConverterService>()).Object,
            (encoder ?? new Mock<ICjxlEncoder>()).Object,
            new FileService(logger),
            logger,
            new Mock<IExiftoolService>().Object,
            (decoder ?? new Mock<IJxlDecoder>()).Object);
    }

    private static string TempInput(string fileName)
    {
        var dir = Path.Combine(Path.GetTempPath(), "RAWtoJXL_SameFormat_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, fileName);
    }

    [Theory]
    [InlineData("photo.jpg", OutputFormat.Jpeg)]
    [InlineData("photo.jpeg", OutputFormat.Jpeg)]
    [InlineData("photo.JPG", OutputFormat.Jpeg)]
    [InlineData("photo.avif", OutputFormat.Avif)]
    [InlineData("photo.jxl", OutputFormat.Jxl)]
    public async Task Convert_SameFormat_ThrowsInvalidOperationException(string fileName, OutputFormat format)
    {
        var input = TempInput(fileName);
        try
        {
            var svc = CreateService();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                svc.ConvertToJxlAsync(input, Path.ChangeExtension(input, ".out"), _ => { }, 90, format));

            Assert.Contains("same-format conversion", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(input)!, true);
        }
    }

    [Fact]
    public async Task Convert_JpegInputToJxl_RoutesToCjxlEncoder()
    {
        var input = TempInput("photo.jpg");
        try
        {
            var encoder = new Mock<ICjxlEncoder>();
            string? ppmSource = null;
            encoder.Setup(x => x.EncodeFromStreamAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(),
                    It.IsAny<Func<Stream, CancellationToken, Task>>(), It.IsAny<CancellationToken>(),
                    It.IsAny<int>(), It.IsAny<Action<double>?>(), It.IsAny<int?>(), It.IsAny<int?>()))
                .Callback<string, string, int, Func<Stream, CancellationToken, Task>, CancellationToken, int, Action<double>?, int?, int?>(
                    (inPath, _, _, _, _, _, _, _, _) => ppmSource = inPath)
                .Returns(Task.CompletedTask);

            var svc = CreateService(encoder: encoder);

            await svc.ConvertToJxlAsync(input, Path.Combine(Path.GetTempPath(), "out.jxl"), _ => { }, 80, OutputFormat.Jxl);

            Assert.Equal(input, ppmSource);
            encoder.Verify(x => x.EncodeFromStreamAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<Func<Stream, CancellationToken, Task>>(), It.IsAny<CancellationToken>(),
                It.IsAny<int>(), It.IsAny<Action<double>?>(), It.IsAny<int?>(), It.IsAny<int?>()), Times.Once);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(input)!, true);
        }
    }

    [Fact]
    public async Task Convert_AvifInputToJpeg_UsesAvifDecodingConverter()
    {
        var input = TempInput("photo.avif");
        try
        {
            var converter = new Mock<IImageConverterService>();
            string? converterInput = null;
            converter.Setup(x => x.ConvertToJpegAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<int?>()))
                .Callback<string, string, int, CancellationToken, int?>((inPath, _, _, _, _) => converterInput = inPath)
                .Returns(Task.CompletedTask);

            var svc = CreateService(converter: converter);

            await svc.ConvertToJxlAsync(input, Path.Combine(Path.GetTempPath(), "out.jpg"), _ => { }, 80, OutputFormat.Jpeg);

            Assert.Equal(input, converterInput);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(input)!, true);
        }
    }

    [Fact]
    public async Task Convert_JxlInputToAvif_DecodesThenConverts()
    {
        var input = TempInput("photo.jxl");
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
            converter.Setup(x => x.ConvertToAvifAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<int?>()))
                .Callback<string, string, int, CancellationToken, int?>((inPath, _, _, _, _) => converterInput = inPath)
                .Returns(Task.CompletedTask);

            var svc = CreateService(converter: converter, decoder: decoder);

            await svc.ConvertToJxlAsync(input, Path.Combine(Path.GetTempPath(), "out.avif"), _ => { }, 80, OutputFormat.Avif);

            Assert.NotNull(decodedTemp);
            Assert.Equal(decodedTemp, converterInput);
            Assert.False(File.Exists(decodedTemp), "temp PNG should be cleaned up");
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(input)!, true);
        }
    }
}
