using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using RAWtoJXL.Core.Interfaces;
using RAWtoJXL.Core.Models;

namespace RAWtoJXL.Core.Services;

public class ImageProcessingService : IImageService
{
    private readonly IImageConverterService _imageConverterService;
    private readonly ICjxlEncoder _cjxlEncoder;
    private readonly IFileService _fileService;
    private readonly ILogger _logger;
    private readonly IExiftoolService _exiftoolService;
    private readonly IJxlDecoder _jxlDecoder;

    public ImageProcessingService(
        IImageConverterService imageConverterService,
        ICjxlEncoder cjxlEncoder,
        IFileService fileService,
        ILogger logger,
        IExiftoolService exiftoolService,
        IJxlDecoder jxlDecoder)
    {
        _imageConverterService = imageConverterService ?? throw new ArgumentNullException(nameof(imageConverterService));
        _cjxlEncoder = cjxlEncoder ?? throw new ArgumentNullException(nameof(cjxlEncoder));
        _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _exiftoolService = exiftoolService ?? throw new ArgumentNullException(nameof(exiftoolService));
        _jxlDecoder = jxlDecoder ?? throw new ArgumentNullException(nameof(jxlDecoder));
    }

    public async Task<byte[]> GetThumbnailAsync(string filePath, CancellationToken cancellationToken = default)
    {
        return await _imageConverterService.ExtractThumbnailAsync(filePath, cancellationToken);
    }

    public async Task ConvertToJxlAsync(
        string inputPath,
        string outputPath,
        Action<double> progress,
        int quality,
        OutputFormat outputFormat = OutputFormat.Jxl,
        CancellationToken cancellationToken = default,
        bool skipMetadata = false,
        int? effort = null,
        int? threads = null)
    {
        EnsureNotSameFormat(inputPath, outputFormat);

        if (outputFormat == OutputFormat.Jxl)
        {
            await ConvertToJxlInternalAsync(inputPath, outputPath, progress, quality, cancellationToken, skipMetadata, effort, threads);
        }
        else if (outputFormat == OutputFormat.Jpeg)
        {
            await ConvertToRasterInternalAsync(inputPath, outputPath, progress, quality, cancellationToken, skipMetadata,
                (path, outPath, q, ct) => _imageConverterService.ConvertToJpegAsync(path, outPath, q, ct));
        }
        else if (outputFormat == OutputFormat.Avif)
        {
            await ConvertToRasterInternalAsync(inputPath, outputPath, progress, quality, cancellationToken, skipMetadata,
                (path, outPath, q, ct) => _imageConverterService.ConvertToAvifAsync(path, outPath, q, ct));
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(outputFormat), $"Unsupported output format: {outputFormat}");
        }
    }

    private static void EnsureNotSameFormat(string inputPath, OutputFormat outputFormat)
    {
        string extension = Path.GetExtension(inputPath);
        bool isSameFormat = outputFormat switch
        {
            OutputFormat.Jxl => SupportedFormats.IsJxlFile(extension),
            OutputFormat.Jpeg => extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                 extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase),
            OutputFormat.Avif => extension.Equals(".avif", StringComparison.OrdinalIgnoreCase),
            _ => false
        };

        if (isSameFormat)
        {
            throw new InvalidOperationException(
                $"Converting {Path.GetFileName(inputPath)} ({extension}) to {outputFormat} is a same-format conversion and is not supported.");
        }
    }

    private async Task ConvertToJxlInternalAsync(
        string inputPath,
        string outputPath,
        Action<double> progress,
        int quality,
        CancellationToken cancellationToken,
        bool skipMetadata = false,
        int? effort = null,
        int? threads = null)
    {
        bool outputExisted = _fileService.FileExists(outputPath);

        try
        {
            ReportProgress(progress, 0.1);

            if (skipMetadata)
            {
                _logger.Write($"[ImageProcessing] Metadata embedding skipped for {Path.GetFileName(inputPath)}");
            }

            ReportProgress(progress, 0.3);

            await _cjxlEncoder.EncodeFromStreamAsync(
                inputPath,
                outputPath,
                quality,
                async (stream, ct) => await _imageConverterService.StreamPpmToAsync(inputPath, stream, ct),
                cancellationToken,
                timeoutSeconds: 300,
                cjxlProgress => ReportProgress(progress, 0.35 + cjxlProgress * 0.63),
                effort,
                threads);

            if (!skipMetadata)
            {
                await _exiftoolService.EmbedMetadataAsync(inputPath, outputPath, cancellationToken);
            }

            ReportProgress(progress, 1.0);
        }
        catch
        {
            if (!outputExisted)
            {
                _fileService.DeleteFile(outputPath);
            }
            throw;
        }
    }

    private async Task ConvertToRasterInternalAsync(
        string inputPath,
        string outputPath,
        Action<double> progress,
        int quality,
        CancellationToken cancellationToken,
        bool skipMetadata,
        Func<string, string, int, CancellationToken, Task> converter)
    {
        bool outputExisted = _fileService.FileExists(outputPath);
        bool isJxlInput = SupportedFormats.IsJxlFile(Path.GetExtension(inputPath));
        string? tempPng = null;

        try
        {
            ReportProgress(progress, 0.1);

            if (skipMetadata)
            {
                _logger.Write($"[ImageProcessing] Metadata embedding skipped for {Path.GetFileName(inputPath)}");
            }

            if (isJxlInput)
            {
                tempPng = Path.Combine(Path.GetTempPath(), $"jxl_decode_{Guid.NewGuid():N}.png");
                await _jxlDecoder.DecodeToPngAsync(inputPath, tempPng, cancellationToken);
                ReportProgress(progress, 0.4);
                await converter(tempPng, outputPath, quality, cancellationToken);
            }
            else
            {
                await converter(inputPath, outputPath, quality, cancellationToken);
            }

            ReportProgress(progress, 0.9);

            if (!skipMetadata)
            {
                await _exiftoolService.EmbedMetadataAsync(inputPath, outputPath, cancellationToken);
            }

            ReportProgress(progress, 1.0);
        }
        catch
        {
            if (!outputExisted)
            {
                _fileService.DeleteFile(outputPath);
            }
            throw;
        }
        finally
        {
            if (tempPng != null)
            {
                _fileService.DeleteFile(tempPng);
            }
        }
    }

    private void ReportProgress(Action<double> progress, double value)
    {
        try
        {
            progress(value);
        }
        catch (Exception ex)
        {
            _logger.Write($"[ImageProcessing] Progress callback threw: {ex.GetBaseException().Message}");
        }
    }
}
