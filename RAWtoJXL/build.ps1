$ErrorActionPreference = "Continue"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
if ([string]::IsNullOrEmpty($scriptDir)) { $scriptDir = Get-Location }

$projectName = "RAWtoJXL.Avalonia/RAWtoJXL.Avalonia.csproj"
$cliProjectName = "RAWtoJXL.Cli/RAWtoJXL.Cli.csproj"
$testProject = "RAWtoJXL.Tests/RAWtoJXL.Tests.csproj"
$runtime = "win-x64"
$configuration = "Release"
$cjxlVersion = "0.11.2"
$cjxlUrl = "https://github.com/libjxl/libjxl/releases/download/v$cjxlVersion/jxl-x64-windows-static.zip"
$cjxlPath = Join-Path $scriptDir "cjxl.exe"
$djxlPath = Join-Path $scriptDir "djxl.exe"
$exiftoolVersion = "13.57"
$exiftoolUrl = "https://sourceforge.net/projects/exiftool/files/exiftool-$exiftoolVersion_64.zip/download"
$exiftoolPath = Join-Path $scriptDir "exiftool.exe"
$publishDir = Join-Path $scriptDir "RAWtoJXL.Avalonia\bin\$configuration\net8.0\$runtime\publish"
$cliPublishDir = Join-Path $scriptDir "RAWtoJXL.Cli\bin\$configuration\net8.0\$runtime\publish"

Write-Host "Starting build process from $scriptDir..." -ForegroundColor Cyan

if (-not (Test-Path $cjxlPath) -or -not (Test-Path $djxlPath)) {
    Write-Host "Downloading libjxl tools v$cjxlVersion..." -ForegroundColor Cyan
    $tempZip = Join-Path $env:TEMP "rawtojxl-jxl.zip"
    $tempExtract = Join-Path $env:TEMP "rawtojxl-jxl"
    try {
        curl.exe -L -s -o $tempZip $cjxlUrl
        if ($LASTEXITCODE -ne 0) {
            throw "libjxl download failed (curl exit $LASTEXITCODE)."
        }
        if (Test-Path $tempExtract) {
            Remove-Item $tempExtract -Recurse -Force
        }
        New-Item -ItemType Directory -Path $tempExtract -Force | Out-Null
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        [System.IO.Compression.ZipFile]::ExtractToDirectory($tempZip, $tempExtract)

        $foundCjxl = Get-ChildItem $tempExtract -Filter "cjxl.exe" -Recurse | Select-Object -First 1
        $foundDjxl = Get-ChildItem $tempExtract -Filter "djxl.exe" -Recurse | Select-Object -First 1
        if (-not $foundCjxl -or -not $foundDjxl) {
            throw "cjxl.exe or djxl.exe was not found in the downloaded archive."
        }
        Copy-Item $foundCjxl.FullName $cjxlPath -Force
        Copy-Item $foundDjxl.FullName $djxlPath -Force
        Write-Host "cjxl.exe and djxl.exe downloaded successfully." -ForegroundColor Green
    } catch {
        Write-Host "Error: Failed to download libjxl tools: $_" -ForegroundColor Red
        exit 1
    } finally {
        Remove-Item $tempZip -Force -ErrorAction SilentlyContinue
        Remove-Item $tempExtract -Recurse -Force -ErrorAction SilentlyContinue
    }
} else {
    Write-Host "cjxl.exe and djxl.exe found." -ForegroundColor Cyan
}

