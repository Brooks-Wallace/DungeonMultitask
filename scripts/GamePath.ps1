# Find the Steam install of Melvor Idle 2.
function Find-MelvorGame {
    param([string]$GamePath)

    if ($GamePath) {
        if (Test-Path (Join-Path $GamePath 'Melvor2.exe')) { return (Resolve-Path $GamePath).Path }
        throw "Melvor2.exe was not found in $GamePath"
    }

    $libraries = New-Object System.Collections.Generic.List[string]
    $steamKey = Get-ItemProperty -Path 'HKCU:\Software\Valve\Steam' -ErrorAction SilentlyContinue
    if ($steamKey -and $steamKey.SteamPath) {
        $steam = ($steamKey.SteamPath -replace '/', '\')
        $libraries.Add($steam)
        $vdf = Join-Path $steam 'steamapps\libraryfolders.vdf'
        if (Test-Path $vdf) {
            foreach ($match in [regex]::Matches((Get-Content $vdf -Raw), '"path"\s+"([^"]+)"')) {
                $libraries.Add(($match.Groups[1].Value -replace '\\\\', '\'))
            }
        }
    }

    foreach ($lib in $libraries) {
        $candidate = Join-Path $lib 'steamapps\common\Melvor Idle 2'
        if (Test-Path (Join-Path $candidate 'Melvor2.exe')) { return (Resolve-Path $candidate).Path }
    }

    throw "Melvor Idle 2 was not found in a Steam library. Re-run with -GamePath."
}
