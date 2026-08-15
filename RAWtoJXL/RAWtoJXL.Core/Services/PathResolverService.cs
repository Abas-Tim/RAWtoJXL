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
            string toolInAppDir = Path.Combine(appDir, toolFileName);
            if (File.Exists(toolInAppDir))
            {
                return toolInAppDir;
            }

            string exeDir = Path.GetDirectoryName(Environment.ProcessPath) ?? appDir;
            string toolInExeDir = Path.Combine(exeDir, toolFileName);
            if (File.Exists(toolInExeDir))
            {
                return toolInExeDir;
            }

            return Path.GetFileNameWithoutExtension(toolFileName);
        }
    }
}
