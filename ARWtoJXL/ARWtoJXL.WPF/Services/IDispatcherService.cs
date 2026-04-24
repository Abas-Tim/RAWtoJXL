using System.Threading.Tasks;

namespace ARWtoJXL.WPF.Services
{
    public interface IDispatcherService
    {
        Task InvokeAsync(Action action);
    }
}
