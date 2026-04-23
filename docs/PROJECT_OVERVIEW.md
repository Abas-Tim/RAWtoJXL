# ARWtoJXL - Project Overview

## Summary

Windows desktop app (.NET 8 WPF) that converts Sony RAW (.ARW) camera files to JPEG-XL (.JXL), JPEG (.JPG), or PNG (.PNG) formats. Uses a multi-stage pipeline: Magick.NET for RAW decoding and metadata extraction + cjxl CLI for JXL encoding with metadata preservation. Features include recursive folder search, conversion history, per-file quality override, and file conflict resolution.

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
    ├── exiftool.exe                       # Metadata tool v13.56 (downloaded at build time)
    ├── ARWtoJXL.sln
    ├── ARWtoJXL.Core/                     # Business logic layer
    │   └── docs/PROJECT.md               # Core project documentation
    ├── ARWtoJXL.WPF/                      # WPF presentation layer
    │   └── docs/PROJECT.md               # WPF project documentation
    └── ARWtoJXL.Tests/                    # xUnit test suite
        └── docs/PROJECT.md               # Tests project documentation
```

## Project Documentation

Each project maintains its own documentation with detailed information on architecture, services, components, and dependencies:

- **ARWtoJXL.Core** — `ARWtoJXL/ARWtoJXL.Core/docs/PROJECT.md`
  - Services, interfaces, DI registration, conversion pipeline, file lock handling, concurrency model, enums

- **ARWtoJXL.WPF** — `ARWtoJXL/ARWtoJXL.WPF/docs/PROJECT.md`
  - UI/UX flow, view models, UI components, settings, keyboard shortcuts, selection logic

- **ARWtoJXL.Tests** — `ARWtoJXL/ARWtoJXL.Tests/docs/PROJECT.md`
  - Test configuration, test suites, DI setup for tests

## Build/Deploy

- `build.ps1` (in `ARWtoJXL/`): checks cjxl.exe, downloads exiftool if missing, copies both to publish dir, then `dotnet restore` + `dotnet publish`
- Single-file publish: `dotnet publish -c Release -r win-x64 --self-contained true -p:DebugType=None -p:DebugSymbols=false`
- cjxl.exe + exiftool.exe copied to output via `<None Include="..\*.exe" CopyToOutputDirectory="PreserveNewest" />`
- Tests: `dotnet test ARWtoJXL.Tests.csproj`

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

## License Compliance

- `THIRD-PARTY-NOTICES.md` contains license texts for all dependencies
- **GPL-3.0**: exiftool.exe is GPL-3.0; bundled in repo with license notice in THIRD-PARTY-NOTICES.md
