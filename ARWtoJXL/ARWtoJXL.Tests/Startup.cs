using Microsoft.Extensions.DependencyInjection;
using ARWtoJXL.Core.Interfaces;
using ARWtoJXL.Core.Services;

namespace ARWtoJXL.Tests;

public class Startup
{
    protected static readonly string TestArwPath = GetTestArwPath();

    private static string GetTestArwPath()
    {
        var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
        var assemblyDir = Path.GetDirectoryName(assemblyLocation) ?? Directory.GetCurrentDirectory();
        var testFile = Path.Combine(assemblyDir, "test1.ARW");

        if (File.Exists(testFile))
            return testFile;

        throw new InvalidOperationException($"Test ARW file not found at: {testFile}");
    }

    protected IServiceProvider Services { get; }

    public Startup()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();
    }

    protected IServiceScope CreateScope()
    {
        return Services.CreateScope();
    }

    protected virtual void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IPathResolver, PathResolverService>();
        services.AddSingleton<IFileService, FileService>();
        services.AddSingleton<ISizeEstimator, SizeEstimatorService>();
        services.AddSingleton<IMagickService, MagickService>();
        services.AddSingleton<ICjxlEncoder>(sp =>
        {
            var pathResolver = sp.GetRequiredService<IPathResolver>();
            return new CjxlEncoderService(pathResolver);
        });
        services.AddSingleton<IImageService>(sp =>
        {
            var magickService = sp.GetRequiredService<IMagickService>();
            var cjxlEncoder = sp.GetRequiredService<ICjxlEncoder>();
            var fileService = sp.GetRequiredService<IFileService>();
            var pathResolver = sp.GetRequiredService<IPathResolver>();
            var sizeEstimator = sp.GetRequiredService<ISizeEstimator>();
            return new ImageProcessingService(magickService, cjxlEncoder, fileService, pathResolver, sizeEstimator);
        });
    }

    protected static string GetOutputPath(string suffix)
    {
        var dir = Path.GetDirectoryName(TestArwPath)!;
        return Path.Combine(dir, $"test1_{suffix}.jxl");
    }

    protected static async Task CleanOutputFile(string outputPath)
    {
        if (File.Exists(outputPath))
            File.Delete(outputPath);
    }
}
