using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;

namespace ARWtoJXL.Tests;

[Trait("category", "smoke")]
public class SmokeTests : IDisposable
{
    private Application? _app;
    private UIA3Automation? _automation;
    private Window? _mainWindow;

    private static string GetAppPath()
    {
        var assemblyDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)
                          ?? Directory.GetCurrentDirectory();
        var paths = new[]
        {
            Path.Combine(assemblyDir, "..", "..", "..", "..", "ARWtoJXL.WPF", "bin", "Debug", "net8.0-windows", "ARWtoJXL.WPF.exe"),
            Path.Combine(assemblyDir, "..", "..", "..", "..", "ARWtoJXL.WPF", "bin", "Release", "net8.0-windows", "ARWtoJXL.WPF.exe"),
        };
        foreach (var path in paths)
        {
            if (File.Exists(path)) return path;
        }
        throw new InvalidOperationException("ARWtoJXL.WPF.exe not found. Build the WPF project first.");
    }

    private void LaunchApp()
    {
        _app = Application.Launch(GetAppPath());
        _automation = new UIA3Automation();
        _mainWindow = _app.GetMainWindow(_automation, TimeSpan.FromSeconds(10));
        Assert.NotNull(_mainWindow);
    }

    [Fact]
    public void MainWindow_Opens_And_HasExpectedTitle()
    {
        LaunchApp();
        Assert.Equal("ARW to JXL Converter", _mainWindow!.Title);
    }

    [Fact]
    public void MainWindow_HasToolbarButtons()
    {
        LaunchApp();
        var buttonNames = new[] { "Open File", "Select All", "Convert", "Remove", "Settings" };
        foreach (var name in buttonNames)
        {
            var button = _mainWindow!.FindFirstDescendant(cf => cf.ByName(name).And(cf.ByControlType(ControlType.Button)));
            Assert.NotNull(button);
        }
    }

    [Fact]
    public void MainWindow_HasGalleryListBox()
    {
        LaunchApp();
        var listBox = _mainWindow!.FindFirstDescendant(cf => cf.ByControlType(ControlType.List));
        Assert.NotNull(listBox);
    }

    [Fact]
    public void MainWindow_HasProgressBar()
    {
        LaunchApp();
        var progressBar = _mainWindow!.FindFirstDescendant(cf => cf.ByControlType(ControlType.ProgressBar));
        Assert.NotNull(progressBar);
    }

    public void Dispose()
    {
        try
        {
            _mainWindow?.Close();
        }
        catch { }
        try
        {
            _app?.Kill();
        }
        catch { }
        _automation?.Dispose();
    }
}
