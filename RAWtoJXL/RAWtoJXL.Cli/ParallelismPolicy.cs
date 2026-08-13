using System;

namespace RAWtoJXL.Cli
{
    public static class ParallelismPolicy
    {
        public const int DefaultJobs = 2;

        public const int HardCap = 4;

        public static int SafeMaxJobs { get; } = ComputeSafeMax(
            Environment.ProcessorCount,
            GC.GetGCMemoryInfo().TotalAvailableMemoryBytes);

        internal static int ComputeSafeMax(int logicalProcessors, long availableMemoryBytes)
        {
            var byCpu = Math.Max(1, (int)Math.Round(logicalProcessors / 4.0));

            var byMemory = availableMemoryBytes switch
            {
                < 4L * 1024 * 1024 * 1024 => 1,
                < 8L * 1024 * 1024 * 1024 => 2,
                _ => int.MaxValue
            };

            return Math.Clamp(Math.Min(byCpu, byMemory), 1, HardCap);
        }

        public static bool IsAboveSafeMax(int jobs) => jobs > SafeMaxJobs;
    }
}
