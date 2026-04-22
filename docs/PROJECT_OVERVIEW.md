# ARWtoJXL - Project Overview

## Summary
Windows desktop app (.NET 8 WPF) that converts Sony RAW (.ARW) camera files to JPEG-XL (.JXL) format using a multi-stage pipeline: Magick.NET for RAW decoding and metadata extraction + cjxl CLI for JXL encoding with metadata preservation.

## Repository Layout

Root-level files tracked in git:
- `.gitignore` — excludes build outputs, IDE artifacts, temp/debug files, bundled binaries
- `ARWtoJXL.slnx` — solution matrix
- `THIRD-PARTY-NOTICES.md` — license notices for all dependencies
- `docs/PROJECT_OVERVIEW.md` — this file
- `ARWtoJXL/` — source projects (includes build.ps1)

## Project Structure
```
ARWtoJXL/
├── build.ps1                          # Build script (restore, download deps, publish)
# Note: cjxl.exe and exiftool.exe are downloaded at build time by build.ps1, not committed to git
├── ARWtoJXL.Core/          # Business logic layer (clean architecture)
│   ├── Models/

   │   │   ├── QualityCalculator.cs       # Static helper for quality→distance/effort mapping
   │   │   └── MetadataProfiles.cs        # Metadata container (EXIF, XMP, ICC, IPTC profiles)
│   ├── Interfaces/
│   │   ├── IImageService.cs           # Primary service interface (enum: ImageStatus)
│   │   ├── IMagickService.cs          # ImageMagick operations interface
│   │   ├── ICjxlEncoder.cs            # cjxl CLI encoder interface
│   │   ├── IFileService.cs            # File system operations interface
│   │   ├── IPathResolver.cs           # Path resolution interface
│   │   ├── IExiftoolService.cs        # exiftool operations interface (EXIF extraction, metadata embedding)
│   │   ├── ILogger.cs                 # Logging interface (replaces static Logger)
│   │   └── IProcessRunner.cs          # Process execution interface (replaces static ProcessHelper)
│   └── Services/
│       ├── ImageProcessingService.cs  # Main service orchestrating conversion pipeline
│       ├── MagickService.cs           # Magick.NET implementation (thumbnails, PNG conversion)
│       ├── CjxlEncoderService.cs      # cjxl CLI wrapper implementation
│       ├── ExiftoolService.cs         # exiftool operations (EXIF extraction, metadata embedding)
│       ├── SystemProcessRunner.cs     # IProcessRunner implementation (exiftool path resolution, version check, process execution)
│       ├── FileService.cs             # File system operations implementation
│       ├── PathResolverService.cs     # Path resolution implementation
      ├── FileLogger.cs              # ILogger implementation (file-based logger)
│       └── ServiceCollectionExtensions.cs # IServiceCollection extension for AddCoreServices()
├── ARWtoJXL.WPF/           # Presentation layer
│   ├── Models/
│   │   └── ImageItem.cs               # ViewModel data model (INotifyPropertyChanged)
│   ├── ViewModels/
│   │   ├── MainViewModel.cs           # MVVM viewmodel (CommunityToolkit.Mvvm)
│   │   └── SettingsViewModel.cs       # Settings dialog viewmodel
│   ├── MainWindow.xaml(.cs)           # Drag-drop gallery UI with DI wiring
│   ├── SettingsWindow.xaml(.cs)       # Settings dialog window
│   ├── BooleanToBrushConverter.cs     # WPF value converters
│   ├── BooleanToTextConverter.cs
│   ├── BooleanToValueConverter.cs
  ├── ImageStatusToStringConverter.cs
    │   ├── AppStrings.cs                  # Centralized UI string constants
│   └── Views/                         # Empty directory (reserved for future views)
└── ARWtoJXL.Tests/         # xUnit test suite
    ├── Startup.cs                    # DI service configuration (Microsoft.Extensions.DependencyInjection)
    ├── ConversionTests.cs            # Core conversion tests (inherits Startup, resolves IImageService)
    ├── MetadataPreservationTests.cs  # Metadata transfer tests (inherits Startup, resolves IMagickService, IImageService)
    ├── MetadataDebugTests.cs         # Diagnostic test with assertions for metadata preservation (inherits Startup)
  ├── QualityCalculatorTests.cs     # Unit tests for quality calculations (no DI)
    └── Services/                     # Empty directory (reserved for future service tests)
```

