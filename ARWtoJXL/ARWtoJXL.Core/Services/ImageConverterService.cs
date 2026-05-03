using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ImageMagick;
using ARWtoJXL.Core.Interfaces;
using ARWtoJXL.Core.Models;

namespace ARWtoJXL.Core.Services;

public class ImageConverterService : IImageConverterService
    {
        private readonly IExiftoolService _exiftoolService;
        private readonly IFileService _fileService;
        private readonly ILogger _logger;

        public ImageConverterService(IExiftoolService exiftoolService, IFileService fileService, ILogger logger)
        {
            _exiftoolService = exiftoolService ?? throw new ArgumentNullException(nameof(exiftoolService));
            _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

        return await Task.Run(() =>
        {
            try
            {
                using var image = new MagickImage(filePath);
                image.Resize(300, 300);
                image.Format = MagickFormat.Jpg;
                image.Quality = 85;
                image.Strip();

                using var ms = new MemoryStream();
                image.Write(ms);
                return ms.ToArray();
            }
            catch (IOException ex) when (FileLockedException.IsFileLocked(ex))
            {
                throw new FileLockedException(filePath, ex);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to load thumbnail for {Path.GetFileName(filePath)}: {ex.Message}", ex);
            }
        }, cancellationToken);
    }

   public async Task ConvertToPngAsync(string inputPath, string outputPath, CancellationToken cancellationToken = default)
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
                image.Depth = 16;
                image.Format = MagickFormat.Png;
                image.Write(outputPath);
            }
            catch (IOException ex) when (FileLockedException.IsFileLocked(ex))
            {
                throw new FileLockedException(inputPath, ex);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to convert {Path.GetFileName(inputPath)} to PNG: {ex.Message}", ex);
            }
        }, cancellationToken);
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

    public async Task<MetadataProfiles> ExtractMetadataProfilesAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var profiles = new MetadataProfiles(_logger);
        var ext = Path.GetExtension(filePath).ToLowerInvariant();

        if (ext == ".arw")
        {
            var exifPath = await _exiftoolService.ExtractExifAsync(filePath, cancellationToken);
            if (!string.IsNullOrEmpty(exifPath))
            {
                profiles.ExifPath = exifPath;
                _logger.Write($"[ImageConverterService] EXIF from exiftool: {profiles.ExifPath}");
            }
        }
        else
        {
            try
            {
                profiles = await ExtractProfilesFromImageAsync(filePath, profiles, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.Write($"[ImageConverterService] Magick.NET extraction failed: {ex.Message}");
            }

            if (string.IsNullOrEmpty(profiles.ExifPath))
            {
                _logger.Write("[ImageConverterService] Trying exiftool fallback for EXIF...");
                var exiftoolPath = await _exiftoolService.ExtractExifAsync(filePath, cancellationToken);
                if (!string.IsNullOrEmpty(exiftoolPath))
                {
                    profiles.ExifPath = exiftoolPath;
                    _logger.Write($"[ImageConverterService] EXIF from exiftool: {profiles.ExifPath}");
                }
            }
        }

        _logger.Write($"[ImageConverterService] Final metadata: Exif={profiles.ExifPath ?? "none"}, Xmp={profiles.XmpPath ?? "none"}, Icc={profiles.IccPath ?? "none"}, Iptc={profiles.IptcPath ?? "none"}");

        return profiles;
    }

    public async Task<byte[]> ExtractToRawRgb16Async(string inputPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            throw new ArgumentException("Input path cannot be null or empty.", nameof(inputPath));
        }

        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException($"Input file not found: {inputPath}");
        }

        return await Task.Run(() =>
        {
            try
            {
                using var image = new MagickImage(inputPath);
                image.Depth = 16;
                image.ColorSpace = ColorSpace.sRGB;

                var width = image.Width;
                var height = image.Height;

                var pixels = image.GetPixels();
                long pixelCount = pixels.Count();

                // Each pixel: R(2 bytes BE) G(2 bytes BE) B(2 bytes BE) = 6 bytes
                var result = new byte[pixelCount * 6];
                int resultIndex = 0;

                foreach (var pixel in pixels)
                {
                    // Magick.NET 16-bit: each channel value is 0-65535
                    ushort r = pixel[0];
                    ushort g = pixel[1];
                    ushort b = pixel[2];

                    // Big-endian for PPM
                    result[resultIndex++] = (byte)(r >> 8);
                    result[resultIndex++] = (byte)(r & 0xFF);
                    result[resultIndex++] = (byte)(g >> 8);
                    result[resultIndex++] = (byte)(g & 0xFF);
                    result[resultIndex++] = (byte)(b >> 8);
                    result[resultIndex++] = (byte)(b & 0xFF);
                }

                _logger.Write($"[ImageConverterService] Extracted RGB16: {width}x{height}, {result.Length} bytes");
                return result;
            }
            catch (IOException ex) when (FileLockedException.IsFileLocked(ex))
            {
                throw new FileLockedException(inputPath, ex);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to extract RGB16 from {Path.GetFileName(inputPath)}: {ex.Message}", ex);
            }
        }, cancellationToken);
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

    private async Task<MetadataProfiles> ExtractProfilesFromImageAsync(string filePath, MetadataProfiles profiles, CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var image = new MagickImage(filePath);

                IImageProfile? exifProfile = image.GetExifProfile();
                _logger.Write($"[ImageConverterService] GetExifProfile: {(exifProfile == null ? "null" : "found")}");
                if (exifProfile == null)
                {
                    exifProfile = image.GetProfile("EXIF");
                    _logger.Write($"[ImageConverterService] GetProfile('EXIF'): {(exifProfile == null ? "null" : "found")}");
                }
                if (exifProfile != null)
                {
                    var bytes = GetProfileBytes(exifProfile);
                    _logger.Write($"[ImageConverterService] EXIF bytes: {(bytes == null ? "null" : $"{bytes.Length} bytes")}");
                    if (bytes != null && bytes.Length > 0)
                    {
                        profiles.ExifPath = _fileService.SaveBytesToTemp(bytes, "exif");
                        _logger.Write($"[ImageConverterService] EXIF temp file: {profiles.ExifPath}");
                    }
                }

                var xmpProfile = image.GetProfile("XMP");
                _logger.Write($"[ImageConverterService] XMP profile: {(xmpProfile == null ? "null" : "found")}");
                if (xmpProfile != null)
                {
                    var bytes = GetProfileBytes(xmpProfile);
                    _logger.Write($"[ImageConverterService] XMP bytes: {(bytes == null ? "null" : $"{bytes.Length} bytes")}");
                    if (bytes != null && bytes.Length > 0)
                    {
                        profiles.XmpPath = _fileService.SaveBytesToTemp(bytes, "xmp");
                        _logger.Write($"[ImageConverterService] XMP temp file: {profiles.XmpPath}");
                    }
                }

                var iccProfile = image.GetProfile("ICC ");
                _logger.Write($"[ImageConverterService] ICC profile: {(iccProfile == null ? "null" : "found")}");
                if (iccProfile != null)
                {
                    var bytes = GetProfileBytes(iccProfile);
                    _logger.Write($"[ImageConverterService] ICC bytes: {(bytes == null ? "null" : $"{bytes.Length} bytes")}");
                    if (bytes != null && bytes.Length > 0)
                    {
                        profiles.IccPath = _fileService.SaveBytesToTemp(bytes, "icc");
                        _logger.Write($"[ImageConverterService] ICC temp file: {profiles.IccPath}");
                    }
                }

                IImageProfile? iptcProfile = image.GetIptcProfile();
                _logger.Write($"[ImageConverterService] IPTC profile: {(iptcProfile == null ? "null" : "found")}");
                if (iptcProfile == null)
                {
                    iptcProfile = image.GetProfile("IPTC");
                }
                if (iptcProfile != null)
                {
                    var bytes = GetProfileBytes(iptcProfile);
                    _logger.Write($"[ImageConverterService] IPTC bytes: {(bytes == null ? "null" : $"{bytes.Length} bytes")}");
                    if (bytes != null && bytes.Length > 0)
                    {
                        profiles.IptcPath = _fileService.SaveBytesToTemp(bytes, "jbf");
                        _logger.Write($"[ImageConverterService] IPTC temp file: {profiles.IptcPath}");
                    }
                }

                return profiles;
            }
            catch (IOException ex) when (FileLockedException.IsFileLocked(ex))
            {
                throw new FileLockedException(filePath, ex);
            }
        }, cancellationToken);
    }

    private byte[]? GetProfileBytes(IImageProfile profile)
    {
        return profile.ToByteArray();
    }

}
