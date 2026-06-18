$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$evidence = Join-Path $root "evidence\windows-20260618"

function Assert-Equal($Actual, $Expected, [string]$Message) {
    if ($Actual -ne $Expected) {
        throw "$Message (expected '$Expected', got '$Actual')"
    }
}

$expectedHashes = @{
    "other-apps\capture.pcapng" = "0c0fd1a52fc6d646531110dacae4acf139fc677102744d542b90646402476f8f"
    "fail-closed\capture.pcapng" = "531c685e27f1004e0ee6e627c7f51cc38babfc388380684434e0ec5d787cc55c"
    "provider-payload\capture.pcapng" = "62d2936b6ba67bac9d0d9015e7fa0c289ccbe2c8b863ccf8e94dac687fcd1037"
    "backend-crash\capture.pcapng" = "0fd9789f647afbc128745d85dca87de61a43d2422ab1601619f036e3fa4980c6"
}

foreach ($relativePath in $expectedHashes.Keys) {
    $path = Join-Path $evidence $relativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Evidence file missing: $relativePath"
    }
    $actual = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    Assert-Equal $actual $expectedHashes[$relativePath] "Evidence hash changed: $relativePath"
}

$provider = Get-Content (Join-Path $evidence "provider-payload\result.json") -Raw | ConvertFrom-Json
Assert-Equal $provider.listenerAddress "127.0.0.1" "Provider listener must be loopback"
Assert-Equal $provider.browserTcpRecords 433 "Provider browser TCP record count changed"
Assert-Equal $provider.browserProxyRecords 173 "Provider proxy record count changed"
Assert-Equal $provider.browserDirectRecords 0 "Provider capture contains direct browser records"
Assert-Equal $provider.browserDnsRecords 0 "Provider capture contains browser DNS records"
Assert-Equal $provider.browserUdpRecords 0 "Provider capture contains browser UDP records"

$crash = Get-Content (Join-Path $evidence "backend-crash\result.json") -Raw | ConvertFrom-Json
Assert-Equal $crash.listenerPresentAfterKill $false "Proxy listener remained after backend termination"
Assert-Equal $crash.browserProxyRecords 78 "Crash proxy-attempt record count changed"
Assert-Equal $crash.browserDirectRecords 0 "Crash capture contains direct browser records"

$absent = Get-Content (Join-Path $evidence "fail-closed\result.json") -Raw | ConvertFrom-Json
Assert-Equal $absent.directSocketCount 0 "Backend-absent capture contains direct browser sockets"

$external = Get-Content (Join-Path $evidence "other-apps\result.json") -Raw | ConvertFrom-Json
Assert-Equal $external.proxy4449Records 0 "External applications used the browser proxy"
Assert-Equal $external.directRecords 40 "External direct record count changed"

Write-Host "PASS: evidence files, hashes, and recorded routing invariants are intact."
