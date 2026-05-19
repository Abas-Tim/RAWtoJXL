using System.IO;
using System.Threading;
using System.Threading.Tasks;
using RAWtoJXL.Core.Models;

namespace RAWtoJXL.Core.Interfaces
{
    public interface IImageConverterService
    {
        Task<byte[]> ExtractThumbnailAsync(string filePath, CancellationToken cancellationToken = default);
        Task ConvertToPngAsync(string inputPath, string outputPath, CancellationToken cancellationToken = default);
        Task ConvertToJpegAsync(string inputPath, string outputPath, int quality, CancellationToken cancellationToken = default);
        Task<MetadataProfiles> ExtractMetadataProfilesAsync(string filePath, CancellationToken cancellationToken = default);
        Task<byte[]> ExtractToRawRgb16Async(string inputPath, CancellationToken cancellationToken = default);
        Task StreamPpmToAsync(string inputPath, Stream output, CancellationToken cancellationToken = default);
    }
}
