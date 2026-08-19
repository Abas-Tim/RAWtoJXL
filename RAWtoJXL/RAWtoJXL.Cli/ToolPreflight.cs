using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using RAWtoJXL.Core.Interfaces;
using RAWtoJXL.Core.Models;

namespace RAWtoJXL.Cli
{
    internal static class ToolPreflight
    {
        public static async Task<bool> VerifyAsync(IServiceProvider services, TextWriter stderr, IReadOnlyList<string> files)
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

            if (files.Any(f => SupportedFormats.IsJxlFile(Path.GetExtension(f))))
            {
                var djxl = pathResolver.ResolveDjxlPath();
                if (!File.Exists(djxl))
                {
                    if (Path.GetFileName(djxl) != djxl || FindOnPath(djxl) == null)
                    {
                        await stderr.WriteLineAsync("error: djxl.exe not found. JXL inputs require djxl.exe; place it next to the executable or add it to PATH.");
                        return false;
                    }
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

                if (!fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    var withExe = candidate + ".exe";
                    if (File.Exists(withExe))
                    {
                        return withExe;
                    }
                }
            }
            return null;
        }
    }
}
