using System.IO;
using Avalonia.Headless.XUnit;
using Moq;
using RAWtoJXL.Avalonia.ViewModels;
using RAWtoJXL.Avalonia.Services;

namespace RAWtoJXL.Tests.GUITests;

[Trait("category", "gui")]
public class MainWindowOpenFolderTests
{
    private static async Task WaitUntilAsync(Func<bool> condition, string failureMessage)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail(failureMessage);
            }
            global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            await Task.Delay(25);
        }
    }

    [AvaloniaFact]
    public async Task OpenFolder_AddsSupportedFilesFromPicker()
    {
        var dir = Path.Combine(Path.GetTempPath(), "RAWtoJXL_OpenFolder_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "a.arw"), "");
            File.WriteAllText(Path.Combine(dir, "b.cr3"), "");
            File.WriteAllText(Path.Combine(dir, "notes.txt"), "not an image");

            var filePicker = new Mock<IFilePickerService>();
            filePicker.Setup(x => x.PickFolderAsync(It.IsAny<string>()))
                      .ReturnsAsync(dir);
            var vm = GUITestHelpers.CreateViewModel(filePickerService: filePicker);

            Assert.True(vm.OpenFolderCommand.CanExecute(null), "OpenFolder should be executable");
            await vm.OpenFolderCommand.ExecuteAsync(null);
            filePicker.Verify(x => x.PickFolderAsync(It.IsAny<string>()), Times.Once);

            Assert.Equal(2, vm.Images.Count);
            Assert.DoesNotContain(vm.Images, i => i.FileName == "notes.txt");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [AvaloniaFact]
    public async Task OpenFolder_EmptyOrNoFiles_AddsNothing()
    {
        var dir = Path.Combine(Path.GetTempPath(), "RAWtoJXL_OpenFolder_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var filePicker = new Mock<IFilePickerService>();
            filePicker.Setup(x => x.PickFolderAsync(It.IsAny<string>()))
                      .ReturnsAsync(dir);
            var vm = GUITestHelpers.CreateViewModel(filePickerService: filePicker);

            vm.OpenFolderCommand.Execute(null);
            await Task.Delay(500);

            Assert.Empty(vm.Images);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
