# ARW to JXL Converter

A high-performance Windows desktop application built with .NET 8 and Avalonia UI to convert Sony RAW (.ARW) files to JPEG-XL (.JXL) format.

## Features
- **Drag and Drop:** Drop `.ARW` files or folders directly into the app.
- **Gallery View:** Preview thumbnails of imported files and converted results.
- **Asynchronous Conversion:** Convert multiple files without freezing the UI.
- **Single File Output:** Portable `.exe` deployment.
- **Quality-Preserving Conversion:** Lossless PNG intermediate + Butteraugli-optimized JXL encoding
- **Metadata Preservation:** Automatically extracts and copies EXIF, XMP, ICC, and IPTC metadata from ARW to JXL
  - EXIF: Camera settings, shutter speed, aperture, ISO, GPS coordinates
  - ICC: Color profile for accurate color reproduction
  - XMP: Adobe metadata and editing history
  - IPTC: Copyright, credits, and editorial information (stored as JUMBF in JXL)

## Quality Settings
- **Quality Range:** 0-100 (higher = better quality)
- **Recommended:** 68-96 for most use cases
- **Quality 90+:** Visually lossless (distance ≤1.0)
- **Quality 100:** Mathematically lossless (distance 0.0, uses modular encoding)
- **Dynamic Encoding Effort:** Automatically adjusts based on quality (effort 5-9)
- **Note:** Lossless mode (quality 100) is significantly slower than lossy modes

## Build & Deployment

To build the application as a single-file, trimmed, portable executable, run the following PowerShell script:

```powershell
./build.ps1
```

Alternatively, you can run the command manually:

```powershell
dotnet publish ARWtoJXL/ARWtoJXL.Avalonia/ARWtoJXL.Avalonia.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true
```

## Development
- **Testing:** Run `dotnet test` to execute the xUnit test suite.
- **Tech Stack:** .NET 8, Avalonia UI, MVVM (CommunityToolkit), Magick.NET.
