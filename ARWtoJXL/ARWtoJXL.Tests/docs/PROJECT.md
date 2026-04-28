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
│   ├── GUITestHelpers.cs         # Shared helpers: CreateViewModel, CreateWindow, GetAllControls, FindAll, SettingsScope
│   ├── MainWindowStructuralTests.cs     # MainWindow structure: title, buttons, ListBox, ProgressBar, status bar, drag-drop, min size
│   ├── MainWindowBehavioralTests.cs     # MainWindow behavior: ListBox item display, command bindings, drag-drop behavior
│   ├── SettingsWindowTests.cs          # SettingsWindow structure: tabs, buttons, subfolder validation
│   ├── SettingsPersistenceTests.cs     # Settings persistence round-trip: quality, format, effort, metadata, subfolder, conflict, presets
│   └── ConfirmDialogTests.cs           # ConfirmDialog structure and behavior: buttons, message, data context, title binding
└── Services/                     # Empty directory (reserved for future service tests)
```

## Test Configuration

- **Startup**: Central DI configuration base class — calls `services.AddCoreServices()` from `ARWtoJXL.Core`. Tests inherit from `Startup` and resolve services from `Services` property. Provides `CreateScope()` for test isolation. Locates `test1.ARW` test fixture relative to assembly directory.
- **TestAppBuilder**: Avalonia headless application builder registered via `[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]`. Configures `App` with `AvaloniaHeadlessPlatformOptions` for GUI tests.

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

### QualityCalculatorTests
4 test methods, 14 test cases for quality→distance/effort mappings. No DI needed.
- `CalculateEffort_ReturnsCorrectEffort` (Theory x6): quality thresholds at 95, 85, 70, 50, and below
- `IsLossless_ReturnsCorrectValue` (Theory x3): boundary values at 99, 100, 101
- `CalculateDistance_Quality90_ReturnsApprox1`: verifies quality 90 maps to distance ~1.0
- `CalculateDistance_Quality0_ReturnsMaxDistance`: verifies quality 0 maps to maximum distance

### FileLockedExceptionTests
10 test methods, 15 test cases for `FileLockedException.IsFileLocked()` static method. Tests HResult 32 detection, inner IOException unwrapping, message pattern matching ("process cannot access the file", "being used by another process"), null handling, and constructor behavior. No DI needed.

### SubfolderValidationTests
8 test methods, 25 test cases for `SettingsViewModel.ValidateSubfolderName()` static method. Tests empty/whitespace input, valid names, invalid path characters (platform-aware), leading/trailing whitespace, length limits, dot/dotdot names, and reserved Windows names (CON, PRN, AUX, NUL, COM1-9, LPT1-9). No DI needed.

### ImageItemViewModelTests
6 test methods for `ImageItemViewModel.EffectiveQuality()` method. Tests global quality fallback, quality override, zero/100 edge cases, and override clearing. No DI needed.
- `EffectiveQuality_NoOverride_ReturnsGlobalQuality`
- `EffectiveQuality_WithOverride_ReturnsOverride`
- `EffectiveQuality_OverrideZero_ReturnsZero`
- `EffectiveQuality_OverrideHundred_ReturnsHundred`
- `EffectiveQuality_GlobalQualityChanged_ReflectsChange`
- `EffectiveQuality_OverrideSetThenCleared_FallsBackToGlobal`

### CjxlEncoderArgumentsTests
10 test methods, 15 test cases for `CjxlEncoderService.BuildEncodingArguments()` via a `protected internal` test subclass. Tests distance/effort argument generation, lossless vs lossy mode flags, metadata argument omission, input/output path positioning, and effort override. Uses Moq for `ILogger`, `IPathResolver`, `IExiftoolService`.

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

**MainWindowStructuralTests** (10 tests):
- **MainWindow_Opens_And_HasExpectedTitle**: Verifies title is "ARW to JXL Converter"
- **MainWindow_HasToolbarButtons**: Verifies all toolbar buttons exist
- **MainWindow_CancelButton_Hidden_WhenNotConverting**: Verifies cancel button hidden by default
- **MainWindow_HasGalleryListBox**: Verifies `ImagesListBox` with `SelectionMode.Multiple`
- **MainWindow_HasProgressBar**: Verifies at least one progress bar exists
- **MainWindow_HasStatusBarText**: Verifies "Ready" status text
- **MainWindow_HasRecentFilesSection**: Verifies "Recent" section label
- **MainWindow_HasDragDropEnabled**: Verifies root Grid has `DragDrop.AllowDrop=true`
- **MainWindow_DataContext_IsMainViewModel**: Verifies data context type
- **MainWindow_MinSize_IsSet**: Verifies min width 800, min height 600

**MainWindowBehavioralTests** (10 tests):
- **MainWindow_ListBox_DisplaysItemsFromViewModel**: Verifies ListBox reflects ViewModel Images collection
- **MainWindow_ListBox_Items_AreImageItemViewModels**: Verifies ListBox items are `ImageItemViewModel` instances
- **MainWindow_ConvertButton_Command_BoundToConvertSelectedCommand**: Verifies Convert button binding
- **MainWindow_RemoveButton_Command_BoundToRemoveSelectedCommand**: Verifies Remove button binding
- **MainWindow_SelectAllButton_Command_BoundToSelectAllCommand**: Verifies Select All button binding
- **MainWindow_SettingsButton_Command_BoundToOpenSettingsCommand**: Verifies Settings button binding
- **MainWindow_CancelButton_Command_BoundToCancelCommand**: Verifies Cancel button binding
- **MainWindow_StatusBar_BindsToStatusMessage**: Verifies status bar binds to ViewModel StatusMessage
- **MainWindow_DragDropBehavior_EnabledOnRootGrid**: Verifies `DragDropBehavior.EnableDragDrop` and `AllowDrop`
- **MainWindow_DragDropBehavior_HasDropHandlerAttached**: Verifies drag-drop behavior infrastructure

**SettingsWindowTests** (5 tests):
- **SettingsWindow_CreatesSuccessfully**: Verifies window title is "Settings"
- **SettingsWindow_HasFourTabs**: Verifies Conversion/Output/Behavior/Presets tabs exist
- **SettingsWindow_HasSaveAndCancelButton**: Verifies Save and Cancel buttons exist
- **SettingsWindow_SubfolderValidation_HidesWhenValid**: Verifies valid subfolder names pass validation
- **SettingsWindow_SubfolderValidation_ShowsWhenInvalid**: Verifies invalid chars, reserved names, whitespace fail validation

**SettingsPersistenceTests** (10 tests):
Each test follows: open settings → select tab → change UI control → verify settings file → close → reopen → verify value persisted. Uses `SettingsScope` to isolate `settings.json` per test.
- **SettingsWindow_QualityPreset_PersistsAcrossReopens**: Conversion tab — quality slider
- **SettingsWindow_OutputFormat_PersistsAcrossReopens**: Conversion tab — output format ComboBox
- **SettingsWindow_CjxlEffort_PersistsAcrossReopens**: Conversion tab — effort ComboBox
- **SettingsWindow_SkipMetadata_PersistsAcrossReopens**: Conversion tab — skip metadata CheckBox
- **SettingsWindow_UseSubfolder_PersistsAcrossReopens**: Output tab — use subfolder CheckBox
- **SettingsWindow_SubfolderName_PersistsAcrossReopens**: Output tab — subfolder name TextBox
- **SettingsWindow_SearchRecursive_PersistsAcrossReopens**: Output tab — recursive search CheckBox
- **SettingsWindow_ConflictResolution_PersistsAcrossReopens**: Behavior tab — conflict resolution ComboBox
- **SettingsWindow_ConfirmOverwrite_PersistsAcrossReopens**: Behavior tab — confirm overwrite CheckBox
- **SettingsWindow_Preset_SavesAndPersists**: Presets tab — create preset via Save As button

**ConfirmDialogTests** (7 tests):
- **ConfirmDialog_CreatesSuccessfully**: Verifies dialog can be created
- **ConfirmDialog_HasYesAndNoButtons**: Verifies Yes/No buttons exist
- **ConfirmDialog_HasMessageTextBlock**: Verifies message TextBlock with bound text
- **ConfirmDialog_DataContext_IsItself**: Verifies data context is the dialog itself
- **ConfirmDialog_YesButton_HasClickHandler**: Verifies Yes button is `IsDefault`
- **ConfirmDialog_NoButton_HasClickHandler**: Verifies No button is `IsCancel`
- **ConfirmDialog_TitleText_BindsToWindowTitle**: Verifies `TitleText` property binds to window `Title`

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
