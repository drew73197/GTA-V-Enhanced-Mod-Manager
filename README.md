# GTA V Mod Manager

A lightweight mod manager for **GTA V Enhanced** (Windows, .NET 8 WinForms). Enable or disable mods with checkboxes and launch the game — no file juggling.

## How it works

The manager scans your game folder and treats anything that isn't part of the vanilla install (plus anything matching known mod patterns like `.asi` files, `dinput8.dll`, `ScriptHookV.dll`, `scripts/`, `mods/`) as a mod. Disabling a mod moves it into a `Disabled mods` folder inside the game directory; enabling moves it back. The game never sees disabled files, so you can switch between a clean and modded install in seconds.

Loose `.rpf` archives in the game root are never touched, so a game update that adds a new archive can't be mistakenly moved out of the install.

## Features

- **Launch Clean / Selected / All** — pick your loadout and go
- **Profiles** — save named mod sets (e.g. "Graphics only", "Script mods") and load them anytime
- **Restore Last** — brings back the selection you had before a clean launch
- **ScriptHookV version check** — warns when your ScriptHookV.dll looks older than the game executable (the most common cause of crashes after an update)
- **Game-running guard** — refuses to move files while GTA V is running

## Usage

1. Run the app and point it at your GTA V Enhanced install folder (defaults to the Steam location).
2. Check the mods you want active.
3. Hit a launch button. Files are moved, then the game starts.

Settings and profiles are stored in `%AppData%\GTAVModManager\config.json`.

## Building

Requires the .NET 8 SDK on Windows:

```
dotnet build -c Release
```

## License

See [LICENSE.txt](LICENSE.txt).
