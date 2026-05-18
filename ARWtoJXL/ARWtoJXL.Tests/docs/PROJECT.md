# ARWtoJXL.Tests

xUnit v3 test suite for ARWtoJXL.Core with DI-based integration tests and unit tests.

## Project Structure

```
ARWtoJXL.Tests/
├── Startup.cs                    # DI service configuration base class (Microsoft.Extensions.DependencyInjection)
├── TestAppBuilder.cs             # Avalonia headless app builder ([assembly: AvaloniaTestApplication])
├── ConversionTests.cs            # Core conversion tests (inherits Startup, resolves IImageService)
├── MetadataPreservationTests.cs  # Metadata transfer test (inherits Startup, resolves IImageConverterService, IImageService)
├── MetadataDebugTests.cs         # Diagnostic test for metadata preservation (inherits Startup, manual-only)
├── QualityCalculatorTests.cs     # Unit tests for quality calculations (no DI)
├── FileLockedExceptionTests.cs   # Unit tests for IsFileLocked() detection logic (Moq, no DI)
├── SubfolderValidationTests.cs   # Unit tests for SettingsViewModel.ValidateSubfolderName() (no DI)
├── ImageItemViewModelTests.cs    # Unit tests for EffectiveQuality fallback logic (no DI)
├── CjxlEncoderArgumentsTests.cs  # Unit tests for BuildEncodingArguments() via protected internal test subclass (Moq)
├── test1.ARW                     # Test fixture ARW file for integration tests
├── GUITests/                     # Avalonia Headless GUI tests (split by category)
│   ├── GUITestHelpers.cs         # Shared helpers: CreateViewModel, CreateWindow, GetAllControls, FindAll, SettingsScope, AddTestFiles
│   ├── MainWindowStructuralTests.cs     # MainWindow structure + functional structural checks: title, buttons, ItemsRepeater binding, ProgressBar binding, drag-drop, min size, layout
│   ├── MainWindowBehavioralTests.cs     # MainWindow functional behavior: SelectAll toggling, RemoveSelected removal, Settings event, Convert pipeline with mock, StatusMessage→UI binding, Cancel visibility, gallery rendering, drag-drop infrastructure, CheckBox→selection binding, quality slider
│   ├── SettingsWindowTests.cs          # SettingsWindow structure and behavior: tabs, buttons, Save/Cancel commands, quality slider, output format, subfolder validation, tab switching, cjxl effort, skip metadata
│   ├── SettingsPersistenceTests.cs     # Settings persistence round-trip: quality, format, effort, metadata, subfolder, conflict, presets
│   └── ConfirmDialogTests.cs           # ConfirmDialog structure and behavior: Yes/No click closes dialog, message binding, data context, title binding
└── Services/                     # Empty directory (reserved for future service tests)
```

## Test Configuration

- **Startup**: Central DI configuration base class — calls `services.AddCoreServices()` from `ARWtoJXL.Core`. Tests inherit from `Startup` and resolve services from `Services` property. Provides `CreateScope()` for test isolation. Locates `test1.ARW` test fixture relative to assembly directory.
- **TestAppBuilder**: Avalonia headless application builder registered via `[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]`. Configures `App` with `AvaloniaHeadlessPlatformOptions` for GUI tests.
- **ConversionTestCollection**: `[CollectionDefinition("Conversion")]` ensures `ConversionTests`, `MetadataPreservationTests`, and `MetadataDebugTests` run sequentially, preventing multiple cjxl processes from competing for CPU cores.

## Running Tests

```bash
# All tests except GUI and manual (default)
dotnet test --filter "category!=gui&category!=manual"

# Include GUI tests
dotnet test --filter "category!=manual"

# Run only unit tests (fast, ~0.4s)
dotnet test --filter "FullyQualifiedName~QualityCalculatorTests|FullyQualifiedName~FileLockedExceptionTests|FullyQualifiedName~SubfolderValidationTests|FullyQualifiedName~ImageItemViewModelTests|FullyQualifiedName~CjxlEncoderArgumentsTests"
```

## Test Suites

### Unit Tests (no DI, fast)

| Suite | Methods | Cases | Target |
|-------|---------|-------|--------|
| QualityCalculatorTests | 4 | 14 | quality→distance/effort mappings |
| FileLockedExceptionTests | 10 | 15 | `IsFileLocked()` — HResult 32, inner IOException, message patterns |
| SubfolderValidationTests | 8 | 25 | `ValidateSubfolderName()` — path chars, reserved names, length limits |
| ImageItemViewModelTests | 6 | 6 | `EffectiveQuality()` — global fallback, override, edge cases |
| CjxlEncoderArgumentsTests | 10 | 15 | `BuildEncodingArguments()` via test subclass — Moq for ILogger/IPathResolver/IExiftoolService |

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

### GUITests
Avalonia Headless GUI tests (no DI, mocks services). Split into `GUITests/` folder by category. Shared utilities in `GUITestHelpers.cs` (`CreateViewModel`, `CreateWindow`, `GetAllControls`, `FindAll`, `SettingsScope`). All tagged `[Trait("category", "gui")]`.

**MainWindowStructuralTests** (15 tests): Window opens with expected title, toolbar buttons, File/List menu items, gallery ItemsRepeater with UniformGridLayout, progress bar bound to TotalCount/CompletedCount, status bar, drag-drop enabled, min size 800x600, cancel button hidden by default, Convert button has accent class, SelectAll command bound.

**MainWindowBehavioralTests** (14 tests): SelectAll toggling, RemoveSelected removal, Settings event firing, Convert pipeline with mock IImageService, StatusMessage→UI binding, Cancel visibility during conversion, gallery rendering/data context/updates, Convert disabled without selection, drag-drop behavior on root Grid, CheckBox→selection binding, quality slider→QualityOverride binding, per-item Open Folder button visibility.

**SettingsWindowTests** (16 tests): Window creates with title "Settings", 5 tabs (Conversion/Output/Behavior/Hardware/Presets), Save/Cancel buttons, quality slider updates/clamped 0-100, output format ComboBox with Jxl/Jpeg/Png, subfolder validation (valid/invalid/binding), tab switching, cjxl effort ComboBox (Auto, 1-9), skip metadata toggle.

**SettingsPersistenceTests** (10 tests): Round-trip persistence via `SettingsScope` isolation. Tests: quality, format, effort, skip metadata, subfolder, recursive search, conflict resolution, confirm overwrite, preset save.

**ConfirmDialogTests** (10 tests): Dialog creates, Yes/No buttons exist and close dialog, message/title binding, DataContext is ConfirmDialogViewModel, Yes is Default, No is Cancel.

## Key Dependencies

- **xunit.v3** (3.2.2): Test framework (Apache-2.0)
- **xunit.runner.visualstudio** (3.1.5): Visual Studio test runner adapter
- **Microsoft.NET.Test.Sdk** (17.14.1): Test SDK infrastructure
- **Moq** (4.20.72): Mocking framework (BSD-3-Clause)
- **Avalonia** (12.0.1): UI framework for GUI tests (MIT)
- **Avalonia.Headless.XUnit** (12.0.1): Headless UI testing for Avalonia (MIT)
- **coverlet.collector** (6.0.4): Code coverage collection
- **Microsoft.Extensions.DependencyInjection** (8.0.1): DI container for test setup (MIT)
- Depends on `ARWtoJXL.Core` for services under test
- Depends on `ARWtoJXL.Avalonia` for GUI test target application
