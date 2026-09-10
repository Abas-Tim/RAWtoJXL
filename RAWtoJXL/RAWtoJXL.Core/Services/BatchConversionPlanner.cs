using System;
using System.Collections.Generic;
using System.IO;
using RAWtoJXL.Core.Interfaces;
using RAWtoJXL.Core.Models;
using RAWtoJXL.Core.Settings;

namespace RAWtoJXL.Core.Services;

public static class BatchConversionPlanner
{
    public static IReadOnlyList<BatchConversionRequest> CreateRequests(
        IReadOnlyList<BatchConversionInput> inputs,
        OutputFormat outputFormat,
        ConflictResolution conflict,
        bool useCustomOutputDirectory,
        string? customOutputDirectory,
        bool useSubfolder,
        string subfolderName,
        bool skipMetadata,
        int? effort,
        int? threads)
    {
        var requests = new List<BatchConversionRequest>(inputs.Count);
        var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var input in inputs)
        {
            var inputPath = Path.GetFullPath(input.InputPath);
            string? outputPath = null;
            BatchConversionStatus? initialStatus = null;
            string? initialError = null;

            try
            {
                if (conflict == ConflictResolution.AppendNumber)
                {
                    var basePath = OutputPathResolver.Resolve(
                        inputPath,
                        outputFormat,
                        ConflictResolution.Overwrite,
                        useCustomOutputDirectory,
                        customOutputDirectory,
                        useSubfolder,
                        subfolderName,
                        createDirectory: false);

                    if (basePath != null)
                    {
                        outputPath = ReserveAppendNumber(basePath, reserved);
                    }
                }
                else
                {
                    outputPath = OutputPathResolver.Resolve(
                        inputPath,
                        outputFormat,
                        conflict,
                        useCustomOutputDirectory,
                        customOutputDirectory,
                        useSubfolder,
                        subfolderName,
                        createDirectory: false);
                }

                if (outputPath == null)
                {
                    initialStatus = BatchConversionStatus.Skipped;
                    initialError = "output already exists";
                }
                else if (!reserved.Add(NormalizePath(outputPath)))
                {
                    initialStatus = BatchConversionStatus.Failed;
                    initialError = "output path already used by another input file";
                }
            }
            catch (Exception ex)
            {
                initialStatus = BatchConversionStatus.Failed;
                initialError = ex.GetBaseException().Message;
            }

            requests.Add(new BatchConversionRequest(
                input.Id,
                inputPath,
                outputPath,
                input.Quality,
                outputFormat,
                skipMetadata,
                effort,
                threads,
                conflict,
                initialStatus,
                initialError));
        }

        return requests;
    }

    private static string ReserveAppendNumber(string basePath, HashSet<string> reserved)
    {
        var directory = Path.GetDirectoryName(basePath) ?? string.Empty;
        var baseName = Path.GetFileNameWithoutExtension(basePath);
        var extension = Path.GetExtension(basePath);
        var candidate = basePath;
        var counter = 1;

        while (File.Exists(candidate) || reserved.Contains(NormalizePath(candidate)))
        {
            candidate = Path.Combine(directory, $"{baseName}_{counter}{extension}");
            counter++;
        }

        return candidate;
    }

    private static string NormalizePath(string path) => Path.GetFullPath(path);
}
