# ARWtoJPEGXL

High-performance Windows desktop app for converting Sony RAW (.ARW) to JPEG-XL (.JXL). .NET 8, WPF, MVVM.

## Directory Structure

```
ARWtoJPEGXL/
├── .gitignore
├── ARWtoJXL.slnx                          # Solution matrix
├── docs/
│   └── PROJECT_OVERVIEW.md
└── ARWtoJXL/
    ├── build.ps1                          # Build script (restore, download deps, publish)
    ├── cjxl.exe                           # JPEG XL encoder v0.11.2
    ├── exiftool.exe                       # Metadata tool v13.56
    ├── ARWtoJXL.sln
    ├── ARWtoJXL.Core/                     # Domain logic
    ├── ARWtoJXL.Tests/                    # xUnit tests
    └── ARWtoJXL.WPF/                      # WPF UI
```

## Agent Guidelines

- Be concise. Use short sentences. Skip filler words.
- No preamble, no summaries, no "here is..." phrases.
- Code changes: show only the diff or minimal context.
- Prefer bullet points over paragraphs.
- If unsure, ask one focused question instead of guessing.
- **Before reading source files, check `docs/PROJECT_OVERVIEW.md` for context on architecture, services, pipeline, enums, and file locations.**
- **For project details (architecture, services, pipeline, enums, etc.), refer to `docs/PROJECT_OVERVIEW.md`.**
- **Write code with no duplication. Design with DI in mind — depend on interfaces, not concrete implementations.**
- **After any source code changes, update `docs/PROJECT_OVERVIEW.md` to reflect the new state.**

## Git Ignore Policy

Excluded via `.gitignore`:
- `bin/`, `obj/` — build outputs
- `packages/`, `*.nupkg`, `project.nuget.cache` — NuGet artifacts
- `*.user`, `*.suo`, `*.sln.docstates` — IDE state
- `.idea/`, `*.sln.iml` — Rider artifacts
- `MediaCache/` — ImageMagick cache
- `*.pdb` — debug symbols
- `cjxl_help_*.txt`, `debug_metadata.csx` — temp debug files
