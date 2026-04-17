using System.IO;

namespace ARWtoJXL.Core.Services
{
    public class PathResolverService : Interfaces.IPathResolver
    {
        public string ResolveCjxlPath()
        {
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string cjxlInAppDir = Path.Combine(appDir, "cjxl.exe");
            if (File.Exists(cjxlInAppDir))
            {
                return cjxlInAppDir;
            }

            string exeDir = Path.GetDirectoryName(Environment.ProcessPath) ?? appDir;
            string cjxlInExeDir = Path.Combine(exeDir, "cjxl.exe");
            if (File.Exists(cjxlInExeDir))
            {
                return cjxlInExeDir;
            }

            return "cjxl";
        }

        public string GetTempPath()
        {
            return Path.GetTempPath();
        }
    }
}
