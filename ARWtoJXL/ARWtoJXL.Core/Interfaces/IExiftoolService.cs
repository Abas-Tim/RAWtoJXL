using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ARWtoJXL.Core.Models;

namespace ARWtoJXL.Core.Interfaces
{
    public interface IExiftoolService
    {
        Task<MetadataProfiles> ExtractMetadataProfilesAsync(string filePath, CancellationToken cancellationToken = default);
        Task EmbedMetadataAsync(string sourcePath, string outputPath, CancellationToken cancellationToken = default);
        Task<byte[]?> ExtractPreviewImageAsync(string filePath, CancellationToken cancellationToken = default);
    }
}
