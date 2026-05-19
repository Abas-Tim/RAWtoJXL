using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using RAWtoJXL.Core.Interfaces;
using RAWtoJXL.Core.Models;

namespace RAWtoJXL.Core.Services;

public class ExiftoolService : IExiftoolService
    {
        private readonly IProcessRunner _processRunner;
        private readonly IFileService _fileService;
        private readonly ILogger _logger;

        public ExiftoolService(IProcessRunner processRunner, IFileService fileService, ILogger logger)
        {
            _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
            _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

    public async Task<MetadataProfiles> ExtractMetadataProfilesAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        string? exiftoolPath = await _processRunner.FindExiftoolAsync("ExiftoolService");
        if (string.IsNullOrEmpty(exiftoolPath))
        {
            _logger.Write("[ExiftoolService] exiftool.exe not found - skipping metadata extraction");
            return new MetadataProfiles(_logger);
        }

        var profiles = new MetadataProfiles(_logger);

        try
        {
            byte[]? exifData = await _processRunner.RunProcessBinaryAsync(
                exiftoolPath,
                $"-b -exif:all \"{filePath}\"",
                cancellationToken);

            if (exifData != null && exifData.Length > 0)
            {
                profiles.ExifPath = _fileService.SaveBytesToTemp(exifData, "exif");
                _logger.Write($"[ExiftoolService] EXIF extracted: {exifData.Length} bytes -> {profiles.ExifPath}");
            }
            else
            {
                _logger.Write("[ExiftoolService] exiftool returned empty EXIF data");
            }
        }
        catch (Exception ex)
        {
            _logger.Write($"[ExiftoolService] EXIF extraction failed: {ex.Message}");
        }

        try
        {
            byte[]? xmpData = await _processRunner.RunProcessBinaryAsync(
                exiftoolPath,
                $"-b -xmp:all \"{filePath}\"",
                cancellationToken);

            if (xmpData != null && xmpData.Length > 0)
            {
                profiles.XmpPath = _fileService.SaveBytesToTemp(xmpData, "xmp");
                _logger.Write($"[ExiftoolService] XMP extracted: {xmpData.Length} bytes -> {profiles.XmpPath}");
            }
        }
        catch (Exception ex)
        {
            _logger.Write($"[ExiftoolService] XMP extraction failed: {ex.Message}");
        }

        try
        {
            byte[]? iccData = await _processRunner.RunProcessBinaryAsync(
                exiftoolPath,
                $"-b -icc_profile \"{filePath}\"",
                cancellationToken);

            if (iccData != null && iccData.Length > 0)
            {
                profiles.IccPath = _fileService.SaveBytesToTemp(iccData, "icc");
                _logger.Write($"[ExiftoolService] ICC extracted: {iccData.Length} bytes -> {profiles.IccPath}");
            }
        }
        catch (Exception ex)
        {
            _logger.Write($"[ExiftoolService] ICC extraction failed: {ex.Message}");
        }

        try
        {
            byte[]? iptcData = await _processRunner.RunProcessBinaryAsync(
                exiftoolPath,
                $"-b -iptc:all \"{filePath}\"",
                cancellationToken);

            if (iptcData != null && iptcData.Length > 0)
            {
                profiles.IptcPath = _fileService.SaveBytesToTemp(iptcData, "jbf");
                _logger.Write($"[ExiftoolService] IPTC extracted: {iptcData.Length} bytes -> {profiles.IptcPath}");
            }
        }
        catch (Exception ex)
        {
            _logger.Write($"[ExiftoolService] IPTC extraction failed: {ex.Message}");
        }

        _logger.Write($"[ExiftoolService] Final metadata: Exif={profiles.ExifPath ?? "none"}, Xmp={profiles.XmpPath ?? "none"}, Icc={profiles.IccPath ?? "none"}, Iptc={profiles.IptcPath ?? "none"}, HasAny={profiles.HasAny}");

        return profiles;
    }

    public async Task<byte[]?> ExtractPreviewImageAsync(string filePath, CancellationToken cancellationToken = default)
        {
            string? exiftoolPath = await _processRunner.FindExiftoolAsync("ExiftoolService");
            if (string.IsNullOrEmpty(exiftoolPath))
            {
                _logger.Write("[ExiftoolService] exiftool.exe not found - skipping preview extraction");
                return null;
            }

            try
            {
                byte[]? previewData = await _processRunner.RunProcessBinaryAsync(
                    exiftoolPath,
                    $"-b -PreviewImage \"{filePath}\"",
                    cancellationToken);

                if (previewData != null && previewData.Length > 0)
                {
                    _logger.Write($"[ExiftoolService] Preview image extracted: {previewData.Length} bytes");
                    return previewData;
                }

                _logger.Write("[ExiftoolService] No preview image found in file");
                return null;
            }
            catch (Exception ex)
            {
                _logger.Write($"[ExiftoolService] Preview extraction failed: {ex.Message}");
                return null;
            }
        }

    public async Task EmbedMetadataAsync(string sourcePath, string outputPath, CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        string? exiftoolPath = await _processRunner.FindExiftoolAsync("ExiftoolService");
        if (string.IsNullOrEmpty(exiftoolPath))
        {
            _logger.Write("[ExiftoolService] exiftool.exe not found - skipping metadata embedding");
            return;
        }

        var exiftoolArgs = new System.Text.StringBuilder();
        exiftoolArgs.Append($"-tagsFromFile \"{sourcePath}\" ");
        exiftoolArgs.Append("-exif:all ");
        exiftoolArgs.Append("-xmp:all ");
        exiftoolArgs.Append("-icc-profile ");
        exiftoolArgs.Append("-overwrite_original \"");
        exiftoolArgs.Append(outputPath.Replace("\\", "/"));
        exiftoolArgs.Append('"');

        _logger.Write($"[ExiftoolService] exiftool command: {exiftoolPath} {exiftoolArgs}");

        var (exitCode, stdout, stderr) = await _processRunner.RunProcessAsync(
            exiftoolPath,
            exiftoolArgs.ToString(),
            cancellationToken);

        _logger.Write($"[ExiftoolService] exiftool metadata embedding exit={exitCode}, stdout='{stdout?.Trim()}', stderr='{stderr?.Trim()}'");

        if (exitCode != 0)
        {
            if (IsFileLockError(stderr, sourcePath))
            {
                throw new FileLockedException(sourcePath);
            }
            throw new IOException(
                $"exiftool metadata embedding failed with exit code {exitCode}. " +
                $"stdout: {stdout?.Trim() ?? "(empty)"} stderr: {stderr?.Trim() ?? "(empty)"}");
        }
    }

    private static bool IsFileLockError(string? stderr, string filePath)
    {
        if (string.IsNullOrEmpty(stderr)) return false;
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

}
