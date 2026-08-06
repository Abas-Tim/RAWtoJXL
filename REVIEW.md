# RAWtoJXL Code Review

Review date: 2026-08-05. Scope: entire repository (Core, Avalonia UI, Tests, build scripts, CI).
Nothing in this document was fixed; findings are documented for follow-up.

Severity legend: **[BUG]** correctness issue / **[PERF]** performance / **[DESIGN]** architecture or maintainability / **[UX]** user-facing behavior / **[DEAD]** unused code / **[OPS]** repo/build hygiene.

---

## 1. Process management (RAWtoJXL.Core/Services/SystemProcessRunner.cs)

### 1.1 [BUG] Orphaned cjxl process when stdin writer fails
`RunProcessWithStdinWriterAsync` (SystemProcessRunner.cs:321) does:

```csharp
await stdinWriter(process.StandardInput.BaseStream, cancellationToken);
process.StandardInput.Close();
```

If the writer delegate throws (e.g., Magick.NET `Write()` to a broken pipe because cjxl exited early, or a `FileLockedException` from the source RAW file), the exception propagates immediately. The process is **not killed** (kill only happens via the cancellation registration or in the timeout block below), and the stdout/stderr read tasks are **never awaited**. `using var process` does not terminate a running process. Net effect: an orphaned `cjxl.exe` keeps running (and consuming CPU) in the background while the user sees a conversion error. Same flaw in `RunProcessWithStdinAsync` (SystemProcessRunner.cs:252) if `stdinTask` faults.
**Fix direction:** wrap the writer invocation in try/catch, kill the process and await the stream reads before rethrowing.

### 1.2 [BUG] `RunProcessAsync` leaks the process on cancellation
`RunProcessAsync` (SystemProcessRunner.cs:90-118) is the only runner method with **no** kill-on-cancel registration (compare `RunProcessWithTimeoutAsync` at :142). It is used by `ExiftoolService.EmbedMetadataAsync` with the user's cancellation token (ExiftoolService.cs:171). Cancelling a conversion during the metadata-embedding step throws `OperationCanceledException` out of `WaitForExitAsync` and leaves exiftool.exe running to completion.
**Fix direction:** add the same `cancellationToken.Register(() => process.Kill())` pattern.

### 1.3 [BUG] File-lock detection checks the wrong file in `IsFileLockError`
`EmbedMetadataAsync` throws `FileLockedException` only when stderr mentions the **source** file name (ExiftoolService.cs:190-203). But `-tagsFromFile ... -overwrite_original <output>` rewrites the **output** file, which is the one that gets locked (e.g., the JXL is open in a viewer). A locked output then falls through to a generic `IOException` ("exiftool metadata embedding failed with exit code..."), confusing the user. The lock message also propagates as-is to `FileLockedException(sourcePath)` with the wrong file.

### 1.4 [PERF] exiftool discovery re-runs `-ver` probe on every operation — never cached
`FindExiftoolAsync` (SystemProcessRunner.cs:24-60) scans 3 hardcoded paths + every PATH entry + app dir, and for each existing candidate launches a real `exiftool -ver` process (`IsExiftoolWorkingAsync`). It is called **per operation**:
- once per thumbnail (`ExiftoolService.ExtractPreviewImageAsync`)
- once per metadata embed (`ExiftoolService.EmbedMetadataAsync`)

So a 200-file batch spawns ~400 exiftool processes purely for version probing, on top of ~400 real operations. The discovery result is never cached (services are transient, so even in-memory state is lost per call).
**Fix direction:** cache the resolved path after first successful probe (static field or singleton registration).

### 1.5 [PERF/DEAD] `RunProcessBinaryAsync` drains stderr into a `MemoryStream` that is never read
(SystemProcessRunner.cs:208-214) — `stderrMs` is allocated and filled for every binary call (preview extraction, metadata extraction) and then discarded. Unnecessary allocation per call.

---

## 2. Progress reporting (RAWtoJXL.Core/Services/CjxlEncoderService.cs)

### 2.1 [UX] Time-based progress is misleading
`ReportProgressAsync` (CjxlEncoderService.cs:457-483) reports `elapsed / timeout` capped at 0.98, where the timeout is **300 seconds** (`DefaultTimeoutSeconds`). A typical 10-20 s encode therefore sits at ~3-7% for nearly its entire duration, then jumps to 100% when cjxl exits. The `ConvertToJxlAsync_ProgressCallback_ReportsSmoothProgress` test (ConversionTests.cs:54) passes only because the values are monotonic, not because they are meaningful. The progress bar gives the user no real information about encoding progress.
**Fix direction:** parse cjxl's stderr output (it emits percentage lines in some modes) or use file-size/time heuristics with a smaller budget.

