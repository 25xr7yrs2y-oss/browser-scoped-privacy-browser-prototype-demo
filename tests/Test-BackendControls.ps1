$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$sourceRoot = Join-Path $root "src\PrivacyBrowser.App"
$backend = Get-Content (Join-Path $sourceRoot "BackendController.cs") -Raw
$models = Get-Content (Join-Path $sourceRoot "Models.cs") -Raw
$errors = Get-Content (Join-Path $sourceRoot "BackendErrors.cs") -Raw
$window = Get-Content (Join-Path $sourceRoot "MainWindow.xaml") -Raw
$windowCode = Get-Content (Join-Path $sourceRoot "MainWindow.xaml.cs") -Raw
$topUp = Get-Content (Join-Path $sourceRoot "TopUpWindow.xaml") -Raw

$backendContracts = @(
    'identities/{Uri.EscapeDataString(identityId)}/balance/refresh',
    'v2/payment-order-gateways?options_currency=MYST',
    'v2/identities/{Uri.EscapeDataString(identityId)}/{Uri.EscapeDataString(gateway.Name)}/payment-order',
    'proposals?service_type=wireguard&access_policy=all',
    'kill_switch = true',
    'include_monitoring_failed = true'
)
foreach ($needle in $backendContracts) {
    if (-not $backend.Contains($needle)) { throw "Backend control contract missing: $needle" }
}

if (-not $backend.Contains('CaptureAsync("connection"') -or
    -not $backend.Contains('CaptureAsync("identity"') -or
    -not $backend.Contains('CaptureAsync("terms"')) {
    throw "Snapshot resources must be isolated so one failed upstream dependency does not take the UI offline"
}

foreach ($needle in @('balance_tokens', 'registration_status', 'public_gateway_data', 'per_gib_tokens')) {
    if (-not $models.Contains($needle)) { throw "Mysterium response model missing: $needle" }
}

foreach ($needle in @('err_id_not_registered', 'err_payment', 'status=[Unregistered]', 'timeout')) {
    if (-not $errors.Contains($needle)) { throw "User-facing backend error translation missing: $needle" }
}

foreach ($needle in @('x:Name="WalletBalanceText"', 'x:Name="TopUpButton"', 'x:Name="ProviderCountText"',
        'x:Name="OperationStatusBorder"', 'x:Name="ConnectionPrerequisiteText"')) {
    if (-not $window.Contains($needle)) { throw "Required native controls UI element missing: $needle" }
}

if (-not $topUp.Contains('x:Class="PrivacyBrowser.App.TopUpWindow"') -or
    -not $topUp.Contains('Content="Create payment order"')) {
    throw "Native top-up workflow is missing"
}

if ($topUp.Contains('WebView') -or $window.Contains('WebView') -or $windowCode.Contains('http://127.0.0.1:44051')) {
    throw "Wallet and backend controls must remain native and must not restore the removed web UI"
}

if (-not $windowCode.Contains('registrationReady') -or
    -not $windowCode.Contains('Register the identity before connecting.')) {
    throw "Connect action does not expose or enforce the registration prerequisite"
}

Write-Host "PASS: resilient backend state, wallet/payment, provider discovery, native controls, and friendly errors are present."
