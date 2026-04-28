using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using ARWtoJXL.Avalonia;
using ARWtoJXL.Avalonia.Services;
using ARWtoJXL.Avalonia.ViewModels;
using ARWtoJXL.Core.Interfaces;
using Moq;

namespace ARWtoJXL.Tests.GUITests;

[Trait("category", "gui")]
public static class GUITestHelpers
{
    public static MainViewModel CreateViewModel(
        Mock<IImageService>? imageService = null,
        Mock<IDialogService>? dialogService = null,
        Mock<IDispatcherService>? dispatcherService = null,
        Mock<IFilePickerService>? filePickerService = null)
    {
        imageService ??= new Mock<IImageService>();
        dialogService ??= new Mock<IDialogService>();
        dispatcherService ??= new Mock<IDispatcherService>();
        dispatcherService.Setup(x => x.InvokeAsync(It.IsAny<Action>()))
                         .Returns<Action>(a => { a(); return Task.CompletedTask; });

        filePickerService ??= new Mock<IFilePickerService>();
        filePickerService.Setup(x => x.PickFilesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                         .ReturnsAsync(Array.Empty<string>());
        filePickerService.Setup(x => x.PickFolderAsync(It.IsAny<string>()))
                         .ReturnsAsync((string?)null);

        return new MainViewModel(
            imageService.Object,
            dialogService.Object,
            dispatcherService.Object,
            filePickerService.Object);
    }

    public static MainWindow CreateWindow(MainViewModel? vm = null)
    {
        vm ??= CreateViewModel();
        var window = new MainWindow();
        window.DataContext = vm;
        window.Show();
        window.UpdateLayout();
        return window;
    }

    public static IEnumerable<T> GetAllControls<T>(Control root) where T : class
    {
        var result = new List<T>();
        Traverse(root);
        return result;

        void Traverse(Control control)
        {
            if (control is T tCtrl) result.Add(tCtrl);

            if (control is Window win && win.Content is Control wc) Traverse(wc);
            else if (control is Panel panel)
            {
                foreach (var child in panel.Children.OfType<Control>())
                    Traverse(child);
            }
            else if (control is Border border && border.Child is Control bc) Traverse(bc);
            else if (control is ContentControl cc && cc.Content is Control ccc) Traverse(ccc);
            else if (control is ItemsControl ic)
            {
                foreach (var container in GetItemContainers(ic))
                    Traverse(container);
            }
            else if (control is ScrollViewer sv && sv.Content is Control sc) Traverse(sc);
        }
    }

    public static IEnumerable<T> FindAll<T>(Control root) where T : class
    {
        var results = new List<T>();
        Traverse(root);
        return results;

        void Traverse(Control control)
        {
            if (control is T match) results.Add(match);
            if (control is Window win && win.Content is Control wc) Traverse(wc);
            else if (control is Panel panel)
            {
                foreach (var child in panel.Children.OfType<Control>())
                    Traverse(child);
            }
            else if (control is Border border && border.Child is Control bc) Traverse(bc);
            else if (control is ContentControl cc && cc.Content is Control ccc) Traverse(ccc);
            else if (control is ItemsControl ic)
            {
                foreach (var container in GetItemContainers(ic))
                    Traverse(container);
            }
            else if (control is ScrollViewer sv && sv.Content is Control sc) Traverse(sc);
        }
    }

    public static IEnumerable<Control> GetItemContainers(ItemsControl ic)
    {
        if (ic.Items is System.Collections.IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                var container = ic.ContainerFromItem(item);
                if (container is Control c) yield return c;
            }
        }
    }

    public static List<string?> GetButtonContents(Window window)
    {
        return GetAllControls<Button>(window).Select(b => b.Content?.ToString()).ToList();
    }

    public static TabItem SelectTab(SettingsWindow window, string header)
    {
        var tabControl = FindAll<TabControl>(window).First();
        var tab = tabControl.Items.Cast<TabItem>()
            .First(t => t.Header?.ToString() == header);
        tabControl.SelectedItem = tab;
        return tab;
    }

    public sealed class SettingsScope : IDisposable
    {
        readonly string _settingsPath;
        readonly string _backupPath;
        readonly bool _fileExisted;

        public SettingsScope()
        {
            _settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ARWtoJXL", "settings.json");
            _backupPath = _settingsPath + ".gui_test_backup_" + Guid.NewGuid().ToString("N");
            _fileExisted = File.Exists(_settingsPath);
            if (_fileExisted)
                File.Copy(_settingsPath, _backupPath, overwrite: true);
            SettingsService.Save(new AppSettings());
        }

        public void Dispose()
        {
            try
            {
                if (File.Exists(_settingsPath))
                    File.Delete(_settingsPath);
                if (_fileExisted && File.Exists(_backupPath))
                    File.Copy(_backupPath, _settingsPath, overwrite: true);
                if (File.Exists(_backupPath))
                    File.Delete(_backupPath);
            }
            catch { }
        }
    }
}
