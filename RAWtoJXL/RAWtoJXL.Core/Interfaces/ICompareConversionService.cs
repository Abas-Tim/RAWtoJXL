using System;
using System.Threading;
using System.Threading.Tasks;

namespace RAWtoJXL.Core.Interfaces
{
    public interface ICompareConversionService
    {
        Task<string> EnsureMasterPngAsync(string inputPath, CancellationToken cancellationToken = default);

        Task<string?> EnsureQuickPreviewAsync(string inputPath, CancellationToken cancellationToken = default);

        Task<string> EnsureTargetFileAsync(
            string inputPath,
            OutputFormat format,
            int quality,
            int? effort,
            CancellationToken cancellationToken = default);

        Task<CompareDisplayPngs> EnsureDisplayPngsAsync(
            string inputPath,
            OutputFormat? format,
            int quality,
            int? effort,
            CancellationToken cancellationToken = default);

        Task<string> EnsureDisplayFullPngAsync(
            string inputPath,
            OutputFormat? format,
            int quality,
            int? effort,
            CancellationToken cancellationToken = default);

        void PurgeStaleEntries();
    }

    public sealed record CompareDisplayPngs(string PreviewPath, string FullPath, int Width, int Height);
}
