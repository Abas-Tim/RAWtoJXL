using System;
using System.IO;
using RAWtoJXL.Core.Services;

namespace RAWtoJXL.Core.Models
{
    public static class CompareDefaults
    {
        public const int CacheSchemaVersion = 5;

        public const int JxlEffort = 5;

        public const int MaxConcurrentMasterRenders = 2;

        public static int JxlThreads => GetLogicalProcessorCount();

        public static int GetJobThreads(int jobs) => Math.Max(1, JxlThreads / Math.Max(1, jobs));

        public const uint PreviewMaxDimension = 4096;

        public const uint QuickPreviewMaxDimension = 1600;

        public const double DifferenceAmplification = 8.0;

        public const int DifferenceMaxDimension = 2048;

        public const long CacheMaxBytes = 2L * 1024 * 1024 * 1024;

        public static string CacheRoot => Path.Combine(Path.GetTempPath(), "RAWtoJXL", "CompareCache");

        private static int GetLogicalProcessorCount()
        {
            int affinityCount = ProcessorAffinityService.GetCurrentAffinityProcessorCount();
            if (affinityCount > 0)
            {
                return affinityCount;
            }

            if (Environment.ProcessorCount > 0)
            {
                return Environment.ProcessorCount;
            }

            string? configuredCount = Environment.GetEnvironmentVariable("NUMBER_OF_PROCESSORS");
            return int.TryParse(configuredCount, out int processorCount) && processorCount > 0
                ? processorCount
                : 1;
        }
    }
}
