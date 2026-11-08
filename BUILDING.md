# Building

You don't need any of this to install the mods. It's for anyone who wants to rebuild the installer or port the patches.

## Installer only

`src/PatchData.cs` already holds every patch, so the installer builds with the C# compiler that ships with Windows (.NET Framework 4):

```
cd src
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe /optimize /out:RogueLeaderModInstaller.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll Installer.cs PatchData.cs
```

## Regenerating the patches

`src/build.cmd` runs `gen.py` first, which rebuilds `PatchData.cs` and `PATCHES.txt` from the tested settings in `tested/` (`GSWE64-modded.ini` and `GCPadNew-modded.ini`). Every patch enabled there has to belong to exactly one installer option, or be listed as left out, or the build stops. Pass the folder as the argument:

```
src\build.cmd ..\tested
```

`build.cmd` expects Python from MSYS2's MinGW-w64 at `G:\msys64`, so change that path to suit your machine. It writes the installer and `PATCHES.txt` to `dist\`.

## Tests

- `test/harness.cs` loads the installer by reflection and drives it in scratch folders only: every option alone, all of them, an existing settings file, a portable Dolphin, removal. Usage: `harness.exe <installer.exe> <scratch dir> <folder holding the tested ini files>`.
- `test/ramcheck.py` reads a running Dolphin's emulated memory and checks the words the patches' helper code writes. Usage: `ramcheck.py <Dolphin pid>`.
- `test/shot.cs` renders the installer window to a PNG.
