using System.IO;
using Moq;
using RAWtoJXL.Core.Interfaces;
using RAWtoJXL.Core.Models;
using RAWtoJXL.Core.Services;

namespace RAWtoJXL.Tests;

public class ExiftoolServiceLockDetectionTests
{
    private const string SourcePath = @"C:\src\img.arw";
    private const string OutputPath = @"C:\out\img.jxl";

    private static ExiftoolService CreateService(string? stderr)
    {
        var runner = new Mock<IProcessRunner>();
        runner.Setup(x => x.FindExiftoolAsync(It.IsAny<string?>()))
              .ReturnsAsync(@"C:\fake\exiftool.exe");
        runner.Setup(x => x.RunProcessAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((1, null, stderr));

        var logger = new FileLogger();
        return new ExiftoolService(runner.Object, new FileService(logger), logger);
    }

    [Fact]
    public async Task EmbedMetadataAsync_StderrMentionsSourceFile_ThrowsFileLockedExceptionForSource()
    {
        var service = CreateService($"Error: File not found or cannot open {SourcePath} - permission denied");

        var ex = await Assert.ThrowsAsync<FileLockedException>(() =>
            service.EmbedMetadataAsync(SourcePath, OutputPath, TestContext.Current.CancellationToken));

        Assert.Equal(SourcePath, ex.FilePath);
    }

    [Fact]
    public async Task EmbedMetadataAsync_StderrMentionsOutputFile_ThrowsFileLockedExceptionForOutput()
    {
        var service = CreateService($"Error: Cannot open {OutputPath.Replace(@"\", "/")} - permission denied");

        var ex = await Assert.ThrowsAsync<FileLockedException>(() =>
            service.EmbedMetadataAsync(SourcePath, OutputPath, TestContext.Current.CancellationToken));

        Assert.Equal(OutputPath, ex.FilePath);
    }

    [Fact]
    public async Task EmbedMetadataAsync_StderrIsUnrelated_ThrowsGenericIOException()
    {
        var service = CreateService("Error: Unknown image format");

        await Assert.ThrowsAsync<IOException>(() =>
            service.EmbedMetadataAsync(SourcePath, OutputPath, TestContext.Current.CancellationToken));
    }
}
