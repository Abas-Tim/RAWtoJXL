namespace ARWtoJXL.Core.Interfaces;

public interface IProcessRunner
{
    string? FindExiftool(string? logPrefix = null);
    bool IsExiftoolWorking(string exiftoolPath, string? logPrefix = null);
    Task<(int ExitCode, string? Stdout, string? Stderr)> RunProcessAsync(string fileName, string arguments, System.Threading.CancellationToken cancellationToken = default);
    byte[]? RunProcessBinaryAsync(string fileName, string arguments, System.Threading.CancellationToken cancellationToken = default);
}
