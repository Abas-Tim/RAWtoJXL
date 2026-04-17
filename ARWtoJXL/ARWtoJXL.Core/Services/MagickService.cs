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
/// <summary>
        /// Extracts a thumbnail from the specified image file.
        /// </summary>
        /// <param name="filePath">Path to the image file.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>JPEG thumbnail bytes resized to 300x300 pixels.</returns>
        /// <exception cref="ArgumentException">Thrown when filePath is null or empty.</exception>
        /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
        /// <exception cref="Exception">Thrown when thumbnail extraction fails.</exception>
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

                    // Resize maintaining aspect ratio, max 300x300
                    image.Resize(300, 300);
                    image.Format = MagickFormat.Jpg;
                    
                    // Optimize JPEG quality and strip metadata for smaller file size
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

        /// <summary>
        /// Converts a 14-bit ARW raw image to 16-bit PNG losslessly.
        /// </summary>
        /// <param name="inputPath">Path to the input ARW file.</param>
        /// <param name="outputPath">Path to the output PNG file.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <exception cref="ArgumentException">Thrown when inputPath or outputPath is null or empty.</exception>
        /// <exception cref="FileNotFoundException">Thrown when the input file does not exist.</exception>
        /// <exception cref="Exception">Thrown when conversion fails.</exception>
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
                    // Load the ARW raw image
                    using var image = new MagickImage(inputPath);

                    // Ensure 16-bit depth for lossless preservation of 14-bit raw data
                    // This prevents quantization loss when converting from 14-bit to PNG
                    image.Depth = 16;

                    // Set PNG format - PNG is inherently lossless
                    image.Format = MagickFormat.Png;

                    // Write the output PNG with 16-bit depth
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
                    // For ARW files, use exiftool as the primary EXIF extraction method
                    // since Magick.NET cannot reliably read EXIF from Sony ARW files
                    var ext = Path.GetExtension(filePath).ToLowerInvariant();
                    if (ext == ".arw")
                    {
                        var exifPath = ExtractExifWithExiftool(filePath);
                        if (!string.IsNullOrEmpty(exifPath))
                        {
                            profiles.ExifPath = exifPath;
                            Logger.Write($"[MagickService] EXIF from exiftool: {profiles.ExifPath}");
                        }
                    }
                    else
                    {
                        // For non-ARW files, try Magick.NET first
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

                    // Extract XMP - use GetProfile since GetXmpProfile() may return null for some formats
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

                    // Extract ICC profile
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

                    // Extract IPTC - try dedicated method first, then generic profile lookup
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

                    // Fallback: use exiftool for JXL files (Magick.NET cannot read EXIF from JXL)
                    if (string.IsNullOrEmpty(profiles.ExifPath) && Path.GetExtension(filePath).ToLowerInvariant() == ".jxl")
                    {
                        Logger.Write("[MagickService] Magick.NET EXIF failed for JXL, trying exiftool fallback...");
                        var exiftoolPath = ExtractExifWithExiftool(filePath);
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

                // Try Value property first
                var valueProp = type.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
                if (valueProp != null)
                {
                    var value = valueProp.GetValue(profile) as byte[];
                    if (value != null && value.Length > 0) return value;
                }

                // Try GetBytes method
                var getBytesMethod = type.GetMethod("GetBytes", BindingFlags.Public | BindingFlags.Instance);
                if (getBytesMethod != null)
                {
                    var result = getBytesMethod.Invoke(profile, null) as byte[];
                    if (result != null && result.Length > 0) return result;
                }

                // Try _data field (used by ImageProfile in Magick.NET 14.x)
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

        /// <summary>
        /// Saves bytes to a temporary file.
        /// </summary>
        /// <param name="data">The byte data to save.</param>
        /// <param name="extension">The file extension without the dot (e.g., "exif", "xmp", "icc").</param>
        /// <returns>The path to the temporary file, or null if saving fails.</returns>
        private string? SaveBytesToTemp(byte[] data, string extension)
        {
            if (data == null || data.Length == 0)
            {
                return null;
            }

            var sanitizedExtension = Path.GetExtension(extension);
            if (string.IsNullOrEmpty(sanitizedExtension))
            {
                sanitizedExtension = "." + extension.TrimStart('.');
            }

            try
            {
                var tempFileName = Guid.NewGuid().ToString("N") + sanitizedExtension;
                var tempPath = Path.Combine(Path.GetTempPath(), tempFileName);

                File.WriteAllBytes(tempPath, data);
                return tempPath;
            }
            catch (Exception ex)
            {
                Logger.Write(
                    $"Failed to save profile to temp file: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Extracts EXIF data from ARW files using exiftool (fallback when Magick.NET fails).
        /// Sony ARW files store EXIF in a proprietary TIFF-based format that Magick.NET cannot read.
        /// </summary>
        private string? ExtractExifWithExiftool(string filePath)
        {
            try
            {
                // Find exiftool.exe in the application directory or PATH
                string? exiftoolPath = null;

                // Check common installation locations first (prefer portable/install versions)
                var commonPaths = new[]
                {
                    @"C:\Program Files\exiftool.exe",
                    @"C:\Program Files (x86)\exiftool.exe",
                    @"F:\Downloads\exiftoolgui516\exiftoolgui\exiftool.exe",
                    @"C:\Users\Public\exiftool.exe"
                };
                foreach (var path in commonPaths)
                {
                    if (File.Exists(path))
                    {
                        exiftoolPath = path;
                        Logger.Write($"[MagickService] Found exiftool at: {exiftoolPath}");
                        break;
                    }
                }

                // Fallback to PATH
                if (string.IsNullOrEmpty(exiftoolPath))
                {
                    var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
                    foreach (var dir in pathEnv.Split(';'))
                    {
                        var candidate = Path.Combine(dir, "exiftool.exe");
                        if (File.Exists(candidate))
                        {
                            exiftoolPath = candidate;
                            Logger.Write($"[MagickService] Using PATH exiftool: {exiftoolPath}");
                            break;
                        }
                    }
                }

                // Check application directory last (may be non-portable version requiring Perl)
                if (string.IsNullOrEmpty(exiftoolPath))
                {
                    var appDir = AppDomain.CurrentDomain.BaseDirectory;
                    if (!string.IsNullOrEmpty(appDir))
                    {
                        var localExiftool = Path.Combine(appDir, "exiftool.exe");
                        if (File.Exists(localExiftool))
                        {
                            exiftoolPath = localExiftool;
                            Logger.Write($"[MagickService] Using local exiftool: {exiftoolPath}");
                        }
                    }
                }

                if (string.IsNullOrEmpty(exiftoolPath))
                {
                    Logger.Write("[MagickService] exiftool.exe not found - skipping EXIF extraction");
                    return null;
                }

                // Verify exiftool actually works before using it
                if (!IsExiftoolWorking(exiftoolPath))
                {
                    Logger.Write($"[MagickService] exiftool at {exiftoolPath} failed version check - skipping");
                    return null;
                }

                // Extract EXIF using exiftool: -b = binary output, -exif:all = all EXIF tags
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = exiftoolPath,
                    Arguments = $"-b -exif:all \"{filePath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = System.Diagnostics.Process.Start(startInfo);
                if (process == null)
                {
                    Logger.Write("[MagickService] Failed to start exiftool process");
                    return null;
                }

                // Read raw binary output (not text!) since EXIF is binary data
                using var ms = new System.IO.MemoryStream();
                process.StandardOutput.BaseStream.CopyTo(ms);
                process.WaitForExit();
                byte[]? exifData = ms.ToArray();

                if (exifData != null && exifData.Length > 0)
                {
                    Logger.Write($"[MagickService] exiftool extracted {exifData.Length} bytes of EXIF");
                    return SaveBytesToTemp(exifData, "exif");
                }
                else
                {
                    Logger.Write("[MagickService] exiftool returned empty EXIF data");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Logger.Write($"[MagickService] exiftool EXIF extraction failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Verifies that exiftool.exe is functional by running a version check.
        /// Filters out non-portable versions that require Perl runtime.
        /// </summary>
        private static bool IsExiftoolWorking(string exiftoolPath)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = exiftoolPath,
                    Arguments = "-ver",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process == null) return false;

                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                var success = process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output) && output[0] switch
                {
                    >= '0' and <= '9' => true,  // Version number on stdout
                    _ => false
                };

                if (!success)
                {
                    Logger.Write($"[MagickService] exiftool version check failed: exit={process.ExitCode}, stdout='{output?.Trim()}', stderr='{error?.Trim()}'");
                }
                else
                {
                    Logger.Write($"[MagickService] exiftool version check passed: {output?.Trim()}");
                }

                return success;
            }
            catch (Exception ex)
            {
                Logger.Write($"[MagickService] exiftool version check threw: {ex.Message}");
                return false;
            }
        }
    }
}

