using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ImageMagick;
using ImageMagick.Formats;
using RAWtoJXL.Core.Interfaces;
using RAWtoJXL.Core.Models;

namespace RAWtoJXL.Core.Services;

public class CompareConversionService : ICompareConversionService
{
    private readonly IImageConverterService _imageConverterService;
    private readonly ICjxlEncoder _cjxlEncoder;
    private readonly IJxlDecoder _jxlDecoder;
    private readonly IRawRenderer _rawRenderer;
    private readonly IFileService _fileService;
    private readonly ILogger _logger;

    private readonly ConcurrentDictionary<string, Task<string>> _inflightMasters = new();
    private readonly ConcurrentDictionary<string, Task<string?>> _inflightQuickPreviews = new();
    private readonly ConcurrentDictionary<string, Task<string>> _inflightVariants = new();
    private readonly ConcurrentDictionary<string, Task<CompareDisplayPngs>> _inflightDisplays = new();
    private readonly ConcurrentDictionary<string, Task<string>> _inflightFullDisplays = new();

    public CompareConversionService(
        IImageConverterService imageConverterService,
        ICjxlEncoder cjxlEncoder,
        IJxlDecoder jxlDecoder,
        IRawRenderer rawRenderer,
        IFileService fileService,
        ILogger logger)
    {
        _imageConverterService = imageConverterService ?? throw new ArgumentNullException(nameof(imageConverterService));
        _cjxlEncoder = cjxlEncoder ?? throw new ArgumentNullException(nameof(cjxlEncoder));
        _jxlDecoder = jxlDecoder ?? throw new ArgumentNullException(nameof(jxlDecoder));
        _rawRenderer = rawRenderer ?? throw new ArgumentNullException(nameof(rawRenderer));
        _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ResourceLimits.Thread = (ulong)CompareDefaults.JxlThreads;
    }

    public Task<string> EnsureMasterPngAsync(string inputPath, CancellationToken cancellationToken = default)
    {
        var fp = ReadFingerprint(inputPath);
        string dir = Path.Combine(CompareDefaults.CacheRoot, "master", $"m-{ComputeHash(fp, null, 0, 0)}");
        string masterPath = Path.Combine(dir, "master.png");
        string metaPath = Path.Combine(dir, "meta.json");

        if (IsCacheValid(metaPath, fp) && File.Exists(masterPath))
        {
            return Task.FromResult(masterPath);
        }

        if (_inflightMasters.TryGetValue(masterPath, out var running))
        {
            return running;
        }

        TryDeleteDirectory(dir);

        return _inflightMasters.GetOrAdd(masterPath, key =>
        {
            var task = Task.Run(() => DecodeMasterAsync(inputPath, dir, masterPath, metaPath, fp, cancellationToken), CancellationToken.None);
            _ = task.ContinueWith(t => _inflightMasters.TryRemove(key, out var ignored), TaskScheduler.Default);
            return task;
        });
    }

    public async Task<string?> EnsureQuickPreviewAsync(string inputPath, CancellationToken cancellationToken = default)
    {
        var fp = ReadFingerprint(inputPath);
        string dir = Path.Combine(CompareDefaults.CacheRoot, "quick", $"q-{ComputeHash(fp, null, 0, 0)}");
        string previewPath = Path.Combine(dir, "embedded-preview.jpg");
        string metaPath = Path.Combine(dir, "meta.json");

        if (IsCacheValid(metaPath, fp) && File.Exists(previewPath))
        {
            return previewPath;
        }

        if (_inflightQuickPreviews.TryGetValue(dir, out var running))
        {
            return await running.ConfigureAwait(false);
        }

        TryDeleteDirectory(dir);

        var task = GenerateQuickPreviewAsync(inputPath, dir, previewPath, metaPath, fp, cancellationToken);
        if (!_inflightQuickPreviews.TryAdd(dir, task))
        {
            return await _inflightQuickPreviews[dir].ConfigureAwait(false);
        }

        try
        {
            return await task.ConfigureAwait(false);
        }
        finally
        {
            _inflightQuickPreviews.TryRemove(dir, out _);
        }
    }

