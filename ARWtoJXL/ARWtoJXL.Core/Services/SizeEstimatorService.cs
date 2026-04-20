using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ARWtoJXL.Core.Interfaces;

namespace ARWtoJXL.Core.Services;

public class SizeEstimatorService : ISizeEstimator
{
    /// <summary>
    /// Compression ratio map: PNG → JXL at various quality levels.
    /// Ratios based on typical photographic content compression characteristics.
    /// JPEG XL achieves ~30-50% reduction at quality 90, ~50-70% at quality 70.
    /// </summary>
    private static readonly (int QualityMin, int QualityMax, double MinRatio, double MaxRatio)[] _qualityRanges =
    [
        (100, 100, 0.85, 1.10),   // Lossless: similar or slightly larger (metadata)
        (95,  99,  0.55, 0.75),   // Very high quality
        (90,  94,  0.40, 0.60),   // High quality
        (80,  89,  0.25, 0.45),   // Very good quality
        (70,  79,  0.15, 0.30),   // Good quality
        (60,  69,  0.10, 0.20),   // Medium quality
        (50,  59,  0.06, 0.12),   // Low quality
        (0,   49,  0.03, 0.08),   // Very low quality
    ];

    public long Estimate(long pngSizeBytes, int quality)
    {
        quality = Math.Max(0, Math.Min(100, quality));

        if (pngSizeBytes <= 0) return 0;

        // Find the matching quality range
        var range = _qualityRanges.First(r => quality >= r.QualityMin && quality <= r.QualityMax);

        // Calculate position within range (0.0 to 1.0)
        double rangeSize = range.QualityMax - range.QualityMin;
        double position = rangeSize > 0 ? (quality - range.QualityMin) / rangeSize : 0.0;

        // Interpolate ratio within range
        double ratio = range.MaxRatio - position * (range.MaxRatio - range.MinRatio);

        // Apply content complexity factor based on PNG size tiers
        // Larger files tend to have more detail, slightly lower compression
        double complexityFactor = GetComplexityFactor(pngSizeBytes);

        double estimatedSize = pngSizeBytes * ratio * complexityFactor;

        return (long)Math.Round(estimatedSize);
    }

    public async Task<long> EstimateFromPngAsync(string pngPath, int quality, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(pngPath))
            throw new FileNotFoundException($"PNG file not found: {pngPath}");

        using var stream = File.OpenRead(pngPath);
        long pngSize = stream.Length;

        return Estimate(pngSize, quality);
    }

    private static double GetComplexityFactor(long pngSizeBytes)
    {
        // Smooth gradients compress better than fine detail/noise
        // Heuristic: larger PNGs (higher resolution or more detail) have slightly lower ratios
        double megapixels = pngSizeBytes / (1024.0 * 1024.0 * 3.0); // Rough 16-bit RGBA estimate

        if (megapixels < 5) return 0.95;  // Small images, slightly better compression
        if (megapixels < 20) return 1.0;  // Medium images, baseline
        if (megapixels < 40) return 1.05; // Large images, slightly less compression
        if (megapixels < 60) return 1.08; // Very large
        return 1.10;                       // Extremely large
    }
}
