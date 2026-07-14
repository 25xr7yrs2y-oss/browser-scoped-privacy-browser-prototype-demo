[CmdletBinding()]
param([string]$ReleaseDirectory = (Join-Path (Split-Path $PSScriptRoot -Parent) "release"))

$ErrorActionPreference = "Stop"
$version = "1.0.0"
$base = "PrivacyBrowser-$version-windows-x64-portable"
$zip = Join-Path $ReleaseDirectory "$base.zip"
$source = Join-Path $ReleaseDirectory "PrivacyBrowser-$version-myst-lmprove-source-227d63b.tar.gz"
$checksums = Join-Path $ReleaseDirectory "PrivacyBrowser-$version-SHA256SUMS.txt"

foreach ($path in @($zip, $source, $checksums)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Release artifact missing: $path" }
    if ((Get-Item -LiteralPath $path).Length -eq 0) { throw "Release artifact is empty: $path" }
}
if ((Get-Item -LiteralPath $zip).Length -lt 100MB) { throw "Portable release is unexpectedly small and may omit dependencies." }
if ((Get-Item -LiteralPath $source).Length -lt 1MB) { throw "Corresponding source archive is unexpectedly small." }

$expectedLines = Get-Content -LiteralPath $checksums
foreach ($path in @($zip, $source)) {
    $actual = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    $name = [IO.Path]::GetFileName($path)
    if ("$actual  $name" -notin $expectedLines) { throw "Checksum file does not match $name." }
}

$temp = Join-Path ([IO.Path]::GetTempPath()) ("privacy-browser-verify-" + [Guid]::NewGuid().ToString("N"))
try {
    Expand-Archive -LiteralPath $zip -DestinationPath $temp -Force
    $root = Join-Path $temp $base
    $exe = Join-Path $root "PrivacyBrowser.exe"
    $browser = Join-Path $root "vendor\mullvad-browser\mullvadbrowser.exe"
    $backend = Join-Path $root "vendor\myst-lmprove\resources\app.asar.unpacked\node_modules\@mysteriumnetwork\node\bin\win\x64\myst.exe"
    foreach ($path in @($exe, $browser, $backend, (Join-Path $root "config\policies.json"), (Join-Path $root "docs\SOURCE_OFFER.md"))) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Portable package content missing: $path" }
    }
    if (Get-ChildItem -LiteralPath $root -Recurse -File -Filter "MysteriumDark-Setup*.exe") {
        throw "Portable package must not contain or execute the upstream service-installing backend installer."
    }
    $info = (Get-Item -LiteralPath $exe).VersionInfo
    if ($info.FileVersion -ne "1.0.0.0" -or -not $info.ProductVersion.StartsWith("1.0.0")) {
        throw "Packaged executable version metadata is incorrect."
    }
    Add-Type -AssemblyName System.Drawing
    $icon = [Drawing.Icon]::ExtractAssociatedIcon($exe)
    if (-not $icon) { throw "Packaged executable has no extractable application icon." }
    $icon.Dispose()
} finally {
    if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Recurse -Force }
}

Write-Host "PASS: portable package, dependencies, source offer, checksums, icon, and version metadata are valid."
