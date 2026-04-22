using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ARWtoJXL.Core.Interfaces;
using ARWtoJXL.Core.Models;

namespace ARWtoJXL.Core.Services;

public class ImageProcessingService : IImageService
{
    private readonly IMagickService _magickService;
    private readonly ICjxlEncoder _cjxlEncoder;
    private readonly IFileService _fileService;
    private readonly IPathResolver _pathResolver;
    private readonly ISizeEstimator _sizeEstimator;
    private readonly ILogger _logger;

    public ImageProcessingService(
        IMagickService magickService,
        ICjxlEncoder cjxlEncoder,
        IFileService fileService,
        IPathResolver pathResolver,
        ISizeEstimator sizeEstimator,
        ILogger logger)
    {
        _magickService = magickService ?? throw new ArgumentNullException(nameof(magickService));
        _cjxlEncoder = cjxlEncoder ?? throw new ArgumentNullException(nameof(cjxlEncoder));
        _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
        _pathResolver = pathResolver ?? throw new ArgumentNullException(nameof(pathResolver));
        _sizeEstimator = sizeEstimator ?? throw new ArgumentNullException(nameof(sizeEstimator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
        CancellationToken cancellationToken = default)
    {
        MetadataProfiles? metadata = null;
        string tempPngPath = _fileService.GetTempFileName();

        try
        {
            progress?.Invoke(0.1);

            try
            {
                metadata = await _magickService.ExtractMetadataProfilesAsync(inputPath, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.Write($"[ImageProcessing] Metadata extraction failed: {ex.GetBaseException().Message}");
            }
            if (metadata != null)
            {
                _logger.Write($"[ImageProcessing] Metadata extracted: Exif={metadata?.ExifPath ?? "none"}, Xmp={metadata?.XmpPath ?? "none"}, Icc={metadata?.IccPath ?? "none"}, Iptc={metadata?.IptcPath ?? "none"}, HasAny={metadata?.HasAny}");
            }

            await _magickService.ConvertToPngAsync(inputPath, tempPngPath, cancellationToken);
            progress?.Invoke(0.5);

            await _cjxlEncoder.EncodeAsync(
                tempPngPath,
                inputPath,
                outputPath,
                quality,
                metadata,
                cancellationToken,
                progress: cjxlProgress => progress?.Invoke(0.5 + cjxlProgress * 0.5));

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
    }

    public async Task<long> EstimateSizeAsync(string arwPath, int quality, CancellationToken cancellationToken = default)
    {
        string tempPngPath = _fileService.GetTempFileName();

        try
        {
            await _magickService.ConvertToPngAsync(arwPath, tempPngPath, cancellationToken);

            if (!_fileService.FileExists(tempPngPath))
                return 0;

            long pngSize = _fileService.GetFileSize(tempPngPath);
            return _sizeEstimator.Estimate(pngSize, quality);
        }
        catch (Exception)
        {
            return 0;
        }
        finally
        {
            _fileService.DeleteFile(tempPngPath);
        }
    }
}
