using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARWtoJXL.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ARWtoJXL.Tests
{
    public class MetadataPreservationTests : Startup
    {
        private readonly IMagickService _magickService;
        private readonly IImageService _imageService;

        public MetadataPreservationTests()
        {
            _magickService = Services.GetRequiredService<IMagickService>();
            _imageService = Services.GetRequiredService<IImageService>();
        }

        [Fact]
        public async Task ConvertArwToJxlAsync_Metadata_TransfersExifProfile()
        {
            using var inputMetadata = await _magickService.ExtractMetadataProfilesAsync(TestArwPath);

            Assert.NotNull(inputMetadata);
            Assert.NotNull(inputMetadata.ExifPath);
            Assert.True(File.Exists(inputMetadata.ExifPath!));

            var outputPath = GetOutputPath("metadata_transfer");
            await CleanOutputFile(outputPath);

            await _imageService.ConvertArwToJxlAsync(TestArwPath, outputPath, p => { }, 90, CancellationToken.None);

            Assert.True(File.Exists(outputPath));

            using var outputMetadata = await _magickService.ExtractMetadataProfilesAsync(outputPath);
            Assert.NotNull(outputMetadata);
            Assert.NotNull(outputMetadata.ExifPath);
            Assert.True(File.Exists(outputMetadata.ExifPath!));

            var outputExifBytes = File.ReadAllBytes(outputMetadata.ExifPath!);
            Assert.True(outputExifBytes.Length > 0, "Output EXIF should not be empty");
        }

        [Fact]
        public async Task ConvertArwToJxlAsync_Metadata_PreservesIccProfile()
        {
            using var inputMetadata = await _magickService.ExtractMetadataProfilesAsync(TestArwPath);

            Assert.NotNull(inputMetadata);

            var outputPath = GetOutputPath("icc_preserved");
            await CleanOutputFile(outputPath);

            await _imageService.ConvertArwToJxlAsync(TestArwPath, outputPath, p => { }, 90, CancellationToken.None);

            Assert.True(File.Exists(outputPath));

            using var outputMetadata = await _magickService.ExtractMetadataProfilesAsync(outputPath);
            Assert.NotNull(outputMetadata);

            if (!string.IsNullOrEmpty(inputMetadata.IccPath) && File.Exists(inputMetadata.IccPath))
            {
                Assert.NotNull(outputMetadata.IccPath);
                Assert.True(File.Exists(outputMetadata.IccPath!));

                var inputIccBytes = File.ReadAllBytes(inputMetadata.IccPath!);
                var outputIccBytes = File.ReadAllBytes(outputMetadata.IccPath!);
                Assert.True(outputIccBytes.Length > 0, "Output ICC should not be empty");
            }
        }

        [Fact]
        public async Task ConvertArwToJxlAsync_Metadata_HasAnyPropertyReturnsTrueWhenMetadataExists()
        {
            using var metadata = await _magickService.ExtractMetadataProfilesAsync(TestArwPath);

            Assert.NotNull(metadata);
            Assert.True(metadata.HasAny, "Input ARW should have metadata");

            var outputPath = GetOutputPath("hasany_verify");
            await CleanOutputFile(outputPath);

            await _imageService.ConvertArwToJxlAsync(TestArwPath, outputPath, p => { }, 90, CancellationToken.None);

            Assert.True(File.Exists(outputPath));

            using var outputMetadata = await _magickService.ExtractMetadataProfilesAsync(outputPath);
            Assert.NotNull(outputMetadata);
            Assert.True(outputMetadata.HasAny, "Output JXL should have metadata when input ARW has metadata");
        }

        [Fact]
        public async Task ConvertArwToJxlAsync_Metadata_VerifiesExifTransferredToOutput()
        {
            using var inputMetadata = await _magickService.ExtractMetadataProfilesAsync(TestArwPath);

            Assert.NotNull(inputMetadata);
            Assert.NotNull(inputMetadata.ExifPath);

            var outputPath = GetOutputPath("exif_verify");
            await CleanOutputFile(outputPath);

            await _imageService.ConvertArwToJxlAsync(TestArwPath, outputPath, p => { }, 90, CancellationToken.None);

            Assert.True(File.Exists(outputPath));

            using var outputMetadata = await _magickService.ExtractMetadataProfilesAsync(outputPath);
            Assert.NotNull(outputMetadata);
            Assert.NotNull(outputMetadata.ExifPath);
            Assert.True(File.Exists(outputMetadata.ExifPath!));

            var inputExifBytes = File.ReadAllBytes(inputMetadata.ExifPath!);
            var outputExifBytes = File.ReadAllBytes(outputMetadata.ExifPath!);
            Assert.True(outputExifBytes.Length > 0, "Output EXIF should not be empty");
        }

        [Fact]
        public async Task ConvertArwToJxlAsync_Metadata_VerifiesIccTransferredToOutput()
        {
            using var inputMetadata = await _magickService.ExtractMetadataProfilesAsync(TestArwPath);

            Assert.NotNull(inputMetadata);

            var outputPath = GetOutputPath("icc_verify");
            await CleanOutputFile(outputPath);

            await _imageService.ConvertArwToJxlAsync(TestArwPath, outputPath, p => { }, 90, CancellationToken.None);

            Assert.True(File.Exists(outputPath));

            using var outputMetadata = await _magickService.ExtractMetadataProfilesAsync(outputPath);
            Assert.NotNull(outputMetadata);

            if (!string.IsNullOrEmpty(inputMetadata.IccPath) && File.Exists(inputMetadata.IccPath))
            {
                Assert.NotNull(outputMetadata.IccPath);
                Assert.True(File.Exists(outputMetadata.IccPath!));

                var inputIccBytes = File.ReadAllBytes(inputMetadata.IccPath!);
                var outputIccBytes = File.ReadAllBytes(outputMetadata.IccPath!);
                Assert.True(outputIccBytes.Length > 0, "Output ICC should not be empty");
            }
        }

        [Theory]
        [InlineData(90)]
        [InlineData(100)]
        public async Task ConvertArwToJxlAsync_Metadata_TransferredAtQualityLevel(int quality)
        {
            using var inputMetadata = await _magickService.ExtractMetadataProfilesAsync(TestArwPath);

            Assert.NotNull(inputMetadata);
            Assert.True(inputMetadata.HasAny, "Input ARW should have metadata for quality level test");

            var outputPath = GetOutputPath($"metadata_q{quality}");
            await CleanOutputFile(outputPath);

            await _imageService.ConvertArwToJxlAsync(TestArwPath, outputPath, p => { }, quality, CancellationToken.None);

            Assert.True(File.Exists(outputPath));

            using var outputMetadata = await _magickService.ExtractMetadataProfilesAsync(outputPath);
            Assert.NotNull(outputMetadata);
            Assert.True(outputMetadata.HasAny, $"Output JXL should have metadata at quality level {quality}");

            if (!string.IsNullOrEmpty(inputMetadata.IccPath) && File.Exists(inputMetadata.IccPath))
            {
                Assert.NotNull(outputMetadata.IccPath);
                Assert.True(File.Exists(outputMetadata.IccPath!));

                var outputIccBytes = File.ReadAllBytes(outputMetadata.IccPath!);
                Assert.True(outputIccBytes.Length > 0, "Output ICC should not be empty");
            }

            if (!string.IsNullOrEmpty(inputMetadata.ExifPath) && File.Exists(inputMetadata.ExifPath))
            {
                Assert.NotNull(outputMetadata.ExifPath);
                Assert.True(File.Exists(outputMetadata.ExifPath!));

                var outputExifBytes = File.ReadAllBytes(outputMetadata.ExifPath!);
                Assert.True(outputExifBytes.Length > 0, "Output EXIF should not be empty");
            }
        }
    }
}
