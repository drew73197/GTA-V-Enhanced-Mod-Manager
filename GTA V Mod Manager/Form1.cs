using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;

namespace GTAVModManager
{
    public class ConfigData
    {
        public string GamePath { get; set; } = @"C:\Program Files (x86)\Steam\steamapps\common\Grand Theft Auto V Enhanced";
    }

    public partial class Form1 : Form
    {
        private const string ConfigFile = "mod_manager_config.json";
        private const string DisabledModsDir = "Disabled mods";

        private ConfigData _config = new ConfigData();

        private TextBox pathTextBox = null!;
        private CheckedListBox modListBox = null!;
        private Label statusLabel = null!;

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

        public Form1()
        {
            SetupUI();
            LoadConfig();
            RefreshModList();
        }

        private void SetupUI()
        {
            this.Text = "GTA V Enhanced Mod Manager";
            this.Size = new Size(800, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(30, 30, 30);
            this.ForeColor = Color.White;

            var pathPanel = new Panel { Dock = DockStyle.Top, Height = 60, Padding = new Padding(15) };

            var lblPath = new Label { Text = "Game Folder:", Location = new Point(15, 5), AutoSize = true, ForeColor = Color.LightGray };
            pathTextBox = new TextBox { Width = 600, Location = new Point(15, 25), BackColor = Color.FromArgb(50, 50, 50), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
            var browseBtn = new Button { Text = "Browse", Location = new Point(625, 24), Height = 22, BackColor = Color.FromArgb(60, 60, 60), FlatStyle = FlatStyle.Flat, ForeColor = Color.White };
            browseBtn.FlatAppearance.BorderSize = 0;
            browseBtn.Click += (s, e) => BrowseFolder();

            pathPanel.Controls.Add(lblPath);
            pathPanel.Controls.Add(pathTextBox);
            pathPanel.Controls.Add(browseBtn);

            modListBox = new CheckedListBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 10),
                CheckOnClick = true
            };

            var listContainer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 0, 20, 0) };
            listContainer.Controls.Add(modListBox);

            statusLabel = new Label { Dock = DockStyle.Bottom, Height = 30, Text = "Ready", ForeColor = Color.Gray, Padding = new Padding(10, 5, 0, 0) };

            var btnPanel = new Panel { Dock = DockStyle.Bottom, Height = 80, Padding = new Padding(10) };

