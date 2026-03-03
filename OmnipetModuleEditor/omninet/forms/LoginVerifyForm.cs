using System;
using System.Drawing;
using System.Windows.Forms;

namespace OmnipetModuleEditor.OmniNet
{
    /// <summary>
    /// Form for verifying login with email code and optional device clearing.
    /// </summary>
    public class LoginVerifyForm : Form
    {
        private readonly string _email;

        private TextBox txtCode;
        private CheckBox chkClearDevices;
        private Button btnVerify;
        private Button btnCancel;
        private Button btnResend;
        private Label lblInfo;
        private Label lblError;
        private Label lblTimer;
        private ProgressBar progressBar;
        private Timer countdownTimer;
        private int secondsRemaining;

        public LoginVerifyForm(string email)
        {
            _email = email;
            secondsRemaining = 5 * 60;

            InitializeComponent();
            StartCountdown();
        }

        private void InitializeComponent()
        {
            this.Text = "Verify Login";
            this.Size = new Size(380, 300);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 8,
                Padding = new Padding(20)
            };

            // Info label
            lblInfo = new Label
            {
                Text = $"A verification code has been sent to:\n{_email}\n\nEnter the 6-character code to complete login:",
                Dock = DockStyle.Fill,
                AutoSize = false,
                Height = 60,
                TextAlign = ContentAlignment.MiddleCenter
            };
            layout.Controls.Add(lblInfo, 0, 0);

            // Code input
            txtCode = new TextBox
            {
                Width = 150,
                Font = new Font("Consolas", 16, FontStyle.Bold),
                TextAlign = HorizontalAlignment.Center,
                MaxLength = 6,
                CharacterCasing = CharacterCasing.Upper
            };
            var codePanel = new Panel { Height = 40, Dock = DockStyle.Fill };
            txtCode.Location = new Point((codePanel.Width - txtCode.Width) / 2, 5);
            codePanel.Controls.Add(txtCode);
            codePanel.Resize += (s, e) => txtCode.Location = new Point((codePanel.Width - txtCode.Width) / 2, 5);
            layout.Controls.Add(codePanel, 0, 1);

            // Timer label
            lblTimer = new Label
            {
                Text = "Time remaining: 5:00",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.Gray
            };
            layout.Controls.Add(lblTimer, 0, 2);

            // Clear devices checkbox
            chkClearDevices = new CheckBox
            {
                Text = "Clear all other logged-in devices",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            };
            layout.Controls.Add(chkClearDevices, 0, 3);

            // Error label
            lblError = new Label
            {
                Text = "",
                Dock = DockStyle.Fill,
                ForeColor = Color.Red,
                TextAlign = ContentAlignment.MiddleCenter
            };
            layout.Controls.Add(lblError, 0, 4);

            // Progress bar
            progressBar = new ProgressBar
            {
                Dock = DockStyle.Fill,
                Style = ProgressBarStyle.Marquee,
                Visible = false,
                Height = 10
            };
            layout.Controls.Add(progressBar, 0, 5);

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

            btnVerify = new Button
            {
                Text = "Verify",
                Width = 80,
                Height = 30,
                Margin = new Padding(0, 0, 10, 0)
            };
            btnVerify.Click += BtnVerify_Click;
            buttonPanel.Controls.Add(btnVerify);

            btnResend = new Button
            {
                Text = "Resend Code",
                Width = 100,
                Height = 30,
                Margin = new Padding(0, 0, 10, 0)
            };
            btnResend.Click += BtnResend_Click;
            buttonPanel.Controls.Add(btnResend);

            layout.Controls.Add(buttonPanel, 0, 6);

            this.Controls.Add(layout);
            this.AcceptButton = btnVerify;
            this.CancelButton = btnCancel;
        }

        private void StartCountdown()
        {
            countdownTimer = new Timer { Interval = 1000 };
            countdownTimer.Tick += CountdownTimer_Tick;
            countdownTimer.Start();
        }

        private void CountdownTimer_Tick(object sender, EventArgs e)
        {
            secondsRemaining--;
            int minutes = secondsRemaining / 60;
            int seconds = secondsRemaining % 60;
            lblTimer.Text = $"Time remaining: {minutes}:{seconds:D2}";

            if (secondsRemaining <= 60)
                lblTimer.ForeColor = Color.OrangeRed;

            if (secondsRemaining <= 0)
            {
                countdownTimer.Stop();
                lblTimer.Text = "Code expired!";
                lblTimer.ForeColor = Color.Red;
                btnVerify.Enabled = false;
                lblError.Text = "The code has expired. Please request a new one.";
            }
        }

