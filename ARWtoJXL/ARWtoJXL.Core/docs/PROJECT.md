# ARWtoJXL.Core

Business logic layer implementing the ARW conversion pipeline with clean architecture and dependency injection.

## Project Structure

```
ARWtoJXL.Core/
├── Interfaces/
│   ├── IImageService.cs           # Primary service interface (defines ImageStatus, OutputFormat enums)
│   ├── IImageConverterService.cs  # ImageMagick operations interface (RAW conversion, thumbnails, metadata extraction)
│   ├── ICjxlEncoder.cs            # cjxl CLI encoder interface
│   ├── IFileService.cs            # File system operations interface
│   ├── IPathResolver.cs           # Path resolution interface
│   ├── IExiftoolService.cs        # Metadata operations interface (EXIF extraction, metadata embedding)
│   ├── ILogger.cs                 # Logging interface (replaces static Logger)
│   └── IProcessRunner.cs          # Process execution interface (replaces static ProcessHelper)
├── Models/
│   ├── FileLockedException.cs     # Custom exception for file-lock errors (IOException wrapper)
│   ├── MetadataProfiles.cs        # Metadata container (EXIF, XMP, ICC, IPTC profiles) with disposable temp file cleanup and logging
│   └── QualityCalculator.cs       # Static helper for quality→distance/effort mapping
├── Services/
│   ├── ImageProcessingService.cs  # Main service orchestrating conversion pipeline
│   ├── ImageConverterService.cs   # Magick.NET implementation (thumbnails, RAW extraction, metadata)
│   ├── CjxlEncoderService.cs      # cjxl CLI wrapper implementation
│   ├── ExiftoolService.cs         # exiftool operations (EXIF extraction, metadata embedding)
│   ├── SystemProcessRunner.cs     # IProcessRunner implementation (exiftool path resolution, version check, process execution)
│   ├── FileService.cs             # File system operations implementation
│   ├── PathResolverService.cs     # Path resolution implementation
│   ├── FileLogger.cs              # ILogger implementation (file-based logger)
│   └── ServiceCollectionExtensions.cs # IServiceCollection extension for AddCoreServices()
└── docs/
    └── PROJECT.md                 # This file
```

## Architecture Pattern

**Dependency Injection (DI)** — all services depend on abstractions (interfaces), not concrete implementations. Registered via `ServiceCollectionExtensions.AddCoreServices()` which configures all 8 services as singletons.

**DI Registration Order** (resolved via `ServiceProvider`):
    ```
    ILogger → FileLogger (singleton)
    IProcessRunner → SystemProcessRunner (depends on ILogger)
    IFileService → FileService (depends on ILogger)
    IPathResolver → PathResolverService (no deps)
    IExiftoolService → ExiftoolService (depends on IProcessRunner, IFileService, ILogger)
    IImageConverterService → ImageConverterService (depends on IExiftoolService, IFileService, ILogger)
    ICjxlEncoder → CjxlEncoderService (depends on IPathResolver, IExiftoolService, ILogger, IProcessRunner)
    IImageService → ImageProcessingService (depends on IImageConverterService, ICjxlEncoder, IFileService, IPathResolver, ILogger, IExiftoolService)
    ```

## Services & Responsibilities

### IImageService (Primary Interface)
Defines two async operations:
- `GetThumbnailAsync(filePath, cancellationToken)` → `byte[]`: Extracts thumbnail from ARW/JXL using Magick.NET (300x300 JPG)
- `ConvertArwToJxlAsync(inputPath, outputPath, progress, quality, outputFormat, cancellationToken, skipMetadata, effort)`: Orchestrates conversion pipeline
  - `progress` is a required `Action<double>` callback (non-nullable, no default). Fault-tolerant: exceptions from the callback are caught and logged, preventing pipeline breakage and orphaned temp files.
  - `outputFormat = OutputFormat.Jxl`: Two-stage direct PPM streaming to cjxl stdin via `StreamPpmToAsync` + writer delegate — zero intermediate disk I/O, single file open, native ImageMagick C-code encoding
  - `outputFormat = OutputFormat.Jpeg`: ARW→JPEG via IImageConverterService with quality setting + exiftool metadata embedding
  - `outputFormat = OutputFormat.Png`: Direct ARW→PNG via Magick.NET (16-bit lossless)
  - `skipMetadata`: When true, skips EXIF extraction and metadata embedding for faster conversion
   - `effort`: Optional cjxl encoding effort override (1-9)
   - `threads`: Optional cjxl thread count override. Null uses `Environment.ProcessorCount`
   - All formats support optional metadata embedding via exiftool post-processing

