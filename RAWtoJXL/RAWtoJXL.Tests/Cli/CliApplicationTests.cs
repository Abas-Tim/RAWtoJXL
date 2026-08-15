using System.Text.Json;
using RAWtoJXL.Cli;
using RAWtoJXL.Core.Settings;
using RAWtoJXL.Tests.GUITests;

namespace RAWtoJXL.Tests.Cli;

public class CliApplicationTests : Startup, IDisposable
{
    private readonly string _sandbox = Path.Combine(Path.GetTempPath(), "rawtojxl_cli_" + Guid.NewGuid().ToString("N"));

    public CliApplicationTests()
    {
        Directory.CreateDirectory(_sandbox);
        GUITestHelpers.NewSettingsSandbox();
    }

    public void Dispose()
    {
        if (Directory.Exists(_sandbox)) Directory.Delete(_sandbox, recursive: true);
    }

    private async Task<(int ExitCode, string Stdout, string Stderr)> RunAsync(params string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exitCode = await CliApplication.RunAsync(args, stdout, stderr, Services);
        return (exitCode, stdout.ToString(), stderr.ToString());
    }

    private string CopyFixture(string name = "photo.dng")
    {
        var target = Path.Combine(_sandbox, name);
        File.Copy(TestArwPath, target);
        return target;
    }

    private static JsonDocument ParseJson(string json) => JsonDocument.Parse(json);

    [Fact]
    public async Task List_Json_ReturnsPlan()
    {
        CopyFixture("a.dng");
        CopyFixture("b.dng");

        var (exit, stdout, _) = await RunAsync("list", _sandbox, "--no-subfolder", "--json");

        Assert.Equal(ExitCodes.Success, exit);
        using var doc = ParseJson(stdout);
        Assert.Equal("list", doc.RootElement.GetProperty("command").GetString());
        Assert.Equal(2, doc.RootElement.GetProperty("total").GetInt32());
        var files = doc.RootElement.GetProperty("files").EnumerateArray().ToList();
        Assert.Equal(2, files.Count);
        Assert.All(files, f => Assert.Equal("planned", f.GetProperty("status").GetString()));
        Assert.All(files, f => Assert.EndsWith(".jxl", f.GetProperty("output").GetString()!));
    }

    [Fact]
    public async Task List_Text_PrintsMappings()
    {
        CopyFixture("a.dng");

        var (exit, stdout, stderr) = await RunAsync("list", _sandbox, "--no-subfolder");

        Assert.Equal(ExitCodes.Success, exit);
        Assert.Contains("a.dng", stdout);
        Assert.Contains("->", stdout);
        Assert.Contains("1 file(s)", stderr);
    }

