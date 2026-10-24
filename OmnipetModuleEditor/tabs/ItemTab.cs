using OmnipetModuleEditor.Models;
using OmnipetModuleEditor.Utils;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;

namespace OmnipetModuleEditor.Tabs
{
    /// <summary>
    /// Editor tab for managing items in the module.
    /// </summary>
    public partial class ItemTab : UserControl, IListClipboardTarget
    {
        // Fields
        private ListBox lstItems;
        private Button btnAdd;
        private Button btnRemove;
        private Button btnSave;
        private Button btnCancel;

        private TextBox txtName;
        private TextBox txtDescription;
        private ComboBox cmbSpriteName;
        private ComboBox cmbEffect;
        private ComboBox cmbStatus;
        private NumericUpDown numAmount;
        private NumericUpDown numBoostTime;
        private NumericUpDown numWeightGain;
        private NumericUpDown numProtocolValue;
        private NumericUpDown numAttackSprite;
        private ComboBox cmbDigisoul;
        private ComboBox cmbCurrency;
        private CheckBox chkReusable;
        private TextBox txtTarget;
        private NumericUpDown numMinStage;
        private NumericUpDown numMaxStage;
        private DataGridView dgvExtraEffects;
        private DataGridViewComboBoxColumn colExtraCurrency;
        private DataGridViewComboBoxColumn colExtraItem;
        private PictureBox pbSprite;

        private ComboBox cmbComponentItem;

        /// <summary>Explains what each item field does. Kept as a field so
        /// it outlives InitializeComponent - a ToolTip that goes out of
        /// scope stops showing.</summary>
        private readonly ToolTip itemTips = new ToolTip
        {
            AutoPopDelay = 20000,
            InitialDelay = 300,
            ReshowDelay = 200,
            ShowAlways = true
        };

        private List<Item> items = new List<Item>();
        private string modulePath;
        private Module module;
        private Item selectedItem;
        private Item copiedItem;

        /// <summary>
        /// Initializes a new instance of the <see cref="ItemTab"/> class.
        /// </summary>
        public ItemTab()
        {
            InitializeComponent();
            this.Name = "ItemTab";
            // Currencies may have been added on the Shop/Currencies tab since.
            this.VisibleChanged += (s, e) =>
            {
                if (Visible && !string.IsNullOrEmpty(modulePath))
                    PopulateCurrencies();
            };
        }

        /// <summary>The Shop/Currencies tab's currency names, saved or not:
        /// a currency effect pays in one of them.</summary>
        public Func<IEnumerable<string>> CurrencyNamesProvider { get; set; }

        /// <summary>
        /// Sets the module context for this tab.
        /// </summary>
        public void SetModule(string modulePath, Module module)
        {
            this.modulePath = modulePath;
            this.module = module;
            EnsureItemsFolderAndPopulateSprites();
            PopulateEffectCombo();
            PopulateStatusCombo();
            PopulateCurrencies();
            LoadItemsFromJson();
            PopulateItemList();
        }

        #region Layout and UI Initialization

