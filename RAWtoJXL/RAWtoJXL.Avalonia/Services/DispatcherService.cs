using System;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace RAWtoJXL.Avalonia.Services
{
    public class DispatcherService : IDispatcherService
    {
        public async Task InvokeAsync(Action action)
        {
            await Dispatcher.UIThread.InvokeAsync(action);
        }
    }
}