### 2.2 [DESIGN] Three near-identical execute methods
`ExecuteEncodingProcessFromStreamAsync` (:281), `ExecuteEncodingProcessAsync` (:331), and `ExecuteEncodingProcessWithWriterAsync` (:380) are ~95% duplicated (logging, linked CTS, progress task, timeout/exit-code handling). Only the runner call differs. Any change to error handling must be made in three places.

### 2.3 [DEAD] `SafeReadStreamAsync` is never called
(CjxlEncoderService.cs:431-455) — leftover from an earlier stdout-reading implementation.

---

## 3. Conversion pipeline (RAWtoJXL.Core/Services/ImageProcessingService.cs)

### 3.1 [DEAD] `_pathResolver` is injected but never used
(ImageProcessingService.cs:14,29) — the service works fine without it.

### 3.2 [DEAD] Legacy APIs kept in public interfaces
- `ICjxlEncoder.EncodeAsync` (file-based) — no callers anywhere; only the writer-delegate overload is used by the app.
- `IImageConverterService.ExtractToRawRgb16Async` — documented as "superseded by StreamPpmToAsync" but still public, with its ~67M-pixel C# loop and `GetPixels()` allocations intact.
- `IFileService.GetTempFileName` — no callers.
- `ICjxlEncoder.EncodeFromStreamAsync(Stream, ...)` — no callers.
All of these expand the interface surface and test matrix for dead paths.

### 3.3 [DESIGN] Metadata embedding responsibility leaked into the encoder
`CjxlEncoderService` depends on `IExiftoolService` and embeds metadata after every encode — while `ImageProcessingService` also embeds metadata for the JPEG/PNG paths. The encoder shouldn't know about metadata; the orchestrator should own it.

### 3.4 [DEAD] Redundant output-existence check
`ConvertToJxlInternalAsync` re-checks `FileExists(outputPath)` after `EncodeFromStreamAsync`, which already calls `VerifyOutputFile` (and throws its own `FileNotFoundException` for empty files).

### 3.5 [BUG] No cleanup of partial output files on failure
If cjxl creates the output but metadata embedding subsequently fails (or the process is orphaned mid-write, see 1.1), the partial/corrupt output file stays on disk. With `ConflictResolution.Skip` or `AppendNumber`, a retry can produce surprising results (stale file counts as "exists" / new numbered file).

### 3.6 [DESIGN] `QualityCalculator.CalculateEffort(int quality)` ignores its parameter
Always returns 7; the signature invites callers to pass quality and assume it matters. Either drop the parameter or keep it for future auto-effort mapping.

---

## 4. Settings persistence (RAWtoJXL.Avalonia)

### 4.1 [BUG] Lost-update race between two view models writing the same file
`MainViewModel.SaveSettings()` (MainViewModel.cs:782) and `SettingsViewModel.Persist()`/`Save()` (SettingsViewModel.cs:66,384) both do read-modify-write of `settings.json`, and `SettingsViewModel.Persist()` runs on a **thread-pool thread** (System.Timers.Timer). Both windows can be open simultaneously (SettingsWindow is modal, but `MainViewModel` property-change saves can be triggered before the dialog opens or while the dialog timer is pending). Interleaved Load/Save pairs lose each other's changes, and concurrent `File.WriteAllText` on the same path can throw (silently swallowed). There is no lock, no single writer, and no serialization.

### 4.2 [PERF] MainViewModel persists on every property change, SettingsViewModel debounces
`MainViewModel` hooks `On<Property>Changed` → `SaveSettings()` for ~12 properties with no debounce. Dragging the quality slider or editing a text field fires a full JSON serialize + `File.WriteAllText` per change event (dozens per second, synchronously on the UI thread). `SettingsViewModel` uses a 500 ms timer for the same purpose. The two VMs are inconsistent; the main window should use the same debounce.

