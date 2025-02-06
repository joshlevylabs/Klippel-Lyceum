using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace LAPxv8
{
    public partial class FormAboutLAPx : BaseForm
    {
        private TabControl tabControl;

        public FormAboutLAPx() : base(false) // Disable BaseForm's menu strip
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            // Completely remove the title bar
            this.Text = string.Empty;
            this.FormBorderStyle = FormBorderStyle.None; // No title bar or borders
            this.Size = new Size(700, 500);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(30, 30, 30); // Dark mode
            this.Padding = new Padding(10); // Reduce awkward borders
            
            // Ensure BaseForm's menu strip is hidden
            if (menuStrip != null)
            {
                menuStrip.Visible = false;
            }

            
            // Tab Control (now at the very top)
            tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10),
                BackColor = Color.FromArgb(40, 40, 40),
                Margin = new Padding(0, 10, 0, 0) // Space below the spacer

            };
            this.Controls.Add(tabControl);

            // Populate tabs
            AddTab("Getting Started", "Welcome to LAPx!\n\nLAPx is a powerful tool for managing Audio Precision testing...");
            AddTab("Running Tests", "To run a test, connect your AP hardware and click 'Run Script'.");
            AddTab("Analyzing Data", "Navigate to 'Test Results Grid' under File to review measurement data.");
            AddTab("Uploading to Lyceum", "Select 'Upload to Lyceum' to save test results online.");
            AddTab("Viewing Logs", "Open 'Log Window' from File to track program activity.");
            AddTab("Troubleshooting", "Common issues include connection failures and missing configurations.");
            AddTab("Support", "For additional help, contact josh@thelyceum.io");

            // Close Button
            Button closeButton = new Button
            {
                Text = "Close",
                Dock = DockStyle.Bottom,
                Height = 40,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(50, 50, 50),
                FlatStyle = FlatStyle.Flat
            };
            closeButton.Click += (sender, e) => this.Close();
            this.Controls.Add(closeButton);
        }

        private void AddTab(string title, string content)
        {
            TabPage tabPage = new TabPage(title)
            {
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White
            };

            RichTextBox contentBox = new RichTextBox
            {
                ReadOnly = true,
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10),
                BorderStyle = BorderStyle.None,
                Text = content
            };

            tabPage.Controls.Add(contentBox);
            tabControl.TabPages.Add(tabPage);
        }
    }
}
