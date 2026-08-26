using System.Threading;
using System.Threading.Tasks;

namespace RAWtoJXL.Core.Interfaces
{
    public interface IRawRenderer
    {
        Task RenderToPngAsync(string inputPath, string outputPath, int threads, CancellationToken cancellationToken = default);
    }
}
