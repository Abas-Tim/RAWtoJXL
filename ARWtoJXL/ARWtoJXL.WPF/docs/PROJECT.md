# ARWtoJXL.WPF

Presentation layer implementing the WPF UI with MVVM pattern, drag-drop support, and conversion management.

## Project Structure

```
ARWtoJXL.WPF/
├── ViewModels/
│   ├── ImageItemViewModel.cs      # WPF view model for image items ([ObservableProperty], BitmapImage thumbnail, QualityOverride for per-file quality, IsRemoved flag, SourceFileSize/OutputFileSize for size tracking, SizeInfoText computed property)
│   ├── MainViewModel.cs           # MVVM viewmodel ([ObservableProperty], [RelayCommand], auto-saves settings, recent files, conflict resolution, custom output directory, depends on IDialogService + IDispatcherService)
│   └── SettingsViewModel.cs       # Settings dialog viewmodel ([ObservableProperty], [RelayCommand], RequestClose event, SubfolderName validation, presets management, custom output directory)
├── Services/
│   ├── IDialogService.cs          # Async dialog interface (ShowConfirmAsync returns Task<bool>)
│   ├── DialogService.cs           # IDialogService implementation using ConfirmDialog window
│   ├── ConfirmDialog.xaml(.cs)    # Async confirm dialog window (replaces blocking MessageBox.Show)
│   ├── IDispatcherService.cs      # Dispatcher abstraction interface (InvokeAsync for UI thread marshaling)
│   └── DispatcherService.cs       # IDispatcherService implementation wrapping System.Windows.Threading.Dispatcher
├── Behaviors/
│   └── DragDropBehavior.cs        # Attached behavior for MVVM drag-drop (replaces code-behind Drop/DragOver handlers)
├── MainWindow.xaml(.cs)           # Gallery UI with DI wiring, attached drag-drop behavior, minimal code-behind (input validation only)
├── SettingsWindow.xaml(.cs)       # Settings dialog window (format, conflict, recursive, confirm overwrite, output directory, presets)
├── SettingsService.cs             # JSON settings persistence, ConflictResolution enum, ConversionPreset model, AppSettings model
├── BooleanToBrushConverter.cs     # WPF value converters (cached static SolidColorBrush, no per-call allocation)
├── BooleanToTextConverter.cs
├── BooleanToValueConverter.cs
├── NullableIntConverter.cs         # string <-> int? converter for per-file quality override (validates range 0-100)
├── ImageStatusToStringConverter.cs
├── StringToVisibilityConverter.cs  # null/empty string → Collapsed, otherwise Visible
├── AppStrings.cs                  # Centralized UI string constants
└── Views/                         # Empty directory (reserved for future views)
```

## UI/UX Flow

1. User drags .ARW files or folders onto MainWindow (recursive or top-level only based on settings), or clicks "Open File" button to browse via OpenFileDialog
2. `MainWindow` constructor creates `ServiceCollection`, calls `AddCoreServices()`, builds `ServiceProvider`, resolves `IImageService`
3. `MainViewModel.AddFilesAsync()` deduplicates by normalized full path (case-insensitive), filters ARW/JXL, creates `ImageItemViewModel` objects and adds them to the gallery **immediately** (no blocking). Thumbnails are generated in background with limited concurrency (4 parallel tasks) and populate progressively as they complete. Background thumbnail generation checks `IsRemoved` flag before updating items to prevent crashes when items are removed during generation
4. Items displayed in gallery with 80x60 thumbnails (lazy-loaded), selection checkboxes, per-item progress spinners, per-file quality override textbox, and post-conversion size info
5. **Recent files bar**: Shows recently converted files (up to 50) as clickable links with "Load All" and "Clear" buttons
6. User selects files, configures settings (quality, subfolder, output format, conflict resolution, recursive search) → clicks "Convert"
7. `ConvertSelectedAsync()` resolves output paths with conflict handling (skip/overwrite/append) → when overwriting and `ConfirmOverwrite` is enabled, shows a Yes/No confirmation dialog per file → spawns concurrent tasks (max = CPU core count via SemaphoreSlim)
8. Each conversion: Ready → Converting → Converted/Failed (or Pending if cancelled/skipped). `FileLockedException` is caught specifically and displays a user-friendly message prefixed with `AppStrings.FileLockedPrefix`
9. Successfully converted files are added to recent files list in settings.json. File sizes (source + output) are captured and displayed in gallery.
10. Output saved to same directory, subfolder, or custom directory (configurable via `UseSubfolder` + `SubfolderName` or `UseCustomOutputDirectory` + `CustomOutputDirectory`) with format determined by `OutputFormat` setting

## UI Components

