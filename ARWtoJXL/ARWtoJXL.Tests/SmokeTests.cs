using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using ARWtoJXL.Avalonia;
using ARWtoJXL.Avalonia.Services;
using ARWtoJXL.Avalonia.ViewModels;
using ARWtoJXL.Core.Interfaces;
using Moq;

namespace ARWtoJXL.Tests;

[Trait("category", "smoke")]
public class SmokeTests
{
    private MainViewModel CreateViewModel(
        Mock<IImageService>? imageService = null,
        Mock<IDialogService>? dialogService = null,
        Mock<IDispatcherService>? dispatcherService = null,
        Mock<IFilePickerService>? filePickerService = null)
    {
        imageService ??= new Mock<IImageService>();
        dialogService ??= new Mock<IDialogService>();
       dispatcherService ??= new Mock<IDispatcherService>();
        dispatcherService.Setup(x => x.InvokeAsync(It.IsAny<Action>()))
                         .Returns<Action>(a => { a(); return Task.CompletedTask; });

        filePickerService ??= new Mock<IFilePickerService>();
        filePickerService.Setup(x => x.PickFilesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                         .ReturnsAsync(Array.Empty<string>());
        filePickerService.Setup(x => x.PickFolderAsync(It.IsAny<string>()))
                         .ReturnsAsync((string?)null);

        return new MainViewModel(
            imageService.Object,
            dialogService.Object,
            dispatcherService.Object,
            filePickerService.Object);
    }

    /* --- MainViewModel smoke tests (no UI needed) --- */

    [Fact]
    public void ViewModel_Commands_AreInitiallyDisabled()
    {
        var vm = CreateViewModel();
        Assert.False(vm.RemoveSelectedCommand.CanExecute(null));
        Assert.False(vm.ConvertSelectedCommand.CanExecute(null));
    }

    [Fact]
    public void AddFiles_AddsItemsToGallery()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ARWtoJXL_Test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var tempFile = Path.Combine(tempDir, "test1.arw");
        File.WriteAllText(tempFile, "");

        try
        {
            var vm = CreateViewModel();
            vm.AddFilesAsync(new[] { tempFile }).Wait();

            Assert.Equal(1, vm.Images.Count);
            Assert.Equal("test1.arw", vm.Images[0].FileName);
            Assert.Equal(ImageStatus.Ready, vm.Images[0].Status);
        }
        finally
        {
            File.Delete(tempFile);
            Directory.Delete(tempDir, false);
        }
    }

    [Fact]
    public void AddFiles_SkipsDuplicates()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ARWtoJXL_Test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var tempFile = Path.Combine(tempDir, "test1.arw");
        File.WriteAllText(tempFile, "");

