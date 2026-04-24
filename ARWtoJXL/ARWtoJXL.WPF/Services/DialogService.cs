using System.Threading.Tasks;
using System.Windows;

namespace ARWtoJXL.WPF.Services
{
    public class DialogService : IDialogService
    {
        public Task<bool> ShowConfirmAsync(string message, string title)
        {
            var dialog = new ConfirmDialog
            {
                Owner = Application.Current.MainWindow,
                MessageText = message,
                TitleText = title
            };
            var result = dialog.ShowDialog() ?? false;
            dialog.Close();
            return Task.FromResult(result);
        }
    }
}
