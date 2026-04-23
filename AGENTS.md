## Agent Guidelines

- Be concise. Use short sentences. Skip filler words. Do not skip name of objects or packages or methods.
- No preamble, no summaries, no "here is..." phrases.
- Prefer bullet points over paragraphs.
- If unsure, ask one focused question instead of guessing.
- Don't rely on knowledge from within the model(yourself). Unless absolutely sure use web search if available and try to get more precise info from web.
- Do not use absolute paths. Use relative and make adapters for paths of different OSs
- **Before reading source files, check the documentation for the project you are working on:**
  - Root overview: `docs/PROJECT_OVERVIEW.md`
  - Core project: `ARWtoJXL/ARWtoJXL.Core/docs/PROJECT.md`
  - WPF project: `ARWtoJXL/ARWtoJXL.WPF/docs/PROJECT.md`
  - Tests project: `ARWtoJXL/ARWtoJXL.Tests/docs/PROJECT.md`
- **Write code with no duplication. Design with DI in mind — depend on interfaces, not concrete implementations.**
- **After any source code changes, update the corresponding project's `docs/PROJECT.md` to reflect the new state.**
- **After changes affecting the overall project structure, update `docs/PROJECT_OVERVIEW.md`.**

# ARWtoJPEGXL

High-performance Windows desktop app for converting Sony RAW (.ARW) to JPEG-XL (.JXL). .NET 8, WPF, MVVM.

## Directory Structure

```
ARWtoJPEGXL/
├── .gitignore
├── ARWtoJXL.slnx                          # Solution matrix
├── docs/
│   └── PROJECT_OVERVIEW.md               # Root project overview
└── ARWtoJXL/
    ├── build.ps1                          # Build script (restore, download deps, publish)
    ├── cjxl.exe                           # JPEG XL encoder v0.11.2
    ├── exiftool.exe                       # Metadata tool v13.56
    ├── ARWtoJXL.sln
    ├── ARWtoJXL.Core/                     # Domain logic
    │   └── docs/PROJECT.md               # Core documentation
    ├── ARWtoJXL.Tests/                    # xUnit tests
    │   └── docs/PROJECT.md               # Tests documentation
    └── ARWtoJXL.WPF/                      # WPF UI
        └── docs/PROJECT.md               # WPF documentation
```

## Git Ignore Policy

Excluded via `.gitignore`:
- `bin/`, `obj/` — build outputs
- `packages/`, `*.nupkg`, `project.nuget.cache` — NuGet artifacts
- `*.user`, `*.suo`, `*.sln.docstates` — IDE state
- `.idea/`, `*.sln.iml` — Rider artifacts
- `MediaCache/` — ImageMagick cache
- `*.pdb` — debug symbols
- `cjxl_help_*.txt`, `debug_metadata.csx` — temp debug files
