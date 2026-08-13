using Moq;
using RAWtoJXL.Cli;
using RAWtoJXL.Core.Interfaces;
using RAWtoJXL.Core.Settings;

namespace RAWtoJXL.Tests.Cli;

public class ConversionRunnerTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "rawtojxl_runner_" + Guid.NewGuid().ToString("N"));

    public ConversionRunnerTests()
    {
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private string CreateFile(string name)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, "dummy");
        return path;
    }

    private static ResolvedOptions Options(
        ConflictResolution conflict = ConflictResolution.Overwrite,
        string? customOutputDirectory = null,
        int? threads = null) => new(
        Format: OutputFormat.Jxl,
        Quality: 90,
        Conflict: conflict,
        UseSubfolder: false,
        SubfolderName: "jxl_output",
        UseCustomOutputDirectory: customOutputDirectory != null,
        CustomOutputDirectory: customOutputDirectory ?? "",
        SkipMetadata: false,
        Effort: 7,
        Threads: threads,
        Recursive: false,
        Extensions: RAWtoJXL.Core.Models.SupportedFormats.RawExtensions,
        Include: Array.Empty<string>(),
        Exclude: Array.Empty<string>(),
        ModifiedAfter: null,
        ModifiedBefore: null);

    private static Mock<IImageService> CreateImageServiceMock()
    {
        var mock = new Mock<IImageService>();
        mock.Setup(s => s.ConvertToJxlAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Action<double>>(),
                It.IsAny<int>(),
                It.IsAny<OutputFormat>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()))
            .Returns(Task.CompletedTask);
        return mock;
    }

    private static void SimulateOutputFile(string inputPath)
    {
        var output = Path.ChangeExtension(inputPath, ".jxl");
        if (!File.Exists(output))
        {
            File.WriteAllText(output, "converted");
        }
    }

    [Fact]
    public async Task Sequential_ConvertsAllInOrder()
    {
        var files = new[] { CreateFile("a.ARW"), CreateFile("b.ARW"), CreateFile("c.ARW") };
        var mock = CreateImageServiceMock();
        mock.Setup(s => s.ConvertToJxlAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Action<double>>(), It.IsAny<int>(), It.IsAny<OutputFormat>(),
                It.IsAny<CancellationToken>(), It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .Returns<string, string, Action<double>, int, OutputFormat, CancellationToken, bool, int?, int?>(
                (input, output, progress, quality, format, ct, skip, effort, threads) =>
                {
                    SimulateOutputFile(input);
                    return Task.CompletedTask;
                });

        var runner = new ConversionRunner(mock.Object);
        var batch = await runner.RunAsync(files, Options(), jobs: 1, progress: null, fileCompleted: null, CancellationToken.None);

        Assert.Equal(3, batch.Converted);
        Assert.Equal(0, batch.Failed);
        Assert.False(batch.Cancelled);
        Assert.Equal(files, batch.Files.Select(f => f.Input).ToArray());
        Assert.All(batch.Files, f => Assert.Equal("converted", f.Status));
        Assert.All(batch.Files, f => Assert.EndsWith(".jxl", f.Output));
    }

    [Fact]
    public async Task Sequential_UsesAllCoresWhenThreadsUnset()
    {
        var file = CreateFile("a.ARW");
        var mock = CreateImageServiceMock();
        int? capturedThreads = -1;
        mock.Setup(s => s.ConvertToJxlAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Action<double>>(), It.IsAny<int>(), It.IsAny<OutputFormat>(),
                It.IsAny<CancellationToken>(), It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .Callback<string, string, Action<double>, int, OutputFormat, CancellationToken, bool, int?, int?>(
                (input, output, progress, quality, format, ct, skip, effort, threads) => capturedThreads = threads)
            .Returns(Task.CompletedTask);

        var runner = new ConversionRunner(mock.Object);
        await runner.RunAsync(new[] { file }, Options(), jobs: 1, progress: null, fileCompleted: null, CancellationToken.None);

        Assert.Equal(Environment.ProcessorCount, capturedThreads);
    }

    [Fact]
    public async Task Sequential_ExplicitThreadsOverride_ArePassedThrough()
    {
        var file = CreateFile("a.ARW");
        var mock = CreateImageServiceMock();
        int? capturedThreads = -1;
        mock.Setup(s => s.ConvertToJxlAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Action<double>>(), It.IsAny<int>(), It.IsAny<OutputFormat>(),
                It.IsAny<CancellationToken>(), It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .Callback<string, string, Action<double>, int, OutputFormat, CancellationToken, bool, int?, int?>(
                (input, output, progress, quality, format, ct, skip, effort, threads) => capturedThreads = threads)
            .Returns(Task.CompletedTask);

        var runner = new ConversionRunner(mock.Object);
        await runner.RunAsync(new[] { file }, Options(threads: 3), jobs: 1, progress: null, fileCompleted: null, CancellationToken.None);

        Assert.Equal(3, capturedThreads);
    }

    [Fact]
    public async Task Sequential_SkipConflict_ExistingOutput()
    {
        var file = CreateFile("a.ARW");
        SimulateOutputFile(file);
        var mock = CreateImageServiceMock();

        var runner = new ConversionRunner(mock.Object);
        var batch = await runner.RunAsync(new[] { file }, Options(ConflictResolution.Skip), jobs: 1, progress: null, fileCompleted: null, CancellationToken.None);

        Assert.Equal(0, batch.Converted);
        Assert.Equal(1, batch.Skipped);
        Assert.Equal("skipped", batch.Files[0].Status);
        mock.Verify(s => s.ConvertToJxlAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Action<double>>(),
            It.IsAny<int>(), It.IsAny<OutputFormat>(), It.IsAny<CancellationToken>(),
            It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<int?>()), Times.Never);
    }

    [Fact]
    public async Task Sequential_FailureIsolation()
    {
        var files = new[] { CreateFile("a.ARW"), CreateFile("b.ARW"), CreateFile("c.ARW") };
        var mock = CreateImageServiceMock();
        mock.Setup(s => s.ConvertToJxlAsync(It.Is<string>(p => p.EndsWith("b.ARW")), It.IsAny<string>(),
                It.IsAny<Action<double>>(), It.IsAny<int>(), It.IsAny<OutputFormat>(),
                It.IsAny<CancellationToken>(), It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var runner = new ConversionRunner(mock.Object);
        var batch = await runner.RunAsync(files, Options(), jobs: 1, progress: null, fileCompleted: null, CancellationToken.None);

        Assert.Equal(2, batch.Converted);
        Assert.Equal(1, batch.Failed);
        Assert.Equal("failed", batch.Files[1].Status);
        Assert.Equal("boom", batch.Files[1].Error);
    }

    [Fact]
    public async Task Sequential_CancelledToken_StopsBeforeStart()
    {
        var files = new[] { CreateFile("a.ARW"), CreateFile("b.ARW") };
        var mock = CreateImageServiceMock();

        var runner = new ConversionRunner(mock.Object);
        var batch = await runner.RunAsync(files, Options(), jobs: 1, progress: null, fileCompleted: null, new CancellationToken(canceled: true));

        Assert.True(batch.Cancelled);
        Assert.Equal(0, batch.Converted);
        mock.Verify(s => s.ConvertToJxlAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Action<double>>(),
            It.IsAny<int>(), It.IsAny<OutputFormat>(), It.IsAny<CancellationToken>(),
            It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<int?>()), Times.Never);
    }

    [Fact]
    public async Task Sequential_CancellationDuringConversion()
    {
        var files = new[] { CreateFile("a.ARW"), CreateFile("b.ARW") };
        var mock = CreateImageServiceMock();
        mock.Setup(s => s.ConvertToJxlAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Action<double>>(), It.IsAny<int>(), It.IsAny<OutputFormat>(),
                It.IsAny<CancellationToken>(), It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ThrowsAsync(new OperationCanceledException());

        var runner = new ConversionRunner(mock.Object);
        var batch = await runner.RunAsync(files, Options(), jobs: 1, progress: null, fileCompleted: null, CancellationToken.None);

        Assert.True(batch.Cancelled);
        Assert.Equal(0, batch.Converted);
        Assert.Single(batch.Files);
        Assert.Equal("cancelled", batch.Files[0].Status);
    }

    [Fact]
    public async Task Sequential_ProgressCallback_Fires()
    {
        var file = CreateFile("a.ARW");
        var mock = CreateImageServiceMock();
        var progressCalls = new List<double>();

        var runner = new ConversionRunner(mock.Object);
        await runner.RunAsync(new[] { file }, Options(), jobs: 1,
            progress: (index, total, fraction, name) => progressCalls.Add(fraction),
            fileCompleted: null, CancellationToken.None);

        Assert.NotEmpty(progressCalls);
    }

    [Fact]
    public async Task Sequential_FileCompletedCallback_FiresPerFile()
    {
        var files = new[] { CreateFile("a.ARW"), CreateFile("b.ARW") };
        var mock = CreateImageServiceMock();
        var completions = new List<(FileResult Result, int Completed)>();

        var runner = new ConversionRunner(mock.Object);
        await runner.RunAsync(files, Options(), jobs: 1, progress: null,
            fileCompleted: (result, completed) => completions.Add((result, completed)),
            CancellationToken.None);

        Assert.Equal(2, completions.Count);
        Assert.Equal(new[] { 1, 2 }, completions.Select(c => c.Completed).ToArray());
    }

    [Fact]
    public async Task Parallel_ConvertsAllAndPreservesOrder()
    {
        var files = new[] { CreateFile("a.ARW"), CreateFile("b.ARW"), CreateFile("c.ARW"), CreateFile("d.ARW") };
        var mock = CreateImageServiceMock();
        mock.Setup(s => s.ConvertToJxlAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Action<double>>(), It.IsAny<int>(), It.IsAny<OutputFormat>(),
                It.IsAny<CancellationToken>(), It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .Returns<string, string, Action<double>, int, OutputFormat, CancellationToken, bool, int?, int?>(
                async (input, output, progress, quality, format, ct, skip, effort, threads) =>
                {
                    await Task.Delay(20);
                    SimulateOutputFile(input);
                });

        var runner = new ConversionRunner(mock.Object);
        var batch = await runner.RunAsync(files, Options(), jobs: 4, progress: null, fileCompleted: null, CancellationToken.None);

        Assert.Equal(4, batch.Converted);
        Assert.Equal(0, batch.Failed);
        Assert.Equal(files, batch.Files.Select(f => f.Input).ToArray());
    }

    [Fact]
    public async Task Parallel_SplitsThreadsAcrossJobs()
    {
        var files = new[] { CreateFile("a.ARW"), CreateFile("b.ARW"), CreateFile("c.ARW"), CreateFile("d.ARW") };
        var mock = CreateImageServiceMock();
        var capturedThreads = new List<int?>();
        mock.Setup(s => s.ConvertToJxlAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Action<double>>(), It.IsAny<int>(), It.IsAny<OutputFormat>(),
                It.IsAny<CancellationToken>(), It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .Callback<string, string, Action<double>, int, OutputFormat, CancellationToken, bool, int?, int?>(
                (input, output, progress, quality, format, ct, skip, effort, threads) => capturedThreads.Add(threads))
            .Returns(Task.CompletedTask);

        var runner = new ConversionRunner(mock.Object);
        await runner.RunAsync(files, Options(), jobs: 4, progress: null, fileCompleted: null, CancellationToken.None);

        var expected = Math.Max(1, Environment.ProcessorCount / 4);
        Assert.All(capturedThreads, t => Assert.Equal(expected, t));
    }

    [Fact]
    public async Task Parallel_FailureIsolation()
    {
        var files = new[] { CreateFile("a.ARW"), CreateFile("b.ARW"), CreateFile("c.ARW") };
        var mock = CreateImageServiceMock();
        mock.Setup(s => s.ConvertToJxlAsync(It.Is<string>(p => p.EndsWith("b.ARW")), It.IsAny<string>(),
                It.IsAny<Action<double>>(), It.IsAny<int>(), It.IsAny<OutputFormat>(),
                It.IsAny<CancellationToken>(), It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ThrowsAsync(new IOException("disk error"));

        var runner = new ConversionRunner(mock.Object);
        var batch = await runner.RunAsync(files, Options(), jobs: 3, progress: null, fileCompleted: null, CancellationToken.None);

        Assert.Equal(2, batch.Converted);
        Assert.Equal(1, batch.Failed);
        Assert.Equal("failed", batch.Files[1].Status);
    }

    [Fact]
    public async Task Parallel_AppendNumber_NoCollisions()
    {
        var dirA = Path.Combine(_dir, "a");
        var dirB = Path.Combine(_dir, "b");
        Directory.CreateDirectory(dirA);
        Directory.CreateDirectory(dirB);
        var fileA = Path.Combine(dirA, "photo.ARW");
        var fileB = Path.Combine(dirB, "photo.ARW");
        File.WriteAllText(fileA, "x");
        File.WriteAllText(fileB, "x");
        var outputDir = Path.Combine(_dir, "out");

        var mock = CreateImageServiceMock();
        var outputs = new List<string>();
        mock.Setup(s => s.ConvertToJxlAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Action<double>>(), It.IsAny<int>(), It.IsAny<OutputFormat>(),
                It.IsAny<CancellationToken>(), It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .Callback<string, string, Action<double>, int, OutputFormat, CancellationToken, bool, int?, int?>(
                (input, output, progress, quality, format, ct, skip, effort, threads) => outputs.Add(output))
            .Returns(Task.CompletedTask);

        var runner = new ConversionRunner(mock.Object);
        var batch = await runner.RunAsync(new[] { fileA, fileB },
            Options(ConflictResolution.AppendNumber, customOutputDirectory: outputDir),
            jobs: 2, progress: null, fileCompleted: null, CancellationToken.None);

        Assert.Equal(2, batch.Converted);
        Assert.Equal(2, outputs.Distinct().Count());
    }

    [Fact]
    public async Task Parallel_DuplicateOutputPaths_OneFailsInsteadOfCorrupting()
    {
        var dirA = Path.Combine(_dir, "a");
        var dirB = Path.Combine(_dir, "b");
        Directory.CreateDirectory(dirA);
        Directory.CreateDirectory(dirB);
        var fileA = Path.Combine(dirA, "photo.ARW");
        var fileB = Path.Combine(dirB, "photo.CR3");
        File.WriteAllText(fileA, "x");
        File.WriteAllText(fileB, "x");
        var outputDir = Path.Combine(_dir, "out");

        var mock = CreateImageServiceMock();

        var runner = new ConversionRunner(mock.Object);
        var batch = await runner.RunAsync(new[] { fileA, fileB },
            Options(ConflictResolution.Overwrite, customOutputDirectory: outputDir),
            jobs: 2, progress: null, fileCompleted: null, CancellationToken.None);

        Assert.Equal(1, batch.Converted);
        Assert.Equal(1, batch.Failed);
        var failedResult = batch.Files.Single(f => f.Status == "failed");
        Assert.Contains("already used", failedResult.Error);
        mock.Verify(s => s.ConvertToJxlAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Action<double>>(),
            It.IsAny<int>(), It.IsAny<OutputFormat>(), It.IsAny<CancellationToken>(),
            It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<int?>()), Times.Once);
    }

    [Fact]
    public async Task Parallel_CancelledToken_StopsEarly()
    {
        var files = new[] { CreateFile("a.ARW"), CreateFile("b.ARW") };
        var mock = CreateImageServiceMock();

        var runner = new ConversionRunner(mock.Object);
        var batch = await runner.RunAsync(files, Options(), jobs: 2, progress: null, fileCompleted: null, new CancellationToken(canceled: true));

        Assert.True(batch.Cancelled);
        Assert.Equal(0, batch.Converted);
    }

    [Fact]
    public async Task Parallel_FileCompletedCallback_CountsAll()
    {
        var files = new[] { CreateFile("a.ARW"), CreateFile("b.ARW") };
        var mock = CreateImageServiceMock();
        var completions = new List<int>();

        var runner = new ConversionRunner(mock.Object);
        await runner.RunAsync(files, Options(), jobs: 2, progress: null,
            fileCompleted: (result, completed) => completions.Add(completed),
            CancellationToken.None);

        Assert.Equal(2, completions.Count);
        Assert.Equal(new[] { 1, 2 }, completions.OrderBy(x => x).ToArray());
    }
}
