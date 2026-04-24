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
        private readonly IFileService _fileService;
        private readonly ILogger _logger;

        public ExiftoolService(IProcessRunner processRunner, IFileService fileService, ILogger logger)
        {
            _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
            _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
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

        byte[]? exifData = await _processRunner.RunProcessBinaryAsync(
            exiftoolPath,
            $"-b -exif:all \"{filePath}\"",
            cancellationToken);

        if (exifData != null && exifData.Length > 0)
        {
            _logger.Write($"[ExiftoolService] exiftool extracted {exifData.Length} bytes of EXIF");
            return _fileService.SaveBytesToTemp(exifData, "exif");
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

        if (!metadata.HasAny)
        {
            _logger.Write("[ExiftoolService] No metadata to embed");
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

}
