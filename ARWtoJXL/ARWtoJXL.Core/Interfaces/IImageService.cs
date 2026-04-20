using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ARWtoJXL.Core.Interfaces
{
    public enum ImageStatus
    {
        Pending,
        Ready,
        Converting,
        Converted,
        Failed
    }

  public interface IImageService
    {
        /// <summary>
        /// Gets a thumbnail for an ARW file (embedded JPEG) or a JXL file.
        /// </summary>
        Task<byte[]> GetThumbnailAsync(string filePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Converts an ARW file to a JXL file.
        /// </summary>
        /// <param name="inputPath">Path to the source .ARW file.</param>
        /// <param name="outputPath">Path where the output file should be saved.</param>
        /// <param name="progress">Callback for conversion progress (0.0 to 1.0).</param>
        /// <param name="quality">Quality preset (0-100, higher = better quality, larger file).</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task ConvertArwToJxlAsync(string inputPath, string outputPath, Action<double> progress, int quality, CancellationToken cancellationToken = default);

        /// <summary>
        /// Estimates the JXL output file size for an ARW file.
        /// </summary>
        /// <param name="arwPath">Path to the source ARW file.</param>
        /// <param name="quality">Quality preset (0-100).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task<long> EstimateSizeAsync(string arwPath, int quality, CancellationToken cancellationToken = default);
    }
}
