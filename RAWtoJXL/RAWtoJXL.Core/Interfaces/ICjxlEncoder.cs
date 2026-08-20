using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace RAWtoJXL.Core.Interfaces
{
    /// <summary>
    /// Interface for encoding images to JPEG XL format.
    /// </summary>
    public interface ICjxlEncoder
    {
        /// <summary>
        /// Asynchronously encodes an image by writing PPM data directly to cjxl stdin via a delegate.
        /// </summary>
        /// <param name="inputPath">Path to the source file for PPM generation.</param>
        /// <param name="outputPath">Path for the output JPEG XL file.</param>
        /// <param name="quality">Quality level (0-100, where 100 is lossless).</param>
        /// <param name="ppmWriter">Delegate that writes PPM data directly to cjxl stdin.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <param name="timeoutSeconds">Optional timeout in seconds (default: 300).</param>
        /// <param name="progress">Optional progress callback (0.0 to 1.0).</param>
        /// <param name="effort">Optional encoding effort override (1-9). Null uses auto based on quality.</param>
        /// <param name="threads">Optional thread count override. Null uses OS processor count.</param>
        Task EncodeFromStreamAsync(
            string inputPath,
            string outputPath,
            int quality,
            Func<Stream, CancellationToken, Task> ppmWriter,
            CancellationToken cancellationToken,
            int timeoutSeconds,
            Action<double>? progress,
            int? effort,
            int? threads = null);

        /// <summary>
        /// Asynchronously encodes an image file (e.g. PNG) to JPEG XL by passing the file
        /// directly to cjxl, without a PPM stdin round-trip.
        /// </summary>
        /// <param name="inputPath">Path to the source image file cjxl can read.</param>
        /// <param name="outputPath">Path for the output JPEG XL file.</param>
        /// <param name="quality">Quality level (0-100, where 100 is lossless).</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <param name="timeoutSeconds">Optional timeout in seconds (default: 300).</param>
        /// <param name="progress">Optional progress callback (0.0 to 1.0).</param>
        /// <param name="effort">Optional encoding effort override (1-9). Null uses auto based on quality.</param>
        /// <param name="threads">Optional thread count override. Null uses OS processor count.</param>
        Task EncodeFromFileAsync(
            string inputPath,
            string outputPath,
            int quality,
            CancellationToken cancellationToken,
            int timeoutSeconds = 300,
            Action<double>? progress = null,
            int? effort = null,
            int? threads = null);
    }
}
