using OmnipetModuleEditor.controls;
using OmnipetModuleEditor.OmniNet;
using OmnipetModuleEditor.Reports;
using OmnipetModuleEditor.Tabs;
using OmnipetModuleEditor.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OmnipetModuleEditor
{
    /// <summary>
    /// Main form for editing a module, including tabs for module, pets, battle, and items.
    /// </summary>
    public partial class ModuleEditorForm : Form
    {
        private string currentPath;
        private Models.Module currentModule;
        private Form selectorForm;

        /// <summary>
        /// Initializes the module editor form.
        /// </summary>
        /// <param name="currentPath">Path to the module folder.</param>
        /// <param name="selectorForm">Reference to the selector form for returning after close.</param>
        public ModuleEditorForm(string currentPath, Form selectorForm)
        {
            this.currentPath = currentPath;
            this.selectorForm = selectorForm;
            InitializeComponent();

            var version = Assembly.GetExecutingAssembly().GetName().Version;
            this.Text = $"Omnipet Module Editor v{version}";

            // Block resizing
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.SizeGripStyle = SizeGripStyle.Hide;

            this.StartPosition = FormStartPosition.CenterScreen;

            LoadOrCreateModule();

            this.FormClosed += ModuleEditorForm_FormClosed;

            AddTabs();
            
            // Auto-login check (this will update publish button internally)
            TryAutoLogin();
            
            // NOTE: UpdatePublishButton() is now called by TryAutoLogin(), no need to call twice
        }

        // =========================
        // Initialization & Events
        // =========================

        /// <summary>
        /// Handles the form closed event to show the selector form again.
        /// </summary>
        private void ModuleEditorForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (selectorForm != null)
                selectorForm.Show();
        }

        // =========================
        // Module Loading & Saving
        // =========================

        /// <summary>
        /// Loads the module from disk or creates a new one if not found.
        /// </summary>
        private void LoadOrCreateModule()
        {
            string moduleFile = Path.Combine(currentPath, "module.json");
            if (File.Exists(moduleFile))
            {
                try
                {
                    string json = File.ReadAllText(moduleFile);
                    currentModule = System.Text.Json.JsonSerializer.Deserialize<Models.Module>(json);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        string.Format(Properties.Resources.ErrorLoadingModule, ex.Message),
                        Properties.Resources.Error,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    currentModule = new Models.Module();
                }
            }
            else
            {
                currentModule = new Models.Module();
            }
        }

        /// <summary>
        /// Saves all tabs by calling their Save method if available.
        /// </summary>
        private void buttonSave_Click(object sender, EventArgs e)
        {
            SaveAll();
            MessageBox.Show(Properties.Resources.ModuleSaved, Properties.Resources.Save, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void SaveAll()
        {
            foreach (TabPage tabPage in tabControlMain.TabPages)
            {
                if (tabPage.Controls.Count > 0)
                {
                    var control = tabPage.Controls[0];
                    var saveMethod = control.GetType().GetMethod("Save", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    if (saveMethod != null)
                    {
                        saveMethod.Invoke(control, null);
                    }
                }
            }
        }

        /// <summary>
        /// Closes the editor form without saving.
        /// </summary>
        private void buttonCancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        // =========================
        // Tab Management
        // =========================

        /// <summary>
        /// Adds all main tabs (Module, Pet, Battle, Item, Quest/Event) to the tab control.
        /// </summary>
        private void AddTabs()
        {
            tabControlMain.TabPages.Clear();

            var moduleTabControl = new ModuleTab();
            moduleTabControl.Dock = DockStyle.Fill;
            moduleTabControl.SetModulePath(currentPath);
            moduleTabControl.LoadFromModule(currentModule);

            var moduleTab = new TabPage(Properties.Resources.TabModule);
            moduleTab.Controls.Add(moduleTabControl);
            tabControlMain.TabPages.Add(moduleTab);

            var petControl = new PetTab();
            petControl.Dock = DockStyle.Fill;
            petControl.SetModule(currentPath, currentModule);

            var battleTabControl = new BattleTab();
            battleTabControl.Dock = DockStyle.Fill;
            battleTabControl.SetModule(currentPath, currentModule);

            // When the sprite format changes in the Module tab, update Pet and Battle tabs immediately
            // using the live values from the comboboxes (no disk save required)
            moduleTabControl.SpriteFormatChanged += (s, e) =>
            {
                currentModule.PrimarySpriteFormat = moduleTabControl.CurrentPrimaryFormat;
                currentModule.SecondarySpriteFormat = moduleTabControl.CurrentSecondaryFormat;
                petControl.RefreshSpriteFormat(currentModule);
                battleTabControl.RefreshSpriteFormat(currentModule);
            };

            var petTab = new TabPage(Properties.Resources.TabPet);
            petTab.Controls.Add(petControl);
            tabControlMain.TabPages.Add(petTab);

            var battleTab = new TabPage(Properties.Resources.TabBattle);
            battleTab.Controls.Add(battleTabControl);
            tabControlMain.TabPages.Add(battleTab);

            var itemControl = new ItemTab();
            itemControl.Dock = DockStyle.Fill;
            itemControl.SetModule(currentPath, currentModule);

            var itemTab = new TabPage(Properties.Resources.TabItem);
            itemTab.Controls.Add(itemControl);
            tabControlMain.TabPages.Add(itemTab);

            var questEventControl = new QuestEventTab();
            questEventControl.Dock = DockStyle.Fill;
            questEventControl.SetModule(currentPath, currentModule);

            var questEventTab = new TabPage("Quests/Events");
            questEventTab.Controls.Add(questEventControl);
            tabControlMain.TabPages.Add(questEventTab);
        }

        private void buttonGenerateDoc_Click(object sender, EventArgs e)
        {
            try
            {
                HTMLGenerator.GenerateDocumentation(currentPath);
                MessageBox.Show("The module's documents were generated", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error while generating the documentation:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void buttonOpenDoc_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(currentPath))
                return;

            string docPath = Path.Combine(currentPath, "documentation", "index.html");
            if (File.Exists(docPath))
            {
                try
                {
                    System.Diagnostics.Process.Start(docPath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error opening documentation:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                MessageBox.Show("Documentation not found. Please generate it first.", "Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void buttonReport_Click(object sender, EventArgs e)
        {
            try
            {
                // Save current state first so report uses latest data
                SaveAll();

                // Reload module from disk to get the freshly saved state
                string moduleFile = Path.Combine(currentPath, "module.json");
                Models.Module freshModule = currentModule;
                if (File.Exists(moduleFile))
                {
                    try
                    {
                        string json = File.ReadAllText(moduleFile);
                        freshModule = System.Text.Json.JsonSerializer.Deserialize<Models.Module>(json);
                    }
                    catch { }
                }

                var generator = new ModuleReportGenerator(currentPath, freshModule);
                string report = generator.Generate();
                var form = new ReportForm(report);
                form.ShowDialog(this);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error generating report:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // =========================
        // Export Sprites
        // =========================

        private void buttonExport_Click(object sender, EventArgs e)
        {
            using (var folderDialog = new FolderBrowserDialog())
            {
                folderDialog.Description = "Select the folder where sprites will be exported";
                if (folderDialog.ShowDialog(this) != DialogResult.OK)
                    return;

                string destRoot = folderDialog.SelectedPath;

                // Determine module name for the output subfolder
                string moduleName = currentModule?.Name;
                if (string.IsNullOrWhiteSpace(moduleName))
                {
                    MessageBox.Show("The module must have a name before exporting.", "Invalid Module",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Sanitize name so it is safe as a folder name
                string safeName = string.Concat(moduleName.Split(Path.GetInvalidFileNameChars()));
                string exportFolder = Path.Combine(destRoot, safeName);

                try
                {
                    Directory.CreateDirectory(exportFolder);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error creating export folder:\n" + ex.Message, "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // ── Collect unique sprite names from pets and enemies ──────────────
                var uniqueNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // Pets (monster.json)
                var pets = PetUtils.LoadPetsFromJson(currentPath);
                foreach (var pet in pets)
                    if (!string.IsNullOrWhiteSpace(pet.Name))
                        uniqueNames.Add(pet.Name);

                // Enemies (battle.json)
                string battlePath = Path.Combine(currentPath, "battle.json");
                if (File.Exists(battlePath))
                {
                    try
                    {
                        string json = File.ReadAllText(battlePath);
                        using (var doc = JsonDocument.Parse(json))
                        {
                            if (doc.RootElement.TryGetProperty("enemies", out var enemiesEl))
                            {
                                var enemies = JsonSerializer.Deserialize<List<BattleEnemy>>(enemiesEl.GetRawText());
                                foreach (var enemy in enemies)
                                    if (!string.IsNullOrWhiteSpace(enemy.Name))
                                        uniqueNames.Add(enemy.Name);
                            }
                        }
                    }
                    catch { /* ignore parse errors – we'll just skip enemies */ }
                }

                if (uniqueNames.Count == 0)
                {
                    MessageBox.Show("No pets or enemies found to export.", "Nothing to Export",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // ── For each unique name, locate the sprite and copy/zip it ───────
                string nameFormat = currentModule?.NameFormat ?? SpriteUtils.DefaultNameFormat;
                string primary    = currentModule?.PrimarySpriteFormat   ?? "Color";
                string secondary  = currentModule?.SecondarySpriteFormat ?? "HD";

                int copied = 0;
                var missingNames = new List<string>();

                foreach (var name in uniqueNames)
                {
                    var result = SpriteUtils.FindSpriteLocation(name, currentPath, nameFormat, primary, secondary);

                    if (string.IsNullOrEmpty(result.LoadedPath))
                    {
                        missingNames.Add(name);
                        continue;
                    }

                    string sourcePath  = result.LoadedPath;
                    string folder      = SpriteUtils.GetFolderForFormat(result.LoadedFormat);
                    string spriteName  = SpriteUtils.GetSpriteName(name, nameFormat);
                    string targetDir   = Path.Combine(exportFolder, folder);
                    Directory.CreateDirectory(targetDir);
                    string targetZip   = Path.Combine(targetDir, spriteName + ".zip");

                    try
                    {
                        if (sourcePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) && File.Exists(sourcePath))
                        {
                            // Already a zip – copy it directly
                            File.Copy(sourcePath, targetZip, overwrite: true);
                            copied++;
                        }
                        else if (Directory.Exists(sourcePath))
                        {
                            // Directory – pack it into a zip
                            if (File.Exists(targetZip))
                                File.Delete(targetZip);
                            ZipFile.CreateFromDirectory(sourcePath, targetZip);
                            copied++;
                        }
                        else
                        {
                            missingNames.Add(name);
                        }
                    }
                    catch
                    {
                        missingNames.Add(name);
                    }
                }

                // ── Report result ─────────────────────────────────────────────────
                string msg = $"Export complete.\n\nSprites copied: {copied}\nDestination: {exportFolder}";
                if (missingNames.Count > 0)
                    msg += $"\n\nNot found ({missingNames.Count}):\n" + string.Join(", ", missingNames);

                MessageBox.Show(msg, "Export",
                    MessageBoxButtons.OK,
                    missingNames.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
            }
        }

        // =========================
        // OmniNet Integration
        // =========================

        /// <summary>
        /// Attempts to auto-login using saved session data.
        /// </summary>
        private async void TryAutoLogin()
        {
            var config = OmniNetConfig.Instance;
            
            System.Diagnostics.Debug.WriteLine("[ModuleEditorForm] TryAutoLogin called");
            System.Diagnostics.Debug.WriteLine($"[ModuleEditorForm] IsLoggedIn: {config.IsLoggedIn}");
            System.Diagnostics.Debug.WriteLine($"[ModuleEditorForm] SecretKey exists: {!string.IsNullOrEmpty(config.SecretKey)}");
            System.Diagnostics.Debug.WriteLine($"[ModuleEditorForm] DeviceId: {config.DeviceId}");
            System.Diagnostics.Debug.WriteLine($"[ModuleEditorForm] Nickname: {config.Nickname}");
            
            if (!config.IsLoggedIn)
            {
                System.Diagnostics.Debug.WriteLine("[ModuleEditorForm] Not logged in, skipping auto-login");
                return;
            }

            try
            {
                using (var client = new OmniNetApiClient())
                {
                    System.Diagnostics.Debug.WriteLine($"[ModuleEditorForm] Validating session with SecretKey");
                    var response = await client.ValidateSecretKeyAsync(config.SecretKey);
                    
                    // Only clear session if authentication explicitly failed (not for network errors)
                    if (!response.Success)
                    {
                        // Check if it's an authentication error (401, 403, invalid credentials)
                        // vs a network/server error (timeout, 500, etc)
                        if (response.Data == null && !string.IsNullOrEmpty(response.ErrorMessage))
                        {
                            // If we got an error message but no data, it might be an auth error
                            var errorLower = response.ErrorMessage.ToLower();
                            if (errorLower.Contains("unauthorized") || 
                                errorLower.Contains("invalid") || 
                                errorLower.Contains("expired") ||
                                errorLower.Contains("forbidden"))
                            {
                                System.Diagnostics.Debug.WriteLine("[ModuleEditorForm] Authentication failed - clearing session");
                                config.ClearSession();
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine("[ModuleEditorForm] Network/server error - preserving session");
                            }
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("[ModuleEditorForm] Unknown error - preserving session for retry");
                        }
                    }
                    else if (response.Data == null)
                    {
                        // Success but no data - this is unusual, preserve session
                        System.Diagnostics.Debug.WriteLine("[ModuleEditorForm] Success but no data - preserving session");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("[ModuleEditorForm] Session validation succeeded");
                        // Update nickname/email if provided in response
                        if (!string.IsNullOrEmpty(response.Data.nickname))
                            config.Nickname = response.Data.nickname;
                        if (!string.IsNullOrEmpty(response.Data.email))
                            config.UserEmail = response.Data.email;
                        config.SaveSession();
                    }
                }
            }
            catch (System.Net.Http.HttpRequestException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ModuleEditorForm] Network error during auto-login: {ex.Message}");
                // Network error - preserve session for retry later
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ModuleEditorForm] Auto-login exception: {ex.Message}");
                // Unknown error - preserve session unless it's clearly an auth issue
            }

            UpdatePublishButton();
        }

        /// <summary>
        /// Updates the publish button text based on module status.
        /// </summary>
        private async void UpdatePublishButton()
        {
            var config = OmniNetConfig.Instance;
            
            if (!config.IsLoggedIn)
            {
                buttonPublish.Text = "Publish";
                buttonPublish.Enabled = true;
                return;
            }

            try
            {
                using (var client = new OmniNetApiClient())
                {
                    var response = await client.GetModuleStatusAsync(currentModule?.Name ?? "", config.SecretKey);
                    
                    if (response.Success && response.Data.success)
                    {
                        switch (response.Data.status)
                        {
                            case "published":
                                buttonPublish.Text = "Unpublish";
                                break;
                            case "unpublished":
                            case "not_found":
                            default:
                                buttonPublish.Text = "Publish";
                                break;
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[ModuleEditorForm] GetModuleStatus failed: {response.ErrorMessage}");
                        buttonPublish.Text = "Publish";
                    }
                }
            }
            catch (System.Net.Http.HttpRequestException ex)
            {
                // Network errors - likely OmniNet is offline
                System.Diagnostics.Debug.WriteLine($"[ModuleEditorForm] Network error checking module status: {ex.Message}");
                buttonPublish.Text = "Publish";
            }
            catch (Exception ex)
            {
                // Log unexpected errors but don't crash the UI
                System.Diagnostics.Debug.WriteLine($"[ModuleEditorForm] Unexpected error in UpdatePublishButton: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[ModuleEditorForm] Stack trace: {ex.StackTrace}");
                buttonPublish.Text = "Publish";
            }
        }

        /// <summary>
        /// Opens the account management dialog.
        /// </summary>
        private void buttonAccount_Click(object sender, EventArgs e)
        {
            var config = OmniNetConfig.Instance;
            var wasLoggedInBefore = config.IsLoggedIn;
            
            System.Diagnostics.Debug.WriteLine($"[ModuleEditorForm] Opening AccountForm - Currently logged in: {wasLoggedInBefore}");
            
            using (var form = new AccountForm())
            {
                var result = form.ShowDialog(this);
                
                // Check current state after dialog closes
                var isLoggedInAfter = config.IsLoggedIn;
                System.Diagnostics.Debug.WriteLine($"[ModuleEditorForm] AccountForm closed with result: {result}");
                System.Diagnostics.Debug.WriteLine($"[ModuleEditorForm] Currently logged in: {isLoggedInAfter}");
                System.Diagnostics.Debug.WriteLine($"[ModuleEditorForm] SecretKey exists: {!string.IsNullOrEmpty(config.SecretKey)}");
                System.Diagnostics.Debug.WriteLine($"[ModuleEditorForm] Nickname: {config.Nickname}");
                
                // Only update if login state changed to avoid unnecessary network calls
                if (wasLoggedInBefore != isLoggedInAfter)
                {
                    System.Diagnostics.Debug.WriteLine($"[ModuleEditorForm] Login state changed, updating publish button");
                    UpdatePublishButton();
                }
                else if (isLoggedInAfter)
                {
                    // Still logged in, but might need to refresh button state
                    // (e.g., if user toggled between published/unpublished in another session)
                    System.Diagnostics.Debug.WriteLine($"[ModuleEditorForm] Still logged in, refreshing publish button state");
                    UpdatePublishButton();
                }
            }
        }

        /// <summary>
        /// Handles publish/unpublish button click.
        /// </summary>
        private async void buttonPublish_Click(object sender, EventArgs e)
        {
            var config = OmniNetConfig.Instance;
            
            if (!config.IsLoggedIn)
            {
                MessageBox.Show("You must be logged in to publish modules.\n\nClick the Account button to login or create an account.",
                    "Login Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // First, save the module
            buttonSave_Click(sender, e);
            
            // Reload module data
            LoadOrCreateModule();

            if (string.IsNullOrWhiteSpace(currentModule?.Name))
            {
                MessageBox.Show("The module must have a name before publishing.", "Invalid Module", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (buttonPublish.Text == "Unpublish")
            {
                var confirmResult = MessageBox.Show(
                    "Are you sure you want to unpublish this module?\n\nIt will no longer be available for download.",
                    "Confirm Unpublish",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (confirmResult != DialogResult.Yes)
                    return;

                try
                {
                    using (var client = new OmniNetApiClient())
                    {
                        var response = await client.UnpublishModuleAsync(currentModule.Name, config.SecretKey);
                        
                        if (response.Success && response.Data.success)
                        {
                            MessageBox.Show("Module unpublished successfully.", "Success",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        else
                        {
                            MessageBox.Show(response.ErrorMessage ?? response.Data?.message ?? "Failed to unpublish module.",
                                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                using (var form = new PublishModuleForm(currentPath, currentModule.Name, currentModule.Version))
                {
                    form.ShowDialog(this);
                }
            }

            UpdatePublishButton();
        }
    }
}
