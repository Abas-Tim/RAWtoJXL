using Avalonia.Headless.XUnit;
using System.Collections.ObjectModel;
using RAWtoJXL.Avalonia.ViewModels;
using RAWtoJXL.Core.Interfaces;

namespace RAWtoJXL.Tests.GUITests;

[Trait("category", "gui")]
public sealed class MainViewModelImageFilterTests
{
    [AvaloniaFact]
    public void FailedFilter_ProjectsOnlyFailuresAndPreservesSourceOrder()
    {
        var converted = CreateItem("converted.arw", ImageStatus.Converted);
        var firstFailed = CreateItem("first-failed.arw", ImageStatus.Failed);
        var skipped = CreateItem("skipped.arw", ImageStatus.Skipped);
        var secondFailed = CreateItem("second-failed.arw", ImageStatus.Failed);
        var ready = CreateItem("ready.arw", ImageStatus.Ready);
        var vm = GUITestHelpers.CreateViewModel();

        foreach (var item in new[] { converted, firstFailed, skipped, secondFailed, ready })
        {
            vm.Images.Add(item);
        }

        Assert.Equal(5, vm.LoadedImageCount);
        Assert.Equal(2, vm.FailedImageCount);
        Assert.Equal(5, vm.VisibleImageCount);

        vm.ShowFailedOnlyFilterCommand.Execute(null);

        Assert.True(vm.ShowFailedOnly);
        Assert.Equal(2, vm.VisibleImageCount);
        Assert.Equal(2, vm.FailedImageCount);
        Assert.Same(firstFailed, vm.VisibleImages[0]);
        Assert.Same(secondFailed, vm.VisibleImages[1]);
        Assert.Contains("Showing 2 failed of 5 images", vm.FilterSummaryText);

        vm.ShowAllImagesFilterCommand.Execute(null);

        Assert.False(vm.ShowFailedOnly);
        Assert.Equal(5, vm.VisibleImageCount);
        Assert.Same(skipped, vm.VisibleImages[2]);
    }

    [AvaloniaFact]
    public void FailedFilter_UpdatesWhenStatusChangesAndExcludesThumbnailErrors()
    {
        var ready = CreateItem("ready.arw", ImageStatus.Ready);
        ready.ErrorMessage = "Thumbnail failed: test decoder error";
        var failed = CreateItem("failed.arw", ImageStatus.Failed);
        var vm = GUITestHelpers.CreateViewModel();
        vm.Images.Add(ready);
        vm.Images.Add(failed);
        vm.ShowFailedOnlyFilterCommand.Execute(null);

        Assert.Single(vm.VisibleImages);
        Assert.Same(failed, vm.VisibleImages[0]);

        ready.Status = ImageStatus.Failed;
        Assert.Equal(2, vm.VisibleImageCount);
        Assert.Equal(2, vm.FailedImageCount);

        failed.Status = ImageStatus.Converted;
        Assert.Single(vm.VisibleImages);
        Assert.Same(ready, vm.VisibleImages[0]);
        Assert.Equal("Failed only (1)", vm.FailedFilterText);
    }

    [AvaloniaFact]
    public void FailedFilter_UsesOnlyVisibleSelectionForActionsAndSelectAll()
    {
        var hiddenSuccess = CreateItem("success.arw", ImageStatus.Converted);
        var failure = CreateItem("failure.arw", ImageStatus.Failed);
        var secondFailure = CreateItem("second-failure.arw", ImageStatus.Failed);
        var vm = GUITestHelpers.CreateViewModel();
        vm.Images.Add(hiddenSuccess);
        vm.Images.Add(failure);
        vm.Images.Add(secondFailure);

        hiddenSuccess.IsSelected = true;
        failure.IsSelected = true;
        vm.ShowFailedOnlyFilterCommand.Execute(null);

        Assert.True(vm.IsAnySelected);
        Assert.True(vm.IsExactlyOneSelected);
        Assert.True(vm.CompareSelectedCommand.CanExecute(null));
        Assert.True(vm.ConvertSelectedCommand.CanExecute(null),
            "The visible failure remains retryable even when a converted item is selected but hidden.");

        vm.SelectAllCommand.Execute(null);
        Assert.True(failure.IsSelected);
        Assert.True(secondFailure.IsSelected);
        Assert.True(hiddenSuccess.IsSelected);

        vm.SelectAllCommand.Execute(null);
        Assert.False(failure.IsSelected);
        Assert.False(secondFailure.IsSelected);
        Assert.True(hiddenSuccess.IsSelected);
        Assert.False(vm.IsAnySelected);
    }

    [AvaloniaFact]
    public void FailedFilter_RemoveSelectedRemovesOnlyVisibleFailures()
    {
        var hidden = CreateItem("hidden.arw", ImageStatus.Converted);
        var failure = CreateItem("failure.arw", ImageStatus.Failed);
        var vm = GUITestHelpers.CreateViewModel();
        vm.Images.Add(hidden);
        vm.Images.Add(failure);
        hidden.IsSelected = true;
        failure.IsSelected = true;

        vm.ShowFailedOnlyFilterCommand.Execute(null);
        vm.RemoveSelectedCommand.Execute(null);

        Assert.Single(vm.Images);
        Assert.Same(hidden, vm.Images[0]);
        Assert.True(hidden.IsSelected, "Hidden selection should be preserved when removing visible failures.");
        Assert.Empty(vm.VisibleImages);
        Assert.True(vm.IsFilterEmpty);
    }

    [AvaloniaFact]
    public void FailedFilter_DetachesReplacedCollectionItems()
    {
        var oldFailure = CreateItem("old-failure.arw", ImageStatus.Failed);
        var newReady = CreateItem("new-ready.arw", ImageStatus.Ready);
        var vm = GUITestHelpers.CreateViewModel();
        vm.Images.Add(oldFailure);
        vm.ShowFailedOnlyFilterCommand.Execute(null);

        vm.Images = new ObservableCollection<ImageItemViewModel> { newReady };

        Assert.Empty(vm.VisibleImages);
        oldFailure.Status = ImageStatus.Failed;
        Assert.Empty(vm.VisibleImages);

        newReady.Status = ImageStatus.Failed;
        Assert.Single(vm.VisibleImages);
        Assert.Same(newReady, vm.VisibleImages[0]);
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