Also defines two enums in the same file:
- **ImageStatus**: `Pending`, `Ready`, `Converting`, `Converted`, `Failed`
- **OutputFormat**: `Jxl`, `Jpeg`, `Png`

### ImageProcessingService (Orchestrator)
Coordinates the conversion pipeline by delegating to specialized services. Supports three output formats:
1. **JXL (default)**: Two-stage via `ImageConverterService.StreamPpmToAsync` (direct PPM stream to cjxl stdin) + `CjxlEncoderService.EncodeFromStreamAsync` with writer delegate — zero intermediate buffering, single file open
2. **JPEG**: Delegates to `IImageConverterService.ConvertToJpegAsync()` for ARW→JPEG + exiftool metadata embedding
3. **PNG**: Direct ARW→PNG via ImageConverterService (16-bit lossless) + exiftool metadata embedding

**Constructor Injection:**
```csharp
public ImageProcessingService(
    IImageConverterService imageConverterService,
    ICjxlEncoder cjxlEncoder,
    IFileService fileService,
    IPathResolver pathResolver,
    ILogger logger,
    IExiftoolService exiftoolService)
```

### IImageConverterService / ImageConverterService
- `ExtractThumbnailAsync()`: Resizes image to 300x300, outputs JPEG
- `ConvertToPngAsync()`: Converts ARW to 16-bit PNG in temp directory
- `ConvertToJpegAsync()`: Converts ARW to JPEG with quality setting, creates output directory if needed
- `ExtractToRawRgb16Async()`: Extracts raw 16-bit RGB pixel data from ARW file as byte array (big-endian, 2 bytes per channel) — legacy, superseded by `StreamPpmToAsync`
- `StreamPpmToAsync()`: Opens ARW, sets Depth=16/ColorSpace=sRGB/Format=Ppm, writes PPM directly to output stream via Magick.NET native `Write()` — single file open, zero intermediate buffering, streams directly to cjxl stdin
- `ExtractMetadataProfilesAsync()`: Extracts EXIF, XMP, ICC, IPTC profiles to temp files for cjxl — fully async, no sync-over-async blocking
  - **ARW files:** Awaits `IExiftoolService.ExtractExifAsync()` for EXIF extraction (Magick.NET cannot reliably read EXIF from Sony ARW files). Much faster (~1s) than the previous Magick.NET fallback chain (~10s). Exceptions propagate to caller.
  - **Non-ARW files:** Offloads Magick.NET profile extraction to `Task.Run` (CPU-bound). Magick.NET failures are caught and logged, then exiftool fallback is attempted for EXIF. Other exceptions propagate to caller.
  - **Helper:** `ExtractProfilesFromImageAsync()` runs Magick.NET profile extraction on a thread-pool thread via `Task.Run`.
- **Helper:** `GetProfileBytes()` extracts raw bytes from a Magick.NET profile via the stable `IImageProfile.ToByteArray()` public API.
- **Constructor:** `ImageConverterService(IExiftoolService exiftoolService, IFileService fileService, ILogger logger)` — all required (non-nullable)

### IExiftoolService / ExiftoolService
- `ExtractExifAsync(filePath)`: Extracts raw EXIF bytes from ARW/JXL files using exiftool, saves to temp file, returns the file path (`string?`)
- `EmbedMetadataAsync(sourcePath, outputPath, metadata)`: Embeds EXIF, XMP, ICC metadata into output file using exiftool's `-tagsFromFile` — copies all available metadata types (EXIF, XMP, ICC) directly from source regardless of which profiles were extracted
- Uses `IProcessRunner.FindExiftoolAsync()` for path resolution (fully async, no thread-pool blocking)
- **Constructor:** `ExiftoolService(IProcessRunner processRunner, IFileService fileService, ILogger logger)`

