using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RAWtoJXL.Core.Interfaces;
using RAWtoJXL.Core.Models;

namespace RAWtoJXL.Core.Services;

/// <summary>
/// Runs complete per-file conversions with a fixed worker pool. A worker owns
/// preparation, encoding, metadata, and promotion for one file, so metadata
/// work cannot create an unbounded backlog behind the encoder.
/// </summary>
public sealed class BatchConversionService : IBatchConversionService
{
    private readonly IImageService _imageService;
    private readonly ILogger? _logger;
    private readonly bool _useAtomicOutputs;
    private readonly object _promotionGate = new();

    public BatchConversionService(
        IImageService imageService,
        ILogger? logger = null,
        bool useAtomicOutputs = true)
    {
        _imageService = imageService ?? throw new ArgumentNullException(nameof(imageService));
        _logger = logger;
        _useAtomicOutputs = useAtomicOutputs;
    }

    public async Task<BatchConversionBatchResult> RunAsync(
        IReadOnlyList<BatchConversionRequest> requests,
        int jobs,
        Action<BatchConversionProgress>? progress = null,
        Func<BatchConversionFileResult, int, Task>? fileCompleted = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requests);

        var batchId = Guid.NewGuid().ToString("N");
        var total = requests.Count;
        if (total == 0)
        {
            return new BatchConversionBatchResult(
                batchId, 0, 0, 0, 0, false, 0, Array.Empty<BatchConversionFileResult>());
        }

        var workerCount = Math.Min(Math.Max(1, jobs), total);
        var results = new BatchConversionFileResult?[total];
        var fractions = new double[total];
        var stateGate = new object();
        using var callbackGate = new SemaphoreSlim(1, 1);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var nextIndex = -1;
        var completedCount = 0;
        var activeJobs = 0;
        var peakActiveJobs = 0;
        var fractionTotal = 0.0;

        BatchConversionProgress SnapshotProgress(int index, double fileFraction)
        {
            lock (stateGate)
            {
                var overall = total == 0
                    ? 1.0
                    : (completedCount + fractionTotal) / total;
                if (completedCount < total)
                {
                    overall = Math.Min(0.999999, overall);
                }

                return new BatchConversionProgress(
                    batchId,
                    requests[index].Id,
                    completedCount,
                    total,
                    fileFraction,
                    Math.Clamp(overall, 0, 1),
                    activeJobs,
                    peakActiveJobs);
            }
        }

        void ReportProgress(int index, double value)
        {
            var fileFraction = Math.Clamp(value, 0, 0.999999);
            BatchConversionProgress snapshot;
            lock (stateGate)
            {
                fractionTotal += fileFraction - fractions[index];
                fractions[index] = fileFraction;
                snapshot = SnapshotProgress(index, fileFraction);
            }

            InvokeProgressSafely(progress, snapshot);
        }

