param(
    [string]$GamePath
)
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'scripts\install.ps1') -GamePath $GamePath
if ($null -ne $LASTEXITCODE -and $LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
