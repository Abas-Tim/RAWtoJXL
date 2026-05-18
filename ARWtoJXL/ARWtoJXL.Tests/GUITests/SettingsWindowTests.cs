using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using ARWtoJXL.Avalonia;
using ARWtoJXL.Avalonia.Services;
using ARWtoJXL.Avalonia.ViewModels;
using ARWtoJXL.Core.Interfaces;

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
    public void SettingsWindow_SaveCommand_PersistsAndCloses()
    {
        using var _ = new GUITestHelpers.SettingsScope();
        var sw = new SettingsWindow();
        sw.Show();
        sw.UpdateLayout();
        sw.Settings.QualityPreset = 25;

        bool closed = false;
        sw.Closed += (_, _) => closed = true;

        sw.Settings.SaveCommand.Execute(null);

        Assert.Equal(25, SettingsService.Load().QualityPreset);
        Assert.True(closed, "Settings window should close after Save");
    }

    [AvaloniaFact]
    public void SettingsWindow_CancelCommand_ClosesWindow()
    {
        using var _ = new GUITestHelpers.SettingsScope();
        var sw = new SettingsWindow();
        sw.Show();
        sw.UpdateLayout();

        bool closed = false;
        sw.Closed += (_, _) => closed = true;

        sw.Settings.CancelCommand.Execute(null);

        Assert.True(closed, "Settings window should close after Cancel");
    }

    [AvaloniaFact]
    public void SettingsWindow_QualitySlider_UpdatesQualityPreset()
    {
        using var _ = new GUITestHelpers.SettingsScope();
        var sw = new SettingsWindow();
        var tab = GUITestHelpers.SelectTab(sw, "Conversion");

        var slider = GUITestHelpers.GetAllControls<Slider>(tab).First();
        slider.Value = 55;

        Assert.Equal(55, sw.Settings.QualityPreset);
    }

    [AvaloniaFact]
    public void SettingsWindow_QualitySlider_ClampedBySliderRange()
    {
        using var _ = new GUITestHelpers.SettingsScope();
        var sw = new SettingsWindow();
        var tab = GUITestHelpers.SelectTab(sw, "Conversion");

        var slider = GUITestHelpers.GetAllControls<Slider>(tab).First();
        Assert.Equal(0, slider.Minimum);
        Assert.Equal(100, slider.Maximum);
    }

    [AvaloniaFact]
    public void SettingsWindow_OutputFormat_UpdatesOnSelection()
    {
        using var _ = new GUITestHelpers.SettingsScope();
        var sw = new SettingsWindow();
        var tab = GUITestHelpers.SelectTab(sw, "Conversion");

        var formatCombo = GUITestHelpers.GetAllControls<ComboBox>(tab)
            .First(c => c.Items.OfType<OutputFormat>().Any());

        formatCombo.SelectedItem = OutputFormat.Png;

        Assert.Equal(OutputFormat.Png, sw.Settings.OutputFormat);
    }

    [AvaloniaFact]
    public void SettingsWindow_OutputFormat_HasAllOptions()
    {
        using var _ = new GUITestHelpers.SettingsScope();
        var sw = new SettingsWindow();
        var tab = GUITestHelpers.SelectTab(sw, "Conversion");

        var formatCombo = GUITestHelpers.GetAllControls<ComboBox>(tab)
            .First(c => c.Items.OfType<OutputFormat>().Any());

        Assert.NotNull(formatCombo.ItemsSource);
        var formats = formatCombo.ItemsSource.Cast<OutputFormat>().ToList();
        Assert.Contains(OutputFormat.Jxl, formats);
        Assert.Contains(OutputFormat.Jpeg, formats);
        Assert.Contains(OutputFormat.Png, formats);
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

    [AvaloniaFact]
    public void SettingsWindow_SubfolderValidation_UpdatesThroughBinding()
    {
        using var _ = new GUITestHelpers.SettingsScope();
        var sw = new SettingsWindow();
        var tab = GUITestHelpers.SelectTab(sw, "Output");

        var textBox = GUITestHelpers.GetAllControls<TextBox>(tab).First();
        textBox.Text = "valid_name";

        Assert.Null(sw.Settings.SubfolderNameValidationResult);
    }

    [AvaloniaFact]
    public void SettingsWindow_TabSwitch_LoadsDifferentContent()
    {
        using var _ = new GUITestHelpers.SettingsScope();
        var sw = new SettingsWindow();
        sw.Show();
        sw.UpdateLayout();

        var conversionTab = GUITestHelpers.SelectTab(sw, "Conversion");
        var sliders = GUITestHelpers.GetAllControls<Slider>(conversionTab).ToList();
        Assert.NotEmpty(sliders);

        var outputTab = GUITestHelpers.SelectTab(sw, "Output");
        var checkBoxes = GUITestHelpers.GetAllControls<CheckBox>(outputTab).ToList();
        Assert.True(checkBoxes.Count >= 2, "Output tab should have multiple checkboxes");
    }

    [AvaloniaFact]
    public void SettingsWindow_CjxlEffort_UpdatesOnSelection()
    {
        using var _ = new GUITestHelpers.SettingsScope();
        var sw = new SettingsWindow();
        var tab = GUITestHelpers.SelectTab(sw, "Conversion");

        var effortCombo = GUITestHelpers.GetAllControls<ComboBox>(tab)
            .First(c => c.Items.OfType<SettingsViewModel.EffortOption>().Any());

        var option7 = sw.Settings.CjxlEffortOptions.First(e => e.Value == 7);
        effortCombo.SelectedItem = option7;

        Assert.Equal(7, sw.Settings.CjxlEffort);
    }

    [AvaloniaFact]
    public void SettingsWindow_CjxlEffort_HasCorrectOptions()
    {
        using var _ = new GUITestHelpers.SettingsScope();
        var sw = new SettingsWindow();
        var tab = GUITestHelpers.SelectTab(sw, "Conversion");

        var effortCombo = GUITestHelpers.GetAllControls<ComboBox>(tab)
            .First(c => c.Items.OfType<SettingsViewModel.EffortOption>().Any());

        Assert.NotNull(effortCombo.ItemsSource);
        var options = effortCombo.ItemsSource.Cast<SettingsViewModel.EffortOption>().ToList();
        Assert.Contains(options, o => o.Display == "1" && o.Value == 1);
        Assert.Contains(options, o => o.Display == "9" && o.Value == 9);
    }

    [AvaloniaFact]
    public void SettingsWindow_SkipMetadata_TogglesOnVM()
    {
        using var _ = new GUITestHelpers.SettingsScope();
        var sw = new SettingsWindow();
        Assert.False(sw.Settings.SkipMetadata);

        sw.Settings.SkipMetadata = true;

        Assert.True(sw.Settings.SkipMetadata);
    }
}
