using System.IO;
using RAWtoJXL.Core.Models;
using RAWtoJXL.Core.Services;

namespace RAWtoJXL.Tests.Cli;

public class ImageFileEnumeratorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "rawtojxl_enumerator_" + Guid.NewGuid().ToString("N"));

    public ImageFileEnumeratorTests()
    {
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(Path.Combine(_root, "sub"));
        Directory.CreateDirectory(Path.Combine(_root, "sub", "deep"));

        File.WriteAllText(Path.Combine(_root, "a.ARW"), "x");
        File.WriteAllText(Path.Combine(_root, "b.arw"), "x");
        File.WriteAllText(Path.Combine(_root, "c.dng"), "x");
        File.WriteAllText(Path.Combine(_root, "d.jpg"), "x");
        File.WriteAllText(Path.Combine(_root, "e.txt"), "x");
        File.WriteAllText(Path.Combine(_root, "sub", "f.CR3"), "x");
        File.WriteAllText(Path.Combine(_root, "sub", "deep", "g.NEF"), "x");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Enumerate_TopDirectoryOnly_IgnoresSubfolders()
    {
        var files = ImageFileEnumerator.Enumerate(new[] { _root }, recursive: false, SupportedFormats.RawExtensions);

        Assert.Equal(new[] { "a.ARW", "b.arw", "c.dng" }, files.Select(Path.GetFileName).ToArray());
    }

    [Fact]
    public void Enumerate_Recursive_FindsNestedFiles()
    {
        var files = ImageFileEnumerator.Enumerate(new[] { _root }, recursive: true, SupportedFormats.RawExtensions);

        Assert.Equal(5, files.Count);
        Assert.Contains(files, f => f.EndsWith(Path.Combine("sub", "f.CR3")));
        Assert.Contains(files, f => f.EndsWith(Path.Combine("sub", "deep", "g.NEF")));
    }

    [Fact]
    public void Enumerate_SingleFile_Pass()
    {
        var file = Path.Combine(_root, "a.ARW");
        var files = ImageFileEnumerator.Enumerate(new[] { file }, recursive: true, SupportedFormats.RawExtensions);

        Assert.Single(files);
        Assert.Equal(file, files[0]);
    }

    [Fact]
    public void Enumerate_SingleFile_WrongExtension_Excluded()
    {
        var file = Path.Combine(_root, "d.jpg");
        var files = ImageFileEnumerator.Enumerate(new[] { file }, recursive: true, SupportedFormats.RawExtensions);

        Assert.Empty(files);
    }

    [Fact]
    public void Enumerate_NonexistentPath_Ignored()
    {
        var files = ImageFileEnumerator.Enumerate(new[] { Path.Combine(_root, "missing") }, recursive: true, SupportedFormats.RawExtensions);

        Assert.Empty(files);
    }

    [Fact]
    public void Enumerate_MultipleRoots_NoDuplicates()
    {
        var files = ImageFileEnumerator.Enumerate(new[] { _root, _root }, recursive: true, SupportedFormats.RawExtensions);

        Assert.Equal(5, files.Count);
    }

    [Fact]
    public void Enumerate_CustomExtensions_OnlySelected()
    {
        var files = ImageFileEnumerator.Enumerate(new[] { _root }, recursive: false, new[] { ".dng", ".cr3" });

        Assert.Equal(new[] { "c.dng" }, files.Select(Path.GetFileName).ToArray());
    }

    [Fact]
    public void Enumerate_CaseInsensitiveExtensions()
    {
        var files = ImageFileEnumerator.Enumerate(new[] { _root }, recursive: false, new[] { ".ARW" });

        Assert.Equal(new[] { "a.ARW", "b.arw" }, files.Select(Path.GetFileName).ToArray());
    }

    [Fact]
    public void Enumerate_SortedOrder()
    {
        var files = ImageFileEnumerator.Enumerate(new[] { _root }, recursive: false, SupportedFormats.RawExtensions);

        Assert.Equal(files.OrderBy(f => f, StringComparer.OrdinalIgnoreCase), files);
    }
}
