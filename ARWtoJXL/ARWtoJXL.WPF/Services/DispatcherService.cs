using System.Threading.Tasks;
using System.Windows.Threading;

namespace ARWtoJXL.WPF.Services
{
    public class DispatcherService : IDispatcherService
    {
        private readonly Dispatcher _dispatcher;

        public DispatcherService(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        public Task InvokeAsync(Action action)
        {
            return _dispatcher.InvokeAsync(action).Task;
        }
    }
}
