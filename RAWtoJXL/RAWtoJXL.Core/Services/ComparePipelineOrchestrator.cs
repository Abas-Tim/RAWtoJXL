using System;
using System.Threading;
using System.Threading.Tasks;
using RAWtoJXL.Core.Interfaces;
using RAWtoJXL.Core.Models;

namespace RAWtoJXL.Core.Services;

public sealed record ComparePanePipelineResult(CompareDisplayPngs Preview, string? TargetPath);

public sealed class ComparePipelineOrchestrator
{
    private readonly ICompareConversionService _conversionService;

    public ComparePipelineOrchestrator(ICompareConversionService conversionService)
    {
        _conversionService = conversionService ?? throw new ArgumentNullException(nameof(conversionService));
    }

    public static bool IsParallelRenderEnabled =>
        Environment.GetEnvironmentVariable("RAWTOJXL_COMPARE_PARALLEL_RENDER") == "1";

    public async Task<ComparePanePipelineResult> RunPaneAsync(
        string inputPath,
        OutputFormat? format,
        int quality,
        int? effort,
        bool allowParallelRender,
        CancellationToken cancellationToken = default,
        int? threads = null)
    {
        if (!allowParallelRender)
        {
            var legacyPreview = await _conversionService
                .EnsureDisplayPngsAsync(inputPath, format, quality, effort, cancellationToken, threads)
                .ConfigureAwait(false);
            return new ComparePanePipelineResult(legacyPreview, null);
        }

        var lease = await _conversionService
            .EnsureMasterRenderLeaseAsync(inputPath, cancellationToken, threads)
            .ConfigureAwait(false);

        try
        {
            if (format == null)
            {
                var originalPreview = await _conversionService
                    .EnsureDisplayPreviewFromRenderAsync(
                        inputPath, lease.PngPath, false, null, quality, effort, cancellationToken, null)
                    .ConfigureAwait(false);
                return new ComparePanePipelineResult(originalPreview, null);
            }

            string target = await _conversionService
                .EnsureTargetFileFromRenderAsync(
                    inputPath, lease.PngPath, format.Value, quality, effort, cancellationToken, threads)
                .ConfigureAwait(false);

            var preview = await _conversionService
                .EnsureDisplayPreviewFromRenderAsync(
                    inputPath, target, format == OutputFormat.Jxl, format, quality, effort, cancellationToken, threads)
                .ConfigureAwait(false);

            return new ComparePanePipelineResult(preview, target);
        }
        finally
        {
            lease.Complete();
        }
    }
}
