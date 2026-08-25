using System;
using System.Threading;
using System.Threading.Tasks;
using RAWtoJXL.Core.Interfaces;

namespace RAWtoJXL.Core.Services;

public sealed class PreferredRawRenderer : IRawRenderer
{
    private readonly ILogger _logger;
    private readonly MagickRawRenderer _magickFallback;

    public PreferredRawRenderer(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _magickFallback = new MagickRawRenderer(logger);
    }

    public async Task RenderToPngAsync(
        string inputPath,
        string outputPath,
        int threads,
        CancellationToken cancellationToken = default)
    {
        string? rawspeed = RawSpeedCliRenderer.ResolveExecutable();
        if (rawspeed != null)
        {
            var rawspeedRenderer = new RawSpeedCliRenderer(_logger, new FileService(_logger));
            try
            {
                await rawspeedRenderer.RenderToPngAsync(inputPath, outputPath, threads, cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (Exception)
            {
                _logger.Write("[PreferredRawRenderer] rawspeed-cli failed; falling back to Magick raw decode.");
            }
        }

        await _magickFallback.RenderToPngAsync(inputPath, outputPath, threads, cancellationToken).ConfigureAwait(false);
    }
}
