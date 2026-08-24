using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RAWtoJXL.Core.Interfaces;

namespace RAWtoJXL.Core.Services;

public sealed class PreferredRawRenderer : IRawRenderer
{
    private readonly ILogger _logger;
    private readonly MagickRawRenderer _magickFallback;

    public PreferredRawRenderer(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _magickFallback = new MagickRawRenderer(logger);
    }

    public static string? ResolveDarktableExecutable()
    {
        string? configured = Environment.GetEnvironmentVariable("RAWTOJXL_DARKTABLE_CLI");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
        {
            return configured;
        }

        string appLocal = Path.Combine(AppContext.BaseDirectory, "Darktable", "darktable-cli.exe");
        if (File.Exists(appLocal))
        {
            return appLocal;
        }

        string appBaseDirect = Path.Combine(AppContext.BaseDirectory, "darktable-cli.exe");
        if (File.Exists(appBaseDirect))
        {
            return appBaseDirect;
        }

        string pathVariable = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (string directory in pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = Path.Combine(directory.Trim(), "darktable-cli.exe");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string root = Path.Combine(programFiles, "darktable");
        if (Directory.Exists(root))
        {
            string? found = Directory.EnumerateFiles(root, "darktable-cli.exe", SearchOption.AllDirectories).FirstOrDefault();
            if (found != null)
            {
                return found;
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
        string? darktable = ResolveDarktableExecutable();
        if (darktable == null)
        {
            await _magickFallback.RenderToPngAsync(inputPath, outputPath, threads, cancellationToken).ConfigureAwait(false);
            return;
        }

        string? outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        _logger.Write($"[PreferredRawRenderer] Rendering {Path.GetFileName(inputPath)} with darktable-cli {darktable}.");
        if (await TryDarktableRenderAsync(darktable, inputPath, outputPath, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        _logger.Write("[PreferredRawRenderer] darktable-cli failed; falling back to Magick raw decode.");
        await _magickFallback.RenderToPngAsync(inputPath, outputPath, threads, cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> TryDarktableRenderAsync(
        string darktablePath,
        string inputPath,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = darktablePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("-i");
        startInfo.ArgumentList.Add(inputPath);
        startInfo.ArgumentList.Add("-o");
        startInfo.ArgumentList.Add(outputPath);
        startInfo.ArgumentList.Add("--core");
        startInfo.ArgumentList.Add("--conf");
        startInfo.ArgumentList.Add("opencl=1");

        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Failed to start darktable-cli.");
            using var registration = cancellationToken.Register(() =>
            {
                try { if (!process.HasExited) process.Kill(); } catch { }
            });

            Task<string> stdout = process.StandardOutput.ReadToEndAsync();
            Task<string> stderr = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            stopwatch.Stop();

            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            if (process.ExitCode != 0 || !File.Exists(outputPath) || new FileInfo(outputPath).Length == 0)
            {
                string error = await stderr.ConfigureAwait(false);
                string output = await stdout.ConfigureAwait(false);
                _logger.Write($"[PreferredRawRenderer] darktable-cli exit={process.ExitCode}: {error.Trim()} {output.Trim()}");
                return false;
            }

            _logger.Write($"[PreferredRawRenderer] Rendered {Path.GetFileName(inputPath)} in {stopwatch.Elapsed.TotalSeconds:F2}s.");
            return true;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.Write($"[PreferredRawRenderer] darktable-cli failed: {ex.Message}");
            return false;
        }
    }
}
