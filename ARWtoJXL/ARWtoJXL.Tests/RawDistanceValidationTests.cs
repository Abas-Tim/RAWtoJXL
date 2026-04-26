using ARWtoJXL.Avalonia.ViewModels;

namespace ARWtoJXL.Tests;

public class RawDistanceValidationTests
{
    [Fact]
    public void ValidateRawDistance_Empty_ReturnsNull()
    {
        Assert.Null(SettingsViewModel.ValidateRawDistance(""));
    }

    [Fact]
    public void ValidateRawDistance_Whitespace_ReturnsNull()
    {
        Assert.Null(SettingsViewModel.ValidateRawDistance("   "));
    }

    [Fact]
    public void ValidateRawDistance_ValidValue_ReturnsNull()
    {
        Assert.Null(SettingsViewModel.ValidateRawDistance("0.5"));
        Assert.Null(SettingsViewModel.ValidateRawDistance("10.0"));
        Assert.Null(SettingsViewModel.ValidateRawDistance("0.0"));
        Assert.Null(SettingsViewModel.ValidateRawDistance("150.0"));
    }

    [Fact]
    public void ValidateRawDistance_Negative_ReturnsError()
    {
        var result = SettingsViewModel.ValidateRawDistance("-1.0");
        Assert.NotNull(result);
    }

    [Fact]
    public void ValidateRawDistance_TooHigh_ReturnsError()
    {
        var result = SettingsViewModel.ValidateRawDistance("151.0");
        Assert.NotNull(result);
    }

    [Fact]
    public void ValidateRawDistance_NonNumeric_ReturnsError()
    {
        var result = SettingsViewModel.ValidateRawDistance("abc");
        Assert.NotNull(result);
    }
}
