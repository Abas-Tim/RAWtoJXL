using System.Collections.Concurrent;
using Moq;
using RAWtoJXL.Core.Interfaces;
using RAWtoJXL.Core.Models;
using RAWtoJXL.Core.Services;
using RAWtoJXL.Core.Settings;

namespace RAWtoJXL.Tests;

public sealed class BatchConversionServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "RAWtoJXL_Batch_" + Guid.NewGuid().ToString("N"));

    public BatchConversionServiceTests()
    {
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public async Task JobsGreaterThanOne_OverlapsWorkAndNeverExceedsBudget()
    {
        var requests = CreateRequests(4);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var active = 0;
        var peak = 0;
        var service = CreateService(async (input, output, _, _, _, cancellationToken, _, _, _) =>
        {
            var current = Interlocked.Increment(ref active);
            UpdateMaximum(ref peak, current);
            if (current >= 2)
            {
                entered.TrySetResult();
            }

            try
            {
                await release.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
                File.WriteAllText(output, input);
            }
            finally
            {
                Interlocked.Decrement(ref active);
            }
        });

        var runTask = service.RunAsync(requests, jobs: 2);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(2, peak);
            Assert.Equal(2, Volatile.Read(ref active));
        }
        finally
        {
            release.TrySetResult();
        }

        var batch = await runTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(4, batch.Converted);
        Assert.Equal(2, batch.PeakActiveJobs);
    }

    [Fact]
    public async Task JobsOne_IsSequentialWhenFirstFileIsHeld()
    {
        var requests = CreateRequests(2);
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = CreateService(async (input, output, _, _, _, cancellationToken, _, _, _) =>
        {
            if (input.EndsWith("0.arw", StringComparison.OrdinalIgnoreCase))
            {
                firstStarted.TrySetResult();
                await release.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
            }
            else
            {
                secondStarted.TrySetResult();
            }

            File.WriteAllText(output, input);
        });

        var runTask = service.RunAsync(requests, jobs: 1);
        try
        {
            await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(secondStarted.Task.IsCompleted);
        }
        finally
        {
            release.TrySetResult();
        }

        var batch = await runTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, batch.Converted);
        Assert.Equal(1, batch.PeakActiveJobs);
    }

    [Fact]
    public async Task FailedFile_DoesNotStopOtherWorkersAndKeepsExistingOutput()
    {
        var requests = CreateRequests(2);
        var existingOutput = requests[0].OutputPath!;
        File.WriteAllText(existingOutput, "original");

        var service = CreateService((input, output, _, _, _, _, _, _, _) =>
        {
            File.WriteAllText(output, "partial");
            if (input.EndsWith("0.arw", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromException(new IOException("simulated failure"));
            }

            return Task.CompletedTask;
        });

        var batch = await service.RunAsync(requests, jobs: 2);

        Assert.Equal(1, batch.Converted);
        Assert.Equal(1, batch.Failed);
        Assert.Equal("original", File.ReadAllText(existingOutput));
        Assert.Empty(Directory.GetFiles(_directory, "*.rawtojxl.*"));
    }

    [Fact]
    public async Task SkipConflictCreatedAfterPreflight_DoesNotReplaceExternalFile()
    {
        var requests = CreateRequests(1, ConflictResolution.Skip);
        var output = requests[0].OutputPath!;
        var service = CreateService((_, temporaryOutput, _, _, _, _, _, _, _) =>
        {
            File.WriteAllText(temporaryOutput, "converted");
            File.WriteAllText(output, "created-after-preflight");
            return Task.CompletedTask;
        });

        var batch = await service.RunAsync(requests, jobs: 1);

        Assert.Equal(0, batch.Converted);
        Assert.Equal(1, batch.Skipped);
        Assert.Equal("created-after-preflight", File.ReadAllText(output));
        Assert.Empty(Directory.GetFiles(_directory, "*.rawtojxl.*"));
    }

    [Fact]
    public async Task CompletionCallbackFailure_DoesNotBreakBatchFinalization()
    {
        var requests = CreateRequests(3);
        var service = CreateService((_, output, _, _, _, _, _, _, _) =>
        {
            File.WriteAllText(output, "converted");
            return Task.CompletedTask;
        });

        var batch = await service.RunAsync(
            requests,
            jobs: 2,
            fileCompleted: (_, _) => throw new InvalidOperationException("callback failure"));

        Assert.Equal(3, batch.Converted);
        Assert.Equal(3, batch.Files.Count);
    }

    [Fact]
    public async Task CancellationDuringConversion_CleansOwnedTemporaryOutputsAndCancelsRemainingFiles()
    {
        var requests = CreateRequests(4);
        using var cancellation = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var startCount = 0;
        var service = CreateService(async (input, output, _, _, _, cancellationToken, _, _, _) =>
        {
            if (Interlocked.Increment(ref startCount) == 2)
            {
                started.TrySetResult();
            }

            File.WriteAllText(output, "partial");
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        });

        var runTask = service.RunAsync(requests, jobs: 2, cancellationToken: cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        var batch = await runTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(batch.Cancelled);
        Assert.Equal(0, batch.Converted);
        Assert.All(batch.Files, result => Assert.Equal(BatchConversionStatus.Cancelled, result.Status));
        Assert.Empty(Directory.GetFiles(_directory, "*.rawtojxl.*"));
        Assert.All(requests, request => Assert.False(File.Exists(request.OutputPath)));
    }

    [Fact]
    public async Task Progress_ClampsActiveFilesAndReachesOneOnlyAfterOutOfOrderCompletion()
    {
        var requests = CreateRequests(2);
        var slowRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var slowProgressReported = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fastCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var progress = new ConcurrentQueue<BatchConversionProgress>();
        var service = CreateService(async (input, output, report, _, _, _, _, _, _) =>
        {
            if (input.EndsWith("0.arw", StringComparison.OrdinalIgnoreCase))
            {
                report(0.9);
                slowProgressReported.TrySetResult();
                await slowRelease.Task.WaitAsync(TimeSpan.FromSeconds(5));
            }
            else
            {
                await slowProgressReported.Task.WaitAsync(TimeSpan.FromSeconds(5));
                report(0.2);
            }

            File.WriteAllText(output, "converted");
        });

        var runTask = service.RunAsync(
            requests,
            jobs: 2,
            progress: value => progress.Enqueue(value),
            fileCompleted: (result, _) =>
            {
                if (result.InputPath.EndsWith("1.arw", StringComparison.OrdinalIgnoreCase))
                {
                    fastCompleted.TrySetResult();
                }

                return Task.CompletedTask;
            });

        try
        {
            await fastCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var afterFastCompletion = progress
                .Where(value => value.CompletedCount == 1 && value.FileId == "1")
                .ToArray();
            Assert.NotEmpty(afterFastCompletion);
            Assert.InRange(afterFastCompletion[^1].OverallFraction, 0.949999, 0.950001);
            Assert.All(progress.Where(value => value.CompletedCount < value.TotalCount), value =>
                Assert.InRange(value.OverallFraction, 0, 0.999999));
        }
        finally
        {
            slowRelease.TrySetResult();
        }

        var batch = await runTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, progress.Last().OverallFraction);
        Assert.Equal(2, batch.Converted);
    }

    private List<BatchConversionRequest> CreateRequests(
        int count,
        ConflictResolution conflict = ConflictResolution.Overwrite)
    {
        var requests = new List<BatchConversionRequest>(count);
        for (var index = 0; index < count; index++)
        {
            var input = Path.Combine(_directory, $"{index}.arw");
            var output = Path.Combine(_directory, $"{index}.jxl");
            File.WriteAllText(input, "input");
            requests.Add(new BatchConversionRequest(
                index.ToString(),
                input,
                output,
                90,
                OutputFormat.Jxl,
                true,
                7,
                2,
                conflict));
        }

        return requests;
    }

    private static BatchConversionService CreateService(
        Func<string, string, Action<double>, int, OutputFormat, CancellationToken, bool, int?, int?, Task> conversion)
    {
        var imageService = new Mock<IImageService>();
        imageService.Setup(service => service.ConvertToJxlAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Action<double>>(),
                It.IsAny<int>(),
                It.IsAny<OutputFormat>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()))
            .Returns(conversion);
        return new BatchConversionService(imageService.Object);
    }

    private static void UpdateMaximum(ref int location, int value)
    {
        while (true)
        {
            var current = Volatile.Read(ref location);
            if (value <= current || Interlocked.CompareExchange(ref location, value, current) == current)
            {
                return;
            }
        }
    }
}
