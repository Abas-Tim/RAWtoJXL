using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using ImageMagick;

namespace ARWtoJXL.Core.Services
{
    public class MagickService : Interfaces.IMagickService
    {
        private readonly Interfaces.IExiftoolService _exiftoolService;

        public MagickService(Interfaces.IExiftoolService? exiftoolService = null)
        {
            _exiftoolService = exiftoolService ?? new ExiftoolService();
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
                catch (Exception ex)
                {
                    throw new Exception($"Failed to convert {Path.GetFileName(inputPath)} to PNG: {ex.Message}", ex);
                }
            }, cancellationToken);
        }

        public async Task<Models.MetadataProfiles> ExtractMetadataProfilesAsync(string filePath, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                using var image = new MagickImage(filePath);
                var profiles = new Models.MetadataProfiles();

                try
                {
                    var ext = Path.GetExtension(filePath).ToLowerInvariant();
                    if (ext == ".arw")
                    {
                        var exifPath = _exiftoolService.ExtractExifAsync(filePath, cancellationToken).GetAwaiter().GetResult();
                        if (!string.IsNullOrEmpty(exifPath))
                        {
                            profiles.ExifPath = exifPath;
                            Logger.Write($"[MagickService] EXIF from exiftool: {profiles.ExifPath}");
                        }
                    }
                    else
                    {
                        object? exifProfile = image.GetExifProfile();
                        Logger.Write($"[MagickService] GetExifProfile: {(exifProfile == null ? "null" : "found")}");
                        if (exifProfile == null)
                        {
                            exifProfile = image.GetProfile("EXIF");
                            Logger.Write($"[MagickService] GetProfile('EXIF'): {(exifProfile == null ? "null" : "found")}");
                        }
                        if (exifProfile != null)
                        {
                            var bytes = GetProfileBytes(exifProfile);
                            Logger.Write($"[MagickService] EXIF bytes: {(bytes == null ? "null" : $"{bytes.Length} bytes")}");
                            if (bytes != null && bytes.Length > 0)
                            {
                                profiles.ExifPath = SaveBytesToTemp(bytes, "exif");
                                Logger.Write($"[MagickService] EXIF temp file: {profiles.ExifPath}");
                            }
                        }
                    }

                    var xmpProfile = image.GetProfile("XMP");
                    Logger.Write($"[MagickService] XMP profile: {(xmpProfile == null ? "null" : "found")}");
                    if (xmpProfile != null)
                    {
                        var bytes = GetProfileBytes(xmpProfile);
                        Logger.Write($"[MagickService] XMP bytes: {(bytes == null ? "null" : $"{bytes.Length} bytes")}");
                        if (bytes != null && bytes.Length > 0)
                        {
                            profiles.XmpPath = SaveBytesToTemp(bytes, "xmp");
                            Logger.Write($"[MagickService] XMP temp file: {profiles.XmpPath}");
                        }
                    }

                    var iccProfile = image.GetProfile("ICC ");
                    Logger.Write($"[MagickService] ICC profile: {(iccProfile == null ? "null" : "found")}");
                    if (iccProfile != null)
                    {
                        var bytes = GetProfileBytes(iccProfile);
                        Logger.Write($"[MagickService] ICC bytes: {(bytes == null ? "null" : $"{bytes.Length} bytes")}");
                        if (bytes != null && bytes.Length > 0)
                        {
                            profiles.IccPath = SaveBytesToTemp(bytes, "icc");
                            Logger.Write($"[MagickService] ICC temp file: {profiles.IccPath}");
                        }
                    }

                    object? iptcProfile = image.GetIptcProfile();
                    Logger.Write($"[MagickService] IPTC profile: {(iptcProfile == null ? "null" : "found")}");
                    if (iptcProfile == null)
                    {
                        iptcProfile = image.GetProfile("IPTC");
                    }
                    if (iptcProfile != null)
                    {
                        var bytes = GetProfileBytes(iptcProfile);
                        Logger.Write($"[MagickService] IPTC bytes: {(bytes == null ? "null" : $"{bytes.Length} bytes")}");
                        if (bytes != null && bytes.Length > 0)
                        {
                            profiles.IptcPath = SaveBytesToTemp(bytes, "jbf");
                            Logger.Write($"[MagickService] IPTC temp file: {profiles.IptcPath}");
                        }
                    }

                    if (string.IsNullOrEmpty(profiles.ExifPath) && Path.GetExtension(filePath).ToLowerInvariant() == ".jxl")
                    {
                        Logger.Write("[MagickService] Magick.NET EXIF failed for JXL, trying exiftool fallback...");
                        var exiftoolPath = _exiftoolService.ExtractExifAsync(filePath, cancellationToken).GetAwaiter().GetResult();
                        if (!string.IsNullOrEmpty(exiftoolPath))
                        {
                            profiles.ExifPath = exiftoolPath;
                            Logger.Write($"[MagickService] EXIF from exiftool: {profiles.ExifPath}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Write($"[MagickService] Metadata extraction error: {ex.Message}");
                }

                Logger.Write($"[MagickService] Final metadata: Exif={profiles.ExifPath ?? "none"}, Xmp={profiles.XmpPath ?? "none"}, Icc={profiles.IccPath ?? "none"}, Iptc={profiles.IptcPath ?? "none"}");

                return profiles;
            }, cancellationToken);
        }

        private static byte[]? GetProfileBytes(object profile)
        {
            try
            {
                var type = profile.GetType();

                var valueProp = type.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
                if (valueProp != null)
                {
                    var value = valueProp.GetValue(profile) as byte[];
                    if (value != null && value.Length > 0) return value;
                }

                var getBytesMethod = type.GetMethod("GetBytes", BindingFlags.Public | BindingFlags.Instance);
                if (getBytesMethod != null)
                {
                    var result = getBytesMethod.Invoke(profile, null) as byte[];
                    if (result != null && result.Length > 0) return result;
                }

                var dataField = type.GetField("_data", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
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

        private string? SaveBytesToTemp(byte[] data, string extension)
        {
            if (data == null || data.Length == 0)
                return null;

            var sanitizedExtension = Path.GetExtension(extension);
            if (string.IsNullOrEmpty(sanitizedExtension))
                sanitizedExtension = "." + extension.TrimStart('.');

            try
            {
                var tempFileName = Guid.NewGuid().ToString("N") + sanitizedExtension;
                var tempPath = Path.Combine(Path.GetTempPath(), tempFileName);
                File.WriteAllBytes(tempPath, data);
                return tempPath;
            }
            catch (Exception ex)
            {
                Logger.Write($"Failed to save profile to temp file: {ex.Message}");
                return null;
            }
        }
    }
}
