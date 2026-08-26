using System.IO;
using System.Threading;
using System.Threading.Tasks;
using RAWtoJXL.Core.Models;

namespace RAWtoJXL.Core.Interfaces
{
    public interface IImageConverterService
    {
        Task<byte[]> ExtractThumbnailAsync(string filePath, CancellationToken cancellationToken = default);
        Task<byte[]?> ExtractEmbeddedPreviewAsync(string filePath, CancellationToken cancellationToken = default);
        Task ConvertToJpegAsync(string inputPath, string outputPath, int quality, CancellationToken cancellationToken = default, int? threads = null);
        Task ConvertToAvifAsync(string inputPath, string outputPath, int quality, CancellationToken cancellationToken = default, int? threads = null);
        Task ConvertToJxlAsync(string inputPath, string outputPath, int quality, int? effort = null, CancellationToken cancellationToken = default, int? threads = null);
        Task ConvertToPngAsync(string inputPath, string outputPath, CancellationToken cancellationToken = default);
        Task<MetadataProfiles> ExtractMetadataProfilesAsync(string filePath, CancellationToken cancellationToken = default);
        Task StreamPpmToAsync(string inputPath, Stream output, CancellationToken cancellationToken = default);
    }
}
