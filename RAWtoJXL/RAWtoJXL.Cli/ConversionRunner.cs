using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RAWtoJXL.Core.Interfaces;
using RAWtoJXL.Core.Models;
using RAWtoJXL.Core.Services;
using RAWtoJXL.Core.Settings;

namespace RAWtoJXL.Cli
{
    public sealed class FileResult
    {
        public string Input { get; init; } = string.Empty;
        public string? Output { get; init; }
        public string Status { get; init; } = string.Empty;
        public string? Error { get; init; }
        public long InputBytes { get; init; }
        public long OutputBytes { get; init; }
    }

    public sealed class BatchResult
    {
        public int Total { get; init; }
        public int Converted { get; init; }
        public int Skipped { get; init; }
        public int Failed { get; init; }
        public bool Cancelled { get; init; }
        public IReadOnlyList<FileResult> Files { get; init; } = Array.Empty<FileResult>();
    }

    public sealed class ConversionRunner
    {
        private readonly IImageService _imageService;

        public ConversionRunner(IImageService imageService)
        {
            _imageService = imageService;
        }

        public async Task<BatchResult> RunAsync(
            IReadOnlyList<string> files,
            ResolvedOptions options,
            int jobs,
            Action<int, int, double, string>? progress,
            Action<FileResult, int>? fileCompleted,
            CancellationToken cancellationToken)
        {
            if (jobs <= 0)
            {
                jobs = ParallelismPolicy.ResolveDefaultJobs();
            }
            jobs = Math.Min(jobs, files.Count == 0 ? 1 : files.Count);

            var effectiveThreads = options.Threads ?? Math.Max(1, Environment.ProcessorCount / jobs);

            if (jobs == 1)
            {
                return await RunSequentialAsync(files, options, effectiveThreads, progress, fileCompleted, cancellationToken);
            }
            return await RunParallelAsync(files, options, jobs, effectiveThreads, fileCompleted, cancellationToken);
        }

        private async Task<BatchResult> RunSequentialAsync(
            IReadOnlyList<string> files,
            ResolvedOptions options,
            int effectiveThreads,
            Action<int, int, double, string>? progress,
            Action<FileResult, int>? fileCompleted,
            CancellationToken cancellationToken)
        {
            int converted = 0, skipped = 0, failed = 0;
            var cancelled = false;
            var results = new List<FileResult>(files.Count);

            for (var i = 0; i < files.Count; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    cancelled = true;
                    break;
                }

                var file = files[i];
                var outputPath = OutputPathResolver.Resolve(
                    file,
                    options.Format,
                    options.Conflict,
                    options.UseCustomOutputDirectory,
                    options.CustomOutputDirectory,
                    options.UseSubfolder,
                    options.SubfolderName);

                if (outputPath == null)
                {
                    skipped++;
                    var skippedResult = new FileResult { Input = file, Status = "skipped" };
                    results.Add(skippedResult);
                    fileCompleted?.Invoke(skippedResult, results.Count(r => r.Status is "converted" or "skipped" or "failed"));
                    progress?.Invoke(i + 1, files.Count, 1.0, file);
                    continue;
                }

                long sourceSize = 0;
                try { sourceSize = new FileInfo(file).Length; } catch { }

                try
                {
                    await _imageService.ConvertToJxlAsync(
                        file,
                        outputPath,
                        p => progress?.Invoke(i + 1, files.Count, p, file),
                        options.Quality,
                        options.Format,
                        cancellationToken,
                        options.SkipMetadata,
                        options.Effort,
                        effectiveThreads);

                    long outputSize = 0;
                    try { outputSize = new FileInfo(outputPath).Length; } catch { }

                    converted++;
                    var result = new FileResult
                    {
                        Input = file,
                        Output = outputPath,
                        Status = "converted",
                        InputBytes = sourceSize,
                        OutputBytes = outputSize
                    };
                    results.Add(result);
                    fileCompleted?.Invoke(result, converted + skipped + failed);
                }
                catch (OperationCanceledException)
                {
                    cancelled = true;
                    var result = new FileResult { Input = file, Status = "cancelled" };
                    results.Add(result);
                    fileCompleted?.Invoke(result, converted + skipped + failed);
                    break;
                }
                catch (FileLockedException ex)
                {
                    failed++;
                    var result = new FileResult { Input = file, Status = "failed", Error = ex.Message, InputBytes = sourceSize };
                    results.Add(result);
                    fileCompleted?.Invoke(result, converted + skipped + failed);
                }
                catch (Exception ex)
                {
                    failed++;
                    var result = new FileResult { Input = file, Status = "failed", Error = ex.Message, InputBytes = sourceSize };
                    results.Add(result);
                    fileCompleted?.Invoke(result, converted + skipped + failed);
                }

                progress?.Invoke(i + 1, files.Count, 1.0, file);
            }

