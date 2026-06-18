$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$policy = Get-Content (Join-Path $root "config\policies.json") -Raw | ConvertFrom-Json
$p = $policy.policies

function Assert-Equal($Actual, $Expected, [string]$Message) {
    if ($Actual -ne $Expected) { throw "$Message (expected '$Expected', got '$Actual')" }
}

Assert-Equal $p.Proxy.Mode "manual" "Proxy mode must be manual"
Assert-Equal $p.Proxy.HTTPProxy "127.0.0.1:4449" "HTTP proxy must use loopback"
Assert-Equal $p.Proxy.SSLProxy "127.0.0.1:4449" "HTTPS proxy must use loopback"
Assert-Equal $p.Proxy.Locked $true "Proxy policy must be locked"
Assert-Equal $p.Preferences."network.trr.mode".Value 5 "Firefox DoH must be disabled"
Assert-Equal $p.Preferences."network.dns.disablePrefetch".Value $true "DNS prefetch must be disabled"
Assert-Equal $p.Preferences."media.peerconnection.enabled".Value $false "WebRTC must be disabled"
Assert-Equal $p.Preferences."media.peerconnection.ice.proxy_only_if_behind_proxy".Value $true "WebRTC defense must honor the browser proxy"
Assert-Equal $p.Preferences."privacy.resistFingerprinting".Value $true "RFP must be enabled"
Assert-Equal $p.Preferences."privacy.resistFingerprinting.letterboxing".Value $true "Letterboxing must be enabled"

foreach ($name in $p.Preferences.PSObject.Properties.Name) {
    Assert-Equal $p.Preferences.$name.Status "locked" "Preference $name must be locked"
}

Write-Host "PASS: policy and privacy invariants are locked."
