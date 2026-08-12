using System;
using System.IO;
using RAWtoJXL.Core.Interfaces;

namespace RAWtoJXL.Core.Services;

public class FileLogger : ILogger
{
    public const long DefaultMaxFileSizeBytes = 5 * 1024 * 1024;
    public const int DefaultMaxRotatedFiles = 1;

    private readonly string _logPath;
    private readonly long _maxFileSizeBytes;
    private readonly int _maxRotatedFiles;
    private readonly object _lockObj = new();

    public FileLogger(string? logPath = null, long maxFileSizeBytes = DefaultMaxFileSizeBytes, int maxRotatedFiles = DefaultMaxRotatedFiles)
    {
        _logPath = logPath ?? Path.Combine(Path.GetTempPath(), "RAWtoJXL.log");
        _maxFileSizeBytes = maxFileSizeBytes;
        _maxRotatedFiles = maxRotatedFiles;
    }

    public void Write(string message)
    {
        lock (_lockObj)
        {
            try
            {
                RotateIfNeeded();
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

    private void RotateIfNeeded()
    {
        if (_maxFileSizeBytes <= 0 || _maxRotatedFiles <= 0 || !File.Exists(_logPath))
        {
            return;
        }

        var info = new FileInfo(_logPath);
        if (info.Length < _maxFileSizeBytes)
        {
            return;
        }

        File.Delete($"{_logPath}.{_maxRotatedFiles}");
        for (var i = _maxRotatedFiles - 1; i >= 1; i--)
        {
            var sourcePath = $"{_logPath}.{i}";
            if (File.Exists(sourcePath))
            {
                File.Move(sourcePath, $"{_logPath}.{i + 1}");
            }
        }

        File.Move(_logPath, $"{_logPath}.1");
    }
}
