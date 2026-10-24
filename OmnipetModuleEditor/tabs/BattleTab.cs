using OmnipetModuleEditor.Controls;
using OmnipetModuleEditor.Models;
using OmnipetModuleEditor.Utils;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OmnipetModuleEditor.Tabs
{
    /// <summary>
    /// Tab for managing and editing battle enemies in the module.
    /// </summary>
    public partial class BattleTab : UserControl, IListClipboardTarget
    {
        // Fields
        private List<BattleEnemy> enemies;
        private string modulePath;
        private Module module;
        private EnemyListPanel enemyListPanel;
        private EnemyEditPanel enemyEditPanel;
        private BattleEnemy copiedEnemy = null;
        private Button btnFastEditor;
        private Button btnSpecialEncounters;
        private PetSpritePanel spritePanel;
        private BattleEnemy selectedEnemy = null;
        private int lastSearchIndex = -1;
        private string lastSearchText = "";
        private CancellationTokenSource _spriteLoadCts;

        /// <summary>
        /// Initializes a new instance of the <see cref="BattleTab"/> class.
        /// </summary>
        public BattleTab()
        {
            InitializeComponent();
            enemyListPanel.BtnRemove.Click += BtnRemove_Click;
            enemyListPanel.BtnCopy.Click += BtnCopy_Click;
            enemyListPanel.BtnPaste.Click += BtnPaste_Click;
            enemyListPanel.BtnAdd.Click += BtnAdd_Click;
            btnFastEditor.Click += BtnFastEditor_Click;
            btnSpecialEncounters.Click += BtnSpecialEncounters_Click;
            // Currencies may have been added on the Shop/Currencies tab since.
            this.VisibleChanged += (s, e) =>
            {
                if (Visible && !string.IsNullOrEmpty(modulePath))
                    enemyEditPanel.LoadCurrencies(CurrencyNames());
            };
            enemyListPanel.BtnGo.Click += (s, e) => SearchGo();
            enemyListPanel.BtnPrev.Click += (s, e) => SearchPrevNext(-1);
            enemyListPanel.BtnNext.Click += (s, e) => SearchPrevNext(1);
            enemyListPanel.TxtSearch.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { SearchGo(); e.SuppressKeyPress = true; } };
            enemyListPanel.VirtualList.ItemSelected += (s, enemy) => SelectEnemy(enemy);
        }

        #region Initialization

        /// <summary>
        /// Initializes the layout and child controls.
        /// </summary>
        private void InitializeComponent()
        {
            this.SuspendLayout();

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(8),
                BackColor = SystemColors.Control
            };
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 320));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70F));

            enemyListPanel = new EnemyListPanel();
            enemyListPanel.Width = 320;
            enemyListPanel.MinimumSize = new Size(320, 0);

            var rightPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0) };

            spritePanel = new PetSpritePanel
            {
                Dock = DockStyle.Top,
                Height = 150
            };

            enemyEditPanel = new EnemyEditPanel
            {
                Dock = DockStyle.Fill
            };

            btnFastEditor = new Button
            {
                Text = Properties.Resources.BattleTab_Button_FastEditor ?? "Fast Editor",
                Width = 140,
                Height = 32,
                Margin = new Padding(8, 16, 8, 8),
                Anchor = AnchorStyles.Right
            };
            btnSpecialEncounters = new Button
            {
                Text = "Special Encounters",
                Width = 160,
                Height = 32,
                Margin = new Padding(8, 16, 8, 8),
                Anchor = AnchorStyles.Right
            };
            var bottomPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true
            };
            bottomPanel.Controls.Add(btnFastEditor);
            bottomPanel.Controls.Add(btnSpecialEncounters);

            rightPanel.Controls.Add(enemyEditPanel);
            rightPanel.Controls.Add(spritePanel);
            rightPanel.Controls.Add(bottomPanel);

            mainLayout.Controls.Add(enemyListPanel, 0, 0);
            mainLayout.Controls.Add(rightPanel, 1, 0);

            this.Controls.Add(mainLayout);
            this.Name = "BattleTab";
            this.Size = new Size(800, 560);
            this.ResumeLayout(false);
        }

        #endregion

        #region Public API

        /// <summary>The Shop/Currencies tab's currency names, saved or not.
        /// Falls back to shop.json on disk.</summary>
        public Func<IEnumerable<string>> CurrencyNamesProvider { get; set; }

        private IEnumerable<string> CurrencyNames()
        {
            if (CurrencyNamesProvider != null)
                return CurrencyNamesProvider();
            string path = Path.Combine(modulePath ?? "", "shop.json");
            if (!File.Exists(path))
                return Enumerable.Empty<string>();
            try
            {
                using (var doc = JsonDocument.Parse(File.ReadAllText(path)))
                {
                    if (!doc.RootElement.TryGetProperty("currency", out var list))
                        return Enumerable.Empty<string>();
                    return list.EnumerateArray()
                        .Select(c => c.TryGetProperty("name", out var n) ? n.GetString() : null)
                        .ToList();
                }
            }
            catch
            {
                return Enumerable.Empty<string>();
            }
        }

        /// <summary>
        /// Sets the module path and loads enemies and related data.
        /// </summary>
        public void SetModule(string modulePath, Module module)
        {
            this.modulePath = modulePath;
            this.module = module;
            LoadEnemiesFromJson();
            enemyEditPanel.LoadAtkSprites(modulePath, module?.PrimarySpriteFormat);
            enemyEditPanel.PopulateAtkCombos();
            enemyEditPanel.LoadItems(modulePath);
            enemyEditPanel.LoadCardPacks(modulePath);
            enemyEditPanel.LoadCurrencies(CurrencyNames());
        }

        /// <summary>
        /// Updates the module sprite format and refreshes the enemy list thumbnails
        /// and the currently selected enemy's sprite panel.
        /// </summary>
        public void RefreshSpriteFormat(Module updatedModule)
        {
            this.module = updatedModule;
            enemyEditPanel.LoadAtkSprites(modulePath, updatedModule?.PrimarySpriteFormat);
            enemyEditPanel.PopulateAtkCombos();
            PopulateEnemyPanel();
            if (selectedEnemy != null)
            {
                SelectEnemy(selectedEnemy);
                spritePanel.CurrentModule = updatedModule;
                spritePanel.RefreshSprites();
            }
        }

        /// <summary>
        /// Saves the enemies to battle.json.
        /// </summary>
        public void Save()
        {
            if (string.IsNullOrEmpty(modulePath) || enemies == null)
                return;

            string battlePath = Path.Combine(modulePath, "battle.json");
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
            var obj = new { enemies = enemies };
            try
            {
                string json = JsonSerializer.Serialize(obj, options);
                File.WriteAllText(battlePath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    string.Format(Properties.Resources.BattleTab_ErrorSaving ?? "Error saving battle.json: {0}", ex.Message),
                    Properties.Resources.Error ?? "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        #endregion

        #region Data Loading

        /// <summary>
        /// Loads enemies from battle.json.
        /// </summary>
        private void LoadEnemiesFromJson()
        {
            string battlePath = Path.Combine(this.modulePath, "battle.json");
            enemies = new List<BattleEnemy>();

            if (File.Exists(battlePath))
            {
                try
                {
                    string json = File.ReadAllText(battlePath);
                    using (JsonDocument doc = JsonDocument.Parse(json))
                    {
                        var root = doc.RootElement;
                        if (root.TryGetProperty("enemies", out var enemiesElement))
                            enemies = JsonSerializer.Deserialize<List<BattleEnemy>>(enemiesElement.GetRawText());
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        string.Format(Properties.Resources.BattleTab_ErrorLoading ?? "Error loading battle.json: {0}", ex.Message),
                        Properties.Resources.Error ?? "Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }

            if (enemies != null)
                PopulateEnemyPanel();
        }

        #endregion

        #region UI Population

        /// <summary>
        /// Populates the enemy list panel with enemy panels.
        /// </summary>
        private void PopulateEnemyPanel()
        {
            enemyListPanel.VirtualList.SetItems(enemies);
            StartAsyncSpriteLoad();
        }

        private void PopulateEnemyPanelAndReturnPanel(BattleEnemy enemyToSelect)
        {
            enemyListPanel.VirtualList.SetItems(enemies);
            StartAsyncSpriteLoad();
            if (enemyToSelect != null)
                SelectEnemy(enemyToSelect);
        }

        /// <summary>
        /// Loads sprites into the virtual list asynchronously, one at a time,
        /// so the UI stays responsive. Cancels any prior load.
        /// </summary>
        private void StartAsyncSpriteLoad()
        {
            _spriteLoadCts?.Cancel();
            _spriteLoadCts?.Dispose();
            _spriteLoadCts = new CancellationTokenSource();
            var cts = _spriteLoadCts;

            var snapshot  = (enemies ?? new List<BattleEnemy>()).ToList();
            string primary   = module?.PrimarySpriteFormat   ?? "Color";
            string secondary = module?.SecondarySpriteFormat ?? "HD";
            string nameFmt   = module?.NameFormat            ?? SpriteUtils.DefaultNameFormat;
            string path      = modulePath;
            var virtualList  = enemyListPanel.VirtualList;

            // Ensure the control handle exists before the background thread tries to
            // BeginInvoke on it. Accessing .Handle forces creation on the UI thread.
            var _ = virtualList.Handle;

            Task.Run(() =>
            {
                foreach (var enemy in snapshot)
                {
                    if (cts.IsCancellationRequested) break;
                    if (string.IsNullOrEmpty(enemy?.Name)) continue;

                    Image sprite = null;
                    try { sprite = SpriteUtils.LoadSingleSprite(enemy.Name, path, nameFmt, primary, secondary); }
                    catch (Exception ex)
                    { System.Diagnostics.Debug.WriteLine($"[BattleTab] Sprite '{enemy.Name}': {ex.Message}"); }

                    if (cts.IsCancellationRequested) { sprite?.Dispose(); break; }

                    var name = enemy.Name;
                    var img  = sprite;
                    try
                    {
                        virtualList.BeginInvoke(new Action(() =>
                        {
                            if (!cts.IsCancellationRequested)
                                virtualList.SetSprite(name, img);
                            else
                                img?.Dispose();
                        }));
                    }
                    catch (InvalidOperationException) { img?.Dispose(); continue; }
                }
                System.Diagnostics.Debug.WriteLine($"[BattleTab] Sprite load done (cancelled={cts.IsCancellationRequested})");
            }, cts.Token);
        }

        #endregion

        #region Selection

        private void SelectEnemy(BattleEnemy enemy)
        {
            if (enemy == null) return;
            selectedEnemy = enemy;
            enemyListPanel.VirtualList.SelectItem(enemy);
            enemyEditPanel.LoadEnemy(enemy);
            spritePanel.CurrentPet = new Pet
            {
                Name      = enemy.Name,
                Stage     = enemy.Stage,
                Version   = enemy.Version,
                AtkMain   = enemy.AtkMain,
                AtkAlt    = enemy.AtkAlt,
                Attribute = enemy.Attribute,
                Power     = enemy.Power,
                Hp        = enemy.Hp
            };
            spritePanel.CurrentModule = module;
            spritePanel.ModulePath    = modulePath;
            spritePanel.RefreshSprites();
        }

        #endregion

        #region Utility

        /// <summary>
        /// Returns the color for the given attribute.
        /// </summary>
        private Color GetAttributeColor(string attr)
        {
            switch (attr)
            {
                case "Da": return Color.FromArgb(66, 165, 245);      // Data
                case "Va": return Color.FromArgb(102, 187, 106);     // Vaccine
                case "Vi": return Color.FromArgb(237, 83, 80);       // Virus
                case "": return Color.FromArgb(171, 71, 188);        // Free
                default: return Color.FromArgb(171, 71, 188);        // Free (fallback)
            }
        }

        /// <summary>
        /// Utility function to clone a BattleEnemy.
        /// </summary>
        private BattleEnemy CloneEnemy(BattleEnemy enemy)
        {
            return new BattleEnemy
            {
                Name = enemy.Name,
                Power = enemy.Power,
                Stage = enemy.Stage,
                Attribute = enemy.Attribute,
                Hp = enemy.Hp,
                Area = enemy.Area,
                Round = enemy.Round,
                Version = enemy.Version,
                Handicap = enemy.Handicap,
                Prize = enemy.Prize,
                Unlock = enemy.Unlock,
                AtkMain = enemy.AtkMain,
                AtkAlt = enemy.AtkAlt,
                AtkAlt2 = enemy.AtkAlt2,
                SpecialEncounter = enemy.SpecialEncounter,
                Friend = enemy.Friend,
                Currency = enemy.Currency,
                CurrencyCost = enemy.CurrencyCost,
                CurrencyReward = enemy.CurrencyReward,
                PrizeCardPack = enemy.PrizeCardPack,
                PrizeChance = enemy.PrizeChance,
                AttackPattern = enemy.AttackPattern != null ? (int[])enemy.AttackPattern.Clone() : null
            };
        }

        /// <summary>
        /// Sorts the enemies list by version, then area, then round.
        /// </summary>
        private void SortEnemies()
        {
            if (enemies == null) return;
            // Special encounters before normal battles. A STABLE sort: enemies
            // sharing a version, area and round keep their order, and an
            // adventure round fights its first one (game_module.get_enemies).
            var sorted = enemies
                .OrderByDescending(e => e.SpecialEncounter)
                .ThenBy(e => e.Version)
                .ThenBy(e => e.Area)
                .ThenBy(e => e.Round)
                .ToList();
            enemies.Clear();
            enemies.AddRange(sorted);
        }

        #endregion

        #region Button Event Handlers

        private void BtnFastEditor_Click(object sender, EventArgs e)
        {
            var fastEditor = new EnemyFastEditorForm();
            fastEditor.SetModulePath(this.modulePath);
            fastEditor.SetEnemies(enemies.Where(en => !en.SpecialEncounter).ToList());

            fastEditor.EnemiesSaved += (normalEnemies) =>
            {
                var specials = enemies.Where(en => en.SpecialEncounter).ToList();
                enemies = specials.Concat(normalEnemies).ToList();
                SortEnemies();
                PopulateEnemyPanel();
                Save();
            };

            fastEditor.ShowDialog();
        }

        private void BtnSpecialEncounters_Click(object sender, EventArgs e)
        {
            var fastEditor = new EnemyFastEditorForm();
            fastEditor.SetSpecialEncounterMode(true);
            fastEditor.SetModulePath(this.modulePath);
            fastEditor.SetEnemies(enemies.Where(en => en.SpecialEncounter).ToList());

            fastEditor.EnemiesSaved += (specialEnemies) =>
            {
                var normals = enemies.Where(en => !en.SpecialEncounter).ToList();
                enemies = normals.Concat(specialEnemies).ToList();
                SortEnemies();
                PopulateEnemyPanel();
                Save();
            };

            fastEditor.ShowDialog();
        }

        /// <summary>Edit menu: open the enemy (fast) editor for normal battlers.</summary>
        public void OpenEnemyEditor() => BtnFastEditor_Click(this, EventArgs.Empty);

        /// <summary>Edit menu: open the special-encounters editor.</summary>
        public void OpenSpecialEncounters() => BtnSpecialEncounters_Click(this, EventArgs.Empty);

        /// <summary>Tools menu: copy each enemy's attack sprites from the matching pet.</summary>
        public void UpdateAtkSprites() => BtnUpdateAtkSprites_Click(this, EventArgs.Empty);

        private void BtnUpdateAtkSprites_Click(object sender, EventArgs e)
        {
            if (enemies == null || enemies.Count == 0)
            {
                MessageBox.Show("No enemies to update.", "Update Attack Sprites", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Load pets from monster.json
            List<Pet> pets;
            try
            {
                pets = PetUtils.LoadPetsFromJson(modulePath);
            }
            catch
            {
                MessageBox.Show("Could not load monster.json.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (pets == null || pets.Count == 0)
            {
                MessageBox.Show("No pets found in monster.json.", "Update Attack Sprites", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Build a lookup by name for fast matching
            var petLookup = new Dictionary<string, Pet>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in pets)
            {
                if (!string.IsNullOrEmpty(p.Name) && !petLookup.ContainsKey(p.Name))
                    petLookup[p.Name] = p;
            }

            // Progress dialog
            var progressForm = new Form
            {
                Text = "Update Attack Sprites",
                Size = new Size(400, 130),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MinimizeBox = false,
                MaximizeBox = false,
                ControlBox = false
            };
            var lblStatus = new Label
            {
                Text = "Updating...",
                Location = new Point(12, 12),
                AutoSize = true
            };
            var progressBar = new ProgressBar
            {
                Location = new Point(12, 40),
                Size = new Size(360, 28),
                Minimum = 0,
                Maximum = enemies.Count,
                Value = 0
            };
            progressForm.Controls.Add(lblStatus);
            progressForm.Controls.Add(progressBar);

            int updatedCount = 0;

            progressForm.Shown += (s2, e2) =>
            {
                Application.DoEvents();
                for (int i = 0; i < enemies.Count; i++)
                {
                    var enemy = enemies[i];
                    if (!string.IsNullOrEmpty(enemy.Name) && petLookup.TryGetValue(enemy.Name, out var matchedPet))
                    {
                        enemy.AtkMain = matchedPet.AtkMain;
                        enemy.AtkAlt = matchedPet.AtkAlt;
                        enemy.AtkAlt2 = matchedPet.AtkAlt2;
                        updatedCount++;
                    }
                    progressBar.Value = i + 1;
                    lblStatus.Text = $"Processing {i + 1} of {enemies.Count}...";
                    Application.DoEvents();
                }
                progressForm.Close();
            };

            progressForm.ShowDialog(this);

            if (updatedCount > 0)
            {
                Save();
                if (selectedEnemy != null)
                    enemyEditPanel.LoadEnemy(selectedEnemy);
            }

            MessageBox.Show($"Updated {updatedCount} of {enemies.Count} enemies.", "Update Attack Sprites", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void SearchGo()
        {
            string query = enemyListPanel.TxtSearch.Text;
            if (string.IsNullOrWhiteSpace(query)) return;
            lastSearchText = query;
            lastSearchIndex = -1;
            SearchPrevNext(1);
        }

        private void SearchPrevNext(int direction)
        {
            string query = enemyListPanel.TxtSearch.Text;
            if (string.IsNullOrWhiteSpace(query)) return;

            int count = enemies?.Count ?? 0;
            if (count == 0) return;

            if (!string.Equals(query, lastSearchText, StringComparison.OrdinalIgnoreCase))
            {
                lastSearchText = query;
                lastSearchIndex = -1;
            }

            int start = lastSearchIndex + direction;
            for (int i = 0; i < count; i++)
            {
                int idx = ((start + i * direction) % count + count) % count;
                var enemy = enemies[idx];
                if (enemy?.Name != null &&
                    enemy.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    lastSearchIndex = idx;
                    SelectEnemy(enemy);
                    return;
                }
            }
        }

        private void ScrollToPanel(Panel panel) { } // kept for compatibility; no longer needed

        private void BtnRemove_Click(object sender, EventArgs e)
        {
            if (selectedEnemy == null) return;
            var result = MessageBox.Show(
                string.Format(Properties.Resources.BattleTab_ConfirmRemove ?? "Do you want to remove the enemy \"{0}\"?", selectedEnemy.Name),
                Properties.Resources.Confirmation ?? "Confirmation",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            );
            if (result == DialogResult.Yes)
            {
                enemies.Remove(selectedEnemy);
                selectedEnemy = null;
                PopulateEnemyPanel();
                Save();
            }
        }

        private void BtnCopy_Click(object sender, EventArgs e)
        {
            if (selectedEnemy == null) return;
            copiedEnemy = CloneEnemy(selectedEnemy);
        }

        private void BtnPaste_Click(object sender, EventArgs e)
        {
            if (copiedEnemy == null) return;
            var newEnemy = CloneEnemy(copiedEnemy);
            enemies.Add(newEnemy);
            SortEnemies();
            PopulateEnemyPanel();
            Save();
            SelectEnemy(newEnemy);
        }

        /// <summary>Ctrl+C — copy the selected enemy into the paste buffer.</summary>
        public void CopySelection() => BtnCopy_Click(this, EventArgs.Empty);

        /// <summary>Ctrl+V — paste a duplicate of the copied enemy.</summary>
        public void PasteClipboard() => BtnPaste_Click(this, EventArgs.Empty);

        private void BtnAdd_Click(object sender, EventArgs e)
        {
            // Load pets from monster.json
            List<Pet> pets = new List<Pet>();
            try
            {
                string monsterPath = Path.Combine(modulePath, "monster.json");
                if (File.Exists(monsterPath))
                {
                    string json = File.ReadAllText(monsterPath);
                    using (JsonDocument doc = JsonDocument.Parse(json))
                    {
                        var root = doc.RootElement;
                        if (root.TryGetProperty("monster", out var monstersElement))
                        {
                            pets = JsonSerializer.Deserialize<List<Pet>>(monstersElement.GetRawText());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    string.Format(Properties.Resources.BattleTab_ErrorLoadingMonster ?? "Error loading monster.json: {0}", ex.Message),
                    Properties.Resources.Error ?? "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }

            using (var dlg = new EnemyAddDialog(pets))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    BattleEnemy newEnemy;
                    if (dlg.SelectedPet == null)
                    {
                        // Custom: create a default enemy
                        newEnemy = new BattleEnemy
                        {
                            Name = Properties.Resources.BattleTab_NewEnemyName ?? "New Enemy",
                            Stage = 0,
                            Version = dlg.SelectedVersion,
                            Power = 10,
                            Hp = 1,
                            Area = 1,
                            Round = 1,
                            Handicap = 0,
                            Prize = "",
                            Unlock = "",
                            AtkMain = 0,
                            AtkAlt = 0,
                            Attribute = ""
                        };
                    }
                    else
                    {
                        // Based on an existing pet
                        var pet = dlg.SelectedPet;
                        newEnemy = new BattleEnemy
                        {
                            Name = pet.Name,
                            Stage = pet.Stage,
                            Version = pet.Version,
                            Power = pet.Power,
                            Hp = pet.Hp,
                            Area = 1,
                            Round = 1,
                            Handicap = 0,
                            Prize = "",
                            Unlock = "",
                            AtkMain = pet.AtkMain,
                            AtkAlt = pet.AtkAlt,
                            Attribute = pet.Attribute
                        };
                    }
                    if (enemies == null)
                        enemies = new List<BattleEnemy>();
                    enemies.Add(newEnemy);
                    SortEnemies();
                    PopulateEnemyPanel();
                    Save();
                    SelectEnemy(newEnemy);
                }
            }
        }

        #endregion

        #region Internal Classes

        // Left panel (enemy list and buttons)
        private class EnemyListPanel : UserControl
        {
            public VirtualEnemyList VirtualList { get; private set; }
            // Kept as Panel for any legacy reference; same object as VirtualList
            public Panel PanelEnemyList => VirtualList;
            public Button BtnAdd { get; private set; }
            public Button BtnRemove { get; private set; }
            public Button BtnCopy { get; private set; }
            public Button BtnPaste { get; private set; }
            public TextBox TxtSearch { get; private set; }
            public Button BtnGo { get; private set; }
            public Button BtnPrev { get; private set; }
            public Button BtnNext { get; private set; }

            public EnemyListPanel() { InitializeComponent(); }

            private void InitializeComponent()
            {
                this.Dock = DockStyle.Fill;
                var leftLayout = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 1,
                    RowCount = 3,
                    BackColor = SystemColors.ControlLight
                };
                leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
                leftLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
                leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));

                var searchPanel = new FlowLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    FlowDirection = FlowDirection.LeftToRight,
                    Padding = new Padding(2, 2, 2, 0),
                    WrapContents = false
                };
                TxtSearch = new TextBox { Width = 130, Margin = new Padding(0, 2, 2, 0) };
                BtnGo   = new Button { Text = "Go", Width = 36, Height = 23, Margin = new Padding(0, 1, 2, 0) };
                BtnPrev = new Button { Text = "<",  Width = 28, Height = 23, Margin = new Padding(0, 1, 2, 0) };
                BtnNext = new Button { Text = ">",  Width = 28, Height = 23, Margin = new Padding(0, 1, 0, 0) };
                searchPanel.Controls.AddRange(new Control[] { TxtSearch, BtnGo, BtnPrev, BtnNext });
                leftLayout.Controls.Add(searchPanel, 0, 0);

                VirtualList = new VirtualEnemyList { Dock = DockStyle.Fill };
                leftLayout.Controls.Add(VirtualList, 0, 1);

                var panelButtons = new FlowLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    FlowDirection = FlowDirection.LeftToRight,
                    Padding = new Padding(4),
                    AutoSize = false,
                    WrapContents = false
                };
                BtnAdd    = new Button { Text = "Add",    Width = 70, Margin = new Padding(0, 0, 4, 0) };
                BtnRemove = new Button { Text = "Remove", Width = 70, Margin = new Padding(0, 0, 4, 0) };
                BtnCopy   = new Button { Text = "Copy",   Width = 70, Margin = new Padding(0, 0, 4, 0) };
                BtnPaste  = new Button { Text = "Paste",  Width = 70, Margin = new Padding(0, 0, 4, 0) };
                BtnAdd.Height = BtnRemove.Height = BtnCopy.Height = BtnPaste.Height = 36;
                panelButtons.Controls.AddRange(new Control[] { BtnAdd, BtnRemove, BtnCopy, BtnPaste });
                leftLayout.Controls.Add(panelButtons, 0, 2);

                this.Controls.Add(leftLayout);
            }
        }

        // Right panel (enemy editing)
        private class EnemyEditPanel : UserControl
        {
            private TextBox TxtName;
            private NumericUpDown NumPower;
            private ComboBox CmbStage;
            private ComboBox CmbAttribute;
            private NumericUpDown NumHp;
            private NumericUpDown NumArea;
            private NumericUpDown NumRound;
            private NumericUpDown NumVersion;
            private NumericUpDown NumHandicap;
            private ComboBox CmbPrize;
            private TextBox TxtUnlock;
            private ComboBox CmbAtkMain;
            private ComboBox CmbAtkAlt;
            private ComboBox CmbAtkAlt2;
            private CheckBox ChkSpecialEncounter;
            private CheckBox ChkFriend;
            private ComboBox CmbCurrency;
            private NumericUpDown NumCost;
            private NumericUpDown NumReward;
            private ComboBox CmbPrizePack;
            private NumericUpDown NumPrizeChance;
            private TextBox TxtAttackPattern;

            /// <summary>A cards.json pack in the Prize Pack list; the empty
            /// one is "no pack".</summary>
            private class PackItem
            {
                public string Id;
                public string Name;
                public override string ToString() => Name;
            }

            /// <summary>What each enemy field does. Kept as a field so it
            /// outlives the layout code - a ToolTip that goes out of scope
            /// stops showing.</summary>
            private readonly ToolTip enemyTips = new ToolTip
            {
                AutoPopDelay = 20000,
                InitialDelay = 300,
                ReshowDelay = 200,
                ShowAlways = true
            };
            private Dictionary<int, Image> atkSprites = new Dictionary<int, Image>();
            private Dictionary<int, Image> atkCritSprites = new Dictionary<int, Image>();

            public EnemyEditPanel()
            {
                InitializeComponent();
            }

            private void InitializeComponent()
            {
                this.Dock = DockStyle.Fill;
                var rightLayout = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 1,
                    RowCount = 1,
                    BackColor = SystemColors.Control,
                    Padding = new Padding(8)
                };

                // 4-column layout: label | control | label | control
                var fieldsPanel = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 4,
                    Padding = new Padding(0, 8, 0, 0),
                    AutoScroll = true
                };
                fieldsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100F));
                fieldsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 185F));
                fieldsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 115F));
                fieldsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 185F));
                for (int i = 0; i < 12; i++)
                    fieldsPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

                // colPair 0 → columns 0+1 (left), colPair 1 → columns 2+3 (right)
                void AddField(string label, Control control, int colPair, int rowIdx, string tip = null)
                {
                    var lbl = new Label
                    {
                        Text = label,
                        TextAlign = ContentAlignment.MiddleRight,
                        AutoSize = false,
                        Width = colPair == 0 ? 100 : 115,
                        Anchor = AnchorStyles.Right,
                        Margin = new Padding(0, 2, 4, 2)
                    };
                    if (!(control is CheckBox))
                    {
                        control.Width = 175;
                        control.MaximumSize = new Size(175, 0);
                    }
                    control.Anchor = AnchorStyles.Left;
                    control.Margin = new Padding(0, 2, 8, 2);
                    fieldsPanel.Controls.Add(lbl, colPair * 2, rowIdx);
                    fieldsPanel.Controls.Add(control, colPair * 2 + 1, rowIdx);
                    if (!string.IsNullOrEmpty(tip))
                    {
                        enemyTips.SetToolTip(lbl, tip);
                        enemyTips.SetToolTip(control, tip);
                    }
                }

                // --- Column 1 controls ---
                TxtName      = new TextBox();
                CmbStage     = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
                CmbStage.Items.AddRange(OmnipetModuleEditor.Models.Stages.Names);
                CmbAttribute = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
                CmbAttribute.Items.AddRange(new object[] { "Free", "Data", "Virus", "Vaccine" });
                NumPower     = new NumericUpDown { Minimum = 1, Maximum = 999999, Value = 10 };
                NumHandicap  = new NumericUpDown { Minimum = 0, Maximum = 999999, Value = 0 };
                CmbAtkMain   = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
                CmbAtkAlt    = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
                CmbAtkAlt2   = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
                CmbAtkMain.DrawMode = DrawMode.OwnerDrawFixed; CmbAtkMain.ItemHeight = 36; CmbAtkMain.DrawItem += AtkCombo_DrawItem;
                CmbAtkAlt.DrawMode  = DrawMode.OwnerDrawFixed; CmbAtkAlt.ItemHeight  = 36; CmbAtkAlt.DrawItem  += AtkCombo_DrawItem;
                CmbAtkAlt2.DrawMode = DrawMode.OwnerDrawFixed; CmbAtkAlt2.ItemHeight = 36; CmbAtkAlt2.DrawItem += AtkCombo_DrawItem;

                // --- Column 2 controls ---
                NumVersion   = new NumericUpDown { Minimum = 0, Maximum = 999999, Value = 1 };
                NumArea      = new NumericUpDown { Minimum = 1, Maximum = 999999, Value = 1 };
                NumRound     = new NumericUpDown { Minimum = 1, Maximum = 999999, Value = 1 };
                ChkSpecialEncounter = new CheckBox { Checked = false, AutoSize = true };
                // A special encounter keeps its round: a battle run fights an
                // area's special rounds in turn (the iC's Coliseum, Digimon
                // World's arenas), so only a lone encounter is round 1.
                NumHp        = new NumericUpDown { Minimum = 0, Maximum = 999999, Value = 1 };
                CmbPrize     = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
                TxtUnlock    = new TextBox();
                ChkFriend    = new CheckBox { Checked = false, AutoSize = true };
                CmbCurrency  = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
                CmbCurrency.Items.Add("");
                NumCost      = new NumericUpDown { Minimum = 0, Maximum = 999999, Value = 0 };
                NumReward    = new NumericUpDown { Minimum = 0, Maximum = 999999, Value = 0 };
                // Only a round 1 enemy charges: its cost is the area's entry fee.
                NumRound.ValueChanged += (s, e) => NumCost.Enabled = NumRound.Value == 1;
                CmbPrizePack = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
                CmbPrizePack.Items.Add(new PackItem { Id = "", Name = "" });
                NumPrizeChance = new NumericUpDown { Minimum = 0, Maximum = 100, Value = 100 };
                TxtAttackPattern = new TextBox();

                // --- Left column: Name, Stage, Attribute, Power, Handicap, Main Attack, Alt Attack, Crit Attack ---
                AddField("Name:",        TxtName,      0, 0,
                    "The enemy\u2019s name. It has to match a Digimon name exactly - the sprite is looked up by it, and a Friend encounter is matched to its pet record by name too.");
                AddField("Stage:",       CmbStage,     0, 1,
                    "The enemy\u2019s stage. Beating it credits the pet\u2019s Stage-N counter for THIS stage, which the Stage-5..8 evolution criteria read.");
                AddField("Attribute:",   CmbAttribute, 0, 2,
                    "Battle attribute: Vaccine beats Virus beats Data beats Vaccine, Free neither gains nor suffers. The module’s Battle Attribute Advantage is added to the hit rate, or to Power with \"as Power\".");
                AddField("Power:",       NumPower,     0, 3,
                    "The enemy\u2019s battle power, weighed against the pet\u2019s to decide the hit rate.");
                AddField("Handicap:",    NumHandicap,  0, 4,
                    "Percentage points subtracted from the hit rate of whoever attacks this enemy, on top of the power comparison (the devices\u2019 Quest Mode handicap; only adventure battles read it). A POSITIVE handicap makes the enemy harder to hit, so this is the knob for making a boss harder without touching its Power.");
                AddField("Main Attack:", CmbAtkMain,   0, 5,
                    "Regular attack sprite. Stored one HIGHER than the device/firmware id, because 0 is reserved for \"no attack sprite\".");
                AddField("Alt Attack:",  CmbAtkAlt,    0, 6,
                    "Alternate (big) attack sprite, from the second sprite bank. Same +1 offset as Main Attack.");
                AddField("Crit Attack:", CmbAtkAlt2,   0, 7,
                    "Sprite used for the critical attack, on modules that enable one. Same +1 offset.");

                // --- Right column: Version, Area, Round, Sp. Encounter, Prize, Unlock, HP ---
                AddField("Version:",       NumVersion,          1, 0,
                    "Which version roster this enemy belongs to. Enemies are resolved per ROUND, not per version: the game collects every version present on the round and, if the pet\u2019s own version is not among them, picks one at random. So a version does not need a record on every round - but giving one to each round of its own region is what stops a pet drawing the other region\u2019s enemy.");
                AddField("Area:",          NumArea,             1, 1,
                    "Adventure area this enemy appears in. Clearing an area is what an \"adventure\" unlock and an evolution\u2019s Area requirement key off.");
                AddField("Round:",         NumRound,            1, 2,
                    "Position within the area. Rounds are fought in order, and the round is what the game resolves an enemy on.");
                AddField("Sp. Encounter:", ChkSpecialEncounter, 1, 3,
                    "Marks this as a special encounter rather than part of the ordinary round sequence. A special encounter whose name matches a pet with Availability \"Friend\" registers that Friend when beaten. Its Round orders the fights of a battle run event (every special round of the area in turn); a lone encounter is round 1.");
                AddField("Prize:",         CmbPrize,            1, 4,
                    "Item awarded for beating this enemy. Leave it empty for no prize - the game hides the prize line entirely rather than showing \"None\".");
                AddField("Unlock:",        TxtUnlock,           1, 5,
                    "Name of an unlock this enemy drops when defeated. It must match an unlock declared on the Unlocks tab.");
                AddField("HP:",            NumHp,               1, 6,
                    "Hit points - how much damage this enemy absorbs before losing.");
                AddField("Friend:",        ChkFriend,           1, 7,
                    "Marks the enemy as a Friend encounter. The Friend itself is declared on the pet side, with Availability \"Friend\"; beating this registers it, and DigiXros requirements read that register.");

                // --- Currency: what the area charges and what the enemy pays ---
                AddField("Currency:",      CmbCurrency,         0, 8,
                    "The currency (Shop/Currencies tab) this enemy charges and pays in. Empty: it neither charges nor pays.");
                AddField("Reward:",        NumReward,           0, 9,
                    "Currency the pet gains for beating this enemy. A lost battle pays nothing.");
                AddField("Entry Cost:",    NumCost,             1, 8,
                    "Currency it costs to enter this enemy\u2019s area, set on its round 1 enemy only (the Digivice iC\u2019s Coliseums charge 0, 100, 500 and 1000 P). Disabled on later rounds.");

                // --- Prize card pack and the prize's drop chance ---
                AddField("Prize Pack:",    CmbPrizePack,        1, 9,
                    "A card pack (Collection tab) won for beating this enemy, opened into the collection. A win draws ONE prize at random from those the round\u2019s enemies carry - items and packs alike - and then rolls its chance.");
                AddField("Prize Chance:",  NumPrizeChance,      0, 10,
                    "Percent chance this enemy\u2019s prize - item or card pack - drops once it is the one drawn. 100 always drops.");
                AddField("Attack Pattern:", TxtAttackPattern,   1, 10,
                    "The attacks this enemy throws in the adventure, one per round, comma-separated, on the module\u2019s battle scale - on the Colour lines 1 is a normal shot and 2 a super one (\"1,2,2,2,2,1\"). The Pendulum Color quests name one per round. Empty: the pattern its stage gives, as before.");

                // Save/Cancel buttons span all 4 columns
                var buttonPanel = new FlowLayoutPanel
                {
                    Dock = DockStyle.Top,
                    FlowDirection = FlowDirection.LeftToRight,
                    AutoSize = true
                };
                var btnSave   = new Button { Text = "Save",   Width = 80, Margin = new Padding(8, 8, 8, 8) };
                var btnCancel = new Button { Text = "Cancel", Width = 80, Margin = new Padding(8, 8, 8, 8) };
                buttonPanel.Controls.Add(btnSave);
                buttonPanel.Controls.Add(btnCancel);
                fieldsPanel.Controls.Add(buttonPanel, 0, 11);
                fieldsPanel.SetColumnSpan(buttonPanel, 4);

                btnSave.Click += (s, e) =>
                {
                    var battleTab = GetBattleTab();
                    if (battleTab != null)
                    {
                        battleTab.enemyEditPanel.SaveToEnemy(battleTab.selectedEnemy);
                        battleTab.Save();
                        var saved = battleTab.selectedEnemy;
                        battleTab.SortEnemies();
                        battleTab.PopulateEnemyPanel();
                        battleTab.SelectEnemy(saved);
                    }
                };
                btnCancel.Click += (s, e) =>
                {
                    if (Parent is TableLayoutPanel parentLayout && parentLayout.Parent is BattleTab battleTab)
                        battleTab.enemyEditPanel.LoadEnemy(battleTab.selectedEnemy);
                };

                rightLayout.Controls.Add(fieldsPanel, 0, 0);
                this.Controls.Add(rightLayout);
            }

            private BattleTab GetBattleTab()
            {
                Control c = this;
                while (c != null && !(c is BattleTab))
                    c = c.Parent;
                return c as BattleTab;
            }

            private int FindAtkComboIndex(ComboBox cmb, int number)
            {
                for (int i = 0; i < cmb.Items.Count; i++)
                {
                    if (cmb.Items[i] is AtkComboItem item && item.Number == number)
                        return i;
                }
                // An id with no sprite is listed rather than saved back as 0.
                if (number != 0)
                    return cmb.Items.Add(new AtkComboItem(number, null));
                return 0;
            }

            public void LoadEnemy(BattleEnemy enemy)
            {
                if (enemy == null) return;

                TxtName.Text = enemy.Name ?? "";
                NumPower.Value = Math.Max(NumPower.Minimum, Math.Min(enemy.Power, NumPower.Maximum));
                CmbStage.SelectedIndex = enemy.Stage;
                CmbAttribute.SelectedIndex = (int)PetUtils.JsonToAttributeEnum(enemy.Attribute ?? "");
                NumHp.Value = Math.Max(NumHp.Minimum, Math.Min(enemy.Hp, NumHp.Maximum));
                NumArea.Value = Math.Max(NumArea.Minimum, Math.Min(enemy.Area, NumArea.Maximum));
                NumRound.Value = Math.Max(NumRound.Minimum, Math.Min(enemy.Round, NumRound.Maximum));
                NumVersion.Value = Math.Max(NumVersion.Minimum, Math.Min(enemy.Version, NumVersion.Maximum));
                NumHandicap.Value = Math.Max(NumHandicap.Minimum, Math.Min(enemy.Handicap, NumHandicap.Maximum));
                // A prize item.json no longer has is kept rather than replaced
                // by the previous enemy's.
                if (!string.IsNullOrEmpty(enemy.Prize) && !CmbPrize.Items.Contains(enemy.Prize))
                    CmbPrize.Items.Add(enemy.Prize);
                CmbPrize.SelectedItem = enemy.Prize ?? "";
                TxtUnlock.Text = enemy.Unlock ?? "";
                CmbAtkMain.SelectedIndex = FindAtkComboIndex(CmbAtkMain, enemy.AtkMain);
                CmbAtkAlt.SelectedIndex = FindAtkComboIndex(CmbAtkAlt, enemy.AtkAlt);
                CmbAtkAlt2.SelectedIndex = FindAtkComboIndex(CmbAtkAlt2, enemy.AtkAlt2);
                ChkSpecialEncounter.Checked = enemy.SpecialEncounter;
                ChkFriend.Checked = enemy.Friend;
                SelectCurrency(enemy.Currency ?? "");
                NumCost.Value = Math.Max(NumCost.Minimum, Math.Min(enemy.CurrencyCost, NumCost.Maximum));
                NumReward.Value = Math.Max(NumReward.Minimum, Math.Min(enemy.CurrencyReward, NumReward.Maximum));
                NumCost.Enabled = NumRound.Value == 1;
                SelectPack(enemy.PrizeCardPack ?? "");
                NumPrizeChance.Value = Math.Max(NumPrizeChance.Minimum, Math.Min(enemy.PrizeChance ?? 100, NumPrizeChance.Maximum));
                TxtAttackPattern.Text = enemy.AttackPattern != null ? string.Join(",", enemy.AttackPattern) : "";
            }

            /// <summary>Selects the pack *id*, keeping one cards.json no
            /// longer has so that saving does not silently drop it.</summary>
            private void SelectPack(string id)
            {
                var item = CmbPrizePack.Items.OfType<PackItem>().FirstOrDefault(p => p.Id == id);
                if (item == null)
                {
                    item = new PackItem { Id = id, Name = $"(missing pack: {id})" };
                    CmbPrizePack.Items.Add(item);
                }
                CmbPrizePack.SelectedItem = item;
            }

            /// <summary>Fills the Prize Pack list from the module's cards.json.</summary>
            public void LoadCardPacks(string modulePath)
            {
                CmbPrizePack.Items.Clear();
                CmbPrizePack.Items.Add(new PackItem { Id = "", Name = "" });
                foreach (var pack in CollectionFile.Load(modulePath).Packs)
                    if (!string.IsNullOrWhiteSpace(pack.Id))
                        CmbPrizePack.Items.Add(new PackItem { Id = pack.Id, Name = pack.Name ?? pack.Id });
                CmbPrizePack.SelectedIndex = 0;
            }

            /// <summary>Selects *name*, keeping one no currency declares any
            /// more so that saving does not silently drop it.</summary>
            private void SelectCurrency(string name)
            {
                if (!CmbCurrency.Items.Contains(name))
                    CmbCurrency.Items.Add(name);
                CmbCurrency.SelectedItem = name;
            }

            /// <summary>Refills the currency list, keeping what is selected.</summary>
            public void LoadCurrencies(IEnumerable<string> names)
            {
                string selected = CmbCurrency.SelectedItem as string ?? "";
                CmbCurrency.Items.Clear();
                CmbCurrency.Items.Add("");
                foreach (var name in names ?? Enumerable.Empty<string>())
                    if (!string.IsNullOrWhiteSpace(name) && !CmbCurrency.Items.Contains(name))
                        CmbCurrency.Items.Add(name);
                SelectCurrency(selected);
            }

            public void SaveToEnemy(BattleEnemy enemy)
            {
                if (enemy == null) return;
                enemy.Name = TxtName.Text;
                enemy.Power = (int)NumPower.Value;
                enemy.Stage = CmbStage.SelectedIndex;
                // Written only when changed: Free shows for an empty, absent or
                // unknown attribute, and those stay as they are.
                var attribute = CmbAttribute.SelectedIndex >= 0
                    ? (AttributeEnum)CmbAttribute.SelectedIndex : AttributeEnum.FREE;
                if (attribute != PetUtils.JsonToAttributeEnum(enemy.Attribute ?? ""))
                    enemy.Attribute = PetUtils.AttributeEnumToJson(attribute);
                enemy.Hp = (int)NumHp.Value;
                enemy.Area = (int)NumArea.Value;
                enemy.Round = (int)NumRound.Value;
                enemy.Version = (int)NumVersion.Value;
                enemy.Handicap = (int)NumHandicap.Value;
                // An empty prize or unlock the record does not have stays out.
                string prize = CmbPrize.SelectedItem?.ToString() ?? "";
                enemy.Prize = prize == "" && enemy.Prize == null ? null : prize;
                string unlock = TxtUnlock.Text;
                enemy.Unlock = unlock == "" && enemy.Unlock == null ? null : unlock;
                enemy.AtkMain = (CmbAtkMain.SelectedItem as AtkComboItem)?.Number ?? 0;
                enemy.AtkAlt = (CmbAtkAlt.SelectedItem as AtkComboItem)?.Number ?? 0;
                enemy.AtkAlt2 = (CmbAtkAlt2.SelectedItem as AtkComboItem)?.Number ?? 0;
                enemy.SpecialEncounter = ChkSpecialEncounter.Checked;
                enemy.Friend = ChkFriend.Checked;
                string currency = CmbCurrency.SelectedItem as string ?? "";
                enemy.Currency = currency == "" ? null : currency;
                enemy.CurrencyReward = (int)NumReward.Value;
                enemy.CurrencyCost = enemy.Round == 1 ? (int)NumCost.Value : 0;
                string pack = (CmbPrizePack.SelectedItem as PackItem)?.Id ?? "";
                enemy.PrizeCardPack = pack == "" ? null : pack;
                // 100% is the default and stays out of battle.json (unless the
                // record already names it).
                enemy.PrizeChance = NumPrizeChance.Value == 100
                    ? (enemy.PrizeChance == 100 ? (int?)100 : null)
                    : (int)NumPrizeChance.Value;
                var shots = new List<int>();
                foreach (var part in TxtAttackPattern.Text.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                    if (int.TryParse(part.Trim(), out int shot) && shot > 0)
                        shots.Add(shot);
                enemy.AttackPattern = shots.Count > 0 ? shots.ToArray() : null;
            }

            public void LoadAtkSprites(string modulePath, string primaryFormat = null)
            {
                this.atkSprites = PetUtils.LoadAtkSprites(modulePath, primaryFormat);
                this.atkCritSprites = PetUtils.LoadAtkCritSprites(modulePath, primaryFormat);
            }

            public void PopulateAtkCombos()
            {
                CmbAtkMain.Items.Clear();
                CmbAtkAlt.Items.Clear();
                CmbAtkAlt2.Items.Clear();
                CmbAtkMain.Items.Add(new AtkComboItem(0, null));
                CmbAtkAlt.Items.Add(new AtkComboItem(0, null));
                CmbAtkAlt2.Items.Add(new AtkComboItem(0, null));
                
                foreach (var kvp in atkSprites.OrderBy(x => x.Key))
                {
                    var item = new AtkComboItem(kvp.Key, kvp.Value);
                    CmbAtkMain.Items.Add(item);
                    CmbAtkAlt.Items.Add(item);
                }

                foreach (var kvp in atkCritSprites.OrderBy(x => x.Key))
                {
                    CmbAtkAlt2.Items.Add(new AtkComboItem(kvp.Key, kvp.Value));
                }
            }

            public void LoadItems(string modulePath)
            {
                CmbPrize.Items.Clear();
                CmbPrize.Items.Add(""); // Adiciona op��o vazia primeiro
                string itemPath = Path.Combine(modulePath, "item.json");
                if (File.Exists(itemPath))
                {
                    try
                    {
                        string json = File.ReadAllText(itemPath);
                        using (JsonDocument doc = JsonDocument.Parse(json))
                        {
                            var root = doc.RootElement;
                            if (root.TryGetProperty("item", out var itemsElement))
                            {
                                var items = JsonSerializer.Deserialize<List<Item>>(itemsElement.GetRawText());
                                if (items != null)
                                {
                                    foreach (var item in items)
                                        CmbPrize.Items.Add(item.Name);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error loading item.json: " + ex.Message);
                    }
                }
            }

            private void AtkCombo_DrawItem(object sender, DrawItemEventArgs e)
            {
                if (e.Index < 0) return;
                var combo = sender as ComboBox;
                var item  = combo.Items[e.Index] as AtkComboItem;
                e.DrawBackground();
                int x = e.Bounds.Left + 2;
                if (item?.Sprite != null)
                {
                    e.Graphics.DrawImage(item.Sprite, x, e.Bounds.Top + 2, 32, 32);
                    x += 36;
                }
                using (var brush = new SolidBrush(e.ForeColor))
                    e.Graphics.DrawString(item?.ToString() ?? "", e.Font, brush, x, e.Bounds.Top + 8);
                e.DrawFocusRectangle();
            }

            // Classe auxiliar para mostrar número + sprite
            private class AtkComboItem
            {
                public int Number { get; }
                public Image Sprite { get; }
                public AtkComboItem(int number, Image sprite)
                {
                    Number = number;
                    Sprite = sprite;
                }
                public override string ToString() => Number == 0 ? "None" : Number.ToString();
            }
        }

        #endregion
    }

    // ---------------------------------------------------------------------------
    // Owner-drawn virtual list — renders only visible rows so there is no per-item
    // WinForms control and no 32 K GDI coordinate ceiling.
    // ---------------------------------------------------------------------------
    internal class VirtualEnemyList : Panel
    {
        private const int ItemHeight = 56;

        private List<BattleEnemy> _items = new List<BattleEnemy>();
        private readonly Dictionary<string, Image> _spriteCache =
            new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
        private int _selectedIndex = -1;
        private Font _nameFont;
        private Font _infoFont;

        public event EventHandler<BattleEnemy> ItemSelected;

        public BattleEnemy SelectedItem =>
            (_selectedIndex >= 0 && _selectedIndex < _items.Count) ? _items[_selectedIndex] : null;

        public int ItemCount => _items.Count;

        public VirtualEnemyList()
        {
            SetStyle(
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.AllPaintingInWmPaint  |
                ControlStyles.UserPaint, true);
            AutoScroll = true;
            BackColor  = Color.White;
            RebuildFonts();
        }

        private void RebuildFonts()
        {
            _nameFont?.Dispose();
            _infoFont?.Dispose();
            _nameFont = new Font(Font.FontFamily, 11, FontStyle.Bold);
            _infoFont = new Font(Font.FontFamily,  8, FontStyle.Regular);
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            RebuildFonts();
        }

        // --- Public API ---

        public void SetItems(List<BattleEnemy> items)
        {
            _items         = items ?? new List<BattleEnemy>();
            _selectedIndex = -1;
            AutoScrollMinSize = new Size(0, _items.Count * ItemHeight);
            Invalidate();
        }

        public void SetSprite(string name, Image sprite)
        {
            if (string.IsNullOrEmpty(name)) return;
            if (_spriteCache.TryGetValue(name, out var old) && old != sprite)
                old?.Dispose();
            _spriteCache[name] = sprite;
            Invalidate();
        }

        public void SelectItem(BattleEnemy enemy)
        {
            int idx = _items.IndexOf(enemy);
            if (idx < 0) return;
            _selectedIndex = idx;
            EnsureVisible(idx);
            Invalidate();
        }

        public BattleEnemy GetItem(int index) =>
            (index >= 0 && index < _items.Count) ? _items[index] : null;

        // --- Painting ---

        protected override void OnPaint(PaintEventArgs e)
        {
            if (_items.Count == 0) return;
            int scrollY     = -AutoScrollPosition.Y;
            int first       = Math.Max(0, scrollY / ItemHeight);
            int last        = Math.Min(_items.Count - 1,
                                       (scrollY + ClientSize.Height + ItemHeight - 1) / ItemHeight);
            var g = e.Graphics;
            for (int i = first; i <= last; i++)
                DrawRow(g, i, scrollY);
        }

        private void DrawRow(Graphics g, int index, int scrollY)
        {
            var enemy    = _items[index];
            int y        = index * ItemHeight - scrollY;
            bool sel     = index == _selectedIndex;
            bool special = enemy.SpecialEncounter;
            int w        = ClientSize.Width;

            Color bgNormal = special ? Color.FromArgb(255, 243, 205) : Color.White;
            Color bgSel    = special ? Color.FromArgb(255, 214, 102) : Color.LightBlue;
            using (var bg = new SolidBrush(sel ? bgSel : bgNormal))
                g.FillRectangle(bg, 0, y, w, ItemHeight);

            using (var ab = new SolidBrush(AttrColor(enemy.Attribute ?? "")))
                g.FillRectangle(ab, 4, y + 4, 48, 48);
            g.DrawRectangle(Pens.Gray, 4, y + 4, 48, 48);

            if (!string.IsNullOrEmpty(enemy.Name) &&
                _spriteCache.TryGetValue(enemy.Name, out var sprite) && sprite != null)
                g.DrawImage(sprite, new Rectangle(4, y + 4, 48, 48));

            string infoText = special
                ? string.Format("Ver. {0} | Stage {1} | Area {2} | Special Encounter",
                    enemy.Version, enemy.Stage, enemy.Area)
                : string.Format("Ver. {0} | Stage {1} | Area {2} | Round {3}",
                    enemy.Version, enemy.Stage, enemy.Area, enemy.Round);

            using (var tb = new SolidBrush(Color.DeepSkyBlue))
            {
                g.DrawString(enemy.Name ?? "", _nameFont, tb, 60, y + 4);
                g.DrawString(infoText, _infoFont, tb, 60, y + 30);
            }

            g.DrawLine(Pens.LightGray, 0, y + ItemHeight - 1, w, y + ItemHeight - 1);
        }

        private static Color AttrColor(string attr)
        {
            switch (attr)
            {
                case "Da": return Color.FromArgb(66,  165, 245);
                case "Va": return Color.FromArgb(102, 187, 106);
                case "Vi": return Color.FromArgb(237,  83,  80);
                default:   return Color.FromArgb(171,  71, 188);
            }
        }

        // --- Interaction ---

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            int idx = (e.Y + (-AutoScrollPosition.Y)) / ItemHeight;
            if (idx >= 0 && idx < _items.Count)
            {
                _selectedIndex = idx;
                Invalidate();
                ItemSelected?.Invoke(this, _items[idx]);
                Focus();
            }
        }

        private void EnsureVisible(int idx)
        {
            int top     = idx * ItemHeight;
            int scrollY = -AutoScrollPosition.Y;
            if (top < scrollY)
                AutoScrollPosition = new Point(0, top);
            else if (top + ItemHeight > scrollY + ClientSize.Height)
                AutoScrollPosition = new Point(0, top + ItemHeight - ClientSize.Height);
        }

        protected override void OnScroll(ScrollEventArgs se)    { base.OnScroll(se);    Invalidate(); }
        protected override void OnMouseWheel(MouseEventArgs e)  { base.OnMouseWheel(e); Invalidate(); }
        protected override void OnResize(EventArgs e)           { base.OnResize(e);     Invalidate(); }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _nameFont?.Dispose();
                _infoFont?.Dispose();
                foreach (var img in _spriteCache.Values) img?.Dispose();
                _spriteCache.Clear();
            }
            base.Dispose(disposing);
        }
    }
}