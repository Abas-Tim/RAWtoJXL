using Microsoft.Extensions.DependencyInjection;
using ARWtoJXL.Core.Interfaces;
using ARWtoJXL.Core.Services;

namespace ARWtoJXL.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        services.AddSingleton<ILogger, FileLogger>();
        services.AddSingleton<IProcessRunner, SystemProcessRunner>();
        services.AddSingleton<IFileService, FileService>(sp => new FileService(sp.GetRequiredService<ILogger>()));
        services.AddSingleton<IPathResolver, PathResolverService>();
        services.AddSingleton<IExiftoolService, ExiftoolService>();
        services.AddSingleton<IImageConverterService, ImageConverterService>();
        services.AddSingleton<ICjxlEncoder, CjxlEncoderService>();
        services.AddSingleton<IImageService, ImageProcessingService>();

        return services;
    }
}