            Button CreateBtn(string text, Color color, EventHandler action, int x)
            {
                var btn = new Button
                {
                    Text = text,
                    BackColor = color,
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 10, FontStyle.Bold),
                    Width = 180,
                    Height = 50,
                    Location = new Point(x, 15),
                    FlatStyle = FlatStyle.Flat
                };
                btn.FlatAppearance.BorderSize = 0;
                btn.Click += action;
                return btn;
            }

            var btnClean = CreateBtn("LAUNCH CLEAN", Color.FromArgb(198, 40, 40), (s, e) => LaunchClean(), 50);
            var btnSelected = CreateBtn("LAUNCH SELECTED", Color.FromArgb(30, 136, 229), (s, e) => LaunchSelected(), 250);
            var btnAll = CreateBtn("LAUNCH ALL MODS", Color.FromArgb(56, 142, 60), (s, e) => LaunchAll(), 450);

            var btnRefresh = new Button { Text = "↻ Refresh List", Location = new Point(650, 25), Width = 100, BackColor = Color.FromArgb(60, 60, 60), FlatStyle = FlatStyle.Flat, ForeColor = Color.White };
            btnRefresh.FlatAppearance.BorderSize = 0;
            btnRefresh.Click += (s, e) => RefreshModList();

            btnPanel.Controls.Add(btnClean);
            btnPanel.Controls.Add(btnSelected);
            btnPanel.Controls.Add(btnAll);
            btnPanel.Controls.Add(btnRefresh);

            this.Controls.Add(listContainer);
            this.Controls.Add(statusLabel);
            this.Controls.Add(btnPanel);
            this.Controls.Add(pathPanel);
        }

        private void LoadConfig()
        {
            if (File.Exists(ConfigFile))
            {
                try
                {
                    string json = File.ReadAllText(ConfigFile);
                    // Handle possible null return from deserialize
                    var loaded = JsonSerializer.Deserialize<ConfigData>(json);
                    if (loaded != null)
                    {
                        _config = loaded;
                    }
                }
                catch
                {
                }
            }
            pathTextBox.Text = _config.GamePath;
        }

        private void SaveConfig()
        {
            _config.GamePath = pathTextBox.Text;
            string json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigFile, json);
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

        private void RefreshModList()
        {
            modListBox.Items.Clear();
            if (!Directory.Exists(_config.GamePath))
            {
                statusLabel.Text = "Game directory not found.";
                return;
            }

            var allMods = new HashSet<string>();
            var activeMods = new HashSet<string>();

            try
            {
                foreach (var entry in Directory.GetFileSystemEntries(_config.GamePath))
                {
                    string name = Path.GetFileName(entry);

                    if (!VanillaWhitelist.Contains(name)
                        && !name.Equals(DisabledModsDir, StringComparison.OrdinalIgnoreCase)
                        && !Path.GetExtension(name).Equals(".log", StringComparison.OrdinalIgnoreCase))
                    {
                        allMods.Add(name);
                        activeMods.Add(name);
                    }
                }
            }
            catch { }

            // 2. Scan Disabled Folder
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

            // 3. Populate UI
            var sortedMods = allMods.OrderBy(x => x).ToList();
            foreach (var mod in sortedMods)
            {
                bool isActive = activeMods.Contains(mod);
                modListBox.Items.Add(mod, isActive);
            }

            statusLabel.Text = $"Found {allMods.Count} mod files";
        }

        private void ProcessMods()
        {
            statusLabel.Text = "Processing mods...";
            Application.DoEvents();

            string gamePath = _config.GamePath;
            string disabledPath = Path.Combine(gamePath, DisabledModsDir);

            if (!Directory.Exists(disabledPath)) Directory.CreateDirectory(disabledPath);

            var modsToEnable = new HashSet<string>();
            foreach (var item in modListBox.CheckedItems)
            {
                if (item != null)
                {
                    modsToEnable.Add(item.ToString()!);
                }
            }

            foreach (var item in modListBox.Items)
            {
                string? modName = item?.ToString();
                if (string.IsNullOrEmpty(modName)) continue;

                string activePath = Path.Combine(gamePath, modName);
                string inactivePath = Path.Combine(disabledPath, modName);

                bool shouldBeActive = modsToEnable.Contains(modName);

                try
                {
                    if (shouldBeActive)
                    {
                        if (File.Exists(inactivePath)) File.Move(inactivePath, activePath);
                        else if (Directory.Exists(inactivePath)) Directory.Move(inactivePath, activePath);
                    }
                    else
                    {
                        if (File.Exists(activePath)) File.Move(activePath, inactivePath);
                        else if (Directory.Exists(activePath)) Directory.Move(activePath, inactivePath);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error moving {modName}: {ex.Message}");
                }
            }

            if (Directory.Exists(disabledPath) && !Directory.EnumerateFileSystemEntries(disabledPath).Any())
            {
                Directory.Delete(disabledPath);
            }

            statusLabel.Text = "Mod processing complete.";
            RefreshModList();
        }

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

        private void LaunchSelected()
        {
            ProcessMods();
            RunGame();
        }

        private void LaunchClean()
        {
            for (int i = 0; i < modListBox.Items.Count; i++)
                modListBox.SetItemChecked(i, false);

            LaunchSelected();
        }

        private void LaunchAll()
        {
            for (int i = 0; i < modListBox.Items.Count; i++)
                modListBox.SetItemChecked(i, true);

            LaunchSelected();
        }
    }
}