## Architecture Pattern
**Dependency Injection (DI)** - All services depend on abstractions (interfaces), not concrete implementations. Registered via `ServiceCollectionExtensions.AddCoreServices()` which configures all 6 services as singletons. This enables:
- Unit testing with mocks
- Swappable implementations
- Clear separation of concerns
- Reduced code duplication through single-responsibility services

**DI Registration Order** (resolved via `ServiceProvider`):
```
ILogger → FileLogger (singleton)
IProcessRunner → SystemProcessRunner (depends on ILogger)
IFileService → FileService (no deps)
IPathResolver → PathResolverService (no deps)
IExiftoolService → ExiftoolService (depends on IProcessRunner, ILogger)
IMagickService → MagickService (depends on IExiftoolService, ILogger)
ICjxlEncoder → CjxlEncoderService (depends on IPathResolver, IExiftoolService, ILogger)
IImageService → ImageProcessingService (depends on all above + ILogger)
```

## Services & Responsibilities

### IImageService (Primary Interface)
Defines two async operations:
- `GetThumbnailAsync(filePath)` → byte[]: Extracts thumbnail from ARW/JXL using Magick.NET (300x300 JPG)
- `ConvertArwToJxlAsync(inputPath, outputPath, progressCallback, quality, outputFormat, cancellationToken)`: Orchestrates two-stage conversion

### ImageProcessingService (Orchestrator)
Coordinates the conversion pipeline by delegating to specialized services:
1. **MagickService**: Extracts metadata profiles, converts ARW → PNG (16-bit lossless intermediate)
2. **CjxlEncoderService**: Encodes PNG → JXL via cjxl CLI with metadata
3. **FileService**: Manages temp file cleanup
4. **PathResolverService**: Locates cjxl.exe executable

**Constructor Injection:**
```csharp
public ImageProcessingService(
    IMagickService magickService,
    ICjxlEncoder cjxlEncoder,
    IFileService fileService,
    IPathResolver pathResolver,
    ILogger logger)
```

  ### IMagickService / MagickService
- `ExtractThumbnailAsync()`: Resizes image to 300x300, outputs JPEG
- `ConvertToPngAsync()`: Converts ARW to 16-bit PNG in temp directory
- `ExtractMetadataProfilesAsync()`: Extracts EXIF, XMP, ICC, IPTC profiles to temp files for cjxl — fully async, no sync-over-async blocking
  - **ARW files:** Awaits `IExiftoolService.ExtractExifAsync()` for EXIF extraction (Magick.NET cannot reliably read EXIF from Sony ARW files). Much faster (~1s) than the previous Magick.NET fallback chain (~10s).
  - **Non-ARW files:** Offloads Magick.NET profile extraction to `Task.Run` (CPU-bound), then awaits exiftool fallback for JXL files.
  - **Helper:** `ExtractProfilesFromImageAsync()` runs Magick.NET profile extraction on a thread-pool thread via `Task.Run`.
- **Constructor:** `MagickService(IExiftoolService exiftoolService, ILogger logger)` — both required (non-nullable)

### IExiftoolService / ExiftoolService
- `ExtractExifAsync(filePath)`: Extracts raw EXIF bytes from ARW/JXL files using exiftool
- `EmbedMetadataAsync(sourcePath, outputPath, metadata)`: Embeds EXIF, XMP, ICC metadata into JXL using exiftool's `-tagsFromFile` post-processing
- Uses `IProcessRunner.FindExiftool()` for path resolution
- **Constructor:** `ExiftoolService(IProcessRunner processRunner, ILogger logger)`

### ICjxlEncoder / CjxlEncoderService
- `EncodeAsync(inputPath, originalArwPath, outputPath, quality, metadata, cancellationToken, timeoutSeconds, progress)`: Invokes cjxl.exe with quality-based parameters
  - `inputPath`: Path to the PNG input (from Magick.NET conversion)
  - `originalArwPath`: Path to the original ARW file (used for exiftool metadata embedding)
  - `progress`: Optional `Action<double>` callback (0.0→1.0 relative to cjxl stage only)
