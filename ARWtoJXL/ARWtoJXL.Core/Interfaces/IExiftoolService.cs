using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ARWtoJXL.Core.Interfaces
{
    public interface IExiftoolService
    {
        Task<string?> ExtractExifAsync(string filePath, CancellationToken cancellationToken = default);
        Task EmbedMetadataAsync(string sourcePath, string outputPath, Models.MetadataProfiles metadata, CancellationToken cancellationToken = default);
    }
}
