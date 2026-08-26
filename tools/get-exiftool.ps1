# Downloads exiftool into the solution directory (RAWtoJXL/) where the csproj
# picks it up and copies it next to the built binaries. Mirrors the CI step in
# .github/workflows/build-release.yml.
# Usage:  pwsh -File tools\get-exiftool.ps1

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$solutionDir = Join-Path $root "RAWtoJXL"
$version = "13.57"

if ((Test-Path (Join-Path $solutionDir "exiftool.exe"))) {
  Write-Output "exiftool.exe already present at $solutionDir"
  exit 0
}

$url = "https://downloads.sourceforge.net/project/exiftool/exiftool-${version}_64.zip"
$zip = Join-Path $env:TEMP "exiftool.zip"

curl.exe -f -L -s -o $zip $url
if ($LASTEXITCODE -ne 0) { Write-Error "exiftool download failed (curl exit $LASTEXITCODE). URL: $url"; exit 1 }

$bytes = [System.IO.File]::ReadAllBytes($zip)
if ($bytes.Length -lt 2 -or $bytes[0] -ne 80 -or $bytes[1] -ne 75) {
  Write-Error "exiftool download failed: expected a ZIP archive but got $($bytes.Length) bytes (proxy interception?). URL: $url"
  exit 1
}

$dest = Join-Path $env:TEMP "exiftool-extract"
New-Item -ItemType Directory -Path $dest -Force | Out-Null
Expand-Archive -Path $zip -DestinationPath $dest -Force

$exe = Get-ChildItem $dest -Filter "exiftool*.exe" -Recurse | Select-Object -First 1
if (-not $exe) { Write-Error "exiftool.exe not found in archive!"; exit 1 }
Copy-Item $exe.FullName (Join-Path $solutionDir "exiftool.exe") -Force

$filesDir = Get-ChildItem $dest -Filter "exiftool_files" -Directory -Recurse | Select-Object -First 1
if ($filesDir) {
  Copy-Item $filesDir.FullName (Join-Path $solutionDir "exiftool_files") -Recurse -Force
}

& (Join-Path $solutionDir "exiftool.exe") -ver
if ($LASTEXITCODE -ne 0) { Write-Error "exiftool.exe version check failed (exit $LASTEXITCODE)"; exit 1 }

Remove-Item $zip, $dest -Recurse -Force -ErrorAction SilentlyContinue
Write-Output "exiftool $version installed to $solutionDir"
