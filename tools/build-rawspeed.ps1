# Builds native/rawspeed + native/rawspeed-cli and stages the result into
# RAWtoJXL\RawSpeedTools\ (bundled by the csproj like cjxl.exe).
# Requires MSVC (Visual Studio Build Tools or VS 2022 with "Desktop C++").
#   winget install Microsoft.VisualStudio.2022.BuildTools --override "--wait --quiet --add Microsoft.VisualStudio.Workload.VCTools --includeRecommended"
# Usage:  pwsh -File tools\build-rawspeed.ps1

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$build = Join-Path $root "native\build"
$out = Join-Path $root "RawSpeedTools"
$thirdParty = Join-Path $root "native\rawspeed\third_party"
$compatHeader = Join-Path $root "native\rawspeed-cli\msvc_compat.h"

New-Item -ItemType Directory -Force -Path $build, $out, $thirdParty | Out-Null

$pugixmlTree = Join-Path $thirdParty "pugixml-1.14"
$zlibTree    = Join-Path $thirdParty "zlib-ng-2.2.1"

$withPugixml = if (Test-Path $pugixmlTree) { "ON" } else { "OFF" }
$withZlib = if (Test-Path $zlibTree) { "ON" } else { "OFF" }
if ($withPugixml -eq "OFF" -or $withZlib -eq "OFF") {
  Write-Warning "Vendored pugixml/zlib missing under native\rawspeed\third_party; building rawspeed without XML/ZLIB. Unsupported RAWs will fall back to Magick."
}


if ($withPugixml -eq "ON") {
  cmake -S (Join-Path $root "native") -B $build -A x64 `
    -DCMAKE_CXX_FLAGS="/Zc:preprocessor;/utf-8" -DWITH_OPENMP=OFF -DBUILD_TESTING=OFF -DBUILD_BENCHMARKING=OFF `
    -DWITH_JPEG=OFF `
    -DWITH_PUGIXML=ON -DUSE_BUNDLED_PUGIXML=ON -DPUGIXML_PATH="$($pugixmlTree.Replace('\','/'))" -DALLOW_DOWNLOADING_PUGIXML=OFF `
    -DWITH_ZLIB=ON -DUSE_BUNDLED_ZLIB=ON -DZLIB_PATH="$($zlibTree.Replace('\','/'))" -DALLOW_DOWNLOADING_ZLIB=OFF `
    -DCMAKE_BUILD_TYPE=Release
} else {
  cmake -S (Join-Path $root "native") -B $build -A x64 `
    -DCMAKE_CXX_FLAGS="/Zc:preprocessor;/utf-8" -DWITH_OPENMP=OFF -DBUILD_TESTING=OFF -DBUILD_BENCHMARKING=OFF `
    -DWITH_JPEG=OFF -DWITH_PUGIXML=OFF -DWITH_ZLIB=OFF `
    -DCMAKE_BUILD_TYPE=Release
}
if ($LASTEXITCODE -ne 0) { Write-Error "cmake configure failed"; exit 1 }

cmake --build $build --config Release --target rawspeed-cli
if ($LASTEXITCODE -ne 0) { Write-Error "rawspeed build failed"; exit 1 }

$stage = Join-Path $build "Release"
Copy-Item (Join-Path $stage "rawspeed-cli.exe") $out -Force
Get-ChildItem $stage -Filter "*.dll" | Copy-Item -Destination $out -Force

& (Join-Path $out "rawspeed-cli.exe") 2>$null
if ($LASTEXITCODE -eq 2) { Write-Output "rawspeed-cli staged to $out" } else { Write-Warning "rawspeed-cli staged but sanity check returned $LASTEXITCODE" }
