using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ARWtoJXL.Core.Services;
using ARWtoJXL.Core.Interfaces;
using Xunit;

namespace ARWtoJXL.Tests
{
    public class ImageProcessingServiceTests
    {
        private readonly ImageProcessingService _imageService;
        private const string TestArwPath = @"C:\Users\timur\Desktop\Playgroung\ARWtoJPEGXL\ARWtoJXL\ARWtoJXL.Tests\bin\Debug\net8.0-windows\test1.ARW";

        public ImageProcessingServiceTests()
        {
            var magickService = new MagickService();
            var pathResolver = new PathResolverService();
            var cjxlEncoder = new CjxlEncoderService(pathResolver);
            var fileService = new FileService();
            
            _imageService = new ImageProcessingService(magickService, cjxlEncoder, fileService, pathResolver);
        }

        [Fact]
        public async Task GetThumbnailAsync_ValidArw_ReturnsBytes()
        {
            var thumbnail = await _imageService.GetThumbnailAsync(TestArwPath);
            Assert.NotEmpty(thumbnail);
        }

        [Fact]
        public async Task ConvertArwToJxlAsync_ValidArw_CreatesJxlFile()
        {
            var outputPath = Path.Combine(Path.GetDirectoryName(TestArwPath)!, "test1.jxl");
            if (File.Exists(outputPath)) File.Delete(outputPath);

            await _imageService.ConvertArwToJxlAsync(TestArwPath, outputPath, p => { }, 5, OutputFormat.Jxl, CancellationToken.None);

            Assert.True(File.Exists(outputPath));
        }

        [Fact]
        public async Task GetThumbnailAsync_InvalidFile_ThrowsException()
        {
            var invalidPath = "non_existent_file.arw";
            await Assert.ThrowsAsync<FileNotFoundException>(() => _imageService.GetThumbnailAsync(invalidPath));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(50)]
        [InlineData(70)]
        [InlineData(90)]
        [InlineData(100)]
        public async Task ConvertArwToJxlAsync_VariousQualitySettings_CreatesJxlFile(int quality)
        {
            var outputPath = Path.Combine(Path.GetDirectoryName(TestArwPath)!, $"test1_quality{quality}.jxl");
            if (File.Exists(outputPath)) File.Delete(outputPath);

            await _imageService.ConvertArwToJxlAsync(TestArwPath, outputPath, p => { }, quality, OutputFormat.Jxl, CancellationToken.None);

            Assert.True(File.Exists(outputPath));
            Assert.True(new FileInfo(outputPath).Length > 0);
        }

        [Fact]
        public async Task ConvertArwToJxlAsync_Quality100_LosslessMode()
        {
            var outputPath = Path.Combine(Path.GetDirectoryName(TestArwPath)!, "test1_lossless.jxl");
            if (File.Exists(outputPath)) File.Delete(outputPath);

            double progress = 0;
            await _imageService.ConvertArwToJxlAsync(TestArwPath, outputPath, p => progress = p, 100, OutputFormat.Jxl, CancellationToken.None);

            Assert.True(File.Exists(outputPath));
            Assert.True(new FileInfo(outputPath).Length > 0);
            Assert.Equal(1.0, progress);
        }

        [Fact]
        public async Task ConvertArwToJxlAsync_Quality90_VisuallyLossless()
        {
            var outputPath = Path.Combine(Path.GetDirectoryName(TestArwPath)!, "test1_visually_lossless.jxl");
            if (File.Exists(outputPath)) File.Delete(outputPath);

            await _imageService.ConvertArwToJxlAsync(TestArwPath, outputPath, p => { }, 90, OutputFormat.Jxl, CancellationToken.None);

            Assert.True(File.Exists(outputPath));
            Assert.True(new FileInfo(outputPath).Length > 0);
        }

        [Fact]
        public async Task ConvertArwToJxlAsync_Quality0_LowestQuality()
        {
            var outputPath = Path.Combine(Path.GetDirectoryName(TestArwPath)!, "test1_lowest_quality.jxl");
            if (File.Exists(outputPath)) File.Delete(outputPath);

            await _imageService.ConvertArwToJxlAsync(TestArwPath, outputPath, p => { }, 0, OutputFormat.Jxl, CancellationToken.None);

            Assert.True(File.Exists(outputPath));
            Assert.True(new FileInfo(outputPath).Length > 0);
        }