### 4.3 [DESIGN] Settings state duplicated in three places
Every setting exists in: `AppSettings` (SettingsService.cs:55), `MainViewModel` (fields + SaveSettings mapping, MainViewModel.cs:782-802), and `SettingsViewModel` (ctor + Persist + Save mapping, SettingsViewModel.cs:29-85). Adding a setting requires touching three files by hand — already demonstrated by the `LoadRecentFilesFromSettings`/`RefreshSettings` twins (MainViewModel.cs:176-230), which are identical except for recent files.

### 4.4 [DESIGN] All settings errors silently swallowed
`SettingsService.Load()`/`Save()` catch everything and return defaults / drop writes (SettingsService.cs:106-134). A corrupt settings file silently resets user config; a failing save silently loses it. No log, no user feedback, no backup of the previous file.

### 4.5 [OPS] Recent-file list grows unbounded on disk write frequency
`AddRecentFile` (SettingsService.cs:136) does a full Load+Save per successfully converted file. With a 50-file cap this is small, but combined with 4.1 it adds more read-modify-write contention during a batch conversion.

---

## 5. MainViewModel (RAWtoJXL.Avalonia/ViewModels/MainViewModel.cs)

### 5.1 [BUG] Invalid paths permanently pollute the dedup set
`AddFilesAsync` (MainViewModel.cs:634-637) adds every path to `_addedFilePaths` **before** validating existence/extension. A stale recent-file entry (deleted file) is therefore blocked forever — even if the user later restores the file or fixes the path, it can never be added again until the app restarts (and the bounded set keeps it only while not evicted). Validation should happen before adding to the set.

### 5.2 [DEAD] `IsCancelRequested` never becomes true
Searched the codebase: it is only ever assigned `false` (MainViewModel.cs:245,378). `OnIsCancelRequestedChanged` notifies `CancelCommand`, but `CanExecuteCancel` is driven by `IsConverting`. The property, its handler, and the XAML binding it feeds are dead weight.

### 5.3 [DESIGN] Confusing dual progress formulas
`UpdateProgress` (MainViewModel.cs:522) uses `(completed - 1 + _currentFileProgress) / total`, `UpdateProgressDisplay` (MainViewModel.cs:530) uses `(completed + _currentFileProgress) / total`. Both are correct only in their respective call contexts (post-file vs mid-file), which is fragile and easy to break by reordering calls.

### 5.4 [UX] Completion status lies
The final block always sets `StatusMessage = "Conversion complete."` (MainViewModel.cs:380) even when files were skipped (conflict), skipped by the user, or failed. Counts are reset to 0, so the user loses the summary. `ConflictResolution.Skip` results are also surfaced as `Failed`/red with "Skipped (file exists)" (MainViewModel.cs:262-268) — an error state for a deliberate user choice.

### 5.5 [PERF] `OpenFolder` enumerates on the UI thread
`Directory.GetFiles(folder, "*.*", AllDirectories)` (MainViewModel.cs:458) runs synchronously on the UI thread; a large recursive tree (or network share) freezes the window. Same pattern in `DragDropBehavior.OnDrop` (DragDropBehavior.cs:62-66, 88-92).

### 5.6 [BUG] `BoundedFilePathSet` eviction can reintroduce duplicates
With >~6000 distinct paths (1 MB cap, `EstimateBytes` = len*2+88), the oldest paths are evicted from the dedup set while their items still exist in `Images`. Re-adding those files later (drag-drop of the same folder) produces duplicate rows. Edge case, but the eviction is not coupled to the list contents.

### 5.7 [UX] Progress callback error handling writes a status string
Every progress tick schedules `OnUiAsync` + a `ContinueWith` fault continuation (MainViewModel.cs:298-310). If `OnUiAsync` ever faults, it overwrites the status message with "Progress error: ..." and the error is otherwise lost. Acceptable, but the error surfacing path is odd (status bar text for internal plumbing errors).

### 5.8 [DESIGN] `HeadlessTestMode` static mutable flag
`internal static bool HeadlessTestMode` (MainViewModel.cs:23) is global mutable state used to skip thumbnail generation in tests — a test seam leaking into production code.

---

## 6. Thumbnails & image services

### 6.1 [PERF] Full-resolution previews loaded as grid thumbnails
`ExtractThumbnailAsync` returns the embedded JPEG preview at its native resolution (Sony ARWs commonly carry 1616x1080 or larger previews, several MB). `GenerateThumbnailsAsync` decodes it at full size into a `Bitmap` for a ~200 px tile (MainViewModel.cs:685-714). A 200-file batch can hold hundreds of MB of bitmaps. The preview should be downscaled (e.g., `MagickImage.Thumbnail(300,300)`) before creating the `Bitmap`.

