using System;

namespace RAWtoJXL.Core.Models
{
    public static class QualityCalculator
    {
        public static float CalculateDistance(int quality)
        {
            quality = Math.Max(0, Math.Min(100, quality));
            return quality >= 100.0f ? 0.0f
                  : quality >= 30
                      ? 0.1f + (100 - quality) * 0.09f
                      : 53.0f / 3000.0f * quality * quality - 23.0f / 20.0f * quality + 25.0f;
        }

        public static int CalculateEffort(int quality)
        {
            return quality switch
            {
                >= 100 => 9,
                >= 90 => 8,
                >= 75 => 7,
                >= 60 => 6,
                >= 45 => 5,
                >= 30 => 4,
                _ => 3
            };
        }

        public static bool IsLossless(int quality)
        {
            return quality >= 100;
        }
    }
}
