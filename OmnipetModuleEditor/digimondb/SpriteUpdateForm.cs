using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OmnipetModuleEditor.DigimonSync
{
    /// <summary>
    /// Small modal window with a progress bar that drives a long-running sprite
    /// update task and reports per-name progress, then shows a summary.
    /// </summary>
    public class SpriteUpdateForm : Form
    {
        private readonly Func<IProgress<(int Done, int Total, string Status)>, Task<string>> _work;
        private ProgressBar bar;
        private Label lblStatus;
        private Button btnClose;

        public SpriteUpdateForm(Func<IProgress<(int Done, int Total, string Status)>, Task<string>> work)
        {
            _work = work;
            InitializeComponent();
            this.Shown += OnShown;
        }

        private void InitializeComponent()
        {
            this.Text = "Update Local Sprite Database";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ControlBox = false;
            this.ClientSize = new Size(420, 110);

            lblStatus = new Label
            {
                Text = "Starting...",
                AutoEllipsis = true,
                Location = new Point(12, 12),
                Size = new Size(396, 20),
            };

            bar = new ProgressBar
            {
                Location = new Point(12, 38),
                Size = new Size(396, 24),
                Style = ProgressBarStyle.Continuous,
                Minimum = 0,
                Maximum = 100,
                Value = 0,
            };

            btnClose = new Button
            {
                Text = "Close",
                DialogResult = DialogResult.OK,
                Size = new Size(90, 28),
                Location = new Point(318, 72),
                Enabled = false,
            };

            this.Controls.Add(lblStatus);
            this.Controls.Add(bar);
            this.Controls.Add(btnClose);
            this.AcceptButton = btnClose;
        }

        private async void OnShown(object sender, EventArgs e)
        {
            var progress = new Progress<(int Done, int Total, string Status)>(p =>
            {
                if (p.Total > 0)
                {
                    bar.Style = ProgressBarStyle.Continuous;
                    bar.Maximum = p.Total;
                    bar.Value = Math.Max(0, Math.Min(p.Done, p.Total));
                }
                if (!string.IsNullOrEmpty(p.Status))
                    lblStatus.Text = p.Status;
            });

            string summary;
            try
            {
                summary = await _work(progress);
            }
            catch (Exception ex)
            {
                summary = "Update failed: " + ex.Message;
            }

            if (bar.Maximum > 0)
                bar.Value = bar.Maximum;
            lblStatus.Text = summary;
            btnClose.Enabled = true;
            this.ControlBox = true;
        }
    }
}
