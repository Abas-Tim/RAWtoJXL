using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ImageMagick;
using ARWtoJXL.Core.Interfaces;
using ARWtoJXL.Core.Models;

namespace ARWtoJXL.Core.Services;

public class ImageProcessingService : IImageService
{
    private readonly IImageConverterService _imageConverterService;
    private readonly ICjxlEncoder _cjxlEncoder;
    private readonly IFileService _fileService;
    private readonly IPathResolver _pathResolver;
    private readonly ILogger _logger;
    private readonly IExiftoolService _exiftoolService;

    public ImageProcessingService(
        IImageConverterService imageConverterService,
        ICjxlEncoder cjxlEncoder,
        IFileService fileService,
        IPathResolver pathResolver,
        ILogger logger,
        IExiftoolService exiftoolService)
    {
        _imageConverterService = imageConverterService ?? throw new ArgumentNullException(nameof(imageConverterService));
        _cjxlEncoder = cjxlEncoder ?? throw new ArgumentNullException(nameof(cjxlEncoder));
        _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
        _pathResolver = pathResolver ?? throw new ArgumentNullException(nameof(pathResolver));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _exiftoolService = exiftoolService ?? throw new ArgumentNullException(nameof(exiftoolService));
    }

    public async Task<byte[]> GetThumbnailAsync(string filePath, CancellationToken cancellationToken = default)
    {
        return await _imageConverterService.ExtractThumbnailAsync(filePath, cancellationToken);
    }

    public async Task ConvertArwToJxlAsync(
        string inputPath,
        string outputPath,
        Action<double> progress,
        int quality,
        OutputFormat outputFormat = OutputFormat.Jxl,
        CancellationToken cancellationToken = default,
        bool skipMetadata = false,
        int? effort = null,
        float? rawDistance = null)
    {
        if (outputFormat == OutputFormat.Jxl)
        {
            await ConvertToJxlAsync(inputPath, outputPath, progress, quality, cancellationToken, skipMetadata, effort, rawDistance);
        }
        else if (outputFormat == OutputFormat.Jpeg)
        {
            await ConvertToJpegAsync(inputPath, outputPath, progress, quality, cancellationToken, skipMetadata);
        }
        else if (outputFormat == OutputFormat.Png)
        {
            await ConvertToPngOutputAsync(inputPath, outputPath, progress, cancellationToken, skipMetadata);
        }
    }

    private async Task ConvertToJxlAsync(
        string inputPath,
        string outputPath,
        Action<double> progress,
        int quality,
        CancellationToken cancellationToken,
        bool skipMetadata = false,
        int? effort = null,
        float? rawDistance = null)
    {
        MetadataProfiles? metadata = null;

        try
        {
            progress?.Invoke(0.1);

            if (!skipMetadata)
            {
                metadata = await ExtractMetadataWithLoggingAsync(inputPath, cancellationToken);
                if (metadata != null)
                {
                    _logger.Write($"[ImageProcessing] Metadata extracted: Exif={metadata?.ExifPath ?? "none"}, Xmp={metadata?.XmpPath ?? "none"}, Icc={metadata?.IccPath ?? "none"}, Iptc={metadata?.IptcPath ?? "none"}, HasAny={metadata?.HasAny}");
                }
            }
            else
            {
                _logger.Write($"[ImageProcessing] Metadata extraction skipped for {Path.GetFileName(inputPath)}");
            }

            // Extract raw 16-bit RGB data
            var rgbBytes = await _imageConverterService.ExtractToRawRgb16Async(inputPath, cancellationToken);
            progress?.Invoke(0.3);

            // Parse image dimensions from the RGB data (we need width/height for PPM header)
            // We'll get dimensions by re-opening the image briefly
            (int width, int height) = await GetImageDimensionsAsync(inputPath, cancellationToken);

            // Build PPM stream: header + raw RGB data
            var ppmStream = BuildPpmStream(width, height, rgbBytes);
            progress?.Invoke(0.4);

            await _cjxlEncoder.EncodeFromStreamAsync(
                ppmStream,
                inputPath,
                outputPath,
                quality,
                metadata,
                cancellationToken,
                timeoutSeconds: 300,
                cjxlProgress => progress?.Invoke(0.4 + cjxlProgress * 0.6),
                effort,
                rawDistance);

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

    private async Task<(int Width, int Height)> GetImageDimensionsAsync(string inputPath, CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            using var image = new ImageMagick.MagickImage(inputPath);
            return ((int)image.Width, (int)image.Height);
        }, cancellationToken);
    }

    private MemoryStream BuildPpmStream(int width, int height, byte[] rgbBytes)
    {
        var ms = new MemoryStream();
        // PPM P6 binary header
        var header = $"P6\n{width} {height}\n65535\n";
        var headerBytes = Encoding.ASCII.GetBytes(header);
        ms.Write(headerBytes, 0, headerBytes.Length);
        ms.Write(rgbBytes, 0, rgbBytes.Length);
        ms.Position = 0;
        _logger.Write($"[ImageProcessing] Built PPM stream: {width}x{height}, {ms.Length} bytes total");
        return ms;
    }

   private async Task ConvertToJpegAsync(
        string inputPath,
        string outputPath,
        Action<double> progress,
        int quality,
        CancellationToken cancellationToken,
        bool skipMetadata = false)
    {
        progress?.Invoke(0.1);

        MetadataProfiles? metadata = null;
        if (!skipMetadata)
        {
            metadata = await ExtractMetadataWithLoggingAsync(inputPath, cancellationToken);
        }
        else
        {
            _logger.Write($"[ImageProcessing] Metadata extraction skipped for {Path.GetFileName(inputPath)}");
        }

        try
        {
            await _imageConverterService.ConvertToJpegAsync(inputPath, outputPath, quality, cancellationToken);
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
        CancellationToken cancellationToken,
        bool skipMetadata = false)
    {
        progress?.Invoke(0.1);

        MetadataProfiles? metadata = null;
        if (!skipMetadata)
        {
            metadata = await ExtractMetadataWithLoggingAsync(inputPath, cancellationToken);
        }
        else
        {
            _logger.Write($"[ImageProcessing] Metadata extraction skipped for {Path.GetFileName(inputPath)}");
        }

        try
        {
            await _imageConverterService.ConvertToPngAsync(inputPath, outputPath, cancellationToken);
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
            return await _imageConverterService.ExtractMetadataProfilesAsync(inputPath, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.Write($"[ImageProcessing] Metadata extraction failed: {ex.GetBaseException().Message}");
            return null;
        }
    }
}
