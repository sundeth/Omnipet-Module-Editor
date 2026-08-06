using OmnipetModuleEditor.Models;
using OmnipetModuleEditor.Utils;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Forms;

namespace OmnipetModuleEditor.Tabs
{
    /// <summary>
    /// Edits the real-device definitions that are saved in devices.json.
    /// Device definitions deliberately live outside module.json so device protocol
    /// values can remain independent from game/evolution line versions.
    /// </summary>
    public class DeviceTab : UserControl
    {
        private readonly List<DeviceDefinition> devices = new List<DeviceDefinition>();
        private List<Pet> eggPets = new List<Pet>();
        private string modulePath;
        private Module module;

        private Panel deviceListPanel;
        private Panel selectedListPanel;
        private DeviceDefinition selectedDevice;
        private string pendingSprite = "";
        private List<DeviceEgg> editingEggs = new List<DeviceEgg>();
        private bool suppressDirtyTracking;
        private bool isDirty;

        private PictureBox pbDeviceSprite;
        private PictureBox pbPreviewDevice;
        private TextBox txtName;
        private NumericUpDown numDeviceVersion;
        private ComboBox cmbBackground;
        private NumericUpDown numBackgroundX;
        private NumericUpDown numBackgroundY;
        private NumericUpDown numBackgroundScale;
        private FlowLayoutPanel eggPanel;
        private Button btnSaveDevice;
        private Button btnCancelDevice;
        private readonly ToolTip toolTip = new ToolTip();

        public DeviceTab()
        {
            InitializeComponent();
            Name = "DeviceTab";
        }

        public void SetModule(string modulePath, Module module)
        {
            this.modulePath = modulePath;
            this.module = module;
            eggPets = PetUtils.LoadPetsFromJson(modulePath) ?? new List<Pet>();
            LoadDevicesFromJson();
            PopulateDeviceList();
            ClearEditor();
        }

        private void InitializeComponent()
        {
            Dock = DockStyle.Fill;

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(8),
                BackColor = SystemColors.Control
            };
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            mainLayout.Controls.Add(BuildDeviceList(), 0, 0);
            mainLayout.Controls.Add(BuildDeviceEditor(), 1, 0);
            Controls.Add(mainLayout);
        }

