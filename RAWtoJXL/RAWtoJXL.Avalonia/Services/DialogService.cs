using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace RAWtoJXL.Avalonia.Services
{
    public class DialogService : IDialogService
    {
        public Task<bool> ShowConfirmAsync(string message, string title)
        {
            var dialog = new ConfirmDialog
            {
                MessageText = message,
                TitleText = title
            };

            var desktop = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            var parent = desktop?.MainWindow;

            if (parent != null)
            {
                return dialog.ShowDialog<bool>(parent);
            }

            return Task.FromResult(false);
        }
    }
}
