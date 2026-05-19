using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace RAWtoJXL.Core.Interfaces
{
    public enum ImageStatus
    {
        Pending,
        Ready,
        Converting,
        Converted,
        Failed
    }

    public enum OutputFormat
    {
        Jxl,
        Jpeg,
        Png
    }

    public interface IImageService
    {
        Task<byte[]> GetThumbnailAsync(string filePath, CancellationToken cancellationToken = default);

        Task ConvertToJxlAsync(
            string inputPath,
            string outputPath,
            Action<double> progress,
            int quality,
            OutputFormat outputFormat = OutputFormat.Jxl,
            CancellationToken cancellationToken = default,
            bool skipMetadata = false,
            int? effort = null,
            int? threads = null);
    }
}
