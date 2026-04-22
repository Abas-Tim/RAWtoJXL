using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ARWtoJXL.Core.Interfaces;
using ARWtoJXL.Core.Models;

namespace ARWtoJXL.Core.Services;

public class ExiftoolService : IExiftoolService
{
    private readonly IProcessRunner _processRunner;
    private readonly ILogger _logger;

    public ExiftoolService(IProcessRunner processRunner, ILogger logger)
    {
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string?> ExtractExifAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        string? exiftoolPath = _processRunner.FindExiftool("ExiftoolService");
        if (string.IsNullOrEmpty(exiftoolPath))
        {
            _logger.Write("[ExiftoolService] exiftool.exe not found - skipping EXIF extraction");
            return null;
        }

        byte[]? exifData = _processRunner.RunProcessBinaryAsync(
            exiftoolPath,
            $"-b -exif:all \"{filePath}\"",
            cancellationToken);

        if (exifData != null && exifData.Length > 0)
        {
            _logger.Write($"[ExiftoolService] exiftool extracted {exifData.Length} bytes of EXIF");
            return SaveBytesToTemp(exifData, "exif");
        }

        _logger.Write("[ExiftoolService] exiftool returned empty EXIF data");
        return null;
    }

    public async Task EmbedMetadataAsync(string sourcePath, string outputPath, MetadataProfiles metadata, CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        string? exiftoolPath = _processRunner.FindExiftool("ExiftoolService");
        if (string.IsNullOrEmpty(exiftoolPath))
        {
            _logger.Write("[ExiftoolService] exiftool.exe not found - skipping metadata embedding");
            return;
        }

        var exiftoolArgs = new System.Text.StringBuilder();
        exiftoolArgs.Append($"-tagsFromFile \"{sourcePath}\" ");

        if (!string.IsNullOrEmpty(metadata.ExifPath) && File.Exists(metadata.ExifPath))
        {
            exiftoolArgs.Append("-exif:all ");
            _logger.Write($"[ExiftoolService] Will embed EXIF from source: {sourcePath}");
        }
        if (!string.IsNullOrEmpty(metadata.XmpPath) && File.Exists(metadata.XmpPath))
        {
            exiftoolArgs.Append("-xmp:all ");
            _logger.Write($"[ExiftoolService] Will embed XMP from source: {sourcePath}");
        }
        if (!string.IsNullOrEmpty(metadata.IccPath) && File.Exists(metadata.IccPath))
        {
            exiftoolArgs.Append("-icc-profile ");
            _logger.Write($"[ExiftoolService] Will embed ICC from source: {sourcePath}");
        }

        if (exiftoolArgs.ToString().Trim() == $"-tagsFromFile \"{sourcePath}\" ")
        {
            _logger.Write("[ExiftoolService] No metadata to embed");
            return;
        }

        exiftoolArgs.Append("-overwrite_original \"");
        exiftoolArgs.Append(outputPath.Replace("\\", "/"));
        exiftoolArgs.Append('"');

        _logger.Write($"[ExiftoolService] exiftool command: {exiftoolPath} {exiftoolArgs}");

        var (exitCode, stdout, stderr) = await _processRunner.RunProcessAsync(
            exiftoolPath,
            exiftoolArgs.ToString(),
            cancellationToken);

        _logger.Write($"[ExiftoolService] exiftool metadata embedding exit={exitCode}, stdout='{stdout?.Trim()}', stderr='{stderr?.Trim()}'");

        if (exitCode != 0 && !string.IsNullOrEmpty(stderr))
        {
            if (IsFileLockError(stderr, sourcePath))
            {
                throw new FileLockedException(sourcePath);
            }
        }
    }

    private static bool IsFileLockError(string stderr, string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        if (stderr.Contains(fileName, StringComparison.OrdinalIgnoreCase) &&
            (stderr.Contains("cannot open", StringComparison.OrdinalIgnoreCase) ||
             stderr.Contains("permission denied", StringComparison.OrdinalIgnoreCase) ||
             stderr.Contains("process cannot access", StringComparison.OrdinalIgnoreCase) ||
             stderr.Contains("unable to open", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }
        return false;
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
            _logger.Write($"Failed to save profile to temp file: {ex.Message}");
            return null;
        }
    }
}
