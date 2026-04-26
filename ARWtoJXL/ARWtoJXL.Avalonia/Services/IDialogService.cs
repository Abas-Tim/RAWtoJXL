using System.Threading.Tasks;

namespace ARWtoJXL.Avalonia.Services
{
    public interface IDialogService
    {
        Task<bool> ShowConfirmAsync(string message, string title);
    }
}
