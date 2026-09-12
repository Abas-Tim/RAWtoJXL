using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using RAWtoJXL.Avalonia.ViewModels;
using RAWtoJXL.Core.Interfaces;

namespace RAWtoJXL.Tests.GUITests;

[Trait("category", "gui")]
public sealed class MainWindowImageFilterTests
{
    [AvaloniaFact]
    public void MainWindow_FailedFilterMenuItem_UpdatesGalleryProjection()
    {
        var converted = CreateItem("converted.arw", ImageStatus.Converted);
        var failed = CreateItem("failed.arw", ImageStatus.Failed);
        var vm = GUITestHelpers.CreateViewModel();
        vm.Images.Add(converted);
        vm.Images.Add(failed);

        var window = GUITestHelpers.CreateWindow(vm);
        var repeater = window.FindControl<ItemsRepeater>("ImagesRepeater");
        var failedMenuItem = window.FindControl<MenuItem>("FailedOnlyFilterMenuItem");
        Assert.NotNull(repeater);
        Assert.NotNull(failedMenuItem);
        Assert.Same(vm.VisibleImages, repeater!.ItemsSource);
        Assert.Equal(2, vm.VisibleImageCount);
        Assert.Equal(MenuItemToggleType.CheckBox, failedMenuItem!.ToggleType);
        Assert.False(failedMenuItem.IsChecked);

        vm.ShowFailedOnlyFilterCommand.Execute(null);
        window.UpdateLayout();

        Assert.True(vm.ShowFailedOnly);
        Assert.Equal("Failed only (1)", failedMenuItem.Header?.ToString());
        Assert.Equal(1, vm.VisibleImageCount);
        Assert.Same(failed, repeater.TryGetElement(0)!.DataContext);
        Assert.True(failedMenuItem.IsChecked);

        failedMenuItem.IsChecked = false;
        window.UpdateLayout();

        Assert.False(vm.ShowFailedOnly);
        Assert.Equal(2, vm.VisibleImageCount);
        Assert.False(failedMenuItem.IsChecked);

        Assert.Same(converted, repeater.TryGetElement(0)!.DataContext);
    }

    [AvaloniaFact]
    public void MainWindow_FailedFilter_ShowsEmptyStateWhenThereAreNoFailures()
    {
        var vm = GUITestHelpers.CreateViewModel();
        vm.Images.Add(CreateItem("ready.arw", ImageStatus.Ready));
        var window = GUITestHelpers.CreateWindow(vm);

        vm.ShowFailedOnlyFilterCommand.Execute(null);
        window.UpdateLayout();

        var emptyPanel = window.FindControl<StackPanel>("EmptyFilterPanel");
        var summary = window.FindControl<TextBlock>("FilterSummaryText");
        var failedMenuItem = window.FindControl<MenuItem>("FailedOnlyFilterMenuItem");
        Assert.NotNull(emptyPanel);
        Assert.True(emptyPanel!.IsVisible);
        Assert.NotNull(summary);
        Assert.Contains("Showing 0 failed of 1 images", summary!.Text);
        Assert.Equal("Failed only (0)", failedMenuItem!.Header?.ToString());
        Assert.True(failedMenuItem.IsChecked);
    }

    private static ImageItemViewModel CreateItem(string fileName, ImageStatus status)
    {
        return new ImageItemViewModel
        {
            FilePath = Path.Combine(Path.GetTempPath(), fileName),
            FileName = fileName,
            Status = status
        };
    }
}
