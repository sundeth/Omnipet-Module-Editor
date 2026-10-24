using OmnipetModuleEditor.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;

namespace OmnipetModuleEditor
{
    /// <summary>
    /// Dialog for editing temporary (battle-only) evolution criteria:
    /// Mode Change / Xros type, strength range, megahit, unlock, friend
    /// pets, friend count, battle area and DP cost — plus, for Xros only, the
    /// background and animation used by the in-game xros sequence.
    /// </summary>
    public class TempEvolutionCriteriaForm : Form
    {
        // Clipboard marker so temp criteria don't mix with normal evolution copies.
        private const string ClipboardPrefix = "OMNIPET_TEMP_EVO:";

        private readonly TempEvolution evolution;
        private readonly List<string> unlockNames;
        private readonly List<Pet> allPets;
        private readonly string modulePath;

        private ComboBox cmbType;
        private NumericUpDown[] numStrength = new NumericUpDown[2];

    /// <summary>What each condition really gates. Kept as a field so it
    /// outlives the layout code - a ToolTip that goes out of scope stops
    /// showing.</summary>
    private readonly ToolTip tempTips = new ToolTip
    {
        AutoPopDelay = 20000,
        InitialDelay = 300,
        ReshowDelay = 200,
        ShowAlways = true
    };

    private void SetTipDeep(Control control, string tip)
    {
        tempTips.SetToolTip(control, tip);
        foreach (Control child in control.Controls)
            SetTipDeep(child, tip);
    }
        private CheckBox chkMegahit;
        private ComboBox cmbUnlock;
        private TextBox txtFriend;
        private List<string> friendSelection = new List<string>();
        private readonly NumericUpDown[] numFriendCount = new NumericUpDown[2];
        private readonly NumericUpDown[] numBattleArea = new NumericUpDown[2];
        private NumericUpDown numXrosCost;

        // Xros-only configuration
        private ComboBox cmbBackground;
        private ComboBox cmbAnimation;
        private readonly Dictionary<string, Image> backgroundThumbs =
            new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Image> animationThumbs =
            new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);

        private Button btnSave;
        private Button btnCancel;
        private Button btnCopy;
        private Button btnPaste;

