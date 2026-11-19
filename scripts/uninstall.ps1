# Removes Dungeon Multitask from the game. The mod loader stays, so other mods keep working.
param(
    [string]$GamePath
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'GamePath.ps1')

$game = Find-MelvorGame $GamePath
$modDir = Join-Path $game 'mods\DungeonMultitask'
if (Test-Path $modDir) {
    Remove-Item $modDir -Recurse -Force
    Write-Host "Removed $modDir"
}
else {
    Write-Host "Dungeon Multitask is not installed in $game"
}
Write-Host "The mod loader was left in place. Delete override.cfg and data_MelvorModLoader_windows_x86_64 to remove that too."
