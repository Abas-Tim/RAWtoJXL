# RAWtoJXL

Windows desktop app (.NET 8, Avalonia 12) converting RAW camera files to JPEG-XL, JPEG, or PNG.

## Documentation

- **Project overview**: `../docs/PROJECT_OVERVIEW.md`
- **Core services**: `RAWtoJXL.Core/docs/PROJECT.md`
- **Avalonia UI**: `RAWtoJXL.Avalonia/docs/PROJECT.md`
- **Tests**: `RAWtoJXL.Tests/docs/PROJECT.md`

## Quick Start

```powershell
# Build
./build.ps1

# Run tests
dotnet test

# Run GUI tests
dotnet test --filter "category=gui"
```

## Supported Formats

**Input:** Sony (.ARW, .SR2, .SRF), Canon (.CR2, .CR3), Nikon (.NEF, .NRW), Fujifilm (.RAF), Olympus (.ORF), Panasonic (.RW2), Adobe (.DNG)

**Output:** JPEG-XL (.JXL), JPEG (.JPG), PNG (.PNG)