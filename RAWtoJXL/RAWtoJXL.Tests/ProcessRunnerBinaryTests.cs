using System.IO;
using System.Text;
using RAWtoJXL.Core.Services;

namespace RAWtoJXL.Tests;

public class ProcessRunnerBinaryTests
{
    [Fact]
    public async Task RunProcessBinaryAsync_CapturesStdoutWhileDrainingLargeStderr()
    {
        var runner = new SystemProcessRunner(new FileLogger());
        var powershell = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        var script = "$s = 'x' * 1048576; [Console]::Error.Write($s); [Console]::Out.Write('binary-stdout-ok')";

        var result = await runner.RunProcessBinaryAsync(
            powershell,
            $"-NoProfile -Command \"{script}\"",
            TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Contains("binary-stdout-ok", Encoding.ASCII.GetString(result!));
    }
}
