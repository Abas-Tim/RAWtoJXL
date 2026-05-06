using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using ARWtoJXL.Avalonia;
using ARWtoJXL.Avalonia.Behaviors;
using ARWtoJXL.Avalonia.Services;
using ARWtoJXL.Avalonia.ViewModels;
using ARWtoJXL.Core.Interfaces;
using Moq;

namespace ARWtoJXL.Tests.GUITests;

[Trait("category", "gui")]
public class MainWindowBehavioralTests
{
    [AvaloniaFact]
    public void MainWindow_SelectAll_TogglesItemSelection()
    {
        var vm = GUITestHelpers.CreateViewModel();
        GUITestHelpers.AddTestFiles(vm, 3);

        foreach (var item in vm.Images)
            item.IsSelected = false;

        Assert.False(vm.IsAllSelected);

        vm.SelectAllCommand.Execute(null);

        Assert.True(vm.IsAllSelected);
        Assert.All(vm.Images, item => Assert.True(item.IsSelected));
    }

    [AvaloniaFact]
    public void MainWindow_RemoveSelected_RemovesItems()
    {
        var vm = GUITestHelpers.CreateViewModel();
        GUITestHelpers.AddTestFiles(vm, 3);

        vm.Images[0].IsSelected = true;
        vm.Images[1].IsSelected = true;

        Assert.Equal(3, vm.Images.Count);

        vm.RemoveSelectedCommand.Execute(null);

        Assert.Single(vm.Images);
    }

    [AvaloniaFact]
    public void MainWindow_SettingsButton_RaisesRequestOpenSettings()
    {
        var vm = GUITestHelpers.CreateViewModel();

        var received = false;
        vm.RequestOpenSettings += () => received = true;

        vm.OpenSettingsCommand.Execute(null);

        Assert.True(received, "RequestOpenSettings event should have been raised");
    }

