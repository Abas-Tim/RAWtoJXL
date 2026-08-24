using System.IO;
using Avalonia.Headless.XUnit;
using ImageMagick;
using Moq;
using RAWtoJXL.Avalonia.Services;
using RAWtoJXL.Avalonia.ViewModels;
using RAWtoJXL.Core.Interfaces;
using RAWtoJXL.Core.Models;

namespace RAWtoJXL.Tests.GUITests;

[Trait("category", "gui")]
public class CompareFormatUniquenessTests
{
    private static Mock<IDispatcherService> CreateDispatcher()
    {
        var mock = new Mock<IDispatcherService>();
        mock.Setup(x => x.InvokeAsync(It.IsAny<Action>()))
            .Returns<Action>(a => { a(); return Task.CompletedTask; });
        return mock;
    }

    private static string CreateTinyPng(string dir, string name = "display.png")
    {
        var path = Path.Combine(dir, name);
        using var image = new MagickImage(MagickColors.Red, 64, 48);
        image.Write(path);
        return path;
    }

    private static Mock<ICompareConversionService> CreateServiceMock(string dir, Action<OutputFormat?>? onDisplay = null, Action<OutputFormat?>? onTarget = null)
    {
        var png = CreateTinyPng(dir);
        var mock = new Mock<ICompareConversionService>();
        mock.Setup(s => s.EnsureDisplayPngsAsync(It.IsAny<string>(), It.IsAny<OutputFormat?>(), It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<CancellationToken>(), It.IsAny<int?>()))
            .Callback<string, OutputFormat?, int, int?, CancellationToken, int?>((_, f, _, _, _, _) => onDisplay?.Invoke(f))
            .ReturnsAsync(new CompareDisplayPngs(png, png, 64, 48));
        mock.Setup(s => s.EnsureTargetFileAsync(It.IsAny<string>(), It.IsAny<OutputFormat>(), It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<CancellationToken>(), It.IsAny<int?>()))
            .Callback<string, OutputFormat, int, int?, CancellationToken, int?>((_, f, _, _, _, _) => onTarget?.Invoke(f))
            .ReturnsAsync(png);
        return mock;
    }

    private static CompareViewModel CreateViewModel(Mock<ICompareConversionService> service, string filePath)
    {
        return new CompareViewModel(filePath, 90, service.Object, CreateDispatcher().Object);
    }

