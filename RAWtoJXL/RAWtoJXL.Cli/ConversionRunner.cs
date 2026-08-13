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
            var outputPaths = new string?[files.Count];
            var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var duplicateIndexes = new HashSet<int>();
            for (var i = 0; i < files.Count; i++)
            {
                var output = OutputPathResolver.Resolve(
                    files[i],
                    options.Format,
                    options.Conflict,
                    options.UseCustomOutputDirectory,
                    options.CustomOutputDirectory,
                    options.UseSubfolder,
                    options.SubfolderName,
                    createDirectory: false);
                if (output != null && options.Conflict == ConflictResolution.AppendNumber)
                {
                    var directory = Path.GetDirectoryName(output)!;
                    var baseName = Path.GetFileNameWithoutExtension(output);
                    var extension = Path.GetExtension(output);
                    var counter = 1;
                    while (File.Exists(output) || reserved.Contains(output))
                    {
                        output = Path.Combine(directory, $"{baseName}_{counter}{extension}");
                        counter++;
                    }
                }
                if (output != null && !reserved.Add(output))
                {
                    duplicateIndexes.Add(i);
                    outputPaths[i] = null;
                }
                else
                {
                    outputPaths[i] = output;
                }
            }

            var results = new FileResult?[files.Count];
            var converted = 0;
            var skipped = 0;
            var failed = 0;
            var cancelled = false;
            var completedCount = 0;
            var completionLock = new object();

            using var semaphore = new SemaphoreSlim(jobs);

            async Task ProcessFileAsync(int index)
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    FileResult result;
                    if (cancellationToken.IsCancellationRequested)
                    {
                        result = new FileResult { Input = files[index], Status = "cancelled" };
                    }
                    else
                    {
                        result = await ConvertOneAsync(files[index], outputPaths[index], options, effectiveThreads, cancellationToken);
                    }

                    results[index] = result;
                    int completedNow;
                    lock (completionLock)
                    {
                        completedCount++;
                        completedNow = completedCount;
                        switch (result.Status)
                        {
                            case "converted": converted++; break;
                            case "skipped": skipped++; break;
                            case "failed": failed++; break;
                            case "cancelled": cancelled = true; break;
                        }
                    }
                    fileCompleted?.Invoke(result, completedNow);
                }
                finally
                {
                    semaphore.Release();
                }
            }

            var tasks = new List<Task>(files.Count);
            for (var i = 0; i < files.Count; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    cancelled = true;
                    break;
                }
                if (duplicateIndexes.Contains(i))
                {
                    var duplicateResult = new FileResult
                    {
                        Input = files[i],
                        Status = "failed",
                        Error = "output path already used by another input file"
                    };
                    results[i] = duplicateResult;
                    int completedNow;
                    lock (completionLock)
                    {
                        completedCount++;
                        failed++;
                        completedNow = completedCount;
                    }
                    fileCompleted?.Invoke(duplicateResult, completedNow);
                    continue;
                }
                var index = i;
                tasks.Add(ProcessFileAsync(index));
            }

            await Task.WhenAll(tasks);

            var orderedResults = new List<FileResult>(files.Count);
            foreach (var result in results)
            {
                if (result != null) orderedResults.Add(result);
            }

            return new BatchResult
            {
                Total = files.Count,
                Converted = converted,
                Skipped = skipped,
                Failed = failed,
                Cancelled = cancelled,
                Files = orderedResults
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
