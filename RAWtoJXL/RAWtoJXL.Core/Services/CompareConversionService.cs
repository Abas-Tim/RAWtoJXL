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
    private readonly ConcurrentDictionary<string, MasterLeaseState> _masterLeaseStates = new();
    private readonly SemaphoreSlim _analysisGate = new(1, 1);

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

    public void ClearCompareCache()
    {
        ClearCache(CompareDefaults.CacheRoot);
    }

    internal static void ClearCache(string root)
    {
        try
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
        catch
        {
        }
    }

    public async Task<MasterRenderLease> EnsureMasterRenderLeaseAsync(
        string inputPath,
        CancellationToken cancellationToken = default,
        int? renderThreads = null)
    {
        var fp = ReadFingerprint(inputPath);
        string dir = Path.Combine(CompareDefaults.CacheRoot, "master", $"m-{ComputeHash(fp, null, 0, 0)}");
        string masterPath = Path.Combine(dir, "master.png");
        string metaPath = Path.Combine(dir, "meta.json");

        if (IsCacheValid(metaPath, fp) && File.Exists(masterPath))
        {
            return MasterRenderLease.ForMaster(masterPath);
        }

        string ext = Path.GetExtension(inputPath).ToLowerInvariant();
        if (!SupportedFormats.IsRawFile(ext))
        {
            string master = await EnsureMasterPngAsync(inputPath, cancellationToken).ConfigureAwait(false);
            return MasterRenderLease.ForMaster(master);
        }

        if (_inflightMasters.TryGetValue(masterPath, out var runningMaster))
        {
            string promotedByLegacy = await runningMaster.ConfigureAwait(false);
            return MasterRenderLease.ForMaster(promotedByLegacy);
        }

        var state = _masterLeaseStates.GetOrAdd(dir, _ => new MasterLeaseState());
        bool takeSlot = false;

        await state.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsCacheValid(metaPath, fp) && File.Exists(masterPath))
            {
                return MasterRenderLease.ForMaster(masterPath);
            }

            if (state.PromotedTask.Task.IsCompleted)
            {
                await state.PromotedTask.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                return MasterRenderLease.ForMaster(masterPath);
            }

            if (state.ActiveSlots < CompareDefaults.MaxConcurrentMasterRenders)
            {
                state.ActiveSlots++;
                takeSlot = true;
            }
        }
        finally
        {
            state.Gate.Release();
        }

        if (!takeSlot)
        {
            await state.PromotedTask.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            return MasterRenderLease.ForMaster(masterPath);
        }

        string tempPath = Path.Combine(dir, $"master.slot-{Guid.NewGuid():N}.png");
        Directory.CreateDirectory(dir);
        try
        {
            try
            {
                await _rawRenderer.RenderToPngAsync(
                    inputPath,
                    tempPath,
                    Math.Max(1, renderThreads ?? CompareDefaults.JxlThreads),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (FileNotFoundException)
            {
                await DecodeRawWithMagickAsync(inputPath, tempPath, cancellationToken).ConfigureAwait(false);
            }

            bool promotedHere = false;
            await state.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (!state.Promoted && !(IsCacheValid(metaPath, fp) && File.Exists(masterPath)))
                {
                    File.Move(tempPath, masterPath);
                    WriteMeta(metaPath, fp, 0, 0);
                    state.Promoted = true;
                    promotedHere = true;
                }
                else
                {
                    state.Promoted = true;
                }
            }
            finally
            {
                state.Gate.Release();
            }

            if (promotedHere)
            {
                state.PromotedTask.TrySetResult(true);
                ReleaseSlot(state, dir);
                return MasterRenderLease.ForMaster(masterPath);
            }

            ReleaseSlot(state, dir);
            return MasterRenderLease.ForTemp(tempPath);
        }
        catch (Exception ex)
        {
            bool isLast = ReleaseSlot(state, dir);
            if (isLast && !state.Promoted)
            {
                state.PromotedTask.TrySetException(ex);
            }

            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch
            {
            }

            throw;
        }
    }

    public async Task<string> EnsureTargetFileFromRenderAsync(
        string inputPath,
        string renderedSourcePath,
        OutputFormat format,
        int quality,
        int? effort,
        CancellationToken cancellationToken = default,
        int? threads = null)
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

        TryDeleteDirectory(dir);

        var task = ConvertTargetAsync(renderedSourcePath, targetPath, metaPath, fp, format, quality, effort, cancellationToken, threads);
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

    public async Task<CompareDisplayPngs> EnsureDisplayPreviewFromRenderAsync(
        string inputPath,
        string sourcePath,
        bool decodeJxlTarget,
        OutputFormat? format,
        int quality,
        int? effort,
        CancellationToken cancellationToken = default,
        int? threads = null)
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

        var task = GenerateRenderedDisplayPngsAsync(
            inputPath, dir, previewPath, metaPath, fp, sourcePath, decodeJxlTarget, format, quality, effort, cancellationToken, threads);
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

    private async Task<CompareDisplayPngs> GenerateRenderedDisplayPngsAsync(
        string inputPath,
        string dir,
        string previewPath,
        string metaPath,
        SourceFingerprint fp,
        string sourcePath,
        bool decodeJxlTarget,
        OutputFormat? format,
        int quality,
        int? effort,
        CancellationToken cancellationToken,
        int? threads)
    {
        Directory.CreateDirectory(dir);
        string? decodedTemp = null;
        try
        {
            string effectiveSource = sourcePath;
            if (decodeJxlTarget && format == OutputFormat.Jxl)
            {
                decodedTemp = Path.Combine(dir, $"djxl_{Guid.NewGuid():N}.png");
                try
                {
                    await _jxlDecoder.DecodeToPngAsync(sourcePath, decodedTemp, cancellationToken, numThreads: threads).ConfigureAwait(false);
                }
                catch (Exception ex) when (IsMissingTool(ex, "djxl"))
                {
                    await _imageConverterService.ConvertToPngAsync(sourcePath, decodedTemp, cancellationToken).ConfigureAwait(false);
                }

                effectiveSource = decodedTemp;
            }

            var result = await Task.Run(() =>
            {
                (int Width, int Height) size = WriteDisplayPreview(effectiveSource, previewPath);
                return new CompareDisplayPngs(previewPath, previewPath, size.Width, size.Height);
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
            if (decodedTemp != null)
            {
                _fileService.DeleteFile(decodedTemp);
            }
        }
    }

    private static (int Width, int Height) WriteDisplayPreview(string sourcePath, string previewPath)
    {
        using var image = new MagickImage(sourcePath);
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
        image.Write(previewPath);

        return (width, height);
    }

    private bool ReleaseSlot(MasterLeaseState state, string dir)
    {
        lock (state)
        {
            state.ActiveSlots--;
            if (state.ActiveSlots == 0 && state.PromotedTask.Task.IsCompleted)
            {
                _masterLeaseStates.TryRemove(dir, out _);
            }

            return state.ActiveSlots == 0;
        }
    }

    private sealed class MasterLeaseState
    {
        public readonly SemaphoreSlim Gate = new(1, 1);
        public int ActiveSlots;
        public bool Promoted;
        public readonly TaskCompletionSource<bool> PromotedTask = new(TaskCreationOptions.RunContinuationsAsynchronously);
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
        CancellationToken cancellationToken = default,
        int? threads = null)
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

        var task = ConvertTargetAsync(masterPath, targetPath, metaPath, fp, format, quality, effort, cancellationToken, threads);
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
        CancellationToken cancellationToken = default,
        int? threads = null)
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

        var task = GenerateDisplayPngsAsync(inputPath, dir, previewPath, metaPath, fp, format, quality, effort, cancellationToken, threads);
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

        var task = _inflightFullDisplays.GetOrAdd(dir, key =>
        {
            var producer = GenerateDisplayFullPngAsync(
                inputPath, dir, fullPath, metaPath, fp, format, quality, effort, CancellationToken.None);
            _ = producer.ContinueWith(
                _ => _inflightFullDisplays.TryRemove(key, out var ignored),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            return producer;
        });
        return await task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<CompareViewportAnalysis> AnalyzeViewportAsync(
        string inputPath,
        OutputFormat format,
        int quality,
        int? effort,
        CompareImageRegion region,
        bool useFullResolution,
        int differenceWidth,
        int differenceHeight,
        bool includeDifference,
        CancellationToken cancellationToken = default)
    {
        string originalPath;
        string targetPath;
        if (useFullResolution)
        {
            originalPath = await EnsureDisplayFullPngAsync(
                inputPath, null, quality, effort, cancellationToken).ConfigureAwait(false);
            targetPath = await EnsureDisplayFullPngAsync(
                inputPath, format, quality, effort, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var original = await EnsureDisplayPngsAsync(
                inputPath, null, quality, effort, cancellationToken).ConfigureAwait(false);
            var target = await EnsureDisplayPngsAsync(
                inputPath, format, quality, effort, cancellationToken).ConfigureAwait(false);
            originalPath = original.PreviewPath;
            targetPath = target.PreviewPath;
        }

        await _analysisGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(
                () => AnalyzeImages(
                    originalPath,
                    targetPath,
                    region,
                    differenceWidth,
                    differenceHeight,
                    includeDifference,
                    cancellationToken),
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _analysisGate.Release();
        }
    }

    internal static CompareViewportAnalysis AnalyzeImages(
        string originalPath,
        string targetPath,
        CompareImageRegion region,
        int differenceWidth,
        int differenceHeight,
        bool includeDifference,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var original = new MagickImage(originalPath);
        using var target = new MagickImage(targetPath);

        if (original.Width != target.Width || original.Height != target.Height)
        {
            throw new InvalidOperationException(
                $"Comparison dimensions do not match: {original.Width}x{original.Height} and {target.Width}x{target.Height}.");
        }

        original.ColorSpace = ColorSpace.sRGB;
        original.ColorType = ColorType.TrueColor;
        target.ColorSpace = ColorSpace.sRGB;
        target.ColorType = ColorType.TrueColor;

        int imageWidth = (int)original.Width;
        int imageHeight = (int)original.Height;
        double left = Math.Clamp(region.Left, 0, 1);
        double top = Math.Clamp(region.Top, 0, 1);
        double right = Math.Clamp(region.Right, left, 1);
        double bottom = Math.Clamp(region.Bottom, top, 1);
        int x = Math.Clamp((int)Math.Floor(left * imageWidth), 0, imageWidth - 1);
        int y = Math.Clamp((int)Math.Floor(top * imageHeight), 0, imageHeight - 1);
        int cropRight = Math.Clamp((int)Math.Ceiling(right * imageWidth), x + 1, imageWidth);
        int cropBottom = Math.Clamp((int)Math.Ceiling(bottom * imageHeight), y + 1, imageHeight);
        int cropWidth = cropRight - x;
        int cropHeight = cropBottom - y;
        var geometry = new MagickGeometry(x, y, (uint)cropWidth, (uint)cropHeight);
        original.Crop(geometry);
        target.Crop(geometry);
        original.ResetPage();
        target.ResetPage();

        cancellationToken.ThrowIfCancellationRequested();
        double distortion = original.Compare(target, ErrorMetric.StructuralSimilarity);
        double ssim = Math.Clamp(1.0 - 2.0 * distortion, 0.0, 1.0);
        byte[]? differencePng = null;

        if (includeDifference)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var difference = target.Clone();
            difference.Composite(original, CompositeOperator.Difference);
            difference.Evaluate(
                Channels.Red | Channels.Green | Channels.Blue,
                EvaluateOperator.Multiply,
                CompareDefaults.DifferenceAmplification);
            difference.ColorSpace = ColorSpace.Gray;

            using var overlay = new MagickImage(MagickColors.Magenta, difference.Width, difference.Height);
            overlay.Alpha(AlphaOption.Set);
            overlay.Composite(difference, CompositeOperator.CopyAlpha);
            overlay.SetBitDepth(8);

            int outputWidth = Math.Clamp(differenceWidth, 1, CompareDefaults.DifferenceMaxDimension);
            int outputHeight = Math.Clamp(differenceHeight, 1, CompareDefaults.DifferenceMaxDimension);
            overlay.Resize(new MagickGeometry((uint)outputWidth, (uint)outputHeight)
            {
                IgnoreAspectRatio = true
            });
            overlay.Format = MagickFormat.Png;
            differencePng = overlay.ToByteArray();
        }

        cancellationToken.ThrowIfCancellationRequested();
        var sampledRegion = new CompareImageRegion(
            x / (double)imageWidth,
            y / (double)imageHeight,
            cropRight / (double)imageWidth,
            cropBottom / (double)imageHeight);
        return new CompareViewportAnalysis(ssim, sampledRegion, differencePng);
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

                    SweepAbandonedSlotTemps(dir);
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
        CancellationToken cancellationToken,
        int? threads)
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
                            threads: threads ?? CompareDefaults.JxlThreads).ConfigureAwait(false);
                    }
                catch (Exception ex) when (IsMissingTool(ex, "cjxl"))
                    {
                        await _imageConverterService.ConvertToJxlAsync(
                            masterPath, targetPath, quality, effort, cancellationToken, threads).ConfigureAwait(false);
                    }
                    break;
                case OutputFormat.Jpeg:
                    await _imageConverterService.ConvertToJpegAsync(masterPath, targetPath, quality, cancellationToken, threads).ConfigureAwait(false);
                    break;
                case OutputFormat.Avif:
                    await _imageConverterService.ConvertToAvifAsync(masterPath, targetPath, quality, cancellationToken, threads).ConfigureAwait(false);
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
        CancellationToken cancellationToken,
        int? threads)
    {
        Directory.CreateDirectory(dir);
        var source = await ResolveDisplaySourceAsync(inputPath, dir, format, quality, effort, cancellationToken, threads).ConfigureAwait(false);
        try
        {
            var result = await Task.Run(() =>
            {
                (int Width, int Height) size = WriteDisplayPreview(source.SourcePath, previewPath);
                return new CompareDisplayPngs(previewPath, previewPath, size.Width, size.Height);
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
        var source = await ResolveDisplaySourceAsync(inputPath, dir, format, quality, effort, cancellationToken, null).ConfigureAwait(false);
        try
        {
            await Task.Run(() =>
            {
                (int Width, int Height) size = WriteDisplayPreview(source.SourcePath, fullPath);

                var existingMeta = ReadMeta(metaPath);
                WriteMeta(metaPath, fp, existingMeta?.Width > 0 ? existingMeta.Width : size.Width, existingMeta?.Height > 0 ? existingMeta.Height : size.Height);
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
        CancellationToken cancellationToken,
        int? threads)
    {
        if (format == null)
        {
            return (await EnsureMasterPngAsync(inputPath, cancellationToken).ConfigureAwait(false), null);
        }

        string targetPath = await EnsureTargetFileAsync(
            inputPath, format.Value, quality, effort, cancellationToken, threads).ConfigureAwait(false);
        if (format != OutputFormat.Jxl)
        {
            return (targetPath, null);
        }

        string decodedPath = Path.Combine(Path.GetDirectoryName(targetPath)!, "decoded.png");
        string? owned = null;
        try
        {
            if (!File.Exists(decodedPath) || new FileInfo(decodedPath).Length == 0)
            {
                owned = decodedPath;
                try
                {
                    await _jxlDecoder.DecodeToPngAsync(targetPath, decodedPath, cancellationToken, numThreads: threads ?? CompareDefaults.JxlThreads).ConfigureAwait(false);
                }
                catch (Exception ex) when (IsMissingTool(ex, "djxl"))
                {
                    await _imageConverterService.ConvertToPngAsync(targetPath, decodedPath, cancellationToken).ConfigureAwait(false);
                }
            }

            return (decodedPath, owned);
        }
        catch
        {
            if (owned != null)
            {
                _fileService.DeleteFile(owned);
            }

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

    private static void SweepAbandonedSlotTemps(string dir)
    {
        try
        {
            foreach (string temp in Directory.EnumerateFiles(dir, "master.slot-*.png"))
            {
                try
                {
                    var info = new FileInfo(temp);
                    if ((DateTime.UtcNow - info.LastWriteTimeUtc).TotalHours >= 24)
                    {
                        info.Delete();
                    }
                }
                catch
                {
                }
            }
        }
        catch
        {
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
