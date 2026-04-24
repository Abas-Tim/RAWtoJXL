# ARWtoJXL.Core

Business logic layer implementing the ARW conversion pipeline with clean architecture and dependency injection.

## Project Structure

```
ARWtoJXL.Core/
├── Models/
│   ├── FileLockedException.cs     # Custom exception for file-lock errors (IOException wrapper)
│   ├── QualityCalculator.cs       # Static helper for quality→distance/effort mapping
│   └── MetadataProfiles.cs        # Metadata container (EXIF, XMP, ICC, IPTC profiles)
├── Interfaces/
│   ├── IImageService.cs           # Primary service interface (enum: ImageStatus)
│   ├── IMagickService.cs          # ImageMagick operations interface
│   ├── ICjxlEncoder.cs            # cjxl CLI encoder interface
│   ├── IFileService.cs            # File system operations interface
│   ├── IPathResolver.cs           # Path resolution interface
│   ├── IExiftoolService.cs        # Metadata operations interface (EXIF extraction, metadata embedding)
│   ├── ILogger.cs                 # Logging interface (replaces static Logger)
│   ├── IProcessRunner.cs          # Process execution interface (replaces static ProcessHelper)
│   └── IPngCache.cs              # PNG intermediate cache interface (disk-based, hash-keyed, LRU eviction)
└── Services/
    ├── ImageProcessingService.cs  # Main service orchestrating conversion pipeline
    ├── MagickService.cs           # Magick.NET implementation (thumbnails, PNG conversion)
    ├── CjxlEncoderService.cs      # cjxl CLI wrapper implementation
    ├── ExiftoolService.cs         # exiftool operations (EXIF extraction, metadata embedding)
    ├── SystemProcessRunner.cs     # IProcessRunner implementation (exiftool path resolution, version check, process execution)
    ├── FileService.cs             # File system operations implementation
    ├── PathResolverService.cs     # Path resolution implementation
    ├── FileLogger.cs              # ILogger implementation (file-based logger)
    ├── PngCache.cs              # IPngCache implementation (disk-based, SHA256-keyed, LRU eviction at 2GB)
    └── ServiceCollectionExtensions.cs # IServiceCollection extension for AddCoreServices()
```

## Architecture Pattern

**Dependency Injection (DI)** — all services depend on abstractions (interfaces), not concrete implementations. Registered via `ServiceCollectionExtensions.AddCoreServices()` which configures all 9 services as singletons.

**DI Registration Order** (resolved via `ServiceProvider`):
    ```
    ILogger → FileLogger (singleton)
    IProcessRunner → SystemProcessRunner (depends on ILogger)
    IFileService → FileService (depends on ILogger)
    IPathResolver → PathResolverService (no deps)
    IExiftoolService → ExiftoolService (depends on IProcessRunner, IFileService, ILogger)
    IMagickService → MagickService (depends on IExiftoolService, IFileService, ILogger)
    ICjxlEncoder → CjxlEncoderService (depends on IPathResolver, IExiftoolService, ILogger, IProcessRunner)
    IPngCache → PngCache (IDisposable, depends on ILogger)
    IImageService → ImageProcessingService (depends on IMagickService, ICjxlEncoder, IFileService, IPathResolver, ILogger, IExiftoolService, IPngCache)
    ```

## Services & Responsibilities

### IImageService (Primary Interface)
Defines two async operations:
- `GetThumbnailAsync(filePath)` → byte[]: Extracts thumbnail from ARW/JXL using Magick.NET (300x300 JPG)
- `ConvertArwToJxlAsync(inputPath, outputPath, progressCallback, quality, outputFormat, cancellationToken)`: Orchestrates conversion pipeline
  - `outputFormat = OutputFormat.Jxl`: Two-stage PNG→JXL via cjxl (default), uses PNG cache to skip ARW→PNG on re-conversion
  - `outputFormat = OutputFormat.Jpeg`: ARW→JPEG via IMagickService with quality setting + exiftool metadata embedding
  - `outputFormat = OutputFormat.Png`: Direct ARW→PNG via Magick.NET (16-bit lossless)
  - All formats support metadata embedding via exiftool post-processing

