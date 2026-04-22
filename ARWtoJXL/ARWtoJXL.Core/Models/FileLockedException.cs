using System;
using System.IO;

namespace ARWtoJXL.Core.Models;

public class FileLockedException : IOException
{
    public FileLockedException(string filePath)
        : base(CreateMessage(filePath))
    {
        FilePath = filePath;
    }

    public FileLockedException(string filePath, Exception inner)
        : base(CreateMessage(filePath), inner)
    {
        FilePath = filePath;
    }

    public string FilePath { get; }

    private static string CreateMessage(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        return $"File '{fileName}' is locked by another application (e.g., Adobe Bridge, Lightroom). Close the file in that application and try again.";
    }

    public static bool IsFileLocked(IOException ex)
    {
        if (ex == null) return false;
        if (ex.HResult == 32) return true;
        if (ex.InnerException is IOException inner && inner.HResult == 32) return true;
        if (ex.Message.Contains("process cannot access the file", StringComparison.OrdinalIgnoreCase)) return true;
        if (ex.Message.Contains("being used by another process", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}
