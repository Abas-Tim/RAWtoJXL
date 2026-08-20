using System;
using System.IO;

namespace RAWtoJXL.Core.Models
{
    public static class CompareDefaults
    {
        public const int CacheSchemaVersion = 5;

        public const int JxlEffort = 5;

        public static int JxlThreads => GetLogicalProcessorCount();

        public const uint PreviewMaxDimension = 4096;

        public const uint QuickPreviewMaxDimension = 1600;

        public const long CacheMaxBytes = 2L * 1024 * 1024 * 1024;

        public static string CacheRoot => Path.Combine(Path.GetTempPath(), "RAWtoJXL", "CompareCache");

        private static int GetLogicalProcessorCount()
        {
            string? configuredCount = Environment.GetEnvironmentVariable("NUMBER_OF_PROCESSORS");
            return int.TryParse(configuredCount, out int processorCount) && processorCount > 0
                ? processorCount
                : Math.Max(1, Environment.ProcessorCount);
        }
    }
}