        try
        {
            var vm = CreateViewModel();
            vm.AddFilesAsync(new[] { tempFile }).Wait();
            vm.AddFilesAsync(new[] { tempFile }).Wait();
            Assert.Equal(1, vm.Images.Count);
        }
        finally
        {
            File.Delete(tempFile);
            Directory.Delete(tempDir, false);
        }
    }

    [Fact]
    public void AddFiles_SkipsInvalidExtensions()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ARWtoJXL_Test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var tempFile = Path.Combine(tempDir, "test1.txt");
        File.WriteAllText(tempFile, "");

        try
        {
            var vm = CreateViewModel();
            vm.AddFilesAsync(new[] { tempFile }).Wait();
            Assert.Equal(0, vm.Images.Count);
        }
        finally
        {
            File.Delete(tempFile);
            Directory.Delete(tempDir, false);
        }
    }

    [Fact]
    public void SelectAll_SelectsAllItems()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ARWtoJXL_Test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var tempFile1 = Path.Combine(tempDir, "test1.arw");
        var tempFile2 = Path.Combine(tempDir, "test2.arw");
        File.WriteAllText(tempFile1, "");
        File.WriteAllText(tempFile2, "");

        try
        {
            var vm = CreateViewModel();
            vm.AddFilesAsync(new[] { tempFile1, tempFile2 }).Wait();
            Assert.False(vm.IsAnySelected);
            vm.SelectAllCommand.Execute(null);
            Assert.True(vm.IsAnySelected);
            Assert.True(vm.IsAllSelected);
            Assert.Equal(2, vm.Images.Count(i => i.IsSelected));
        }
        finally
        {
            try { File.Delete(tempFile1); } catch { }
            try { File.Delete(tempFile2); } catch { }
            try { Directory.Delete(tempDir, false); } catch { }
        }
    }

    [Fact]
    public void RemoveSelected_RemovesItems()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ARWtoJXL_Test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var tempFile1 = Path.Combine(tempDir, "test1.arw");
        var tempFile2 = Path.Combine(tempDir, "test2.arw");
        File.WriteAllText(tempFile1, "");
        File.WriteAllText(tempFile2, "");

        try
        {
            var vm = CreateViewModel();
            vm.AddFilesAsync(new[] { tempFile1, tempFile2 }).Wait();
            Assert.Equal(2, vm.Images.Count);
            vm.SelectAllCommand.Execute(null);
            vm.RemoveSelectedCommand.Execute(null);
            Assert.Equal(0, vm.Images.Count);
            Assert.False(vm.IsAnySelected);
            Assert.Contains("removed", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            try { File.Delete(tempFile1); } catch { }
            try { File.Delete(tempFile2); } catch { }
            try { Directory.Delete(tempDir, false); } catch { }
        }
    }

    [Fact]
    public void RemoveSelected_DoesNotCrashApp()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ARWtoJXL_Test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var tempFile1 = Path.Combine(tempDir, "test1.arw");
        var tempFile2 = Path.Combine(tempDir, "test2.arw");
        File.WriteAllText(tempFile1, "");
        File.WriteAllText(tempFile2, "");

        try
        {
            var vm = CreateViewModel();
            vm.AddFilesAsync(new[] { tempFile1, tempFile2 }).Wait();
            vm.SelectAllCommand.Execute(null);
            vm.RemoveSelectedCommand.Execute(null);
            Assert.Equal(0, vm.Images.Count);
        }
        finally
        {
            try { File.Delete(tempFile1); } catch { }
            try { File.Delete(tempFile2); } catch { }
            try { Directory.Delete(tempDir, false); } catch { }
        }
    }

    [Fact]
    public void ClearRecentFiles_ClearsList()
    {
        var vm = CreateViewModel();
        vm.ClearRecentFilesCommand.Execute(null);
        Assert.Equal(0, vm.RecentFiles.Count);
    }

    [Fact]
    public void ViewModel_RemoveCommand_Enabled_AfterSelection()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ARWtoJXL_Test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var tempFile = Path.Combine(tempDir, "test1.arw");
        File.WriteAllText(tempFile, "");

        try
        {
            var vm = CreateViewModel();
            vm.AddFilesAsync(new[] { tempFile }).Wait();
            vm.SelectAllCommand.Execute(null);
            Assert.True(vm.RemoveSelectedCommand.CanExecute(null));
        }
        finally
        {
            try { File.Delete(tempFile); } catch { }
            try { Directory.Delete(tempDir, false); } catch { }
        }
    }

    [Fact]
    public void ImageItem_EffectiveQuality_UsesOverride()
    {
        var item = new ImageItemViewModel
        {
            FilePath = "test.arw",
            FileName = "test.arw",
            Status = ImageStatus.Ready,
            QualityOverride = 75
        };
        Assert.Equal(75, item.EffectiveQuality(90));
    }

    [Fact]
    public void ImageItem_EffectiveQuality_FallsBackToGlobal()
    {
        var item = new ImageItemViewModel
        {
            FilePath = "test.arw",
            FileName = "test.arw",
            Status = ImageStatus.Ready
        };
        Assert.Equal(90, item.EffectiveQuality(90));
    }

    /* --- UI element tests (require Avalonia initialization) --- */

    [AvaloniaFact]
    public void MainWindow_Opens_And_HasExpectedTitle()
    {
        var window = CreateWindow();
        Assert.Equal("ARW to JXL Converter", window.Title);
    }

    [AvaloniaFact]
    public void MainWindow_HasToolbarButtons()
    {
        var window = CreateWindow();
        var buttonContents = GetButtonContents(window);
        var expectedButtons = new[] { "Open File", "Select All", "Convert", "Remove", "Cancel", "Open Output Folder", "Settings", "Load All", "Clear" };
        foreach (var expected in expectedButtons)
        {
            Assert.Contains(expected, buttonContents, StringComparer.OrdinalIgnoreCase);
        }
    }

    [AvaloniaFact]
    public void MainWindow_CancelButton_Hidden_WhenNotConverting()
    {
        var window = CreateWindow();
        var cancelButton = GetAllControls<Button>(window)
            .FirstOrDefault(b => b.Content?.ToString() == "Cancel");
        Assert.NotNull(cancelButton);
        Assert.False(cancelButton!.IsVisible);
    }

    [AvaloniaFact]
    public void MainWindow_HasGalleryListBox()
    {
        var window = CreateWindow();
        var listBox = window.FindControl<ListBox>("ImagesListBox");
        Assert.NotNull(listBox);
        Assert.Equal(SelectionMode.Multiple, listBox!.SelectionMode);
    }

    [AvaloniaFact]
    public void MainWindow_HasProgressBar()
    {
        var window = CreateWindow();
        var count = GetAllControls<ProgressBar>(window).Count();
        Assert.True(count >= 1, "Expected at least one ProgressBar in the status bar");
    }

    [AvaloniaFact]
    public void MainWindow_HasStatusBarText()
    {
        var window = CreateWindow();
        var texts = GetAllControls<TextBlock>(window).Select(t => t.Text).ToList();
        Assert.Contains("Ready", texts);
    }

    [AvaloniaFact]
    public void MainWindow_HasRecentFilesSection()
    {
        var window = CreateWindow();
        var texts = GetAllControls<TextBlock>(window).Select(t => t.Text).ToList();
        Assert.Contains(texts, t => t.Contains("Recent", StringComparison.OrdinalIgnoreCase));
    }

    [AvaloniaFact]
    public void SettingsWindow_CreatesSuccessfully()
    {
        var settingsWindow = new SettingsWindow();
        Assert.Equal("Settings", settingsWindow.Title);
    }

    [AvaloniaFact]
    public void SettingsWindow_HasExpectedControls()
    {
        var settingsWindow = new SettingsWindow();
        var buttons = GetAllControls<Button>(settingsWindow).Select(b => b.Content?.ToString()).ToList();
        var sliderCount = GetAllControls<Slider>(settingsWindow).Count();
        var checkBoxCount = GetAllControls<CheckBox>(settingsWindow).Count();
        var comboBoxCount = GetAllControls<ComboBox>(settingsWindow).Count();

        Assert.Contains("Save", buttons, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Cancel", buttons, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Browse", buttons, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Load", buttons, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Save As", buttons, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Delete", buttons, StringComparer.OrdinalIgnoreCase);
        Assert.True(sliderCount >= 1, "Expected at least one Slider for quality");
        Assert.True(checkBoxCount >= 1, "Expected CheckBox controls in settings");
        Assert.True(comboBoxCount >= 1, "Expected ComboBox controls in settings");
    }

    [AvaloniaFact]
    public void ConfirmDialog_CreatesSuccessfully()
    {
        var dialog = new ARWtoJXL.Avalonia.Services.ConfirmDialog();
        Assert.NotNull(dialog);
    }

    /* --- UI helpers --- */

    private MainWindow CreateWindow()
    {
        var vm = CreateViewModel();
        var window = new MainWindow();
        window.DataContext = vm;
        return window;
    }

    private static IEnumerable<T> GetAllControls<T>(Control root) where T : class
    {
        var result = new List<T>();
        Traverse(root);
        return result;

        void Traverse(Control control)
        {
            if (control is T tCtrl) result.Add(tCtrl);
            if (control is Panel panel)
            {
                foreach (var child in panel.Children)
                {
                    if (child is Control childCtrl)
                        Traverse(childCtrl);
                }
            }
            else if (control is ContentControl cc && cc.Content is Control contentCtrl)
            {
                Traverse(contentCtrl);
            }
        }
    }

    private static List<string?> GetButtonContents(Window window)
    {
        return GetAllControls<Button>(window).Select(b => b.Content?.ToString()).ToList();
    }
}
