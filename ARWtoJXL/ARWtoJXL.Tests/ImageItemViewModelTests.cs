using ARWtoJXL.Avalonia.ViewModels;

namespace ARWtoJXL.Tests;

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
}
