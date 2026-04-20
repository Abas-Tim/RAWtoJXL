using System.Configuration;
using System.Data;
using System.Windows;

namespace ARWtoJXL.WPF;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private void App_OnStartup(object sender, StartupEventArgs e)
    {
        var mainWindow = new MainWindow();
        Wpf.Ui.Appearance.ApplicationThemeManager.Apply(mainWindow);
        mainWindow.Show();
    }
}

