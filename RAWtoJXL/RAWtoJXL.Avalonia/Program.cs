using System;
using Avalonia;
using RAWtoJXL.Core.Services;

namespace RAWtoJXL.Avalonia
{
    internal static class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant API before invoking Main.
        [STAThread]
        public static void Main(string[] args)
        {
            ProcessorAffinityService.TryExpandToAllLogicalProcessors();
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        public static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .LogToTrace();
        }
    }
}
