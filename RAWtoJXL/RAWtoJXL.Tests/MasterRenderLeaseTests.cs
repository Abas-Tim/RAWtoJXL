using System.IO;
using Moq;
using RAWtoJXL.Core.Interfaces;
using RAWtoJXL.Core.Models;
using RAWtoJXL.Core.Services;

namespace RAWtoJXL.Tests;

public class MasterRenderLeaseTests
{
    private sealed record ServiceHarness(
        CompareConversionService Service,
        Mock<IRawRenderer> RawRenderer,
        string Dir)
    {
        public string InputPath => Path.Combine(Dir, "source.dng");
        public string MasterDir => Directory
            .EnumerateDirectories(Path.Combine(CompareDefaults.CacheRoot, "master"))
            .FirstOrDefault(d => Path.GetFileName(d).StartsWith("m-", StringComparison.Ordinal))
            ?? string.Empty;
    }

    private static ServiceHarness CreateHarness(Action<Mock<IRawRenderer>>? configureRenderer = null)
    {
        var dir = Path.Combine(Path.GetTempPath(), "RAWtoJXL_Lease_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var input = Path.Combine(dir, "source.dng");
        File.Copy(TestAssetGenerator.AssetPath, input);

        var logger = new Mock<ILogger>();
        var exif = new Mock<IExiftoolService>();
        var cjxl = new Mock<ICjxlEncoder>();
        var djxl = new Mock<IJxlDecoder>();
        var rawRenderer = new Mock<IRawRenderer>();
        configureRenderer?.Invoke(rawRenderer);

        var fileService = new FileService(logger.Object);
        var converter = new ImageConverterService(exif.Object, fileService, logger.Object, djxl.Object);
        var service = new CompareConversionService(converter, cjxl.Object, djxl.Object, rawRenderer.Object, fileService, logger.Object);
        return new ServiceHarness(service, rawRenderer, dir);
    }

    [Fact]
    public async Task TwoConcurrentLeases_RenderInSlots_PromoteOnce()
    {
        var harness = CreateHarness();
        int started = 0;
        var bothStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.RawRenderer
            .Setup(x => x.RenderToPngAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(async (string _, string output, int _, CancellationToken _) =>
            {
                if (Interlocked.Increment(ref started) == 2)
                {
                    bothStarted.TrySetResult();
                }
                else
                {
                    await bothStarted.Task;
                }

                await Task.Delay(50);
                await File.WriteAllTextAsync(output, "png");
            });

        string[]? before = null;
        try
        {
            before = SnapshotMasterDirs();
            var firstTask = harness.Service.EnsureMasterRenderLeaseAsync(harness.InputPath);
            var secondTask = harness.Service.EnsureMasterRenderLeaseAsync(harness.InputPath);
            var leases = await Task.WhenAll(firstTask, secondTask);

            Assert.NotEqual(leases[0].PngPath, leases[1].PngPath);
            Assert.Equal(1, leases.Count(l => l.IsPromotedMaster));
            Assert.Contains(leases, l => l.PngPath.EndsWith("master.png"));

            foreach (var lease in leases)
            {
                lease.Complete();
            }

            string masterDir = harness.MasterDir;
            Assert.True(File.Exists(Path.Combine(masterDir, "master.png")));
            Assert.True(File.Exists(Path.Combine(masterDir, "meta.json")));
            Assert.Empty(Directory.EnumerateFiles(masterDir, "master.slot-*.png"));

            var cached = await harness.Service.EnsureMasterRenderLeaseAsync(harness.InputPath);
            Assert.True(cached.IsPromotedMaster);
            cached.Complete();

            foreach (var lease in leases)
            {
                lease.Complete();
            }
        }
        finally
        {
            TryDelete(harness.Dir);
            CleanupNewMasterDirs(before!);
        }
    }

    [Fact]
    public async Task ThirdLeaseBeyondLimit_JoinsPromotion()
    {
        var harness = CreateHarness();
        int started = 0;
        var bothStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.RawRenderer
            .Setup(x => x.RenderToPngAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(async (string _, string output, int _, CancellationToken _) =>
            {
                int index = Interlocked.Increment(ref started);
                if (index <= CompareDefaults.MaxConcurrentMasterRenders)
                {
                    if (index == CompareDefaults.MaxConcurrentMasterRenders)
                    {
                        bothStarted.TrySetResult();
                    }
                    else
                    {
                        await bothStarted.Task;
                    }
                }

                await File.WriteAllTextAsync(output, "png");
            });

        string[]? before = null;
        try
        {
            before = SnapshotMasterDirs();
            var tasks = new[]
            {
                harness.Service.EnsureMasterRenderLeaseAsync(harness.InputPath),
                harness.Service.EnsureMasterRenderLeaseAsync(harness.InputPath),
                harness.Service.EnsureMasterRenderLeaseAsync(harness.InputPath)
            };
            var leases = await Task.WhenAll(tasks);

            Assert.Equal(CompareDefaults.MaxConcurrentMasterRenders + 1, leases.Length);
            Assert.Contains(leases, l => l.IsPromotedMaster && l.PngPath.EndsWith("master.png"));

            foreach (var lease in leases)
            {
                lease.Complete();
            }
        }
        finally
        {
            TryDelete(harness.Dir);
            CleanupNewMasterDirs(before!);
        }
    }

    [Fact]
    public async Task AllSlotsFail_ThrowAndLeaveNoMaster()
    {
        var harness = CreateHarness(configureRenderer: renderer =>
            renderer
                .Setup(x => x.RenderToPngAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("render boom")));

        string[]? before = null;
        try
        {
            before = SnapshotMasterDirs();
            var tasks = new[]
            {
                harness.Service.EnsureMasterRenderLeaseAsync(harness.InputPath),
                harness.Service.EnsureMasterRenderLeaseAsync(harness.InputPath)
            };

            var failures = await Task.WhenAll(
                Assert.ThrowsAsync<InvalidOperationException>(() => tasks[0]),
                Assert.ThrowsAsync<InvalidOperationException>(() => tasks[1]));

            Assert.All(failures, f => Assert.Equal("render boom", f.Message));

            string masterDir = harness.MasterDir;
            Assert.False(Directory.Exists(masterDir) && File.Exists(Path.Combine(masterDir, "master.png")));
            Assert.Empty(Directory.Exists(masterDir) ? Directory.EnumerateFiles(masterDir, "master.slot-*.png") : Array.Empty<string>());
        }
        finally
        {
            TryDelete(harness.Dir);
            CleanupNewMasterDirs(before!);
        }
    }

    private static string[] SnapshotMasterDirs()
    {
        string root = Path.Combine(CompareDefaults.CacheRoot, "master");
        return Directory.Exists(root)
            ? Directory.EnumerateDirectories(root).ToArray()
            : Array.Empty<string>();
    }

    private static void CleanupNewMasterDirs(string[] before)
    {
        var known = new HashSet<string>(before, StringComparer.OrdinalIgnoreCase);
        foreach (string dir in SnapshotMasterDirs())
        {
            if (!known.Contains(dir))
            {
                TryDelete(dir);
            }
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        catch
        {
        }
    }
}
