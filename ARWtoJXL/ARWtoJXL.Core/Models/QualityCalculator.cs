using System;

namespace ARWtoJXL.Core.Models
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
            if (quality >= 95) return 9;
            if (quality >= 85) return 8;
            if (quality >= 70) return 7;
            if (quality >= 50) return 6;
            return 5;
        }

        public static bool IsLossless(int quality)
        {
            return quality >= 100;
        }
    }
}
