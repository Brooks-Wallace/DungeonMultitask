# Dungeon Multitask

A Melvor Idle 2 mod that keeps more than one action running. How many you can run is earned by clearing dungeons.

This is a cheat. The game is balanced around one action at a time.

The action-keeping code, the mod loader, and the skill-start helpers are adapted from [Melvor Mod Loader](https://github.com/falorfrozen-cmd/MelvorModLoader) commit `c69867e0` (Multitask 0.2.2 and loader 0.1.3) by falorfrozen, which was published under the MIT license. See `LICENSE`.

## Install

You need the .NET 10 SDK. The game is found from Steam. From this folder:

```powershell
.\install.ps1
```

That publishes the mod loader into the game the first time (it leaves an existing loader alone), builds this mod, and copies it to `mods\DungeonMultitask`. If `mods\Multitask` is already there, the script stops. The two mods both keep actions running and cannot be installed together.

Start the game from Steam, open a character, and press **F5**.

If the game will not start, delete `override.cfg` next to `Melvor2.exe`. The game then runs without mods.

To point at a game folder Steam did not find:

```powershell
.\install.ps1 -GamePath "D:\Games\Melvor Idle 2"
```

## How many actions

You start at one action, the same as the normal game.

- Each standard dungeon you have cleared at least once adds one action. Twelve dungeons means 13.
- Six of those dungeons at 10 or more clears adds one more action. All twelve at 10 or more adds one more after that.
- The cap never goes past the number of skills the mod can actually run. A skill can only do one thing.
- Quest dungeons (Stonehopper Cave and Abyssal Vault) do not count.
- F5 can turn the bonus off, or use fewer actions than you have earned. That choice is saved per character.

Clear a dungeon while the game is open and the cap updates. A save that already has clears gets them the moment the character loads.

Fish, then start cooking: both run, once you have earned a second action. Start a third thing and the oldest drops out when there is no room. Press the game's own Stop to drop one. A fight is dropped if your character dies. An action that runs out of materials waits and continues when there are some again.

Action Queue still reads the same capacity number this mod publishes. Its own reader only uses up to 8 of those actions.

## Uninstall

```powershell
.\uninstall.ps1
```

That deletes `mods\DungeonMultitask` only. The loader stays for any other mod. Delete `override.cfg` and `data_MelvorModLoader_windows_x86_64` to remove the loader as well.
