using Avalonia.Headless.XUnit;
using ImageMagick;
using Moq;
using RAWtoJXL.Avalonia.Services;
using RAWtoJXL.Avalonia.ViewModels;
using RAWtoJXL.Core.Interfaces;
using RAWtoJXL.Core.Models;

namespace RAWtoJXL.Tests.GUITests;

[Trait("category", "gui")]
public class CompareReconvertThreadingTests
{
    [AvaloniaFact]
    public async Task InitialLoad_SplitsThreadsAcrossBothConversionPanes()
    {
        var context = CreateContext();
        try
        {
            await context.ViewModel.InitializeAsync();
            await Task.Delay(100, TestContext.Current.CancellationToken);

            int expected = CompareDefaults.GetJobThreads(2);
            Assert.Contains(context.CapturedThreads, entry => entry.Format == OutputFormat.Jxl && entry.Threads == expected);
            Assert.Contains(context.CapturedThreads, entry => entry.Format == OutputFormat.Avif && entry.Threads == expected);
        }
        finally
        {
            context.Dispose();
        }
    }

    [AvaloniaFact]
    public async Task EffortOnlyReconvert_ReencodesJxlPaneWithAllThreads()
    {
        var context = CreateContext();
        try
        {
            await context.ViewModel.InitializeAsync();
            await Task.Delay(100, TestContext.Current.CancellationToken);
            context.CapturedThreads.Clear();

            context.ViewModel.JxlEffort = 7;
            context.ViewModel.TriggerReconvertTick();
            await Task.Delay(200, TestContext.Current.CancellationToken);

            Assert.Contains(context.CapturedThreads,
                entry => entry.Format == OutputFormat.Jxl && entry.Threads == CompareDefaults.JxlThreads);
            Assert.DoesNotContain(context.CapturedThreads, entry => entry.Format == OutputFormat.Avif);
        }
        finally
        {
            context.Dispose();
        }
    }

    [AvaloniaFact]
    public async Task QualityReconvert_SplitsThreadsBetweenJxlAndAvifPanes()
    {
        var context = CreateContext();
        try
        {
            await context.ViewModel.InitializeAsync();
            await Task.Delay(100, TestContext.Current.CancellationToken);
            context.CapturedThreads.Clear();

            context.ViewModel.Quality = 80;
            context.ViewModel.TriggerReconvertTick();
            await Task.Delay(200, TestContext.Current.CancellationToken);

            int expected = CompareDefaults.GetJobThreads(2);
            Assert.Contains(context.CapturedThreads, entry => entry.Format == OutputFormat.Jxl && entry.Threads == expected);
            Assert.Contains(context.CapturedThreads, entry => entry.Format == OutputFormat.Avif && entry.Threads == expected);
        }
        finally
        {
            context.Dispose();
        }
    }

    private static TestContextData CreateContext()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"RAWtoJXL_CompareThreads_{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string sourcePath = Path.Combine(directory, "source.png");
        using (var image = new MagickImage(MagickColors.SteelBlue, 64, 48))
        {
            image.Write(sourcePath);
        }

        var service = new Mock<ICompareConversionService>();
        var captured = new List<(OutputFormat? Format, int? Threads)>();
        service.Setup(item => item.EnsureDisplayPngsAsync(
                It.IsAny<string>(),
                It.IsAny<OutputFormat?>(),
                It.IsAny<int>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<int?>()))
            .Callback<string, OutputFormat?, int, int?, CancellationToken, int?>(
                (_, format, _, _, _, threads) => captured.Add((format, threads)))
            .ReturnsAsync(new CompareDisplayPngs(sourcePath, string.Empty, 64, 48));
        service.Setup(item => item.EnsureTargetFileAsync(
                It.IsAny<string>(),
                It.IsAny<OutputFormat>(),
                It.IsAny<int>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<int?>()))
            .Callback<string, OutputFormat, int, int?, CancellationToken, int?>(
                (_, format, _, _, _, threads) => captured.Add((format, threads)))
            .ReturnsAsync(sourcePath);

        var dispatcher = new Mock<IDispatcherService>();
        dispatcher.Setup(item => item.InvokeAsync(It.IsAny<Action>()))
            .Returns<Action>(action =>
            {
                action();
                return Task.CompletedTask;
            });

        var viewModel = new CompareViewModel(sourcePath, 90, service.Object, dispatcher.Object);
        return new TestContextData(directory, sourcePath, captured, viewModel);
    }

    private sealed record TestContextData(
        string Directory,
        string SourcePath,
        List<(OutputFormat? Format, int? Threads)> CapturedThreads,
        CompareViewModel ViewModel) : IDisposable
    {
        public void Dispose()
        {
            ViewModel.Dispose();
            foreach (var pane in ViewModel.Panes)
            {
                pane.Preview?.Dispose();
            }
            System.IO.Directory.Delete(Directory, true);
        }
    }
}
