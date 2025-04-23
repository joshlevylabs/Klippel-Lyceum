using System;
using System.Windows.Forms;
using System.Drawing;
using System.Collections.Generic;
using static LyceumKlippel.LYKHome;

namespace LyceumKlippel
{
    public class ChartPreferencesForm : Form
    {
        private TextBox txtXAxisMin, txtXAxisMax, txtYAxisMin, txtYAxisMax;
        private ComboBox cmbXAxisScaling, cmbYAxisScaling;
        private CheckBox chkShowMajorTicksX, chkShowMinorTicksX, chkShowMajorTicksY, chkShowMinorTicksY;
        private Button btnOK, btnCancel;
        private ChartPreferences preferences;

        public ChartPreferencesForm(ChartPreferences currentPreferences)
        {
            this.preferences = new ChartPreferences
            {
                XMin = currentPreferences.XMin,
                XMax = currentPreferences.XMax,
                XScaling = currentPreferences.XScaling,
                YMin = currentPreferences.YMin,
                YMax = currentPreferences.YMax,
                YScaling = currentPreferences.YScaling
            };
            InitializeComponents();
        }

        public ChartPreferences GetPreferences()
        {
            preferences.XMin = ParseDouble(txtXAxisMin.Text);
            preferences.XMax = ParseDouble(txtXAxisMax.Text);
            preferences.XScaling = cmbXAxisScaling.SelectedItem?.ToString() ?? "Logarithmic";
            preferences.YMin = ParseDouble(txtYAxisMin.Text);
            preferences.YMax = ParseDouble(txtYAxisMax.Text);
            preferences.YScaling = cmbYAxisScaling.SelectedItem?.ToString() ?? "Linear";
            return preferences;
        }

        private void InitializeComponents()
        {
            this.Text = "Chart Preferences";
            this.Size = new Size(300, 400);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            Label lblXAxisMin = new Label { Text = "X-Axis Minimum:", Location = new Point(10, 10), AutoSize = true };
            txtXAxisMin = new TextBox { Location = new Point(120, 10), Width = 150, Text = preferences.XMin?.ToString() ?? "" };
            this.Controls.Add(lblXAxisMin);
            this.Controls.Add(txtXAxisMin);

            Label lblXAxisMax = new Label { Text = "X-Axis Maximum:", Location = new Point(10, 40), AutoSize = true };
            txtXAxisMax = new TextBox { Location = new Point(120, 40), Width = 150, Text = preferences.XMax?.ToString() ?? "" };
            this.Controls.Add(lblXAxisMax);
            this.Controls.Add(txtXAxisMax);

            Label lblXAxisScaling = new Label { Text = "X-Axis Scaling:", Location = new Point(10, 70), AutoSize = true };
            cmbXAxisScaling = new ComboBox { Location = new Point(120, 70), Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbXAxisScaling.Items.AddRange(new string[] { "Linear", "Logarithmic" });
            cmbXAxisScaling.SelectedItem = preferences.XScaling;
            this.Controls.Add(lblXAxisScaling);
            this.Controls.Add(cmbXAxisScaling);

            Label lblYAxisMin = new Label { Text = "Y-Axis Minimum:", Location = new Point(10, 110), AutoSize = true };
            txtYAxisMin = new TextBox { Location = new Point(120, 110), Width = 150, Text = preferences.YMin?.ToString() ?? "" };
            this.Controls.Add(lblYAxisMin);
            this.Controls.Add(txtYAxisMin);

            Label lblYAxisMax = new Label { Text = "Y-Axis Maximum:", Location = new Point(10, 140), AutoSize = true };
            txtYAxisMax = new TextBox { Location = new Point(120, 140), Width = 150, Text = preferences.YMax?.ToString() ?? "" };
            this.Controls.Add(lblYAxisMax);
            this.Controls.Add(txtYAxisMax);

            Label lblYAxisScaling = new Label { Text = "Y-Axis Scaling:", Location = new Point(10, 170), AutoSize = true };
            cmbYAxisScaling = new ComboBox { Location = new Point(120, 170), Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbYAxisScaling.Items.AddRange(new string[] { "Linear", "Logarithmic" });
            cmbYAxisScaling.SelectedItem = preferences.YScaling;
            this.Controls.Add(lblYAxisScaling);
            this.Controls.Add(cmbYAxisScaling);

            Label lblTickOptions = new Label { Text = "Tick Marks:", Location = new Point(10, 200), AutoSize = true };
            this.Controls.Add(lblTickOptions);

            chkShowMajorTicksX = new CheckBox { Text = "Major Ticks X", Location = new Point(120, 200), Checked = preferences.ShowMajorTicksX };
            this.Controls.Add(chkShowMajorTicksX);

            chkShowMinorTicksX = new CheckBox { Text = "Minor Ticks X", Location = new Point(120, 230), Checked = preferences.ShowMinorTicksX };
            this.Controls.Add(chkShowMinorTicksX);

            chkShowMajorTicksY = new CheckBox { Text = "Major Ticks Y", Location = new Point(120, 260), Checked = preferences.ShowMajorTicksY };
            this.Controls.Add(chkShowMajorTicksY);

            chkShowMinorTicksY = new CheckBox { Text = "Minor Ticks Y", Location = new Point(120, 290), Checked = preferences.ShowMinorTicksY };
            this.Controls.Add(chkShowMinorTicksY);

            btnOK = new Button { Text = "OK", Location = new Point(120, 330), Width = 70 };
            btnOK.Click += (s, e) => { this.DialogResult = DialogResult.OK; this.Close(); };
            this.Controls.Add(btnOK);

            btnCancel = new Button { Text = "Cancel", Location = new Point(200, 330), Width = 70 };
            btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };
            this.Controls.Add(btnCancel);

            this.AcceptButton = btnOK;
            this.CancelButton = btnCancel;
        }

        private double? ParseDouble(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            if (double.TryParse(text, out double value)) return value;
            return null;
        }
    }

    public class ProjectSession
    {
        public string Title { get; set; }
        public string Data { get; set; }
        public List<LYKHome.SignalPathData> SignalPaths { get; set; }
        public Dictionary<string, string> GlobalProperties { get; set; }
    }

    public class ChartPreferences
    {
        public double? XMin { get; set; }
        public double? XMax { get; set; }
        public string XScaling { get; set; } = "Logarithmic";
        public double? YMin { get; set; }
        public double? YMax { get; set; }
        public string YScaling { get; set; } = "Linear";
        public bool ShowMajorTicksX { get; set; } = true;
        public bool ShowMinorTicksX { get; set; } = true;
        public bool ShowMajorTicksY { get; set; } = true;
        public bool ShowMinorTicksY { get; set; } = true;
    }
}