- Uses `QualityCalculator` for distance/effort mapping
- Handles both lossless (quality≥100) and lossy modes
- **cjxl progress estimation:** cjxl v0.11.2 does not output percentage progress during encoding. A background task (`ReportProgressAsync`) reports linear progress from 0.0 to 0.98 during cjxl encoding (updated every 100ms), mapped to 0.5→1.0 in the overall pipeline.
- **Metadata embedding:** Delegates to `IExiftoolService.EmbedMetadataAsync()` for post-encoding metadata embedding via exiftool.
- **Constructor:** `CjxlEncoderService(IPathResolver pathResolver, IExiftoolService exiftoolService, ILogger logger)` — all required (non-nullable)

### IFileService / FileService
- `DeleteFile()`: Safe file deletion with exception handling
- `FileExists()`: File existence check
- `CombinePaths()`: Path concatenation
- `GetTempFileName()`: Generates unique temp PNG path

### IPathResolver / PathResolverService
- `ResolveCjxlPath()`: Searches app directory, then executable directory, falls back to PATH
- `GetTempPath()`: Returns system temp directory

### IProcessRunner / SystemProcessRunner
Interface + implementation for process execution (replaces static `ProcessHelper`):
- `FindExiftool(logPrefix)`: Searches common paths, PATH, and app directory for exiftool.exe
- `IsExiftoolWorking(exiftoolPath, logPrefix)`: Runs `exiftool -ver` to verify functionality
- `RunProcessAsync(fileName, arguments)`: Generic async process launcher with stdout/stderr capture
- `RunProcessBinaryAsync(fileName, arguments)`: Runs process and returns raw binary stdout
- Injected into `ExiftoolService` for testability

### ILogger / FileLogger
Interface + implementation for logging (replaces static `Logger`):
- `Write(string message)`: Appends timestamped message to temp file (`%TEMP%\ARWtoJXL.log`)
- `Clear()`: Deletes the log file
- Injected into all services that need logging for testability

### QualityCalculator (Static Helper)
Centralized quality calculations to avoid duplication:
- `CalculateDistance(int quality)`: Maps 0-100 quality to Butteraugli distance (0.0-25.0)
- `CalculateEffort(int quality)`: Maps quality to encoding effort (5-9)
- `IsLossless(int quality)`: Returns true if quality ≥ 100

**Quality→Distance mapping:** quality 100 → distance 0.0 (lossless), quality 90 → distance 1.0 (visually lossless), quality 68-96 recommended
**Dynamic effort:** quality ≥95 → effort 9, ≥85 → effort 8, ≥70 → effort 7, ≥50 → effort 6, else effort 5

## Conversion Pipeline Details
**Three-stage process:**
1. **Stage 0 (Magick.NET):** Extract metadata profiles (EXIF, XMP, ICC, IPTC) to temp files
2. **Stage 1 (Magick.NET):** ARW → PNG (16-bit lossless intermediate in %TEMP%)
3. **Stage 2 (cjxl.exe):** PNG → JXL with metadata (external CLI tool bundled with app)

**cjxl arguments:**
- Lossless: `--distance=0.0 --effort={5-9} --num_threads={CPU} --container=1 --modular=1`
- Lossy: `--distance={0.1-25.0} --effort={5-9} --num_threads={CPU} --container=1 --progressive_dc=1`
- Metadata: `-x exif={path}`, `-x xmp={path}`, `-x icc_pathname={path}`, `-x jumbf={path}` (when available)

**Progress tracking:** 0.1 (metadata) → 0.5 (PNG complete) → 0.5→1.0 smooth (cjxl encoding via time-based estimation) → 1.0 (JXL complete)