### ICjxlEncoder / CjxlEncoderService
- `EncodeAsync(inputPath, originalArwPath, outputPath, quality, metadata, cancellationToken, timeoutSeconds, progress, effort)`: Invokes cjxl.exe with quality-based parameters
  - `inputPath`: Path to the PNG input (from Magick.NET conversion)
  - `originalArwPath`: Path to the original ARW file (used for exiftool metadata embedding)
  - `progress`: Optional `Action<double>?` callback (0.0→1.0 relative to cjxl stage only)
  - `timeoutSeconds`: Default 300 (`DefaultTimeoutSeconds` constant)
  - `effort`: Optional effort override (1-9). Null uses auto based on quality
- `threads`: Optional thread count override. Null uses `Environment.ProcessorCount`
- `EncodeFromStreamAsync(Stream inputStream, ...)`: Encodes from a PPM stream piped to cjxl stdin via `IProcessRunner.RunProcessWithStdinAsync`
- `EncodeFromStreamAsync(inputPath, originalArwPath, outputPath, quality, metadata, ppmWriter, cancellationToken, timeoutSeconds, progress, effort)`: Encodes from a PPM writer delegate — `ppmWriter(Stream, CancellationToken)` writes PPM directly to cjxl stdin via `ExecuteEncodingProcessWithWriterAsync`, zero intermediate buffering. All parameters except `progress` and `effort` are required (no defaults).
- Uses `QualityCalculator` for distance/effort mapping when overrides not provided
- Handles lossless (quality≥100) and lossy modes
- **cjxl progress estimation:** cjxl v0.11.2 does not output percentage progress during encoding. A background task (`ReportProgressAsync`) reports linear progress from 0.0 to 0.98 during cjxl encoding (updated every 100ms), mapped to 0.5→1.0 in the overall pipeline. Progress invocations are wrapped in try-catch so a failing callback does not abort encoding.
- **Metadata embedding:** Delegates to `IExiftoolService.EmbedMetadataAsync()` for post-encoding metadata embedding via exiftool.
- **BuildEncodingArguments:** `protected internal` method for constructing cjxl CLI arguments. Accepts optional effort override and raw distance. Testable via subclass in test project (covered by `CjxlEncoderArgumentsTests`).
- **BuildStreamEncodingArguments:** `protected internal` method for constructing cjxl CLI arguments for stdin pipe encoding (input arg is `-`).
- **Safe stream reading:** `ExecuteEncodingProcessWithWriterAsync` uses `SafeReadStreamAsync` for stdout/stderr capture — bounded 5s read timeout per stream, catches `OperationCanceledException` and `IOException` (broken pipe on process kill), returns partial output instead of hanging. On cancellation/timeout, a separate 2s drain window captures any remaining output from the killed process; drain errors catch `OperationCanceledException` and `IOException` silently, log unexpected exceptions.
- **Constructor:** `CjxlEncoderService(IPathResolver pathResolver, IExiftoolService exiftoolService, ILogger logger, IProcessRunner processRunner)` — all required (non-nullable)

### CjxlEncodingException
Custom exception thrown by `CjxlEncoderService` when cjxl encoding fails. Properties:
- `ExitCode`: The cjxl process exit code
- Four constructor overloads: message-only, message+exitCode, message+innerException, message+exitCode+innerException

### IFileService / FileService
- `DeleteFile()`: Safe file deletion with exception logging via ILogger
- `FileExists()`: File existence check
- `GetFileSize()`: Returns file size in bytes (`long`)
- `CombinePaths()`: Path concatenation
- `GetTempFileName()`: Generates unique temp PNG path
- `SaveBytesToTemp()`: Writes byte array to a uniquely-named temp file, returns path or null on failure
- **Constructor:** `FileService(ILogger logger)` — depends on ILogger for exception logging

### IPathResolver / PathResolverService
- `ResolveCjxlPath()`: Searches app directory, then executable directory, falls back to PATH
- `GetTempPath()`: Returns system temp directory

