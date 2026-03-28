using OmnipetModuleEditor.Controls;
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
using System.Windows.Forms;

namespace OmnipetModuleEditor.Tabs
{
    /// <summary>
    /// Tab for managing and editing pets in the module.
    /// </summary>
    public partial class PetTab : UserControl
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
        public PetTab()
        {
            InitializeComponent();
            petListPanel.BtnRemove.Click += BtnRemove_Click;
            petListPanel.BtnCopy.Click += BtnCopy_Click;
            petListPanel.BtnPaste.Click += BtnPaste_Click;
            petListPanel.BtnAdd.Click += BtnAdd_Click;
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
            petEditPanel.LoadAtkSprites(modulePath);
            petEditPanel.PopulateAtkCombos();
            PopulatePetPanel();
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
            var scrollPos = petListPanel.PanelPetList.AutoScrollPosition;
            petListPanel.PanelPetList.SuspendLayout();
            petListPanel.PanelPetList.Controls.Clear();
            int y = 0;
            
            // Sort pets using the same logic as PetUtils.SortPets
            var sortedPets = GetSortedPets();
            
            foreach (var pet in sortedPets)
            {
                var petPanel = CreatePetPanel(pet, y);
                petListPanel.PanelPetList.Controls.Add(petPanel);
                y += 56;
            }
            petListPanel.PanelPetList.AutoScrollMinSize = new Size(0, y);
            petListPanel.PanelPetList.ResumeLayout(true);
            petListPanel.PanelPetList.AutoScrollPosition = new Point(-scrollPos.X, -scrollPos.Y);
        }

        /// <summary>
        /// Populates the pet list and returns the panel for a specific pet.
        /// </summary>
        internal Panel PopulatePetPanelAndReturnPanel(Pet petToSelect = null)
        {
            var scrollPos = petListPanel.PanelPetList.AutoScrollPosition;
            petListPanel.PanelPetList.SuspendLayout();
            petListPanel.PanelPetList.Controls.Clear();
            int y = 0;
            Panel selected = null;
            
            // Sort pets using the same logic as PetUtils.SortPets
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
            petListPanel.PanelPetList.AutoScrollPosition = new Point(-scrollPos.X, -scrollPos.Y);
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
            newPet.Name += Properties.Resources.PetTab_CopySuffix ?? " Copy";
            pets.Add(newPet);
            SortPets();
            var panel = PopulatePetPanelAndReturnPanel(newPet);
            if (panel != null)
                SelectPetPanel(panel);
            Save();
        }

