$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$launcher = Get-Content (Join-Path $root "Start-PrivacyBrowser.ps1") -Raw

$required = @(
    'app\PrivacyBrowser.App.exe',
    '"--bundle-root", $PSScriptRoot',
    '"--browser-exe", (Resolve-Path $BrowserExe).Path',
    '"--backend-exe", $BackendExe',
    '"--profile", $ProfilePath',
    '"--initial-url", $InitialUrl',
    '& $NativeAppExe @arguments'
)
foreach ($needle in $required) {
    if (-not $launcher.Contains($needle)) { throw "Launcher invariant missing: $needle" }
}

$forbidden = @(
    '127.0.0.1:44051',
    '--web-ui-port',
    'Invoke-RestMethod',
    'Start-Process -FilePath $BackendExe'
)
foreach ($needle in $forbidden) {
    if ($launcher.Contains($needle)) { throw "Removed web UI dependency is still present: $needle" }
}

if ($launcher -match 'netsh\s+winhttp\s+set|Set-ItemProperty.+Internet Settings|New-NetRoute|Set-DnsClientServerAddress|New-NetFirewallRule') {
    throw "Launcher contains a forbidden system-wide network mutation"
}

Write-Host "PASS: launcher delegates to the native app without a localhost web UI dependency."
