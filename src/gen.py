# Reads the tested mod set (GSWE64-modded.ini + GCPadNew-modded.ini) and writes PatchData.cs and
# ..\dist\PATCHES.txt. Every patch that is enabled in the tested ini must belong to exactly one
# option, and every patch that isn't must be listed in LEFT_OUT, or the build stops.
# Usage: gen.py ["folder holding GSWE64-modded.ini and GCPadNew-modded.ini"]
import os, sys

SRC = sys.argv[1] if len(sys.argv) > 1 else r"G:\Game Files\Dolphin\RL-flight-mod"
HERE = os.path.dirname(os.path.abspath(__file__))

# key, title, patch name prefixes, settings (section, key, value), Gecko codes to enable
OPTIONS = [
    ("widescreen", "Safe 16:9 widescreen (fixes the Kothlis and Death Star crashes)",
     ["$Safe 16:9 widescreen", "$Bigger VM page pool", "$Terrain cell record array", "$Skip terrain cells",
      "$Invalid terrain texture", "$Terrain bin index clamp", "$Surface row-span table"],
     [("Core", "RAMOverrideEnable", "True"), ("Core", "MEM1Size", "50331648"),
      ("Video_Settings", "AspectRatio", "4"), ("Video_Settings", "CustomAspectRatioWidth", "64"),
      ("Video_Settings", "CustomAspectRatioHeight", "35")], []),
    ("flight", "Precise flying: gentler lean, linear turn and pitch",
     ["$Bank curve", "$Linear turn", "$Linear pitch"], [], []),
    ("tight", "Tight turn on its own button (C-stick Left), no speed loss",
     ["$Tight turn button", "$Tight turn - any stick", "$Tight turn - pitch", "$Tight turn on C-stick"], [], []),
    ("deadzone", "Remove the game's stick dead zone",
     ["$No game stick dead zone"], [], []),
    ("convergence", "Laser convergence instead of aim assist",
     ["$Laser convergence"], [], []),
    ("gunsight", "Single dark orange crosshair (no big ring or side lines)",
     ["$Gunsight"], [], []),
    ("gold", "Show the Gold requirements once you have Gold",
     ["$Results screen - \"Needed for Gold\"", "$Results screen - show Gold"], [], []),
    ("kills", "Count wingman kills in \"Enemies destroyed\"",
     ["$Enemies destroyed"], [], []),
    ("efb", "Keep the menu fade-outs",
     [], [("Video_Hacks", "EFBToTextureEnable", "False")], []),
    ("dither", "Remove dithering",
     [], [], ["$Disable Dithering"]),
    ("overclock", "Emulated CPU at 130% (less in-game slowdown)",
     [], [("Core", "OverclockEnable", "True"), ("Core", "Overclock", "1.3")], []),
]
# in the tested ini but switched off there, so they aren't part of the release
LEFT_OUT = ["$Disable laser aim assist", "$Widescreen 100deg render pass", "$Terrain builder: skip off-map cells"]
# in the tested ini, deliberately not installed for other people (the texture pack isn't included)
LEFT_OUT_SETTINGS = [("Video_Settings", "HiresTextures")]


def read_ini(path):
    sections, cur = {}, None
    for line in open(path, encoding="utf-8").read().splitlines():
        if line.startswith("[") and line.rstrip().endswith("]"):
            cur = line.strip()[1:-1]
            sections.setdefault(cur, [])
        elif cur is not None and line.strip():
            sections[cur].append(line.rstrip())
    return sections


ini = read_ini(os.path.join(SRC, "GSWE64-modded.ini"))
blocks, order, name = {}, [], None
for line in ini["OnFrame"]:
    if line.startswith("$"):
        name = line
        blocks[name] = []
        order.append(name)
    else:
        blocks[name].append(line)
enabled = set(ini["OnFrame_Enabled"])

used = {}
for key, title, prefixes, settings, gecko in OPTIONS:
    for p in prefixes:
        hits = [n for n in order if n.startswith(p)]
        if len(hits) != 1:
            sys.exit("prefix %r matched %r" % (p, hits))
        if hits[0] in used:
            sys.exit("%s is in two options" % hits[0])
        if hits[0] not in enabled:
            sys.exit("%s is in option %s but not enabled in the tested ini" % (hits[0], key))
        used[hits[0]] = key
