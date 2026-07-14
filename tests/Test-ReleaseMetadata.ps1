$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$project = [xml](Get-Content (Join-Path $root "src\PrivacyBrowser.App\PrivacyBrowser.App.csproj") -Raw)
$properties = $project.Project.PropertyGroup

function Assert-Equal($Actual, $Expected, [string]$Message) {
    if ($Actual -ne $Expected) { throw "$Message (expected '$Expected', got '$Actual')" }
}

Assert-Equal $properties.Version "1.0.0" "Application version must be 1.0.0"
Assert-Equal $properties.PackageVersion "1.0.0" "Package version must be 1.0.0"
Assert-Equal $properties.AssemblyVersion "1.0.0.0" "Assembly version must be 1.0.0.0"
Assert-Equal $properties.FileVersion "1.0.0.0" "File version must be 1.0.0.0"
Assert-Equal $properties.InformationalVersion "1.0.0" "Informational version must be 1.0.0"
Assert-Equal $properties.ApplicationIcon "Assets\AppIcon.ico" "Executable icon declaration is missing"

$assets = Join-Path $root "src\PrivacyBrowser.App\Assets"
$iconPath = Join-Path $assets "AppIcon.ico"
$masterPath = Join-Path $assets "IconMaster.png"
$windowXaml = Get-Content (Join-Path $root "src\PrivacyBrowser.App\MainWindow.xaml") -Raw
$manifest = Get-Content (Join-Path $root "src\PrivacyBrowser.App\app.manifest") -Raw
if (-not $windowXaml.Contains('Icon="Assets/AppIcon.ico"')) { throw "The WPF window does not use the official icon." }
if (-not $manifest.Contains('assemblyIdentity version="1.0.0.0"')) { throw "Manifest version is not 1.0.0.0." }
if (-not $manifest.Contains('name="PrivacyBrowser"')) { throw "Manifest application identity is inconsistent." }

foreach ($path in @($iconPath, $masterPath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Required icon asset missing: $path" }
}
foreach ($size in @(16, 20, 24, 32, 40, 48, 64, 128, 256, 512)) {
    $png = Join-Path $assets "Icons\app-icon-$size.png"
    if (-not (Test-Path -LiteralPath $png -PathType Leaf)) { throw "PNG icon size missing: $size" }
}

$bytes = [IO.File]::ReadAllBytes($iconPath)
$count = [BitConverter]::ToUInt16($bytes, 4)
Assert-Equal $count 9 "ICO must contain nine size entries"
$icoSizes = @()
for ($i = 0; $i -lt $count; $i++) {
    $width = [int]$bytes[6 + ($i * 16)]
    if ($width -eq 0) { $width = 256 }
    $icoSizes += $width
}
foreach ($expected in @(16, 20, 24, 32, 40, 48, 64, 128, 256)) {
    if ($expected -notin $icoSizes) { throw "ICO size entry missing: $expected" }
}

$exe = Join-Path $root "app\PrivacyBrowser.exe"
if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { throw "Built executable missing: $exe" }
$version = (Get-Item -LiteralPath $exe).VersionInfo
Assert-Equal $version.FileVersion "1.0.0.0" "Executable file version is incorrect"
if (-not $version.ProductVersion.StartsWith("1.0.0")) { throw "Executable product version is incorrect: $($version.ProductVersion)" }

Add-Type -AssemblyName System.Drawing
$embeddedIcon = [Drawing.Icon]::ExtractAssociatedIcon($exe)
if (-not $embeddedIcon) { throw "Executable does not contain an extractable application icon." }
$embeddedBitmap = $embeddedIcon.ToBitmap()
$expectedBitmap = [Drawing.Bitmap]::FromFile((Join-Path $assets "Icons\app-icon-32.png"))
try {
    $comparison = New-Object Drawing.Bitmap 32, 32
    $graphics = [Drawing.Graphics]::FromImage($comparison)
    try { $graphics.DrawImage($embeddedBitmap, 0, 0, 32, 32) } finally { $graphics.Dispose() }
    $difference = 0L
    for ($y = 0; $y -lt 32; $y++) {
        for ($x = 0; $x -lt 32; $x++) {
            $a = $comparison.GetPixel($x, $y)
            $b = $expectedBitmap.GetPixel($x, $y)
            $difference += [Math]::Abs([int]$a.R - [int]$b.R)
            $difference += [Math]::Abs([int]$a.G - [int]$b.G)
            $difference += [Math]::Abs([int]$a.B - [int]$b.B)
        }
    }
    $meanDifference = $difference / (32 * 32 * 3)
    if ($meanDifference -gt 25) { throw "Embedded executable icon differs from the approved icon (mean channel difference $meanDifference)." }
} finally {
    if ($comparison) { $comparison.Dispose() }
    $expectedBitmap.Dispose()
    $embeddedBitmap.Dispose()
    $embeddedIcon.Dispose()
}

Write-Host "PASS: version 1.0.0 metadata and approved executable/window icon are embedded."
