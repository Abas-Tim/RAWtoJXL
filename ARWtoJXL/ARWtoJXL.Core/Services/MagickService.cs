using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ImageMagick;
using ARWtoJXL.Core.Interfaces;
using ARWtoJXL.Core.Models;

namespace ARWtoJXL.Core.Services;

public class MagickService : IMagickService
    {
        private readonly IExiftoolService _exiftoolService;
        private readonly IFileService _fileService;
        private readonly ILogger _logger;

        public MagickService(IExiftoolService exiftoolService, IFileService fileService, ILogger logger)
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
        var profiles = new MetadataProfiles();
        var ext = Path.GetExtension(filePath).ToLowerInvariant();

        try
        {
            if (ext == ".arw")
            {
                var exifPath = await _exiftoolService.ExtractExifAsync(filePath, cancellationToken);
                if (!string.IsNullOrEmpty(exifPath))
                {
                    profiles.ExifPath = exifPath;
                    _logger.Write($"[MagickService] EXIF from exiftool: {profiles.ExifPath}");
                }
            }
            else
            {
                profiles = await ExtractProfilesFromImageAsync(filePath, profiles, cancellationToken);
            }

            if (string.IsNullOrEmpty(profiles.ExifPath) && ext != ".arw")
            {
                _logger.Write("[MagickService] Magick.NET EXIF failed, trying exiftool fallback...");
                var exiftoolPath = await _exiftoolService.ExtractExifAsync(filePath, cancellationToken);
                if (!string.IsNullOrEmpty(exiftoolPath))
                {
                    profiles.ExifPath = exiftoolPath;
                    _logger.Write($"[MagickService] EXIF from exiftool: {profiles.ExifPath}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Write($"[MagickService] Metadata extraction error: {ex.Message}");
        }

        _logger.Write($"[MagickService] Final metadata: Exif={profiles.ExifPath ?? "none"}, Xmp={profiles.XmpPath ?? "none"}, Icc={profiles.IccPath ?? "none"}, Iptc={profiles.IptcPath ?? "none"}");

        return profiles;
    }

    private async Task<MetadataProfiles> ExtractProfilesFromImageAsync(string filePath, MetadataProfiles profiles, CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var image = new MagickImage(filePath);

                object? exifProfile = image.GetExifProfile();
                _logger.Write($"[MagickService] GetExifProfile: {(exifProfile == null ? "null" : "found")}");
                if (exifProfile == null)
                {
                    exifProfile = image.GetProfile("EXIF");
                    _logger.Write($"[MagickService] GetProfile('EXIF'): {(exifProfile == null ? "null" : "found")}");
                }
                if (exifProfile != null)
                {
                    var bytes = GetProfileBytes(exifProfile);
                    _logger.Write($"[MagickService] EXIF bytes: {(bytes == null ? "null" : $"{bytes.Length} bytes")}");
                    if (bytes != null && bytes.Length > 0)
                    {
                        profiles.ExifPath = _fileService.SaveBytesToTemp(bytes, "exif");
                        _logger.Write($"[MagickService] EXIF temp file: {profiles.ExifPath}");
                    }
                }

                var xmpProfile = image.GetProfile("XMP");
                _logger.Write($"[MagickService] XMP profile: {(xmpProfile == null ? "null" : "found")}");
                if (xmpProfile != null)
                {
                    var bytes = GetProfileBytes(xmpProfile);
                    _logger.Write($"[MagickService] XMP bytes: {(bytes == null ? "null" : $"{bytes.Length} bytes")}");
                    if (bytes != null && bytes.Length > 0)
                    {
                        profiles.XmpPath = _fileService.SaveBytesToTemp(bytes, "xmp");
                        _logger.Write($"[MagickService] XMP temp file: {profiles.XmpPath}");
                    }
                }

                var iccProfile = image.GetProfile("ICC ");
                _logger.Write($"[MagickService] ICC profile: {(iccProfile == null ? "null" : "found")}");
                if (iccProfile != null)
                {
                    var bytes = GetProfileBytes(iccProfile);
                    _logger.Write($"[MagickService] ICC bytes: {(bytes == null ? "null" : $"{bytes.Length} bytes")}");
                    if (bytes != null && bytes.Length > 0)
                    {
                        profiles.IccPath = _fileService.SaveBytesToTemp(bytes, "icc");
                        _logger.Write($"[MagickService] ICC temp file: {profiles.IccPath}");
                    }
                }

                object? iptcProfile = image.GetIptcProfile();
                _logger.Write($"[MagickService] IPTC profile: {(iptcProfile == null ? "null" : "found")}");
                if (iptcProfile == null)
                {
                    iptcProfile = image.GetProfile("IPTC");
                }
                if (iptcProfile != null)
                {
                    var bytes = GetProfileBytes(iptcProfile);
                    _logger.Write($"[MagickService] IPTC bytes: {(bytes == null ? "null" : $"{bytes.Length} bytes")}");
                    if (bytes != null && bytes.Length > 0)
                    {
                        profiles.IptcPath = _fileService.SaveBytesToTemp(bytes, "jbf");
                        _logger.Write($"[MagickService] IPTC temp file: {profiles.IptcPath}");
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

    private static byte[]? GetProfileBytes(object profile)
    {
        try
        {
            var type = profile.GetType();

            var valueProp = type.GetProperty("Value", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (valueProp != null)
            {
                var value = valueProp.GetValue(profile) as byte[];
                if (value != null && value.Length > 0) return value;
            }

            var getBytesMethod = type.GetMethod("GetBytes", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (getBytesMethod != null)
            {
                var result = getBytesMethod.Invoke(profile, null) as byte[];
                if (result != null && result.Length > 0) return result;
            }

            var dataField = type.GetField("_data", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (dataField != null)
            {
                var data = dataField.GetValue(profile) as byte[];
                if (data != null && data.Length > 0) return data;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

}
