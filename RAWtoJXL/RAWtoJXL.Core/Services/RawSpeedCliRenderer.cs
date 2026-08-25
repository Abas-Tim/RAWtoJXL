using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ImageMagick;
using RAWtoJXL.Core.Models;
using RAWtoJXL.Core.Interfaces;

namespace RAWtoJXL.Core.Services;

public sealed class RawSpeedCliRenderer : IRawRenderer
{
    private readonly ILogger _logger;
    private readonly IFileService _fileService;

    public RawSpeedCliRenderer(ILogger logger, IFileService fileService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
    }

    public static string? ResolveExecutable()
    {
        string? configured = Environment.GetEnvironmentVariable("RAWTOJXL_RAWSPEED_CLI");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
        {
            return configured;
        }

        string appLocal = Path.Combine(AppContext.BaseDirectory, "RawSpeedTools", "rawspeed-cli.exe");
        if (File.Exists(appLocal))
        {
            return appLocal;
        }

        string pathVariable = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (string directory in pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = Path.Combine(directory.Trim(), "rawspeed-cli.exe");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    public async Task RenderToPngAsync(
        string inputPath,
        string outputPath,
        int threads,
        CancellationToken cancellationToken = default)
    {
        string? executable = ResolveExecutable();
        if (executable == null)
        {
            throw new FileNotFoundException("rawspeed-cli.exe was not found.", "rawspeed-cli.exe");
        }

        string outputDirectory = Path.GetDirectoryName(outputPath) ?? string.Empty;
        if (!string.IsNullOrEmpty(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        string tempPpm = Path.Combine(outputDirectory, $"rawspeed_{Guid.NewGuid():N}.ppm");
        try
        {
            _logger.Write($"[RawSpeedCliRenderer] Rendering {Path.GetFileName(inputPath)} ({executable}).");
            var stopwatch = Stopwatch.StartNew();
            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add(inputPath);
            startInfo.ArgumentList.Add(tempPpm);
            startInfo.ArgumentList.Add("8");

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Failed to start rawspeed-cli.");
            using var registration = cancellationToken.Register(() =>
            {
                try { if (!process.HasExited) process.Kill(); } catch { }
            });

            Task<string> stdout = process.StandardOutput.ReadToEndAsync();
            Task<string> stderr = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            stopwatch.Stop();

            if (process.ExitCode != 0 || !File.Exists(tempPpm) || new FileInfo(tempPpm).Length == 0)
            {
                string error = await stderr.ConfigureAwait(false);
                string output = await stdout.ConfigureAwait(false);
                _logger.Write($"[RawSpeedCliRenderer] rawspeed-cli exit={process.ExitCode}: {error.Trim()} {output.Trim()}");
                throw new InvalidOperationException("rawspeed-cli rendering failed.");
            }

            await Task.Run(() =>
            {
                using var image = new MagickImage(tempPpm);
                image.ColorSpace = ColorSpace.sRGB;
                image.Settings.SetDefine(MagickFormat.Png, "compression-level", "1");
                image.Format = MagickFormat.Png;
                image.Write(outputPath);
            }, cancellationToken).ConfigureAwait(false);

            _logger.Write($"[RawSpeedCliRenderer] Rendered {Path.GetFileName(inputPath)} in {stopwatch.Elapsed.TotalSeconds:F2}s.");
        }
        finally
        {
            _fileService.DeleteFile(tempPpm);
        }
    }
}
