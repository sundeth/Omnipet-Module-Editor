using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OmnipetModuleEditor.OmniNet
{
    /// <summary>
    /// Form for publishing, unpublishing, or managing a module on OmniNet.
    /// </summary>
    public class PublishModuleForm : Form
    {
        private readonly string _modulePath;
        private readonly string _moduleName;
        private readonly string _moduleVersion;

        private Label lblInfo;
        private Label lblModuleStatus;
        private Label lblStatus;
        private Label lblError;
        private Button btnPublish;
        private Button btnUnpublish;
        private Button btnManageContributors;
        private Button btnCancel;
        private ProgressBar progressBar;
        
        private string _action; // "create_new", "update_existing", "not_authorized"
        private string _moduleId; // Module ID from check response
        private bool _isOwner; // True if user is the owner (not just contributor)
        private string _currentStatus; // Current module status: "published", "unpublished", "banned"

        public PublishModuleForm(string modulePath, string moduleName, string moduleVersion)
        {
            _modulePath = modulePath;
            _moduleName = moduleName;
            _moduleVersion = moduleVersion;

            InitializeComponent();
            CheckModuleStatus();
        }

        private void InitializeComponent()
        {
            this.Text = "Manage Module";
            this.Size = new Size(500, 400);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 7,
                Padding = new Padding(20)
            };

            // Module info
            lblInfo = new Label
            {
                Text = $"Module: {_moduleName}\nVersion: {_moduleVersion}",
                Dock = DockStyle.Fill,
                AutoSize = false,
                Height = 50,
                Font = new Font(SystemFonts.DefaultFont.FontFamily, 10, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            };
            layout.Controls.Add(lblInfo, 0, 0);

            // Module status label (new)
            lblModuleStatus = new Label
            {
                Text = "",
                Dock = DockStyle.Fill,
                AutoSize = false,
                Height = 30,
                Font = new Font(SystemFonts.DefaultFont.FontFamily, 9, FontStyle.Regular),
                TextAlign = ContentAlignment.MiddleCenter,
                Visible = false
            };
            layout.Controls.Add(lblModuleStatus, 0, 1);

            // Status
            lblStatus = new Label
            {
                Text = "Checking module status...",
                Dock = DockStyle.Fill,
                AutoSize = false,
                Height = 60,
                TextAlign = ContentAlignment.MiddleCenter
            };
            layout.Controls.Add(lblStatus, 0, 2);

            // Error label
            lblError = new Label
            {
                Text = "",
                Dock = DockStyle.Fill,
                ForeColor = Color.Red,
                TextAlign = ContentAlignment.MiddleCenter,
                Height = 40
            };
            layout.Controls.Add(lblError, 0, 3);

            // Progress bar
            progressBar = new ProgressBar
            {
                Dock = DockStyle.Fill,
                Style = ProgressBarStyle.Marquee,
                Visible = true,
                Height = 15
            };
            layout.Controls.Add(progressBar, 0, 4);

            // Buttons panel - horizontal layout
            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(0, 10, 0, 0),
                WrapContents = true
            };

            btnPublish = new Button
            {
                Text = "Publish",
                Width = 120,
                Height = 35,
                Enabled = false
            };
            btnPublish.Click += BtnPublish_Click;
            buttonPanel.Controls.Add(btnPublish);

            btnUnpublish = new Button
            {
                Text = "Unpublish",
                Width = 120,
                Height = 35,
                Enabled = false,
                Visible = false
            };
            btnUnpublish.Click += BtnUnpublish_Click;
            buttonPanel.Controls.Add(btnUnpublish);

            btnManageContributors = new Button
            {
                Text = "Contributors",
                Width = 120,
                Height = 35,
                Enabled = false,
                Visible = false
            };
            btnManageContributors.Click += BtnManageContributors_Click;
            buttonPanel.Controls.Add(btnManageContributors);

            btnCancel = new Button
            {
                Text = "Close",
                Width = 80,
                Height = 35,
                DialogResult = DialogResult.Cancel
            };
            buttonPanel.Controls.Add(btnCancel);

            layout.Controls.Add(buttonPanel, 0, 5);

            this.Controls.Add(layout);
            this.CancelButton = btnCancel;
        }

        private async void CheckModuleStatus()
        {
            var config = OmniNetConfig.Instance;

            if (!config.IsLoggedIn)
            {
                lblStatus.Text = "You must be logged in to manage modules.";
                lblStatus.ForeColor = Color.Red;
                progressBar.Visible = false;
                return;
            }

            try
            {
                using (var client = new OmniNetApiClient())
                {
                    var response = await client.CheckModuleAsync(_moduleName, _moduleVersion, config.SecretKey);

                    if (response.Success && response.Data != null)
                    {
                        // Store module ID from response (use existing_module_id or module_id)
                        _moduleId = response.Data.module_id ?? response.Data.existing_module_id;
                        _action = response.Data.action;

                        System.Diagnostics.Debug.WriteLine($"[PublishModuleForm] Check response: action={_action}, module_id={_moduleId}");

                        // If module exists, get detailed status
                        if (_action == "update_existing" && !string.IsNullOrEmpty(_moduleId))
                        {
                            await LoadModuleDetails();
                        }

                        switch (_action)
                        {
                            case "create_new":
                                lblStatus.Text = "This module name is available.\nClick Publish to create a new module.";
                                lblStatus.ForeColor = Color.Green;
                                btnPublish.Enabled = true;
                                btnPublish.Text = "Publish New";
                                break;

                            case "update_existing":
                                // Status message will be updated by LoadModuleDetails
                                lblStatus.ForeColor = Color.Blue;
                                btnPublish.Enabled = true;
                                btnPublish.Text = "Update Module";
                                
                                // Show additional buttons for existing modules
                                btnUnpublish.Visible = true;
                                btnUnpublish.Enabled = true;
                                btnManageContributors.Visible = true;
                                btnManageContributors.Enabled = true;
                                
                                _isOwner = true; // User can update, so they're owner or contributor
                                break;

                            case "name_taken":
                                lblStatus.Text = "This module name is already taken by another user.\nPlease choose a different name in module.json.";
                                lblStatus.ForeColor = Color.Red;
                                break;

                            case "not_authorized":
                                lblStatus.Text = response.Data.message ?? 
                                    "You are not the owner or a contributor of this module.\nContact the owner to be added as a contributor.";
                                lblStatus.ForeColor = Color.Red;
                                break;

                            default:
                                lblStatus.Text = response.Data.message ?? "Unknown status.";
                                break;
                        }
                    }
                    else
                    {
                        lblStatus.Text = response.ErrorMessage ?? "Failed to check module status.";
                        lblStatus.ForeColor = Color.Red;
                    }
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"Error: {ex.Message}";
                lblStatus.ForeColor = Color.Red;
                System.Diagnostics.Debug.WriteLine($"[PublishModuleForm] Check error: {ex}");
            }
            finally
            {
                progressBar.Visible = false;
            }
        }

        private async Task LoadModuleDetails()
        {
            try
            {
                var config = OmniNetConfig.Instance;
                using (var client = new OmniNetApiClient())
                {
                    var response = await client.GetModuleStatusAsync(_moduleId, config.SecretKey);

                    if (response.Success && response.Data != null && response.Data.success)
                    {
                        _currentStatus = response.Data.status ?? "unknown";
                        
                        System.Diagnostics.Debug.WriteLine($"[PublishModuleForm] Module status: {_currentStatus}");

                        // Update status label with color coding
                        lblModuleStatus.Visible = true;
                        
                        switch (_currentStatus.ToLower())
                        {
                            case "published":
                                lblModuleStatus.Text = "Status: PUBLISHED ?";
                                lblModuleStatus.ForeColor = Color.Green;
                                lblStatus.Text = "Module is currently published.\nClick Update to publish a new version, or use options below.";
                                btnUnpublish.Text = "Unpublish";
                                break;
                            
                            case "unpublished":
                                lblModuleStatus.Text = "Status: UNPUBLISHED";
                                lblModuleStatus.ForeColor = Color.Orange;
                                lblStatus.Text = "Module is unpublished (not visible to users).\nClick Publish to make it available again.";
                                btnPublish.Text = "Publish";
                                btnUnpublish.Visible = false; // Already unpublished
                                break;
                            
                            case "banned":
                                lblModuleStatus.Text = "Status: BANNED ?";
                                lblModuleStatus.ForeColor = Color.Red;
                                lblStatus.Text = "This module has been banned by administrators.\nContact support for more information.";
                                lblStatus.ForeColor = Color.Red;
                                btnPublish.Enabled = false;
                                btnUnpublish.Enabled = false;
                                break;
                            
                            default:
                                lblModuleStatus.Text = $"Status: {_currentStatus.ToUpper()}";
                                lblModuleStatus.ForeColor = Color.Gray;
                                lblStatus.Text = "You own this module.\nClick Publish to update, or use other options below.";
                                break;
                        }
                    }
                    else
                    {
                        // Failed to get detailed status, use default message
                        lblStatus.Text = "You own this module.\nClick Publish to update, or use other options below.";
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PublishModuleForm] LoadModuleDetails error: {ex}");
                // Don't show error to user, just use default message
                lblStatus.Text = "You own this module.\nClick Publish to update, or use other options below.";
            }
        }

        private async void BtnPublish_Click(object sender, EventArgs e)
        {
            // Block publishing of incomplete modules before any upload happens.
            var validationErrors = ValidateModuleForPublish();
            if (validationErrors.Count > 0)
            {
                MessageBox.Show(
                    "This module cannot be published until the following issues are resolved:\n\n• "
                    + string.Join("\n• ", validationErrors),
                    "Cannot Publish Module",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            var confirmMessage = _action == "create_new"
                ? "Are you sure you want to publish this new module to OmniNet?"
                : "Are you sure you want to update this module on OmniNet?";

            var result = MessageBox.Show(confirmMessage, "Confirm Publish",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (result != DialogResult.Yes)
                return;

            SetLoading(true);
            lblError.Text = "";

            try
            {
                var config = OmniNetConfig.Instance;
                using (var client = new OmniNetApiClient())
                {
                    var response = await client.PublishModuleAsync(config.SecretKey, _modulePath);

                    if (response.Success && response.Data != null)
                    {
                        // Store the module ID from publish response
                        _moduleId = response.Data.module_id ?? _moduleId;
                        var version = response.Data.version ?? _moduleVersion;
                        
                        MessageBox.Show(
                            $"Module published successfully!\n\nModule ID: {_moduleId}\nVersion: {version}",
                            "Success",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);

                        // Refresh status to show new options
                        CheckModuleStatus();
                    }
                    else
                    {
                        lblError.Text = response.ErrorMessage ?? response.Data?.message ?? "Failed to publish module.";
                    }
                }
            }
            catch (Exception ex)
            {
                lblError.Text = $"Error: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"[PublishModuleForm] Publish error: {ex}");
            }
            finally
            {
                SetLoading(false);
            }
        }

        private async void BtnUnpublish_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_moduleId))
            {
                MessageBox.Show("Module ID is not available. Cannot unpublish.", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var result = MessageBox.Show(
                "Are you sure you want to unpublish this module?\n\nIt will no longer be available for download.",
                "Confirm Unpublish",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result != DialogResult.Yes)
                return;

            SetLoading(true);
            lblError.Text = "";

            try
            {
                var config = OmniNetConfig.Instance;
                using (var client = new OmniNetApiClient())
                {
                    var response = await client.UnpublishModuleAsync(_moduleId, config.SecretKey);

                    if (response.Success && response.Data != null && response.Data.success)
                    {
                        MessageBox.Show("Module unpublished successfully.", "Success",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                        
                        this.DialogResult = DialogResult.OK;
                        this.Close();
                    }
                    else
                    {
                        lblError.Text = response.ErrorMessage ?? response.Data?.message ?? "Failed to unpublish module.";
                    }
                }
            }
            catch (Exception ex)
            {
                lblError.Text = $"Error: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"[PublishModuleForm] Unpublish error: {ex}");
            }
            finally
            {
                SetLoading(false);
            }
        }

        private void BtnManageContributors_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_moduleId))
            {
                MessageBox.Show("Module ID is not available. Cannot manage contributors.", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                using (var form = new ManageContributorsForm(_moduleId, _moduleName))
                {
                    form.ShowDialog(this);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening contributors form: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                System.Diagnostics.Debug.WriteLine($"[PublishModuleForm] Contributors error: {ex}");
            }
        }

        /// <summary>
        /// Validate that the module on disk meets the minimum requirements to
        /// be published. Returns a list of human-readable problems (empty = OK).
        ///
        /// Rules:
        ///   - logo.png and Flag.png must always exist.
        ///   - Adventure-mode modules must also have BattleIcon.png (battle flag).
        ///   - At least one pet (monster.json).
        ///   - Adventure-mode modules need at least one battler that is not a
        ///     special encounter (battle.json).
        /// </summary>
        private List<string> ValidateModuleForPublish()
        {
            var errors = new List<string>();

            if (string.IsNullOrEmpty(_modulePath) || !Directory.Exists(_modulePath))
            {
                errors.Add("Module folder could not be found.");
                return errors;
            }

            // Logo + flag are always required.
            if (!File.Exists(Path.Combine(_modulePath, "logo.png")))
                errors.Add("Missing logo sprite (logo.png).");
            if (!File.Exists(Path.Combine(_modulePath, "Flag.png")))
                errors.Add("Missing flag sprite (Flag.png).");

            bool adventureMode = GetAdventureMode();

            // The battle flag is only required when the module has adventure mode.
            if (adventureMode && !File.Exists(Path.Combine(_modulePath, "BattleIcon.png")))
                errors.Add("Adventure mode modules require a battle flag sprite (BattleIcon.png).");

            // At least one pet.
            if (CountPets() < 1)
                errors.Add("Module must have at least one pet.");

            // Adventure mode requires at least one non-special battler.
            if (adventureMode && CountNonSpecialEnemies() < 1)
                errors.Add("Adventure mode modules require at least one battler that is not a special encounter.");

            return errors;
        }

        /// <summary>Reads the adventure_mode flag from module.json (false on any error).</summary>
        private bool GetAdventureMode()
        {
            try
            {
                string moduleJsonPath = Path.Combine(_modulePath, "module.json");
                if (!File.Exists(moduleJsonPath))
                    return false;
                using (var doc = JsonDocument.Parse(File.ReadAllText(moduleJsonPath)))
                {
                    if (doc.RootElement.TryGetProperty("adventure_mode", out var el))
                    {
                        if (el.ValueKind == JsonValueKind.True) return true;
                        if (el.ValueKind == JsonValueKind.False) return false;
                        if (el.ValueKind == JsonValueKind.String)
                            return bool.TryParse(el.GetString(), out var b) && b;
                    }
                }
            }
            catch { /* treat unreadable module.json as non-adventure */ }
            return false;
        }

        /// <summary>Counts pets in monster.json (0 on any error).</summary>
        private int CountPets()
        {
            try
            {
                string monsterPath = Path.Combine(_modulePath, "monster.json");
                if (!File.Exists(monsterPath))
                    return 0;
                using (var doc = JsonDocument.Parse(File.ReadAllText(monsterPath)))
                {
                    if (doc.RootElement.TryGetProperty("monster", out var arr)
                        && arr.ValueKind == JsonValueKind.Array)
                        return arr.GetArrayLength();
                }
            }
            catch { /* malformed monster.json counts as zero pets */ }
            return 0;
        }

        /// <summary>Counts battlers in battle.json that are NOT special encounters (0 on any error).</summary>
        private int CountNonSpecialEnemies()
        {
            try
            {
                string battlePath = Path.Combine(_modulePath, "battle.json");
                if (!File.Exists(battlePath))
                    return 0;
                using (var doc = JsonDocument.Parse(File.ReadAllText(battlePath)))
                {
                    if (doc.RootElement.TryGetProperty("enemies", out var arr)
                        && arr.ValueKind == JsonValueKind.Array)
                    {
                        int count = 0;
                        foreach (var enemy in arr.EnumerateArray())
                        {
                            bool special = enemy.TryGetProperty("special_encounter", out var se)
                                && se.ValueKind == JsonValueKind.True;
                            if (!special)
                                count++;
                        }
                        return count;
                    }
                }
            }
            catch { /* malformed battle.json counts as zero battlers */ }
            return 0;
        }

        private void SetLoading(bool loading)
        {
            progressBar.Visible = loading;
            btnPublish.Enabled = !loading && !string.IsNullOrEmpty(_action) &&
                (_action == "create_new" || _action == "update_existing") &&
                _currentStatus != "banned";
            btnUnpublish.Enabled = !loading && btnUnpublish.Visible && _currentStatus != "banned";
            btnManageContributors.Enabled = !loading && btnManageContributors.Visible;
        }
    }
}
