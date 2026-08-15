using System.IO;
using RAWtoJXL.Core.Interfaces;
using RAWtoJXL.Core.Settings;

namespace RAWtoJXL.Tests;

public class SettingsServiceLegacyOutputFormatTests
{
    private static string NewSandbox()
    {
        var dir = Path.Combine(Path.GetTempPath(), "RAWtoJXL_SettingsLegacy_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        SettingsService.Reset();
        SettingsService.SettingsDirectory = dir;
        return dir;
    }

    [Fact]
    public void Load_LegacyPngNumericValue_MapsToJxl()
    {
        var dir = NewSandbox();
        try
        {
            var json = """
                {
                  "outputFormat": 2,
                  "presets": [
                    {
                      "name": "old",
                      "outputFormat": 2
                    }
                  ]
                }
                """;
            File.WriteAllText(Path.Combine(dir, "settings.json"), json);

            var settings = SettingsService.Load();

            Assert.Equal(OutputFormat.Jxl, settings.OutputFormat);
            Assert.Equal(OutputFormat.Jxl, settings.Presets[0].OutputFormat);
        }
        finally
        {
            SettingsService.Reset();
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Load_LegacyPngStringValue_MapsToJxl()
    {
        var dir = NewSandbox();
        try
        {
            var json = """
                {
                  "outputFormat": "Png"
                }
                """;
            File.WriteAllText(Path.Combine(dir, "settings.json"), json);

            var settings = SettingsService.Load();

            Assert.Equal(OutputFormat.Jxl, settings.OutputFormat);
        }
        finally
        {
            SettingsService.Reset();
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Save_Load_AvifRoundTrips()
    {
        var dir = NewSandbox();
        try
        {
            SettingsService.Save(new AppSettings { OutputFormat = OutputFormat.Avif });

            var loaded = SettingsService.Load();

            Assert.Equal(OutputFormat.Avif, loaded.OutputFormat);
        }
        finally
        {
            SettingsService.Reset();
            Directory.Delete(dir, true);
        }
    }
}
