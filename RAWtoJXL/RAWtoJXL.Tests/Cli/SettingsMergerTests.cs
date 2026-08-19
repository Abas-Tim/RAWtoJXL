using RAWtoJXL.Cli;
using RAWtoJXL.Cli.Options;
using RAWtoJXL.Core.Interfaces;
using RAWtoJXL.Core.Settings;

namespace RAWtoJXL.Tests.Cli;

public class SettingsMergerTests
{
    private static AppSettings DefaultSettings() => new()
    {
        QualityPreset = 90,
        OutputFormat = OutputFormat.Jxl,
        ConflictResolution = ConflictResolution.Overwrite,
        UseSubfolder = true,
        SubfolderName = "jxl_output",
        SearchRecursive = false,
        UseCustomOutputDirectory = false,
        CustomOutputDirectory = "",
        SkipMetadata = false,
        CjxlEffort = 7,
        CjxlThreads = -1
    };

    private static CliOptions DefaultCli() => new() { Paths = new[] { "x" } };

    [Fact]
    public void Merge_NoCliNoPreset_UsesSettingsDefaults()
    {
        var resolved = SettingsMerger.Merge(DefaultCli(), DefaultSettings(), null);

        Assert.Equal(90, resolved.Quality);
        Assert.Equal(OutputFormat.Jxl, resolved.Format);
        Assert.Equal(ConflictResolution.Overwrite, resolved.Conflict);
        Assert.True(resolved.UseSubfolder);
        Assert.Equal("jxl_output", resolved.SubfolderName);
        Assert.False(resolved.SkipMetadata);
        Assert.Equal(7, resolved.Effort);
        Assert.Null(resolved.Threads);
        Assert.False(resolved.Recursive);
    }

    [Fact]
    public void Merge_CliFlags_OverrideSettings()
    {
        var cli = DefaultCli();
        cli.Quality = 100;
        cli.Format = "Avif";
        cli.Conflict = "Skip";
        cli.NoSubfolder = true;
        cli.SkipMetadata = true;
        cli.Effort = 3;
        cli.Threads = 4;
        cli.Recursive = true;

        var resolved = SettingsMerger.Merge(cli, DefaultSettings(), null);

        Assert.Equal(100, resolved.Quality);
        Assert.Equal(OutputFormat.Avif, resolved.Format);
        Assert.Equal(ConflictResolution.Skip, resolved.Conflict);
        Assert.False(resolved.UseSubfolder);
        Assert.True(resolved.SkipMetadata);
        Assert.Equal(3, resolved.Effort);
        Assert.Equal(4, resolved.Threads);
        Assert.True(resolved.Recursive);
    }

    [Fact]
    public void Merge_Preset_WinsOverSettings()
    {
        var preset = new ConversionPreset
        {
            Name = "hi",
            Quality = 100,
            OutputFormat = OutputFormat.Jpeg,
            ConflictResolution = ConflictResolution.AppendNumber,
            UseSubfolder = false,
            SkipMetadata = true,
            CjxlEffort = 9,
            CjxlThreads = 6
        };

        var resolved = SettingsMerger.Merge(DefaultCli(), DefaultSettings(), preset);

        Assert.Equal(100, resolved.Quality);
        Assert.Equal(OutputFormat.Jpeg, resolved.Format);
        Assert.Equal(ConflictResolution.AppendNumber, resolved.Conflict);
        Assert.False(resolved.UseSubfolder);
        Assert.True(resolved.SkipMetadata);
        Assert.Equal(9, resolved.Effort);
        Assert.Equal(6, resolved.Threads);
    }

    [Fact]
    public void Merge_CliFlags_WinOverPreset()
    {
        var preset = new ConversionPreset { Name = "p", Quality = 100, OutputFormat = OutputFormat.Jpeg, CjxlEffort = 9 };
        var cli = DefaultCli();
        cli.Quality = 50;
        cli.Format = "Jxl";
        cli.Effort = 1;

        var resolved = SettingsMerger.Merge(cli, DefaultSettings(), preset);

        Assert.Equal(50, resolved.Quality);
        Assert.Equal(OutputFormat.Jxl, resolved.Format);
        Assert.Equal(1, resolved.Effort);
    }

    [Fact]
    public void Merge_PresetSubfolder_WinsOverSettingsSubfolder()
    {
        var preset = new ConversionPreset { Name = "p", UseSubfolder = true, SubfolderName = "custom_out" };
        var resolved = SettingsMerger.Merge(DefaultCli(), DefaultSettings(), preset);

        Assert.True(resolved.UseSubfolder);
        Assert.Equal("custom_out", resolved.SubfolderName);
    }

    [Fact]
    public void Merge_CliSubfolder_WinsOverPreset()
    {
        var preset = new ConversionPreset { Name = "p", SubfolderName = "preset_out" };
        var cli = DefaultCli();
        cli.Subfolder = "cli_out";

        var resolved = SettingsMerger.Merge(cli, DefaultSettings(), preset);

        Assert.Equal("cli_out", resolved.SubfolderName);
    }