            return new BatchResult
            {
                Total = files.Count,
                Converted = converted,
                Skipped = skipped,
                Failed = failed,
                Cancelled = cancelled,
                Files = results
            };
        }

        private async Task<BatchResult> RunParallelAsync(
            IReadOnlyList<string> files,
            ResolvedOptions options,
            int jobs,
            int effectiveThreads,
            Action<FileResult, int>? fileCompleted,
            CancellationToken cancellationToken)
        {
            var inputs = files.Select((file, index) => new BatchConversionInput(
                index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                file,
                options.Quality)).ToList();
            var requests = BatchConversionPlanner.CreateRequests(
                inputs,
                options.Format,
                options.Conflict,
                options.UseCustomOutputDirectory,
                options.CustomOutputDirectory,
                options.UseSubfolder,
                options.SubfolderName,
                options.SkipMetadata,
                options.Effort,
                effectiveThreads);

            var batchService = new BatchConversionService(_imageService);
            var batch = await batchService.RunAsync(
                requests,
                jobs,
                progress: null,
                fileCompleted: fileCompleted == null
                    ? null
                    : (result, completed) =>
                    {
                        fileCompleted(ToFileResult(result), completed);
                        return Task.CompletedTask;
                    },
                cancellationToken: cancellationToken);

            return new BatchResult
            {
                Total = batch.Total,
                Converted = batch.Converted,
                Skipped = batch.Skipped,
                Failed = batch.Failed,
                Cancelled = batch.Cancelled,
                Files = batch.Files.Select(ToFileResult).ToList()
            };
        }

        private static FileResult ToFileResult(BatchConversionFileResult result)
        {
            return new FileResult
            {
                Input = result.InputPath,
                Output = result.OutputPath,
                Status = result.Status.ToString().ToLowerInvariant(),
                Error = result.Error,
                InputBytes = result.InputBytes,
                OutputBytes = result.OutputBytes
            };
        }

        private async Task<FileResult> ConvertOneAsync(
            string file,
            string? outputPath,
            ResolvedOptions options,
            int effectiveThreads,
            CancellationToken cancellationToken)
        {
            if (outputPath == null)
            {
                return new FileResult { Input = file, Status = "skipped" };
            }

            long sourceSize = 0;
            try { sourceSize = new FileInfo(file).Length; } catch { }

            try
            {
                await _imageService.ConvertToJxlAsync(
                    file,
                    outputPath,
                    _ => { },
                    options.Quality,
                    options.Format,
                    cancellationToken,
                    options.SkipMetadata,
                    options.Effort,
                    effectiveThreads);

                long outputSize = 0;
                try { outputSize = new FileInfo(outputPath).Length; } catch { }

                return new FileResult
                {
                    Input = file,
                    Output = outputPath,
                    Status = "converted",
                    InputBytes = sourceSize,
                    OutputBytes = outputSize
                };
            }
            catch (OperationCanceledException)
            {
                return new FileResult { Input = file, Status = "cancelled" };
            }
            catch (FileLockedException ex)
            {
                return new FileResult { Input = file, Status = "failed", Error = ex.Message, InputBytes = sourceSize };
            }
            catch (Exception ex)
            {
                return new FileResult { Input = file, Status = "failed", Error = ex.Message, InputBytes = sourceSize };
            }
        }
    }
}
