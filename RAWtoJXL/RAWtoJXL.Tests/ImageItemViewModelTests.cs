using RAWtoJXL.Avalonia.ViewModels;

namespace RAWtoJXL.Tests;

public class ImageItemViewModelTests
{
    [Fact]
    public void EffectiveQuality_NoOverride_ReturnsGlobalQuality()
    {
        var vm = new ImageItemViewModel();
        Assert.Equal(85, vm.EffectiveQuality(85));
    }

    [Fact]
    public void EffectiveQuality_WithOverride_ReturnsOverride()
    {
        var vm = new ImageItemViewModel { QualityOverride = 95 };
        Assert.Equal(95, vm.EffectiveQuality(85));
    }

    [Fact]
    public void EffectiveQuality_OverrideZero_ReturnsZero()
    {
        var vm = new ImageItemViewModel { QualityOverride = 0 };
        Assert.Equal(0, vm.EffectiveQuality(85));
    }

    [Fact]
    public void EffectiveQuality_OverrideHundred_ReturnsHundred()
    {
        var vm = new ImageItemViewModel { QualityOverride = 100 };
        Assert.Equal(100, vm.EffectiveQuality(50));
    }

    [Fact]
    public void EffectiveQuality_GlobalQualityChanged_ReflectsChange()
    {
        var vm = new ImageItemViewModel();
        Assert.Equal(70, vm.EffectiveQuality(70));
        Assert.Equal(90, vm.EffectiveQuality(90));
    }

    [Fact]
    public void EffectiveQuality_OverrideSetThenCleared_FallsBackToGlobal()
    {
        var vm = new ImageItemViewModel { QualityOverride = 95 };
        Assert.Equal(95, vm.EffectiveQuality(85));
        vm.QualityOverride = null;
        Assert.Equal(85, vm.EffectiveQuality(85));
    }

    [Fact]
    public void QualitySliderValue_NoOverride_ReflectsGlobalPreset()
    {
        var vm = new ImageItemViewModel { GlobalQualityPreset = 75 };
        Assert.Equal(75, vm.QualitySliderValue);
    }

    [Fact]
    public void QualitySliderValue_GlobalPresetChange_UpdatesDisplayedValue()
    {
        var vm = new ImageItemViewModel();
        Assert.Equal(90, vm.QualitySliderValue);

        vm.GlobalQualityPreset = 60;

        Assert.Equal(60, vm.QualitySliderValue);
    }

    [Fact]
    public void QualitySliderValue_WithOverride_ShowsOverrideNotPreset()
    {
        var vm = new ImageItemViewModel { GlobalQualityPreset = 75 };
        vm.QualitySliderValue = 42;

        Assert.Equal(42, vm.QualitySliderValue);
        Assert.Equal(42, vm.QualityOverride);
        Assert.Equal(42, vm.EffectiveQuality(75));

        vm.GlobalQualityPreset = 50;
        Assert.Equal(42, vm.QualitySliderValue);
    }

    [Fact]
    public void QualitySliderValue_ClearingOverride_FallsBackToGlobalPreset()
    {
        var vm = new ImageItemViewModel { GlobalQualityPreset = 75 };
        vm.QualitySliderValue = 42;
        vm.QualityOverride = null;

        Assert.Equal(75, vm.QualitySliderValue);
    }
}
