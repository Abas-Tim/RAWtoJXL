using RAWtoJXL.Core.Models;
using RAWtoJXL.Core.Interfaces;
using RAWtoJXL.Core.Services;
using RAWtoJXL.Core.Settings;

namespace RAWtoJXL.Tests;

public sealed class BatchConversionPlannerTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "RAWtoJXL_Planner_" + Guid.NewGuid().ToString("N"));

    public BatchConversionPlannerTests()
    {
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void AppendNumber_ReservesDistinctPathsCaseInsensitively()
    {
        var first = CreateInput(Path.Combine("a", "photo.arw"));
        var second = CreateInput(Path.Combine("b", "PHOTO.cr3"));
        var outputDirectory = Path.Combine(_directory, "out");

        var requests = BatchConversionPlanner.CreateRequests(
            new[]
            {
                new BatchConversionInput("a", first, 90),
                new BatchConversionInput("b", second, 90)
            },
            OutputFormat.Jxl,
            ConflictResolution.AppendNumber,
            useCustomOutputDirectory: true,
            customOutputDirectory: outputDirectory,
            useSubfolder: false,
            subfolderName: "unused",
            skipMetadata: true,
            effort: 7,
            threads: 2);

        Assert.All(requests, request => Assert.Null(request.InitialStatus));
        Assert.Equal(2, requests.Select(request => request.OutputPath).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(Path.Combine(outputDirectory, "photo.jxl"), requests[0].OutputPath);
        Assert.Equal(Path.Combine(outputDirectory, "PHOTO_1.jxl"), requests[1].OutputPath);
    }

    [Fact]
    public void DuplicateDestinations_AreRejectedForOverwrite()
    {
        var first = CreateInput(Path.Combine("a", "photo.arw"));
        var second = CreateInput(Path.Combine("b", "photo.cr3"));
        var outputDirectory = Path.Combine(_directory, "out");

        var requests = BatchConversionPlanner.CreateRequests(
            new[]
            {
                new BatchConversionInput("a", first, 90),
                new BatchConversionInput("b", second, 90)
            },
            OutputFormat.Jxl,
            ConflictResolution.Overwrite,
            useCustomOutputDirectory: true,
            customOutputDirectory: outputDirectory,
            useSubfolder: false,
            subfolderName: "unused",
            skipMetadata: true,
            effort: 7,
            threads: 2);

        Assert.Null(requests[0].InitialStatus);
        Assert.Equal(BatchConversionStatus.Failed, requests[1].InitialStatus);
        Assert.Contains("already used", requests[1].InitialError);
    }

    private string CreateInput(string relativePath)
    {
        var input = Path.Combine(_directory, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(input)!);
        File.WriteAllText(input, "input");
        return input;
    }
}
