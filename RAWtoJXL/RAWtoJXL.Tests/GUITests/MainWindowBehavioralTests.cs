using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using RAWtoJXL.Avalonia;
using RAWtoJXL.Avalonia.Behaviors;
using RAWtoJXL.Avalonia.Controls;
using RAWtoJXL.Avalonia.Services;
using RAWtoJXL.Avalonia.ViewModels;
using RAWtoJXL.Core.Interfaces;
using RAWtoJXL.Core.Settings;
using Moq;

namespace RAWtoJXL.Tests.GUITests;

[Trait("category", "gui")]
[Collection("Settings")]
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
    public void MainWindow_SettingsToggle_OpensRightPanel()
    {
        using var _ = new GUITestHelpers.SettingsScope();
        var vm = GUITestHelpers.CreateViewModel();
        var window = GUITestHelpers.CreateWindow(vm);

        var toggle = GUITestHelpers.GetAllControls<ToggleButton>(window)
            .First(t => t.Content?.ToString() == "Settings");

        toggle.IsChecked = true;

        Assert.True(vm.IsSettingsOpen, "Settings toggle should open the settings panel");
        var overlay = window.FindControl<Grid>("SettingsOverlay");
        Assert.True(overlay!.IsVisible, "Settings overlay should be visible when open");
        var host = window.FindControl<ContentControl>("SettingsHost");
        Assert.NotNull(host!.Content);
        Assert.Contains(
            GUITestHelpers.GetAllControls<TextBlock>(host),
            t => t.Text == "Settings");
    }

    [AvaloniaFact]
    public void MainWindow_SettingsToggle_ClosesPanelWhenUnchecked()
    {
        using var _ = new GUITestHelpers.SettingsScope();
        var vm = GUITestHelpers.CreateViewModel();
        var window = GUITestHelpers.CreateWindow(vm);

        var toggle = GUITestHelpers.GetAllControls<ToggleButton>(window)
            .First(t => t.Content?.ToString() == "Settings");

        toggle.IsChecked = true;
        Assert.True(vm.IsSettingsOpen);

        toggle.IsChecked = false;

        Assert.False(vm.IsSettingsOpen, "Unchecking the toggle should close the panel");
        var host = window.FindControl<ContentControl>("SettingsHost");
        Assert.Null(host!.Content);
    }

    [AvaloniaFact]
    public void MainWindow_SettingsPanel_BackdropClickCloses()
    {
        using var _ = new GUITestHelpers.SettingsScope();
        var vm = GUITestHelpers.CreateViewModel();
        var window = GUITestHelpers.CreateWindow(vm);

        vm.IsSettingsOpen = true;
        window.UpdateLayout();
        Assert.True(vm.IsSettingsOpen);

        window.MouseDown(new global::Avalonia.Point(20, 350), MouseButton.Left, RawInputModifiers.None);

        Assert.False(vm.IsSettingsOpen, "Clicking outside the panel should close it");
    }

    [AvaloniaFact]
    public void MainWindow_SettingsPanel_CloseButtonCloses()
    {
        using var _ = new GUITestHelpers.SettingsScope();
        var vm = GUITestHelpers.CreateViewModel();
        var window = GUITestHelpers.CreateWindow(vm);

        vm.IsSettingsOpen = true;
        window.UpdateLayout();

        var host = window.FindControl<ContentControl>("SettingsHost");
        var closeButton = GUITestHelpers.GetAllControls<Button>(host!)
            .First(b => b.Content?.ToString() == "✕");
        closeButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.False(vm.IsSettingsOpen, "Close button should close the panel");
    }

    [AvaloniaFact]
    public void MainWindow_SettingsPanel_ChangesRefreshMainViewModelOnClose()
    {
        using var _ = new GUITestHelpers.SettingsScope();
        var vm = GUITestHelpers.CreateViewModel();
        var window = GUITestHelpers.CreateWindow(vm);

        vm.IsSettingsOpen = true;
        window.UpdateLayout();

        var host = window.FindControl<ContentControl>("SettingsHost");
        var panelView = host!.Content as SettingsPanelView;
        Assert.NotNull(panelView);

        panelView!.Settings.QualityPreset = 33;
        Assert.Equal(33, SettingsService.Load().QualityPreset);

        vm.IsSettingsOpen = false;

        Assert.Equal(33, vm.QualityPreset);
    }

    [AvaloniaFact]
    public void MainWindow_ConvertButton_InvokesConversion()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "RAWtoJXL_Test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var arwFile = Path.Combine(tempDir, "test.arw");
        File.WriteAllText(arwFile, "");

        try
        {
            var mockImageService = new Mock<IImageService>();
       mockImageService
                 .Setup(x => x.ConvertToJxlAsync(
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
                x => x.ConvertToJxlAsync(
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
        Assert.Equal(0.0, openFolderButton!.Opacity);
        Assert.False(openFolderButton.IsEnabled);

        vm.Images[0].OutputPath = @"C:\some\output\path";

        Assert.Equal(1.0, openFolderButton.Opacity);
        Assert.True(openFolderButton.IsEnabled);
    }
}