**Metadata handling:**
- **EXIF extraction (ARW):** `IExiftoolService.ExtractExifAsync()` uses exiftool (`-b -exif:all`) to extract raw EXIF bytes from source ARW (~1s). Non-ARW files use Magick.NET first.
- **XMP/ICC/IPTC:** Extracted via Magick.NET profile lookup (`GetProfile("XMP")`, `GetProfile("ICC ")`, `GetIptcProfile()`).
- **Metadata embedding:** cjxl's `-x exif` argument does not reliably embed metadata (v0.11.2 known issue). Post-encoding, `IExiftoolService.EmbedMetadataAsync()` uses exiftool `-tagsFromFile source.arw -exif:all -overwrite_original output.jxl` to copy metadata from the original ARW to the JXL.
- **Path resolution:** `IProcessRunner.FindExiftool()` centralizes exiftool.exe discovery (common paths → PATH → app directory).
- Metadata temp files kept alive during encoding (disposed AFTER `EncodeAsync` completes in finally block).
- Auto-cleanup via `MetadataProfiles.Dispose()` after encoding completes.

## UI/UX Flow
1. User drags .ARW files or folders onto MainWindow, or clicks "Open File" button to browse via OpenFileDialog
2. `MainWindow` constructor creates `ServiceCollection`, calls `AddCoreServices()`, builds `ServiceProvider`, resolves `IImageService`
3. `MainViewModel.AddFilesAsync()` deduplicates by normalized full path (case-insensitive), filters ARW/JXL, creates `ImageItem` objects, generates thumbnails via `GetThumbnailAsync()`
4. Items displayed in gallery with 80x60 thumbnails, selection checkboxes, and per-item progress spinners
5. User selects files, configures settings (quality, subfolder) → clicks "Convert"
6. `ConvertSelectedAsync()` spawns concurrent tasks (max = CPU core count via SemaphoreSlim)
7. Each conversion: Ready → Converting → Converted/Failed (or Pending if cancelled)
8. User can click "Cancel" to abort ongoing conversions
9. Output saved to same directory or subfolder (configurable via `UseSubfolder` + `SubfolderName`)

## UI Components
- **Open File Button:** Opens OpenFileDialog with ARW/JXL filter, calls `AddFilesAsync()` with selected files
- **Gallery (ListBox):** Displays ImageItem objects with thumbnail previews, checkboxes, status indicators
- **Per-Item Spinner:** Indeterminate ProgressBar visible when `Status == ImageStatus.Converting`
- **Global ProgressBar:** Shows overall progress (CompletedCount/TotalCount), hidden when `IsConverting == false`
- **Cancel Button:** Enabled during conversion, triggers `CancellationTokenSource.Cancel()`
- **Settings Window (Dialog):** Separate SettingsWindow with quality slider (0-100, default 90), subfolder checkbox, subfolder name TextBox
- **Select All/Deselect All Button:** Toggles selection state, text bound to `IsAllSelected`
- **Open Output Folder Button:** Opens the output directory in File Explorer. Enabled after conversion completes (via `CanExecute` on `OpenOutputFolderCommand`). Uses `Process.Start` with `UseShellExecute = true`.

## Selection Logic
- `IsAllSelected`: True when `Images.All(i => i.IsSelected)`
- `IsAnySelected`: True when `_selectedImages.Any()`
- `UpdateSelectionState()`: Recalculates selection state on every `IsSelected` change
- `RemoveSelected()`: Removes selected items and resets selection state

## Keyboard Shortcuts

Defined in `MainWindow.xaml` via `Window.InputBindings`:

| Shortcut | Action |
|----------|--------|
| `Ctrl+A` | Select All |
| `Ctrl+C` | Convert Selected |
| `Delete` | Remove Selected |
| `Ctrl+D` | Open File Dialog |

## Settings (SettingsViewModel + SettingsWindow)

`SettingsWindow` is a separate dialog window (not an Expander in MainWindow):
- **SettingsViewModel**: `UseSubfolder` (bool, default true), `SubfolderName` (string, default "jxl_output"), `QualityPreset` (int, default 90)
- **SettingsWindow.xaml**: CheckBox for subfolder, TextBox for subfolder name, Slider for quality (0-100)
- Settings applied via `MainViewModel.ApplySettings(SettingsViewModel)` on window close
- `MainViewModel` exposes `UseSubfolder`, `SubfolderName`, `QualityPreset` properties synced from SettingsWindow

