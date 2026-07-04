using OmnipetModuleEditor.DigimonSync;
using OmnipetModuleEditor.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Forms;

namespace OmnipetModuleEditor.Tabs
{
    /// <summary>
    /// Collection tab: collectable cards, their effects (keyed by binary value)
    /// and card packs — persisted in the module's cards.json with sprites under
    /// the "cards" folder. See Documentation/CollectionSystem_Spec.md (Omnipet).
    /// </summary>
    public partial class CollectionTab : UserControl
    {
        private string modulePath;
        private Module module;
        private CollectionFile collection = new CollectionFile();
        private List<Item> moduleItems = new List<Item>();

        private TabControl innerTabs;

        public CollectionTab()
        {
            InitializeComponent();
            this.Name = "CollectionTab";
        }

        public void SetModule(string modulePath, Module module)
        {
            this.modulePath = modulePath;
            this.module = module;
            collection = CollectionFile.Load(modulePath);
            moduleItems = controls.HTMLGenerator.ReadItems(modulePath);
            thumbCache.Clear();
            PopulateCardList();
            PopulateEffectGroupList();
            PopulatePackList();
        }

        /// <summary>Called by the main form's SaveAll (via reflection).</summary>
        public void Save()
        {
            ApplyCardFields(false);
            ApplyEffectGroupFields();
            ApplyPackFields();
            if (string.IsNullOrEmpty(modulePath)) return;
            collection.Save(modulePath);
        }

        #region Layout

        private void InitializeComponent()
        {
            this.Dock = DockStyle.Fill;
            innerTabs = new TabControl { Dock = DockStyle.Fill };

            var cardsPage = new TabPage("Cards");
            cardsPage.Controls.Add(BuildCardsPage());
            innerTabs.TabPages.Add(cardsPage);

            var effectsPage = new TabPage("Effects");
            effectsPage.Controls.Add(BuildEffectsPage());
            innerTabs.TabPages.Add(effectsPage);

            var packsPage = new TabPage("Card Packs");
            packsPage.Controls.Add(BuildPacksPage());
            innerTabs.TabPages.Add(packsPage);

            this.Controls.Add(innerTabs);
        }

        #endregion

        // ====================================================================
        // Cards tab
        // ====================================================================
        #region Cards tab

        private ComboBox cmbFilter;
        private ListBox lstCards;
        private PictureBox pbFront, pbBack;
        private TextBox txtCardName, txtCardSeries, txtCardValue;
        private ComboBox cmbCardType, cmbCardLr, cmbCardRarity;
        private NumericUpDown numCardNumber;
        private Button btnCardSave, btnCardCancel, btnCardRfid;

        private List<CollectionCard> filteredCards = new List<CollectionCard>();
        private CollectionCard selectedCard;
        private readonly Dictionary<string, Image> thumbCache = new Dictionary<string, Image>();