    [AvaloniaFact]
    public async Task InitializeAsync_RawInput_DefaultsToJxlAndAvif_WithUniqueOptions()
    {
        var dir = Path.Combine(Path.GetTempPath(), "RAWtoJXL_CmpFmt_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var service = CreateServiceMock(dir);
            var vm = CreateViewModel(service, Path.Combine(dir, "photo.dng"));

            await vm.InitializeAsync();

            Assert.Null(vm.LeftPane.Format);
            Assert.True(vm.LeftPane.IsOriginal);
            Assert.Equal(OutputFormat.Jxl, vm.MiddlePane.Format);
            Assert.Equal(OutputFormat.Avif, vm.RightPane.Format);
            Assert.DoesNotContain(OutputFormat.Avif, vm.MiddlePane.AvailableFormats);
            Assert.DoesNotContain(OutputFormat.Jxl, vm.RightPane.AvailableFormats);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [AvaloniaFact]
    public async Task InitializeAsync_JxlInput_ExcludesJxlFromBothDropdowns()
    {
        var dir = Path.Combine(Path.GetTempPath(), "RAWtoJXL_CmpFmt_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var service = CreateServiceMock(dir);
            var vm = CreateViewModel(service, Path.Combine(dir, "photo.jxl"));

            await vm.InitializeAsync();

            Assert.Equal(OutputFormat.Jxl, vm.OriginalFormat);
            Assert.Equal(OutputFormat.Avif, vm.MiddlePane.Format);
            Assert.Equal(OutputFormat.Jpeg, vm.RightPane.Format);
            Assert.DoesNotContain(OutputFormat.Jxl, vm.MiddlePane.AvailableFormats);
            Assert.DoesNotContain(OutputFormat.Jxl, vm.RightPane.AvailableFormats);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [AvaloniaFact]
    public async Task FormatChange_CollisionWithOtherPane_ResolvesAndReruns()
    {
        var dir = Path.Combine(Path.GetTempPath(), "RAWtoJXL_CmpFmt_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var displayCalls = new List<OutputFormat?>();
            var service = CreateServiceMock(dir, onDisplay: displayCalls.Add);
            var vm = CreateViewModel(service, Path.Combine(dir, "photo.dng"));
            await vm.InitializeAsync();
            int callsBefore = displayCalls.Count(f => f != null);

            vm.MiddlePane.Format = OutputFormat.Avif;
            await WaitUntil(() => displayCalls.Count(f => f != null) >= callsBefore + 2);

            Assert.Equal(OutputFormat.Avif, vm.MiddlePane.Format);
            Assert.NotEqual(OutputFormat.Avif, vm.RightPane.Format);
            Assert.Equal(OutputFormat.Jxl, vm.RightPane.Format);
            Assert.DoesNotContain(OutputFormat.Avif, vm.RightPane.AvailableFormats);
            Assert.DoesNotContain(OutputFormat.Jxl, vm.MiddlePane.AvailableFormats);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [AvaloniaFact]
    public async Task QualityChange_ReconvertsBothConvertedPanes_NotOriginal()
    {
        var dir = Path.Combine(Path.GetTempPath(), "RAWtoJXL_CmpFmt_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var displayCalls = new List<OutputFormat?>();
            var service = CreateServiceMock(dir, onDisplay: displayCalls.Add);
            var vm = CreateViewModel(service, Path.Combine(dir, "photo.dng"));
            await vm.InitializeAsync();
            int originalCalls = displayCalls.Count(f => f == null);
            int convertedCalls = displayCalls.Count(f => f != null);

            vm.Quality = 55;
            vm.TriggerReconvertTick();
            await WaitUntil(() => displayCalls.Count(f => f != null) >= convertedCalls + 2);

            Assert.Equal(originalCalls, displayCalls.Count(f => f == null));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [AvaloniaFact]
    public async Task EffortChange_ReconvertsOnlyJxlPane()
    {
        var dir = Path.Combine(Path.GetTempPath(), "RAWtoJXL_CmpFmt_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var displayCalls = new List<OutputFormat?>();
            var service = CreateServiceMock(dir, onDisplay: displayCalls.Add);
            var vm = CreateViewModel(service, Path.Combine(dir, "photo.dng"));
            await vm.InitializeAsync();
            int jxlCalls = displayCalls.Count(f => f == OutputFormat.Jxl);
            int avifCalls = displayCalls.Count(f => f == OutputFormat.Avif);

            vm.JxlEffort = 7;
            vm.TriggerReconvertTick();
            await WaitUntil(() => displayCalls.Count(f => f == OutputFormat.Jxl) >= jxlCalls + 1);
            await Task.Delay(50);

            Assert.Equal(avifCalls, displayCalls.Count(f => f == OutputFormat.Avif));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [AvaloniaFact]
    public async Task InitializeAsync_ReportsSizesOnPanes()
    {
        var dir = Path.Combine(Path.GetTempPath(), "RAWtoJXL_CmpFmt_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var service = CreateServiceMock(dir);
            var vm = CreateViewModel(service, Path.Combine(dir, "photo.dng"));

            await vm.InitializeAsync();

            Assert.False(string.IsNullOrEmpty(vm.MiddlePane.FileSizeText));
            Assert.False(string.IsNullOrEmpty(vm.RightPane.FileSizeText));
            Assert.Equal("Ready: 3 of 3", vm.StatusMessage);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    private static async Task WaitUntil(Func<bool> condition, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("Condition not met within timeout.");
            }
            await Task.Delay(25);
        }
    }
}
