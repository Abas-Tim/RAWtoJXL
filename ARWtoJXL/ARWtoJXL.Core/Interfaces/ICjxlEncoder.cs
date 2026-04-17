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
        Task EncodeAsync(
            string inputPath,
            string originalArwPath,
            string outputPath,
            int quality,
            MetadataProfiles? metadata = null,
            CancellationToken cancellationToken = default,
            int timeoutSeconds = 300);
    }
}
