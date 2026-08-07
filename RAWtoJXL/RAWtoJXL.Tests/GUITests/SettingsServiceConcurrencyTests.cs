using System.IO;
using System.Text.Json;
using RAWtoJXL.Avalonia;

namespace RAWtoJXL.Tests.GUITests;

[Collection("Settings")]
public class SettingsServiceConcurrencyTests
{
    private static string SettingsPath => SettingsService.SettingsPath;

    [Fact]
    public async Task ConcurrentWritesAndReads_NeverObserveTornJson()
    {
        using var _ = new GUITestHelpers.SettingsScope();

        var corrupt = 0;
        using var stop = new CancellationTokenSource();

        var readTask = Task.Run(() =>
        {
            while (!stop.IsCancellationRequested)
            {
                if (!File.Exists(SettingsPath)) continue;
                try
                {
                    using var fs = new FileStream(SettingsPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    JsonSerializer.Deserialize<AppSettings>(fs);
                }
                catch (JsonException)
                {
                    Interlocked.Increment(ref corrupt);
                }
                catch (IOException)
                {
                }
            }
        });

        var writers = Enumerable.Range(0, 8).Select(i => Task.Run(() =>
        {
            for (int j = 0; j < 20; j++)
            {
                var s = SettingsService.Load();
                s.QualityPreset = (i + j) % 101;
                SettingsService.Save(s);
            }
        }));

        await Task.WhenAll(writers);
        stop.Cancel();
        await readTask;

        Assert.Equal(0, corrupt);
    }

    [Fact]
    public void ConcurrentSaves_FileAlwaysReadableAndValid()
    {
        using var _ = new GUITestHelpers.SettingsScope();

        var tasks = Enumerable.Range(0, 8).Select(i => Task.Run(() =>
        {
            for (int j = 0; j < 20; j++)
            {
                var s = SettingsService.Load();
                s.QualityPreset = (i * 100 + j) % 101;
                SettingsService.Save(s);
            }
        }));

        Task.WaitAll(tasks.ToArray());

        var final = SettingsService.Load();
        Assert.InRange(final.QualityPreset, 0, 100);
    }
}
