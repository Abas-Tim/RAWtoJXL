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

**MainWindowStructuralTests** (15 tests):
- **MainWindow_Opens_And_HasExpectedTitle**: Verifies title is "ARW to JXL Converter"
- **MainWindow_HasToolbarButtons**: Verifies all toolbar buttons exist
- **MainWindow_HasFileMenuItems**: Verifies menu headers: File, Open File, Open Folder, Load All, Clear Recent, List, Remove
- **MainWindow_CancelButton_Hidden_WhenNotConverting**: Verifies cancel button hidden by default
- **MainWindow_HasGalleryRepeater**: Verifies `ImagesRepeater` is an `ItemsRepeater` with `UniformGridLayout`
- **MainWindow_GalleryRepeater_HasItemsSourceBinding**: Verifies ItemsRepeater renders items from Images collection via TryGetElement
- **MainWindow_HasProgressBar**: Verifies at least one progress bar exists
- **MainWindow_ProgressBar_BoundToViewModel**: Verifies Main progress bar Maximum/Value reflects TotalCount/CompletedCount
- **MainWindow_HasStatusBarText**: Verifies "Ready" status text
- **MainWindow_HasRecentFilesInMenu**: Verifies Load All and Clear Recent menu items
- **MainWindow_HasDragDropEnabled**: Verifies root Grid has `DragDrop.AllowDrop=true`
- **MainWindow_DataContext_IsMainViewModel**: Verifies data context type
- **MainWindow_MinSize_IsSet**: Verifies min width 800, min height 600
- **MainWindow_InitialLayout_DoesNotThrow**: Verifies window layouts without exceptions
- **MainWindow_ConvertButton_HasAccentClass**: Verifies Convert button has `accent` CSS class
- **MainWindow_SelectAllMenuHeader_CommandBound**: Verifies SelectAll menu item is bound to command

**MainWindowBehavioralTests** (14 tests):
- **MainWindow_SelectAll_TogglesItemSelection**: Executes SelectAll command, verifies IsAllSelected becomes true and all items selected
- **MainWindow_RemoveSelected_RemovesItems**: Selects items, executes RemoveSelected, verifies Images count decreases
- **MainWindow_SettingsButton_RaisesRequestOpenSettings**: Executes OpenSettings command, verifies RequestOpenSettings event fires
- **MainWindow_ConvertButton_InvokesConversion**: Creates temp ARW file with mock IImageService, executes ConvertSelected, verifies ConvertArwToJxlAsync called and status becomes Converted
- **MainWindow_StatusMessage_UpdatesUI**: Sets StatusMessage on VM, verifies TextBlock in UI reflects the new text
- **MainWindow_CancelButton_VisibleWhenConverting**: Sets IsConverting=true on VM, verifies Cancel button becomes visible
- **MainWindow_Gallery_RendersItemElements**: Adds images to VM, verifies ItemsRepeater.TryGetElement returns non-null for multiple indices
- **MainWindow_Gallery_RenderedItemsHaveCorrectDataContext**: Verifies rendered ItemsRepeater elements have ImageItemViewModel DataContext matching VM collection
- **MainWindow_Gallery_UpdatesWhenImagesAddedOrRemoved**: Removes item from Images, verifies ItemsRepeater TryGetElement still returns correct elements
- **MainWindow_ConvertButton_DisabledWithoutSelection**: Verifies ConvertSelectedCommand.CanExecute returns false with no selection; returns true after selecting an item
- **MainWindow_DragDropBehavior_EnabledOnRootGrid**: Verifies DragDropBehavior.EnableDragDrop and AllowDrop attached properties are true on root Grid
- **MainWindow_PerItemCheckBox_UpdatesSelectionState**: Sets CheckBox.IsChecked in ItemsRepeater item template, verifies VM.Image.IsSelected and IsAnySelected update
- **MainWindow_QualitySlider_UpdatesQualityOverride**: Sets Slider.Value in ItemsRepeater item template, verifies VM.Image.QualityOverride updates
- **MainWindow_ItemOpenFolderButton_VisibilityUpdatesWithOutputPath**: Verifies "Open folder" button hidden when OutputPath empty, visible when set

**SettingsWindowTests** (16 tests):
- **SettingsWindow_CreatesSuccessfully**: Verifies window title is "Settings"
- **SettingsWindow_HasFiveTabs**: Verifies Conversion/Output/Behavior/Hardware/Presets tabs exist
- **SettingsWindow_HasSaveAndCancelButton**: Verifies Save and Cancel buttons exist
- **SettingsWindow_SaveCommand_PersistsAndCloses**: Executes SaveCommand, verifies QualityPreset persisted and window closes
- **SettingsWindow_CancelCommand_ClosesWindow**: Executes CancelCommand, verifies window closes
- **SettingsWindow_QualitySlider_UpdatesQualityPreset**: Sets slider.Value, verifies QualityPreset updates
- **SettingsWindow_QualitySlider_ClampedBySliderRange**: Verifies slider Minimum=0, Maximum=100
- **SettingsWindow_OutputFormat_UpdatesOnSelection**: Selects OutputFormat.Png in ComboBox, verifies VM.OutputFormat changes
- **SettingsWindow_OutputFormat_HasAllOptions**: Verifies ComboBox contains Jxl, Jpeg, Png
- **SettingsWindow_SubfolderValidation_HidesWhenValid**: Verifies valid subfolder names pass validation (return null)
- **SettingsWindow_SubfolderValidation_ShowsWhenInvalid**: Verifies invalid chars, reserved names, whitespace fail validation
- **SettingsWindow_SubfolderValidation_UpdatesThroughBinding**: Sets TextBox.Text to valid name, verifies ValidationResult is null
- **SettingsWindow_TabSwitch_LoadsDifferentContent**: Switches between Conversion and Output tabs, verifies different controls present
- **SettingsWindow_CjxlEffort_UpdatesOnSelection**: Selects effort=7 in ComboBox, verifies VM.CjxlEffort = 7
- **SettingsWindow_CjxlEffort_HasCorrectOptions**: Verifies effort ComboBox has Auto, 1-9 options
- **SettingsWindow_SkipMetadata_TogglesOnVM**: Toggles SkipMetadata property, verifies value changes

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

**ConfirmDialogTests** (10 tests):
- **ConfirmDialog_CreatesSuccessfully**: Verifies dialog can be created
- **ConfirmDialog_HasYesAndNoButtons**: Verifies Yes/No buttons exist
- **ConfirmDialog_HasMessageTextBlock**: Verifies message TextBlock renders MessageText
- **ConfirmDialog_DataContext_IsItself**: Verifies data context is ConfirmDialogViewModel
- **ConfirmDialog_YesButton_Click_ClosesDialog**: Clicks Yes button (RaiseEvent on ClickEvent), verifies dialog Closed event fires
- **ConfirmDialog_NoButton_Click_ClosesDialog**: Clicks No button (RaiseEvent on ClickEvent), verifies dialog Closed event fires
- **ConfirmDialog_YesButton_IsDefault**: Verifies Yes button IsDefault = true
- **ConfirmDialog_NoButton_IsCancel**: Verifies No button IsCancel = true
- **ConfirmDialog_TitleText_BindsToWindowTitle**: Verifies TitleText property propagates to dialog Title
- **ConfirmDialog_MessageText_BindsToDataContext**: Verifies MessageText property propagates to inner ViewModel.MessageText

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