Write-Host "Checking exiftool.exe..." -ForegroundColor Cyan
if (-not (Test-Path $exiftoolPath)) {
    Write-Host "Downloading exiftool.exe v$exiftoolVersion..." -ForegroundColor Cyan
    $tempZip = Join-Path $env:TEMP "exiftool.zip"
    $downloadSuccess = $false
    try {
        curl.exe -L -s `
            -A "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36" `
            -H "Accept: text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8" `
            -H "Accept-Language: en-US,en;q=0.9" `
            -o $tempZip $exiftoolUrl
        $bytes = [System.IO.File]::ReadAllBytes($tempZip)
        if ($bytes[0] -eq 80 -and $bytes[1] -eq 75) {
            Add-Type -AssemblyName System.IO.Compression.FileSystem
            [System.IO.Compression.ZipFile]::ExtractToDirectory($tempZip, $scriptDir, $true)
            $extracted = Get-ChildItem $scriptDir -Filter "exiftool(-k).exe" -Recurse | Select-Object -First 1
            if ($extracted) {
                Copy-Item $extracted.FullName $exiftoolPath -Force
                Remove-Item $extracted.FullName -Force -ErrorAction SilentlyContinue
                $downloadSuccess = $true
            }
        }
        Remove-Item $tempZip -Force -ErrorAction SilentlyContinue
        Remove-Item (Join-Path $scriptDir "exiftool_files") -Recurse -Force -ErrorAction SilentlyContinue
        if ($downloadSuccess) {
            Write-Host "exiftool.exe downloaded successfully." -ForegroundColor Green
        } else {
            throw "Downloaded file is not a valid ZIP archive."
        }
    } catch {
        Write-Host "Warning: Failed to download exiftool.exe: $_" -ForegroundColor Yellow
        Write-Host "Please download manually from: https://exiftool.org/" -ForegroundColor Yellow
        Write-Host "Extract exiftool(-k).exe and exiftool_files folder to: $scriptDir" -ForegroundColor Yellow
        Write-Host "Rename exiftool(-k).exe to exiftool.exe" -ForegroundColor Yellow
    }
} else {
    Write-Host "exiftool.exe found at $exiftoolPath" -ForegroundColor Cyan
}

$rawTherapeeCliPath = $env:RAWTOJXL_RAWTHERAPEE_CLI
if (-not $rawTherapeeCliPath -or -not (Test-Path $rawTherapeeCliPath)) {
    $rawTherapeeCommand = Get-Command "rawtherapee-cli.exe" -ErrorAction SilentlyContinue
    $rawTherapeeCliPath = if ($rawTherapeeCommand) { $rawTherapeeCommand.Source } else { $null }
}
if (-not $rawTherapeeCliPath) {
    $rawTherapeeRoot = Join-Path $env:ProgramFiles "RawTherapee"
    if (Test-Path $rawTherapeeRoot) {
        $rawTherapeeCliPath = Get-ChildItem $rawTherapeeRoot -Filter "rawtherapee-cli.exe" -Recurse |
            Select-Object -ExpandProperty FullName -First 1
    }
}
if ($rawTherapeeCliPath) {
    Write-Host "RawTherapee CLI found at $rawTherapeeCliPath" -ForegroundColor Cyan
} else {
    Write-Host "Warning: RawTherapee CLI was not found. RAW previews will use the lower-performance Magick.NET fallback." -ForegroundColor Yellow
    Write-Host "Install RawTherapee from https://rawtherapee.com/downloads/ or set RAWTOJXL_RAWTHERAPEE_CLI." -ForegroundColor Yellow
}

Write-Host "Copying cjxl, djxl and exiftool to publish directories..." -ForegroundColor Cyan
foreach ($dir in @($publishDir, $cliPublishDir)) {
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
    Copy-Item $cjxlPath -Destination (Join-Path $dir "cjxl.exe") -Force
    if (Test-Path $djxlPath) {
        Copy-Item $djxlPath -Destination (Join-Path $dir "djxl.exe") -Force
    }
    if (Test-Path $exiftoolPath) {
        Copy-Item $exiftoolPath -Destination (Join-Path $dir "exiftool.exe") -Force
    }
}

Write-Host "Building project..." -ForegroundColor Cyan
dotnet restore "$scriptDir/$projectName"
if ($LASTEXITCODE -ne 0) {
    Write-Host "Restore failed!" -ForegroundColor Red
    Read-Host "Press Enter to exit"
    exit 1
}


Write-Host "Publishing application..." -ForegroundColor Cyan
dotnet publish "$scriptDir/$projectName" `
    -c $configuration `
    -r $runtime `
    --self-contained true `
    -p:DebugType=None `
    -p:DebugSymbols=false

Write-Host "Publishing CLI..." -ForegroundColor Cyan
dotnet publish "$scriptDir/$cliProjectName" `
    -c $configuration `
    -r $runtime `
    --self-contained true `
    -p:DebugType=None `
    -p:DebugSymbols=false

if ($LASTEXITCODE -eq 0) {
    Write-Host "`nBuild Successful!" -ForegroundColor Green
    Write-Host "GUI executable: $publishDir" -ForegroundColor Green
    Write-Host "CLI executable: $cliPublishDir\rawtojxl-cli.exe" -ForegroundColor Green
    Write-Host "cjxl.exe and djxl.exe are included in both publish directories." -ForegroundColor Green
} else {
    Write-Host "`nBuild Failed!" -ForegroundColor Red
}

Read-Host "Press Enter to exit"
