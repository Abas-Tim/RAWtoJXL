using System.IO;
using Avalonia.Headless.XUnit;
using Moq;
using RAWtoJXL.Avalonia;
using RAWtoJXL.Avalonia.ViewModels;
using RAWtoJXL.Core.Interfaces;

namespace RAWtoJXL.Tests.GUITests;

[Trait("category", "gui")]
public class MainWindowConversionSummaryTests
{
    private static string CreateTempDir(out List<string> files, int count)
    {
        var dir = Path.Combine(Path.GetTempPath(), "RAWtoJXL_Summary_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        files = new List<string>();
        for (int i = 0; i < count; i++)
        {
            var f = Path.Combine(dir, $"img_{i}.arw");
            File.WriteAllText(f, "");
            files.Add(f);
        }
        return dir;
    }

    private static Mock<IImageService> CreateImageService()
    {
        var mock = new Mock<IImageService>();
        mock.Setup(x => x.ConvertToJxlAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Action<double>>(),
                It.IsAny<int>(), It.IsAny<OutputFormat>(), It.IsAny<CancellationToken>(),
                It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .Returns(Task.CompletedTask);
        return mock;
    }

    private static async Task WaitUntilAsync(Func<bool> condition, string failureMessage)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail(failureMessage);
            }
            await Task.Delay(25);
        }
    }

    [AvaloniaFact]
    public async Task ConvertSelectedAsync_AllSucceed_ShowsPlainCompletionMessage()
    {
        var dir = CreateTempDir(out var files, 2);
        try
        {
            var vm = GUITestHelpers.CreateViewModel(imageService: CreateImageService());
            vm.UseSubfolder = false;
            vm.AddFilesAsync(files).Wait();
            foreach (var item in vm.Images) item.IsSelected = true;

            vm.ConvertSelectedCommand.Execute(null);
            await WaitUntilAsync(() => !vm.IsConverting, "conversion did not finish");

            Assert.Equal(AppStrings.ConversionComplete, vm.StatusMessage);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [AvaloniaFact]
    public async Task ConvertSelectedAsync_WithConflicts_ShowsSkippedInSummary()
    {
        var dir = CreateTempDir(out var files, 1);
        try
        {
            var outputPath = Path.Combine(dir, "img_0.jxl");
            File.WriteAllText(outputPath, "existing");

            var vm = GUITestHelpers.CreateViewModel(imageService: CreateImageService());
            vm.UseSubfolder = false;
            vm.ConflictResolution = ConflictResolution.Skip;
            vm.AddFilesAsync(files).Wait();
            vm.Images[0].IsSelected = true;

            vm.ConvertSelectedCommand.Execute(null);
            await WaitUntilAsync(() => !vm.IsConverting, "conversion did not finish");

            Assert.Contains("0 converted", vm.StatusMessage);
            Assert.Contains("1 skipped", vm.StatusMessage);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [AvaloniaFact]
    public async Task ConvertSelectedAsync_WithFailure_ShowsFailedInSummary()
    {
        var dir = CreateTempDir(out var files, 1);
        try
        {
            var imageService = CreateImageService();
            imageService.Setup(x => x.ConvertToJxlAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Action<double>>(),
                    It.IsAny<int>(), It.IsAny<OutputFormat>(), It.IsAny<CancellationToken>(),
                    It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<int?>()))
                .ThrowsAsync(new IOException("simulated failure"));

            var vm = GUITestHelpers.CreateViewModel(imageService: imageService);
            vm.UseSubfolder = false;
            vm.AddFilesAsync(files).Wait();
            vm.Images[0].IsSelected = true;

            vm.ConvertSelectedCommand.Execute(null);
            await WaitUntilAsync(() => !vm.IsConverting, "conversion did not finish");

            Assert.Contains("1 failed", vm.StatusMessage);
            Assert.Equal(ImageStatus.Failed, vm.Images[0].Status);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
