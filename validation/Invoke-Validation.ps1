[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$ModifiedBrowserExe,
    [string]$OutputDirectory = (Join-Path (Split-Path $PSScriptRoot -Parent) "evidence")
)

$ErrorActionPreference = "Stop"
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Run this evidence collector from an elevated PowerShell prompt."
}
if (-not (Test-Path $ModifiedBrowserExe -PathType Leaf)) {
    throw "Modified browser executable not found: $ModifiedBrowserExe"
}

$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$run = Join-Path $OutputDirectory $stamp
New-Item -ItemType Directory -Path $run -Force | Out-Null

function Save-Snapshot([string]$Name) {
    $path = Join-Path $run "$Name.txt"
    @(
        "=== timestamp ==="
        (Get-Date -Format o)
        "=== processes ==="
        (Get-Process | Select-Object Id, ProcessName, Path | Format-Table -AutoSize | Out-String -Width 260)
        "=== TCP ==="
        (Get-NetTCPConnection | Sort-Object OwningProcess, LocalPort | Format-Table -AutoSize | Out-String -Width 260)
        "=== UDP ==="
        (Get-NetUDPEndpoint | Sort-Object OwningProcess, LocalPort | Format-Table -AutoSize | Out-String -Width 260)
        "=== netstat -abno ==="
        (& netstat.exe -abno | Out-String -Width 260)
        "=== routes ==="
        (Get-NetRoute | Sort-Object InterfaceIndex, DestinationPrefix | Format-Table -AutoSize | Out-String -Width 260)
        "=== DNS ==="
        (Get-DnsClientServerAddress | Format-Table -AutoSize | Out-String -Width 260)
        "=== adapters ==="
        (Get-NetAdapter -IncludeHidden | Format-Table -AutoSize | Out-String -Width 260)
        "=== WinHTTP proxy ==="
        (& netsh.exe winhttp show proxy | Out-String)
        "=== user Internet Settings ==="
        (Get-ItemProperty "HKCU:\Software\Microsoft\Windows\CurrentVersion\Internet Settings" | Out-String -Width 260)
    ) | Set-Content -Path $path -Encoding UTF8
}

Save-Snapshot "00-before"

$etl = Join-Path $run "capture.etl"
$pcap = Join-Path $run "capture.pcapng"
& pktmon.exe stop 2>$null | Out-Null
& pktmon.exe filter remove | Out-Null
& pktmon.exe start --capture --comp nics --pkt-size 0 --file-name $etl | Out-Null
Write-Host "Capture started. Perform one named validation scenario, then press Enter."
[void](Read-Host)
Save-Snapshot "01-during"
& pktmon.exe stop | Out-Null
& pktmon.exe etl2pcap $etl --out $pcap | Out-Null
Save-Snapshot "02-after"

@{
    timestamp = (Get-Date -Format o)
    modifiedBrowserExe = (Resolve-Path $ModifiedBrowserExe).Path
    capture = $pcap
    note = "Classify this single action using docs/VALIDATION_PLAN.md and record the result."
} | ConvertTo-Json | Set-Content (Join-Path $run "manifest.json") -Encoding UTF8

Write-Host "Evidence written to $run"

