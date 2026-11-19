# Installs the mod loader when the game does not have one, builds Dungeon Multitask, and copies it into the game.
param(
    [string]$GamePath
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'GamePath.ps1')

$game = Find-MelvorGame $GamePath
Write-Host "Game folder: $game"

$conflict = Join-Path $game 'mods\Multitask'
if (Test-Path $conflict) {
    throw @"
The official Multitask mod is installed at:
  $conflict
The two mods both keep actions running, so they cannot be installed together.
Move that folder out of the game, then run this script again.
"@
}

$escaped = [System.Security.SecurityElement]::Escape($game)
@"
<Project>
  <PropertyGroup>
    <MelvorGameDir>$escaped</MelvorGameDir>
  </PropertyGroup>
</Project>
"@ | Set-Content -Path (Join-Path $root 'local.props') -Encoding utf8

$loaderDll = Join-Path $game 'data_MelvorModLoader_windows_x86_64\MelvorModLoader.dll'
$override = Join-Path $game 'override.cfg'
if (-not (Test-Path $loaderDll)) {
    Write-Host "Publishing Melvor Mod Loader 0.1.3 into the game folder..."
    dotnet publish (Join-Path $root 'src\Loader\MelvorModLoader.csproj') -c Release
    if ($LASTEXITCODE -ne 0) { throw "The loader failed to build." }
    $published = Join-Path $root 'src\Loader\bin\Release\net10.0\win-x64\publish'
    $dest = Join-Path $game 'data_MelvorModLoader_windows_x86_64'
    New-Item -ItemType Directory -Force -Path $dest | Out-Null
    Copy-Item -Path (Join-Path $published '*') -Destination $dest -Recurse -Force
    @"
[dotnet]
project/assembly_name="MelvorModLoader"
"@ | Set-Content -Path $override -Encoding ascii
    Write-Host "Loader installed. override.cfg is next to Melvor2.exe."
}
else {
    Write-Host "A mod loader is already installed. Leaving it in place."
    if (-not (Test-Path $override)) {
        @"
[dotnet]
project/assembly_name="MelvorModLoader"
"@ | Set-Content -Path $override -Encoding ascii
        Write-Host "Wrote the missing override.cfg."
    }
}

Write-Host "Building Dungeon Multitask..."
dotnet build (Join-Path $root 'src\DungeonMultitask\DungeonMultitask.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw "The mod failed to build." }

$built = Join-Path $root 'src\DungeonMultitask\bin\Release\net10.0\DungeonMultitask.dll'
if (-not (Test-Path $built)) { throw "Build finished but $built is missing." }

$modDir = Join-Path $game 'mods\DungeonMultitask'
New-Item -ItemType Directory -Force -Path $modDir | Out-Null
Copy-Item $built (Join-Path $modDir 'DungeonMultitask.dll') -Force
Write-Host "Installed: $modDir\DungeonMultitask.dll"
Write-Host "Start the game from Steam, open a character, and press F5."
