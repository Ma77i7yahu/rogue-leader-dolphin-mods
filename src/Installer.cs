// Star Wars Rogue Squadron II: Rogue Leader (Dolphin) mod installer.
// Merges the optional patches in PatchData.cs into Dolphin's per-game settings file, GSWE64.ini.
// Nothing outside the user's Dolphin settings folder is touched, and no game files are needed.
// The file as it was first found is kept as "GSWE64.ini.before-rogue-leader-mods". Settings the
// installer owns go back to their values in that file when an option is removed; anything else
// in GSWE64.ini (including edits made after installing) is left alone.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Win32;

class PatchBlock {
    public readonly string Name; public readonly string[] Lines;
    public PatchBlock(string name, string[] lines) { Name = name; Lines = lines; }
}
class Setting {
    public readonly string Section, Key, Value;
    public Setting(string section, string key, string value) { Section = section; Key = key; Value = value; }
}
class OptionData {
    public readonly string Key, Title; public readonly PatchBlock[] Patches; public readonly Setting[] Settings; public readonly string[] Gecko;
    public OptionData(string key, string title, PatchBlock[] patches, Setting[] settings, string[] gecko) { Key = key; Title = title; Patches = patches; Settings = settings; Gecko = gecko; }
}

class IniSection {
    public string Name; public List<string> Lines = new List<string>();
}

// line-based ini that keeps everything it doesn't change exactly as it was
class Ini {
    public readonly List<IniSection> Sections = new List<IniSection>();

    public static Ini Load(string path) {
        var ini = new Ini();
        if (!File.Exists(path)) return ini;
        IniSection cur = null;
        foreach (var line in File.ReadAllLines(path)) {
            var t = line.Trim();
            if (t.StartsWith("[") && t.EndsWith("]")) {
                cur = new IniSection { Name = t.Substring(1, t.Length - 2) };
                ini.Sections.Add(cur);
            } else {
                if (cur == null) { cur = new IniSection { Name = "" }; ini.Sections.Add(cur); }
                cur.Lines.Add(line);
            }
        }
        return ini;
    }
    public void Save(string path) {
        var all = new List<string>();
        foreach (var s in Sections) {
            if (s.Name.Length > 0) all.Add("[" + s.Name + "]");
            all.AddRange(s.Lines);
        }
        File.WriteAllLines(path + ".tmp", all.ToArray());
        File.Copy(path + ".tmp", path, true);
        File.Delete(path + ".tmp");
    }
    public IniSection Find(string name) { return Sections.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase)); }
    public IniSection Get(string name) {
        var s = Find(name);
        if (s == null) { s = new IniSection { Name = name }; Sections.Add(s); }
        return s;
    }
    static int KeyIndex(IniSection s, string key) {
        return s.Lines.FindIndex(l => {
            int e = l.IndexOf('=');
            return e > 0 && l.Substring(0, e).Trim().Equals(key, StringComparison.OrdinalIgnoreCase);
        });
    }
    public string GetKey(string section, string key) {
        var s = Find(section);
        if (s == null) return null;
        int i = KeyIndex(s, key);
        return i < 0 ? null : s.Lines[i].Substring(s.Lines[i].IndexOf('=') + 1).Trim();
    }
    public void SetKey(string section, string key, string value) {
        var s = Get(section);
        int i = KeyIndex(s, key);
        string line = key + " = " + value;
        if (i >= 0) s.Lines[i] = line; else InsertLine(s, line);
    }
    public void RemoveKey(string section, string key) {
        var s = Find(section);
        if (s == null) return;
        int i = KeyIndex(s, key);
        if (i >= 0) s.Lines.RemoveAt(i);
    }
    public bool HasLine(string section, string line) {
        var s = Find(section);
        return s != null && s.Lines.Any(l => l.Trim() == line);
    }
    public void AddLine(string section, string line) {
        if (!HasLine(section, line)) InsertLine(Get(section), line);
    }
    public void RemoveLine(string section, string line) {
        var s = Find(section);
        if (s != null) s.Lines.RemoveAll(l => l.Trim() == line);
    }
    // adds before any blank lines that end the section
    static void InsertLine(IniSection s, string line) {
        int i = s.Lines.Count;
        while (i > 0 && s.Lines[i - 1].Trim().Length == 0) i--;
        s.Lines.Insert(i, line);
    }
    // a "$name" patch and the lines under it, up to the next "$name"
    public string[] GetPatch(string name) {
        var s = Find("OnFrame");
        if (s == null) return null;
        int a = s.Lines.FindIndex(l => l.Trim() == name);
        if (a < 0) return null;
        var r = new List<string>();
        for (int i = a + 1; i < s.Lines.Count && !s.Lines[i].TrimStart().StartsWith("$"); i++)
            if (s.Lines[i].Trim().Length > 0) r.Add(s.Lines[i].Trim());
        return r.ToArray();
    }
    public void RemovePatch(string name) {
        var s = Find("OnFrame");
        if (s == null) return;
        int a = s.Lines.FindIndex(l => l.Trim() == name);
        if (a < 0) return;
        int b = a + 1;
        while (b < s.Lines.Count && !s.Lines[b].TrimStart().StartsWith("$")) b++;
        // keep a blank line that separates this patch from whatever comes next
        while (b > a + 1 && s.Lines[b - 1].Trim().Length == 0) b--;
        s.Lines.RemoveRange(a, b - a);
    }
    public void AddPatch(PatchBlock p) {
        var s = Get("OnFrame");
        int i = s.Lines.Count;
        while (i > 0 && s.Lines[i - 1].Trim().Length == 0) i--;
        s.Lines.InsertRange(i, new[] { p.Name }.Concat(p.Lines));
    }
}

