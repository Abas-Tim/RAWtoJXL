using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using ARWtoJXL.Avalonia;
using ARWtoJXL.Avalonia.ViewModels;

namespace ARWtoJXL.Tests.GUITests;

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
        var expectedButtons = new[] { "Convert", "Cancel", "Open Output Folder", "Settings" };
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
     public void MainWindow_HasGalleryItemsControl()
     {
         var window = GUITestHelpers.CreateWindow();
         var itemsControl = window.FindControl<ItemsControl>("ImagesListBox");
         Assert.NotNull(itemsControl);
     }

    [AvaloniaFact]
    public void MainWindow_HasProgressBar()
    {
        var window = GUITestHelpers.CreateWindow();
        var count = GUITestHelpers.GetAllControls<ProgressBar>(window).Count();
        Assert.True(count >= 1, "Expected at least one ProgressBar in the status bar");
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
}
