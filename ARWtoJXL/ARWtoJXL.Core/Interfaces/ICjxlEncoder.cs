using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ARWtoJXL.Core.Models;

namespace ARWtoJXL.Core.Interfaces
{
    /// <summary>
    /// Interface for encoding images to JPEG XL format.
    /// </summary>
    public interface ICjxlEncoder
    {
        /// <summary>
        /// Asynchronously encodes an image to JPEG XL format.
        /// </summary>
        /// <param name="inputPath">Path to the input image file (PNG).</param>
        /// <param name="originalArwPath">Path to the original ARW file (for metadata extraction).</param>
        /// <param name="outputPath">Path for the output JPEG XL file.</param>
        /// <param name="quality">Quality level (0-100, where 100 is lossless).</param>
        /// <param name="metadata">Optional metadata profiles to embed.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <param name="timeoutSeconds">Optional timeout in seconds (default: 300).</param>
        /// <param name="progress">Optional progress callback (0.0 to 1.0).</param>
        /// <param name="effort">Optional encoding effort override (1-9). Null uses auto based on quality.</param>
        /// <param name="rawDistance">Optional raw Butteraugli distance override (0.0-25.0). Null uses auto based on quality.</param>
        Task EncodeAsync(
            string inputPath,
            string originalArwPath,
            string outputPath,
            int quality,
            MetadataProfiles? metadata = null,
            CancellationToken cancellationToken = default,
            int timeoutSeconds = 300,
            Action<double>? progress = null,
            int? effort = null,
            float? rawDistance = null);

        /// <summary>
        /// Asynchronously encodes an image from a PPM stream to JPEG XL format.
        /// </summary>
        /// <param name="inputStream">Stream containing PPM data (P6 binary format).</param>
        /// <param name="originalArwPath">Path to the original ARW file (for metadata extraction).</param>
        /// <param name="outputPath">Path for the output JPEG XL file.</param>
        /// <param name="quality">Quality level (0-100, where 100 is lossless).</param>
        /// <param name="metadata">Optional metadata profiles to embed.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <param name="timeoutSeconds">Optional timeout in seconds (default: 300).</param>
        /// <param name="progress">Optional progress callback (0.0 to 1.0).</param>
        /// <param name="effort">Optional encoding effort override (1-9). Null uses auto based on quality.</param>
        /// <param name="rawDistance">Optional raw Butteraugli distance override (0.0-25.0). Null uses auto based on quality.</param>
        Task EncodeFromStreamAsync(
            Stream inputStream,
            string originalArwPath,
            string outputPath,
            int quality,
            MetadataProfiles? metadata = null,
            CancellationToken cancellationToken = default,
            int timeoutSeconds = 300,
            Action<double>? progress = null,
            int? effort = null,
            float? rawDistance = null);
    }
}