- **Open File Button:** Opens OpenFileDialog with ARW/JXL filter, calls `AddFilesAsync()` with selected files
- **Recent Files Bar:** Displays recently converted files as clickable links. "Load All" loads all recent files, "Clear" removes history
- **Gallery (ListBox):** Displays ImageItemViewModel objects with thumbnail previews, checkboxes, status indicators, per-file quality override textbox (empty = use global), and size info text (output size + compression %)
- **Per-Item Spinner:** Indeterminate ProgressBar visible when `Status == ImageStatus.Converting`
- **Per-Item Quality Override:** TextBox bound to `QualityOverride` property. Empty/null uses global `QualityPreset`, number (0-100) overrides per file. Input validated in `MainWindow.QualityTextBox_PreviewTextInput` (blocks non-digits, values > 100, and > 3 characters) and `NullableIntConverter.ConvertBack` (rejects values outside 0-100)
- **Per-Item Size Info:** TextBlock showing output file size and compression percentage (e.g., "4.2 MB (-62%)"). Only visible for converted items. Formatted as KB/MB based on size.
- **Global ProgressBar:** Shows overall progress (CompletedCount/TotalCount), hidden when `IsConverting == false`
- **Cancel Button:** Enabled during conversion, triggers `CancellationTokenSource.Cancel()`
- **Settings Window (Dialog):** Separate SettingsWindow with quality slider (0-100, default 90), subfolder checkbox, subfolder name TextBox, custom output directory with Browse button, recursive folder checkbox, output format ComboBox (JXL/JPEG/PNG), conflict resolution ComboBox (Overwrite/Skip/Append), presets management section
- **Select All/Deselect All Button:** Toggles selection state, text bound to `IsAllSelected`
- **Open Output Folder Button:** Opens the output directory in File Explorer. Enabled after conversion completes (via `CanExecute` on `OpenOutputFolderCommand`). Uses `Process.Start` with `UseShellExecute = true`.

## Selection Logic

- `IsAllSelected`: True when `Images.All(i => i.IsSelected)`
- `IsAnySelected`: True when `_selectedImages.Any()`
- `UpdateSelectionState()`: Recalculates selection state on every `IsSelected` change
- `RemoveSelected()`: Marks items as removed (`IsRemoved = true`), unsubscribes event handlers, removes from collection, and resets selection state. The `IsRemoved` flag prevents background thumbnail generation from updating removed items (race condition fix)

## Keyboard Shortcuts

Defined in `MainWindow.xaml` via `Window.InputBindings`:

| Shortcut | Action |
|----------|--------|
| `Ctrl+A` | Select All |
| `Ctrl+C` | Convert Selected |
| `Delete` | Remove Selected |
| `Ctrl+D` | Open File Dialog |

## Settings (SettingsViewModel + SettingsWindow)

`SettingsWindow` is a separate dialog window (not an Expander in MainWindow):
- **SettingsViewModel**: `UseSubfolder` (bool, default true), `SubfolderName` (string, default "jxl_output"), `QualityPreset` (int, default 90), `SearchRecursive` (bool, default false), `OutputFormat` (enum: Jxl/Jpeg/Png, default Jxl), `ConflictResolution` (enum: Overwrite/Skip/AppendNumber, default Overwrite), `ConfirmOverwrite` (bool, default true), `UseCustomOutputDirectory` (bool, default false), `CustomOutputDirectory` (string, default empty)
- **SettingsWindow.xaml**: Quality slider, output format ComboBox, subfolder checkbox + TextBox, custom output directory checkbox + TextBox + Browse button, recursive folder checkbox, conflict resolution ComboBox, confirm overwrite checkbox, presets section with ComboBox + Load/Save As/Delete buttons
- Settings applied via `MainViewModel.ApplySettings(SettingsViewModel)` on window close
- Save/Cancel commands fire `RequestClose` event → `SettingsWindow` subscribes and calls `this.Close()`
- `MainViewModel` exposes all settings properties synced from SettingsWindow
- **Settings persistence**: `MainViewModel` auto-saves settings to `SettingsService` (JSON in `%APPDATA%\ARWtoJXL\settings.json`)
- **ConfirmOverwrite**: When enabled (default), shows a Yes/No MessageBox before overwriting existing output files during conversion. Confirmations are serialized via `SemaphoreSlim(1,1)` to prevent stacked dialogs during parallel conversions. When disabled, overwrites silently as before.
- **SubfolderName validation**: `SettingsViewModel.ValidateSubfolderName()` (internal static) validates against invalid path characters, leading/trailing whitespace, length > 255, reserved Windows names (CON, PRN, AUX, etc.), and `.`/`..`. Testable from test project via `InternalsVisibleTo`.
- **Custom output directory**: When `UseCustomOutputDirectory` is true, all outputs go to `CustomOutputDirectory` regardless of source file location. The Browse button uses a workaround (OpenFileDialog with `ValidateNames=false`) to allow directory selection.
- **Conversion presets**: Named profiles stored in `ConversionPreset` model. Each preset captures quality, output format, conflict resolution, subfolder settings, custom output directory, and confirm overwrite. Users can Save As (creates/overwrites), Load (applies preset to current settings), and Delete presets. Presets persist in settings.json.

## Key Dependencies

- **CommunityToolkit.Mvvm** (8.4.2): `[ObservableProperty]`, `[RelayCommand]` source generators (MIT)
- **WPF-UI**: Modern Fluent UI controls for WPF (MIT)
- Depends on `ARWtoJXL.Core` for all business logic via DI
