using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using ARWtoJXL.Avalonia;
using ARWtoJXL.Avalonia.ViewModels;

namespace ARWtoJXL.Tests.GUITests;

[Trait("category", "gui")]
public class SettingsWindowTests
{
    [AvaloniaFact]
    public void SettingsWindow_CreatesSuccessfully()
    {
        using var _ = new GUITestHelpers.SettingsScope();
        var sw = new SettingsWindow();
        Assert.Equal("Settings", sw.Title);
    }

    [AvaloniaFact]
    public void SettingsWindow_HasFiveTabs()
    {
        using var _ = new GUITestHelpers.SettingsScope();
        var sw = new SettingsWindow();
        var tabControl = GUITestHelpers.FindAll<TabControl>(sw).First();
        var headers = tabControl.Items.Cast<TabItem>().Select(t => t.Header?.ToString()).ToList();
        Assert.Equal(new[] { "Conversion", "Output", "Behavior", "Hardware", "Presets" }, headers);
    }

    [AvaloniaFact]
    public void SettingsWindow_HasSaveAndCancelButton()
    {
        using var _ = new GUITestHelpers.SettingsScope();
        var sw = new SettingsWindow();
        var buttons = GUITestHelpers.GetAllControls<Button>(sw).Select(b => b.Content?.ToString()).ToList();
        Assert.Contains("Save", buttons, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Cancel", buttons, StringComparer.OrdinalIgnoreCase);
    }

    [AvaloniaFact]
    public void SettingsWindow_SubfolderValidation_HidesWhenValid()
    {
        Assert.Null(SettingsViewModel.ValidateSubfolderName("valid_name"));
        Assert.Null(SettingsViewModel.ValidateSubfolderName("my folder"));
        Assert.Null(SettingsViewModel.ValidateSubfolderName("output_2024"));
    }

    [AvaloniaFact]
    public void SettingsWindow_SubfolderValidation_ShowsWhenInvalid()
    {
        Assert.NotNull(SettingsViewModel.ValidateSubfolderName("invalid|name"));
        Assert.NotNull(SettingsViewModel.ValidateSubfolderName("test\x00folder"));
        Assert.NotNull(SettingsViewModel.ValidateSubfolderName("  leading"));
        Assert.NotNull(SettingsViewModel.ValidateSubfolderName("CON"));
    }
}
