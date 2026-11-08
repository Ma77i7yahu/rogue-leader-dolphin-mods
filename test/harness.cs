// Drives the installer form without showing it, in scratch folders only.
// Usage: harness.exe <installer.exe> <scratch dir> <folder holding the tested GSWE64-modded.ini + GCPadNew-modded.ini>
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

class Harness {
    const BindingFlags Inst = BindingFlags.NonPublic | BindingFlags.Instance;
    const BindingFlags Stat = BindingFlags.NonPublic | BindingFlags.Static;
    static Type T; static object F;
    static object Get(string n) { return T.GetField(n, Inst).GetValue(F); }
    static object Call(string n, params object[] a) { return T.GetMethod(n, Inst).Invoke(F, a); }
    static void Opt(string key, bool v) { Call("Set", key, v); }
    static bool On(string key) { return (bool)Call("On", key); }
    static string Message() { return (string)T.GetField("LastMessage", Stat).GetValue(null); }
    static void Open(string dir) { ((TextBox)Get("folder")).Text = dir; Call("LoadFolder"); }
    static string Status() { return ((Label)Get("status")).Text; }
    static int fails, count;
    static void Check(string what, bool ok) { Console.WriteLine((ok ? "PASS " : "FAIL ") + what); count++; if (!ok) fails++; }

    // independent copy of which patches belong to which option (name prefixes)
    static readonly Dictionary<string, string[]> Map = new Dictionary<string, string[]> {
        { "widescreen", new[] { "$Safe 16:9 widescreen", "$Bigger VM page pool", "$Terrain cell record array", "$Skip terrain cells", "$Invalid terrain texture", "$Terrain bin index clamp", "$Surface row-span table" } },
        { "flight", new[] { "$Bank curve", "$Linear turn", "$Linear pitch" } },
        { "tight", new[] { "$Tight turn button", "$Tight turn - any stick", "$Tight turn - pitch", "$Tight turn on C-stick" } },
        { "deadzone", new[] { "$No game stick dead zone" } },
        { "convergence", new[] { "$Laser convergence" } },
        { "gunsight", new[] { "$Gunsight" } },
        { "gold", new[] { "$Results screen" } },
        { "kills", new[] { "$Enemies destroyed" } },
        { "efb", new string[0] }, { "dither", new string[0] }, { "overclock", new string[0] },
    };
    static readonly string[] AllKeys = Map.Keys.ToArray();

    // section -> non-blank lines, for comparing files without caring about their order
    static Dictionary<string, List<string>> Parse(string path) {
        var r = new Dictionary<string, List<string>>(); List<string> cur = null;
        foreach (var raw in File.ReadAllLines(path)) {
            var l = raw.Trim();
            if (l.StartsWith("[") && l.EndsWith("]")) { var n = l.Substring(1, l.Length - 2); if (!r.ContainsKey(n)) r[n] = new List<string>(); cur = r[n]; }
            else if (l.Length > 0 && cur != null) cur.Add(l);
        }
        return r;
    }
    static Dictionary<string, string[]> Blocks(Dictionary<string, List<string>> ini) {
        var r = new Dictionary<string, string[]>();
        if (!ini.ContainsKey("OnFrame")) return r;
        string n = null; var cur = new List<string>();
        foreach (var l in ini["OnFrame"]) {
            if (l.StartsWith("$")) { if (n != null) r[n] = cur.ToArray(); n = l; cur = new List<string>(); } else cur.Add(l);
        }
        if (n != null) r[n] = cur.ToArray();
        return r;
    }
    static HashSet<string> Enabled(Dictionary<string, List<string>> ini) {
        return new HashSet<string>(ini.ContainsKey("OnFrame_Enabled") ? ini["OnFrame_Enabled"] : new List<string>());
    }
    static bool Same(string[] a, string[] b) { return a.OrderBy(x => x).SequenceEqual(b.OrderBy(x => x)); }
    static bool SameFile(string a, string b) {
        var x = Parse(a); var y = Parse(b);
        return x.Count == y.Count && x.All(kv => y.ContainsKey(kv.Key) && Same(kv.Value.ToArray(), y[kv.Key].ToArray()));
    }
    static string Fresh(string root, string name) {
        string d = Path.Combine(root, name);
        if (Directory.Exists(d)) Directory.Delete(d, true);
        Directory.CreateDirectory(Path.Combine(d, "Config"));
        return d;
    }
    static void All(bool v) { foreach (var k in AllKeys) Opt(k, v); Opt("profile", v); }