        public TempEvolutionCriteriaForm(TempEvolution evo, Module module, List<Pet> pets, string modulePath)
        {
            evolution = evo;
            allPets = pets ?? new List<Pet>();
            this.modulePath = modulePath;
            unlockNames = (module?.Unlocks ?? new List<Unlock>())
                .Select(u => u.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct()
                .ToList();

            LoadBackgroundThumbs();
            LoadAnimationThumbs();
            InitializeComponent();
            LoadEvolution();

            this.FormClosed += (s, e) => DisposeThumbs();
        }

        // =====================================================================
        // Asset scanning (backgrounds / animations folders)
        // =====================================================================

        /// <summary>Load an image without locking the file on disk.</summary>
        private static Image LoadImageUnlocked(string path)
        {
            using (var ms = new MemoryStream(File.ReadAllBytes(path)))
                return Image.FromStream(ms);
        }

        private static Image MakeThumb(string path, int size)
        {
            try
            {
                using (var full = LoadImageUnlocked(path))
                {
                    var thumb = new Bitmap(size, size);
                    using (var g = Graphics.FromImage(thumb))
                    {
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                        // Fit keeping proportion
                        float scale = Math.Min((float)size / full.Width, (float)size / full.Height);
                        int w = Math.Max(1, (int)(full.Width * scale));
                        int h = Math.Max(1, (int)(full.Height * scale));
                        g.DrawImage(full, (size - w) / 2, (size - h) / 2, w, h);
                    }
                    return thumb;
                }
            }
            catch
            {
                return null;
            }
        }

        private static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg", ".bmp", ".gif" };

        /// <summary>
        /// All image files in the module's backgrounds folder (whether or not
        /// they're registered in module.json), ignoring "_high" variants.
        /// </summary>
        private void LoadBackgroundThumbs()
        {
            if (string.IsNullOrEmpty(modulePath)) return;
            string dir = Path.Combine(modulePath, "backgrounds");
            if (!Directory.Exists(dir)) return;

            foreach (var file in Directory.GetFiles(dir)
                     .Where(f => ImageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                     .OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                string name = Path.GetFileNameWithoutExtension(file);
                if (name.EndsWith("_high", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!backgroundThumbs.ContainsKey(name))
                    backgroundThumbs[name] = MakeThumb(file, 32);
            }
        }

        /// <summary>
        /// Animations in the module's animations folder.  Files are named
        /// "name_frameId"; each animation is listed once (only the name is
        /// saved) and its thumbnail is the LAST frame of the sequence.
        /// </summary>
        private void LoadAnimationThumbs()
        {
            if (string.IsNullOrEmpty(modulePath)) return;
            string dir = Path.Combine(modulePath, "animations");
            if (!Directory.Exists(dir)) return;

            var lastFrame = new Dictionary<string, (int Frame, string File)>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in Directory.GetFiles(dir)
                     .Where(f => ImageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant())))
            {
                string stem = Path.GetFileNameWithoutExtension(file);
                int sep = stem.LastIndexOf('_');
                if (sep <= 0) continue;
                string name = stem.Substring(0, sep);
                if (!int.TryParse(stem.Substring(sep + 1), out int frame))
                    continue;
                if (!lastFrame.TryGetValue(name, out var cur) || frame > cur.Frame)
                    lastFrame[name] = (frame, file);
            }

            foreach (var kv in lastFrame.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
                animationThumbs[kv.Key] = MakeThumb(kv.Value.File, 32);
        }

        private void DisposeThumbs()
        {
            foreach (var img in backgroundThumbs.Values) img?.Dispose();
            foreach (var img in animationThumbs.Values) img?.Dispose();
            backgroundThumbs.Clear();
            animationThumbs.Clear();
        }

        // =====================================================================
        // Layout
        // =====================================================================

        /// <summary>Owner-drawn combobox showing a small thumbnail next to each name.</summary>
        private static ComboBox MakeThumbCombo(Dictionary<string, Image> thumbs)
        {
            var cmb = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 200,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 36
            };
            cmb.Items.Add(""); // none
            foreach (var name in thumbs.Keys)
                cmb.Items.Add(name);

            cmb.DrawItem += (s, e) =>
            {
                e.DrawBackground();
                if (e.Index >= 0)
                {
                    string name = cmb.Items[e.Index].ToString();
                    int x = e.Bounds.Left + 2;
                    if (!string.IsNullOrEmpty(name) &&
                        thumbs.TryGetValue(name, out var img) && img != null)
                    {
                        e.Graphics.DrawImage(img, new Rectangle(x, e.Bounds.Top + 2, 32, 32));
                    }
                    string display = string.IsNullOrEmpty(name) ? "(none)" : name;
                    using (var brush = new SolidBrush(e.ForeColor))
                        e.Graphics.DrawString(display, e.Font, brush,
                            x + 38, e.Bounds.Top + (e.Bounds.Height - e.Font.Height) / 2);
                }
                e.DrawFocusRectangle();
            };
            return cmb;
        }

        private void InitializeComponent()
        {
            this.Text = "Temporary Evolution Criteria";
            this.Size = new Size(440, 540);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 2,
                AutoSize = true,
                Padding = new Padding(12)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            int row = 0;

            Label lastLabel = null;
            void AddLabel(string text)
            {
                var lbl = new Label
                {
                    Text = text,
                    Anchor = AnchorStyles.Left,
                    AutoSize = true,
                    Font = new Font("Segoe UI", 8),
                    Margin = new Padding(0, 6, 0, 0)
                };
                layout.Controls.Add(lbl, 0, row);
                lastLabel = lbl;
            }

            void AddControl(Control control, string tip = null)
            {
                control.Margin = new Padding(0, 4, 0, 0);
                layout.Controls.Add(control, 1, row);
                row++;
                if (!string.IsNullOrEmpty(tip))
                {
                    if (lastLabel != null) tempTips.SetToolTip(lastLabel, tip);
                    SetTipDeep(control, tip);
                }
            }

            // Type
            AddLabel("Type:");
            cmbType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130 };
            cmbType.Items.AddRange(new object[] { TempEvolution.TYPE_MODE_CHANGE, TempEvolution.TYPE_XROS });
            cmbType.SelectedIndexChanged += (s, e) => UpdateXrosFieldState();
            AddControl(cmbType, "Mode Change or DigiXros. Both are battle-only forms: the pet wears it for the fight and reverts afterwards, so nothing should evolve into one and it needs no Special Key. The type decides how the form is presented and coloured.");

            // Strength range (-1 = disabled, 999999 = infinite — same as normal criteria)
            AddLabel("Strength:");
            var rangePanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                WrapContents = false
            };
            numStrength[0] = new NumericUpDown { Minimum = -1, Maximum = 999999, Width = 70, Value = -1 };
            numStrength[1] = new NumericUpDown { Minimum = -1, Maximum = 999999, Width = 70, Value = -1 };
            rangePanel.Controls.Add(numStrength[0]);
            rangePanel.Controls.Add(new Label
            {
                Text = "to",
                AutoSize = true,
                Padding = new Padding(4, 6, 4, 0)
            });
            rangePanel.Controls.Add(numStrength[1]);
            AddControl(rangePanel, "Range the pet\u2019s Strength stat must be in for this form to be offered. 999999 in the right box means \"or more\"; leave it at -1 to not use the criterion.");

