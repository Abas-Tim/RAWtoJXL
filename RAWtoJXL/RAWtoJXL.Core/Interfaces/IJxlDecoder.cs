using System.Threading;
using System.Threading.Tasks;

namespace RAWtoJXL.Core.Interfaces
{
    /// <summary>
    /// Decodes JPEG XL files into pixel formats that the rest of the pipeline can read.
    /// </summary>
    public interface IJxlDecoder
    {
        /// <summary>
        /// Decodes a JPEG XL file to a PNG file (16-bit when the source is 16-bit).
        /// </summary>
        /// <param name="inputPath">Path to the source .jxl file.</param>
        /// <param name="outputPath">Path for the output PNG file.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <param name="timeoutSeconds">Optional timeout in seconds (default: 300).</param>
        /// <param name="numThreads">Optional worker thread count passed as --num_threads to djxl.</param>
        Task DecodeToPngAsync(string inputPath, string outputPath, CancellationToken cancellationToken = default, int timeoutSeconds = 300, int? numThreads = null);
    }
}
