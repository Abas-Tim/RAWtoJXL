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
    private readonly IProcessRunner _processRunner;
    private const int DefaultTimeoutSeconds = 300;

    public CjxlEncoderService(
        IPathResolver pathResolver,
        IExiftoolService exiftoolService,
        ILogger logger,
        IProcessRunner processRunner)
    {
        _pathResolver = pathResolver ?? throw new ArgumentNullException(nameof(pathResolver));
        _exiftoolService = exiftoolService ?? throw new ArgumentNullException(nameof(exiftoolService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
    }

    public async Task EncodeAsync(
        string inputPath,
        string originalSourcePath,
        string outputPath,
        int quality,
        CancellationToken cancellationToken = default,
        int timeoutSeconds = DefaultTimeoutSeconds,
        Action<double>? progress = null,
        int? effort = null,
        bool skipMetadata = false,
        int? threads = null)
    {
        ValidateInputParameters(inputPath, outputPath, quality);
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException($"Input file not found: {inputPath}", inputPath);
        }

        EnsureOutputDirectoryExists(outputPath);

        string cjxlPath = await ResolveCjxlExecutableAsync(cancellationToken);

        var args = BuildEncodingArguments(quality, inputPath, outputPath, effort, threads);

        await ExecuteEncodingProcessAsync(cjxlPath, args, cancellationToken, timeoutSeconds, progress);

        VerifyOutputFile(outputPath);

        if (!skipMetadata)
        {
            await _exiftoolService.EmbedMetadataAsync(originalSourcePath, outputPath, cancellationToken);
        }
    }

    public async Task EncodeFromStreamAsync(
        Stream inputStream,
        string originalSourcePath,
        string outputPath,
        int quality,
        CancellationToken cancellationToken = default,
        int timeoutSeconds = DefaultTimeoutSeconds,
        Action<double>? progress = null,
        int? effort = null,
        bool skipMetadata = false,
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

        await ExecuteEncodingProcessFromStreamAsync(cjxlPath, args, inputStream, cancellationToken, timeoutSeconds, progress);

        VerifyOutputFile(outputPath);

        if (!skipMetadata)
        {
            await _exiftoolService.EmbedMetadataAsync(originalSourcePath, outputPath, cancellationToken);
        }
    }

    public async Task EncodeFromStreamAsync(
        string inputPath,
        string originalSourcePath,
        string outputPath,
        int quality,
        Func<Stream, CancellationToken, Task> ppmWriter,
        CancellationToken cancellationToken,
        int timeoutSeconds,
        Action<double>? progress,
        int? effort,
        bool skipMetadata = false,
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

        await ExecuteEncodingProcessWithWriterAsync(cjxlPath, args, ppmWriter, inputPath, cancellationToken, timeoutSeconds, progress);

        VerifyOutputFile(outputPath);

        if (!skipMetadata)
        {
            await _exiftoolService.EmbedMetadataAsync(originalSourcePath, outputPath, cancellationToken);
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

    protected internal List<string> BuildEncodingArguments(
        int quality,
        string inputPath,
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

        _logger.Write($"[CjxlEncoder] Building args: quality={quality}, effort={effort}, distance={distance:F2}");

        args.Add(inputPath);
        args.Add(outputPath);

        return args;
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

    private async Task ExecuteEncodingProcessFromStreamAsync(
        string cjxlPath,
        List<string> args,
        Stream inputStream,
        CancellationToken cancellationToken,
        int timeoutSeconds,
        Action<double>? progress)
    {
        var argumentsString = string.Join(" ", args.Select(EscapeArgument));

        _logger.Write($"[CjxlEncoder] Full cjxl command (stdin): {cjxlPath} {argumentsString}");
        _logger.Write($"[CjxlEncoder] Raw args ({args.Count}): [{string.Join("] [", args)}]");

        var startTime = DateTime.UtcNow;
        using var progressCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        progressCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        var progressTask = ReportProgressAsync(startTime, TimeSpan.FromSeconds(timeoutSeconds), progress, progressCts.Token, _logger);

        var encodeTask = _processRunner.RunProcessWithStdinAsync(cjxlPath, argumentsString, inputStream, timeoutSeconds, cancellationToken);
        var result = await encodeTask;

        progressCts.Cancel();

        if (progressTask != null)
        {
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

        var startTime = DateTime.UtcNow;
        using var progressCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        progressCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        var progressTask = ReportProgressAsync(startTime, TimeSpan.FromSeconds(timeoutSeconds), progress, progressCts.Token, _logger);

        var encodeTask = _processRunner.RunProcessWithTimeoutAsync(cjxlPath, argumentsString, timeoutSeconds, cancellationToken);
        var result = await encodeTask;

        progressCts.Cancel();

        if (progressTask != null)
        {
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

    private async Task ExecuteEncodingProcessWithWriterAsync(
        string cjxlPath,
        List<string> args,
        Func<Stream, CancellationToken, Task> ppmWriter,
        string inputPath,
        CancellationToken cancellationToken,
        int timeoutSeconds,
        Action<double>? progress)
    {
        var argumentsString = string.Join(" ", args.Select(EscapeArgument));

        _logger.Write($"[CjxlEncoder] Full cjxl command (stdin): {cjxlPath} {argumentsString}");
        _logger.Write($"[CjxlEncoder] Raw args ({args.Count}): [{string.Join("] [", args)}]");

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        var startTime = DateTime.UtcNow;
        var progressTask = ReportProgressAsync(startTime, TimeSpan.FromSeconds(timeoutSeconds), progress, timeoutCts.Token, _logger);

        var startInfo = new ProcessStartInfo
        {
            FileName = cjxlPath,
            Arguments = argumentsString,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = System.Diagnostics.Process.Start(startInfo);
        if (process == null)
        {
            throw new FileNotFoundException($"Failed to start cjxl: {cjxlPath}");
        }

        bool timedOut = false;

        try
        {
            var stdoutTask = SafeReadStreamAsync(process.StandardOutput, timeoutCts.Token);
            var stderrTask = SafeReadStreamAsync(process.StandardError, timeoutCts.Token);

            await ppmWriter(process.StandardInput.BaseStream, cancellationToken);
            process.StandardInput.Close();

            await process.WaitForExitAsync(timeoutCts.Token);

            string stdout = await stdoutTask;
            string stderr = await stderrTask;

            timeoutCts.Cancel();

            if (progressTask != null)
            {
                try { await progressTask; } catch { /* Progress reporting is best-effort */ }
            }

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
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            timedOut = !cancellationToken.IsCancellationRequested;

            if (!process.HasExited)
            {
                try { process.Kill(); } catch { }
                process.WaitForExit();
            }

            try
            {
                using var drainCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                string stdout = await SafeReadStreamAsync(process.StandardOutput, drainCts.Token);
                string stderr = await SafeReadStreamAsync(process.StandardError, drainCts.Token);
                _logger.Write($"cjxl stdout (aborted): {stdout}");
                _logger.Write($"cjxl stderr (aborted): {stderr}");
            }
            catch (OperationCanceledException)
            {
                // Drain timeout is expected when process was killed
            }
            catch (IOException)
            {
                // Pipe broken after process kill is expected
            }
            catch (Exception ex)
            {
                _logger.Write($"[CjxlEncoder] Error draining killed process output: {ex.Message}");
            }

            if (timedOut)
            {
                throw new TimeoutException(
                    $"cjxl encoding timed out after {timeoutSeconds} seconds. " +
                    "Consider increasing the timeout for large files.");
            }
            throw;
        }
        catch (CjxlEncodingException)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (!process.HasExited)
            {
                try { process.Kill(); } catch { }
                process.WaitForExit();
            }
            throw new Exception($"Failed to encode {Path.GetFileName(inputPath)}: {ex.Message}", ex);
        }
    }

   private static async Task<string> SafeReadStreamAsync(System.IO.StreamReader reader, CancellationToken token)
    {
        var buffer = new char[4096];
        var result = new System.Text.StringBuilder();

        try
        {
            while (!token.IsCancellationRequested)
            {
                int bytesRead = await reader.ReadAsync(buffer.AsMemory(), token);
                if (bytesRead == 0) break;
                result.Append(buffer, 0, bytesRead);
            }
        }
        catch (OperationCanceledException)
        {
            // Reading cancelled; return whatever was captured
        }
        catch (IOException)
        {
            // Pipe broken (process killed); return whatever was captured
        }

        return result.ToString();
    }

    private static async Task ReportProgressAsync(
        DateTime startTime,
        TimeSpan maxTime,
        Action<double>? progress,
        CancellationToken cancellationToken,
        ILogger logger)
    {
        if (progress == null) return;

        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(100, cancellationToken);
            var elapsed = DateTime.UtcNow - startTime;
            var fraction = Math.Min(elapsed.TotalSeconds / maxTime.TotalSeconds, 0.98);
            try
            {
                progress(fraction);
            }
            catch (Exception ex)
            {
                logger.Write($"[CjxlEncoder] Progress callback threw: {ex.GetBaseException().Message}");
            }

            if (elapsed.TotalSeconds >= maxTime.TotalSeconds)
                break;
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
