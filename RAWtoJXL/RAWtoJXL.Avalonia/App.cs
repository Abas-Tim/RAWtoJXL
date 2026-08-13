using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using RAWtoJXL.Core;
using RAWtoJXL.Core.Interfaces;
using RAWtoJXL.Core.Settings;
using RAWtoJXL.Avalonia.Services;
using RAWtoJXL.Avalonia.ViewModels;

using Avalonia.Threading;

namespace RAWtoJXL.Avalonia;

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
            desktop.Exit += OnDesktopExit;

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

        var logger = Services?.GetService<ILogger>();

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            CrashReporter.Record(logger, "crash", args.ExceptionObject as Exception);

        Dispatcher.UIThread.UnhandledException += (_, args) =>
        {
            CrashReporter.Record(logger, "ui", args.Exception);
            args.Handled = true;
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            CrashReporter.Record(logger, "task", args.Exception);
            args.SetObserved();
        };

        base.OnFrameworkInitializationCompleted();
    }

    private void OnDesktopExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Exit -= OnDesktopExit;

        SettingsService.Flush();
    }
}
