using System;
using System.Threading;
using System.Threading.Tasks;
using ARWtoJXL.Core.Models;

namespace ARWtoJXL.Core.Services
{
    public class ImageProcessingService : IImageService
    {
        private readonly Interfaces.IMagickService _magickService;
        private readonly Interfaces.ICjxlEncoder _cjxlEncoder;
        private readonly Interfaces.IFileService _fileService;
        private readonly Interfaces.IPathResolver _pathResolver;

        public ImageProcessingService(
            Interfaces.IMagickService magickService,
            Interfaces.ICjxlEncoder cjxlEncoder,
            Interfaces.IFileService fileService,
            Interfaces.IPathResolver pathResolver)
        {
            _magickService = magickService;
            _cjxlEncoder = cjxlEncoder;
            _fileService = fileService;
            _pathResolver = pathResolver;
        }

        public async Task<byte[]> GetThumbnailAsync(string filePath, CancellationToken cancellationToken = default)
        {
            return await _magickService.ExtractThumbnailAsync(filePath, cancellationToken);
        }

        public async Task ConvertArwToJxlAsync(
            string inputPath, 
            string outputPath, 
            Action<double> progress, 
            int quality, 
            OutputFormat outputFormat, 
            CancellationToken cancellationToken = default)
        {
            await Task.Run(() =>
            {
                Models.MetadataProfiles? metadata = null;
                string tempPngPath = _fileService.GetTempFileName();

                try
                {
                    progress?.Invoke(0.1);

                    try
                    {
                        metadata = _magickService.ExtractMetadataProfilesAsync(inputPath, cancellationToken).Result;
                    }
                    catch (Exception ex)
                    {
                        Logger.Write($"[ImageProcessing] Metadata extraction failed: {ex.GetBaseException().Message}");
                        // Continue without metadata - don't fail the conversion
                    }
                    if (metadata != null)
                    {
                        Logger.Write($"[ImageProcessing] Metadata extracted: Exif={metadata?.ExifPath ?? "none"}, Xmp={metadata?.XmpPath ?? "none"}, Icc={metadata?.IccPath ?? "none"}, Iptc={metadata?.IptcPath ?? "none"}, HasAny={metadata?.HasAny}");
                    }

                    _magickService.ConvertToPngAsync(inputPath, tempPngPath, cancellationToken).Wait(cancellationToken);
                    progress?.Invoke(0.5);

                    _cjxlEncoder.EncodeAsync(tempPngPath, inputPath, outputPath, quality, metadata, cancellationToken).Wait(cancellationToken);
                    progress?.Invoke(1.0);

                    if (!_fileService.FileExists(outputPath))
                    {
                        throw new FileNotFoundException($"Conversion completed but output file not found at: {outputPath}");
                    }
                }
                finally
                {
                    metadata?.Dispose();
                    _fileService.DeleteFile(tempPngPath);
                }
            }, cancellationToken);
        }
    }
}
