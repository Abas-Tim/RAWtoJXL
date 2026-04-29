# ARWtoJXL.Avalonia

Avalonia UI presentation layer implementing the desktop app with MVVM pattern, drag-drop support, and conversion management.

## Project Structure

```
ARWtoJXL.Avalonia/
├── App.axaml + App.cs                       # Application entry point with DI container setup
├── AppStrings.cs                            # Shared string resource constants (26 string constants)
├── MainWindow.axaml + MainWindow.axaml.cs   # Main window with File/List dropdown menus, toolbar (Convert, Cancel, Open Output Folder), Settings, file cards (per-file quality slider), status bar
├── SettingsWindow.axaml + SettingsWindow.axaml.cs # Resizable tabbed settings dialog (Conversion, Output, Behavior, Presets tabs)
├── SettingsService.cs                       # Settings persistence (JSON-based), AppSettings/ConversionPreset models, ConflictResolution enum
├── ViewLocator.cs                           # IDataTemplate implementation for MVVM view-model to view mapping
├── Program.cs                               # App bootstrap with Avalonia app builder
├── app.manifest                             # Application manifest (requestedExecutionLevel, Windows OS compatibility)
├── Behaviors/
│   └── DragDropBehavior.cs                  # Avalonia attached property for drag-drop file/folder handling
├── Converters/
│   ├── BooleanToBrushConverter.cs           # Bool to SolidColorBrush converter (declared in XAML, currently unused)
│   ├── BooleanToTextConverter.cs            # Bool to text converter (used for Select All/Deselect All menu item toggle)
│   ├── BooleanToValueConverter.cs           # Bool to value converter (supports "Invert", "InvertContent", "DefaultIfZero" parameters)
│   ├── ImageStatusToStringConverter.cs      # ImageStatus enum to string converter
│   ├── ImageStatusToVisibilityConverter.cs  # ImageStatus to bool (IsVisible) converter
│   ├── IntToDoubleConverter.cs              # int? to double converter (slider binding)
│   ├── NullableIntConverter.cs              # Nullable int converter
│   ├── StringToIntConverter.cs              # String to int converter (ComboBox Tag binding)
│   └── StringToVisibilityConverter.cs       # String to bool (IsVisible) converter
├── Services/
│   ├── ConfirmDialog.axaml + ConfirmDialog.axaml.cs # Confirmation dialog window (MessageText, TitleText properties)
│   ├── DialogService.cs                     # IDialogService implementation
│   ├── DispatcherService.cs                 # IDispatcherService implementation (Avalonia Dispatcher.UIThread)
│   ├── FilePickerService.cs                 # IFilePickerService implementation (Avalonia storage APIs)
│   ├── IDialogService.cs                    # Dialog service interface
│   ├── IDispatcherService.cs                # Dispatcher service interface
│   └── IFilePickerService.cs                # File picker service interface
├── ViewModels/
│   ├── ImageItemViewModel.cs                # View model for image items (ObservableProperty, Bitmap thumbnail, QualityOverride)
│   ├── MainViewModel.cs                     # Main view model with IFilePickerService injection, auto-persists settings on change
│   └── SettingsViewModel.cs                 # Settings view model with presets, validation, debounced auto-persist (500ms) via OnPropertyChanged
└── docs/
    └── PROJECT.md                           # This file
```

- For any unknowns refer to API reference https://docs.avaloniaui.net/api#namespaces and from there to related topics

## Architecture

**MVVM Pattern** with CommunityToolkit.Mvvm (`[ObservableProperty]`, `[RelayCommand]`, `[INotifyPropertyChanged]`).

**Dependency Injection** via Microsoft.Extensions.DependencyInjection. Services registered in `App.cs`:
- `IDispatcherService` → `DispatcherService` (singleton)
- `IDialogService` → `DialogService` (singleton)
- `IFilePickerService` → `FilePickerService` (singleton)
- ViewModels resolved per-instance with injected services
- `App.Services` exposes the `IServiceProvider` as a static property for runtime resolution (e.g., `SettingsWindow` resolves `IFilePickerService` from it)

**XAML Loading**: Uses `InitializeComponent()` in constructors. `AvaloniaXamlLoader.Load(this)` replaced for Avalonia 12 compatibility.

**ViewLocator**: Implements `IDataTemplate` to automatically map view models to views. `Match()` only matches types from `.ViewModels` namespace to avoid intercepting strings and other primitives. `Build()` replaces `.ViewModels` with `.Views` and strips "ViewModel" suffix from type name. Falls back to "Not Found" TextBlock if type not found. No `Views/` folder exists in the project.

**Compiled Bindings**: Disabled (`<AvaloniaUseCompiledBindingsByDefault>false</AvaloniaUseCompiledBindingsByDefault>`) due to incompatibility with CommunityToolkit.Mvvm generated command properties.