    [STAThread]
    static int Main(string[] a) {
        string exe = a[0], root = a[1], src = a[2];
        var asm = Assembly.LoadFrom(exe);
        T = asm.GetType("InstallerForm");
        T.GetField("Quiet", Stat).SetValue(null, true);
        F = T.GetConstructor(Inst, null, Type.EmptyTypes, null).Invoke(null);

        var tested = Parse(Path.Combine(src, "GSWE64-modded.ini"));
        var testedBlocks = Blocks(tested);
        var testedEnabled = Enabled(tested);

        // --- 1. clean Dolphin, everything on, against the tested files
        string u = Fresh(root, "clean");
        Open(u);
        string ini = Path.Combine(u, "GameSettings", "GSWE64.ini"), bak = ini + ".before-rogue-leader-mods";
        string prof = Path.Combine(u, "Config", "Profiles", "GCPad", "Rogue Leader Mods - Extreme 3D.ini");
        Check("clean: every option ticked except the controller profile", AllKeys.All(On) && !On("profile"));
        Check("clean: Remove is disabled", !((Button)Get("restore")).Enabled);
        Opt("profile", true);
        Call("DoInstall");
        Check("clean: no error: " + Message().Split('\r')[0], Message().StartsWith("Installed:"));
        var got = Parse(ini);
        var gotBlocks = Blocks(got);
        Check("all on: enabled patches = tested enabled patches", Enabled(got).SetEquals(testedEnabled));
        Check("all on: patch lines identical to the tested ini", testedEnabled.All(n => gotBlocks.ContainsKey(n) && gotBlocks[n].SequenceEqual(testedBlocks[n])));
        Check("all on: nothing switched off in the tested ini is included", gotBlocks.Count == testedEnabled.Count);
        foreach (var sec in new[] { "Core", "Video_Hacks", "Gecko_Enabled" })
            Check("all on: [" + sec + "] identical", Same(got[sec].ToArray(), tested[sec].ToArray()));
        Check("all on: [Video_Settings] identical except the texture pack", Same(got["Video_Settings"].ToArray(), tested["Video_Settings"].Where(l => !l.StartsWith("HiresTextures")).ToArray()));
        Check("all on: per-game profile set", got["Controls"].SequenceEqual(new[] { "PadProfile1 = Rogue Leader Mods - Extreme 3D" }));
        var pad = File.ReadAllLines(Path.Combine(src, "GCPadNew-modded.ini"));
        pad[0] = "[Profile]";
        Check("profile file = tested mapping", File.ReadAllLines(prof).SequenceEqual(pad));
        Check("empty backup = no earlier file", new FileInfo(bak).Length == 0);
        Check("message mentions Roll", Message().Contains("Roll to ON"));
        Check("reload: everything ticked", AllKeys.All(On) && On("profile"));
        Check("reload: Remove enabled", ((Button)Get("restore")).Enabled);
        string first = File.ReadAllText(ini);
        Call("DoInstall");
        Check("installing again changes nothing", File.ReadAllText(ini) == first);

        // --- 2. each option alone contains its patches and no others
        foreach (var k in AllKeys) {
            All(false); Opt(k, true);
            Call("DoInstall");
            var g = Parse(ini); var en = Enabled(g); var bl = Blocks(g);
            var want = testedEnabled.Where(n => Map[k].Any(p => n.StartsWith(p))).ToArray();
            bool ok = Same(en.ToArray(), want) && bl.Count == want.Length && want.All(n => bl[n].SequenceEqual(testedBlocks[n]));
            bool ticks = AllKeys.All(x => On(x) == (x == k)) && !On("profile");
            Check(k + " alone: " + want.Length + " patches, reload ticks only it", ok && ticks);
        }
        // settings of each settings-only option alone
        All(false); Opt("overclock", true); Call("DoInstall");
        got = Parse(ini);
        Check("overclock alone: only its settings", got.ContainsKey("Core") && got["Core"].Count == 2 && !got.ContainsKey("Video_Settings") && !got.ContainsKey("OnFrame"));
        Check("profile file removed when unticked", !File.Exists(prof));
        All(false); Opt("widescreen", true); Call("DoInstall");
        got = Parse(ini);
        Check("widescreen alone: 48 MB + custom aspect", got["Core"].Contains("MEM1Size = 50331648") && got["Video_Settings"].Contains("AspectRatio = 4") && !got["Core"].Any(l => l.StartsWith("Overclock")));
        All(false); Call("DoInstall");
        Check("nothing ticked: no mods left", new FileInfo(ini).Length == 0 || Parse(ini).Values.All(v => v.Count == 0));
        Check("nothing ticked: message says so", Message().StartsWith("No options"));

        // --- 3. remove from clean: leaves no trace
        All(true); Call("DoInstall");
        Call("DoRestore");
        Check("remove: ini deleted (there wasn't one before)", !File.Exists(ini));
        Check("remove: backup deleted", !File.Exists(bak));
        Check("remove: profile deleted", !File.Exists(prof));
        Check("remove: defaults ticked again", AllKeys.All(On) && !On("profile"));

        // --- 4. someone's existing settings survive, conflicts go and come back
        u = Fresh(root, "existing");
        ini = Path.Combine(u, "GameSettings", "GSWE64.ini"); bak = ini + ".before-rogue-leader-mods";
        prof = Path.Combine(u, "Config", "Profiles", "GCPad", "Rogue Leader Mods - Extreme 3D.ini");
        Directory.CreateDirectory(Path.GetDirectoryName(ini));
        string[] mine = {
            "[Core]", "Overclock = 2", "OverclockEnable = True", "CPUThread = False",
            "[Video_Settings]", "AspectRatio = 1", "wideScreenHack = True", "InternalResolution = 4",
            "[Controls]", "PadProfile1 = My Pad",
            "[Gecko_Enabled]", "$16:9 Widescreen", "$Some Code",
            "[OnFrame]", "$My own patch", "0x80000000:dword:0x00000001",
            "[OnFrame_Enabled]", "$My own patch",
        };
        File.WriteAllLines(ini, mine);
        string before = Path.Combine(root, "existing-before.ini");
        File.WriteAllLines(before, mine);
        Open(u);
        All(true); Call("DoInstall");
        got = Parse(ini);
        Check("existing: backup is the old file", File.ReadAllText(bak) == File.ReadAllText(before));
        Check("existing: own patch kept + enabled", Enabled(got).Contains("$My own patch") && Blocks(got)["$My own patch"].SequenceEqual(new[] { "0x80000000:dword:0x00000001" }));
        Check("existing: unrelated settings kept", got["Core"].Contains("CPUThread = False") && got["Video_Settings"].Contains("InternalResolution = 4") && got["Gecko_Enabled"].Contains("$Some Code"));
        Check("existing: old 16:9 code + hack off", !got["Gecko_Enabled"].Contains("$16:9 Widescreen") && !got["Video_Settings"].Any(l => l.StartsWith("wideScreenHack")));
        Check("existing: our values win", got["Core"].Contains("Overclock = 1.3") && got["Video_Settings"].Contains("AspectRatio = 4") && got["Controls"].Contains("PadProfile1 = Rogue Leader Mods - Extreme 3D"));
        // untick widescreen + overclock + profile: their settings return to the player's own
        Opt("widescreen", false); Opt("overclock", false); Opt("profile", false); Call("DoInstall");
        got = Parse(ini);
        Check("existing: unticked = old aspect, hack + code back", got["Video_Settings"].Contains("AspectRatio = 1") && got["Video_Settings"].Contains("wideScreenHack = True") && got["Gecko_Enabled"].Contains("$16:9 Widescreen"));
        Check("existing: unticked = old overclock + pad", got["Core"].Contains("Overclock = 2") && got["Controls"].Contains("PadProfile1 = My Pad"));
        Check("existing: backup unchanged after a second install", File.ReadAllText(bak) == File.ReadAllText(before));
        Call("DoRestore");
        Check("existing: remove = same content as before", SameFile(ini, before));
        Check("existing: backup gone", !File.Exists(bak));
        // a setting the player changes after installing survives removal
        All(true); Call("DoInstall");
        File.AppendAllLines(ini, new[] { "[Audio]", "Later = Edit" });
        Call("DoRestore");
        got = Parse(ini);
        Check("existing: later edit survives remove", got.ContainsKey("Audio") && got["Audio"].Contains("Later = Edit"));
        Check("existing: no patches of ours left", Blocks(got).Keys.SequenceEqual(new[] { "$My own patch" }));

        // --- 5. a modified profile with our name isn't deleted
        u = Fresh(root, "ownprofile");
        Open(u);
        prof = Path.Combine(u, "Config", "Profiles", "GCPad", "Rogue Leader Mods - Extreme 3D.ini");
        All(true); Call("DoInstall");
        File.AppendAllLines(prof, new[] { "Buttons/Y = `Button 2`" });
        Call("DoRestore");
        Check("edited profile is kept on remove", File.Exists(prof));

        // --- 6. cheats warning
        u = Fresh(root, "cheats");
        Open(u);
        All(true); Call("DoInstall");
        Check("warns when cheats are off", Message().Contains("Enable Cheats"));
        File.WriteAllLines(Path.Combine(u, "Config", "Dolphin.ini"), new[] { "[Core]", "EnableCheats = True" });
        Call("DoInstall");
        Check("no warning when cheats are on", !Message().Contains("Enable Cheats"));

        // --- 7. folder detection
        string port = Path.Combine(root, "portable");
        if (Directory.Exists(port)) Directory.Delete(port, true);
        Directory.CreateDirectory(port);
        File.WriteAllText(Path.Combine(port, "Dolphin.exe"), "");
        Open(port);
        Check("non-portable Dolphin folder refused", Get("userDir") == null && !((Button)Get("install")).Enabled);
        File.WriteAllText(Path.Combine(port, "portable.txt"), "");
        Open(port);
        Check("portable folder -> User", (string)Get("userDir") == Path.Combine(port, "User") && ((Button)Get("install")).Enabled);
        Call("DoInstall");
        Check("portable with no User folder yet installs", File.Exists(Path.Combine(port, "User", "GameSettings", "GSWE64.ini")));
        Open(Path.Combine(root, "no-such-folder"));
        Check("missing folder refused", Get("userDir") == null);
        string plain = Path.Combine(root, "plain");
        Directory.CreateDirectory(plain);
        Open(plain);
        Check("unrelated folder refused", Get("userDir") == null);

        Console.WriteLine(fails == 0 ? "ALL " + count + " PASS" : fails + " of " + count + " FAILED");
        return fails == 0 ? 0 : 1;
    }
}
