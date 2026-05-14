using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ARWtoJXL.Core.Interfaces;

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
        if (outputFormat == OutputFormat.Jxl)
        {
            await ConvertToJxlInternalAsync(inputPath, outputPath, progress, quality, cancellationToken, skipMetadata, effort, threads);
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
        ReportProgress(progress, 0.1);

        if (skipMetadata)
        {
            _logger.Write($"[ImageProcessing] Metadata embedding skipped for {Path.GetFileName(inputPath)}");
        }

        ReportProgress(progress, 0.3);

        await _cjxlEncoder.EncodeFromStreamAsync(
            inputPath,
            inputPath,
            outputPath,
            quality,
            async (stream, ct) => await _imageConverterService.StreamPpmToAsync(inputPath, stream, ct),
            cancellationToken,
            timeoutSeconds: 300,
            cjxlProgress => ReportProgress(progress, 0.35 + cjxlProgress * 0.63),
            effort,
            skipMetadata,
            threads);

        ReportProgress(progress, 1.0);

        if (!_fileService.FileExists(outputPath))
        {
            throw new FileNotFoundException($"Conversion completed but output file not found at: {outputPath}");
        }
    }

 private async Task ConvertToJpegAsync(
        string inputPath,
        string outputPath,
        Action<double> progress,
        int quality,
        CancellationToken cancellationToken,
        bool skipMetadata = false)
    {
        ReportProgress(progress, 0.1);

        if (skipMetadata)
        {
            _logger.Write($"[ImageProcessing] Metadata embedding skipped for {Path.GetFileName(inputPath)}");
        }

        await _imageConverterService.ConvertToJpegAsync(inputPath, outputPath, quality, cancellationToken);
        ReportProgress(progress, 0.9);

        if (!skipMetadata)
        {
            await _exiftoolService.EmbedMetadataAsync(inputPath, outputPath, cancellationToken);
        }

        ReportProgress(progress, 1.0);

        if (!_fileService.FileExists(outputPath))
        {
            throw new FileNotFoundException($"Conversion completed but output file not found at: {outputPath}");
        }
    }

    private async Task ConvertToPngOutputAsync(
        string inputPath,
        string outputPath,
        Action<double> progress,
        CancellationToken cancellationToken,
        bool skipMetadata = false)
    {
        ReportProgress(progress, 0.1);

        if (skipMetadata)
        {
            _logger.Write($"[ImageProcessing] Metadata embedding skipped for {Path.GetFileName(inputPath)}");
        }

        await _imageConverterService.ConvertToPngAsync(inputPath, outputPath, cancellationToken);
        ReportProgress(progress, 0.7);

        if (!skipMetadata)
        {
            await _exiftoolService.EmbedMetadataAsync(inputPath, outputPath, cancellationToken);
        }

        ReportProgress(progress, 1.0);

        if (!_fileService.FileExists(outputPath))
        {
            throw new FileNotFoundException($"Conversion completed but output file not found at: {outputPath}");
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
