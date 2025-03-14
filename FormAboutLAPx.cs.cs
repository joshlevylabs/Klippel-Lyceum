using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace LyceumKlippel
{
    public partial class FormAboutLAPx : BaseForm
    {
        private TabControl tabControl;

        public FormAboutLAPx() : base(false) // Disable BaseForm's menu strip
        {
            InitializeComponent();
            AddTabs(); // Populate the tabs with content
        }

        private void InitializeComponent()
        {
            // Remove title bar
            this.Text = string.Empty;
            this.FormBorderStyle = FormBorderStyle.None; // No title bar or borders
            this.Size = new Size(700, 550);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(30, 30, 30); // Dark mode
            this.Padding = new Padding(10);

            // Ensure BaseForm's menu strip is hidden
            if (menuStrip != null)
            {
                menuStrip.Visible = false;
            }

            // Tab Control (properly aligned to avoid being hidden)
            tabControl = new TabControl
            {
                Dock = DockStyle.Fill, // Ensures full use of the window
                Font = new Font("Segoe UI", 10),
                BackColor = Color.FromArgb(40, 40, 40)
            };
            this.Controls.Add(tabControl);
        }

        private void AddTabs()
        {
            AddTab("Getting Started",
@"🏁 **Getting Started with LAPx**

Welcome to LAPx!  
LAPx is a powerful software solution designed to streamline **audio precision testing**, manage test **limits**, and integrate with **Lyceum** for cloud-based data storage and visualization.

🔹 **Why was LAPx Built?**
- To **simplify** collecting, analyzing, and visualizing data from **Audio Precision** hardware.
- To **centralize** test results, enabling better **collaboration** between engineers.
- To provide an **automation framework** that eliminates manual tasks.

🔹 **Applications of LAPx**
- 🎛 **R&D and Product Development** – Measure and analyze amplifier and speaker performance.
- 📊 **Production Line Testing** – Automate quality control and ensure compliance with test standards.
- 🚀 **Field Testing & Validation** – Collect and analyze real-world test data remotely.");

            AddTab("Limit Management",
@"🎯 **Managing Limits in LAPx**

🔹 **What are Limits?**
Limits define the **acceptable performance range** for audio tests. With LAPx, you can:
- **Set up new test limits** tailored to your project.
- **Edit and refine** limits based on real-world test data.
- **Upload and Download** limits to and from **APx500** & **Lyceum**.

🔹 **How to Manage Limits**
1. 📂 **Create new limits** by defining measurement constraints.
2. 🛠 **Edit existing limits** from stored sessions.
3. 🔄 **Upload Limits to Lyceum** for cloud-based sharing.
4. 📥 **Download Limits to APx** for real-time hardware testing.

💡 **Tip:** Limits ensure that tests remain within **performance specifications**, preventing failures in later stages of product development.");

            AddTab("Data",
@"📊 **Managing and Visualizing Data in LAPx**

🔹 **Saving & Organizing Data**
LAPx enables users to:
- **Save test data** as structured **sessions** for easy retrieval.
- **Upload data** to **Lyceum** for **collaborative** analysis.
- **Organize results** with **global configurations** and metadata.

🔹 **Visualizing Data**
LAPx provides a variety of **data visualization tools**, including:
- 📈 **Graphical plotting** of test measurements.
- 🔍 **Detailed tables** for analyzing raw data.
- 📊 **Comparative analysis** between different test runs.");

            AddTab("Uploading Data to Lyceum",
@"☁ **Uploading Data to Lyceum**

LAPx allows you to upload test results to **Lyceum** for cloud-based storage and analysis. You can upload data in **two ways**:

### **1️⃣ Upload from the Home Page**
- Navigate to the **Home Page**.
- Select **Get Checked Data** to extract the relevant test results.
- Click **Upload to Lyceum** to send the extracted data to the cloud.

### **2️⃣ Upload from Session Manager**
- Open the **Session Manager**.
- Select a previously saved **session** containing test data.
- Click **Upload** to push the session to **Lyceum**.

💡 **Tip:** Uploading data ensures all test results are stored **securely** and can be **accessed anywhere** for further analysis.");

            AddTab("Automation",
@"🤖 **Automating Your Workflow in LAPx**

🔹 **Why Automate?**
Automation helps:
- **Save time** by eliminating repetitive manual tasks.
- **Improve accuracy** by reducing human errors.
- **Scale testing** to handle large datasets seamlessly.

🔹 **Automated Upload Settings**
LAPx allows you to automate several parts of the upload process, including:
- 📁 **Selecting Lyceum Group**: Define which **Lyceum group** the uploaded data should belong to.
- 🏷 **Auto-Naming Upload Sessions**: Set up rules to **automatically generate a title** based on **APx session details**.
- 📏 **Unit Mapping**: Configure unit settings so the **APx measurement units** match correctly with **Lyceum standards**.

💡 **Tip:** Pre-configuring upload settings ensures **consistent data organization** and **reduces errors** when transferring test results.");
        }

        private void AddTab(string title, string formattedText)
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
                Font = new Font("Segoe UI", 10, FontStyle.Regular),
                BorderStyle = BorderStyle.None,
                Text = formattedText // Plain text instead of RTF to prevent errors
            };

            tabPage.Controls.Add(contentBox);
            tabControl.TabPages.Add(tabPage);
        }
    }
}