### ImageProcessingService (Orchestrator)
Coordinates the conversion pipeline by delegating to specialized services. Supports three output formats:
1. **JXL (default)**: Two-stage via MagickService (ARW→PNG) + CjxlEncoderService (PNG→JXL)
2. **JPEG**: Delegates to `IMagickService.ConvertToJpegAsync()` for ARW→JPEG + exiftool metadata embedding
3. **PNG**: Direct ARW→PNG via MagickService (16-bit lossless) + exiftool metadata embedding

**Constructor Injection:**
```csharp
public ImageProcessingService(
    IMagickService magickService,
    ICjxlEncoder cjxlEncoder,
    IFileService fileService,
    IPathResolver pathResolver,
    ILogger logger,
    IExiftoolService exiftoolService,
    IPngCache pngCache)
```

### IMagickService / MagickService
- `ExtractThumbnailAsync()`: Resizes image to 300x300, outputs JPEG
- `ConvertToPngAsync()`: Converts ARW to 16-bit PNG in temp directory
- `ConvertToJpegAsync()`: Converts ARW to JPEG with quality setting, creates output directory if needed
- `ExtractMetadataProfilesAsync()`: Extracts EXIF, XMP, ICC, IPTC profiles to temp files for cjxl — fully async, no sync-over-async blocking
  - **ARW files:** Awaits `IExiftoolService.ExtractExifAsync()` for EXIF extraction (Magick.NET cannot reliably read EXIF from Sony ARW files). Much faster (~1s) than the previous Magick.NET fallback chain (~10s).
  - **Non-ARW files:** Offloads Magick.NET profile extraction to `Task.Run` (CPU-bound), then awaits exiftool fallback for JXL files.
  - **Helper:** `ExtractProfilesFromImageAsync()` runs Magick.NET profile extraction on a thread-pool thread via `Task.Run`.
- **Constructor:** `MagickService(IExiftoolService exiftoolService, IFileService fileService, ILogger logger)` — all required (non-nullable)

### IExiftoolService / ExiftoolService
- `ExtractExifAsync(filePath)`: Extracts raw EXIF bytes from ARW/JXL files using exiftool
- `EmbedMetadataAsync(sourcePath, outputPath, metadata)`: Embeds EXIF, XMP, ICC metadata into output file using exiftool's `-tagsFromFile` — copies all available metadata types (EXIF, XMP, ICC) directly from source regardless of which profiles were extracted
- Uses `IProcessRunner.FindExiftool()` for path resolution
- **Constructor:** `ExiftoolService(IProcessRunner processRunner, IFileService fileService, ILogger logger)`

### ICjxlEncoder / CjxlEncoderService
- `EncodeAsync(inputPath, originalArwPath, outputPath, quality, metadata, cancellationToken, timeoutSeconds, progress)`: Invokes cjxl.exe with quality-based parameters
  - `inputPath`: Path to the PNG input (from Magick.NET conversion)
  - `originalArwPath`: Path to the original ARW file (used for exiftool metadata embedding)
  - `progress`: Optional `Action<double>` callback (0.0→1.0 relative to cjxl stage only)
- Uses `QualityCalculator` for distance/effort mapping
- Handles both lossless (quality≥100) and lossy modes
- **cjxl progress estimation:** cjxl v0.11.2 does not output percentage progress during encoding. A background task (`ReportProgressAsync`) reports linear progress from 0.0 to 0.98 during cjxl encoding (updated every 100ms), mapped to 0.5→1.0 in the overall pipeline.
- **Metadata embedding:** Delegates to `IExiftoolService.EmbedMetadataAsync()` for post-encoding metadata embedding via exiftool.
- **BuildEncodingArguments:** `protected internal` method for constructing cjxl CLI arguments. Testable via subclass in test project (covered by `CjxlEncoderArgumentsTests`).
- **Constructor:** `CjxlEncoderService(IPathResolver pathResolver, IExiftoolService exiftoolService, ILogger logger, IProcessRunner processRunner)` — all required (non-nullable)

### IFileService / FileService
    - `DeleteFile()`: Safe file deletion with exception logging via ILogger
    - `FileExists()`: File existence check
    - `CombinePaths()`: Path concatenation
    - `GetTempFileName()`: Generates unique temp PNG path
    - `SaveBytesToTemp()`: Writes byte array to a uniquely-named temp file, returns path or null on failure
    - **Constructor:** `FileService(ILogger logger)` — depends on ILogger for exception logging

