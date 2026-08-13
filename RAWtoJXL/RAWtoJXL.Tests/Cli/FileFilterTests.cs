using System.IO;
using RAWtoJXL.Cli;
using RAWtoJXL.Core.Interfaces;
using RAWtoJXL.Core.Settings;

namespace RAWtoJXL.Tests.Cli;

public class FileFilterTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "rawtojxl_filter_" + Guid.NewGuid().ToString("N"));

    public FileFilterTests()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "IMG0001.ARW"), "x");
        File.WriteAllText(Path.Combine(_dir, "IMG0002.ARW"), "x");
        File.WriteAllText(Path.Combine(_dir, "IMG0003.arw"), "x");
        File.WriteAllText(Path.Combine(_dir, "PANO_0001.ARW"), "x");
        File.WriteAllText(Path.Combine(_dir, "photo.dng"), "x");
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private IReadOnlyList<string> Files() =>
        Directory.GetFiles(_dir).OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();

    private static ResolvedOptions Options(
        IReadOnlyList<string>? include = null,
        IReadOnlyList<string>? exclude = null,
        DateTime? modifiedAfter = null,
        DateTime? modifiedBefore = null) => new(
        Format: OutputFormat.Jxl,
        Quality: 90,
        Conflict: ConflictResolution.Overwrite,
        UseSubfolder: false,
        SubfolderName: "jxl_output",
        UseCustomOutputDirectory: false,
        CustomOutputDirectory: "",
        SkipMetadata: false,
        Effort: null,
        Threads: null,
        Recursive: false,
        Extensions: RAWtoJXL.Core.Models.SupportedFormats.RawExtensions,
        Include: include ?? Array.Empty<string>(),
        Exclude: exclude ?? Array.Empty<string>(),
        ModifiedAfter: modifiedAfter,
        ModifiedBefore: modifiedBefore);

    [Fact]
    public void Apply_NoFilters_ReturnsAll()
    {
        var result = FileFilter.Apply(Files(), Options());

        Assert.Equal(5, result.Count);
    }

    [Fact]
    public void Apply_IncludeGlob_KeepsOnlyMatching()
    {
        var result = FileFilter.Apply(Files(), Options(include: new[] { "IMG*.ARW" }));

        Assert.Equal(new[] { "IMG0001.ARW", "IMG0002.ARW", "IMG0003.arw" },
            result.Select(Path.GetFileName).ToArray());
    }

    [Fact]
    public void Apply_IncludeGlob_IsCaseInsensitive()
    {
        var result = FileFilter.Apply(Files(), Options(include: new[] { "img0003.arw" }));

        Assert.Single(result);
        Assert.EndsWith("IMG0003.arw", result[0]);
    }

    [Fact]
    public void Apply_MultipleIncludes_AreOrCombined()
    {
        var result = FileFilter.Apply(Files(), Options(include: new[] { "IMG0001*", "PANO*" }));

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Apply_ExcludeGlob_RemovesMatching()
    {
        var result = FileFilter.Apply(Files(), Options(exclude: new[] { "IMG*" }));

        Assert.Equal(new[] { "PANO_0001.ARW", "photo.dng" },
            result.Select(Path.GetFileName).ToArray());
    }

    [Fact]
    public void Apply_IncludeAndExclude_ExcludeWins()
    {
        var result = FileFilter.Apply(Files(), Options(include: new[] { "IMG*" }, exclude: new[] { "IMG0002*" }));

        Assert.Equal(new[] { "IMG0001.ARW", "IMG0003.arw" },
            result.Select(Path.GetFileName).ToArray());
    }

    [Fact]
    public void Apply_QuestionMark_SingleCharacterWildcard()
    {
        var result = FileFilter.Apply(Files(), Options(include: new[] { "IMG????.ARW" }));

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void Apply_ModifiedAfter_FiltersByLastWriteTime()
    {
        var old = Path.Combine(_dir, "IMG0001.ARW");
        var recent = Path.Combine(_dir, "IMG0002.ARW");
        File.SetLastWriteTime(old, new DateTime(2020, 1, 1));
        File.SetLastWriteTime(recent, new DateTime(2025, 6, 1));

        var result = FileFilter.Apply(Files(), Options(modifiedAfter: new DateTime(2024, 1, 1)));

        Assert.Contains(recent, result);
        Assert.DoesNotContain(old, result);
    }

    [Fact]
    public void Apply_ModifiedBefore_FiltersByLastWriteTime()
    {
        var old = Path.Combine(_dir, "IMG0001.ARW");
        var recent = Path.Combine(_dir, "IMG0002.ARW");
        File.SetLastWriteTime(old, new DateTime(2020, 1, 1));
        File.SetLastWriteTime(recent, new DateTime(2025, 6, 1));

        var result = FileFilter.Apply(Files(), Options(modifiedBefore: new DateTime(2024, 1, 1)));

        Assert.Contains(old, result);
        Assert.DoesNotContain(recent, result);
    }

    [Fact]
    public void Apply_ModifiedBetween_Window()
    {
        var a = Path.Combine(_dir, "IMG0001.ARW");
        var b = Path.Combine(_dir, "IMG0002.ARW");
        var c = Path.Combine(_dir, "IMG0003.arw");
        File.SetLastWriteTime(a, new DateTime(2025, 1, 1));
        File.SetLastWriteTime(b, new DateTime(2025, 5, 1));
        File.SetLastWriteTime(c, new DateTime(2025, 12, 1));

        var result = FileFilter.Apply(Files(), Options(
            modifiedAfter: new DateTime(2025, 2, 1),
            modifiedBefore: new DateTime(2025, 10, 1)));

        Assert.Single(result);
        Assert.Equal(b, result[0]);
    }

    [Theory]
    [InlineData("*.arw", "IMG0001.ARW", true)]
    [InlineData("*.arw", "photo.dng", false)]
    [InlineData("img*", "IMG0002.ARW", true)]
    [InlineData("pano_*", "PANO_0001.ARW", true)]
    [InlineData("photo.?ng", "photo.dng", true)]
    [InlineData("photo", "photo.dng", false)]
    public void GlobToRegex_MatchesExpected(string glob, string name, bool expected)
    {
        var regex = FileFilter.GlobToRegex(glob);
        Assert.Equal(expected, regex.IsMatch(name));
    }
}
