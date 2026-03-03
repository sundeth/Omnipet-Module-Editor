using System;
using System.Drawing;
using System.Windows.Forms;

namespace OmnipetModuleEditor.OmniNet
{
    /// <summary>
    /// Form for logging in to an existing OmniNet account.
    /// </summary>
    public class LoginForm : Form
    {
        private TextBox txtEmail;
        private TextBox txtPassword;
        private Button btnLogin;
        private Button btnCancel;
        private Label lblError;
        private ProgressBar progressBar;

        public LoginForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "Login to OmniNet";
            this.Size = new Size(380, 220);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 5,
                Padding = new Padding(20)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            int row = 0;

            // Email
            layout.Controls.Add(new Label { Text = "Email:", Anchor = AnchorStyles.Right, AutoSize = true }, 0, row);
            txtEmail = new TextBox { Dock = DockStyle.Fill };
            layout.Controls.Add(txtEmail, 1, row++);

            // Password
            layout.Controls.Add(new Label { Text = "Password:", Anchor = AnchorStyles.Right, AutoSize = true }, 0, row);
            txtPassword = new TextBox { Dock = DockStyle.Fill, UseSystemPasswordChar = true };
            layout.Controls.Add(txtPassword, 1, row++);

            // Error label
            lblError = new Label
            {
                Text = "",
                Dock = DockStyle.Fill,
                ForeColor = Color.Red,
                TextAlign = ContentAlignment.MiddleCenter
            };
            layout.Controls.Add(lblError, 0, row);
            layout.SetColumnSpan(lblError, 2);
            row++;

            // Progress bar
            progressBar = new ProgressBar
            {
                Dock = DockStyle.Fill,
                Style = ProgressBarStyle.Marquee,
                Visible = false,
                Height = 10
            };
            layout.Controls.Add(progressBar, 0, row);
            layout.SetColumnSpan(progressBar, 2);
            row++;

            // Buttons
            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft
            };

            btnCancel = new Button
            {
                Text = "Cancel",
                Width = 80,
                Height = 30,
                DialogResult = DialogResult.Cancel
            };
            buttonPanel.Controls.Add(btnCancel);

            btnLogin = new Button
            {
                Text = "Login",
                Width = 80,
                Height = 30,
                Margin = new Padding(0, 0, 10, 0)
            };
            btnLogin.Click += BtnLogin_Click;
            buttonPanel.Controls.Add(btnLogin);

            layout.Controls.Add(buttonPanel, 0, row);
            layout.SetColumnSpan(buttonPanel, 2);

            this.Controls.Add(layout);
            this.AcceptButton = btnLogin;
            this.CancelButton = btnCancel;
        }

        private async void BtnLogin_Click(object sender, EventArgs e)
        {
            lblError.Text = "";

            if (string.IsNullOrWhiteSpace(txtEmail.Text) || !txtEmail.Text.Contains("@"))
            {
                lblError.Text = "Please enter a valid email address.";
                return;
            }

            if (string.IsNullOrWhiteSpace(txtPassword.Text))
            {
                lblError.Text = "Please enter your password.";
                return;
            }

            SetLoading(true);

            try
            {
                using (var client = new OmniNetApiClient())
                {
                    System.Diagnostics.Debug.WriteLine($"[LoginForm] Calling LoginAsync with email={txtEmail.Text}");
                    
                    var response = await client.LoginAsync(txtEmail.Text, txtPassword.Text);

                    System.Diagnostics.Debug.WriteLine($"[LoginForm] Response received - Success: {response.Success}");

                    if (response.Success && response.Data != null && response.Data.success)
                    {
                        System.Diagnostics.Debug.WriteLine("[LoginForm] Login successful, opening verification form");
                        
                        // Show verification form as a modal dialog
                        using (var verifyForm = new LoginVerifyForm(txtEmail.Text))
                        {
                            System.Diagnostics.Debug.WriteLine("[LoginForm] Calling verifyForm.ShowDialog()");
                            var result = verifyForm.ShowDialog(this);
                            System.Diagnostics.Debug.WriteLine($"[LoginForm] verifyForm.ShowDialog() returned: {result}");
                            
                            if (result == DialogResult.OK)
                            {
                                // Login successful - close this form with OK result
                                System.Diagnostics.Debug.WriteLine("[LoginForm] Verification succeeded, setting DialogResult to OK and closing");
                                this.DialogResult = DialogResult.OK;
                                this.Close();
                                System.Diagnostics.Debug.WriteLine("[LoginForm] LoginForm closed");
                            }
                            else
                            {
                                // Verification cancelled or failed - re-enable login form
                                System.Diagnostics.Debug.WriteLine($"[LoginForm] Verification cancelled or failed (result={result}), re-enabling form");
                                SetLoading(false);
                            }
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[LoginForm] Login failed - ErrorMessage: {response.ErrorMessage}");
                        lblError.Text = response.ErrorMessage ?? response.Data?.message ?? "Login failed.";
                        SetLoading(false);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LoginForm] EXCEPTION: {ex.GetType().Name} - {ex.Message}");
                lblError.Text = $"Error: {ex.Message}";
                SetLoading(false);
            }
        }

        private void SetLoading(bool loading)
        {
            progressBar.Visible = loading;
            btnLogin.Enabled = !loading;
            txtEmail.Enabled = !loading;
            txtPassword.Enabled = !loading;
        }
    }
}