        private Control BuildDeviceList()
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = SystemColors.ControlLight
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));

            var title = new Label
            {
                Text = "Devices",
                Dock = DockStyle.Fill,
                Font = new Font(Font, FontStyle.Bold),
                Padding = new Padding(8, 7, 0, 0),
                ForeColor = Color.FromArgb(55, 55, 55)
            };
            layout.Controls.Add(title, 0, 0);

            deviceListPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.White
            };
            layout.Controls.Add(deviceListPanel, 0, 1);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(4),
                WrapContents = false
            };
            var btnAdd = new Button { Text = "Add", Width = 70, Height = 30 };
            var btnRemove = new Button { Text = "Remove", Width = 70, Height = 30 };
            btnAdd.Click += BtnAdd_Click;
            btnRemove.Click += BtnRemove_Click;
            buttons.Controls.Add(btnAdd);
            buttons.Controls.Add(btnRemove);
            layout.Controls.Add(buttons, 0, 2);
            return layout;
        }

        private Control BuildDeviceEditor()
        {
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(16, 4, 16, 8)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            var preview = BuildPreview();
            layout.Controls.Add(preview, 0, 0);

            var fields = BuildFields();
            layout.Controls.Add(fields, 0, 1);

            var eggs = BuildEggSection();
            layout.Controls.Add(eggs, 0, 2);

            var buttons = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(0, 8, 0, 0)
            };
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            btnSaveDevice = new Button { Text = "Save", Width = 86, Height = 30 };
            btnCancelDevice = new Button { Text = "Cancel", Width = 86, Height = 30 };
            btnSaveDevice.Click += BtnSaveDevice_Click;
            btnCancelDevice.Click += BtnCancelDevice_Click;
            var buttonFlow = new FlowLayoutPanel
            {
                AutoSize = true,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight
            };
            buttonFlow.Controls.Add(btnSaveDevice);
            buttonFlow.Controls.Add(btnCancelDevice);
            buttons.Controls.Add(buttonFlow, 1, 0);
            layout.Controls.Add(buttons, 0, 3);

            scroll.Controls.Add(layout);
            return scroll;
        }

        private Control BuildPreview()
        {
            var group = new GroupBox
            {
                Text = "Device Preview",
                Dock = DockStyle.Top,
                Height = 145,
                Padding = new Padding(8)
            };
            var canvas = new Panel
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(42, 42, 42)
            };

            pbPreviewDevice = new PictureBox
            {
                BackColor = Color.Transparent,
                SizeMode = PictureBoxSizeMode.Normal,
                Visible = false
            };
            canvas.Controls.Add(pbPreviewDevice);
            group.Controls.Add(canvas);
            canvas.Resize += (s, e) => UpdatePreviewLayout();
            return group;
        }

        private Control BuildFields()
        {
            var group = new GroupBox
            {
                Text = "Device Details",
                Dock = DockStyle.Top,
                Height = 176,
                Padding = new Padding(8)
            };
            var body = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1
            };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145F));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            pbDeviceSprite = new PictureBox
            {
                Width = 105,
                Height = 80,
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                Cursor = Cursors.Hand,
                Margin = new Padding(3)
            };
            pbDeviceSprite.Click += PbDeviceSprite_Click;
            toolTip.SetToolTip(pbDeviceSprite, "Click to choose the device PNG. It is copied to this module's devices folder.");

            var spritePanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
            spritePanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            spritePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
            pbDeviceSprite.Anchor = AnchorStyles.None;
            spritePanel.Controls.Add(pbDeviceSprite, 0, 0);
            spritePanel.Controls.Add(new Label
            {
                Text = "Click sprite to replace",
                TextAlign = ContentAlignment.TopCenter,
                Dock = DockStyle.Fill,
                ForeColor = Color.DimGray,
                Font = new Font(Font.FontFamily, 8.25F)
            }, 0, 1);
            body.Controls.Add(spritePanel, 0, 0);

            var fields = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 4,
                Padding = new Padding(0, 2, 0, 0)
            };
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110F));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            for (int row = 0; row < 4; row++) fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 31F));

            txtName = new TextBox { Dock = DockStyle.Fill };
            AddFormRow(fields, "Name:", txtName, 0);

            numDeviceVersion = NumberBox(0, 9999, 1);
            AddFormRow(fields, "Device Version:", numDeviceVersion, 1);

            cmbBackground = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbBackground.SelectedIndexChanged += (s, e) => { MarkDirty(); UpdatePreview(); };
            AddFormRow(fields, "Background:", cmbBackground, 2);

            numBackgroundX = NumberBox(-9999, 9999, 0);
            numBackgroundY = NumberBox(-9999, 9999, 0);
            var positionPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                WrapContents = false,
                Padding = new Padding(0, 1, 0, 0)
            };
            positionPanel.Controls.Add(new Label { Text = "X", AutoSize = true, Padding = new Padding(0, 5, 3, 0) });
            positionPanel.Controls.Add(numBackgroundX);
            positionPanel.Controls.Add(new Label { Text = "Y", AutoSize = true, Padding = new Padding(12, 5, 3, 0) });
            positionPanel.Controls.Add(numBackgroundY);

            numBackgroundScale = NumberBox(1, 100, 100);
            positionPanel.Controls.Add(new Label { Text = "Scale", AutoSize = true, Padding = new Padding(12, 5, 3, 0) });
            positionPanel.Controls.Add(numBackgroundScale);
            positionPanel.Controls.Add(new Label { Text = "%", AutoSize = true, Padding = new Padding(3, 5, 0, 0) });
            AddFormRow(fields, "Screen Placement:", positionPanel, 3);
            body.Controls.Add(fields, 1, 0);
            group.Controls.Add(body);

            txtName.TextChanged += (s, e) => MarkDirty();
            numDeviceVersion.ValueChanged += (s, e) => MarkDirty();
            numBackgroundX.ValueChanged += (s, e) => { MarkDirty(); UpdatePreview(); };
            numBackgroundY.ValueChanged += (s, e) => { MarkDirty(); UpdatePreview(); };
            numBackgroundScale.ValueChanged += (s, e) => { MarkDirty(); UpdatePreview(); };
            toolTip.SetToolTip(numDeviceVersion, "The real device version used by compatible battle protocols, not the game evolution-line version.");
            toolTip.SetToolTip(cmbBackground, "A background's internal module name. The low-resolution image is used in the preview.");
            toolTip.SetToolTip(numBackgroundX, "X coordinate of the background's top-left corner in the device sprite.");
            toolTip.SetToolTip(numBackgroundY, "Y coordinate of the background's top-left corner in the device sprite.");
            toolTip.SetToolTip(numBackgroundScale, "Scale of the low-resolution background, from 1 to 100 percent.");
            return group;
        }

        private Control BuildEggSection()
        {
            var group = new GroupBox
            {
                Text = "Egg Selection",
                Dock = DockStyle.Top,
                AutoSize = true,
                Padding = new Padding(8)
            };
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 1,
                RowCount = 2
            };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(new Label
            {
                Text = "Click an egg to remove it. Gold borders identify special eggs.",
                AutoSize = true,
                ForeColor = Color.DimGray,
                Padding = new Padding(0, 0, 0, 5)
            }, 0, 0);
            eggPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                MinimumSize = new Size(0, 58),
                Padding = new Padding(3),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                WrapContents = true
            };
            layout.Controls.Add(eggPanel, 0, 1);
            group.Controls.Add(layout);
            return group;
        }

        private NumericUpDown NumberBox(decimal minimum, decimal maximum, decimal value)
        {
            return new NumericUpDown
            {
                Minimum = minimum,
                Maximum = maximum,
                Value = value,
                Width = 70,
                DecimalPlaces = 0
            };
        }

        private void AddFormRow(TableLayoutPanel layout, string label, Control control, int row)
        {
            var fieldLabel = new Label
            {
                Text = label,
                TextAlign = ContentAlignment.MiddleRight,
                Dock = DockStyle.Fill,
                AutoSize = true
            };
            layout.Controls.Add(fieldLabel, 0, row);
            layout.Controls.Add(control, 1, row);
        }

        private void LoadDevicesFromJson()
        {
            devices.Clear();
            if (string.IsNullOrWhiteSpace(modulePath)) return;

            string path = Path.Combine(modulePath, "devices.json");
            if (!File.Exists(path)) return;
            try
            {
                using (var document = JsonDocument.Parse(File.ReadAllText(path)))
                {
                    JsonElement items;
                    if (document.RootElement.TryGetProperty("devices", out items))
                    {
                        var loaded = JsonSerializer.Deserialize<List<DeviceDefinition>>(items.GetRawText());
                        if (loaded != null) devices.AddRange(loaded);
                    }
                }
                foreach (var device in devices)
                {
                    if (device.Eggs == null) device.Eggs = new List<DeviceEgg>();
                    if (device.BackgroundScale < 1) device.BackgroundScale = 100;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading devices.json: " + ex.Message, "Device", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void PopulateDeviceList()
        {
            if (deviceListPanel == null) return;
            int scroll = -deviceListPanel.AutoScrollPosition.Y;
            deviceListPanel.SuspendLayout();
            foreach (Control control in deviceListPanel.Controls) control.Dispose();
            deviceListPanel.Controls.Clear();

            int y = 0;
            foreach (var device in devices)
            {
                var panel = CreateDeviceListItem(device, y);
                deviceListPanel.Controls.Add(panel);
                y += 72;
            }
            deviceListPanel.AutoScrollMinSize = new Size(0, y);
            deviceListPanel.ResumeLayout(true);
            if (IsHandleCreated && scroll > 0)
                BeginInvoke(new Action(() => deviceListPanel.AutoScrollPosition = new Point(0, scroll)));
        }

        private Panel CreateDeviceListItem(DeviceDefinition device, int y)
        {
            var panel = new Panel
            {
                Location = new Point(0, y),
                Size = new Size(Math.Max(260, deviceListPanel.ClientSize.Width - 20), 70),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                Tag = device,
                Cursor = Cursors.Hand
            };
            panel.Click += (s, e) => SelectDevicePanel(panel);

            var sprite = new PictureBox
            {
                Location = new Point(4, 4),
                Size = new Size(92, 60),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(235, 235, 235),
                BorderStyle = BorderStyle.FixedSingle,
                Image = LoadDeviceSprite(device.Sprite),
                Cursor = Cursors.Hand
            };
            sprite.Click += (s, e) => SelectDevicePanel(panel);
            panel.Controls.Add(sprite);

            var name = new Label
            {
                Text = string.IsNullOrWhiteSpace(device.Name) ? "Unnamed Device" : device.Name,
                Location = new Point(104, 10),
                AutoSize = true,
                Font = new Font(Font, FontStyle.Bold),
                ForeColor = Color.DeepSkyBlue,
                Cursor = Cursors.Hand
            };
            name.Click += (s, e) => SelectDevicePanel(panel);
            panel.Controls.Add(name);

            var details = new Label
            {
                Text = "Device Version " + device.DeviceVersion,
                Location = new Point(104, 36),
                AutoSize = true,
                ForeColor = Color.DimGray,
                Cursor = Cursors.Hand
            };
            details.Click += (s, e) => SelectDevicePanel(panel);
            panel.Controls.Add(details);
            return panel;
        }

        private void SelectDevicePanel(Panel panel)
        {
            var target = panel?.Tag as DeviceDefinition;
            if (target == null || target == selectedDevice) return;

            if (isDirty && selectedDevice != null)
            {
                var result = MessageBox.Show(
                    "Save changes to the current device before switching?",
                    "Unsaved Device Changes",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Warning);
                if (result == DialogResult.Cancel) return;
                if (result == DialogResult.Yes) CommitSelectedDevice(true);
            }

            selectedDevice = target;
            selectedListPanel = FindDevicePanel(target) ?? panel;
            foreach (Control control in deviceListPanel.Controls)
                control.BackColor = control == selectedListPanel ? Color.LightBlue : Color.White;
            LoadSelectedDevice();
        }

        private void LoadSelectedDevice()
        {
            suppressDirtyTracking = true;
            PopulateBackgroundCombo();
            if (selectedDevice == null)
            {
                ClearEditor(false);
                suppressDirtyTracking = false;
                return;
            }

            txtName.Text = selectedDevice.Name ?? "";
            numDeviceVersion.Value = ClampValue(numDeviceVersion, selectedDevice.DeviceVersion);
            cmbBackground.SelectedItem = selectedDevice.Background ?? "";
            if (cmbBackground.SelectedIndex < 0) cmbBackground.SelectedIndex = 0;
            numBackgroundX.Value = ClampValue(numBackgroundX, selectedDevice.BackgroundX);
            numBackgroundY.Value = ClampValue(numBackgroundY, selectedDevice.BackgroundY);
            numBackgroundScale.Value = ClampValue(numBackgroundScale, selectedDevice.BackgroundScale < 1 ? 100 : selectedDevice.BackgroundScale);
            pendingSprite = selectedDevice.Sprite ?? "";
            editingEggs = CloneEggs(selectedDevice.Eggs);
            SetImage(pbDeviceSprite, LoadDeviceSprite(pendingSprite));
            suppressDirtyTracking = false;
            isDirty = false;
            SetEditorEnabled(true);
            RefreshEggPanel();
            UpdatePreview();
        }

        private decimal ClampValue(NumericUpDown control, int value)
        {
            return Math.Max(control.Minimum, Math.Min(control.Maximum, value));
        }

        private void ClearEditor(bool resetSelection = true)
        {
            suppressDirtyTracking = true;
            if (resetSelection)
            {
                selectedDevice = null;
                selectedListPanel = null;
            }
            PopulateBackgroundCombo();
            txtName.Text = "";
            numDeviceVersion.Value = 1;
            cmbBackground.SelectedIndex = 0;
            numBackgroundX.Value = 0;
            numBackgroundY.Value = 0;
            numBackgroundScale.Value = 100;
            pendingSprite = "";
            editingEggs = new List<DeviceEgg>();
            SetImage(pbDeviceSprite, null);
            eggPanel.Controls.Clear();
            SetImage(pbPreviewDevice, null);
            pbPreviewDevice.Visible = false;
            suppressDirtyTracking = false;
            isDirty = false;
            SetEditorEnabled(false);
        }

        private void SetEditorEnabled(bool enabled)
        {
            pbDeviceSprite.Enabled = enabled;
            txtName.Enabled = enabled;
            numDeviceVersion.Enabled = enabled;
            cmbBackground.Enabled = enabled;
            numBackgroundX.Enabled = enabled;
            numBackgroundY.Enabled = enabled;
            numBackgroundScale.Enabled = enabled;
            eggPanel.Enabled = enabled;
            btnSaveDevice.Enabled = enabled;
            btnCancelDevice.Enabled = enabled;
        }

        private void PopulateBackgroundCombo()
        {
            string current = cmbBackground.SelectedItem as string;
            cmbBackground.Items.Clear();
            cmbBackground.Items.Add("");
            if (module?.Backgrounds != null)
            {
                foreach (var background in module.Backgrounds
                    .Where(b => !string.IsNullOrWhiteSpace(b.Name))
                    .OrderBy(b => b.Name, StringComparer.OrdinalIgnoreCase))
                    cmbBackground.Items.Add(background.Name);
            }
            if (!string.IsNullOrEmpty(current) && cmbBackground.Items.Contains(current))
                cmbBackground.SelectedItem = current;
            else
                cmbBackground.SelectedIndex = 0;
        }

        private void RefreshEggPanel()
        {
            eggPanel.SuspendLayout();
            foreach (Control control in eggPanel.Controls) control.Dispose();
            eggPanel.Controls.Clear();
            if (selectedDevice != null)
            {
                foreach (var egg in editingEggs)
                    eggPanel.Controls.Add(CreateEggBox(egg));
            }
            eggPanel.Controls.Add(CreateAddEggBox());
            eggPanel.ResumeLayout(true);
        }

        private Control CreateEggBox(DeviceEgg egg)
        {
            var box = new PictureBox
            {
                Width = 48,
                Height = 48,
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = PetUtils.LoadSinglePetSprite(egg.Name, modulePath, module),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Cursor = Cursors.Hand,
                Tag = egg,
                Margin = new Padding(2)
            };
            if (IsSpecialEgg(egg))
            {
                box.BorderStyle = BorderStyle.None;
                box.Paint += (s, e) =>
                {
                    using (var pen = new Pen(Color.Gold, 3))
                        e.Graphics.DrawRectangle(pen, 1, 1, box.Width - 3, box.Height - 3);
                };
            }
            toolTip.SetToolTip(box, egg.Name + " (Version " + egg.Version + ")\nClick to remove.");
            box.Click += (s, e) => RemoveEgg((DeviceEgg)box.Tag);
            return box;
        }

        private Control CreateAddEggBox()
        {
            var button = new Button
            {
                Text = "+",
                Width = 48,
                Height = 48,
                Font = new Font(Font.FontFamily, 18, FontStyle.Bold),
                Margin = new Padding(2),
                Cursor = Cursors.Hand
            };
            toolTip.SetToolTip(button, "Add a stage 0 egg to this device.");
            button.Click += (s, e) => AddEgg();
            return button;
        }

        private bool IsSpecialEgg(DeviceEgg egg)
        {
            return eggPets.Any(p => p.Stage == 0 && p.Special && p.Version == egg.Version &&
                string.Equals(p.Name, egg.Name, StringComparison.OrdinalIgnoreCase));
        }

        private void AddEgg()
        {
            if (selectedDevice == null) return;
            var available = eggPets
                .Where(p => p.Stage == 0)
                .OrderBy(p => p.Version)
                .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (available.Count == 0)
            {
                MessageBox.Show("This module has no stage 0 pets to add.", "Add Egg", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var dialog = new EggPickerDialog(available))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK || dialog.SelectedEgg == null) return;
                var selectedEgg = dialog.SelectedEgg;
                bool alreadyAdded = editingEggs.Any(e => e.Version == selectedEgg.Version &&
                    string.Equals(e.Name, selectedEgg.Name, StringComparison.OrdinalIgnoreCase));
                if (alreadyAdded)
                {
                    MessageBox.Show("That egg is already associated with this device.", "Add Egg", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                editingEggs.Add(new DeviceEgg { Name = selectedEgg.Name, Version = selectedEgg.Version });
                isDirty = true;
                RefreshEggPanel();
            }
        }

        private void RemoveEgg(DeviceEgg egg)
        {
            if (selectedDevice == null || egg == null) return;
            var result = MessageBox.Show(
                "Remove " + egg.Name + " (Version " + egg.Version + ") from this device?",
                "Remove Egg",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (result != DialogResult.Yes) return;
            editingEggs.Remove(egg);
            isDirty = true;
            RefreshEggPanel();
        }

        private void PbDeviceSprite_Click(object sender, EventArgs e)
        {
            if (selectedDevice == null || string.IsNullOrWhiteSpace(modulePath)) return;
            string devicesPath = Path.Combine(modulePath, "devices");
            Directory.CreateDirectory(devicesPath);
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "Choose Device Sprite";
                dialog.Filter = "PNG files (*.png)|*.png";
                dialog.InitialDirectory = devicesPath;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                string spriteName = Path.GetFileNameWithoutExtension(dialog.FileName);
                string destination = Path.Combine(devicesPath, spriteName + ".png");
                try
                {
                    if (!string.Equals(Path.GetFullPath(dialog.FileName), Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase))
                        File.Copy(dialog.FileName, destination, true);
                    pendingSprite = spriteName;
                    SetImage(pbDeviceSprite, LoadDeviceSprite(spriteName));
                    isDirty = true;
                    UpdatePreview();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Could not import the device sprite: " + ex.Message, "Device", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void BtnAdd_Click(object sender, EventArgs e)
        {
            if (isDirty && selectedDevice != null)
            {
                var result = MessageBox.Show("Save changes to the current device before adding another?", "Unsaved Device Changes", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
                if (result == DialogResult.Cancel) return;
                if (result == DialogResult.Yes) CommitSelectedDevice(true);
            }
            var device = new DeviceDefinition { Name = "New Device", DeviceVersion = 1, BackgroundScale = 100 };
            devices.Add(device);
            PopulateDeviceList();
            SelectDeviceByReference(device);
        }

        private void BtnRemove_Click(object sender, EventArgs e)
        {
            if (selectedDevice == null) return;
            var result = MessageBox.Show(
                "Do you want to remove the device \"" + selectedDevice.Name + "\"?",
                "Remove Device",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (result != DialogResult.Yes) return;
            devices.Remove(selectedDevice);
            selectedDevice = null;
            selectedListPanel = null;
            PopulateDeviceList();
            ClearEditor();
            Save();
        }

        private void BtnSaveDevice_Click(object sender, EventArgs e)
        {
            CommitSelectedDevice(true);
        }

        private void BtnCancelDevice_Click(object sender, EventArgs e)
        {
            LoadSelectedDevice();
        }

        private void CommitSelectedDevice(bool saveToFile)
        {
            if (selectedDevice == null) return;
            selectedDevice.Name = txtName.Text.Trim();
            selectedDevice.Sprite = pendingSprite;
            selectedDevice.DeviceVersion = (int)numDeviceVersion.Value;
            selectedDevice.Background = cmbBackground.SelectedItem as string ?? "";
            selectedDevice.BackgroundX = (int)numBackgroundX.Value;
            selectedDevice.BackgroundY = (int)numBackgroundY.Value;
            selectedDevice.BackgroundScale = (int)numBackgroundScale.Value;
            selectedDevice.Eggs = CloneEggs(editingEggs);
            isDirty = false;
            PopulateDeviceList();
            ReselectCurrentListItem();
            if (saveToFile) Save();
        }

        private void SelectDeviceByReference(DeviceDefinition device)
        {
            var panel = FindDevicePanel(device);
            if (panel != null) SelectDevicePanel(panel);
        }

        private Panel FindDevicePanel(DeviceDefinition device)
        {
            foreach (Control control in deviceListPanel.Controls)
                if (ReferenceEquals(control.Tag, device)) return control as Panel;
            return null;
        }

        private void ReselectCurrentListItem()
        {
            if (selectedDevice == null) return;
            foreach (Control control in deviceListPanel.Controls)
            {
                if (ReferenceEquals(control.Tag, selectedDevice))
                {
                    selectedListPanel = control as Panel;
                    control.BackColor = Color.LightBlue;
                }
            }
        }

        private void MarkDirty()
        {
            if (!suppressDirtyTracking && selectedDevice != null) isDirty = true;
        }

        private void UpdatePreview()
        {
            if (selectedDevice == null) return;
            string backgroundName = cmbBackground.SelectedItem as string;
            SetImage(pbPreviewDevice, CreatePreviewImage(pendingSprite, backgroundName));
            UpdatePreviewLayout();
        }

        private void UpdatePreviewLayout()
        {
            var canvas = pbPreviewDevice?.Parent as Panel;
            if (canvas == null) return;

            if (pbPreviewDevice.Image != null)
            {
                int x = Math.Max(0, (canvas.ClientSize.Width - pbPreviewDevice.Image.Width) / 2);
                int y = Math.Max(0, (canvas.ClientSize.Height - pbPreviewDevice.Image.Height) / 2);
                pbPreviewDevice.Location = new Point(x, y);
                pbPreviewDevice.Size = pbPreviewDevice.Image.Size;
                pbPreviewDevice.Visible = true;
            }
            else pbPreviewDevice.Visible = false;

            pbPreviewDevice.BringToFront();
        }

        /// <summary>
        /// Compose the preview into one bitmap rather than stacking two
        /// PictureBoxes. WinForms "transparent" child controls only reveal
        /// their parent, not sibling controls, so that approach hid the
        /// background through transparent pixels in the device PNG.
        /// </summary>
        private Image CreatePreviewImage(string spriteName, string backgroundName)
        {
            Image device = LoadDeviceSprite(spriteName);
            if (device == null) return null;

            Image background = LoadBackground(backgroundName);
            if (background == null) return device;

            try
            {
                int scale = numBackgroundScale == null ? 100 : (int)numBackgroundScale.Value;
                int width = Math.Max(1, background.Width * scale / 100);
                int height = Math.Max(1, background.Height * scale / 100);
                int x = numBackgroundX == null ? 0 : (int)numBackgroundX.Value;
                int y = numBackgroundY == null ? 0 : (int)numBackgroundY.Value;

                var preview = new Bitmap(device.Width, device.Height, PixelFormat.Format32bppArgb);
                using (var graphics = Graphics.FromImage(preview))
                {
                    graphics.Clear(Color.Transparent);
                    graphics.CompositingMode = CompositingMode.SourceOver;
                    graphics.CompositingQuality = CompositingQuality.HighSpeed;
                    graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
                    graphics.PixelOffsetMode = PixelOffsetMode.Half;
                    graphics.SmoothingMode = SmoothingMode.None;
                    graphics.DrawImage(background, new Rectangle(x, y, width, height),
                        0, 0, background.Width, background.Height, GraphicsUnit.Pixel);
                    graphics.DrawImageUnscaled(device, 0, 0);
                }
                device.Dispose();
                return preview;
            }
            catch
            {
                device.Dispose();
                throw;
            }
            finally
            {
                background.Dispose();
            }
        }

        private Image LoadDeviceSprite(string spriteName)
        {
            if (string.IsNullOrWhiteSpace(modulePath) || string.IsNullOrWhiteSpace(spriteName)) return null;
            return LoadImage(Path.Combine(modulePath, "devices", spriteName + ".png"));
        }

        private Image LoadBackground(string backgroundName)
        {
            if (string.IsNullOrWhiteSpace(modulePath) || string.IsNullOrWhiteSpace(backgroundName)) return null;
            var background = module?.Backgrounds?.FirstOrDefault(b => string.Equals(b.Name, backgroundName, StringComparison.OrdinalIgnoreCase));
            string fileName = background?.DayNight == true
                ? "bg_" + backgroundName + "_day.png"
                : "bg_" + backgroundName + ".png";
            return LoadImage(Path.Combine(modulePath, "backgrounds", fileName));
        }

        private Image LoadImage(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var image = Image.FromStream(stream))
                    return new Bitmap(image);
            }
            catch { return null; }
        }

        private void SetImage(PictureBox box, Image image)
        {
            if (box == null) return;
            var old = box.Image;
            box.Image = image;
            if (old != null && !ReferenceEquals(old, image)) old.Dispose();
        }

        private List<DeviceEgg> CloneEggs(IEnumerable<DeviceEgg> eggs)
        {
            return (eggs ?? Enumerable.Empty<DeviceEgg>())
                .Where(e => e != null)
                .Select(e => new DeviceEgg { Name = e.Name ?? "", Version = e.Version })
                .ToList();
        }

        /// <summary>Called by the editor's Save All operation.</summary>
        public void Save()
        {
            if (string.IsNullOrWhiteSpace(modulePath)) return;
            string path = Path.Combine(modulePath, "devices.json");
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                };
                File.WriteAllText(path, JsonSerializer.Serialize(new { devices = devices }, options));
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error saving devices.json: " + ex.Message, "Device", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                SetImage(pbDeviceSprite, null);
                SetImage(pbPreviewDevice, null);
                toolTip.Dispose();
            }
            base.Dispose(disposing);
        }

        private class EggPickerDialog : Form
        {
            private readonly ComboBox combo;
            private readonly List<Pet> eggs;
            public Pet SelectedEgg { get; private set; }

            public EggPickerDialog(List<Pet> eggs)
            {
                this.eggs = eggs;
                Text = "Add Egg";
                FormBorderStyle = FormBorderStyle.FixedDialog;
                StartPosition = FormStartPosition.CenterParent;
                MaximizeBox = false;
                MinimizeBox = false;
                ClientSize = new Size(320, 104);

                var label = new Label { Text = "Stage 0 pet:", Location = new Point(12, 14), AutoSize = true };
                combo = new ComboBox { Location = new Point(12, 34), Width = 296, DropDownStyle = ComboBoxStyle.DropDownList };
                foreach (var egg in eggs)
                    combo.Items.Add(new EggChoice(egg));
                if (combo.Items.Count > 0) combo.SelectedIndex = 0;

                var accept = new Button { Text = "Add", Location = new Point(152, 68), Width = 75, DialogResult = DialogResult.OK };
                var cancel = new Button { Text = "Cancel", Location = new Point(233, 68), Width = 75, DialogResult = DialogResult.Cancel };
                accept.Click += (s, e) => SelectedEgg = (combo.SelectedItem as EggChoice)?.Pet;
                Controls.Add(label);
                Controls.Add(combo);
                Controls.Add(accept);
                Controls.Add(cancel);
                AcceptButton = accept;
                CancelButton = cancel;
            }

            private class EggChoice
            {
                public Pet Pet { get; private set; }
                public EggChoice(Pet pet) { Pet = pet; }
                public override string ToString()
                {
                    return Pet.Name + " (Version " + Pet.Version + ")" + (Pet.Special ? " ★" : "");
                }
            }
        }
    }
}
