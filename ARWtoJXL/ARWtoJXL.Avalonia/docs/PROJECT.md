# ARWtoJXL.Avalonia

Avalonia UI presentation layer implementing the desktop app with MVVM pattern, drag-drop support, and conversion management.

## Project Structure

```
ARWtoJXL.Avalonia/
├── App.axaml + App.axaml.cs          # Application entry point with DI container setup
├── AppStrings.cs                     # Shared string resources
├── MainWindow.axaml + .axaml.cs      # Main window with keyboard bindings, drag-drop, recent files, item template
├── SettingsWindow.axaml + .axaml.cs  # Settings dialog with presets, cjxl options, file picker integration
├── ViewLocator.cs                    # IDataTemplate implementation for MVVM view-model to view mapping
├── Program.cs                        # App bootstrap with Avalonia app builder
├── app.manifest                      # Application manifest (DPI awareness, v6 support)
├── Behaviors/
│   └── DragDropBehavior.cs           # Avalonia attached properties for drag-drop file/folder handling
├── Converters/
│   ├── BooleanToBrushConverter.cs    # Bool to SolidColorBrush converter
│   ├── BooleanToTextConverter.cs     # Bool to text converter
│   ├── BooleanToValueConverter.cs    # Bool to value converter
│   ├── ImageStatusToStringConverter.cs # ImageStatus enum to string converter
│   ├── ImageStatusToVisibilityConverter.cs # ImageStatus to bool (IsVisible) converter
│   ├── NullableIntConverter.cs       # Nullable int converter
│   └── StringToVisibilityConverter.cs # String to bool (IsVisible) converter
├── Services/
│   ├── ConfirmDialog.axaml + .axaml.cs # Confirmation dialog window
│   ├── DialogService.cs              # IDialogService implementation
│   ├── DispatcherService.cs          # IDispatcherService implementation (Avalonia Dispatcher.UIThread)
│   ├── FilePickerService.cs          # IFilePickerService implementation (Avalonia storage APIs)
│   ├── IDialogService.cs             # Dialog service interface
│   ├── IDispatcherService.cs         # Dispatcher service interface
│   └── IFilePickerService.cs         # File picker service interface
├── Settings/
│   └── SettingsService.cs            # Settings persistence (JSON-based)
└── ViewModels/
    ├── ImageItemViewModel.cs         # View model for image items (ObservableProperty, Bitmap thumbnail, QualityOverride)
    ├── MainViewModel.cs              # Main view model with IFilePickerService injection
    └── SettingsViewModel.cs          # Settings view model with presets, validation
```

- For any uknowns refer to API refernce https://docs.avaloniaui.net/api#namespaces and from there to related topics

## Architecture

**MVVM Pattern** with CommunityToolkit.Mvvm (`[ObservableProperty]`, `[RelayCommand]`, `[INotifyPropertyChanged]`).

**Dependency Injection** via Microsoft.Extensions.DependencyInjection. Services registered in `App.cs`:
- `IDispatcherService` → `DispatcherService` (singleton)
- `IDialogService` → `DialogService` (singleton)
- `IFilePickerService` → `FilePickerService` (singleton)
- ViewModels resolved per-instance with injected services

**XAML Loading**: Uses `InitializeComponent()` in constructors. `AvaloniaXamlLoader.Load(this)` replaced for Avalonia 12 compatibility.

**ViewLocator**: Implements `IDataTemplate` to automatically map view models to views. `Match()` only matches types from `.ViewModels` namespace to avoid intercepting strings and other primitives.

**Compiled Bindings**: Disabled (`<AvaloniaUseCompiledBindingsByDefault>false</AvaloniaUseCompiledBindingsByDefault>`) due to incompatibility with CommunityToolkit.Mvvm generated command properties.

## Key Features

- **Drag-Drop**: Attached properties pattern (`DragDropBehavior.AllowDrop`, `DragDropBehavior.DropCommand`) with Avalonia `DataTransfer.TryGetFiles()` API
- **Keyboard Shortcuts**: `Key.Delete` for remove, `Ctrl+A` for select all, `Ctrl+Shift+R` for reset selection
- **Recent Files**: Clickable list of recently used files/folders in the main window
- **File Picker**: Avalonia storage APIs (`StorageProvider.OpenFilePickerAsync`, `OpenFolderPickerAsync`)
- **Presets**: Named conversion presets with quality, effort, raw distance settings
- **Confirmation Dialogs**: Custom dialog for destructive operations (remove, clear completed)

## Key Dependencies

- **Avalonia** (12.0.1): Cross-platform UI framework (MIT)
- **Avalonia.Desktop** (12.0.1): Desktop runtime (Windows/Linux/macOS)
- **Avalonia.Themes.Fluent** (12.0.1): Fluent design theme
- **Avalonia.Fonts.Inter** (12.0.1): Inter font family
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
