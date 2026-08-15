using System;
using System.IO;
using RAWtoJXL.Core.Interfaces;
using RAWtoJXL.Core.Settings;

namespace RAWtoJXL.Core.Services
{
    public static class OutputPathResolver
    {
        public static string GetOutputExtension(OutputFormat format)
        {
            return format switch
            {
                OutputFormat.Jxl => ".jxl",
                OutputFormat.Jpeg => ".jpg",
                OutputFormat.Avif => ".avif",
                _ => ".jxl"
            };
        }

        public static string? Resolve(
            string inputPath,
            OutputFormat outputFormat,
            ConflictResolution conflictResolution,
            bool useCustomOutputDirectory,
            string? customOutputDirectory,
            bool useSubfolder,
            string subfolderName,
            bool createDirectory = true)
        {
            string directory;
            if (useCustomOutputDirectory && !string.IsNullOrEmpty(customOutputDirectory))
            {
                directory = customOutputDirectory!;
            }
            else
            {
                directory = useSubfolder
                    ? Path.Combine(Path.GetDirectoryName(inputPath)!, subfolderName)
                    : Path.GetDirectoryName(inputPath)!;
            }
            if (createDirectory)
            {
                Directory.CreateDirectory(directory);
            }

            string baseName = Path.GetFileNameWithoutExtension(inputPath);
            string extension = GetOutputExtension(outputFormat);
            string outputPath = Path.Combine(directory, baseName + extension);

            return ResolveConflict(outputPath, conflictResolution);
        }

        private static string? ResolveConflict(string outputPath, ConflictResolution conflictResolution)
        {
            if (!File.Exists(outputPath))
                return outputPath;

            switch (conflictResolution)
            {
                case ConflictResolution.Skip:
                    return null;
                case ConflictResolution.Overwrite:
                    return outputPath;
                case ConflictResolution.AppendNumber:
                    int counter = 1;
                    string directory = Path.GetDirectoryName(outputPath)!;
                    string baseName = Path.GetFileNameWithoutExtension(outputPath);
                    string extension = Path.GetExtension(outputPath);
                    string candidate;
                    do
                    {
                        candidate = Path.Combine(directory, $"{baseName}_{counter}{extension}");
                        counter++;
                    } while (File.Exists(candidate));
                    return candidate;
                default:
                    return outputPath;
            }
        }
    }
}