        private async void BtnVerify_Click(object sender, EventArgs e)
        {
            lblError.Text = "";

            if (string.IsNullOrWhiteSpace(txtCode.Text) || txtCode.Text.Length != 6)
            {
                lblError.Text = "Please enter the 6-character code.";
                return;
            }

            SetLoading(true);

            try
            {
                using (var client = new OmniNetApiClient())
                {
                    System.Diagnostics.Debug.WriteLine($"[LoginVerifyForm] Calling ConfirmLoginAsync with email={_email}, code={txtCode.Text}");
                    
                    var response = await client.ConfirmLoginAsync(_email, txtCode.Text, chkClearDevices.Checked);

                    System.Diagnostics.Debug.WriteLine($"[LoginVerifyForm] Response received - Success: {response.Success}");
                    System.Diagnostics.Debug.WriteLine($"[LoginVerifyForm] Response.Data is null: {response.Data == null}");
                    
                    if (response.Data != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[LoginVerifyForm] Response.Data.success: {response.Data.success}");
                        System.Diagnostics.Debug.WriteLine($"[LoginVerifyForm] Response.Data.secret_key exists: {!string.IsNullOrEmpty(response.Data.secret_key)}");
                        System.Diagnostics.Debug.WriteLine($"[LoginVerifyForm] Response.Data.nickname: {response.Data.nickname}");
                    }

                    // Check if we got a secret_key - that indicates success
                    // The API doesn't always include a "success" field in the response
                    if (response.Success && response.Data != null && !string.IsNullOrEmpty(response.Data.secret_key))
                    {
                        System.Diagnostics.Debug.WriteLine("[LoginVerifyForm] Login verification SUCCEEDED - secret_key received");
                        
                        // CRITICAL: Save session data IMMEDIATELY
                        var config = OmniNetConfig.Instance;
                        config.SecretKey = response.Data.secret_key;
                        config.DeviceId = response.Data.device_id;  // SAVE DEVICE ID!
                        config.UserEmail = _email;
                        config.Nickname = response.Data.nickname ?? _email.Split('@')[0];
                        
                        System.Diagnostics.Debug.WriteLine($"[LoginVerifyForm] Session data SET in memory - SecretKey: {config.SecretKey?.Substring(0, Math.Min(20, config.SecretKey.Length))}...");
                        System.Diagnostics.Debug.WriteLine($"[LoginVerifyForm] DeviceId: {config.DeviceId}");
                        System.Diagnostics.Debug.WriteLine($"[LoginVerifyForm] Nickname: {config.Nickname}");
                        System.Diagnostics.Debug.WriteLine($"[LoginVerifyForm] IsLoggedIn: {config.IsLoggedIn}");
                        
                        // Save to disk
                        System.Diagnostics.Debug.WriteLine("[LoginVerifyForm] Calling SaveSession()...");
                        config.SaveSession();
                        System.Diagnostics.Debug.WriteLine("[LoginVerifyForm] SaveSession() completed");

                        // CRITICAL: Set DialogResult BEFORE closing
                        System.Diagnostics.Debug.WriteLine("[LoginVerifyForm] Setting DialogResult to OK");
                        this.DialogResult = DialogResult.OK;
                        
                        // Stop timer before closing
                        System.Diagnostics.Debug.WriteLine("[LoginVerifyForm] Stopping countdown timer");
                        countdownTimer?.Stop();
                        
                        // Close this form - AccountForm will handle the success message
                        System.Diagnostics.Debug.WriteLine("[LoginVerifyForm] Closing form");
                        this.Close();
                        System.Diagnostics.Debug.WriteLine("[LoginVerifyForm] Form closed");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[LoginVerifyForm] Login verification FAILED");
                        System.Diagnostics.Debug.WriteLine($"[LoginVerifyForm] ErrorMessage: {response.ErrorMessage}");
                        lblError.Text = response.ErrorMessage ?? response.Data?.message ?? "Invalid code.";
                        SetLoading(false);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LoginVerifyForm] EXCEPTION: {ex.GetType().Name} - {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[LoginVerifyForm] Stack trace: {ex.StackTrace}");
                lblError.Text = $"Error: {ex.Message}";
                SetLoading(false);
            }
        }

        private async void BtnResend_Click(object sender, EventArgs e)
        {
            lblError.Text = "";
            SetLoading(true);

            try
            {
                using (var client = new OmniNetApiClient())
                {
                    var response = await client.ResendVerificationCodeAsync(_email);

                    if (response.Success && response.Data.success)
                    {
                        secondsRemaining = 5 * 60;
                        lblTimer.ForeColor = Color.Gray;
                        btnVerify.Enabled = true;
                        countdownTimer.Start();
                        MessageBox.Show("A new verification code has been sent.", "Code Sent",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        lblError.Text = response.ErrorMessage ?? "Failed to resend code.";
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
            btnVerify.Enabled = !loading && secondsRemaining > 0;
            btnResend.Enabled = !loading;
            txtCode.Enabled = !loading;
            chkClearDevices.Enabled = !loading;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            countdownTimer?.Stop();
            countdownTimer?.Dispose();
            base.OnFormClosing(e);
        }
    }
}
