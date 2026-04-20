using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARWtoJXL.Core.Models;

namespace ARWtoJXL.Core.Services
{
    /// <summary>
    /// Service for encoding images to JPEG XL format using the cjxl command-line tool.
    /// </summary>
    public class CjxlEncoderService : Interfaces.ICjxlEncoder
    {
        private readonly Interfaces.IPathResolver _pathResolver;
        private const int DefaultTimeoutSeconds = 300; // 5 minutes default timeout

        public CjxlEncoderService(Interfaces.IPathResolver pathResolver)
        {
            _pathResolver = pathResolver ?? throw new ArgumentNullException(nameof(pathResolver));
        }

        /// <summary>
        /// Asynchronously encodes an image to JPEG XL format.
        /// </summary>
        /// <param name="inputPath">Path to the input image file.</param>
        /// <param name="outputPath">Path for the output JPEG XL file.</param>
        /// <param name="quality">Quality level (0-100, where 100 is lossless).</param>
        /// <param name="metadata">Optional metadata profiles to embed.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <param name="timeoutSeconds">Optional timeout in seconds (default: 300).</param>
        /// <exception cref="ArgumentNullException">Thrown when inputPath or outputPath is null or empty.</exception>
        /// <exception cref="ArgumentException">Thrown when inputPath or outputPath is invalid.</exception>
        /// <exception cref="FileNotFoundException">Thrown when input file or cjxl executable is not found.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when quality is outside valid range.</exception>
        /// <exception cref="OperationCanceledException">Thrown when operation is cancelled.</exception>
        /// <exception cref="TimeoutException">Thrown when encoding exceeds the timeout.</exception>
        /// <exception cref="CjxlEncodingException">Thrown when cjxl encoding fails.</exception>
        public async Task EncodeAsync(
            string inputPath,
            string originalArwPath,
            string outputPath,
            int quality,
            MetadataProfiles? metadata = null,
            CancellationToken cancellationToken = default,
            int timeoutSeconds = DefaultTimeoutSeconds,
            Action<double>? progress = null)
        {
            // Validate input parameters
            ValidateInputParameters(inputPath, outputPath, quality);

            // Check for cancellation before starting
            cancellationToken.ThrowIfCancellationRequested();

            // Verify input file exists
            if (!File.Exists(inputPath))
            {
                throw new FileNotFoundException($"Input file not found: {inputPath}", inputPath);
            }

            // Ensure output directory exists
            EnsureOutputDirectoryExists(outputPath);

            // Resolve cjxl executable path
            string cjxlPath = await ResolveCjxlExecutableAsync(cancellationToken);

            // Build encoding arguments
            var args = BuildEncodingArguments(quality, metadata, inputPath, outputPath);

            // Execute the encoding process
            await ExecuteEncodingProcessAsync(cjxlPath, args, cancellationToken, timeoutSeconds, progress);

            // Verify output file was created
            VerifyOutputFile(outputPath);

            // Embed metadata using exiftool (cjxl -x exif does not reliably work)
            if (metadata != null && metadata.HasAny)
            {
                await EmbedMetadataWithExiftoolAsync(originalArwPath, outputPath, metadata, cancellationToken);
            }
        }

        /// <summary>
        /// Validates the input parameters for encoding.
        /// </summary>
        private static void ValidateInputParameters(string inputPath, string outputPath, int quality)
        {
            if (string.IsNullOrWhiteSpace(inputPath))
            {
                throw new ArgumentNullException(nameof(inputPath), "Input path cannot be null or empty.");
            }

            if (string.IsNullOrWhiteSpace(outputPath))
            {
                throw new ArgumentNullException(nameof(outputPath), "Output path cannot be null or empty.");
            }

            if (!Path.IsPathRooted(inputPath) && !inputPath.StartsWith("."))
            {
                // Allow relative paths that start with . or ..
                throw new ArgumentException($"Input path must be a valid file path: {inputPath}", nameof(inputPath));
            }

            if (!Path.IsPathRooted(outputPath) && !outputPath.StartsWith("."))
            {
                throw new ArgumentException($"Output path must be a valid file path: {outputPath}", nameof(outputPath));
            }

            if (quality < 0 || quality > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(quality), "Quality must be between 0 and 100.");
            }
        }

