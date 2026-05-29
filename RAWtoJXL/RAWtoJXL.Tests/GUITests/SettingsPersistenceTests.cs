using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using RAWtoJXL.Avalonia;
using RAWtoJXL.Avalonia.ViewModels;
using RAWtoJXL.Core.Interfaces;

namespace RAWtoJXL.Tests.GUITests;

[Trait("category", "gui")]
public class SettingsPersistenceTests
{
    [AvaloniaFact]
    public void SettingsWindow_QualityPreset_PersistsAcrossReopens()
    {
        using var _ = new GUITestHelpers.SettingsScope();
        var sw1 = new SettingsWindow();
        var tab = GUITestHelpers.SelectTab(sw1, "Conversion");
        var slider = GUITestHelpers.GetAllControls<Slider>(tab).First();
        slider.Value = 75;
        sw1.UpdateLayout();
        sw1.Settings.Persist();
        Assert.Equal(75, SettingsService.Load().QualityPreset);
        sw1.Close();
        var sw2 = new SettingsWindow();
        Assert.Equal(75, sw2.Settings.QualityPreset);
    }

    [AvaloniaFact]
    public void SettingsWindow_OutputFormat_PersistsAcrossReopens()
    {
        using var _ = new GUITestHelpers.SettingsScope();
        var sw1 = new SettingsWindow();
        var tab = GUITestHelpers.SelectTab(sw1, "Conversion");
        var combo = GUITestHelpers.GetAllControls<ComboBox>(tab)
            .First(c => c.Items.OfType<OutputFormat>().Any());
        combo.SelectedItem = OutputFormat.Jpeg;
        sw1.UpdateLayout();
        sw1.Settings.Persist();
        Assert.Equal(OutputFormat.Jpeg, SettingsService.Load().OutputFormat);
        sw1.Close();
        var sw2 = new SettingsWindow();
        Assert.Equal(OutputFormat.Jpeg, sw2.Settings.OutputFormat);
    }

    [AvaloniaFact]
    public void SettingsWindow_CjxlEffort_PersistsAcrossReopens()
    {
        using var _ = new GUITestHelpers.SettingsScope();
        var sw1 = new SettingsWindow();
        var tab = GUITestHelpers.SelectTab(sw1, "Conversion");
        var effortCombo = GUITestHelpers.GetAllControls<ComboBox>(tab)
            .First(c => c.Items.OfType<SettingsViewModel.EffortOption>().Any());
        var option7 = sw1.Settings.CjxlEffortOptions.First(e => e.Value == 7);
        effortCombo.SelectedItem = option7;
        sw1.UpdateLayout();
        sw1.Settings.Persist();
        Assert.Equal(7, SettingsService.Load().CjxlEffort);
        sw1.Close();
        var sw2 = new SettingsWindow();
        Assert.Equal(7, sw2.Settings.CjxlEffort);
    }

    [AvaloniaFact]
    public void SettingsWindow_SkipMetadata_PersistsAcrossReopens()
    {
        using var _ = new GUITestHelpers.SettingsScope();
        var sw1 = new SettingsWindow();
        var tab = GUITestHelpers.SelectTab(sw1, "Conversion");
        var checkBox = GUITestHelpers.GetAllControls<CheckBox>(tab).First();
        checkBox.IsChecked = true;
        sw1.UpdateLayout();
        sw1.Settings.Persist();
        Assert.True(SettingsService.Load().SkipMetadata);
        sw1.Close();
        var sw2 = new SettingsWindow();
        Assert.True(sw2.Settings.SkipMetadata);
    }

    [AvaloniaFact]
    public void SettingsWindow_UseSubfolder_PersistsAcrossReopens()
    {
        using var _ = new GUITestHelpers.SettingsScope();
        var sw1 = new SettingsWindow();
        var tab = GUITestHelpers.SelectTab(sw1, "Output");
        var checkBoxes = GUITestHelpers.GetAllControls<CheckBox>(tab).ToList();
        checkBoxes.First().IsChecked = false;
        sw1.UpdateLayout();
        sw1.Settings.Persist();
        Assert.False(SettingsService.Load().UseSubfolder);
        sw1.Close();
        var sw2 = new SettingsWindow();
        Assert.False(sw2.Settings.UseSubfolder);
    }

