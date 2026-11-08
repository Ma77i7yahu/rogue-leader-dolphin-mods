@echo off
rem Rebuilds PatchData.cs + ..\dist\PATCHES.txt from the tested mod set, then the installer.
rem Usage: build.cmd ["folder holding GSWE64-modded.ini and GCPadNew-modded.ini"]
cd /d "%~dp0"
G:\msys64\mingw64\bin\python.exe -I gen.py %1 || exit /b 1
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe /optimize /out:..\dist\RogueLeaderModInstaller.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll Installer.cs PatchData.cs || exit /b 1
