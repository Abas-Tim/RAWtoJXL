using RAWtoJXL.Cli;

namespace RAWtoJXL.Tests.Cli;

public class ParallelismPolicyTests
{
    private const long PlentyRam = 32L * 1024 * 1024 * 1024;

    [Fact]
    public void DefaultJobs_IsTwo()
    {
        Assert.Equal(2, ParallelismPolicy.DefaultJobs);
    }

    [Fact]
    public void SafeMaxJobs_IsWithinHardCap()
    {
        Assert.InRange(ParallelismPolicy.SafeMaxJobs, 1, ParallelismPolicy.HardCap);
    }

    [Theory]
    [InlineData(2, 1)]
    [InlineData(4, 1)]
    [InlineData(6, 2)]
    [InlineData(8, 2)]
    [InlineData(12, 3)]
    [InlineData(16, 4)]
    [InlineData(24, 4)]
    [InlineData(32, 4)]
    [InlineData(64, 4)]
    [InlineData(128, 4)]
    public void ComputeSafeMax_ScalesWithCpuAndCapsAtHardCap(int logicalProcessors, int expected)
    {
        Assert.Equal(expected, ParallelismPolicy.ComputeSafeMax(logicalProcessors, PlentyRam));
    }

    [Theory]
    [InlineData(3L * 1024 * 1024 * 1024, 1)]
    [InlineData(6L * 1024 * 1024 * 1024, 2)]
    [InlineData(16L * 1024 * 1024 * 1024, 4)]
    public void ComputeSafeMax_ScalesWithMemory(long availableMemoryBytes, int expected)
    {
        Assert.Equal(expected, ParallelismPolicy.ComputeSafeMax(16, availableMemoryBytes));
    }

    [Fact]
    public void ComputeSafeMax_MinimumIsOneEvenOnTinyHosts()
    {
        Assert.Equal(1, ParallelismPolicy.ComputeSafeMax(1, 1L * 1024 * 1024 * 1024));
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(4, false)]
    [InlineData(5, true)]
    [InlineData(8, true)]
    public void IsAboveSafeMax_OnSixteenLogicalHost(int jobs, bool expected)
    {
        var safeMax = ParallelismPolicy.ComputeSafeMax(16, PlentyRam);
        Assert.Equal(expected, jobs > safeMax);
    }
}
