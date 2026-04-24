using System.Threading;

namespace ARWtoJXL.Core.Interfaces;

public interface IProcessRunner
{
    string? FindExiftool(string? logPrefix = null);
    bool IsExiftoolWorking(string exiftoolPath, string? logPrefix = null);
    Task<(int ExitCode, string? Stdout, string? Stderr)> RunProcessAsync(string fileName, string arguments, CancellationToken cancellationToken = default);
    Task<(int ExitCode, string? Stdout, string? Stderr, bool TimedOut)> RunProcessWithTimeoutAsync(string fileName, string arguments, int timeoutSeconds, CancellationToken cancellationToken = default);
    Task<byte[]?> RunProcessBinaryAsync(string fileName, string arguments, CancellationToken cancellationToken = default);
}
