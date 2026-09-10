using System.Collections.Concurrent;
using Avalonia.Headless.XUnit;
using Moq;
using RAWtoJXL.Avalonia;
using RAWtoJXL.Avalonia.ViewModels;
using RAWtoJXL.Core.Interfaces;
using RAWtoJXL.Core.Services;

namespace RAWtoJXL.Tests.GUITests;

[Trait("category", "gui")]
public sealed class MainViewModelBatchConversionTests
{
    [AvaloniaFact]
    public async Task ConvertSelectedAsync_UsesConfiguredParallelFiles()
    {
        var directory = CreateFiles(3, out var files);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var active = 0;
        var peak = 0;
        var imageService = CreateImageService(async (_, output, _, _, _, cancellationToken, _, _, _) =>
        {
            var current = Interlocked.Increment(ref active);
            UpdateMaximum(ref peak, current);
            if (current >= 2)
            {
                entered.TrySetResult();
            }

            try
            {
                await release.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
                File.WriteAllText(output, "converted");
            }
            finally
            {
                Interlocked.Decrement(ref active);
            }
        });

        try
        {
            var vm = GUITestHelpers.CreateViewModel(imageService: imageService);
            vm.UseSubfolder = false;
            vm.BatchJobs = 2;
            await vm.AddFilesAsync(files);
            foreach (var item in vm.Images)
            {
                item.IsSelected = true;
            }

            vm.ConvertSelectedCommand.Execute(null);
            try
            {
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.Equal(2, peak);
                Assert.Equal(2, Volatile.Read(ref active));
            }
            finally
            {
                release.TrySetResult();
            }

            await WaitUntilAsync(() => !vm.IsConverting, "conversion did not finish");
            Assert.All(vm.Images, item => Assert.Equal(ImageStatus.Converted, item.Status));
            Assert.Equal(AppStrings.ConversionComplete, vm.StatusMessage);
        }
        finally
        {
            release.TrySetResult();
            Directory.Delete(directory, recursive: true);
        }
    }

    [AvaloniaFact]
    public async Task ConvertSelectedAsync_SnapshotsQualityFormatAndThreadSettings()
    {
        var directory = CreateFiles(2, out var files);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var captured = new ConcurrentBag<(int Quality, OutputFormat Format, int? Effort, int? Threads)>();
        var imageService = CreateImageService(async (_, output, _, quality, format, _, _, effort, threads) =>
        {
            captured.Add((quality, format, effort, threads));
            if (captured.Count >= 2)
            {
                entered.TrySetResult();
            }

            await release.Task.WaitAsync(TimeSpan.FromSeconds(5));
            File.WriteAllText(output, "converted");
        });

        try
        {
            var vm = GUITestHelpers.CreateViewModel(imageService: imageService);
            vm.UseSubfolder = false;
            vm.BatchJobs = 2;
            vm.QualityPreset = 87;
            vm.OutputFormat = OutputFormat.Jxl;
            vm.CjxlEffort = 7;
            vm.CjxlThreads = -1;
            await vm.AddFilesAsync(files);
            vm.Images[0].QualityOverride = 41;
            foreach (var item in vm.Images)
            {
                item.IsSelected = true;
            }

            vm.ConvertSelectedCommand.Execute(null);
            try
            {
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

                // These edits happen after requests have been built. They must
                // affect a later batch, not the current one.
                vm.QualityPreset = 12;
                vm.OutputFormat = OutputFormat.Avif;
                vm.CjxlEffort = 1;
                vm.CjxlThreads = 1;
                vm.BatchJobs = 1;
            }
            finally
            {
                release.TrySetResult();
            }

            await WaitUntilAsync(() => !vm.IsConverting, "conversion did not finish");
            Assert.Equal(2, captured.Count);
            Assert.Contains(captured, value => value.Quality == 41);
            Assert.Contains(captured, value => value.Quality == 87);
            Assert.All(captured, value =>
            {
                Assert.Equal(OutputFormat.Jxl, value.Format);
                Assert.Equal(7, value.Effort);
                Assert.Equal(BatchParallelismPolicy.ResolveEncoderThreads(-1, 2), value.Threads);
            });
        }
        finally
        {
            release.TrySetResult();
            Directory.Delete(directory, recursive: true);
        }
    }

    private static Mock<IImageService> CreateImageService(
        Func<string, string, Action<double>, int, OutputFormat, CancellationToken, bool, int?, int?, Task> conversion)
    {
        var imageService = new Mock<IImageService>();
        imageService.Setup(service => service.ConvertToJxlAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Action<double>>(),
                It.IsAny<int>(),
                It.IsAny<OutputFormat>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()))
            .Returns(conversion);
        return imageService;
    }

    private static string CreateFiles(int count, out List<string> files)
    {
        var directory = Path.Combine(Path.GetTempPath(), "RAWtoJXL_GUI_Batch_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        files = new List<string>(count);
        for (var index = 0; index < count; index++)
        {
            var path = Path.Combine(directory, $"{index}.arw");
            File.WriteAllText(path, "input");
            files.Add(path);
        }

        return directory;
    }

    private static async Task WaitUntilAsync(Func<bool> condition, string message)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline)
            {
                Assert.Fail(message);
            }

            await Task.Delay(25);
        }
    }

    private static void UpdateMaximum(ref int location, int value)
    {
        while (true)
        {
            var current = Volatile.Read(ref location);
            if (value <= current || Interlocked.CompareExchange(ref location, value, current) == current)
            {
                return;
            }
        }
    }
}
