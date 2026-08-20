using System.IO;
using Avalonia.Headless.XUnit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using RAWtoJXL.Avalonia;
using RAWtoJXL.Avalonia.Services;
using RAWtoJXL.Avalonia.ViewModels;
using RAWtoJXL.Core.Interfaces;

namespace RAWtoJXL.Tests.GUITests;

[Trait("category", "gui")]
public class MainViewModelCompareButtonTests
{
    [AvaloniaFact]
    public void CompareSelected_NoSelection_CannotExecute()
    {
        var vm = GUITestHelpers.CreateViewModel();
        GUITestHelpers.AddTestFiles(vm, 1);

        Assert.False(vm.CompareSelectedCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public void CompareSelected_ExactlyOneSelection_CanExecuteAndRaisesEventWithPath()
    {
        var vm = GUITestHelpers.CreateViewModel();
        GUITestHelpers.AddTestFiles(vm, 2);

        string? raisedPath = null;
        vm.RequestOpenCompare += p => raisedPath = p;

        vm.Images[0].IsSelected = true;

        Assert.True(vm.IsExactlyOneSelected);
        Assert.True(vm.CompareSelectedCommand.CanExecute(null));

        vm.CompareSelectedCommand.Execute(null);

        Assert.NotNull(raisedPath);
        Assert.Equal(vm.Images[0].FilePath, raisedPath);
    }

    [AvaloniaFact]
    public void CompareSelected_TwoSelections_CannotExecute()
    {
        var vm = GUITestHelpers.CreateViewModel();
        GUITestHelpers.AddTestFiles(vm, 2);

        vm.Images[0].IsSelected = true;
        vm.Images[1].IsSelected = true;

        Assert.False(vm.IsExactlyOneSelected);
        Assert.False(vm.CompareSelectedCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public void CompareSelected_DeselectToZero_CannotExecute()
    {
        var vm = GUITestHelpers.CreateViewModel();
        GUITestHelpers.AddTestFiles(vm, 1);

        vm.Images[0].IsSelected = true;
        Assert.True(vm.CompareSelectedCommand.CanExecute(null));

        vm.Images[0].IsSelected = false;

        Assert.False(vm.IsExactlyOneSelected);
        Assert.False(vm.CompareSelectedCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public void CompareSelected_WhileConverting_CannotExecute()
    {
        var vm = GUITestHelpers.CreateViewModel();
        GUITestHelpers.AddTestFiles(vm, 1);

        vm.Images[0].IsSelected = true;
        Assert.True(vm.CompareSelectedCommand.CanExecute(null));

        vm.IsConverting = true;
        Assert.False(vm.CompareSelectedCommand.CanExecute(null));

        vm.IsConverting = false;
        Assert.True(vm.CompareSelectedCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public void CompareButton_ExistsInMainWindow()
    {
        using var scope = new GUITestHelpers.SettingsScope();
        var window = GUITestHelpers.CreateWindow();

        var contents = GUITestHelpers.GetButtonContents(window);

        Assert.Contains("Compare", contents);
    }

    [AvaloniaFact]
    public void CompareSelected_WithServices_OpensCompareWindow()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new Mock<ICompareConversionService>().Object);
        var dispatcher = new Mock<IDispatcherService>();
        dispatcher.Setup(x => x.InvokeAsync(It.IsAny<Action>()))
            .Returns<Action>(a => { a(); return Task.CompletedTask; });
        services.AddSingleton(dispatcher.Object);

        var previous = App.Services;
        App.Services = services.BuildServiceProvider();
        try
        {
            var vm = GUITestHelpers.CreateViewModel();
            GUITestHelpers.AddTestFiles(vm, 1);
            var window = GUITestHelpers.CreateWindow(vm);
            vm.Images[0].IsSelected = true;

            vm.CompareSelectedCommand.Execute(null);

            Assert.NotNull(window.CompareToolWindow);
            Assert.True(window.CompareToolWindow!.IsVisible);

            window.CompareToolWindow.Close();
        }
        finally
        {
            App.Services = previous;
        }
    }
}
