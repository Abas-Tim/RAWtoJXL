using RAWtoJXL.Core.Services;

namespace RAWtoJXL.Tests;

public class ProgressReportingTests
{
    private static readonly FileLogger Logger = new();

    [Fact]
    public async Task ReportProgressAsync_WithStreamingPhase_ReportsConstantThenMonotonicRamp()
    {
        var values = new List<double>();
        using var cts = new CancellationTokenSource();
        var phase = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var task = CjxlEncoderService.ReportProgressAsync(
            DateTime.UtcNow, TimeSpan.FromSeconds(60), phase.Task, values.Add, cts.Token, Logger);

        await Task.Delay(250);
        var duringStreaming = values.ToList();

        phase.SetResult();
        await Task.Delay(400);

        cts.Cancel();
        try { await task; } catch (OperationCanceledException) { }

        Assert.NotEmpty(duringStreaming);
        Assert.All(duringStreaming, v => Assert.Equal(0.05, v));
        Assert.Contains(values, v => v > 0.05);
        Assert.True(values.SequenceEqual(values.OrderBy(v => v)), "progress must be monotonic");
    }

    [Fact]
    public async Task ReportProgressAsync_NoStreamingPhase_StartsRampingImmediately()
    {
        var values = new List<double>();
        using var cts = new CancellationTokenSource();

        var task = CjxlEncoderService.ReportProgressAsync(
            DateTime.UtcNow, TimeSpan.FromSeconds(60), null, values.Add, cts.Token, Logger);

        await Task.Delay(300);

        cts.Cancel();
        try { await task; } catch (OperationCanceledException) { }

        Assert.NotEmpty(values);
        Assert.All(values, v => Assert.True(v > 0.05, $"expected values above the streaming constant, got {v}"));
        Assert.True(values.SequenceEqual(values.OrderBy(v => v)), "progress must be monotonic");
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
}
