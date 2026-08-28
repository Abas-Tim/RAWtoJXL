using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using RAWtoJXL.Avalonia;
using RAWtoJXL.Avalonia.Controls;
using RAWtoJXL.Avalonia.Services;
using RAWtoJXL.Avalonia.ViewModels;
using RAWtoJXL.Core.Interfaces;
using RAWtoJXL.Core.Settings;

namespace RAWtoJXL.Tests.GUITests;

[Trait("category", "gui")]
[Collection("Settings")]
public class SettingsPanelTests
{
    [AvaloniaFact]
    public void SettingsPanel_CreatesSuccessfully()
    {
        using var _ = new GUITestHelpers.SettingsScope();
        var panel = new SettingsPanelView();
        var headers = GUITestHelpers.GetAllControls<TextBlock>(panel).Select(t => t.Text).ToList();
        Assert.Contains("Settings", headers);
    }

    [AvaloniaFact]
    public void SettingsPanel_HasFiveTabs()
    {
        using var _ = new GUITestHelpers.SettingsScope();
        var panel = new SettingsPanelView();
        var tabControl = GUITestHelpers.FindAll<TabControl>(panel).First();
        var headers = tabControl.Items.Cast<TabItem>().Select(t => t.Header?.ToString()).ToList();
        Assert.Equal(new[] { "Conversion", "Output", "Behavior", "Hardware", "Presets" }, headers);
    }