    public async Task<string> EnsureTargetFileAsync(
        string inputPath,
        OutputFormat format,
        int quality,
        int? effort,
        CancellationToken cancellationToken = default)
    {
        var fp = ReadFingerprint(inputPath);
        string dir = Path.Combine(CompareDefaults.CacheRoot, "variant", $"v-{ComputeHash(fp, format, quality, effort)}");
        string targetPath = Path.Combine(dir, $"target.{GetExtension(format)}");
        string metaPath = Path.Combine(dir, "meta.json");

        if (IsCacheValid(metaPath, fp) && File.Exists(targetPath) && new FileInfo(targetPath).Length > 0)
        {
            return targetPath;
        }

        if (_inflightVariants.TryGetValue(targetPath, out var running))
        {
            return await running.ConfigureAwait(false);
        }

        string masterPath = await EnsureMasterPngAsync(inputPath, cancellationToken).ConfigureAwait(false);
        TryDeleteDirectory(dir);

        var task = ConvertTargetAsync(masterPath, targetPath, metaPath, fp, format, quality, effort, cancellationToken);
        if (!_inflightVariants.TryAdd(targetPath, task))
        {
            return await _inflightVariants[targetPath].ConfigureAwait(false);
        }

        try
        {
            return await task.ConfigureAwait(false);
        }
        finally
        {
            _inflightVariants.TryRemove(targetPath, out _);
        }
    }

    public async Task<CompareDisplayPngs> EnsureDisplayPngsAsync(
        string inputPath,
        OutputFormat? format,
        int quality,
        int? effort,
        CancellationToken cancellationToken = default)
    {
        var fp = ReadFingerprint(inputPath);
        string dir = Path.Combine(CompareDefaults.CacheRoot, "display", $"d-{ComputeHash(fp, format, format == null ? 0 : quality, format == null ? null : effort)}");
        string previewPath = Path.Combine(dir, "display-preview.png");
        string metaPath = Path.Combine(dir, "meta.json");

        var cached = ReadMeta(metaPath);
        if (IsCacheValid(metaPath, fp) && cached != null && cached.Width > 0 && File.Exists(previewPath))
        {
            return new CompareDisplayPngs(previewPath, string.Empty, cached.Width, cached.Height);
        }

        if (_inflightDisplays.TryGetValue(dir, out var running))
        {
            return await running.ConfigureAwait(false);
        }

        TryDeleteDirectory(dir);

        var task = GenerateDisplayPngsAsync(inputPath, dir, previewPath, metaPath, fp, format, quality, effort, cancellationToken);
        if (!_inflightDisplays.TryAdd(dir, task))
        {
            return await _inflightDisplays[dir].ConfigureAwait(false);
        }

        try
        {
            return await task.ConfigureAwait(false);
        }
        finally
        {
            _inflightDisplays.TryRemove(dir, out _);
        }
    }

    public async Task<string> EnsureDisplayFullPngAsync(
        string inputPath,
        OutputFormat? format,
        int quality,
        int? effort,
        CancellationToken cancellationToken = default)
    {
        var fp = ReadFingerprint(inputPath);
        string dir = Path.Combine(CompareDefaults.CacheRoot, "display", $"d-{ComputeHash(fp, format, format == null ? 0 : quality, format == null ? null : effort)}");
        string fullPath = Path.Combine(dir, "display-full.png");
        string metaPath = Path.Combine(dir, "meta.json");

        if (IsCacheValid(metaPath, fp) && File.Exists(fullPath) && new FileInfo(fullPath).Length > 0)
        {
            return fullPath;
        }

        if (_inflightFullDisplays.TryGetValue(dir, out var running))
        {
            return await running.ConfigureAwait(false);
        }

        var task = GenerateDisplayFullPngAsync(
            inputPath, dir, fullPath, metaPath, fp, format, quality, effort, cancellationToken);
        if (!_inflightFullDisplays.TryAdd(dir, task))
        {
            return await _inflightFullDisplays[dir].ConfigureAwait(false);
        }

        try
        {
            return await task.ConfigureAwait(false);
        }
        finally
        {
            _inflightFullDisplays.TryRemove(dir, out _);
        }
    }

    public void PurgeStaleEntries()
    {
        try
        {
            var root = CompareDefaults.CacheRoot;
            if (!Directory.Exists(root))
            {
                return;
            }

            var survivors = new List<(string Dir, DateTime CreatedUtc)>();

            foreach (var sub in new[] { "master", "variant", "display", "quick" })
            {
                string subDir = Path.Combine(root, sub);
                if (!Directory.Exists(subDir))
                {
                    continue;
                }

                foreach (var dir in Directory.EnumerateDirectories(subDir))
                {
                    string metaPath = Path.Combine(dir, "meta.json");
                    var meta = ReadMeta(metaPath);
                    if (meta == null || !File.Exists(meta.InputPath))
                    {
                        TryDeleteDirectory(dir);
                        continue;
                    }

                    try
                    {
                        var fi = new FileInfo(meta.InputPath);
                        if (fi.Length != meta.FileSize || fi.LastWriteTimeUtc.Ticks != meta.LastWriteTicks)
                        {
                            TryDeleteDirectory(dir);
                            continue;
                        }
                    }
                    catch
                    {
                        TryDeleteDirectory(dir);
                        continue;
                    }

                    survivors.Add((dir, meta.CreatedUtc));
                }
            }

            EnforceSizeCap(survivors);
        }
        catch (Exception ex)
        {
            _logger.Write($"[CompareConversionService] Cache purge failed: {ex.Message}");
        }
    }

