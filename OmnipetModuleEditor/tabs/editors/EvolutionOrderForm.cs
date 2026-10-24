using OmnipetModuleEditor.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace OmnipetModuleEditor
{
    /// <summary>
    /// Window for rearranging the order of a pet's evolutions.
    ///
    /// Each row shows the evolution requirements box (rendered exactly like
    /// the Evolution Editor canvas, including special styling) and, to the
    /// right, the evolution target (sprite + name), with ▲/▼ buttons to move
    /// the record.  Standard and temporary evolutions are two separate
    /// sections, each independently reorderable.
    /// </summary>
    public class EvolutionOrderForm : Form
    {
        /// <summary>One evolution record to be ordered.</summary>
        public class Entry
        {
            /// <summary>Evolution or TempEvolution.</summary>
            public object Criteria;
            public Pet Target;
            /// <summary>Opaque caller key (e.g. the editor's connection object).</summary>
            public object Key;
        }

        /// <summary>Reordered standard evolutions (set when OK is pressed).</summary>
        public List<Entry> NormalOrder { get; private set; }
        /// <summary>Reordered temporary evolutions (set when OK is pressed).</summary>
        public List<Entry> TempOrder { get; private set; }

        private readonly Func<Pet, Image> spriteLoader;
        private FlowLayoutPanel flowNormal;
        private FlowLayoutPanel flowTemp;

        public EvolutionOrderForm(string petName, List<Entry> normal, List<Entry> temp,
                                  Func<Pet, Image> spriteLoader)
        {
            this.spriteLoader = spriteLoader;
            InitializeComponent(petName, normal ?? new List<Entry>(), temp ?? new List<Entry>());
        }

        private void InitializeComponent(string petName, List<Entry> normal, List<Entry> temp)
        {
            this.Text = $"Evolution Order — {petName}";
            this.Size = new Size(680, 640);
            this.MinimizeBox = false;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;

            var scroll = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(10)
            };

            var stack = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                Dock = DockStyle.Top
            };

            stack.Controls.Add(MakeSectionLabel("Evolutions"));
            flowNormal = MakeSectionFlow();
            foreach (var entry in normal)
                flowNormal.Controls.Add(CreateRow(entry, flowNormal));
            if (normal.Count == 0)
                flowNormal.Controls.Add(MakeEmptyLabel());
            stack.Controls.Add(flowNormal);

            stack.Controls.Add(MakeSectionLabel("Temporary Evolutions"));
            flowTemp = MakeSectionFlow();
            foreach (var entry in temp)
                flowTemp.Controls.Add(CreateRow(entry, flowTemp));
            if (temp.Count == 0)
                flowTemp.Controls.Add(MakeEmptyLabel());
            stack.Controls.Add(flowTemp);

            scroll.Controls.Add(stack);

            var btnOk = new Button { Text = "OK", Width = 90, DialogResult = DialogResult.OK };
            var btnCancel = new Button { Text = "Cancel", Width = 90, DialogResult = DialogResult.Cancel };
            btnOk.Click += (s, e) =>
            {
                NormalOrder = CollectOrder(flowNormal);
                TempOrder = CollectOrder(flowTemp);
            };

            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                Height = 44,
                Padding = new Padding(8)
            };
            buttonPanel.Controls.Add(btnCancel);
            buttonPanel.Controls.Add(btnOk);

            this.Controls.Add(scroll);
            this.Controls.Add(buttonPanel);
            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;
        }

        private static Label MakeSectionLabel(string text) => new Label
        {
            Text = text,
            AutoSize = true,
            Font = new Font(FontFamily.GenericSansSerif, 10, FontStyle.Bold),
            Margin = new Padding(0, 8, 0, 4)
        };

        private static Label MakeEmptyLabel() => new Label
        {
            Text = "(none)",
            AutoSize = true,
            ForeColor = Color.Gray,
            Margin = new Padding(8, 4, 0, 4)
        };

        private static FlowLayoutPanel MakeSectionFlow() => new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            Margin = new Padding(0)
        };

        /// <summary>Build one reorderable row: [criteria box] → [sprite][name]   ▲ ▼</summary>
        private Panel CreateRow(Entry entry, FlowLayoutPanel owner)
        {
            var row = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.WhiteSmoke,
                Padding = new Padding(4),
                Margin = new Padding(0, 2, 0, 2),
                Tag = entry
            };

            // Requirements box — identical rendering to the editor canvas.
            Panel criteria = entry.Criteria is TempEvolution tempEvo
                ? EvolutionBoxRenderer.CreatePanel(tempEvo)
                : EvolutionBoxRenderer.CreatePanel(entry.Criteria as Evolution);
            criteria.Cursor = Cursors.Default;
            criteria.Margin = new Padding(2, 2, 6, 2);
            row.Controls.Add(criteria);

            row.Controls.Add(new Label
            {
                Text = "→",
                AutoSize = true,
                Font = new Font(FontFamily.GenericSansSerif, 12, FontStyle.Bold),
                Margin = new Padding(0, 14, 6, 0)
            });

            var pb = new PictureBox
            {
                Size = new Size(44, 44),
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 4, 4, 0)
            };
            try { pb.Image = spriteLoader?.Invoke(entry.Target); } catch { }
            row.Controls.Add(pb);

            row.Controls.Add(new Label
            {
                Text = entry.Target?.Name ?? "?",
                AutoSize = true,
                Font = new Font(FontFamily.GenericSansSerif, 10, FontStyle.Bold),
                ForeColor = Color.DeepSkyBlue,
                Margin = new Padding(0, 16, 12, 0)
            });

            var btnUp = new Button { Text = "▲", Width = 32, Height = 26, Margin = new Padding(0, 8, 2, 0) };
            var btnDown = new Button { Text = "▼", Width = 32, Height = 26, Margin = new Padding(0, 8, 0, 0) };
            btnUp.Click += (s, e) => MoveRow(owner, row, -1);
            btnDown.Click += (s, e) => MoveRow(owner, row, +1);
            row.Controls.Add(btnUp);
            row.Controls.Add(btnDown);

            return row;
        }

        private static void MoveRow(FlowLayoutPanel owner, Panel row, int delta)
        {
            int idx = owner.Controls.IndexOf(row);
            int newIdx = idx + delta;
            if (newIdx < 0 || newIdx >= owner.Controls.Count)
                return;
            owner.Controls.SetChildIndex(row, newIdx);
        }

        private static List<Entry> CollectOrder(FlowLayoutPanel flow)
            => flow.Controls.OfType<Control>()
                   .Where(c => c.Tag is Entry)
                   .Select(c => (Entry)c.Tag)
                   .ToList();
    }
}
