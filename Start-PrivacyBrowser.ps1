[CmdletBinding()]
param(
    [string]$BrowserExe = (Join-Path $PSScriptRoot "vendor\mullvad-browser\mullvadbrowser.exe"),
    [string]$BackendExe = (Join-Path $PSScriptRoot "vendor\myst-lmprove\MysteriumVPN.exe"),
    [string]$ProfilePath = (Join-Path $PSScriptRoot "state\profile"),
    [ValidateRange(0, 3600)]
    [int]$BackendReadyTimeoutSeconds = 600,
    [string]$InitialUrl = "about:blank",
    [string[]]$AdditionalBrowserArguments = @(),
    [switch]$KeepBackendRunning,
    [switch]$SkipBackendLaunch
)

$ErrorActionPreference = "Stop"
$ProxyHost = "127.0.0.1"
$ProxyPort = 4449
$BackendApi = "http://127.0.0.1:44051"
$PolicySource = Join-Path $PSScriptRoot "config\policies.json"
$BackendProcess = $null
$BrowserProcess = $null

function Test-LoopbackProxyOwner {
    param([string]$ExpectedRoot)

    $listeners = @(Get-NetTCPConnection -State Listen -LocalPort $ProxyPort -ErrorAction SilentlyContinue)
    if ($listeners.Count -eq 0) { return $false }
    foreach ($listener in $listeners) {
        if ($listener.LocalAddress -notin @("127.0.0.1", "::1")) {
            throw "Proxy port $ProxyPort has a non-loopback listener: $($listener.LocalAddress)"
        }
        $owner = Get-Process -Id $listener.OwningProcess -ErrorAction Stop
        if (-not $owner.Path -or -not $owner.Path.StartsWith($ExpectedRoot, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Proxy port $ProxyPort is owned by unexpected process PID $($owner.Id): $($owner.Path)"
        }
    }
    return $true
}

function Get-BackendConnected {
    try {
        $status = Invoke-RestMethod -Uri "$BackendApi/api/status" -TimeoutSec 3
        $connectionState = $status.connection.data.status
        if (-not $connectionState) { $connectionState = $status.connection.data.state }
        return [string]$connectionState -ieq "CONNECTED"
    } catch {
        return $false
    }
}

function Install-BrowserPolicy {
    param([string]$FirefoxPath)

    $distribution = Join-Path (Split-Path $FirefoxPath -Parent) "distribution"
    $target = Join-Path $distribution "policies.json"
    New-Item -ItemType Directory -Path $distribution -Force | Out-Null
    if (Test-Path $target) {
        $existingHash = (Get-FileHash $target -Algorithm SHA256).Hash
        $sourceHash = (Get-FileHash $PolicySource -Algorithm SHA256).Hash
        if ($existingHash -ne $sourceHash) {
            throw "Refusing to overwrite an unrelated browser policy at $target"
        }
    }
    Copy-Item $PolicySource $target -Force
}

function Stop-OwnedBackend {
    if (-not $BackendProcess -or $KeepBackendRunning) { return }
    try {
        Invoke-RestMethod -Method Post -Uri "$BackendApi/api/node/stop" -TimeoutSec 10 | Out-Null
    } catch {
        Write-Warning "Backend API shutdown did not complete: $($_.Exception.Message)"
    }
    if (-not $BackendProcess.HasExited) {
        & taskkill.exe /PID $BackendProcess.Id /T /F 2>$null | Out-Null
    }
}

try {
    foreach ($requiredFile in @($BrowserExe, $PolicySource)) {
        if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
            throw "Required file not found: $requiredFile"
        }
    }
    if (-not $SkipBackendLaunch -and -not (Test-Path -LiteralPath $BackendExe -PathType Leaf)) {
        throw "Backend executable not found: $BackendExe"
    }

    $BrowserExe = (Resolve-Path $BrowserExe).Path
    $BackendRoot = if ($SkipBackendLaunch) { Split-Path $BackendExe -Parent } else { (Resolve-Path (Split-Path $BackendExe -Parent)).Path }
    Install-BrowserPolicy -FirefoxPath $BrowserExe
    New-Item -ItemType Directory -Path $ProfilePath -Force | Out-Null

    $preexistingProxy = Test-LoopbackProxyOwner -ExpectedRoot $BackendRoot
    if (-not $SkipBackendLaunch) {
        if ($preexistingProxy) {
            throw "A backend proxy is already running. Stop it first so this launcher can own its lifecycle."
        }
        $BackendProcess = Start-Process -FilePath $BackendExe -ArgumentList "--web-ui-port=44051" -PassThru
    }

    if ($BackendReadyTimeoutSeconds -gt 0) {
        Write-Host "Waiting for a connected backend. Complete provider setup at $BackendApi/."
        $deadline = (Get-Date).AddSeconds($BackendReadyTimeoutSeconds)
        do {
            if ($BackendProcess -and $BackendProcess.HasExited) {
                throw "Backend exited before becoming ready (exit code $($BackendProcess.ExitCode))."
            }
            if ((Get-BackendConnected) -and (Test-LoopbackProxyOwner -ExpectedRoot $BackendRoot)) { break }
            Start-Sleep -Seconds 1
        } while ((Get-Date) -lt $deadline)
        if (-not (Get-BackendConnected) -or -not (Test-LoopbackProxyOwner -ExpectedRoot $BackendRoot)) {
            throw "Backend did not establish an owned loopback proxy within $BackendReadyTimeoutSeconds seconds."
        }
    }

    $arguments = @("-no-remote", "-new-instance", "-profile", $ProfilePath)
    $arguments += $AdditionalBrowserArguments
    $arguments += $InitialUrl
    $BrowserProcess = Start-Process -FilePath $BrowserExe -ArgumentList $arguments -PassThru
    Write-Host "Privacy browser started as PID $($BrowserProcess.Id)."
    $BrowserProcess.WaitForExit()
} finally {
    Stop-OwnedBackend
}
