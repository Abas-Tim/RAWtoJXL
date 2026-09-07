using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using RAWtoJXL.Avalonia;
using RAWtoJXL.Avalonia.Controls;
using RAWtoJXL.Avalonia.ViewModels;
using RAWtoJXL.Core.Interfaces;
using RAWtoJXL.Core.Settings;

namespace RAWtoJXL.Tests.GUITests;

[Trait("category", "gui")]
[Collection("Settings")]
public class SettingsPersistenceTests
{
    [AvaloniaFact]
    public void SettingsPanel_QualityPreset_PersistsAcrossReopens()
    {
        using var _ = new GUITestHelpers.SettingsScope();
        var panel1 = new SettingsPanelView();
        var tab = GUITestHelpers.SelectTab(panel1, "Conversion");
        var slider = GUITestHelpers.GetAllControls<Slider>(tab).First();
        slider.Value = 75;
        panel1.UpdateLayout();
        panel1.Settings.Persist();
        Assert.Equal(75, SettingsService.Load().QualityPreset);
        panel1.Settings.Dispose();
        var panel2 = new SettingsPanelView();
        Assert.Equal(75, panel2.Settings.QualityPreset);
    }

    [AvaloniaFact]
    public void SettingsPanel_OutputFormat_PersistsAcrossReopens()
    {
        using var _ = new GUITestHelpers.SettingsScope();
        var panel1 = new SettingsPanelView();
        var tab = GUITestHelpers.SelectTab(panel1, "Conversion");
        var combo = GUITestHelpers.GetAllControls<ComboBox>(tab)
            .First(c => c.Items.OfType<OutputFormat>().Any());
        combo.SelectedItem = OutputFormat.Jpeg;
        panel1.UpdateLayout();
        panel1.Settings.Persist();
        Assert.Equal(OutputFormat.Jpeg, SettingsService.Load().OutputFormat);
        panel1.Settings.Dispose();
        var panel2 = new SettingsPanelView();
        Assert.Equal(OutputFormat.Jpeg, panel2.Settings.OutputFormat);
    }

    [AvaloniaFact]
    public void SettingsPanel_CjxlEffort_PersistsAcrossReopens()
    {
        using var _ = new GUITestHelpers.SettingsScope();
        var panel1 = new SettingsPanelView();
        var tab = GUITestHelpers.SelectTab(panel1, "Conversion");
        var effortCombo = GUITestHelpers.GetAllControls<ComboBox>(tab)
            .First(c => c.Items.OfType<SettingsViewModel.EffortOption>().Any());
        var option7 = panel1.Settings.CjxlEffortOptions.First(e => e.Value == 7);
        effortCombo.SelectedItem = option7;
        panel1.UpdateLayout();
        panel1.Settings.Persist();
        Assert.Equal(7, SettingsService.Load().CjxlEffort);
        panel1.Settings.Dispose();
        var panel2 = new SettingsPanelView();
        Assert.Equal(7, panel2.Settings.CjxlEffort);
    }

    [AvaloniaFact]
    public void SettingsPanel_SkipMetadata_PersistsAcrossReopens()
    {
        using var _ = new GUITestHelpers.SettingsScope();
        var panel1 = new SettingsPanelView();
        var tab = GUITestHelpers.SelectTab(panel1, "Conversion");
        var checkBox = GUITestHelpers.GetAllControls<CheckBox>(tab).First();
        checkBox.IsChecked = true;
        panel1.UpdateLayout();
        panel1.Settings.Persist();
        Assert.True(SettingsService.Load().SkipMetadata);
        panel1.Settings.Dispose();
        var panel2 = new SettingsPanelView();
        Assert.True(panel2.Settings.SkipMetadata);
    }

    [AvaloniaFact]
    public void SettingsPanel_UseSubfolder_PersistsAcrossReopens()
    {
        using var _ = new GUITestHelpers.SettingsScope();
        var panel1 = new SettingsPanelView();
        var tab = GUITestHelpers.SelectTab(panel1, "Output");
        var checkBoxes = GUITestHelpers.GetAllControls<CheckBox>(tab).ToList();
        checkBoxes.First().IsChecked = false;
        panel1.UpdateLayout();
        panel1.Settings.Persist();
        Assert.False(SettingsService.Load().UseSubfolder);
        panel1.Settings.Dispose();
        var panel2 = new SettingsPanelView();
        Assert.False(panel2.Settings.UseSubfolder);
    }