### IPngCache / PngCache (IDisposable)
- `GetCachedPng(inputPath)`: Returns cached PNG path for a given input ARW file, or null if not cached
- `StorePng(inputPath, pngPath)`: Stores a PNG in the cache keyed by input file hash
- `EvictIfNeeded(newFileSize)`: Evicts oldest entries (by last access time) when cache exceeds 2GB limit
- `Dispose()`: Clears all cached PNG files and removes cache directory on app exit
- **Cache location:** `%TEMP%\ARWtoJXL\png_cache\` with SHA256 hash-based filenames
- **Hash input:** Combination of file path, last write time, and file size — invalidates cache on file modification
- **Memory-efficient:** Only file paths and sizes tracked in memory; actual PNGs stored on disk
- **LRU eviction:** Evicts to 50% capacity before adding new entry, based on last access time
- **Exception logging:** All file operations (index rebuild, store, eviction, disposal) log failures via ILogger

### IPathResolver / PathResolverService
- `ResolveCjxlPath()`: Searches app directory, then executable directory, falls back to PATH
- `GetTempPath()`: Returns system temp directory

### IProcessRunner / SystemProcessRunner
Interface + implementation for process execution (replaces static `ProcessHelper`):
- `FindExiftool(logPrefix)`: Searches common paths, PATH, and app directory for exiftool.exe
- `IsExiftoolWorking(exiftoolPath, logPrefix)`: Runs `exiftool -ver` to verify functionality
- `RunProcessAsync(fileName, arguments, cancellationToken)`: Generic async process launcher with stdout/stderr capture and cancellation support
- `RunProcessBinaryAsync(fileName, arguments, cancellationToken)`: Async process runner returning raw binary stdout; respects CancellationToken via process kill on cancellation
- `RunProcessWithTimeoutAsync(fileName, arguments, timeoutSeconds, cancellationToken)`: Runs process with timeout, returns `(ExitCode, Stdout, Stderr, TimedOut)`. Kills orphan process on timeout via linked `CancellationTokenSource`. Injected into `CjxlEncoderService` for cjxl encoding with timeout protection.
- Injected into `ExiftoolService` for testability

### ILogger / FileLogger
Interface + implementation for logging (replaces static `Logger`):
- `Write(string message)`: Appends timestamped message to temp file (`%TEMP%\ARWtoJXL.log`); falls back to Console.Error on write failure
- `Clear()`: Deletes the log file; falls back to Console.Error on delete failure
- Injected into all services that need logging for testability

### QualityCalculator (Static Helper)
Centralized quality calculations to avoid duplication:
- `CalculateDistance(int quality)`: Maps 0-100 quality to Butteraugli distance (0.0-25.0)
- `CalculateEffort(int quality)`: Maps quality to encoding effort (5-9)
- `IsLossless(int quality)`: Returns true if quality ≥ 100

**Quality→Distance mapping:** quality 100 → distance 0.0 (lossless), quality 90 → distance 1.0 (visually lossless), quality 68-96 recommended
**Dynamic effort:** quality ≥95 → effort 9, ≥85 → effort 8, ≥70 → effort 7, ≥50 → effort 6, else effort 5

## Conversion Pipeline Details

**JXL Pipeline (Three-stage process):**
1. **Stage 0 (Magick.NET):** Extract metadata profiles (EXIF, XMP, ICC, IPTC) to temp files
2. **Stage 1 (Magick.NET):** ARW → PNG (16-bit lossless intermediate in %TEMP%) — **cached** via `IPngCache` keyed by file hash, so re-conversion with different quality settings skips this step
3. **Stage 2 (cjxl.exe):** PNG → JXL with metadata (external CLI tool bundled with app)

**JPEG Pipeline (Two-stage process):**
1. **Stage 0 (Magick.NET):** Extract metadata profiles
2. **Stage 1 (Magick.NET via IMagickService):** ARW → JPEG with quality setting
3. **Post-processing:** exiftool metadata embedding

**PNG Pipeline (Two-stage process):**
1. **Stage 0 (Magick.NET):** Extract metadata profiles
2. **Stage 1 (Magick.NET):** ARW → PNG (16-bit lossless)
3. **Post-processing:** exiftool metadata embedding

**cjxl arguments:**
- Lossless: `--distance=0.0 --effort={5-9} --num_threads={CPU} --container=1 --modular=1`
- Lossy: `--distance={0.1-25.0} --effort={5-9} --num_threads={CPU} --container=1 --progressive_dc=1`
- Metadata: `-x exif={path}`, `-x xmp={path}`, `-x icc_pathname={path}`, `-x jumbf={path}` (when available)

**Progress tracking:** 0.1 (metadata) → 0.5 (PNG complete) → 0.5→1.0 smooth (cjxl encoding via time-based estimation) → 1.0 (JXL complete)

**Metadata handling:**
- **EXIF extraction (ARW):** `IExiftoolService.ExtractExifAsync()` uses exiftool (`-b -exif:all`) to extract raw EXIF bytes from source ARW (~1s). Non-ARW files use Magick.NET first.
- **XMP/ICC/IPTC:** Extracted via Magick.NET profile lookup (`GetProfile("XMP")`, `GetProfile("ICC ")`, `GetIptcProfile()`).
- **Metadata embedding:** cjxl's `-x exif` argument does not reliably embed metadata (v0.11.2 known issue). Post-encoding, `IExiftoolService.EmbedMetadataAsync()` uses exiftool `-tagsFromFile source.arw -exif:all -xmp:all -icc-profile -overwrite_original output.jxl` to copy all available metadata (EXIF, XMP, ICC) from the original ARW to the output file.
- **Path resolution:** `IProcessRunner.FindExiftool()` centralizes exiftool.exe discovery (common paths → PATH → app directory).
- Metadata temp files kept alive during encoding (disposed AFTER `EncodeAsync` completes in finally block).
- Auto-cleanup via `MetadataProfiles.Dispose()` after encoding completes.

## File Lock Handling

When an ARW file is locked by another application (Adobe Bridge, Lightroom, etc.), file access throws `IOException` with HResult 32 on Windows:

- **FileLockedException** (`ARWtoJXL.Core.Models`): Custom exception extending `IOException` with user-friendly message identifying the locked file and suggesting closing it in the other app
- **Detection**: `FileLockedException.IsFileLocked()` checks HResult 32, inner IOException, and message patterns ("process cannot access the file", "being used by another process")
- **MagickService**: Catches IOException in `ExtractThumbnailAsync`, `ConvertToPngAsync`, and `ExtractProfilesFromImageAsync`, rethrows as `FileLockedException`
- **ExiftoolService**: Checks stderr for file-lock indicators in `EmbedMetadataAsync` (ex: "cannot open", "permission denied")

## Concurrency Model

- `SemaphoreSlim(maxConcurrency = Environment.ProcessorCount)` limits parallel conversions
- Progress tracking via `Interlocked.Increment()` on completed count
- `CancellationTokenSource` for graceful cancellation (checked at semaphore wait and passed to services)
- `OperationCanceledException` caught to mark items as Pending with "Cancelled" error

## Key Dependencies

- **Magick.NET-Q16-AnyCPU** (14.11.1): RAW image decoding, thumbnail extraction (Apache-2.0)
- **cjxl.exe** (downloaded at build): JPEG-XL encoder from libjxl (BSD-3-Clause)
- **exiftool.exe** + **exiftool_files/** (bundled): Metadata extraction/embedding — exiftool requires companion Perl runtime DLLs from `exiftool_files/` (perl532.dll, etc.) to function. Both `exiftool.exe` and the entire `exiftool_files/` directory are copied to output in all projects (Core, WPF, Tests).

## Enums

### ImageStatus
```
Pending    # Initial state OR cancelled conversion
Ready      # Loaded, awaiting conversion
Converting # Active conversion in progress
Converted  # Successfully converted to JXL (file sizes captured for compression ratio display)
Failed     # Conversion error (ErrorMessage populated)
```

### OutputFormat
```
Jxl   # JPEG XL output (default, uses cjxl encoder)
Jpeg  # JPEG output (uses Magick.NET)
Png   # PNG output (uses Magick.NET)
```

### ConflictResolution
```
Overwrite    # Overwrite existing file (default) — shows confirmation dialog if ConfirmOverwrite is enabled
Skip         # Skip conversion if output file exists
AppendNumber # Append _1, _2, etc. to filename if conflict
```
