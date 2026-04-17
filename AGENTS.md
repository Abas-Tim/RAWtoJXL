# ARWtoJPEGXL

High-performance Windows desktop application for converting Sony RAW (.ARW) files to JPEG-XL (.JXL) format. Built with .NET 8, WPF, and MVVM pattern.

## Quick Reference

- **Tech Stack:** .NET 8, WPF, C#, MVVM (CommunityToolkit.Mvvm)
- **Core Dependencies:** Magick.NET-Q16-AnyCPU v14.11.1, cjxl.exe v0.11.2 bundled
- **Test Framework:** xUnit v2.9.3, Moq v4.20.72
- **Build Command:** `.\build.ps1` or `dotnet publish ARWtoJXL.WPF/ARWtoJXL.WPF.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true`
- **Test Command:** `dotnet test`

## Directory Structure

```
ARWtoJPEGXL/
├── .gitignore                             # Excludes bin/, obj/, NuGet cache, VS/Rider artifacts, temp files
├── ARWtoJXL.slnx                          # Solution matrix
├── build.ps1                              # Build script (restore, test, publish)
├── cjxl.exe                               # JPEG XL encoder binary v0.11.2
├── docs/
│   └── PROJECT_OVERVIEW.md               # Detailed architecture documentation
└── ARWtoJXL/
    ├── ARWtoJXL.sln                       # Visual Studio solution (3 projects)
    ├── README.md
    ├── ARWtoJXL.Core/                     # Domain logic & services
    ├── ARWtoJXL.Tests/                    # xUnit tests
    └── ARWtoJXL.WPF/                      # WPF presentation layer
```

## Git Ignore Policy

Generated/build artifacts are excluded via `.gitignore`:
- `bin/`, `obj/` — all build outputs across projects
- `packages/`, `*.nupkg`, `project.nuget.cache` — NuGet artifacts
- `*.user`, `*.suo`, `*.sln.docstates` — IDE state files
- `.idea/`, `*.sln.iml` — JetBrains Rider artifacts
- `MediaCache/` — ImageMagick cache directory
- `*.pdb` — debug symbol files
- `cjxl_help_*.txt`, `debug_metadata.csx` — temporary debugging files

## Project Architecture

### Solution (ARWtoJXL.sln)

Three projects with clear dependency boundaries:

| Project | Type | Dependencies | Purpose |
|---------|------|-------------|---------|
| `ARWtoJXL.Core` | Class Library | Magick.NET-Q16-AnyCPU | Domain models, interfaces, services |
| `ARWtoJXL.WPF` | WPF App | CommunityToolkit.Mvvm, references Core | UI, MVVM viewmodels, converters |
| `ARWtoJXL.Tests` | Test Project | xUnit, Moq, references Core+WPF | Unit + integration tests |

### Core Project (`ARWtoJXL.Core`)

```
ARWtoJXL.Core/
├── Interfaces/
│   ├── ICjxlEncoder.cs        # JPEG XL encoding contract (inputPath, originalArwPath, outputPath, quality, metadata)
│   ├── IFileService.cs        # File operations contract
│   ├── IMagickService.cs      # ImageMagick operations contract
│   └── IPathResolver.cs       # Path resolution contract
├── Models/
│   ├── ConversionOptions.cs   # Quality, format, output path config
│   ├── ConversionResult.cs    # Success/failure, progress, error info
│   ├── MetadataProfiles.cs    # EXIF/XMP/ICC/IPTC temp file container (IDisposable)
│   └── QualityCalculator.cs   # Static: quality->distance/effort mapping
└── Services/
    ├── IImageService.cs       # ImageStatus enum, OutputFormat enum, IImageService interface
    ├── ImageProcessingService.cs   # Main 3-stage conversion pipeline
    ├── MagickService.cs       # Magick.NET implementation (thumbnails, ARW->PNG, metadata)
    ├── CjxlEncoderService.cs  # cjxl CLI wrapper with timeout/cancellation
    ├── FileService.cs         # File I/O operations
    └── PathResolverService.cs # Resolves cjxl.exe location
```

