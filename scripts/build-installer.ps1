param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$workspaceRoot = Split-Path -Parent $PSScriptRoot
Set-Location $workspaceRoot

& "$PSScriptRoot\publish-desktop.ps1" -Configuration $Configuration -Runtime $Runtime -OutputDir "artifacts/publish/desktop-win-x64" -SelfContained $true

$innoCandidates = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
)

$isccPath = $innoCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $isccPath) {
    Write-Warning "Inno Setup 6 was not found. Publish output is ready in artifacts/publish/desktop-win-x64, but the .exe installer was not built."
    exit 0
}

& $isccPath "$workspaceRoot\installer\NewDialerDesktop.iss"

Write-Host "Installer build complete. Output is in artifacts/installer."
