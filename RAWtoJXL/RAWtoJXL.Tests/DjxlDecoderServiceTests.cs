using System.IO;
using Moq;
using RAWtoJXL.Core.Interfaces;
using RAWtoJXL.Core.Services;

namespace RAWtoJXL.Tests;

public class DjxlDecoderServiceTests
{
    private static (DjxlDecoderService Service, Mock<IPathResolver> PathResolver, Mock<IProcessRunner> Runner, string Dir) CreateService(
        bool djxlExists = true)
    {
        var dir = Path.Combine(Path.GetTempPath(), "RAWtoJXL_Djxl_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var djxlPath = Path.Combine(dir, "djxl.exe");
        if (djxlExists)
        {
            File.WriteAllText(djxlPath, "dummy");
        }

        var pathResolver = new Mock<IPathResolver>();
        pathResolver.Setup(x => x.ResolveDjxlPath()).Returns(djxlExists ? djxlPath : Path.Combine(dir, "missing_djxl.exe"));
        var runner = new Mock<IProcessRunner>();
        var logger = new Mock<ILogger>();

        var service = new DjxlDecoderService(pathResolver.Object, logger.Object, runner.Object);
        return (service, pathResolver, runner, dir);
    }

    [Fact]
    public async Task DecodeToPngAsync_Success_ProducesOutputFile()
    {
        var (service, _, runner, dir) = CreateService();
        var input = Path.Combine(dir, "in.jxl");
        var output = Path.Combine(dir, "out.png");
        File.WriteAllText(input, "fake jxl");

        runner.Setup(x => x.RunProcessWithTimeoutAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, int, CancellationToken>((_, args, _, _) =>
            {
                Assert.Contains("in.jxl", args);
                Assert.Contains("out.png", args);
                File.WriteAllText(output, "decoded");
            })
            .ReturnsAsync((0, string.Empty, string.Empty, false));

        try
        {
            await service.DecodeToPngAsync(input, output);

            Assert.True(File.Exists(output));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task DecodeToPngAsync_NonZeroExit_ThrowsJxlDecodingException()
    {
        var (service, _, runner, dir) = CreateService();
        var input = Path.Combine(dir, "in.jxl");
        var output = Path.Combine(dir, "out.png");
        File.WriteAllText(input, "fake jxl");

        runner.Setup(x => x.RunProcessWithTimeoutAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((1, string.Empty, "decode error", false));

        try
        {
            var ex = await Assert.ThrowsAsync<JxlDecodingException>(() => service.DecodeToPngAsync(input, output));

            Assert.Equal(1, ex.ExitCode);
            Assert.Contains("decode error", ex.Message);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task DecodeToPngAsync_Timeout_ThrowsTimeoutException()
    {
        var (service, _, runner, dir) = CreateService();
        var input = Path.Combine(dir, "in.jxl");
        var output = Path.Combine(dir, "out.png");
        File.WriteAllText(input, "fake jxl");

        runner.Setup(x => x.RunProcessWithTimeoutAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((0, string.Empty, string.Empty, true));

        try
        {
            await Assert.ThrowsAsync<TimeoutException>(() => service.DecodeToPngAsync(input, output));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task DecodeToPngAsync_DjxlMissing_ThrowsFileNotFoundException()
    {
        var (service, _, _, dir) = CreateService(djxlExists: false);
        var input = Path.Combine(dir, "in.jxl");
        var output = Path.Combine(dir, "out.png");
        File.WriteAllText(input, "fake jxl");

        try
        {
            var ex = await Assert.ThrowsAsync<FileNotFoundException>(() => service.DecodeToPngAsync(input, output));

            Assert.Contains("djxl", ex.Message);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task DecodeToPngAsync_BareMissingToolName_ThrowsFileNotFoundException()
    {
        var (service, pathResolver, _, dir) = CreateService();
        var input = Path.Combine(dir, "in.jxl");
        var output = Path.Combine(dir, "out.png");
        File.WriteAllText(input, "fake jxl");
        pathResolver.Setup(x => x.ResolveDjxlPath()).Returns("djxl.exe");

        try
        {
            await Assert.ThrowsAsync<FileNotFoundException>(() => service.DecodeToPngAsync(input, output));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task DecodeToPngAsync_SuccessButNoOutput_ThrowsIOException()
    {
        var (service, _, runner, dir) = CreateService();
        var input = Path.Combine(dir, "in.jxl");
        var output = Path.Combine(dir, "out.png");
        File.WriteAllText(input, "fake jxl");

        runner.Setup(x => x.RunProcessWithTimeoutAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((0, string.Empty, string.Empty, false));

        try
        {
            await Assert.ThrowsAsync<IOException>(() => service.DecodeToPngAsync(input, output));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task DecodeToPngAsync_InputMissing_ThrowsFileNotFoundException()
    {
        var (service, _, _, dir) = CreateService();

        try
        {
            await Assert.ThrowsAsync<FileNotFoundException>(() =>
                service.DecodeToPngAsync(Path.Combine(dir, "nope.jxl"), Path.Combine(dir, "out.png")));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
