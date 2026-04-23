# ARWtoJXL.Tests

xUnit test suite for ARWtoJXL.Core with DI-based integration tests and unit tests.

## Project Structure

```
ARWtoJXL.Tests/
├── Startup.cs                    # DI service configuration (Microsoft.Extensions.DependencyInjection)
├── ConversionTests.cs            # Core conversion tests (inherits Startup, resolves IImageService)
├── MetadataPreservationTests.cs  # Metadata transfer tests (inherits Startup, resolves IMagickService, IImageService)
├── MetadataDebugTests.cs         # Diagnostic test with assertions for metadata preservation (inherits Startup)
├── QualityCalculatorTests.cs     # Unit tests for quality calculations (no DI)
├── SmokeTests.cs                 # FlaUI-based UI smoke tests (launches WPF app, verifies main window elements)
└── Services/                     # Empty directory (reserved for future service tests)
```

## Test Configuration

- **Startup**: Central DI configuration — calls `services.AddCoreServices()` from `ARWtoJXL.Core`. Tests inherit from `Startup` and resolve services from `ServiceProvider`. Provides `CreateScope()` for test isolation.

## Test Suites

### QualityCalculatorTests
12 unit tests for quality→distance/effort mappings. No DI needed.

### ConversionTests
Integration tests with real ARW files (inherits `Startup`):
- Resolves `IImageService` from `ServiceProvider`
- Thumbnail extraction
- Conversion at various quality levels (0, 50, 70, 90, 100)
- Lossless mode verification
- Progress callback verification (smooth updates, monotonic increase, final 1.0)

### MetadataPreservationTests
Metadata-specific tests (inherits `Startup`):
- Resolves `IMagickService` and `IImageService` from `ServiceProvider`
- EXIF transfer verification
- ICC profile preservation
- HasAny property verification
- Metadata at different quality levels (90, 100)

### MetadataDebugTests
Diagnostic test with assertions for full metadata preservation verification (inherits `Startup`):
- Resolves services from `ServiceProvider`
- Extracts metadata from ARW, converts to JXL, verifies 15+ EXIF tags preserved via exiftool
- Uses exiftool `-s -n -Make -Model ...` format for tag-specific reading
- Assertions: minimum 5 matched tags, no missing tags, output has metadata

### SmokeTests
FlaUI-based UI smoke tests (no DI, implements `IDisposable` for cleanup):
- Launches `ARWtoJXL.WPF.exe` as a separate process
- Connects via FlaUI UIA3 automation to the main window
- Verifies window title is "ARW to JXL Converter"
- Verifies toolbar buttons exist: Open File, Select All, Convert, Remove, Settings
- Verifies gallery ListBox (file list) is present
- Verifies progress bar is present
- Cleans up application process on test disposal
- Tagged with `[Trait("category", "smoke")]` — run with `dotnet test --filter "category=smoke"`

## Key Dependencies

- **xUnit**: Unit testing framework (Apache-2.0)
- **Moq**: Mocking framework (BSD-3-Clause)
- **FlaUI.Core** + **FlaUI.UIA3** (4.0.0): Windows UI automation for smoke tests (MIT)
- Depends on `ARWtoJXL.Core` for services under test
- Depends on `ARWtoJXL.WPF` for smoke test target application