class Option {
    public string Key, Title, Info;
    public CheckBox Box;
}

class InstallerForm : Form {
    const string GameIni = "GSWE64.ini";
    const string BackupSuffix = ".before-rogue-leader-mods";
    const string ProfileName = "Rogue Leader Mods - Extreme 3D";
    const string ProfileKey = "profile";

    // conflicts with the widescreen option: removed while it is on, put back when it comes off
    const string OldWidescreenGecko = "$16:9 Widescreen";
    static readonly Setting[] WidescreenConflicts = { new Setting("Video_Settings", "wideScreenHack", "") };
    static readonly Setting ProfileSetting = new Setting("Controls", "PadProfile1", ProfileName);

    readonly TextBox folder = new TextBox();
    readonly Label status = new Label();
    readonly TextBox info = new TextBox();
    readonly List<Option> options = new List<Option>();
    readonly Button install = new Button(), restore = new Button();
    string userDir;

    // the test harness turns message boxes off and reads what would have been shown
    static bool Quiet = false;
    static string LastMessage = "";

    string IniPath { get { return Path.Combine(userDir, "GameSettings", GameIni); } }
    string BackupPath { get { return IniPath + BackupSuffix; } }
    string ProfilePath { get { return Path.Combine(userDir, "Config", "Profiles", "GCPad", ProfileName + ".ini"); } }

    Option Opt(string key) { return options.First(o => o.Key == key); }
    bool On(string key) { return Opt(key).Box.Checked; }
    void Set(string key, bool v) { Opt(key).Box.Checked = v; }
    static OptionData Data(string key) { return PatchData.Options.First(o => o.Key == key); }

    InstallerForm() {
        Text = "Star Wars Rogue Squadron II: Rogue Leader (Dolphin) - Mod Installer v1.0";
        Font = new Font("Segoe UI", 9f);
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(10);

        var root = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false };
        Controls.Add(root);