### WPF Project (`ARWtoJXL.WPF`)

```
ARWtoJXL.WPF/
├── App.xaml / App.xaml.cs          # WPF application entry
├── MainWindow.xaml / MainWindow.xaml.cs   # Drag-drop window
├── ViewModels/
│   └── MainViewModel.cs            # MVVM viewmodel (343 lines, core logic)
├── Models/
│   └── ImageItem.cs                # UI data model (INotifyPropertyChanged)
├── BooleanToBrushConverter.cs      # Selected item background highlight
├── BooleanToTextConverter.cs       # Toggle "Select All"/"Deselect All"
├── BooleanToValueConverter.cs      # Bool->value mapping utilities
├── ImageStatusToStringConverter.cs # ImageStatus enum to display strings
└── OutputFormatToBoolConverter.cs  # OutputFormat binding helper
```

## Conversion Pipeline

The core conversion happens in `ImageProcessingService.ConvertArwToJxlAsync()` as a 3-stage pipeline:

1. **Stage 1 (progress 0.1):** Extract metadata (EXIF, XMP, ICC, IPTC) via MagickService -> saves to temp files via MetadataProfiles
2. **Stage 2 (progress 0.5):** Convert ARW -> PNG (16-bit) via MagickService (Magick.NET)
3. **Stage 3 (progress 1.0):** Encode PNG -> JXL via cjxl.exe CLI (CjxlEncoderService)
4. **Post-processing:** Embed metadata into JXL using exiftool `-tagsFromFile` (cjxl's `-x exif` does not work reliably)

Cleanup in finally block: disposes MetadataProfiles (deletes temp metadata files), deletes temp PNG.

### cjxl Arguments

Built by `CjxlEncoderService.BuildEncodingArguments()`:

| Mode | Args |
|------|------|
| **Lossy** | `--distance={F2}` (from QualityCalculator), `--effort={N}`, `--num_threads={count}`, `--container=1`, `--progressive_dc=1` |
| **Lossless** | `--distance=0`, `--effort={N}`, `--num_threads={count}`, `--container=1`, `--modular=1` |
| **Metadata (cjxl)** | `-x exif={path} -x xmp={path} -x icc_pathname={path} -x jumbf={path}` (NOT reliably embedded in v0.11.2) |

**Metadata embedding workaround:** Since cjxl's `-x exif` does not reliably embed metadata, `CjxlEncoderService.EmbedMetadataWithExiftoolAsync()` uses exiftool post-processing: `-tagsFromFile source.arw -exif:all -overwrite_original output.jxl`.

## Quality Calculator

`QualityCalculator` (static class) maps quality (0-100) to Butteraugli distance and encoder effort:

- **`CalculateDistance(int quality)`**: quality 100 -> 0.0, quality 90 -> ~1.0 (visually lossless). Uses quadratic formula below quality 30.
- **`CalculateEffort(int quality)`**: >=95:9, >=85:8, >=70:7, >=50:6, else:5
- **`IsLossless(int quality)`**: returns `true` when quality >= 100

## Enums

### ImageStatus (ARWtoJXL.Core.Services)
`Pending | Ready | Converting | Converted | Failed`

### OutputFormat (ARWtoJXL.Core.Services)
`Jxl` (currently only format supported)

## Concurrency Model

- **Multiple file conversion:** `SemaphoreSlim(Environment.ProcessorCount)` limits parallel tasks
- **Per-file stages:** `Task.Run()` with progress callbacks via `Action<double>`
- **Cancellation:** `CancellationTokenSource` passed through pipeline
- **UI updates:** `Dispatcher.InvokeAsync()` for thumbnail loading
- **Thread safety:** `Interlocked.Increment()` for completion counter

## MVVM Pattern (MainViewModel)

Uses CommunityToolkit.Mvvm source generators:
- `[ObservableProperty]` for all UI-bound properties
- `[RelayCommand(CanExecute=...)]` for commands
- Key commands: `ConvertSelectedCommand`, `RemoveSelectedCommand`, `SelectAllCommand`, `CancelCommand`
- Selection tracking via `_selectedImages` HashSet + `Item_PropertyChanged` handler

## Services (Constructor Injection in Core)

| Interface | Implementation | Responsibility |
|-----------|---------------|----------------|
| `IImageService` | `ImageProcessingService` | Orchestrates full ARW->JXL pipeline |
| `IMagickService` | `MagickService` | ARW decoding, PNG conversion, thumbnail extraction, metadata extraction (exiftool for ARW EXIF, Magick.NET for XMP/ICC/IPTC) |
| `ICjxlEncoder` | `CjxlEncoderService` | cjxl.exe process execution, exiftool post-processing for metadata embedding |
| `IFileService` | `FileService` | Basic file I/O operations |
| `IPathResolver` | `PathResolverService` | Resolves cjxl.exe path (base dir -> exe dir -> PATH) |

## Testing

Located in `ARWtoJXL.Tests/`:

| Test File | Purpose |
|-----------|---------|
| `QualityCalculatorTests.cs` | Unit tests for distance/effort calculations |
| `ImageProcessingServiceTests.cs` | Integration tests for conversion pipeline |
| `MetadataDebugTests.cs` | Diagnostic test with assertions: extracts metadata from ARW, converts to JXL, verifies 15+ EXIF tags preserved via exiftool |

## Build & Deployment

`build.ps1` workflow:
1. Downloads cjxl.exe v0.11.2 from GitHub releases if missing
2. `dotnet restore`
3. `dotnet test --no-build`
4. `dotnet publish` with `DebugType=None`, `DebugSymbols=false`, single-file, trimmed
5. Copies cjxl.exe to publish directory

Output: `ARWtoJXL.WPF\bin\Release\net8.0-windows\win-x64\publish\`

## Key Implementation Details

- **Metadata extraction:** exiftool (`-b -exif:all`) for ARW EXIF (primary, ~1s). Magick.NET profile objects with reflection-based `GetProfileBytes()` for XMP/ICC/IPTC.
- **Metadata embedding:** cjxl's `-x exif` does not reliably work (v0.11.2). Post-processing with exiftool `-tagsFromFile source.arw -exif:all -overwrite_original output.jxl`.
- **Thumbnail generation:** 300x300 JPG, quality 85, metadata stripped
- **cjxl path resolution:** Checks `AppDomain.BaseDirectory\cjxl.exe` -> exe parent dir -> PATH lookup
- **cjxl encoding:** Redirected stdout/stderr, UTF8 encoding, 300s default timeout, `CjxlEncodingException` on non-zero exit
- **ICjxlEncoder.EncodeAsync signature:** `EncodeAsync(inputPath, originalArwPath, outputPath, quality, metadata, cancellationToken)` — `originalArwPath` used for exiftool metadata embedding
- **WPF wiring:** Services instantiated directly in MainWindow constructor (no DI container)

## Files to Edit by Task Type

- **Conversion logic / cjxl args:** `ARWtoJXL.Core/Services/CjxlEncoderService.cs`, `ARWtoJXL.Core/Models/QualityCalculator.cs`
- **Image processing / metadata:** `ARWtoJXL.Core/Services/MagickService.cs`, `ARWtoJXL.Core/Services/ImageProcessingService.cs`
- **UI / viewmodel:** `ARWtoJXL.WPF/ViewModels/MainViewModel.cs`, `ARWtoJXL.WPF/MainWindow.xaml`
- **Converters:** `ARWtoJXL.WPF/*Converter.cs`
- **Models:** `ARWtoJXL.Core/Models/`, `ARWtoJXL.WPF/Models/ImageItem.cs`
- **Tests:** `ARWtoJXL.Tests/`
- **Build/config:** `build.ps1`, `*.csproj` files