    [AvaloniaFact]
    public void MainWindow_ConvertButton_InvokesConversion()
    {
        MainViewModel.HeadlessTestMode = true;
        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "ARWtoJXL_Test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var arwFile = Path.Combine(tempDir, "test.arw");
            File.WriteAllText(arwFile, "");

            try
            {
                var mockImageService = new Mock<IImageService>();
                mockImageService
                    .Setup(x => x.ConvertArwToJxlAsync(
                        It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Action<double>>(),
                        It.IsAny<int>(), It.IsAny<OutputFormat>(), It.IsAny<CancellationToken>(),
                        It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<int?>()))
                    .Returns(Task.CompletedTask);

                mockImageService
                    .Setup(x => x.GetThumbnailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(Array.Empty<byte>());

                var mockDialog = new Mock<IDialogService>();
                mockDialog
                    .Setup(x => x.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>()))
                    .ReturnsAsync(true);

                var vm = GUITestHelpers.CreateViewModel(
                    imageService: mockImageService,
                    dialogService: mockDialog);
                vm.UseSubfolder = false;
                vm.OutputDirectory = tempDir;

                vm.AddFilesAsync(new[] { arwFile }).Wait();
                vm.Images[0].IsSelected = true;

                Assert.True(vm.ConvertSelectedCommand.CanExecute(null));

                vm.ConvertSelectedCommand.Execute(null);

                Assert.Equal(ImageStatus.Converted, vm.Images[0].Status);

                mockImageService.Verify(
                    x => x.ConvertArwToJxlAsync(
                        arwFile, It.IsAny<string>(), It.IsAny<Action<double>>(),
                        It.IsAny<int>(), It.IsAny<OutputFormat>(), It.IsAny<CancellationToken>(),
                        It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<int?>()),
                    Times.Once);
            }
            finally
            {
                try { File.Delete(arwFile); } catch { }
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
        finally
        {
            MainViewModel.HeadlessTestMode = false;
        }
    }

    [AvaloniaFact]
    public void MainWindow_StatusMessage_UpdatesUI()
    {
        var vm = GUITestHelpers.CreateViewModel();
        var window = GUITestHelpers.CreateWindow(vm);

        vm.StatusMessage = "Processing 1/3 (33%)";

        var textBlocks = GUITestHelpers.GetAllControls<TextBlock>(window)
            .Select(t => t.Text)
            .ToList();

        Assert.Contains("Processing 1/3 (33%)", textBlocks);
    }

    [AvaloniaFact]
    public void MainWindow_CancelButton_VisibleWhenConverting()
    {
        var vm = GUITestHelpers.CreateViewModel();
        var window = GUITestHelpers.CreateWindow(vm);

        var cancelButton = GUITestHelpers.GetAllControls<Button>(window)
            .FirstOrDefault(b => b.Content?.ToString() == "Cancel" && b.Classes.Contains("danger"));
        Assert.NotNull(cancelButton);
        Assert.False(cancelButton!.IsVisible, "Cancel button should be hidden when not converting");

        vm.IsConverting = true;

        Assert.True(cancelButton.IsVisible, "Cancel button should be visible when converting");
    }

    [AvaloniaFact]
    public void MainWindow_Gallery_RendersItemElements()
    {
        var vm = GUITestHelpers.CreateViewModel();
        GUITestHelpers.AddTestFiles(vm, 3);

        var window = GUITestHelpers.CreateWindow(vm);
        window.UpdateLayout();

        var repeater = window.FindControl<ItemsRepeater>("ImagesRepeater");
        Assert.NotNull(repeater);

        var firstElement = repeater!.TryGetElement(0);
        Assert.NotNull(firstElement);

        var secondElement = repeater.TryGetElement(1);
        Assert.NotNull(secondElement);
    }

    [AvaloniaFact]
    public void MainWindow_Gallery_RenderedItemsHaveCorrectDataContext()
    {
        var vm = GUITestHelpers.CreateViewModel();
        GUITestHelpers.AddTestFiles(vm, 2);

        var window = GUITestHelpers.CreateWindow(vm);
        window.UpdateLayout();

        var repeater = window.FindControl<ItemsRepeater>("ImagesRepeater");
        Assert.NotNull(repeater);

        var firstElement = repeater!.TryGetElement(0);
        Assert.NotNull(firstElement);
        Assert.IsType<ImageItemViewModel>(firstElement!.DataContext);

        var secondElement = repeater.TryGetElement(1);
        Assert.NotNull(secondElement);
        Assert.IsType<ImageItemViewModel>(secondElement!.DataContext);
    }

    [AvaloniaFact]
    public void MainWindow_Gallery_UpdatesWhenImagesAddedOrRemoved()
    {
        var vm = GUITestHelpers.CreateViewModel();
        GUITestHelpers.AddTestFiles(vm, 3);

        var window = GUITestHelpers.CreateWindow(vm);
        window.UpdateLayout();

        var repeater = window.FindControl<ItemsRepeater>("ImagesRepeater");
        Assert.NotNull(repeater);
        Assert.NotNull(repeater!.TryGetElement(0));
        Assert.NotNull(repeater.TryGetElement(2));

        vm.Images.RemoveAt(0);
        window.UpdateLayout();

        Assert.NotNull(repeater.TryGetElement(0));
        Assert.NotNull(repeater.TryGetElement(1));
    }

    [AvaloniaFact]
    public void MainWindow_ConvertButton_DisabledWithoutSelection()
    {
        var vm = GUITestHelpers.CreateViewModel();
        Assert.False(vm.IsAnySelected);
        Assert.False(vm.ConvertSelectedCommand.CanExecute(null));

        GUITestHelpers.AddTestFiles(vm, 1);
        vm.Images[0].IsSelected = true;

        Assert.True(vm.ConvertSelectedCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public void MainWindow_DragDropBehavior_EnabledOnRootGrid()
    {
        var vm = GUITestHelpers.CreateViewModel();
        var window = GUITestHelpers.CreateWindow(vm);
        var rootGrid = window.Content as Grid;
        Assert.NotNull(rootGrid);

        Assert.True(DragDropBehavior.GetEnableDragDrop(rootGrid!));
        Assert.True(DragDrop.GetAllowDrop(rootGrid!));
    }

    [AvaloniaFact]
    public void MainWindow_PerItemCheckBox_UpdatesSelectionState()
    {
        var vm = GUITestHelpers.CreateViewModel();
        GUITestHelpers.AddTestFiles(vm, 2);

        var window = GUITestHelpers.CreateWindow(vm);
        window.UpdateLayout();

        var repeater = window.FindControl<ItemsRepeater>("ImagesRepeater");
        Assert.NotNull(repeater);

        var firstElement = repeater!.TryGetElement(0);
        Assert.NotNull(firstElement);

        var checkBoxes = GUITestHelpers.GetAllControls<CheckBox>(firstElement!);
        var checkBox = checkBoxes.FirstOrDefault();
        Assert.NotNull(checkBox);

        Assert.False(vm.IsAnySelected);

        checkBox!.IsChecked = true;

        Assert.True(vm.Images[0].IsSelected, "VM should reflect CheckBox.IsChecked=true");
        Assert.True(vm.IsAnySelected);

        checkBox.IsChecked = false;

        Assert.False(vm.Images[0].IsSelected, "VM should reflect CheckBox.IsChecked=false");
        Assert.False(vm.IsAnySelected);
    }

    [AvaloniaFact]
    public void MainWindow_QualitySlider_UpdatesQualityOverride()
    {
        var vm = GUITestHelpers.CreateViewModel();
        GUITestHelpers.AddTestFiles(vm, 1);

        var window = GUITestHelpers.CreateWindow(vm);
        window.UpdateLayout();

        var repeater = window.FindControl<ItemsRepeater>("ImagesRepeater");
        Assert.NotNull(repeater);

        var firstElement = repeater!.TryGetElement(0);
        Assert.NotNull(firstElement);

        var sliders = GUITestHelpers.GetAllControls<Slider>(firstElement!);
        Assert.NotEmpty(sliders);

        var slider = sliders.First();
        slider.Value = 42;

        Assert.Equal(42, vm.Images[0].QualityOverride);
    }

    [AvaloniaFact]
    public void MainWindow_ItemOpenFolderButton_VisibilityUpdatesWithOutputPath()
    {
        var vm = GUITestHelpers.CreateViewModel();
        GUITestHelpers.AddTestFiles(vm, 1);

        var window = GUITestHelpers.CreateWindow(vm);
        window.UpdateLayout();

        var repeater = window.FindControl<ItemsRepeater>("ImagesRepeater");
        Assert.NotNull(repeater);

        var firstElement = repeater!.TryGetElement(0);
        Assert.NotNull(firstElement);

        var buttons = GUITestHelpers.GetAllControls<Button>(firstElement!)
            .Where(b => (b.Content?.ToString() ?? "").Contains("Open folder", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var openFolderButton = buttons.FirstOrDefault();
        Assert.NotNull(openFolderButton);
        Assert.False(openFolderButton!.IsVisible, "Open folder button should be hidden when OutputPath is empty");

        vm.Images[0].OutputPath = @"C:\some\output\path";

        Assert.True(openFolderButton.IsVisible, "Open folder button should be visible when OutputPath is set");
    }
}
