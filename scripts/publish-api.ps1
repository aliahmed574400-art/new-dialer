param(
    [string]$Configuration = "Release",
    [string]$OutputDir = "artifacts/publish/api"
)

$ErrorActionPreference = "Stop"

$workspaceRoot = Split-Path -Parent $PSScriptRoot
Set-Location $workspaceRoot

$env:DOTNET_CLI_HOME = Join-Path $workspaceRoot ".dotnet"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"

$publishPath = Join-Path $workspaceRoot $OutputDir

dotnet publish "src/NewDialer.Api/NewDialer.Api.csproj" `
    -c $Configuration `
    --output $publishPath

Write-Host "API publish complete: $publishPath"
