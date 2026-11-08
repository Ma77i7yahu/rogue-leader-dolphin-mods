# Star Wars Rogue Squadron II: Rogue Leader (Dolphin) – Mod Installer

Optional fixes and improvements for **Star Wars Rogue Squadron II: Rogue Leader** (GameCube, US version `GSWE64`) in the Dolphin emulator: a 16:9 widescreen that doesn't crash on Kothlis or the Death Star, more precise flying, a tight turn on its own button, laser convergence instead of aim assist, a single dark orange crosshair, and results-screen tweaks. Each option can be turned on or off on its own, and you can remove them all later.

**This is a patch only.** It adds patches to Dolphin's settings for this game. No game files are included, so you need your own copy of the game.

## Download

Get `RogueLeaderMods-v1.0.zip` from the [**Releases**](https://github.com/Ma77i7yahu/rogue-leader-dolphin-mods/releases/latest) page. It holds the installer, its README and `PATCHES.txt`. `SHA256SUMS.txt` on the release page lets you check your download.

Full instructions, controller setup and troubleshooting: [docs/README-installer.md](docs/README-installer.md).

## The options

| Option | Status |
|---|---|
| Safe 16:9 widescreen (fixes the Kothlis and Death Star crashes) | Tested at 1920x1080; Kothlis and the Death Star completed, other missions less tested |
| Precise flying: gentler lean, linear turn and pitch | Tested |
| Tight turn on its own button (C-stick Left), no speed loss | Tested |
| Remove the game's stick dead zone | Tested with a Logitech Extreme 3D |
| Laser convergence instead of aim assist | Tested |
| Single dark orange crosshair (no big ring or side lines) | Tested |
| Show the Gold requirements once you have Gold | Tested |
| Count wingman kills in "Enemies destroyed" | Tested |
| Keep the menu fade-outs | Tested |
| Remove dithering | Tested |
| Emulated CPU at 130% | Tested on an i5-8600K at 5 GHz |
| Controller profile for the Logitech Extreme 3D | Mapping tested; loading it as a per-game profile not yet played |

Tested with Dolphin 2609a and disc revision 0. Other regions won't work.

## Applying the patches by hand

You don't need the installer. Every patch in `PATCHES.txt` is a Dolphin patch: copy its lines into the `[OnFrame]` section of `User\GameSettings\GSWE64.ini` and its `$name` into `[OnFrame_Enabled]`, or add it under the game's **Properties > Patches**. A patch to the game's code only writes where the original code is found.

## What's in this repo

- `docs/README-installer.md`: the README that comes with the installer.
- `PATCHES.txt`: every change the installer makes.
- `src/`: the installer's C# source and the generator that builds its patch list. See [BUILDING.md](BUILDING.md).
- `tested/`: the Dolphin settings the patches were played with, which the generator reads.
- `test/`: the test harness used to check the installer's output.

## Licence

The code, patches and documentation here are under the [MIT licence](LICENSE): reuse or port them freely, with credit. The game itself isn't covered.

## Credits

Thanks to the Dolphin team for the emulator, its debugging tools, and its original 16:9 code, which showed where the game's aspect ratio is set.

## Disclaimer

Unofficial fan-made mods, not affiliated with or endorsed by Lucasfilm, LucasArts, Disney, Factor 5 or Nintendo. Star Wars is a trademark of its owners. No game files are distributed here. Use at your own risk.
