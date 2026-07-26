using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GTAVModManager
{
    public class ConfigData
    {
        public string GamePath { get; set; } = @"C:\Program Files (x86)\Steam\steamapps\common\Grand Theft Auto V Enhanced";
        public Dictionary<string, List<string>> Profiles { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> LastSelection { get; set; } = new();
    }

    public class Form1 : Form
    {
        // ---------- Theme ----------

        private static readonly Color ColBack = Color.FromArgb(17, 17, 20);
        private static readonly Color ColPanel = Color.FromArgb(23, 23, 27);
        private static readonly Color ColRow = Color.FromArgb(32, 32, 37);
        private static readonly Color ColRowOn = Color.FromArgb(36, 42, 40);
        private static readonly Color ColRowHover = Color.FromArgb(46, 46, 53);
        private static readonly Color ColField = Color.FromArgb(38, 38, 44);
        private static readonly Color ColButton = Color.FromArgb(52, 52, 60);
        private static readonly Color ColTextDim = Color.FromArgb(148, 148, 156);
        private static readonly Color ColTextFaint = Color.FromArgb(105, 105, 112);
        private static readonly Color Accent = Color.FromArgb(16, 185, 129);   // emerald
        private static readonly Color ColRed = Color.FromArgb(196, 57, 57);
        private static readonly Color ColBlue = Color.FromArgb(52, 116, 219);
        private static readonly Color ColGreen = Color.FromArgb(38, 152, 92);

        private static GraphicsPath Rounded(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            int d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        // ---------- State ----------

        private static readonly string ConfigDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GTAVModManager");
        private static readonly string ConfigFile = Path.Combine(ConfigDir, "config.json");
        private const string LegacyConfigFile = "mod_manager_config.json"; // old location, next to exe
        private const string DisabledModsDir = "Disabled mods";

        private ConfigData _config = new ConfigData();

        private TextBox pathTextBox = null!;
        private ModListBox modListBox = null!;
        private Label statusLabel = null!;
        private Label shvLabel = null!;
        private ComboBox profileCombo = null!;
        private readonly List<Control> _actionControls = new();

        private readonly HashSet<string> VanillaWhitelist = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Folders
            "BattlEye", "D3D12-REDIST", "Redistributables", "update", "x64", ".egstore",

            // Exes and important files
            "GTA5_Enhanced.exe", "GTA5_Enhanced_BE.exe", "PlayGTAV.exe", "GTAVLauncher.exe",
            "common.rpf", "installscript.vdf", "installscript_sdk.vdf", "title.rgl", "rpf.cache",

            // DLLs and Libraries
            "amd_ags_x64.dll", "amd_fidelityfx_dx12.dll", "bink2w64.dll",
            "dstorage.dll", "dstoragecore.dll", "fvad.dll",
            "GFSDK_Aftermath_Lib.x64.dll", "libcurl.dll", "libtox.dll",
            "nvngx_dlss.dll", "nvngx_dlssg.dll", "oo2core_5_win64.dll",
            "opus.dll", "opusenc.dll",
            "sl.common.dll", "sl.dlss.dll", "sl.dlss_g.dll", "sl.interposer.dll", "sl.pcl.dll", "sl.reflex.dll",
            "steam_api64.dll", "XCurl.dll", "zlib1.dll",

            // x64 Archives
            "x64a.rpf", "x64b.rpf", "x64c.rpf", "x64d.rpf", "x64e.rpf", "x64f.rpf", "x64g.rpf",
            "x64h.rpf", "x64i.rpf", "x64j.rpf", "x64k.rpf", "x64l.rpf", "x64m.rpf", "x64n.rpf",
            "x64o.rpf", "x64p.rpf", "x64q.rpf", "x64r.rpf", "x64s.rpf", "x64t.rpf", "x64u.rpf",
            "x64v.rpf", "x64w.rpf"
        };

        // Always treated as mods, even if a future game update ships a file with the same name.
        private static readonly HashSet<string> KnownModFiles = new(StringComparer.OrdinalIgnoreCase)
        {
            "dinput8.dll", "dsound.dll", "dxgi.dll", "d3d11.dll",
            "ScriptHookV.dll", "ScriptHookVDotNet.asi", "ScriptHookVDotNet2.dll", "ScriptHookVDotNet3.dll",
            "ReShade.ini", "ReShadePreset.ini", "OpenIV.asi"
        };

        private static readonly HashSet<string> KnownModDirs = new(StringComparer.OrdinalIgnoreCase)
        {
            "scripts", "mods", "reshade-shaders", "lml", "asi"
        };

        private static readonly HashSet<string> ModExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".asi", ".oiv"
        };

        private const string DefaultProfileName = "Default";

        public Form1()
        {
            SetupUI();
            LoadConfig();
            RefreshModList();
            EnsureDefaultProfile();
        }

        // Paint the whole window into one off-screen buffer (WS_EX_COMPOSITED).
        // Eliminates the flicker/flash from panels, buttons, and the combo box.
        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x02000000; // WS_EX_COMPOSITED
                return cp;
            }
        }

        // ---------- UI ----------

        private void SetupUI()
        {
            this.Text = "GTA V Enhanced Mod Manager";

            // Never open larger than the available screen space (accounts for
            // DPI scaling, small laptop screens, taskbar, etc.).
            var workArea = Screen.FromPoint(Cursor.Position).WorkingArea;
            this.Size = new Size(
                Math.Min(880, workArea.Width - 40),
                Math.Min(700, workArea.Height - 40));
            this.MinimumSize = new Size(600, 460);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = ColBack;
            this.ForeColor = Color.White;
            this.Font = new Font("Segoe UI", 9F);
            this.DoubleBuffered = true;

            // --- Header ---
            var headerPanel = new SmoothPanel { Dock = DockStyle.Top, Height = 58, BackColor = ColPanel };
            headerPanel.Paint += (s, e) =>
            {
                using var pen = new Pen(Accent, 2);
                e.Graphics.DrawLine(pen, 0, headerPanel.Height - 1, headerPanel.Width, headerPanel.Height - 1);
            };

            var titleLabel = new Label
            {
                Text = "GTA V  MOD MANAGER",
                Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(22, 15),
                BackColor = Color.Transparent
            };
            var editionLabel = new Label
            {
                Text = "ENHANCED EDITION",
                Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
                ForeColor = Accent,
                AutoSize = true,
                Location = new Point(240, 24),
                BackColor = Color.Transparent
            };

            var refreshBtn = new RoundedButton
            {
                Text = "\u21bb  Refresh",
                Size = new Size(96, 30),
                BackColor = ColButton,
                ForeColor = Color.White,
                Radius = 8,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            refreshBtn.Location = new Point(headerPanel.Width, 14); // repositioned on layout
            headerPanel.Layout += (s, e) => refreshBtn.Left = headerPanel.Width - refreshBtn.Width - 22;
            refreshBtn.Click += (s, e) => RefreshModList();
            _actionControls.Add(refreshBtn);

            headerPanel.Controls.Add(titleLabel);
            headerPanel.Controls.Add(editionLabel);
            headerPanel.Controls.Add(refreshBtn);

            // --- Game folder row ---
            var pathPanel = new SmoothPanel { Dock = DockStyle.Top, Height = 70, BackColor = ColBack };

            var lblPath = new Label
            {
                Text = "GAME FOLDER",
                Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
                ForeColor = ColTextFaint,
                AutoSize = true,
                Location = new Point(24, 12)
            };
            pathTextBox = new TextBox
            {
                Location = new Point(24, 32),
                BackColor = ColField,
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9.5F),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            pathTextBox.Leave += (s, e) =>
            {
                if (!pathTextBox.Text.Equals(_config.GamePath, StringComparison.OrdinalIgnoreCase))
                {
                    SaveConfig();
                    RefreshModList();
                }
            };

            var browseBtn = new RoundedButton
            {
                Text = "Browse\u2026",
                Size = new Size(90, 27),
                BackColor = ColButton,
                ForeColor = Color.White,
                Radius = 8,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            browseBtn.Click += (s, e) => BrowseFolder();
            _actionControls.Add(browseBtn);

            pathPanel.Layout += (s, e) =>
            {
                browseBtn.Location = new Point(pathPanel.Width - browseBtn.Width - 24, 31);
                pathTextBox.Width = browseBtn.Left - pathTextBox.Left - 10;
            };

            pathPanel.Controls.Add(lblPath);
            pathPanel.Controls.Add(pathTextBox);
            pathPanel.Controls.Add(browseBtn);

            // --- Profile row ---
            var profilePanel = new SmoothPanel { Dock = DockStyle.Top, Height = 46, BackColor = ColBack };

            var lblProfile = new Label
            {
                Text = "PROFILE",
                Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
                ForeColor = ColTextFaint,
                AutoSize = true,
                Location = new Point(24, 13)
            };
            profileCombo = new ComboBox
            {
                Location = new Point(88, 8),
                Width = 190,
                BackColor = ColField,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F),
                DropDownStyle = ComboBoxStyle.DropDown
            };

            RoundedButton CreateSmallBtn(string text, EventHandler action, int x, int width = 72)
            {
                var btn = new RoundedButton
                {
                    Text = text,
                    Location = new Point(x, 7),
                    Size = new Size(width, 28),
                    BackColor = ColButton,
                    ForeColor = Color.White,
                    Radius = 8
                };
                btn.Click += action;
                _actionControls.Add(btn);
                return btn;
            }

            var restoreBtn = CreateSmallBtn("Restore Last", (s, e) => RestoreLastSelection(), 0, 110);
            restoreBtn.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            profilePanel.Layout += (s, e) => restoreBtn.Left = profilePanel.Width - restoreBtn.Width - 24;

            profilePanel.Controls.Add(lblProfile);
            profilePanel.Controls.Add(profileCombo);
            profilePanel.Controls.Add(CreateSmallBtn("Load", (s, e) => LoadProfile(), 290));
            profilePanel.Controls.Add(CreateSmallBtn("Save", (s, e) => SaveProfile(), 370));
            profilePanel.Controls.Add(CreateSmallBtn("Delete", (s, e) => DeleteProfile(), 450));
            profilePanel.Controls.Add(restoreBtn);

            // --- Mod list ---
            modListBox = new ModListBox
            {
                Dock = DockStyle.Fill,
                BackColor = ColPanel,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F)
            };

            var listContainer = new SmoothPanel { Dock = DockStyle.Fill, Padding = new Padding(20, 10, 20, 10), BackColor = ColBack };
            listContainer.Controls.Add(modListBox);

            // --- Status bar ---
            var infoPanel = new SmoothPanel { Dock = DockStyle.Bottom, Height = 32, BackColor = ColPanel };
            shvLabel = new Label
            {
                Dock = DockStyle.Right,
                Width = 400,
                Text = "",
                ForeColor = ColTextDim,
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(0, 0, 18, 0)
            };
            statusLabel = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Ready",
                ForeColor = ColTextDim,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(18, 0, 0, 0)
            };
            infoPanel.Controls.Add(statusLabel);
            infoPanel.Controls.Add(shvLabel);

            // --- Launch buttons ---
            var btnPanel = new SmoothPanel { Dock = DockStyle.Bottom, Height = 92, BackColor = ColBack };

            RoundedButton CreateLaunchBtn(string text, Color color, EventHandler action)
            {
                var btn = new RoundedButton
                {
                    Text = text,
                    BackColor = color,
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold),
                    Size = new Size(200, 54),
                    Radius = 12
                };
                btn.Click += action;
                _actionControls.Add(btn);
                return btn;
            }

            var btnClean = CreateLaunchBtn("LAUNCH CLEAN", ColRed, (s, e) => LaunchClean());
            var btnSelected = CreateLaunchBtn("LAUNCH SELECTED", ColBlue, (s, e) => LaunchSelected());
            var btnAll = CreateLaunchBtn("LAUNCH ALL MODS", ColGreen, (s, e) => LaunchAll());

            btnPanel.Controls.Add(btnClean);
            btnPanel.Controls.Add(btnSelected);
            btnPanel.Controls.Add(btnAll);

            btnPanel.Layout += (s, e) =>
            {
                const int gap = 14;
                const int margin = 20;

                // Shrink the buttons if the window is too narrow for full-size ones.
                int btnW = Math.Min(200, (btnPanel.Width - margin * 2 - gap * 2) / 3);
                btnClean.Width = btnW;
                btnSelected.Width = btnW;
                btnAll.Width = btnW;

                int total = btnW * 3 + gap * 2;
                int x = Math.Max(margin, (btnPanel.Width - total) / 2);
                int y = (btnPanel.Height - btnClean.Height) / 2;
                btnClean.Location = new Point(x, y);
                btnSelected.Location = new Point(x + btnW + gap, y);
                btnAll.Location = new Point(x + (btnW + gap) * 2, y);
            };

            this.Controls.Add(listContainer);
            this.Controls.Add(infoPanel);
            this.Controls.Add(btnPanel);
            this.Controls.Add(profilePanel);
            this.Controls.Add(pathPanel);
            this.Controls.Add(headerPanel);
        }

        // ---------- Config ----------

        private void LoadConfig()
        {
            string? fileToLoad = null;
            if (File.Exists(ConfigFile)) fileToLoad = ConfigFile;
            else if (File.Exists(LegacyConfigFile)) fileToLoad = LegacyConfigFile;

            if (fileToLoad != null)
            {
                try
                {
                    var loaded = JsonSerializer.Deserialize<ConfigData>(File.ReadAllText(fileToLoad));
                    if (loaded != null) _config = loaded;
                }
                catch
                {
                    // Corrupt config — fall back to defaults.
                }
            }

            pathTextBox.Text = _config.GamePath;

            // One-time migration of the old exe-relative config into %AppData%.
            if (fileToLoad == LegacyConfigFile)
            {
                SaveConfig();
                try { File.Delete(LegacyConfigFile); } catch { }
            }

            RefreshProfileList();
        }

        private void SaveConfig()
        {
            _config.GamePath = pathTextBox.Text;
            try
            {
                Directory.CreateDirectory(ConfigDir);
                string json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigFile, json);
            }
            catch (Exception ex)
            {
                statusLabel.Text = "Failed to save config: " + ex.Message;
            }
        }

        private void BrowseFolder()
        {
            using (var fbd = new FolderBrowserDialog { SelectedPath = _config.GamePath })
            {
                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    pathTextBox.Text = fbd.SelectedPath;
                    SaveConfig();
                    RefreshModList();
                }
            }
        }

        // ---------- Mod detection ----------

        private bool IsMod(string entryPath)
        {
            string name = Path.GetFileName(entryPath);
            string ext = Path.GetExtension(name);
            bool isDir = Directory.Exists(entryPath);

            // Known mod files/folders are always mods, even if a game update collides with the name.
            if (KnownModFiles.Contains(name)) return true;
            if (isDir && KnownModDirs.Contains(name)) return true;
            if (ModExtensions.Contains(ext)) return true;

            if (VanillaWhitelist.Contains(name)) return false;
            if (name.Equals(DisabledModsDir, StringComparison.OrdinalIgnoreCase)) return false;
            if (ext.Equals(".log", StringComparison.OrdinalIgnoreCase)) return false;

            // Safety net: never treat a loose .rpf in the game root as a mod. A game update
            // could add a new archive (e.g. x64x.rpf) that isn't in the whitelist yet, and
            // moving it out would break the install.
            if (!isDir && ext.Equals(".rpf", StringComparison.OrdinalIgnoreCase)) return false;

            return true;
        }

        private void RefreshModList()
        {
            modListBox.ClearItems();
            if (!Directory.Exists(_config.GamePath))
            {
                statusLabel.Text = "Game directory not found.";
                shvLabel.Text = "";
                return;
            }

            var allMods = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var activeMods = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                foreach (var entry in Directory.GetFileSystemEntries(_config.GamePath))
                {
                    if (IsMod(entry))
                    {
                        string name = Path.GetFileName(entry);
                        allMods.Add(name);
                        activeMods.Add(name);
                    }
                }
            }
            catch { }

            // Scan disabled folder — anything in there was put there by us, so list it all.
            string disabledPath = Path.Combine(_config.GamePath, DisabledModsDir);
            if (Directory.Exists(disabledPath))
            {
                foreach (var entry in Directory.GetFileSystemEntries(disabledPath))
                {
                    string name = Path.GetFileName(entry);
                    if (!Path.GetExtension(name).Equals(".log", StringComparison.OrdinalIgnoreCase))
                    {
                        allMods.Add(name);
                    }
                }
            }

            foreach (var mod in allMods.OrderBy(x => x))
            {
                modListBox.AddItem(mod, activeMods.Contains(mod));
            }

            statusLabel.Text = $"Found {allMods.Count} mods ({activeMods.Count} enabled)";
            UpdateShvStatus();
        }

        // ---------- ScriptHookV version check ----------

        private void UpdateShvStatus()
        {
            shvLabel.Text = "";
            shvLabel.ForeColor = ColTextDim;

            string shvPath = Path.Combine(_config.GamePath, "ScriptHookV.dll");
            if (!File.Exists(shvPath))
                shvPath = Path.Combine(_config.GamePath, DisabledModsDir, "ScriptHookV.dll");
            if (!File.Exists(shvPath)) return;

            string? exePath = new[] { "GTA5_Enhanced.exe", "GTA5.exe" }
                .Select(e => Path.Combine(_config.GamePath, e))
                .FirstOrDefault(File.Exists);

            try
            {
                string shvVer = FileVersionInfo.GetVersionInfo(shvPath).FileVersion ?? "?";
                if (exePath == null)
                {
                    shvLabel.Text = $"ScriptHookV {shvVer}";
                    return;
                }

                string gameVer = FileVersionInfo.GetVersionInfo(exePath).ProductVersion ?? "?";
                bool outdated = Version.TryParse(shvVer, out var sv)
                             && Version.TryParse(gameVer, out var gv)
                             && gv > sv;

                if (outdated)
                {
                    shvLabel.Text = $"\u26a0 ScriptHookV {shvVer} may be outdated (game is {gameVer})";
                    shvLabel.ForeColor = Color.Orange;
                }
                else
                {
                    shvLabel.Text = $"ScriptHookV {shvVer} / game {gameVer}";
                }
            }
            catch
            {
                // Version info unreadable — not worth surfacing.
            }
        }

        // ---------- Mod processing ----------

        private static bool IsGameRunning()
        {
            string[] names = { "GTA5", "GTA5_Enhanced", "GTAVLauncher", "PlayGTAV" };
            return names.Any(n => Process.GetProcessesByName(n).Length > 0);
        }

        private static void CopyDirectory(string source, string dest)
        {
            Directory.CreateDirectory(dest);
            foreach (var file in Directory.GetFiles(source))
                File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), true);
            foreach (var dir in Directory.GetDirectories(source))
                CopyDirectory(dir, Path.Combine(dest, Path.GetFileName(dir)));
        }

        // Move with a copy+delete fallback for cross-volume moves / junctions.
        private static void MoveEntry(string source, string dest)
        {
            if (File.Exists(source))
            {
                try { File.Move(source, dest); }
                catch (IOException)
                {
                    File.Copy(source, dest, true);
                    File.Delete(source);
                }
            }
            else if (Directory.Exists(source))
            {
                try { Directory.Move(source, dest); }
                catch (IOException)
                {
                    CopyDirectory(source, dest);
                    Directory.Delete(source, true);
                }
            }
            // Neither exists: already in the right place, nothing to do.
        }

        private void SetActionsEnabled(bool enabled)
        {
            foreach (var c in _actionControls) c.Enabled = enabled;
            modListBox.Enabled = enabled;
            profileCombo.Enabled = enabled;
        }

        private async Task<bool> ProcessModsAsync(bool rememberSelection = true)
        {
            if (IsGameRunning())
            {
                MessageBox.Show(
                    "GTA V appears to be running. Close the game before enabling or disabling mods.",
                    "Game Running", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            string gamePath = _config.GamePath;
            if (!Directory.Exists(gamePath))
            {
                statusLabel.Text = "Game directory not found.";
                return false;
            }

            string disabledPath = Path.Combine(gamePath, DisabledModsDir);

            var allMods = modListBox.AllNames.ToList();
            var enabled = modListBox.CheckedNames.ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (rememberSelection)
            {
                _config.LastSelection = enabled.ToList();
                SaveConfig();
            }

            SetActionsEnabled(false);
            statusLabel.Text = "Processing mods...";
            var errors = new List<string>();

            await Task.Run(() =>
            {
                Directory.CreateDirectory(disabledPath);

                foreach (var modName in allMods)
                {
                    string activePath = Path.Combine(gamePath, modName);
                    string inactivePath = Path.Combine(disabledPath, modName);

                    try
                    {
                        if (enabled.Contains(modName)) MoveEntry(inactivePath, activePath);
                        else MoveEntry(activePath, inactivePath);
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"{modName}: {ex.Message}");
                    }
                }

                try
                {
                    if (Directory.Exists(disabledPath) && !Directory.EnumerateFileSystemEntries(disabledPath).Any())
                        Directory.Delete(disabledPath);
                }
                catch { }
            });

            SetActionsEnabled(true);

            if (errors.Count > 0)
            {
                MessageBox.Show("Some mods could not be moved:\n\n" + string.Join("\n", errors),
                    "Errors", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            statusLabel.Text = "Mod processing complete.";
            RefreshModList();
            return true;
        }

        // ---------- Profiles ----------

        private void EnsureDefaultProfile()
        {
            if (!_config.Profiles.ContainsKey(DefaultProfileName))
            {
                // First run: snapshot whatever is currently enabled.
                _config.Profiles[DefaultProfileName] = modListBox.CheckedNames.ToList();
                SaveConfig();
            }
            RefreshProfileList();
        }

        private void RefreshProfileList()
        {
            string current = profileCombo.Text;
            profileCombo.Items.Clear();
            foreach (var name in _config.Profiles.Keys.OrderBy(k => k))
                profileCombo.Items.Add(name);

            // Never leave the box blank.
            if (current.Length > 0 && _config.Profiles.ContainsKey(current))
                profileCombo.Text = current;
            else if (_config.Profiles.ContainsKey(DefaultProfileName))
                profileCombo.Text = DefaultProfileName;
            else if (profileCombo.Items.Count > 0)
                profileCombo.Text = (string)profileCombo.Items[0]!;
        }

        private void SaveProfile()
        {
            string name = profileCombo.Text.Trim();
            if (name.Length == 0)
            {
                MessageBox.Show("Type a profile name in the box first.", "Save Profile",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _config.Profiles[name] = modListBox.CheckedNames.ToList();
            SaveConfig();
            RefreshProfileList();
            profileCombo.Text = name;
            statusLabel.Text = $"Profile '{name}' saved.";
        }

        private void LoadProfile()
        {
            string name = profileCombo.Text.Trim();
            if (!_config.Profiles.TryGetValue(name, out var mods))
            {
                statusLabel.Text = "Profile not found.";
                return;
            }

            ApplySelection(mods);
            statusLabel.Text = $"Profile '{name}' loaded — press a launch button to apply.";
        }

        private void DeleteProfile()
        {
            string name = profileCombo.Text.Trim();
            if (name.Equals(DefaultProfileName, StringComparison.OrdinalIgnoreCase))
            {
                statusLabel.Text = "The Default profile can't be deleted — overwrite it with Save instead.";
                return;
            }
            if (_config.Profiles.Remove(name))
            {
                SaveConfig();
                profileCombo.Text = "";
                RefreshProfileList(); // falls back to Default
                statusLabel.Text = $"Profile '{name}' deleted.";
            }
        }

        private void RestoreLastSelection()
        {
            if (_config.LastSelection.Count == 0)
            {
                statusLabel.Text = "No previous selection saved.";
                return;
            }
            ApplySelection(_config.LastSelection);
            statusLabel.Text = "Last selection restored — press a launch button to apply.";
        }

        private void ApplySelection(IEnumerable<string> mods)
        {
            var set = new HashSet<string>(mods, StringComparer.OrdinalIgnoreCase);
            foreach (var name in modListBox.AllNames.ToList())
                modListBox.SetChecked(name, set.Contains(name));
        }

        // ---------- Launching ----------

        private void RunGame()
        {
            string[] exes = { "PlayGTAV.exe", "GTA5_Enhanced.exe", "GTA5.exe", "GTAVLauncher.exe" };
            foreach (var exe in exes)
            {
                string exePath = Path.Combine(_config.GamePath, exe);
                if (File.Exists(exePath))
                {
                    Process.Start(new ProcessStartInfo { FileName = exePath, WorkingDirectory = _config.GamePath });
                    return;
                }
            }
            MessageBox.Show("Could not find a valid game executable.", "Launch Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            Process.Start("explorer.exe", _config.GamePath);
        }

        private async void LaunchSelected()
        {
            if (await ProcessModsAsync()) RunGame();
        }

        private async void LaunchClean()
        {
            // Remember what was enabled so "Restore Last" can bring it back afterward.
            _config.LastSelection = modListBox.CheckedNames.ToList();
            SaveConfig();

            modListBox.SetAllChecked(false);

            if (await ProcessModsAsync(rememberSelection: false)) RunGame();
        }

        private async void LaunchAll()
        {
            modListBox.SetAllChecked(true);

            if (await ProcessModsAsync()) RunGame();
        }

        // ---------- Custom controls ----------

        /// <summary>Panel with double buffering enabled to prevent flicker.</summary>
        private sealed class SmoothPanel : Panel
        {
            public SmoothPanel()
            {
                SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint
                       | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            }
        }

        /// <summary>Flat rounded button with hover/press shading.</summary>
        private sealed class RoundedButton : Button
        {
            public int Radius { get; set; } = 10;
            private bool _hover;
            private bool _pressed;

            public RoundedButton()
            {
                SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint
                       | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                FlatStyle = FlatStyle.Flat;
                FlatAppearance.BorderSize = 0;
                Cursor = Cursors.Hand;
            }

            protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
            protected override void OnMouseLeave(EventArgs e) { _hover = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
            protected override void OnMouseDown(MouseEventArgs e) { _pressed = true; Invalidate(); base.OnMouseDown(e); }
            protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Parent?.BackColor ?? ColBack);

                Color fill = BackColor;
                if (!Enabled) fill = Color.FromArgb(40, 40, 45);
                else if (_pressed) fill = ControlPaint.Dark(BackColor, 0.05f);
                else if (_hover) fill = ControlPaint.Light(BackColor, 0.25f);

                var rect = new Rectangle(0, 0, Width - 1, Height - 1);
                using (var path = Rounded(rect, Radius))
                using (var brush = new SolidBrush(fill))
                {
                    g.FillPath(brush, path);
                }

                Color textColor = Enabled ? ForeColor : ColTextFaint;
                TextRenderer.DrawText(g, Text, Font, rect, textColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        /// <summary>Owner-drawn mod list with toggle switches and hover highlighting.</summary>
        private sealed class ModListBox : ListBox
        {
            private readonly HashSet<string> _checkedNames = new(StringComparer.OrdinalIgnoreCase);
            private int _hoverIndex = -1;

            public ModListBox()
            {
                DrawMode = DrawMode.OwnerDrawFixed;
                ItemHeight = 42;
                BorderStyle = BorderStyle.None;
                IntegralHeight = false;
                SelectionMode = SelectionMode.One;
                Cursor = Cursors.Hand;
                SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint
                       | ControlStyles.ResizeRedraw, true);
            }

            public IEnumerable<string> AllNames => Items.Cast<string>();
            public IEnumerable<string> CheckedNames => AllNames.Where(n => _checkedNames.Contains(n));

            public void AddItem(string name, bool isChecked)
            {
                Items.Add(name);
                if (isChecked) _checkedNames.Add(name);
            }

            public void ClearItems()
            {
                Items.Clear();
                _checkedNames.Clear();
            }

            public void SetChecked(string name, bool value)
            {
                bool changed = value ? _checkedNames.Add(name) : _checkedNames.Remove(name);
                if (!changed) return;
                InvalidateItem(Items.IndexOf(name));
            }

            private void InvalidateItem(int index)
            {
                if (index >= 0 && index < Items.Count)
                    Invalidate(GetItemRectangle(index));
            }

            public void SetAllChecked(bool value)
            {
                _checkedNames.Clear();
                if (value)
                    foreach (var n in AllNames) _checkedNames.Add(n);
                Invalidate();
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                int idx = IndexFromPoint(e.Location);
                if (idx != _hoverIndex)
                {
                    // Only repaint the two rows that changed, not the whole list.
                    int old = _hoverIndex;
                    _hoverIndex = idx;
                    InvalidateItem(old);
                    InvalidateItem(idx);
                }
                base.OnMouseMove(e);
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                int old = _hoverIndex;
                _hoverIndex = -1;
                InvalidateItem(old);
                base.OnMouseLeave(e);
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                int idx = IndexFromPoint(e.Location);
                if (idx >= 0 && idx < Items.Count)
                {
                    string name = (string)Items[idx];
                    SetChecked(name, !_checkedNames.Contains(name));
                }
                base.OnMouseDown(e);
            }

            protected override void OnDrawItem(DrawItemEventArgs e)
            {
                if (e.Index < 0 || e.Index >= Items.Count) return;

                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                string name = (string)Items[e.Index];
                bool on = _checkedNames.Contains(name);
                bool hover = e.Index == _hoverIndex;

                using (var bg = new SolidBrush(BackColor))
                    g.FillRectangle(bg, e.Bounds);

                var row = new Rectangle(e.Bounds.X + 4, e.Bounds.Y + 3, e.Bounds.Width - 8, e.Bounds.Height - 6);
                Color rowColor = hover ? ColRowHover : (on ? ColRowOn : ColRow);
                using (var path = Rounded(row, 8))
                using (var brush = new SolidBrush(rowColor))
                    g.FillPath(brush, path);

                // Toggle switch
                var pill = new Rectangle(row.X + 14, row.Y + row.Height / 2 - 10, 38, 20);
                Color pillColor = on ? Accent : Color.FromArgb(72, 72, 80);
                using (var path = Rounded(pill, 10))
                using (var brush = new SolidBrush(pillColor))
                    g.FillPath(brush, path);

                int knobSize = 14;
                int knobX = on ? pill.Right - knobSize - 3 : pill.X + 3;
                int knobY = pill.Y + (pill.Height - knobSize) / 2;
                g.FillEllipse(Brushes.White, knobX, knobY, knobSize, knobSize);

                // Mod name
                var textRect = new Rectangle(pill.Right + 14, row.Y, row.Right - pill.Right - 110, row.Height);
                Color textColor = on ? Color.White : ColTextDim;
                TextRenderer.DrawText(g, name, Font, textRect, textColor,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);

                // Subtle state text on the right
                string state = on ? "ENABLED" : "disabled";
                var stateRect = new Rectangle(row.Right - 90, row.Y, 80, row.Height);
                using var stateFont = new Font("Segoe UI", 7.5F, FontStyle.Bold);
                TextRenderer.DrawText(g, state, stateFont, stateRect,
                    on ? Accent : ColTextFaint,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
            }
        }
    }
}
