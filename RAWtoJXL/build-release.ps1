#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Build a portable Windows release of RAWtoJXL and package it as a ZIP archive.

.DESCRIPTION
    This script restores external dependencies (cjxl, exiftool), publishes the
    Avalonia application as a self-contained win-x64 executable, and creates a
    portable ZIP archive ready for distribution or GitHub Releases upload.

.PARAMETER Configuration
    Build configuration. Default: 'Release'

.PARAMETER Runtime
    Target runtime identifier. Default: 'win-x64'

.PARAMETER Version
    Override the version tag for the output filename. If omitted, the version
    is read from the csproj's <Version> or <AssemblyVersion> property.

.PARAMETER OutputDir
    Directory where the final ZIP archive is written. Default: 'artifacts'
    (relative to the script directory).

.PARAMETER SkipDownload
    Skip downloading cjxl and exiftool. Useful when the binaries already exist.

.PARAMETER NoPack
    Build and publish only; do not create a ZIP archive.

.EXAMPLE
    .\build-release.ps1

    .\build-release.ps1 -Version "1.2.0" -OutputDir "releases"

    .\build-release.ps1 -SkipDownload -NoPack
#>

[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Version = "",
    [string]$OutputDir = "",
    [switch]$SkipDownload,
    [switch]$NoPack
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
if ([string]::IsNullOrEmpty($scriptDir)) { $scriptDir = Get-Location }
$scriptDir = Convert-Path $scriptDir

# ── Paths ──────────────────────────────────────────────────────────────────
$projectName = "RAWtoJXL.Avalonia/RAWtoJXL.Avalonia.csproj"
$solutionFile = "RAWtoJXL.sln"
$cjxlPath = Join-Path $scriptDir "cjxl.exe"
$exiftoolPath = Join-Path $scriptDir "exiftool.exe"
$exiftoolFilesDir = Join-Path $scriptDir "exiftool_files"
$publishDir = Join-Path $scriptDir "RAWtoJXL.Avalonia\bin\$Configuration\net8.0\$Runtime\publish"

# ── Version resolution ────────────────────────────────────────────────────
if (-not $Version) {
    Write-Host "Resolving version from project file..." -ForegroundColor Cyan
    $csprojPath = Join-Path $scriptDir $projectName
    $csprojContent = Get-Content $csprojPath -Raw
    # Try <Version> first, then <AssemblyVersion>, then default
    if ($csprojContent -match '<Version>([^<]+)</Version>') {
        $Version = $matches[1].Trim()
    } elseif ($csprojContent -match '<AssemblyVersion>([^<]+)</AssemblyVersion>') {
        $Version = $matches[1].Trim()
    } else {
        $Version = "1.0.0"
    }
}
Write-Host "Building version: $Version" -ForegroundColor Cyan

# ── Output directory ───────────────────────────────────────────────────────
if (-not $OutputDir) {
    $OutputDir = Join-Path $scriptDir "artifacts"
} else {
    # If relative path, resolve against scriptDir
    if (-not ([System.IO.Path]::IsPathRooted($OutputDir))) {
        $OutputDir = Join-Path $scriptDir $OutputDir
    }
}
$OutputDir = Convert-Path $OutputDir -ErrorAction SilentlyContinue
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

# ── Dependencies ───────────────────────────────────────────────────────────
$cjxlVersion = "0.11.2"
$cjxlUrl = "https://github.com/libjxl/libjxl/releases/download/v$cjxlVersion/jxl-x64-windows-static.zip"
$exiftoolVersion = "13.57"
$exiftoolUrl = "https://sourceforge.net/projects/exiftool/files/exiftool-$exiftoolVersion_64.zip/download"

if (-not $SkipDownload) {
    # ── cjxl ──────────────────────────────────────────────────────────────
    if (-not (Test-Path $cjxlPath)) {
        Write-Host "Downloading cjxl v$cjxlVersion..." -ForegroundColor Cyan
        $cjxlZip = Join-Path $env:TEMP "jxl-cjxl.zip"
        try {
            curl.exe -L -s -o $cjxlZip $cjxlUrl
            $tempExtract = Join-Path $env:TEMP "jxl-extract"
            if (Test-Path $tempExtract) { Remove-Item $tempExtract -Recurse -Force }
            New-Item -ItemType Directory -Path $tempExtract -Force | Out-Null
            Expand-Archive -Path $cjxlZip -DestinationPath $tempExtract -Force
            $foundCjxl = Get-ChildItem $tempExtract -Filter "cjxl.exe" -Recurse | Select-Object -First 1
            if ($foundCjxl) {
                Copy-Item $foundCjxl.FullName $cjxlPath -Force
                Write-Host "cjxl.exe downloaded successfully." -ForegroundColor Green
            } else {
                Write-Host "Warning: cjxl.exe not found in downloaded archive." -ForegroundColor Yellow
            }
            Remove-Item $tempExtract -Recurse -Force -ErrorAction SilentlyContinue
        } finally {
            Remove-Item $cjxlZip -Force -ErrorAction SilentlyContinue
        }
    } else {
        Write-Host "cjxl.exe already exists at $cjxlPath" -ForegroundColor Gray
    }

    # ── exiftool ──────────────────────────────────────────────────────────
    if (-not (Test-Path $exiftoolPath)) {
        Write-Host "Downloading exiftool v$exiftoolVersion..." -ForegroundColor Cyan
        $tempZip = Join-Path $env:TEMP "exiftool.zip"
        try {
            curl.exe -L -s `
                -A "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36" `
                -H "Accept: text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8" `
                -H "Accept-Language: en-US,en;q=0.9" `
                -o $tempZip $exiftoolUrl
            $bytes = [System.IO.File]::ReadAllBytes($tempZip)
            if ($bytes[0] -eq 80 -and $bytes[1] -eq 75) {
                $tempExtract = Join-Path $env:TEMP "exiftool-extract"
                if (Test-Path $tempExtract) { Remove-Item $tempExtract -Recurse -Force }
                New-Item -ItemType Directory -Path $tempExtract -Force | Out-Null
                Expand-Archive -Path $tempZip -DestinationPath $tempExtract -Force

                # Find exiftool executable (may have special chars in name)
                $foundExiftool = Get-ChildItem $tempExtract -Filter "exiftool*.exe" -Recurse | Select-Object -First 1
                if ($foundExiftool) {
                    Copy-Item $foundExiftool.FullName $exiftoolPath -Force
                    Write-Host "exiftool.exe downloaded successfully." -ForegroundColor Green
                } else {
                    Write-Host "Warning: exiftool executable not found in archive." -ForegroundColor Yellow
                }

                # Copy exiftool_files folder if present
                $foundFilesDir = Get-ChildItem $tempExtract -Filter "exiftool_files" -Directory -Recurse | Select-Object -First 1
                if ($foundFilesDir -and -not (Test-Path $exiftoolFilesDir)) {
                    Copy-Item $foundFilesDir.FullName $exiftoolFilesDir -Recurse -Force
                    Write-Host "exiftool_files copied successfully." -ForegroundColor Green
                }

                Remove-Item $tempExtract -Recurse -Force -ErrorAction SilentlyContinue
            } else {
                throw "Downloaded file is not a valid ZIP archive."
            }
        } catch {
            Write-Host "Warning: Failed to download exiftool: $_" -ForegroundColor Yellow
            Write-Host "Please download manually from https://exiftool.org/" -ForegroundColor Yellow
        } finally {
            Remove-Item $tempZip -Force -ErrorAction SilentlyContinue
        }
    } else {
        Write-Host "exiftool.exe already exists at $exiftoolPath" -ForegroundColor Gray
    }
}

# ── Restore ────────────────────────────────────────────────────────────────
Write-Host "`nRestoring NuGet packages..." -ForegroundColor Cyan
dotnet restore "$scriptDir/$solutionFile"
if ($LASTEXITCODE -ne 0) {
    Write-Host "Restore failed!" -ForegroundColor Red
    exit 1
}

# ── Publish ────────────────────────────────────────────────────────────────
Write-Host "`nPublishing self-contained $Runtime application..." -ForegroundColor Cyan

# Clean previous publish output
if (Test-Path $publishDir) {
    Remove-Item $publishDir -Recurse -Force
}

dotnet publish "$scriptDir/$projectName" `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -p:PublishSingleFile=false `
    -p:EnableReadyToRun=false

if ($LASTEXITCODE -ne 0) {
    Write-Host "Publish failed!" -ForegroundColor Red
    exit 1
}

# ── Copy dependencies to publish folder ────────────────────────────────────
Write-Host "`nCopying dependencies to publish directory..." -ForegroundColor Cyan

if (Test-Path $cjxlPath) {
    Copy-Item $cjxlPath -Destination (Join-Path $publishDir "cjxl.exe") -Force
    Write-Host "  cjxl.exe -> publish/" -ForegroundColor Gray
}

if (Test-Path $exiftoolPath) {
    Copy-Item $exiftoolPath -Destination (Join-Path $publishDir "exiftool.exe") -Force
    Write-Host "  exiftool.exe -> publish/" -ForegroundColor Gray
}

if (Test-Path $exiftoolFilesDir) {
    Copy-Item $exiftoolFilesDir -Destination (Join-Path $publishDir "exiftool_files") -Recurse -Force
    Write-Host "  exiftool_files/ -> publish/" -ForegroundColor Gray
}

# ── Create portable ZIP package ────────────────────────────────────────────
if (-not $NoPack) {
    $packageName = "RAWtoJXL-$Version-win-x64"
    $zipPath = Join-Path $OutputDir "$packageName.zip"

    # Create a staging folder so the ZIP doesn't include deep path prefixes
    $stagingDir = Join-Path $env:TEMP "$packageName-staging"
    if (Test-Path $stagingDir) { Remove-Item $stagingDir -Recurse -Force }
    New-Item -ItemType Directory -Path $stagingDir -Force | Out-Null

    # Copy all publish files into staging root
    Get-ChildItem $publishDir -File | Copy-Item -Destination $stagingDir -Force
    Write-Host "`nPackaging portable release..." -ForegroundColor Cyan
    Write-Host "  Staging: $stagingDir" -ForegroundColor Gray

    # Create ZIP archive
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    Compress-Archive -Path (Join-Path $stagingDir "*") -DestinationPath $zipPath -CompressionLevel Optimal

    # Cleanup staging
    Remove-Item $stagingDir -Recurse -Force

    $zipSize = [math]::Round((Get-Item $zipPath).Length / 1MB, 2)
    Write-Host "`nRelease package created:" -ForegroundColor Green
    Write-Host "  Path: $zipPath" -ForegroundColor Green
    Write-Host "  Size: ${zipSize} MB" -ForegroundColor Green
}

# ── Summary ────────────────────────────────────────────────────────────────
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Build Successful!" -ForegroundColor Green
Write-Host "Version: $Version" -ForegroundColor Green
Write-Host "Runtime: $Runtime" -ForegroundColor Green
Write-Host "Publish directory: $publishDir" -ForegroundColor Green
if (-not $NoPack) {
    Write-Host "Archive: $zipPath" -ForegroundColor Green
}
Write-Host "========================================" -ForegroundColor Cyan

Write-Host ""
Write-Host "To upload to GitHub Releases:" -ForegroundColor Yellow
Write-Host "  gh release create v$Version '$zipPath' --generate-notes" -ForegroundColor Yellow
Write-Host ""