    [AvaloniaFact]
    public void SettingsWindow_SubfolderName_PersistsAcrossReopens()
    {
        using var _ = new GUITestHelpers.SettingsScope();
        var sw1 = new SettingsWindow();
        var tab = GUITestHelpers.SelectTab(sw1, "Output");
        var textBox = GUITestHelpers.GetAllControls<TextBox>(tab).First();
        textBox.Text = "my_jxl_output";
        sw1.UpdateLayout();
        sw1.Settings.Persist();
        Assert.Equal("my_jxl_output", SettingsService.Load().SubfolderName);
        sw1.Close();
        var sw2 = new SettingsWindow();
        Assert.Equal("my_jxl_output", sw2.Settings.SubfolderName);
    }

    [AvaloniaFact]
    public void SettingsWindow_SearchRecursive_PersistsAcrossReopens()
    {
        using var _ = new GUITestHelpers.SettingsScope();
        var sw1 = new SettingsWindow();
        var tab = GUITestHelpers.SelectTab(sw1, "Output");
        var checkBoxes = GUITestHelpers.GetAllControls<CheckBox>(tab).ToList();
        checkBoxes.Last().IsChecked = true;
        sw1.UpdateLayout();
        sw1.Settings.Persist();
        Assert.True(SettingsService.Load().SearchRecursive);
        sw1.Close();
        var sw2 = new SettingsWindow();
        Assert.True(sw2.Settings.SearchRecursive);
    }

    [AvaloniaFact]
    public void SettingsWindow_ConflictResolution_PersistsAcrossReopens()
    {
        using var _ = new GUITestHelpers.SettingsScope();
        var sw1 = new SettingsWindow();
        var tab = GUITestHelpers.SelectTab(sw1, "Behavior");
        var combo = GUITestHelpers.GetAllControls<ComboBox>(tab).First();
        combo.SelectedItem = ConflictResolution.Skip;
        sw1.UpdateLayout();
        sw1.Settings.Persist();
        Assert.Equal(ConflictResolution.Skip, SettingsService.Load().ConflictResolution);
        sw1.Close();
        var sw2 = new SettingsWindow();
        Assert.Equal(ConflictResolution.Skip, sw2.Settings.ConflictResolution);
    }

    [AvaloniaFact]
    public void SettingsWindow_ConfirmOverwrite_PersistsAcrossReopens()
    {
        using var _ = new GUITestHelpers.SettingsScope();
        var sw1 = new SettingsWindow();
        var tab = GUITestHelpers.SelectTab(sw1, "Behavior");
        var checkBox = GUITestHelpers.GetAllControls<CheckBox>(tab).First();
        checkBox.IsChecked = false;
        sw1.UpdateLayout();
        sw1.Settings.Persist();
        Assert.False(SettingsService.Load().ConfirmOverwrite);
        sw1.Close();
        var sw2 = new SettingsWindow();
        Assert.False(sw2.Settings.ConfirmOverwrite);
    }

    [AvaloniaFact]
    public void SettingsWindow_Preset_SavesAndPersists()
    {
        using var _ = new GUITestHelpers.SettingsScope();
        var sw1 = new SettingsWindow();
        var tab = GUITestHelpers.SelectTab(sw1, "Presets");
        sw1.Settings.NewPresetName = "TestPreset";
        var saveAsButton = GUITestHelpers.GetAllControls<Button>(tab)
            .First(b => b.Content?.ToString() == "Save As");
        saveAsButton.Command?.Execute(null);
        sw1.UpdateLayout();
        sw1.Settings.Persist();
        Assert.Single(SettingsService.Load().Presets, p => p.Name == "TestPreset");
        sw1.Close();
        var sw2 = new SettingsWindow();
        Assert.Single(sw2.Settings.Presets, p => p.Name == "TestPreset");
    }
}