### IProcessRunner / SystemProcessRunner
Interface + implementation for process execution (replaces static `ProcessHelper`):
- `FindExiftoolAsync(logPrefix)`: Searches `CommonExiftoolPaths` (3 hardcoded paths: `C:\Program Files\`, `C:\Program Files (x86)\`, `C:\Users\Public\`), then PATH, then app directory for exiftool.exe — fully async via `IsExiftoolWorkingAsync`
- `IsExiftoolWorkingAsync(exiftoolPath, logPrefix)`: Runs `exiftool -ver` via `RunProcessAsync` to verify functionality — no synchronous `Process.WaitForExit()` blocking
- `RunProcessAsync(fileName, arguments, cancellationToken)`: Generic async process launcher with stdout/stderr capture and cancellation support
- `RunProcessBinaryAsync(fileName, arguments, cancellationToken)`: Async process runner returning raw binary stdout; respects CancellationToken via process kill on cancellation
- `RunProcessWithTimeoutAsync(fileName, arguments, timeoutSeconds, cancellationToken)`: Runs process with timeout, returns `(ExitCode, Stdout, Stderr, TimedOut)`. Kills orphan process on timeout via linked `CancellationTokenSource`. Post-timeout stdout/stderr drain catches `OperationCanceledException` and `IOException` silently; unexpected exceptions are logged. Injected into `CjxlEncoderService` for cjxl encoding with timeout protection.
- `RunProcessWithStdinAsync(fileName, arguments, stdinStream, timeoutSeconds, cancellationToken)`: Runs process with a Stream piped to stdin. Post-timeout drain uses same exception handling as `RunProcessWithTimeoutAsync`. Used by `CjxlEncoderService.ExecuteEncodingProcessFromStreamAsync` for PPM→JXL encoding without intermediate files.
- Injected into `ExiftoolService` for testability

### ILogger / FileLogger
Interface + implementation for logging (replaces static `Logger`):
- `Write(string message)`: Appends timestamped message to temp file (`%TEMP%\ARWtoJXL.log`); falls back to Console.Error on write failure. Thread-safe via `lock`.
- `Clear()`: Deletes the log file; falls back to Console.Error on delete failure. Thread-safe via `lock`.
- **Constructor:** `FileLogger(string? logPath = null)` — optional `logPath` parameter; defaults to `%TEMP%\ARWtoJXL.log`
- Injected into all services that need logging for testability

### QualityCalculator (Static Helper)
Centralized quality calculations to avoid duplication:
- `CalculateDistance(int quality)`: Maps 0-100 quality to Butteraugli distance (0.0-25.0)
  - quality ≥ 100 → distance 0.0 (lossless)
  - quality ≥ 30 → linear: `0.1f + (100 - quality) * 0.09f`
  - quality < 30 → quadratic: `53.0f/3000.0f * q² - 23.0f/20.0f * q + 25.0f`
- `CalculateEffort(int quality)`: Maps quality to encoding effort (5-9)
  - ≥95 → 9, ≥85 → 8, ≥70 → 7, ≥50 → 6, else → 5
- `IsLossless(int quality)`: Returns true if quality ≥ 100

**Quality→Distance mapping:** quality 100 → distance 0.0 (lossless), quality 90 → distance 1.0 (visually lossless), quality 68-96 recommended

## Conversion Pipeline Details

**JXL Pipeline (Two-stage process):**
1. **Stage 0 (Magick.NET + exiftool):** Extract metadata profiles (EXIF, XMP, ICC, IPTC) to temp files (~0-10% progress)
2. **Stage 1 (Magick.NET + cjxl stdin pipe):** `StreamPpmToAsync` opens ARW once, configures `Format=Ppm`, `Depth=16`, `ColorSpace=sRGB`, then streams PPM directly to cjxl stdin via native `image.Write()` — zero intermediate buffering, single file open, native C-code encoding (~10-98% progress)

**Old pipeline (replaced):** 2x file opens, ~67M pixel iterations in C# (`ExtractToRawRgb16Async`), manual PPM header construction (`BuildPpmStream`), ~800MB RAM for 24MP images.
**New pipeline:** 1x file open, native ImageMagick C-code PPM encoding via `StreamPpmToAsync`, delegates PPM write to `CjxlEncoderService.ExecuteEncodingProcessWithWriterAsync`, ~4MB RAM overhead.

**JPEG Pipeline (Two-stage process):**
1. **Stage 0 (Magick.NET):** Extract metadata profiles
2. **Stage 1 (Magick.NET via IImageConverterService):** ARW → JPEG with quality setting
3. **Post-processing:** exiftool metadata embedding

**PNG Pipeline (Two-stage process):**
1. **Stage 0 (Magick.NET):** Extract metadata profiles
2. **Stage 1 (Magick.NET):** ARW → PNG (16-bit lossless)
3. **Post-processing:** exiftool metadata embedding

**cjxl arguments:**
  - Lossless: `--distance=0.0 --effort={1-9} --num_threads={threads} --container=1 --modular=1`
- Lossy: `--distance={0.1-150.0} --effort={1-9} --num_threads={threads} --container=1 --progressive_dc=1`
- Metadata: `-x exif={path}`, `-x xmp={path}`, `-x icc_pathname={path}`, `-x jumbf={path}` (when available)
- Stdin pipe: Input arg is `-` instead of file path

**Progress tracking:** 0.1 (metadata) → 0.3 (PPM streaming) → 0.35→0.98 smooth (cjxl encoding via time-based estimation) → 1.0 (JXL complete)

**Metadata handling:**
- **EXIF extraction (ARW):** `IExiftoolService.ExtractExifAsync()` uses exiftool (`-b -exif:all`) to extract raw EXIF bytes from source ARW, saves to temp file, returns file path (~1s). Non-ARW files use Magick.NET first.
- **XMP/ICC/IPTC:** Extracted via Magick.NET profile lookup (`GetProfile("XMP")`, `GetProfile("ICC ")`, `GetIptcProfile()`).
- **Metadata embedding:** cjxl's `-x exif` argument does not reliably embed metadata (v0.11.2 known issue). Post-encoding, `IExiftoolService.EmbedMetadataAsync()` uses exiftool `-tagsFromFile source.arw -exif:all -xmp:all -icc-profile -overwrite_original output.jxl` to copy all available metadata (EXIF, XMP, ICC) from the original ARW to the output file.
- **Skip metadata:** When `skipMetadata` is true, both metadata extraction and embedding are skipped entirely, improving conversion speed.
- **Path resolution:** `IProcessRunner.FindExiftoolAsync()` centralizes exiftool.exe discovery (common paths → PATH → app directory) — fully async.
- Metadata temp files kept alive during encoding (disposed AFTER `EncodeAsync` completes in finally block).
- Auto-cleanup via `MetadataProfiles.Dispose()` after encoding completes.

## File Lock Handling

When an ARW file is locked by another application (Adobe Bridge, Lightroom, etc.), file access throws `IOException` with HResult 32 on Windows:

- **FileLockedException** (`ARWtoJXL.Core.Models`): Custom exception extending `IOException` with user-friendly message identifying the locked file and suggesting closing it in the other app. Exposes `FilePath` property. Two constructors: `(filePath)` and `(filePath, innerException)`.
- **Detection**: `FileLockedException.IsFileLocked()` checks HResult 32, inner IOException, and message patterns ("process cannot access the file", "being used by another process")
- **ImageConverterService**: Catches IOException in `ExtractThumbnailAsync`, `ConvertToPngAsync`, and `ExtractProfilesFromImageAsync`, rethrows as `FileLockedException`
- **ExiftoolService**: Checks stderr for file-lock indicators in `EmbedMetadataAsync` (ex: "cannot open", "permission denied")

## Concurrency Model

- `SemaphoreSlim(maxConcurrency = Environment.ProcessorCount)` limits parallel conversions
- Progress tracking via `Interlocked.Increment()` on completed count
- `CancellationTokenSource` for graceful cancellation (checked at semaphore wait and passed to services)
- `OperationCanceledException` caught to mark items as Pending with "Cancelled" error

## Key Dependencies

- **Magick.NET-Q16-AnyCPU** (14.13.0): RAW image decoding, thumbnail extraction (Apache-2.0)
- **Microsoft.Extensions.DependencyInjection.Abstractions** (8.0.1): DI abstractions (MIT)
- **cjxl.exe** (downloaded at build): JPEG-XL encoder from libjxl v0.11.2 (BSD-3-Clause)
- **exiftool.exe** + **exiftool_files/** (bundled): Metadata extraction/embedding — exiftool v13.57 requires companion Perl runtime DLLs from `exiftool_files/` (perl532.dll, etc.) to function. Both `exiftool.exe` and the entire `exiftool_files/` directory are copied to output in all projects (Core, Avalonia, Tests).

## Additional Notes

- `InternalsVisibleTo` is set for `ARWtoJXL.Tests` to allow test access to `protected internal` members (e.g., `BuildEncodingArguments`, `BuildStreamEncodingArguments`).

# Web Fetch Docs
cjxl start commands params info is at @[here](https://man.archlinux.org/man/cjxl.1.en.txt)
