using System;
using RAWtoJXL.Core.Services;

namespace RAWtoJXL.Cli
{
    public static class ParallelismPolicy
    {
        public const int DefaultJobs = BatchParallelismPolicy.DefaultJobs;

        public const int HardCap = BatchParallelismPolicy.HardCap;

        public static int SafeMaxJobs => BatchParallelismPolicy.SafeMaxJobs;

        public static int ResolveDefaultJobs() => BatchParallelismPolicy.ResolveDefaultJobs();

        internal static int ResolveDefaultJobs(int safeMaxJobs) =>
            Math.Min(DefaultJobs, Math.Max(1, safeMaxJobs));

        internal static int ComputeSafeMax(int logicalProcessors, long availableMemoryBytes)
            => BatchParallelismPolicy.ComputeSafeMax(logicalProcessors, availableMemoryBytes);

        public static bool IsAboveSafeMax(int jobs) => BatchParallelismPolicy.SafeMaxJobs < jobs;
    }
}
