using System.IO;
using Avalonia.Headless.XUnit;
using RAWtoJXL.Avalonia.ViewModels;

namespace RAWtoJXL.Tests.GUITests;

[Trait("category", "gui")]
public class MainViewModelDedupTests
{
    private static string CreateTempArw(out string dir)
    {
        dir = Path.Combine(Path.GetTempPath(), "RAWtoJXL_Dedup_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "test.arw");
        File.WriteAllText(file, "x");
        return file;
    }

    [AvaloniaFact]
    public void AddFilesAsync_SamePathTwice_AddedOnlyOnce()
    {
        var file = CreateTempArw(out var dir);
        try
        {
            var vm = GUITestHelpers.CreateViewModel();

            vm.AddFilesAsync(new[] { file }).Wait();
            vm.AddFilesAsync(new[] { file }).Wait();

            Assert.Single(vm.Images);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [AvaloniaFact]
    public void RemoveSelected_ThenReAddSamePath_Allowed()
    {
        var file = CreateTempArw(out var dir);
        try
        {
            var vm = GUITestHelpers.CreateViewModel();

            vm.AddFilesAsync(new[] { file }).Wait();
            vm.Images[0].IsSelected = true;
            vm.RemoveSelectedCommand.Execute(null);
            Assert.Empty(vm.Images);

            vm.AddFilesAsync(new[] { file }).Wait();
            Assert.Single(vm.Images);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
