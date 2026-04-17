# ARWtoJXL - Project Overview

## Summary
Windows desktop app (.NET 8 WPF) that converts Sony RAW (.ARW) camera files to JPEG-XL (.JXL) format using a multi-stage pipeline: Magick.NET for RAW decoding and metadata extraction + cjxl CLI for JXL encoding with metadata preservation.

## Repository Layout

Root-level files tracked in git:
- `.gitignore` — excludes build outputs, IDE artifacts, temp/debug files
- `ARWtoJXL.slnx` — solution matrix
- `build.ps1` — build script (restore, test, publish)
- `cjxl.exe` — JPEG XL encoder binary v0.11.2 (bundled)
- `docs/PROJECT_OVERVIEW.md` — this file
- `ARWtoJXL/` — source projects

## Project Structure
```
ARWtoJXL/
├── ARWtoJXL.Core/          # Business logic layer (clean architecture)
│   ├── Models/
  │   │   ├── ConversionOptions.cs       # Configuration for conversion settings
   │   │   ├── ConversionResult.cs        # Result wrapper for conversion operations
   │   │   ├── QualityCalculator.cs       # Static helper for quality→distance/effort mapping
   │   │   └── MetadataProfiles.cs        # Metadata container (EXIF, XMP, ICC, IPTC profiles)
│   ├── Interfaces/
│   │   ├── IImageService.cs           # Primary service interface (enums: ImageStatus, OutputFormat)
│   │   ├── IMagickService.cs          # ImageMagick operations interface
│   │   ├── ICjxlEncoder.cs            # cjxl CLI encoder interface
│   │   ├── IFileService.cs            # File system operations interface
│   │   └── IPathResolver.cs           # Path resolution interface
│   └── Services/
│       ├── ImageProcessingService.cs  # Main service orchestrating conversion pipeline
│       ├── MagickService.cs           # Magick.NET implementation (thumbnails, PNG conversion)
│       ├── CjxlEncoderService.cs      # cjxl CLI wrapper implementation
│       ├── FileService.cs             # File system operations implementation
│       └── PathResolverService.cs     # Path resolution implementation
├── ARWtoJXL.WPF/           # Presentation layer
│   ├── Models/
│   │   └── ImageItem.cs               # ViewModel data model (INotifyPropertyChanged)
│   ├── ViewModels/
│   │   └── MainViewModel.cs           # MVVM viewmodel (CommunityToolkit.Mvvm)
│   ├── Converters/                    # WPF value converters
│   │   ├── BooleanToBrushConverter.cs
│   │   ├── BooleanToTextConverter.cs
│   │   ├── BooleanToValueConverter.cs
│   │   └── ImageStatusToStringConverter.cs
│   └── MainWindow.xaml(.cs)           # Drag-drop gallery UI with DI wiring
└── ARWtoJXL.Tests/         # xUnit test suite
    ├── MetadataDebugTests.cs         # Diagnostic test with assertions for metadata preservation
    ├── ImageProcessingServiceTests.cs # Integration tests for conversion pipeline
    └── QualityCalculatorTests.cs      # Unit tests for quality calculations
```

## Architecture Pattern
**Dependency Injection (DI)** - Services depend on abstractions (interfaces), not concrete implementations. This enables:
- Unit testing with mocks
- Swappable implementations
- Clear separation of concerns
- Reduced code duplication through single-responsibility services

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
    IPathResolver pathResolver)
