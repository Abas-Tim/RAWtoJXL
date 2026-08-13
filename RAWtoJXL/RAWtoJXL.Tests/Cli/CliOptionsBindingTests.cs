using System.CommandLine;
using System.CommandLine.Parsing;
using RAWtoJXL.Cli;
using RAWtoJXL.Cli.Options;

namespace RAWtoJXL.Tests.Cli;

public class CliOptionsBindingTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "rawtojxl_bind_" + Guid.NewGuid().ToString("N"));
    private readonly string _rawFile;

    public CliOptionsBindingTests()
    {
        Directory.CreateDirectory(_tempDir);
        _rawFile = Path.Combine(_tempDir, "photo.ARW");
        File.WriteAllText(_rawFile, "x");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    private static (CommandOptions Options, ParseResult Parse) ParseArgs(params string[] args)
    {
        var options = new CommandOptions();
        var command = new Command("convert");
        command.Add(options.Paths);
        CliCommandFactory.AddOptions(command, options);
        var parse = command.Parse(args, new ParserConfiguration());
        return (options, parse);
    }

    private CliOptions Bind(params string[] args)
    {
        var (options, parse) = ParseArgs(args);
        return CliOptionsBinding.Bind(parse, options);
    }

    [Fact]
    public void Bind_Paths()
    {
        var cli = Bind(_rawFile);

        Assert.Equal(new[] { _rawFile }, cli.Paths);
    }

    [Fact]
    public void Bind_MultiplePaths()
    {
        var cli = Bind(_rawFile, _tempDir);

        Assert.Equal(new[] { _rawFile, _tempDir }, cli.Paths);
    }

    [Fact]
    public void Bind_AllDefaults_WhenNoFlags()
    {
        var cli = Bind(_tempDir);

        Assert.Null(cli.Format);
        Assert.Equal(-1, cli.Quality);
        Assert.Null(cli.Conflict);
        Assert.False(cli.Recursive);
        Assert.Null(cli.OutputDirectory);
        Assert.False(cli.NoSubfolder);
        Assert.Null(cli.Subfolder);
        Assert.Null(cli.Preset);
        Assert.Empty(cli.Extensions);
        Assert.Empty(cli.Include);
        Assert.Empty(cli.Exclude);
        Assert.Null(cli.ModifiedAfter);
        Assert.Null(cli.ModifiedBefore);
        Assert.False(cli.SkipMetadata);
        Assert.Equal(-1, cli.Effort);
        Assert.Equal(-1, cli.Threads);
        Assert.Equal(-1, cli.Jobs);
        Assert.False(cli.DryRun);
        Assert.False(cli.Json);
        Assert.False(cli.Quiet);
        Assert.False(cli.Verbose);
    }

    [Theory]
    [InlineData("jxl", "Jxl")]
    [InlineData("JXL", "Jxl")]
    [InlineData("jpg", "Jpeg")]
    [InlineData("jpeg", "Jpeg")]
    [InlineData("png", "Png")]
    public void Bind_Format_Normalized(string input, string expected)
    {
        var cli = Bind(_tempDir, "--format", input);

        Assert.Equal(expected, cli.Format);
    }

    [Fact]
    public void Bind_Format_Invalid_ThrowsUsage()
    {
        var ex = Assert.Throws<UsageException>(() => Bind(_tempDir, "--format", "webp"));
        Assert.Contains("--format", ex.Message);
    }

    [Theory]
    [InlineData("overwrite", "Overwrite")]
    [InlineData("skip", "Skip")]
    [InlineData("rename", "AppendNumber")]
    [InlineData("append", "AppendNumber")]
    public void Bind_Conflict_Normalized(string input, string expected)
    {
        var cli = Bind(_tempDir, "--conflict", input);

        Assert.Equal(expected, cli.Conflict);
    }

    [Fact]
    public void Bind_Conflict_Invalid_ThrowsUsage()
    {
        Assert.Throws<UsageException>(() => Bind(_tempDir, "--conflict", "merge"));
    }

    [Fact]
    public void Bind_Quality()
    {
        var cli = Bind(_tempDir, "--quality", "100");

        Assert.Equal(100, cli.Quality);
    }

    [Fact]
    public void Bind_Quality_OutOfRange_ThrowsUsage()
    {
        var ex = Assert.Throws<UsageException>(() => Bind(_tempDir, "--quality", "101"));
        Assert.Contains("0-100", ex.Message);
        Assert.Throws<UsageException>(() => Bind(_tempDir, "--quality", "-5"));
    }

    [Fact]
    public void Bind_Quality_NonNumeric_IsParseError()
    {
        var (_, parse) = ParseArgs(_tempDir, "--quality", "abc");
        Assert.NotEmpty(parse.Errors);
    }

    [Fact]
    public void Bind_Effort()
    {
        Assert.Equal(9, Bind(_tempDir, "--effort", "9").Effort);
        Assert.Throws<UsageException>(() => Bind(_tempDir, "--effort", "0"));
        Assert.Throws<UsageException>(() => Bind(_tempDir, "--effort", "10"));
    }

    [Fact]
    public void Bind_Threads()
    {
        Assert.Equal(8, Bind(_tempDir, "--threads", "8").Threads);
        Assert.Throws<UsageException>(() => Bind(_tempDir, "--threads", "0"));
    }

    [Fact]
    public void Bind_Jobs()
    {
        Assert.Equal(4, Bind(_tempDir, "--jobs", "4").Jobs);
        Assert.Equal(4, Bind(_tempDir, "-j", "4").Jobs);
        Assert.Throws<UsageException>(() => Bind(_tempDir, "--jobs", "0"));
    }

    [Fact]
    public void Bind_BoolFlags()
    {
        var cli = Bind(_tempDir, "-r", "--no-subfolder", "--skip-metadata", "--dry-run", "--json", "--quiet", "--verbose");

        Assert.True(cli.Recursive);
        Assert.True(cli.NoSubfolder);
        Assert.True(cli.SkipMetadata);
        Assert.True(cli.DryRun);
        Assert.True(cli.Json);
        Assert.True(cli.Quiet);
        Assert.True(cli.Verbose);
    }

    [Fact]
    public void Bind_OutputDirectoryAndSubfolder()
    {
        var cli = Bind(_tempDir, "-o", @"C:\out", "--subfolder", "converted");

        Assert.Equal(@"C:\out", cli.OutputDirectory);
        Assert.Equal("converted", cli.Subfolder);
    }

    [Fact]
    public void Bind_Preset()
    {
        Assert.Equal("MyPreset", Bind(_tempDir, "-p", "MyPreset").Preset);
    }

    [Fact]
    public void Bind_Extensions_CommaSeparated()
    {
        var cli = Bind(_tempDir, "--ext", "arw,cr3");

        Assert.Equal(new[] { ".arw", ".cr3" }, cli.Extensions);
    }

    [Fact]
    public void Bind_Extensions_SemicolonAndDotPrefix()
    {
        var cli = Bind(_tempDir, "--ext", ".NEF;.dng");

        Assert.Equal(new[] { ".nef", ".dng" }, cli.Extensions);
    }

    [Fact]
    public void Bind_Extensions_Unsupported_ThrowsUsage()
    {
        var ex = Assert.Throws<UsageException>(() => Bind(_tempDir, "--ext", "arw,xyz"));
        Assert.Contains("xyz", ex.Message);
    }

    [Fact]
    public void Bind_Include_Repeatable()
    {
        var cli = Bind(_tempDir, "--include", "*.arw", "--include", "*.dng");

        Assert.Equal(new[] { "*.arw", "*.dng" }, cli.Include);
    }

    [Fact]
    public void Bind_Exclude_CommaSplit()
    {
        var cli = Bind(_tempDir, "--exclude", "*.jpg,*.png");

        Assert.Equal(new[] { "*.jpg", "*.png" }, cli.Exclude);
    }

    [Fact]
    public void Bind_ModifiedDates_ValidIso()
    {
        var cli = Bind(_tempDir, "--modified-after", "2025-11-15", "--modified-before", "2025-12-31");

        Assert.StartsWith("2025-11-15", cli.ModifiedAfter);
        Assert.StartsWith("2025-12-31", cli.ModifiedBefore);
    }

    [Fact]
    public void Bind_ModifiedDate_Invalid_ThrowsUsage()
    {
        var ex = Assert.Throws<UsageException>(() => Bind(_tempDir, "--modified-after", "not-a-date"));
        Assert.Contains("--modified-after", ex.Message);
    }

    [Fact]
    public void Bind_UnknownOption_ThrowsUsage()
    {
        var ex = Assert.Throws<UsageException>(() => Bind("--nope", _tempDir));
        Assert.Contains("--nope", ex.Message);
    }

    [Fact]
    public void Bind_MissingPath_ThrowsUsage()
    {
        var missing = Path.Combine(_tempDir, "missing");
        var ex = Assert.Throws<UsageException>(() => Bind(missing));
        Assert.Contains("path does not exist", ex.Message);
    }

    [Fact]
    public void Bind_NoPaths_IsParseError()
    {
        var (_, parse) = ParseArgs();
        Assert.NotEmpty(parse.Errors);
    }
}
