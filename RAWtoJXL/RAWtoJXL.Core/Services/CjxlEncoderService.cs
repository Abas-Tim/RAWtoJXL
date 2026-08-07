using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RAWtoJXL.Core.Interfaces;
using RAWtoJXL.Core.Models;

namespace RAWtoJXL.Core.Services;

public class CjxlEncoderService : ICjxlEncoder
{
    private readonly IPathResolver _pathResolver;
    private readonly ILogger _logger;
    private readonly IProcessRunner _processRunner;
    private const int DefaultTimeoutSeconds = 300;

    public CjxlEncoderService(
        IPathResolver pathResolver,
        ILogger logger,
        IProcessRunner processRunner)
    {
        _pathResolver = pathResolver ?? throw new ArgumentNullException(nameof(pathResolver));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
    }

    public async Task EncodeFromStreamAsync(
        string inputPath,
        string outputPath,
        int quality,
        Func<Stream, CancellationToken, Task> ppmWriter,
        CancellationToken cancellationToken,
        int timeoutSeconds,
        Action<double>? progress,
        int? effort,
        int? threads = null)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentNullException(nameof(outputPath), "Output path cannot be null or empty.");
        }

        if (quality < 0 || quality > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(quality), "Quality must be between 0 and 100.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        EnsureOutputDirectoryExists(outputPath);

        string cjxlPath = await ResolveCjxlExecutableAsync(cancellationToken);

        var args = BuildStreamEncodingArguments(quality, outputPath, effort, threads);

        var streamPhase = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Func<Stream, CancellationToken, Task> timedWriter = async (stream, ct) =>
        {
            try
            {
                await ppmWriter(stream, ct);
                streamPhase.TrySetResult();
            }
            catch (Exception ex)
            {
                streamPhase.TrySetException(ex);
                throw;
            }
        };

        await ExecuteEncodingProcessAsync(
            cjxlPath, args, usesStdin: true, cancellationToken, timeoutSeconds, progress, streamPhase.Task,
            argumentsString => _processRunner.RunProcessWithStdinWriterAsync(cjxlPath, argumentsString, timedWriter, timeoutSeconds, cancellationToken));

        VerifyOutputFile(outputPath);
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

    protected internal List<string> BuildStreamEncodingArguments(
        int quality,
        string outputPath,
        int? effortOverride = null,
        int? threadsOverride = null)
    {
        var args = new List<string>(10);

        float distance = QualityCalculator.CalculateDistance(quality);
        int effort = effortOverride ?? QualityCalculator.CalculateEffort(quality);
        bool isLossless = QualityCalculator.IsLossless(quality);
        int threads = threadsOverride ?? Environment.ProcessorCount;

        args.Add(isLossless ? "--distance=0" : $"--distance={distance:F2}");
        args.Add($"--effort={effort}");
        args.Add($"--num_threads={threads}");
        args.Add("--container=1");

        if (isLossless)
        {
            args.Add("--modular=1");
        }
        else
        {
            args.Add("--progressive_dc=1");
        }

        _logger.Write($"[CjxlEncoder] Building stream args: quality={quality}, effort={effort}, distance={distance:F2}");

        args.Add("-");
        args.Add(outputPath);

        return args;
    }

    private async Task ExecuteEncodingProcessAsync(
        string cjxlPath,
        List<string> args,
        bool usesStdin,
        CancellationToken cancellationToken,
        int timeoutSeconds,
        Action<double>? progress,
        Task? streamingPhase,
        Func<string, Task<(int ExitCode, string? Stdout, string? Stderr, bool TimedOut)>> runProcess)
    {
        var argumentsString = string.Join(" ", args.Select(EscapeArgument));

        _logger.Write($"[CjxlEncoder] Full cjxl command ({(usesStdin ? "stdin" : "file")}): {cjxlPath} {argumentsString}");
        _logger.Write($"[CjxlEncoder] Raw args ({args.Count}): [{string.Join("] [", args)}]");

        var startTime = DateTime.UtcNow;
        using var progressCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        progressCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        var progressTask = ReportProgressAsync(startTime, TimeSpan.FromSeconds(timeoutSeconds), streamingPhase, progress, progressCts.Token, _logger);

        (int ExitCode, string? Stdout, string? Stderr, bool TimedOut) result;
        try
        {
            result = await runProcess(argumentsString);
        }
        finally
        {
            progressCts.Cancel();
            try { await progressTask; } catch { /* Progress reporting is best-effort */ }
        }

        _logger.Write($"cjxl stdout: {result.Stdout}");
        _logger.Write($"cjxl stderr: {result.Stderr}");

        if (result.TimedOut)
        {
            throw new TimeoutException(
                $"cjxl encoding timed out after {timeoutSeconds} seconds. " +
                "Consider increasing the timeout for large files.");
        }

        if (result.ExitCode != 0)
        {
            string errorMessage = string.IsNullOrWhiteSpace(result.Stderr)
                ? "Unknown error occurred during encoding"
                : result.Stderr.Trim();

            throw new CjxlEncodingException(
                $"cjxl encoding failed with exit code {result.ExitCode}: {errorMessage}",
                result.ExitCode);
        }
    }

    internal static async Task ReportProgressAsync(
        DateTime startTime,
        TimeSpan maxTime,
        Task? streamingPhase,
        Action<double>? progress,
        CancellationToken cancellationToken,
        ILogger logger)
    {
        if (progress == null) return;

        var hardBudget = maxTime < TimeSpan.FromSeconds(60) ? maxTime : TimeSpan.FromSeconds(60);
        DateTime? encodeStart = null;
        TimeSpan? budget = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(100, cancellationToken);

            double fraction;
            if (streamingPhase == null)
            {
                budget ??= hardBudget;
                fraction = 0.05 + 0.93 * Math.Min((DateTime.UtcNow - startTime).TotalSeconds / budget.Value.TotalSeconds, 1.0);
            }
            else if (!streamingPhase.IsCompleted)
            {
                fraction = 0.05;
            }
            else
            {
                encodeStart ??= DateTime.UtcNow;
                budget ??= ClampBudget((encodeStart.Value - startTime) * 3, 8, hardBudget);
                fraction = 0.05 + 0.93 * Math.Min((DateTime.UtcNow - encodeStart.Value).TotalSeconds / budget.Value.TotalSeconds, 1.0);
            }

            try
            {
                progress(fraction);
            }
            catch (Exception ex)
            {
                logger.Write($"[CjxlEncoder] Progress callback threw: {ex.GetBaseException().Message}");
            }
        }
    }

    internal static TimeSpan ClampBudget(TimeSpan value, double minSeconds, TimeSpan max)
    {
        var min = TimeSpan.FromSeconds(minSeconds);
        if (value < min) return min;
        return value > max ? max : value;
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
