using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using RAWtoJXL.Avalonia;
using RAWtoJXL.Avalonia.ViewModels;

namespace RAWtoJXL.Tests.GUITests;

[Trait("category", "gui")]
public class MainWindowStructuralTests
{
    [AvaloniaFact]
    public void MainWindow_Opens_And_HasExpectedTitle()
    {
        var window = GUITestHelpers.CreateWindow();
        Assert.Equal("ARW to JXL Converter", window.Title);
    }

    [AvaloniaFact]
    public void MainWindow_HasToolbarButtons()
    {
        var window = GUITestHelpers.CreateWindow();
        var buttonContents = GUITestHelpers.GetButtonContents(window);
        var expectedButtons = new[] { "Convert", "Cancel", "Settings" };
        foreach (var expected in expectedButtons)
        {
            Assert.Contains(expected, buttonContents, StringComparer.OrdinalIgnoreCase);
        }
    }

    [AvaloniaFact]
    public void MainWindow_HasFileMenuItems()
    {
        var window = GUITestHelpers.CreateWindow();
        var menuHeaders = GUITestHelpers.GetMenuItemHeaders(window)?.Select(h => h?.Replace("_", "")).Where(h => h != null && !h.Contains("Controls")).Cast<string>().ToList() ?? new();
        var expectedMenuItems = new[] { "File", "Open File", "Open Folder", "Load All", "Clear Recent", "List", "Remove" };
        foreach (var expected in expectedMenuItems)
        {
            Assert.Contains(expected, menuHeaders, StringComparer.OrdinalIgnoreCase);
        }
    }

    [AvaloniaFact]
    public void MainWindow_CancelButton_Hidden_WhenNotConverting()
    {
        var window = GUITestHelpers.CreateWindow();
        var cancelButton = GUITestHelpers.GetAllControls<Button>(window)
            .FirstOrDefault(b => b.Content?.ToString() == "Cancel" && b.Classes.Contains("danger"));
        Assert.NotNull(cancelButton);
        Assert.False(cancelButton!.IsVisible);
    }

    [AvaloniaFact]
    public void MainWindow_HasGalleryRepeater()
    {
        var window = GUITestHelpers.CreateWindow();
        var repeater = window.FindControl<ItemsRepeater>("ImagesRepeater");
        Assert.NotNull(repeater);
        Assert.IsType<UniformGridLayout>(repeater!.Layout);
    }

    [AvaloniaFact]
    public void MainWindow_GalleryRepeater_HasItemsSourceBinding()
    {
        var vm = GUITestHelpers.CreateViewModel();
        vm.Images.Add(new ImageItemViewModel { FilePath = @"C:\test\img1.arw", FileName = "img1.arw", Status = Core.Interfaces.ImageStatus.Ready });

        var window = GUITestHelpers.CreateWindow(vm);
        window.UpdateLayout();

        var repeater = window.FindControl<ItemsRepeater>("ImagesRepeater");
        Assert.NotNull(repeater);
        Assert.NotNull(repeater!.ItemsSource);

        var firstElement = repeater.TryGetElement(0);
        Assert.NotNull(firstElement);
    }

    [AvaloniaFact]
    public void MainWindow_HasProgressBar()
    {
        var window = GUITestHelpers.CreateWindow();
        var count = GUITestHelpers.GetAllControls<ProgressBar>(window).Count();
        Assert.True(count >= 1, "Expected at least one ProgressBar in the status bar");
    }

    [AvaloniaFact]
    public void MainWindow_ProgressBar_BoundToViewModel()
    {
        var vm = GUITestHelpers.CreateViewModel();
        vm.TotalCount = 10;
        vm.CompletedCount = 5;

        var window = GUITestHelpers.CreateWindow(vm);

        var progressBars = GUITestHelpers.GetAllControls<ProgressBar>(window).ToList();
        var progressBar = progressBars.FirstOrDefault(p => p.Minimum == 0 && p.Height < 10);
        Assert.NotNull(progressBar);

        Assert.Equal(10, progressBar!.Maximum);
        Assert.Equal(5, progressBar.Value);
    }

    [AvaloniaFact]
    public void MainWindow_HasStatusBarText()
    {
        var window = GUITestHelpers.CreateWindow();
        var texts = GUITestHelpers.GetAllControls<TextBlock>(window).Select(t => t.Text).ToList();
        Assert.Contains("Ready", texts);
    }

    [AvaloniaFact]
    public void MainWindow_HasRecentFilesInMenu()
    {
        var window = GUITestHelpers.CreateWindow();
        var menuHeaders = GUITestHelpers.GetMenuItemHeaders(window)?.Select(h => h?.Replace("_", "")).Where(h => h != null && !h.Contains("Controls")).Cast<string>().ToList() ?? new();
        Assert.Contains("Load All", menuHeaders, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Clear Recent", menuHeaders, StringComparer.OrdinalIgnoreCase);
    }

    [AvaloniaFact]
    public void MainWindow_HasDragDropEnabled()
    {
        var window = GUITestHelpers.CreateWindow();
        var rootGrid = window.Content as Grid;
        Assert.NotNull(rootGrid);
        Assert.True(DragDrop.GetAllowDrop(rootGrid!), "Main Grid should allow drop");
    }

    [AvaloniaFact]
    public void MainWindow_DataContext_IsMainViewModel()
    {
        var window = GUITestHelpers.CreateWindow();
        Assert.IsType<MainViewModel>(window.DataContext);
    }

    [AvaloniaFact]
    public void MainWindow_MinSize_IsSet()
    {
        var window = GUITestHelpers.CreateWindow();
        Assert.Equal(800, window.MinWidth);
        Assert.Equal(600, window.MinHeight);
    }

    [AvaloniaFact]
    public void MainWindow_InitialLayout_DoesNotThrow()
    {
        var window = GUITestHelpers.CreateWindow();
        window.UpdateLayout();
        Assert.True(true, "Window should layout without exceptions");
    }

    [AvaloniaFact]
    public void MainWindow_ConvertButton_HasAccentClass()
    {
        var window = GUITestHelpers.CreateWindow();
        var convertButton = GUITestHelpers.GetAllControls<Button>(window)
            .FirstOrDefault(b => b.Content?.ToString() == "Convert");
        Assert.NotNull(convertButton);
        Assert.Contains("accent", convertButton!.Classes);
    }

    [AvaloniaFact]
    public void MainWindow_SelectAllMenuHeader_CommandBound()
    {
        var vm = GUITestHelpers.CreateViewModel();
        var window = GUITestHelpers.CreateWindow(vm);

        var menuItems = GUITestHelpers.GetAllControls<MenuItem>(window);
        var selectAllItem = menuItems.FirstOrDefault(m => m.Command == vm.SelectAllCommand);
        Assert.NotNull(selectAllItem);
    }
}
