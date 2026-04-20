using System.Threading;
using System.Threading.Tasks;

namespace ARWtoJXL.Core.Interfaces;

/// <summary>
/// Estimates JPEG XL output file size based on PNG source size and quality settings.
/// </summary>
public interface ISizeEstimator
{
    /// <summary>
    /// Estimates the resulting JXL file size in bytes.
    /// </summary>
    /// <param name="pngSizeBytes">Size of the source PNG file in bytes.</param>
    /// <param name="quality">Quality preset (0-100). 100 = lossless.</param>
    /// <returns>Estimated file size in bytes.</returns>
    long Estimate(long pngSizeBytes, int quality);

    /// <summary>
    /// Estimates the resulting JXL file size based on an existing PNG file on disk.
    /// </summary>
    /// <param name="pngPath">Path to the PNG file.</param>
    /// <param name="quality">Quality preset (0-100).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<long> EstimateFromPngAsync(string pngPath, int quality, CancellationToken cancellationToken = default);
}
