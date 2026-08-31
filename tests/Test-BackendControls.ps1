$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$sourceRoot = Join-Path $root "src\PrivacyBrowser.App"
$backend = Get-Content (Join-Path $sourceRoot "BackendController.cs") -Raw
$models = Get-Content (Join-Path $sourceRoot "Models.cs") -Raw
$errors = Get-Content (Join-Path $sourceRoot "BackendErrors.cs") -Raw
$window = Get-Content (Join-Path $sourceRoot "MainWindow.xaml") -Raw
$topUpCode = Get-Content (Join-Path $sourceRoot "TopUpWindow.xaml.cs") -Raw
$paymentParser = Get-Content (Join-Path $sourceRoot "PaymentTargetParser.cs") -Raw
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

foreach ($needle in @(
        'Timeout = Timeout.InfiniteTimeSpan',
        'TimeSpan.FromSeconds(2)',
        'TimeSpan.FromSeconds(15)',
        'TimeSpan.FromSeconds(30)',
        'TimeSpan.FromSeconds(75)',
        'TimeSpan.FromSeconds(8)',
        'BackendOperation.ProviderDiscovery',
        'BackendOperation.ProviderConnect',
        'GetConnectionStateAsync(cancellationToken)',
        'IsConnectOutcomeIndeterminate',
        'ConnectionPath => $"connection?id={ProxyPort}"',
        '"connection?id={proxy_port}", ConnectionPath')) {
    if (-not $backend.Contains($needle)) { throw "Explicit backend deadline/reconciliation invariant missing: $needle" }
}
if ($backend.Contains('HttpMethod.Delete, "connection", "connection"') -or
    $backend.Contains('BackendOperation.ConnectionStatus, "connection", "connection"')) {
    throw "App-owned proxy status/disconnect must never default to connection ID 0"
}
if ($backend.Contains('Timeout = TimeSpan.FromSeconds(15)') -or $backend.Contains('CancelAfter(TimeSpan.FromSeconds(75))')) {
    throw "A shared or competing timeout can still preempt an operation-specific deadline"
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

foreach ($needle in @(
        'if (!PaymentTargetParser.SupportsGateway(gateway.Name))',
        'PaymentTargetParser.SupportsGateway(g.Name)',
        'GetPaymentUri(gateway.Name)')) {
    if (-not ($backend + $topUpCode).Contains($needle)) {
        throw "Gateway-bound payment target control missing: $needle"
    }
}
foreach ($forbidden in @('FindUri(', 'GetRawText()')) {
    if (($models + $topUpCode).Contains($forbidden)) {
        throw "Unsafe payment response handling remains: $forbidden"
    }
}
if (-not $paymentParser.Contains('CoinGatePaymentUrlField = "paymentUrl"') -or
    -not $paymentParser.Contains('Uri.UriSchemeHttps') -or
    -not $paymentParser.Contains('uri.UserInfo') -or
    -not $paymentParser.Contains('uri.IsDefaultPort') -or
    -not $paymentParser.Contains('HasExplicitEmptyPort(value)')) {
    throw "Payment target parser security invariant missing"
}
if (-not $topUpCode.Contains('_paymentUri = null;') -or
    -not $topUpCode.Contains('CreatedOrder = null;') -or
    $topUpCode.IndexOf('_paymentUri = null;', [StringComparison]::Ordinal) -gt
        $topUpCode.IndexOf('_backend.CreatePaymentOrderAsync(', [StringComparison]::Ordinal)) {
    throw "A new payment-order attempt does not clear the previously validated target"
}

dotnet run --project (Join-Path $root "tests\PrivacyBrowser.PaymentTargetParser.Tests\PrivacyBrowser.PaymentTargetParser.Tests.csproj") --configuration Release
if ($LASTEXITCODE -ne 0) { throw "Payment target parser runtime tests failed with code $LASTEXITCODE" }

dotnet run --project (Join-Path $root "tests\PrivacyBrowser.OperationFeedback.Tests\PrivacyBrowser.OperationFeedback.Tests.csproj") --configuration Release
if ($LASTEXITCODE -ne 0) { throw "Operation feedback runtime tests failed with code $LASTEXITCODE" }

Write-Host "PASS: resilient backend state, wallet/payment, provider discovery, native controls, and friendly errors are present."
