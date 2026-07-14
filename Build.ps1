[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SelfContained
)

$ErrorActionPreference = "Stop"
$project = Join-Path $PSScriptRoot "src\PrivacyBrowser.App\PrivacyBrowser.App.csproj"
$output = Join-Path $PSScriptRoot "app"
$selfContainedValue = $SelfContained.IsPresent.ToString().ToLowerInvariant()

dotnet publish $project `
    --configuration $Configuration `
    --runtime $Runtime `
    --self-contained $selfContainedValue `
    --output $output

if ($LASTEXITCODE -ne 0) {
    throw "Native application build failed with code $LASTEXITCODE."
}

Write-Host "Native application published to $output"
