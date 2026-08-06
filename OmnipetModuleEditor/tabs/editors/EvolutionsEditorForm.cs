using OmnipetModuleEditor.Controls;
using OmnipetModuleEditor.Models;
using OmnipetModuleEditor.Properties;
using OmnipetModuleEditor.Utils;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;

namespace OmnipetModuleEditor
{
    /// <summary>
    /// Editor window for managing pet evolutions visually.
    /// /// </summary>
    public partial class EvolutionsEditorForm : Form
    {
        // Fields
        private List<Pet> pets;
        private string modulePath;
        private Module module;
        private ComboBox cmbVersions;
        private Button btnOrganize;
        private Button btnSave;
        private Button btnCancel;

        private Panel panelTop;
        private Panel panelLeft;
        private Panel panelRight;
        private Panel panelChart;

        private Panel draggedPanel;
        private Point dragOffset;

        private Panel draggingPanel = null;
        private Point dragStartPoint;

        private bool connectMode = false;
        private Button btnToggleConnect;
        private Panel selectedForConnection = null;

        private bool tempConnectMode = false;
        private Button btnCreateTempEvo;

        private bool deleteMode = false;
        private Button btnDelete;

        private bool orderMode = false;
        private Button btnEvolutionOrder;

        private List<Connection> connections = new List<Connection>();
        private List<Item> items;
        private static readonly Random rand = new Random();
        private PetListPanel petListPanel;

        /// <summary>
        /// Initializes a new instance of the <see cref="EvolutionsEditorForm"/> class.
        /// </summary>
        public EvolutionsEditorForm(List<Pet> pets, string modulePath, Module module)
        {
            this.pets = pets;
            this.modulePath = modulePath;
            this.module = module;
            InitializeLayout();
            LoadDigimonsForSelectedVersion();
            LoadItemsFromJson();
        }

        #region Layout and UI Initialization

        /// <summary>
        /// Initializes the layout and UI controls.
        /// </summary>
        private void InitializeLayout()
        {
            this.Text = "Evolution EdiTor";
            this.Size = new Size(1200, 800);

            // Top bar with controls
            var flowTop = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 50,
                Padding = new Padding(8),
                BackColor = SystemColors.Control,
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = false,
                WrapContents = false
            };

            flowTop.Controls.Add(new Label
            {
                Text = Resources.Label_Version,
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 12, 4, 0)
            });

