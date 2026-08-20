using System.IO;

namespace RAWtoJXL.Core.Services
{
    public class PathResolverService : Interfaces.IPathResolver
    {
        public string ResolveCjxlPath()
        {
            return ResolveToolPath("cjxl.exe");
        }

        public string ResolveDjxlPath()
        {
            return ResolveToolPath("djxl.exe");
        }

        public string GetTempPath()
        {
            return Path.GetTempPath();
        }

        private static string ResolveToolPath(string toolFileName)
        {
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string exeDir = Path.GetDirectoryName(Environment.ProcessPath) ?? appDir;
            var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                appDir,
                exeDir,
                Directory.GetCurrentDirectory()
            };

            for (var directory = new DirectoryInfo(appDir); directory.Parent != null; directory = directory.Parent)
            {
                directories.Add(directory.FullName);
                directories.Add(directory.Parent.FullName);
            }

            foreach (var directory in directories)
            {
                string candidate = Path.Combine(directory, toolFileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            string pathVariable = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (var directory in pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                string candidate = Path.Combine(directory.Trim(), toolFileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return toolFileName;
        }
    }
}
