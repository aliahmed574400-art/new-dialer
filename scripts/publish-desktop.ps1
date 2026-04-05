param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$OutputDir = "artifacts/publish/desktop-win-x64",
    [bool]$SelfContained = $true
)

$ErrorActionPreference = "Stop"

$workspaceRoot = Split-Path -Parent $PSScriptRoot
Set-Location $workspaceRoot

$env:DOTNET_CLI_HOME = Join-Path $workspaceRoot ".dotnet"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"

$publishPath = Join-Path $workspaceRoot $OutputDir

dotnet publish "src/NewDialer.Desktop/NewDialer.Desktop.csproj" `
    -c $Configuration `
    -r $Runtime `
    --self-contained:$SelfContained `
    --output $publishPath

Write-Host "Desktop publish complete: $publishPath"