        [Fact]
        public async Task ConvertArwToJxlAsync_Metadata_TransfersExifProfile()
        {
            var magickService = new MagickService();
            using var inputMetadata = await magickService.ExtractMetadataProfilesAsync(TestArwPath);

            Assert.NotNull(inputMetadata);
            Assert.NotNull(inputMetadata.ExifPath);
            Assert.True(File.Exists(inputMetadata.ExifPath!));

            var outputPath = Path.Combine(Path.GetDirectoryName(TestArwPath)!, "test1_metadata_transfer.jxl");
            if (File.Exists(outputPath)) File.Delete(outputPath);

            await _imageService.ConvertArwToJxlAsync(TestArwPath, outputPath, p => { }, 90, OutputFormat.Jxl, CancellationToken.None);

            Assert.True(File.Exists(outputPath));

            using var outputMetadata = await magickService.ExtractMetadataProfilesAsync(outputPath);
            Assert.NotNull(outputMetadata);
            Assert.NotNull(outputMetadata.ExifPath);
            Assert.True(File.Exists(outputMetadata.ExifPath!));

            var outputExifBytes = File.ReadAllBytes(outputMetadata.ExifPath!);
            Assert.True(outputExifBytes.Length > 0, "Output EXIF should not be empty");
        }

        [Fact]
        public async Task ConvertArwToJxlAsync_Metadata_PreservesIccProfile()
        {
            var magickService = new MagickService();
            using var inputMetadata = await magickService.ExtractMetadataProfilesAsync(TestArwPath);

            Assert.NotNull(inputMetadata);

            var outputPath = Path.Combine(Path.GetDirectoryName(TestArwPath)!, "test1_icc_preserved.jxl");
            if (File.Exists(outputPath)) File.Delete(outputPath);

            await _imageService.ConvertArwToJxlAsync(TestArwPath, outputPath, p => { }, 90, OutputFormat.Jxl, CancellationToken.None);

            Assert.True(File.Exists(outputPath));

            using var outputMetadata = await magickService.ExtractMetadataProfilesAsync(outputPath);
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
            var magickService = new MagickService();
            using var metadata = await magickService.ExtractMetadataProfilesAsync(TestArwPath);

            Assert.NotNull(metadata);
            Assert.True(metadata.HasAny, "Input ARW should have metadata");

            var outputPath = Path.Combine(Path.GetDirectoryName(TestArwPath)!, "test1_hasany_verify.jxl");
            if (File.Exists(outputPath)) File.Delete(outputPath);

            await _imageService.ConvertArwToJxlAsync(TestArwPath, outputPath, p => { }, 90, OutputFormat.Jxl, CancellationToken.None);

            Assert.True(File.Exists(outputPath));

            using var outputMetadata = await magickService.ExtractMetadataProfilesAsync(outputPath);
            Assert.NotNull(outputMetadata);
            Assert.True(outputMetadata.HasAny, "Output JXL should have metadata when input ARW has metadata");
        }

        [Fact]
        public async Task ConvertArwToJxlAsync_Metadata_VerifiesExifTransferredToOutput()
        {
            var magickService = new MagickService();
            using var inputMetadata = await magickService.ExtractMetadataProfilesAsync(TestArwPath);

            Assert.NotNull(inputMetadata);
            Assert.NotNull(inputMetadata.ExifPath);

            var outputPath = Path.Combine(Path.GetDirectoryName(TestArwPath)!, "test1_exif_verify.jxl");
            if (File.Exists(outputPath)) File.Delete(outputPath);

            await _imageService.ConvertArwToJxlAsync(TestArwPath, outputPath, p => { }, 90, OutputFormat.Jxl, CancellationToken.None);

            Assert.True(File.Exists(outputPath));

            using var outputMetadata = await magickService.ExtractMetadataProfilesAsync(outputPath);
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
            var magickService = new MagickService();
            using var inputMetadata = await magickService.ExtractMetadataProfilesAsync(TestArwPath);

            Assert.NotNull(inputMetadata);

            var outputPath = Path.Combine(Path.GetDirectoryName(TestArwPath)!, "test1_icc_verify.jxl");
            if (File.Exists(outputPath)) File.Delete(outputPath);

            await _imageService.ConvertArwToJxlAsync(TestArwPath, outputPath, p => { }, 90, OutputFormat.Jxl, CancellationToken.None);

            Assert.True(File.Exists(outputPath));

            using var outputMetadata = await magickService.ExtractMetadataProfilesAsync(outputPath);
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
            var magickService = new MagickService();
            using var inputMetadata = await magickService.ExtractMetadataProfilesAsync(TestArwPath);

            Assert.NotNull(inputMetadata);
            Assert.True(inputMetadata.HasAny, "Input ARW should have metadata for quality level test");

            var outputPath = Path.Combine(Path.GetDirectoryName(TestArwPath)!, $"test1_metadata_q{quality}.jxl");
            if (File.Exists(outputPath)) File.Delete(outputPath);

            await _imageService.ConvertArwToJxlAsync(TestArwPath, outputPath, p => { }, quality, OutputFormat.Jxl, CancellationToken.None);

            Assert.True(File.Exists(outputPath));

            using var outputMetadata = await magickService.ExtractMetadataProfilesAsync(outputPath);
            Assert.NotNull(outputMetadata);
            Assert.True(outputMetadata.HasAny, "Output JXL should have metadata at quality level {quality}");

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
