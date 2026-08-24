using System.Diagnostics;
using System.Numerics;
using RAWtoJXL.Core.Services;

namespace RAWtoJXL.Tests;

public class ProcessorAffinityServiceTests
{
    [Theory]
    [InlineData(1, 1UL)]
    [InlineData(12, 0xFFFUL)]
    [InlineData(24, 0xFFFFFFUL)]
    public void BuildAffinityMask_SetsAllProcessorBits(int processorCount, ulong expectedMask)
    {
        Assert.Equal(expectedMask, ProcessorAffinityService.BuildAffinityMask(processorCount));
    }

    [Fact]
    public void BuildAffinityMask_SixtyFourProcessorsUsesFullMask()
    {
        Assert.Equal(ulong.MaxValue, ProcessorAffinityService.BuildAffinityMask(64));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65)]
    public void BuildAffinityMask_InvalidProcessorCountReturnsZero(int processorCount)
    {
        Assert.Equal(0UL, ProcessorAffinityService.BuildAffinityMask(processorCount));
    }

    [Fact]
    public void GetCurrentAffinityProcessorCount_MatchesCurrentProcessAffinity()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var process = Process.GetCurrentProcess();
        int expected = BitOperations.PopCount(unchecked((ulong)process.ProcessorAffinity.ToInt64()));

        Assert.Equal(expected, ProcessorAffinityService.GetCurrentAffinityProcessorCount());
    }
}
