using System;
using System.Diagnostics;
using System.IO;

namespace ARWtoJXL.Core.Services
{
    public static class ProcessHelper
    {
        private static readonly string[] CommonExiftoolPaths =
        [
            @"C:\Program Files\exiftool.exe",
            @"C:\Program Files (x86)\exiftool.exe",
            @"F:\Downloads\exiftoolgui516\exiftoolgui\exiftool.exe",
            @"C:\Users\Public\exiftool.exe"
        ];

        public static string? FindExiftool(string? logPrefix = null)
        {
            string prefix = $"[{logPrefix ?? "ProcessHelper"}]";

            foreach (var path in CommonExiftoolPaths)
            {
                if (File.Exists(path) && IsExiftoolWorking(path, prefix))
                {
                    Logger.Write($"{prefix} Found exiftool at: {path}");
                    return path;
                }
            }

            var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (var dir in pathEnv.Split(';'))
            {
                var candidate = Path.Combine(dir, "exiftool.exe");
                if (File.Exists(candidate) && IsExiftoolWorking(candidate, prefix))
                {
                    Logger.Write($"{prefix} Using PATH exiftool: {candidate}");
                    return candidate;
                }
            }

            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            if (!string.IsNullOrEmpty(appDir))
            {
                var local = Path.Combine(appDir, "exiftool.exe");
                if (File.Exists(local) && IsExiftoolWorking(local, prefix))
                {
                    Logger.Write($"{prefix} Using local exiftool: {local}");
                    return local;
                }
            }

            return null;
        }

        public static bool IsExiftoolWorking(string exiftoolPath, string? logPrefix = null)
        {
            string prefix = $"[{logPrefix ?? "ProcessHelper"}]";

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = exiftoolPath,
                    Arguments = "-ver",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process == null) return false;

                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                var success = process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output) && output[0] switch
                {
                    >= '0' and <= '9' => true,
                    _ => false
                };

                if (!success)
                {
                    Logger.Write($"{prefix} exiftool version check failed: exit={process.ExitCode}, stdout='{output?.Trim()}', stderr='{error?.Trim()}'");
                }
                else
                {
                    Logger.Write($"{prefix} exiftool version check passed: {output?.Trim()}");
                }

                return success;
            }
            catch (Exception ex)
            {
                Logger.Write($"{prefix} exiftool version check threw: {ex.Message}");
                return false;
            }
        }

        public static async Task<(int ExitCode, string? Stdout, string? Stderr)> RunProcessAsync(
            string fileName,
            string arguments,
            System.Threading.CancellationToken cancellationToken = default)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return (-1, null, null);
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            var waitTask = process.WaitForExitAsync(cancellationToken);

            await waitTask;

            return (process.ExitCode, await stdoutTask, await stderrTask);
        }

        public static byte[]? RunProcessBinaryAsync(
            string fileName,
            string arguments,
            System.Threading.CancellationToken cancellationToken = default)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return null;
            }

            using var ms = new MemoryStream();
            process.StandardOutput.BaseStream.CopyTo(ms);
            process.WaitForExit();

            byte[]? result = ms.ToArray();
            return result.Length > 0 ? result : null;
        }
    }
}
