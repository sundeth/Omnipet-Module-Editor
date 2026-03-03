using System;
using System.Drawing;
using System.Windows.Forms;

namespace OmnipetModuleEditor.OmniNet
{
    /// <summary>
    /// Form for generating and displaying a game link code.
    /// </summary>
    public class LinkGameForm : Form
    {
        private Label lblInfo;
        private Label lblCode;
        private Label lblTimer;
        private Button btnGenerate;
        private Button btnClose;
        private ProgressBar progressBar;
        private Timer countdownTimer;
        private int secondsRemaining;

        public LinkGameForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "Link Game Device";
            this.Size = new Size(350, 280);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 6,
                Padding = new Padding(20)
            };

            // Info label
            lblInfo = new Label
            {
                Text = "Generate a code to link your game device.\n\nIn-game, go to Connect > Account > Link Account\nand enter the code shown below.",
                Dock = DockStyle.Fill,
                AutoSize = false,
                Height = 80,
                TextAlign = ContentAlignment.MiddleCenter
            };
            layout.Controls.Add(lblInfo, 0, 0);

            // Code display
            lblCode = new Label
            {
                Text = "----",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Consolas", 32, FontStyle.Bold),
                ForeColor = Color.DarkBlue,
                Height = 50
            };
            layout.Controls.Add(lblCode, 0, 1);

            // Timer label
            lblTimer = new Label
            {
                Text = "",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.Gray
            };
            layout.Controls.Add(lblTimer, 0, 2);

            // Progress bar
            progressBar = new ProgressBar
            {
                Dock = DockStyle.Fill,
                Style = ProgressBarStyle.Marquee,
                Visible = false,
                Height = 10
            };
            layout.Controls.Add(progressBar, 0, 3);

            // Buttons
            var buttonPanel = new FlowLayoutPanel
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
            buttonPanel.Controls.Add(btnClose);

            btnGenerate = new Button
            {
                Text = "Generate Code",
                Width = 110,
                Height = 30,
                Margin = new Padding(0, 0, 10, 0)
            };
            btnGenerate.Click += BtnGenerate_Click;
            buttonPanel.Controls.Add(btnGenerate);

            layout.Controls.Add(buttonPanel, 0, 4);

            this.Controls.Add(layout);
            this.CancelButton = btnClose;

            // Auto-generate on load
            this.Load += (s, e) => BtnGenerate_Click(s, e);
        }

        private async void BtnGenerate_Click(object sender, EventArgs e)
        {
            SetLoading(true);
            lblCode.Text = "----";
            lblTimer.Text = "";

            try
            {
                var config = OmniNetConfig.Instance;
                using (var client = new OmniNetApiClient())
                {
                    var response = await client.GenerateGameLinkCodeAsync(config.SecretKey);

                    // Check if response was successful and we got a code
                    if (response.Success && response.Data != null && !string.IsNullOrEmpty(response.Data.code))
                    {
                        lblCode.Text = response.Data.code;
                        secondsRemaining = response.Data.expires_in_seconds;  // Use seconds directly
                        StartCountdown();
                    }
                    else
                    {
                        lblCode.Text = "ERROR";
                        lblTimer.Text = response.ErrorMessage ?? "Failed to generate code.";
                        lblTimer.ForeColor = Color.Red;
                    }
                }
            }
            catch (Exception ex)
            {
                lblCode.Text = "ERROR";
                lblTimer.Text = ex.Message;
                lblTimer.ForeColor = Color.Red;
            }
            finally
            {
                SetLoading(false);
            }
        }

        private void StartCountdown()
        {
            if (countdownTimer != null)
            {
                countdownTimer.Stop();
                countdownTimer.Dispose();
            }

            countdownTimer = new Timer { Interval = 1000 };
            countdownTimer.Tick += CountdownTimer_Tick;
            countdownTimer.Start();
            UpdateTimerLabel();
        }

        private void CountdownTimer_Tick(object sender, EventArgs e)
        {
            secondsRemaining--;
            UpdateTimerLabel();

            if (secondsRemaining <= 0)
            {
                countdownTimer.Stop();
                lblTimer.Text = "Code expired! Generate a new one.";
                lblTimer.ForeColor = Color.Red;
                lblCode.ForeColor = Color.Gray;
            }
        }

        private void UpdateTimerLabel()
        {
            int minutes = secondsRemaining / 60;
            int seconds = secondsRemaining % 60;
            lblTimer.Text = $"Code expires in: {minutes}:{seconds:D2}";
            lblTimer.ForeColor = secondsRemaining <= 60 ? Color.OrangeRed : Color.Gray;
        }

        private void SetLoading(bool loading)
        {
            progressBar.Visible = loading;
            btnGenerate.Enabled = !loading;
            lblCode.ForeColor = Color.DarkBlue;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            countdownTimer?.Stop();
            countdownTimer?.Dispose();
            base.OnFormClosing(e);
        }
    }
}
