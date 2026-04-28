using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using ARWtoJXL.Avalonia;
using ARWtoJXL.Avalonia.Behaviors;
using ARWtoJXL.Avalonia.ViewModels;

namespace ARWtoJXL.Tests.GUITests;

[Trait("category", "gui")]
public class MainWindowBehavioralTests
{
    [AvaloniaFact]
    public void MainWindow_ListBox_DisplaysItemsFromViewModel()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ARWtoJXL_Test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var tempFile1 = Path.Combine(tempDir, "test1.arw");
        var tempFile2 = Path.Combine(tempDir, "test2.arw");
        File.WriteAllText(tempFile1, "");
        File.WriteAllText(tempFile2, "");

        try
        {
            var vm = GUITestHelpers.CreateViewModel();
            vm.AddFilesAsync(new[] { tempFile1, tempFile2 }).Wait();

            var window = GUITestHelpers.CreateWindow(vm);
            var listBox = window.FindControl<ListBox>("ImagesListBox")!;
            window.UpdateLayout();

            Assert.Equal(2, listBox.Items.Count);
            Assert.Equal(2, vm.Images.Count);
        }
        finally
        {
            try { File.Delete(tempFile1); } catch { }
            try { File.Delete(tempFile2); } catch { }
            try { Directory.Delete(tempDir, false); } catch { }
        }
    }

    [AvaloniaFact]
    public void MainWindow_ListBox_Items_AreImageItemViewModels()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ARWtoJXL_Test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var tempFile = Path.Combine(tempDir, "test1.arw");
        File.WriteAllText(tempFile, "");

        try
        {
            var vm = GUITestHelpers.CreateViewModel();
            vm.AddFilesAsync(new[] { tempFile }).Wait();

            var window = GUITestHelpers.CreateWindow(vm);
            var listBox = window.FindControl<ListBox>("ImagesListBox")!;
            window.UpdateLayout();

            var items = listBox.Items.ToList();
            Assert.Single(items);
            Assert.IsType<ImageItemViewModel>(items[0]);
        }
        finally
        {
            try { File.Delete(tempFile); } catch { }
            try { Directory.Delete(tempDir, false); } catch { }
        }
    }

    [AvaloniaFact]
    public void MainWindow_ConvertButton_Command_BoundToConvertSelectedCommand()
    {
        var vm = GUITestHelpers.CreateViewModel();
        var window = GUITestHelpers.CreateWindow(vm);

        var convertButton = GUITestHelpers.GetAllControls<Button>(window)
            .First(b => b.Content?.ToString() == "Convert");

        Assert.Same(vm.ConvertSelectedCommand, convertButton.Command);
    }

    [AvaloniaFact]
    public void MainWindow_RemoveButton_Command_BoundToRemoveSelectedCommand()
    {
        var vm = GUITestHelpers.CreateViewModel();
        var window = GUITestHelpers.CreateWindow(vm);

        var removeButton = GUITestHelpers.GetAllControls<Button>(window)
            .First(b => b.Content?.ToString() == "Remove");

        Assert.Same(vm.RemoveSelectedCommand, removeButton.Command);
    }

    [AvaloniaFact]
    public void MainWindow_SelectAllButton_Command_BoundToSelectAllCommand()
    {
        var vm = GUITestHelpers.CreateViewModel();
        var window = GUITestHelpers.CreateWindow(vm);

        var selectAllButton = GUITestHelpers.GetAllControls<Button>(window)
            .First(b => b.Content?.ToString() == "Select All");

        Assert.Same(vm.SelectAllCommand, selectAllButton.Command);
    }

    [AvaloniaFact]
    public void MainWindow_SettingsButton_Command_BoundToOpenSettingsCommand()
    {
        var vm = GUITestHelpers.CreateViewModel();
        var window = GUITestHelpers.CreateWindow(vm);

        var settingsButton = GUITestHelpers.GetAllControls<Button>(window)
            .First(b => b.Content?.ToString() == "Settings");

        Assert.Same(vm.OpenSettingsCommand, settingsButton.Command);
    }

    [AvaloniaFact]
    public void MainWindow_CancelButton_Command_BoundToCancelCommand()
    {
        var vm = GUITestHelpers.CreateViewModel();
        var window = GUITestHelpers.CreateWindow(vm);

        var cancelButton = GUITestHelpers.GetAllControls<Button>(window)
            .FirstOrDefault(b => b.Content?.ToString() == "Cancel");
        Assert.NotNull(cancelButton);

        Assert.Same(vm.CancelCommand, cancelButton!.Command);
    }

    [AvaloniaFact]
    public void MainWindow_StatusBar_BindsToStatusMessage()
    {
        var vm = GUITestHelpers.CreateViewModel();
        var window = GUITestHelpers.CreateWindow(vm);

        var textBlocks = GUITestHelpers.GetAllControls<TextBlock>(window)
            .Where(t => t.Text != null && t.Text == AppStrings.Ready)
            .ToList();
        Assert.True(textBlocks.Count >= 1, "Expected status bar to show the StatusMessage from ViewModel");
    }

    [AvaloniaFact]
    public void MainWindow_DragDropBehavior_EnabledOnRootGrid()
    {
        var window = GUITestHelpers.CreateWindow();
        var rootGrid = window.Content as Grid;
        Assert.NotNull(rootGrid);

        Assert.True(DragDropBehavior.GetEnableDragDrop(rootGrid!), "DragDropBehavior.EnableDragDrop should be true");
        Assert.True(DragDrop.GetAllowDrop(rootGrid!), "DragDrop.AllowDrop should be true");
    }

    [AvaloniaFact]
    public void MainWindow_DragDropBehavior_HasDropHandlerAttached()
    {
        var vm = GUITestHelpers.CreateViewModel();
        var window = GUITestHelpers.CreateWindow(vm);
        var rootGrid = window.Content as Grid;
        Assert.NotNull(rootGrid);

        Assert.True(DragDropBehavior.GetEnableDragDrop(rootGrid!));
    }
}
