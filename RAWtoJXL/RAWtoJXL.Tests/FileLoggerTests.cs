using System.IO;
using RAWtoJXL.Core.Services;

namespace RAWtoJXL.Tests;

public class FileLoggerTests
{
    [Fact]
    public void Write_AppendsTimestampedLines()
    {
        var path = GetTempLogPath();
        try
        {
            var logger = new FileLogger(path);
            logger.Write("hello");

            var lines = File.ReadAllLines(path);
            Assert.Single(lines);
            Assert.EndsWith(" hello", lines[0]);
        }
        finally
        {
            CleanupLogs(path);
        }
    }

    [Fact]
    public void Write_RotatesWhenFileExceedsMaxSize()
    {
        var path = GetTempLogPath();
        try
        {
            var logger = new FileLogger(path, maxFileSizeBytes: 256, maxRotatedFiles: 1);

            logger.Write(new string('a', 200));
            logger.Write(new string('b', 200));
            logger.Write(new string('c', 200));

            Assert.True(File.Exists(path));
            Assert.True(File.Exists($"{path}.1"));

            var rotated = File.ReadAllText($"{path}.1");
            Assert.Contains('a', rotated);
            Assert.Contains('b', rotated);

            var current = File.ReadAllText(path);
            Assert.Contains('c', current);
            Assert.DoesNotContain('a', current);
            Assert.DoesNotContain('b', current);
        }
        finally
        {
            CleanupLogs(path);
        }
    }

    [Fact]
    public void Write_KeepsBoundedNumberOfRotatedFiles()
    {
        var path = GetTempLogPath();
        try
        {
            var logger = new FileLogger(path, maxFileSizeBytes: 64, maxRotatedFiles: 2);

            for (var i = 0; i < 8; i++)
            {
                logger.Write(new string((char)('a' + i), 100));
            }

            Assert.True(File.Exists(path));
            Assert.True(File.Exists($"{path}.1"));
            Assert.True(File.Exists($"{path}.2"));
            Assert.False(File.Exists($"{path}.3"));
        }
        finally
        {
            CleanupLogs(path);
        }
    }

    [Fact]
    public void Clear_DeletesLogFile()
    {
        var path = GetTempLogPath();
        try
        {
            var logger = new FileLogger(path);
            logger.Write("before clear");
            Assert.True(File.Exists(path));

            logger.Clear();
            Assert.False(File.Exists(path));

            logger.Write("after clear");
            Assert.True(File.Exists(path));
        }
        finally
        {
            CleanupLogs(path);
        }
    }

    private static string GetTempLogPath()
    {
        return Path.Combine(Path.GetTempPath(), $"RAWtoJXL-tests-{Guid.NewGuid():N}.log");
    }

    private static void CleanupLogs(string path)
    {
        foreach (var candidate in new[] { path, $"{path}.1", $"{path}.2", $"{path}.3" })
        {
            try
            {
                File.Delete(candidate);
            }
            catch
            {
            }
        }
    }
}
