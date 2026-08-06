using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;

namespace OmnipetModuleEditor.OmniNet
{
    /// <summary>
    /// Omninet ▸ Manage Modules — shows every module published on Omninet
    /// against what is installed in the game's modules folder.
    ///
    /// Anyone can open this window and download any module (no login
    /// required); the Ownership column (Owner / Contributor) is only filled
    /// when logged in, and publishing/updating stays in the existing
    /// Manage Module dialog (owners/contributors only).
    /// </summary>
    public class ModuleBrowserForm : Form
    {
        private readonly string _modulesRoot;
        private readonly string _currentModuleFolder;

        private DataGridView grid;
        private Label lblStatus;
        private Button btnRefresh;
        private Button btnClose;

        private List<ModuleListItem> _serverModules = new List<ModuleListItem>();
        // module name (from module.json) -> (version, folder name)
        private Dictionary<string, (string Version, string Folder)> _installed =
            new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase);
        // server module id -> "Owner" / "Contributor"
        private Dictionary<string, string> _ownership =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private bool _busy;

        /// <param name="modulesRoot">The game's modules directory.</param>
        /// <param name="currentModuleFolder">Folder name of the module currently
        /// open in the editor (downloads into it are refused to avoid clobbering
        /// unsaved work), or null.</param>
        public ModuleBrowserForm(string modulesRoot, string currentModuleFolder = null)
        {
            _modulesRoot = modulesRoot;
            _currentModuleFolder = currentModuleFolder;
            InitializeComponent();
            Shown += async (s, e) => await LoadDataAsync();
        }

