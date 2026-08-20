using System.IO;
using Moq;
using RAWtoJXL.Avalonia.Controls;
using RAWtoJXL.Avalonia.Services;
using RAWtoJXL.Avalonia.ViewModels;
using RAWtoJXL.Core.Interfaces;

namespace RAWtoJXL.Tests.GUITests;

[Trait("category", "gui")]
public class CompareMirroringTests
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

    [Fact]
    public void MirroringEnabled_RequestsFitOnAllPanes()
    {
        var vm = CreateViewModel();
        var fitCounts = vm.Panes.ToDictionary(p => p, _ => 0);
        foreach (var pane in vm.Panes)
        {
            var captured = pane;
            captured.RequestFit += () => fitCounts[captured]++;
        }

        vm.IsMirroring = true;

        Assert.All(vm.Panes, p => Assert.Equal(1, fitCounts[p]));
    }

    [Fact]
    public void MirroringOn_ViewportChangeFansOutToOtherPanes()
    {
        var vm = CreateViewModel();
        vm.IsMirroring = true;

        var received = new Dictionary<ComparePaneViewModel, List<CompareViewport>>();
        foreach (var pane in vm.Panes)
        {
            received[pane] = new List<CompareViewport>();
            var captured = pane;
            captured.RequestSetViewport += vp => received[captured].Add(vp);
        }

        var vp = new CompareViewport(2.0, 0.3, 0.4);
        vm.OnPaneViewportChanged(vm.LeftPane, vp);

        Assert.Empty(received[vm.LeftPane]);
        Assert.Single(received[vm.MiddlePane]);
        Assert.Single(received[vm.RightPane]);
        Assert.True(received[vm.MiddlePane][0].Equals(vp));
        Assert.True(received[vm.RightPane][0].Equals(vp));
    }

    [Fact]
    public void MirroringOff_ViewportChangeDoesNotFanOut()
    {
        var vm = CreateViewModel();
        int events = 0;
        foreach (var pane in vm.Panes)
        {
            pane.RequestSetViewport += _ => events++;
        }

        vm.OnPaneViewportChanged(vm.LeftPane, new CompareViewport(2.0, 0.3, 0.4));

        Assert.Equal(0, events);
    }

    [Fact]
    public void MirroringReEnabled_RequestsFitAgainOnAllPanes()
    {
        var vm = CreateViewModel();
        var fitCounts = vm.Panes.ToDictionary(p => p, _ => 0);
        foreach (var pane in vm.Panes)
        {
            var captured = pane;
            captured.RequestFit += () => fitCounts[captured]++;
        }

        vm.IsMirroring = true;
        vm.IsMirroring = false;
        vm.IsMirroring = true;

        Assert.All(vm.Panes, p => Assert.Equal(2, fitCounts[p]));
    }
}