            // Megahit
            AddLabel("Megahit:");
            chkMegahit = new CheckBox();
            AddControl(chkMegahit, "A battle-time condition, NOT a gate on choosing the form - it does not affect whether the form is offered before the fight starts.");

            // Unlock
            AddLabel("Unlock:");
            cmbUnlock = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
            cmbUnlock.Items.Add(""); // none
            foreach (var name in unlockNames)
                cmbUnlock.Items.Add(name);
            AddControl(cmbUnlock, "A module unlock the player must already hold for this form to be offered. Leave it empty for no unlock requirement.");

            // Friend (pet list)
            AddLabel("Friend:");
            var friendPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                WrapContents = false
            };
            txtFriend = new TextBox { Width = 150, ReadOnly = true };
            var btnFriend = new Button { Text = "Edit...", Width = 60 };
            btnFriend.Click += (s, e) =>
            {
                using (var picker = new Tabs.ModuleTab.PetListEditorForm(
                    new List<string>(friendSelection), allPets))
                {
                    if (picker.ShowDialog(this) == DialogResult.OK)
                    {
                        friendSelection = picker.SelectedPets ?? new List<string>();
                        UpdateFriendText();
                    }
                }
            };
            friendPanel.Controls.Add(txtFriend);
            friendPanel.Controls.Add(btnFriend);
            AddControl(friendPanel, "Friends that must be registered before this form is offered. A Friend is registered by beating its special encounter - the pet declared with Availability \"Friend\" on the Pets tab. A pet can only ever earn the Friends its own forms ask for.");

            // Friend count (how many Friends the module's list holds)
            AddLabel("Friend count:");
            var countPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                WrapContents = false
            };
            numFriendCount[0] = new NumericUpDown { Minimum = -1, Maximum = 999999, Width = 70, Value = -1 };
            numFriendCount[1] = new NumericUpDown { Minimum = -1, Maximum = 999999, Width = 70, Value = -1 };
            countPanel.Controls.Add(numFriendCount[0]);
            countPanel.Controls.Add(new Label
            {
                Text = "to",
                AutoSize = true,
                Padding = new Padding(4, 6, 4, 0)
            });
            countPanel.Controls.Add(numFriendCount[1]);
            AddControl(countPanel, "How many Friends the module's Friend list must hold for this form to be offered (DMXW's Shoutmon X7: Superior Mode, 13 or more). 999999 in the right box means \"or more\"; leave both at -1 to not use the criterion.");

            // Battle area (the area the battle is fought in; 0 = any other battle)
            AddLabel("Battle area:");
            var areaPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                WrapContents = false
            };
            numBattleArea[0] = new NumericUpDown { Minimum = -1, Maximum = 999999, Width = 70, Value = -1 };
            numBattleArea[1] = new NumericUpDown { Minimum = -1, Maximum = 999999, Width = 70, Value = -1 };
            areaPanel.Controls.Add(numBattleArea[0]);
            areaPanel.Controls.Add(new Label
            {
                Text = "to",
                AutoSize = true,
                Padding = new Padding(4, 6, 4, 0)
            });
            areaPanel.Controls.Add(numBattleArea[1]);
            AddControl(areaPanel, "The area the battle must be fought in for this form to be offered: an adventure area of this module, or 0 for any other battle (events and special encounters, Versus, DCom). DMXW's Arresterdramon: Superior Mode is 0 to 19 (\"not available during World 20\"), its Brave Snatcher 20 to 20 (\"only available during World 20\"). 999999 in the right box means \"or more\"; leave both at -1 to not use the criterion.");

            // DP cost
            AddLabel("DP cost:");
            numXrosCost = new NumericUpDown { Minimum = 0, Maximum = 999, Width = 70, Value = 0 };
            AddControl(numXrosCost, "DP the form costs. It is offered only to a pet holding that much DP, and taking it spends the DP as the battle starts; DP comes back as the module gives it (sleep, Protein). DMXW's DigiXros: humulos's \"Required DP\", 1 for Shoutmon X2 up to 7 for Shoutmon X7. 0 = free.");

            // Xros-only: Background (module backgrounds folder, no "_high" files)
            AddLabel("Background:");
            cmbBackground = MakeThumbCombo(backgroundThumbs);
            AddControl(cmbBackground, "Background shown while this form is active in battle. Leave empty to keep the current one.");

            // Xros-only: Animation (module animations folder, "name_frame" files)
            AddLabel("Animation:");
            cmbAnimation = MakeThumbCombo(animationThumbs);
            AddControl(cmbAnimation, "Transformation animation played when the form is entered.");

            // Buttons
            btnSave = new Button { Text = "Save", Width = 80, DialogResult = DialogResult.OK };
            btnCancel = new Button { Text = "Cancel", Width = 80, DialogResult = DialogResult.Cancel };
            btnCopy = new Button { Text = "Copy", Width = 80 };
            btnPaste = new Button { Text = "Paste", Width = 80 };
            btnCopy.Click += BtnCopy_Click;
            btnPaste.Click += BtnPaste_Click;
            btnSave.Click += (s, e) => SaveEvolution();

            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                Height = 44,
                Padding = new Padding(8)
            };
            buttonPanel.Controls.Add(btnCancel);
            buttonPanel.Controls.Add(btnSave);
            buttonPanel.Controls.Add(btnPaste);
            buttonPanel.Controls.Add(btnCopy);

            this.Controls.Add(layout);
            this.Controls.Add(buttonPanel);
            this.AcceptButton = btnSave;
            this.CancelButton = btnCancel;
        }

        private void UpdateFriendText()
        {
            txtFriend.Text = friendSelection.Count == 0
                ? "(none)"
                : $"{friendSelection.Count} pet(s): {string.Join(", ", friendSelection)}";
        }

        /// <summary>
        /// Background / Animation only apply to Xros evolutions — keep them
        /// empty and disabled for Mode Change.  Megahit is the opposite: it
        /// only works for Mode Change, so it's disabled for Xros.
        /// </summary>
        private void UpdateXrosFieldState()
        {
            bool isXros = cmbType.SelectedItem?.ToString() == TempEvolution.TYPE_XROS;
            cmbBackground.Enabled = isXros;
            cmbAnimation.Enabled = isXros;
            if (!isXros)
            {
                cmbBackground.SelectedIndex = cmbBackground.Items.Count > 0 ? 0 : -1;
                cmbAnimation.SelectedIndex = cmbAnimation.Items.Count > 0 ? 0 : -1;
            }
            chkMegahit.Enabled = !isXros;
            if (isXros)
                chkMegahit.Checked = false;
        }

        // =====================================================================
        // Load / Save
        // =====================================================================

        private static void SetRange(NumericUpDown[] pair, int[] range)
        {
            bool set = range != null && range.Length >= 2;
            pair[0].Value = set ? Math.Max(-1, Math.Min(999999, range[0])) : -1;
            pair[1].Value = set ? Math.Max(-1, Math.Min(999999, range[1])) : -1;
        }

        private static int[] GetRange(NumericUpDown[] pair)
        {
            int a = (int)pair[0].Value;
            int b = (int)pair[1].Value;
            return (a == -1 && b == -1) ? null : new[] { a, b };
        }

        private void SetCost(int? cost)
        {
            numXrosCost.Value = Math.Max(0, Math.Min(999, cost ?? 0));
        }

        /// <summary>The DP cost, or null when free (not written).</summary>
        private int? GetCost()
        {
            int cost = (int)numXrosCost.Value;
            return cost > 0 ? (int?)cost : null;
        }

        private static void SelectComboValue(ComboBox cmb, string value)
        {
            if (!string.IsNullOrEmpty(value) && cmb.Items.Contains(value))
                cmb.SelectedItem = value;
            else if (cmb.Items.Count > 0)
                cmb.SelectedIndex = 0;
        }

        private void LoadEvolution()
        {
            cmbType.SelectedItem = evolution.Type == TempEvolution.TYPE_XROS
                ? TempEvolution.TYPE_XROS
                : TempEvolution.TYPE_MODE_CHANGE;

            if (evolution.Strength != null && evolution.Strength.Length >= 2)
            {
                numStrength[0].Value = Math.Max(-1, Math.Min(999999, evolution.Strength[0]));
                numStrength[1].Value = Math.Max(-1, Math.Min(999999, evolution.Strength[1]));
            }
            else
            {
                numStrength[0].Value = -1;
                numStrength[1].Value = -1;
            }

            chkMegahit.Checked = evolution.Megahit == true;
            cmbUnlock.SelectedItem = unlockNames.Contains(evolution.Unlock ?? "")
                ? evolution.Unlock
                : "";
            friendSelection = evolution.Friend != null
                ? new List<string>(evolution.Friend)
                : new List<string>();
            UpdateFriendText();
            SetRange(numFriendCount, evolution.FriendCount);
            SetRange(numBattleArea, evolution.BattleArea);
            SetCost(evolution.XrosCost);

            SelectComboValue(cmbBackground, evolution.Background);
            SelectComboValue(cmbAnimation, evolution.Animation);
            UpdateXrosFieldState();
        }

        private void SaveEvolution()
        {
            evolution.Type = cmbType.SelectedItem?.ToString() ?? TempEvolution.TYPE_MODE_CHANGE;

            int a = (int)numStrength[0].Value;
            int b = (int)numStrength[1].Value;
            evolution.Strength = (a == -1 && b == -1) ? null : new[] { a, b };

            evolution.Megahit = chkMegahit.Checked ? (bool?)true : null;

            string unlock = cmbUnlock.SelectedItem?.ToString();
            evolution.Unlock = string.IsNullOrEmpty(unlock) ? null : unlock;

            evolution.Friend = friendSelection.Count > 0 ? new List<string>(friendSelection) : null;
            evolution.FriendCount = GetRange(numFriendCount);
            evolution.BattleArea = GetRange(numBattleArea);
            evolution.XrosCost = GetCost();

            bool isXros = evolution.Type == TempEvolution.TYPE_XROS;
            string bg = cmbBackground.SelectedItem?.ToString();
            string anim = cmbAnimation.SelectedItem?.ToString();
            evolution.Background = (isXros && !string.IsNullOrEmpty(bg)) ? bg : null;
            evolution.Animation = (isXros && !string.IsNullOrEmpty(anim)) ? anim : null;
        }

        // =====================================================================
        // Copy / Paste (criteria only — the target pet is never copied)
        // =====================================================================

        private TempEvolution SnapshotUiState()
        {
            var copy = new TempEvolution();
            int a = (int)numStrength[0].Value;
            int b = (int)numStrength[1].Value;
            copy.Type = cmbType.SelectedItem?.ToString();
            copy.Strength = (a == -1 && b == -1) ? null : new[] { a, b };
            copy.Megahit = chkMegahit.Checked ? (bool?)true : null;
            string unlock = cmbUnlock.SelectedItem?.ToString();
            copy.Unlock = string.IsNullOrEmpty(unlock) ? null : unlock;
            copy.Friend = friendSelection.Count > 0 ? new List<string>(friendSelection) : null;
            copy.FriendCount = GetRange(numFriendCount);
            copy.BattleArea = GetRange(numBattleArea);
            copy.XrosCost = GetCost();
            bool isXros = copy.Type == TempEvolution.TYPE_XROS;
            string bg = cmbBackground.SelectedItem?.ToString();
            string anim = cmbAnimation.SelectedItem?.ToString();
            copy.Background = (isXros && !string.IsNullOrEmpty(bg)) ? bg : null;
            copy.Animation = (isXros && !string.IsNullOrEmpty(anim)) ? anim : null;
            return copy;
        }

        private void BtnCopy_Click(object sender, EventArgs e)
        {
            try
            {
                Clipboard.SetText(ClipboardPrefix + JsonSerializer.Serialize(SnapshotUiState()));
            }
            catch (Exception ex)
            {
                MessageBox.Show("Copy failed: " + ex.Message);
            }
        }

        private void BtnPaste_Click(object sender, EventArgs e)
        {
            try
            {
                string text = Clipboard.GetText();
                if (string.IsNullOrEmpty(text) || !text.StartsWith(ClipboardPrefix))
                {
                    MessageBox.Show("Clipboard does not contain temporary evolution criteria.");
                    return;
                }
                var pasted = JsonSerializer.Deserialize<TempEvolution>(
                    text.Substring(ClipboardPrefix.Length));
                if (pasted == null) return;

                cmbType.SelectedItem = pasted.Type == TempEvolution.TYPE_XROS
                    ? TempEvolution.TYPE_XROS
                    : TempEvolution.TYPE_MODE_CHANGE;
                if (pasted.Strength != null && pasted.Strength.Length >= 2)
                {
                    numStrength[0].Value = Math.Max(-1, Math.Min(999999, pasted.Strength[0]));
                    numStrength[1].Value = Math.Max(-1, Math.Min(999999, pasted.Strength[1]));
                }
                else
                {
                    numStrength[0].Value = -1;
                    numStrength[1].Value = -1;
                }
                chkMegahit.Checked = pasted.Megahit == true;
                cmbUnlock.SelectedItem = unlockNames.Contains(pasted.Unlock ?? "") ? pasted.Unlock : "";
                friendSelection = pasted.Friend != null ? new List<string>(pasted.Friend) : new List<string>();
                UpdateFriendText();
                SetRange(numFriendCount, pasted.FriendCount);
                SetRange(numBattleArea, pasted.BattleArea);
                SetCost(pasted.XrosCost);
                SelectComboValue(cmbBackground, pasted.Background);
                SelectComboValue(cmbAnimation, pasted.Animation);
                UpdateXrosFieldState();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Paste failed: " + ex.Message);
            }
        }
    }
}
