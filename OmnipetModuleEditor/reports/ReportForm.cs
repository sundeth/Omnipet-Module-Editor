using System;
using System.Drawing;
using System.Windows.Forms;

namespace OmnipetModuleEditor.Reports
{
    public class ReportForm : Form
    {
        private TextBox txtReport;
        private Button btnCopy;
        private Button btnClose;

        public ReportForm(string reportText)
        {
            this.Text = "Module Report";
            this.Size = new Size(800, 600);
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
                BackColor = Color.White
            };

            var bottomPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                Height = 40,
                Padding = new Padding(4)
            };

            btnClose = new Button
            {
                Text = "Close",
                Width = 80,
                Height = 28,
                Margin = new Padding(4)
            };
            btnClose.Click += (s, e) => this.Close();

            btnCopy = new Button
            {
                Text = "Copy to Clipboard",
                Width = 130,
                Height = 28,
                Margin = new Padding(4)
            };
            btnCopy.Click += (s, e) =>
            {
                if (!string.IsNullOrEmpty(txtReport.Text))
                {
                    Clipboard.SetText(txtReport.Text);
                    MessageBox.Show("Report copied to clipboard.", "Copy", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };

            bottomPanel.Controls.Add(btnClose);
            bottomPanel.Controls.Add(btnCopy);

            this.Controls.Add(txtReport);
            this.Controls.Add(bottomPanel);
        }
    }
}
