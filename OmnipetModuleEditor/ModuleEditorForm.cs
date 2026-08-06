using OmnipetModuleEditor.controls;
using OmnipetModuleEditor.DigimonSync;
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

        // Tab references, kept so menu items can trigger tab-specific actions.
        private PetTab petControl;
        private BattleTab battleTabControl;
        private CollectionTab collectionTabControl;

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

            // Validate any saved Omninet session in the background.
            TryAutoLogin();
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

        /// <summary>
        /// Routes Ctrl+C / Ctrl+V to the active tab's list/grid so the selected
        /// entry can be duplicated. Text-entry controls keep their normal copy
        /// and paste behaviour (guarded by <see cref="ClipboardListUtils.IsTextEntryFocused"/>).
        /// </summary>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            bool isCopy = keyData == (Keys.Control | Keys.C);
            bool isPaste = keyData == (Keys.Control | Keys.V);
            if ((isCopy || isPaste) && !ClipboardListUtils.IsTextEntryFocused())
            {
                var page = tabControlMain.SelectedTab;
                if (page != null && page.Controls.Count > 0
                    && page.Controls[0] is IListClipboardTarget target)
                {
                    if (isCopy) target.CopySelection();
                    else target.PasteClipboard();
                    return true;
                }
            }
            return base.ProcessCmdKey(ref msg, keyData);
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

            var deviceControl = new DeviceTab();
            deviceControl.Dock = DockStyle.Fill;
            deviceControl.SetModule(currentPath, currentModule);

            var deviceTab = new TabPage("Device");
            deviceTab.Controls.Add(deviceControl);
            tabControlMain.TabPages.Add(deviceTab);

            petControl = new PetTab();
            petControl.Dock = DockStyle.Fill;
            petControl.SetModule(currentPath, currentModule);

            battleTabControl = new BattleTab();
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

            collectionTabControl = new CollectionTab();
            collectionTabControl.Dock = DockStyle.Fill;
            collectionTabControl.SetModule(currentPath, currentModule);

            var collectionTab = new TabPage("Collection");
            collectionTab.Controls.Add(collectionTabControl);
            tabControlMain.TabPages.Add(collectionTab);

            var questEventControl = new QuestEventTab();
            questEventControl.Dock = DockStyle.Fill;
            questEventControl.SetModule(currentPath, currentModule);

            var questEventTab = new TabPage("Quests/Events");
            questEventTab.Controls.Add(questEventControl);
            tabControlMain.TabPages.Add(questEventTab);

            var passwordControl = new PasswordTab();
            passwordControl.Dock = DockStyle.Fill;
            passwordControl.SetModule(currentPath, currentModule);

            var passwordTab = new TabPage("Passwords");
            passwordTab.Controls.Add(passwordControl);
            tabControlMain.TabPages.Add(passwordTab);
        }

        /// <summary>Tools ▸ Import Collection from Module — merges another
        /// module's cards/effects/packs into this one (matched by uuid).</summary>
        private void importCollection_Click(object sender, EventArgs e)
        {
            collectionTabControl?.ImportFromModuleFlow();
        }

        private void buttonGenerateDoc_Click(object sender, EventArgs e)
        {
            try
            {
                HTMLGenerator.GenerateDocumentation(currentPath);
                OmnipetModuleEditor.docgenerators.DeviceGenerator.GenerateDevicesPage(
                    Path.Combine(currentPath, "documentation"), currentPath);
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
        }

        /// <summary>
        /// Opens the account management dialog (Omninet ▸ Account).
        /// </summary>
        private void buttonAccount_Click(object sender, EventArgs e)
        {
            using (var form = new AccountForm())
            {
                form.ShowDialog(this);
            }
        }

        /// <summary>
        /// Opens the module management dialog (Omninet ▸ Manage Module).
        /// The dialog handles publishing, updating, unpublishing and
        /// contributor management based on the module's current status.
        /// </summary>
        private void manageModule_Click(object sender, EventArgs e)
        {
            var config = OmniNetConfig.Instance;

            if (!config.IsLoggedIn)
            {
                MessageBox.Show(
                    "You must be logged in to manage modules.\n\nOpen Omninet ▸ Account to login or create an account.",
                    "Login Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Persist the latest edits and reload so the dialog sees current data.
            SaveAll();
            LoadOrCreateModule();

            if (string.IsNullOrWhiteSpace(currentModule?.Name))
            {
                MessageBox.Show("The module must have a name before publishing.", "Invalid Module",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (var form = new PublishModuleForm(currentPath, currentModule.Name, currentModule.Version))
            {
                form.ShowDialog(this);
            }
        }

        /// <summary>
        /// Opens the module browser (Omninet ▸ Manage Modules): all modules
        /// published on Omninet vs what is installed in the game's modules
        /// folder, with per-row download. Works without being logged in —
        /// login only enriches the Ownership column.
        /// </summary>
        private void manageModules_Click(object sender, EventArgs e)
        {
            string modulesRoot = Directory.GetParent(currentPath)?.FullName;
            if (string.IsNullOrEmpty(modulesRoot) || !Directory.Exists(modulesRoot))
            {
                MessageBox.Show("Could not resolve the game's modules folder.",
                    "Manage Modules", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (var form = new ModuleBrowserForm(modulesRoot, Path.GetFileName(currentPath)))
            {
                form.ShowDialog(this);
            }
        }

        // =========================
        // Edit / Tools menu actions
        // =========================

        /// <summary>Edit ▸ Edit Evolutions — same as the Pet tab button.</summary>
        private void editEvolutions_Click(object sender, EventArgs e) => petControl?.OpenEvolutionsEditor();

        /// <summary>Edit ▸ Enemy Editor — same as the Battle tab "Fast Editor" button.</summary>
        private void enemyEditor_Click(object sender, EventArgs e) => battleTabControl?.OpenEnemyEditor();

        /// <summary>Edit ▸ Special Encounters — same as the Battle tab button.</summary>
        private void specialEncounters_Click(object sender, EventArgs e) => battleTabControl?.OpenSpecialEncounters();

        /// <summary>Tools ▸ Update ATK Sprites — moved here from the Battle tab.</summary>
        private void updateAtkSprites_Click(object sender, EventArgs e) => battleTabControl?.UpdateAtkSprites();

        /// <summary>
        /// Tools ▸ Generate Pet Index — assigns the Index field for all pets in the
        /// module based on the current pet-list ordering (eggs = -1, otherwise 0-based
        /// per version).
        /// </summary>
        private void generatePetIndex_Click(object sender, EventArgs e)
        {
            if (petControl == null)
                return;

            petControl.GenerateIndexes();
            MessageBox.Show("Pet indexes were generated.", "Generate Pet Index",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // =========================
        // Digimon Database menu
        // =========================

        private async void digimonRecordMatch_Click(object sender, EventArgs e)
            => await RunDigimonOperation("Record Match",
                svc => (svc.BuildRecordMatchReport(), null, (Func<string>)null));

        private async void digimonValidateNames_Click(object sender, EventArgs e)
            => await RunDigimonOperation("Validate Digimon Names",
                svc => (svc.BuildValidateReport(), null, (Func<string>)null));

        private async void digimonNormalizeNames_Click(object sender, EventArgs e)
            => await RunDigimonOperation("Normalize Digimon Names",
                svc => { var r = svc.BuildNormalize(); return (r.Report, "Apply", r.Apply); });

        private async void digimonImportMinWeight_Click(object sender, EventArgs e)
            => await RunDigimonOperation("Import Min Weight",
                svc => { var r = svc.BuildImportMinWeight(); return (r.Report, "Apply", r.Apply); });

        /// <summary>
        /// Digimon Database ▸ Update Local Sprite Database. Refreshes the global
        /// assets sprite library against the database for this module's names.
        /// </summary>
        private async void digimonUpdateSprites_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(currentPath)
                || !File.Exists(Path.Combine(currentPath, "monster.json")))
            {
                MessageBox.Show("This module has no monster.json to work with.",
                    "Update Local Sprite Database", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SaveAll();

            System.Collections.Generic.List<DigimonRecord> records;
            this.UseWaitCursor = true;
            try
            {
                records = await DigimonDbClient.GetAllAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not reach the Digimon Database:\n" + ex.Message,
                    "Update Local Sprite Database", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            finally
            {
                this.UseWaitCursor = false;
            }

            string customPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "digimondb", "custom_matches.json");
            var db = new DigimonDb(records, customPath);
            var updater = new LocalSpriteUpdater(currentPath, currentModule?.NameFormat,
                currentModule?.PrimarySpriteFormat, currentModule?.SecondarySpriteFormat, db);

            using (var form = new SpriteUpdateForm(p => updater.RunAsync(p)))
                form.ShowDialog(this);
        }

        /// <summary>
        /// Shared driver for the Digimon Database menu actions: flush edits,
        /// fetch the catalogue from the live API (cached), run the requested
        /// operation, show its report, and reload tabs for apply operations.
        /// </summary>
        private async Task RunDigimonOperation(string title,
            Func<ModuleSyncService, (string Report, string ApplyText, Func<string> Apply)> op)
        {
            if (string.IsNullOrEmpty(currentPath)
                || !File.Exists(Path.Combine(currentPath, "monster.json")))
            {
                MessageBox.Show("This module has no monster.json to work with.", title,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Flush any unsaved editor changes so the sync sees current data.
            SaveAll();

            System.Collections.Generic.List<DigimonRecord> records;
            this.UseWaitCursor = true;
            try
            {
                records = await DigimonDbClient.GetAllAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not reach the Digimon Database:\n" + ex.Message,
                    title, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            finally
            {
                this.UseWaitCursor = false;
            }

            string customPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "digimondb", "custom_matches.json");
            var db = new DigimonDb(records, customPath);
            var svc = new ModuleSyncService(currentPath, currentModule?.NameFormat, db);

            var result = op(svc);
            using (var form = new DigimonReportForm(title, result.Report, result.ApplyText, result.Apply))
            {
                var dr = form.ShowDialog(this);
                if (result.Apply != null && dr == DialogResult.OK)
                {
                    // Reload the module + tabs so the editor reflects the writes.
                    LoadOrCreateModule();
                    AddTabs();
                }
            }
        }
    }
}