        private Control BuildCardsPage()
        {
            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(8),
            };
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            // ---- left: filter + list + buttons ----
            var leftPanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
            leftPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
            leftPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            leftPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 68F));

            cmbFilter = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbFilter.Items.Add("All");
            foreach (var t in CollectionCard.AllTypes) cmbFilter.Items.Add(t);
            cmbFilter.SelectedIndex = 0;
            cmbFilter.SelectedIndexChanged += (s, e) => PopulateCardList();
            leftPanel.Controls.Add(cmbFilter, 0, 0);

            lstCards = new ListBox
            {
                Dock = DockStyle.Fill,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 44,
            };
            lstCards.DrawItem += LstCards_DrawItem;
            lstCards.SelectedIndexChanged += LstCards_SelectedIndexChanged;
            leftPanel.Controls.Add(lstCards, 0, 1);

            var buttonPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(2) };
            var btnAdd = new Button { Text = "Add", Width = 60 };
            var btnImport = new Button { Text = "Import", Width = 60 };
            var btnEditArt = new Button { Text = "Edit Art", Width = 62 };
            var btnRemove = new Button { Text = "Remove", Width = 62 };
            btnAdd.Click += BtnCardAdd_Click;
            btnImport.Click += BtnCardImport_Click;
            btnEditArt.Click += BtnCardEditArt_Click;
            btnRemove.Click += BtnCardRemove_Click;
            buttonPanel.Controls.AddRange(new Control[] { btnAdd, btnImport, btnEditArt, btnRemove });
            leftPanel.Controls.Add(buttonPanel, 0, 2);

            mainLayout.Controls.Add(leftPanel, 0, 0);

            // ---- right: sprites + fields ----
            var rightPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 10,
                Padding = new Padding(16, 8, 8, 8),
            };
            rightPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90F));
            rightPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            rightPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 200F));

            var spritesFlow = new FlowLayoutPanel { Dock = DockStyle.Fill };
            pbFront = MakeCardSpriteBox("Front\n(click to set)");
            pbBack = MakeCardSpriteBox("Back\n(click to set)");
            pbFront.Click += (s, e) => UploadCardSprite(true);
            pbBack.Click += (s, e) => UploadCardSprite(false);
            spritesFlow.Controls.Add(WrapLabeled(pbFront, "Front"));
            spritesFlow.Controls.Add(WrapLabeled(pbBack, "Back"));
            rightPanel.Controls.Add(new Label { Text = "Sprites:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 0);
            rightPanel.Controls.Add(spritesFlow, 1, 0);

            int row = 1;
            rightPanel.Controls.Add(new Label { Text = "Name:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, row);
            txtCardName = new TextBox { Dock = DockStyle.Fill };
            rightPanel.Controls.Add(txtCardName, 1, row++);

            rightPanel.Controls.Add(new Label { Text = "Type:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, row);
            cmbCardType = new ComboBox { Dock = DockStyle.Left, Width = 160, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbCardType.Items.AddRange(CollectionCard.AllTypes);
            rightPanel.Controls.Add(cmbCardType, 1, row++);

            rightPanel.Controls.Add(new Label { Text = "Series:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, row);
            txtCardSeries = new TextBox { Dock = DockStyle.Left, Width = 80 };
            rightPanel.Controls.Add(txtCardSeries, 1, row++);

            rightPanel.Controls.Add(new Label { Text = "Number:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, row);
            numCardNumber = new NumericUpDown { Dock = DockStyle.Left, Width = 80, Minimum = 0, Maximum = 9999 };
            rightPanel.Controls.Add(numCardNumber, 1, row++);

            rightPanel.Controls.Add(new Label { Text = "Value:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, row);
            txtCardValue = new TextBox { Dock = DockStyle.Left, Width = 120, MaxLength = 10 };
            rightPanel.Controls.Add(txtCardValue, 1, row++);

            rightPanel.Controls.Add(new Label { Text = "L / R:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, row);
            cmbCardLr = new ComboBox { Dock = DockStyle.Left, Width = 80, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbCardLr.Items.AddRange(new object[] { "L/R", "L", "R" });
            rightPanel.Controls.Add(cmbCardLr, 1, row++);

            rightPanel.Controls.Add(new Label { Text = "Rarity:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, row);
            cmbCardRarity = new ComboBox { Dock = DockStyle.Left, Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbCardRarity.Items.AddRange(CollectionCard.Rarities);
            rightPanel.Controls.Add(cmbCardRarity, 1, row++);

            var cardBtns = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
            btnCardSave = new Button { Text = "Save", Width = 80, Margin = new Padding(8) };
            btnCardCancel = new Button { Text = "Cancel", Width = 80, Margin = new Padding(8) };
            btnCardRfid = new Button { Text = "Export RFID Data", Width = 130, Margin = new Padding(8) };
            btnCardSave.Click += (s, e) => ApplyCardFields(true);
            btnCardCancel.Click += (s, e) => { if (selectedCard != null) LoadCardFields(selectedCard); };
            btnCardRfid.Click += BtnCardRfid_Click;
            cardBtns.Controls.AddRange(new Control[] { btnCardSave, btnCardCancel, btnCardRfid });
            rightPanel.Controls.Add(new Label(), 0, row);
            rightPanel.Controls.Add(cardBtns, 1, row++);

            mainLayout.Controls.Add(rightPanel, 1, 0);
            return mainLayout;
        }

        private PictureBox MakeCardSpriteBox(string emptyText)
        {
            var pb = new PictureBox
            {
                Width = 125,
                Height = 175,
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                Cursor = Cursors.Hand,
                Margin = new Padding(4),
                Tag = emptyText,
            };
            pb.Paint += (s, e) =>
            {
                if (pb.Image == null)
                {
                    using (var f = new Font(FontFamily.GenericSansSerif, 8))
                    using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                        e.Graphics.DrawString((string)pb.Tag, f, Brushes.Gray, pb.ClientRectangle, sf);
                }
            };
            return pb;
        }

        private Control WrapLabeled(Control inner, string label)
        {
            var panel = new Panel { Width = inner.Width + 8, Height = inner.Height + 24, Margin = new Padding(4) };
            inner.Location = new Point(4, 0);
            var lbl = new Label { Text = label, Location = new Point(4, inner.Height + 2), AutoSize = true, ForeColor = Color.DimGray };
            panel.Controls.Add(inner);
            panel.Controls.Add(lbl);
            return panel;
        }

        private void PopulateCardList()
        {
            string filter = cmbFilter.SelectedItem?.ToString() ?? "All";
            var ordered = collection.Ordered();
            if (filter == CollectionCard.TypeCustom)
                filteredCards = ordered.Where(c => c.Type == CollectionCard.TypeCustom || c.CustomArt).ToList();
            else if (filter != "All")
                filteredCards = ordered.Where(c => c.Type == filter).ToList();
            else
                filteredCards = ordered;

            lstCards.BeginUpdate();
            lstCards.Items.Clear();
            foreach (var c in filteredCards)
                lstCards.Items.Add(c.DisplayLabel);
            lstCards.EndUpdate();
            if (filteredCards.Count == 0)
            {
                selectedCard = null;
                ClearCardFields();
            }
        }

        private void LstCards_DrawItem(object sender, DrawItemEventArgs e)
        {
            e.DrawBackground();
            if (e.Index < 0 || e.Index >= filteredCards.Count) return;
            var card = filteredCards[e.Index];

            var thumb = GetThumb(card);
            if (thumb != null)
                e.Graphics.DrawImage(thumb, e.Bounds.X + 4, e.Bounds.Y + 2, 28, 40);
            else
            {
                using (var pen = new Pen(Color.LightGray))
                    e.Graphics.DrawRectangle(pen, e.Bounds.X + 4, e.Bounds.Y + 2, 28, 40);
            }

            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            using (var nameBrush = new SolidBrush(selected ? SystemColors.HighlightText : SystemColors.ControlText))
            using (var subBrush = new SolidBrush(selected ? SystemColors.HighlightText : Color.DimGray))
            using (var nameFont = new Font(FontFamily.GenericSansSerif, 9, FontStyle.Bold))
            using (var subFont = new Font(FontFamily.GenericSansSerif, 7.5f))
            {
                string title = card.Type == CollectionCard.TypeSoulPlate
                    ? (card.Name ?? "") : $"{card.Name}  #{card.Number}";
                e.Graphics.DrawString(title, nameFont, nameBrush, e.Bounds.X + 38, e.Bounds.Y + 6);
                string sub = card.Type + (card.CustomArt ? " (custom art)" : "");
                e.Graphics.DrawString(sub, subFont, subBrush, e.Bounds.X + 38, e.Bounds.Y + 24);
            }
            e.DrawFocusRectangle();
        }

        private Image GetThumb(CollectionCard card)
        {
            if (card?.Id == null) return null;
            if (thumbCache.TryGetValue(card.Id, out var cached)) return cached;
            Image img = LoadSpriteImage(card.Sprites?.Front);
            thumbCache[card.Id] = img;   // null cached too (avoids disk hits)
            return img;
        }

        private Image LoadSpriteImage(string relativePath)
        {
            if (string.IsNullOrEmpty(modulePath) || string.IsNullOrEmpty(relativePath)) return null;
            string path = Path.Combine(modulePath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) return null;
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var img = Image.FromStream(fs))
                    return new Bitmap(img);
            }
            catch { return null; }
        }

        private void LstCards_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lstCards.SelectedIndex < 0 || lstCards.SelectedIndex >= filteredCards.Count)
            {
                selectedCard = null;
                ClearCardFields();
                return;
            }
            selectedCard = filteredCards[lstCards.SelectedIndex];
            LoadCardFields(selectedCard);
        }

        private void LoadCardFields(CollectionCard card)
        {
            txtCardName.Text = card.Name ?? "";
            cmbCardType.SelectedItem = card.Type;
            txtCardSeries.Text = card.Series ?? "";
            numCardNumber.Value = Math.Max(0, Math.Min(9999, card.Number));
            txtCardValue.Text = card.Value ?? "";
            cmbCardLr.SelectedItem = string.IsNullOrEmpty(card.Lr) ? "L/R" : card.Lr;
            cmbCardLr.Enabled = card.Type == CollectionCard.TypeIdPlate;
            cmbCardRarity.SelectedItem = card.Rarity ?? "Common";
            RefreshCardSpriteBoxes(card);
        }

        private void ClearCardFields()
        {
            txtCardName.Text = "";
            cmbCardType.SelectedIndex = -1;
            txtCardSeries.Text = "";
            numCardNumber.Value = 0;
            txtCardValue.Text = "";
            cmbCardLr.SelectedIndex = -1;
            cmbCardRarity.SelectedIndex = -1;
            pbFront.Image = null;
            pbBack.Image = null;
        }

        private void RefreshCardSpriteBoxes(CollectionCard card)
        {
            pbFront.Image = LoadSpriteImage(card.Sprites?.Front);
            string back = card.Type == CollectionCard.TypeDdpChip
                ? CollectionFile.SpritesFolder + "/" + CollectionFile.DdpSharedBack
                : card.Sprites?.Back;
            pbBack.Image = LoadSpriteImage(back);
            pbFront.Invalidate();
            pbBack.Invalidate();
        }

        /// <summary>Writes field edits back to the selected card. `interactive`
        /// shows the "won't change the artwork" warning on divergence.</summary>
        private void ApplyCardFields(bool interactive)
        {
            if (selectedCard == null || cmbCardType.SelectedItem == null) return;

            string value = txtCardValue.Text.Trim();
            if (!IsValidBinary(value))
            {
                if (interactive)
                    MessageBox.Show("Value must be 1 to 10 binary digits (0/1).", "Invalid Value",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            bool diverges = selectedCard.Sprites?.Front != null &&
                (selectedCard.Name != txtCardName.Text
                 || selectedCard.Type != cmbCardType.SelectedItem.ToString()
                 || (selectedCard.Series ?? "") != txtCardSeries.Text.Trim()
                 || selectedCard.Number != (int)numCardNumber.Value);
            if (interactive && diverges)
            {
                var r = MessageBox.Show(
                    "Changing these fields will NOT update the card's artwork —\n" +
                    "the sprites keep showing the values they were rendered with.\n\nSave anyway?",
                    "Artwork unchanged", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
                if (r != DialogResult.OK) return;
            }

            selectedCard.Name = txtCardName.Text;
            selectedCard.Type = cmbCardType.SelectedItem.ToString();
            selectedCard.Series = string.IsNullOrWhiteSpace(txtCardSeries.Text) ? null : txtCardSeries.Text.Trim();
            selectedCard.Number = (int)numCardNumber.Value;
            selectedCard.Value = value;
            selectedCard.Lr = selectedCard.Type == CollectionCard.TypeSoulPlate ? "L/R"
                : selectedCard.Type == CollectionCard.TypeIdPlate ? (cmbCardLr.SelectedItem?.ToString() ?? "L/R")
                : null;
            selectedCard.Rarity = cmbCardRarity.SelectedItem?.ToString() ?? "Common";

            var keep = selectedCard;
            PopulateCardList();
            SelectCard(keep);
        }

        private static bool IsValidBinary(string v)
        {
            if (string.IsNullOrEmpty(v) || v.Length > 10) return false;
            foreach (char c in v) if (c != '0' && c != '1') return false;
            return true;
        }

        private void SelectCard(CollectionCard card)
        {
            int idx = filteredCards.IndexOf(card);
            if (idx < 0 && filteredCards.Count > 0) idx = 0;
            lstCards.SelectedIndex = idx;
        }

        // ---- Add ----
        private void BtnCardAdd_Click(object sender, EventArgs e)
        {
            string type = PromptCardType();
            if (type == null) return;

            var card = new CollectionCard
            {
                Id = Guid.NewGuid().ToString(),
                Type = type,
                Name = "New Card",
                Number = collection.NextNumber(type),
                Series = type == CollectionCard.TypeSoulPlate ? null : "1",
                Value = type == CollectionCard.TypeDdpChip ? "000100" :
                        type == CollectionCard.TypeCustom ? "0" : "00100",
                Lr = type == CollectionCard.TypeSoulPlate ? "L/R"
                   : type == CollectionCard.TypeIdPlate ? "L/R" : null,
                Rarity = "Common",
                CustomArt = type == CollectionCard.TypeCustom,
            };
            collection.Cards.Add(card);
            PopulateCardList();
            SelectCard(card);
        }

        private string PromptCardType()
        {
            using (var dlg = new Form
            {
                Text = "Add Card",
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                Width = 300,
                Height = 140,
                MinimizeBox = false,
                MaximizeBox = false,
            })
            {
                var lbl = new Label { Text = "Card type:", Location = new Point(12, 15), AutoSize = true };
                var cmb = new ComboBox
                {
                    Location = new Point(90, 12),
                    Width = 180,
                    DropDownStyle = ComboBoxStyle.DropDownList,
                };
                cmb.Items.AddRange(CollectionCard.AllTypes);
                cmb.SelectedIndex = 0;
                var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(110, 60), Width = 75 };
                var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(195, 60), Width = 75 };
                dlg.Controls.AddRange(new Control[] { lbl, cmb, ok, cancel });
                dlg.AcceptButton = ok;
                dlg.CancelButton = cancel;
                return dlg.ShowDialog(this) == DialogResult.OK ? cmb.SelectedItem.ToString() : null;
            }
        }

        // ---- Remove ----
        private void BtnCardRemove_Click(object sender, EventArgs e)
        {
            if (selectedCard == null) return;
            var r = MessageBox.Show(
                $"Remove the card \"{selectedCard.DisplayLabel}\"?\nIts sprite files will be deleted.",
                "Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (r != DialogResult.Yes) return;

            DeleteCardSprites(selectedCard);
            collection.Cards.Remove(selectedCard);
            // remove pack entries pointing at it
            foreach (var pack in collection.Packs)
                pack.Cards.RemoveAll(p => p.Id == selectedCard.Id);
            thumbCache.Remove(selectedCard.Id);
            selectedCard = null;
            PopulateCardList();
            ClearCardFields();
        }

        private void DeleteCardSprites(CollectionCard card)
        {
            TryDeleteRelative(card.Sprites?.Front);
            // per-card back only (DDP shares one back)
            if (card.Type != CollectionCard.TypeDdpChip)
                TryDeleteRelative(card.Sprites?.Back);
            else if (!collection.Cards.Any(c => c != card && c.Type == CollectionCard.TypeDdpChip))
                TryDeleteRelative(CollectionFile.SpritesFolder + "/" + CollectionFile.DdpSharedBack);
        }

        private void TryDeleteRelative(string relative)
        {
            if (string.IsNullOrEmpty(relative) || string.IsNullOrEmpty(modulePath)) return;
            try
            {
                string p = Path.Combine(modulePath, relative.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(p)) File.Delete(p);
            }
            catch { /* best effort */ }
        }

        // ---- sprite upload (click on the preview box) ----
        private void UploadCardSprite(bool front)
        {
            if (selectedCard == null || string.IsNullOrEmpty(modulePath)) return;

            if (!front && selectedCard.Type == CollectionCard.TypeDdpChip)
            {
                var warn = MessageBox.Show(
                    "DDP Chips share a single back sprite — replacing it affects ALL DDP cards.\nContinue?",
                    "Shared back", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (warn != DialogResult.Yes) return;
            }

            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "Select card sprite";
                dialog.Filter = "PNG files (*.png)|*.png";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    EnsureCardsFolder();
                    string relative;
                    if (!front && selectedCard.Type == CollectionCard.TypeDdpChip)
                        relative = CollectionFile.SpritesFolder + "/" + CollectionFile.DdpSharedBack;
                    else
                        relative = CollectionFile.SpritesFolder + "/" + selectedCard.Id + (front ? ".png" : "_back.png");

                    using (var src = new Bitmap(dialog.FileName))
                    using (var resized = FitWithin(src, CollectionCard.SpriteSize(selectedCard.Type)))
                        resized.Save(Path.Combine(modulePath, relative.Replace('/', Path.DirectorySeparatorChar)),
                            System.Drawing.Imaging.ImageFormat.Png);

                    if (front) selectedCard.Sprites.Front = relative;
                    else if (selectedCard.Type != CollectionCard.TypeDdpChip) selectedCard.Sprites.Back = relative;

                    thumbCache.Remove(selectedCard.Id);
                    if (!front && selectedCard.Type == CollectionCard.TypeDdpChip) thumbCache.Clear();
                    RefreshCardSpriteBoxes(selectedCard);
                    lstCards.Invalidate();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed to import sprite: " + ex.Message, "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void EnsureCardsFolder()
        {
            string folder = Path.Combine(modulePath, CollectionFile.SpritesFolder);
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
        }

        /// <summary>Scales down (never up) to fit inside `box`, preserving aspect.</summary>
        private static Bitmap FitWithin(Bitmap src, Size box)
        {
            if (src.Width <= box.Width && src.Height <= box.Height)
                return new Bitmap(src);
            double scale = Math.Min((double)box.Width / src.Width, (double)box.Height / src.Height);
            int w = Math.Max(1, (int)Math.Round(src.Width * scale));
            int h = Math.Max(1, (int)Math.Round(src.Height * scale));
            var bmp = new Bitmap(w, h);
            using (var g = Graphics.FromImage(bmp))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(src, 0, 0, w, h);
            }
            return bmp;
        }

        // ---- RFID export ----
        private void BtnCardRfid_Click(object sender, EventArgs e)
        {
            if (selectedCard == null) return;
            using (var dialog = new SaveFileDialog())
            {
                dialog.Title = "Export RFID data";
                dialog.Filter = "JSON files (*.json)|*.json";
                string safe = string.Concat((selectedCard.Name ?? "card").Split(Path.GetInvalidFileNameChars()));
                dialog.FileName = safe + "_rfid.json";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    File.WriteAllText(dialog.FileName, selectedCard.BuildRfidJson());
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed to export: " + ex.Message, "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        // ---- Edit Art ----
        private void BtnCardEditArt_Click(object sender, EventArgs e)
        {
            if (selectedCard == null) return;
            if (selectedCard.Type == CollectionCard.TypeCustom)
            {
                MessageBox.Show("Custom cards have no card-maker data — edit their sprites directly.",
                    "Edit Art", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (selectedCard.CustomArt)
            {
                MessageBox.Show(
                    "This card uses custom (uploaded) artwork, which is not stored on the site.\n" +
                    "Open the card maker manually and re-upload the artwork to recreate it.",
                    "Edit Art", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (selectedCard.Art == null)
            {
                MessageBox.Show("This card has no card-maker data (art) to edit.",
                    "Edit Art", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                var node = JsonNode.Parse(selectedCard.Art.Value.GetRawText()).AsObject();
                // sync the editor-editable fields so the card maker opens current
                node["id"] = selectedCard.Id;
                node["name"] = selectedCard.Name ?? "";
                node["rarity"] = selectedCard.Rarity ?? "Common";
                switch (selectedCard.Type)
                {
                    case CollectionCard.TypeSoulPlate:
                        node["binary"] = selectedCard.Value;
                        break;
                    case CollectionCard.TypeIdPlate:
                        node["series"] = selectedCard.Series ?? "1";
                        node["card_number"] = selectedCard.Number.ToString();
                        node["id_binary"] = selectedCard.Value;
                        node["lr"] = selectedCard.Lr ?? "L/R";
                        break;
                    case CollectionCard.TypeDdpChip:
                        node["ddp_file"] = selectedCard.Series ?? "1";
                        node["card_number"] = selectedCard.Number.ToString();
                        node["ddp_binary"] = selectedCard.Value;
                        break;
                }
                string b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(node.ToJsonString()))
                    .Replace('+', '-').Replace('/', '_');
                string url = DigimonDbClient.BaseUrl + "/card-maker#card=" + b64;
                System.Diagnostics.Process.Start(url);
                MessageBox.Show(
                    "The card maker was opened in your browser.\n\n" +
                    "After editing, download the ZIP and use Import here — the card\n" +
                    "will be matched by its id and you can replace it.",
                    "Edit Art", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to open the card maker: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion

        // ====================================================================
        // Import (card maker ZIPs)
        // ====================================================================
        #region Import

        private void BtnCardImport_Click(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "Import card ZIPs (Digimon Database card maker)";
                dialog.Filter = "ZIP files (*.zip)|*.zip";
                dialog.Multiselect = true;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                ImportCardZips(dialog.FileNames);
            }
        }

        private class ImportCandidate
        {
            public CollectionCard Card;
            public byte[] FrontPng;
            public byte[] BackPng;
        }

        private void ImportCardZips(string[] zipPaths)
        {
            var candidates = new List<ImportCandidate>();
            var errors = new List<string>();

            foreach (var zipPath in zipPaths)
            {
                try
                {
                    var cand = ParseCardZip(zipPath);
                    if (cand != null) candidates.Add(cand);
                    else errors.Add(Path.GetFileName(zipPath) + ": no card json found");
                }
                catch (Exception ex)
                {
                    errors.Add(Path.GetFileName(zipPath) + ": " + ex.Message);
                }
            }

            // uuid conflicts: ask once for the whole batch
            var byId = collection.Cards.ToDictionary(c => c.Id, c => c);
            var idConflicts = candidates.Where(c => byId.ContainsKey(c.Card.Id)).ToList();
            bool replaceById = false;
            if (idConflicts.Count > 0)
            {
                var r = MessageBox.Show(
                    $"{idConflicts.Count} imported card(s) already exist in this module (same id).\n" +
                    "Replace the existing ones?",
                    "Import", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                replaceById = r == DialogResult.Yes;
            }

            int imported = 0, skipped = 0;
            foreach (var cand in candidates)
            {
                if (byId.TryGetValue(cand.Card.Id, out var existingById))
                {
                    if (!replaceById) { skipped++; continue; }
                    ReplaceCard(existingById, cand);
                    imported++;
                    continue;
                }

                // same type + number + name → ask per card
                var dup = collection.Cards.FirstOrDefault(c =>
                    c.Type == cand.Card.Type && c.Number == cand.Card.Number &&
                    string.Equals(c.Name, cand.Card.Name, StringComparison.OrdinalIgnoreCase));
                if (dup != null)
                {
                    var r = MessageBox.Show(
                        $"A {cand.Card.Type} card \"{cand.Card.Name}\" #{cand.Card.Number} already exists.\n\n" +
                        "Yes = override the existing card\nNo = add as a new card\nCancel = skip",
                        "Duplicate card", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                    if (r == DialogResult.Cancel) { skipped++; continue; }
                    if (r == DialogResult.Yes)
                    {
                        ReplaceCard(dup, cand);
                        imported++;
                        continue;
                    }
                    cand.Card.Id = Guid.NewGuid().ToString();   // add as new
                }

                WriteCandidateSprites(cand);
                collection.Cards.Add(cand.Card);
                byId[cand.Card.Id] = cand.Card;
                imported++;
            }

            PopulateCardList();
            string msg = $"Imported {imported} card(s).";
            if (skipped > 0) msg += $" Skipped {skipped}.";
            if (errors.Count > 0) msg += "\n\nErrors:\n" + string.Join("\n", errors);
            MessageBox.Show(msg, "Import", MessageBoxButtons.OK,
                errors.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }

        private void ReplaceCard(CollectionCard existing, ImportCandidate cand)
        {
            int idx = collection.Cards.IndexOf(existing);
            DeleteCardSprites(existing);
            thumbCache.Remove(existing.Id);
            // pack entries follow the id — keep them pointing at the replacement
            string oldId = existing.Id;
            collection.Cards[idx] = cand.Card;
            if (oldId != cand.Card.Id)
                foreach (var pack in collection.Packs)
                    foreach (var entry in pack.Cards)
                        if (entry.Id == oldId) entry.Id = cand.Card.Id;
            WriteCandidateSprites(cand);
        }

        private void WriteCandidateSprites(ImportCandidate cand)
        {
            EnsureCardsFolder();
            var size = CollectionCard.SpriteSize(cand.Card.Type);

            if (cand.FrontPng != null)
            {
                string rel = CollectionFile.SpritesFolder + "/" + cand.Card.Id + ".png";
                SaveResizedPng(cand.FrontPng, size, rel);
                cand.Card.Sprites.Front = rel;
            }
            if (cand.BackPng != null)
            {
                string rel = cand.Card.Type == CollectionCard.TypeDdpChip
                    ? CollectionFile.SpritesFolder + "/" + CollectionFile.DdpSharedBack
                    : CollectionFile.SpritesFolder + "/" + cand.Card.Id + "_back.png";
                SaveResizedPng(cand.BackPng, size, rel);
                cand.Card.Sprites.Back = rel;
            }
            else if (cand.Card.Type == CollectionCard.TypeDdpChip &&
                     File.Exists(Path.Combine(modulePath, CollectionFile.SpritesFolder, CollectionFile.DdpSharedBack)))
            {
                cand.Card.Sprites.Back = CollectionFile.SpritesFolder + "/" + CollectionFile.DdpSharedBack;
            }
            thumbCache.Remove(cand.Card.Id);
        }

        private void SaveResizedPng(byte[] pngBytes, Size target, string relative)
        {
            using (var ms = new MemoryStream(pngBytes))
            using (var src = new Bitmap(ms))
            {
                Bitmap output;
                if (src.Width == target.Width && src.Height == target.Height)
                    output = new Bitmap(src);
                else
                {
                    output = new Bitmap(target.Width, target.Height);
                    using (var g = Graphics.FromImage(output))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.DrawImage(src, 0, 0, target.Width, target.Height);
                    }
                }
                using (output)
                    output.Save(Path.Combine(modulePath, relative.Replace('/', Path.DirectorySeparatorChar)),
                        System.Drawing.Imaging.ImageFormat.Png);
            }
        }

        private ImportCandidate ParseCardZip(string zipPath)
        {
            using (var zip = ZipFile.OpenRead(zipPath))
            {
                var jsonEntry = zip.Entries.FirstOrDefault(en =>
                    en.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
                if (jsonEntry == null) return null;

                string json;
                using (var reader = new StreamReader(jsonEntry.Open(), Encoding.UTF8))
                    json = reader.ReadToEnd();

                using (var doc = JsonDocument.Parse(json))
                {
                    var root = doc.RootElement;
                    var card = BuildCardFromMakerJson(root);
                    if (card == null) return null;

                    string baseName = Path.GetFileNameWithoutExtension(jsonEntry.FullName);
                    var cand = new ImportCandidate { Card = card };
                    cand.FrontPng = ReadZipEntry(zip, baseName + ".png");
                    cand.BackPng = ReadZipEntry(zip, baseName + "_back.png");
                    return cand;
                }
            }
        }

        private static byte[] ReadZipEntry(ZipArchive zip, string name)
        {
            var entry = zip.Entries.FirstOrDefault(en =>
                string.Equals(en.FullName, name, StringComparison.OrdinalIgnoreCase));
            if (entry == null) return null;
            using (var ms = new MemoryStream())
            using (var s = entry.Open())
            {
                s.CopyTo(ms);
                return ms.ToArray();
            }
        }

        private static string GetString(JsonElement root, string prop)
        {
            return root.TryGetProperty(prop, out var el) && el.ValueKind == JsonValueKind.String
                ? el.GetString() : null;
        }

        /// <summary>Maps a card-maker JSON into a collection card (art kept verbatim).</summary>
        private CollectionCard BuildCardFromMakerJson(JsonElement root)
        {
            string type = GetString(root, "type");
            if (type != CollectionCard.TypeSoulPlate && type != CollectionCard.TypeIdPlate &&
                type != CollectionCard.TypeDdpChip)
                return null;

            var card = new CollectionCard
            {
                Type = type,
                Name = GetString(root, "name") ?? "",
                Rarity = GetString(root, "rarity") ?? "Common",
                Art = root.Clone(),
            };

            string id = GetString(root, "id");
            card.Id = (id != null && Guid.TryParse(id, out _)) ? id : Guid.NewGuid().ToString();

            string numberStr = GetString(root, "card_number");
            int number;
            if (!int.TryParse(numberStr, out number)) number = 0;

            switch (type)
            {
                case CollectionCard.TypeSoulPlate:
                    card.Number = 0;
                    card.Value = GetString(root, "binary") ?? "00100";
                    card.Lr = "L/R";
                    break;
                case CollectionCard.TypeIdPlate:
                    card.Number = number;
                    card.Series = GetString(root, "series");
                    card.Value = GetString(root, "id_binary") ?? "00100";
                    card.Lr = GetString(root, "lr") ?? "L/R";
                    break;
                case CollectionCard.TypeDdpChip:
                    card.Number = number;
                    card.Series = GetString(root, "ddp_file");
                    card.Value = GetString(root, "ddp_binary") ?? "000100";
                    break;
            }

            // custom artwork: uploaded in the card maker → not recoverable from the site
            if (root.TryGetProperty("artwork", out var art) && art.ValueKind == JsonValueKind.Object)
            {
                bool hasUrl = art.TryGetProperty("url", out var url) &&
                              url.ValueKind == JsonValueKind.String &&
                              !string.IsNullOrEmpty(url.GetString());
                card.CustomArt = !hasUrl && type != CollectionCard.TypeSoulPlate;
            }
            return card;
        }

        #endregion

        // ====================================================================
        // Effects tab
        // ====================================================================
        #region Effects tab

        private ListBox lstEffectGroups;
        private TextBox txtGroupValue;
        private ComboBox cmbGroupLr;
        private ListBox lstGroupEffects;
        private ComboBox cmbFxType, cmbFxItem, cmbFxDna, cmbFxUnlock;
        private NumericUpDown numFxAmount, numFxArea, numFxRound, numFxVersion;
        private Label lblFxItem, lblFxDna, lblFxAmount, lblFxArea, lblFxRound, lblFxUnlock;

        private CardEffectGroup selectedGroup;
        private CardEffect selectedEffect;

        private Control BuildEffectsPage()
        {
            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(8),
            };
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            // left: binary list + add/remove
            var leftPanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
            leftPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            leftPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));

            lstEffectGroups = new ListBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 11),
            };
            lstEffectGroups.SelectedIndexChanged += LstEffectGroups_SelectedIndexChanged;
            leftPanel.Controls.Add(lstEffectGroups, 0, 0);

            var btns = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(2) };
            var btnAdd = new Button { Text = "Add", Width = 70 };
            var btnRemove = new Button { Text = "Remove", Width = 70 };
            btnAdd.Click += (s, e) =>
            {
                var g = new CardEffectGroup { Value = "00100", Lr = "Any" };
                collection.Effects.Add(g);
                PopulateEffectGroupList();
                lstEffectGroups.SelectedIndex = collection.Effects.IndexOf(g);
            };
            btnRemove.Click += (s, e) =>
            {
                if (selectedGroup == null) return;
                var r = MessageBox.Show($"Remove the effects for \"{selectedGroup.DisplayLabel}\"?",
                    "Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (r != DialogResult.Yes) return;
                collection.Effects.Remove(selectedGroup);
                selectedGroup = null;
                PopulateEffectGroupList();
            };
            btns.Controls.AddRange(new Control[] { btnAdd, btnRemove });
            leftPanel.Controls.Add(btns, 0, 1);
            mainLayout.Controls.Add(leftPanel, 0, 0);

            // right: group fields + effect list + effect editor
            var right = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 12,
                Padding = new Padding(16, 8, 8, 8),
            };
            right.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90F));
            right.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            int row = 0;
            right.Controls.Add(new Label { Text = "Binary:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, row);
            txtGroupValue = new TextBox { Dock = DockStyle.Left, Width = 120, MaxLength = 10, Font = new Font("Consolas", 10) };
            txtGroupValue.TextChanged += (s, e) => cmbGroupLr.Enabled = txtGroupValue.Text.Trim().Length == 5;
            right.Controls.Add(txtGroupValue, 1, row++);

            right.Controls.Add(new Label { Text = "L / R:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, row);
            cmbGroupLr = new ComboBox { Dock = DockStyle.Left, Width = 80, DropDownStyle = ComboBoxStyle.DropDownList, Enabled = false };
            cmbGroupLr.Items.AddRange(new object[] { "Any", "L", "R" });
            right.Controls.Add(cmbGroupLr, 1, row++);

            var btnGroupSave = new Button { Text = "Apply", Width = 80, Margin = new Padding(0, 4, 0, 8) };
            btnGroupSave.Click += (s, e) => ApplyEffectGroupFields(true);
            right.Controls.Add(new Label(), 0, row);
            right.Controls.Add(btnGroupSave, 1, row++);

            right.Controls.Add(new Label { Text = "Effects:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, row);
            var fxListPanel = new TableLayoutPanel { Dock = DockStyle.Fill, Height = 120, ColumnCount = 2, RowCount = 1 };
            fxListPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            fxListPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80F));
            lstGroupEffects = new ListBox { Dock = DockStyle.Fill };
            lstGroupEffects.SelectedIndexChanged += LstGroupEffects_SelectedIndexChanged;
            fxListPanel.Controls.Add(lstGroupEffects, 0, 0);
            var fxBtns = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown };
            var btnFxAdd = new Button { Text = "Add", Width = 70 };
            var btnFxRemove = new Button { Text = "Remove", Width = 70 };
            btnFxAdd.Click += (s, e) =>
            {
                if (selectedGroup == null) return;
                var fx = new CardEffect { Type = "Item", Amount = 1, Version = -1 };
                selectedGroup.Effects.Add(fx);
                PopulateGroupEffectsList();
                lstGroupEffects.SelectedIndex = selectedGroup.Effects.IndexOf(fx);
            };
            btnFxRemove.Click += (s, e) =>
            {
                if (selectedGroup == null || selectedEffect == null) return;
                selectedGroup.Effects.Remove(selectedEffect);
                selectedEffect = null;
                PopulateGroupEffectsList();
            };
            fxBtns.Controls.AddRange(new Control[] { btnFxAdd, btnFxRemove });
            fxListPanel.Controls.Add(fxBtns, 1, 0);
            right.Controls.Add(fxListPanel, 1, row++);

            // effect editor
            right.Controls.Add(new Label { Text = "Type:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, row);
            cmbFxType = new ComboBox { Dock = DockStyle.Left, Width = 130, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbFxType.Items.AddRange(CardEffect.Types);
            cmbFxType.SelectedIndexChanged += (s, e) => UpdateEffectFieldVisibility();
            right.Controls.Add(cmbFxType, 1, row++);

            lblFxItem = new Label { Text = "Item:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill };
            right.Controls.Add(lblFxItem, 0, row);
            cmbFxItem = new ComboBox { Dock = DockStyle.Left, Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
            right.Controls.Add(cmbFxItem, 1, row++);

            lblFxDna = new Label { Text = "DNA:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill };
            right.Controls.Add(lblFxDna, 0, row);
            cmbFxDna = new ComboBox { Dock = DockStyle.Left, Width = 160, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbFxDna.Items.AddRange(CardEffect.DnaOptions);
            right.Controls.Add(cmbFxDna, 1, row++);

            lblFxAmount = new Label { Text = "Amount:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill };
            right.Controls.Add(lblFxAmount, 0, row);
            numFxAmount = new NumericUpDown { Dock = DockStyle.Left, Width = 80, Minimum = 1, Maximum = 9999, Value = 1 };
            right.Controls.Add(numFxAmount, 1, row++);

            lblFxArea = new Label { Text = "Area:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill };
            right.Controls.Add(lblFxArea, 0, row);
            numFxArea = new NumericUpDown { Dock = DockStyle.Left, Width = 80, Minimum = 1, Maximum = 999, Value = 1 };
            right.Controls.Add(numFxArea, 1, row++);

            lblFxRound = new Label { Text = "Round:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill };
            right.Controls.Add(lblFxRound, 0, row);
            numFxRound = new NumericUpDown { Dock = DockStyle.Left, Width = 80, Minimum = 1, Maximum = 999, Value = 1 };
            right.Controls.Add(numFxRound, 1, row++);

            lblFxUnlock = new Label { Text = "Unlock:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill };
            right.Controls.Add(lblFxUnlock, 0, row);
            cmbFxUnlock = new ComboBox { Dock = DockStyle.Left, Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
            right.Controls.Add(cmbFxUnlock, 1, row++);

            right.Controls.Add(new Label { Text = "Version:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, row);
            numFxVersion = new NumericUpDown { Dock = DockStyle.Left, Width = 80, Minimum = -1, Maximum = 99, Value = -1 };
            right.Controls.Add(numFxVersion, 1, row++);

            var btnFxApply = new Button { Text = "Apply Effect", Width = 100, Margin = new Padding(0, 4, 0, 0) };
            btnFxApply.Click += (s, e) => ApplyEffectFields();
            right.Controls.Add(new Label(), 0, row);
            right.Controls.Add(btnFxApply, 1, row++);

            mainLayout.Controls.Add(right, 1, 0);
            UpdateEffectFieldVisibility();
            return mainLayout;
        }

        private void PopulateEffectGroupList()
        {
            lstEffectGroups.BeginUpdate();
            lstEffectGroups.Items.Clear();
            foreach (var g in collection.Effects)
                lstEffectGroups.Items.Add(g.DisplayLabel);
            lstEffectGroups.EndUpdate();
            if (collection.Effects.Count == 0)
            {
                selectedGroup = null;
                txtGroupValue.Text = "";
                lstGroupEffects.Items.Clear();
            }
        }

        private void LstEffectGroups_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lstEffectGroups.SelectedIndex < 0 || lstEffectGroups.SelectedIndex >= collection.Effects.Count)
            {
                selectedGroup = null;
                return;
            }
            selectedGroup = collection.Effects[lstEffectGroups.SelectedIndex];
            txtGroupValue.Text = selectedGroup.Value ?? "";
            cmbGroupLr.SelectedItem = string.IsNullOrEmpty(selectedGroup.Lr) ? "Any" : selectedGroup.Lr;
            cmbGroupLr.Enabled = (selectedGroup.Value ?? "").Length == 5;
            PopulateGroupEffectsList();
            RefreshEffectCombos();
        }

        private void PopulateGroupEffectsList()
        {
            lstGroupEffects.BeginUpdate();
            lstGroupEffects.Items.Clear();
            if (selectedGroup != null)
                foreach (var fx in selectedGroup.Effects)
                    lstGroupEffects.Items.Add(fx.DisplayLabel);
            lstGroupEffects.EndUpdate();
            selectedEffect = null;
        }

        private void RefreshEffectCombos()
        {
            cmbFxItem.Items.Clear();
            foreach (var it in moduleItems)
                if (!string.IsNullOrWhiteSpace(it.Name)) cmbFxItem.Items.Add(it.Name);
            cmbFxUnlock.Items.Clear();
            if (module?.Unlocks != null)
                foreach (var u in module.Unlocks)
                    if (!string.IsNullOrWhiteSpace(u.Name)) cmbFxUnlock.Items.Add(u.Name);
        }

        private void LstGroupEffects_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (selectedGroup == null || lstGroupEffects.SelectedIndex < 0 ||
                lstGroupEffects.SelectedIndex >= selectedGroup.Effects.Count)
            {
                selectedEffect = null;
                return;
            }
            selectedEffect = selectedGroup.Effects[lstGroupEffects.SelectedIndex];
            cmbFxType.SelectedItem = selectedEffect.Type ?? "Item";
            cmbFxItem.SelectedItem = selectedEffect.Item;
            cmbFxDna.SelectedItem = selectedEffect.Dna;
            numFxAmount.Value = Math.Max(numFxAmount.Minimum, Math.Min(numFxAmount.Maximum, selectedEffect.Amount ?? 1));
            numFxArea.Value = Math.Max(numFxArea.Minimum, Math.Min(numFxArea.Maximum, selectedEffect.Area ?? 1));
            numFxRound.Value = Math.Max(numFxRound.Minimum, Math.Min(numFxRound.Maximum, selectedEffect.Round ?? 1));
            cmbFxUnlock.SelectedItem = selectedEffect.Unlock;
            numFxVersion.Value = Math.Max(-1, Math.Min(99, selectedEffect.Version));
            UpdateEffectFieldVisibility();
        }

        private void UpdateEffectFieldVisibility()
        {
            string type = cmbFxType.SelectedItem?.ToString() ?? "Item";
            bool isItem = type == "Item", isDna = type == "DNA",
                 isEnc = type == "Encounter", isUnlock = type == "Unlock";
            lblFxItem.Visible = cmbFxItem.Visible = isItem;
            lblFxDna.Visible = cmbFxDna.Visible = isDna;
            lblFxAmount.Visible = numFxAmount.Visible = isItem || isDna;
            lblFxArea.Visible = numFxArea.Visible = isEnc;
            lblFxRound.Visible = numFxRound.Visible = isEnc;
            lblFxUnlock.Visible = cmbFxUnlock.Visible = isUnlock;
        }

        private void ApplyEffectFields()
        {
            if (selectedGroup == null || selectedEffect == null) return;
            string type = cmbFxType.SelectedItem?.ToString() ?? "Item";
            selectedEffect.Type = type;
            selectedEffect.Item = type == "Item" ? cmbFxItem.SelectedItem?.ToString() : null;
            selectedEffect.Dna = type == "DNA" ? cmbFxDna.SelectedItem?.ToString() : null;
            selectedEffect.Amount = (type == "Item" || type == "DNA") ? (int?)numFxAmount.Value : null;
            selectedEffect.Area = type == "Encounter" ? (int?)numFxArea.Value : null;
            selectedEffect.Round = type == "Encounter" ? (int?)numFxRound.Value : null;
            selectedEffect.Unlock = type == "Unlock" ? cmbFxUnlock.SelectedItem?.ToString() : null;
            selectedEffect.Version = (int)numFxVersion.Value;
            int keep = lstGroupEffects.SelectedIndex;
            PopulateGroupEffectsList();
            if (keep >= 0 && keep < lstGroupEffects.Items.Count) lstGroupEffects.SelectedIndex = keep;
        }

        private void ApplyEffectGroupFields(bool interactive = false)
        {
            if (selectedGroup == null) return;
            string value = txtGroupValue.Text.Trim();
            if (!IsValidBinary(value))
            {
                if (interactive)
                    MessageBox.Show("Binary must be 1 to 10 digits of 0/1.", "Invalid Binary",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            selectedGroup.Value = value;
            selectedGroup.Lr = value.Length == 5 ? (cmbGroupLr.SelectedItem?.ToString() ?? "Any") : "Any";
            int keep = collection.Effects.IndexOf(selectedGroup);
            PopulateEffectGroupList();
            if (keep >= 0) lstEffectGroups.SelectedIndex = keep;
        }

        #endregion

        // ====================================================================
        // Card Packs tab
        // ====================================================================
        #region Packs tab

        private ListBox lstPacks;
        private TextBox txtPackName;
        private PictureBox pbPackSprite;
        private NumericUpDown numPackCards, numPackShine;
        private DataGridView gridPack;
        private CardPack selectedPack;
        private BindingList<PackEntry> packBinding;

        private class CardChoice
        {
            public string Id { get; set; }
            public string Label { get; set; }
        }

        private Control BuildPacksPage()
        {
            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(8),
            };
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            var leftPanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
            leftPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            leftPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));

            lstPacks = new ListBox { Dock = DockStyle.Fill, Font = new Font(FontFamily.GenericSansSerif, 10) };
            lstPacks.SelectedIndexChanged += LstPacks_SelectedIndexChanged;
            leftPanel.Controls.Add(lstPacks, 0, 0);

            var btns = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(2) };
            var btnAdd = new Button { Text = "Add", Width = 70 };
            var btnRemove = new Button { Text = "Remove", Width = 70 };
            btnAdd.Click += (s, e) =>
            {
                var pack = new CardPack { Id = Guid.NewGuid().ToString(), Name = "New Pack", CardsPerPack = 5 };
                collection.Packs.Add(pack);
                PopulatePackList();
                lstPacks.SelectedIndex = collection.Packs.IndexOf(pack);
            };
            btnRemove.Click += (s, e) =>
            {
                if (selectedPack == null) return;
                var r = MessageBox.Show($"Remove the pack \"{selectedPack.Name}\"?",
                    "Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (r != DialogResult.Yes) return;
                TryDeleteRelative(selectedPack.Sprite);
                collection.Packs.Remove(selectedPack);
                selectedPack = null;
                PopulatePackList();
            };
            btns.Controls.AddRange(new Control[] { btnAdd, btnRemove });
            leftPanel.Controls.Add(btns, 0, 1);
            mainLayout.Controls.Add(leftPanel, 0, 0);

            var right = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 7,
                Padding = new Padding(16, 8, 8, 8),
            };
            right.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110F));
            right.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            int row = 0;
            right.Controls.Add(new Label { Text = "Name:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, row);
            txtPackName = new TextBox { Dock = DockStyle.Left, Width = 240 };
            right.Controls.Add(txtPackName, 1, row++);

            right.Controls.Add(new Label { Text = "Sprite:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, row);
            pbPackSprite = MakeCardSpriteBox("Pack sprite\n(click to set)");
            pbPackSprite.Width = 100;
            pbPackSprite.Height = 100;
            pbPackSprite.Click += (s, e) => UploadPackSprite();
            var packSpritePanel = new Panel { Height = 108, Dock = DockStyle.Top };
            packSpritePanel.Controls.Add(pbPackSprite);
            right.Controls.Add(packSpritePanel, 1, row++);

            right.Controls.Add(new Label { Text = "Cards per pack:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, row);
            numPackCards = new NumericUpDown { Dock = DockStyle.Left, Width = 80, Minimum = 1, Maximum = 99, Value = 5 };
            right.Controls.Add(numPackCards, 1, row++);

            right.Controls.Add(new Label { Text = "Shine chance %:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, row);
            numPackShine = new NumericUpDown
            {
                Dock = DockStyle.Left,
                Width = 90,
                Minimum = 0,
                Maximum = 100,
                DecimalPlaces = 2,
                Increment = 0.05m,
            };
            right.Controls.Add(numPackShine, 1, row++);

            right.Controls.Add(new Label { Text = "Cards / odds:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, row);
            gridPack = new DataGridView
            {
                Dock = DockStyle.Fill,
                Height = 240,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = true,
                RowHeadersVisible = false,
            };
            gridPack.DataError += (s, e) => { e.ThrowException = false; };
            var colCard = new DataGridViewComboBoxColumn
            {
                HeaderText = "Card",
                DataPropertyName = "Id",
                DisplayMember = "Label",
                ValueMember = "Id",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                FlatStyle = FlatStyle.Flat,
            };
            var colOdds = new DataGridViewTextBoxColumn
            {
                HeaderText = "Odds",
                DataPropertyName = "Odds",
                Width = 70,
            };
            gridPack.Columns.Add(colCard);
            gridPack.Columns.Add(colOdds);
            right.Controls.Add(gridPack, 1, row++);
            right.RowStyles.Clear();
            for (int i = 0; i < 5; i++) right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var packBtns = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
            var btnEntryAdd = new Button { Text = "Add Card", Width = 90, Margin = new Padding(4) };
            var btnBulk = new Button { Text = "Add All of Type/Series…", Width = 160, Margin = new Padding(4) };
            var btnPackSave = new Button { Text = "Save", Width = 80, Margin = new Padding(4) };
            btnEntryAdd.Click += (s, e) =>
            {
                if (selectedPack == null || packBinding == null) return;
                var first = collection.Cards.FirstOrDefault();
                if (first == null) return;
                packBinding.Add(new PackEntry { Id = first.Id, Odds = CollectionCard.RarityWeight(first.Rarity) });
            };
            btnBulk.Click += (s, e) => BulkAddToPack();
            btnPackSave.Click += (s, e) => ApplyPackFields(true);
            packBtns.Controls.AddRange(new Control[] { btnEntryAdd, btnBulk, btnPackSave });
            right.Controls.Add(new Label(), 0, row);
            right.Controls.Add(packBtns, 1, row++);

            mainLayout.Controls.Add(right, 1, 0);
            return mainLayout;
        }

        private void PopulatePackList()
        {
            lstPacks.BeginUpdate();
            lstPacks.Items.Clear();
            foreach (var p in collection.Packs)
                lstPacks.Items.Add(p.Name ?? "");
            lstPacks.EndUpdate();
            if (collection.Packs.Count == 0)
            {
                selectedPack = null;
                txtPackName.Text = "";
                pbPackSprite.Image = null;
                gridPack.DataSource = null;
                packBinding = null;
            }
        }

        private void LstPacks_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lstPacks.SelectedIndex < 0 || lstPacks.SelectedIndex >= collection.Packs.Count)
            {
                selectedPack = null;
                return;
            }
            selectedPack = collection.Packs[lstPacks.SelectedIndex];
            txtPackName.Text = selectedPack.Name ?? "";
            numPackCards.Value = Math.Max(1, Math.Min(99, selectedPack.CardsPerPack));
            decimal shine = (decimal)selectedPack.ShineChance;
            numPackShine.Value = Math.Max(0, Math.Min(100, shine));
            pbPackSprite.Image = LoadSpriteImage(selectedPack.Sprite);
            pbPackSprite.Invalidate();

            // refresh the combo choices before binding rows
            var choices = collection.Ordered()
                .Select(c => new CardChoice { Id = c.Id, Label = $"{c.DisplayLabel} [{c.Type}]" })
                .ToList();
            ((DataGridViewComboBoxColumn)gridPack.Columns[0]).DataSource = choices;

            packBinding = new BindingList<PackEntry>(selectedPack.Cards);
            gridPack.DataSource = packBinding;
        }

        private void ApplyPackFields(bool interactive = false)
        {
            if (selectedPack == null) return;
            gridPack.EndEdit();
            selectedPack.Name = txtPackName.Text;
            selectedPack.CardsPerPack = (int)numPackCards.Value;
            selectedPack.ShineChance = (float)numPackShine.Value;
            int keep = collection.Packs.IndexOf(selectedPack);
            PopulatePackList();
            if (keep >= 0) lstPacks.SelectedIndex = keep;
        }

        private void UploadPackSprite()
        {
            if (selectedPack == null || string.IsNullOrEmpty(modulePath)) return;
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "Select pack sprite";
                dialog.Filter = "PNG files (*.png)|*.png";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    EnsureCardsFolder();
                    string relative = CollectionFile.SpritesFolder + "/pack_" + selectedPack.Id + ".png";
                    File.Copy(dialog.FileName,
                        Path.Combine(modulePath, relative.Replace('/', Path.DirectorySeparatorChar)), true);
                    selectedPack.Sprite = relative;
                    pbPackSprite.Image = LoadSpriteImage(relative);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed to copy sprite: " + ex.Message, "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void BulkAddToPack()
        {
            if (selectedPack == null || packBinding == null) return;

            using (var dlg = new Form
            {
                Text = "Add All of Type/Series",
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                Width = 320,
                Height = 170,
                MinimizeBox = false,
                MaximizeBox = false,
            })
            {
                var lblType = new Label { Text = "Type:", Location = new Point(12, 15), AutoSize = true };
                var cmbType = new ComboBox { Location = new Point(90, 12), Width = 190, DropDownStyle = ComboBoxStyle.DropDownList };
                cmbType.Items.Add("All");
                foreach (var t in CollectionCard.AllTypes) cmbType.Items.Add(t);
                cmbType.SelectedIndex = 0;
                var lblSeries = new Label { Text = "Series:", Location = new Point(12, 47), AutoSize = true };
                var txtSeries = new TextBox { Location = new Point(90, 44), Width = 100 };
                var lblHint = new Label { Text = "(empty = any series)", Location = new Point(195, 47), AutoSize = true, ForeColor = Color.Gray };
                var ok = new Button { Text = "Add", DialogResult = DialogResult.OK, Location = new Point(120, 90), Width = 75 };
                var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(205, 90), Width = 75 };
                dlg.Controls.AddRange(new Control[] { lblType, cmbType, lblSeries, txtSeries, lblHint, ok, cancel });
                dlg.AcceptButton = ok;
                dlg.CancelButton = cancel;
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                string type = cmbType.SelectedItem.ToString();
                string series = txtSeries.Text.Trim();
                var present = new HashSet<string>(packBinding.Select(p => p.Id));
                int added = 0;
                foreach (var c in collection.Ordered())
                {
                    if (type != "All" && c.Type != type) continue;
                    if (series.Length > 0 && !string.Equals(c.Series, series, StringComparison.OrdinalIgnoreCase)) continue;
                    if (present.Contains(c.Id)) continue;
                    packBinding.Add(new PackEntry { Id = c.Id, Odds = CollectionCard.RarityWeight(c.Rarity) });
                    added++;
                }
                MessageBox.Show($"Added {added} card(s) with rarity-based odds.", "Add All",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        #endregion

        // ====================================================================
        // Tools ▸ Import Collection from Module
        // ====================================================================
        #region Import from module

        /// <summary>Tools menu action: merge another module's collection into this one.</summary>
        public void ImportFromModuleFlow()
        {
            using (var folderDialog = new FolderBrowserDialog())
            {
                folderDialog.Description = "Select the module folder to import the collection from";
                if (folderDialog.ShowDialog(this) != DialogResult.OK) return;

                string sourcePath = folderDialog.SelectedPath;
                if (!File.Exists(Path.Combine(sourcePath, CollectionFile.FileName)))
                {
                    MessageBox.Show("The selected folder has no cards.json.", "Import Collection",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                var source = CollectionFile.Load(sourcePath);

                // what to import
                bool impCards = true, impEffects = true, impPacks = true;
                using (var dlg = new Form
                {
                    Text = "Import Collection",
                    FormBorderStyle = FormBorderStyle.FixedDialog,
                    StartPosition = FormStartPosition.CenterParent,
                    Width = 300,
                    Height = 190,
                    MinimizeBox = false,
                    MaximizeBox = false,
                })
                {
                    var chkCards = new CheckBox { Text = $"Cards ({source.Cards.Count})", Checked = true, Location = new Point(20, 15), AutoSize = true };
                    var chkEffects = new CheckBox { Text = $"Effects ({source.Effects.Count})", Checked = true, Location = new Point(20, 42), AutoSize = true };
                    var chkPacks = new CheckBox { Text = $"Card Packs ({source.Packs.Count})", Checked = true, Location = new Point(20, 69), AutoSize = true };
                    var ok = new Button { Text = "Import", DialogResult = DialogResult.OK, Location = new Point(100, 110), Width = 80 };
                    var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(190, 110), Width = 80 };
                    dlg.Controls.AddRange(new Control[] { chkCards, chkEffects, chkPacks, ok, cancel });
                    dlg.AcceptButton = ok;
                    dlg.CancelButton = cancel;
                    if (dlg.ShowDialog(this) != DialogResult.OK) return;
                    impCards = chkCards.Checked;
                    impEffects = chkEffects.Checked;
                    impPacks = chkPacks.Checked;
                }

                int conflicts = 0;
                if (impCards) conflicts += source.Cards.Count(c => collection.Cards.Any(x => x.Id == c.Id));
                if (impEffects) conflicts += source.Effects.Count(g => collection.Effects.Any(x => x.Value == g.Value && (x.Lr ?? "Any") == (g.Lr ?? "Any")));
                if (impPacks) conflicts += source.Packs.Count(p => collection.Packs.Any(x => x.Id == p.Id));
                bool replace = false;
                if (conflicts > 0)
                {
                    var r = MessageBox.Show(
                        $"{conflicts} entrie(s) already exist in this module.\nReplace the existing ones?",
                        "Import Collection", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    replace = r == DialogResult.Yes;
                }

                int imported = 0;
                if (impCards)
                {
                    foreach (var card in source.Cards)
                    {
                        var existing = collection.Cards.FirstOrDefault(x => x.Id == card.Id);
                        if (existing != null)
                        {
                            if (!replace) continue;
                            collection.Cards.Remove(existing);
                        }
                        CopyCardSpritesFrom(sourcePath, card);
                        collection.Cards.Add(card);
                        thumbCache.Remove(card.Id);
                        imported++;
                    }
                }
                if (impEffects)
                {
                    foreach (var g in source.Effects)
                    {
                        var existing = collection.Effects.FirstOrDefault(x =>
                            x.Value == g.Value && (x.Lr ?? "Any") == (g.Lr ?? "Any"));
                        if (existing != null)
                        {
                            if (!replace) continue;
                            collection.Effects.Remove(existing);
                        }
                        collection.Effects.Add(g);
                        imported++;
                    }
                }
                if (impPacks)
                {
                    foreach (var p in source.Packs)
                    {
                        var existing = collection.Packs.FirstOrDefault(x => x.Id == p.Id);
                        if (existing != null)
                        {
                            if (!replace) continue;
                            collection.Packs.Remove(existing);
                        }
                        CopyRelativeFrom(sourcePath, p.Sprite);
                        collection.Packs.Add(p);
                        imported++;
                    }
                }

                PopulateCardList();
                PopulateEffectGroupList();
                PopulatePackList();
                MessageBox.Show($"Imported {imported} entrie(s).", "Import Collection",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void CopyCardSpritesFrom(string sourcePath, CollectionCard card)
        {
            CopyRelativeFrom(sourcePath, card.Sprites?.Front);
            CopyRelativeFrom(sourcePath, card.Sprites?.Back);
        }

        private void CopyRelativeFrom(string sourcePath, string relative)
        {
            if (string.IsNullOrEmpty(relative) || string.IsNullOrEmpty(modulePath)) return;
            try
            {
                string src = Path.Combine(sourcePath, relative.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(src)) return;
                string dest = Path.Combine(modulePath, relative.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                File.Copy(src, dest, true);
            }
            catch { /* best effort */ }
        }

        #endregion
    }
}
