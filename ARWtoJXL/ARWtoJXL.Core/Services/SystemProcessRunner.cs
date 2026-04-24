using System;
using System.Diagnostics;
using System.IO;
using ARWtoJXL.Core.Interfaces;

namespace ARWtoJXL.Core.Services;

public class SystemProcessRunner : IProcessRunner
{
    private readonly ILogger _logger;

    private static readonly string[] CommonExiftoolPaths =
    [
        @"C:\Program Files\exiftool.exe",
        @"C:\Program Files (x86)\exiftool.exe",
        @"C:\Users\Public\exiftool.exe"
    ];

    public SystemProcessRunner(ILogger logger)
    {
        _logger = logger;
    }

    public string? FindExiftool(string? logPrefix = null)
    {
        string prefix = $"[{logPrefix ?? "SystemProcessRunner"}]";

        foreach (var path in CommonExiftoolPaths)
        {
            if (File.Exists(path) && IsExiftoolWorking(path, prefix))
            {
                _logger.Write($"{prefix} Found exiftool at: {path}");
                return path;
            }
        }

        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathEnv.Split(';'))
        {
            var candidate = Path.Combine(dir, "exiftool.exe");
            if (File.Exists(candidate) && IsExiftoolWorking(candidate, prefix))
            {
                _logger.Write($"{prefix} Using PATH exiftool: {candidate}");
                return candidate;
            }
        }

        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        if (!string.IsNullOrEmpty(appDir))
        {
            var local = Path.Combine(appDir, "exiftool.exe");
            if (File.Exists(local) && IsExiftoolWorking(local, prefix))
            {
                _logger.Write($"{prefix} Using local exiftool: {local}");
                return local;
            }
        }

        return null;
    }

    public bool IsExiftoolWorking(string exiftoolPath, string? logPrefix = null)
    {
        string prefix = $"[{logPrefix ?? "SystemProcessRunner"}]";

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = exiftoolPath,
                Arguments = "-ver",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return false;

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            var success = process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output) && output[0] switch
            {
                >= '0' and <= '9' => true,
                _ => false
            };

            if (!success)
            {
                _logger.Write($"{prefix} exiftool version check failed: exit={process.ExitCode}, stdout='{output?.Trim()}', stderr='{error?.Trim()}'");
            }
            else
            {
                _logger.Write($"{prefix} exiftool version check passed: {output?.Trim()}");
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.Write($"{prefix} exiftool version check threw: {ex.Message}");
            return false;
        }
    }

    public async Task<(int ExitCode, string? Stdout, string? Stderr)> RunProcessAsync(
        string fileName,
        string arguments,
        System.Threading.CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            return (-1, null, null);
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        var waitTask = process.WaitForExitAsync(cancellationToken);

        await waitTask;

        return (process.ExitCode, await stdoutTask, await stderrTask);
    }

    public async Task<(int ExitCode, string? Stdout, string? Stderr, bool TimedOut)> RunProcessWithTimeoutAsync(
        string fileName,
        string arguments,
        int timeoutSeconds,
        System.Threading.CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            return (-1, null, null, false);
        }

        using var cancellationRegistration = cancellationToken.Register(() =>
        {
            try { if (!process.HasExited) process.Kill(); } catch { }
        });

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        bool timedOut = false;
        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            timedOut = !cancellationToken.IsCancellationRequested;
            if (!process.HasExited)
            {
                try { process.Kill(); } catch { }
                process.WaitForExit();
            }
        }

        string? stdout = null;
        string? stderr = null;
        try { stdout = await stdoutTask; } catch { }
        try { stderr = await stderrTask; } catch { }

        return (process.ExitCode, stdout, stderr, timedOut);
    }

    public async Task<byte[]?> RunProcessBinaryAsync(
        string fileName,
        string arguments,
        System.Threading.CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            return null;
        }

        using var cancellationRegistration = cancellationToken.Register(() =>
        {
            try { if (!process.HasExited) process.Kill(); } catch { }
        });

        using var ms = new MemoryStream();
        await process.StandardOutput.BaseStream.CopyToAsync(ms, cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        byte[]? result = ms.ToArray();
        return result.Length > 0 ? result : null;
    }
}
