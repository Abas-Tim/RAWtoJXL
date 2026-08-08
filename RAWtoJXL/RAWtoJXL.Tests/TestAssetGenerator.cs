using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using ImageMagick;
using Microsoft.Extensions.DependencyInjection;
using RAWtoJXL.Core;
using RAWtoJXL.Core.Interfaces;
using Xunit;

namespace RAWtoJXL.Tests;

public class TestAssetGenerator : Startup
{
    internal static readonly string AssetPath =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "RAWtoJXL.Tests", "test1.dng"));

    [Fact]
    public async Task TestAsset_EndToEnd_PipelineWorks()
    {
        var path = AssetPath;
        Assert.True(File.Exists(path), $"test asset missing at {path}");

        using (var image = new MagickImage(path))
        {
            Assert.Equal(1600, (int)image.Width);
            Assert.Equal(1200, (int)image.Height);
            Assert.Equal(16, (int)image.Depth);
        }

        var converter = Services.GetRequiredService<IImageConverterService>();
        using var metadata = await converter.ExtractMetadataProfilesAsync(path, TestContext.Current.CancellationToken);
        Assert.True(metadata.HasAny, "test asset should expose metadata");

        var thumbnail = await Services.GetRequiredService<IImageService>().GetThumbnailAsync(path, TestContext.Current.CancellationToken);
        Assert.NotEmpty(thumbnail);

        var output = Path.Combine(Path.GetTempPath(), $"asset_verify_{System.Guid.NewGuid():N}.jxl");
        await Services.GetRequiredService<IImageService>().ConvertToJxlAsync(path, output, p => { }, 90, OutputFormat.Jxl, TestContext.Current.CancellationToken);
        Assert.True(File.Exists(output) && new FileInfo(output).Length > 0);
        File.Delete(output);
    }

    [Fact]
    public void RegenerateTestAsset_WhenMissing()
    {
        if (File.Exists(AssetPath))
        {
            return;
        }

        GenerateMinimalCfaDng(AssetPath);

        var exiftool = Path.Combine(AppContext.BaseDirectory, "exiftool.exe");
        if (!File.Exists(exiftool))
        {
            throw new Xunit.Sdk.XunitException(
                $"Generated {AssetPath} but exiftool.exe was not found at {exiftool}; run build-release.ps1 to fetch exiftool, then re-run.");
        }

        var psi = new ProcessStartInfo
        {
            FileName = exiftool,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var arg in new[]
                 {
                     "-overwrite_original", "-Make=Sony", "-Model=\"Test Camera\"",
                     "-LensModel=\"Test Lens\"", "-ISO=100", "-DateTimeOriginal=\"2026:01:01 12:00:00\"",
                     AssetPath
                 })
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = Process.Start(psi);
        process!.WaitForExit();
        Assert.Equal(0, process.ExitCode);
    }

    private static void GenerateMinimalCfaDng(string path)
    {
        const int width = 1600;
        const int height = 1200;
        const int ifdCount = 25;
        int pixelsOffset = 4096;
        int pixelBytes = width * height * 2;

        using var fs = new FileStream(path, FileMode.Create);
        using var w = new BinaryWriter(fs);

        void WriteEntry(ushort tag, ushort type, int count, Action<BinaryWriter> valueWriter, bool inline)
        {
            w.Write(tag);
            w.Write(type);
            w.Write(count);
            long valuePos = w.BaseStream.Position;
            if (inline)
            {
                valueWriter(w);
                while (w.BaseStream.Position - valuePos < 4) w.Write((byte)0);
            }
            else
            {
                w.Write(0);
            }
        }

        w.Write((byte)'I');
        w.Write((byte)'I');
        w.Write((ushort)42);
        w.Write(8);

        w.Write((ushort)ifdCount);

        var deferred = new List<(ushort tag, ushort type, int count, Action<BinaryWriter> write, long offsetPos)>();

        void Defer(ushort tag, ushort type, int count, Action<BinaryWriter> write)
        {
            long offsetPos = w.BaseStream.Position + 8;
            deferred.Add((tag, type, count, write, offsetPos));
            w.Write(tag);
            w.Write(type);
            w.Write(count);
            w.Write(0);
        }

        WriteEntry(256, 4, 1, b => b.Write(width), true);
        WriteEntry(257, 4, 1, b => b.Write(height), true);
        WriteEntry(258, 3, 1, b => b.Write((ushort)16), true);
        WriteEntry(259, 3, 1, b => b.Write((ushort)1), true);
        WriteEntry(262, 3, 1, b => b.Write((ushort)32803), true);
        WriteEntry(273, 4, 1, b => b.Write(pixelsOffset), true);
        WriteEntry(277, 3, 1, b => b.Write((ushort)1), true);
        WriteEntry(278, 4, 1, b => b.Write(height), true);
        WriteEntry(279, 4, 1, b => b.Write(pixelBytes), true);
        WriteEntry(284, 3, 1, b => b.Write((ushort)1), true);
        WriteEntry(339, 3, 1, b => b.Write((ushort)1), true);
        WriteEntry(33421, 3, 2, b => { b.Write((ushort)2); b.Write((ushort)2); }, true);
        WriteEntry(33422, 1, 4, b => { b.Write((byte)0); b.Write((byte)1); b.Write((byte)1); b.Write((byte)2); }, true);
        WriteEntry(50706, 1, 4, b => { b.Write((byte)1); b.Write((byte)4); b.Write((byte)0); b.Write((byte)0); }, true);
        WriteEntry(50707, 1, 4, b => { b.Write((byte)1); b.Write((byte)1); b.Write((byte)0); b.Write((byte)0); }, true);
        Defer(50708, 2, 19, b => b.Write(Encoding.ASCII.GetBytes("RAWtoJXL Test Camera\0")));
        WriteEntry(50710, 1, 3, b => { b.Write((byte)0); b.Write((byte)1); b.Write((byte)2); }, true);
        WriteEntry(50711, 3, 1, b => b.Write((ushort)1), true);
        WriteEntry(50714, 3, 1, b => b.Write((ushort)0), true);
        WriteEntry(50717, 3, 1, b => b.Write((ushort)65535), true);
        Defer(50719, 4, 4, b => { b.Write(0); b.Write(0); b.Write(0); b.Write(0); });
        Defer(50720, 4, 2, b => { b.Write(width); b.Write(height); });
        Defer(50721, 10, 9, b =>
        {
            int[] m = { 1, 0, 0, 1, 0, 0, 0, 0, 1 };
            foreach (int v in m) { b.Write(v); b.Write(1); }
        });
        Defer(50728, 5, 3, b => { for (int i = 0; i < 3; i++) { b.Write(1); b.Write(1); } });
        WriteEntry(50778, 3, 1, b => b.Write((ushort)21), true);
        w.Write(0);

        long expected = 8 + 2 + ifdCount * 12 + 4;
        foreach (var (_, _, _, write, offsetPos) in deferred)
        {
            long here = w.BaseStream.Position;
            w.BaseStream.Position = offsetPos;
            w.Write((int)here);
            w.BaseStream.Position = here;
            write(w);
            while (w.BaseStream.Position - expected < 4) w.Write((byte)0);
            while ((w.BaseStream.Position - expected) % 4 != 0) w.Write((byte)0);
            expected = w.BaseStream.Position;
        }

        if (w.BaseStream.Position < pixelsOffset)
        {
            w.BaseStream.Position = pixelsOffset;
        }

        var pixelData = new byte[pixelBytes];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int i = (y * width + x) * 2;
                ushort v = (ushort)((((x / 16) + (y / 16)) % 2) * 60000 + (ushort)((x * 4095) / (width - 1)));
                pixelData[i] = (byte)(v & 0xFF);
                pixelData[i + 1] = (byte)(v >> 8);
            }
        }
        w.Write(pixelData);
    }
}
