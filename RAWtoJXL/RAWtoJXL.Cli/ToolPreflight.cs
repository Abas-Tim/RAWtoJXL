using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using RAWtoJXL.Core.Interfaces;

namespace RAWtoJXL.Cli
{
    internal static class ToolPreflight
    {
        public static async Task<bool> VerifyAsync(IServiceProvider services, TextWriter stderr)
        {
            var pathResolver = services.GetRequiredService<IPathResolver>();
            var processRunner = services.GetRequiredService<IProcessRunner>();

            var cjxl = pathResolver.ResolveCjxlPath();
            if (!File.Exists(cjxl))
            {
                if (Path.GetFileName(cjxl) != cjxl || FindOnPath(cjxl) == null)
                {
                    await stderr.WriteLineAsync("error: cjxl.exe not found. Place it next to the executable or add it to PATH.");
                    return false;
                }
            }

            var exiftool = await processRunner.FindExiftoolAsync();
            if (exiftool == null)
            {
                await stderr.WriteLineAsync("error: exiftool.exe not found or not working. Place it next to the executable or add it to PATH.");
                return false;
            }

            return true;
        }

        private static string? FindOnPath(string fileName)
        {
            var pathVariable = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (var directory in pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = Path.Combine(directory.Trim(), fileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            return null;
        }
    }
}
