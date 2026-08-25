using RAWtoJXL.Core.Services;
using RAWtoJXL.Core.Interfaces;
using Xunit;

public class RawSpeedBenchTests
{
    private sealed class NullLogger : ILogger
    {
        public void Write(string message) => Console.WriteLine(message);
        public void Clear() { }
    }

    private sealed class NullFileService : IFileService
    {
        public void DeleteFile(string filePath) { if (File.Exists(filePath)) File.Delete(filePath); }
        public bool FileExists(string filePath) => File.Exists(filePath);
        public long GetFileSize(string filePath) => new FileInfo(filePath).Length;
        public string CombinePaths(string path1, string path2) => Path.Combine(path1, path2);
        public string? SaveBytesToTemp(byte[] data, string extension)
        {
            var p = Path.GetTempFileName() + extension;
            File.WriteAllBytes(p, data);
            return p;
        }
    }

    [Fact]
    public async Task RawSpeedCli_RendersLargeArw_Fast()
    {
        var arw = @"C:\Users\Moshni\Desktop\IMG06154.ARW";
        if (!File.Exists(arw))
        {
            return;
        }

        var staged = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "RawSpeedTools", "rawspeed-cli.exe"));
        Environment.SetEnvironmentVariable("RAWTOJXL_RAWSPEED_CLI", File.Exists(staged) ? staged : null);

        Assert.NotNull(RawSpeedCliRenderer.ResolveExecutable());

        var renderer = new RawSpeedCliRenderer(new NullLogger(), new NullFileService());
        var outPng = Path.Combine(Path.GetTempPath(), $"rs_bench_{Guid.NewGuid():N}.png");
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            await renderer.RenderToPngAsync(arw, outPng, 0, CancellationToken.None);
            sw.Stop();

            Assert.True(File.Exists(outPng));
            Assert.True(sw.Elapsed.TotalSeconds < 30, $"too slow: {sw.Elapsed.TotalSeconds:F1}s");

            var png = File.ReadAllBytes(outPng);
            Assert.True(png.Length > 1_000_000, $"png too small: {png.Length}");
            Assert.Equal(0x89, png[0]);
            int width = (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19];
            int height = (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23];
            Assert.True(width > 8000, $"unexpected width {width}");

            Console.WriteLine($"rawspeed-cli rendered {width}x{height} in {sw.Elapsed.TotalSeconds:F2}s");
        }
        finally
        {
            if (File.Exists(outPng)) File.Delete(outPng);
        }
    }
}
