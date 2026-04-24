using System.Threading.Tasks;

namespace ARWtoJXL.WPF.Services
{
    public interface IDialogService
    {
        Task<bool> ShowConfirmAsync(string message, string title);
    }
}
