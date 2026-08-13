# RAWtoJXL.Cli

Headless command-line interface for the RAW→JXL/JPEG/PNG conversion pipeline. Shares all conversion logic with the desktop app via `RAWtoJXL.Core`; suited for scripts, scheduled automation, and LLM agents.

## Project Structure

```
RAWtoJXL.Cli/
├── Program.cs                 # Entry point: DI setup, UTF-8 console, RunAsync
├── CliApplication.cs          # Parse → validate → invoke pipeline; exit code mapping
├── CliCommandFactory.cs       # RootCommand + subcommands (convert, list, presets)
├── CliHandlers.cs             # Command handlers: bind, merge, enumerate, run, report
├── ConversionRunner.cs        # Sequential/parallel batch conversion loop
├── SettingsMerger.cs          # CLI flags > preset > settings.json > defaults
├── FileFilter.cs              # Include/exclude wildcards, date windows
├── ConsoleReporter.cs         # Progress (stderr), JSON (stdout), summaries
├── ToolPreflight.cs           # cjxl/exiftool discovery before conversion
├── ExitCodes.cs               # Stable exit codes for automation
├── UsageException.cs          # User errors mapped to exit code 2
└── Options/
    ├── CliOptions.cs          # Parsed argument POCO
    └── CliOptionsBinding.cs   # CommandOptions definitions + parse validation
```

## Commands

```
rawtojxl-cli convert <paths...> [options]   # Convert files/folders
rawtojxl-cli list <paths...> [options]      # Dry-run plan
rawtojxl-cli presets [--json]               # Named presets from GUI settings
```

Options: `--format jxl|jpg|png`, `--quality 0-100`, `--conflict overwrite|skip|rename`,
`--recursive/-r`, `--output-dir/-o`, `--no-subfolder`, `--subfolder`, `--preset/-p`,
`--ext a,b`, `--include glob`, `--exclude glob`, `--modified-after/-before ISO-date`,
`--skip-metadata`, `--effort 1-9`, `--threads N`, `--jobs N`, `--dry-run`, `--json`,
`--quiet`, `--verbose`.

Note: paths must be given **before** multi-value options (`--ext`, `--include`,
`--exclude`), e.g. `rawtojxl-cli convert C:\photos --include "*.arw"`.

## Design

- **Settings sharing** — reads `%APPDATA%\RAWtoJXL\settings.json` (the GUI's settings)
  as defaults; never writes. Precedence: CLI flags > `--preset` > settings > built-ins.
- **Parallel batch** — `--jobs N` converts N files concurrently. The default is a
  conservative 2; each job's cjxl gets `cores/jobs` threads. `ParallelismPolicy`
  computes a machine-specific stable limit (`SafeMaxJobs`, hard-capped at 4):
  `min(logicalProcessors/4, memory tier)` — 4 jobs for 16+ logical processors with
  ≥8 GB RAM, fewer on smaller hosts. Requesting more prints a warning to stderr
  (visible to agents in `--json` mode too) but still runs, since 5 has proven
  stable on some hosts while 8+ crashes libjxl 0.11.2
  (STATUS_STACK_BUFFER_OVERRUN).
- **Deterministic output planning** — parallel mode pre-resolves every output path
  sequentially, so `--conflict rename` never collides between concurrent jobs.
  Duplicate output paths (e.g. `a.arw` + `a.cr3`) fail loudly instead of
  concurrent-writing one file.
- **Machine-readable output** — `--json` writes a single JSON document to stdout;
  all progress/diagnostics go to stderr, so stdout stays pipe-clean.

## Exit Codes

| Code | Meaning |
|---|---|
| 0 | success (or dry-run/list with matches) |
| 1 | finished with at least one failed file |
| 2 | usage/argument error |
| 3 | no files found |
| 4 | cjxl.exe or exiftool.exe missing |
| 130 | cancelled |

## Dependencies

- System.CommandLine 2.0.11 (MIT)
- Microsoft.Extensions.DependencyInjection 8.0.1 (MIT)
- RAWtoJXL.Core (project reference)
- cjxl.exe + exiftool.exe + exiftool_files/ copied to output (same as GUI)