    [AvaloniaFact]
    public void SettingsPanel_SubfolderName_PersistsAcrossReopens()
    {
        using var _ = new GUITestHelpers.SettingsScope();
        var panel1 = new SettingsPanelView();
        panel1.Settings.SubfolderName = "my_jxl_output";
        panel1.Settings.Persist();
        Assert.Equal("my_jxl_output", SettingsService.Load().SubfolderName);
        panel1.Settings.Dispose();
        var panel2 = new SettingsPanelView();
        Assert.Equal("my_jxl_output", panel2.Settings.SubfolderName);
    }

    [AvaloniaFact]
    public void SettingsPanel_SearchRecursive_PersistsAcrossReopens()
    {
        using var _ = new GUITestHelpers.SettingsScope();
        var panel1 = new SettingsPanelView();
        var tab = GUITestHelpers.SelectTab(panel1, "Output");
        var checkBoxes = GUITestHelpers.GetAllControls<CheckBox>(tab).ToList();
        checkBoxes.Last().IsChecked = true;
        panel1.UpdateLayout();
        panel1.Settings.Persist();
        Assert.True(SettingsService.Load().SearchRecursive);
        panel1.Settings.Dispose();
        var panel2 = new SettingsPanelView();
        Assert.True(panel2.Settings.SearchRecursive);
    }

    [AvaloniaFact]
    public void SettingsPanel_ConflictResolution_PersistsAcrossReopens()
    {
        using var _ = new GUITestHelpers.SettingsScope();
        var panel1 = new SettingsPanelView();
        var tab = GUITestHelpers.SelectTab(panel1, "Behavior");
        var combo = GUITestHelpers.GetAllControls<ComboBox>(tab).First();
        combo.SelectedItem = ConflictResolution.Skip;
        panel1.UpdateLayout();
        panel1.Settings.Persist();
        Assert.Equal(ConflictResolution.Skip, SettingsService.Load().ConflictResolution);
        panel1.Settings.Dispose();
        var panel2 = new SettingsPanelView();
        Assert.Equal(ConflictResolution.Skip, panel2.Settings.ConflictResolution);
    }

    [AvaloniaFact]
    public void SettingsPanel_ConfirmOverwrite_PersistsAcrossReopens()
    {
        using var _ = new GUITestHelpers.SettingsScope();
        var panel1 = new SettingsPanelView();
        var tab = GUITestHelpers.SelectTab(panel1, "Behavior");
        var checkBox = GUITestHelpers.GetAllControls<CheckBox>(tab).First();
        checkBox.IsChecked = false;
        panel1.UpdateLayout();
        panel1.Settings.Persist();
        Assert.False(SettingsService.Load().ConfirmOverwrite);
        panel1.Settings.Dispose();
        var panel2 = new SettingsPanelView();
        Assert.False(panel2.Settings.ConfirmOverwrite);
    }

    [AvaloniaFact]
    public void SettingsPanel_Preset_SavesAndPersists()
    {
        using var _ = new GUITestHelpers.SettingsScope();
        var panel1 = new SettingsPanelView();
        var tab = GUITestHelpers.SelectTab(panel1, "Presets");
        panel1.Settings.NewPresetName = "TestPreset";
        var saveAsButton = GUITestHelpers.GetAllControls<Button>(tab)
            .First(b => b.Content?.ToString() == "Save As");
        saveAsButton.Command?.Execute(null);
        panel1.UpdateLayout();
        panel1.Settings.Persist();
        Assert.Single(SettingsService.Load().Presets, p => p.Name == "TestPreset");
        panel1.Settings.Dispose();
        var panel2 = new SettingsPanelView();
        Assert.Single(panel2.Settings.Presets, p => p.Name == "TestPreset");
    }
}
