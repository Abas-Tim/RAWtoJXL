using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARWtoJXL.Core.Interfaces;
using ARWtoJXL.Core.Models;

namespace ARWtoJXL.Core.Services;

public class CjxlEncoderService : ICjxlEncoder
{
    private readonly IPathResolver _pathResolver;
    private readonly IExiftoolService _exiftoolService;
    private readonly ILogger _logger;
    private const int DefaultTimeoutSeconds = 300;

    public CjxlEncoderService(
        IPathResolver pathResolver,
        IExiftoolService exiftoolService,
        ILogger logger)
    {
        _pathResolver = pathResolver ?? throw new ArgumentNullException(nameof(pathResolver));
        _exiftoolService = exiftoolService ?? throw new ArgumentNullException(nameof(exiftoolService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task EncodeAsync(
        string inputPath,
        string originalArwPath,
        string outputPath,
        int quality,
        MetadataProfiles? metadata = null,
        CancellationToken cancellationToken = default,
        int timeoutSeconds = DefaultTimeoutSeconds,
        Action<double>? progress = null)
    {
        ValidateInputParameters(inputPath, outputPath, quality);
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException($"Input file not found: {inputPath}", inputPath);
        }

        EnsureOutputDirectoryExists(outputPath);

        string cjxlPath = await ResolveCjxlExecutableAsync(cancellationToken);

        var args = BuildEncodingArguments(quality, metadata, inputPath, outputPath);

        await ExecuteEncodingProcessAsync(cjxlPath, args, cancellationToken, timeoutSeconds, progress);

        VerifyOutputFile(outputPath);

        if (metadata != null && metadata.HasAny)
        {
            await _exiftoolService.EmbedMetadataAsync(originalArwPath, outputPath, metadata, cancellationToken);
        }
    }

    private static void ValidateInputParameters(string inputPath, string outputPath, int quality)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            throw new ArgumentNullException(nameof(inputPath), "Input path cannot be null or empty.");
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentNullException(nameof(outputPath), "Output path cannot be null or empty.");
        }

        if (!Path.IsPathRooted(inputPath) && !inputPath.StartsWith("."))
        {
            throw new ArgumentException($"Input path must be a valid file path: {inputPath}", nameof(inputPath));
        }

        if (!Path.IsPathRooted(outputPath) && !outputPath.StartsWith("."))
        {
            throw new ArgumentException($"Output path must be a valid file path: {outputPath}", nameof(outputPath));
        }

        if (quality < 0 || quality > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(quality), "Quality must be between 0 and 100.");
        }
    }

    private static void EnsureOutputDirectoryExists(string outputPath)
    {
        string? outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDirectory) && !Directory.Exists(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }
    }

    private async Task<string> ResolveCjxlExecutableAsync(CancellationToken cancellationToken)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();

        string cjxlPath = _pathResolver.ResolveCjxlPath();

        if (string.IsNullOrEmpty(cjxlPath))
        {
            throw new FileNotFoundException(
                "cjxl executable path is empty. Please ensure cjxl.exe is installed alongside the application.",
                "cjxl.exe");
        }

        if (!File.Exists(cjxlPath))
        {
            throw new FileNotFoundException(
                $"cjxl executable not found at: {cjxlPath}. Please ensure it is installed alongside the application.",
                cjxlPath);
        }

        return cjxlPath;
    }

    private List<string> BuildEncodingArguments(
        int quality,
        MetadataProfiles? metadata,
        string inputPath,
        string outputPath)
    {
        var args = new List<string>(16);

        float distance = QualityCalculator.CalculateDistance(quality);
        int effort = QualityCalculator.CalculateEffort(quality);
        bool isLossless = QualityCalculator.IsLossless(quality);

        args.Add(isLossless ? "--distance=0" : $"--distance={distance:F2}");
        args.Add($"--effort={effort}");
        args.Add($"--num_threads={Environment.ProcessorCount}");
        args.Add("--container=1");

        if (isLossless)
        {
            args.Add("--modular=1");
        }
        else
        {
            args.Add("--progressive_dc=1");
        }

        _logger.Write($"[CjxlEncoder] Building args: quality={quality}, metadata={metadata?.HasAny}");
        AddMetadataArguments(args, metadata);

        args.Add(inputPath);
        args.Add(outputPath);

        return args;
    }

    private void AddMetadataArguments(List<string> args, MetadataProfiles? metadata)
    {
        if (metadata is null || !metadata.HasAny)
        {
            _logger.Write("[CjxlEncoder] No metadata to add");
            return;
        }

        _logger.Write($"[CjxlEncoder] Adding metadata profiles: Exif={metadata.ExifPath ?? "none"}, Xmp={metadata.XmpPath ?? "none"}, Icc={metadata.IccPath ?? "none"}, Iptc={metadata.IptcPath ?? "none"}");

        void AddMetaArg(string key, string? path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            var size = new FileInfo(path).Length;
            if (size == 0) return;
            var cleanPath = path.Replace("\\", "/");
            args.Add("-x");
            args.Add($"{key}={cleanPath}");
            _logger.Write($"[CjxlEncoder] Added metadata: -x {key}={cleanPath} ({size} bytes)");
        }

        AddMetaArg("exif", metadata.ExifPath);
        AddMetaArg("xmp", metadata.XmpPath);
        AddMetaArg("icc_pathname", metadata.IccPath);
        AddMetaArg("jumbf", metadata.IptcPath);
    }

    private async Task ExecuteEncodingProcessAsync(
        string cjxlPath,
        List<string> args,
        CancellationToken cancellationToken,
        int timeoutSeconds,
        Action<double>? progress)
    {
        var argumentsString = string.Join(" ", args.Select(EscapeArgument));

        _logger.Write($"[CjxlEncoder] Full cjxl command: {cjxlPath} {argumentsString}");
        _logger.Write($"[CjxlEncoder] Raw args ({args.Count}): [{string.Join("] [", args)}]");

        var startInfo = new ProcessStartInfo
        {
            FileName = cjxlPath,
            Arguments = argumentsString,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8
        };

        using var process = new Process { StartInfo = startInfo };

        using var cancellationRegistration = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                }
            }
            catch
            {
            }
        });

        process.Start();
        var startTime = DateTime.UtcNow;
        var maxTime = TimeSpan.FromSeconds(timeoutSeconds);

        var progressTask = ReportProgressAsync(process, startTime, maxTime, progress, cancellationToken);

        var readOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var readErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        var waitExitTask = process.WaitForExitAsync(cancellationToken);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        try
        {
            await waitExitTask.WaitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            if (!process.HasExited)
            {
                process.Kill();
            }
            throw new TimeoutException(
                $"cjxl encoding timed out after {timeoutSeconds} seconds. " +
                "Consider increasing the timeout for large files.");
        }

        string stdout = await readOutputTask;
        string stderr = await readErrorTask;

        _logger.Write($"cjxl stdout: {stdout}");
        _logger.Write($"cjxl stderr: {stderr}");

        if (process.ExitCode != 0)
        {
            string errorMessage = string.IsNullOrWhiteSpace(stderr)
                ? "Unknown error occurred during encoding"
                : stderr.Trim();

            throw new CjxlEncodingException(
                $"cjxl encoding failed with exit code {process.ExitCode}: {errorMessage}",
                process.ExitCode);
        }
    }

    private static async Task ReportProgressAsync(
        Process process,
        DateTime startTime,
        TimeSpan maxTime,
        Action<double>? progress,
        CancellationToken cancellationToken)
    {
        if (progress == null) return;

        while (!process.HasExited && !cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(100, cancellationToken);
            var elapsed = DateTime.UtcNow - startTime;
            var fraction = Math.Min(elapsed.TotalSeconds / maxTime.TotalSeconds, 0.98);
            progress?.Invoke(fraction);
        }
    }

    private static string EscapeArgument(string argument)
    {
        if (argument.Any(c => char.IsWhiteSpace(c) || c == '"'))
        {
            return $"\"{argument.Replace("\"", "\\\"")}\"";
        }
        return argument;
    }

    private static void VerifyOutputFile(string outputPath)
    {
        if (!File.Exists(outputPath))
        {
            throw new FileNotFoundException(
                $"Output file was created but is empty: {outputPath}",
                outputPath);
        }

        long fileSize = new FileInfo(outputPath).Length;
        if (fileSize == 0)
        {
            throw new IOException($"Output file was created but is empty: {outputPath}");
        }
    }
}

public class CjxlEncodingException : Exception
{
    public int ExitCode { get; }

    public CjxlEncodingException(string message) : base(message)
    {
    }

    public CjxlEncodingException(string message, int exitCode) : base(message)
    {
        ExitCode = exitCode;
    }

    public CjxlEncodingException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public CjxlEncodingException(string message, int exitCode, Exception innerException) : base(message, innerException)
    {
        ExitCode = exitCode;
    }
}