    [AvaloniaFact]
    public void SettingsPanel_HasSaveAndCancelButton()
    {
        using var _ = new GUITestHelpers.SettingsScope();
        var panel = new SettingsPanelView();
        var buttons = GUITestHelpers.GetAllControls<Button>(panel).Select(b => b.Content?.ToString()).ToList();
        Assert.Contains("Save", buttons, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Cancel", buttons, StringComparer.OrdinalIgnoreCase);
    }

    [AvaloniaFact]
    public void SettingsPanel_HasCloseButton()
    {
        using var _ = new GUITestHelpers.SettingsScope();
        var panel = new SettingsPanelView();
        var buttons = GUITestHelpers.GetAllControls<Button>(panel).Select(b => b.Content?.ToString()).ToList();
        Assert.Contains("✕", buttons);
    }

    [AvaloniaFact]
    public void SettingsPanel_SaveCommand_PersistsAndRequestsClose()
    {
        using var _ = new GUITestHelpers.SettingsScope();
        var panel = new SettingsPanelView();
        panel.Settings.QualityPreset = 25;

        bool closed = false;
        panel.RequestClose += (_, _) => closed = true;

        panel.Settings.SaveCommand.Execute(null);

        Assert.Equal(25, SettingsService.Load().QualityPreset);
        Assert.True(closed, "Settings panel should request close after Save");
    }

    [AvaloniaFact]
    public void SettingsPanel_CancelCommand_RequestsClose()
    {
        using var _ = new GUITestHelpers.SettingsScope();
        var panel = new SettingsPanelView();

        bool closed = false;
        panel.RequestClose += (_, _) => closed = true;

        panel.Settings.CancelCommand.Execute(null);

        Assert.True(closed, "Settings panel should request close after Cancel");
    }

    [AvaloniaFact]
    public void SettingsPanel_QualitySlider_UpdatesQualityPreset()
    {
        using var _ = new GUITestHelpers.SettingsScope();
        var panel = new SettingsPanelView();
        var tab = GUITestHelpers.SelectTab(panel, "Conversion");

        var slider = GUITestHelpers.GetAllControls<Slider>(tab).First();
        slider.Value = 55;

        Assert.Equal(55, panel.Settings.QualityPreset);
    }

    [AvaloniaFact]
    public void SettingsPanel_QualitySlider_ClampedBySliderRange()
    {
        using var _ = new GUITestHelpers.SettingsScope();
        var panel = new SettingsPanelView();
        var tab = GUITestHelpers.SelectTab(panel, "Conversion");

        var slider = GUITestHelpers.GetAllControls<Slider>(tab).First();
        Assert.Equal(0, slider.Minimum);
        Assert.Equal(100, slider.Maximum);
    }

    [AvaloniaFact]
    public void SettingsPanel_OutputFormat_UpdatesOnSelection()
    {
        using var _ = new GUITestHelpers.SettingsScope();
        var panel = new SettingsPanelView();
        var tab = GUITestHelpers.SelectTab(panel, "Conversion");

        var formatCombo = GUITestHelpers.GetAllControls<ComboBox>(tab)
            .First(c => c.Items.OfType<OutputFormat>().Any());

        formatCombo.SelectedItem = OutputFormat.Avif;

        Assert.Equal(OutputFormat.Avif, panel.Settings.OutputFormat);
    }

    [AvaloniaFact]
    public void SettingsPanel_OutputFormat_HasAllOptions()
    {
        using var _ = new GUITestHelpers.SettingsScope();
        var panel = new SettingsPanelView();
        var tab = GUITestHelpers.SelectTab(panel, "Conversion");

        var formatCombo = GUITestHelpers.GetAllControls<ComboBox>(tab)
            .First(c => c.Items.OfType<OutputFormat>().Any());

        Assert.NotNull(formatCombo.ItemsSource);
        var formats = formatCombo.ItemsSource.Cast<OutputFormat>().ToList();
        Assert.Contains(OutputFormat.Jxl, formats);
        Assert.Contains(OutputFormat.Jpeg, formats);
        Assert.Contains(OutputFormat.Avif, formats);
        Assert.Equal(3, formats.Count);
    }

    [AvaloniaFact]
    public void SettingsPanel_SubfolderValidation_HidesWhenValid()
    {
        Assert.Null(SettingsViewModel.ValidateSubfolderName("valid_name"));
        Assert.Null(SettingsViewModel.ValidateSubfolderName("my folder"));
        Assert.Null(SettingsViewModel.ValidateSubfolderName("output_2024"));
    }

    [AvaloniaFact]
    public void SettingsPanel_SubfolderValidation_ShowsWhenInvalid()
    {
        Assert.NotNull(SettingsViewModel.ValidateSubfolderName("invalid|name"));
        Assert.NotNull(SettingsViewModel.ValidateSubfolderName("test\x00folder"));
        Assert.NotNull(SettingsViewModel.ValidateSubfolderName("  leading"));
        Assert.NotNull(SettingsViewModel.ValidateSubfolderName("CON"));
    }

    [AvaloniaFact]
    public void SettingsPanel_SubfolderValidation_UpdatesThroughBinding()
    {
        using var _ = new GUITestHelpers.SettingsScope();
        var panel = new SettingsPanelView();
        var tab = GUITestHelpers.SelectTab(panel, "Output");

        var textBox = GUITestHelpers.GetAllControls<TextBox>(tab).First();
        textBox.Text = "valid_name";

        Assert.Null(panel.Settings.SubfolderNameValidationResult);
    }

    [AvaloniaFact]
    public void SettingsPanel_TabSwitch_LoadsDifferentContent()
    {
        using var _ = new GUITestHelpers.SettingsScope();
        var panel = new SettingsPanelView();

        var conversionTab = GUITestHelpers.SelectTab(panel, "Conversion");
        var sliders = GUITestHelpers.GetAllControls<Slider>(conversionTab).ToList();
        Assert.NotEmpty(sliders);

        var outputTab = GUITestHelpers.SelectTab(panel, "Output");
        var checkBoxes = GUITestHelpers.GetAllControls<CheckBox>(outputTab).ToList();
        Assert.True(checkBoxes.Count >= 2, "Output tab should have multiple checkboxes");
    }

    [AvaloniaFact]
    public void SettingsPanel_CjxlEffort_UpdatesOnSelection()
    {
        using var _ = new GUITestHelpers.SettingsScope();
        var panel = new SettingsPanelView();
        var tab = GUITestHelpers.SelectTab(panel, "Conversion");

        var effortCombo = GUITestHelpers.GetAllControls<ComboBox>(tab)
            .First(c => c.Items.OfType<SettingsViewModel.EffortOption>().Any());

        var option7 = panel.Settings.CjxlEffortOptions.First(e => e.Value == 7);
        effortCombo.SelectedItem = option7;

        Assert.Equal(7, panel.Settings.CjxlEffort);
    }

    [AvaloniaFact]
    public void SettingsPanel_CjxlEffort_HasCorrectOptions()
    {
        using var _ = new GUITestHelpers.SettingsScope();
        var panel = new SettingsPanelView();
        var tab = GUITestHelpers.SelectTab(panel, "Conversion");

        var effortCombo = GUITestHelpers.GetAllControls<ComboBox>(tab)
            .First(c => c.Items.OfType<SettingsViewModel.EffortOption>().Any());

        Assert.NotNull(effortCombo.ItemsSource);
        var options = effortCombo.ItemsSource.Cast<SettingsViewModel.EffortOption>().ToList();
        Assert.Contains(options, o => o.Display == "1" && o.Value == 1);
        Assert.Contains(options, o => o.Display == "9" && o.Value == 9);
    }

    [AvaloniaFact]
    public void SettingsPanel_SkipMetadata_TogglesOnVM()
    {
        using var _ = new GUITestHelpers.SettingsScope();
        var panel = new SettingsPanelView();
        Assert.False(panel.Settings.SkipMetadata);

        panel.Settings.SkipMetadata = true;

        Assert.True(panel.Settings.SkipMetadata);
    }
}
