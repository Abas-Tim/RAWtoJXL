# ARWtoJXL.WPF

Presentation layer implementing the WPF UI with MVVM pattern, drag-drop support, and conversion management.

## Project Structure

```
ARWtoJXL.WPF/
├── ViewModels/
│   ├── ImageItemViewModel.cs      # WPF view model for image items ([ObservableProperty], BitmapImage thumbnail, QualityOverride for per-file quality)
│   ├── MainViewModel.cs           # MVVM viewmodel ([ObservableProperty], [RelayCommand], auto-saves settings, recent files, conflict resolution)
│   └── SettingsViewModel.cs       # Settings dialog viewmodel ([ObservableProperty], [RelayCommand], SubfolderName validation, output format, conflict resolution, confirm overwrite)
├── MainWindow.xaml(.cs)           # Drag-drop gallery UI with DI wiring, recursive folder search, recent files handlers, lazy thumbnail loading
├── SettingsWindow.xaml(.cs)       # Settings dialog window (format, conflict, recursive, confirm overwrite options)
├── SettingsService.cs             # JSON settings persistence with recent files tracking
├── BooleanToBrushConverter.cs     # WPF value converters
├── BooleanToTextConverter.cs
├── BooleanToValueConverter.cs
├── NullableIntConverter.cs         # string <-> int? converter for per-file quality override
├── ImageStatusToStringConverter.cs
├── StringToVisibilityConverter.cs  # null/empty string → Collapsed, otherwise Visible
├── AppStrings.cs                  # Centralized UI string constants
└── Views/                         # Empty directory (reserved for future views)
```

## UI/UX Flow

1. User drags .ARW files or folders onto MainWindow (recursive or top-level only based on settings), or clicks "Open File" button to browse via OpenFileDialog
2. `MainWindow` constructor creates `ServiceCollection`, calls `AddCoreServices()`, builds `ServiceProvider`, resolves `IImageService`
3. `MainViewModel.AddFilesAsync()` deduplicates by normalized full path (case-insensitive), filters ARW/JXL, creates `ImageItemViewModel` objects and adds them to the gallery **immediately** (no blocking). Thumbnails are generated in background with limited concurrency (4 parallel tasks) and populate progressively as they complete
4. Items displayed in gallery with 80x60 thumbnails (lazy-loaded), selection checkboxes, per-item progress spinners, and per-file quality override textbox
5. **Recent files bar**: Shows recently converted files (up to 50) as clickable links with "Load All" and "Clear" buttons
6. User selects files, configures settings (quality, subfolder, output format, conflict resolution, recursive search) → clicks "Convert"
7. `ConvertSelectedAsync()` resolves output paths with conflict handling (skip/overwrite/append) → when overwriting and `ConfirmOverwrite` is enabled, shows a Yes/No confirmation dialog per file → spawns concurrent tasks (max = CPU core count via SemaphoreSlim)
8. Each conversion: Ready → Converting → Converted/Failed (or Pending if cancelled/skipped)
9. Successfully converted files are added to recent files list in settings.json
10. Output saved to same directory or subfolder (configurable via `UseSubfolder` + `SubfolderName`) with format determined by `OutputFormat` setting

## UI Components

- **Open File Button:** Opens OpenFileDialog with ARW/JXL filter, calls `AddFilesAsync()` with selected files
- **Recent Files Bar:** Displays recently converted files as clickable links. "Load All" loads all recent files, "Clear" removes history
- **Gallery (ListBox):** Displays ImageItemViewModel objects with thumbnail previews, checkboxes, status indicators, and per-file quality override textbox (empty = use global)
- **Per-Item Spinner:** Indeterminate ProgressBar visible when `Status == ImageStatus.Converting`
- **Per-Item Quality Override:** TextBox bound to `QualityOverride` property. Empty/null uses global `QualityPreset`, number (0-100) overrides per file
- **Global ProgressBar:** Shows overall progress (CompletedCount/TotalCount), hidden when `IsConverting == false`
- **Cancel Button:** Enabled during conversion, triggers `CancellationTokenSource.Cancel()`
- **Settings Window (Dialog):** Separate SettingsWindow with quality slider (0-100, default 90), subfolder checkbox, subfolder name TextBox, recursive folder checkbox, output format ComboBox (JXL/JPEG/PNG), conflict resolution ComboBox (Overwrite/Skip/Append)
- **Select All/Deselect All Button:** Toggles selection state, text bound to `IsAllSelected`
- **Open Output Folder Button:** Opens the output directory in File Explorer. Enabled after conversion completes (via `CanExecute` on `OpenOutputFolderCommand`). Uses `Process.Start` with `UseShellExecute = true`.

## Selection Logic

- `IsAllSelected`: True when `Images.All(i => i.IsSelected)`
- `IsAnySelected`: True when `_selectedImages.Any()`
- `UpdateSelectionState()`: Recalculates selection state on every `IsSelected` change
- `RemoveSelected()`: Removes selected items and resets selection state

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
- **SettingsViewModel**: `UseSubfolder` (bool, default true), `SubfolderName` (string, default "jxl_output"), `QualityPreset` (int, default 90), `SearchRecursive` (bool, default false), `OutputFormat` (enum: Jxl/Jpeg/Png, default Jxl), `ConflictResolution` (enum: Overwrite/Skip/AppendNumber, default Overwrite), `ConfirmOverwrite` (bool, default true)
- **SettingsWindow.xaml**: CheckBox for subfolder, TextBox for subfolder name, Slider for quality (0-100), CheckBox for recursive folder search, ComboBox for output format, ComboBox for conflict resolution, CheckBox for confirm overwrite
- Settings applied via `MainViewModel.ApplySettings(SettingsViewModel)` on window close
- `MainViewModel` exposes all settings properties synced from SettingsWindow
- **Settings persistence**: `MainViewModel` auto-saves settings to `SettingsService` (JSON in `%APPDATA%\ARWtoJXL\settings.json`)
- **ConfirmOverwrite**: When enabled (default), shows a Yes/No MessageBox before overwriting existing output files during conversion. When disabled, overwrites silently as before.
- **SubfolderName validation**: `SettingsViewModel` validates against invalid path characters, leading/trailing whitespace, length > 255, reserved Windows names (CON, PRN, AUX, etc.), and `.`/`..`

## Key Dependencies

- **CommunityToolkit.Mvvm** (8.4.2): `[ObservableProperty]`, `[RelayCommand]` source generators (MIT)
- **WPF-UI**: Modern Fluent UI controls for WPF (MIT)
- Depends on `ARWtoJXL.Core` for all business logic via DI