        async Task CompleteAsync(int index, BatchConversionFileResult result)
        {
            BatchConversionProgress snapshot;
            int completedNow;
            lock (stateGate)
            {
                fractionTotal += 1.0 - fractions[index];
                fractions[index] = 1.0;
                results[index] = result;
                completedCount++;
                completedNow = completedCount;
                snapshot = SnapshotProgress(index, 1.0);
            }

            InvokeProgressSafely(progress, snapshot);

            if (fileCompleted == null)
            {
                return;
            }

            await callbackGate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                try
                {
                    await fileCompleted(result, completedNow).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger?.Write($"[BatchConversion] completion callback failed for {result.InputPath}: {ex.GetBaseException().Message}");
                }
            }
            finally
            {
                callbackGate.Release();
            }
        }

        async Task WorkerAsync()
        {
            while (true)
            {
                var index = Interlocked.Increment(ref nextIndex);
                if (index >= total)
                {
                    return;
                }

                var request = requests[index];
                BatchConversionFileResult result;

                if (linkedCts.IsCancellationRequested)
                {
                    result = Cancelled(request);
                }
                else if (request.InitialStatus.HasValue)
                {
                    result = InitialResult(request);
                }
                else if (string.IsNullOrWhiteSpace(request.OutputPath))
                {
                    result = Failed(request, "no output path was reserved");
                }
                else
                {
                    lock (stateGate)
                    {
                        activeJobs++;
                        peakActiveJobs = Math.Max(peakActiveJobs, activeJobs);
                    }

                    try
                    {
                        result = await ConvertOneAsync(
                            request,
                            value => ReportProgress(index, value),
                            linkedCts.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException ex)
                    {
                        linkedCts.Cancel();
                        result = Cancelled(request, ex.Message);
                    }
                    catch (Exception ex)
                    {
                        result = Failed(request, ex.GetBaseException().Message);
                    }
                    finally
                    {
                        lock (stateGate)
                        {
                            activeJobs--;
                        }
                    }
                }

                if (result.Status == BatchConversionStatus.Cancelled)
                {
                    linkedCts.Cancel();
                }

                await CompleteAsync(index, result).ConfigureAwait(false);
            }
        }

        var workers = Enumerable.Range(0, workerCount)
            .Select(_ => WorkerAsync())
            .ToArray();
        await Task.WhenAll(workers).ConfigureAwait(false);

        var orderedResults = results
            .Select(result => result ?? throw new InvalidOperationException("batch worker did not produce a result"))
            .ToArray();

        return new BatchConversionBatchResult(
            batchId,
            total,
            orderedResults.Count(r => r.Status == BatchConversionStatus.Converted),
            orderedResults.Count(r => r.Status == BatchConversionStatus.Skipped),
            orderedResults.Count(r => r.Status == BatchConversionStatus.Failed),
            orderedResults.Any(r => r.Status == BatchConversionStatus.Cancelled),
            peakActiveJobs,
            orderedResults);
    }

    private async Task<BatchConversionFileResult> ConvertOneAsync(
        BatchConversionRequest request,
        Action<double> progress,
        CancellationToken cancellationToken)
    {
        var outputPath = request.OutputPath!;
        var sourceBytes = GetFileSize(request.InputPath);
        string? temporaryPath = null;

        try
        {
            var outputDirectory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            var conversionPath = outputPath;
            if (_useAtomicOutputs)
            {
                temporaryPath = CreateTemporaryPath(outputPath);
                conversionPath = temporaryPath;
            }

            await _imageService.ConvertToJxlAsync(
                request.InputPath,
                conversionPath,
                progress,
                request.Quality,
                request.OutputFormat,
                cancellationToken,
                request.SkipMetadata,
                request.Effort,
                request.Threads).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            if (_useAtomicOutputs && File.Exists(temporaryPath))
            {
                var promotion = PromoteTemporaryFile(temporaryPath, outputPath, request.Conflict);
                if (promotion.Skipped)
                {
                    return new BatchConversionFileResult(
                        request.Id,
                        request.InputPath,
                        null,
                        BatchConversionStatus.Skipped,
                        "output was created after preflight",
                        sourceBytes,
                        0);
                }

                outputPath = promotion.OutputPath!;
                temporaryPath = null;
            }
            else if (_useAtomicOutputs)
            {
                // IImageService implementations materialize the output. Keeping
                // the successful result here also makes the scheduler usable with
                // logical test doubles that model completion without writing bytes.
                _logger?.Write($"[BatchConversion] conversion reported success without a temporary output: {request.InputPath}");
            }

            return new BatchConversionFileResult(
                request.Id,
                request.InputPath,
                outputPath,
                BatchConversionStatus.Converted,
                null,
                sourceBytes,
                GetFileSize(outputPath));
        }
        catch
        {
            throw;
        }
        finally
        {
            if (temporaryPath != null)
            {
                TryDelete(temporaryPath);
            }
        }
    }

    private PromotionOutcome PromoteTemporaryFile(
        string temporaryPath,
        string outputPath,
        RAWtoJXL.Core.Settings.ConflictResolution conflict)
    {
        lock (_promotionGate)
        {
            if (conflict == RAWtoJXL.Core.Settings.ConflictResolution.Skip && File.Exists(outputPath))
            {
                return new PromotionOutcome(null, true);
            }

            if (conflict == RAWtoJXL.Core.Settings.ConflictResolution.Skip)
            {
                try
                {
                    File.Move(temporaryPath, outputPath);
                    return new PromotionOutcome(outputPath, false);
                }
                catch (IOException) when (File.Exists(outputPath))
                {
                    return new PromotionOutcome(null, true);
                }
            }

            if (conflict == RAWtoJXL.Core.Settings.ConflictResolution.AppendNumber)
            {
                var candidate = outputPath;
                var counter = 1;
                var directory = Path.GetDirectoryName(outputPath) ?? string.Empty;
                var baseName = Path.GetFileNameWithoutExtension(outputPath);
                var extension = Path.GetExtension(outputPath);

                while (true)
                {
                    if (!File.Exists(candidate))
                    {
                        try
                        {
                            File.Move(temporaryPath, candidate);
                            return new PromotionOutcome(candidate, false);
                        }
                        catch (IOException) when (File.Exists(candidate))
                        {
                            // Another process created the candidate between the
                            // existence check and the move; select the next name.
                        }
                    }

                    candidate = Path.Combine(directory, $"{baseName}_{counter}{extension}");
                    counter++;
                }
            }

            File.Move(temporaryPath, outputPath, overwrite: true);
            return new PromotionOutcome(outputPath, false);
        }
    }

    private sealed record PromotionOutcome(string? OutputPath, bool Skipped);

    private static string CreateTemporaryPath(string outputPath)
    {
        var directory = Path.GetDirectoryName(outputPath) ?? Path.GetTempPath();
        var fileName = Path.GetFileName(outputPath);
        return Path.Combine(directory, $".{fileName}.{Guid.NewGuid():N}.rawtojxl{Path.GetExtension(outputPath)}");
    }

    private static BatchConversionFileResult InitialResult(BatchConversionRequest request)
    {
        return new BatchConversionFileResult(
            request.Id,
            request.InputPath,
            request.OutputPath,
            request.InitialStatus!.Value,
            request.InitialError,
            GetFileSize(request.InputPath),
            0);
    }

    private static BatchConversionFileResult Failed(BatchConversionRequest request, string error)
    {
        return new BatchConversionFileResult(
            request.Id,
            request.InputPath,
            request.OutputPath,
            BatchConversionStatus.Failed,
            error,
            GetFileSize(request.InputPath),
            0);
    }

    private static BatchConversionFileResult Cancelled(BatchConversionRequest request, string? error = null)
    {
        return new BatchConversionFileResult(
            request.Id,
            request.InputPath,
            request.OutputPath,
            BatchConversionStatus.Cancelled,
            string.IsNullOrWhiteSpace(error) ? "cancelled" : error,
            GetFileSize(request.InputPath),
            0);
    }

    private static long GetFileSize(string path)
    {
        try
        {
            return new FileInfo(path).Length;
        }
        catch
        {
            return 0;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    private void InvokeProgressSafely(Action<BatchConversionProgress>? callback, BatchConversionProgress value)
    {
        if (callback == null)
        {
            return;
        }

        try
        {
            callback(value);
        }
        catch (Exception ex)
        {
            _logger?.Write($"[BatchConversion] progress callback failed: {ex.GetBaseException().Message}");
        }
    }
}
