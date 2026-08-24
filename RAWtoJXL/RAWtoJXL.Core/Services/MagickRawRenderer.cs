using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ImageMagick;
using RAWtoJXL.Core.Interfaces;
using RAWtoJXL.Core.Models;

namespace RAWtoJXL.Core.Services;

public sealed class MagickRawRenderer : IRawRenderer
{
    private readonly ILogger _logger;

    public MagickRawRenderer(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task RenderToPngAsync(
        string inputPath,
        string outputPath,
        int threads,
        CancellationToken cancellationToken = default)
    {
        string outputDirectory = Path.GetDirectoryName(outputPath) ?? string.Empty;
        if (!string.IsNullOrEmpty(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        _logger.Write($"[MagickRawRenderer] Rendering {Path.GetFileName(inputPath)}.");
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await Task.Run(() =>
            {
                using var image = new MagickImage(inputPath);
                image.ColorSpace = ColorSpace.sRGB;
                if (image.Depth > 8)
                {
                    image.Depth = 16;
                }
                image.Format = MagickFormat.Png;
                image.Write(outputPath);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException ex) when (FileLockedException.IsFileLocked(ex))
        {
            throw new FileLockedException(inputPath, ex);
        }
        finally
        {
            stopwatch.Stop();
            _logger.Write($"[MagickRawRenderer] Rendered {Path.GetFileName(inputPath)} in {stopwatch.Elapsed.TotalSeconds:F2}s.");
        }
    }
}
