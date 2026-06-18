$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$launcher = Get-Content (Join-Path $root "Start-PrivacyBrowser.ps1") -Raw

$required = @(
    'Get-NetTCPConnection -State Listen -LocalPort $ProxyPort',
    'LocalAddress -notin @("127.0.0.1", "::1")',
    'owned by unexpected process',
    'Get-BackendConnected',
    'BackendProcess.HasExited',
    'taskkill.exe /PID $BackendProcess.Id /T /F',
    '"-no-remote", "-new-instance", "-profile"',
    '$arguments += $InitialUrl'
)
foreach ($needle in $required) {
    if (-not $launcher.Contains($needle)) { throw "Launcher invariant missing: $needle" }
}

if ($launcher -match 'netsh\s+winhttp\s+set|Set-ItemProperty.+Internet Settings|New-NetRoute|Set-DnsClientServerAddress|New-NetFirewallRule') {
    throw "Launcher contains a forbidden system-wide network mutation"
}

Write-Host "PASS: launcher ownership, lifecycle, isolation, and no-system-mutation invariants are present."