        private void BtnAdd_Click(object sender, EventArgs e)
        {
            using (var dlg = new StageSelectForm())
            {
                if (dlg.ShowDialog(this) == DialogResult.OK && dlg.SelectedStage >= 0 && dlg.SelectedStage <= 8)
                {
                    var template = OmnipetModuleEditor.Models.PetTemplates.ByStage[dlg.SelectedStage];
                    var newPet = PetUtils.ClonePet(template);
                    newPet.Name = Properties.Resources.PetTab_NewPetName ?? "New Pet";
                    newPet.Stage = dlg.SelectedStage;
                    pets.Add(newPet);
                    SortPets();
                    var panel = PopulatePetPanelAndReturnPanel(newPet);
                    if (panel != null)
                        SelectPetPanel(panel);
                    Save();
                }
            }
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
                    RowCount = 2,
                    BackColor = SystemColors.ControlLight
                };
                leftLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
                leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));

                PanelPetList = new Panel
                {
                    Dock = DockStyle.Fill,
                    AutoScroll = true,
                    BackColor = Color.White
                };
                leftLayout.Controls.Add(PanelPetList, 0, 0);

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
                leftLayout.Controls.Add(panelButtons, 0, 1);

                this.Controls.Add(leftLayout);
            }
        }

        // Right panel (pet editing)
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
            public ComboBox CmbAttribute;
            public NumericUpDown NumConditionHearts;
            public CheckBox ChkJogress;
            public NumericUpDown NumHp;

            // VB-specific fields - NEW
            public NumericUpDown NumStar;
            public NumericUpDown NumAttack;
            public NumericUpDown NumCriticalTurn;

            private Button btnSave;
            private Button btnCancel;

            private Pet currentPet;

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
                    NumIndex.Value = Math.Max(NumIndex.Minimum, pet.Index ?? 0);
                    NumIndex.Enabled = true;
                }
                
                NumVersion.Value = pet.Version;
                ChkSpecial.Checked = pet.Special;
                TxtSpecialKey.Text = pet.SpecialKey ?? "";

                // Normalize to HH:mm format
                TxtSleeps.Text = NormalizeTime(pet.Sleeps);
                TxtWakes.Text = NormalizeTime(pet.Wakes);

                CmbAtkMain.SelectedIndex = Math.Max(0, Math.Min(pet.AtkMain, 300));
                CmbAtkAlt.SelectedIndex = Math.Max(0, Math.Min(pet.AtkAlt, 300));
                CmbAtkAlt2.SelectedIndex = Math.Max(0, Math.Min(pet.AtkAlt2, 300));
                NumTime.Value = Math.Max(NumTime.Minimum, pet.Time);
                NumPoopTimer.Value = Math.Max(NumPoopTimer.Minimum, pet.PoopTimer);
                NumEnergy.Value = Math.Max(NumEnergy.Minimum, pet.Energy);
                NumMinWeight.Value = Math.Max(NumMinWeight.Minimum, pet.MinWeight);
                NumEvolWeight.Value = Math.Max(NumEvolWeight.Minimum, pet.EvolWeight);
                NumStomach.Value = Math.Max(NumStomach.Minimum, pet.Stomach);
                NumHungerLoss.Value = Math.Max(NumHungerLoss.Minimum, pet.HungerLoss);
                NumStrengthLoss.Value = Math.Max(NumStrengthLoss.Minimum, pet.StrengthLoss);
                NumHealDoses.Value = Math.Max(NumHealDoses.Minimum, pet.HealDoses);
                NumPower.Value = Math.Max(NumPower.Minimum, pet.Power);
                CmbAttribute.SelectedItem = pet.Attribute ?? "";
                CmbAttribute.SelectedIndex = (int)PetUtils.JsonToAttributeEnum(pet.Attribute ?? "");
                NumConditionHearts.Value = Math.Max(NumConditionHearts.Minimum, pet.ConditionHearts);
                ChkJogress.Checked = pet.JogressAvaliable;
                NumHp.Value = Math.Max(NumHp.Minimum, pet.Hp);

                // VB-specific fields - NEW
                NumStar.Value = Math.Max(NumStar.Minimum, pet.Star);
                NumAttack.Value = Math.Max(NumAttack.Minimum, pet.Attack);
                NumCriticalTurn.Value = Math.Max(NumCriticalTurn.Minimum, pet.CriticalTurn);
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
                
                // Index: Stage 0 pets always have index -1 (or null)
                if (pet.Stage == 0)
                {
                    pet.Index = null;  // Stage 0 doesn't need index
                }
                else
                {
                    pet.Index = (int)NumIndex.Value;
                }
                
                pet.Version = (int)NumVersion.Value;
                pet.Special = ChkSpecial.Checked;
                pet.SpecialKey = TxtSpecialKey.Text;

                // Salva null se vazio, inválido ou igual a "  :"
                pet.Sleeps = IsValidTime(TxtSleeps.Text) ? TxtSleeps.Text : null;
                pet.Wakes = IsValidTime(TxtWakes.Text) ? TxtWakes.Text : null;

                pet.AtkMain = CmbAtkMain.SelectedIndex;
                pet.AtkAlt = CmbAtkAlt.SelectedIndex;
                pet.AtkAlt2 = CmbAtkAlt2.SelectedIndex;  // NEW: Save AtkAlt2
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
                if (CmbAttribute.SelectedIndex >= 0)
                    pet.Attribute = PetUtils.AttributeEnumToJson((AttributeEnum)CmbAttribute.SelectedIndex);
                else
                    pet.Attribute = "";
                pet.ConditionHearts = (int)NumConditionHearts.Value;
                pet.JogressAvaliable = ChkJogress.Checked;
                pet.Hp = (int)NumHp.Value;

                // VB-specific fields - NEW
                pet.Star = (int)NumStar.Value;
                pet.Attack = (int)NumAttack.Value;
                pet.CriticalTurn = (int)NumCriticalTurn.Value;
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
                void AddField(string label, Control control)
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

                    col++;
                }

                // Field instantiation
                TxtName = new TextBox();
                ChkSpecial = new CheckBox();
                TxtSpecialKey = new TextBox(); TxtSpecialKey.Enabled = false;
                ChkSpecial.CheckedChanged += (s, e) => TxtSpecialKey.Enabled = ChkSpecial.Checked;
                CmbStage = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
                CmbStage.Items.AddRange(Enum.GetNames(typeof(OmnipetModuleEditor.Models.StageEnum)));
                NumIndex = new NumericUpDown { Minimum = -1, Maximum = 9999, Value = 0 };  // NEW: Index field
                NumVersion = new NumericUpDown { Minimum = 0, Value = 1 };
                NumTime = new NumericUpDown { Minimum = 1, Value = 1, Maximum = 99999 };
                CmbAttribute = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
                CmbAttribute.Items.AddRange(Enum.GetNames(typeof(OmnipetModuleEditor.Models.AttributeEnum)));
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
                NumHp = new NumericUpDown { Minimum = 0, Value = 0 };
                NumHungerLoss = new NumericUpDown { Minimum = 2, Value = 4, Maximum = 99999 };
                NumStomach = new NumericUpDown { Minimum = 2, Value = 4 };
                NumStrengthLoss = new NumericUpDown { Minimum = 2, Value = 4, Maximum = 99999 };
                NumMinWeight = new NumericUpDown { Minimum = 5, Value = 5 };
                NumEvolWeight = new NumericUpDown { Minimum = 0, Value = 0, Maximum = 99 };
                NumPoopTimer = new NumericUpDown { Minimum = 3, Value = 3, Maximum = 99999 };
                NumConditionHearts = new NumericUpDown { Minimum = 0, Value = 0 };
                NumHealDoses = new NumericUpDown { Minimum = 1, Value = 1 };
                ChkJogress = new CheckBox();

                // VB-specific fields
                NumStar = new NumericUpDown { Minimum = 0, Value = 0, Maximum = 99 };
                NumAttack = new NumericUpDown { Minimum = 0, Value = 1, Maximum = 99 };
                NumCriticalTurn = new NumericUpDown { Minimum = 0, Value = 0, Maximum = 99 };

                // Add fields - REORGANIZED: Attack and Critical Turn moved to second column
                AddField("Name:", TxtName);
                AddField("Special:", ChkSpecial);
                AddField("Stage:", CmbStage);
                AddField("Special Key:", TxtSpecialKey);
                AddField("Index:", NumIndex);
                AddField("Time:", NumTime);
                AddField("Version:", NumVersion);
                AddField("Energy:", NumEnergy);
                AddField("Attribute:", CmbAttribute);
                AddField("Sleeps:", TxtSleeps);
                AddField("ATK Main:", CmbAtkMain);
                AddField("Wakes:", TxtWakes);
                AddField("ATK Alt:", CmbAtkAlt);
                AddField("Power:", NumPower);
                AddField("ATK Alt 2:", CmbAtkAlt2);
                AddField("HP:", NumHp);
                AddField("Hunger Loss:", NumHungerLoss);
                AddField("Stomach:", NumStomach);
                AddField("Strength Loss:", NumStrengthLoss);
                AddField("Min Weight:", NumMinWeight);
                AddField("Poop Timer:", NumPoopTimer);
                AddField("Evol Weight:", NumEvolWeight);
                AddField("Condition Hearts:", NumConditionHearts);
                AddField("Heal Doses:", NumHealDoses);
                AddField("Jogress Available:", ChkJogress);
                AddField("Star:", NumStar);
                // VB-specific fields - Attack and Critical Turn in second column
                AddField("Attack:", NumAttack);
                AddField("Critical Turn:", NumCriticalTurn);

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

            internal void LoadAtkSprites(string modulePath)
            {
                this.atkSprites = PetUtils.LoadAtkSprites(modulePath);
                this.atkCritSprites = PetUtils.LoadAtkCritSprites(modulePath);
            }
        }

        #endregion
    }
}