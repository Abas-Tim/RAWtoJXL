using System.Collections.Generic;
using System.Threading.Tasks;

namespace ARWtoJXL.Avalonia.Services
{
    public interface IFilePickerService
    {
        Task<IEnumerable<string>> PickFilesAsync(string title, string filter, bool multiselect);
        Task<string?> PickFolderAsync(string initialDirectory);
    }
}
