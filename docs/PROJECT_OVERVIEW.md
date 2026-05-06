# ARWtoJXL - Project Overview

## Summary

Windows desktop app (.NET 8, Avalonia 12 UI) that converts RAW camera files to JPEG-XL (.JXL), JPEG (.JPG), or PNG (.PNG) formats. Supports Sony (.ARW, .SR2, .SRF), Canon (.CRW, .CR2, .CR3), Nikon (.NEF, .NRW), Fujifilm (.RAF), Olympus/OM System (.ORF), Panasonic (.RW2), and Adobe (.DNG). Uses a multi-stage pipeline: Magick.NET for RAW decoding and metadata extraction + cjxl CLI for JXL encoding with metadata preservation. The JXL pipeline pipes 16-bit RGB PPM data directly to cjxl stdin for zero intermediate disk I/O. Features include recursive folder search, conversion history, per-file quality override, file conflict resolution, custom output directory picker, named conversion presets, per-file compression ratio display, metadata skip toggle, and advanced cjxl options (effort, near-lossless, raw distance).

## Repository Layout

```
ARWtoJPEGXL/
├── .gitignore
├── ARWtoJXL.slnx                          # Solution matrix
├── THIRD-PARTY-NOTICES.md                 # License notices for all dependencies
├── docs/
│   └── PROJECT_OVERVIEW.md               # This file
└── ARWtoJXL/
    ├── build.ps1                          # Build script (restore, download deps, publish)
    ├── cjxl.exe                           # JPEG XL encoder v0.11.2 (downloaded at build time)
    ├── exiftool.exe                       # Metadata tool v13.57 (downloaded at build time)
    ├── exiftool_files/                    # exiftool companion Perl runtime DLLs
    ├── ARWtoJXL.sln
    ├── ARWtoJXL.Core/                     # Business logic layer
    │   └── docs/PROJECT.md               # Core project documentation
    ├── ARWtoJXL.Avalonia/                      # Avalonia UI presentation layer
    │   └── docs/PROJECT.md               # Avalonia project documentation
    └── ARWtoJXL.Tests/                    # xUnit test suite
        └── docs/PROJECT.md               # Tests project documentation
```

## Project Documentation

Each project maintains its own documentation with detailed information on architecture, services, components, and dependencies:

- **ARWtoJXL.Core** — `ARWtoJXL/ARWtoJXL.Core/docs/PROJECT.md`
  - Services, interfaces, DI registration, conversion pipeline, file lock handling, concurrency model, enums

- **ARWtoJXL.Avalonia** — `ARWtoJXL/ARWtoJXL.Avalonia/docs/PROJECT.md`
  - UI/UX flow, view models, UI components, settings, selection logic

- **ARWtoJXL.Tests** — `ARWtoJXL/ARWtoJXL.Tests/docs/PROJECT.md`
  - Test configuration, test suites, DI setup for tests

## Build/Deploy

- `build.ps1` (in `ARWtoJXL/`): checks cjxl.exe exists, downloads exiftool v13.57 if missing (from SourceForge), copies both to publish dir, then `dotnet restore` + `dotnet publish`
- Single-file publish: `dotnet publish -c Release -r win-x64 --self-contained true -p:DebugType=None -p:DebugSymbols=false`
- cjxl.exe + exiftool.exe + exiftool_files/ copied to output via `<None Include="..\*.exe" CopyToOutputDirectory="PreserveNewest" />`
- Tests: `dotnet test ARWtoJXL.Tests.csproj`
- GUI tests: `dotnet test ARWtoJXL.Tests.csproj --filter "category=gui"`

## Git Ignore Policy

Excluded via root `.gitignore`:
- `bin/`, `obj/` — build outputs
- `packages/`, `*.nupkg`, `project.nuget.cache` — NuGet artifacts
- `*.user`, `*.suo`, `*.sln.docstates` — IDE state
- `.idea/`, `*.sln.iml` — Rider artifacts
- `MediaCache/` — ImageMagick cache
- `*.pdb` — debug symbols
- `cjxl_help_*.txt`, `debug_metadata.csx` — temp debug files
- `cjxl.exe` — bundled binary (downloaded at build time)

## Git LFS

Git LFS manages large binary test fixtures. Required to clone or pull this repository.

- **Setup:** `git lfs install` (once per user), then `git clone` / `git pull`
- **Tracked patterns** (`.gitattributes`): `*.ARW`
- **Files:** `ARWtoJXL/ARWtoJXL.Tests/test1.ARW` (~47 MB test fixture)
- **Migrate existing files to LFS:** `git lfs migrate import --include="*.ARW" --include-ref=refs/heads/<branch>`

## License Compliance

- `THIRD-PARTY-NOTICES.md` contains license texts for all dependencies
- **GPL-3.0**: exiftool.exe is GPL-3.0; bundled in repo with license notice in THIRD-PARTY-NOTICES.md
