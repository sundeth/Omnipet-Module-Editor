using System;
using System.Drawing;
using System.Windows.Forms;

namespace OmnipetModuleEditor.OmniNet
{
    /// <summary>
    /// Form for entering the 6-character verification code.
    /// </summary>
    public class VerifyCodeForm : Form
    {
        private readonly string _email;
        private readonly string _nickname;
        private readonly bool _isNewAccount;

        private TextBox txtCode;
        private Button btnVerify;
        private Button btnCancel;
        private Button btnResend;
        private Label lblInfo;
        private Label lblError;
        private Label lblTimer;
        private ProgressBar progressBar;
        private Timer countdownTimer;
        private int secondsRemaining;

        public VerifyCodeForm(string email, string nickname, bool isNewAccount)
        {
            _email = email;
            _nickname = nickname;
            _isNewAccount = isNewAccount;
            secondsRemaining = 5 * 60; // 5 minutes

            InitializeComponent();
            StartCountdown();
        }

        private void InitializeComponent()
        {
            this.Text = "Verify Email";
            this.Size = new Size(350, 250);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 7,
                Padding = new Padding(20)
            };

            // Info label
            lblInfo = new Label
            {
                Text = $"A verification code has been sent to:\n{_email}\n\nPlease enter the 6-character code below:",
                Dock = DockStyle.Fill,
                AutoSize = false,
                Height = 60,
                TextAlign = ContentAlignment.MiddleCenter
            };
            layout.Controls.Add(lblInfo, 0, 0);

            // Code input
            txtCode = new TextBox
            {
                Dock = DockStyle.None,
                Width = 150,
                Font = new Font("Consolas", 16, FontStyle.Bold),
                TextAlign = HorizontalAlignment.Center,
                MaxLength = 6,
                Anchor = AnchorStyles.None
            };
            txtCode.CharacterCasing = CharacterCasing.Upper;
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

            layout.Controls.Add(buttonPanel, 0, 5);

            this.Controls.Add(layout);
            this.AcceptButton = btnVerify;
            this.CancelButton = btnCancel;
        }

        private void StartCountdown()
        {
            countdownTimer = new Timer();
            countdownTimer.Interval = 1000;
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
                    var response = await client.VerifyEmailCodeAsync(_email, txtCode.Text);

                    // Check if we got a secret_key - that indicates success
                    // The API doesn't always include a "success" field in the response
                    if (response.Success && response.Data != null && !string.IsNullOrEmpty(response.Data.secret_key))
                    {
                        // CRITICAL: Save session data IMMEDIATELY
                        var config = OmniNetConfig.Instance;
                        config.SecretKey = response.Data.secret_key;
                        config.DeviceId = response.Data.device_id;  // SAVE DEVICE ID!
                        config.UserEmail = _email;
                        config.Nickname = response.Data.nickname ?? _nickname;

                        System.Diagnostics.Debug.WriteLine($"[VerifyCodeForm] Session saved - SecretKey: {config.SecretKey?.Substring(0, Math.Min(20, config.SecretKey.Length))}...");
                        System.Diagnostics.Debug.WriteLine($"[VerifyCodeForm] DeviceId: {config.DeviceId}");
                        System.Diagnostics.Debug.WriteLine($"[VerifyCodeForm] Nickname: {config.Nickname}");
                        System.Diagnostics.Debug.WriteLine($"[VerifyCodeForm] IsLoggedIn: {config.IsLoggedIn}");
                        
                        // Save to disk
                        config.SaveSession();

                        // CRITICAL: Set DialogResult BEFORE closing
                        this.DialogResult = DialogResult.OK;
                        
                        // Stop timer before closing
                        countdownTimer?.Stop();
                        
                        // Close this form - CreateAccountForm will handle the success message
                        this.Close();
                    }
                    else
                    {
                        lblError.Text = response.ErrorMessage ?? response.Data?.message ?? "Invalid code.";
                        SetLoading(false);
                    }
                }
            }
            catch (Exception ex)
            {
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
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            countdownTimer?.Stop();
            countdownTimer?.Dispose();
            base.OnFormClosing(e);
        }
    }
}
