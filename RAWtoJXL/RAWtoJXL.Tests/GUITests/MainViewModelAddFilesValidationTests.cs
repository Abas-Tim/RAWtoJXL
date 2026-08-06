using System.IO;
using Avalonia.Headless.XUnit;
using RAWtoJXL.Avalonia.ViewModels;

namespace RAWtoJXL.Tests.GUITests;

[Trait("category", "gui")]
public class MainViewModelAddFilesValidationTests
{
    [AvaloniaFact]
    public void AddFilesAsync_InvalidPathThenValid_FileCanBeAddedLater()
    {
        MainViewModel.HeadlessTestMode = true;
        var dir = Path.Combine(Path.GetTempPath(), "RAWtoJXL_Validation_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "test.arw");
        try
        {
            var vm = GUITestHelpers.CreateViewModel();

            vm.AddFilesAsync(new[] { file }).Wait();
            Assert.Empty(vm.Images);

            File.WriteAllText(file, "now exists");
            vm.AddFilesAsync(new[] { file }).Wait();
            Assert.Single(vm.Images);
        }
        finally
        {
            Directory.Delete(dir, true);
            MainViewModel.HeadlessTestMode = false;
        }
    }

    [AvaloniaFact]
    public void AddFilesAsync_UnsupportedExtension_FileCanBeAddedLaterWhenSupported()
    {
        MainViewModel.HeadlessTestMode = true;
        var dir = Path.Combine(Path.GetTempPath(), "RAWtoJXL_Validation_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "test.txt");
        try
        {
            var vm = GUITestHelpers.CreateViewModel();
            File.WriteAllText(file, "text");

            vm.AddFilesAsync(new[] { file }).Wait();
            Assert.Empty(vm.Images);

            File.Move(file, Path.ChangeExtension(file, ".arw"));
            vm.AddFilesAsync(new[] { Path.ChangeExtension(file, ".arw") }).Wait();
            Assert.Single(vm.Images);
        }
        finally
        {
            Directory.Delete(dir, true);
            MainViewModel.HeadlessTestMode = false;
        }
    }

    [AvaloniaFact]
    public void AddFilesAsync_ValidPathTwice_AddedOnlyOnce()
    {
        MainViewModel.HeadlessTestMode = true;
        var dir = Path.Combine(Path.GetTempPath(), "RAWtoJXL_Validation_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "test.arw");
        try
        {
            File.WriteAllText(file, "x");
            var vm = GUITestHelpers.CreateViewModel();

            vm.AddFilesAsync(new[] { file }).Wait();
            vm.AddFilesAsync(new[] { file }).Wait();

            Assert.Single(vm.Images);
        }
        finally
        {
            Directory.Delete(dir, true);
            MainViewModel.HeadlessTestMode = false;
        }
    }
}
