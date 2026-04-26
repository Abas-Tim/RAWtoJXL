using System.Threading.Tasks;

namespace ARWtoJXL.Avalonia.Services
{
    public interface IDispatcherService
    {
        Task InvokeAsync(Action action);
    }
}
