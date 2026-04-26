# ARWtoJXL.Tests

xUnit test suite for ARWtoJXL.Core with DI-based integration tests and unit tests.

## Project Structure

```
ARWtoJXL.Tests/
├── Startup.cs                    # DI service configuration (Microsoft.Extensions.DependencyInjection)
├── ConversionTests.cs            # Core conversion tests (inherits Startup, resolves IImageService)
├── MetadataPreservationTests.cs  # Metadata transfer test (inherits Startup, resolves IImageConverterService, IImageService)
├── MetadataDebugTests.cs         # Diagnostic test for metadata preservation (inherits Startup, manual-only)
├── QualityCalculatorTests.cs     # Unit tests for quality calculations (no DI)
├── FileLockedExceptionTests.cs   # Unit tests for IsFileLocked() detection logic (Moq, no DI)
├── SubfolderValidationTests.cs   # Unit tests for SettingsViewModel.ValidateSubfolderName() (no DI)
├── RawDistanceValidationTests.cs # Unit tests for SettingsViewModel.ValidateRawDistance() (no DI)
├── ImageItemViewModelTests.cs    # Unit tests for EffectiveQuality fallback logic (no DI)
├── CjxlEncoderArgumentsTests.cs  # Unit tests for BuildEncodingArguments() via protected internal test subclass (Moq)
├── SmokeTests.cs                 # Avalonia Headless UI smoke tests (headless MainWindow, MainViewModel, command tests)
└── Services/                     # Empty directory (reserved for future service tests)
```

## Test Configuration

- **Startup**: Central DI configuration — calls `services.AddCoreServices()` from `ARWtoJXL.Core`. Tests inherit from `Startup` and resolve services from `ServiceProvider`. Provides `CreateScope()` for test isolation.

## Running Tests

```bash
# All tests except smoke and manual (default)
dotnet test --filter "category!=smoke&category!=manual"

# Include smoke tests
dotnet test --filter "category!=manual"

# Run only unit tests (fast, ~0.4s)
dotnet test --filter "FullyQualifiedName~QualityCalculatorTests|FullyQualifiedName~FileLockedExceptionTests|FullyQualifiedName~SubfolderValidationTests|FullyQualifiedName~RawDistanceValidationTests|FullyQualifiedName~ImageItemViewModelTests|FullyQualifiedName~CjxlEncoderArgumentsTests"
```

## Test Suites

### QualityCalculatorTests
12 unit tests for quality→distance/effort mappings. No DI needed.

### FileLockedExceptionTests
10 unit tests for `FileLockedException.IsFileLocked()` static method. Tests HResult 32 detection, inner IOException unwrapping, message pattern matching ("process cannot access the file", "being used by another process"), null handling, and constructor behavior. No DI needed.

### SubfolderValidationTests
13 unit tests for `SettingsViewModel.ValidateSubfolderName()` static method. Tests empty/whitespace input, valid names, invalid path characters (platform-aware), leading/trailing whitespace, length limits, dot/dotdot names, and reserved Windows names (CON, PRN, AUX, NUL, COM1-9, LPT1-9). No DI needed.

### RawDistanceValidationTests
7 unit tests for `SettingsViewModel.ValidateRawDistance()` static method. Tests empty/whitespace input (returns null), valid numeric values (0.0-150.0), negative values, too-high values, and non-numeric strings. No DI needed.

### ImageItemViewModelTests
6 unit tests for `ImageItemViewModel.EffectiveQuality()` method. Tests global quality fallback, quality override, zero/100 edge cases, and override clearing. Also covers `SizeInfoText` computed property for compression ratio display. No DI needed.

### CjxlEncoderArgumentsTests
15 unit tests for `CjxlEncoderService.BuildEncodingArguments()` via a `protected internal` test subclass. Tests distance/effort argument generation, lossless vs lossy mode flags, metadata argument omission, input/output path positioning, effort override, and raw distance override. Uses Moq for `ILogger`, `IPathResolver`, `IExiftoolService`.