    private async Task<string?> GenerateQuickPreviewAsync(
        string inputPath,
        string dir,
        string previewPath,
        string metaPath,
        SourceFingerprint fp,
        CancellationToken cancellationToken)
    {
        try
        {
            byte[]? embedded = await _imageConverterService
                .ExtractEmbeddedPreviewAsync(inputPath, cancellationToken)
                .ConfigureAwait(false);
            if (embedded == null || embedded.Length == 0)
            {
                return null;
            }

            Directory.CreateDirectory(dir);
            await Task.Run(() =>
            {
                using var image = new MagickImage(embedded);
                image.ColorType = ColorType.TrueColor;
                image.SetBitDepth(8);
                image.Density = new Density(96, 96);
                if (image.Width > CompareDefaults.QuickPreviewMaxDimension ||
                    image.Height > CompareDefaults.QuickPreviewMaxDimension)
                {
                    image.Thumbnail(CompareDefaults.QuickPreviewMaxDimension, CompareDefaults.QuickPreviewMaxDimension);
                }
                image.Format = MagickFormat.Jpg;
                image.Quality = 85;
                image.Write(previewPath);
            }, cancellationToken).ConfigureAwait(false);

            WriteMeta(metaPath, fp, 0, 0);
            return previewPath;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.Write($"[CompareConversionService] Embedded preview failed for {Path.GetFileName(inputPath)}: {ex.Message}");
            TryDeleteDirectory(dir);
            return null;
        }
    }

    private async Task<string> DecodeMasterAsync(
        string inputPath,
        string dir,
        string masterPath,
        string metaPath,
        SourceFingerprint fp,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(dir);
        try
        {
            string ext = Path.GetExtension(inputPath).ToLowerInvariant();
            if (SupportedFormats.IsJxlFile(ext))
            {
                try
                {
                    await _jxlDecoder.DecodeToPngAsync(inputPath, masterPath, cancellationToken).ConfigureAwait(false);
                }
                catch (FileNotFoundException ex) when (IsMissingTool(ex, "djxl"))
                {
                    await _imageConverterService.ConvertToPngAsync(inputPath, masterPath, cancellationToken).ConfigureAwait(false);
                }
            }
            else if (SupportedFormats.IsRawFile(ext))
            {
                try
                {
                    await _rawRenderer.RenderToPngAsync(
                        inputPath, masterPath, CompareDefaults.JxlThreads, cancellationToken).ConfigureAwait(false);
                }
                catch (FileNotFoundException)
                {
                    await DecodeRawWithMagickAsync(inputPath, masterPath, cancellationToken).ConfigureAwait(false);
                }
            }
            else
            {
                await DecodeRawWithMagickAsync(inputPath, masterPath, cancellationToken).ConfigureAwait(false);
            }

            WriteMeta(metaPath, fp, 0, 0);
            return masterPath;
        }
        catch (IOException ex) when (FileLockedException.IsFileLocked(ex))
        {
            TryDeleteDirectory(dir);
            throw new FileLockedException(inputPath, ex);
        }
        catch
        {
            TryDeleteDirectory(dir);
            throw;
        }
    }

