using System.Diagnostics;
using System.IO;
using RAWtoJXL.Core.Interfaces;
using RAWtoJXL.Core.Services;

namespace RAWtoJXL.Tests;

public class ProcessRunnerCancellationTests
{
    private sealed class CapturingRunner : SystemProcessRunner
    {
        public CapturingRunner(ILogger logger) : base(logger) { }

        public int? LastProcessId { get; private set; }

        protected internal override Process? StartProcess(ProcessStartInfo startInfo)
        {
            var process = base.StartProcess(startInfo);
            LastProcessId = process?.Id;
            return process;
        }
    }

    private static readonly string Ping = Path.Combine(Environment.SystemDirectory, "ping.exe");

    [Fact]
    public async Task RunProcessAsync_Cancelled_ThrowsOperationCanceledAndKillsProcess()
    {
        var runner = new CapturingRunner(new FileLogger());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            runner.RunProcessAsync(Ping, "-t 127.0.0.1", cts.Token));

        Assert.NotNull(runner.LastProcessId);
        await WaitForProcessExitAsync(runner.LastProcessId!.Value);
    }

    [Fact]
    public async Task RunProcessAsync_NotCancelled_CompletesNormally()
    {
        var runner = new CapturingRunner(new FileLogger());

        var result = await runner.RunProcessAsync(
            Ping,
            "-n 2 127.0.0.1",
            TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.False(string.IsNullOrEmpty(result.Stdout));
    }

    private static async Task WaitForProcessExitAsync(int pid)
    {
        for (int i = 0; i < 50; i++)
        {
            try
            {
                Process.GetProcessById(pid);
            }
            catch (ArgumentException)
            {
                return;
            }
            await Task.Delay(100);
        }
        Assert.Fail($"child process {pid} is still running after 5s");
    }
}
