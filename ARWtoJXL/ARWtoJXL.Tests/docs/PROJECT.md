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

## Key Dependencies

- **xUnit**: Unit testing framework (Apache-2.0)
- **Moq**: Mocking framework (BSD-3-Clause)
- Depends on `ARWtoJXL.Core` for services under test
