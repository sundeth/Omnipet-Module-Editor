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
    /// Mode Change / Xros type, strength range, megahit, unlock and friend
    /// pets — plus, for Xros only, the background and animation used by the
    /// in-game xros sequence.
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
        private CheckBox chkMegahit;
        private ComboBox cmbUnlock;
        private TextBox txtFriend;
        private List<string> friendSelection = new List<string>();

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
            this.Size = new Size(440, 480);
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

            void AddLabel(string text)
            {
                layout.Controls.Add(new Label
                {
                    Text = text,
                    Anchor = AnchorStyles.Left,
                    AutoSize = true,
                    Font = new Font("Segoe UI", 8),
                    Margin = new Padding(0, 6, 0, 0)
                }, 0, row);
            }

            void AddControl(Control control)
            {
                control.Margin = new Padding(0, 4, 0, 0);
                layout.Controls.Add(control, 1, row);
                row++;
            }

            // Type
            AddLabel("Type:");
            cmbType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130 };
            cmbType.Items.AddRange(new object[] { TempEvolution.TYPE_MODE_CHANGE, TempEvolution.TYPE_XROS });
            cmbType.SelectedIndexChanged += (s, e) => UpdateXrosFieldState();
            AddControl(cmbType);

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
            AddControl(rangePanel);

            // Megahit
            AddLabel("Megahit:");
            chkMegahit = new CheckBox();
            AddControl(chkMegahit);

            // Unlock
            AddLabel("Unlock:");
            cmbUnlock = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
            cmbUnlock.Items.Add(""); // none
            foreach (var name in unlockNames)
                cmbUnlock.Items.Add(name);
            AddControl(cmbUnlock);

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
            AddControl(friendPanel);

            // Xros-only: Background (module backgrounds folder, no "_high" files)
            AddLabel("Background:");
            cmbBackground = MakeThumbCombo(backgroundThumbs);
            AddControl(cmbBackground);

            // Xros-only: Animation (module animations folder, "name_frame" files)
            AddLabel("Animation:");
            cmbAnimation = MakeThumbCombo(animationThumbs);
            AddControl(cmbAnimation);

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