**InternalsVisibleTo**: Set for `ARWtoJXL.Tests` to allow GUI test access to internal members (e.g., `HeadlessTestMode`).

## Key Features

- **Drag-Drop**: Single attached property `DragDropBehavior.EnableDragDrop` on root Grid. Internally wires `DragDrop.SetAllowDrop`, `AddDragEnterHandler`, `AddDragOverHandler`, `AddDropHandler`. Drop handler finds ancestor `MainViewModel` via `DataContext` chain, reads `SearchRecursive` for recursive folder enumeration. Supports both structured file data (`DataFormat.File`) and plain text paths (`DataFormat.Text`) for Windows Explorer compatibility.
- **Recent Files**: Scrollable list in the File menu with Load All and Clear Recent actions. `SettingsService.AddRecentFile()` maintains max 50 entries.
- **File Picker**: Avalonia storage APIs (`StorageProvider.OpenFilePickerAsync`, `OpenFolderPickerAsync`)
- **Presets**: Named conversion presets with quality, effort, raw distance settings
- **Confirmation Dialogs**: Custom `ConfirmDialog` window with `MessageText`/`TitleText` properties. Yes button (`IsDefault`) closes with `true`, No button (`IsCancel`) closes with `false`.
- **Settings Persistence**: Both `MainViewModel` and `SettingsViewModel` auto-save to disk on property change via `OnPropertyChanged`. `SettingsViewModel` uses a 500ms debounce timer to batch rapid edits — avoids synchronous I/O on UI thread and race conditions. `Dispose()` flushes pending persist. `MainViewModel` loads all settings from disk on startup. Settings stored in `%APPDATA%\ARWtoJXL\settings.json`. Settings window syncs through shared persistence — `SettingsViewModel` loads current state from disk on open, `MainViewModel.RefreshSettings()` reloads from disk on close.
- **Quality Scale Segments**: The quality slider in SettingsWindow displays a three-segment color bar below the track: Lossy (0-89, red), Near-lossless (90-99, amber), Lossless (100, green) with labeled captions aligned to each segment.
- **HeadlessTestMode**: `MainViewModel.HeadlessTestMode` static flag skips thumbnail generation during GUI tests to avoid file I/O.

## ViewModels

### MainViewModel
**Properties (all `[ObservableProperty]`):**
- `Images` (ObservableCollection<ImageItemViewModel>), `IsCancelRequested` (bool), `StatusMessage` (string), `IsConverting` (bool), `OutputPath` (string), `SubfolderName` (string), `IsAllSelected` (bool), `OutputDirectory` (string), `UseSubfolder` (bool), `QualityPreset` (int), `SearchRecursive` (bool), `OutputFormat` (OutputFormat), `ConflictResolution` (ConflictResolution), `ConfirmOverwrite` (bool), `UseCustomOutputDirectory` (bool), `CustomOutputDirectory` (string), `RecentFiles` (ObservableCollection<string>), `SkipMetadata` (bool), `CjxlEffort` (int), `IsAnySelected` (bool), `CompletedCount` (int), `TotalCount` (int)

**Private fields:** `_currentFileProgress` (double) — tracks per-file progress (0.0-1.0) from conversion pipeline for smooth overall progress display.

**Commands (`[RelayCommand]`):**
- `ConvertSelectedCommand`, `RemoveSelectedCommand`, `SelectAllCommand`, `CancelCommand`, `OpenSettingsCommand`, `OpenFileCommand`, `OpenFolderCommand`, `OpenOutputFolderCommand`, `LoadRecentFilesCommand`, `ClearRecentFilesCommand`
- `CancelCommand.CanExecute` returns `IsConverting` — enabled throughout conversion for immediate cancellation.

**Progress tracking:** `UpdateProgressDisplay()` computes overall percentage from completed count + current file progress. `OnFileProgress()` receives per-file progress from `IImageService.ConvertArwToJxlAsync` callback and updates status message with live percentage.

**Public methods:** `RefreshSettings()` — reloads settings from disk (called when SettingsWindow closes).

**Nested class:** `BoundedFilePathSet` — memory-bounded deduplication set for added file paths (1 MB limit, FIFO eviction via LinkedList + HashSet).

### SettingsViewModel
Implements `IDisposable` — `Dispose()` stops debounce timer, flushes pending persist, and disposes timer resources. Called from `SettingsWindow.Closing`.

**Properties (all `[ObservableProperty]`):**
- `UseSubfolder`, `SubfolderName`, `QualityPreset`, `SearchRecursive`, `OutputFormat`, `ConflictResolution`, `ConfirmOverwrite`, `UseCustomOutputDirectory`, `CustomOutputDirectory`, `IsSaving`, `SubfolderNameValidationResult`, `Presets` (ObservableCollection<ConversionPreset>), `SelectedPreset`, `HasSelectedPreset`, `NewPresetName`, `SkipMetadata`, `CjxlEffort`, `SelectedEffortOption`

