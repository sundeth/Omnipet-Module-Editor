using OmnipetModuleEditor.Models;
using OmnipetModuleEditor.Utils;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace OmnipetModuleEditor
{
    /// <summary>
    /// Editor window for managing fast enemy areas and rounds.
    /// </summary>
    public partial class EnemyFastEditorForm : Form
    {
        // Data fields
        private List<BattleEnemy> enemies;
        private List<Pet> pets;
        private string modulePath;
        private Module module;

        // UI controls
        private TableLayoutPanel mainLayout;
        private FlowLayoutPanel topBar;
        private ComboBox cmbVersions;
        private ComboBox cmbStages;
        private Button btnAddArea;
        private Button btnSave;
        private Button btnCancel;
        private Panel panelPetList;
        private TabControl tabAreas;

        // State
        private int selectedVersion = -1;
        private int selectedStage = -1;
        private bool _specialEncounterMode = false;
        private readonly List<AreaGrid> areaGrids = new List<AreaGrid>();
        private readonly List<string> itemList = new List<string>();

        /// <summary>
        /// Switches the editor into Special Encounter mode: only 1 round per area is allowed
        /// and all saved enemies are marked as special encounters.
        /// Must be called before SetModulePath.
        /// </summary>
        public void SetSpecialEncounterMode(bool special)
        {
            _specialEncounterMode = special;
            Text = special ? "Special Encounters Fast Editor" : "Fast Enemy Editor";
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EnemyFastEditorForm"/> class.
        /// </summary>
        public EnemyFastEditorForm()
        {
            InitializeLayout();
        }

        /// <summary>
        /// Sets the module object for this editor.
        /// </summary>
        public void SetModule(Module module)
        {
            this.module = module;
        }

        /// <summary>
        /// Sets the list of enemies to be edited.
        /// </summary>
        public void SetEnemies(List<BattleEnemy> list)
        {
            enemies = list ?? new List<BattleEnemy>();
            // Descobre o maior índice de área nos inimigos
            int maxArea = enemies.Count > 0 ? enemies.Max(e => e.Area) : 0;
            // Garante que existam abas suficientes
            while (areaGrids.Count < maxArea)
                AddAreaTab();
            // Carrega os inimigos em cada grid
            foreach (var g in areaGrids)
                g.LoadEnemies(enemies);

            // Enemies no grid can show - a version no pet with power has, an
            // area or round below 1 - are kept as they are and saved back.
            var shown = new HashSet<BattleEnemy>(areaGrids.SelectMany(g => g.ShownEnemies()), ReferenceComparer.Instance);
            keptAside = enemies.Where(e => e != null && !shown.Contains(e)).ToList();
        }

        /// <summary>Enemies loaded but not on any grid, saved back unchanged.</summary>
        private List<BattleEnemy> keptAside = new List<BattleEnemy>();

        private sealed class ReferenceComparer : IEqualityComparer<BattleEnemy>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();
            public bool Equals(BattleEnemy a, BattleEnemy b) => ReferenceEquals(a, b);
            public int GetHashCode(BattleEnemy e) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(e);
        }

        /// <summary>
        /// Sets the module path and loads all related data.
        /// </summary>
        public void SetModulePath(string path)
        {
            modulePath = path;
            LoadModule();
            LoadPetsFromJson();
            LoadItemsFromJson();
            PopulateVersionCombo();
            PopulateStageCombo();
            PopulatePetList();
            foreach (var g in areaGrids)
                g.SetItemList(itemList);
            if (areaGrids.Count == 0)
                AddAreaTab();
        }

        #region Layout and UI Initialization

        /// <summary>
        /// Initializes the layout and UI controls.
        /// </summary>
        private void InitializeLayout()
        {
            Text = "Fast Enemy Editor";
            Size = new Size(1100, 700);
            MinimumSize = new Size(900, 600);

            mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
            };
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(mainLayout);

            topBar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(8, 8, 8, 8),
                AutoSize = true,
                WrapContents = false
            };

            cmbVersions = new ComboBox { Width = 90, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbStages = new ComboBox { Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
            btnAddArea = new Button { Text = "Add Area", AutoSize = true };
            btnSave = new Button { Text = "Save", AutoSize = true };
            btnCancel = new Button { Text = "Cancel", AutoSize = true };

            cmbVersions.SelectedIndexChanged += (_, __) =>
            {
                if (cmbVersions.SelectedIndex > 0)
                    selectedVersion = int.Parse(cmbVersions.Text.Replace("Version ", ""));
                else
                    selectedVersion = -1;
                PopulatePetList();
            };

            cmbStages.SelectedIndexChanged += (_, __) =>
            {
                selectedStage = cmbStages.SelectedIndex - 1;
                PopulatePetList();
            };

            btnAddArea.Click += (_, __) => AddAreaTab();
            btnSave.Click += (_, __) => { SaveEnemiesFromGrids(); DialogResult = DialogResult.OK; };
            btnCancel.Click += (_, __) => DialogResult = DialogResult.Cancel;

            topBar.Controls.AddRange(new Control[]
            {
                new Label{Text="Version:", AutoSize=true, Margin=new Padding(0,8,4,0)},
                cmbVersions,
                new Label{Text="Stage:", AutoSize=true, Margin=new Padding(12,8,4,0)},
                cmbStages,
                btnAddArea,
                btnSave,
                btnCancel
            });

            mainLayout.Controls.Add(topBar, 0, 0);
            mainLayout.SetColumnSpan(topBar, 2);

            panelPetList = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(8),
                BackColor = Color.WhiteSmoke
            };
            mainLayout.Controls.Add(panelPetList, 0, 1);

            tabAreas = new TabControl
            {
                Dock = DockStyle.Fill,
                Alignment = TabAlignment.Top,
                ItemSize = new Size(120, 36),
                SizeMode = TabSizeMode.Fixed
            };
            mainLayout.Controls.Add(tabAreas, 1, 1);
        }

        #endregion

        #region ComboBox Data Population

        /// <summary>
        /// Populates the version combo box.
        /// </summary>
        private void PopulateVersionCombo()
        {
            cmbVersions.Items.Clear();
            cmbVersions.Items.Add("");
            if (pets == null) return;
            foreach (var v in pets.Where(p => p.Power > 0)
                                   .Select(p => p.Version)
                                   .Distinct().OrderBy(v => v))
                cmbVersions.Items.Add($"Version {v}");
            cmbVersions.SelectedIndex = 0;
            selectedVersion = -1;
        }

        /// <summary>
        /// Populates the stage combo box.
        /// </summary>
        private void PopulateStageCombo()
        {
            cmbStages.Items.Clear();
            cmbStages.Items.Add("");
            foreach (var n in Stages.Names)
                cmbStages.Items.Add(n);
            cmbStages.SelectedIndex = 0;
            selectedStage = -1;
        }

        #endregion

        #region Area Tabs

        /// <summary>
        /// Adds a new area tab to the editor.
        /// </summary>
        private void AddAreaTab()
        {
            int idx = tabAreas.TabPages.Count + 1;
            var page = new TabPage($"Area {idx}");

            var grid = new AreaGrid(GetModuleVersions(), module, modulePath, itemList, idx, _specialEncounterMode);
            areaGrids.Add(grid);

            grid.Panel.Dock = DockStyle.Fill;
            page.Controls.Add(grid.Panel);
            tabAreas.TabPages.Add(page);
            tabAreas.SelectedTab = page;
        }

        /// <summary>
        /// Gets the list of module versions.
        /// </summary>
        private List<int> GetModuleVersions() =>
            pets?.Where(p => p.Power > 0)
                 .Select(p => p.Version)
                 .Distinct()
                 .OrderBy(v => v)
                 .ToList() ?? new List<int>();

        #endregion

        #region Pet List

        /// <summary>
        /// Populates the pet list panel with filtered pets.
        /// </summary>
        private void PopulatePetList()
        {
            panelPetList.Controls.Clear();
            if (pets == null) return;

            var filtered = pets.Where(p => p.Power > 0 &&
                                           (selectedVersion == -1 || p.Version == selectedVersion) &&
                                           (selectedStage == -1 || p.Stage == selectedStage))
                               .OrderBy(p => p.Stage)
                               .ThenBy(p => p.Version)
                               .ThenBy(p => p.Name);

            int y = 0;
            foreach (var pet in filtered)
            {
                var p = CreatePetPanel(pet);
                p.Location = new Point(0, y);
                panelPetList.Controls.Add(p);
                y += p.Height + 8;
            }
        }

        /// <summary>
        /// Creates a panel representing a pet for the pet list.
        /// </summary>
        private Panel CreatePetPanel(Pet pet)
        {
            var pnl = new Panel { Size = new Size(230, 56), BorderStyle = BorderStyle.FixedSingle, Tag = pet };
            pnl.Click += (s, e) =>
            {
                if (tabAreas.SelectedIndex >= 0 && tabAreas.SelectedIndex < areaGrids.Count)
                {
                    areaGrids[tabAreas.SelectedIndex].AddEnemyFromPet(pet);
                }
            };

            var pb = new PictureBox
            {
                Location = new Point(4, 4),
                Size = new Size(48, 48),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = GetAttributeColor(pet.Attribute ?? ""),
                BorderStyle = BorderStyle.FixedSingle
            };

            LoadSpriteIntoPictureBox(pb, pet.Name);

            var name = new Label
            {
                Text = pet.Name,
                Location = new Point(60, 4),
                AutoSize = true,
                Font = new Font(FontFamily.GenericSansSerif, 11, FontStyle.Bold),
                ForeColor = Color.DeepSkyBlue
            };

            var info = new Label
            {
                Text = $"Stage {pet.Stage} | Ver. {pet.Version}",
                Location = new Point(60, 28),
                AutoSize = true,
                Font = new Font(FontFamily.GenericSansSerif, 8),
                ForeColor = Color.DeepSkyBlue
            };

            pnl.Controls.AddRange(new Control[] { pb, name, info });
            return pnl;
        }

        /// <summary>
        /// Loads the pet sprite into the given PictureBox.
        /// </summary>
        private void LoadSpriteIntoPictureBox(PictureBox pb, string petName)
        {
            if (string.IsNullOrEmpty(modulePath)) return;
            
            string primary = module?.PrimarySpriteFormat ?? "Color";
            string secondary = module?.SecondarySpriteFormat ?? "HD";
            var sprite = SpriteUtils.LoadSingleSprite(petName, modulePath, PetUtils.FixedNameFormat, primary, secondary);
            pb.Image = sprite;
        }

        /// <summary>
        /// Gets the color for a given attribute.
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

        #endregion

        #region Save and Data Events

        /// <summary>
        /// Event triggered when enemies are saved.
        /// </summary>
        public event Action<List<BattleEnemy>> EnemiesSaved;

        /// <summary>
        /// Saves the enemies from all area grids.
        /// </summary>
        private void SaveEnemiesFromGrids()
        {
            enemies = areaGrids.SelectMany(g => g.GetAllBattleEnemies()).Concat(keptAside).ToList();
            EnemiesSaved?.Invoke(enemies);
        }

        #endregion

        #region JSON Data Loading

        /// <summary>
        /// Loads the module from module.json.
        /// </summary>
        private void LoadModule()
        {
            if (module != null || string.IsNullOrEmpty(modulePath)) return;
            string f = Path.Combine(modulePath, "module.json");
            if (File.Exists(f))
                try { module = System.Text.Json.JsonSerializer.Deserialize<Module>(File.ReadAllText(f)); }
                catch { module = null; }
        }

        /// <summary>
        /// Loads the pets from monster.json.
        /// </summary>
        private void LoadPetsFromJson()
        {
            pets = new List<Pet>();
            if (string.IsNullOrEmpty(modulePath)) return;
            string f = Path.Combine(modulePath, "monster.json");
            if (!File.Exists(f)) return;
            try
            {
                var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(f));
                if (doc.RootElement.TryGetProperty("monster", out var arr))
                    pets = System.Text.Json.JsonSerializer.Deserialize<List<Pet>>(arr.GetRawText());
            }
            catch { }
        }

        /// <summary>
        /// Loads the item list from item.json.
        /// </summary>
        private void LoadItemsFromJson()
        {
            itemList.Clear();
            if (string.IsNullOrEmpty(modulePath)) return;
            string f = Path.Combine(modulePath, "item.json");
            if (!File.Exists(f)) return;
            try
            {
                var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(f));
                if (doc.RootElement.TryGetProperty("item", out var arr) && arr.ValueKind == System.Text.Json.JsonValueKind.Array)
                    foreach (var i in arr.EnumerateArray())
                        if (i.TryGetProperty("name", out var n)) itemList.Add(n.GetString());
            }
            catch { }
        }

        #endregion

        #region AreaGrid Internal Class

        /// <summary>
        /// Represents a grid for managing enemies in a specific area.
        /// </summary>
        private class AreaGrid
        {
            public Panel Panel { get; }
            private readonly TableLayoutPanel grid;
            private readonly List<int> versions;
            // [version][round - 1] -> every enemy of that slot. The game takes
            // several (a special slot meets one at random, game_module
            // get_enemies), so a slot is a list and the grid shows its first.
            private readonly List<List<List<BattleEnemy>>> slots;
            private readonly Module module;
            private readonly string modulePath;
            private List<string> itemList;
            private readonly int areaIndex;
            private readonly bool specialEncounterMode;

            private int columnCount = 1;
            private List<TextBox> unlockBoxes = new List<TextBox>();
            private List<ComboBox> itemCombos = new List<ComboBox>();
            // What each round's boxes showed when loaded, and whether the
            // round's enemies disagreed (a box then shows empty). A box is
            // written to the round's enemies only when it was changed, so
            // one version's prize never overwrites another's.
            private readonly List<string> loadedUnlock = new List<string>();
            private readonly List<string> loadedPrize = new List<string>();
            private readonly List<bool> mixedUnlock = new List<bool>();
            private readonly List<bool> mixedPrize = new List<bool>();
            private readonly ToolTip boxTips = new ToolTip();

            public AreaGrid(List<int> versions, Module module, string modulePath, List<string> items, int areaIdx, bool specialEncounterMode = false)
            {
                this.versions = versions;
                this.module = module;
                this.modulePath = modulePath;
                this.itemList = items ?? new List<string>();
                areaIndex = areaIdx;
                this.specialEncounterMode = specialEncounterMode;
                slots = versions.Select(_ => new List<List<BattleEnemy>> { new List<BattleEnemy>() }).ToList();

                Panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true };

                grid = new TableLayoutPanel
                {
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    Location = Point.Empty,
                    Anchor = AnchorStyles.Top | AnchorStyles.Left,
                    ColumnCount = 1,
                    RowCount = versions.Count + 2,
                    CellBorderStyle = TableLayoutPanelCellBorderStyle.Single,
                    GrowStyle = TableLayoutPanelGrowStyle.AddColumns
                };

                grid.Controls.Add(MakeHeaderLabel("Round 1"), 0, 0);
                for (int i = 0; i < versions.Count; i++)
                    grid.Controls.Add(MakeHeaderLabel($"Version {versions[i]}", ContentAlignment.MiddleRight), 0, i + 1);

                AddUnlockRow();

                Panel.Controls.Add(grid);
            }

            private static Label MakeHeaderLabel(string txt, ContentAlignment align = ContentAlignment.MiddleCenter) =>
                new Label { Text = txt, TextAlign = align, Dock = DockStyle.Fill, Font = new Font(FontFamily.GenericSansSerif, 9, FontStyle.Bold) };

            private void AddUnlockRow()
            {
                unlockBoxes.Clear();
                itemCombos.Clear();
                loadedUnlock.Clear();
                loadedPrize.Clear();
                mixedUnlock.Clear();
                mixedPrize.Clear();

                for (int c = 0; c < columnCount; c++)
                {
                    var cellPanel = new Panel { Dock = DockStyle.Fill, Height = 70 };
                    var unlockLabel = new Label { Text = "Unlock:", Dock = DockStyle.Top, AutoSize = true, TextAlign = ContentAlignment.MiddleLeft };
                    var unlockBox = new TextBox { Width = 100, Dock = DockStyle.Top, Margin = new Padding(0, 0, 0, 2) };
                    var itemLabel = new Label { Text = "Item:", Dock = DockStyle.Top, AutoSize = true, TextAlign = ContentAlignment.MiddleLeft };
                    var itemCombo = new ComboBox
                    {
                        Width = 100,
                        Dock = DockStyle.Top,
                        DropDownStyle = ComboBoxStyle.DropDownList
                    };
                    itemCombo.Items.Add("");
                    foreach (var item in itemList)
                        itemCombo.Items.Add(item);

                    cellPanel.Controls.Add(itemCombo);
                    cellPanel.Controls.Add(itemLabel);
                    cellPanel.Controls.Add(unlockBox);
                    cellPanel.Controls.Add(unlockLabel);

                    grid.Controls.Add(cellPanel, c, versions.Count + 1);

                    unlockBoxes.Add(unlockBox);
                    itemCombos.Add(itemCombo);
                    NoteNewRoundBoxes();
                }
            }

            private void NoteNewRoundBoxes()
            {
                loadedUnlock.Add("");
                loadedPrize.Add("");
                mixedUnlock.Add(false);
                mixedPrize.Add(false);
            }

            private void ExpandUnlockRow()
            {
                for (int c = unlockBoxes.Count; c < columnCount; c++)
                {
                    var cellPanel = new Panel { Dock = DockStyle.Fill, Height = 70 };
                    var unlockLabel = new Label { Text = "Unlock:", Dock = DockStyle.Top, AutoSize = true, TextAlign = ContentAlignment.MiddleLeft };
                    var unlockBox = new TextBox { Width = 100, Dock = DockStyle.Top, Margin = new Padding(0, 0, 0, 2) };
                    var itemLabel = new Label { Text = "Item:", Dock = DockStyle.Top, AutoSize = true, TextAlign = ContentAlignment.MiddleLeft };
                    var itemCombo = new ComboBox
                    {
                        Width = 100,
                        Dock = DockStyle.Top,
                        DropDownStyle = ComboBoxStyle.DropDownList
                    };
                    itemCombo.Items.Add("");
                    foreach (var item in itemList)
                        itemCombo.Items.Add(item);

                    cellPanel.Controls.Add(itemCombo);
                    cellPanel.Controls.Add(itemLabel);
                    cellPanel.Controls.Add(unlockBox);
                    cellPanel.Controls.Add(unlockLabel);

                    grid.Controls.Add(cellPanel, c, versions.Count + 1);

                    unlockBoxes.Add(unlockBox);
                    itemCombos.Add(itemCombo);
                    NoteNewRoundBoxes();
                }
            }

            private void AddColumn()
            {
                columnCount++;
                grid.ColumnCount = columnCount;
                grid.Controls.Add(MakeHeaderLabel($"Round {columnCount}"), columnCount - 1, 0);
                for (int r = 1; r <= versions.Count; r++)
                    grid.Controls.Add(new Panel { Size = new Size(140, 40) }, columnCount - 1, r);
                foreach (var row in slots)
                    while (row.Count < columnCount) row.Add(new List<BattleEnemy>());
                ExpandUnlockRow();
            }

            /// <summary>Every enemy on this grid, in version, round, slot order.</summary>
            public IEnumerable<BattleEnemy> ShownEnemies() =>
                slots.SelectMany(row => row.SelectMany(slot => slot));

            private void PlaceEnemy(int vIdx, int col)
            {
                var cell = grid.GetControlFromPosition(col, vIdx + 1);
                if (cell != null) grid.Controls.Remove(cell);

                var list = slots[vIdx][col];
                if (list.Count == 0)
                {
                    grid.Controls.Add(new Panel { Size = new Size(140, 40) }, col, vIdx + 1);
                    return;
                }
                var e = list[0];
                var slot = new Panel { Size = new Size(140, 40), BackColor = Color.LightYellow, BorderStyle = BorderStyle.FixedSingle };
                var pb = new PictureBox { Location = new Point(2, 2), Size = new Size(30, 30), SizeMode = PictureBoxSizeMode.Zoom };
                LoadSprite(pb, e.Name);
                // A slot holding more than one enemy (a special slot the game
                // meets one of at random) says how many more it has.
                string more = list.Count > 1 ? $" (+{list.Count - 1})" : "";
                var info = new Label
                {
                    Text = $"{e.Name}{more}\nS:{e.Stage} V:{e.Version}\nP:{e.Power}",
                    Location = new Point(34, 0),
                    AutoSize = true,
                    Font = new Font(FontFamily.GenericSansSerif, 7)
                };
                slot.Controls.AddRange(new Control[] { pb, info });
                if (list.Count > 1)
                    boxTips.SetToolTip(info, string.Join("\n", list.Select(x => x.Name)) + "\nRight-click removes the first.");

                MouseEventHandler remove = (s, ev) =>
                {
                    if (ev.Button == MouseButtons.Right)
                        RemoveEnemyAndShiftLeft(vIdx, col);
                };
                slot.MouseUp += remove;
                info.MouseUp += remove;
                pb.MouseUp += remove;

                grid.Controls.Add(slot, col, vIdx + 1);
            }

            private void LoadSprite(PictureBox pb, string name)
            {
                if (string.IsNullOrEmpty(modulePath)) return;
                
                string primary = module?.PrimarySpriteFormat ?? "Color";
                string secondary = module?.SecondarySpriteFormat ?? "HD";
                var sprite = SpriteUtils.LoadSingleSprite(name, modulePath, PetUtils.FixedNameFormat, primary, secondary);
                pb.Image = sprite;
            }

            private BattleEnemy CreateBattleEnemyFromPet(Pet p, int area, int round) => new BattleEnemy
            {
                Name = p.Name,
                Power = p.Power,
                Attribute = p.Attribute,
                Hp = p.Hp,
                Version = p.Version,
                Stage = p.Stage,
                AtkMain = p.AtkMain,
                AtkAlt = p.AtkAlt,
                Area = area,
                Round = specialEncounterMode ? 1 : round,
                SpecialEncounter = specialEncounterMode
            };

            /// <summary>
            /// Loads enemies into the grid for this area.
            /// </summary>
            public void LoadEnemies(List<BattleEnemy> all)
            {
                foreach (var row in slots)
                    foreach (var slot in row) slot.Clear();
                foreach (var be in all.Where(b => b != null && b.Area == areaIndex))
                {
                    int vIdx = versions.IndexOf(be.Version);
                    if (vIdx < 0 || be.Round < 1) continue; // kept aside by the form
                    while (columnCount < be.Round) AddColumn();
                    slots[vIdx][be.Round - 1].Add(be);
                }

                for (int v = 0; v < versions.Count; v++)
                    for (int c = 0; c < columnCount; c++)
                        if (slots[v][c].Count > 0) PlaceEnemy(v, c);

                // Each round's boxes show the value its enemies share; when
                // they differ (a prize per version) the box shows empty and
                // is only written if it is changed.
                for (int c = 0; c < columnCount && c < unlockBoxes.Count; c++)
                {
                    var inRound = slots.SelectMany(row => row[c]).ToList();
                    if (inRound.Count == 0) continue;
                    var unlocks = inRound.Select(e => e.Unlock ?? "").Distinct().ToList();
                    var prizes = inRound.Select(e => e.Prize ?? "").Distinct().ToList();

                    mixedUnlock[c] = unlocks.Count > 1;
                    loadedUnlock[c] = mixedUnlock[c] ? "" : unlocks[0];
                    unlockBoxes[c].Text = loadedUnlock[c];
                    if (mixedUnlock[c])
                        boxTips.SetToolTip(unlockBoxes[c], "The round's enemies have different unlocks: " + string.Join(", ", unlocks.Select(u => u == "" ? "(none)" : u)) + ". Typing one here gives it to all of them.");

                    mixedPrize[c] = prizes.Count > 1;
                    loadedPrize[c] = mixedPrize[c] ? "" : prizes[0];
                    if (loadedPrize[c] != "" && !itemCombos[c].Items.Contains(loadedPrize[c]))
                        itemCombos[c].Items.Add(loadedPrize[c]);
                    itemCombos[c].SelectedItem = loadedPrize[c];
                    if (mixedPrize[c])
                        boxTips.SetToolTip(itemCombos[c], "The round's enemies have different prizes: " + string.Join(", ", prizes.Select(p => p == "" ? "(none)" : p)) + ". Choosing one here gives it to all of them.");
                }
            }

            /// <summary>
            /// Gets all battle enemies from this area grid.
            /// </summary>
            public List<BattleEnemy> GetAllBattleEnemies()
            {
                var list = new List<BattleEnemy>();

                for (int c = 0; c < columnCount; c++)
                {
                    string unlock = unlockBoxes.Count > c ? unlockBoxes[c].Text : "";
                    string prize = itemCombos.Count > c ? (itemCombos[c].SelectedItem as string ?? "") : "";
                    bool unlockChanged = c < loadedUnlock.Count && unlock != loadedUnlock[c];
                    bool prizeChanged = c < loadedPrize.Count && prize != loadedPrize[c];
                    bool unlockShared = c < mixedUnlock.Count && !mixedUnlock[c];
                    bool prizeShared = c < mixedPrize.Count && !mixedPrize[c];

                    foreach (var be in slots.SelectMany(row => row[c]))
                    {
                        // A changed box is the round's new value for every
                        // version; an unchanged one only fills an enemy added
                        // here, and leaves each loaded enemy its own.
                        if (unlockChanged)
                            be.Unlock = unlock == "" ? null : unlock;
                        else if (unlockShared && unlock != "" && string.IsNullOrEmpty(be.Unlock))
                            be.Unlock = unlock;
                        if (prizeChanged)
                            be.Prize = prize == "" ? null : prize;
                        else if (prizeShared && prize != "" && string.IsNullOrEmpty(be.Prize))
                            be.Prize = prize;
                    }
                }

                for (int v = 0; v < versions.Count; v++)
                {
                    for (int c = 0; c < columnCount; c++)
                    {
                        foreach (var be in slots[v][c])
                        {
                            be.Area = areaIndex;
                            be.Round = c + 1;
                            list.Add(be);
                        }
                    }
                }

                return list;
            }

            /// <summary>
            /// Updates the item list for all item combo boxes.
            /// </summary>
            public void SetItemList(List<string> items)
            {
                itemList = items ?? new List<string>();
                foreach (var combo in itemCombos)
                {
                    var current = combo.SelectedItem as string;
                    combo.Items.Clear();
                    combo.Items.Add("");
                    foreach (var item in itemList)
                        combo.Items.Add(item);
                    if (!string.IsNullOrEmpty(current) && combo.Items.Contains(current))
                        combo.SelectedItem = current;
                }
            }

            /// <summary>
            /// Adds an enemy to the grid from a Pet object.
            /// </summary>
            public void AddEnemyFromPet(Pet pet)
            {
                int vIdx = versions.IndexOf(pet.Version);
                if (vIdx < 0) return;

                int col;
                if (specialEncounterMode)
                {
                    // Special encounters: round 1. Added beside any enemy the
                    // slot has (the game meets one of them at random);
                    // right-click removes one.
                    col = 0;
                }
                else
                {
                    col = slots[vIdx].FindIndex(s => s.Count == 0);
                    if (col == -1)
                    {
                        AddColumn();
                        col = columnCount - 1;
                    }
                }

                var be = CreateBattleEnemyFromPet(pet, areaIndex, col + 1);
                slots[vIdx][col].Add(be);
                PlaceEnemy(vIdx, col);
            }

            /// <summary>
            /// Removes the slot's first enemy; a slot left empty closes up,
            /// the row's later rounds shifting left.
            /// </summary>
            private void RemoveEnemyAndShiftLeft(int vIdx, int col)
            {
                if (vIdx < 0 || vIdx >= slots.Count) return;
                if (col < 0 || col >= slots[vIdx].Count || slots[vIdx][col].Count == 0) return;

                slots[vIdx][col].RemoveAt(0);
                if (slots[vIdx][col].Count == 0)
                {
                    slots[vIdx].RemoveAt(col);
                    slots[vIdx].Add(new List<BattleEnemy>());
                }

                RedrawEnemiesRow(vIdx);
            }

            /// <summary>
            /// Redraws the row of enemies for a version.
            /// </summary>
            private void RedrawEnemiesRow(int vIdx)
            {
                for (int c = 0; c < columnCount; c++)
                    PlaceEnemy(vIdx, c);
            }
        }

        #endregion
    }
}
