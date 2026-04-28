using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using ARWtoJXL.Core;
using ARWtoJXL.Core.Interfaces;
using ARWtoJXL.Avalonia.Services;
using ARWtoJXL.Avalonia.ViewModels;

namespace ARWtoJXL.Avalonia;

public partial class App : Application
{
    public static IServiceProvider? Services { get; internal set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddCoreServices();
            serviceCollection.AddSingleton<IDialogService, DialogService>();
            serviceCollection.AddSingleton<IDispatcherService, DispatcherService>();
            serviceCollection.AddSingleton<IFilePickerService, FilePickerService>();
            var serviceProvider = serviceCollection.BuildServiceProvider();
            Services = serviceProvider;

            var imageService = serviceProvider.GetRequiredService<IImageService>();
            var dialogService = serviceProvider.GetRequiredService<IDialogService>();
            var dispatcherService = serviceProvider.GetRequiredService<IDispatcherService>();
            var filePickerService = serviceProvider.GetRequiredService<IFilePickerService>();
            var viewModel = new MainViewModel(imageService, dialogService, dispatcherService, filePickerService);

            var mainWindow = new MainWindow
            {
                DataContext = viewModel
            };
            viewModel.RequestOpenSettings += () => mainWindow.OpenSettings();
            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