```

### IMagickService / MagickService
- `ExtractThumbnailAsync()`: Resizes image to 300x300, outputs JPEG
- `ConvertToPngAsync()`: Converts ARW to 16-bit PNG in temp directory
- `ExtractMetadataProfilesAsync()`: Extracts EXIF, XMP, ICC, IPTC profiles to temp files for cjxl
  - **ARW files:** Uses exiftool as primary EXIF extractor (Magick.NET cannot reliably read EXIF from Sony ARW files). Much faster (~1s) than the previous Magick.NET fallback chain (~10s).
  - **Non-ARW files:** Uses Magick.NET profile extraction first, falls back to exiftool for JXL files.

### ICjxlEncoder / CjxlEncoderService
- `EncodeAsync(inputPath, originalArwPath, outputPath, quality, metadata, cancellationToken)`: Invokes cjxl.exe with quality-based parameters
  - `inputPath`: Path to the PNG input (from Magick.NET conversion)
  - `originalArwPath`: Path to the original ARW file (used for exiftool metadata embedding)
- Uses `QualityCalculator` for distance/effort mapping
- Handles both lossless (quality≥100) and lossy modes
- **Metadata embedding:** cjxl's `-x exif` argument does not reliably embed metadata (v0.11.2). Instead, `EmbedMetadataWithExiftoolAsync()` uses exiftool's `-tagsFromFile` post-processing step to copy metadata from the source ARW to the output JXL after cjxl encoding completes.

### IFileService / FileService
- `DeleteFile()`: Safe file deletion with exception handling
- `FileExists()`: File existence check
- `CombinePaths()`: Path concatenation
- `GetTempFileName()`: Generates unique temp PNG path

### IPathResolver / PathResolverService
- `ResolveCjxlPath()`: Searches app directory, then executable directory, falls back to PATH
- `GetTempPath()`: Returns system temp directory

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

**Progress tracking:** 0.1 (start) → 0.5 (PNG complete) → 1.0 (JXL complete)

**Metadata handling:**
- **EXIF extraction (ARW):** exiftool (`-b -exif:all`) extracts raw EXIF bytes from source ARW (~1s). Non-ARW files use Magick.NET first.
- **XMP/ICC/IPTC:** Extracted via Magick.NET profile lookup (`GetProfile("XMP")`, `GetProfile("ICC ")`, `GetIptcProfile()`).
- **Metadata embedding:** cjxl's `-x exif` argument does not reliably embed metadata (v0.11.2 known issue). Post-encoding, `EmbedMetadataWithExiftoolAsync()` uses exiftool `-tagsFromFile source.arw -exif:all -overwrite_original output.jxl` to copy metadata from the original ARW to the JXL.
- Metadata temp files kept alive during encoding (disposed AFTER `EncodeAsync` completes in finally block).
- Auto-cleanup via `MetadataProfiles.Dispose()` after encoding completes.

## UI/UX Flow
1. User drags .ARW files or folders onto MainWindow
2. `MainViewModel.AddFilesAsync()` filters ARW/JXL, creates `ImageItem` objects, generates thumbnails via `GetThumbnailAsync()`
3. Items displayed in gallery with 80x60 thumbnails, selection checkboxes, and per-item progress spinners
4. User selects files, configures settings (quality, subfolder) → clicks "Convert"
5. `ConvertSelectedAsync()` spawns concurrent tasks (max = CPU core count via SemaphoreSlim)
6. Each conversion: Ready → Converting → Converted/Failed (or Pending if cancelled)
7. User can click "Cancel" to abort ongoing conversions
8. Output saved to same directory or subfolder (configurable via `UseSubfolder` + `SubfolderName`)

## UI Components
- **Gallery (ListBox):** Displays ImageItem objects with thumbnail previews, checkboxes, status indicators
- **Per-Item Spinner:** Indeterminate ProgressBar visible when `Status == ImageStatus.Converting`
- **Global ProgressBar:** Shows overall progress (CompletedCount/TotalCount), hidden when `IsConverting == false`
- **Cancel Button:** Enabled during conversion, triggers `CancellationTokenSource.Cancel()`
- **Settings Panel (Expander):** Quality slider (0-100, default 90) and subfolder options
- **Select All/Deselect All Button:** Toggles selection state, text bound to `IsAllSelected`

## Selection Logic
- `IsAllSelected`: True when `Images.All(i => i.IsSelected)`
- `IsAnySelected`: True when `_selectedImages.Any()`
- `UpdateSelectionState()`: Recalculates selection state on every `IsSelected` change
- `RemoveSelected()`: Removes selected items and resets selection state

## Concurrency Model
- `SemaphoreSlim(maxConcurrency = Environment.ProcessorCount)` limits parallel conversions
- Progress tracking via `Interlocked.Increment()` on completed count
- `Dispatcher.InvokeAsync()` for UI thread marshaling
- `CancellationTokenSource` for graceful cancellation (checked at semaphore wait and passed to services)
- `OperationCanceledException` caught to mark items as Pending with "Cancelled" error

## Key Dependencies
- **Magick.NET-Q16-AnyCPU** (14.11.1): RAW image decoding, thumbnail extraction
- **CommunityToolkit.Mvvm** (8.4.2): `[ObservableProperty]`, `[RelayCommand]` source generators
- **cjxl.exe** (bundled): JPEG-XL encoder from libjxl
- **xUnit + Moq**: Unit testing framework

## Enums

### ImageStatus
```
Pending    # Initial state OR cancelled conversion
Ready      # Loaded, awaiting conversion
Converting # Active conversion in progress
Converted  # Successfully converted to JXL
Failed     # Conversion error (ErrorMessage populated)
```

### OutputFormat
```
Jxl  # Native JPEG-XL format
```

## Testing
- **QualityCalculatorTests**: 12 unit tests for quality→distance/effort mappings
- **MetadataDebugTests**: Diagnostic test with assertions for full metadata preservation verification
  - Extracts metadata from ARW, converts to JXL, verifies 15+ EXIF tags preserved via exiftool
  - Uses exiftool `-s -n -Make -Model ...` format for tag-specific reading
  - Assertions: minimum 5 matched tags, no missing tags, output has metadata
- **ImageProcessingServiceTests**: Integration tests with real ARW files
  - Thumbnail extraction
  - Conversion at various quality levels (0, 50, 70, 90, 100)
  - Lossless mode verification
  - Metadata transfer verification (EXIF, ICC presence and non-empty verification)
  - Error handling for invalid files

## Build/Deploy
- Single-file publish: `dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true`
- cjxl.exe copied to output via `<None Include="..\cjxl.exe" CopyToOutputDirectory="PreserveNewest" />`
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
