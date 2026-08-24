using Avalonia.Headless.XUnit;
using ImageMagick;
using Moq;
using RAWtoJXL.Avalonia.Controls;
using RAWtoJXL.Avalonia.Services;
using RAWtoJXL.Avalonia.ViewModels;
using RAWtoJXL.Core.Interfaces;

namespace RAWtoJXL.Tests.GUITests;

[Trait("category", "gui")]
public class CompareAnalysisTests
{
    [AvaloniaFact]
    public async Task RapidViewportChanges_AnalyzeOnlyFinalVisibleRegion()
    {
        var context = CreateContext();
        try
        {
            await context.ViewModel.InitializeAsync();
            var first = new CompareImageRegion(0, 0, 0.5, 0.5);
            var final = new CompareImageRegion(0.4, 0.3, 0.8, 0.7);

            context.ViewModel.OnPaneViewportChanged(
                context.ViewModel.MiddlePane,
                new CompareViewport(1, 0.25, 0.25),
                first,
                400,
                300,
                9556,
                6366);
            context.ViewModel.OnPaneViewportChanged(
                context.ViewModel.MiddlePane,
                new CompareViewport(2, 0.6, 0.5),
                final,
                500,
                350,
                9556,
                6366);

            await Task.Delay(500, TestContext.Current.CancellationToken);

            context.Service.Verify(service => service.AnalyzeViewportAsync(
                context.SourcePath,
                OutputFormat.Jxl,
                90,
                5,
                final,
                false,
                500,
                350,
                false,
                It.IsAny<CancellationToken>()), Times.Once);
            Assert.Equal(0.9876, context.ViewModel.MiddlePane.ViewportSsim);
            Assert.Equal("SSIM 0.9876", context.ViewModel.MiddlePane.SsimText);
        }
        finally
        {
            context.Dispose();
        }
    }

    [AvaloniaFact]
    public async Task FullDisplay_RequestsFullResolutionAnalysis()
    {
        var context = CreateContext();
        try
        {
            await context.ViewModel.InitializeAsync();
            var region = new CompareImageRegion(0.2, 0.1, 0.7, 0.8);

            context.ViewModel.OnPaneViewportChanged(
                context.ViewModel.MiddlePane,
                new CompareViewport(2, 0.45, 0.45),
                region,
                520,
                420,
                9556,
                6366);
            context.ViewModel.OnPaneDisplayStateChanged(
                context.ViewModel.MiddlePane,
                CompareDisplayState.Full);

            await Task.Delay(500, TestContext.Current.CancellationToken);

            context.Service.Verify(service => service.AnalyzeViewportAsync(
                context.SourcePath,
                OutputFormat.Jxl,
                90,
                5,
                region,
                true,
                520,
                420,
                false,
                It.IsAny<CancellationToken>()), Times.Once);
            Assert.Equal(CompareDisplayState.Full, context.ViewModel.MiddlePane.DisplayState);
            Assert.Equal("Full", context.ViewModel.MiddlePane.DisplayStateText);
        }
        finally
        {
            context.Dispose();
        }
    }

    private static TestContextData CreateContext()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"RAWtoJXL_CompareAnalysis_{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string sourcePath = Path.Combine(directory, "source.png");
        using (var image = new MagickImage(MagickColors.SteelBlue, 64, 48))
        {
            image.Write(sourcePath);
        }

        var service = new Mock<ICompareConversionService>();
        service.Setup(item => item.EnsureDisplayPngsAsync(
                It.IsAny<string>(),
                It.IsAny<OutputFormat?>(),
                It.IsAny<int>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<int?>()))
            .ReturnsAsync(new CompareDisplayPngs(sourcePath, string.Empty, 64, 48));
        service.Setup(item => item.EnsureTargetFileAsync(
                It.IsAny<string>(),
                It.IsAny<OutputFormat>(),
                It.IsAny<int>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<int?>()))
            .ReturnsAsync(sourcePath);
        service.Setup(item => item.AnalyzeViewportAsync(
                It.IsAny<string>(),
                It.IsAny<OutputFormat>(),
                It.IsAny<int>(),
                It.IsAny<int?>(),
                It.IsAny<CompareImageRegion>(),
                It.IsAny<bool>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, OutputFormat, int, int?, CompareImageRegion, bool, int, int, bool, CancellationToken>(
                (_, _, _, _, region, _, _, _, _, _) =>
                    Task.FromResult(new CompareViewportAnalysis(0.9876, region, null)));

        var dispatcher = new Mock<IDispatcherService>();
        dispatcher.Setup(item => item.InvokeAsync(It.IsAny<Action>()))
            .Returns<Action>(action =>
            {
                action();
                return Task.CompletedTask;
            });

        var viewModel = new CompareViewModel(sourcePath, 90, service.Object, dispatcher.Object);
        return new TestContextData(directory, sourcePath, service, viewModel);
    }

    private sealed record TestContextData(
        string Directory,
        string SourcePath,
        Mock<ICompareConversionService> Service,
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
