using System.IO;
using RAWtoJXL.Core.Interfaces;
using RAWtoJXL.Core.Services;
using RAWtoJXL.Core.Settings;

namespace RAWtoJXL.Tests.Cli;

public class OutputPathResolverTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "rawtojxl_outresolver_" + Guid.NewGuid().ToString("N"));

    public OutputPathResolverTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    private string CreateSource(string name = "photo.ARW")
    {
        var path = Path.Combine(_tempDir, name);
        File.WriteAllText(path, "dummy");
        return path;
    }

    private string CreateExisting(string name)
    {
        var path = Path.Combine(_tempDir, name);
        File.WriteAllText(path, "existing");
        return path;
    }

    [Fact]
    public void Resolve_SameDirectory_NoSubfolder()
    {
        var source = CreateSource();
        var result = OutputPathResolver.Resolve(source, OutputFormat.Jxl, ConflictResolution.Overwrite,
            useCustomOutputDirectory: false, customOutputDirectory: null, useSubfolder: false, subfolderName: "jxl_output");

        Assert.Equal(Path.Combine(_tempDir, "photo.jxl"), result);
    }

    [Fact]
    public void Resolve_Subfolder_CreatesDirectory()
    {
        var source = CreateSource();
        var result = OutputPathResolver.Resolve(source, OutputFormat.Jxl, ConflictResolution.Overwrite,
            useCustomOutputDirectory: false, customOutputDirectory: null, useSubfolder: true, subfolderName: "out");

        Assert.Equal(Path.Combine(_tempDir, "out", "photo.jxl"), result);
        Assert.True(Directory.Exists(Path.Combine(_tempDir, "out")));
    }

    [Fact]
    public void Resolve_Subfolder_CreateDirectoryFalse_DoesNotCreate()
    {
        var source = CreateSource();
        var result = OutputPathResolver.Resolve(source, OutputFormat.Jxl, ConflictResolution.Overwrite,
            useCustomOutputDirectory: false, customOutputDirectory: null, useSubfolder: true, subfolderName: "out",
            createDirectory: false);

        Assert.Equal(Path.Combine(_tempDir, "out", "photo.jxl"), result);
        Assert.False(Directory.Exists(Path.Combine(_tempDir, "out")));
    }

    [Fact]
    public void Resolve_CustomOutputDirectory_WinsOverSubfolder()
    {
        var source = CreateSource();
        var custom = Path.Combine(_tempDir, "custom");
        var result = OutputPathResolver.Resolve(source, OutputFormat.Jxl, ConflictResolution.Overwrite,
            useCustomOutputDirectory: true, customOutputDirectory: custom, useSubfolder: true, subfolderName: "out");

        Assert.Equal(Path.Combine(custom, "photo.jxl"), result);
        Assert.True(Directory.Exists(custom));
        Assert.False(Directory.Exists(Path.Combine(_tempDir, "out")));
    }

    [Theory]
    [InlineData(OutputFormat.Jxl, ".jxl")]
    [InlineData(OutputFormat.Jpeg, ".jpg")]
    [InlineData(OutputFormat.Avif, ".avif")]
    public void Resolve_UsesCorrectExtension(OutputFormat format, string expectedExtension)
    {
        var source = CreateSource();
        var result = OutputPathResolver.Resolve(source, format, ConflictResolution.Overwrite,
            useCustomOutputDirectory: false, customOutputDirectory: null, useSubfolder: false, subfolderName: "x");

        Assert.EndsWith(expectedExtension, result);
    }

    [Fact]
    public void GetOutputExtension_MapsAllFormats()
    {
        Assert.Equal(".jxl", OutputPathResolver.GetOutputExtension(OutputFormat.Jxl));
        Assert.Equal(".jpg", OutputPathResolver.GetOutputExtension(OutputFormat.Jpeg));
        Assert.Equal(".avif", OutputPathResolver.GetOutputExtension(OutputFormat.Avif));
    }

    [Fact]
    public void Resolve_ConflictOverwrite_ExistingFile_ReturnsSamePath()
    {
        var source = CreateSource();
        CreateExisting("photo.jxl");
        var result = OutputPathResolver.Resolve(source, OutputFormat.Jxl, ConflictResolution.Overwrite,
            useCustomOutputDirectory: false, customOutputDirectory: null, useSubfolder: false, subfolderName: "x");

        Assert.Equal(Path.Combine(_tempDir, "photo.jxl"), result);
    }

    [Fact]
    public void Resolve_ConflictSkip_ExistingFile_ReturnsNull()
    {
        var source = CreateSource();
        CreateExisting("photo.jxl");
        var result = OutputPathResolver.Resolve(source, OutputFormat.Jxl, ConflictResolution.Skip,
            useCustomOutputDirectory: false, customOutputDirectory: null, useSubfolder: false, subfolderName: "x");

        Assert.Null(result);
    }

    [Fact]
    public void Resolve_ConflictSkip_NoExistingFile_ReturnsPath()
    {
        var source = CreateSource();
        var result = OutputPathResolver.Resolve(source, OutputFormat.Jxl, ConflictResolution.Skip,
            useCustomOutputDirectory: false, customOutputDirectory: null, useSubfolder: false, subfolderName: "x");

        Assert.Equal(Path.Combine(_tempDir, "photo.jxl"), result);
    }

    [Fact]
    public void Resolve_ConflictAppendNumber_AppendsCounter()
    {
        var source = CreateSource();
        CreateExisting("photo.jxl");
        CreateExisting("photo_1.jxl");
        var result = OutputPathResolver.Resolve(source, OutputFormat.Jxl, ConflictResolution.AppendNumber,
            useCustomOutputDirectory: false, customOutputDirectory: null, useSubfolder: false, subfolderName: "x");

        Assert.Equal(Path.Combine(_tempDir, "photo_2.jxl"), result);
    }

    [Fact]
    public void Resolve_ConflictAppendNumber_NoConflict_NoSuffix()
    {
        var source = CreateSource();
        var result = OutputPathResolver.Resolve(source, OutputFormat.Jxl, ConflictResolution.AppendNumber,
            useCustomOutputDirectory: false, customOutputDirectory: null, useSubfolder: false, subfolderName: "x");

        Assert.Equal(Path.Combine(_tempDir, "photo.jxl"), result);
    }

    [Fact]
    public void Resolve_EmptyCustomDirectory_FallsBackToSourceDirectory()
    {
        var source = CreateSource();
        var result = OutputPathResolver.Resolve(source, OutputFormat.Jxl, ConflictResolution.Overwrite,
            useCustomOutputDirectory: true, customOutputDirectory: "", useSubfolder: false, subfolderName: "x");

        Assert.Equal(Path.Combine(_tempDir, "photo.jxl"), result);
    }
}
