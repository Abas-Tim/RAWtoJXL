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

        Task<CompareViewportAnalysis> AnalyzeViewportAsync(
            string inputPath,
            OutputFormat format,
            int quality,
            int? effort,
            CompareImageRegion region,
            bool useFullResolution,
            int differenceWidth,
            int differenceHeight,
            bool includeDifference,
            CancellationToken cancellationToken = default);

        void PurgeStaleEntries();
    }

    public sealed record CompareDisplayPngs(string PreviewPath, string FullPath, int Width, int Height);

    public readonly record struct CompareImageRegion(double Left, double Top, double Right, double Bottom)
    {
        public double Width => Right - Left;

        public double Height => Bottom - Top;
    }

    public sealed record CompareViewportAnalysis(
        double Ssim,
        CompareImageRegion Region,
        byte[]? DifferencePng);
}
