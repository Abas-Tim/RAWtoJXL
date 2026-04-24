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
    private readonly ILogger _logger;
    private readonly IExiftoolService _exiftoolService;
    private readonly IPngCache _pngCache;

    public ImageProcessingService(
        IMagickService magickService,
        ICjxlEncoder cjxlEncoder,
        IFileService fileService,
        IPathResolver pathResolver,
        ILogger logger,
        IExiftoolService exiftoolService,
        IPngCache pngCache)
    {
        _magickService = magickService ?? throw new ArgumentNullException(nameof(magickService));
        _cjxlEncoder = cjxlEncoder ?? throw new ArgumentNullException(nameof(cjxlEncoder));
        _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
        _pathResolver = pathResolver ?? throw new ArgumentNullException(nameof(pathResolver));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _exiftoolService = exiftoolService ?? throw new ArgumentNullException(nameof(exiftoolService));
        _pngCache = pngCache ?? throw new ArgumentNullException(nameof(pngCache));
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
        OutputFormat outputFormat = OutputFormat.Jxl,
        CancellationToken cancellationToken = default)
    {
        if (outputFormat == OutputFormat.Jxl)
        {
            await ConvertToJxlAsync(inputPath, outputPath, progress, quality, cancellationToken);
        }
        else if (outputFormat == OutputFormat.Jpeg)
        {
            await ConvertToJpegAsync(inputPath, outputPath, progress, quality, cancellationToken);
        }
        else if (outputFormat == OutputFormat.Png)
        {
            await ConvertToPngOutputAsync(inputPath, outputPath, progress, cancellationToken);
        }
    }

    private async Task ConvertToJxlAsync(
        string inputPath,
        string outputPath,
        Action<double> progress,
        int quality,
        CancellationToken cancellationToken)
    {
        MetadataProfiles? metadata = null;
        string? cachedPng = _pngCache.GetCachedPng(inputPath);
        string? tempPngPath = null;
        bool usedCache = cachedPng != null;

        try
        {
            progress?.Invoke(0.1);

            metadata = await ExtractMetadataWithLoggingAsync(inputPath, cancellationToken);
            if (metadata != null)
            {
                _logger.Write($"[ImageProcessing] Metadata extracted: Exif={metadata?.ExifPath ?? "none"}, Xmp={metadata?.XmpPath ?? "none"}, Icc={metadata?.IccPath ?? "none"}, Iptc={metadata?.IptcPath ?? "none"}, HasAny={metadata?.HasAny}");
            }

            if (usedCache)
            {
                tempPngPath = cachedPng;
                _logger.Write($"[ImageProcessing] PNG cache hit for {Path.GetFileName(inputPath)}");
                progress?.Invoke(0.5);
            }
            else
            {
                tempPngPath = _fileService.GetTempFileName();
                await _magickService.ConvertToPngAsync(inputPath, tempPngPath, cancellationToken);
                _pngCache.StorePng(inputPath, tempPngPath);
                _logger.Write($"[ImageProcessing] PNG cached for {Path.GetFileName(inputPath)}");
                progress?.Invoke(0.5);
            }

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
            if (!usedCache && tempPngPath != null)
            {
                _fileService.DeleteFile(tempPngPath);
            }
        }
    }

  private async Task ConvertToJpegAsync(
        string inputPath,
        string outputPath,
        Action<double> progress,
        int quality,
        CancellationToken cancellationToken)
    {
        progress?.Invoke(0.1);

        MetadataProfiles? metadata = await ExtractMetadataWithLoggingAsync(inputPath, cancellationToken);

        try
        {
            await _magickService.ConvertToJpegAsync(inputPath, outputPath, quality, cancellationToken);
            progress?.Invoke(0.9);

            if (metadata != null && metadata.HasAny)
            {
                await _exiftoolService.EmbedMetadataAsync(inputPath, outputPath, metadata, cancellationToken);
            }

            progress?.Invoke(1.0);

            if (!_fileService.FileExists(outputPath))
            {
                throw new FileNotFoundException($"Conversion completed but output file not found at: {outputPath}");
            }
        }
        finally
        {
            metadata?.Dispose();
        }
    }

    private async Task ConvertToPngOutputAsync(
        string inputPath,
        string outputPath,
        Action<double> progress,
        CancellationToken cancellationToken)
    {
        progress?.Invoke(0.1);

        MetadataProfiles? metadata = await ExtractMetadataWithLoggingAsync(inputPath, cancellationToken);

        try
        {
            await _magickService.ConvertToPngAsync(inputPath, outputPath, cancellationToken);
            progress?.Invoke(0.7);

            if (metadata != null && metadata.HasAny)
            {
                await _exiftoolService.EmbedMetadataAsync(inputPath, outputPath, metadata, cancellationToken);
            }

            progress?.Invoke(1.0);

            if (!_fileService.FileExists(outputPath))
            {
                throw new FileNotFoundException($"Conversion completed but output file not found at: {outputPath}");
            }
        }
        finally
        {
            metadata?.Dispose();
        }
    }

    private async Task<MetadataProfiles?> ExtractMetadataWithLoggingAsync(string inputPath, CancellationToken cancellationToken)
    {
        try
        {
            return await _magickService.ExtractMetadataProfilesAsync(inputPath, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.Write($"[ImageProcessing] Metadata extraction failed: {ex.GetBaseException().Message}");
            return null;
        }
    }
}
