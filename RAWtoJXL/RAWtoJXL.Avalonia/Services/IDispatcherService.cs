using System.Threading.Tasks;

namespace RAWtoJXL.Avalonia.Services
{
    public interface IDispatcherService
    {
        Task InvokeAsync(Action action);
    }
}
