# Release Guide

This document explains how to build portable Windows releases of RAWtoJXL and
publish them to GitHub Releases.

---

## Quick Start

### Local Build (PowerShell)

```powershell
cd RAWtoJXL
.\build-release.ps1
```

This will:

1. Download `cjxl.exe` and `exiftool.exe` (if not already present).
2. Restore NuGet packages and publish a self-contained `win-x64` executable.
3. Bundle dependencies into the output folder.
4. Create a portable ZIP archive in `RAWtoJXL/artifacts/`.

The resulting archive is named `RAWtoJXL-{version}-win-x64.zip`.

### Options

| Parameter        | Description                              | Default    |
|------------------|------------------------------------------|------------|
| `-Version`       | Override version in filename             | from csproj|
| `-OutputDir`     | Directory for the ZIP archive            | `artifacts`|
| `-SkipDownload`  | Skip downloading cjxl/exiftool           | `$false`   |
| `-NoPack`        | Build only, skip ZIP packaging           | `$false`   |
| `-Configuration` | Build configuration                      | `Release`  |
| `-Runtime`       | Target runtime identifier                | `win-x64`  |

**Examples:**

```powershell
# Build with explicit version
.\build-release.ps1 -Version "1.2.0"

# Build to a custom output folder
.\build-release.ps1 -OutputDir "C:\releases"

# Build without downloading dependencies (already present)
.\build-release.ps1 -SkipDownload

# Build but don't create a ZIP (just publish folder)
.\build-release.ps1 -NoPack
```

---

## GitHub Releases

### Option A: Automated via Git Tags (Recommended)

Push an annotated tag to trigger a full CI/CD pipeline that builds, tests,
packages, and publishes a GitHub Release automatically:

```bash
# Create and push a version tag
git tag -a v1.2.0 -m "Release v1.2.0"
git push origin v1.2.0
```

The workflow (`.github/workflows/build-release.yml`) will:

1. **Build job** — downloads dependencies, restores packages, builds, runs
   tests, publishes a self-contained EXE, creates a ZIP with SHA-256 checksum.
2. **Release job** — downloads the ZIP artifact, creates a GitHub Release
   with auto-generated release notes, and uploads the ZIP + checksum as assets.

### Option B: Manual Trigger via GitHub UI

1. Go to **Actions → Build & Release** workflow.
2. Click **"Run workflow"**.
3. Optionally enter a version override and check **"Create GitHub Release"**.
4. Click **Run workflow**.

### Option C: Manual Upload with `gh` CLI

After a local build, upload the ZIP to a GitHub Release:

```powershell
# Build locally
cd RAWtoJXL
.\build-release.ps1 -Version "1.2.0"

# Create release and upload artifacts\*.zip + *.sha256
gh release create v1.2.0 artifacts\* --generate-notes
```

---

## Version Management

The version is resolved in the following order (first match wins):

| Source            | Example                      |
|-------------------|------------------------------|
| `-Version` flag   | `.\build-release.ps1 -Version 1.2.0` |
| Git tag           | `v1.2.0` → `1.2.0`           |
| `<Version>` in csproj | `<Version>1.2.0</Version>` |
| `<AssemblyVersion>`| `<AssemblyVersion>1.2.0</AssemblyVersion>` |
| Fallback          | `1.0.0`                      |

To set a version in the project file, add to
`RAWtoJXL.Avalonia/RAWtoJXL.Avalonia.csproj`:

```xml
<PropertyGroup>
  <Version>1.2.0</Version>
</PropertyGroup>
```

---

## Release Assets

Each release includes:

| Asset                                | Description                        |
|--------------------------------------|------------------------------------|
| `RAWtoJXL-{version}-win-x64.zip`     | Portable ZIP with all binaries     |
| `RAWtoJXL-{version}-win-x64.zip.sha256` | SHA-256 checksum for verification |

### Verifying a Download

```powershell
# Download the ZIP and .sha256 file, then verify:
$expected = Get-Content RAWtoJXL-1.2.0-win-x64.zip.sha256
$actual   = (Get-FileHash RAWtoJXL-1.2.0-win-x64.zip -Algorithm SHA256).Hash
if ($expected -eq $actual) {
    Write-Host "Checksum verified!" -ForegroundColor Green
} else {
    Write-Host "Checksum mismatch — file may be corrupted!" -ForegroundColor Red
}
```

---

## Workflow Triggers

| Event                  | Action                                         |
|------------------------|-------------------------------------------------|
| `push` to `main`       | Build + test + upload artifacts                 |
| `push` to `develop`    | Build + test + upload artifacts                 |
| `push` of `v*` tag     | Build + test + **create GitHub Release**        |
| `pull_request`         | Build + test only (no artifacts or release)     |
| `workflow_dispatch`    | Manual build with optional release creation     |

---

## Troubleshooting

### `cjxl.exe not found`

The build script downloads cjxl from the libjxl GitHub Releases page. If the
download fails, place `cjxl.exe` manually in the `RAWtoJXL/` folder or use
`-SkipDownload` after a manual download from:
<https://github.com/libjxl/libjxl/releases>

### `exiftool.exe not found`

Same as above — download from <https://exiftool.org/> and place in `RAWtoJXL/`.

### GitHub Release fails with "artifact not found"

The release job downloads artifacts from the build job. Make sure the build
job completed successfully before the release job runs. Check the build job
logs for errors.

### Version mismatch in release assets

Ensure the version in your git tag matches the `<Version>` in the csproj for
consistent naming. Use `-Version` override if needed.