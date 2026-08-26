using System.IO;
using Moq;
using RAWtoJXL.Core.Interfaces;
using RAWtoJXL.Core.Services;

namespace RAWtoJXL.Tests;

public class CjxlEncoderFileEncodingTests
{
    private static (CjxlEncoderService Service, Mock<IPathResolver> PathResolver, Mock<IProcessRunner> Runner, string Dir) CreateService(
        bool cjxlExists = true)
    {
        var dir = Path.Combine(Path.GetTempPath(), "RAWtoJXL_CjxlFile_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var cjxlPath = Path.Combine(dir, "cjxl.exe");
        if (cjxlExists)
        {
            File.WriteAllText(cjxlPath, "dummy");
        }

        var pathResolver = new Mock<IPathResolver>();
        pathResolver.Setup(x => x.ResolveCjxlPath()).Returns(cjxlExists ? cjxlPath : Path.Combine(dir, "missing_cjxl.exe"));
        var runner = new Mock<IProcessRunner>();
        var logger = new Mock<ILogger>();

        var service = new CjxlEncoderService(pathResolver.Object, logger.Object, runner.Object);
        return (service, pathResolver, runner, dir);
    }

    [Fact]
    public async Task EncodeFromFileAsync_Success_ProducesOutputFile()
    {
        var (service, _, runner, dir) = CreateService();
        var input = Path.Combine(dir, "in.png");
        var output = Path.Combine(dir, "out.jxl");
        File.WriteAllText(input, "fake png");

        runner.Setup(x => x.RunProcessWithTimeoutAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, int, CancellationToken>((_, args, _, _) =>
            {
                Assert.Contains("in.png", args);
                Assert.Contains("out.jxl", args);
                Assert.Contains("--effort=4", args);
                Assert.DoesNotContain(" - ", " " + args + " ");
                File.WriteAllText(output, "encoded");
            })
            .ReturnsAsync((0, string.Empty, string.Empty, false));

        try
        {
            await service.EncodeFromFileAsync(input, output, 90, CancellationToken.None, 300, null, 4);

            Assert.True(File.Exists(output));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task EncodeFromFileAsync_NonZeroExit_ThrowsCjxlEncodingException()
    {
        var (service, _, runner, dir) = CreateService();
        var input = Path.Combine(dir, "in.png");
        var output = Path.Combine(dir, "out.jxl");
        File.WriteAllText(input, "fake png");

        runner.Setup(x => x.RunProcessWithTimeoutAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((1, string.Empty, "encode error", false));

        try
        {
            var ex = await Assert.ThrowsAsync<CjxlEncodingException>(() =>
                service.EncodeFromFileAsync(input, output, 90, CancellationToken.None));

            Assert.Equal(1, ex.ExitCode);
            Assert.Contains("encode error", ex.Message);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task EncodeFromFileAsync_CancelledProcess_ThrowsOperationCanceledException()
    {
        var (service, _, runner, dir) = CreateService();
        var input = Path.Combine(dir, "in.png");
        var output = Path.Combine(dir, "out.jxl");
        File.WriteAllText(input, "fake png");
        using var cts = new CancellationTokenSource();

        runner.Setup(x => x.RunProcessWithTimeoutAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback(() => cts.Cancel())
            .ReturnsAsync((-1, string.Empty, "JPEG XL encoder was terminated", false));

        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                service.EncodeFromFileAsync(input, output, 100, cts.Token));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task EncodeFromFileAsync_Timeout_ThrowsTimeoutException()
    {
        var (service, _, runner, dir) = CreateService();
        var input = Path.Combine(dir, "in.png");
        var output = Path.Combine(dir, "out.jxl");
        File.WriteAllText(input, "fake png");

        runner.Setup(x => x.RunProcessWithTimeoutAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((0, string.Empty, string.Empty, true));

        try
        {
            await Assert.ThrowsAsync<TimeoutException>(() =>
                service.EncodeFromFileAsync(input, output, 90, CancellationToken.None));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task EncodeFromFileAsync_InputMissing_ThrowsFileNotFoundException()
    {
        var (service, _, _, dir) = CreateService();

        try
        {
            await Assert.ThrowsAsync<FileNotFoundException>(() =>
                service.EncodeFromFileAsync(Path.Combine(dir, "nope.png"), Path.Combine(dir, "out.jxl"), 90, CancellationToken.None));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task EncodeFromFileAsync_CjxlMissing_ThrowsFileNotFoundException()
    {
        var (service, _, _, dir) = CreateService(cjxlExists: false);
        var input = Path.Combine(dir, "in.png");
        File.WriteAllText(input, "fake png");

        try
        {
            var ex = await Assert.ThrowsAsync<FileNotFoundException>(() =>
                service.EncodeFromFileAsync(input, Path.Combine(dir, "out.jxl"), 90, CancellationToken.None));

            Assert.Contains("cjxl", ex.Message);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task EncodeFromFileAsync_OutputMissing_ThrowsIOException()
    {
        var (service, _, runner, dir) = CreateService();
        var input = Path.Combine(dir, "in.png");
        var output = Path.Combine(dir, "out.jxl");
        File.WriteAllText(input, "fake png");

        runner.Setup(x => x.RunProcessWithTimeoutAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((0, string.Empty, string.Empty, false));

        try
        {
            await Assert.ThrowsAnyAsync<IOException>(() =>
                service.EncodeFromFileAsync(input, output, 90, CancellationToken.None));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void BuildFileEncodingArguments_IncludesQualityEffortThreadsAndPaths()
    {
        var (service, _, _, dir) = CreateService();
        try
        {
            var args = service.BuildFileEncodingArguments(90, "in.png", "out.jxl", effortOverride: 4, threadsOverride: 2);

            Assert.Contains("--effort=4", args);
            Assert.Contains("--num_threads=2", args);
            Assert.Contains("--container=1", args);
            Assert.Equal("in.png", args[^2]);
            Assert.Equal("out.jxl", args[^1]);
            Assert.DoesNotContain("-", args);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void BuildFileEncodingArguments_LosslessQuality_UsesModular()
    {
        var (service, _, _, dir) = CreateService();
        try
        {
            var args = service.BuildFileEncodingArguments(100, "in.png", "out.jxl");

            Assert.Contains("--distance=0", args);
            Assert.Contains("--modular=1", args);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