    [Fact]
    public async Task List_FilterByExtension()
    {
        CopyFixture("a.dng");
        CopyFixture("b.dng");

        var (exit, stdout, _) = await RunAsync("list", _sandbox, "--ext", "dng", "--json");

        Assert.Equal(ExitCodes.Success, exit);
        using var doc = ParseJson(stdout);
        Assert.Equal(2, doc.RootElement.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task List_IncludeFilter()
    {
        CopyFixture("a.dng");
        CopyFixture("b.dng");

        var (exit, stdout, _) = await RunAsync("list", _sandbox, "--include", "a.*", "--json");

        Assert.Equal(ExitCodes.Success, exit);
        using var doc = ParseJson(stdout);
        Assert.Equal(1, doc.RootElement.GetProperty("total").GetInt32());
        Assert.EndsWith("a.dng", doc.RootElement.GetProperty("files")[0].GetProperty("input").GetString());
    }

    [Fact]
    public async Task List_ExcludeFilter()
    {
        CopyFixture("a.dng");
        CopyFixture("b.dng");

        var (exit, stdout, _) = await RunAsync("list", _sandbox, "--exclude", "b.*", "--json");

        Assert.Equal(ExitCodes.Success, exit);
        using var doc = ParseJson(stdout);
        Assert.Equal(1, doc.RootElement.GetProperty("total").GetInt32());
        Assert.EndsWith("a.dng", doc.RootElement.GetProperty("files")[0].GetProperty("input").GetString());
    }

    [Fact]
    public async Task List_NoMatches_ExitNoFiles()
    {
        var (exit, _, stderr) = await RunAsync("list", _sandbox);

        Assert.Equal(ExitCodes.NoFiles, exit);
        Assert.Contains("no files found", stderr);
    }

    [Fact]
    public async Task List_UnknownOption_ExitUsage()
    {
        var (exit, _, stderr) = await RunAsync("list", _sandbox, "--bogus");

        Assert.Equal(ExitCodes.Usage, exit);
        Assert.Contains("unrecognized option", stderr);
    }

    [Fact]
    public async Task Convert_DryRun_CreatesNothing()
    {
        CopyFixture("a.dng");

        var (exit, stdout, _) = await RunAsync("convert", _sandbox, "--no-subfolder", "--dry-run", "--json");

        Assert.Equal(ExitCodes.Success, exit);
        using var doc = ParseJson(stdout);
        Assert.Equal("planned", doc.RootElement.GetProperty("files")[0].GetProperty("status").GetString());
        Assert.Empty(Directory.GetFiles(_sandbox, "*.jxl"));
    }

    [Fact]
    public async Task Convert_RealConversion_JsonSummary()
    {
        CopyFixture("a.dng");

        var (exit, stdout, _) = await RunAsync("convert", _sandbox, "--no-subfolder", "--json");

        Assert.Equal(ExitCodes.Success, exit);
        using var doc = ParseJson(stdout);
        Assert.Equal("convert", doc.RootElement.GetProperty("command").GetString());
        Assert.Equal(1, doc.RootElement.GetProperty("converted").GetInt32());
        Assert.Equal(0, doc.RootElement.GetProperty("failed").GetInt32());
        var file = doc.RootElement.GetProperty("files")[0];
        Assert.Equal("converted", file.GetProperty("status").GetString());
        Assert.True(file.GetProperty("outputBytes").GetInt64() > 0);
        Assert.True(File.Exists(file.GetProperty("output").GetString()));
    }

    [Fact]
    public async Task Convert_ConflictSkip_ExistingOutput_IsSkipped()
    {
        var source = CopyFixture("a.dng");
        var existing = Path.ChangeExtension(source, ".jxl");
        File.WriteAllText(existing, "pre-existing");
        var content = File.ReadAllBytes(existing);

        var (exit, stdout, _) = await RunAsync("convert", _sandbox, "--no-subfolder", "--conflict", "skip", "--json", "--ext", "dng");

        Assert.Equal(ExitCodes.Success, exit);
        using var doc = ParseJson(stdout);
        Assert.Equal(1, doc.RootElement.GetProperty("skipped").GetInt32());
        Assert.Equal(0, doc.RootElement.GetProperty("converted").GetInt32());
        Assert.Equal("skipped", doc.RootElement.GetProperty("files")[0].GetProperty("status").GetString());
        Assert.Equal(content, File.ReadAllBytes(existing));
    }

    [Fact]
    public async Task Convert_ConflictRename_CreatesNumberedOutput()
    {
        var source = CopyFixture("a.dng");
        File.WriteAllText(Path.ChangeExtension(source, ".jxl"), "pre-existing");

        var (exit, stdout, _) = await RunAsync("convert", _sandbox, "--no-subfolder", "--conflict", "rename", "--json", "--ext", "dng");

        Assert.Equal(ExitCodes.Success, exit);
        using var doc = ParseJson(stdout);
        Assert.Equal(1, doc.RootElement.GetProperty("converted").GetInt32());
        var output = doc.RootElement.GetProperty("files")[0].GetProperty("output").GetString();
        Assert.EndsWith("a_1.jxl", output);
        Assert.True(File.Exists(output));
    }

    [Fact]
    public async Task Convert_Parallel_TwoFiles()
    {
        CopyFixture("a.dng");
        CopyFixture("b.dng");

        var (exit, stdout, _) = await RunAsync("convert", _sandbox, "--no-subfolder", "--jobs", "2", "--json");

        Assert.Equal(ExitCodes.Success, exit);
        using var doc = ParseJson(stdout);
        Assert.Equal(2, doc.RootElement.GetProperty("converted").GetInt32());
        Assert.Equal(0, doc.RootElement.GetProperty("failed").GetInt32());
    }

    [Fact]
    public async Task Convert_Preset_AppliesOutputFormat()
    {
        var settings = new AppSettings();
        settings.Presets.Add(new ConversionPreset
        {
            Name = "jpeg90",
            Quality = 90,
            OutputFormat = RAWtoJXL.Core.Interfaces.OutputFormat.Jpeg,
            UseSubfolder = false,
            CjxlEffort = 7
        });
        SettingsService.Save(settings);
        CopyFixture("a.dng");

        var (exit, _, _) = await RunAsync("convert", _sandbox, "--preset", "jpeg90", "--json");

        Assert.Equal(ExitCodes.Success, exit);
        Assert.True(File.Exists(Path.Combine(_sandbox, "a.jpg")));
        Assert.Empty(Directory.GetFiles(_sandbox, "*.jxl"));
    }

    [Fact]
    public async Task Convert_Preset_Unknown_ExitUsage()
    {
        CopyFixture("a.dng");

        var (exit, _, stderr) = await RunAsync("convert", _sandbox, "--preset", "missing", "--no-subfolder");

        Assert.Equal(ExitCodes.Usage, exit);
        Assert.Contains("not found", stderr);
    }

    [Fact]
    public async Task Convert_MissingPath_ExitUsage()
    {
        var (exit, _, stderr) = await RunAsync("convert", Path.Combine(_sandbox, "missing"));

        Assert.Equal(ExitCodes.Usage, exit);
        Assert.Contains("path does not exist", stderr);
    }

    [Fact]
    public async Task Convert_QualityOutOfRange_ExitUsage()
    {
        CopyFixture("a.dng");

        var (exit, _, stderr) = await RunAsync("convert", _sandbox, "--quality", "150");

        Assert.Equal(ExitCodes.Usage, exit);
        Assert.Contains("0-100", stderr);
    }

    [Fact]
    public async Task Convert_NonNumericQuality_ExitUsage()
    {
        CopyFixture("a.dng");

        var (exit, _, stderr) = await RunAsync("convert", _sandbox, "--quality", "abc");

        Assert.Equal(ExitCodes.Usage, exit);
        Assert.Contains("error", stderr);
    }

    [Fact]
    public async Task NoArguments_PrintsUsage_ExitUsage()
    {
        var (exit, _, stderr) = await RunAsync();

        Assert.Equal(ExitCodes.Usage, exit);
        Assert.Contains("rawtojxl-cli convert", stderr);
    }

    [Fact]
    public async Task Presets_Text_ListsNames()
    {
        var settings = new AppSettings();
        settings.Presets.Add(new ConversionPreset { Name = "p1" });
        settings.Presets.Add(new ConversionPreset { Name = "p2" });
        SettingsService.Save(settings);

        var (exit, stdout, _) = await RunAsync("presets");

        Assert.Equal(ExitCodes.Success, exit);
        Assert.Contains("p1", stdout);
        Assert.Contains("p2", stdout);
    }

    [Fact]
    public async Task Presets_Json_ContainsDetails()
    {
        var settings = new AppSettings();
        settings.Presets.Add(new ConversionPreset
        {
            Name = "p1",
            Quality = 95,
            OutputFormat = RAWtoJXL.Core.Interfaces.OutputFormat.Avif,
            CjxlEffort = 9
        });
        SettingsService.Save(settings);

        var (exit, stdout, _) = await RunAsync("presets", "--json");

        Assert.Equal(ExitCodes.Success, exit);
        using var doc = ParseJson(stdout);
        var preset = doc.RootElement.GetProperty("presets")[0];
        Assert.Equal("p1", preset.GetProperty("name").GetString());
        Assert.Equal(95, preset.GetProperty("quality").GetInt32());
        Assert.Equal("Avif", preset.GetProperty("outputFormat").GetString());
        Assert.Equal(9, preset.GetProperty("cjxlEffort").GetInt32());
    }

    [Fact]
    public async Task Convert_Parallel_DuplicateOutputs_OneFailsWithError()
    {
        CopyFixture("a.dng");
        CopyFixture("a.CR3");

        var (exit, stdout, _) = await RunAsync("convert", _sandbox, "--no-subfolder", "--jobs", "2", "--json");

        Assert.Equal(ExitCodes.PartialFailure, exit);
        using var doc = ParseJson(stdout);
        Assert.Equal(1, doc.RootElement.GetProperty("converted").GetInt32());
        Assert.Equal(1, doc.RootElement.GetProperty("failed").GetInt32());
        var failed = doc.RootElement.GetProperty("files").EnumerateArray()
            .Single(f => f.GetProperty("status").GetString() == "failed");
        Assert.Contains("already used", failed.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Convert_JobsAboveSafeLimit_WarnsOnStderr()
    {
        CopyFixture("a.dng");
        var requested = ParallelismPolicy.SafeMaxJobs + 1;

        var (exit, _, stderr) = await RunAsync("convert", _sandbox, "--no-subfolder", "--jobs", requested.ToString(), "--json");

        Assert.Equal(ExitCodes.Success, exit);
        Assert.Contains("warning:", stderr);
        Assert.Contains("stable limit", stderr);
    }

    [Fact]
    public async Task Convert_JobsWithinSafeLimit_NoWarning()
    {
        CopyFixture("a.dng");

        var (exit, _, stderr) = await RunAsync("convert", _sandbox, "--no-subfolder", "--jobs", "1", "--json");

        Assert.Equal(ExitCodes.Success, exit);
        Assert.DoesNotContain("warning:", stderr);
    }

    [Fact]
    public async Task Convert_DefaultJobs_NoWarningOnAnyHost()
    {
        CopyFixture("a.dng");

        var (exit, _, stderr) = await RunAsync("convert", _sandbox, "--no-subfolder", "--json");

        Assert.Equal(ExitCodes.Success, exit);
        Assert.DoesNotContain("warning:", stderr);
    }

    [Fact]
    public async Task Convert_Quiet_SuppressesProgressButKeepsJson()
    {
        CopyFixture("a.dng");

        var (exit, stdout, stderr) = await RunAsync("convert", _sandbox, "--no-subfolder", "--jobs", "1", "--quiet", "--json");

        Assert.Equal(ExitCodes.Success, exit);
        Assert.StartsWith("{", stdout);
        Assert.Empty(stderr);
    }
}
