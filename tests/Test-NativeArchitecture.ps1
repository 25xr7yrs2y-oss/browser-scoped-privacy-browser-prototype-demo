$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$sourceRoot = Join-Path $root "src\PrivacyBrowser.App"
$sourceFiles = Get-ChildItem $sourceRoot -File -Recurse -Include @("*.cs", "*.xaml")
$allSource = ($sourceFiles | Get-Content -Raw) -join "`n"
$backend = Get-Content (Join-Path $sourceRoot "BackendController.cs") -Raw
$window = Get-Content (Join-Path $sourceRoot "MainWindow.xaml") -Raw
$browser = Get-Content (Join-Path $sourceRoot "BrowserLauncher.cs") -Raw

$required = @(
    '--ui.enable=false',
    '--usermode',
    '--proxymode',
    '--proxy.bind.address=127.0.0.1',
    '--tequilapi.address=127.0.0.1',
    'ControlPort = 44050',
    'ProxyPort = 4449',
    'UseProxy = false',
    'agreed_consumer = true',
    'Kill(entireProcessTree: true)'
)
foreach ($needle in $required) {
    if (-not $backend.Contains($needle)) { throw "Native backend invariant missing: $needle" }
}

if ($allSource.Contains('44051') -or $allSource.Contains('web-ui-port') -or $allSource.Contains('shell.openExternal')) {
    throw "Native source still references the removed browser-based web UI"
}
if (-not $window.Contains('HorizontalAlignment="Right"') -or -not $window.Contains('Content="Controls"')) {
    throw "The controls entry must remain in the upper-right area of the native window"
}
if (-not $browser.Contains('Refusing to overwrite an unrelated browser policy') -or
    -not $browser.Contains('IsOwnedProxyListening')) {
    throw "Browser launch ownership or policy safety invariant is missing"
}

Write-Host "PASS: native WPF UI, direct node ownership, port removal, and proxy safety invariants are present."
