$ErrorActionPreference = "Continue"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
if ([string]::IsNullOrEmpty($scriptDir)) { $scriptDir = Get-Location }

$projectName = "RAWtoJXL.Avalonia/RAWtoJXL.Avalonia.csproj"
$testProject = "RAWtoJXL.Tests/RAWtoJXL.Tests.csproj"
$runtime = "win-x64"
$configuration = "Release"
$cjxlVersion = "0.11.2"
$cjxlUrl = "https://github.com/libjxl/libjxl/releases/download/v$cjxlVersion/jxl-x64-windows-static.zip"
$cjxlPath = Join-Path $scriptDir "cjxl.exe"
$exiftoolVersion = "13.57"
$exiftoolUrl = "https://sourceforge.net/projects/exiftool/files/exiftool-$exiftoolVersion_64.zip/download"
$exiftoolPath = Join-Path $scriptDir "exiftool.exe"
$publishDir = Join-Path $scriptDir "RAWtoJXL.Avalonia\bin\$configuration\net8.0\$runtime\publish"

Write-Host "Starting build process from $scriptDir..." -ForegroundColor Cyan

Write-Host "cjxl.exe found at $cjxlPath" -ForegroundColor Cyan

if (-not (Test-Path $cjxlPath)) {
    Write-Host "Error: cjxl.exe not found at $cjxlPath" -ForegroundColor Red
    Read-Host "Press Enter to exit"
    exit 1
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
            Expand-Archive -Path $tempZip -DestinationPath $scriptDir -Force
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

Write-Host "Copying cjxl and exiftool to publish directory..." -ForegroundColor Cyan
if (-not (Test-Path $publishDir)) {
    New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
}
Copy-Item $cjxlPath -Destination (Join-Path $publishDir "cjxl.exe") -Force
if (Test-Path $exiftoolPath) {
    Copy-Item $exiftoolPath -Destination (Join-Path $publishDir "exiftool.exe") -Force
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

if ($LASTEXITCODE -eq 0) {
    Write-Host "`nBuild Successful!" -ForegroundColor Green
    Write-Host "The executable can be found in: $publishDir" -ForegroundColor Green
    Write-Host "cjxl.exe is included in the publish directory." -ForegroundColor Green
} else {
    Write-Host "`nBuild Failed!" -ForegroundColor Red
}

Read-Host "Press Enter to exit"
