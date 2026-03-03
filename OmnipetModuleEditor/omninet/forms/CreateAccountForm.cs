using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OmnipetModuleEditor.OmniNet
{
    /// <summary>
    /// Form for creating a new OmniNet account.
    /// </summary>
    public class CreateAccountForm : Form
    {
        private TextBox txtNickname;
        private TextBox txtEmail;
        private TextBox txtPassword;
        private TextBox txtConfirmPassword;
        private Button btnCreate;
        private Button btnCancel;
        private Label lblError;
        private ProgressBar progressBar;

        public CreateAccountForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "Create OmniNet Account";
            this.Size = new Size(400, 320);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 7,
                Padding = new Padding(20)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            int row = 0;

            // Nickname
            layout.Controls.Add(new Label { Text = "Nickname:", Anchor = AnchorStyles.Right, AutoSize = true }, 0, row);
            txtNickname = new TextBox { Dock = DockStyle.Fill };
            layout.Controls.Add(txtNickname, 1, row++);

            // Email
            layout.Controls.Add(new Label { Text = "Email:", Anchor = AnchorStyles.Right, AutoSize = true }, 0, row);
            txtEmail = new TextBox { Dock = DockStyle.Fill };
            layout.Controls.Add(txtEmail, 1, row++);

            // Password
            layout.Controls.Add(new Label { Text = "Password:", Anchor = AnchorStyles.Right, AutoSize = true }, 0, row);
            txtPassword = new TextBox { Dock = DockStyle.Fill, UseSystemPasswordChar = true };
            layout.Controls.Add(txtPassword, 1, row++);

            // Confirm Password
            layout.Controls.Add(new Label { Text = "Confirm Password:", Anchor = AnchorStyles.Right, AutoSize = true }, 0, row);
            txtConfirmPassword = new TextBox { Dock = DockStyle.Fill, UseSystemPasswordChar = true };
            layout.Controls.Add(txtConfirmPassword, 1, row++);

            // Error label
            lblError = new Label
            {
                Text = "",
                Dock = DockStyle.Fill,
                ForeColor = Color.Red,
                AutoSize = false,
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

            btnCreate = new Button
            {
                Text = "Create Account",
                Width = 110,
                Height = 30,
                Margin = new Padding(0, 0, 10, 0)
            };
            btnCreate.Click += BtnCreate_Click;
            buttonPanel.Controls.Add(btnCreate);

            layout.Controls.Add(buttonPanel, 0, row);
            layout.SetColumnSpan(buttonPanel, 2);

            this.Controls.Add(layout);
            this.AcceptButton = btnCreate;
            this.CancelButton = btnCancel;
        }

        private async void BtnCreate_Click(object sender, EventArgs e)
        {
            lblError.Text = "";

            // Validation
            if (string.IsNullOrWhiteSpace(txtNickname.Text))
            {
                lblError.Text = "Please enter a nickname.";
                return;
            }

            if (string.IsNullOrWhiteSpace(txtEmail.Text) || !txtEmail.Text.Contains("@"))
            {
                lblError.Text = "Please enter a valid email address.";
                return;
            }

            if (string.IsNullOrWhiteSpace(txtPassword.Text) || txtPassword.Text.Length < 6)
            {
                lblError.Text = "Password must be at least 6 characters.";
                return;
            }

            if (txtPassword.Text != txtConfirmPassword.Text)
            {
                lblError.Text = "Passwords do not match.";
                return;
            }

            // Confirm
            var confirmResult = MessageBox.Show(
                $"Create account with email: {txtEmail.Text}?\n\nA verification code will be sent to this email.",
                "Confirm Account Creation",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirmResult != DialogResult.Yes)
                return;

            // Create account
            SetLoading(true);

            try
            {
                using (var client = new OmniNetApiClient())
                {
                    System.Diagnostics.Debug.WriteLine($"[CreateAccount] Attempting to create account for {txtEmail.Text}");

                    var response = await client.CreateAccountAsync(txtNickname.Text, txtEmail.Text, txtPassword.Text);

                    System.Diagnostics.Debug.WriteLine($"[CreateAccount] Response received - Success: {response.Success}");

                    if (response.Success && response.Data != null && response.Data.success)
                    {
                        System.Diagnostics.Debug.WriteLine("[CreateAccount] Account created successfully, showing verification form");

                        // Show verification form
                        using (var verifyForm = new VerifyCodeForm(txtEmail.Text, txtNickname.Text, isNewAccount: true))
                        {
                            if (verifyForm.ShowDialog(this) == DialogResult.OK)
                            {
                                this.DialogResult = DialogResult.OK;
                                this.Close();
                            }
                        }
                    }
                    else
                    {
                        string error = response.ErrorMessage ?? response.Data?.message ?? "Failed to create account.";
                        System.Diagnostics.Debug.WriteLine($"[CreateAccount] Error: {error}");

                        // Normalize error for comparison (handle both "Email is already registered" and "email already exists" variations)
                        string errorLower = error.ToLower();
                        
                        // Check if email already exists (registered but not verified)
                        if ((errorLower.Contains("email") && (errorLower.Contains("already") || errorLower.Contains("exist") || errorLower.Contains("registered"))) ||
                            errorLower.Contains("email is already registered"))
                        {
                            System.Diagnostics.Debug.WriteLine($"[CreateAccount] Detected email already registered error");
                            
                            var resendResult = MessageBox.Show(
                                "This email is already registered.\n\n" +
                                "If you haven't verified your account yet, would you like to resend the verification code?\n\n" +
                                "Otherwise, please use the Login option.",
                                "Email Already Registered",
                                MessageBoxButtons.YesNo,
                                MessageBoxIcon.Question);

                            if (resendResult == DialogResult.Yes)
                            {
                                System.Diagnostics.Debug.WriteLine($"[CreateAccount] Resending verification code to {txtEmail.Text}");
                                
                                var resendResponse = await client.ResendVerificationCodeAsync(txtEmail.Text);
                                
                                System.Diagnostics.Debug.WriteLine($"[CreateAccount] Resend response - Success: {resendResponse.Success}");
                                
                                if (resendResponse.Success && resendResponse.Data != null && resendResponse.Data.success)
                                {
                                    System.Diagnostics.Debug.WriteLine("[CreateAccount] Verification code resent successfully");
                                    
                                    MessageBox.Show(
                                        "Verification code has been resent to your email.",
                                        "Code Sent",
                                        MessageBoxButtons.OK,
                                        MessageBoxIcon.Information);
                                    
                                    using (var verifyForm = new VerifyCodeForm(txtEmail.Text, txtNickname.Text, isNewAccount: true))
                                    {
                                        if (verifyForm.ShowDialog(this) == DialogResult.OK)
                                        {
                                            this.DialogResult = DialogResult.OK;
                                            this.Close();
                                        }
                                    }
                                }
                                else
                                {
                                    string resendError = resendResponse.ErrorMessage ?? resendResponse.Data?.message ?? "Failed to resend verification code.";
                                    System.Diagnostics.Debug.WriteLine($"[CreateAccount] Resend error: {resendError}");
                                    lblError.Text = resendError;
                                }
                            }
                        }
                        // Check if nickname already exists
                        else if (errorLower.Contains("nickname") && (errorLower.Contains("already") || errorLower.Contains("taken")))
                        {
                            System.Diagnostics.Debug.WriteLine($"[CreateAccount] Detected nickname already taken error");
                            lblError.Text = "This nickname is already taken. Please choose a different one.";
                            txtNickname.Focus();
                            txtNickname.SelectAll();
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"[CreateAccount] Unhandled error type, showing raw message");
                            lblError.Text = error;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CreateAccount] Exception: {ex.GetType().Name} - {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[CreateAccount] Stack trace: {ex.StackTrace}");
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
            btnCreate.Enabled = !loading;
            txtNickname.Enabled = !loading;
            txtEmail.Enabled = !loading;
            txtPassword.Enabled = !loading;
            txtConfirmPassword.Enabled = !loading;
        }
    }
}
