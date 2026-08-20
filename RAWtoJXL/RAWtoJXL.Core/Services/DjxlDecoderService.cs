using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using RAWtoJXL.Core.Interfaces;

namespace RAWtoJXL.Core.Services;

public class DjxlDecoderService : IJxlDecoder
{
    private readonly IPathResolver _pathResolver;
    private readonly ILogger _logger;
    private readonly IProcessRunner _processRunner;

    public DjxlDecoderService(
        IPathResolver pathResolver,
        ILogger logger,
        IProcessRunner processRunner)
    {
        _pathResolver = pathResolver ?? throw new ArgumentNullException(nameof(pathResolver));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
    }

    public async Task DecodeToPngAsync(string inputPath, string outputPath, CancellationToken cancellationToken = default, int timeoutSeconds = 300)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            throw new ArgumentException("Input path cannot be null or empty.", nameof(inputPath));
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentNullException(nameof(outputPath), "Output path cannot be null or empty.");
        }

        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException($"Input file not found: {inputPath}");
        }

        cancellationToken.ThrowIfCancellationRequested();

        string? outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDirectory) && !Directory.Exists(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        string djxlPath = await ResolveDjxlExecutableAsync(cancellationToken);

        string arguments = $"{Quote(inputPath)} {Quote(outputPath)}";

        _logger.Write($"[DjxlDecoder] Decoding {Path.GetFileName(inputPath)} to {Path.GetFileName(outputPath)}");
        _logger.Write($"[DjxlDecoder] Full djxl command: {djxlPath} {arguments}");

        var (exitCode, stdout, stderr, timedOut) = await _processRunner.RunProcessWithTimeoutAsync(
            djxlPath, arguments, timeoutSeconds, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        _logger.Write($"[DjxlDecoder] djxl exit={exitCode}, stdout='{stdout?.Trim()}', stderr='{stderr?.Trim()}'");

        if (timedOut || exitCode != 0)
        {
            TryDeleteOutput(outputPath);

            if (timedOut)
            {
                throw new TimeoutException(
                    $"djxl decoding timed out after {timeoutSeconds} seconds for {Path.GetFileName(inputPath)}.");
            }

            string errorMessage = string.IsNullOrWhiteSpace(stderr)
                ? "Unknown error occurred during decoding"
                : stderr.Trim();

            throw new JxlDecodingException(
                $"djxl decoding failed with exit code {exitCode}: {errorMessage}",
                exitCode);
        }

        if (!File.Exists(outputPath) || new FileInfo(outputPath).Length == 0)
        {
            throw new IOException($"djxl did not produce a valid output file: {outputPath}");
        }
    }

    private async Task<string> ResolveDjxlExecutableAsync(CancellationToken cancellationToken)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();

        string djxlPath = _pathResolver.ResolveDjxlPath();

        if (string.IsNullOrEmpty(djxlPath))
        {
            throw new FileNotFoundException(
                "djxl executable path is empty. Please ensure djxl.exe is installed alongside the application.",
                "djxl.exe");
        }

        if (!File.Exists(djxlPath))
        {
            throw new FileNotFoundException(
                $"djxl executable not found at: {djxlPath}. Please ensure it is installed alongside the application.",
                djxlPath);
        }

        return djxlPath;
    }

    private static void TryDeleteOutput(string outputPath)
    {
        try
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
        catch
        {
        }
    }

    private static string Quote(string path)
    {
        if (path.Any(c => char.IsWhiteSpace(c) || c == '"'))
        {
            return $"\"{path.Replace("\"", "\\\"")}\"";
        }
        return path;
    }
}

public class JxlDecodingException : Exception
{
    public int ExitCode { get; }

    public JxlDecodingException(string message, int exitCode) : base(message)
    {
        ExitCode = exitCode;
    }

    public JxlDecodingException(string message, int exitCode, Exception innerException) : base(message, innerException)
    {
        ExitCode = exitCode;
    }
}
