using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ARWtoJXL.Core.Models;

namespace ARWtoJXL.Core.Services
{
    public class ExiftoolService : Interfaces.IExiftoolService
    {

        public async Task<string?> ExtractExifAsync(string filePath, CancellationToken cancellationToken = default)
        {
            await Task.Yield();

            string? exiftoolPath = ProcessHelper.FindExiftool("ExiftoolService");
            if (string.IsNullOrEmpty(exiftoolPath))
            {
                Logger.Write("[ExiftoolService] exiftool.exe not found - skipping EXIF extraction");
                return null;
            }

            byte[]? exifData = ProcessHelper.RunProcessBinaryAsync(
                exiftoolPath,
                $"-b -exif:all \"{filePath}\"",
                cancellationToken);

            if (exifData != null && exifData.Length > 0)
            {
                Logger.Write($"[ExiftoolService] exiftool extracted {exifData.Length} bytes of EXIF");
                return SaveBytesToTemp(exifData, "exif");
            }

            Logger.Write("[ExiftoolService] exiftool returned empty EXIF data");
            return null;
        }

        public async Task EmbedMetadataAsync(string sourcePath, string outputPath, MetadataProfiles metadata, CancellationToken cancellationToken = default)
        {
            await Task.Yield();

            string? exiftoolPath = ProcessHelper.FindExiftool("ExiftoolService");
            if (string.IsNullOrEmpty(exiftoolPath))
            {
                Logger.Write("[ExiftoolService] exiftool.exe not found - skipping metadata embedding");
                return;
            }

            var exiftoolArgs = new System.Text.StringBuilder();
            exiftoolArgs.Append($"-tagsFromFile \"{sourcePath}\" ");

            if (!string.IsNullOrEmpty(metadata.ExifPath) && File.Exists(metadata.ExifPath))
            {
                exiftoolArgs.Append("-exif:all ");
                Logger.Write($"[ExiftoolService] Will embed EXIF from source: {sourcePath}");
            }
            if (!string.IsNullOrEmpty(metadata.XmpPath) && File.Exists(metadata.XmpPath))
            {
                exiftoolArgs.Append("-xmp:all ");
                Logger.Write($"[ExiftoolService] Will embed XMP from source: {sourcePath}");
            }
            if (!string.IsNullOrEmpty(metadata.IccPath) && File.Exists(metadata.IccPath))
            {
                exiftoolArgs.Append("-icc-profile ");
                Logger.Write($"[ExiftoolService] Will embed ICC from source: {sourcePath}");
            }

            if (exiftoolArgs.ToString().Trim() == $"-tagsFromFile \"{sourcePath}\" ")
            {
                Logger.Write("[ExiftoolService] No metadata to embed");
                return;
            }

            exiftoolArgs.Append("-overwrite_original \"");
            exiftoolArgs.Append(outputPath.Replace("\\", "/"));
            exiftoolArgs.Append('"');

            Logger.Write($"[ExiftoolService] exiftool command: {exiftoolPath} {exiftoolArgs}");

            var (exitCode, stdout, stderr) = await ProcessHelper.RunProcessAsync(
                exiftoolPath,
                exiftoolArgs.ToString(),
                cancellationToken);

            Logger.Write($"[ExiftoolService] exiftool metadata embedding exit={exitCode}, stdout='{stdout?.Trim()}', stderr='{stderr?.Trim()}'");
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
