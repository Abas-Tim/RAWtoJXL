using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using ImageMagick;
using ARWtoJXL.Core.Services;
using Xunit;

namespace ARWtoJXL.Tests
{
    public class MetadataDebugTests
    {
        private const string TestArwPath = @"C:\Users\timur\Desktop\Playgroung\ARWtoJPEGXL\ARWtoJXL\ARWtoJXL.Tests\bin\Debug\net8.0-windows\test1.ARW";

        [Fact]
        public async Task Debug_FullExtractionAndConversion()
        {
            var magickService = new MagickService();
            var pathResolver = new PathResolverService();
            var cjxlEncoder = new CjxlEncoderService(pathResolver);
            var fileService = new FileService();
            var sizeEstimator = new SizeEstimatorService();
            var imageService = new ImageProcessingService(magickService, cjxlEncoder, fileService, pathResolver, sizeEstimator);

            // Step 1: Extract metadata via MagickService
            var metadata = await magickService.ExtractMetadataProfilesAsync(TestArwPath);
            Console.WriteLine($"=== Extraction Result ===");
            Console.WriteLine($"HasAny: {metadata.HasAny}");
            Console.WriteLine($"ExifPath: {metadata.ExifPath ?? "null"}");
            Console.WriteLine($"XmpPath: {metadata.XmpPath ?? "null"}");
            Console.WriteLine($"IccPath: {metadata.IccPath ?? "null"}");
            Console.WriteLine($"IptcPath: {metadata.IptcPath ?? "null"}");

            if (!string.IsNullOrEmpty(metadata.XmpPath) && File.Exists(metadata.XmpPath))
            {
                var xmpBytes = File.ReadAllBytes(metadata.XmpPath);
                Console.WriteLine($"XMP temp file: {xmpBytes.Length} bytes");
            }
            if (!string.IsNullOrEmpty(metadata.ExifPath) && File.Exists(metadata.ExifPath))
            {
                var exifBytes = File.ReadAllBytes(metadata.ExifPath);
                Console.WriteLine($"EXIF temp file: {exifBytes.Length} bytes");
            }

            // Step 2: Read input metadata with exiftool for verification
            var inputMetadata = ReadMetadataWithExiftool(TestArwPath);
            Console.WriteLine($"\n=== Input Metadata (exiftool) ===");
            foreach (var kvp in inputMetadata)
            {
                Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
            }

            // Step 3: Convert
            var outputPath = Path.Combine(Path.GetDirectoryName(TestArwPath)!, "test1_metadata_verify.jxl");
            if (File.Exists(outputPath)) File.Delete(outputPath);

            await imageService.ConvertArwToJxlAsync(TestArwPath, outputPath, p => { }, 90, System.Threading.CancellationToken.None);

            Console.WriteLine($"\n=== Output File ===");
            Console.WriteLine($"JXL exists: {File.Exists(outputPath)}");
            Console.WriteLine($"JXL size: {new FileInfo(outputPath).Length} bytes");

            // Step 4: Check output metadata via MagickService
            var outputMetadata = await magickService.ExtractMetadataProfilesAsync(outputPath);
            Console.WriteLine($"\n=== Output Metadata (MagickService) ===");
            Console.WriteLine($"HasAny: {outputMetadata.HasAny}");
            Console.WriteLine($"ExifPath: {outputMetadata.ExifPath ?? "null"}");
            Console.WriteLine($"XmpPath: {outputMetadata.XmpPath ?? "null"}");
            Console.WriteLine($"IccPath: {outputMetadata.IccPath ?? "null"}");
            Console.WriteLine($"IptcPath: {outputMetadata.IptcPath ?? "null"}");

            // Step 5: Read output metadata with exiftool for verification
            var outputMetadataExiftool = ReadMetadataWithExiftool(outputPath);
            Console.WriteLine($"\n=== Output Metadata (exiftool) ===");
            foreach (var kvp in outputMetadataExiftool)
            {
                Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
            }

            // Step 6: Verify metadata preservation using exiftool
            Console.WriteLine($"\n=== Metadata Preservation Verification ===");
            var preservedTags = new[]
            {
                "Make", "Model", "Orientation", "XResolution", "YResolution",
                "Software", "DateTimeOriginal", "CreateDate", "TimeOriginal",
                "WhiteBalance", "ExposureTime", "FNumber", "ISO",
                "FocalLength", "LensModel", "Lens", "LensInfo"
            };

            var matchedTags = new List<string>();
            var missingTags = new List<string>();

            foreach (var tag in preservedTags)
            {
                if (inputMetadata.ContainsKey(tag) && outputMetadataExiftool.ContainsKey(tag))
                {
                    var inputVal = inputMetadata[tag];
                    var outputVal = outputMetadataExiftool[tag];
                    if (inputVal == outputVal)
                    {
                        matchedTags.Add($"{tag}={inputVal}");
                        Console.WriteLine($"  PASS: {tag} preserved");
                    }
                    else
                    {
                        Console.WriteLine($"  DIFF: {tag}: '{inputVal}' -> '{outputVal}'");
                    }
                }
                else if (inputMetadata.ContainsKey(tag))
                {
                    missingTags.Add(tag);
                    Console.WriteLine($"  MISSING: {tag} not found in output");
                }
            }

            Console.WriteLine($"\n  Matched: {matchedTags.Count}/{preservedTags.Length}");
            if (missingTags.Count > 0)
            {
                Console.WriteLine($"  Tags in input but not in output: {string.Join(", ", missingTags)}");
            }

            // Step 7: Check raw profiles in output using reflection
            try
            {
                using var outImg = new MagickImage(outputPath);
                var fieldInfo = typeof(MagickImage).GetField("_profiles", BindingFlags.NonPublic | BindingFlags.Instance);
                if (fieldInfo != null)
                {
                    var profiles = fieldInfo.GetValue(outImg) as System.Collections.IEnumerable;
                    Console.WriteLine($"\n=== Raw Output Profiles ===");
                    if (profiles != null)
                    {
                        foreach (var profile in profiles)
                        {
                            var nameProp = profile.GetType().GetProperty("Name");
                            if (nameProp != null)
                            {
                                Console.WriteLine($"  - {nameProp.GetValue(profile)}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not read raw profiles: {ex.Message}");
            }

            // Assertions
            Assert.True(File.Exists(outputPath), "JXL output file should exist");

            Assert.True(matchedTags.Count >= 5,
                $"Expected at least 5 matched tags, got {matchedTags.Count}. Missing: {string.Join(", ", missingTags)}");

            Assert.True(missingTags.Count == 0,
                $"Tags missing from output: {string.Join(", ", missingTags)}");

            Assert.True(outputMetadataExiftool.Count > 0,
                "Output JXL should have metadata tags");
        }

        private static Dictionary<string, string> ReadMetadataWithExiftool(string filePath)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var exiftoolPath = FindExiftool();

            if (string.IsNullOrEmpty(exiftoolPath))
            {
                Console.WriteLine("[Test] exiftool.exe not found - skipping exiftool verification");
                return result;
            }

            try
            {
                var tags = new[] { "Make", "Model", "Orientation", "XResolution", "YResolution",
                    "Software", "DateTimeOriginal", "CreateDate", "TimeOriginal", "WhiteBalance",
                    "ExposureTime", "FNumber", "ISO", "FocalLength", "LensModel", "Lens", "LensInfo" };
                var tagArgs = string.Join(" ", tags.Select(t => $"-{t}"));
                var startInfo = new ProcessStartInfo
                {
                    FileName = exiftoolPath,
                    Arguments = $"-s -n {tagArgs} \"{filePath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    Console.WriteLine("[Test] Failed to start exiftool process");
                    return result;
                }

                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                foreach (var line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var separatorIndex = line.IndexOf(':');
                    if (separatorIndex > 0)
                    {
                        var key = line.Substring(0, separatorIndex).Trim();
                        var value = line.Substring(separatorIndex + 1).Trim();
                        result[key] = value;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Test] exiftool read failed: {ex.Message}");
            }

            return result;
        }

        private static string? FindExiftool()
        {
            // Check common installation locations first (prefer portable/install versions)
            var commonPaths = new[]
            {
                @"C:\Program Files\exiftool.exe",
                @"C:\Program Files (x86)\exiftool.exe",
                @"F:\Downloads\exiftoolgui516\exiftoolgui\exiftool.exe",
                @"C:\Users\Public\exiftool.exe"
            };

            foreach (var path in commonPaths)
            {
                if (File.Exists(path) && IsExiftoolWorking(path)) return path;
            }

            // Fallback to PATH
            var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (var dir in pathEnv.Split(';'))
            {
                var candidate = Path.Combine(dir, "exiftool.exe");
                if (File.Exists(candidate) && IsExiftoolWorking(candidate)) return candidate;
            }

            // Check application directory last (may be non-portable version requiring Perl)
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            if (!string.IsNullOrEmpty(appDir))
            {
                var local = Path.Combine(appDir, "exiftool.exe");
                if (File.Exists(local) && IsExiftoolWorking(local)) return local;
            }

            return null;
        }

        private static bool IsExiftoolWorking(string exiftoolPath)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = exiftoolPath,
                    Arguments = "-ver",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process == null) return false;

                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    Console.WriteLine($"[Test] exiftool version check failed: exit={process.ExitCode}, stderr='{error?.Trim()}'");
                    return false;
                }

                return !string.IsNullOrWhiteSpace(output) && output[0] >= '0' && output[0] <= '9';
            }
            catch
            {
                return false;
            }
        }
    }
}
