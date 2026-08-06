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
    /// Editor tab for managing redeemable passwords (codes.json).
    /// A password grants an item, a pet, an unlock, or starts a special
    /// encounter, gated by a cooldown (0 = none, -1 = one use only).
    /// </summary>
    public partial class PasswordTab : UserControl, IListClipboardTarget
    {
        // Fields
        private ListBox lstPasswords;
        private Button btnAdd;
        private Button btnRemove;
        private Button btnSave;
        private Button btnCancel;

        private TextBox txtName;
        private TextBox txtCode;
        private ComboBox cmbType;
        private ComboBox cmbItem;
        private NumericUpDown numAmount;
        private ComboBox cmbPet;
        private ComboBox cmbPetVersion;
        private ComboBox cmbUnlock;
        private NumericUpDown numArea;
        private NumericUpDown numCooldown;
        private ToolTip toolTip;

        private List<Password> passwords = new List<Password>();
        private string modulePath;
        private Module module;
        private Password selectedPassword;
        private Password copiedPassword;

        // (name, version) pairs from monster.json, stage > 0
        private List<Tuple<string, int>> petEntries = new List<Tuple<string, int>>();

        public PasswordTab()
        {
            InitializeComponent();
            this.Name = "PasswordTab";
        }

        /// <summary>
        /// Sets the module context for this tab.
        /// </summary>
        public void SetModule(string modulePath, Module module)
        {
            this.modulePath = modulePath;
            this.module = module;
            PopulateTypeCombo();
            PopulateItemCombo();
            PopulatePetCombos();
            PopulateUnlockCombo();
            LoadPasswordsFromJson();
            PopulatePasswordList();
            UpdateTypeFields();
        }

        #region Layout and UI Initialization

        private void InitializeComponent()
        {
            this.Dock = DockStyle.Fill;

            toolTip = new ToolTip();

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

            lstPasswords = new ListBox
            {
                Dock = DockStyle.Fill,
                Font = new Font(FontFamily.GenericSansSerif, 10, FontStyle.Regular)
            };
            lstPasswords.SelectedIndexChanged += LstPasswords_SelectedIndexChanged;
            leftPanel.Controls.Add(lstPasswords, 0, 0);

            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(4),
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

            // Right panel: Password fields
            var rightPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 11,
                Padding = new Padding(16, 8, 8, 8),
                AutoSize = true
            };
            rightPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100F));
            rightPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            int row = 0;

            rightPanel.Controls.Add(new Label { Text = "Name:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, row);
            txtName = new TextBox { Dock = DockStyle.Fill };
            rightPanel.Controls.Add(txtName, 1, row++);

            rightPanel.Controls.Add(new Label { Text = "Code:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, row);
            txtCode = new TextBox { Dock = DockStyle.Fill, CharacterCasing = CharacterCasing.Upper };
            toolTip.SetToolTip(txtCode, "Letters and/or numbers the player types in the game's Specials menu.");
            rightPanel.Controls.Add(txtCode, 1, row++);

            rightPanel.Controls.Add(new Label { Text = "Type:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, row);
            cmbType = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbType.SelectedIndexChanged += (s, e) => UpdateTypeFields();
            rightPanel.Controls.Add(cmbType, 1, row++);

            rightPanel.Controls.Add(new Label { Text = "Item:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, row);
            cmbItem = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            rightPanel.Controls.Add(cmbItem, 1, row++);

            rightPanel.Controls.Add(new Label { Text = "Amount:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, row);
            numAmount = new NumericUpDown { Dock = DockStyle.Left, Minimum = 1, Maximum = 9999, Width = 80, Value = 1 };
            rightPanel.Controls.Add(numAmount, 1, row++);

            rightPanel.Controls.Add(new Label { Text = "Pet:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, row);
            cmbPet = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbPet.SelectedIndexChanged += (s, e) => UpdatePetVersionCombo();
            rightPanel.Controls.Add(cmbPet, 1, row++);

            rightPanel.Controls.Add(new Label { Text = "Pet Version:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, row);
            cmbPetVersion = new ComboBox { Dock = DockStyle.Left, DropDownStyle = ComboBoxStyle.DropDownList, Width = 80 };
            rightPanel.Controls.Add(cmbPetVersion, 1, row++);

            rightPanel.Controls.Add(new Label { Text = "Unlock:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, row);
            cmbUnlock = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            rightPanel.Controls.Add(cmbUnlock, 1, row++);

            rightPanel.Controls.Add(new Label { Text = "Area:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, row);
            numArea = new NumericUpDown { Dock = DockStyle.Left, Minimum = 1, Maximum = 9999, Width = 80, Value = 1 };
            toolTip.SetToolTip(numArea, "Special encounter area (round 1).");
            rightPanel.Controls.Add(numArea, 1, row++);

            rightPanel.Controls.Add(new Label { Text = "Cooldown:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, row);
            numCooldown = new NumericUpDown { Dock = DockStyle.Left, Minimum = -1, Maximum = 999999, Width = 80, Value = 0 };
            toolTip.SetToolTip(numCooldown,
                "Minutes before the code can be redeemed again.\n0 = no cooldown, -1 = one use only.");
            rightPanel.Controls.Add(numCooldown, 1, row++);

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
            rightPanel.Controls.Add(new Label(), 0, row);
            rightPanel.Controls.Add(buttonPanelRight, 1, row++);

            mainLayout.Controls.Add(rightPanel, 1, 0);

            this.Controls.Add(mainLayout);

            btnSave.Click += BtnSave_Click;
            btnCancel.Click += BtnCancel_Click;
        }

        #endregion

        #region Data Loading and Population

        private void PopulateTypeCombo()
        {
            cmbType.Items.Clear();
            foreach (var value in Enum.GetValues(typeof(PasswordTypeEnum)))
                cmbType.Items.Add(value.ToString());
            cmbType.SelectedIndex = 0;
        }

        private void PopulateItemCombo()
        {
            cmbItem.Items.Clear();
            cmbItem.Items.Add("");
            if (string.IsNullOrEmpty(modulePath))
                return;
            string itemPath = Path.Combine(modulePath, "item.json");
            if (!File.Exists(itemPath))
                return;
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(File.ReadAllText(itemPath)))
                {
                    if (doc.RootElement.TryGetProperty("item", out var itemsElement))
                    {
                        foreach (var item in itemsElement.EnumerateArray())
                        {
                            if (item.TryGetProperty("name", out var name))
                                cmbItem.Items.Add(name.GetString());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading item.json: " + ex.Message);
            }
        }

        private void PopulatePetCombos()
        {
            petEntries.Clear();
            cmbPet.Items.Clear();
            cmbPet.Items.Add("");
            if (string.IsNullOrEmpty(modulePath))
                return;
            string monsterPath = Path.Combine(modulePath, "monster.json");
            if (!File.Exists(monsterPath))
                return;
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(File.ReadAllText(monsterPath)))
                {
                    if (doc.RootElement.TryGetProperty("monster", out var monsters))
                    {
                        foreach (var m in monsters.EnumerateArray())
                        {
                            int stage = m.TryGetProperty("stage", out var st) ? st.GetInt32() : 0;
                            if (stage <= 0)
                                continue;
                            string name = m.TryGetProperty("name", out var nm) ? nm.GetString() : null;
                            int version = m.TryGetProperty("version", out var ver) ? ver.GetInt32() : 1;
                            if (!string.IsNullOrEmpty(name))
                                petEntries.Add(Tuple.Create(name, version));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading monster.json: " + ex.Message);
            }

            foreach (var name in petEntries.Select(p => p.Item1).Distinct().OrderBy(n => n))
                cmbPet.Items.Add(name);
        }

        private void PopulateUnlockCombo()
        {
            cmbUnlock.Items.Clear();
            cmbUnlock.Items.Add("");
            if (module?.Unlocks == null)
                return;
            foreach (var unlock in module.Unlocks)
            {
                if (!string.IsNullOrEmpty(unlock.Name))
                    cmbUnlock.Items.Add(unlock.Name);
            }
        }

        private void UpdatePetVersionCombo()
        {
            var current = cmbPetVersion.SelectedItem?.ToString();
            cmbPetVersion.Items.Clear();
            var petName = cmbPet.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(petName))
                return;
            foreach (var version in petEntries.Where(p => p.Item1 == petName).Select(p => p.Item2).Distinct().OrderBy(v => v))
                cmbPetVersion.Items.Add(version.ToString());
            if (cmbPetVersion.Items.Count > 0)
            {
                int idx = current != null ? cmbPetVersion.Items.IndexOf(current) : -1;
                cmbPetVersion.SelectedIndex = idx >= 0 ? idx : 0;
            }
        }

        private void LoadPasswordsFromJson()
        {
            passwords.Clear();
            if (string.IsNullOrEmpty(modulePath))
                return;

            string codesPath = Path.Combine(modulePath, "codes.json");
            if (File.Exists(codesPath))
            {
                try
                {
                    using (JsonDocument doc = JsonDocument.Parse(File.ReadAllText(codesPath)))
                    {
                        if (doc.RootElement.TryGetProperty("passwords", out var element))
                            passwords = JsonSerializer.Deserialize<List<Password>>(element.GetRawText());
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error loading codes.json: " + ex.Message);
                }
            }
        }

        private void PopulatePasswordList()
        {
            lstPasswords.Items.Clear();
            if (passwords == null) return;
            foreach (var pw in passwords)
                lstPasswords.Items.Add(pw.Name);
        }

        #endregion

        #region Selection and Editing

        private void LstPasswords_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lstPasswords.SelectedIndex < 0 || lstPasswords.SelectedIndex >= passwords.Count)
            {
                selectedPassword = null;
                ClearFields();
                return;
            }
            selectedPassword = passwords[lstPasswords.SelectedIndex];
            LoadFieldsFromPassword(selectedPassword);
        }

        private void LoadFieldsFromPassword(Password pw)
        {
            txtName.Text = pw.Name ?? "";
            txtCode.Text = pw.Code ?? "";
            cmbType.SelectedItem = string.IsNullOrEmpty(pw.Type) ? "item" : pw.Type;
            cmbItem.SelectedItem = string.IsNullOrEmpty(pw.Item) ? "" : pw.Item;
            numAmount.Value = Math.Max(numAmount.Minimum, Math.Min(numAmount.Maximum, pw.Amount ?? 1));
            cmbPet.SelectedItem = string.IsNullOrEmpty(pw.Pet) ? "" : pw.Pet;
            UpdatePetVersionCombo();
            if (pw.Version.HasValue)
            {
                int idx = cmbPetVersion.Items.IndexOf(pw.Version.Value.ToString());
                if (idx >= 0) cmbPetVersion.SelectedIndex = idx;
            }
            cmbUnlock.SelectedItem = string.IsNullOrEmpty(pw.Unlock) ? "" : pw.Unlock;
            numArea.Value = Math.Max(numArea.Minimum, Math.Min(numArea.Maximum, pw.Area ?? 1));
            numCooldown.Value = Math.Max(numCooldown.Minimum, Math.Min(numCooldown.Maximum, pw.Cooldown));
            UpdateTypeFields();
        }

        private void ClearFields()
        {
            txtName.Text = "";
            txtCode.Text = "";
            cmbType.SelectedIndex = 0;
            cmbItem.SelectedIndex = 0;
            numAmount.Value = 1;
            cmbPet.SelectedIndex = 0;
            cmbPetVersion.Items.Clear();
            cmbUnlock.SelectedIndex = 0;
            numArea.Value = 1;
            numCooldown.Value = 0;
            UpdateTypeFields();
        }

        /// <summary>
        /// Enables only the field group that matches the selected type.
        /// </summary>
        private void UpdateTypeFields()
        {
            var type = cmbType.SelectedItem?.ToString() ?? "item";
            cmbItem.Enabled = type == "item";
            numAmount.Enabled = type == "item";
            cmbPet.Enabled = type == "pet";
            cmbPetVersion.Enabled = type == "pet";
            cmbUnlock.Enabled = type == "unlock";
            numArea.Enabled = type == "encounter";
        }

        #endregion

        #region Button Events

        private void BtnAdd_Click(object sender, EventArgs e)
        {
            var pw = new Password
            {
                Name = "New Password",
                Code = "",
                Type = "item",
                Cooldown = 0
            };
            passwords.Add(pw);
            PopulatePasswordList();
            lstPasswords.SelectedIndex = passwords.Count - 1;
        }

        /// <summary>Ctrl+C — copy the selected password into the paste buffer.</summary>
        public void CopySelection()
        {
            if (lstPasswords.SelectedIndex < 0 || lstPasswords.SelectedIndex >= passwords.Count)
                return;
            copiedPassword = ClipboardListUtils.DeepClone(passwords[lstPasswords.SelectedIndex]);
        }

        /// <summary>Ctrl+V — paste a duplicate of the copied password.</summary>
        public void PasteClipboard()
        {
            if (copiedPassword == null) return;
            passwords.Add(ClipboardListUtils.DeepClone(copiedPassword));
            PopulatePasswordList();
            lstPasswords.SelectedIndex = passwords.Count - 1;
        }

        private void BtnRemove_Click(object sender, EventArgs e)
        {
            if (lstPasswords.SelectedIndex < 0 || lstPasswords.SelectedIndex >= passwords.Count)
                return;
            var result = MessageBox.Show(
                $"Do you want to remove the password \"{passwords[lstPasswords.SelectedIndex].Name}\"?",
                "Confirmation",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            );
            if (result == DialogResult.Yes)
            {
                passwords.RemoveAt(lstPasswords.SelectedIndex);
                PopulatePasswordList();
                ClearFields();
            }
        }

        private void ApplyFieldsToSelected()
        {
            selectedPassword.Name = txtName.Text;
            selectedPassword.Code = txtCode.Text.Trim().ToUpperInvariant();
            var type = cmbType.SelectedItem?.ToString() ?? "item";
            selectedPassword.Type = type;
            selectedPassword.Cooldown = (int)numCooldown.Value;

            // Only persist the field group for the selected type.
            selectedPassword.Item = type == "item" ? (cmbItem.SelectedItem?.ToString() ?? "") : null;
            selectedPassword.Amount = type == "item" ? (int?)numAmount.Value : null;
            selectedPassword.Pet = type == "pet" ? (cmbPet.SelectedItem?.ToString() ?? "") : null;
            selectedPassword.Version = type == "pet" && cmbPetVersion.SelectedItem != null
                ? (int?)int.Parse(cmbPetVersion.SelectedItem.ToString()) : null;
            selectedPassword.Unlock = type == "unlock" ? (cmbUnlock.SelectedItem?.ToString() ?? "") : null;
            selectedPassword.Area = type == "encounter" ? (int?)numArea.Value : null;
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            if (selectedPassword == null || lstPasswords.SelectedIndex < 0)
                return;
            ApplyFieldsToSelected();
            lstPasswords.Items[lstPasswords.SelectedIndex] = selectedPassword.Name;
        }

        private void BtnCancel_Click(object sender, EventArgs e)
        {
            if (selectedPassword != null)
                LoadFieldsFromPassword(selectedPassword);
        }

        #endregion

        #region Save to File

        /// <summary>
        /// Saves the current password list to codes.json.
        /// </summary>
        public void Save()
        {
            if (string.IsNullOrEmpty(modulePath) || passwords == null)
                return;

            if (selectedPassword != null && lstPasswords.SelectedIndex >= 0)
                ApplyFieldsToSelected();

            string codesPath = Path.Combine(modulePath, "codes.json");

            // Don't create an empty codes.json for modules without passwords.
            if (passwords.Count == 0)
            {
                if (File.Exists(codesPath))
                {
                    try { File.Delete(codesPath); }
                    catch (Exception ex) { MessageBox.Show("Error removing codes.json: " + ex.Message); }
                }
                return;
            }

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
            var obj = new { passwords };
            try
            {
                string json = JsonSerializer.Serialize(obj, options);
                File.WriteAllText(codesPath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error saving codes.json: " + ex.Message);
            }
        }

        #endregion
    }
}