for n in order:
    if n in used:
        continue
    if not any(n.startswith(p) for p in LEFT_OUT):
        sys.exit("patch not assigned to any option: " + n)
    if n in enabled:
        sys.exit("left-out patch is enabled in the tested ini: " + n)
missing = enabled - set(order)
if missing:
    sys.exit("enabled but not defined: %r" % missing)

# every setting in the tested ini must be installed by an option or be deliberately left out
owned = {(s, k) for o in OPTIONS for s, k, v in o[3]}
for sec in ini:
    if sec in ("OnFrame", "OnFrame_Enabled", "Gecko_Enabled"):
        continue
    for line in ini[sec]:
        k, v = [x.strip() for x in line.split("=", 1)]
        if (sec, k) in owned:
            want = [o for o in OPTIONS for s, kk, vv in o[3] if (s, kk) == (sec, k) and vv == v]
            if not want:
                sys.exit("setting %s.%s = %s differs from the option's value" % (sec, k, v))
        elif (sec, k) not in LEFT_OUT_SETTINGS:
            sys.exit("setting not assigned to any option: %s.%s" % (sec, k))
gecko_owned = {g for o in OPTIONS for g in o[4]}
if gecko_owned != set(ini["Gecko_Enabled"]):
    sys.exit("Gecko list differs from the tested ini: %r" % ini["Gecko_Enabled"])

# controller profile: the tested mapping with the profile section name
pad = open(os.path.join(SRC, "GCPadNew-modded.ini"), encoding="utf-8").read().splitlines()
if pad[0].strip() != "[GCPad1]":
    sys.exit("unexpected first line in GCPadNew-modded.ini")
pad[0] = "[Profile]"


def cs(s):
    return '"' + s.replace("\\", "\\\\").replace('"', '\\"') + '"'


def arr(items):
    return "new string[] {" + ", ".join(cs(i) for i in items) + "}"


out = ["// Generated by gen.py from the tested mod set. Don't edit.",
       "static class PatchData {",
       "    public static readonly OptionData[] Options = {"]
for key, title, prefixes, settings, gecko in OPTIONS:
    names = [n for n in order if used.get(n) == key]
    out.append("        new OptionData(%s, %s, new PatchBlock[] {" % (cs(key), cs(title)))
    for n in names:
        out.append("            new PatchBlock(%s, %s)," % (cs(n), arr(blocks[n])))
    out.append("        }, new Setting[] {")
    for s, k, v in settings:
        out.append("            new Setting(%s, %s, %s)," % (cs(s), cs(k), cs(v)))
    out.append("        }, %s)," % arr(gecko))
out.append("    };")
out.append("    public static readonly string[] Profile = %s;" % arr(pad))
out.append("}")
open(os.path.join(HERE, "PatchData.cs"), "w", encoding="utf-8", newline="\r\n").write("\n".join(out) + "\n")

txt = ["Star Wars Rogue Squadron II: Rogue Leader (GSWE64, US disc, revision 0) - every change the installer makes",
       "",
       "These go into Dolphin's per-game settings file, User\\GameSettings\\GSWE64.ini.",
       "Patch lines are address:dword:new[:original]. A line with an original value only writes when",
       "that value is found at the address. A line without one writes to memory the game doesn't use.",
       ""]
for key, title, prefixes, settings, gecko in OPTIONS:
    txt += ["=" * 90, title, "=" * 90]
    for s, k, v in settings:
        txt.append("[%s] %s = %s" % (s, k, v))
    for g in gecko:
        txt.append("[Gecko_Enabled] %s (a code that comes with Dolphin)" % g)
    for n in order:
        if used.get(n) == key:
            txt.append(n)
            txt += ["  " + l for l in blocks[n]]
    txt.append("")
txt += ["=" * 90, "Logitech Extreme 3D controller profile (User\\Config\\Profiles\\GCPad)", "=" * 90] + pad + [""]
os.makedirs(os.path.join(HERE, "..", "dist"), exist_ok=True)
open(os.path.join(HERE, "..", "dist", "PATCHES.txt"), "w", encoding="utf-8", newline="\r\n").write("\n".join(txt))
print("options: %d, patches: %d, left out: %d" % (len(OPTIONS), len(used), len(order) - len(used)))
