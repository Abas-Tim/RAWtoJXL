using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using RAWtoJXL.Avalonia.Services;
using RAWtoJXL.Core.Interfaces;

namespace RAWtoJXL.Avalonia
{
    internal static class CrashReporter
    {
        private static bool _dialogOpen;

        public static void Record(ILogger? logger, string context, Exception? ex)
        {
            Record(logger, context, ex?.ToString() ?? "(null exception)");
        }

        public static void Record(ILogger? logger, string context, string message)
        {
            if (logger != null)
            {
                try
                {
                    logger.Write($"[CrashReporter:{context}] {message}");
                }
                catch
                {
                }
            }

            ShowErrorDialog(message);
        }

        private static void ShowErrorDialog(string message)
        {
            if (_dialogOpen || Application.Current is null)
            {
                return;
            }

            try
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (_dialogOpen)
                    {
                        return;
                    }
                    _dialogOpen = true;
                    var dialog = new ErrorDialog
                    {
                        MessageText = message,
                        TitleText = "RAWtoJXL Error"
                    };
                    dialog.Closed += (_, _) => _dialogOpen = false;
                    var desktop = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
                    var parent = desktop?.MainWindow;
                    if (parent != null)
                    {
                        dialog.ShowDialog(parent);
                    }
                    else
                    {
                        dialog.Show();
                    }
                });
            }
            catch
            {
            }
        }
    }
}
