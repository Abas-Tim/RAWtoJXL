using System.Threading;
using System.Threading.Tasks;
using ARWtoJXL.Core.Models;

namespace ARWtoJXL.Core.Interfaces
{
    public interface IMagickService
    {
        Task<byte[]> ExtractThumbnailAsync(string filePath, CancellationToken cancellationToken = default);
        Task ConvertToPngAsync(string inputPath, string outputPath, CancellationToken cancellationToken = default);
        Task ConvertToJpegAsync(string inputPath, string outputPath, int quality, CancellationToken cancellationToken = default);
        Task<MetadataProfiles> ExtractMetadataProfilesAsync(string filePath, CancellationToken cancellationToken = default);
    }
}
