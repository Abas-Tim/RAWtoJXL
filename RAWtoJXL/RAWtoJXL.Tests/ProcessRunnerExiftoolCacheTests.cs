using System.Diagnostics;
using RAWtoJXL.Core.Interfaces;
using RAWtoJXL.Core.Services;

namespace RAWtoJXL.Tests;

public class ProcessRunnerExiftoolCacheTests
{
    private sealed class CountingRunner : SystemProcessRunner
    {
        public CountingRunner(ILogger logger) : base(logger) { }

        public int ProcessStarts { get; private set; }

        protected internal override Process? StartProcess(ProcessStartInfo startInfo)
        {
            ProcessStarts++;
            return base.StartProcess(startInfo);
        }
    }

    [Fact]
    public async Task FindExiftoolAsync_SecondCall_DoesNotSpawnAnyProbeProcess()
    {
        var runner = new CountingRunner(new FileLogger());

        var first = await runner.FindExiftoolAsync("test");
        Assert.False(string.IsNullOrEmpty(first), "bundled exiftool.exe should be discoverable from the app directory");
        int startsAfterFirstCall = runner.ProcessStarts;

        var second = await runner.FindExiftoolAsync("test");

        Assert.Equal(first, second);
        Assert.Equal(startsAfterFirstCall, runner.ProcessStarts);
    }

    [Fact]
    public async Task FindExiftoolAsync_ConcurrentCalls_AllResolveToSamePath()
    {
        var runner = new CountingRunner(new FileLogger());

        var results = await Task.WhenAll(
            Enumerable.Range(0, 8).Select(_ => Task.Run(() => runner.FindExiftoolAsync("test"))));

        Assert.All(results, r => Assert.False(string.IsNullOrEmpty(r)));
        Assert.All(results, r => Assert.Equal(results[0], r));
    }
}
