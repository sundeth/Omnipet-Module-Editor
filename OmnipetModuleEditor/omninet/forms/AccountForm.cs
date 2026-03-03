using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OmnipetModuleEditor.OmniNet
{
    /// <summary>
    /// Main account management form - shows login status and options.
    /// </summary>
    public class AccountForm : Form
    {
        private Label lblStatus;
        private Label lblNickname;
        private Label lblEmail;
        private Button btnCreateAccount;
        private Button btnLogin;
        private Button btnLogout;
        private Button btnLinkGame;
        private Button btnClose;
        private Panel panelLoggedIn;
        private Panel panelLoggedOut;

        public AccountForm()
        {
            InitializeComponent();
            UpdateUI();
        }

        private void InitializeComponent()
        {
            this.Text = "OmniNet Account";
            this.Size = new Size(400, 300);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1,
                Padding = new Padding(15)
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));

            // Logged out panel
            panelLoggedOut = new Panel { Dock = DockStyle.Fill };
            
            var loggedOutLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 4,
                ColumnCount = 1
            };
            loggedOutLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 30));
            loggedOutLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
            loggedOutLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
            loggedOutLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 20));

            var lblNotLoggedIn = new Label
            {
                Text = "You are not logged in to OmniNet.\nLog in to publish and manage modules.",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font(SystemFonts.DefaultFont.FontFamily, 10)
            };
            loggedOutLayout.Controls.Add(lblNotLoggedIn, 0, 0);

            btnCreateAccount = new Button
            {
                Text = "Create New Account",
                Dock = DockStyle.Fill,
                Margin = new Padding(40, 5, 40, 5),
                Height = 35
            };
            btnCreateAccount.Click += BtnCreateAccount_Click;
            loggedOutLayout.Controls.Add(btnCreateAccount, 0, 1);

            btnLogin = new Button
            {
                Text = "Login with Existing Account",
                Dock = DockStyle.Fill,
                Margin = new Padding(40, 5, 40, 5),
                Height = 35
            };
            btnLogin.Click += BtnLogin_Click;
            loggedOutLayout.Controls.Add(btnLogin, 0, 2);

            panelLoggedOut.Controls.Add(loggedOutLayout);

            // Logged in panel
            panelLoggedIn = new Panel { Dock = DockStyle.Fill };
            
            var loggedInLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 5,
                ColumnCount = 1
            };
            loggedInLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            loggedInLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            loggedInLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
            loggedInLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            loggedInLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));

            lblStatus = new Label
            {
                Text = "Logged in as:",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.BottomLeft,
                Font = new Font(SystemFonts.DefaultFont.FontFamily, 9)
            };
            loggedInLayout.Controls.Add(lblStatus, 0, 0);

            lblNickname = new Label
            {
                Text = "",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font(SystemFonts.DefaultFont.FontFamily, 12, FontStyle.Bold),
                ForeColor = Color.DarkBlue
            };
            loggedInLayout.Controls.Add(lblNickname, 0, 1);

            lblEmail = new Label
            {
                Text = "",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.TopLeft,
                Font = new Font(SystemFonts.DefaultFont.FontFamily, 9),
                ForeColor = Color.Gray
            };
            loggedInLayout.Controls.Add(lblEmail, 0, 2);

            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                WrapContents = false
            };

            btnLinkGame = new Button
            {
                Text = "Link Game Device",
                Width = 130,
                Height = 35,
                Margin = new Padding(0, 0, 10, 0)
            };
            btnLinkGame.Click += BtnLinkGame_Click;
            buttonPanel.Controls.Add(btnLinkGame);

            btnLogout = new Button
            {
                Text = "Logout",
                Width = 80,
                Height = 35
            };
            btnLogout.Click += BtnLogout_Click;
            buttonPanel.Controls.Add(btnLogout);

            loggedInLayout.Controls.Add(buttonPanel, 0, 4);

            panelLoggedIn.Controls.Add(loggedInLayout);

            // Add panels to main layout (they will be shown/hidden based on login state)
            var contentPanel = new Panel { Dock = DockStyle.Fill };
            contentPanel.Controls.Add(panelLoggedIn);
            contentPanel.Controls.Add(panelLoggedOut);
            mainLayout.Controls.Add(contentPanel, 0, 0);

            // Close button
            var bottomPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 10, 0, 0)
            };

            btnClose = new Button
            {
                Text = "Close",
                Width = 80,
                Height = 30,
                DialogResult = DialogResult.Cancel
            };
            bottomPanel.Controls.Add(btnClose);

            mainLayout.Controls.Add(bottomPanel, 0, 1);

            this.Controls.Add(mainLayout);
            this.CancelButton = btnClose;
        }

        private void UpdateUI()
        {
            var config = OmniNetConfig.Instance;
            
            System.Diagnostics.Debug.WriteLine($"[AccountForm] UpdateUI called - IsLoggedIn: {config.IsLoggedIn}");
            System.Diagnostics.Debug.WriteLine($"[AccountForm] Nickname: '{config.Nickname}'");
            System.Diagnostics.Debug.WriteLine($"[AccountForm] Email: '{config.UserEmail}'");
            
            if (config.IsLoggedIn)
            {
                panelLoggedIn.Visible = true;
                panelLoggedOut.Visible = false;
                panelLoggedIn.BringToFront();
                
                lblNickname.Text = config.Nickname ?? "Unknown";
                lblEmail.Text = config.UserEmail ?? "";
            }
            else
            {
                panelLoggedIn.Visible = false;
                panelLoggedOut.Visible = true;
                panelLoggedOut.BringToFront();
            }
        }

        private void BtnCreateAccount_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("[AccountForm] BtnCreateAccount_Click called");
            
            using (var form = new CreateAccountForm())
            {
                System.Diagnostics.Debug.WriteLine("[AccountForm] Opening CreateAccountForm dialog");
                var result = form.ShowDialog(this);
                System.Diagnostics.Debug.WriteLine($"[AccountForm] CreateAccountForm closed with result: {result}");
                
                if (result == DialogResult.OK)
                {
                    System.Diagnostics.Debug.WriteLine("[AccountForm] Result is OK, showing success message first");
                    
                    // Show success message FIRST
                    MessageBox.Show("Account created and logged in successfully!", "Success", 
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    System.Diagnostics.Debug.WriteLine("[AccountForm] Success message closed, now calling UpdateUI()");
                    
                    // Then reload the UI to show logged-in state with correct nickname
                    UpdateUI();
                    System.Diagnostics.Debug.WriteLine("[AccountForm] UpdateUI() completed");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[AccountForm] Result is NOT OK, it is: {result}");
                }
            }
        }

        private void BtnLogin_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("[AccountForm] BtnLogin_Click called");
            
            using (var form = new LoginForm())
            {
                System.Diagnostics.Debug.WriteLine("[AccountForm] Opening LoginForm dialog");
                var result = form.ShowDialog(this);
                System.Diagnostics.Debug.WriteLine($"[AccountForm] LoginForm closed with result: {result}");
                
                if (result == DialogResult.OK)
                {
                    System.Diagnostics.Debug.WriteLine("[AccountForm] Result is OK, showing success message first");
                    
                    // Show success message FIRST
                    MessageBox.Show("Logged in successfully!", "Success", 
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    System.Diagnostics.Debug.WriteLine("[AccountForm] Success message closed, now calling UpdateUI()");
                    
                    // Then reload the UI to show logged-in state with correct nickname
                    UpdateUI();
                    System.Diagnostics.Debug.WriteLine("[AccountForm] UpdateUI() completed");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[AccountForm] Result is NOT OK, it is: {result}");
                }
            }
        }

        private async void BtnLogout_Click(object sender, EventArgs e)
        {
            var result = MessageBox.Show("Are you sure you want to logout?", "Confirm Logout",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            
            if (result != DialogResult.Yes)
                return;

            using (var client = new OmniNetApiClient())
            {
                var config = OmniNetConfig.Instance;
                await client.LogoutAsync(config.SecretKey);
                config.ClearSession();
                UpdateUI();
            }
        }

        private void BtnLinkGame_Click(object sender, EventArgs e)
        {
            using (var form = new LinkGameForm())
            {
                form.ShowDialog(this);
            }
        }
    }
}
