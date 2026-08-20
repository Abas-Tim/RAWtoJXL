using System.IO;
using Avalonia.Headless.XUnit;
using Moq;
using RAWtoJXL.Avalonia;
using RAWtoJXL.Avalonia.Controls;
using RAWtoJXL.Avalonia.Services;
using RAWtoJXL.Avalonia.ViewModels;
using RAWtoJXL.Core.Interfaces;

namespace RAWtoJXL.Tests.GUITests;

[Trait("category", "gui")]
public class CompareWindowStructuralTests
{
    private static CompareViewModel CreateViewModel()
    {
        var mock = new Mock<ICompareConversionService>();
        var dispatcher = new Mock<IDispatcherService>();
        dispatcher.Setup(x => x.InvokeAsync(It.IsAny<Action>()))
            .Returns<Action>(a => { a(); return Task.CompletedTask; });
        return new CompareViewModel(
            Path.Combine(Path.GetTempPath(), "photo.dng"),
            90,
            mock.Object,
            dispatcher.Object);
    }

    [AvaloniaFact]
    public void CompareWindow_LoadsWithViewModelAndThreeViewers()
    {
        var vm = CreateViewModel();
        var window = new CompareWindow
        {
            DataContext = vm
        };

        window.Show();
        window.UpdateLayout();

        var viewers = GUITestHelpers.GetAllControls<ZoomPanImageViewer>(window).ToList();
        Assert.Equal(3, viewers.Count);
        Assert.InRange(ZoomPanImageViewer.FullResZoomThreshold, 0.5, 1.0);
        Assert.All(viewers, viewer => Assert.True(viewer.Bounds.Width > 0));
        Assert.InRange(Math.Abs(viewers[0].Bounds.Width - viewers[1].Bounds.Width), 0, 1);
        Assert.InRange(Math.Abs(viewers[1].Bounds.Width - viewers[2].Bounds.Width), 0, 1);

        window.Close();
        vm.Dispose();
    }
}