        var fl = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, Margin = new Padding(0) };
        fl.Controls.Add(new Label { Text = "Dolphin folder:", AutoSize = true, Margin = new Padding(0, 7, 4, 0) });
        folder.Width = 460;
        folder.Margin = new Padding(0, 3, 4, 0);
        folder.Leave += (s, e) => LoadFolder();
        fl.Controls.Add(folder);
        var browse = new Button { Text = "Browse...", AutoSize = true };
        browse.Click += (s, e) => {
            using (var d = new FolderBrowserDialog { Description = "Select your Dolphin user folder (usually Documents\\Dolphin Emulator), or the folder holding a portable Dolphin" }) {
                if (Directory.Exists(folder.Text)) d.SelectedPath = folder.Text;
                if (d.ShowDialog(this) == DialogResult.OK) { folder.Text = d.SelectedPath; LoadFolder(); }
            }
        };
        fl.Controls.Add(browse);
        root.Controls.Add(fl);
        status.AutoSize = true; status.MaximumSize = new Size(640, 0); status.Margin = new Padding(0, 4, 0, 6);
        root.Controls.Add(status);

        var g1 = Group(root, "Widescreen");
        Check(g1, "widescreen",
            "The game was built for 4:3, and Dolphin's built-in 16:9 code makes it crash on Kothlis because several of the " +
            "game's fixed-size buffers overflow with the wider view. This replaces that code with patches that fix each " +
            "limit. The HUD, radar and text keep their proportions.\r\n\r\n" +
            "Sets Dolphin to emulate 48 MB of main memory for this game, so old save states won't load (memory card saves " +
            "are fine). Turns off Dolphin's \"16:9 Widescreen\" code and widescreen hack if you have them on.\r\n\r\n" +
            "For 16:9 screens. These patches depend on each other, so they're one option.\r\n\r\n" +
            "Known limits: cutscenes are letterboxed inside the 16:9 picture, water reflections may look slightly squashed, " +
            "and a \"GFX FIFO: Unknown Opcode\" warning has appeared at pause and results screens in earlier builds.\r\n\r\n" +
            "Status: tested at 1920x1080. Kothlis and the Death Star were completed; other missions have had less testing.");

        var g2 = Group(root, "Flight");
        Check(g2, "flight",
            "Small stick movements lean the ship much less (25% stick leans about 3 degrees instead of 13), and turning and " +
            "pitching become a straight line instead of jumping sharply past 80% stick. Full-stick turning drops to about " +
            "half (36 degrees a second), so use the tight turn for hard corners. Towing an AT-AT keeps the stock turn rate.\r\n\r\n" +
            "Tip: also set Options > Game Settings > Roll to ON in the game.\r\n\r\nStatus: tested.");
        Check(g2, "tight",
            "In the stock game the L click slows you to minimum speed and only tightens the turn at full stick. Now C-stick " +
            "Left tightens both turning and pitching at any stick position, on every craft, with no speed loss (full stick " +
            "goes from 36 to 79 degrees a second with the Precise flying option). A full analog L press is the instant hard " +
            "brake.\r\n\r\nYou need a button mapped to C-stick Left in Dolphin's controller settings. This takes C-stick Left " +
            "away from anything else.\r\n\r\nStatus: tested.");
        Check(g2, "deadzone",
            "The game ignores about the first 15% of stick travel, on top of Dolphin's own dead zone. With this on, small " +
            "movements respond straight away and mid-stick turns are a little quicker. Full stick is unchanged.\r\n\r\n" +
            "Status: tested with a Logitech Extreme 3D at Dolphin's 5% dead zone. A worn stick may drift, so raise Dolphin's " +
            "dead zone if it does.");
        Check(g2, "convergence",
            "The stock game bends your shots toward a target near the reticle. Now, when the game finds a target, both guns " +
            "aim at the point straight ahead of the nose at that target's distance. Close-range hits land and nothing pulls " +
            "your shots sideways. This also applies to AI ships.\r\n\r\nStatus: tested.");
        Check(g2, "gunsight",
            "The stock gunsight is a big faded ring near the ship, a small X sight further out and two vertical lines, all " +
            "in see-through green. This draws only the small X sight, in solid dark orange, so it's easier to see what " +
            "you're aiming at. The sight stays orange when it's over a target.\r\n\r\nStatus: tested.");

        var g3 = Group(root, "Results screen");
        Check(g3, "gold",
            "The game hides the requirements column once a mission's best medal is Gold. This keeps it visible under a " +
            "\"Needed for Gold\" heading, so you can see what you beat or missed.\r\n\r\nStatus: tested.");
        Check(g3, "kills",
            "The game only counts enemies you shot yourself. With this on, kills by anyone on your side count too (crashes " +
            "and debris still don't). This makes the \"Enemies destroyed\" medal target easier to reach.\r\n\r\nStatus: tested.");

        var g4 = Group(root, "Picture and speed");
        Check(g4, "efb",
            "Dolphin's default texture setting loses some of the game's menu fade-outs. This turns off \"Store EFB Copies to " +
            "Texture Only\" for this game. It costs a little speed.\r\n\r\nStatus: tested.");
        Check(g4, "dither",
            "Turns on the \"Disable Dithering\" code that comes with Dolphin, which removes the dot pattern in the picture.\r\n\r\n" +
            "This is a cheat code, so \"Enable Cheats\" must be on in Dolphin (Config > General). The installer tells you if " +
            "it's off.\r\n\r\nStatus: tested.");
        Check(g4, "overclock",
            "Some of the game's slowdown comes from the emulated CPU, so this runs it at 130%. It needs a fast PC: if the " +
            "game runs below full speed, untick this.\r\n\r\nStatus: tested on an i5-8600K at 5 GHz.");

        var g5 = Group(root, "Controls");
        CheckTitled(g5, ProfileKey, "Controller profile for the Logitech Extreme 3D",
            "Adds a Dolphin controller profile and uses it for this game only. Your other games' controls don't change.\r\n\r\n" +
            "The throttle slider is the brake and speed-up, button 9 is the tight turn, button 11 is the hard brake and " +
            "button 12 is boost. Leave this unticked for any other controller, and see the README for what to map.\r\n\r\n" +
            "Status: the mapping is tested. Loading it as a per-game profile hasn't been played with this game.");

        info.Multiline = true; info.ReadOnly = true; info.ScrollBars = ScrollBars.Vertical;
        info.Size = new Size(640, 150); info.BackColor = SystemColors.Info;
        info.Text = "Point at an option to see what it does.";
        root.Controls.Add(info);

        var bl = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, Margin = new Padding(0, 8, 0, 0) };
        install.Text = "Install selected"; install.AutoSize = true;
        install.Click += (s, e) => DoInstall();
        restore.Text = "Remove all mods"; restore.AutoSize = true;
        restore.Click += (s, e) => DoRestore();
        var close = new Button { Text = "Close", AutoSize = true };
        close.Click += (s, e) => Close();
        bl.Controls.AddRange(new Control[] { install, restore, close });
        root.Controls.Add(bl);

        folder.Text = DefaultFolder();
        LoadFolder();
    }

    GroupBox Group(Control parent, string title) {
        var g = new GroupBox { Text = title, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Width = 640, MinimumSize = new Size(640, 0), Padding = new Padding(8, 4, 8, 6) };
        var p = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, Location = new Point(8, 20) };
        g.Controls.Add(p);
        parent.Controls.Add(g);
        return g;
    }
    void Check(GroupBox g, string key, string text) { CheckTitled(g, key, Data(key).Title, text); }
    void CheckTitled(GroupBox g, string key, string title, string text) {
        var c = new CheckBox { Text = title, AutoSize = true };
        g.Controls[0].Controls.Add(c);
        var o = new Option { Key = key, Title = title, Info = text, Box = c };
        c.MouseEnter += (s, e) => info.Text = o.Title + "\r\n\r\n" + o.Info;
        c.GotFocus += (s, e) => info.Text = o.Title + "\r\n\r\n" + o.Info;
        options.Add(o);
    }

    // ---- finding Dolphin's user folder ----

    static bool IsUserDir(string d) { return Directory.Exists(Path.Combine(d, "GameSettings")) || Directory.Exists(Path.Combine(d, "Config")); }

    // a user folder, a portable Dolphin folder or the folder above "User"; null if it's none of them
    static string FindUserDir(string dir) {
        if (dir.Length == 0 || !Directory.Exists(dir)) return null;
        if (File.Exists(Path.Combine(dir, "portable.txt"))) return Path.Combine(dir, "User");
        if (File.Exists(Path.Combine(dir, "Dolphin.exe"))) return null;   // an ordinary install: its settings are elsewhere
        if (IsUserDir(dir)) return dir;
        string u = Path.Combine(dir, "User");
        return IsUserDir(u) ? u : null;
    }

    // the same order Dolphin itself uses, starting with a portable Dolphin the installer sits beside
    static string DefaultFolder() {
        string here = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
        if (File.Exists(Path.Combine(here, "portable.txt"))) return here;
        try {
            using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Dolphin Emulator")) {
                if (k != null) {
                    var p = k.GetValue("UserConfigPath") as string;
                    if (!string.IsNullOrEmpty(p) && IsUserDir(p)) return p;
                }
            }
        } catch { }
        string docs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Dolphin Emulator");
        if (IsUserDir(docs)) return docs;
        string appdata = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Dolphin Emulator");
        return IsUserDir(appdata) ? appdata : "";
    }

    void LoadFolder() {
        userDir = null;
        install.Enabled = restore.Enabled = false;
        foreach (var o in options) o.Box.Enabled = false;
        string dir = folder.Text.Trim().Trim('"');
        userDir = FindUserDir(dir);
        if (userDir == null) {
            status.ForeColor = Color.DarkRed;
            status.Text = dir.Length == 0
                ? "Couldn't find Dolphin's settings. Click Browse and choose your Dolphin user folder (usually Documents\\Dolphin Emulator), or the folder holding a portable Dolphin."
                : File.Exists(Path.Combine(dir, "Dolphin.exe"))
                    ? "This Dolphin isn't portable, so its settings aren't in this folder. Choose your Dolphin user folder instead (usually Documents\\Dolphin Emulator)."
                    : "That isn't a Dolphin user folder. It should contain a \"Config\" or \"GameSettings\" folder.";
            return;
        }
        foreach (var o in options) o.Box.Enabled = true;
        install.Enabled = true;

        Ini cur;
        try { cur = Ini.Load(IniPath); }
        catch (Exception ex) { status.ForeColor = Color.DarkRed; status.Text = "Can't read " + IniPath + ": " + ex.Message; install.Enabled = false; return; }
        bool installed = File.Exists(BackupPath);
        restore.Enabled = installed;
        status.ForeColor = Color.DarkGreen;
        status.Text = "Using Dolphin settings in: " + userDir + (installed
            ? "\r\nThe mods are installed. The ticked options show what's installed now."
            : "\r\nIf you use a different or portable Dolphin, click Browse and choose it instead.");
        foreach (var d in PatchData.Options) Set(d.Key, installed ? IsIn(cur, d) : true);
        Set(ProfileKey, installed && ProfileIn(cur));
    }

    static bool IsIn(Ini ini, OptionData d) {
        foreach (var p in d.Patches) {
            var lines = ini.GetPatch(p.Name);
            if (lines == null || !lines.SequenceEqual(p.Lines) || !ini.HasLine("OnFrame_Enabled", p.Name)) return false;
        }
        foreach (var s in d.Settings) if (ini.GetKey(s.Section, s.Key) != s.Value) return false;
        foreach (var g in d.Gecko) if (!ini.HasLine("Gecko_Enabled", g)) return false;
        return true;
    }
    bool ProfileIn(Ini ini) {
        return ini.GetKey(ProfileSetting.Section, ProfileSetting.Key) == ProfileSetting.Value && File.Exists(ProfilePath);
    }

    // ---- merging ----

    static void PutBack(Ini cur, Ini orig, Setting s) {
        string v = orig.GetKey(s.Section, s.Key);
        if (v != null) cur.SetKey(s.Section, s.Key, v); else cur.RemoveKey(s.Section, s.Key);
    }
    static void PutBackGecko(Ini cur, Ini orig, string g) {
        if (orig.HasLine("Gecko_Enabled", g)) cur.AddLine("Gecko_Enabled", g); else cur.RemoveLine("Gecko_Enabled", g);
    }

    // takes everything of ours out; owned settings go back to what the original file had
    static void Strip(Ini cur, Ini orig) {
        foreach (var d in PatchData.Options) {
            foreach (var p in d.Patches) {
                cur.RemovePatch(p.Name);
                cur.RemoveLine("OnFrame_Enabled", p.Name);
                cur.RemoveLine("OnFrame_Disabled", p.Name);
            }
            foreach (var s in d.Settings) PutBack(cur, orig, s);
            foreach (var g in d.Gecko) PutBackGecko(cur, orig, g);
        }
        foreach (var s in WidescreenConflicts) PutBack(cur, orig, s);
        PutBack(cur, orig, ProfileSetting);
        PutBackGecko(cur, orig, OldWidescreenGecko);
        // sections that only we were using
        cur.Sections.RemoveAll(s => s.Lines.All(l => l.Trim().Length == 0) && orig.Find(s.Name) == null);
    }

    Ini Merge(Ini orig, List<string> applied) {
        var cur = Ini.Load(IniPath);
        Strip(cur, orig);
        foreach (var d in PatchData.Options) {
            if (!On(d.Key)) continue;
            foreach (var p in d.Patches) { cur.AddPatch(p); cur.AddLine("OnFrame_Enabled", p.Name); }
            foreach (var s in d.Settings) cur.SetKey(s.Section, s.Key, s.Value);
            foreach (var g in d.Gecko) cur.AddLine("Gecko_Enabled", g);
            applied.Add(d.Title);
        }
        if (On("widescreen")) {
            foreach (var s in WidescreenConflicts) cur.RemoveKey(s.Section, s.Key);
            cur.RemoveLine("Gecko_Enabled", OldWidescreenGecko);
        }
        if (On(ProfileKey)) {
            cur.SetKey(ProfileSetting.Section, ProfileSetting.Key, ProfileSetting.Value);
            applied.Add(Opt(ProfileKey).Title);
        }
        return cur;
    }

    bool ProfileIsOurs() {
        return File.Exists(ProfilePath) && File.ReadAllLines(ProfilePath).SequenceEqual(PatchData.Profile);
    }

    bool CheatsOn() {
        string v = Ini.Load(Path.Combine(userDir, "Config", "Dolphin.ini")).GetKey("Core", "EnableCheats");
        return v != null && v.Equals("True", StringComparison.OrdinalIgnoreCase);
    }

    void Say(string msg, MessageBoxIcon icon) {
        LastMessage = msg;
        if (!Quiet) MessageBox.Show(this, msg, Text, MessageBoxButtons.OK, icon);
    }

    // returns false after explaining the problem
    bool Guard(Action work) {
        try { work(); return true; }
        catch (IOException ex) {
            Say("Couldn't write Dolphin's settings. Close Dolphin if it's running and try again.\r\n\r\n" + ex.Message, MessageBoxIcon.Error);
        } catch (UnauthorizedAccessException ex) {
            Say("Windows denied access to Dolphin's settings folder. Right-click the installer and choose \"Run as administrator\".\r\n\r\n" + ex.Message, MessageBoxIcon.Error);
        }
        return false;
    }

    void DoInstall() {
        if (userDir == null) return;
        var applied = new List<string>();
        bool ok = Guard(() => {
            Directory.CreateDirectory(Path.GetDirectoryName(IniPath));
            // an empty backup means there was no GSWE64.ini
            if (!File.Exists(BackupPath)) {
                if (File.Exists(IniPath)) File.Copy(IniPath, BackupPath); else File.WriteAllText(BackupPath, "");
            }
            var merged = Merge(Ini.Load(BackupPath), applied);
            merged.Save(IniPath);
            if (On(ProfileKey)) {
                Directory.CreateDirectory(Path.GetDirectoryName(ProfilePath));
                File.WriteAllLines(ProfilePath, PatchData.Profile);
            } else if (ProfileIsOurs()) File.Delete(ProfilePath);
        });
        if (!ok) return;
        string msg = applied.Count == 0 ? "No options were selected, so none of the mods are installed."
                                        : "Installed:\r\n  - " + string.Join("\r\n  - ", applied);
        msg += "\r\n\r\nYour previous settings for this game are saved as \"" + GameIni + BackupSuffix + "\" in the GameSettings folder.";
        if (On("flight")) msg += "\r\n\r\nIn the game, also set Options > Game Settings > Roll to ON.";
        if (On("tight") && !On(ProfileKey)) msg += "\r\n\r\nMap a button to C-stick Left in Dolphin's controller settings to use the tight turn.";
        if (On("dither") && !CheatsOn()) msg += "\r\n\r\n\"Enable Cheats\" is off in Dolphin (Config > General). Turn it on, or dithering won't be removed. The other options don't need it.";
        msg += RunningNote();
        Say(msg, MessageBoxIcon.Information);
        LoadFolder();
    }

    void DoRestore() {
        if (userDir == null || !File.Exists(BackupPath)) return;
        if (!Quiet && MessageBox.Show(this, "Remove every Rogue Leader mod and put your previous settings for this game back?", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        bool ok = Guard(() => {
            var orig = Ini.Load(BackupPath);
            bool hadFile = new FileInfo(BackupPath).Length > 0;
            var cur = Ini.Load(IniPath);
            Strip(cur, orig);
            bool empty = cur.Sections.All(s => s.Lines.All(l => l.Trim().Length == 0));
            if (empty && !hadFile) { if (File.Exists(IniPath)) File.Delete(IniPath); }
            else cur.Save(IniPath);
            if (ProfileIsOurs()) File.Delete(ProfilePath);
            File.Delete(BackupPath);
        });
        if (!ok) return;
        Say("Every mod has been removed and your previous settings for this game are back." + RunningNote(), MessageBoxIcon.Information);
        LoadFolder();
    }

    static string RunningNote() {
        try {
            if (Process.GetProcessesByName("Dolphin").Length > 0)
                return "\r\n\r\nDolphin is running. If the game is open, stop it and start it again for this to take effect.";
        } catch { }
        return "";
    }

    [STAThread]
    static void Main() {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new InstallerForm());
    }
}
