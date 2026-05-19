using Microsoft.Extensions.DependencyInjection;
using RAWtoJXL.Core.Interfaces;
using RAWtoJXL.Core.Services;

namespace RAWtoJXL.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        services.AddSingleton<ILogger, FileLogger>();
        services.AddTransient<IProcessRunner, SystemProcessRunner>();
        services.AddTransient<IFileService, FileService>();
        services.AddTransient<IPathResolver, PathResolverService>();
        services.AddTransient<IExiftoolService, ExiftoolService>();
        services.AddTransient<IImageConverterService, ImageConverterService>();
        services.AddTransient<ICjxlEncoder, CjxlEncoderService>();
        services.AddTransient<IImageService, ImageProcessingService>();

        return services;
    }
}