    [Fact]
    public void Merge_CliSubfolder_ImpliesUseSubfolderEvenWhenSettingsDisabled()
    {
        var settings = DefaultSettings();
        settings.UseSubfolder = false;
        var cli = DefaultCli();
        cli.Subfolder = "cli_out";

        var resolved = SettingsMerger.Merge(cli, settings, null);

        Assert.True(resolved.UseSubfolder);
        Assert.Equal("cli_out", resolved.SubfolderName);
    }

    [Fact]
    public void Merge_NoSubfolder_OverridesCliSubfolder()
    {
        var cli = DefaultCli();
        cli.NoSubfolder = true;
        cli.Subfolder = "ignored";

        var resolved = SettingsMerger.Merge(cli, DefaultSettings(), null);

        Assert.False(resolved.UseSubfolder);
    }

    [Fact]
    public void ParseDate_ReturnsLocalKind()
    {
        var date = SettingsMerger.ParseDate("2025-11-15");

        Assert.NotNull(date);
        Assert.Equal(DateTimeKind.Local, date!.Value.Kind);
    }

    [Fact]
    public void Merge_OutputDirectory_UsesSettingsCustomDirectory()
    {
        var settings = DefaultSettings();
        settings.UseCustomOutputDirectory = true;
        settings.CustomOutputDirectory = @"C:\out";

        var resolved = SettingsMerger.Merge(DefaultCli(), settings, null);

        Assert.True(resolved.UseCustomOutputDirectory);
        Assert.Equal(@"C:\out", resolved.CustomOutputDirectory);
    }

    [Fact]
    public void Merge_OutputDirectory_CliWins()
    {
        var settings = DefaultSettings();
        settings.UseCustomOutputDirectory = true;
        settings.CustomOutputDirectory = @"C:\out";
        var cli = DefaultCli();
        cli.OutputDirectory = @"D:\other";

        var resolved = SettingsMerger.Merge(cli, settings, null);

        Assert.Equal(@"D:\other", resolved.CustomOutputDirectory);
    }

    [Fact]
    public void Merge_PresetOutputDirectory_UsedWhenEnabled()
    {
        var preset = new ConversionPreset { Name = "p", UseCustomOutputDirectory = true, CustomOutputDirectory = @"E:\preset" };
        var resolved = SettingsMerger.Merge(DefaultCli(), DefaultSettings(), preset);

        Assert.True(resolved.UseCustomOutputDirectory);
        Assert.Equal(@"E:\preset", resolved.CustomOutputDirectory);
    }

    [Fact]
    public void Merge_UnsetEffortInSettings_FallsBackToNull()
    {
        var settings = DefaultSettings();
        settings.CjxlEffort = -1;

        var resolved = SettingsMerger.Merge(DefaultCli(), settings, null);

        Assert.Null(resolved.Effort);
    }

    [Fact]
    public void Merge_UnsetThreadsInSettings_FallsBackToNull()
    {
        var settings = DefaultSettings();
        settings.CjxlThreads = 0;

        var resolved = SettingsMerger.Merge(DefaultCli(), settings, null);

        Assert.Null(resolved.Threads);
    }

    [Fact]
    public void Merge_CliExtensions_WinOverDefaults()
    {
        var cli = DefaultCli();
        cli.Extensions = new[] { ".dng" };

        var resolved = SettingsMerger.Merge(cli, DefaultSettings(), null);

        Assert.Equal(new[] { ".dng" }, resolved.Extensions);
    }

    [Fact]
    public void Merge_NoExtensions_UsesAllInputExtensions()
    {
        var resolved = SettingsMerger.Merge(DefaultCli(), DefaultSettings(), null);

        Assert.Equal(RAWtoJXL.Core.Models.SupportedFormats.AllInputExtensions.Length, resolved.Extensions.Count);
    }

    [Theory]
    [InlineData("Jxl", OutputFormat.Jxl)]
    [InlineData("Jpeg", OutputFormat.Jpeg)]
    [InlineData("Avif", OutputFormat.Avif)]
    [InlineData("jxl", OutputFormat.Jxl)]
    [InlineData("avif", OutputFormat.Avif)]
    [InlineData("png", null)]
    [InlineData("garbage", null)]
    [InlineData(null, null)]
    public void ParseFormat_MapsValues(string? value, OutputFormat? expected)
    {
        Assert.Equal(expected, SettingsMerger.ParseFormat(value));
    }

    [Theory]
    [InlineData("Overwrite", ConflictResolution.Overwrite)]
    [InlineData("Skip", ConflictResolution.Skip)]
    [InlineData("AppendNumber", ConflictResolution.AppendNumber)]
    [InlineData("appendnumber", ConflictResolution.AppendNumber)]
    [InlineData("bogus", null)]
    [InlineData(null, null)]
    public void ParseConflict_MapsValues(string? value, ConflictResolution? expected)
    {
        Assert.Equal(expected, SettingsMerger.ParseConflict(value));
    }
}
