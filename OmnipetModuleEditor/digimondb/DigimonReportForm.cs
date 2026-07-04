using System;
using System.Drawing;
using System.Windows.Forms;

namespace OmnipetModuleEditor.DigimonSync
{
    /// <summary>
    /// A scrollable, monospaced report window. When an apply action is supplied
    /// it shows Apply / Cancel (for preview-then-commit operations like
    /// Normalize and Import Min Weight); otherwise it is a read-only report.
    /// DialogResult.OK indicates the apply action ran successfully.
    /// </summary>
    public class DigimonReportForm : Form
    {
        private readonly Func<string> _applyAction;
        private TextBox txtReport;

        public DigimonReportForm(string title, string reportText,
            string applyButtonText = null, Func<string> applyAction = null)
        {
            _applyAction = applyAction;

            this.Text = title;
            this.Size = new Size(820, 600);
            this.StartPosition = FormStartPosition.CenterParent;
            this.MinimizeBox = false;
            this.MaximizeBox = true;
            this.FormBorderStyle = FormBorderStyle.Sizable;

            txtReport = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 9.5f, FontStyle.Regular),
                Text = reportText,
                BackColor = Color.White,
            };

            var bottomPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                Height = 44,
                Padding = new Padding(4),
            };

            var btnClose = new Button
            {
                Text = applyAction != null ? "Cancel" : "Close",
                Width = 90,
                Height = 30,
                Margin = new Padding(4),
                DialogResult = DialogResult.Cancel,
            };

            var btnCopy = new Button
            {
                Text = "Copy to Clipboard",
                Width = 130,
                Height = 30,
                Margin = new Padding(4),
            };
            btnCopy.Click += (s, e) =>
            {
                if (!string.IsNullOrEmpty(txtReport.Text))
                {
                    try { Clipboard.SetText(txtReport.Text); } catch { }
                }
            };

            bottomPanel.Controls.Add(btnClose);
            bottomPanel.Controls.Add(btnCopy);

            if (applyAction != null)
            {
                var btnApply = new Button
                {
                    Text = applyButtonText ?? "Apply",
                    Width = 100,
                    Height = 30,
                    Margin = new Padding(4),
                };
                btnApply.Click += BtnApply_Click;
                bottomPanel.Controls.Add(btnApply);
            }

            this.Controls.Add(txtReport);
            this.Controls.Add(bottomPanel);
            this.CancelButton = btnClose;
        }

        private void BtnApply_Click(object sender, EventArgs e)
        {
            try
            {
                string result = _applyAction();
                MessageBox.Show(result ?? "Done.", "Digimon Database",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to apply changes:\n" + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
