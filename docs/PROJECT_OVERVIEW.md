# RAWtoJXL - Project Overview

## Summary

Windows desktop app (.NET 8, Avalonia 12 UI) that converts RAW camera files to JPEG-XL (.JXL), JPEG (.JPG), or PNG (.PNG) formats. Supports Sony (.ARW, .SR2, .SRF), Canon (.CRW, .CR2, .CR3), Nikon (.NEF, .NRW), Fujifilm (.RAF), Olympus/OM System (.ORF), Panasonic (.RW2), and Adobe (.DNG). Uses a multi-stage pipeline: Magick.NET for RAW decoding + cjxl CLI for JXL encoding + exiftool for metadata operations. The JXL pipeline pipes 16-bit RGB PPM data directly to cjxl stdin for zero intermediate disk I/O. Metadata is handled entirely by exiftool: extracted via dedicated commands for verification, embedded post-encoding via `-tagsFromFile` (single invocation per file). Thumbnail generation uses embedded EXIF previews when available (zero decode), falling back to fast decode with camera LUT disabled and nearest-neighbor resampling, with concurrency scaled to `max(4, cores/2)`. Features include recursive folder search, conversion history, per-file quality override, file conflict resolution, custom output directory picker, named conversion presets, per-file compression ratio display, metadata skip toggle, and advanced cjxl options (effort, near-lossless, raw distance).

## Repository Layout

```
RAWtoJXL/
├── .gitignore
├── RAWtoJXL.slnx                          # Solution matrix
├── THIRD-PARTY-NOTICES.md                 # License notices for all dependencies
├── docs/
│   └── PROJECT_OVERVIEW.md               # This file
└── RAWtoJXL/
    ├── build.ps1                          # Build script (restore, download deps, publish)
    ├── cjxl.exe                           # JPEG XL encoder v0.11.2 (downloaded at build time)
    ├── exiftool.exe                       # Metadata tool v13.57 (downloaded at build time)
    ├── exiftool_files/                    # exiftool companion Perl runtime DLLs
    ├── RAWtoJXL.sln
    ├── RAWtoJXL.Core/                     # Business logic layer
    │   └── docs/PROJECT.md               # Core project documentation
    ├── RAWtoJXL.Avalonia/                      # Avalonia UI presentation layer
    │   └── docs/PROJECT.md               # Avalonia project documentation
    └── RAWtoJXL.Tests/                    # xUnit test suite
        └── docs/PROJECT.md               # Tests project documentation
```

## Project Documentation

Each project maintains its own documentation with detailed information on architecture, services, components, and dependencies:

- **RAWtoJXL.Core** — `RAWtoJXL/RAWtoJXL.Core/docs/PROJECT.md`
  - Services, interfaces, DI registration, conversion pipeline, file lock handling, concurrency model, enums

- **RAWtoJXL.Avalonia** — `RAWtoJXL/RAWtoJXL.Avalonia/docs/PROJECT.md`
  - UI/UX flow, view models, UI components, settings, selection logic

- **RAWtoJXL.Tests** — `RAWtoJXL/RAWtoJXL.Tests/docs/PROJECT.md`
  - Test configuration, test suites, DI setup for tests

## Build/Deploy

- `build.ps1` (in `RAWtoJXL/`): checks cjxl.exe exists, downloads exiftool v13.57 if missing (from SourceForge), copies both to publish dir, then `dotnet restore` + `dotnet publish`
- Single-file publish: `dotnet publish -c Release -r win-x64 --self-contained true -p:DebugType=None -p:DebugSymbols=false`
- cjxl.exe + exiftool.exe + exiftool_files/ copied to output via `<None Include="..\*.exe" CopyToOutputDirectory="PreserveNewest" />`
- Tests: `dotnet test RAWtoJXL.Tests.csproj`
- GUI tests: `dotnet test RAWtoJXL.Tests.csproj --filter "category=gui"`

## Git LFS

Git LFS manages large binary test fixtures. Required to clone or pull this repository. Run `git lfs install` before clone/pull. `*.ARW` files are tracked (see `.gitattributes`).