**Public methods:** `Persist()` — forces immediate save (flushes debounce). Used in tests to verify persistence without waiting for timer.

**Public members:**
- `OutputFormatOptions` (Array), `ConflictResolutionOptions` (Array), `CjxlEffortOptions` (EffortOption[] with 10 options: Auto -1 through 9)
- `static ValidateSubfolderName(string)` — validates subfolder name against path characters, reserved names, length limits

**Commands (`[RelayCommand]`):**
- `BrowseOutputDirectoryCommand`, `AddPresetCommand`, `RemovePresetCommand`, `LoadPresetCommand`, `SaveCommand`, `CancelCommand`

**Nested class:** `EffortOption` — display text and value pair for effort ComboBox items.

### ImageItemViewModel
**Properties (all `[ObservableProperty]`):**
- `FilePath`, `FileName`, `Status`, `Thumbnail` (Bitmap?), `ErrorMessage`, `IsSelected`, `QualityOverride` (int?), `IsRemoved`, `SourceFileSize`, `OutputFileSize`, `OutputPath`

**Computed:** `SizeInfoText` — formatted output size with percentage change

**Slider binding:** `QualitySliderValue` (double) — two-way property for per-file quality slider; maps null `QualityOverride` to default 90, clamps 0-100 on set. Fires `OnPropertyChanged` when `QualityOverride` changes.

**Methods:** `EffectiveQuality(int globalQuality)`, `static FormatBytes(long bytes)`

## Data Models (in SettingsService.cs)

### ConflictResolution (enum)
- `Overwrite`, `Skip`, `AppendNumber`

### AppSettings
JSON-serializable settings container: `UseSubfolder`, `SubfolderName`, `QualityPreset`, `SearchRecursive`, `RecentFiles`, `OutputFormat`, `ConflictResolution`, `ConfirmOverwrite`, `UseCustomOutputDirectory`, `CustomOutputDirectory`, `Presets`, `SkipMetadata`, `CjxlEffort`

### ConversionPreset
Named preset: `Name`, `Quality`, `OutputFormat`, `ConflictResolution`, `UseSubfolder`, `SubfolderName`, `UseCustomOutputDirectory`, `CustomOutputDirectory`, `ConfirmOverwrite`, `SkipMetadata`, `CjxlEffort`

## Key Dependencies

- **Avalonia** (12.0.1): Cross-platform UI framework (MIT)
- **Avalonia.Desktop** (12.0.1): Desktop runtime (Windows/Linux/macOS)
- **Avalonia.Themes.Fluent** (12.0.1): Fluent design theme
- **Avalonia.Fonts.Inter** (12.0.1): Inter font family
- **AvaloniaUI.DiagnosticsSupport** (2.2.1): Debug-only diagnostics support (excluded from Release builds)
- **CommunityToolkit.Mvvm** (8.4.2): MVVM helpers (MIT)
- **Microsoft.Extensions.DependencyInjection** (8.0.1): DI container (MIT)

## Avalonia 12 Migration Notes

### Breaking Changes Applied
- **Namespace**: Updated from `https://github.com/avaloniaui` (Avalonia 11) to `https://github.com/avaloniaui` (Avalonia 12)
- **Theme**: `<StyleInclude>` replaced with `<FluentTheme/>`
- **Brushes**: `Background="Background"` invalid, use `Background="Transparent"`
- **Window Decorations**: `SystemDecorations` deprecated, use `WindowDecorations="Full"`
- **Text Trimming**: `TextBlock.Trimming` renamed to `TextTrimming`
- **ComboBox**: `SelectedValuePath` removed, use `SelectedValueBinding`. `DisplayMember` removed, use `ItemTemplate` with `DataTemplate`
- **XAML Loading**: `AvaloniaXamlLoader.Load(this)` replaced with `InitializeComponent()`
- **SelectionMode**: Set in code-behind via `FindControl<T>()` due to XAML enum resolution issues
- **DataTemplates**: Single template uses `ItemTemplate` instead of `DataTemplates` collection to avoid `x:DataType` requirements

### Known Fixes
- **ViewLocator "Not Found" bug**: `Match()` was returning `true` for all non-Control objects, intercepting button content strings. Fixed to only match `.ViewModels` namespace types.
- **Command naming**: `[RelayCommand]` method `LoadRecentFilesCommand` renamed to `LoadRecentFiles` to avoid conflict with generated `LoadRecentFilesCommand` property.
- **Effort ComboBox InvalidCastException**: `SelectedItem` binding to `int` property failed with `ComboBoxItem` items. Fixed with `SelectedValue` + `SelectedValueBinding` + `StringToIntConverter` to convert string `Tag` values to `int`.
