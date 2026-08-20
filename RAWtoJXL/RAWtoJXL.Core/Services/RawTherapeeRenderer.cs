using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RAWtoJXL.Core.Interfaces;

namespace RAWtoJXL.Core.Services;

public sealed class RawTherapeeRenderer : IRawRenderer
{
    private readonly ILogger _logger;

    public RawTherapeeRenderer(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task RenderToPngAsync(
        string inputPath,
        string outputPath,
        int threads,
        CancellationToken cancellationToken = default)
    {
        string executablePath = ResolveExecutablePath();
        string? outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.Environment["OMP_NUM_THREADS"] = Math.Max(1, threads).ToString();
        startInfo.Environment["OMP_DYNAMIC"] = "FALSE";
        startInfo.ArgumentList.Add("-q");
        startInfo.ArgumentList.Add("-o");
        startInfo.ArgumentList.Add(outputPath);
        startInfo.ArgumentList.Add("-n");
        startInfo.ArgumentList.Add("-b16");
        startInfo.ArgumentList.Add("-Y");
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add(inputPath);

        _logger.Write($"[RawTherapeeRenderer] Rendering {Path.GetFileName(inputPath)} with {Math.Max(1, threads)} threads.");

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start rawtherapee-cli.");
        using var registration = cancellationToken.Register(() =>
        {
            try { if (!process.HasExited) process.Kill(); } catch { }
        });

        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);

        if (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }

        string error = await stderr.ConfigureAwait(false);
        if (process.ExitCode != 0 || !File.Exists(outputPath) || new FileInfo(outputPath).Length == 0)
        {
            string output = await stdout.ConfigureAwait(false);
            throw new InvalidOperationException($"RawTherapee rendering failed: {error.Trim()} {output.Trim()}");
        }
    }

    private static string ResolveExecutablePath()
    {
        string? configuredPath = Environment.GetEnvironmentVariable("RAWTOJXL_RAWTHERAPEE_CLI");
        if (!string.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath))
        {
            return configuredPath;
        }

        string appLocalPath = Path.Combine(AppContext.BaseDirectory, "rawtherapee-cli.exe");
        if (File.Exists(appLocalPath))
        {
            return appLocalPath;
        }

        string appLocalDirectoryPath = Path.Combine(AppContext.BaseDirectory, "RawTherapee", "rawtherapee-cli.exe");
        if (File.Exists(appLocalDirectoryPath))
        {
            return appLocalDirectoryPath;
        }

        string pathVariable = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (string directory in pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = Path.Combine(directory.Trim(), "rawtherapee-cli.exe");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string root = Path.Combine(programFiles, "RawTherapee");
        if (Directory.Exists(root))
        {
            string? candidate = Directory.EnumerateFiles(root, "rawtherapee-cli.exe", SearchOption.AllDirectories)
                .FirstOrDefault();
            if (candidate != null)
            {
                return candidate;
            }
        }

        throw new FileNotFoundException(
            "rawtherapee-cli.exe was not found. Install RawTherapee or set RAWTOJXL_RAWTHERAPEE_CLI.",
            "rawtherapee-cli.exe");
    }
}
