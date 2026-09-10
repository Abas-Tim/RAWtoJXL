using System;
using RAWtoJXL.Core.Models;

namespace RAWtoJXL.Core.Services;

public static class BatchParallelismPolicy
{
    public const int DefaultJobs = 2;
    public const int HardCap = 4;

    public static int SafeMaxJobs => ComputeSafeMax(
        GetLogicalProcessorCount(),
        GC.GetGCMemoryInfo().TotalAvailableMemoryBytes);

    public static int ResolveDefaultJobs() => Math.Min(DefaultJobs, SafeMaxJobs);

    public static int ResolveJobs(int requestedJobs, int fileCount)
    {
        var desired = requestedJobs > 0 ? requestedJobs : ResolveDefaultJobs();
        desired = Math.Clamp(desired, 1, HardCap);
        return Math.Min(desired, Math.Max(1, fileCount));
    }

    public static int ResolveEncoderThreads(int configuredThreads, int jobs)
    {
        if (configuredThreads > 0)
        {
            return configuredThreads;
        }

        return Math.Max(1, GetLogicalProcessorCount() / Math.Max(1, jobs));
    }

    public static int GetLogicalProcessorCount()
    {
        var affinityCount = ProcessorAffinityService.GetCurrentAffinityProcessorCount();
        return affinityCount > 0 ? affinityCount : Math.Max(1, Environment.ProcessorCount);
    }

    public static int ComputeSafeMax(int logicalProcessors, long availableMemoryBytes)
    {
        var byCpu = Math.Max(1, (int)Math.Round(Math.Max(1, logicalProcessors) / 4.0));
        var byMemory = availableMemoryBytes switch
        {
            < 4L * 1024 * 1024 * 1024 => 1,
            < 8L * 1024 * 1024 * 1024 => 2,
            _ => int.MaxValue
        };

        return Math.Clamp(Math.Min(byCpu, byMemory), 1, HardCap);
    }
}
