using OmnipetModuleEditor.Controls;
using OmnipetModuleEditor.DigimonSync;
using OmnipetModuleEditor.Models;
using OmnipetModuleEditor.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OmnipetModuleEditor.Tabs
{
    /// <summary>
    /// Tab for managing and editing pets in the module.
    /// </summary>
    public partial class PetTab : UserControl, IListClipboardTarget
    {
        // Fields - Changed to internal so PetEditPanel can access them
        internal List<Pet> pets;
        internal string modulePath;
        internal Module module;
        private PetListPanel petListPanel;
        private PetEditPanel petEditPanel;
        private Pet copiedPet = null;
        private PetSpritePanel spritePanel;
        private Panel selectedPanel = null;
        private Pet selectedPet = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="PetTab"/> class.
        /// </summary>
        private int lastSearchIndex = -1;
        private string lastSearchText = "";

        public PetTab()
        {
            InitializeComponent();
            petListPanel.BtnRemove.Click += BtnRemove_Click;
            petListPanel.BtnCopy.Click += BtnCopy_Click;
            petListPanel.BtnPaste.Click += BtnPaste_Click;
            petListPanel.BtnAdd.Click += BtnAdd_Click;
            petListPanel.BtnGo.Click += (s, e) => SearchGo();
            petListPanel.BtnPrev.Click += (s, e) => SearchPrevNext(-1);
            petListPanel.BtnNext.Click += (s, e) => SearchPrevNext(1);
            petListPanel.TxtSearch.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { SearchGo(); e.SuppressKeyPress = true; } };
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

            petListPanel = new PetListPanel();
            petListPanel.Width = 320;
            petListPanel.MinimumSize = new Size(320, 0);

            var rightPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0) };

            spritePanel = new PetSpritePanel
            {
                Dock = DockStyle.Top,
                Height = 150
            };

            petEditPanel = new PetEditPanel
            {
                Dock = DockStyle.Fill
            };
            
            // Set the owner reference so PetEditPanel can access PetTab data
            petEditPanel.SetOwner(this);

            rightPanel.Controls.Add(petEditPanel);
            rightPanel.Controls.Add(spritePanel);

            mainLayout.Controls.Add(petListPanel, 0, 0);
            mainLayout.Controls.Add(rightPanel, 1, 0);

            this.Controls.Add(mainLayout);
            this.Name = "PetTab";
            this.Size = new Size(800, 560);
            this.ResumeLayout(false);
        }

        #endregion

        #region Public API

        /// <summary>
        /// Sets the module path and loads pets and attack sprites.
        /// </summary>
        public void SetModule(string modulePath, Module module)
        {
            this.modulePath = modulePath;
            this.module = module;
            pets = PetUtils.LoadPetsFromJson(modulePath);
            petEditPanel.LoadAtkSprites(modulePath, module?.PrimarySpriteFormat);
            petEditPanel.PopulateAtkCombos();
            PopulatePetPanel();
        }

        /// <summary>
        /// Updates the module sprite format and refreshes the pet list thumbnails and the
        /// currently selected pet's sprite panel.
        /// </summary>
        public void RefreshSpriteFormat(Module updatedModule)
        {
            this.module = updatedModule;

            // Reload attack sprites with the updated format
            petEditPanel.LoadAtkSprites(modulePath, updatedModule?.PrimarySpriteFormat);
            petEditPanel.PopulateAtkCombos();

            // Repopulate the list to refresh thumbnails (preserve scroll via BeginInvoke)
            if (selectedPet != null)
            {
                var panel = PopulatePetPanelAndReturnPanel(selectedPet);
                if (panel != null)
                    SelectPetPanel(panel);
            }
            else
            {
                PopulatePetPanel();
            }

            // Refresh the sprite panel for the selected pet
            if (selectedPet != null)
            {
                spritePanel.CurrentModule = updatedModule;
                spritePanel.RefreshSprites();
            }
        }

        /// <summary>
        /// Saves the pets to monster.json.
        /// </summary>
        public void Save()
        {
            if (string.IsNullOrEmpty(modulePath) || pets == null)
                return;

            string monsterPath = Path.Combine(modulePath, "monster.json");
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
            var obj = new { monster = pets };
            try
            {
                string json = JsonSerializer.Serialize(obj, options);
                File.WriteAllText(monsterPath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    string.Format(Properties.Resources.ErrorSavingMonster, ex.Message),
                    Properties.Resources.Error,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        #endregion

        #region UI Population

        /// <summary>
        /// Populates the pet list panel with pet panels.
        /// </summary>
        private void PopulatePetPanel()
        {
            int savedScrollY = -petListPanel.PanelPetList.AutoScrollPosition.Y;

            petListPanel.PanelPetList.AutoScrollPosition = new Point(0, 0);
            petListPanel.PanelPetList.SuspendLayout();
            petListPanel.PanelPetList.Controls.Clear();
            int y = 0;

            var sortedPets = GetSortedPets();

            foreach (var pet in sortedPets)
            {
                var petPanel = CreatePetPanel(pet, y);
                petListPanel.PanelPetList.Controls.Add(petPanel);
                y += 56;
            }
            petListPanel.PanelPetList.AutoScrollMinSize = new Size(0, y);
            petListPanel.PanelPetList.ResumeLayout(true);

            var listPanel = petListPanel.PanelPetList;
            if (IsHandleCreated && savedScrollY > 0)
                BeginInvoke(new Action(() => listPanel.AutoScrollPosition = new Point(0, savedScrollY)));
        }

        /// <summary>
        /// Populates the pet list and returns the panel for a specific pet.
        /// </summary>
        internal Panel PopulatePetPanelAndReturnPanel(Pet petToSelect = null)
        {
            int savedScrollY = -petListPanel.PanelPetList.AutoScrollPosition.Y;

            petListPanel.PanelPetList.AutoScrollPosition = new Point(0, 0);
            petListPanel.PanelPetList.SuspendLayout();
            petListPanel.PanelPetList.Controls.Clear();
            int y = 0;
            Panel selected = null;

            var sortedPets = GetSortedPets();

            foreach (var pet in sortedPets)
            {
                var petPanel = CreatePetPanel(pet, y);
                petListPanel.PanelPetList.Controls.Add(petPanel);
                if (petToSelect != null && pet == petToSelect)
                    selected = petPanel;
                y += 56;
            }
            petListPanel.PanelPetList.AutoScrollMinSize = new Size(0, y);
            petListPanel.PanelPetList.ResumeLayout(true);

            var listPanel = petListPanel.PanelPetList;
            if (IsHandleCreated && savedScrollY > 0)
                BeginInvoke(new Action(() => listPanel.AutoScrollPosition = new Point(0, savedScrollY)));

            return selected;
        }

        /// <summary>
        /// Gets the sorted pets list following the sorting rules:
        /// - Version 0 pets are always at the top
        /// - If module has index field filled: version > index
        /// - Otherwise: version > stage > name
        /// </summary>
        private List<Pet> GetSortedPets()
        {
            if (pets == null || pets.Count == 0)
                return new List<Pet>();

            // Check if any pet in the list has an Index value set (excluding stage 0 pets)
            bool useIndex = pets.Any(p => p.Stage != 0 && p.Index.HasValue && p.Index.Value >= 0);

            var sortedPets = new List<Pet>(pets);
            sortedPets.Sort((a, b) =>
            {
                // First sort by version - version 0 should be at the top
                // Handle version 0 specially: version 0 should always be first
                if (a.Version == 0 && b.Version != 0) return -1;  // a comes before b
                if (b.Version == 0 && a.Version != 0) return 1;   // b comes before a
                
                // Both are version 0 or neither is version 0 - compare normally
                int cmp = a.Version.CompareTo(b.Version);
                if (cmp != 0) return cmp;

                // If using index, sort by index (stage 0 pets always have index -1)
                if (useIndex)
                {
                    int indexA = (a.Stage == 0) ? -1 : (a.Index ?? int.MaxValue);
                    int indexB = (b.Stage == 0) ? -1 : (b.Index ?? int.MaxValue);
                    cmp = indexA.CompareTo(indexB);
                    if (cmp != 0) return cmp;
                }

                // Fall back to stage then name
                cmp = a.Stage.CompareTo(b.Stage);
                if (cmp != 0) return cmp;
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });

            return sortedPets;
        }

        /// <summary>
        /// Creates a panel for a single pet.
        /// </summary>
        private Panel CreatePetPanel(Pet pet, int y)
        {
            var itemPanel = new Panel
            {
                Location = new Point(0, y),
                Size = new Size(petListPanel.PanelPetList.Width - 20, 56),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                Tag = pet
            };

            itemPanel.Click += (s, e) => SelectPetPanel(itemPanel);

            PictureBox pb = new PictureBox
            {
                Location = new Point(4, 4),
                Size = new Size(48, 48),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = PetUtils.GetAttributeColor(pet.Attribute ?? ""),
                BorderStyle = BorderStyle.FixedSingle
            };

            // Use new sprite loading system with module support
            var sprite = PetUtils.LoadSinglePetSprite(pet.Name, modulePath, module);
            pb.Image = sprite;

            itemPanel.Controls.Add(pb);

            Label lblName = new Label
            {
                Text = pet.Name,
                Location = new Point(60, 4),
                AutoSize = true,
                Font = new Font(Font.FontFamily, 11, FontStyle.Bold),
                ForeColor = Color.DeepSkyBlue
            };

            itemPanel.Controls.Add(lblName);

            // Updated to show Version, Index, and Stage
            string indexDisplay = (pet.Stage == 0) ? "-" : (pet.Index?.ToString() ?? "-");
            Label lblInfo = new Label
            {
                Text = string.Format("Ver. {0} | Idx. {1} | Stage {2}", pet.Version, indexDisplay, pet.Stage),
                Location = new Point(60, 28),
                AutoSize = true,
                Font = new Font(Font.FontFamily, 8, FontStyle.Regular),
                ForeColor = Color.DeepSkyBlue
            };

            itemPanel.Controls.Add(lblInfo);

            return itemPanel;
        }

        #endregion

        #region Selection

        /// <summary>
        /// Selects the given pet panel and loads its data for editing.
        /// </summary>
        internal void SelectPetPanel(Panel panel)
        {
            if (selectedPanel != null)
                selectedPanel.BackColor = Color.White;

            selectedPanel = panel;
            selectedPanel.BackColor = Color.LightBlue;

            selectedPet = panel.Tag as Pet;
            petEditPanel.LoadPet(selectedPet);

            // Update the sprite panel
            spritePanel.CurrentPet = selectedPet;
            spritePanel.CurrentModule = module;
            spritePanel.ModulePath = modulePath;
            spritePanel.RefreshSprites();
        }

        #endregion

        #region Utility

        /// <summary>
        /// Sorts the pets list.
        /// </summary>
        internal void SortPets()
        {
            PetUtils.SortPets(pets);
        }

        #endregion

        #region Button Event Handlers

        private void BtnRemove_Click(object sender, EventArgs e)
        {
            if (selectedPet == null) return;
            var result = MessageBox.Show(
                string.Format(Properties.Resources.ConfirmRemovePet, selectedPet.Name),
                Properties.Resources.Confirmation,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            );
            if (result == DialogResult.Yes)
            {
                pets.Remove(selectedPet);
                selectedPet = null;
                selectedPanel = null;
                SortPets();
                PopulatePetPanel();
                Save();
            }
        }

        private void BtnCopy_Click(object sender, EventArgs e)
        {
            if (selectedPet == null) return;
            copiedPet = PetUtils.ClonePet(selectedPet);
        }

        private void BtnPaste_Click(object sender, EventArgs e)
        {
            if (copiedPet == null) return;
            var newPet = PetUtils.ClonePet(copiedPet);
            pets.Add(newPet);
            SortPets();
            var panel = PopulatePetPanelAndReturnPanel(newPet);
            if (panel != null)
                SelectPetPanel(panel);
            Save();
        }

        /// <summary>Ctrl+C — copy the selected pet into the paste buffer.</summary>
        public void CopySelection() => BtnCopy_Click(this, EventArgs.Empty);

        /// <summary>Ctrl+V — paste a duplicate of the copied pet.</summary>
        public void PasteClipboard() => BtnPaste_Click(this, EventArgs.Empty);

        /// <summary>Edit menu: open the evolutions editor for this module's pets.</summary>
        public void OpenEvolutionsEditor()
        {
            var dlg = new EvolutionsEditorForm(pets, modulePath, module);
            dlg.ShowDialog(this);
        }

        /// <summary>
        /// Tools menu: assign the Index field to every pet following the order they
        /// currently appear in the pet list. Stage 0 (egg) pets are always index -1;
        /// for all other pets the index restarts at 0 for each version and increments
        /// in list order (version 1 → 0,1,2,…, then version 2 → 0,1,2,…).
        /// </summary>
        public void GenerateIndexes()
        {
            if (pets == null || pets.Count == 0)
                return;

            // Use the same ordering shown in the pet list panel.
            var ordered = GetSortedPets();
            var perVersionCounter = new Dictionary<int, int>();

            foreach (var pet in ordered)
            {
                if (pet.Stage == 0)
                {
                    pet.Index = -1;
                    continue;
                }

                int next = perVersionCounter.TryGetValue(pet.Version, out int c) ? c : 0;
                pet.Index = next;
                perVersionCounter[pet.Version] = next + 1;
            }

            SortPets();
            PopulatePetPanel();
            Save();
        }

        private async void BtnAdd_Click(object sender, EventArgs e)
        {
            // Try to load the Digimon catalogue so the Add dialog can offer
            // database-driven options; fall back to Custom-only when offline.
            List<DigimonRecord> records = null;
            Dictionary<string, int> levelMap = null;
            this.Cursor = Cursors.WaitCursor;
            try
            {
                records = await DigimonDbClient.GetAllAsync();
                levelMap = await DigimonDbClient.GetLevelNameToIdAsync();
            }
            catch
            {
                records = null;
                levelMap = null;
            }
            finally
            {
                this.Cursor = Cursors.Default;
            }

            using (var dlg = new AddPetForm(0, records, levelMap))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK
                    || dlg.SelectedStage < 0 || dlg.SelectedStage > 8)
                    return;

                int stage = dlg.SelectedStage;
                var template = OmnipetModuleEditor.Models.PetTemplates.ByStage[stage];
                var newPet = PetUtils.ClonePet(template);
                newPet.Stage = stage;

                var digimon = dlg.SelectedDigimon;
                if (digimon == null)
                {
                    // "Custom" — keep the original blank-template behaviour.
                    newPet.Name = Properties.Resources.PetTab_NewPetName ?? "New Pet";
                }
                else
                {
                    newPet.Name = digimon.NameEnglish
                                  ?? (Properties.Resources.PetTab_NewPetName ?? "New Pet");
                    if (digimon.MinWeight.HasValue)
                        newPet.MinWeight = digimon.MinWeight.Value;
                    newPet.Attribute = DigimonPetFactory.MapAttributeCode(digimon.Attributes);

                    if (dlg.SelectedModule != null && digimon.ExtraData != null
                        && digimon.ExtraData.TryGetValue(dlg.SelectedModule, out var extra))
                    {
                        // Import all of the module's extra-data into the pet,
                        // then pull that module's custom sprite sheets in.
                        newPet = DigimonPetFactory.WithExtraData(newPet, extra);
                        await ImportModuleSpritesAsync(digimon.Id, dlg.SelectedModule, newPet.Name);
                    }
                }

                pets.Add(newPet);
                SortPets();
                var panel = PopulatePetPanelAndReturnPanel(newPet);
                if (panel != null)
                    SelectPetPanel(panel);
                Save();
            }
        }

        /// <summary>
        /// Download the source module's custom sprite sheets for a Digimon and
        /// write them into this module's sprite folders under the pet's name.
        /// Best-effort: formats with no custom sheet are simply skipped.
        /// </summary>
        private async Task ImportModuleSpritesAsync(string digimonId, string sourceModule, string petName)
        {
            var formats = new[]
            {
                ("color", "monsters"),
                ("dot", "monsters_dot"),
                ("hd", "monsters_hidef"),
            };
            string spriteName = SpriteUtils.GetSpriteName(petName, module?.NameFormat);
            this.Cursor = Cursors.WaitCursor;
            try
            {
                foreach (var fmt in formats)
                {
                    var bytes = await DigimonDbClient.DownloadModuleSheetAsync(digimonId, fmt.Item1, sourceModule);
                    if (bytes == null || bytes.Length == 0)
                        continue;
                    var dir = Path.Combine(modulePath, fmt.Item2);
                    Directory.CreateDirectory(dir);
                    File.WriteAllBytes(Path.Combine(dir, spriteName + ".zip"), bytes);
                }
            }
            catch { /* sprite import is best-effort */ }
            finally
            {
                this.Cursor = Cursors.Default;
            }
        }

        private void SearchGo()
        {
            string query = petListPanel.TxtSearch.Text;
            if (string.IsNullOrWhiteSpace(query)) return;
            lastSearchText = query;
            lastSearchIndex = -1;
            SearchPrevNext(1);
        }

        private void SearchPrevNext(int direction)
        {
            string query = petListPanel.TxtSearch.Text;
            if (string.IsNullOrWhiteSpace(query)) return;

            var controls = petListPanel.PanelPetList.Controls;
            int count = controls.Count;
            if (count == 0) return;

            // If search text changed, reset
            if (!string.Equals(query, lastSearchText, StringComparison.OrdinalIgnoreCase))
            {
                lastSearchText = query;
                lastSearchIndex = -1;
            }

            int start = lastSearchIndex + direction;
            for (int i = 0; i < count; i++)
            {
                int idx = ((start + i * direction) % count + count) % count;
                var panel = controls[idx] as Panel;
                if (panel?.Tag is Pet pet && pet.Name != null
                    && pet.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    lastSearchIndex = idx;
                    SelectPetPanel(panel);
                    ScrollToPanel(panel);
                    return;
                }
            }
        }

        private void ScrollToPanel(Panel panel)
        {
            var listPanel = petListPanel.PanelPetList;
            // panel.Top is relative to the visible client area (affected by current scroll).
            // Virtual absolute Y = panel.Top - AutoScrollPosition.Y  (AutoScrollPosition.Y is negative when scrolled down)
            int virtualY = panel.Top - listPanel.AutoScrollPosition.Y;
            listPanel.AutoScrollPosition = new Point(0, virtualY);
        }

        #endregion

        #region Internal Classes

        // Left panel (pet list and buttons)
        private class PetListPanel : UserControl
        {
            public Panel PanelPetList { get; private set; }
            public Button BtnAdd { get; private set; }
            public Button BtnRemove { get; private set; }
            public Button BtnCopy { get; private set; }
            public Button BtnPaste { get; private set; }
            public TextBox TxtSearch { get; private set; }
            public Button BtnGo { get; private set; }
            public Button BtnPrev { get; private set; }
            public Button BtnNext { get; private set; }

            public PetListPanel()
            {
                InitializeComponent();
            }

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

                // Search bar
                var searchPanel = new FlowLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    FlowDirection = FlowDirection.LeftToRight,
                    Padding = new Padding(2, 2, 2, 0),
                    WrapContents = false
                };
                TxtSearch = new TextBox { Width = 130, Margin = new Padding(0, 2, 2, 0) };
                BtnGo = new Button { Text = "Go", Width = 36, Height = 23, Margin = new Padding(0, 1, 2, 0) };
                BtnPrev = new Button { Text = "<", Width = 28, Height = 23, Margin = new Padding(0, 1, 2, 0) };
                BtnNext = new Button { Text = ">", Width = 28, Height = 23, Margin = new Padding(0, 1, 0, 0) };
                searchPanel.Controls.AddRange(new Control[] { TxtSearch, BtnGo, BtnPrev, BtnNext });
                leftLayout.Controls.Add(searchPanel, 0, 0);

                PanelPetList = new Panel
                {
                    Dock = DockStyle.Fill,
                    AutoScroll = true,
                    BackColor = Color.White
                };
                leftLayout.Controls.Add(PanelPetList, 0, 1);

                var panelButtons = new FlowLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    FlowDirection = FlowDirection.LeftToRight,
                    Padding = new Padding(4),
                    AutoSize = false,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    WrapContents = false
                };

                BtnAdd = new Button { Text = "Add", Width = 70, Margin = new Padding(0, 0, 4, 0) };
                BtnRemove = new Button { Text = "Remove", Width = 70, Margin = new Padding(0, 0, 4, 0) };
                BtnCopy = new Button { Text = "Copy", Width = 70, Margin = new Padding(0, 0, 4, 0) };
                BtnPaste = new Button { Text = "Paste", Width = 70, Margin = new Padding(0, 0, 4, 0) };

                panelButtons.Controls.AddRange(new Control[] { BtnAdd, BtnRemove, BtnCopy, BtnPaste });
                leftLayout.Controls.Add(panelButtons, 0, 2);

                this.Controls.Add(leftLayout);
            }
        }

        // Right panel (pet editing)
        /// <summary>encounter_chances as "10/5/5" ("-" for a null entry).</summary>
        private static string EncounterChancesText(int?[] chances)
        {
            if (chances == null) return "";
            return string.Join("/", Array.ConvertAll(chances, c => c.HasValue ? c.Value.ToString() : "-"));
        }

        /// <summary>"10/5/5" back to encounter_chances; blank is null.</summary>
        private static int?[] ParseEncounterChances(string text)
        {
            var words = (text ?? "").Split(new[] { '/', ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) return null;
            var chances = new int?[words.Length];
            for (int i = 0; i < words.Length; i++)
            {
                int n;
                chances[i] = int.TryParse(words[i], out n) ? Math.Max(0, Math.Min(100, n)) : (int?)null;
            }
            return chances;
        }

        private class PetEditPanel : UserControl
        {
            // Remove old sprite-related fields that are now in PetSpritePanel
            // public TableLayoutPanel SpritePanel { get; private set; }
            // public Button BtnRefresh { get; private set; }
            // public Button BtnDownload { get; private set; }
            // public Button BtnImport { get; private set; }

            // Editing fields
            public TextBox TxtName;
            public ComboBox CmbStage;
            public NumericUpDown NumIndex;  // NEW: Index field
            public NumericUpDown NumVersion;
            public CheckBox ChkSpecial;
            public TextBox TxtSpecialKey;
            public MaskedTextBox TxtSleeps;
            public MaskedTextBox TxtWakes;
            public ComboBox CmbAtkMain;
            public ComboBox CmbAtkAlt;
            public ComboBox CmbAtkAlt2;
            public NumericUpDown NumTime;
            public NumericUpDown NumPoopTimer;
            public NumericUpDown NumEnergy;
            public NumericUpDown NumMinWeight;
            public NumericUpDown NumEvolWeight;
            public NumericUpDown NumStomach;
            public NumericUpDown NumHungerLoss;
            public NumericUpDown NumStrengthLoss;
            public NumericUpDown NumHealDoses;
            public NumericUpDown NumPower;
            public NumericUpDown NumRpm;
            public ComboBox CmbAttribute;
            public ComboBox CmbDigisoul;
            public NumericUpDown NumInjuryProbability;
            public NumericUpDown NumEncounterTimeGroup;
            public NumericUpDown NumEncounterOpponentGroup;
            public TextBox TxtEncounterChances;
        public ComboBox CmbPersonality;
            public TextBox TxtTags;
            public NumericUpDown NumConditionHearts;
            public CheckBox ChkJogress;
            public NumericUpDown NumHp;
            public ComboBox CmbAvailability;

            // VB-specific fields - NEW
            public NumericUpDown NumStar;
            public NumericUpDown NumAttack;
            public NumericUpDown NumCriticalTurn;

            private Button btnSave;
            private Button btnCancel;

            private Pet currentPet;

            /// <summary>Explains what each pet stat does. Kept as a field so it
            /// outlives InitializeComponent - a ToolTip that goes out of scope
            /// stops showing.</summary>
            private readonly ToolTip petTips = new ToolTip
            {
                AutoPopDelay = 20000,
                InitialDelay = 300,
                ReshowDelay = 200,
                ShowAlways = true
            };

            // Remove old spriteBoxes list - no longer needed
            // private List<Panel> spriteBoxes = new List<Panel>();

            private Dictionary<int, Image> atkSprites = new Dictionary<int, Image>();
            private Dictionary<int, Image> atkCritSprites = new Dictionary<int, Image>();

            private Button btnEditEvolutions;
            
            // Reference to parent PetTab
            private PetTab ownerPetTab;

            public PetEditPanel()
            {
                InitializeComponent();
            }
            
            /// <summary>
            /// Sets the owner PetTab reference for accessing pets, modulePath, and module.
            /// </summary>
            public void SetOwner(PetTab petTab)
            {
                ownerPetTab = petTab;
            }

            private int FindAtkComboIndex(ComboBox cmb, int number)
            {
                for (int i = 0; i < cmb.Items.Count; i++)
                {
                    if (cmb.Items[i] is AtkComboItem item && item.Number == number)
                        return i;
                }
                // An id with no sprite in atk/ or the shared assets is listed
                // (without a picture) rather than saved back as 0.
                if (number != 0)
                    return cmb.Items.Add(new AtkComboItem(number, null));
                return 0;
            }

            /// <summary>Selects <paramref name="value"/>, adding it to the list
            /// first when the list does not name it, so a value the form does
            /// not offer is kept on save rather than replaced.</summary>
            private static void SelectKeeping(ComboBox cmb, string value, string fallback)
            {
                if (!string.IsNullOrEmpty(value) && !cmb.Items.Contains(value))
                    cmb.Items.Add(value);
                cmb.SelectedItem = string.IsNullOrEmpty(value) ? fallback : value;
            }

            public void LoadPet(Pet pet)
            {
                if (pet == null) return;
                currentPet = pet;

                TxtName.Text = pet.Name ?? "";
                CmbStage.SelectedIndex = pet.Stage;
                
                // Index: Stage 0 pets always have index -1 and cannot be changed
                if (pet.Stage == 0)
                {
                    NumIndex.Value = -1;
                    NumIndex.Enabled = false;
                }
                else
                {
                    // -1 shows "no index" and saves as none.
                    NumIndex.Value = Math.Max(NumIndex.Minimum, pet.Index ?? -1);
                    NumIndex.Enabled = true;
                }
                
                NumVersion.Value = pet.Version;
                ChkSpecial.Checked = pet.Special;
                TxtSpecialKey.Text = pet.SpecialKey ?? "";

                // Normalize to HH:mm format
                TxtSleeps.Text = NormalizeTime(pet.Sleeps);
                TxtWakes.Text = NormalizeTime(pet.Wakes);

                CmbAtkMain.SelectedIndex = FindAtkComboIndex(CmbAtkMain, pet.AtkMain);
                CmbAtkAlt.SelectedIndex = FindAtkComboIndex(CmbAtkAlt, pet.AtkAlt);
                CmbAtkAlt2.SelectedIndex = FindAtkComboIndex(CmbAtkAlt2, pet.AtkAlt2);
                NumTime.Value = Math.Max(NumTime.Minimum, pet.Time);
                NumPoopTimer.Value = Math.Max(NumPoopTimer.Minimum, pet.PoopTimer);
                NumEnergy.Value = Math.Max(NumEnergy.Minimum, pet.Energy);
                NumMinWeight.Value = Math.Max(NumMinWeight.Minimum, pet.MinWeight);
                NumEvolWeight.Value = Math.Max(NumEvolWeight.Minimum, pet.EvolWeight);
                NumStomach.Value = Math.Max(NumStomach.Minimum, pet.Stomach);
                NumHungerLoss.Value = Math.Max(NumHungerLoss.Minimum, pet.HungerLoss);
                NumStrengthLoss.Value = Math.Max(NumStrengthLoss.Minimum, pet.StrengthLoss);
                NumHealDoses.Value = Math.Max(NumHealDoses.Minimum, Math.Min(NumHealDoses.Maximum, pet.HealDoses));
                NumPower.Value = Math.Max(NumPower.Minimum, pet.Power);
                NumRpm.Value = Math.Max(NumRpm.Minimum,
                    Math.Min(NumRpm.Maximum, pet.Rpm));
                CmbAttribute.SelectedItem = pet.Attribute ?? "";
                CmbAttribute.SelectedIndex = (int)PetUtils.JsonToAttributeEnum(pet.Attribute ?? "");
                SelectKeeping(CmbDigisoul, pet.Digisoul, "");
                if (CmbDigisoul.SelectedIndex < 0)
                    CmbDigisoul.SelectedIndex = 0;
                NumInjuryProbability.Value = Math.Min(
                    NumInjuryProbability.Maximum,
                    Math.Max(NumInjuryProbability.Minimum, pet.InjuryProbability));
                NumEncounterTimeGroup.Value = Math.Min(NumEncounterTimeGroup.Maximum, pet.EncounterTimeGroup ?? -1);
                NumEncounterOpponentGroup.Value = Math.Min(NumEncounterOpponentGroup.Maximum, pet.EncounterOpponentGroup ?? -1);
                TxtEncounterChances.Text = EncounterChancesText(pet.EncounterChances);
            SelectKeeping(CmbPersonality, pet.Personality, "Normal");
            if (CmbPersonality.SelectedIndex < 0) CmbPersonality.SelectedIndex = 2;
                TxtTags.Text = pet.Tags != null ? string.Join(", ", pet.Tags) : "";
                NumConditionHearts.Value = Math.Max(NumConditionHearts.Minimum, pet.ConditionHearts);
                ChkJogress.Checked = pet.JogressAvailable;
                NumHp.Value = Math.Max(NumHp.Minimum, pet.Hp);

                // VB-specific fields - NEW
                NumStar.Value = Math.Max(NumStar.Minimum, pet.Star);
                NumAttack.Value = Math.Max(NumAttack.Minimum, pet.Attack);
                NumCriticalTurn.Value = Math.Max(NumCriticalTurn.Minimum, pet.CriticalTurn);

                // Availability — null means Normal; a value the list does not
                // name is kept.
                SelectKeeping(CmbAvailability, pet.Availability, "Normal");
                if (CmbAvailability.SelectedIndex < 0) CmbAvailability.SelectedIndex = 0;
            }

            // Helper to normalize time format
            private string NormalizeTime(string value)
            {
                if (string.IsNullOrWhiteSpace(value)) return "";
                if (TimeSpan.TryParse(value, out var ts))
                    return ts.ToString(@"hh\:mm");
                return value;
            }

            public void SaveToPet(Pet pet)
            {
                if (pet == null) return;

                pet.Name = TxtName.Text;
                pet.Stage = CmbStage.SelectedIndex;
                
                // Index: Stage 0 pets always have index -1 (or none). What the
                // record already says is kept, so saving a form changes only
                // what was edited.
                if (pet.Stage == 0)
                {
                    if (pet.Index.HasValue && pet.Index.Value != -1)
                        pet.Index = -1;
                }
                else
                {
                    int index = (int)NumIndex.Value;
                    // -1 is "no index": none stays none.
                    pet.Index = index >= 0 ? index : (pet.Index < 0 ? pet.Index : (int?)null);
                }

                pet.Version = (int)NumVersion.Value;
                pet.Special = ChkSpecial.Checked;
                string specialKey = TxtSpecialKey.Text;
                pet.SpecialKey = specialKey.Length == 0 && pet.SpecialKey == null ? null : specialKey;

                // Null when empty, invalid or "  :"; an empty string the record
                // already had stays an empty string.
                pet.Sleeps = IsValidTime(TxtSleeps.Text) ? TxtSleeps.Text : (pet.Sleeps == "" ? "" : null);
                pet.Wakes = IsValidTime(TxtWakes.Text) ? TxtWakes.Text : (pet.Wakes == "" ? "" : null);

                pet.AtkMain = (CmbAtkMain.SelectedItem as AtkComboItem)?.Number ?? 0;
                pet.AtkAlt = (CmbAtkAlt.SelectedItem as AtkComboItem)?.Number ?? 0;
                pet.AtkAlt2 = (CmbAtkAlt2.SelectedItem as AtkComboItem)?.Number ?? 0;
                pet.Time = (int)NumTime.Value;
                pet.PoopTimer = (int)NumPoopTimer.Value;
                pet.Energy = (int)NumEnergy.Value;
                pet.MinWeight = (int)NumMinWeight.Value;
                pet.EvolWeight = (int)NumEvolWeight.Value;
                pet.Stomach = (int)NumStomach.Value;
                pet.HungerLoss = (int)NumHungerLoss.Value;
                pet.StrengthLoss = (int)NumStrengthLoss.Value;
                pet.HealDoses = (int)NumHealDoses.Value;
                pet.Power = (int)NumPower.Value;
                pet.Rpm = (int)NumRpm.Value;
                // "Normal" is what an empty personality shows: it stays empty.
                string personality = CmbPersonality.SelectedItem as string ?? "Normal";
                if (!(personality == "Normal" && string.IsNullOrEmpty(pet.Personality)))
                    pet.Personality = personality;
                var tags = TxtTags.Text.Split(',').Select(t => t.Trim())
                    .Where(t => t.Length > 0).Distinct().ToArray();
                pet.Tags = tags.Length > 0 ? tags : null;
                // Written only when changed: Free shows for an empty, absent or
                // unknown attribute, and those stay as they are.
                var attribute = CmbAttribute.SelectedIndex >= 0
                    ? (AttributeEnum)CmbAttribute.SelectedIndex : AttributeEnum.FREE;
                if (attribute != PetUtils.JsonToAttributeEnum(pet.Attribute ?? ""))
                    pet.Attribute = PetUtils.AttributeEnumToJson(attribute);
                string digisoul = CmbDigisoul.SelectedItem as string ?? "";
                if (!(digisoul == "" && pet.Digisoul == null))
                    pet.Digisoul = digisoul;
                pet.InjuryProbability = (int)NumInjuryProbability.Value;
                // -1 is "none" and stays out of the file.
                pet.EncounterTimeGroup = NumEncounterTimeGroup.Value < 0 ? (int?)null : (int)NumEncounterTimeGroup.Value;
                pet.EncounterOpponentGroup = NumEncounterOpponentGroup.Value < 0 ? (int?)null : (int)NumEncounterOpponentGroup.Value;
                pet.EncounterChances = ParseEncounterChances(TxtEncounterChances.Text);
                pet.ConditionHearts = (int)NumConditionHearts.Value;
                pet.JogressAvailable = ChkJogress.Checked;
                pet.Hp = (int)NumHp.Value;

                // VB-specific fields - NEW
                pet.Star = (int)NumStar.Value;
                pet.Attack = (int)NumAttack.Value;
                pet.CriticalTurn = (int)NumCriticalTurn.Value;

                // Availability — "Normal" is the default and isn't saved
                string avail = CmbAvailability.SelectedItem?.ToString();
                if (!(avail == "Normal" && pet.Availability == "Normal"))
                    pet.Availability = (string.IsNullOrEmpty(avail) || avail == "Normal") ? null : avail;
            }

            // Função auxiliar para validar hora no formato HH:mm
            private bool IsValidTime(string value)
            {
                if (string.IsNullOrWhiteSpace(value)) return false;
                if (value.Trim() == "  :" || value.Trim() == ":") return false;
                TimeSpan ts;
                return TimeSpan.TryParse(value, out ts);
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
                rightLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

                var fieldsPanel = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 4,
                    Padding = new Padding(0, 8, 0, 0),
                    AutoScroll = true
                };
                fieldsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80F));
                fieldsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200F));
                fieldsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));
                fieldsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200F));

                int row = 0, col = 0;
                void AddField(string label, Control control, string tip = null)
                {
                    if (col >= 2)
                    {
                        col = 0;
                        row++;
                    }

                    fieldsPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                    var lbl = new Label
                    {
                        Text = label,
                        TextAlign = ContentAlignment.MiddleRight,
                        AutoSize = false,
                        Width = 110,
                        Anchor = AnchorStyles.Right,
                        Margin = new Padding(0, 2, 4, 2)
                    };

                    control.Width = 200;
                    control.Anchor = AnchorStyles.Left;
                    control.Margin = new Padding(0, 2, 8, 2);

                    fieldsPanel.Controls.Add(lbl, col * 2, row);
                    fieldsPanel.Controls.Add(control, col * 2 + 1, row);

                    if (!string.IsNullOrEmpty(tip))
                    {
                        petTips.SetToolTip(lbl, tip);
                        petTips.SetToolTip(control, tip);
                    }

                    col++;
                }

                // Field instantiation
                TxtName = new TextBox();
                ChkSpecial = new CheckBox();
                TxtSpecialKey = new TextBox(); TxtSpecialKey.Enabled = false;
                ChkSpecial.CheckedChanged += (s, e) => TxtSpecialKey.Enabled = ChkSpecial.Checked;
                CmbStage = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
                CmbStage.Items.AddRange(OmnipetModuleEditor.Models.Stages.Names);
                NumIndex = new NumericUpDown { Minimum = -1, Maximum = 9999, Value = 0 };  // NEW: Index field
                NumVersion = new NumericUpDown { Minimum = 0, Value = 1 };
                // The minimums below are 0: a form must not raise a value the
                // module already has (eggs have no timer, many forms lose no
                // hunger or strength, eggs weigh 0).
                NumTime = new NumericUpDown { Minimum = 0, Value = 1, Maximum = 99999 };
                // Which round the special move fires on, and so which round
            // earns +2 AP. Devices with no personality data use Normal.
            CmbPersonality = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            CmbPersonality.Items.AddRange(new object[] {
                "Stoic", "Active", "Normal", "Indoor", "Lazy" });
            CmbPersonality.SelectedIndex = 2;
                TxtTags = new TextBox();
            CmbAttribute = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
                CmbAttribute.Items.AddRange(Enum.GetNames(typeof(OmnipetModuleEditor.Models.AttributeEnum)));
                CmbDigisoul = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
                CmbDigisoul.Items.AddRange(new object[] {
                    "", "Beast", "Bird", "Machine", "Water", "Dragon",
                    "Insect", "Holy", "Dark"
                });
                CmbDigisoul.SelectedIndex = 0;
                NumInjuryProbability = new NumericUpDown
                {
                    Minimum = 0,
                    Maximum = 100,
                    Value = 0
                };
                NumEncounterTimeGroup = new NumericUpDown { Minimum = -1, Maximum = 99, Value = -1 };
                NumEncounterOpponentGroup = new NumericUpDown { Minimum = -1, Maximum = 99, Value = -1 };
                TxtEncounterChances = new TextBox();
                NumEnergy = new NumericUpDown { Minimum = 0, Value = 0 };
                TxtSleeps = new MaskedTextBox { Mask = "00:00" };
                CmbAtkMain = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
                CmbAtkAlt = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
                CmbAtkAlt2 = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
                CmbAtkMain.DrawMode = DrawMode.OwnerDrawFixed;
                CmbAtkMain.ItemHeight = 36;
                CmbAtkMain.DrawItem += AtkCombo_DrawItem;
                CmbAtkAlt.DrawMode = DrawMode.OwnerDrawFixed;
                CmbAtkAlt.ItemHeight = 36;
                CmbAtkAlt.DrawItem += AtkCombo_DrawItem;
                CmbAtkAlt2.DrawMode = DrawMode.OwnerDrawFixed;
                CmbAtkAlt2.ItemHeight = 36;
                CmbAtkAlt2.DrawItem += AtkCombo_DrawItem;
                TxtWakes = new MaskedTextBox { Mask = "00:00" };
                NumPower = new NumericUpDown { Minimum = 0, Value = 0, Maximum = 300 };
                // 0 is "none stated", and the rates on record run 50-85;
                // the ceiling is loose so an unread device can be recorded.
                NumRpm = new NumericUpDown { Minimum = 0, Value = 0, Maximum = 999 };
                NumHp = new NumericUpDown { Minimum = 0, Value = 0 };
                NumHungerLoss = new NumericUpDown { Minimum = 0, Value = 4, Maximum = 99999 };
                NumStomach = new NumericUpDown { Minimum = 0, Value = 4 };
                // Eggs (stage 0) can't eat, so default their stomach to 0 when
                // the stage is set to egg.
                CmbStage.SelectedIndexChanged += (s, e) =>
                {
                    if (CmbStage.SelectedIndex == 0)
                        NumStomach.Value = 0;
                };
                NumStrengthLoss = new NumericUpDown { Minimum = 0, Value = 4, Maximum = 99999 };
                NumMinWeight = new NumericUpDown { Minimum = 0, Value = 5 };
                NumEvolWeight = new NumericUpDown { Minimum = 0, Value = 0, Maximum = 99 };
                NumPoopTimer = new NumericUpDown { Minimum = 0, Value = 3, Maximum = 99999 };
                NumConditionHearts = new NumericUpDown { Minimum = 0, Value = 0 };
                // 0: a form that neither falls sick nor is injured (most Baby I).
                NumHealDoses = new NumericUpDown { Minimum = 0, Value = 1 };
                ChkJogress = new CheckBox();

                // VB-specific fields
                NumStar = new NumericUpDown { Minimum = 0, Value = 0, Maximum = 99 };
                NumAttack = new NumericUpDown { Minimum = 0, Value = 1, Maximum = 99 };
                NumCriticalTurn = new NumericUpDown { Minimum = 0, Value = 0, Maximum = 99 };

                CmbAvailability = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };
                CmbAvailability.Items.AddRange(new object[] { "Normal", "Unobtainable", "Friend" });
                CmbAvailability.SelectedIndex = 0;

                // Add fields - REORGANIZED: Attack and Critical Turn moved to second column
                AddField("Name:", TxtName,
                    "The pet\u2019s name, and the key everything else joins on - evolution targets, battle enemies and friend encounters all name it exactly. It also builds the sprite folder through the module\u2019s Sprite Name Format.");
                AddField("Special:", ChkSpecial,
                    "Marks the pet as gated behind an unlock. With Special Key it means \"nothing can evolve into this, and no egg can hatch it, until that unlock is held\" - which is how \"clear area N to unlock X\" is modelled. One clear is enough, forever.");
                AddField("Stage:", CmbStage,
                    "0 Egg, 1 Baby I, 2 Baby II, 3 Child, 4 Adult, 5 Perfect, 6 Ultimate, 7 Super Ultimate, 8 Super Ultimate+. A stage-0 Digitama only animates frames 0-2, so the full frame table does not apply to it.");
                AddField("Special Key:", TxtSpecialKey,
                    "Name of the unlock that must be held before this pet can be evolved into or hatched. It has to match an unlock declared on the Unlocks tab exactly. For an area clear the convention is area_<area>_ver_<version> (VB and VH keep the older area_<area>_<version>), with this pet\u2019s own version.");
                AddField("Index:", NumIndex,
                    "The pet\u2019s id on the original device or DIM card, used to keep the roster in the order the hardware lists it. Eggs are -1.");
                AddField("Time:", NumTime,
                    "Evolution timer in MINUTES - the engine compares it against minutes elapsed, not hours. The pet becomes eligible to evolve once this much time has passed and an evolution route\u2019s requirements are met.");
                AddField("Version:", NumVersion,
                    "The gameplay line this pet belongs to: which evolutions and unlocks apply. This is not the hardware revision - that is device_version on the Devices tab, and it is what battle protocols and connection requirements key off.");
                AddField("Energy:", NumEnergy,
                    "Maximum DP. DP is spent on battles and training and refilled by Protein.");
                AddField("Attribute:", CmbAttribute,
                    "Battle attribute advantage: Vaccine beats Virus beats Data beats Vaccine. Free neither gains nor suffers. The module’s Battle Attribute Advantage is added to the hit rate, or to Power with \"as Power\".");
                AddField("DigiSoul:", CmbDigisoul,
                    "Native DigiSoul family used by Digivice iC devices. Leave empty when the Digimon has no assigned DigiSoul family.");
            AddField("Personality:", CmbPersonality,
                "The DIM card\u2019s activityType (Stoic, Active, Normal, Indoor, Lazy; Normal when the device has none), kept for the round the Vital Bracelet\u2019s special move fires on. Stored only: the game does not read it yet.");
                AddField("Tags:", TxtTags,
                    "Comma-separated labels an evolution can count defeats of (its Defeat Tag), in the adventure and in versus battles. The PENGZ marks its G-Monsters - the species Metal Garurumon must defeat ten of - with G-Monster.");
                AddField("Sleeps:", TxtSleeps,
                    "Time the pet goes to sleep. It gets drowsy and stops responding, and hunger and strength stop draining while it sleeps.");
                AddField("ATK Main:", CmbAtkMain,
                    "The pet\u2019s regular attack sprite. Stored one HIGHER than the device/firmware id, because 0 is reserved for \"no attack sprite\" - a firmware value of 19 is stored here as 20.");
                AddField("Wakes:", TxtWakes,
                    "Time the pet wakes up and can be interacted with normally again.");
                AddField("ATK Alt:", CmbAtkAlt,
                    "The alternate (big) attack sprite, drawn from the second sprite bank. Same +1 offset as ATK Main.");
                AddField("Power:", NumPower,
                    "Base battle power. Anything above this comes from the module\u2019s Power Bonus Rule - full Strength hearts, stars, level, or a Traited Egg.");
                AddField("RPM:", NumRpm,
                    "The shake rate this Digimon\u2019s charge asks for, on the RPM minigame (Digivice iC). Hitting it exactly throws the Hyper x5 attack row - faster is NOT better. The rates on record run 50 to 85, rising with stage. Leave at 0 on every other minigame: 0 means \"not stated\", and the charge then falls back to a stage/attribute table.");
                AddField("Crit Attack:", CmbAtkAlt2,
                    "Sprite used for the critical attack, on modules that enable one. Same +1 offset as the other two.");
                AddField("HP:", NumHp,
                    "Hit points - how much damage the pet absorbs in a battle before losing. On a device that tracks Vital Values a full bar adds up to +6 on top of this.");
                AddField("Injury Prob.:", NumInjuryProbability,
                    "Base percentage chance, from 0 to 100, that this Digimon is injured after losing a battle.");
                AddField("Encounter Time:", NumEncounterTimeGroup,
                    "Scheduled encounters (the Godzilla devices): the group of the Quests/Events Schedule whose times this Digimon’s encounters come at. -1 for none.");
                AddField("Encounter Opp.:", NumEncounterOpponentGroup,
                    "Scheduled encounters: which pool of a scheduled pooled-encounter event this Digimon meets its opponents from, in turn. -1 for none.");
                AddField("Battle Chances:", TxtEncounterChances,
                    "The Vital Bracelet’s random battle (a Card battle event): this character’s chance in percent of being met by a pet of its version at stage 1-4, 5-7 and 8, as \"10/5/5\" (the card’s three battle pools; \"-\" for none). Each version’s chances should add up to 100 per stage group. Blank for a Digimon not on a card.");
                AddField("Hunger Loss:", NumHungerLoss,
                    "Minutes between losing one hunger heart. HIGHER is slower, so a bigger number is an easier pet to keep. Does not tick while the pet sleeps.");
                AddField("Stomach:", NumStomach,
                    "How many hunger hearts the pet has. Feeding past full counts as overfeeding. Eggs cannot eat, so a stage-0 pet is set to 0 automatically.");
                AddField("Strength Loss:", NumStrengthLoss,
                    "Minutes between losing one strength heart. HIGHER is slower. Does not tick while the pet sleeps.");
                AddField("Min Weight:", NumMinWeight,
                    "The lightest this pet can get. Eggs belong at 0 - a 99 here is usually the importer\u2019s default rather than a rule, except on DMH, where every pet is 99 because the device ignores weight.");
                AddField("Poop Timer:", NumPoopTimer,
                    "Minutes between poops. Leaving poop uncleaned earns care mistakes.");
                AddField("Evol Weight:", NumEvolWeight,
                    "Weight the pet starts at immediately after evolving into this form.");
                AddField("Condition Hearts:", NumConditionHearts,
                    "How many condition hearts this pet has - the care meter, filled by good care and drained by care mistakes. Evolution routes can require a range of it.");
                AddField("Heal Doses:", NumHealDoses,
                    "Doses of medicine needed to cure a sickness or an injury. 0: this form cannot fall sick or be injured.");
                AddField("Jogress Available:", ChkJogress,
                    "Stored in the module but NOT read by the game - it gates nothing. Whether a pet can jogress is decided entirely by the jogress routes on its Evolutions.");
                AddField("Availability:", CmbAvailability,
                    "Normal is an ordinary pet. Unobtainable keeps it out of the roster. Friend declares it a special encounter: beating it registers the friend, which DigiXros requirements then read.");
                AddField("Star:", NumStar,
                    "Vital Bracelet star rating. Under the Star power rule the pet\u2019s battle power is its base plus 16 per star.");
                // VB-specific fields - Attack and Critical Turn in second column
                AddField("Attack:", NumAttack,
                    "Vital Bracelet AP - the damage landed on a won round. The pet\u2019s personality round adds +2 to it.");
                AddField("Critical Turn:", NumCriticalTurn,
                    "Round the critical hit lands on. VBE\u2019s values were generated randomly at import, so they cannot be checked against a card.");

                // Save and Cancel buttons
                btnSave = new Button { Text = "Save", Width = 80, Margin = new Padding(8, 8, 8, 8) };
                btnCancel = new Button { Text = "Cancel", Width = 80, Margin = new Padding(8, 8, 8, 8) };
                var buttonPanel = new FlowLayoutPanel
                {
                    Dock = DockStyle.Top,
                    FlowDirection = FlowDirection.LeftToRight,
                    AutoSize = true
                };
                buttonPanel.Controls.Add(btnSave);
                buttonPanel.Controls.Add(btnCancel);

                // Add button panel to fieldsPanel
                fieldsPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                fieldsPanel.Controls.Add(buttonPanel, 0, ++row);
                fieldsPanel.SetColumnSpan(buttonPanel, 4);

                // Button events
                btnSave.Click += (s, e) =>
                {
                    if (currentPet != null && ownerPetTab != null)
                    {
                        SaveToPet(currentPet);
                        ownerPetTab.SortPets();
                        var panel = ownerPetTab.PopulatePetPanelAndReturnPanel(currentPet);
                        if (panel != null)
                            ownerPetTab.SelectPetPanel(panel);
                        ownerPetTab.Save();
                    }
                };
                btnCancel.Click += (s, e) =>
                {
                    if (currentPet != null)
                        LoadPet(currentPet);
                };

                fieldsPanel.Dock = DockStyle.Fill;
                fieldsPanel.AutoScroll = true;
                rightLayout.Controls.Add(fieldsPanel, 0, 0);

                this.Controls.Add(rightLayout);

                // Remove all the old sprite-related button event handlers - they're now in PetSpritePanel

                // When populating the ComboBox:
                CmbAttribute.Items.Clear();
                CmbAttribute.Items.AddRange(new object[] { "Free", "Data", "Virus", "Vaccine" });

                // Bot�o Edit Evolutions
                btnEditEvolutions = new Button
                {
                    Text = "Edit Evolutions",
                    Width = 140,
                    Height = 32,
                    Margin = new Padding(8, 16, 8, 8),
                    Anchor = AnchorStyles.Right
                };

                // Adicione o bot�o ao final do painel de campos
                var bottomPanel = new FlowLayoutPanel
                {
                    Dock = DockStyle.Bottom,
                    FlowDirection = FlowDirection.RightToLeft,
                    AutoSize = true
                };
                bottomPanel.Controls.Add(btnEditEvolutions);
                this.Controls.Add(bottomPanel);

                // Evento do bot�o
                btnEditEvolutions.Click += (s, e) =>
                {
                    if (ownerPetTab != null)
                    {
                        var dlg = new EvolutionsEditorForm(ownerPetTab.pets, ownerPetTab.modulePath, ownerPetTab.module);
                        dlg.ShowDialog(this);
                    }
                    else
                    {
                        MessageBox.Show("Unable to access pet data. Please try again.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                };
            }

            public void PopulateAtkCombos()
            {
                // Clear and add item 0 (None)
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

            // Custom draw to show sprite + number
            private void AtkCombo_DrawItem(object sender, DrawItemEventArgs e)
            {
                if (e.Index < 0) return;
                var combo = sender as ComboBox;
                var item = combo.Items[e.Index] as AtkComboItem;
                e.DrawBackground();
                int x = e.Bounds.Left + 2;
                if (item.Sprite != null)
                {
                    e.Graphics.DrawImage(item.Sprite, x, e.Bounds.Top + 2, 32, 32);
                    x += 36;
                }
                using (var brush = new SolidBrush(e.ForeColor))
                {
                    e.Graphics.DrawString(item.ToString(), e.Font, brush, x, e.Bounds.Top + 8);
                }
                e.DrawFocusRectangle();
            }

            internal void LoadAtkSprites(string modulePath, string primaryFormat = null)
            {
                this.atkSprites = PetUtils.LoadAtkSprites(modulePath, primaryFormat);
                this.atkCritSprites = PetUtils.LoadAtkCritSprites(modulePath, primaryFormat);
            }
        }

        #endregion
    }
}
