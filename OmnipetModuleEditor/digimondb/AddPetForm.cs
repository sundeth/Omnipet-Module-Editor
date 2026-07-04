using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace OmnipetModuleEditor.DigimonSync
{
    /// <summary>
    /// "Add Pet" dialog: pick a stage, then optionally a Digimon from the
    /// database (alphabetical, "Custom" first) and one of that Digimon's
    /// extra-data modules ("None" first). The selections drive how the new pet
    /// is created back in the Pet tab.
    /// </summary>
    public class AddPetForm : Form
    {
        /// <summary>Stage 0-8.</summary>
        public int SelectedStage { get; private set; } = -1;
        /// <summary>The chosen Digimon, or null for "Custom".</summary>
        public DigimonRecord SelectedDigimon { get; private set; }
        /// <summary>The chosen extra-data module, or null for "None".</summary>
        public string SelectedModule { get; private set; }

        private readonly List<DigimonRecord> _records;
        private readonly Dictionary<string, int> _levelMap;

        private ComboBox comboStage;
        private ComboBox comboDigimon;
        private ComboBox comboModule;

        private sealed class DigimonItem
        {
            public readonly DigimonRecord Rec;
            public DigimonItem(DigimonRecord rec) { Rec = rec; }
            public override string ToString() => Rec.NameEnglish ?? Rec.Id;
        }

        public AddPetForm(int defaultStage, List<DigimonRecord> records, Dictionary<string, int> levelMap)
        {
            _records = records;
            _levelMap = levelMap;
            InitializeComponent(defaultStage);
            PopulateDigimon();
        }

        private void InitializeComponent(int defaultStage)
        {
            this.Text = "Add Pet";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ClientSize = new Size(320, 200);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 7,
                Padding = new Padding(12),
            };

            layout.Controls.Add(new Label { Text = "Stage:", AutoSize = true, Margin = new Padding(0, 6, 0, 2) });
            comboStage = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 280 };
            for (int i = 0; i <= 8; i++)
                comboStage.Items.Add($"Stage {i}");
            comboStage.SelectedIndex = (defaultStage >= 0 && defaultStage <= 8) ? defaultStage : 0;
            comboStage.SelectedIndexChanged += (s, e) => PopulateDigimon();
            layout.Controls.Add(comboStage);

            layout.Controls.Add(new Label { Text = "Digimon:", AutoSize = true, Margin = new Padding(0, 8, 0, 2) });
            comboDigimon = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 280 };
            comboDigimon.SelectedIndexChanged += (s, e) => PopulateModule();
            layout.Controls.Add(comboDigimon);

            layout.Controls.Add(new Label { Text = "Module data:", AutoSize = true, Margin = new Padding(0, 8, 0, 2) });
            comboModule = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 280 };
            layout.Controls.Add(comboModule);

            var btnOk = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Dock = DockStyle.Bottom,
                Height = 32,
            };
            btnOk.Click += BtnOk_Click;

            this.Controls.Add(layout);
            this.Controls.Add(btnOk);
            this.AcceptButton = btnOk;
        }

        private bool RecordInStage(DigimonRecord r, int stage)
        {
            if (r.Levels == null || _levelMap == null) return false;
            foreach (var name in r.Levels)
                if (_levelMap.TryGetValue(name, out var id) && id == stage)
                    return true;
            return false;
        }

        private void PopulateDigimon()
        {
            comboDigimon.Items.Clear();
            comboDigimon.Items.Add("Custom");
            if (_records != null && _levelMap != null)
            {
                int stage = comboStage.SelectedIndex;
                var matches = _records
                    .Where(r => RecordInStage(r, stage))
                    .OrderBy(r => r.NameEnglish ?? r.Id, StringComparer.OrdinalIgnoreCase);
                foreach (var r in matches)
                    comboDigimon.Items.Add(new DigimonItem(r));
            }
            comboDigimon.SelectedIndex = 0;   // Custom (also triggers PopulateModule)
        }

        private void PopulateModule()
        {
            comboModule.Items.Clear();
            comboModule.Items.Add("None");
            var item = comboDigimon.SelectedItem as DigimonItem;
            if (item != null && item.Rec.ExtraData != null && item.Rec.ExtraData.Count > 0)
            {
                foreach (var key in item.Rec.ExtraData.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
                    comboModule.Items.Add(key);
                comboModule.Enabled = true;
            }
            else
            {
                comboModule.Enabled = false;
            }
            comboModule.SelectedIndex = 0;   // None
        }

        private void BtnOk_Click(object sender, EventArgs e)
        {
            SelectedStage = comboStage.SelectedIndex;
            SelectedDigimon = (comboDigimon.SelectedItem as DigimonItem)?.Rec;
            SelectedModule = (comboModule.SelectedIndex > 0) ? comboModule.SelectedItem as string : null;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
