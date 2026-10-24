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
    /// Tab for managing and editing quests and events in the module.
    /// </summary>
    public partial class QuestEventTab : UserControl, IListClipboardTarget
    {
        private TabControl mainTabControl;
        private TabPage questTab;
        private TabPage eventTab;
        private QuestPanel questPanel;
        private EventPanel eventPanel;
        private TabPage connectTab;
        private TabPage scheduleTab;
        private ConnectPointsPanel connectPanel;
        private SchedulePanel schedulePanel;
        private string modulePath;
        private Module module;

        /// <summary>Ctrl+C — copy the selected row of the active (Quests/Events) sub-tab.</summary>
        public void CopySelection()
        {
            if (mainTabControl.SelectedTab == questTab) questPanel.CopySelection();
            else if (mainTabControl.SelectedTab == eventTab) eventPanel.CopySelection();
        }

        /// <summary>Ctrl+V — paste a duplicate row into the active sub-tab.</summary>
        public void PasteClipboard()
        {
            if (mainTabControl.SelectedTab == questTab) questPanel.PasteClipboard();
            else if (mainTabControl.SelectedTab == eventTab) eventPanel.PasteClipboard();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="QuestEventTab"/> class.
        /// </summary>
        public QuestEventTab()
        {
            InitializeComponent();
            this.Name = "QuestEventTab";
        }

        /// <summary>
        /// Sets the module context for this tab.
        /// </summary>
        public void SetModule(string modulePath, Module module)
        {
            this.modulePath = modulePath;
            this.module = module;
            questPanel.SetModule(modulePath, module);
            eventPanel.SetModule(modulePath, module);
            connectPanel.Bind(eventPanel);
            schedulePanel.Bind(eventPanel);
        }

        /// <summary>
        /// Saves quests and events to their respective JSON files. The
        /// Connect Points and Schedule sub-tabs are part of events.json.
        /// </summary>
        public void Save()
        {
            questPanel?.Save();
            connectPanel?.Commit();
            schedulePanel?.Commit();
            eventPanel?.Save();
        }

        #region Layout and UI Initialization

        /// <summary>
        /// Initializes the layout and UI controls.
        /// </summary>
        private void InitializeComponent()
        {
            this.Dock = DockStyle.Fill;

            mainTabControl = new TabControl
            {
                Dock = DockStyle.Fill
            };

            // Quests Tab
            questTab = new TabPage("Quests");
            questPanel = new QuestPanel();
            questPanel.Dock = DockStyle.Fill;
            questTab.Controls.Add(questPanel);

            // Events Tab
            eventTab = new TabPage("Events");
            eventPanel = new EventPanel();
            eventPanel.Dock = DockStyle.Fill;
            eventTab.Controls.Add(eventPanel);

            // Connect Points and Schedule (events.json blocks)
            connectTab = new TabPage("Connect Points");
            connectPanel = new ConnectPointsPanel();
            connectTab.Controls.Add(connectPanel);
            scheduleTab = new TabPage("Schedule");
            schedulePanel = new SchedulePanel();
            scheduleTab.Controls.Add(schedulePanel);

            mainTabControl.TabPages.Add(questTab);
            mainTabControl.TabPages.Add(eventTab);
            mainTabControl.TabPages.Add(connectTab);
            mainTabControl.TabPages.Add(scheduleTab);

            this.Controls.Add(mainTabControl);
        }

        #endregion

        #region Internal Classes

        /// <summary>
        /// Panel for managing quests.
        /// </summary>
        private class QuestPanel : UserControl
        {
            private DataGridView dgvQuests;
            private BindingSource bindingSource;
            private Button btnAdd;
            private Button btnRemove;
            private List<Quest> quests = new List<Quest>();
            private string modulePath;
            private Module module;
            private List<Item> items = new List<Item>();
            private List<string> itemNames = new List<string>(); // Cache item names
            private bool isUpdatingCombos = false; // Prevent recursive updates

            public QuestPanel()
            {
                InitializeComponent();
            }

            public void SetModule(string modulePath, Module module)
            {
                this.modulePath = modulePath;
                this.module = module;
                LoadItemsFromJson();
                LoadQuestsFromJson();
            }

            public void Save()
            {
                if (string.IsNullOrEmpty(modulePath) || quests == null)
                    return;

                // Update quest names automatically
                UpdateQuestNames();

                string questPath = Path.Combine(modulePath, "quests.json");
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                };
                var obj = new { quests = quests };
                try
                {
                    string json = JsonSerializer.Serialize(obj, options);
                    File.WriteAllText(questPath, json);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error saving quests.json: " + ex.Message);
                }
            }

            private void InitializeComponent()
            {
                this.Dock = DockStyle.Fill;

                var mainLayout = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    RowCount = 2,
                    ColumnCount = 1,
                    Padding = new Padding(8),
                    BackColor = SystemColors.Control
                };
                mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
                mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));

                // Initialize BindingSource
                bindingSource = new BindingSource();

                dgvQuests = new DataGridView
                {
                    Dock = DockStyle.Fill,
                    AutoGenerateColumns = false,
                    AllowUserToAddRows = false,
                    AllowUserToDeleteRows = false,
                    RowHeadersVisible = false,
                    SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                    MultiSelect = false,
                    ReadOnly = false,
                    AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                    DataSource = bindingSource
                };

                // Name column (read-only, auto-generated)
                var colName = new DataGridViewTextBoxColumn
                {
                    DataPropertyName = "Name",
                    HeaderText = "Name",
                    ReadOnly = true
                };
                colName.ToolTipText =
                    "Name shown to the player when the quest is offered and in the quest log.";
                dgvQuests.Columns.Add(colName);

                // Quest Type column
                var colQuestType = new DataGridViewComboBoxColumn
                {
                    DataPropertyName = "QuestType",
                    HeaderText = "Type",
                    DataSource = Enum.GetValues(typeof(QuestType)),
                    FlatStyle = FlatStyle.Flat
                };
                colQuestType.ToolTipText =
                    "What the player must do: Boss, Training, Battle, Feeding, Evolution, Armor_Evolution or Jogress. The engine counts progress against whichever action this names. Daily_Gift asks nothing: it is offered every day, in one of the three daily slots, ready to claim (the amounts are ignored).";
                dgvQuests.Columns.Add(colQuestType);

                // Target Amount Range columns
                var colRangeMin = new DataGridViewTextBoxColumn
                {
                    Name = "RangeMin",
                    HeaderText = "Min Amount",
                    ReadOnly = false
                };
                colRangeMin.ToolTipText =
                    "Lowest target the quest can roll. The actual target is drawn between Min and Max when the quest is generated, so a spread makes the same quest easier or harder on different runs.";
                dgvQuests.Columns.Add(colRangeMin);

                var colRangeMax = new DataGridViewTextBoxColumn
                {
                    Name = "RangeMax",
                    HeaderText = "Max Amount",
                    ReadOnly = false
                };
                colRangeMax.ToolTipText =
                    "Highest target the quest can roll. Set it equal to Min for a fixed target.";
                dgvQuests.Columns.Add(colRangeMax);

                // Reward Type column
                var colRewardType = new DataGridViewComboBoxColumn
                {
                    DataPropertyName = "RewardType",
                    HeaderText = "Reward Type",
                    DataSource = Enum.GetValues(typeof(RewardType)),
                    FlatStyle = FlatStyle.Flat
                };
                colRewardType.ToolTipText =
                    "What completing the quest pays out - Experience, Trophy and Vital Values are added to the pet directly; an Item reward hands over the Reward Value item instead.";
                dgvQuests.Columns.Add(colRewardType);

                // Reward Value column - set up with initial empty data source
                var colRewardValue = new DataGridViewComboBoxColumn
                {
                    Name = "RewardValue", // Add the Name property
                    DataPropertyName = "RewardValue",
                    HeaderText = "Reward Value",
                    FlatStyle = FlatStyle.Flat,
                    DataSource = new List<string> { "" } // Initial empty source
                };
                colRewardValue.ToolTipText =
                    "The reward itself: the item granted, or the stat that Quantity is added to.";
                dgvQuests.Columns.Add(colRewardValue);

                // Reward Quantity column
                var colRewardQuantity = new DataGridViewTextBoxColumn
                {
                    DataPropertyName = "RewardQuantity",
                    HeaderText = "Quantity"
                };
                colRewardQuantity.ToolTipText =
                    "How much of the reward is paid out.";
                dgvQuests.Columns.Add(colRewardQuantity);

                dgvQuests.CellFormatting += DgvQuests_CellFormatting;
                dgvQuests.CellValueChanged += DgvQuests_CellValueChanged;
                dgvQuests.CurrentCellDirtyStateChanged += DgvQuests_CurrentCellDirtyStateChanged;

                // Button panel
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

                mainLayout.Controls.Add(dgvQuests, 0, 0);
                mainLayout.Controls.Add(buttonPanel, 0, 1);

                this.Controls.Add(mainLayout);
            }

            private void DgvQuests_CurrentCellDirtyStateChanged(object sender, EventArgs e)
            {
                if (dgvQuests.IsCurrentCellDirty)
                {
                    dgvQuests.CommitEdit(DataGridViewDataErrorContexts.Commit);
                }
            }

            private void LoadQuestsFromJson()
            {
                quests.Clear();
                if (string.IsNullOrEmpty(modulePath))
                    return;

                string questPath = Path.Combine(modulePath, "quests.json");
                if (File.Exists(questPath))
                {
                    try
                    {
                        string json = File.ReadAllText(questPath);
                        using (JsonDocument doc = JsonDocument.Parse(json))
                        {
                            var root = doc.RootElement;
                            if (root.TryGetProperty("quests", out var questsElement))
                            {
                                quests = JsonSerializer.Deserialize<List<Quest>>(questsElement.GetRawText()) ?? new List<Quest>();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error loading quests.json: " + ex.Message);
                    }
                }

                // Ensure all quests have proper IDs and arrays
                foreach (var quest in quests)
                {
                    if (string.IsNullOrEmpty(quest.Id))
                        quest.Id = Guid.NewGuid().ToString();
                    if (quest.TargetAmountRange == null || quest.TargetAmountRange.Length < 2)
                        quest.TargetAmountRange = new int[] { 1, 1 };
                }

                bindingSource.DataSource = quests;
                bindingSource.ResetBindings(false);
                
                // Update reward value combo after loading
                UpdateRewardValueCombo();
            }

            private void LoadItemsFromJson()
            {
                items.Clear();
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
                                items = JsonSerializer.Deserialize<List<Item>>(itemsElement.GetRawText()) ?? new List<Item>();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error loading item.json: " + ex.Message);
                    }
                }

                // Cache item names
                itemNames = items.Select(i => i.Name).ToList();
                itemNames.Insert(0, "");
            }

            private void UpdateRewardValueCombo()
            {
                if (isUpdatingCombos) return;
                isUpdatingCombos = true;

                try
                {
                    var rewardValueColumn = dgvQuests.Columns["RewardValue"] as DataGridViewComboBoxColumn;
                    if (rewardValueColumn != null)
                    {
                        // For now, we'll keep it simple and show item names for all reward types
                        // This could be enhanced later to show different options based on reward type:
                        // - Item: item names
                        // - Trophy: trophy names/IDs
                        // - Experience: experience amounts
                        // - Vital_Values: vital value amounts
                        rewardValueColumn.DataSource = itemNames.ToList();
                    }
                }
                finally
                {
                    isUpdatingCombos = false;
                }
            }

            private void DgvQuests_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
            {
                if (e.RowIndex < 0 || e.RowIndex >= quests.Count || isUpdatingCombos) return;

                var quest = quests[e.RowIndex];
                var column = dgvQuests.Columns[e.ColumnIndex];

                if (column.Name == "RangeMin")
                {
                    e.Value = quest.TargetAmountRange?.Length > 0 ? quest.TargetAmountRange[0] : 1;
                }
                else if (column.Name == "RangeMax")
                {
                    e.Value = quest.TargetAmountRange?.Length > 1 ? quest.TargetAmountRange[1] : 1;
                }
                // Remove the RewardValue cell formatting that was causing the loop
            }

            private void DgvQuests_CellValueChanged(object sender, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex < 0 || e.RowIndex >= quests.Count) return;

                var quest = quests[e.RowIndex];
                var column = dgvQuests.Columns[e.ColumnIndex];

                if (column.Name == "RangeMin" || column.Name == "RangeMax")
                {
                    if (quest.TargetAmountRange == null)
                        quest.TargetAmountRange = new int[2] { 1, 1 };

                    var cell = dgvQuests.Rows[e.RowIndex].Cells[e.ColumnIndex];
                    if (int.TryParse(cell.Value?.ToString(), out int value) && value > 0)
                    {
                        if (column.Name == "RangeMin")
                            quest.TargetAmountRange[0] = value;
                        else
                            quest.TargetAmountRange[1] = value;
                    }
                    UpdateQuestName(quest);
                }
                else if (column.DataPropertyName == "QuestType")
                {
                    UpdateQuestName(quest);
                }
                else if (column.DataPropertyName == "RewardType")
                {
                    // When reward type changes, update the combo box data source
                    UpdateRewardValueCombo();
                }

                bindingSource.ResetBindings(false);
            }

            private void UpdateQuestNames()
            {
                foreach (var quest in quests)
                {
                    UpdateQuestName(quest);
                }
            }

            private void UpdateQuestName(Quest quest)
            {
                if (quest.TargetAmountRange?.Length >= 2)
                {
                    int min = quest.TargetAmountRange[0];
                    int max = quest.TargetAmountRange[1];
                    
                    string amountText = min == max ? min.ToString() : $"{min}-{max}";
                    
                    // Generate quest name based on type
                    switch (quest.QuestType)
                    {
                        case QuestType.Boss:
                            quest.Name = min == max && min == 1 
                                ? $"Defeat {amountText} Boss" 
                                : $"Defeat {amountText} Bosses";
                            break;
                        case QuestType.Training:
                            quest.Name = min == max && min == 1 
                                ? $"Train {amountText} Time" 
                                : $"Train {amountText} Times";
                            break;
                        case QuestType.Battle:
                            quest.Name = min == max && min == 1 
                                ? $"Win {amountText} Battle" 
                                : $"Win {amountText} Battles";
                            break;
                        case QuestType.Feeding:
                            quest.Name = min == max && min == 1 
                                ? $"Eat {amountText} Time" 
                                : $"Eat {amountText} Times";
                            break;
                        case QuestType.Evolution:
                            quest.Name = min == max && min == 1 
                                ? $"Evolve {amountText} Time" 
                                : $"Evolve {amountText} Times";
                            break;
                        case QuestType.Armor_Evolution:
                            quest.Name = min == max && min == 1 
                                ? $"Armor Evolve {amountText} Time" 
                                : $"Armor Evolve {amountText} Times";
                            break;
                        case QuestType.Jogress:
                            quest.Name = min == max && min == 1
                                ? $"Jogress {amountText} Time"
                                : $"Jogress {amountText} Times";
                            break;
                        case QuestType.Daily_Gift:
                            // Its own name (DW1's "Meat Farm") is kept; a new one is named
                            // after what it gives.
                            if (string.IsNullOrWhiteSpace(quest.Name) || quest.Name == "New Quest" || quest.Name.StartsWith("Complete ")
                                || quest.Name.StartsWith("Defeat ") || quest.Name.StartsWith("Train ") || quest.Name.StartsWith("Win ")
                                || quest.Name.StartsWith("Eat ") || quest.Name.StartsWith("Evolve ") || quest.Name.StartsWith("Armor Evolve ")
                                || quest.Name.StartsWith("Jogress "))
                                quest.Name = string.IsNullOrWhiteSpace(quest.RewardValue) ? "Daily Gift" : $"Daily {quest.RewardValue}";
                            break;
                        default:
                            quest.Name = $"Complete {amountText} {quest.QuestType}";
                            break;
                    }
                }
            }

            private void BtnAdd_Click(object sender, EventArgs e)
            {
                var newQuest = new Quest
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = "New Quest",
                    QuestType = QuestType.Boss,
                    TargetAmountRange = new int[] { 1, 1 },
                    RewardType = RewardType.Item,
                    RewardValue = "",
                    RewardQuantity = 1
                };
                UpdateQuestName(newQuest);
                
                quests.Add(newQuest);
                bindingSource.ResetBindings(false);

                // Select the newly added row
                if (dgvQuests.Rows.Count > 0)
                {
                    try
                    {
                        dgvQuests.CurrentCell = dgvQuests.Rows[dgvQuests.Rows.Count - 1].Cells[0];
                    }
                    catch
                    {
                        // Ignore selection errors
                    }
                }
            }

            private void BtnRemove_Click(object sender, EventArgs e)
            {
                if (bindingSource.Current is Quest selectedQuest)
                {
                    var result = MessageBox.Show(
                        $"Do you want to remove the quest \"{selectedQuest.Name}\"?",
                        "Confirmation",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning
                    );
                    if (result == DialogResult.Yes)
                    {
                        quests.Remove(selectedQuest);
                        bindingSource.ResetBindings(false);
                    }
                }
            }

            private Quest copiedQuest;

            public void CopySelection()
            {
                if (bindingSource.Current is Quest selected)
                    copiedQuest = ClipboardListUtils.DeepClone(selected);
            }

            public void PasteClipboard()
            {
                if (copiedQuest == null) return;
                var newQuest = ClipboardListUtils.DeepClone(copiedQuest);
                newQuest.Id = Guid.NewGuid().ToString();
                quests.Add(newQuest);
                bindingSource.ResetBindings(false);
                if (dgvQuests.Rows.Count > 0)
                {
                    try { dgvQuests.CurrentCell = dgvQuests.Rows[dgvQuests.Rows.Count - 1].Cells[0]; }
                    catch { }
                }
            }
        }

        /// <summary>
        /// Panel for managing events.
        /// </summary>
        private class EventPanel : UserControl
        {
            private DataGridView dgvEvents;
            private BindingSource bindingSource;
            private Button btnAdd;
            private Button btnRemove;
            private List<Event> events = new List<Event>();
            private string modulePath;
            private Module module;
            private List<Item> items = new List<Item>();
            private List<string> itemNames = new List<string>(); // Cache item names
            private bool isUpdatingCombos = false; // Prevent recursive updates

            /// <summary>events.json's other blocks, which the Connect Points
            /// and Schedule sub-tabs edit: kept here so one file is written.</summary>
            public ConnectPoints ConnectPoints { get; private set; } = new ConnectPoints();
            public Dictionary<string, List<string>> Schedule { get; private set; } = new Dictionary<string, List<string>>();
            public List<string> ItemNames => itemNames;

            public EventPanel()
            {
                InitializeComponent();
            }

            public void SetModule(string modulePath, Module module)
            {
                this.modulePath = modulePath;
                this.module = module;
                LoadItemsFromJson();
                LoadEventsFromJson();
            }

            public void Save()
            {
                if (string.IsNullOrEmpty(modulePath) || events == null)
                    return;

                string eventPath = Path.Combine(modulePath, "events.json");
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                };
                // The blocks are written only when they hold something, so a
                // module without them keeps the file's old shape.
                var obj = new Dictionary<string, object> { { "events", events } };
                if (Schedule.Count > 0)
                    obj["schedule"] = Schedule;
                if (ConnectPoints.Cap > 0 || ConnectPoints.Rewards.Count > 0)
                    obj["connect_points"] = ConnectPoints;
                try
                {
                    string json = JsonSerializer.Serialize(obj, options);
                    File.WriteAllText(eventPath, json);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error saving events.json: " + ex.Message);
                }
            }

            private void InitializeComponent()
            {
                this.Dock = DockStyle.Fill;

                var mainLayout = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    RowCount = 2,
                    ColumnCount = 1,
                    Padding = new Padding(8),
                    BackColor = SystemColors.Control
                };
                mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
                mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));

                // Initialize BindingSource
                bindingSource = new BindingSource();

                dgvEvents = new DataGridView
                {
                    Dock = DockStyle.Fill,
                    AutoGenerateColumns = false,
                    AllowUserToAddRows = false,
                    AllowUserToDeleteRows = false,
                    RowHeadersVisible = false,
                    SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                    MultiSelect = false,
                    ReadOnly = false,
                    // Too many columns to fill the width: each takes what it
                    // needs and the grid scrolls.
                    AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells,
                    ScrollBars = ScrollBars.Both,
                    DataSource = bindingSource
                };

                // Name column
                var colName = new DataGridViewTextBoxColumn
                {
                    DataPropertyName = "Name",
                    HeaderText = "Name",
                    ReadOnly = false
                };
                colName.ToolTipText =
                    "Name of the random event, shown when it fires.";
                dgvEvents.Columns.Add(colName);

                // Global column
                var colGlobal = new DataGridViewCheckBoxColumn
                {
                    DataPropertyName = "Global",
                    HeaderText = "Global",
                    ReadOnly = false
                };
                colGlobal.ToolTipText =
                    "Lets this event fire even when the player is not raising a pet from this module. A module the player IS raising offers all of its events; every other module contributes only the ones ticked here.";
                dgvEvents.Columns.Add(colGlobal);

                var colUntilFriend = new DataGridViewCheckBoxColumn
                {
                    DataPropertyName = "UntilFriend",
                    HeaderText = "Until Friend",
                    ReadOnly = false
                };
                colUntilFriend.ToolTipText =
                    "SpecialEncounter: the event stops once its Digimon is a Friend of the module (beaten as a battle.json enemy with Friend set). Digimon World's recruits are met until recruited.";
                dgvEvents.Columns.Add(colUntilFriend);

                // Event Type column
                var colEventType = new DataGridViewComboBoxColumn
                {
                    DataPropertyName = "Type",
                    HeaderText = "Type",
                    DataSource = Enum.GetValues(typeof(EventType)),
                    FlatStyle = FlatStyle.Flat
                };
                colEventType.ToolTipText =
                    "What the event does when the player accepts it.";
                dgvEvents.Columns.Add(colEventType);

                // Chance Percent column
                var colChancePercent = new DataGridViewTextBoxColumn
                {
                    DataPropertyName = "ChancePercent",
                    HeaderText = "Chance %",
                    ReadOnly = false
                };
                colChancePercent.ToolTipText =
                    "Relative weight within this module, not an independent probability: the game picks a module, sums the weights of its events and rolls against that total. Doubling one event\u2019s number makes it twice as likely as its siblings.";
                dgvEvents.Columns.Add(colChancePercent);

                // Area column
                var colArea = new DataGridViewTextBoxColumn
                {
                    DataPropertyName = "Area",
                    HeaderText = "Area",
                    ReadOnly = false
                };
                colArea.ToolTipText =
                    "Adventure area the event sends the player to, where that applies.";
                dgvEvents.Columns.Add(colArea);

                // Round column
                var colRound = new DataGridViewTextBoxColumn
                {
                    DataPropertyName = "Round",
                    HeaderText = "Round",
                    ReadOnly = false
                };
                colRound.ToolTipText =
                    "Round within that area.";
                dgvEvents.Columns.Add(colRound);

                // Item column - set up with initial data source
                var colItem = new DataGridViewComboBoxColumn
                {
                    DataPropertyName = "Item",
                    HeaderText = "Item",
                    FlatStyle = FlatStyle.Flat,
                    DataSource = new List<string> { "" } // Initial empty source
                };
                colItem.ToolTipText =
                    "Item the event hands over. It must be an item declared on the Items tab.";
                colItem.Name = "Item"; // Add the Name property
                dgvEvents.Columns.Add(colItem);

                // Item Quantity column
                var colItemQuantity = new DataGridViewTextBoxColumn
                {
                    DataPropertyName = "ItemQuantity",
                    HeaderText = "Quantity"
                };
                colItemQuantity.ToolTipText =
                    "How many copies of the item the event gives.";
                dgvEvents.Columns.Add(colItemQuantity);

                // Triggers and the newer event types
                var colTrigger = new DataGridViewComboBoxColumn
                {
                    DataPropertyName = "Trigger",
                    HeaderText = "Trigger",
                    FlatStyle = FlatStyle.Flat,
                    DataSource = new List<string> { "", "random", "adventure", "area_clear", "connection", "schedule" }
                };
                colTrigger.ToolTipText =
                    "When the event is considered. random (or empty): the hourly roll, Chance a weight among the module's random events. adventure / area_clear / connection: after an adventure battle, an area clear, or a battle with another device or pet, Chance a chance in 100. schedule: at the times of the pet's Encounter Time group on the Schedule tab.";
                dgvEvents.Columns.Add(colTrigger);

                var colOutcome = new DataGridViewComboBoxColumn
                {
                    DataPropertyName = "Outcome",
                    HeaderText = "Outcome",
                    FlatStyle = FlatStyle.Flat,
                    DataSource = new List<string> { "", "win", "loss", "any" }
                };
                colOutcome.ToolTipText = "For a battle trigger: whether it follows a win (the default), a loss or any battle.";
                dgvEvents.Columns.Add(colOutcome);

                AddTextColumn("OpponentDeviceText", "Opp. Device",
                    "For a connection trigger: the device_version the other side must be. -1 any device but the pet's own; empty any.");
                AddTextColumn("ProtocolText", "Protocol",
                    "For a connection trigger: the battle format it has to be fought on (IC, DMM ...). Empty: any. Device numbers repeat across formats: a Digimon Mini Ver.1 and an iC 10X both read 1.");
                AddTextColumn("VersionsText", "Versions",
                    "The pet versions the event is for, as in 1, 2. Empty for all.");
                AddTextColumn("TriggerAreasText", "Areas",
                    "For an adventure or area_clear trigger: the adventure areas whose battles roll it (the area fought in, or the one just cleared), as in 8, 9. Empty for any area. Digimon World's recruits are met in their own region.");
                AddTextColumn("Shop", "Shop",
                    "ShopAccess: the shop.json shop it opens. Mark that shop Event Only on the Shop tab so nothing else opens it.");
                AddTextColumn("AccessMinutes", "Access Min",
                    "ShopAccess: how many minutes the shop stays open. 0 opens it for good. -1 is one visit: after a battle the shop is the alert, accepting it goes straight in, and leaving it (or letting the alert go) closes it - the iC's DigiShops after a connection.");
                AddTextColumn("DrawText", "Draw",
                    "For a battle trigger: events with the same draw name are one roll - at most one of those fitting the battle happens, each with its Chance of the hundred in turn. The iC 20X after a connection: half the time a shop by the device beaten, or the Coliseum run. Empty: rolled on its own.");
                AddTextColumn("RewardRound", "Reward Round",
                    "BattleRun: the round after which Item is paid. 0 pays after the last.");
                AddTextColumn("DelayText", "Delay",
                    "FriendBattle after a battle: minutes it is held back, as in 1-5 (DMXW's area-clear Friend battles).");
                AddTextColumn("PoolField", "Pool Field",
                    "PoolBattle: the monster field whose value picks the pool, e.g. encounter_opponent_group.");

                var colPools = new DataGridViewButtonColumn
                {
                    Name = "Pools",
                    DataPropertyName = "PoolsText",
                    HeaderText = "Pools",
                    FlatStyle = FlatStyle.Flat
                };
                colPools.ToolTipText =
                    "PoolBattle: each pool's special encounters, met in turn (an empty turn means no encounter that time). Click to edit.";
                dgvEvents.Columns.Add(colPools);

                var colSequence = new DataGridViewButtonColumn
                {
                    Name = "Sequence",
                    DataPropertyName = "SequenceText",
                    HeaderText = "Item Sequence",
                    FlatStyle = FlatStyle.Flat
                };
                colSequence.ToolTipText =
                    "CardBattle: the item each win pays, in turn (an empty step is a win without an item); each pet keeps its own place. The VBE's is the BE's 200-step grant order. Click to edit.";
                dgvEvents.Columns.Add(colSequence);

                var colItemPool = new DataGridViewButtonColumn
                {
                    Name = "ItemPool",
                    DataPropertyName = "ItemPoolText",
                    HeaderText = "Item Pool",
                    FlatStyle = FlatStyle.Flat
                };
                colItemPool.ToolTipText =
                    "ItemPackage or BattleRun: one of these items at random, drawn each time the event happens, instead of Item (the iC 20X's Special Coliseum: any Attack +2 weapon). Click to edit.";
                dgvEvents.Columns.Add(colItemPool);
                dgvEvents.CellContentClick += DgvEvents_CellContentClick;
                dgvEvents.DataError += (s, e) => { e.ThrowException = false; };

                // Remove the problematic CellFormatting event
                // dgvEvents.CellFormatting += DgvEvents_CellFormatting;

                // Button panel
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

                mainLayout.Controls.Add(dgvEvents, 0, 0);
                mainLayout.Controls.Add(buttonPanel, 0, 1);

                this.Controls.Add(mainLayout);
            }

            private void AddTextColumn(string property, string header, string tip)
            {
                dgvEvents.Columns.Add(new DataGridViewTextBoxColumn
                {
                    DataPropertyName = property,
                    HeaderText = header,
                    ToolTipText = tip
                });
            }

            private void DgvEvents_CellContentClick(object sender, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex < 0 || e.RowIndex >= events.Count)
                    return;
                var ev = events[e.RowIndex];
                if (dgvEvents.Columns[e.ColumnIndex].Name == "Sequence")
                {
                    using (var dialog = new SequenceDialog(ev.ItemSequence, itemNames))
                    {
                        if (dialog.ShowDialog(this) == DialogResult.OK)
                        {
                            ev.ItemSequence = dialog.Sequence.Count > 0 ? dialog.Sequence : null;
                            bindingSource.ResetBindings(false);
                        }
                    }
                    return;
                }
                if (dgvEvents.Columns[e.ColumnIndex].Name == "ItemPool")
                {
                    using (var dialog = new SequenceDialog(ev.ItemPool, itemNames, "Item Pool", "#",
                                                           "Item (one drawn at random)"))
                    {
                        if (dialog.ShowDialog(this) == DialogResult.OK)
                        {
                            var pool = dialog.Sequence.Where(n => !string.IsNullOrEmpty(n)).ToList();
                            ev.ItemPool = pool.Count > 0 ? pool : null;
                            bindingSource.ResetBindings(false);
                        }
                    }
                    return;
                }
                if (dgvEvents.Columns[e.ColumnIndex].Name != "Pools")
                    return;
                using (var dialog = new PoolsDialog(ev.Pools))
                {
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                    {
                        ev.Pools = dialog.Pools.Count > 0 ? dialog.Pools : null;
                        bindingSource.ResetBindings(false);
                    }
                }
            }

            private void LoadEventsFromJson()
            {
                events.Clear();
                Schedule = new Dictionary<string, List<string>>();
                ConnectPoints = new ConnectPoints();
                if (string.IsNullOrEmpty(modulePath))
                    return;

                string eventPath = Path.Combine(modulePath, "events.json");
                if (File.Exists(eventPath))
                {
                    try
                    {
                        string json = File.ReadAllText(eventPath);
                        using (JsonDocument doc = JsonDocument.Parse(json))
                        {
                            var root = doc.RootElement;
                            if (root.TryGetProperty("events", out var eventsElement))
                            {
                                events = JsonSerializer.Deserialize<List<Event>>(eventsElement.GetRawText()) ?? new List<Event>();
                            }
                            if (root.TryGetProperty("schedule", out var scheduleElement))
                                Schedule = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(scheduleElement.GetRawText())
                                           ?? new Dictionary<string, List<string>>();
                            if (root.TryGetProperty("connect_points", out var pointsElement))
                                ConnectPoints = JsonSerializer.Deserialize<ConnectPoints>(pointsElement.GetRawText())
                                                ?? new ConnectPoints();
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error loading events.json: " + ex.Message);
                    }
                }

                // Ensure all events have proper IDs
                foreach (var eventItem in events)
                {
                    if (string.IsNullOrEmpty(eventItem.Id))
                        eventItem.Id = Guid.NewGuid().ToString();
                }

                bindingSource.DataSource = events;
                bindingSource.ResetBindings(false);
                
                // Update item combo after loading
                UpdateItemCombo();
            }

            private void LoadItemsFromJson()
            {
                items.Clear();
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
                                items = JsonSerializer.Deserialize<List<Item>>(itemsElement.GetRawText()) ?? new List<Item>();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error loading item.json: " + ex.Message);
                    }
                }

                // Cache item names
                itemNames = items.Select(i => i.Name).ToList();
                itemNames.Insert(0, "");
            }

            private void UpdateItemCombo()
            {
                if (isUpdatingCombos) return;
                isUpdatingCombos = true;

                try
                {
                    var itemColumn = dgvEvents.Columns["Item"] as DataGridViewComboBoxColumn;
                    if (itemColumn != null)
                    {
                        // Set the global data source for all cells in this column
                        itemColumn.DataSource = itemNames.ToList();
                    }
                }
                finally
                {
                    isUpdatingCombos = false;
                }
            }

            private void BtnAdd_Click(object sender, EventArgs e)
            {
                var newEvent = new Event
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = "New Event",
                    Global = false,
                    Type = EventType.ItemPackage,
                    ChancePercent = 10.0,
                    Area = 1,
                    Round = 1,
                    Item = "",
                    ItemQuantity = 1
                };
                
                events.Add(newEvent);
                bindingSource.ResetBindings(false);

                // Select the newly added row
                if (dgvEvents.Rows.Count > 0)
                {
                    try
                    {
                        dgvEvents.CurrentCell = dgvEvents.Rows[dgvEvents.Rows.Count - 1].Cells[0];
                    }
                    catch
                    {
                        // Ignore selection errors
                    }
                }
            }

            private void BtnRemove_Click(object sender, EventArgs e)
            {
                if (bindingSource.Current is Event selectedEvent)
                {
                    var result = MessageBox.Show(
                        $"Do you want to remove the event \"{selectedEvent.Name}\"?",
                        "Confirmation",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning
                    );
                    if (result == DialogResult.Yes)
                    {
                        events.Remove(selectedEvent);
                        bindingSource.ResetBindings(false);
                    }
                }
            }

            private Event copiedEvent;

            public void CopySelection()
            {
                if (bindingSource.Current is Event selected)
                    copiedEvent = ClipboardListUtils.DeepClone(selected);
            }

            public void PasteClipboard()
            {
                if (copiedEvent == null) return;
                var newEvent = ClipboardListUtils.DeepClone(copiedEvent);
                newEvent.Id = Guid.NewGuid().ToString();
                events.Add(newEvent);
                bindingSource.ResetBindings(false);
                if (dgvEvents.Rows.Count > 0)
                {
                    try { dgvEvents.CurrentCell = dgvEvents.Rows[dgvEvents.Rows.Count - 1].Cells[0]; }
                    catch { }
                }
            }
        }

        /// <summary>A pooled encounter's pools: one line a pool, its key, then
        /// its special encounters in turn ("" a turn with none).</summary>
        private class PoolsDialog : Form
        {
            private readonly DataGridView grid;
            public Dictionary<string, List<string>> Pools { get; private set; }

            public PoolsDialog(Dictionary<string, List<string>> pools)
            {
                Text = "Pools";
                Width = 760;
                Height = 420;
                StartPosition = FormStartPosition.CenterParent;
                grid = new DataGridView
                {
                    Dock = DockStyle.Fill,
                    AllowUserToAddRows = true,
                    AllowUserToDeleteRows = true,
                    RowHeadersWidth = 24,
                    AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
                };
                grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Key", HeaderText = "Pool", FillWeight = 15 });
                grid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = "Names", HeaderText = "Special encounters in turn (comma-separated; an empty entry is a turn with none)", FillWeight = 85
                });
                foreach (var pool in pools ?? new Dictionary<string, List<string>>())
                    grid.Rows.Add(pool.Key, string.Join(", ", pool.Value ?? new List<string>()));
                var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 36, FlowDirection = FlowDirection.RightToLeft };
                var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Width = 80 };
                var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 80 };
                ok.Click += (s, e) => Commit();
                buttons.Controls.Add(cancel);
                buttons.Controls.Add(ok);
                Controls.Add(grid);
                Controls.Add(buttons);
                AcceptButton = ok;
                CancelButton = cancel;
            }

            private void Commit()
            {
                grid.EndEdit();
                Pools = new Dictionary<string, List<string>>();
                foreach (DataGridViewRow row in grid.Rows)
                {
                    if (row.IsNewRow) continue;
                    var key = Convert.ToString(row.Cells["Key"].Value)?.Trim() ?? "";
                    if (key == "") continue;
                    Pools[key] = (Convert.ToString(row.Cells["Names"].Value) ?? "")
                        .Split(',').Select(n => n.Trim()).ToList();
                }
            }
        }

        /// <summary>A card battle's item sequence: one row a win, in turn, its
        /// item or none.</summary>
        private class SequenceDialog : Form
        {
            private readonly DataGridView grid;
            public List<string> Sequence { get; private set; }

            public SequenceDialog(List<string> sequence, List<string> itemNames, string title = "Item Sequence",
                                  string stepHeader = "Win", string itemHeader = "Item (empty: a win without one)")
            {
                Text = title;
                Width = 520;
                Height = 560;
                StartPosition = FormStartPosition.CenterParent;
                grid = new DataGridView
                {
                    Dock = DockStyle.Fill,
                    AllowUserToAddRows = true,
                    AllowUserToDeleteRows = true,
                    RowHeadersWidth = 24,
                    AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
                };
                grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Win", HeaderText = stepHeader, FillWeight = 15, ReadOnly = true });
                var names = (itemNames ?? new List<string>()).ToList();
                if (!names.Contains("")) names.Insert(0, "");
                foreach (var name in sequence ?? new List<string>())
                    if (!names.Contains(name ?? "")) names.Add(name ?? "");
                grid.Columns.Add(new DataGridViewComboBoxColumn
                {
                    Name = "Item", HeaderText = itemHeader, FillWeight = 85,
                    FlatStyle = FlatStyle.Flat, DataSource = names
                });
                grid.DataError += (s, e) => { e.ThrowException = false; };
                grid.RowsAdded += (s, e) => Number();
                grid.RowsRemoved += (s, e) => Number();
                foreach (var name in sequence ?? new List<string>())
                    grid.Rows.Add("", name ?? "");
                Number();
                var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 36, FlowDirection = FlowDirection.RightToLeft };
                var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Width = 80 };
                var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 80 };
                ok.Click += (s, e) => Commit();
                buttons.Controls.Add(cancel);
                buttons.Controls.Add(ok);
                Controls.Add(grid);
                Controls.Add(buttons);
                AcceptButton = ok;
                CancelButton = cancel;
            }

            private void Number()
            {
                foreach (DataGridViewRow row in grid.Rows)
                    if (!row.IsNewRow) row.Cells["Win"].Value = (row.Index + 1).ToString();
            }

            private void Commit()
            {
                grid.EndEdit();
                Sequence = new List<string>();
                foreach (DataGridViewRow row in grid.Rows)
                    if (!row.IsNewRow)
                        Sequence.Add(Convert.ToString(row.Cells["Item"].Value) ?? "");
            }
        }

        /// <summary>events.json's Connect Points: the cap (0 switches them
        /// off), the points a win and a loss give, the battles that give
        /// them, and the rewards a pet draws when it fills the meter, each
        /// for one pet version or all, each listed one equally likely, a
        /// "(no item)" row a draw of nothing.</summary>
        private class ConnectPointsPanel : UserControl
        {
            /// <summary>The Item combo's row for a draw that gives nothing
            /// (written as "").</summary>
            private const string NoItem = "(no item)";
            private readonly NumericUpDown numCap, numWin, numLoss;
            private readonly CheckBox chkAdventure, chkConnection;
            private readonly DataGridView grid;
            private readonly DataGridViewComboBoxColumn colItem;
            private EventPanel owner;

            public ConnectPointsPanel()
            {
                Dock = DockStyle.Fill;
                var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 5, Padding = new Padding(8) };
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                for (int i = 0; i < 4; i++) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                var tips = new ToolTip { AutoPopDelay = 20000 };

                Label Lbl(string text) => new Label { Text = text, TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill };
                numCap = new NumericUpDown { Minimum = 0, Maximum = 9999, Width = 80 };
                numWin = new NumericUpDown { Minimum = 0, Maximum = 9999, Width = 80 };
                numLoss = new NumericUpDown { Minimum = 0, Maximum = 9999, Width = 80 };
                layout.Controls.Add(Lbl("Cap:"), 0, 0);
                layout.Controls.Add(numCap, 1, 0);
                layout.Controls.Add(Lbl("Per win:"), 0, 1);
                layout.Controls.Add(numWin, 1, 1);
                layout.Controls.Add(Lbl("Per loss:"), 0, 2);
                layout.Controls.Add(numLoss, 1, 2);
                chkAdventure = new CheckBox { Text = "Adventure", AutoSize = true };
                chkConnection = new CheckBox { Text = "Connection", AutoSize = true, Checked = true };
                var battlesRow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
                battlesRow.Controls.Add(chkAdventure);
                battlesRow.Controls.Add(chkConnection);
                layout.Controls.Add(Lbl("Battles:"), 0, 3);
                layout.Controls.Add(battlesRow, 1, 3);
                tips.SetToolTip(numCap, "The points that fill the meter. 0 switches Connect Points off. The Digivice iC fills at 15. 1 makes a drop table rolled after every battle that counts, with no meter shown (the Vital Bracelets).");
                tips.SetToolTip(numWin, "Points a pet gets for a battle that counts and that it wins.");
                tips.SetToolTip(numLoss, "Points a pet gets for one it loses. 0: a loss gives nothing (with a cap of 1, only wins roll).");
                tips.SetToolTip(chkAdventure, "Adventure battles give points (the module's own adventure, or any adventure the pet fights in).");
                tips.SetToolTip(chkConnection, "Battles with another device or pet give points: DCom, Versus, online. Connection alone (or nothing ticked) is left out of the file: the default.");

                grid = new DataGridView
                {
                    Dock = DockStyle.Fill,
                    AllowUserToAddRows = true,
                    AllowUserToDeleteRows = true,
                    RowHeadersWidth = 24,
                    AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
                };
                grid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = "Version", HeaderText = "Version", FillWeight = 20,
                    ValueType = typeof(string),
                    ToolTipText = "The pet version that draws this reward; empty for every version."
                });
                colItem = new DataGridViewComboBoxColumn
                {
                    Name = "Item", HeaderText = "Item", FillWeight = 80, FlatStyle = FlatStyle.Flat,
                    ToolTipText = "The item given. Every reward listed for the pet's version is equally likely, so list an item twice to double its chance (the iC 10X: Leo Cap once, Level Reverse and Reliability Max twice). (no item) is a draw that gives nothing: the Vital Bracelets list it 56 times in 100."
                };
                grid.Columns.Add(colItem);
                grid.DataError += (s, e) => { e.ThrowException = false; };
                layout.Controls.Add(Lbl("Rewards:"), 0, 4);
                layout.Controls.Add(grid, 1, 4);
                Controls.Add(layout);
            }

            public void Bind(EventPanel panel)
            {
                owner = panel;
                var points = panel.ConnectPoints;
                numCap.Value = Math.Max(0, Math.Min(9999, points.Cap));
                numWin.Value = Math.Max(0, Math.Min(9999, points.Win));
                numLoss.Value = Math.Max(0, Math.Min(9999, points.Loss));
                var battles = (points.Battles ?? new List<string>()).Select(b => (b ?? "").ToLowerInvariant()).ToList();
                chkAdventure.Checked = battles.Contains("adventure");
                chkConnection.Checked = battles.Count == 0 || battles.Contains("connection");
                colItem.Items.Clear();
                colItem.Items.Add(NoItem);
                foreach (var name in panel.ItemNames.Concat(points.Rewards.Select(r => r.Item ?? ""))
                                                    .Where(n => n != "" && n != NoItem).Distinct())
                    colItem.Items.Add(name);
                grid.Rows.Clear();
                foreach (var reward in points.Rewards)
                    grid.Rows.Add(reward.Version?.ToString() ?? "", string.IsNullOrEmpty(reward.Item) ? NoItem : reward.Item);
            }

            public void Commit()
            {
                if (owner == null) return;
                grid.EndEdit();
                var points = owner.ConnectPoints;
                points.Cap = (int)numCap.Value;
                points.Win = (int)numWin.Value;
                points.Loss = (int)numLoss.Value;
                // Connections alone are the default and stay out of the file.
                var battles = new List<string>();
                if (chkAdventure.Checked) battles.Add("adventure");
                if (chkConnection.Checked) battles.Add("connection");
                points.Battles = chkAdventure.Checked ? battles : null;
                points.Rewards = new List<ConnectReward>();
                foreach (DataGridViewRow row in grid.Rows)
                {
                    if (row.IsNewRow) continue;
                    var item = Convert.ToString(row.Cells["Item"].Value) ?? "";
                    if (item == "") continue;
                    if (item == NoItem) item = "";
                    int version;
                    points.Rewards.Add(new ConnectReward
                    {
                        Version = int.TryParse(Convert.ToString(row.Cells["Version"].Value), out version) ? version : (int?)null,
                        Item = item
                    });
                }
            }
        }

        /// <summary>events.json's schedule: the times each encounter time
        /// group's scheduled events can fire (the Godzilla devices).</summary>
        private class SchedulePanel : UserControl
        {
            private readonly DataGridView grid;
            private EventPanel owner;

            public SchedulePanel()
            {
                Dock = DockStyle.Fill;
                grid = new DataGridView
                {
                    Dock = DockStyle.Fill,
                    AllowUserToAddRows = true,
                    AllowUserToDeleteRows = true,
                    RowHeadersWidth = 24,
                    AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
                };
                grid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = "Group", HeaderText = "Time Group", FillWeight = 15,
                    ToolTipText = "A Digimon's Encounter Time (Pet tab) names one of these."
                });
                grid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = "Times", HeaderText = "Times (HH:MM, comma-separated)", FillWeight = 85,
                    ToolTipText = "The minutes a schedule-triggered event can fire for those Digimon, each day."
                });
                Controls.Add(grid);
            }

            public void Bind(EventPanel panel)
            {
                owner = panel;
                grid.Rows.Clear();
                foreach (var group in panel.Schedule)
                    grid.Rows.Add(group.Key, string.Join(", ", group.Value ?? new List<string>()));
            }

            public void Commit()
            {
                if (owner == null) return;
                grid.EndEdit();
                owner.Schedule.Clear();
                foreach (DataGridViewRow row in grid.Rows)
                {
                    if (row.IsNewRow) continue;
                    var key = Convert.ToString(row.Cells["Group"].Value)?.Trim() ?? "";
                    if (key == "") continue;
                    owner.Schedule[key] = (Convert.ToString(row.Cells["Times"].Value) ?? "")
                        .Split(',').Select(t => t.Trim()).Where(t => t != "").ToList();
                }
            }
        }

        #endregion
    }
}