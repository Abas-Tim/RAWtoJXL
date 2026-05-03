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

    public async Task<string?> FindExiftoolAsync(string? logPrefix = null)
    {
        string prefix = $"[{logPrefix ?? "SystemProcessRunner"}]";

        foreach (var path in CommonExiftoolPaths)
        {
            if (File.Exists(path) && await IsExiftoolWorkingAsync(path, prefix))
            {
                _logger.Write($"{prefix} Found exiftool at: {path}");
                return path;
            }
        }

        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathEnv.Split(';'))
        {
            var candidate = Path.Combine(dir, "exiftool.exe");
            if (File.Exists(candidate) && await IsExiftoolWorkingAsync(candidate, prefix))
            {
                _logger.Write($"{prefix} Using PATH exiftool: {candidate}");
                return candidate;
            }
        }

        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        if (!string.IsNullOrEmpty(appDir))
        {
            var local = Path.Combine(appDir, "exiftool.exe");
            if (File.Exists(local) && await IsExiftoolWorkingAsync(local, prefix))
            {
                _logger.Write($"{prefix} Using local exiftool: {local}");
                return local;
            }
        }

        return null;
    }

    public async Task<bool> IsExiftoolWorkingAsync(string exiftoolPath, string? logPrefix = null)
    {
        string prefix = $"[{logPrefix ?? "SystemProcessRunner"}]";

        try
        {
            var (exitCode, output, error) = await RunProcessAsync(exiftoolPath, "-ver");

            bool success = exitCode == 0 && output?.TrimStart() is string trimmed && trimmed.Length > 0 && trimmed[0] is >= '0' and <= '9';

            if (!success)
            {
                _logger.Write($"{prefix} exiftool version check failed: exit={exitCode}, stdout='{output?.Trim()}', stderr='{error?.Trim()}'");
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
        try { stdout = await stdoutTask; }
        catch (OperationCanceledException) { }
        catch (IOException) { }
        catch (Exception ex) { _logger.Write($"[SystemProcessRunner] Failed to read stdout: {ex.Message}"); }
        try { stderr = await stderrTask; }
        catch (OperationCanceledException) { }
        catch (IOException) { }
        catch (Exception ex) { _logger.Write($"[SystemProcessRunner] Failed to read stderr: {ex.Message}"); }

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
        using var stderrMs = new MemoryStream();

        var stdoutTask = process.StandardOutput.BaseStream.CopyToAsync(ms, cancellationToken);
        var stderrTask = process.StandardError.BaseStream.CopyToAsync(stderrMs, cancellationToken);

        await Task.WhenAll(stdoutTask, stderrTask);
        await process.WaitForExitAsync(cancellationToken);

        byte[]? result = ms.ToArray();
        return result.Length > 0 ? result : null;
    }

    public async Task<(int ExitCode, string? Stdout, string? Stderr, bool TimedOut)> RunProcessWithStdinAsync(
        string fileName,
        string arguments,
        Stream stdinStream,
        int timeoutSeconds,
        System.Threading.CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardInput = true,
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
        var stdinTask = stdinStream.CopyToAsync(process.StandardInput.BaseStream, cancellationToken);

        await stdinTask;
        process.StandardInput.Close();

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
        try { stdout = await stdoutTask; }
        catch (OperationCanceledException) { }
        catch (IOException) { }
        catch (Exception ex) { _logger.Write($"[SystemProcessRunner] Failed to read stdout: {ex.Message}"); }
        try { stderr = await stderrTask; }
        catch (OperationCanceledException) { }
        catch (IOException) { }
        catch (Exception ex) { _logger.Write($"[SystemProcessRunner] Failed to read stderr: {ex.Message}"); }

        return (process.ExitCode, stdout, stderr, timedOut);
    }
}