### 6.2 [DEAD] `MetadataProfiles`/`ExtractMetadataProfilesAsync` is test-only machinery
Four sequential exiftool invocations (EXIF, XMP, ICC, IPTC) plus temp-file lifecycle management (ExiftoolService.cs:23-115, MetadataProfiles.cs) used only by tests — the production pipeline embeds via `-tagsFromFile` in one call. Significant code that is never exercised by the shipped app path (and 1.4 makes it 4x more expensive when tests do use it).

### 6.3 [DESIGN] Thumbnail failure handling swallows exceptions
`TryGetDngThumbnail` catches everything and returns null (ImageConverterService.cs:65-99), `FallbackDecodeThumbnail` errors surface as a wrapped `Exception` with only a message; `ExtractThumbnailAsync`'s catch (ImageConverterService.cs:57-62) has a dead `if (ex is FileLockedException) throw;` (it can't be thrown inside that try, the `IOException` filter already handled it). Error diagnostics for thumbnail failures are effectively empty.

---

## 7. UI / Avalonia

### 7.1 [DESIGN] ItemsRepeater layout reset hack
On every conversion batch, `MainWindow` re-assigns a brand-new `UniformGridLayout` and calls `UpdateLayout()` twice (MainWindow.axaml.cs:29-43). This is a workaround for a layout/virtualization bug and forces re-realization of the entire gallery — a smell that should be root-caused.

### 7.2 [DESIGN] Crash handlers write only to `Debug.WriteLine`
`App.cs:52-65` — `UnhandledException`, `Dispatcher.UnhandledException` (marked handled, swallowing the failure), and `UnobservedTaskException` (SetObserved, swallowing) go nowhere in a Release build. Real user-facing failures vanish; the app appears to silently misbehave. At minimum, log to the `FileLogger`/event log and show a dialog.

### 7.3 [DESIGN] Service locator anti-pattern
`App.Services` is a public static `IServiceProvider`, and `SettingsWindow` pulls `IFilePickerService` from it (SettingsWindow.axaml.cs:15) instead of receiving it via DI. Also `App.OnDesktopExit` subscribes only to unsubscribe (App.cs:70-74) — pointless.

### 7.4 [DESIGN] `DragDropBehavior` never detaches handlers
`OnEnableDragDropChanged` (DragDropBehavior.cs:27-36) only handles the enabled=true case; setting the property to false leaves all handlers wired. `async void` handlers (`OnDrop`, `OnEnableDragDropChanged`) — an exception in `OnDrop` crashes the app (only `AddFilesAsync` is awaited; `Directory.GetFiles` inside can throw unhandled).

### 7.5 [UX] Per-item quality slider state vs global preset
`QualitySliderValue` defaults to 90 when no override exists (ImageItemViewModel.cs:40-45) but the global default quality is also 90 — if the global `QualityPreset` is changed to e.g. 75, existing items still show a slider at 90 while `EffectiveQuality` returns 75. The slider visually disagrees with what will actually be used.

### 7.6 [OPS] `AvaloniaUseCompiledBindingsByDefault=false`
(Avalonia.csproj:9) — all bindings are reflection-based; combined with `x:DataType` absence, binding errors surface only at runtime. Compiled bindings would catch them at build time.

---

## 8. Tests

### 8.1 [DESIGN] Tests depend on real binaries and a real RAW file
`ConversionTests`, `MetadataPreservationTests` etc. require `cjxl.exe`, `exiftool.exe`, and a checked-in LFS ARW (test1.ARW) and run real conversions. They are slow, environment-sensitive (exiftool discovery), and can flake on CI; unit tests for argument building and progress behavior exist but the heavy paths are not hermetic.

### 8.2 [DESIGN] Progress test asserts a broken implementation
`ConvertToJxlAsync_ProgressCallback_ReportsSmoothProgress` (ConversionTests.cs:54-85) asserts monotonicity and the 0.1/0.3/0.35-1.0 bands of the time-based estimator (2.1). If progress is ever made real (non-monotonic jumps), the test must change — it currently locks in the misleading behavior.

