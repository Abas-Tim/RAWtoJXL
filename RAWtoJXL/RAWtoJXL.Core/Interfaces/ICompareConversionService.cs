using System;
using System.Threading;
using System.Threading.Tasks;
using RAWtoJXL.Core.Models;

namespace RAWtoJXL.Core.Interfaces
{
    public interface ICompareConversionService
    {
        Task<string> EnsureMasterPngAsync(string inputPath, CancellationToken cancellationToken = default);

        Task<MasterRenderLease> EnsureMasterRenderLeaseAsync(
            string inputPath,
            CancellationToken cancellationToken = default,
            int? renderThreads = null);

        Task<string> EnsureTargetFileFromRenderAsync(
            string inputPath,
            string renderedSourcePath,
            OutputFormat format,
            int quality,
            int? effort,
            CancellationToken cancellationToken = default,
            int? threads = null);

        Task<CompareDisplayPngs> EnsureDisplayPreviewFromRenderAsync(
            string inputPath,
            string sourcePath,
            bool decodeJxlTarget,
            OutputFormat? format,
            int quality,
            int? effort,
            CancellationToken cancellationToken = default,
            int? threads = null);

        Task<string?> EnsureQuickPreviewAsync(string inputPath, CancellationToken cancellationToken = default);

        Task<string> EnsureTargetFileAsync(
            string inputPath,
            OutputFormat format,
            int quality,
            int? effort,
            CancellationToken cancellationToken = default,
            int? threads = null);

        Task<CompareDisplayPngs> EnsureDisplayPngsAsync(
            string inputPath,
            OutputFormat? format,
            int quality,
            int? effort,
            CancellationToken cancellationToken = default,
            int? threads = null);

        Task<string> EnsureDisplayFullPngAsync(
            string inputPath,
            OutputFormat? format,
            int quality,
            int? effort,
            CancellationToken cancellationToken = default);

        void PurgeStaleEntries();

        void ClearCompareCache();
    }

    public sealed record CompareDisplayPngs(string PreviewPath, string FullPath, int Width, int Height);

    public readonly record struct CompareImageRegion(double Left, double Top, double Right, double Bottom)
    {
        public double Width => Right - Left;

        public double Height => Bottom - Top;
    }
}
