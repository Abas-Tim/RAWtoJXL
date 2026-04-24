using System;
using System.IO;
using ARWtoJXL.Core.Interfaces;

namespace ARWtoJXL.Core.Services;

public class FileLogger : ILogger
{
    private readonly string _logPath;
    private readonly object _lockObj = new();

    public FileLogger(string? logPath = null)
    {
        _logPath = logPath ?? Path.Combine(Path.GetTempPath(), "ARWtoJXL.log");
    }

    public void Write(string message)
    {
        lock (_lockObj)
        {
            try
            {
                File.AppendAllText(_logPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}{Environment.NewLine}");
            }
            catch (Exception ex)
            {
                try { Console.Error.WriteLine($"[FileLogger] Write failed: {ex.Message}"); } catch { }
            }
        }
    }

    public void Clear()
    {
        lock (_lockObj)
        {
            try
            {
                File.Delete(_logPath);
            }
            catch (Exception ex)
            {
                try { Console.Error.WriteLine($"[FileLogger] Clear failed: {ex.Message}"); } catch { }
            }
        }
    }
}