            cmbVersions = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 120,
                Margin = new Padding(0, 8, 8, 0)
            };
            var versions = pets.Select(p => p.Version).Distinct().OrderBy(v => v).ToArray();
            foreach (var v in versions)
                cmbVersions.Items.Add(string.Format(Resources.Label_VersionWithNumber, v));
            if (cmbVersions.Items.Count > 0)
                cmbVersions.SelectedIndex = 0;
            flowTop.Controls.Add(cmbVersions);

            btnOrganize = new Button { Text = Resources.Button_Organize, Width = 90, Margin = new Padding(8, 8, 0, 0) };
            btnSave = new Button { Text = Resources.Button_Save, Width = 90, Margin = new Padding(8, 8, 0, 0) };
            btnCancel = new Button { Text = Resources.Button_Cancel, Width = 90, Margin = new Padding(8, 8, 0, 0) };

            flowTop.Controls.Add(btnOrganize);
            flowTop.Controls.Add(btnSave);
            flowTop.Controls.Add(btnCancel);

            btnToggleConnect = new Button
            {
                Text = "Create Evolution",
                Width = 120,
                Margin = new Padding(8, 8, 0, 0),
                BackColor = Color.LightGray
            };
            btnToggleConnect.Click += BtnToggleConnect_Click;
            flowTop.Controls.Add(btnToggleConnect);

            btnCreateTempEvo = new Button
            {
                Text = "Create Temp Evolution",
                Width = 150,
                Margin = new Padding(8, 8, 0, 0),
                BackColor = Color.LightGray
            };
            btnCreateTempEvo.Click += BtnCreateTempEvo_Click;
            flowTop.Controls.Add(btnCreateTempEvo);

            btnEvolutionOrder = new Button
            {
                Text = "Evolution Order",
                Width = 120,
                Margin = new Padding(8, 8, 0, 0),
                BackColor = Color.LightGray
            };
            btnEvolutionOrder.Click += BtnEvolutionOrder_Click;
            flowTop.Controls.Add(btnEvolutionOrder);

            btnDelete = new Button
            {
                Text = Resources.Button_Delete,
                Width = 90,
                Margin = new Padding(8, 8, 0, 0),
                BackColor = Color.LightGray
            };
            btnDelete.Click += BtnDelete_Click;
            flowTop.Controls.Add(btnDelete);

            panelTop = new Panel
            {
                Dock = DockStyle.Top,
                Height = 50,
                Padding = new Padding(0),
                BackColor = SystemColors.Control
            };
            panelTop.Controls.Add(flowTop);

            // Pet list panel (left)
            petListPanel = new PetListPanel
            {
                Dock = DockStyle.Left,
                Width = 230,
                Padding = new Padding(4),
                BackColor = Color.WhiteSmoke
            };

            LoadPetListPanel();

            // Left panel (hidden, kept for compatibility)
            panelLeft = new Panel
            {
                Dock = DockStyle.Left,
                Width = 0,
                Padding = new Padding(0),
                BackColor = Color.WhiteSmoke
            };

            // Right panel (canvas)
            panelRight = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(4),
                BackColor = Color.White
            };

            panelChart = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = ColorTranslator.FromHtml("#757575"),
                AutoScroll = true
            };
            panelRight.Controls.Add(panelChart);

            // Add controls to form
            this.Controls.Add(panelRight);
            this.Controls.Add(petListPanel);
            this.Controls.Add(panelTop);

            // Events
            cmbVersions.SelectedIndexChanged += (s, e) =>
            {
                LoadPetListPanel();
                LoadDigimonsForSelectedVersion();
                BuildEvolutionTree();
            };
            btnCancel.Click += (s, e) => this.DialogResult = DialogResult.Cancel;

            panelChart.AllowDrop = true;
            panelChart.DragEnter += PanelChart_DragEnter;
            panelChart.DragDrop += PanelChart_DragDrop;
            panelChart.Paint += PanelChart_Paint;
            panelChart.MouseClick += PanelChart_MouseClick;

            btnOrganize.Click += BtnOrganize_Click;

            btnSave.Click += (s, e) =>
            {
                SaveEvolutionTree();
                this.DialogResult = DialogResult.OK;
            };

            AddStage0PetToCanvas();
            BuildEvolutionTree();
        }

        #endregion

        #region Pet List and Canvas Population

        /// <summary>
        /// Populates the pet list panel with pets from the selected version.
        /// </summary>
        private void LoadPetListPanel()
        {
            petListPanel.PanelPetList.Controls.Clear();
            if (cmbVersions.SelectedIndex < 0) return;

            var selectedVersion = int.Parse(cmbVersions.SelectedItem.ToString().Replace(Resources.Label_Version + " ", ""));
            var digimons = pets.Where(p => p.Version == selectedVersion).ToList();
            
            // Sort using PetUtils sorting (which respects Index when available)
            PetUtils.SortPets(digimons);

            int y = 8;
            foreach (var pet in digimons)
            {
                var petPanel = CreatePetPanel(pet, false);
                petPanel.Location = new Point(8, y);
                petPanel.Cursor = Cursors.Hand;
                petPanel.MouseDown += PetPanel_MouseDown;
                petListPanel.PanelPetList.Controls.Add(petPanel);
                y += petPanel.Height + 8;
            }
        }

        /// <summary>
        /// Loads the pet list for the selected version (legacy, kept for compatibility).
        /// </summary>
        private void LoadDigimonsForSelectedVersion()
        {
            petListPanel.PanelPetList.Controls.Clear();
            if (cmbVersions.SelectedIndex < 0) return;

            var selectedVersion = int.Parse(cmbVersions.SelectedItem.ToString().Replace(Resources.Label_Version + " ", ""));
            var digimons = pets.Where(p => p.Version == selectedVersion).ToList();
            
            // Sort using PetUtils sorting (which respects Index when available)
            PetUtils.SortPets(digimons);

            int y = 8;
            foreach (var pet in digimons)
            {
                var petPanel = CreatePetPanel(pet);
                petPanel.Location = new Point(8, y);
                petPanel.Cursor = Cursors.Hand;
                petPanel.MouseDown += PetPanel_MouseDown;
                petListPanel.PanelPetList.Controls.Add(petPanel);
                y += petPanel.Height + 8;
            }
        }

        /// <summary>
        /// Adds the root pet and its evolutions to the canvas.
        /// </summary>
        private void AddStage0PetToCanvas()
        {
            BuildEvolutionTree();
        }

        /// <summary>
        /// Builds the evolution tree on the canvas for the selected version.
        /// </summary>
        private void BuildEvolutionTree()
        {
            panelChart.Controls.Clear();
            foreach (var conn in connections)
                if (conn.CriteriaPanel != null)
                    panelChart.Controls.Remove(conn.CriteriaPanel);
            connections.Clear();

            if (cmbVersions.SelectedIndex < 0) return;
            int selectedVersion = int.Parse(cmbVersions.SelectedItem.ToString().Replace(Resources.Label_Version + " ", ""));
            var rootPets = pets.Where(p => p.Version == selectedVersion && p.Stage == 0).ToList();
            if (rootPets.Count == 0) return;

            var petToPanel = new Dictionary<Pet, Panel>();
            foreach (var root in rootPets)
                AddPetAndEvolutionsRecursive(root, petToPanel);

            OrganizeCanvas();
        }

        /// <summary>
        /// Recursively adds a pet and its evolutions to the canvas.  Panels go
        /// on at the margin and OrganizeCanvas places them afterwards, so the
        /// tree never opens spread over a different arrangement than the one
        /// the Organize button gives.
        /// </summary>
        private void AddPetAndEvolutionsRecursive(Pet pet, Dictionary<Pet, Panel> petToPanel)
        {
            if (petToPanel.ContainsKey(pet))
                return;

            var petPanel = CreatePetPanel(pet, true);
            petPanel.Location = new Point(CanvasMargin, CanvasMargin);
            panelChart.Controls.Add(petPanel);
            petPanel.BringToFront();
            petToPanel[pet] = petPanel;

            int normalCount = pet.Evolve?.Count ?? 0;
            int tempCount = pet.TempEvolve?.Count ?? 0;
            int count = normalCount + tempCount;
            if (count == 0)
                return;

            for (int i = 0; i < count; i++)
            {
                bool isTemp = i >= normalCount;
                string targetName = isTemp ? pet.TempEvolve[i - normalCount].To : pet.Evolve[i].To;
                var childPet = pets.FirstOrDefault(p => p.Name == targetName && p.Version == pet.Version);
                if (childPet == null) continue;

                AddPetAndEvolutionsRecursive(childPet, petToPanel);

                var childPanel = petToPanel[childPet];
                var conn = new Connection(petPanel, childPanel, GetRandomLineColor());
                if (isTemp)
                {
                    conn.IsTemp = true;
                    conn.CriteriaPanel = CreateTempCriteriaPanel(pet.TempEvolve[i - normalCount]);
                }
                else
                {
                    conn.CriteriaPanel = CreateCriteriaPanel(pet.Evolve[i]);
                }
                panelChart.Controls.Add(conn.CriteriaPanel);
                conn.CriteriaPanel.BringToFront();
                connections.Add(conn);
            }
        }

        #endregion

        #region Pet Panel Creation and Attribute Helpers

        /// <summary>
        /// Creates a panel representing a pet, for either the list or the canvas.
        /// </summary>
        private Panel CreatePetPanel(Pet pet, bool forCanvas = false)
        {
            var itemPanel = new Panel
            {
                Size = new Size(190, 56),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                Tag = pet
            };

            PictureBox pb = new PictureBox
            {
                Location = new Point(4, 4),
                Size = new Size(48, 48),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = GetAttributeColor(pet.Attribute ?? ""),
                BorderStyle = BorderStyle.FixedSingle
            };

            // Use new sprite loading system with format support
            string primary = module?.PrimarySpriteFormat ?? "Color";
            string secondary = module?.SecondarySpriteFormat ?? "HD";
            var sprite = SpriteUtils.LoadSingleSprite(pet.Name, modulePath, PetUtils.FixedNameFormat, primary, secondary);
            pb.Image = sprite;

            itemPanel.Controls.Add(pb);

            Label lblName = new Label
            {
                Text = pet.Name,
                Location = new Point(60, 4),
                AutoSize = true,
                Font = new Font(FontFamily.GenericSansSerif, 11, FontStyle.Bold),
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
                Font = new Font(FontFamily.GenericSansSerif, 8, FontStyle.Regular),
                ForeColor = Color.DeepSkyBlue
            };
            itemPanel.Controls.Add(lblInfo);

            if (forCanvas)
            {
                itemPanel.MouseDown += PetPanel_MouseDown;
                itemPanel.MouseMove += PetPanel_MouseMove;
                itemPanel.MouseUp += PetPanel_MouseUp;
                itemPanel.Click += PetPanel_ClickForConnection;
                itemPanel.Click += PetPanel_ClickForDelete;
                itemPanel.Click += PetPanel_ClickForOrder;
            }

            return itemPanel;
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

        /// <summary>
        /// Gets a random color for evolution lines.
        /// </summary>
        private Color GetRandomLineColor()
        {
            return Color.FromArgb(255, rand.Next(32, 224), rand.Next(32, 224), rand.Next(32, 224));
        }

        #endregion

        #region Drag and Drop, Mouse, and Canvas Events

        /// <summary>
        /// Handles mouse down for pet panels (drag from list or move in canvas).
        /// </summary>
        private void PetPanel_MouseDown(object sender, MouseEventArgs e)
        {
            if (sender is Panel panel && e.Button == MouseButtons.Left)
            {
                if (panel.Parent == petListPanel.PanelPetList)
                {
                    if (panel.Tag is Pet pet)
                    {
                        string data = $"{pet.Name}|{pet.Version}";
                        panel.DoDragDrop(data, DragDropEffects.Copy);
                    }
                }
                else if (panel.Parent == panelChart)
                {
                    draggingPanel = panel;
                    dragStartPoint = e.Location;
                    panel.BringToFront();
                }
            }
        }

        private void PetPanel_MouseMove(object sender, MouseEventArgs e)
        {
            if (draggingPanel != null && e.Button == MouseButtons.Left)
            {
                var newLocation = draggingPanel.Location;
                newLocation.Offset(e.X - dragStartPoint.X, e.Y - dragStartPoint.Y);
                // only the top-left corner is clamped: the canvas follows a
                // panel dragged right or down and shrinks back once the drag
                // ends, so the bars stay tied to what is actually on the sheet
                newLocation.X = Math.Max(0, newLocation.X);
                newLocation.Y = Math.Max(0, newLocation.Y);
                draggingPanel.Location = newLocation;
                panelChart.Invalidate();
                UpdateCanvasScroll(false);
                UpdateCriteriaPanels();
            }
        }

        private void PetPanel_MouseUp(object sender, MouseEventArgs e)
        {
            if (draggingPanel == null) return;
            draggingPanel = null;
            UpdateCanvasScroll();
        }

        private void DraggedPanel_MouseMove(object sender, MouseEventArgs e)
        {
            if (draggedPanel != null && e.Button == MouseButtons.Left)
            {
                var mousePos = panelChart.PointToClient(Cursor.Position);
                draggedPanel.Location = new Point(mousePos.X - dragOffset.X, mousePos.Y - dragOffset.Y);
            }
        }

        private void DraggedPanel_MouseUp(object sender, MouseEventArgs e)
        {
            if (draggedPanel != null)
            {
                draggedPanel.MouseMove -= DraggedPanel_MouseMove;
                draggedPanel.MouseUp -= DraggedPanel_MouseUp;
                draggedPanel = null;
            }
        }

        private void PanelChart_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.StringFormat))
                e.Effect = DragDropEffects.Copy;
            else
                e.Effect = DragDropEffects.None;
        }

        private void PanelChart_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.StringFormat))
            {
                string data = (string)e.Data.GetData(DataFormats.StringFormat);
                var parts = data.Split('|');
                if (parts.Length == 2)
                {
                    string name = parts[0];
                    int version;
                    if (int.TryParse(parts[1], out version))
                    {
                        var pet = pets.FirstOrDefault(p => p.Name == name && p.Version == version);
                        if (pet != null)
                        {
                            var clientPoint = panelChart.PointToClient(new Point(e.X, e.Y));
                            var petPanel = CreatePetPanel(pet, true);
                            petPanel.Location = clientPoint;
                            panelChart.Controls.Add(petPanel);
                            petPanel.BringToFront();
                            // Restore any stored evolutions between this pet and
                            // pets already on the canvas — otherwise saving would
                            // erase them (the save rebuilds from visible
                            // connections only).
                            AutoConnectPet(petPanel);
                            UpdateCanvasScroll();
                            panelChart.Invalidate();
                            UpdateCriteriaPanels();
                        }
                    }
                }
            }
        }

        private void PanelChart_Paint(object sender, PaintEventArgs e)
        {
            foreach (var conn in connections)
            {
                if (conn.From.Parent != panelChart || conn.To.Parent != panelChart)
                    continue;

                var p1 = conn.From;
                var p2 = conn.To;
                Point c1 = new Point(p1.Left + p1.Width / 2, p1.Top + p1.Height / 2);
                Point c2 = new Point(p2.Left + p2.Width / 2, p2.Top + p2.Height / 2);

                using (var pen = new Pen(conn.LineColor, 3))
                {
                    pen.CustomEndCap = new AdjustableArrowCap(8, 10, true);
                    // Temporary evolutions are drawn with spaced dashes so
                    // they're clearly differentiated from standard ones.
                    if (conn.IsTemp)
                        pen.DashPattern = new float[] { 4f, 3f };
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    e.Graphics.DrawLine(pen, c1, c2);
                }
            }
            UpdateCriteriaPanels();
        }

        private void PanelChart_MouseClick(object sender, MouseEventArgs e)
        {
            if (!deleteMode) return;

            const int hitTestRadius = 6;
            Connection toRemove = null;
            foreach (var conn in connections)
            {
                if (conn.From.Parent != panelChart || conn.To.Parent != panelChart)
                    continue;

                var p1 = conn.From;
                var p2 = conn.To;
                Point c1 = new Point(p1.Left + p1.Width / 2, p1.Top + p1.Height / 2);
                Point c2 = new Point(p2.Left + p2.Width / 2, p2.Top + p2.Height / 2);

                float dist = DistancePointToSegment(e.Location, c1, c2);
                if (dist <= hitTestRadius)
                {
                    toRemove = conn;
                    break;
                }
            }

            if (toRemove != null)
            {
                if (toRemove.CriteriaPanel != null)
                    panelChart.Controls.Remove(toRemove.CriteriaPanel);
                connections.Remove(toRemove);
                panelChart.Invalidate();
                deleteMode = false;
                btnDelete.BackColor = Color.LightGray;
                panelChart.Cursor = connectMode ? Cursors.Cross : Cursors.Default;
                return;
            }
        }

        #endregion

        #region Evolution Connections and Criteria

        /// <summary>
        /// Turns every canvas interaction mode off (connect, temp connect,
        /// delete, order).  Each toggle button re-enables just its own mode,
        /// keeping them mutually exclusive.
        /// </summary>
        private void ResetModes()
        {
            connectMode = false;
            tempConnectMode = false;
            deleteMode = false;
            orderMode = false;
            if (selectedForConnection != null)
            {
                selectedForConnection.BackColor = Color.White;
                selectedForConnection = null;
            }
            btnToggleConnect.BackColor = Color.LightGray;
            btnCreateTempEvo.BackColor = Color.LightGray;
            btnDelete.BackColor = Color.LightGray;
            btnEvolutionOrder.BackColor = Color.LightGray;
            panelChart.Cursor = Cursors.Default;
        }

        /// <summary>
        /// Handles the connect mode toggle button click.
        /// </summary>
        private void BtnToggleConnect_Click(object sender, EventArgs e)
        {
            bool wasActive = connectMode;
            ResetModes();
            connectMode = !wasActive;
            btnToggleConnect.BackColor = connectMode ? Color.LightGreen : Color.LightGray;
            panelChart.Cursor = connectMode ? Cursors.Cross : Cursors.Default;
        }

        /// <summary>
        /// Handles the temporary-evolution connect mode toggle button click.
        /// Works like connect mode but creates a dashed temporary evolution.
        /// </summary>
        private void BtnCreateTempEvo_Click(object sender, EventArgs e)
        {
            bool wasActive = tempConnectMode;
            ResetModes();
            tempConnectMode = !wasActive;
            btnCreateTempEvo.BackColor = tempConnectMode ? Color.MediumAquamarine : Color.LightGray;
            panelChart.Cursor = tempConnectMode ? Cursors.Cross : Cursors.Default;
        }

        /// <summary>
        /// Handles the evolution-order mode toggle button click.  While
        /// active, clicking a pet on the canvas opens the order window.
        /// </summary>
        private void BtnEvolutionOrder_Click(object sender, EventArgs e)
        {
            bool wasActive = orderMode;
            ResetModes();
            orderMode = !wasActive;
            btnEvolutionOrder.BackColor = orderMode ? Color.Khaki : Color.LightGray;
            panelChart.Cursor = orderMode ? Cursors.Hand : Cursors.Default;
        }

        /// <summary>
        /// Handles click for connecting two pet panels (standard or temporary
        /// evolution, depending on the active mode).
        /// </summary>
        private void PetPanel_ClickForConnection(object sender, EventArgs e)
        {
            if (!connectMode && !tempConnectMode) return;
            if (!(sender is Panel panel)) return;

            if (selectedForConnection == null)
            {
                selectedForConnection = panel;
                panel.BackColor = Color.LightBlue;
            }
            else if (selectedForConnection != panel)
            {
                var fromPet = (Pet)selectedForConnection.Tag;
                var toPet = (Pet)panel.Tag;
                var conn = new Connection(selectedForConnection, panel, GetRandomLineColor());
                if (tempConnectMode)
                {
                    var tempEvo = new TempEvolution { To = toPet.Name };
                    conn.IsTemp = true;
                    conn.CriteriaPanel = CreateTempCriteriaPanel(tempEvo);
                }
                else
                {
                    var evo = new Evolution { To = toPet.Name };
                    conn.CriteriaPanel = CreateCriteriaPanel(evo);
                }
                panelChart.Controls.Add(conn.CriteriaPanel);
                conn.CriteriaPanel.BringToFront();
                connections.Add(conn);
                selectedForConnection.BackColor = Color.White;
                selectedForConnection = null;
                panelChart.Invalidate();
                UpdateCriteriaPanels();
            }
        }

        /// <summary>
        /// Handles click on a pet panel while Evolution Order mode is active —
        /// opens the ordering window for that pet's outgoing evolutions.
        /// </summary>
        private void PetPanel_ClickForOrder(object sender, EventArgs e)
        {
            if (!orderMode) return;
            if (!(sender is Panel panel) || !(panel.Tag is Pet pet)) return;

            var normalConns = connections.Where(c => c.From == panel && !c.IsTemp).ToList();
            var tempConns = connections.Where(c => c.From == panel && c.IsTemp).ToList();
            if (normalConns.Count == 0 && tempConns.Count == 0)
            {
                MessageBox.Show($"{pet.Name} has no evolutions on the canvas.",
                    "Evolution Order", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            EvolutionOrderForm.Entry MakeEntry(Connection c) => new EvolutionOrderForm.Entry
            {
                Criteria = c.CriteriaPanel?.Tag,
                Target = c.To.Tag as Pet,
                Key = c
            };

            string primary = module?.PrimarySpriteFormat ?? "Color";
            string secondary = module?.SecondarySpriteFormat ?? "HD";
            Func<Pet, Image> spriteLoader = p => p == null ? null :
                SpriteUtils.LoadSingleSprite(p.Name, modulePath, PetUtils.FixedNameFormat, primary, secondary);

            using (var form = new EvolutionOrderForm(
                pet.Name,
                normalConns.Select(MakeEntry).ToList(),
                tempConns.Select(MakeEntry).ToList(),
                spriteLoader))
            {
                if (form.ShowDialog(this) != DialogResult.OK)
                    return;

                // Re-append this pet's outgoing connections in the chosen
                // order.  SaveEvolutionTree writes pet.Evolve / pet.TempEvolve
                // in connections-list order, so this persists the new order.
                foreach (var c in normalConns.Concat(tempConns))
                    connections.Remove(c);
                foreach (var entry in (form.NormalOrder ?? new List<EvolutionOrderForm.Entry>())
                         .Concat(form.TempOrder ?? new List<EvolutionOrderForm.Entry>()))
                {
                    if (entry.Key is Connection c)
                        connections.Add(c);
                }
            }
        }

        /// <summary>
        /// Handles click for deleting a pet panel and its connections.
        /// </summary>
        private void PetPanel_ClickForDelete(object sender, EventArgs e)
        {
            if (!deleteMode) return;
            if (!(sender is Panel panel)) return;

            var toRemove = connections.Where(conn => conn.From == panel || conn.To == panel).ToList();
            foreach (var conn in toRemove)
            {
                if (conn.CriteriaPanel != null)
                    panelChart.Controls.Remove(conn.CriteriaPanel);
                connections.Remove(conn);
            }

            panelChart.Controls.Remove(panel);
            panelChart.Invalidate();
            deleteMode = false;
            btnDelete.BackColor = Color.LightGray;
            panelChart.Cursor = connectMode ? Cursors.Cross : Cursors.Default;
            UpdateCanvasScroll();
        }

        /// <summary>
        /// Updates the position of criteria panels for all connections.
        /// </summary>
        private void UpdateCriteriaPanels()
        {
            var grouped = connections
                .Where(c => c.CriteriaPanel != null && c.From.Parent == panelChart && c.To.Parent == panelChart)
                .GroupBy(c => new { From = c.From, To = c.To })
                .ToList();

            // where layout coordinate 0 currently sits, so a box is kept on
            // the sheet without being pinned to the corner once it is scrolled
            var origin = panelChart.AutoScrollPosition;

            foreach (var group in grouped)
            {
                var connList = group.ToList();
                int n = connList.Count;
                if (n == 1)
                {
                    var conn = connList[0];
                    var p1 = conn.From;
                    var p2 = conn.To;
                    Point c1 = new Point(p1.Left + p1.Width / 2, p1.Top + p1.Height / 2);
                    Point c2 = new Point(p2.Left + p2.Width / 2, p2.Top + p2.Height / 2);
                    int x = (c1.X + c2.X) / 2 - conn.CriteriaPanel.Width / 2;
                    int y = (c1.Y + c2.Y) / 2 - conn.CriteriaPanel.Height / 2;
                    conn.CriteriaPanel.Location = new Point(Math.Max(origin.X, x),
                                                            Math.Max(origin.Y, y));
                    conn.CriteriaPanel.BringToFront();
                }
                else
                {
                    var p1 = group.Key.From;
                    var p2 = group.Key.To;
                    Point c1 = new Point(p1.Left + p1.Width / 2, p1.Top + p1.Height / 2);
                    Point c2 = new Point(p2.Left + p2.Width / 2, p2.Top + p2.Height / 2);
                    int centerX = (c1.X + c2.X) / 2;
                    int centerY = (c1.Y + c2.Y) / 2;
                    int totalWidth = connList.Sum(c => c.CriteriaPanel.Width) + (n - 1) * 8;
                    int startX = centerX - totalWidth / 2;
                    for (int i = 0; i < n; i++)
                    {
                        var panel = connList[i].CriteriaPanel;
                        int x = startX;
                        int y = centerY - panel.Height / 2;
                        panel.Location = new Point(Math.Max(origin.X, x),
                                                   Math.Max(origin.Y, y));
                        panel.BringToFront();
                        startX += panel.Width + 8;
                    }
                }
            }
        }

        /// <summary>
        /// Creates a panel displaying evolution criteria (rendering shared
        /// with the Evolution Order window via EvolutionBoxRenderer).
        /// Clicking it opens the criteria editor and rebuilds the panel.
        /// </summary>
        private Panel CreateCriteriaPanel(Evolution evo)
        {
            var panel = EvolutionBoxRenderer.CreatePanel(evo);

            EventHandler clickHandler = (s, e) =>
            {
                using (var form = new EvolutionCriteriaForm(evo, this.items))
                {
                    if (form.ShowDialog(this) == DialogResult.OK)
                        ReplaceCriteriaPanel(panel, CreateCriteriaPanel(evo));
                }
            };
            EvolutionBoxRenderer.WireClick(panel, clickHandler);
            return panel;
        }

        /// <summary>
        /// Creates a criteria panel for a temporary (battle-only) evolution.
        /// Clicking it opens the temporary-evolution criteria editor.
        /// </summary>
        private Panel CreateTempCriteriaPanel(TempEvolution evo)
        {
            var panel = EvolutionBoxRenderer.CreatePanel(evo);

            EventHandler clickHandler = (s, e) =>
            {
                using (var form = new TempEvolutionCriteriaForm(evo, module, pets, modulePath))
                {
                    if (form.ShowDialog(this) == DialogResult.OK)
                        ReplaceCriteriaPanel(panel, CreateTempCriteriaPanel(evo));
                }
            };
            EvolutionBoxRenderer.WireClick(panel, clickHandler);
            return panel;
        }

        /// <summary>
        /// Swaps an on-canvas criteria panel for a freshly rendered one
        /// (after its evolution was edited) and relinks its connection.
        /// </summary>
        private void ReplaceCriteriaPanel(Panel oldPanel, Panel newPanel)
        {
            var parent = oldPanel.Parent;
            int idx = parent?.Controls.IndexOf(oldPanel) ?? -1;
            if (parent != null && idx >= 0)
            {
                parent.Controls.Remove(oldPanel);
                parent.Controls.Add(newPanel);
                newPanel.BringToFront();
            }
            var conn = connections.FirstOrDefault(c => c.CriteriaPanel == oldPanel);
            if (conn != null)
                conn.CriteriaPanel = newPanel;
            UpdateCriteriaPanels();
        }

        /// <summary>
        /// Serializer options for comparing evolution records by content.
        /// </summary>
        private static readonly JsonSerializerOptions DedupJsonOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        /// <summary>
        /// Content signature of an evolution record — covers every
        /// requirement field, so two paths to the same target with different
        /// requirements produce different signatures.
        /// </summary>
        private static string CriteriaSignature(object record)
            => record == null ? "" : JsonSerializer.Serialize(record, record.GetType(), DedupJsonOptions);

        /// <summary>
        /// After a pet lands on the canvas, restore every stored evolution
        /// (standard and temporary) between it and the pets already there —
        /// in both directions (its own evolutions, and other pets evolving
        /// into it).
        /// </summary>
        private void AutoConnectPet(Panel newPanel)
        {
            if (!(newPanel.Tag is Pet newPet))
                return;

            var petPanels = panelChart.Controls.OfType<Panel>()
                .Where(p => p.Tag is Pet)
                .ToList();

            foreach (var otherPanel in petPanels)
            {
                if (otherPanel == newPanel)
                    continue;
                EnsureConnectionsBetween(otherPanel, newPanel); // others → new pet
                EnsureConnectionsBetween(newPanel, otherPanel); // new pet → others
            }
        }

        /// <summary>
        /// Make sure every evolution record stored on <paramref name="fromPanel"/>'s
        /// pet that targets <paramref name="toPanel"/>'s pet has exactly one
        /// connection on the canvas.
        ///
        /// A pet can have multiple paths to the same target (same "to",
        /// different requirements), so matching is done by full-content
        /// signature with multiset counting: each existing connection only
        /// "consumes" one stored record with identical requirements, and any
        /// remaining path gets its own connection — no path is ever lost or
        /// collapsed into another.
        /// </summary>
        private void EnsureConnectionsBetween(Panel fromPanel, Panel toPanel)
        {
            if (!(fromPanel.Tag is Pet fromPet) || !(toPanel.Tag is Pet toPet))
                return;
            if (fromPet.Version != toPet.Version)
                return;

            // Existing connection signatures between the two pets (matched by
            // pet, not panel, so duplicate panels of a pet don't double-add).
            List<string> ExistingSignatures(bool isTemp) => connections
                .Where(c => c.IsTemp == isTemp
                            && (c.From.Tag as Pet) == fromPet
                            && (c.To.Tag as Pet) == toPet
                            && c.CriteriaPanel?.Tag != null)
                .Select(c => CriteriaSignature(c.CriteriaPanel.Tag))
                .ToList();

            var normalSigs = ExistingSignatures(false);
            foreach (var evo in (fromPet.Evolve ?? new List<Evolution>())
                     .Where(ev => ev.To == toPet.Name))
            {
                if (normalSigs.Remove(CriteriaSignature(evo)))
                    continue; // this exact path is already on the canvas
                var conn = new Connection(fromPanel, toPanel, GetRandomLineColor());
                conn.CriteriaPanel = CreateCriteriaPanel(evo);
                panelChart.Controls.Add(conn.CriteriaPanel);
                conn.CriteriaPanel.BringToFront();
                connections.Add(conn);
            }

            var tempSigs = ExistingSignatures(true);
            foreach (var tempEvo in (fromPet.TempEvolve ?? new List<TempEvolution>())
                     .Where(ev => ev.To == toPet.Name))
            {
                if (tempSigs.Remove(CriteriaSignature(tempEvo)))
                    continue; // this exact path is already on the canvas
                var conn = new Connection(fromPanel, toPanel, GetRandomLineColor()) { IsTemp = true };
                conn.CriteriaPanel = CreateTempCriteriaPanel(tempEvo);
                panelChart.Controls.Add(conn.CriteriaPanel);
                conn.CriteriaPanel.BringToFront();
                connections.Add(conn);
            }
        }

        #endregion

        #region Utility and Save Methods

        /// <summary>
        /// Sizes the scrollable area to the content plus one margin, so the
        /// bars stop where the chart stops.  The old version doubled the area
        /// whenever anything came near an edge, which is what let the
        /// scrollbars run away.  <paramref name="allowShrink"/> is false while
        /// a panel is being dragged, so the sheet does not shift under the
        /// pointer mid-drag.
        /// </summary>
        private void UpdateCanvasScroll(bool allowShrink = true)
        {
            // panel coordinates move with the scroll position, while
            // AutoScrollMinSize is measured from the unscrolled origin
            var scroll = panelChart.AutoScrollPosition;
            int maxX = 0, maxY = 0;
            foreach (Control ctrl in panelChart.Controls)
            {
                if (ctrl is Panel p)
                {
                    maxX = Math.Max(maxX, p.Right - scroll.X);
                    maxY = Math.Max(maxY, p.Bottom - scroll.Y);
                }
            }

            int width = maxX + CanvasMargin;
            int height = maxY + CanvasMargin;
            if (!allowShrink)
            {
                width = Math.Max(width, panelChart.AutoScrollMinSize.Width);
                height = Math.Max(height, panelChart.AutoScrollMinSize.Height);
            }

            if (panelChart.AutoScrollMinSize.Width != width ||
                panelChart.AutoScrollMinSize.Height != height)
                panelChart.AutoScrollMinSize = new Size(width, height);
        }

        /// <summary>
        /// Organizes the pet panels on the canvas by stage.
        /// </summary>
        private void BtnOrganize_Click(object sender, EventArgs e)
        {
            OrganizeCanvas();
        }

        // Pet panels are a fixed 190x56 - see CreatePetPanel.
        private const int NodeW = 190;
        private const int NodeH = 56;
        private const int NodeGapX = 24;      // between neighbours in a row
        private const int ClusterGapX = 64;   // between unconnected clusters
        private const int CanvasMargin = 40;
        private const int MinRowGap = 100;    // free height between two rows

        /// <summary>
        /// Arranges every pet panel on the canvas: one row per stage in use,
        /// each row ordered so the evolution lines cross as little as
        /// possible, and each panel then pulled towards the average of what it
        /// connects to - which lands an evolution directly under its source
        /// when it is the only one.  Panels that share no evolution form their
        /// own cluster and are packed side by side instead of being spread
        /// over the whole sheet.
        ///
        /// Both the initial build and the Organize button call this, so the
        /// chart opens in the arrangement the button produces.
        /// </summary>
        private void OrganizeCanvas()
        {
            var nodes = panelChart.Controls.OfType<Panel>()
                .Where(p => p.Tag is Pet)
                .ToList();
            if (nodes.Count == 0)
            {
                UpdateCanvasScroll();
                return;
            }

            // scrolling home first keeps panel coordinates and layout
            // coordinates the same thing while we place them
            panelChart.AutoScrollPosition = Point.Empty;

            var id = new Dictionary<Panel, int>();
            for (int i = 0; i < nodes.Count; i++)
                id[nodes[i]] = i;

            // one row per stage actually present, so an unused stage does not
            // leave an empty band across the chart
            var stages = nodes.Select(p => ((Pet)p.Tag).Stage).Distinct().OrderBy(s => s).ToList();
            var rowOfStage = new Dictionary<int, int>();
            for (int i = 0; i < stages.Count; i++)
                rowOfStage[stages[i]] = i;
            int rowCount = stages.Count;
            var row = nodes.Select(p => rowOfStage[((Pet)p.Tag).Stage]).ToArray();

            var adj = new List<int>[nodes.Count];
            for (int i = 0; i < nodes.Count; i++)
                adj[i] = new List<int>();
            foreach (var conn in connections)
            {
                int a, b;
                if (!id.TryGetValue(conn.From, out a) || !id.TryGetValue(conn.To, out b))
                    continue;
                if (a == b || adj[a].Contains(b))
                    continue;
                adj[a].Add(b);
                adj[b].Add(a);
            }

            var x = new double[nodes.Count];
            double packX = 0;

            foreach (var members in FindClusters(adj, row))
            {
                var rows = new List<int>[rowCount];
                for (int r = 0; r < rowCount; r++)
                    rows[r] = new List<int>();
                foreach (var i in SeedOrder(members, adj, row))
                    rows[row[i]].Add(i);

                // barycentre sweeps settle the order inside each row
                var pos = new double[nodes.Count];
                for (int r = 0; r < rowCount; r++)
                    for (int k = 0; k < rows[r].Count; k++)
                        pos[rows[r][k]] = k;
                for (int sweep = 0; sweep < 4; sweep++)
                {
                    for (int r = 1; r < rowCount; r++)
                        SortRowByNeighbours(rows[r], adj, row, pos, -1);
                    for (int r = rowCount - 2; r >= 0; r--)
                        SortRowByNeighbours(rows[r], adj, row, pos, +1);
                }

                // then the coordinates: one pass up, so a source centres over
                // what it evolves into, and one back down, so a target lines
                // up under its source.  Ending downwards is what puts an only
                // target directly below the panel it comes from.  A second
                // round is not worth it - measured over every chart in the
                // modules the alignment has already settled, and each extra
                // round only pushes rows further apart.
                const double pitch = NodeW + NodeGapX;
                for (int r = 0; r < rowCount; r++)
                    for (int k = 0; k < rows[r].Count; k++)
                        x[rows[r][k]] = k * pitch;
                for (int r = rowCount - 2; r >= 0; r--)
                    PlaceRow(rows[r], adj, row, x, +1, pitch);
                for (int r = 1; r < rowCount; r++)
                    PlaceRow(rows[r], adj, row, x, -1, pitch);

                double lo = members.Min(i => x[i]);
                double hi = members.Max(i => x[i]);
                foreach (var i in members)
                    x[i] += packX - lo;
                packX += (hi - lo) + NodeW + ClusterGapX;
            }

            // every gap has to hold the tallest requirement box drawn in it
            var gap = new double[rowCount];
            for (int r = 0; r < rowCount; r++)
                gap[r] = MinRowGap;
            foreach (var conn in connections)
            {
                int a, b;
                if (conn.CriteriaPanel == null) continue;
                if (!id.TryGetValue(conn.From, out a) || !id.TryGetValue(conn.To, out b))
                    continue;
                if (Math.Abs(row[a] - row[b]) != 1) continue;   // a longer jump has room already
                int top = Math.Min(row[a], row[b]);
                int boxHeight = Math.Max(conn.CriteriaPanel.Height,
                                         conn.CriteriaPanel.PreferredSize.Height);
                gap[top] = Math.Max(gap[top], boxHeight + 28);
            }

            var rowY = new double[rowCount];
            rowY[0] = CanvasMargin;
            for (int r = 1; r < rowCount; r++)
                rowY[r] = rowY[r - 1] + NodeH + gap[r - 1];

            foreach (var pair in id)
                pair.Key.Location = new Point(
                    (int)Math.Round(x[pair.Value]) + CanvasMargin,
                    (int)Math.Round(rowY[row[pair.Value]]));

            UpdateCriteriaPanels();
            UpdateCanvasScroll();
            panelChart.Invalidate();
        }

        /// <summary>
        /// Splits the canvas into groups of panels joined by evolutions.  The
        /// group reaching the shallowest stage comes first and, at the same
        /// stage, the bigger one does - so the egg lines lead and a stray
        /// panel dropped on the canvas ends up on the right.
        /// </summary>
        private static List<List<int>> FindClusters(List<int>[] adj, int[] row)
        {
            var of = new int[adj.Length];
            for (int i = 0; i < of.Length; i++)
                of[i] = -1;

            var found = new List<List<int>>();
            for (int i = 0; i < of.Length; i++)
            {
                if (of[i] >= 0) continue;
                var members = new List<int>();
                var stack = new Stack<int>();
                stack.Push(i);
                of[i] = found.Count;
                while (stack.Count > 0)
                {
                    int n = stack.Pop();
                    members.Add(n);
                    foreach (var m in adj[n])
                        if (of[m] < 0)
                        {
                            of[m] = found.Count;
                            stack.Push(m);
                        }
                }
                members.Sort();
                found.Add(members);
            }

            return found
                .OrderBy(m => m.Min(i => row[i]))
                .ThenByDescending(m => m.Count)
                .ThenBy(m => m[0])
                .ToList();
        }

        /// <summary>
        /// Walks a cluster from its topmost panels, so the branches of one
        /// source start out next to each other and the barycentre sweeps have
        /// a sane order to improve on.
        /// </summary>
        private static List<int> SeedOrder(List<int> members, List<int>[] adj, int[] row)
        {
            var seen = new HashSet<int>();
            var order = new List<int>();
            foreach (var start in members.OrderBy(i => row[i]).ThenBy(i => i))
            {
                var stack = new Stack<int>();
                stack.Push(start);
                while (stack.Count > 0)
                {
                    int n = stack.Pop();
                    if (!seen.Add(n)) continue;
                    order.Add(n);
                    var next = adj[n].Where(m => !seen.Contains(m))
                                     .OrderBy(m => row[m]).ThenBy(m => m).ToList();
                    for (int k = next.Count - 1; k >= 0; k--)
                        stack.Push(next[k]);
                }
            }
            return order;
        }

        /// <summary>
        /// Reorders one row by the average position of the neighbours each of
        /// its panels connects to in the row above (dir -1) or below (dir +1).
        /// A panel with no neighbour that way keeps its place.
        /// </summary>
        private static void SortRowByNeighbours(List<int> rowNodes, List<int>[] adj,
                                                int[] row, double[] pos, int dir)
        {
            if (rowNodes.Count < 2) return;

            var key = new Dictionary<int, double>();
            foreach (var n in rowNodes)
            {
                double sum = 0;
                int count = 0;
                foreach (var m in adj[n])
                    if (dir < 0 ? row[m] < row[n] : row[m] > row[n])
                    {
                        sum += pos[m];
                        count++;
                    }
                key[n] = count > 0 ? sum / count : pos[n];
            }

            var sorted = rowNodes.OrderBy(n => key[n]).ThenBy(n => pos[n]).ToList();
            rowNodes.Clear();
            rowNodes.AddRange(sorted);
            for (int k = 0; k < rowNodes.Count; k++)
                pos[rowNodes[k]] = k;
        }

        /// <summary>
        /// Places one row: every panel aims for the average x of the
        /// neighbours it connects to in <paramref name="dir"/>, and the row is
        /// then packed left to right and pulled back right to left so nothing
        /// overlaps.  The order inside the row is never changed.
        /// </summary>
        private static void PlaceRow(List<int> rowNodes, List<int>[] adj, int[] row,
                                     double[] x, int dir, double pitch)
        {
            int n = rowNodes.Count;
            if (n == 0) return;

            var want = new double[n];
            for (int k = 0; k < n; k++)
            {
                int node = rowNodes[k];
                double sum = 0;
                int count = 0;
                foreach (var m in adj[node])
                    if (dir < 0 ? row[m] < row[node] : row[m] > row[node])
                    {
                        sum += x[m];
                        count++;
                    }
                want[k] = count > 0 ? sum / count : x[node];
            }

            var placed = new double[n];
            placed[0] = want[0];
            for (int k = 1; k < n; k++)
                placed[k] = Math.Max(want[k], placed[k - 1] + pitch);
            for (int k = n - 2; k >= 0; k--)
                placed[k] = Math.Min(placed[k], placed[k + 1] - pitch);

            // Packing a crowded row pushes panels off the spot they asked for.
            // Correcting that by the average error of the whole row would drag
            // the panels that did fit off their mark too, so instead each run
            // of panels that ended up pressed against each other is shifted by
            // its own average error, as far as the runs beside it allow.  A
            // panel with room to spare then keeps exactly the position it
            // asked for, which is what leaves a 1:1 chain in a straight line.
            int start = 0;
            while (start < n)
            {
                int end = start;
                while (end + 1 < n && placed[end + 1] - placed[end] < pitch + 0.5)
                    end++;

                double shift = 0;
                for (int k = start; k <= end; k++)
                    shift += want[k] - placed[k];
                shift /= (end - start + 1);

                if (start > 0)
                    shift = Math.Max(shift, placed[start - 1] + pitch - placed[start]);
                if (end + 1 < n)
                    shift = Math.Min(shift, placed[end + 1] - pitch - placed[end]);

                for (int k = start; k <= end; k++)
                    placed[k] += shift;
                start = end + 1;
            }

            for (int k = 0; k < n; k++)
                x[rowNodes[k]] = placed[k];
        }

        /// <summary>
        /// Utility function for point-to-line distance.
        /// </summary>
        private float DistancePointToSegment(Point pt, Point p1, Point p2)
        {
            float dx = p2.X - p1.X;
            float dy = p2.Y - p1.Y;
            if (dx == 0 && dy == 0)
                return (float)Math.Sqrt((pt.X - p1.X) * (pt.X - p1.X) + (pt.Y - p1.Y) * (pt.Y - p1.Y));
            float t = ((pt.X - p1.X) * dx + (pt.Y - p1.Y) * dy) / (dx * dx + dy * dy);
            t = Math.Max(0, Math.Min(1, t));
            float projX = p1.X + t * dx;
            float projY = p1.Y + t * dy;
            return (float)Math.Sqrt((pt.X - projX) * (pt.X - projX) + (pt.Y - projY) * (pt.Y - projY));
        }

        /// <summary>
        /// Saves the current evolution tree to the pet objects.
        /// </summary>
        private void SaveEvolutionTree()
        {
            if (cmbVersions.SelectedIndex < 0) return;
            int selectedVersion = int.Parse(cmbVersions.SelectedItem.ToString().Replace(Resources.Label_Version + " ", ""));
            var petsInCanvas = panelChart.Controls.OfType<Panel>()
                .Where(p => p.Tag is Pet pet && pet.Version == selectedVersion)
                .Select(p => (Pet)p.Tag)
                .ToList();

            foreach (var pet in petsInCanvas)
            {
                pet.Evolve = new List<Evolution>();
                pet.TempEvolve = null; // rebuilt below; stays null when empty
            }

            foreach (var conn in connections)
            {
                if (!(conn.From.Tag is Pet fromPet) || !(conn.To.Tag is Pet toPet))
                    continue;
                if (fromPet.Version != selectedVersion || toPet.Version != selectedVersion)
                    continue;

                if (conn.IsTemp)
                {
                    var tempEvo = conn.CriteriaPanel?.Tag as TempEvolution
                                  ?? new TempEvolution();
                    var clone = tempEvo.Clone();
                    clone.To = toPet.Name;
                    if (fromPet.TempEvolve == null)
                        fromPet.TempEvolve = new List<TempEvolution>();
                    fromPet.TempEvolve.Add(clone);
                    continue;
                }

                Evolution evo = null;
                if (conn.CriteriaPanel != null && conn.CriteriaPanel.Tag is Evolution taggedEvo)
                {
                    evo = taggedEvo;
                }
                else
                {
                    evo = fromPet.Evolve?.FirstOrDefault(e => e.To == toPet.Name);
                }

                if (evo == null)
                    evo = new Evolution { To = toPet.Name };

                var evoClone = CloneEvolution(evo);
                evoClone.To = toPet.Name;
                fromPet.Evolve.Add(evoClone);
            }
        }

        /// <summary>
        /// Utility function to copy all fields of Evolution.
        /// </summary>
        private Evolution CloneEvolution(Evolution evo)
        {
            return new Evolution
            {
                To = evo.To,
                ConditionHearts = evo.ConditionHearts != null ? (int[])evo.ConditionHearts.Clone() : null,
                Training = evo.Training != null ? (int[])evo.Training.Clone() : null,
                Battles = evo.Battles != null ? (int[])evo.Battles.Clone() : null,
                WinRatio = evo.WinRatio != null ? (int[])evo.WinRatio.Clone() : null,
                WinCount = evo.WinCount != null ? (int[])evo.WinCount.Clone() : null, // New field
                Mistakes = evo.Mistakes != null ? (int[])evo.Mistakes.Clone() : null,
                Level = evo.Level != null ? (int[])evo.Level.Clone() : null,
                Overfeed = evo.Overfeed != null ? (int[])evo.Overfeed.Clone() : null,
                SleepDisturbances = evo.SleepDisturbances != null ? (int[])evo.SleepDisturbances.Clone() : null,
                Area = evo.Area,
                Stage = evo.Stage,
                Version = evo.Version,
                Attribute = evo.Attribute,
                Jogress = evo.Jogress,
                JogressPrefix = evo.JogressPrefix, // <-- Adicionado
                SpecialEncounter = evo.SpecialEncounter,
                Stage5 = evo.Stage5 != null ? (int[])evo.Stage5.Clone() : null,
                Stage6 = evo.Stage6 != null ? (int[])evo.Stage6.Clone() : null, // New field
                Stage7 = evo.Stage7 != null ? (int[])evo.Stage7.Clone() : null, // New field
                Stage8 = evo.Stage8 != null ? (int[])evo.Stage8.Clone() : null, // New field
                Item = evo.Item,
                TimeRange = evo.TimeRange != null ? (string[])evo.TimeRange.Clone() : null, // <-- Adicionado
                Trophies = evo.Trophies != null ? (int[])evo.Trophies.Clone() : null, // New field
                VitalValues = evo.VitalValues != null ? (int[])evo.VitalValues.Clone() : null, // New field
                Weigth = evo.Weigth != null ? (int[])evo.Weigth.Clone() : null, // New field
                QuestsCompleted = evo.QuestsCompleted != null ? (int[])evo.QuestsCompleted.Clone() : null, // New field
                Pvp = evo.Pvp != null ? (int[])evo.Pvp.Clone() : null, // New field

                // G-Cell evolution criteria
                GCellHatch = evo.GCellHatch,
                BlueGCells = evo.BlueGCells != null ? (int[])evo.BlueGCells.Clone() : null,
                YellowGCells = evo.YellowGCells != null ? (int[])evo.YellowGCells.Clone() : null,
                RedGCells = evo.RedGCells != null ? (int[])evo.RedGCells.Clone() : null
            };
        }

        /// <summary>
        /// Handles the delete mode toggle button click.
        /// </summary>
        private void BtnDelete_Click(object sender, EventArgs e)
        {
            bool wasActive = deleteMode;
            ResetModes();
            deleteMode = !wasActive;
            btnDelete.BackColor = deleteMode ? Color.Red : Color.LightGray;
            panelChart.Cursor = deleteMode ? Cursors.No : Cursors.Default;
        }

        /// <summary>
        /// Loads the items from the item.json file.
        /// </summary>
        private void LoadItemsFromJson()
        {
            this.items = new List<Item>();
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
                            this.items = JsonSerializer.Deserialize<List<Item>>(itemsElement.GetRawText());
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error loading item.json: " + ex.Message);
                }
            }
        }

        #endregion

        #region Internal Classes

        /// <summary>
        /// Represents a visual connection (evolution) between two pet panels.
        /// </summary>
        private class Connection
        {
            public Panel From { get; }
            public Panel To { get; }
            public Color LineColor { get; }
            public Panel CriteriaPanel { get; set; }
            /// <summary>True for temporary (battle-only) evolutions — drawn dashed.</summary>
            public bool IsTemp { get; set; }
            public Connection(Panel from, Panel to, Color color)
            {
                From = from;
                To = to;
                LineColor = color;
                CriteriaPanel = null;
            }
        }

        #endregion
    }
}
