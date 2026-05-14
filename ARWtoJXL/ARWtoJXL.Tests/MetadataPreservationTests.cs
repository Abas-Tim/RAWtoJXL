using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ARWtoJXL.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ARWtoJXL.Tests
{
    [Collection("Conversion")]
    public class MetadataPreservationTests : Startup
    {
        private readonly IImageConverterService _imageConverterService;
        private readonly IImageService _imageService;

        public MetadataPreservationTests()
        {
            _imageConverterService = Services.GetRequiredService<IImageConverterService>();
            _imageService = Services.GetRequiredService<IImageService>();
        }

        [Fact]
        public async Task ConvertToJxlAsync_Metadata_PreservesExifAndIccProfiles()
        {
            using var inputMetadata = await _imageConverterService.ExtractMetadataProfilesAsync(TestArwPath, TestContext.Current.CancellationToken);

            Assert.NotNull(inputMetadata);
            Assert.True(inputMetadata.HasAny, "Input ARW should have metadata");

            var outputPath = GetOutputPath("metadata_preserved");
            await CleanOutputFile(outputPath);

            await _imageService.ConvertToJxlAsync(TestArwPath, outputPath, p => { }, 90, OutputFormat.Jxl, TestContext.Current.CancellationToken);

            Assert.True(File.Exists(outputPath));

             using var outputMetadata = await _imageConverterService.ExtractMetadataProfilesAsync(outputPath, TestContext.Current.CancellationToken);
            Assert.NotNull(outputMetadata);
            Assert.True(outputMetadata.HasAny, "Output JXL should have metadata");

            if (!string.IsNullOrEmpty(inputMetadata.ExifPath) && File.Exists(inputMetadata.ExifPath))
            {
                Assert.NotNull(outputMetadata.ExifPath);
                Assert.True(File.Exists(outputMetadata.ExifPath!));
                var outputExifBytes = File.ReadAllBytes(outputMetadata.ExifPath!);
                Assert.True(outputExifBytes.Length > 0, "Output EXIF should not be empty");
            }

            if (!string.IsNullOrEmpty(inputMetadata.IccPath) && File.Exists(inputMetadata.IccPath))
            {
                Assert.NotNull(outputMetadata.IccPath);
                Assert.True(File.Exists(outputMetadata.IccPath!));
                var outputIccBytes = File.ReadAllBytes(outputMetadata.IccPath!);
                Assert.True(outputIccBytes.Length > 0, "Output ICC should not be empty");
            }
        }
    }
}
