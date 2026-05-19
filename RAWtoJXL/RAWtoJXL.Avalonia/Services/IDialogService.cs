using System.Threading.Tasks;

namespace RAWtoJXL.Avalonia.Services
{
    public interface IDialogService
    {
        Task<bool> ShowConfirmAsync(string message, string title);
    }
}
