using System.IO;
using Moq;
using RAWtoJXL.Core.Interfaces;
using RAWtoJXL.Core.Services;

namespace RAWtoJXL.Tests;

public class PreferredRawRendererTests
{
    [Fact]
    public async Task ResolveAndRender_FallsBackToMagick_WhenRawSpeedMissing()
    {
        string? saved = Environment.GetEnvironmentVariable("RAWTOJXL_RAWSPEED_CLI");
        var logger = new Mock<ILogger>();
        try
        {
            Environment.SetEnvironmentVariable("RAWTOJXL_RAWSPEED_CLI", null);
            if (RawSpeedCliRenderer.ResolveExecutable() != null)
            {
                return;
            }

            string dir = Path.Combine(Path.GetTempPath(), "RAWtoJXL_PrefRender_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            string input = Path.Combine(dir, "input.dng");
            File.Copy(TestAssetGenerator.AssetPath, input);
            string output = Path.Combine(dir, "master.png");

            var renderer = new PreferredRawRenderer(logger.Object);
            await renderer.RenderToPngAsync(input, output, 24, TestContext.Current.CancellationToken);

            Assert.True(File.Exists(output));
            using var image = new ImageMagick.MagickImage(output);
            Assert.True(image.Width >= 1500);
            Assert.True(image.Height >= 1100);

            Directory.Delete(dir, true);
        }
        finally
        {
            Environment.SetEnvironmentVariable("RAWTOJXL_RAWSPEED_CLI", saved);
        }
    }
}
