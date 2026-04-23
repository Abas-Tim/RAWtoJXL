using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using System.Runtime.InteropServices;

namespace ARWtoJXL.Tests;

[Trait("category", "smoke")]
public class SmokeTests : IDisposable
{
    private Application? _app;
    private UIA3Automation? _automation;
    private Window? _mainWindow;
    private readonly List<string> _tempFiles = new();

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

    [Fact(Timeout = 60000)]
    public void RemoveSelected_DoesNotCrashApp()
    {
        LaunchApp();

        var tempDir = Path.Combine(Path.GetTempPath(), "ARWtoJXL_Test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        var tempFiles = new[]
        {
            Path.Combine(tempDir, "test1.arw"),
            Path.Combine(tempDir, "test2.arw"),
        };

        foreach (var f in tempFiles)
        {
            File.WriteAllText(f, "");
            _tempFiles.Add(f);
        }

        try
        {
            var openFileButton = _mainWindow!.FindFirstDescendant(cf => cf.ByName("Open File").And(cf.ByControlType(ControlType.Button)));
            Assert.NotNull(openFileButton);
            openFileButton!.Click();

            Thread.Sleep(1000);

            var dialogWindow = _app!.GetAllTopLevelWindows(_automation!).FirstOrDefault(w => w.Title.Contains("Open"));
            if (dialogWindow == null)
            {
                dialogWindow = _app.GetAllTopLevelWindows(_automation!).FirstOrDefault();
            }
            Assert.NotNull(dialogWindow);

            var editBox = dialogWindow!.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit));
            Assert.NotNull(editBox);

            var dialogWinHandle = System.Diagnostics.Process.GetProcessById(_app!.ProcessId).MainWindowHandle;
            SetForegroundWindow(dialogWinHandle);
            Thread.Sleep(200);

            editBox!.Focus();
            Thread.Sleep(200);

            var handle = GetForegroundWindow();
            SetForegroundWindow(handle);
            Thread.Sleep(100);

            // Type the file path character by character
            foreach (var c in tempFiles[0])
            {
                keybd_event((byte)c, 0, 0, IntPtr.Zero);
                keybd_event((byte)c, 0, KEYEVENTF_KEYUP, IntPtr.Zero);
                Thread.Sleep(5);
            }
            Thread.Sleep(500);

            PressEnter();
            Thread.Sleep(2000);

            Thread.Sleep(2000);

            _mainWindow = _app.GetMainWindow(_automation!, TimeSpan.FromSeconds(5)) ?? _mainWindow;
            Assert.NotNull(_mainWindow);

            var listBox = _mainWindow!.FindFirstDescendant(cf => cf.ByControlType(ControlType.List));
            Assert.NotNull(listBox);
            var initialItems = listBox!.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem)).ToList();
            var initialItemCount = initialItems.Count;
            Assert.True(initialItemCount >= 1, $"Expected at least 1 item in gallery, got {initialItemCount}");

            Thread.Sleep(500);

            var selectAllButton = _mainWindow.FindFirstDescendant(cf => cf.ByName("Select All").And(cf.ByControlType(ControlType.Button)));
            Assert.NotNull(selectAllButton);
            selectAllButton!.Click();
            Thread.Sleep(500);

            var removeButton = _mainWindow.FindFirstDescendant(cf => cf.ByName("Remove").And(cf.ByControlType(ControlType.Button)));
            Assert.NotNull(removeButton);
            removeButton!.Click();

            Thread.Sleep(1000);

            Assert.NotNull(_mainWindow);

            var remainingItems = listBox!.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem)).ToList();
            var remainingCount = remainingItems.Count;
            Assert.True(remainingCount < initialItemCount, $"Expected fewer items after removal. Before: {initialItemCount}, After: {remainingCount}");

            Assert.Equal("ARW to JXL Converter", _mainWindow.Title);
        }
        finally
        {
            foreach (var f in _tempFiles)
            {
                try { File.Delete(f); } catch { }
            }
            try { Directory.Delete(tempDir, false); } catch { }
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, IntPtr dwExtraInfo);

    private const int KEYEVENTF_KEYUP = 0x0002;
    private const byte VK_V = 0x56;
    private const byte VK_CONTROL = 0x11;
    private const byte VK_RETURN = 0x0D;

    private static void PressKey(byte vk, bool ctrl = false)
    {
        if (ctrl) keybd_event(VK_CONTROL, 0, 0, IntPtr.Zero);
        keybd_event(vk, 0, 0, IntPtr.Zero);
        keybd_event(vk, 0, KEYEVENTF_KEYUP, IntPtr.Zero);
        if (ctrl) keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, IntPtr.Zero);
    }

    private static void CtrlV()
    {
        PressKey(VK_V, ctrl: true);
        Thread.Sleep(100);
    }

    private static void PressEnter()
    {
        PressKey(VK_RETURN);
        Thread.Sleep(100);
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