## Concurrency Model
- `SemaphoreSlim(maxConcurrency = Environment.ProcessorCount)` limits parallel conversions
- Progress tracking via `Interlocked.Increment()` on completed count
- `Dispatcher.InvokeAsync()` for UI thread marshaling
- `CancellationTokenSource` for graceful cancellation (checked at semaphore wait and passed to services)
- `OperationCanceledException` caught to mark items as Pending with "Cancelled" error

## Key Dependencies
- **Magick.NET-Q16-AnyCPU** (14.11.1): RAW image decoding, thumbnail extraction (Apache-2.0)
- **CommunityToolkit.Mvvm** (8.4.2): `[ObservableProperty]`, `[RelayCommand]` source generators (MIT)
- **WPF-UI**: Modern Fluent UI controls for WPF (MIT)
- **cjxl.exe** (downloaded at build): JPEG-XL encoder from libjxl (BSD-3-Clause)
- **exiftool.exe** (downloaded at build): Metadata extraction/embedding (GPL-3.0)
- **xUnit + Moq** (test): Unit testing framework (Apache-2.0 / BSD-3-Clause)

## Enums

### ImageStatus
```
Pending    # Initial state OR cancelled conversion
Ready      # Loaded, awaiting conversion
Converting # Active conversion in progress
Converted  # Successfully converted to JXL
Failed     # Conversion error (ErrorMessage populated)
```


## Testing
- **QualityCalculatorTests**: 12 unit tests for quality→distance/effort mappings (no DI needed)
- **Startup**: Central DI configuration — calls `services.AddCoreServices()` from `ARWtoJXL.Core`. Tests inherit from `Startup` and resolve services from `ServiceProvider`. Provides `CreateScope()` for test isolation.
- **MetadataDebugTests**: Diagnostic test with assertions for full metadata preservation verification (inherits `Startup`)
  - Resolves services from `ServiceProvider`
  - Extracts metadata from ARW, converts to JXL, verifies 15+ EXIF tags preserved via exiftool
  - Uses exiftool `-s -n -Make -Model ...` format for tag-specific reading
  - Assertions: minimum 5 matched tags, no missing tags, output has metadata
- **ConversionTests**: Integration tests with real ARW files (inherits `Startup`)
  - Resolves `IImageService` from `ServiceProvider`
  - Thumbnail extraction
  - Conversion at various quality levels (0, 50, 70, 90, 100)
  - Lossless mode verification
  - Progress callback verification (smooth updates, monotonic increase, final 1.0)
- **MetadataPreservationTests**: Metadata-specific tests (inherits `Startup`)
  - Resolves `IMagickService` and `IImageService` from `ServiceProvider`
  - EXIF transfer verification
  - ICC profile preservation
  - HasAny property verification
  - Metadata at different quality levels (90, 100)

## Build/Deploy
- `build.ps1` (in `ARWtoJXL/`): checks cjxl.exe, downloads exiftool if missing, copies both to publish dir, then `dotnet restore` + `dotnet publish`
- Single-file publish: `dotnet publish -c Release -r win-x64 --self-contained true -p:DebugType=None -p:DebugSymbols=false`
- cjxl.exe + exiftool.exe copied to output via `<None Include="..\*.exe" CopyToOutputDirectory="PreserveNewest" />`
- Tests: `dotnet test ARWtoJXL.Tests.csproj`

## Git Ignore Policy

Excluded via root `.gitignore`:
- `bin/`, `obj/` — all build outputs
- `packages/`, `*.nupkg`, `project.nuget.cache` — NuGet artifacts
- `*.user`, `*.suo`, `*.sln.docstates` — IDE state files
- `.idea/`, `*.sln.iml` — JetBrains Rider artifacts
- `MediaCache/` — ImageMagick cache directory
- `*.pdb` — debug symbol files
- `cjxl_help_*.txt`, `debug_metadata.csx` — temporary debugging files
- `cjxl.exe` — bundled binary (downloaded at build time)

## License Compliance
- `THIRD-PARTY-NOTICES.md` contains license texts for all dependencies
- **GPL-3.0**: exiftool.exe is GPL-3.0; bundled in repo with license notice in THIRD-PARTY-NOTICES.md
- Consider adding a `LICENSE` file for the project's own code (e.g., MIT or Apache-2.0)
