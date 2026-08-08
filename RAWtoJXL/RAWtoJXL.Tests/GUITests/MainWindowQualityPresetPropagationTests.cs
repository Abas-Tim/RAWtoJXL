using System.IO;
using Avalonia.Headless.XUnit;
using RAWtoJXL.Avalonia.ViewModels;

namespace RAWtoJXL.Tests.GUITests;

[Trait("category", "gui")]
public class MainWindowQualityPresetPropagationTests
{
    [AvaloniaFact]
    public void QualityPresetChange_PropagatesToExistingItems()
    {
        var dir = Path.Combine(Path.GetTempPath(), "RAWtoJXL_Preset_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var file = Path.Combine(dir, "test.arw");
            File.WriteAllText(file, "");
            var vm = GUITestHelpers.CreateViewModel();
            vm.AddFilesAsync(new[] { file }).Wait();

            Assert.Equal(90, vm.Images[0].GlobalQualityPreset);
            Assert.Equal(90, vm.Images[0].QualitySliderValue);

            vm.QualityPreset = 65;

            Assert.Equal(65, vm.Images[0].GlobalQualityPreset);
            Assert.Equal(65, vm.Images[0].QualitySliderValue);

            vm.Images[0].QualitySliderValue = 30;
            Assert.Equal(30, vm.Images[0].QualitySliderValue);

            vm.QualityPreset = 40;
            Assert.Equal(30, vm.Images[0].QualitySliderValue);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [AvaloniaFact]
    public void NewlyAddedItems_ReceiveCurrentGlobalPreset()
    {
        var dir = Path.Combine(Path.GetTempPath(), "RAWtoJXL_Preset_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var file = Path.Combine(dir, "test.arw");
            File.WriteAllText(file, "");
            var vm = GUITestHelpers.CreateViewModel();
            vm.QualityPreset = 55;

            vm.AddFilesAsync(new[] { file }).Wait();

            Assert.Equal(55, vm.Images[0].GlobalQualityPreset);
            Assert.Equal(55, vm.Images[0].QualitySliderValue);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
