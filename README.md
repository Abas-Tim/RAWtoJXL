# RAWtoJXL

Convert RAW camera files to JPEG-XL, JPEG, or PNG — with a fast, modern desktop UI.

Built on .NET 8 and Avalonia. Uses Magick.NET for RAW decoding, `cjxl` for JPEG-XL encoding, and `exiftool` for metadata preservation.

## Why JPEG-XL?

JPEG-XL is a next-generation image format that outperforms JPEG, WebP, AVIF, and PNG across the board:

| Source format | Typical RAW size | JXL (quality 90) | Size saved |
|---|---|---|---|
| Sony ARW (24MP) | ~25 MB | ~3–5 MB | **up to 85%** |
| Canon CR3 (30MP) | ~15 MB | ~2–4 MB | **up to 80%** |
| Nikon NEF (45MP) | ~45 MB | ~6–10 MB | **up to 80%** |
| Fujifilm RAF (26MP) | ~20 MB | ~3–5 MB | **up to 80%** |
| Adobe DNG (raw) | ~30 MB | ~4–7 MB | **up to 85%** |

At quality 90 (visually lossless), JXL files are typically **4–10× smaller** than the original RAW while preserving perceptual quality. Switch to lossless mode (quality 100) and JXL still beats DNG by a wide margin.

JXL also supports:
- **Up to 32-bit per channel** — no precision loss from 12/14-bit RAW
- **Wide-gamut & HDR** — native color space handling
- **Progressive loading** — preview from 1% of the file
- **Lossless transcoding** — reversible conversion back to JPEG
- **CMYK & print** — full creative workflow support

## Supported Formats

### Input (RAW)
Sony `.ARW` / `.SR2` / `.SRF` · Canon `.CRW` / `.CR2` / `.CR3` · Nikon `.NEF` / `.NRW` · Fujifilm `.RAF` · Olympus / OM System `.ORF` · Panasonic `.RW2` · Adobe `.DNG`

### Output
`.JXL` (JPEG-XL) · `.JPG` (JPEG) · `.PNG` (16-bit lossless)

## Features

- **Drag-and-drop** files and folders — recursive folder scanning
- **Per-file quality override** — global preset with individual sliders
- **Batch conversion** with live progress and file-level compression ratio
- **Metadata preservation** — EXIF, XMP, ICC, IPTC copied via `exiftool`
- **Fast thumbnails** — reads embedded EXIF previews when available (zero decode)
- **Named presets** — save and load conversion profiles
- **Custom output directory** — pick any destination, optional subfolder
- **Conflict resolution** — overwrite, skip, or auto-rename
- **Advanced cjxl options** — effort (1–9), thread count, near-lossless mode
- **Cancel anytime** — graceful cancellation mid-batch
- **Recent files** — quick-access list of last 50 files

## Screenshot

<img width="1502" height="1165" alt="image" src="https://github.com/user-attachments/assets/fa4e747d-ef40-4554-b1d3-f891be82fa7c" />
<img width="719" height="614" alt="image" src="https://github.com/user-attachments/assets/6c240378-7da6-4d03-8dfd-7f8753b31ae5" />


## Quick Start

### Build

```powershell
cd RAWtoJXL
./build.ps1
```

The build script downloads `cjxl.exe` and `exiftool` if missing, restores NuGet packages, and publishes a self-contained Windows executable.

### Run

```powershell
dotnet run --project RAWtoJXL/RAWtoJXL.Avalonia
```

### Test

```powershell
dotnet test RAWtoJXL/RAWtoJXL.Tests
```

## Conversion Pipeline

```
RAW file
  │
  ├─ Thumbnail:  exiftool PreviewImage → embedded JPEG (zero decode)
  │
  ├─ JXL:        Magick.NET streams 16-bit PPM → cjxl stdin (zero disk I/O)
  │              └─ exiftool embeds metadata from source
  │
  ├─ JPEG:       Magick.NET RAW → JPEG at chosen quality
  │              └─ exiftool embeds metadata from source
  │
  └─ PNG:        Magick.NET RAW → 16-bit PNG (lossless)
                 └─ exiftool embeds metadata from source
```

The JXL pipeline pipes 16-bit RGB PPM data directly to `cjxl` stdin — no intermediate files, single file open, ~4 MB RAM overhead for a 24 MP image.

## Settings

All settings persist to `%APPDATA%\RAWtoJXL\settings.json`. Configure:

- **Conversion** — quality, output format, skip metadata toggle
- **Output** — custom directory, subfolder name, conflict resolution
- **Behavior** — recursive search, overwrite confirmation
- **Hardware** — cjxl effort (1–9), thread count
- **Presets** — named profiles for one-click conversion

## Architecture

```
RAWtoJXL/
├── RAWtoJXL.Core/          Business logic: conversion pipeline, services, DI
├── RAWtoJXL.Avalonia/      Desktop UI: MVVM, drag-drop, settings, gallery
└── RAWtoJXL.Tests/         xUnit tests: unit + GUI tests
```

Each project documents its internals in `docs/PROJECT.md`. See `docs/PROJECT_OVERVIEW.md` for the full repository layout.

## Dependencies

| Dependency | Role | License |
|---|---|---|
| .NET 8 | Runtime | MIT |
| Avalonia 12 | UI framework | MIT |
| Magick.NET-Q16-AnyCPU | RAW decoding, image conversion | Apache-2.0 |
| cjxl (libjxl 0.11.2) | JPEG-XL encoding | BSD-3-Clause |
| exiftool 13.57 | Metadata extraction & embedding | Artistic-2.0 |
| CommunityToolkit.Mvvm | MVVM helpers | MIT |

## License

See [LICENSE](LICENSE) for details. Third-party notices in [THIRD-PARTY-NOTES.md](THIRD-PARTY-NOTICES.md).
