using System.Collections.Concurrent;
using RAWtoJXL.Core.Services;

namespace RAWtoJXL.Tests;

public class ProgressReportingTests
{
    private static readonly FileLogger Logger = new();

    [Fact]
    public async Task ReportProgressAsync_WithStreamingPhase_ReportsConstantThenMonotonicRamp()
    {
        var values = new ConcurrentQueue<double>();
        using var cts = new CancellationTokenSource();
        var phase = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var task = CjxlEncoderService.ReportProgressAsync(
            DateTime.UtcNow, TimeSpan.FromSeconds(60), phase.Task, values.Enqueue, cts.Token, Logger);

        await WaitUntilAsync(() => values.Count > 0, "expected at least one streaming-phase progress value");
        var duringStreaming = values.ToArray();

        phase.SetResult();
        await WaitUntilAsync(() => values.ToArray().Any(v => v > 0.05), "expected the encode ramp to start after the streaming phase");

        cts.Cancel();
        try { await task; } catch (OperationCanceledException) { }

        Assert.NotEmpty(duringStreaming);
        Assert.All(duringStreaming, v => Assert.Equal(0.05, v));
        Assert.True(values.ToArray().SequenceEqual(values.ToArray().OrderBy(v => v)), "progress must be monotonic");
    }

    [Fact]
    public async Task ReportProgressAsync_NoStreamingPhase_StartsRampingImmediately()
    {
        var values = new ConcurrentQueue<double>();
        using var cts = new CancellationTokenSource();

        var task = CjxlEncoderService.ReportProgressAsync(
            DateTime.UtcNow, TimeSpan.FromSeconds(60), null, values.Enqueue, cts.Token, Logger);

        await WaitUntilAsync(() => values.ToArray().Any(v => v > 0.05), "expected the encode ramp to start immediately");

        cts.Cancel();
        try { await task; } catch (OperationCanceledException) { }

        Assert.True(values.ToArray().SequenceEqual(values.ToArray().OrderBy(v => v)), "progress must be monotonic");
    }

    [Theory]
    [InlineData(1, 8, 60, 8)]
    [InlineData(20, 8, 60, 20)]
    [InlineData(100, 8, 60, 60)]
    [InlineData(5, 8, 60, 8)]
    public void ClampBudget_ClampsToRange(double seconds, double minSeconds, double maxSeconds, double expectedSeconds)
    {
        var result = CjxlEncoderService.ClampBudget(TimeSpan.FromSeconds(seconds), minSeconds, TimeSpan.FromSeconds(maxSeconds));

        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), result);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, string failureMessage)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail(failureMessage);
            }
            await Task.Delay(25);
        }
    }
}
