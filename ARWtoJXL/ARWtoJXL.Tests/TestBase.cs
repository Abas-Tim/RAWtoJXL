using System.Diagnostics;
using System.IO;
using System.Reflection;
using ARWtoJXL.Core.Interfaces;
using ARWtoJXL.Core.Services;

namespace ARWtoJXL.Tests
{
    public abstract class TestBase
    {
        protected static readonly string TestArwPath = GetTestArwPath();

        private static string GetTestArwPath()
        {
            var assemblyLocation = Assembly.GetExecutingAssembly().Location;
            var assemblyDir = Path.GetDirectoryName(assemblyLocation) ?? Directory.GetCurrentDirectory();
            var testFile = Path.Combine(assemblyDir, "test1.ARW");

            if (File.Exists(testFile))
                return testFile;

            throw new InvalidOperationException($"Test ARW file not found at: {testFile}");
        }

        protected static IImageService CreateImageService()
        {
            var magickService = new MagickService();
            var pathResolver = new PathResolverService();
            var cjxlEncoder = new CjxlEncoderService(pathResolver);
            var fileService = new FileService();
            var sizeEstimator = new SizeEstimatorService();

            return new ImageProcessingService(magickService, cjxlEncoder, fileService, pathResolver, sizeEstimator);
        }

        protected static async Task CleanOutputFile(string outputPath)
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }

        protected static string GetOutputPath(string suffix)
        {
            var dir = Path.GetDirectoryName(TestArwPath)!;
            return Path.Combine(dir, $"test1_{suffix}.jxl");
        }

        protected static string? FindExiftoolForTests()
        {
            var commonPaths = new[]
            {
                @"C:\Program Files\exiftool.exe",
                @"C:\Program Files (x86)\exiftool.exe",
                @"F:\Downloads\exiftoolgui516\exiftoolgui\exiftool.exe",
                @"C:\Users\Public\exiftool.exe"
            };

            foreach (var path in commonPaths)
            {
                if (File.Exists(path) && IsExiftoolWorking(path)) return path;
            }

            var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (var dir in pathEnv.Split(';'))
            {
                var candidate = Path.Combine(dir, "exiftool.exe");
                if (File.Exists(candidate) && IsExiftoolWorking(candidate)) return candidate;
            }

            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            if (!string.IsNullOrEmpty(appDir))
            {
                var local = Path.Combine(appDir, "exiftool.exe");
                if (File.Exists(local) && IsExiftoolWorking(local)) return local;
            }

            return null;
        }

        protected static bool IsExiftoolWorking(string exiftoolPath)
        {
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
                process.WaitForExit();

                return process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output) && output[0] >= '0' && output[0] <= '9';
            }
            catch
            {
                return false;
            }
        }
    }
}
