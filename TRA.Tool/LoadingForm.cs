using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using TRA_Lib;

namespace TRA.Tool
{
    public partial class LoadingForm : Form
    {
        // Haupt-Container: TableLayoutPanel (eine Spalte, dynamische Zeilen)
        readonly TableLayoutPanel table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            ColumnCount = 1,
            RowCount = 0,
            Padding = new Padding(8),
            AutoScroll = true
        };

        public LoadingForm()
        {
            InitializeComponent();
            Text = "Calculating...";
            Width = 700;
            Height = 380;
            MinimumSize = new Size(420, 260);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;

            // Tabelle konfigurieren
            table.ColumnStyles.Clear();
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            Controls.Add(table);
        }

        /// <summary>
        /// Adds a labeled progress bar for a trasse and returns an IProgress&lt;ProgressReport&gt; that updates it.
        /// Safe to call from any thread; updates are marshalled to the form thread.
        /// </summary>
        public IProgress<ProgressReport> AddProgressBar(string labelText, int total)
        {
            if (InvokeRequired)
            {
                IProgress<ProgressReport> result = null;
                Invoke(new Action(() => result = AddProgressBar(labelText, total)));
                return result;
            }

            // Suspend layout einmalig beim Hinzufügen von Zeilen
            table.SuspendLayout();

            // kleine Zeile als TableLayout: Label oben, ProgressBar unten
            var row = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 2,
                Dock = DockStyle.Top,
                Margin = new Padding(4)
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            row.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            row.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var lbl = new Label
            {
                Text = $"{labelText}: 0 of {total}",
                AutoSize = true,
                Dock = DockStyle.Fill,
                Margin = new Padding(2, 6, 2, 2),
                TextAlign = ContentAlignment.MiddleLeft
            };

            var pb = new ProgressBar
            {
                Minimum = 0,
                Maximum = total > 0 ? total : 1,
                Height = 18,
                Dock = DockStyle.Fill,
                Margin = new Padding(2, 2, 2, 8),
                Style = ProgressBarStyle.Continuous
            };

            row.Controls.Add(lbl, 0, 0);
            row.Controls.Add(pb, 0, 1);

            // Zeile zur Haupttabelle hinzufügen (neue Zeile)
            int newRow = table.RowCount;
            table.RowCount = newRow + 1;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.Controls.Add(row, 0, newRow);

            // ResumeLayout einmalig
            table.ResumeLayout(true);

            // Rückgabe eines Progress, der auf diesem UI-Thread läuft
            return new Progress<ProgressReport>(report =>
            {
                if (lbl.IsDisposed || pb.IsDisposed) return;

                if (report.Total > 0)
                {
                    int cur = Math.Min(Math.Max(0, report.Current), report.Total);
                    try { pb.Value = Math.Min(cur, pb.Maximum); } catch { }
                    lbl.Text = $"{labelText}: {report.Current} of {report.Total}";
                }
                else
                {
                    lbl.Text = $"{labelText}: {report.Current}";
                }
            });
        }
    }
}