        private void InitializeComponent()
        {
            Text = "Omninet Modules";
            Size = new Size(760, 480);
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(10)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

            grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AllowUserToResizeRows = false
            };
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colName", HeaderText = "Module", FillWeight = 24 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colAuthor", HeaderText = "Author", FillWeight = 16 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCategory", HeaderText = "Category", FillWeight = 13 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colInstalled", HeaderText = "Installed", FillWeight = 11 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colServer", HeaderText = "Omninet", FillWeight = 11 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colOwnership", HeaderText = "Ownership", FillWeight = 12 });
            grid.Columns.Add(new DataGridViewButtonColumn { Name = "colAction", HeaderText = "", FillWeight = 13 });
            grid.CellClick += Grid_CellClick;
            layout.Controls.Add(grid, 0, 0);

            lblStatus = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "Loading..."
            };
            layout.Controls.Add(lblStatus, 0, 1);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft
            };
            btnClose = new Button { Text = "Close", Width = 90, Height = 28 };
            btnClose.Click += (s, e) => Close();
            btnRefresh = new Button { Text = "Refresh", Width = 90, Height = 28 };
            btnRefresh.Click += async (s, e) => await LoadDataAsync();
            buttons.Controls.Add(btnClose);
            buttons.Controls.Add(btnRefresh);
            layout.Controls.Add(buttons, 0, 2);

            Controls.Add(layout);
        }

        // =========================
        // Data loading
        // =========================

        private async System.Threading.Tasks.Task LoadDataAsync()
        {
            if (_busy) return;
            _busy = true;
            btnRefresh.Enabled = false;
            lblStatus.ForeColor = SystemColors.ControlText;
            lblStatus.Text = "Loading modules from Omninet...";

            try
            {
                ScanInstalledModules();

                using (var client = new OmniNetApiClient())
                {
                    var listResponse = await client.ListAllModulesAsync();
                    if (!listResponse.Success)
                    {
                        lblStatus.ForeColor = Color.Red;
                        lblStatus.Text = $"Could not reach Omninet: {listResponse.ErrorMessage}";
                        _serverModules = new List<ModuleListItem>();
                        PopulateGrid();
                        return;
                    }
                    _serverModules = listResponse.Data ?? new List<ModuleListItem>();

                    // Ownership column — only available when logged in.
                    _ownership.Clear();
                    var config = OmniNetConfig.Instance;
                    if (config.IsLoggedIn)
                    {
                        var mineResponse = await client.ListMyModulesAsync(config.SecretKey);
                        if (mineResponse.Success && mineResponse.Data != null)
                        {
                            foreach (var m in mineResponse.Data)
                            {
                                bool isOwner = string.Equals(m.owner_nickname, config.Nickname,
                                    StringComparison.OrdinalIgnoreCase);
                                _ownership[m.id ?? ""] = isOwner ? "Owner" : "Contributor";
                            }
                        }
                    }
                }

                PopulateGrid();
                lblStatus.Text = $"{_serverModules.Count} module(s) on Omninet, " +
                                 $"{_installed.Count} installed locally.";
            }
            finally
            {
                _busy = false;
                btnRefresh.Enabled = true;
            }
        }

        /// <summary>
        /// Read name + version out of every modules-folder module.json.
        /// </summary>
        private void ScanInstalledModules()
        {
            _installed.Clear();
            if (string.IsNullOrEmpty(_modulesRoot) || !Directory.Exists(_modulesRoot))
                return;

            foreach (var dir in Directory.GetDirectories(_modulesRoot))
            {
                var jsonPath = Path.Combine(dir, "module.json");
                if (!File.Exists(jsonPath))
                    continue;
                try
                {
                    using (var doc = JsonDocument.Parse(File.ReadAllText(jsonPath)))
                    {
                        var root = doc.RootElement;
                        string name = root.TryGetProperty("name", out var n) ? n.GetString() : null;
                        string version = root.TryGetProperty("version", out var v) ? v.GetString() : "?";
                        if (!string.IsNullOrEmpty(name))
                            _installed[name] = (version ?? "?", Path.GetFileName(dir));
                    }
                }
                catch
                {
                    // Malformed module.json — skip the folder.
                }
            }
        }

        private void PopulateGrid()
        {
            grid.Rows.Clear();
            foreach (var mod in _serverModules.OrderBy(m => m.name, StringComparer.OrdinalIgnoreCase))
            {
                string installedVersion = null;
                if (_installed.TryGetValue(mod.name ?? "", out var inst))
                    installedVersion = inst.Version;

                string ownership = _ownership.TryGetValue(mod.id ?? "", out var o) ? o : "-";

                string action;
                if (installedVersion == null)
                    action = "Download";
                else if (installedVersion != mod.version)
                    action = "Update";
                else
                    action = "Reinstall";

                int rowIdx = grid.Rows.Add(
                    mod.name,
                    mod.owner_nickname,
                    mod.category_name ?? "-",
                    installedVersion ?? "-",
                    mod.version,
                    ownership,
                    action);
                grid.Rows[rowIdx].Tag = mod;

                // Subtle highlight when an update is available.
                if (installedVersion != null && installedVersion != mod.version)
                    grid.Rows[rowIdx].DefaultCellStyle.BackColor = Color.FromArgb(255, 250, 220);
            }
        }

        // =========================
        // Download / install
        // =========================

        private async void Grid_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (_busy || e.RowIndex < 0)
                return;
            if (grid.Columns[e.ColumnIndex].Name != "colAction")
                return;
            var mod = grid.Rows[e.RowIndex].Tag as ModuleListItem;
            if (mod == null)
                return;

            string targetFolder = SafeFolderName(mod.name, mod.id);
            if (!string.IsNullOrEmpty(_currentModuleFolder) &&
                string.Equals(targetFolder, _currentModuleFolder, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(
                    "This module is currently open in the editor.\n\n" +
                    "Close it first so the download doesn't overwrite unsaved work.",
                    "Module In Use", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string targetDir = Path.Combine(_modulesRoot, targetFolder);
            if (Directory.Exists(targetDir))
            {
                var confirm = MessageBox.Show(
                    $"'{mod.name}' is already installed.\n\nReplace the local copy with version {mod.version} from Omninet?",
                    "Replace Module", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (confirm != DialogResult.Yes)
                    return;
            }

            _busy = true;
            btnRefresh.Enabled = false;
            lblStatus.ForeColor = SystemColors.ControlText;
            lblStatus.Text = $"Downloading {mod.name}...";
            grid.Rows[e.RowIndex].Cells["colAction"].Value = "...";

            try
            {
                byte[] zipBytes;
                using (var client = new OmniNetApiClient())
                {
                    var response = await client.DownloadModuleZipAsync(mod.id);
                    if (!response.Success || response.Data == null)
                    {
                        lblStatus.ForeColor = Color.Red;
                        lblStatus.Text = $"Download failed: {response.ErrorMessage}";
                        grid.Rows[e.RowIndex].Cells["colAction"].Value = "Retry";
                        return;
                    }
                    zipBytes = response.Data;
                }

                lblStatus.Text = $"Installing {mod.name}...";
                InstallModuleZip(targetDir, zipBytes);

                // Refresh the row in place.
                _installed[mod.name] = (mod.version, targetFolder);
                grid.Rows[e.RowIndex].Cells["colInstalled"].Value = mod.version;
                grid.Rows[e.RowIndex].Cells["colAction"].Value = "Reinstall";
                grid.Rows[e.RowIndex].DefaultCellStyle.BackColor = grid.DefaultCellStyle.BackColor;
                lblStatus.Text = $"{mod.name} v{mod.version} installed.";
            }
            catch (Exception ex)
            {
                lblStatus.ForeColor = Color.Red;
                lblStatus.Text = $"Install failed: {ex.Message}";
                grid.Rows[e.RowIndex].Cells["colAction"].Value = "Retry";
            }
            finally
            {
                _busy = false;
                btnRefresh.Enabled = true;
            }
        }

        /// <summary>
        /// Folder name used on disk — sanitized display name (matches the
        /// game's install layout), falling back to the id.
        /// </summary>
        private static string SafeFolderName(string moduleName, string moduleId)
        {
            var safe = new string((moduleName ?? "")
                .Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == ' ')
                .ToArray()).Trim();
            return string.IsNullOrEmpty(safe) ? moduleId : safe;
        }

        /// <summary>
        /// Extract the zip into the target folder, flattening a single
        /// wrapping top-level directory so module.json sits at the root —
        /// the same layout the game produces when it installs modules.
        /// </summary>
        private static void InstallModuleZip(string targetDir, byte[] zipBytes)
        {
            if (Directory.Exists(targetDir))
                Directory.Delete(targetDir, true);
            Directory.CreateDirectory(targetDir);

            using (var ms = new MemoryStream(zipBytes))
            using (var archive = new ZipArchive(ms, ZipArchiveMode.Read))
            {
                archive.ExtractToDirectory(targetDir);
            }

            // Flatten a single wrapping folder.
            var entries = Directory.GetFileSystemEntries(targetDir);
            if (entries.Length == 1 && Directory.Exists(entries[0]) &&
                !File.Exists(Path.Combine(targetDir, "module.json")))
            {
                string inner = entries[0];
                foreach (var item in Directory.GetFileSystemEntries(inner))
                {
                    string dest = Path.Combine(targetDir, Path.GetFileName(item));
                    if (Directory.Exists(item))
                        Directory.Move(item, dest);
                    else
                        File.Move(item, dest);
                }
                Directory.Delete(inner);
            }
        }
    }
}
