# Builds native/rawspeed + native/rawspeed-cli and stages the result into
# RAWtoJXL\RawSpeedTools\ (bundled by the csproj like cjxl.exe / Darktable).
# Requires MSVC (Visual Studio Build Tools or VS 2022 with "Desktop C++").
#   winget install Microsoft.VisualStudio.2022.BuildTools --override "--wait --quiet --add Microsoft.VisualStudio.Workload.VCTools --includeRecommended"
# Usage:  pwsh -File tools\build-rawspeed.ps1

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$build = Join-Path $root "native\build"
$out = Join-Path $root "RawSpeedTools"
$thirdParty = Join-Path $root "native\third_party"

New-Item -ItemType Directory -Force -Path $build, $out, $thirdParty | Out-Null

# Fetch bundled pugixml + zlib sources (rawspeed's own in-tree build uses these)
$pugixmlZip = Join-Path $thirdParty "pugixml.zip"
$zlibTar    = Join-Path $thirdParty "zlib.tar.gz"
$fetchOk = $true
if (-not (Test-Path $pugixmlZip) -or (Get-Item $pugixmlZip).Length -lt 100000) {
  curl.exe -f -L -s -o $pugixmlZip "https://github.com/pugixml/pugixml/releases/download/v1.14/pugixml-1.14.zip"
  if ($LASTEXITCODE -ne 0 -or (Get-Item $pugixmlZip).Length -lt 100000) { $fetchOk = $false }
}
if (-not (Test-Path $zlibTar) -or (Get-Item $zlibTar).Length -lt 100000) {
  curl.exe -f -L -s -o $zlibTar "https://github.com/zlib-ng/zlib-ng/archive/refs/tags/2.2.1.tar.gz"
  if ($LASTEXITCODE -ne 0 -or (Get-Item $zlibTar).Length -lt 100000) { $fetchOk = $false }
}

$withPugixml = "ON"
$withZlib = "ON"
if (-not $fetchOk) {
  Write-Warning "Third-party downloads failed; building rawspeed without XML/ZLIB. Unsupported RAWs will fall back to Magick."
  $withPugixml = "OFF"
  $withZlib = "OFF"
} else {
  $pugixmlTree = Join-Path $thirdParty "pugixml-1.14"
  $zlibTree    = Join-Path $thirdParty "zlib-ng-2.2.1"
  if (-not (Test-Path $pugixmlTree)) {
    tar.exe -xf $pugixmlZip -C $thirdParty 2>$null
    if (-not (Test-Path $pugixmlTree)) { Write-Error "pugixml extraction failed"; exit 1 }
  }
  if (-not (Test-Path $zlibTree)) {
    tar.exe -xzf $zlibTar -C $thirdParty 2>$null
    if (-not (Test-Path $zlibTree)) { Write-Error "zlib extraction failed"; exit 1 }
  }
}

if ($withPugixml -eq "ON") {
  cmake -S (Join-Path $root "native") -B $build -A x64 `
    -DBUILD_TESTING=OFF -DBUILD_BENCHMARKING=OFF `
    -DWITH_JPEG=OFF `
    -DWITH_PUGIXML=ON -DUSE_BUNDLED_PUGIXML=ON -DPUGIXML_PATH=$pugixmlTree -DALLOW_DOWNLOADING_PUGIXML=ON `
    -DWITH_ZLIB=ON -DUSE_BUNDLED_ZLIB=ON -DZLIB_PATH=$zlibTree -DALLOW_DOWNLOADING_ZLIB=ON `
    -DCMAKE_BUILD_TYPE=Release
} else {
  cmake -S (Join-Path $root "native") -B $build -A x64 `
    -DBUILD_TESTING=OFF -DBUILD_BENCHMARKING=OFF `
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