        /// <summary>
        /// Ensures the directory for the output file exists.
        /// </summary>
        private static void EnsureOutputDirectoryExists(string outputPath)
        {
            string? outputDirectory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDirectory) && !Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }
        }

        /// <summary>
        /// Resolves and validates the cjxl executable path.
        /// </summary>
        private async Task<string> ResolveCjxlExecutableAsync(CancellationToken cancellationToken)
        {
            // Allow cancellation check during path resolution
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();

            string cjxlPath = _pathResolver.ResolveCjxlPath();

            if (string.IsNullOrEmpty(cjxlPath))
            {
                throw new FileNotFoundException(
                    "cjxl executable path is empty. Please ensure cjxl.exe is installed alongside the application.",
                    "cjxl.exe");
            }

            if (!File.Exists(cjxlPath))
            {
                throw new FileNotFoundException(
                    $"cjxl executable not found at: {cjxlPath}. Please ensure it is installed alongside the application.",
                    cjxlPath);
            }

            return cjxlPath;
        }

        /// <summary>
        /// Builds the command-line arguments for cjxl encoding.
        /// </summary>
        private static List<string> BuildEncodingArguments(
            int quality,
            MetadataProfiles? metadata,
            string inputPath,
            string outputPath)
        {
            var args = new List<string>(16); // Pre-allocate capacity

            // Calculate encoding parameters
            float distance = QualityCalculator.CalculateDistance(quality);
            int effort = QualityCalculator.CalculateEffort(quality);
            bool isLossless = QualityCalculator.IsLossless(quality);

            // Quality settings
            args.Add(isLossless ? "--distance=0" : $"--distance={distance:F2}");
            args.Add($"--effort={effort}");
            args.Add($"--num_threads={Environment.ProcessorCount}");

            // Container format (embeds metadata)
            args.Add("--container=1");

            // Encoding mode based on quality
            if (isLossless)
            {
                args.Add("--modular=1");
            }
            else
            {
                args.Add("--progressive_dc=1");
            }

            // Add metadata arguments if present
            AddMetadataArguments(args, metadata);

            // Input and output paths (without quotes - ProcessStartInfo handles this)
            args.Add(inputPath);
            args.Add(outputPath);

            return args;
        }

        /// <summary>
        /// Adds metadata-related arguments to the argument list.
        /// </summary>
        private static void AddMetadataArguments(List<string> args, MetadataProfiles? metadata)
        {
            if (metadata is null || !metadata.HasAny)
            {
                Logger.Write($"[CjxlEncoder] No metadata to add");
                return;
            }

            Logger.Write($"[CjxlEncoder] Adding metadata profiles: Exif={metadata.ExifPath ?? "none"}, Xmp={metadata.XmpPath ?? "none"}, Icc={metadata.IccPath ?? "none"}, Iptc={metadata.IptcPath ?? "none"}");

            // Use forward slashes (Windows accepts them) and split -x from value
            // to avoid EscapeArgument quoting. Each arg processed separately by
            // CreateProcess so no quoting/escaping issues.
            void AddMetaArg(string key, string? path)
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
                var size = new FileInfo(path).Length;
                if (size == 0) return;
                // Convert backslashes to forward slashes so EscapeArgument won't quote
                var cleanPath = path.Replace("\\", "/");
                args.Add("-x");
                args.Add($"{key}={cleanPath}");
                Logger.Write($"[CjxlEncoder] Added metadata: -x {key}={cleanPath} ({size} bytes)");
            }

            AddMetaArg("exif", metadata.ExifPath);
            AddMetaArg("xmp", metadata.XmpPath);
            AddMetaArg("icc_pathname", metadata.IccPath);
            AddMetaArg("jumbf", metadata.IptcPath);
        }

        /// <summary>
        /// Executes the cjxl encoding process asynchronously.
        /// </summary>
        private async Task ExecuteEncodingProcessAsync(
            string cjxlPath,
            List<string> args,
            CancellationToken cancellationToken,
            int timeoutSeconds,
            Action<double>? progress)
        {
            var argumentsString = string.Join(" ", args.Select(EscapeArgument));
            
            Logger.Write($"[CjxlEncoder] Full cjxl command: {cjxlPath} {argumentsString}");
            Logger.Write($"[CjxlEncoder] Raw args ({args.Count}): [{string.Join("] [", args)}]");

            var startInfo = new ProcessStartInfo
            {
                FileName = cjxlPath,
                Arguments = argumentsString,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8
            };

            using var process = new Process { StartInfo = startInfo };
            
            // Setup cancellation support
            using var cancellationRegistration = cancellationToken.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill();
                    }
                }
                catch
                {
                    // Ignore cancellation exceptions
                }
            });

            process.Start();
            var startTime = DateTime.UtcNow;
            var maxTime = TimeSpan.FromSeconds(timeoutSeconds);

            // Progress reporter task - runs in background to update progress based on elapsed time
            var progressTask = ReportProgressAsync(process, startTime, maxTime, progress, cancellationToken);

            // Read output asynchronously
            var readOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var readErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            var waitExitTask = process.WaitForExitAsync(cancellationToken);

            // Wait for completion with timeout
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            try
            {
                await waitExitTask.WaitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                if (!process.HasExited)
                {
                    process.Kill();
                }
                throw new TimeoutException(
                    $"cjxl encoding timed out after {timeoutSeconds} seconds. " +
                    "Consider increasing the timeout for large files.");
            }

            // Read the output
            string stdout = await readOutputTask;
            string stderr = await readErrorTask;

            Logger.Write($"cjxl stdout: {stdout}");
            Logger.Write($"cjxl stderr: {stderr}");

            // Check for errors
            if (process.ExitCode != 0)
            {
                string errorMessage = string.IsNullOrWhiteSpace(stderr) 
                    ? "Unknown error occurred during encoding" 
                    : stderr.Trim();
                
                throw new CjxlEncodingException(
                    $"cjxl encoding failed with exit code {process.ExitCode}: {errorMessage}",
                    process.ExitCode);
            }
        }

        /// <summary>
        /// Reports progress during cjxl encoding using time-based estimation.
        /// cjxl v0.11.2 does not output percentage progress, so we estimate based on elapsed time.
        /// </summary>
        private static async Task ReportProgressAsync(
            Process process,
            DateTime startTime,
            TimeSpan maxTime,
            Action<double>? progress,
            CancellationToken cancellationToken)
        {
            if (progress == null) return;

            while (!process.HasExited && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(100, cancellationToken);
                var elapsed = DateTime.UtcNow - startTime;
                var fraction = Math.Min(elapsed.TotalSeconds / maxTime.TotalSeconds, 0.98);
                progress?.Invoke(fraction);
            }
        }

        /// <summary>
        /// Escapes an argument for command-line usage.
        /// </summary>
        private static string EscapeArgument(string argument)
        {
            // Only quote arguments that contain spaces or double quotes
            // Do NOT check for backslashes - Windows accepts forward slashes and backslashes
            // are NOT escape characters for CreateProcess with UseShellExecute=false
            if (argument.Any(c => char.IsWhiteSpace(c) || c == '"'))
            {
                return $"\"{argument.Replace("\"", "\\\"")}\"";
            }
            return argument;
        }

          /// <summary>
        /// Verifies that the output file was created successfully.
        /// </summary>
        private static void VerifyOutputFile(string outputPath)
        {
            if (!File.Exists(outputPath))
            {
                throw new FileNotFoundException(
                    $"Output file was created but is empty: {outputPath}",
                    outputPath);
            }

            // Additional check: verify file has content
            long fileSize = new FileInfo(outputPath).Length;
            if (fileSize == 0)
            {
                throw new IOException($"Output file was created but is empty: {outputPath}");
            }
        }

         /// <summary>
        /// Embeds metadata into the output JXL file using exiftool.
        /// cjxl's -x exif argument does not reliably embed metadata, so we use exiftool as a post-processing step.
        /// Reads metadata directly from the original ARW file (not from temp EXIF bytes).
        /// </summary>
        private async Task EmbedMetadataWithExiftoolAsync(
            string inputPath,
            string outputPath,
            Models.MetadataProfiles metadata,
            CancellationToken cancellationToken)
        {
            await Task.Yield();

            string? exiftoolPath = null;

            var commonPaths = new[]
            {
                @"C:\Program Files\exiftool.exe",
                @"C:\Program Files (x86)\exiftool.exe",
                @"F:\Downloads\exiftoolgui516\exiftoolgui\exiftool.exe",
                @"C:\Users\Public\exiftool.exe"
            };
            foreach (var path in commonPaths)
            {
                if (File.Exists(path)) { exiftoolPath = path; break; }
            }
            if (string.IsNullOrEmpty(exiftoolPath))
            {
                var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
                foreach (var dir in pathEnv.Split(';'))
                {
                    var candidate = Path.Combine(dir, "exiftool.exe");
                    if (File.Exists(candidate)) { exiftoolPath = candidate; break; }
                }
            }
            if (string.IsNullOrEmpty(exiftoolPath))
            {
                var appDir = AppDomain.CurrentDomain.BaseDirectory;
                if (!string.IsNullOrEmpty(appDir))
                {
                    var local = Path.Combine(appDir, "exiftool.exe");
                    if (File.Exists(local)) exiftoolPath = local;
                }
            }

            if (string.IsNullOrEmpty(exiftoolPath))
            {
                Logger.Write("[CjxlEncoder] exiftool.exe not found - skipping metadata embedding");
                return;
            }

            if (!IsExiftoolWorking(exiftoolPath))
            {
                Logger.Write($"[CjxlEncoder] exiftool at {exiftoolPath} failed version check - skipping metadata embedding");
                return;
            }

            var exiftoolArgs = new System.Text.StringBuilder();

            // Use exiftool to copy metadata directly from the source ARW file
            exiftoolArgs.Append($"-tagsFromFile \"{inputPath}\" ");

            if (!string.IsNullOrEmpty(metadata.ExifPath) && File.Exists(metadata.ExifPath))
            {
                exiftoolArgs.Append("-exif:all ");
                Logger.Write($"[CjxlEncoder] Will embed EXIF from source: {inputPath}");
            }
            if (!string.IsNullOrEmpty(metadata.XmpPath) && File.Exists(metadata.XmpPath))
            {
                exiftoolArgs.Append("-xmp:all ");
                Logger.Write($"[CjxlEncoder] Will embed XMP from source: {inputPath}");
            }
            if (!string.IsNullOrEmpty(metadata.IccPath) && File.Exists(metadata.IccPath))
            {
                exiftoolArgs.Append("-icc-profile ");
                Logger.Write($"[CjxlEncoder] Will embed ICC from source: {inputPath}");
            }

            if (exiftoolArgs.ToString().Trim() == $"-tagsFromFile \"{inputPath}\" ")
            {
                Logger.Write("[CjxlEncoder] No metadata to embed");
                return;
            }

            exiftoolArgs.Append($"-overwrite_original \"{outputPath}\"");

            Logger.Write($"[CjxlEncoder] exiftool command: {exiftoolPath} {exiftoolArgs}");

            var startInfo = new ProcessStartInfo
            {
                FileName = exiftoolPath,
                Arguments = exiftoolArgs.ToString(),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                Logger.Write("[CjxlEncoder] Failed to start exiftool process for metadata embedding");
                return;
            }

            var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            Logger.Write($"[CjxlEncoder] exiftool metadata embedding exit={process.ExitCode}, stdout='{stdout?.Trim()}', stderr='{stderr?.Trim()}'");
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

                var success = process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output) && output[0] switch
                {
                    >= '0' and <= '9' => true,
                    _ => false
                };

                if (!success)
                {
                    Logger.Write($"[CjxlEncoder] exiftool version check failed: exit={process.ExitCode}, stdout='{output?.Trim()}', stderr='{error?.Trim()}'");
                }
                else
                {
                    Logger.Write($"[CjxlEncoder] exiftool version check passed: {output?.Trim()}");
                }

                return success;
            }
            catch (Exception ex)
            {
                Logger.Write($"[CjxlEncoder] exiftool version check threw: {ex.Message}");
                return false;
            }
         }
    }

    /// <summary>
    /// Exception thrown when cjxl encoding fails.
    /// </summary>
    public class CjxlEncodingException : Exception
    {
        /// <summary>
        /// The exit code from the cjxl process.
        /// </summary>
        public int ExitCode { get; }

        public CjxlEncodingException(string message) : base(message)
        {
        }

        public CjxlEncodingException(string message, int exitCode) : base(message)
        {
            ExitCode = exitCode;
        }

        public CjxlEncodingException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public CjxlEncodingException(string message, int exitCode, Exception innerException) : base(message, innerException)
        {
            ExitCode = exitCode;
        }
    }
}
