using System.Diagnostics;
using System.IO;
using RAWtoJXL.Core.Interfaces;
using RAWtoJXL.Core.Services;

namespace RAWtoJXL.Tests;

public class ProcessRunnerStdinFailureTests
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
    public async Task RunProcessWithStdinWriterAsync_WriterThrows_ProcessIsKilledAndExceptionRethrown()
    {
        var runner = new CapturingRunner(new FileLogger());
        Func<Stream, CancellationToken, Task> throwingWriter = (_, _) => throw new IOException("simulated pipe write failure");

        var ex = await Assert.ThrowsAsync<IOException>(() =>
            runner.RunProcessWithStdinWriterAsync(Ping, "-t 127.0.0.1", throwingWriter, timeoutSeconds: 30, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("simulated pipe write failure", ex.Message);
        Assert.NotNull(runner.LastProcessId);
        await WaitForProcessExitAsync(runner.LastProcessId!.Value);
    }

    [Fact]
    public async Task RunProcessWithStdinAsync_StreamReadThrows_ProcessIsKilledAndExceptionRethrown()
    {
        var runner = new CapturingRunner(new FileLogger());
        using var failingStream = new ThrowingReadStream();

        var ex = await Assert.ThrowsAsync<IOException>(() =>
            runner.RunProcessWithStdinAsync(Ping, "-t 127.0.0.1", failingStream, timeoutSeconds: 30, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("source read failed", ex.Message);
        Assert.NotNull(runner.LastProcessId);
        await WaitForProcessExitAsync(runner.LastProcessId!.Value);
    }

    [Fact]
    public async Task RunProcessWithStdinWriterAsync_WriterCompletes_ProcessExitsNormally()
    {
        var runner = new CapturingRunner(new FileLogger());
        var powershell = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        Func<Stream, CancellationToken, Task> writer = async (stream, _) =>
        {
            var bytes = System.Text.Encoding.ASCII.GetBytes("P6\n1 1\n255\n");
            await stream.WriteAsync(bytes);
            await stream.FlushAsync();
        };

        var result = await runner.RunProcessWithStdinWriterAsync(
            powershell,
            "-NoProfile -Command \"$input | Out-Null\"",
            writer,
            timeoutSeconds: 30,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.False(result.TimedOut);
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

    private sealed class ThrowingReadStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => 0; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new IOException("source read failed");
        public override int Read(Span<byte> buffer) => throw new IOException("source read failed");
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => throw new IOException("source read failed");
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => throw new IOException("source read failed");
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
