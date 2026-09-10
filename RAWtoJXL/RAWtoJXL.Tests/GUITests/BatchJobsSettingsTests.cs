using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using RAWtoJXL.Avalonia;
using RAWtoJXL.Avalonia.Controls;
using RAWtoJXL.Avalonia.ViewModels;
using RAWtoJXL.Core.Settings;

namespace RAWtoJXL.Tests.GUITests;

[Trait("category", "gui")]
[Collection("Settings")]
public sealed class BatchJobsSettingsTests
{
    [AvaloniaFact]
    public void SettingsPanel_BatchJobs_PersistsAcrossReopens()
    {
        using var _ = new GUITestHelpers.SettingsScope();
        var panel = new SettingsPanelView();
        var tab = GUITestHelpers.SelectTab(panel, "Hardware");
        var combo = GUITestHelpers.GetAllControls<ComboBox>(tab)
            .First(control => control.Items.OfType<SettingsViewModel.JobOption>().Any());

        combo.SelectedItem = panel.Settings.BatchJobsOptions.First(option => option.Value == 3);
        panel.UpdateLayout();
        panel.Settings.Persist();

        Assert.Equal(3, SettingsService.Load().BatchJobs);
        panel.Settings.Dispose();
        var reopened = new SettingsPanelView();
        Assert.Equal(3, reopened.Settings.BatchJobs);
        Assert.Equal(3, reopened.Settings.SelectedBatchJobsOption!.Value);
    }

    [AvaloniaFact]
    public void SettingsPanel_BatchJobs_OffersAutoAndOneThroughFour()
    {
        using var _ = new GUITestHelpers.SettingsScope();
        var panel = new SettingsPanelView();

        var options = panel.Settings.BatchJobsOptions;

        Assert.Equal(new[] { -1, 1, 2, 3, 4 }, options.Select(option => option.Value));
        Assert.Equal("Auto", options[0].Display);
    }

    [AvaloniaFact]
    public void SettingsWithoutBatchJobs_UsesBackwardCompatibleAutoValue()
    {
        using var _ = new GUITestHelpers.SettingsScope();
        File.WriteAllText(
            Path.Combine(SettingsService.SettingsDirectory, "settings.json"),
            "{\"qualityPreset\":90}");
        SettingsService.Reset();

        var panel = new SettingsPanelView();

        Assert.Equal(-1, panel.Settings.BatchJobs);
        Assert.Equal(-1, panel.Settings.SelectedBatchJobsOption!.Value);
    }
}
