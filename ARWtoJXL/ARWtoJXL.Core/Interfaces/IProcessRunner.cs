using System.IO;
using System.Threading;

namespace ARWtoJXL.Core.Interfaces;

public interface IProcessRunner
{
    Task<string?> FindExiftoolAsync(string? logPrefix = null);
    Task<bool> IsExiftoolWorkingAsync(string exiftoolPath, string? logPrefix = null);
    Task<(int ExitCode, string? Stdout, string? Stderr)> RunProcessAsync(string fileName, string arguments, CancellationToken cancellationToken = default);
    Task<(int ExitCode, string? Stdout, string? Stderr, bool TimedOut)> RunProcessWithTimeoutAsync(string fileName, string arguments, int timeoutSeconds, CancellationToken cancellationToken = default);
    Task<byte[]?> RunProcessBinaryAsync(string fileName, string arguments, CancellationToken cancellationToken = default);
    Task<(int ExitCode, string? Stdout, string? Stderr, bool TimedOut)> RunProcessWithStdinAsync(string fileName, string arguments, Stream stdinStream, int timeoutSeconds, CancellationToken cancellationToken = default);
}
