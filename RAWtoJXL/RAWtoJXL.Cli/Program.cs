using System;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using RAWtoJXL.Cli;
using RAWtoJXL.Core;
using RAWtoJXL.Core.Services;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        ProcessorAffinityService.TryExpandToAllLogicalProcessors();
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;

        var services = new ServiceCollection();
        services.AddCoreServices();
        using var provider = services.BuildServiceProvider();

        return await CliApplication.RunAsync(args, Console.Out, Console.Error, provider);
    }
}