    private static Task DecodeRawWithMagickAsync(string inputPath, string masterPath, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            using var image = new MagickImage(inputPath);
            image.ColorSpace = ColorSpace.sRGB;
            if (image.Depth > 8)
            {
                image.Depth = 16;
            }
            image.Format = MagickFormat.Png;
            image.Write(masterPath);
        }, cancellationToken);
    }

    private async Task<string> ConvertTargetAsync(
        string masterPath,
        string targetPath,
        string metaPath,
        SourceFingerprint fp,
        OutputFormat format,
        int quality,
        int? effort,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        try
        {
            switch (format)
            {
                case OutputFormat.Jxl:
                    try
                    {
                        await _cjxlEncoder.EncodeFromFileAsync(
                            masterPath, targetPath, quality, cancellationToken,
                            timeoutSeconds: 300, progress: null,
                            effort: effort ?? CompareDefaults.JxlEffort,
                            threads: CompareDefaults.JxlThreads).ConfigureAwait(false);
                    }
                catch (Exception ex) when (IsMissingTool(ex, "cjxl"))
                    {
                        await _imageConverterService.ConvertToJxlAsync(
                            masterPath, targetPath, quality, effort, cancellationToken).ConfigureAwait(false);
                    }
                    break;
                case OutputFormat.Jpeg:
                    await _imageConverterService.ConvertToJpegAsync(masterPath, targetPath, quality, cancellationToken).ConfigureAwait(false);
                    break;
                case OutputFormat.Avif:
                    await _imageConverterService.ConvertToAvifAsync(masterPath, targetPath, quality, cancellationToken).ConfigureAwait(false);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(format), $"Unsupported output format: {format}");
            }

            WriteMeta(metaPath, fp, 0, 0);
            return targetPath;
        }
        catch
        {
            TryDeleteDirectory(Path.GetDirectoryName(targetPath)!);
            throw;
        }
    }

    private async Task<CompareDisplayPngs> GenerateDisplayPngsAsync(
        string inputPath,
        string dir,
        string previewPath,
        string metaPath,
        SourceFingerprint fp,
        OutputFormat? format,
        int quality,
        int? effort,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(dir);
        var source = await ResolveDisplaySourceAsync(inputPath, dir, format, quality, effort, cancellationToken).ConfigureAwait(false);
        try
        {
            var result = await Task.Run(() =>
            {
                using var image = new MagickImage(source.SourcePath);
                int width = (int)image.Width;
                int height = (int)image.Height;
                image.ColorSpace = ColorSpace.sRGB;
                image.ColorType = ColorType.TrueColor;
                image.SetBitDepth(8);
                image.Density = new Density(96, 96);
                image.Strip();
                image.Settings.SetDefines(new PngWriteDefines
                {
                    BitDepth = 8,
                    ColorType = ColorType.TrueColor
                });
                image.Format = MagickFormat.Png;

                if (image.Width > CompareDefaults.PreviewMaxDimension || image.Height > CompareDefaults.PreviewMaxDimension)
                {
                    image.Thumbnail(CompareDefaults.PreviewMaxDimension, CompareDefaults.PreviewMaxDimension);
                }
                image.Write(previewPath);

                return new CompareDisplayPngs(previewPath, string.Empty, width, height);
            }, cancellationToken).ConfigureAwait(false);

            WriteMeta(metaPath, fp, result.Width, result.Height);
            return result;
        }
        catch
        {
            TryDeleteDirectory(dir);
            throw;
        }
        finally
        {
            if (source.TempPng != null)
            {
                _fileService.DeleteFile(source.TempPng);
            }
        }
    }

    private async Task<string> GenerateDisplayFullPngAsync(
        string inputPath,
        string dir,
        string fullPath,
        string metaPath,
        SourceFingerprint fp,
        OutputFormat? format,
        int quality,
        int? effort,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(dir);
        var source = await ResolveDisplaySourceAsync(inputPath, dir, format, quality, effort, cancellationToken).ConfigureAwait(false);
        try
        {
            await Task.Run(() =>
            {
                using var image = new MagickImage(source.SourcePath);
                int width = (int)image.Width;
                int height = (int)image.Height;
                image.ColorSpace = ColorSpace.sRGB;
                image.ColorType = ColorType.TrueColor;
                image.SetBitDepth(8);
                image.Density = new Density(96, 96);
                image.Strip();
                image.Settings.SetDefines(new PngWriteDefines
                {
                    BitDepth = 8,
                    ColorType = ColorType.TrueColor
                });
                image.Format = MagickFormat.Png;
                image.Write(fullPath);

                var existingMeta = ReadMeta(metaPath);
                WriteMeta(metaPath, fp, existingMeta?.Width > 0 ? existingMeta.Width : width, existingMeta?.Height > 0 ? existingMeta.Height : height);
            }, cancellationToken).ConfigureAwait(false);

            return fullPath;
        }
        catch
        {
            _fileService.DeleteFile(fullPath);
            throw;
        }
        finally
        {
            if (source.TempPng != null)
            {
                _fileService.DeleteFile(source.TempPng);
            }
        }
    }

    private async Task<(string SourcePath, string? TempPng)> ResolveDisplaySourceAsync(
        string inputPath,
        string dir,
        OutputFormat? format,
        int quality,
        int? effort,
        CancellationToken cancellationToken)
    {
        if (format == null)
        {
            return (await EnsureMasterPngAsync(inputPath, cancellationToken).ConfigureAwait(false), null);
        }

        string targetPath = await EnsureTargetFileAsync(
            inputPath, format.Value, quality, effort, cancellationToken).ConfigureAwait(false);
        if (format != OutputFormat.Jxl)
        {
            return (targetPath, null);
        }

        string tempPng = Path.Combine(dir, $"djxl_{Guid.NewGuid():N}.png");
        try
        {
            try
            {
                await _jxlDecoder.DecodeToPngAsync(targetPath, tempPng, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (IsMissingTool(ex, "djxl"))
            {
                await _imageConverterService.ConvertToPngAsync(targetPath, tempPng, cancellationToken).ConfigureAwait(false);
            }

            return (tempPng, tempPng);
        }
        catch
        {
            _fileService.DeleteFile(tempPng);
            throw;
        }
    }

    private static SourceFingerprint ReadFingerprint(string inputPath)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            throw new ArgumentException("File path cannot be null or empty.", nameof(inputPath));
        }

        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException($"File not found: {inputPath}");
        }

        var fi = new FileInfo(inputPath);
        return new SourceFingerprint(fi.FullName, fi.Length, fi.LastWriteTimeUtc.Ticks);
    }

    private static string ComputeHash(SourceFingerprint fp, OutputFormat? format, int quality, int? effort)
    {
        string formatToken = format?.ToString() ?? "orig";
        string raw = $"{CompareDefaults.CacheSchemaVersion}|{fp.FullPath}|{fp.FileSize}|{fp.LastWriteTicks}|{formatToken}|{quality}|{effort?.ToString() ?? "-"}";
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string GetExtension(OutputFormat format) => format switch
    {
        OutputFormat.Jxl => "jxl",
        OutputFormat.Jpeg => "jpg",
        OutputFormat.Avif => "avif",
        _ => throw new ArgumentOutOfRangeException(nameof(format), $"Unsupported output format: {format}")
    };

    private static bool IsMissingTool(Exception exception, string toolName)
    {
        for (Exception? current = exception; current != null; current = current.InnerException)
        {
            if ((current is FileNotFoundException || current is Win32Exception) &&
                current.Message.Contains(toolName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsCacheValid(string metaPath, SourceFingerprint fp)
    {
        var meta = ReadMeta(metaPath);
        if (meta == null || !File.Exists(meta.InputPath))
        {
            return false;
        }

        try
        {
            var fi = new FileInfo(meta.InputPath);
            return fi.Length == meta.FileSize && fi.LastWriteTimeUtc.Ticks == meta.LastWriteTicks;
        }
        catch
        {
            return false;
        }
    }

    private static void WriteMeta(string metaPath, SourceFingerprint fp, int width, int height)
    {
        var meta = new CacheMeta
        {
            InputPath = fp.FullPath,
            FileSize = fp.FileSize,
            LastWriteTicks = fp.LastWriteTicks,
            Width = width,
            Height = height,
            CreatedUtc = DateTime.UtcNow
        };
        File.WriteAllText(metaPath, JsonSerializer.Serialize(meta));
    }

    private static CacheMeta? ReadMeta(string metaPath)
    {
        try
        {
            if (!File.Exists(metaPath))
            {
                return null;
            }

            return JsonSerializer.Deserialize<CacheMeta>(File.ReadAllText(metaPath));
        }
        catch
        {
            return null;
        }
    }

    private static void TryDeleteDirectory(string dir)
    {
        try
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
        }
        catch
        {
        }
    }

    private void EnforceSizeCap(List<(string Dir, DateTime CreatedUtc)> entries)
    {
        long total = 0;
        foreach (var (dir, _) in entries)
        {
            total += GetDirectorySize(dir);
        }

        if (total <= CompareDefaults.CacheMaxBytes)
        {
            return;
        }

        foreach (var (dir, _) in entries.OrderBy(e => e.CreatedUtc))
        {
            if (total <= CompareDefaults.CacheMaxBytes)
            {
                break;
            }

            total -= GetDirectorySize(dir);
            TryDeleteDirectory(dir);
        }
    }

    private static long GetDirectorySize(string dir)
    {
        try
        {
            return Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
                .Sum(f => { try { return new FileInfo(f).Length; } catch { return 0; } });
        }
        catch
        {
            return 0;
        }
    }

    private readonly record struct SourceFingerprint(string FullPath, long FileSize, long LastWriteTicks);

    private sealed class CacheMeta
    {
        public string InputPath { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public long LastWriteTicks { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public DateTime CreatedUtc { get; set; }
    }
}
