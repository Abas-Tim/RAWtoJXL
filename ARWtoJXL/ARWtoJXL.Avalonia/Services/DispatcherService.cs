using System;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace ARWtoJXL.Avalonia.Services
{
    public class DispatcherService : IDispatcherService
    {
        public async Task InvokeAsync(Action action)
        {
            await Dispatcher.UIThread.InvokeAsync(action);
        }
    }
}