### 8.3 [DESIGN] `Startup` exposes raw `IServiceProvider` and public test paths
`Startup.CreateScope` and the `Services` property (Startup.cs) are protected/inherited; fine for xUnit inheritance, but `GetOutputPath` writes outputs next to the LFS test asset in the repo tree — the temp artifacts can end up committed (repo root already contains `publish_test/` and `debug_metadata.csx` leftovers, suggesting this has happened).

---

## 9. Repository / build hygiene

### 9.1 [OPS] Duplicated and drifted documentation
Three `docs/PROJECT.md` copies (Core 22 KB, Avalonia 16 KB, Tests 8 KB) plus two READMEs (root + RAWtoJXL/). The Core copy is the most accurate; the others drift (e.g., Tests copy describes older architecture). Single source of truth should be generated/linked.

### 9.2 [OPS] Committed debug artifacts
- `publish_test/` — publish output committed to the repo.
- `debug_metadata.csx` — a debugging script.
- `cjxl_help*.txt` (4 files) — cjxl help output dumps.
- `.opencode/` and `.urag/` — tool directories (`.urag` is gitignored; verify `.opencode` is).

### 9.3 [OPS] Two solution files
`RAWtoJXL.slnx` (root) and `RAWtoJXL.sln` (RAWtoJXL/). CI uses the inner one; a contributor opening the root one gets a different project graph (and possibly missing the bundled tools paths).

### 9.4 [OPS] Placeholder package metadata
`RepositoryUrl` = `https://github.com/yourname/RAWtoJXL` (both csproj files), `<Version>0.1</Version>` hardcoded. CI resolves release versions from tags, so the csproj version is stale/unused in releases.

### 9.5 [OPS] Build logic duplicated between `build.ps1` and CI
`build-release.yml` reimplements cjxl/exiftool download + bundling that `build.ps1`/`build-release.ps1` already do — they can drift (they already have: different exiftool download verification). One script should be shared.

### 9.6 [OPS] `.env` present in working tree
Contains `OPENAI_API_KEY`-style entries; it is properly gitignored (commit c411486) so no secret is in the repo — but note it must never be committed (secret hygiene).

### 9.7 [OPS] `FileLogger` log file never rotates
`%TEMP%\RAWtoJXL.log` grows without bound (FileLogger.cs:23). No size cap, no daily rollover, `Clear()` is never called by the app.

---

## 10. Minor / nitpicks

- [DESIGN] `EscapeArgument` (CjxlEncoderService.cs:485) escapes `"` as `\"` — invalid Windows command-line quoting (should be `""`). Paths containing quotes will produce malformed arguments.
- [DEAD] `AppStrings.CacheCleared/CacheClearFailed/CacheInfo` reference a "PNG cache" feature that no longer exists (AppStrings.cs:33-35).
- [DEAD] `AppStrings.Pending`/`True`/`False`/`SelectAll`/`DeselectAll` — check usage; several are unused.
- [DESIGN] `ImageConverterService.ExtractThumbnailAsync` returns `byte[]` (the JPEG preview) but the contract name/type doesn't convey the format; callers must know it's JPEG.
- [DESIGN] `DialogService.ShowConfirmAsync` returns `Task.FromResult(false)` when there's no main window — silent no is surprising in headless contexts.
- [DESIGN] `ImageItemViewModel.SizeInfoText` uses `saved` naming where `saved = source - output`; the semantics (negative % = smaller output) work but the name misleads.
- [DESIGN] `CjxlEncoderService.BuildEncodingArguments` vs `BuildStreamEncodingArguments` are 90% duplicate (same as 2.2).
- [PERF] `FileLogger.Write` opens/closes the file per line (`File.AppendAllText`) — fine at current volume, but the lock + per-line open/close is a scaling trap if logging grows.
- [DESIGN] `MetadataProfiles` constructor takes `ILogger?` optional param; half the call sites pass it, half don't — inconsistent.

---

## Suggested priority order (if acted on later)

1. Process orphan/kill bugs (1.1, 1.2) — data/CPU corruption, hangs, invisible background processes.
2. exiftool discovery caching (1.4) — biggest measured perf win for batches.
3. Settings save race + debounce (4.1, 4.2).
4. Real progress reporting (2.1).
5. Crash/error visibility in Release (7.2).
6. Dedup-set pollution (5.1), partial-output cleanup (3.5), lock-error detection (1.3).
7. Dead code/API surface reduction (3.2, 5.2, 2.3, 6.2).
8. Repo hygiene (9.x).
