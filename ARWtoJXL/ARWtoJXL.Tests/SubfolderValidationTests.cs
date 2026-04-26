using ARWtoJXL.Avalonia.ViewModels;

namespace ARWtoJXL.Tests;

public class SubfolderValidationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateSubfolderName_EmptyOrWhitespace_ReturnsNull(string? value)
    {
        Assert.Null(SettingsViewModel.ValidateSubfolderName(value!));
    }

    [Theory]
    [InlineData("output")]
    [InlineData("JXL Photos")]
    [InlineData("2024-01")]
    [InlineData("_converted")]
    public void ValidateSubfolderName_ValidNames_ReturnsNull(string value)
    {
        Assert.Null(SettingsViewModel.ValidateSubfolderName(value));
    }

    [Fact]
    public void ValidateSubfolderName_PipeCharacter_ReturnsError()
    {
        var result = SettingsViewModel.ValidateSubfolderName("test|folder");
        Assert.NotNull(result);
        Assert.Contains("Invalid character", result);
    }

    [Fact]
    public void ValidateSubfolderName_ControlCharacter_ReturnsError()
    {
        var result = SettingsViewModel.ValidateSubfolderName("test\x00folder");
        Assert.NotNull(result);
        Assert.Contains("Invalid character", result);
    }

    [Theory]
    [InlineData(" leading")]
    [InlineData("trailing ")]
    [InlineData(" both ")]
    public void ValidateSubfolderName_LeadingOrTrailingWhitespace_ReturnsError(string value)
    {
        var result = SettingsViewModel.ValidateSubfolderName(value);
        Assert.NotNull(result);
        Assert.Contains("whitespace", result);
    }

    [Fact]
    public void ValidateSubfolderName_TooLong_ReturnsError()
    {
        var value = new string('a', 256);
        var result = SettingsViewModel.ValidateSubfolderName(value);
        Assert.NotNull(result);
        Assert.Contains("255", result);
    }

    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    public void ValidateSubfolderName_DotOrDotDot_ReturnsError(string value)
    {
        var result = SettingsViewModel.ValidateSubfolderName(value);
        Assert.NotNull(result);
        Assert.Contains("Invalid folder name", result);
    }

    [Theory]
    [InlineData("CON")]
    [InlineData("PRN")]
    [InlineData("AUX")]
    [InlineData("NUL")]
    [InlineData("COM1")]
    [InlineData("COM9")]
    [InlineData("LPT1")]
    [InlineData("LPT9")]
    [InlineData("con")]
    [InlineData("Con")]
    public void ValidateSubfolderName_ReservedNames_ReturnsError(string value)
    {
        var result = SettingsViewModel.ValidateSubfolderName(value);
        Assert.NotNull(result);
        Assert.Contains("reserved", result);
    }
}