        /// <summary>
        /// Initializes the layout and UI controls.
        /// </summary>
        private void InitializeComponent()
        {
            this.Dock = DockStyle.Fill;

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(8),
                BackColor = SystemColors.Control
            };
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            // The row is the tab's height: an auto-sized row took the item
            // fields' height (about 740 px), and in a shorter window the list's
            // Add/Remove buttons fell below the visible area.
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            // Left panel: List and buttons
            var leftPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1,
                BackColor = SystemColors.ControlLight
            };
            leftPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            leftPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));

            lstItems = new ListBox
            {
                Dock = DockStyle.Fill,
                Font = new Font(FontFamily.GenericSansSerif, 10, FontStyle.Regular)
            };
            lstItems.SelectedIndexChanged += LstItems_SelectedIndexChanged;
            leftPanel.Controls.Add(lstItems, 0, 0);

            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(4),
                AutoSize = false,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false
            };
            btnAdd = new Button { Text = "Add", Width = 70, Margin = new Padding(0, 0, 4, 0) };
            btnRemove = new Button { Text = "Remove", Width = 70, Margin = new Padding(0, 0, 4, 0) };
            btnAdd.Click += BtnAdd_Click;
            btnRemove.Click += BtnRemove_Click;
            buttonPanel.Controls.Add(btnAdd);
            buttonPanel.Controls.Add(btnRemove);
            leftPanel.Controls.Add(buttonPanel, 0, 1);

            mainLayout.Controls.Add(leftPanel, 0, 0);

            // Right panel: Item fields
            var rightPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 2,
                RowCount = 19, // WeightGain, ProtocolValue, AttackSprite, Digisoul, Currency, Reusable, Target, MinStage, MaxStage, then Extra Effects
                Padding = new Padding(16, 8, 8, 8),
                AutoSize = true
            };
            rightPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100F));
            rightPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            int row = 0;

            rightPanel.Controls.Add(new Label { Text = "Name:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, row);
            txtName = new TextBox { Dock = DockStyle.Fill };
            rightPanel.Controls.Add(txtName, 1, row++);

            rightPanel.Controls.Add(new Label { Text = "Description:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, row);
            txtDescription = new TextBox { Dock = DockStyle.Fill, Multiline = true, Height = 48, ScrollBars = ScrollBars.Vertical };
            rightPanel.Controls.Add(txtDescription, 1, row++);

            rightPanel.Controls.Add(new Label { Text = "Sprite Name:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, row);
            cmbSpriteName = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbSpriteName.SelectedIndexChanged += (s, e) => LoadSprite();
            rightPanel.Controls.Add(cmbSpriteName, 1, row++);

            rightPanel.Controls.Add(new Label { Text = "Effect:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, row);
            cmbEffect = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            rightPanel.Controls.Add(cmbEffect, 1, row++);

            rightPanel.Controls.Add(new Label { Text = "Component Item:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, row);
            cmbComponentItem = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Enabled = false };
            rightPanel.Controls.Add(cmbComponentItem, 1, row++);
            cmbComponentItem.Items.Add("");

            rightPanel.Controls.Add(new Label { Text = "Status:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, row);
            cmbStatus = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            rightPanel.Controls.Add(cmbStatus, 1, row++);

            rightPanel.Controls.Add(new Label { Text = "Amount:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, row);
            // From 0: items whose effect takes no amount carry 0 (ACCEL's), and
            // the form must not raise it.
            numAmount = new NumericUpDown { Dock = DockStyle.Left, Minimum = 0, Maximum = 9999, Width = 80, Value = 1 };
            rightPanel.Controls.Add(numAmount, 1, row++);

            rightPanel.Controls.Add(new Label { Text = "Boost Time:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, row);
            numBoostTime = new NumericUpDown { Dock = DockStyle.Left, Minimum = 0, Maximum = 9999, Width = 80, Value = 0 };
            rightPanel.Controls.Add(numBoostTime, 1, row++);

            rightPanel.Controls.Add(new Label { Text = "Weight Gain:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, row);
            numWeightGain = new NumericUpDown { Dock = DockStyle.Left, Minimum = -99, Maximum = 99, Width = 80, Value = 0 };
            rightPanel.Controls.Add(numWeightGain, 1, row++);

            rightPanel.Controls.Add(new Label { Text = "Protocol Value:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, row);
            numProtocolValue = new NumericUpDown { Dock = DockStyle.Left, Minimum = 0, Maximum = 255, Width = 80, Value = 0 };
            rightPanel.Controls.Add(numProtocolValue, 1, row++);

            rightPanel.Controls.Add(new Label { Text = "Attack Sprite:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, row);
            numAttackSprite = new NumericUpDown { Dock = DockStyle.Left, Minimum = 0, Maximum = 999, Width = 80, Value = 0 };
            rightPanel.Controls.Add(numAttackSprite, 1, row++);

            rightPanel.Controls.Add(new Label { Text = "DigiSoul:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, row);
            cmbDigisoul = new ComboBox { Dock = DockStyle.Left, Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbDigisoul.Items.AddRange(Item.DigisoulValues);
            cmbDigisoul.SelectedIndex = 0;
            rightPanel.Controls.Add(cmbDigisoul, 1, row++);

            rightPanel.Controls.Add(new Label { Text = "Currency:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, row);
            cmbCurrency = new ComboBox { Dock = DockStyle.Left, Width = 160, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbCurrency.Items.Add("");
            cmbCurrency.SelectedIndex = 0;
            rightPanel.Controls.Add(cmbCurrency, 1, row++);

            rightPanel.Controls.Add(new Label { Text = "Reusable:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, row);
            chkReusable = new CheckBox { Dock = DockStyle.Left, Text = "Not used up" };
            rightPanel.Controls.Add(chkReusable, 1, row++);

            rightPanel.Controls.Add(new Label { Text = "Target:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, row);
            txtTarget = new TextBox { Dock = DockStyle.Fill };
            rightPanel.Controls.Add(txtTarget, 1, row++);

            rightPanel.Controls.Add(new Label { Text = "Min Stage:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, row);
            numMinStage = new NumericUpDown { Dock = DockStyle.Left, Minimum = 0, Maximum = 9, Width = 80, Value = 0 };
            rightPanel.Controls.Add(numMinStage, 1, row++);

            rightPanel.Controls.Add(new Label { Text = "Max Stage:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, row);
            numMaxStage = new NumericUpDown { Dock = DockStyle.Left, Minimum = 0, Maximum = 9, Width = 80, Value = 0 };
            rightPanel.Controls.Add(numMaxStage, 1, row++);

            rightPanel.Controls.Add(new Label { Text = "Extra Effects:", TextAlign = ContentAlignment.TopRight, Dock = DockStyle.Fill }, 0, row);
            dgvExtraEffects = new DataGridView
            {
                Dock = DockStyle.Fill,
                Height = 130,
                AllowUserToAddRows = true,
                AllowUserToDeleteRows = true,
                RowHeadersWidth = 24,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                EditMode = DataGridViewEditMode.EditOnEnter
            };
            var colEffect = new DataGridViewComboBoxColumn { Name = "Effect", HeaderText = "Effect", FlatStyle = FlatStyle.Flat, FillWeight = 120 };
            colEffect.Items.AddRange(ExtraEffectNames);
            var colStatus = new DataGridViewComboBoxColumn { Name = "Status", HeaderText = "Status", FlatStyle = FlatStyle.Flat, FillWeight = 110 };
            colStatus.Items.Add("");
            foreach (var value in Enum.GetValues(typeof(StatusEnum)))
                colStatus.Items.Add(value.ToString());
            var colAmount = new DataGridViewTextBoxColumn { Name = "Amount", HeaderText = "Amount", ValueType = typeof(int), FillWeight = 60 };
            var colBoost = new DataGridViewTextBoxColumn { Name = "BoostTime", HeaderText = "Boost Time", ValueType = typeof(int), FillWeight = 60 };
            colExtraCurrency = new DataGridViewComboBoxColumn { Name = "Currency", HeaderText = "Currency", FlatStyle = FlatStyle.Flat, FillWeight = 100 };
            colExtraCurrency.Items.Add("");
            var colSoul = new DataGridViewComboBoxColumn { Name = "Digisoul", HeaderText = "DigiSoul", FlatStyle = FlatStyle.Flat, FillWeight = 80 };
            colSoul.Items.AddRange(Item.DigisoulValues);
            // An item row's item: the module's item names, filled on load.
            colExtraItem = new DataGridViewComboBoxColumn { Name = "Item", HeaderText = "Item", FlatStyle = FlatStyle.Flat, FillWeight = 110 };
            colExtraItem.Items.Add("");
            dgvExtraEffects.Columns.AddRange(colEffect, colStatus, colAmount, colBoost, colExtraCurrency, colSoul, colExtraItem);
            // A value the combo does not list (a currency renamed elsewhere)
            // is kept as it is rather than raising a dialog.
            dgvExtraEffects.DataError += (s, e) => { e.ThrowException = false; };
            rightPanel.Controls.Add(dgvExtraEffects, 1, row++);

            // Sprite box
            pbSprite = new PictureBox
            {
                Width = 48,
                Height = 48,
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                Margin = new Padding(4)
            };
            var spritePanel = new Panel
            {
                Height = 56,
                Dock = DockStyle.Top
            };
            spritePanel.Controls.Add(pbSprite);
            rightPanel.Controls.Add(new Label(), 0, row); // empty label for alignment
            rightPanel.Controls.Add(spritePanel, 1, row++);

            // Save and Cancel buttons
            btnSave = new Button { Text = "Save", Width = 80, Margin = new Padding(8, 8, 8, 8) };
            btnCancel = new Button { Text = "Cancel", Width = 80, Margin = new Padding(8, 8, 8, 8) };
            var buttonPanelRight = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true
            };
            buttonPanelRight.Controls.Add(btnSave);
            buttonPanelRight.Controls.Add(btnCancel);
            rightPanel.Controls.Add(new Label(), 0, row); // empty label for alignment
            rightPanel.Controls.Add(buttonPanelRight, 1, row++);

            // The fields scroll when the tab is shorter than they are.
            var rightScroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            rightScroll.Controls.Add(rightPanel);
            mainLayout.Controls.Add(rightScroll, 1, 0);


            // The field labels are anonymous, so pair each control with the
            // label sharing its row rather than restructuring the layout.
            void Tip(Control field, string text)
            {
                itemTips.SetToolTip(field, text);
                var pos = rightPanel.GetPositionFromControl(field);
                if (pos.Row < 0) return;
                var lbl = rightPanel.GetControlFromPosition(0, pos.Row);
                if (lbl != null) itemTips.SetToolTip(lbl, text);
            }

            Tip(txtName,
                "The item\u2019s name. Evolution routes that require an item name it exactly, and an item whose name contains \"Digimental\" is drawn as an Armor evolution on the chart.");
            Tip(txtDescription,
                "Text shown in the inventory. Keep it short - a long line is clipped in the game\u2019s item window.");
            Tip(cmbSpriteName,
                "Which sprite in the module\u2019s item folder represents this item.");
            Tip(cmbEffect,
                "What using the item does.  status_change alters a stat directly.  status_boost stores a timed battle effect.  component consumes several copies to craft the Component Item instead of being used on a pet.  " +
                "The Digivice iC’s: cure heals the Status ailment (sick, injury, or empty for either); bedding sleeps the pet Amount minutes, heals it and forgives one care mistake; accessory is worn on the Status place (head, face, aura) until used again; closeup brings the Digimon up to the screen; evolve turns the pet into Target; random_evolve into one of the next-stage forms its own routes lead to; encounter starts the battle.json special encounter named Target.  " +
                "The Vital Bracelet BE’s: multiplier multiplies the pet’s gains of Status (pp the trophies training earns, vp the Vital Values a Card battle wins; bp, ap and hp have no trained stat in Omnipet yet) by Amount for Boost Time minutes, one such item running at a time; training_limit adds Amount minutes to the Limit timer, which Omnipet does not have yet, so the game refuses it.  " +
                "Anything else is treated as feeding.");
            Tip(chkReusable,
                "Using the item does not use it up: the Digivice iC’s Meat and medicines, its C’mon Switch, and accessories, which are put on and taken off again.");
            Tip(txtTarget,
                "For evolve: the Digimon the pet becomes, by name, on the pet’s own version.  For encounter: the name of the battle.json special encounter the item starts.  Empty otherwise; a ticket with no such encounter is refused and kept.");
            Tip(numMinStage,
                "The lowest stage that can take the item, 0 for any.  The Digivice iC’s hidden-Ultimate items work from Child (3), its Aura on Ultimates (6).");
            Tip(numMaxStage,
                "The highest stage that can take the item, 0 for no limit.  Digimon World’s evolution items work only one stage below their Digimon: Min Stage and Max Stage the same.");            Tip(cmbComponentItem,
                "For a component effect only: the item crafted when the player spends Amount copies of this one.");
            Tip(cmbStatus,
                "Which stat the effect applies to. \"hunger\" and \"strength\" are the feeding stats and are handled as food; any other status is a direct status change or a battle boost.");
            Tip(numAmount,
                "How much the effect is worth - the size of the stat change or boost. For a component item it is instead how many copies are consumed per craft.");
            Tip(numBoostTime,
                "How long the effect lasts. status_boost (a battle item): how many countdowns - one after the area’s boss, or after each battle without sequential rounds, or after each round where battle items are used every round; 0 is not permanent: it ends at the first countdown, as 1 does. multiplier (a Vital Bracelet BE item): minutes; 0 ends at once.");
            Tip(numWeightGain,
                "Weight the pet puts on (or, negative, loses) when it takes this item, whatever the effect. It never drops below the Digimon’s minimum weight, so -99 takes it to its minimum.");
            Tip(cmbCurrency,
                "For a currency effect: the currency the item pays, Amount of it. The list is the Shop/Currencies tab’s.");
            Tip(dgvExtraEffects,
                "Effects applied after the main one, to every pet that took the item (the main effect still decides who can). A status_change row is a plain change of the stat: never refused, no weight, stopping at the stat’s limits, so +99 fills it and -99 empties it. A currency row pays once per use. Leave the list empty for a single-effect item.");
            Tip(cmbDigisoul,
                "A DigiSoul family. With effect dna the item adds that DNA to the pet (Amount of it). With status_boost only pets of that family can use the item, as with a Digivice iC family weapon. Empty means no family.");
            Tip(numAttackSprite,
                "The attack sprite the pet throws while this item\u2019s status_boost is active: an id in the module\u2019s atk folder. 0 keeps the pet\u2019s own.  On the Digivice iC a weapon draws its family\u2019s sprite (Dragon 3, Beast 8, Bird 11) and the any-family weapons draw 4.");
            Tip(numProtocolValue,
                "What the item sends on the module\u2019s battle wire while its status_boost is active. 0 sends nothing.  On the Digivice iC wire it is the equipped weapon\u2019s code in packet 3: a Beast Claw sent 2, a Water Drop 3 and a Fowl Tornado 4, read from a device.");

            this.Controls.Add(mainLayout);

            btnSave.Click += BtnSave_Click;
            btnCancel.Click += BtnCancel_Click;
            cmbEffect.SelectedIndexChanged += (s, e) => UpdateComponentItemCombo();
        }

        #endregion

        #region Data Loading and Population

        /// <summary>
        /// Ensures the items folder exists and populates the sprite combo box.
        /// </summary>
        private void EnsureItemsFolderAndPopulateSprites()
        {
            if (string.IsNullOrEmpty(modulePath))
                return;

            string itemsFolder = Path.Combine(modulePath, "items");
            if (!Directory.Exists(itemsFolder))
                Directory.CreateDirectory(itemsFolder);

            cmbSpriteName.Items.Clear();
            var spriteFiles = Directory.GetFiles(itemsFolder, "*.png")
                .Select(f => Path.GetFileNameWithoutExtension(f))
                .OrderBy(f => f)
                .ToArray();

            cmbSpriteName.Items.AddRange(spriteFiles);
        }

        /// <summary>
        /// Populates the effect combo box.
        /// </summary>
        private void PopulateEffectCombo()
        {
            cmbEffect.Items.Clear();
            cmbEffect.Items.Add(""); // Option for "None" or empty
            foreach (var value in Enum.GetValues(typeof(EffectEnum)))
                cmbEffect.Items.Add(value.ToString());
        }

        /// <summary>The effects an extra row can name: crafting and armor
        /// evolution only make sense as an item's main effect.</summary>
        private static readonly string[] ExtraEffectNames =
            { "", "status_change", "status_boost", "dna", "currency", "devolve", "item" };

        /// <summary>
        /// Fills the Currency combo and the extra effects' Currency column
        /// with the Shop/Currencies tab's names, keeping any name an item
        /// already uses.
        /// </summary>
        private void PopulateCurrencies()
        {
            var names = new List<string> { "" };
            if (CurrencyNamesProvider != null)
                names.AddRange(CurrencyNamesProvider().Where(n => !string.IsNullOrEmpty(n)));
            foreach (var item in items ?? new List<Item>())
            {
                if (!string.IsNullOrEmpty(item.Currency)) names.Add(item.Currency);
                foreach (var row in item.ExtraEffects ?? new List<ItemEffect>())
                    if (!string.IsNullOrEmpty(row.Currency)) names.Add(row.Currency);
            }
            names = names.Distinct().ToList();

            var current = cmbCurrency.SelectedItem?.ToString() ?? "";
            cmbCurrency.Items.Clear();
            cmbCurrency.Items.AddRange(names.ToArray());
            cmbCurrency.SelectedItem = names.Contains(current) ? current : "";

            colExtraCurrency.Items.Clear();
            colExtraCurrency.Items.AddRange(names.ToArray());
        }

        /// <summary>
        /// Populates the status combo box.
        /// </summary>
        private void PopulateStatusCombo()
        {
            cmbStatus.Items.Clear();
            cmbStatus.Items.Add(""); // Option for "None" or empty
            foreach (var value in Enum.GetValues(typeof(StatusEnum)))
                cmbStatus.Items.Add(value.ToString());
            // The BE's All Training Multiplier names four targets.
            cmbStatus.Items.Add("pp,bp,ap,hp");
        }

        /// <summary>
        /// Loads items from the item.json file.
        /// </summary>
        private void LoadItemsFromJson()
        {
            items.Clear();
            lstItems.Items.Clear();
            if (string.IsNullOrEmpty(modulePath))
                return;

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
                            items = JsonSerializer.Deserialize<List<Item>>(itemsElement.GetRawText());
                        }
                        PopulateCurrencies();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error loading item.json: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// Populates the item list box with item names.
        /// </summary>
        private void PopulateItemList()
        {
            lstItems.Items.Clear();
            if (items == null) return;
            foreach (var item in items)
            {
                lstItems.Items.Add(item.Name);
            }
        }

        #endregion

        #region Item Selection and Editing

        /// <summary>
        /// Handles item selection change in the list.
        /// </summary>
        private void LstItems_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lstItems.SelectedIndex < 0 || lstItems.SelectedIndex >= items.Count)
            {
                selectedItem = null;
                ClearFields();
                return;
            }
            selectedItem = items[lstItems.SelectedIndex];
            LoadFieldsFromItem(selectedItem);
        }

        /// <summary>
        /// Loads the selected item's fields into the UI.
        /// </summary>
        private void LoadFieldsFromItem(Item item)
        {
            txtName.Text = item.Name ?? "";
            txtDescription.Text = item.Description ?? "";
            // A sprite or effect the lists do not name is added rather than
            // left to the previous item's selection (which saving would
            // then write onto this one); no sprite selects nothing.
            if (string.IsNullOrEmpty(item.SpriteName))
                cmbSpriteName.SelectedIndex = -1;
            else
            {
                if (!cmbSpriteName.Items.Contains(item.SpriteName))
                    cmbSpriteName.Items.Add(item.SpriteName);
                cmbSpriteName.SelectedItem = item.SpriteName;
            }
            if (!string.IsNullOrEmpty(item.Effect) && !cmbEffect.Items.Contains(item.Effect))
                cmbEffect.Items.Add(item.Effect);
            cmbEffect.SelectedItem = string.IsNullOrEmpty(item.Effect) ? "" : item.Effect;
            // A status the list does not name (another multiplier's targets)
            // is kept rather than lost on save.
            if (!string.IsNullOrEmpty(item.Status) && !cmbStatus.Items.Contains(item.Status))
                cmbStatus.Items.Add(item.Status);
            cmbStatus.SelectedItem = string.IsNullOrEmpty(item.Status) ? "" : item.Status;
            numAmount.Value = Math.Max(numAmount.Minimum, Math.Min(numAmount.Maximum, item.Amount));
            numBoostTime.Value = Math.Max(numBoostTime.Minimum, Math.Min(numBoostTime.Maximum, item.BoostTime));
            numWeightGain.Value = Math.Max(numWeightGain.Minimum, Math.Min(numWeightGain.Maximum, item.WeightGain));
            numProtocolValue.Value = Math.Max(numProtocolValue.Minimum, Math.Min(numProtocolValue.Maximum, item.ProtocolValue));
            numAttackSprite.Value = Math.Max(numAttackSprite.Minimum, Math.Min(numAttackSprite.Maximum, item.AttackSprite));
            cmbDigisoul.SelectedItem = Array.IndexOf(Item.DigisoulValues, item.Digisoul ?? "") >= 0 ? (item.Digisoul ?? "") : "";
            cmbCurrency.SelectedItem = cmbCurrency.Items.Contains(item.Currency ?? "") ? (item.Currency ?? "") : "";
            chkReusable.Checked = item.Reusable;
            txtTarget.Text = item.Target ?? "";
            numMinStage.Value = Math.Max(numMinStage.Minimum, Math.Min(numMinStage.Maximum, item.MinStage));
            numMaxStage.Value = Math.Max(numMaxStage.Minimum, Math.Min(numMaxStage.Maximum, item.MaxStage));
            LoadExtraEffects(item.ExtraEffects);
            UpdateComponentItemCombo();
            if (!string.IsNullOrEmpty(item.ComponentItem) && !cmbComponentItem.Items.Contains(item.ComponentItem))
                cmbComponentItem.Items.Add(item.ComponentItem);
            cmbComponentItem.SelectedItem = string.IsNullOrEmpty(item.ComponentItem) ? "" : item.ComponentItem;
            LoadSprite();
        }

        /// <summary>
        /// Clears all item fields in the UI.
        /// </summary>
        private void ClearFields()
        {
            txtName.Text = "";
            txtDescription.Text = "";
            cmbSpriteName.SelectedIndex = -1;
            cmbEffect.SelectedIndex = 0;
            cmbStatus.SelectedIndex = 0;
            numAmount.Value = 1;
            numBoostTime.Value = 0;
            numWeightGain.Value = 0;
            numProtocolValue.Value = 0;
            numAttackSprite.Value = 0;
            cmbDigisoul.SelectedIndex = 0;
            cmbCurrency.SelectedIndex = 0;
            chkReusable.Checked = false;
            txtTarget.Text = "";
            numMinStage.Value = 0;
            numMaxStage.Value = 0;
            dgvExtraEffects.Rows.Clear();
            pbSprite.Image = null;
            cmbComponentItem.SelectedIndex = 0;
        }

        /// <summary>
        /// Loads the sprite image for the selected sprite name.
        /// </summary>
        private void LoadSprite()
        {
            pbSprite.Image = null;
            if (string.IsNullOrEmpty(modulePath) || cmbSpriteName.SelectedItem == null)
                return;
            string spriteName = cmbSpriteName.SelectedItem.ToString();
            if (string.IsNullOrWhiteSpace(spriteName))
                return;
            string spritePath = Path.Combine(modulePath, "items", spriteName + ".png");
            if (File.Exists(spritePath))
            {
                try
                {
                    using (var fs = new FileStream(spritePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        var img = Image.FromStream(fs);
                        pbSprite.Image = new Bitmap(img);
                    }
                }
                catch
                {
                    pbSprite.Image = null;
                }
            }
        }

        #endregion

        #region Button Events

        /// <summary>
        /// Handles the Add button click event.
        /// </summary>
        private void BtnAdd_Click(object sender, EventArgs e)
        {
            var newItem = new Item
            {
                Name = "New Item",
                Description = "",
                SpriteName = "",
                Effect = "",
                Status = "",
                Amount = 1,
                BoostTime = 0,
                WeightGain = 0,
                ProtocolValue = 0,
                AttackSprite = 0,
                Digisoul = "",
                Id = ""
            };
            items.Add(newItem);
            PopulateItemList();
            lstItems.SelectedIndex = items.Count - 1;
        }

        /// <summary>Ctrl+C — copy the selected item into the paste buffer.</summary>
        public void CopySelection()
        {
            if (lstItems.SelectedIndex < 0 || lstItems.SelectedIndex >= items.Count)
                return;
            copiedItem = ClipboardListUtils.DeepClone(items[lstItems.SelectedIndex]);
        }

        /// <summary>Ctrl+V — paste a duplicate of the copied item.</summary>
        public void PasteClipboard()
        {
            if (copiedItem == null) return;
            var pasted = ClipboardListUtils.DeepClone(copiedItem);
            // A copy is a new item: it gets its own id (as pasted quests and
            // events do), not the original's.
            pasted.Id = Guid.NewGuid().ToString();
            items.Add(pasted);
            PopulateItemList();
            lstItems.SelectedIndex = items.Count - 1;
        }

        /// <summary>
        /// Handles the Remove button click event.
        /// </summary>
        private void BtnRemove_Click(object sender, EventArgs e)
        {
            if (lstItems.SelectedIndex < 0 || lstItems.SelectedIndex >= items.Count)
                return;
            var result = MessageBox.Show(
                $"Do you want to remove the item \"{items[lstItems.SelectedIndex].Name}\"?",
                "Confirmation",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            );
            if (result == DialogResult.Yes)
            {
                items.RemoveAt(lstItems.SelectedIndex);
                PopulateItemList();
                ClearFields();
            }
        }

        /// <summary>
        /// Handles the Save button click event.
        /// </summary>
        private void BtnSave_Click(object sender, EventArgs e)
        {
            if (selectedItem == null || lstItems.SelectedIndex < 0)
                return;

            WriteFieldsToItem(selectedItem);

            lstItems.Items[lstItems.SelectedIndex] = selectedItem.Name;
        }

        /// <summary>
        /// Handles the Cancel button click event.
        /// </summary>
        private void BtnCancel_Click(object sender, EventArgs e)
        {
            if (selectedItem != null)
                LoadFieldsFromItem(selectedItem);
        }

        #endregion

        #region Save to File

        /// <summary>
        /// Saves the current item list to item.json.
        /// </summary>
        public void Save()
        {
            if (string.IsNullOrEmpty(modulePath) || items == null)
                return;

            // Update selected item with current fields
            if (selectedItem != null && lstItems.SelectedIndex >= 0)
                WriteFieldsToItem(selectedItem);

            // Ensure all items have an id
            foreach (var item in items)
            {
                if (string.IsNullOrWhiteSpace(item.Id))
                    item.Id = Guid.NewGuid().ToString();
            }

            string itemPath = Path.Combine(modulePath, "item.json");
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
            var obj = new { item = items };
            try
            {
                string json = JsonSerializer.Serialize(obj, options);
                File.WriteAllText(itemPath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error saving item.json: " + ex.Message);
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>Copies the edited fields onto <paramref name="item"/>.</summary>
        private void WriteFieldsToItem(Item item)
        {
            item.Name = txtName.Text;
            item.Description = txtDescription.Text;
            string spriteName = cmbSpriteName.SelectedItem?.ToString() ?? "";
            item.SpriteName = spriteName == "" && item.SpriteName == null ? null : spriteName;
            item.Effect = cmbEffect.SelectedItem?.ToString() ?? "";
            item.Status = cmbStatus.SelectedItem?.ToString() ?? "";
            item.Amount = (int)numAmount.Value;
            item.BoostTime = (int)numBoostTime.Value;
            item.WeightGain = (int)numWeightGain.Value;
            item.ProtocolValue = (int)numProtocolValue.Value;
            item.AttackSprite = (int)numAttackSprite.Value;
            item.Digisoul = cmbDigisoul.SelectedItem?.ToString() ?? "";
            // Empty stays as the record had it (absent or "").
            string componentItem = cmbComponentItem.SelectedItem?.ToString() ?? "";
            item.ComponentItem = componentItem == "" && item.ComponentItem == null ? null : componentItem;
            // Unused, these stay out of the file.
            var currency = cmbCurrency.SelectedItem?.ToString() ?? "";
            item.Currency = currency == "" ? null : currency;
            // No rows: none, or the empty list the record already had.
            var extraEffects = ReadExtraEffects();
            item.ExtraEffects = extraEffects == null && item.ExtraEffects != null && item.ExtraEffects.Count == 0
                ? item.ExtraEffects : extraEffects;
            item.Reusable = chkReusable.Checked;
            var target = txtTarget.Text.Trim();
            item.Target = target == "" ? null : target;
            item.MinStage = (int)numMinStage.Value;
            item.MaxStage = (int)numMaxStage.Value;
        }

        /// <summary>Shows <paramref name="rows"/> in the extra effects grid.</summary>
        private void LoadExtraEffects(List<ItemEffect> rows)
        {
            dgvExtraEffects.Rows.Clear();
            colExtraItem.Items.Clear();
            colExtraItem.Items.Add("");
            foreach (var name in ItemNames().Concat((rows ?? new List<ItemEffect>()).Select(r => r.Item ?? ""))
                                            .Where(n => !string.IsNullOrEmpty(n)).Distinct())
                colExtraItem.Items.Add(name);
            foreach (var row in rows ?? new List<ItemEffect>())
            {
                if (!string.IsNullOrEmpty(row.Currency) && !colExtraCurrency.Items.Contains(row.Currency))
                    colExtraCurrency.Items.Add(row.Currency);
                dgvExtraEffects.Rows.Add(row.Effect ?? "", row.Status ?? "", row.Amount, row.BoostTime,
                    row.Currency ?? "", row.Digisoul ?? "", row.Item ?? "");
            }
        }

        /// <summary>The grid's rows as extra effects; null when there are
        /// none, so a single-effect item keeps no extra_effects key. A row
        /// with no effect is dropped.</summary>
        private List<ItemEffect> ReadExtraEffects()
        {
            dgvExtraEffects.EndEdit();
            var rows = new List<ItemEffect>();
            foreach (DataGridViewRow gridRow in dgvExtraEffects.Rows)
            {
                if (gridRow.IsNewRow) continue;
                string Text(string column) => gridRow.Cells[column].Value?.ToString() ?? "";
                int Number(string column) => int.TryParse(Text(column), out var n) ? n : 0;
                var effect = Text("Effect");
                if (effect == "") continue;
                rows.Add(new ItemEffect
                {
                    Effect = effect,
                    Status = Text("Status"),
                    Amount = Number("Amount"),
                    BoostTime = Number("BoostTime"),
                    Currency = Text("Currency") == "" ? null : Text("Currency"),
                    Digisoul = Text("Digisoul") == "" ? null : Text("Digisoul"),
                    Item = Text("Item") == "" ? null : Text("Item")
                });
            }
            return rows.Count > 0 ? rows : null;
        }

        /// <summary>The module's item names as this tab holds them, saved or
        /// not: the Shop tab sells from this list.</summary>
        public List<string> ItemNames()
        {
            return items == null ? new List<string>() : items.Select(i => i.Name).ToList();
        }

        /// <summary>
        /// Updates the component item combo box based on the selected effect.
        /// </summary>
        private void UpdateComponentItemCombo()
        {
            var effect = cmbEffect.SelectedItem?.ToString();
            cmbComponentItem.Items.Clear();
            cmbComponentItem.Items.Add("");
            if (effect == "component")
            {
                foreach (var item in items)
                    cmbComponentItem.Items.Add(item.Name);
                cmbComponentItem.Enabled = true;
            }
            else
            {
                cmbComponentItem.Enabled = false;
                cmbComponentItem.SelectedIndex = 0;
            }
        }

        #endregion
    }
}