### ConversionTests
Integration tests with real ARW files (inherits `Startup`):
- Resolves `IImageService` from `ServiceProvider`
- Thumbnail extraction (valid ARW, invalid file)
- Conversion at various quality levels via Theory (0, 50, 70, 90, 100)
- Progress callback verification (smooth updates, monotonic increase, final 1.0)

### MetadataPreservationTests
Single metadata preservation test (inherits `Startup`):
- Resolves `IImageConverterService` and `IImageService` from `ServiceProvider`
- Converts ARW to JXL at quality 90
- Verifies EXIF profile transferred and non-empty
- Verifies ICC profile preserved and non-empty
- Verifies HasAny property on both input and output

### MetadataDebugTests
Diagnostic test tagged with `[Trait("category", "manual")]` — does NOT run by default:
- Resolves services from `ServiceProvider`
- Extracts metadata from ARW, converts to JXL, verifies 15+ EXIF tags preserved via exiftool
- Uses exiftool `-s -n -Make -Model ...` format for tag-specific reading
- Assertions: minimum 5 matched tags, no missing tags, output has metadata
- Run with: `dotnet test --filter "category=manual"`

### SmokeTests
Avalonia Headless UI smoke tests (no DI, implements `IDisposable` for cleanup):
- Uses `Avalonia.Headless` to launch `App` in headless session
- Mocks `IDialogService`, `IFilePickerService`, `IDispatcherService`
- Creates `MainViewModel` and `MainWindow` directly in headless context
- Verifies window title is "ARW to JXL Converter"
- Verifies toolbar buttons exist: Open File, Select All, Convert, Remove, Cancel, Open Output Folder, Settings, Load All, Clear
- Verifies gallery ListBox (`ImagesListBox`) with `SelectionMode.Multiple`
- Verifies progress bar, status bar text ("Ready"), recent files section
- **AddFiles_AddsItemsToGallery**: Creates temp .arw files, adds via `MainViewModel.AddFilesAsync`, verifies count, filename, status
- **AddFiles_SkipsDuplicates**: Verifies duplicate files are not added
- **AddFiles_SkipsInvalidExtensions**: Verifies non-.arw/.jxl files are rejected
- **SelectAll_SelectsAllItems**: Verifies `SelectAllCommand` selects all items and sets `IsAllSelected`/`IsAnySelected`
- **RemoveSelected_RemovesItems**: Verifies `RemoveSelectedCommand` removes items, clears selection, updates status message
- **RemoveSelected_DoesNotCrashApp**: Regression test for race condition with background thumbnail generation
- **ClearRecentFiles_ClearsList**: Verifies `ClearRecentFilesCommand` clears recent files
- **SettingsWindow_CreatesSuccessfully**: Verifies `SettingsWindow` can be created in headless mode
- **SettingsWindow_HasExpectedControls**: Verifies settings window has Save/Cancel/Browse/Load/Save As/Delete buttons, sliders, checkboxes, comboboxes
- **ConfirmDialog_CreatesSuccessfully**: Verifies `ConfirmDialog` can be created
- **ViewModel_Commands_AreInitiallyDisabled**: Verifies `RemoveSelectedCommand` and `ConvertSelectedCommand` are disabled with no selection
- **ViewModel_RemoveCommand_Enabled_AfterSelection**: Verifies `RemoveSelectedCommand` becomes enabled after selection
- **ImageItem_EffectiveQuality_UsesOverride**: Verifies `ImageItemViewModel.EffectiveQuality()` uses per-file override
- **ImageItem_EffectiveQuality_FallsBackToGlobal**: Verifies `ImageItemViewModel.EffectiveQuality()` falls back to global quality
- Tagged with `[Trait("category", "smoke")]` — run with `dotnet test --filter "category=smoke"`

## Key Dependencies

- **xUnit**: Unit testing framework (Apache-2.0)
- **Moq**: Mocking framework (BSD-3-Clause)
- **Avalonia.Headless** (12.0.1): Headless UI testing for Avalonia (MIT)
- Depends on `ARWtoJXL.Core` for services under test
- Depends on `ARWtoJXL.Avalonia` for smoke test target application
