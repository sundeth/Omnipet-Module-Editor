using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace OmnipetModuleEditor.OmniNet
{
    /// <summary>
    /// Form for managing module contributors.
    /// </summary>
    public class ManageContributorsForm : Form
    {
        private readonly string _moduleId;
        private readonly string _moduleName;
        
        private ListBox lstContributors;
        private TextBox txtNewContributor;
        private Button btnAdd;
        private Button btnRemove;
        private Button btnSave;
        private Button btnCancel;
        private Label lblError;
        private ProgressBar progressBar;
        private List<string> _contributors = new List<string>();
        private bool _isOwner;

        public ManageContributorsForm(string moduleId, string moduleName)
        {
            _moduleId = moduleId;
            _moduleName = moduleName;
            InitializeComponent();
            LoadContributors();
        }

        private void InitializeComponent()
        {
            this.Text = "Manage Contributors";
            this.Size = new Size(400, 400);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 6,
                Padding = new Padding(15)
            };

            // Header
            var lblHeader = new Label
            {
                Text = $"Contributors for: {_moduleName}",
                Dock = DockStyle.Fill,
                Font = new Font(SystemFonts.DefaultFont.FontFamily, 10, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Height = 30
            };
            layout.Controls.Add(lblHeader, 0, 0);

            // Contributors list
            lstContributors = new ListBox
            {
                Dock = DockStyle.Fill,
                Height = 150
            };
            layout.Controls.Add(lstContributors, 0, 1);

            // Add contributor panel
            var addPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Height = 35
            };
            addPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            addPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60));
            addPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));

            txtNewContributor = new TextBox
            {
                Dock = DockStyle.Fill
            };
            // PlaceholderText not available in .NET Framework 4.7.2, use tooltip instead
            var toolTip = new ToolTip();
            toolTip.SetToolTip(txtNewContributor, "Enter nickname to add as contributor");
            addPanel.Controls.Add(txtNewContributor, 0, 0);

            btnAdd = new Button
            {
                Text = "Add",
                Dock = DockStyle.Fill,
                Height = 25
            };
            btnAdd.Click += BtnAdd_Click;
            addPanel.Controls.Add(btnAdd, 1, 0);

            btnRemove = new Button
            {
                Text = "Remove",
                Dock = DockStyle.Fill,
                Height = 25
            };
            btnRemove.Click += BtnRemove_Click;
            addPanel.Controls.Add(btnRemove, 2, 0);

            layout.Controls.Add(addPanel, 0, 2);

            // Error label
            lblError = new Label
            {
                Text = "",
                Dock = DockStyle.Fill,
                ForeColor = Color.Red,
                TextAlign = ContentAlignment.MiddleCenter
            };
            layout.Controls.Add(lblError, 0, 3);

            // Progress bar
            progressBar = new ProgressBar
            {
                Dock = DockStyle.Fill,
                Style = ProgressBarStyle.Marquee,
                Visible = false,
                Height = 10
            };
            layout.Controls.Add(progressBar, 0, 4);

            // Buttons
            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 10, 0, 0)
            };

            btnCancel = new Button
            {
                Text = "Cancel",
                Width = 80,
                Height = 30,
                DialogResult = DialogResult.Cancel
            };
            buttonPanel.Controls.Add(btnCancel);

            btnSave = new Button
            {
                Text = "Save Changes",
                Width = 100,
                Height = 30,
                Margin = new Padding(0, 0, 10, 0),
                Enabled = false
            };
            btnSave.Click += BtnSave_Click;
            buttonPanel.Controls.Add(btnSave);

            layout.Controls.Add(buttonPanel, 0, 5);

            this.Controls.Add(layout);
            this.CancelButton = btnCancel;
        }

        private async void LoadContributors()
        {
            SetLoading(true);

            try
            {
                var config = OmniNetConfig.Instance;
                using (var client = new OmniNetApiClient())
                {
                    var response = await client.GetContributorsAsync(_moduleId, config.SecretKey);

                    if (response.Success && response.Data != null && response.Data.success)
                    {
                        _isOwner = response.Data.is_owner;
                        
                        // Extract nicknames from contributor info list
                        _contributors = response.Data.contributors?
                            .Select(c => c.nickname)
                            .ToList() ?? new List<string>();
                        
                        RefreshList();

                        if (!_isOwner)
                        {
                            lblError.Text = "You can view contributors but only the owner can modify them.";
                            lblError.ForeColor = Color.Orange;
                            btnAdd.Enabled = false;
                            btnRemove.Enabled = false;
                            btnSave.Enabled = false;
                        }
                        else
                        {
                            btnSave.Enabled = true;
                        }
                    }
                    else
                    {
                        lblError.Text = response.ErrorMessage ?? response.Data?.message ?? "Failed to load contributors.";
                    }
                }
            }
            catch (Exception ex)
            {
                lblError.Text = $"Error: {ex.Message}";
            }
            finally
            {
                SetLoading(false);
            }
        }

        private void RefreshList()
        {
            lstContributors.Items.Clear();
            foreach (var contributor in _contributors)
            {
                lstContributors.Items.Add(contributor);
            }
        }

        private void BtnAdd_Click(object sender, EventArgs e)
        {
            var nickname = txtNewContributor.Text.Trim();
            
            if (string.IsNullOrWhiteSpace(nickname))
            {
                lblError.Text = "Please enter a nickname.";
                return;
            }

            if (_contributors.Contains(nickname, StringComparer.OrdinalIgnoreCase))
            {
                lblError.Text = "This contributor is already in the list.";
                return;
            }

            _contributors.Add(nickname);
            RefreshList();
            txtNewContributor.Clear();
            lblError.Text = "";
        }

        private void BtnRemove_Click(object sender, EventArgs e)
        {
            if (lstContributors.SelectedIndex < 0)
            {
                lblError.Text = "Please select a contributor to remove.";
                return;
            }

            _contributors.RemoveAt(lstContributors.SelectedIndex);
            RefreshList();
            lblError.Text = "";
        }

        private async void BtnSave_Click(object sender, EventArgs e)
        {
            SetLoading(true);
            lblError.Text = "";

            try
            {
                var config = OmniNetConfig.Instance;
                using (var client = new OmniNetApiClient())
                {
                    var response = await client.UpdateContributorsAsync(_moduleId, config.SecretKey, _contributors);

                    if (response.Success && response.Data != null && response.Data.success)
                    {
                        MessageBox.Show("Contributors updated successfully!", "Success",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                        this.DialogResult = DialogResult.OK;
                        this.Close();
                    }
                    else
                    {
                        lblError.Text = response.ErrorMessage ?? response.Data?.message ?? "Failed to update contributors.";
                    }
                }
            }
            catch (Exception ex)
            {
                lblError.Text = $"Error: {ex.Message}";
            }
            finally
            {
                SetLoading(false);
            }
        }

        private void SetLoading(bool loading)
        {
            progressBar.Visible = loading;
            btnAdd.Enabled = !loading && _isOwner;
            btnRemove.Enabled = !loading && _isOwner;
            btnSave.Enabled = !loading && _isOwner;
            txtNewContributor.Enabled = !loading && _isOwner;
            lstContributors.Enabled = !loading;
        }
    }
}
