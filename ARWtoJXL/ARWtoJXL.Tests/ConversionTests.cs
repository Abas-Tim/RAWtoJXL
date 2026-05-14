using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARWtoJXL.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ARWtoJXL.Tests
{
    [Collection("Conversion")]
    public class ConversionTests : Startup
    {
        private readonly IImageService _imageService;

        public ConversionTests()
        {
            _imageService = Services.GetRequiredService<IImageService>();
        }

        [Fact]
        public async Task GetThumbnailAsync_ValidArw_ReturnsBytes()
        {
            var thumbnail = await _imageService.GetThumbnailAsync(TestArwPath, TestContext.Current.CancellationToken);
            Assert.NotEmpty(thumbnail);
        }

        [Fact]
        public async Task GetThumbnailAsync_InvalidFile_ThrowsException()
        {
            var invalidPath = "non_existent_file.arw";
            await Assert.ThrowsAsync<FileNotFoundException>(() => _imageService.GetThumbnailAsync(invalidPath, TestContext.Current.CancellationToken));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(50)]
        [InlineData(70)]
        [InlineData(90)]
        [InlineData(100)]
        public async Task ConvertToJxlAsync_VariousQualitySettings_CreatesJxlFile(int quality)
        {
            var outputPath = GetOutputPath($"quality{quality}");
            await CleanOutputFile(outputPath);

            await _imageService.ConvertToJxlAsync(TestArwPath, outputPath, p => { }, quality, OutputFormat.Jxl, TestContext.Current.CancellationToken);

            Assert.True(File.Exists(outputPath));
            Assert.True(new FileInfo(outputPath).Length > 0);
        }

        [Fact]
        public async Task ConvertToJxlAsync_ProgressCallback_ReportsSmoothProgress()
        {
            var outputPath = GetOutputPath("progress");
            await CleanOutputFile(outputPath);

            var progressValues = new List<double>();
            var lockObj = new object();
            await _imageService.ConvertToJxlAsync(
                TestArwPath,
                outputPath,
                p => { lock (lockObj) progressValues.Add(p); },
                50,
                OutputFormat.Jxl,
                TestContext.Current.CancellationToken);

            Assert.True(File.Exists(outputPath));
            Assert.True(progressValues.Count > 2, $"Expected multiple progress updates, got {progressValues.Count}");

            lock (lockObj)
            {
                Assert.True(progressValues.Any(v => v >= 0.05 && v <= 0.15), "Should report progress at metadata stage (~0.1)");
                Assert.True(progressValues.Any(v => v >= 0.25 && v <= 0.35), "Should report progress at RGB extraction stage (~0.3)");
                Assert.True(progressValues.Any(v => v >= 0.35 && v < 1.0), "Should report smooth progress during cjxl encoding");
                Assert.True(progressValues.Any(v => v >= 1.0), "Should report final progress of 1.0");

                for (int i = 1; i < progressValues.Count; i++)
                {
                    Assert.True(progressValues[i] >= progressValues[i - 1],
                        $"Progress should be monotonically increasing: {progressValues[i - 1]} -> {progressValues[i]}");
                }
            }
        }
    }
}
