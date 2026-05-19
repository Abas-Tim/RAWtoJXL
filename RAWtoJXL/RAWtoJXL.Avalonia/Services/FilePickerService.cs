using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace RAWtoJXL.Avalonia.Services
{
    public class FilePickerService : IFilePickerService
    {
        public async Task<IEnumerable<string>> PickFilesAsync(string title, string filter, bool multiselect)
        {
            var desktop = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            var topLevel = desktop?.MainWindow as Window;
            if (topLevel == null || topLevel.StorageProvider == null)
                return Enumerable.Empty<string>();

            var filterParts = filter.Split('|');
            var patterns = filterParts.Where((_, i) => i % 2 == 1).Select(p => p.Trim()).ToList();
            var displayName = filterParts.Length > 0 ? filterParts[0] : "All Files";

            var tipFilter = new FilePickerFileType(displayName)
            {
                Patterns = patterns
            };

            var options = new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = multiselect,
                FileTypeFilter = new[] { tipFilter }
            };

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(options);
            return files.Select(f => f.Path.LocalPath);
        }

        public async Task<string?> PickFolderAsync(string? initialDirectory)
        {
            var desktop = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            var topLevel = desktop?.MainWindow as Window;
            if (topLevel == null || topLevel.StorageProvider == null)
                return null;

            var options = new FolderPickerOpenOptions
            {
                Title = "Select Folder"
            };

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(options);
            return folders.FirstOrDefault()?.Path.LocalPath;
        }
    }
}
