# Builds native/rawspeed + native/rawspeed-cli and stages the result into
# RAWtoJXL\RawSpeedTools\ (bundled by the csproj like cjxl.exe / Darktable).
# Requires MSVC (Visual Studio Build Tools or VS 2022 with "Desktop C++").
#   winget install Microsoft.VisualStudio.2022.BuildTools --override "--wait --quiet --add Microsoft.VisualStudio.Workload.VCTools --includeRecommended"
# Usage:  pwsh -File tools\build-rawspeed.ps1

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$build = Join-Path $root "native\build"
$out = Join-Path $root "RawSpeedTools"

New-Item -ItemType Directory -Force -Path $build, $out | Out-Null

cmake -S (Join-Path $root "native") -B $build -A x64 -DBUILD_TESTING=OFF -DBUILD_BENCHMARKING=OFF -DUSE_BUNDLED_PUGIXML=ON -DWITH_JPEG=OFF -DUSE_BUNDLED_ZLIB=ON -DCMAKE_BUILD_TYPE=Release
cmake --build $build --config Release --target rawspeed-cli

$stage = Join-Path $build "Release"
Copy-Item (Join-Path $stage "rawspeed-cli.exe") $out -Force
Get-ChildItem $stage -Filter "*.dll" | Copy-Item -Destination $out -Force

& (Join-Path $out "rawspeed-cli.exe") 2>$null
if ($LASTEXITCODE -eq 2) { Write-Output "rawspeed-cli staged to $out" } else { Write-Warning "rawspeed-cli staged but sanity check returned $LASTEXITCODE" }
