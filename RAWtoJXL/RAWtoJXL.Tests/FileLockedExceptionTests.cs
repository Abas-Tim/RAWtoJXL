using System.IO;
using RAWtoJXL.Core.Models;

namespace RAWtoJXL.Tests;

public class FileLockedExceptionTests
{
    [Fact]
    public void IsFileLocked_NullException_ReturnsFalse()
    {
        Assert.False(FileLockedException.IsFileLocked(null!));
    }

    [Fact]
    public void IsFileLocked_HResult32_ReturnsTrue()
    {
        var ex = new IOException("locked") { HResult = 32 };
        Assert.True(FileLockedException.IsFileLocked(ex));
    }

    [Fact]
    public void IsFileLocked_HResultNot32_ReturnsFalse()
    {
        var ex = new IOException("other error") { HResult = -2147024892 };
        Assert.False(FileLockedException.IsFileLocked(ex));
    }

    [Fact]
    public void IsFileLocked_InnerIOException_HResult32_ReturnsTrue()
    {
        var inner = new IOException("inner locked") { HResult = 32 };
        var ex = new IOException("outer", inner);
        Assert.True(FileLockedException.IsFileLocked(ex));
    }

    [Fact]
    public void IsFileLocked_InnerExceptionNotIOException_ReturnsFalse()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new IOException("outer", inner);
        Assert.False(FileLockedException.IsFileLocked(ex));
    }

    [Theory]
    [InlineData("The process cannot access the file 'test.jpg' because it is being used by another process.")]
    [InlineData("process cannot access the file")]
    [InlineData("PROCESS CANNOT ACCESS THE FILE")]
    public void IsFileLocked_MessageContainsCannotAccess_ReturnsTrue(string message)
    {
        var ex = new IOException(message);
        Assert.True(FileLockedException.IsFileLocked(ex));
    }

    [Theory]
    [InlineData("The file 'test.jpg' is being used by another process.")]
    [InlineData("being used by another process")]
    [InlineData("BEING USED BY ANOTHER PROCESS")]
    public void IsFileLocked_MessageContainsBeingUsed_ReturnsTrue(string message)
    {
        var ex = new IOException(message);
        Assert.True(FileLockedException.IsFileLocked(ex));
    }

    [Fact]
    public void IsFileLocked_UnrecognizedMessage_ReturnsFalse()
    {
        var ex = new IOException("The network path was not found.");
        Assert.False(FileLockedException.IsFileLocked(ex));
    }

    [Fact]
    public void Constructor_SetsFilePath()
    {
        var path = @"C:\test\file.arw";
        var ex = new FileLockedException(path);
        Assert.Equal(path, ex.FilePath);
        Assert.Contains("file.arw", ex.Message);
    }

    [Fact]
    public void Constructor_WithInnerException_PreservesInner()
    {
        var inner = new IOException("inner") { HResult = 32 };
        var path = @"C:\test\file.arw";
        var ex = new FileLockedException(path, inner);
        Assert.Equal(path, ex.FilePath);
        Assert.Same(inner, ex.InnerException);
    }
}
