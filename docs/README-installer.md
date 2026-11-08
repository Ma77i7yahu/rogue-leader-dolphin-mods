# Star Wars Rogue Squadron II: Rogue Leader (Dolphin) – Mod Installer v1.0

A small installer that adds optional fixes and improvements to Rogue Leader in the Dolphin emulator.
It only changes this game's settings in Dolphin and contains none of the game's files, so you need your
own copy of the game. You can change or remove any option later.

It has only been played on one PC so far, so reports from other setups are welcome.

## What it can install

| Option | What it does | Status |
|---|---|---|
| **Safe 16:9 widescreen** | Dolphin's built-in 16:9 code crashes this game on Kothlis because the wider view overflows several of the game's fixed-size buffers. This replaces it with patches that fix each limit. The HUD, radar and text keep their proportions. It emulates 48 MB of main memory for this game. | Tested at 1920x1080. Kothlis and the Death Star were completed. Other missions have had less testing. |
| **Precise flying** | Small stick movements lean the ship much less, and turning and pitching become a straight line instead of jumping sharply past 80% stick. Full-stick turning drops to about half (36 degrees a second), so use the tight turn for hard corners. Towing an AT-AT keeps the stock turn rate. | Tested |
| **Tight turn on its own button** | C-stick Left tightens turning and pitching at any stick position, on every craft, with no speed loss. A full analog L press is the instant hard brake. You need a button mapped to C-stick Left. | Tested |
| **Remove the game's stick dead zone** | The game ignores about the first 15% of stick travel. With this on, small movements respond straight away. Full stick is unchanged. | Tested with a Logitech Extreme 3D |
| **Laser convergence** | The stock game bends your shots toward a target near the reticle. Now both guns aim straight ahead of the nose at the target's distance, so close-range hits land and nothing pulls your shots sideways. This also applies to AI ships. | Tested |
| **Single dark orange crosshair** | The stock gunsight is a big faded ring, a small X sight and two vertical lines in see-through green. This draws only the small X sight, in solid dark orange. It stays orange over targets. | Tested |
| **Show the Gold requirements once you have Gold** | Keeps the results screen's requirements column visible under a "Needed for Gold" heading. | Tested |
| **Count wingman kills in "Enemies destroyed"** | Kills by anyone on your side count, which makes that medal target easier to reach. Crashes and debris still don't count. | Tested |
| **Keep the menu fade-outs** | Turns off Dolphin's "Store EFB Copies to Texture Only" for this game. It costs a little speed. | Tested |
| **Remove dithering** | Turns on the "Disable Dithering" code that comes with Dolphin. "Enable Cheats" must be on in Dolphin. | Tested |
| **Emulated CPU at 130%** | Reduces the game's own slowdown, but it needs a fast PC. If the game runs below full speed, untick it. | Tested on an i5-8600K at 5 GHz |
| **Controller profile for the Logitech Extreme 3D** | Adds a controller profile used for this game only. | The mapping is tested. Loading it as a per-game profile hasn't been played with this game. |

Each option has been played as part of the full set. Other combinations install correctly but haven't all been
played. The widescreen patches depend on each other, so they're one option.

## Requirements

- **Rogue Leader, US version, game ID `GSWE64`.** Tested with disc revision 0. Other regions won't work. To see
  your version, right-click the game in Dolphin and open **Properties > Info**.
- **Dolphin.** Tested with release 2609a. Older versions are untested, and this game needs a recent one to run well.
- **A 16:9 screen** for the widescreen option.
- Windows with .NET Framework 4 (included in Windows 8, 10 and 11).

RetroAchievements hardcore mode blocks patches, so leave it off.

## How to install

1. Stop the game in Dolphin. It's safest to close Dolphin completely.
2. Run `RogueLeaderModInstaller.exe`. It looks for your Dolphin settings and shows the folder it found.
   **If you use a portable Dolphin, click Browse and choose its folder**, or the mods will go into a different copy of Dolphin's settings.
3. Tick the options you want. Point at an option to see what it does.
4. Click **Install selected**.
5. In the game, set **Options > Game Settings > Roll** to ON. It makes a big difference to the flying.

Your settings for this game are saved first as `GameSettings\GSWE64.ini.before-rogue-leader-mods`. To change options,
run the installer again: it shows what's installed now. **Remove all mods** takes them out and puts back any
settings the installer changed. Other settings for this game, including ones you change later, are left alone.

Old save states won't load with the widescreen option, because it changes the emulated memory size. Memory card saves are fine.

## Controls with other controllers

Set these up in Dolphin under **Controllers > Configure** for your GameCube controller:

| Action | Map to |
|---|---|
| Tight turn | **C-Stick Left** (the Tight turn option takes this direction over) |
| Hard brake | **L-Analog** at full press |
| Boost | **R** button |
| Throttle on a slider | **L-Analog** for slowing down and **R-Analog** for speeding up. Near the middle, the game holds your current speed. |

Don't map the digital **L** button if you can avoid it. Dolphin also presses L-Analog fully with it, so it acts as a
hard brake and a tight turn at once.

The Extreme 3D profile uses: slider = speed, button 9 = tight turn, button 11 and button 3 = hard brake, button 12 = boost,
button 4 = X. It's only been used with a Logitech Extreme 3D. Dolphin has to see that stick as its first joystick.

## Known issues

- Cutscenes are letterboxed inside the 16:9 picture.
- Water reflections may look slightly squashed, because the patch that widens them is left out (see below).
- A "GFX FIFO: Unknown Opcode" warning has appeared at pause and results screens in earlier builds. Leaving out the
  reflection patch stopped it in the one Death Star run tested since. If you see it, please report it and which mission it was on.

## Troubleshooting

- **The options didn't change anything in the game.** The mods probably went into a different copy of Dolphin's settings.
  Check the folder at the top of the installer. If you use a portable Dolphin, choose its folder with Browse.
- **Crashes on Kothlis or a freeze with a constant tone.** Make sure Dolphin's "16:9 Widescreen" code is off in the game's
  **Properties > Gecko Codes**, and that the patches are all on under **Properties > Patches**. The installer sets both.
- **The game runs below full speed.** Untick **Emulated CPU at 130%**. This game is heavy to emulate.
- **The picture is stretched or has bars.** The widescreen option sets Dolphin's aspect ratio to Custom 64:35. Don't change it
  to Stretch or Force 16:9 for this game.
- **Still dithered.** Turn on **Enable Cheats** in Dolphin under **Config > General**.

## HD textures

The installer doesn't include or turn on a texture pack. If you have one for `GSWE64`, it works alongside these mods as usual.

## For the curious

`PATCHES.txt` lists every change the installer makes. You'll also see each patch under the game's **Properties > Patches** in
Dolphin. A patch to the game's code only applies where the original code is found. The helper code the patches add sits in
memory the game doesn't use.

## Credits

Patches made by reverse engineering the game in Dolphin. Thanks to the Dolphin team for the emulator, its debugging tools,
and its original 16:9 code, which showed where the game's aspect ratio is set.
