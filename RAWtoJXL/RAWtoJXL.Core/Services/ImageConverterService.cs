using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ImageMagick;
using ImageMagick.Formats;
using RAWtoJXL.Core.Interfaces;
using RAWtoJXL.Core.Models;

namespace RAWtoJXL.Core.Services;

   public class ImageConverterService : IImageConverterService
    {
        private readonly IExiftoolService _exiftoolService;
        private readonly IFileService _fileService;
        private readonly ILogger _logger;
        private readonly IJxlDecoder _jxlDecoder;

        private const uint MaxThumbnailDimension = 300;

        public ImageConverterService(IExiftoolService exiftoolService, IFileService fileService, ILogger logger, IJxlDecoder jxlDecoder)
        {
            _exiftoolService = exiftoolService ?? throw new ArgumentNullException(nameof(exiftoolService));
            _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _jxlDecoder = jxlDecoder ?? throw new ArgumentNullException(nameof(jxlDecoder));
        }

    public async Task<byte[]> ExtractThumbnailAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        try
        {
            var preview = await _exiftoolService.ExtractPreviewImageAsync(filePath, cancellationToken);
            if (preview != null)
            {
                return DownscalePreview(preview, MaxThumbnailDimension, _logger);
            }

            var dngThumbnail = TryGetDngThumbnail(filePath);
            if (dngThumbnail != null)
            {
                return DownscalePreview(dngThumbnail, MaxThumbnailDimension, _logger);
            }

            if (Models.SupportedFormats.IsJxlFile(Path.GetExtension(filePath)))
            {
                var jxlThumbnail = await DecodeJxlThumbnailAsync(filePath, cancellationToken);
                if (jxlThumbnail != null)
                {
                    return DownscalePreview(jxlThumbnail, MaxThumbnailDimension, _logger);
                }
            }

            return await Task.Run(() => FallbackDecodeThumbnail(filePath), cancellationToken);
        }
        catch (IOException ex) when (FileLockedException.IsFileLocked(ex))
        {
            throw new FileLockedException(filePath, ex);
        }
        catch (Exception ex)
        {
            if (ex is FileLockedException)
                throw;
            throw new Exception($"Failed to load thumbnail for {Path.GetFileName(filePath)}: {ex.Message}", ex);
        }
    }

    private byte[]? TryGetDngThumbnail(string filePath)
    {
        try
        {
            using var image = new MagickImage();
            image.Settings.SetDefines(new DngReadDefines
            {
                ReadThumbnail = true
            });
            image.Ping(filePath);

            var profile = image.GetProfile("dng:thumbnail");
            if (profile == null)
            {
                return null;
            }

            var data = profile.ToByteArray();
            if (data == null || data.Length == 0)
            {
                return null;
            }

            using var thumbnail = new MagickImage(data);
            using var ms = new MemoryStream();
            thumbnail.Format = MagickFormat.Jpg;
            thumbnail.Quality = 85;
            thumbnail.Write(ms);
            return ms.ToArray();
        }
        catch (Exception ex)
        {
            _logger.Write($"[ImageConverterService] DNG thumbnail extraction failed for {Path.GetFileName(filePath)}: {ex.Message}");
            return null;
        }
    }

    private async Task<byte[]?> DecodeJxlThumbnailAsync(string filePath, CancellationToken cancellationToken)
    {
        string tempPng = Path.Combine(Path.GetTempPath(), $"jxl_thumb_{Guid.NewGuid():N}.png");
        try
        {
            await _jxlDecoder.DecodeToPngAsync(filePath, tempPng, cancellationToken);
            return await File.ReadAllBytesAsync(tempPng, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.Write($"[ImageConverterService] JXL thumbnail decode failed for {Path.GetFileName(filePath)}: {ex.Message}");
            return null;
        }
        finally
        {
            _fileService.DeleteFile(tempPng);
        }
    }

    internal static byte[] DownscalePreview(byte[] imageData, uint maxDimension, ILogger logger)    {
        try
        {
            using var image = new MagickImage(imageData);
            if (image.Width <= maxDimension && image.Height <= maxDimension)
            {
                return imageData;
            }

            image.Thumbnail(maxDimension, maxDimension);
            image.Format = MagickFormat.Jpg;
            image.Quality = 85;
            image.Strip();
            using var ms = new MemoryStream();
            image.Write(ms);
            return ms.ToArray();
        }
        catch (Exception ex)
        {
            logger.Write($"[ImageConverterService] Thumbnail downscale failed, returning original bytes: {ex.Message}");
            return imageData;
        }
    }

    private byte[] FallbackDecodeThumbnail(string filePath)
    {
        var settings = new MagickReadSettings();
        settings.SetDefine(MagickFormat.Unknown, "raw:use-camera-lookup-table", "false");

        using var image = new MagickImage(filePath, settings);
        image.ColorSpace = ColorSpace.sRGB;
        image.Thumbnail(MaxThumbnailDimension, MaxThumbnailDimension);
        image.Format = MagickFormat.Jpg;
        image.Quality = 80;
        image.Strip();

        using var ms = new MemoryStream();
        image.Write(ms);
        return ms.ToArray();
    }

   public async Task ConvertToJpegAsync(string inputPath, string outputPath, int quality, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            throw new ArgumentException("Input path cannot be null or empty.", nameof(inputPath));
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("Output path cannot be null or empty.", nameof(outputPath));
        }

        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException($"Input file not found: {inputPath}");
        }

        await Task.Run(() =>
        {
            try
            {
                using var image = new MagickImage(inputPath);
                image.Quality = (uint)Math.Max(1, Math.Min(100, quality));
                image.Format = MagickFormat.Jpg;

                var outputDir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }

                image.Write(outputPath);
            }
            catch (IOException ex) when (FileLockedException.IsFileLocked(ex))
            {
                throw new FileLockedException(inputPath, ex);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to convert {Path.GetFileName(inputPath)} to JPEG: {ex.Message}", ex);
            }
        }, cancellationToken);
    }

    public async Task ConvertToAvifAsync(string inputPath, string outputPath, int quality, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            throw new ArgumentException("Input path cannot be null or empty.", nameof(inputPath));
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("Output path cannot be null or empty.", nameof(outputPath));
        }

        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException($"Input file not found: {inputPath}");
        }

        await Task.Run(() =>
        {
            try
            {
                using var image = new MagickImage(inputPath);
                image.Quality = (uint)Math.Max(1, Math.Min(100, quality));
                if (image.Depth > 8)
                {
                    image.Depth = 16;
                }
                image.ColorSpace = ColorSpace.sRGB;
                image.Format = MagickFormat.Avif;

                var outputDir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }

                image.Write(outputPath);
            }
            catch (IOException ex) when (FileLockedException.IsFileLocked(ex))
            {
                throw new FileLockedException(inputPath, ex);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to convert {Path.GetFileName(inputPath)} to AVIF: {ex.Message}", ex);
            }
        }, cancellationToken);
    }

    public async Task<MetadataProfiles> ExtractMetadataProfilesAsync(string filePath, CancellationToken cancellationToken = default)
    {
        return await _exiftoolService.ExtractMetadataProfilesAsync(filePath, cancellationToken);
    }

    public async Task StreamPpmToAsync(string inputPath, Stream output, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            throw new ArgumentException("Input path cannot be null or empty.", nameof(inputPath));
        }

        if (output == null)
        {
            throw new ArgumentNullException(nameof(output), "Output stream cannot be null.");
        }

        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException($"Input file not found: {inputPath}");
        }

        await Task.Run(() =>
        {
            try
            {
                using var image = new MagickImage(inputPath);
                image.Depth = 16;
                image.ColorSpace = ColorSpace.sRGB;
                image.Format = MagickFormat.Ppm;

                _logger.Write($"[ImageConverterService] Streaming PPM: {image.Width}x{image.Height}");
                image.Write(output);
            }
            catch (IOException ex) when (FileLockedException.IsFileLocked(ex))
            {
                throw new FileLockedException(inputPath, ex);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to stream PPM from {Path.GetFileName(inputPath)}: {ex.Message}", ex);
            }
        }, cancellationToken);
    }

}
