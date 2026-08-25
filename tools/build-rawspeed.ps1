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
$compatHeader = Join-Path $root "native\rawspeed-cli\msvc_compat.h"

New-Item -ItemType Directory -Force -Path $build, $out, $thirdParty | Out-Null

# Fetch bundled pugixml + zlib sources (rawspeed's own in-tree build uses these)
$pugixmlTar = Join-Path $thirdParty "pugixml.tar.gz"
$zlibTar    = Join-Path $thirdParty "zlib.tar.gz"
$fetchOk = $true
foreach ($dl in @(@{ f = $pugixmlTar; u = "https://github.com/pugixml/pugixml/archive/refs/tags/v1.14.tar.gz" }, @{ f = $zlibTar; u = "https://github.com/zlib-ng/zlib-ng/archive/refs/tags/2.2.1.tar.gz" })) {
  $need = (-not (Test-Path $dl.f)) -or (Get-Item $dl.f).Length -lt 100000
  if ($need) {
    curl.exe -f -L -s -o $dl.f $dl.u
    $okSize = (Test-Path $dl.f) -and (Get-Item $dl.f).Length -ge 100000
    if (-not $okSize) { $fetchOk = $false; continue }
    $b = [System.IO.File]::ReadAllBytes($dl.f)[0..1]
    if (-not ($b[0] -eq 0x1F -and $b[1] -eq 0x8B)) { Remove-Item $dl.f -Force; $fetchOk = $false }
  }
}

$pugixmlTree = Join-Path $thirdParty "pugixml-1.14"
$zlibTree    = Join-Path $thirdParty "zlib-ng-2.2.1"

$withPugixml = "ON"
$withZlib = "ON"
if (-not $fetchOk) {
  Write-Warning "Third-party downloads failed; building rawspeed without XML/ZLIB. Unsupported RAWs will fall back to Magick."
  $withPugixml = "OFF"
  $withZlib = "OFF"
} else {
  if (-not (Test-Path $pugixmlTree)) {
    tar.exe -xzf $pugixmlTar -C $thirdParty 2>$null
    if (-not (Test-Path $pugixmlTree)) { Write-Error "pugixml extraction failed"; exit 1 }
  }
  if (-not (Test-Path $zlibTree)) {
    tar.exe -xzf $zlibTar -C $thirdParty 2>$null
    if (-not (Test-Path $zlibTree)) { Write-Error "zlib extraction failed"; exit 1 }
  }
}

# Patch rawspeed for MSVC compatibility (upstream uses GCC/clang-cl on Windows;
# its config.h.in hardcodes __attribute__ macros which MSVC rejects)
$config = Join-Path $root "native\rawspeed\src\config.h.in"
$configText = Get-Content $config -Raw
if ($configText -notmatch 'defined\(_MSC_VER\) && !defined\(__clang__\)') {
  $old = @(
    '#define RAWSPEED_UNLIKELY_FUNCTION __attribute__((cold))',
    '#define RAWSPEED_NOINLINE __attribute__((noinline))',
    '#define RAWSPEED_READONLY __attribute__((pure))',
    '#define RAWSPEED_READNONE __attribute__((const))'
  )
  $new = @(
    '#if defined(_MSC_VER) && !defined(__clang__)',
    '#define RAWSPEED_UNLIKELY_FUNCTION',
    '#define RAWSPEED_NOINLINE __declspec(noinline)',
    '#define RAWSPEED_READONLY',
    '#define RAWSPEED_READNONE',
    '#define RAWSPEED_ALWAYS_INLINE __forceinline',
    '#else'
  ) + $old + @(
    '#define RAWSPEED_ALWAYS_INLINE __attribute__((always_inline))',
    '#endif'
  )
  foreach ($line in $old) {
    if (-not $configText.Contains($line)) { Write-Error "patch anchor missing: $line"; exit 1 }
  }
  $pattern = ($old | ForEach-Object { [regex]::Escape($_) }) -join '\r?\n'
  $configText = $configText -replace $pattern, ($new -join "`n")
  if ($configText -notmatch 'defined\(_MSC_VER\) && !defined\(__clang__\)') { Write-Error "config.h.in patch did not apply"; exit 1 }
  Set-Content -LiteralPath $config -Value $configText -NoNewline
  Write-Output "config.h.in patched for MSVC."
}

$common = Join-Path $root "native\rawspeed\src\librawspeed\common\Common.h"
$commonText = Get-Content $common -Raw
if ($commonText -match '__attribute__\(\(format\(printf' -and $commonText -notmatch '!defined\(_MSC_VER\)') {
  $oldDecl = @(
    'void writeLog(DEBUG_PRIO priority, const char* format, ...)',
    '    __attribute__((format(printf, 2, 3)));'
  )
  $newDecl = @(
    '#if !defined(_MSC_VER) || defined(__clang__)'
  ) + $oldDecl + @(
    '#else',
    'void writeLog(DEBUG_PRIO priority, const char* format, ...);',
    '#endif'
  )
  $pattern = ($oldDecl | ForEach-Object { [regex]::Escape($_) }) -join '\r?\n'
  $commonText = $commonText -replace $pattern, ($newDecl -join "`n")
  if ($commonText -notmatch '!defined\(_MSC_VER\)') { Write-Error "Common.h patch did not apply"; exit 1 }
  Set-Content -LiteralPath $common -Value $commonText -NoNewline
  Write-Output "Common.h patched for MSVC."
}

$dng = Join-Path $root "native\rawspeed\src\librawspeed\decompressors\AbstractDngDecompressor.cpp"
$dngText = Get-Content $dng -Raw
if ($dngText -notmatch 'Array1DRef<const DngSliceElement>\(slices\.data') {
  $dngText = $dngText -replace [regex]::Escape('Array1DRef(slices.data(), implicit_cast<int>(slices.size()))'),
    'Array1DRef<const DngSliceElement>(slices.data(), implicit_cast<int>(slices.size()))'
  if ($dngText -notmatch 'Array1DRef<const DngSliceElement>\(slices\.data') { Write-Error "DNG CTAD patch did not apply"; exit 1 }
  Set-Content -LiteralPath $dng -Value $dngText -NoNewline
  Write-Output "AbstractDngDecompressor.cpp patched for MSVC CTAD."
}

$ljpeg = Join-Path $root "native\rawspeed\src\librawspeed\decompressors\LJpegDecompressor.cpp"
$ljpegText = [System.IO.File]::ReadAllText($ljpeg)
if ($ljpegText.Contains([string][char]0x0421)) {
  $ljpegText = $ljpegText.Replace([string][char]0x0421, "C")
  [System.IO.File]::WriteAllText($ljpeg, $ljpegText)
  Write-Output "LJpegDecompressor.cpp cyrillic homoglyph patched."
}

if ($withPugixml -eq "ON") {
  cmake -S (Join-Path $root "native") -B $build -A x64 `
    -DCMAKE_CXX_FLAGS="/Zc:preprocessor;/utf-8" -DWITH_OPENMP=OFF -DBUILD_TESTING=OFF -DBUILD_BENCHMARKING=OFF `
    -DWITH_JPEG=OFF `
    -DWITH_PUGIXML=ON -DUSE_BUNDLED_PUGIXML=ON -DPUGIXML_PATH=$pugixmlTree -DALLOW_DOWNLOADING_PUGIXML=ON `
    -DWITH_ZLIB=ON -DUSE_BUNDLED_ZLIB=ON -DZLIB_PATH=$zlibTree -DALLOW_DOWNLOADING_ZLIB=ON `
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
