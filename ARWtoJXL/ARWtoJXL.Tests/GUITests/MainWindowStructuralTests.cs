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
        var expectedButtons = new[] { "Open File", "Select All", "Convert", "Remove", "Cancel", "Open Output Folder", "Settings", "Load All", "Clear" };
        foreach (var expected in expectedButtons)
        {
            Assert.Contains(expected, buttonContents, StringComparer.OrdinalIgnoreCase);
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
    public void MainWindow_HasGalleryListBox()
    {
        var window = GUITestHelpers.CreateWindow();
        var listBox = window.FindControl<ListBox>("ImagesListBox");
        Assert.NotNull(listBox);
        Assert.Equal(SelectionMode.Multiple, listBox!.SelectionMode);
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
    public void MainWindow_HasRecentFilesSection()
    {
        var window = GUITestHelpers.CreateWindow();
        var texts = GUITestHelpers.GetAllControls<TextBlock>(window).Select(t => t.Text).Where(t => t != null).Cast<string>().ToList();
        Assert.Contains(texts, t => t.Contains("Recent", StringComparison.OrdinalIgnoreCase));
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
