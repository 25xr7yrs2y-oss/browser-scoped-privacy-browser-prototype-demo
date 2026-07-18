$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$sourceRoot = Join-Path $root "src\PrivacyBrowser.App"
$backend = Get-Content (Join-Path $sourceRoot "BackendController.cs") -Raw
$browser = Get-Content (Join-Path $sourceRoot "BrowserLauncher.cs") -Raw
$window = Get-Content (Join-Path $sourceRoot "MainWindow.xaml") -Raw
$windowCode = Get-Content (Join-Path $sourceRoot "MainWindow.xaml.cs") -Raw
$stateStore = Get-Content (Join-Path $sourceRoot "UserStateStore.cs") -Raw
$bundle = Get-Content (Join-Path $sourceRoot "BundleValidator.cs") -Raw
$package = Get-Content (Join-Path $root "Package-Release.ps1") -Raw

if ($backend -match 'passphrase\s*=\s*""') {
    throw "Identity operations must never hard-code an empty passphrase."
}
foreach ($needle in @(
        'CreateIdentityAsync(string passphrase',
        'ImportIdentityAsync(',
        '"identities-import"',
        'current_passphrase = currentPassphrase',
        'new_passphrase = currentPassphrase',
        'UnlockIdentityAsync(',
        'RestartAsync(',
        'Browser launch is disabled for an adopted backend')) {
    if (-not $backend.Contains($needle)) { throw "Identity/lifecycle hardening invariant missing: $needle" }
}

foreach ($needle in @(
        'x:Name="IdentityComboBox"',
        'x:Name="ImportIdentityButton"',
        'x:Name="UnlockIdentityButton"',
        'x:Name="RestartBackendButton"',
        'x:Name="BrowserReadinessIcon"',
        'x:Name="ProviderSearchTextBox"')) {
    if (-not $window.Contains($needle)) { throw "Required product-hardening control missing: $needle" }
}

foreach ($needle in @(
        'PromptForExistingPassphrase',
        'TakePassphrase()',
        'GetSnapshotAsync(_selectedIdentityId)',
        'identity?.IsRegistered == true',
        '_browserReadiness.CanLaunch')) {
    if (-not $windowCode.Contains($needle)) { throw "Secure workflow/readiness invariant missing: $needle" }
}

if (-not $browser.Contains('EvaluateReadiness(BackendSnapshot snapshot)') -or
    -not $browser.Contains('PolicyHasRequiredPrivacySettings') -or
    -not $browser.Contains('IsOwnedProxyListening')) {
    throw "Browser launch must share the composite readiness and policy gate used by the UI."
}
if (-not $stateStore.Contains('private sealed record ControllerState(string? SelectedIdentityId);')) {
    throw "User state must persist only the public identity selection, never credentials."
}
if (-not $bundle.Contains('SHA256.HashDataAsync') -or
    -not $package.Contains('bundle-manifest.json') -or
    -not $package.Contains('GetRelativePath')) {
    throw "Portable bundle component integrity validation is missing."
}

Write-Host "PASS: protected identities, explicit selection, readiness, lifecycle recovery, and bundle integrity gates are present."
