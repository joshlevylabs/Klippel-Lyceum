using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Drawing;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.IO;
using static LAPxv8.FormAudioPrecision8;
using System.Net.Http;
using System.Threading.Tasks;

namespace LAPxv8
{
    public partial class FormAutomationConfigs : BaseForm
    {
        public string TitleFormat { get; private set; }
        public string SelectedLyceumGroup { get; private set; }
        private Dictionary<string, string> globalProperties;
        private List<JObject> lyceumGroups; // ✅ Changed to store full group details
        private string configFilePath;
        private string accessToken; // ✅ Added for API authentication
        private static readonly HttpClient client = new HttpClient(); // ✅ Fix for missing `client`


        private TextBox titleFormatBox;
        private ListBox globalPropertiesList;
        private string SelectedGroupId { get; set; }
        private string SelectedGroupName { get; set; }

        public FormAutomationConfigs(string accessToken, Dictionary<string, string> globalProperties = null)
    : base(false, BaseForm.GetAccessToken())
        {
            try
            {
                LogManager.AppendLog("🚀 FormAutomationConfigs constructor started.");

                this.accessToken = accessToken ?? throw new ArgumentNullException(nameof(accessToken), "Access token is null!");
                LogManager.AppendLog($"✅ Stored accessToken (First 15 chars): {accessToken.Substring(0, Math.Min(15, accessToken.Length))}...");

                this.globalProperties = globalProperties ?? GetGlobalPropertiesFromParent();
                this.lyceumGroups = new List<JObject>();
                this.configFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LAPxv8", "config.json");

                // ✅ Increase the window size for better UI visibility
                this.Size = new Size(800, 600);
                this.MinimumSize = new Size(800, 600);

                InitializeUI();
                LoadTitleFormat();

                LogManager.AppendLog("✅ FormAutomationConfigs constructor completed successfully.");
            }
            catch (Exception ex)
            {
                LogManager.AppendLog($"❌ ERROR in FormAutomationConfigs Constructor: {ex.Message}");
            }
        }

        private void InitializeUI()
        {
            this.Text = "Automation Configurations";
            this.BackColor = Color.FromArgb(30, 30, 30);
            this.ForeColor = Color.White;
            this.Font = new Font("Segoe UI", 10);

            TabControl tabControl = new TabControl
            {
                Dock = DockStyle.Fill, // ✅ Ensures it fills the window dynamically
                BackColor = Color.FromArgb(45, 45, 48)
            };

            TabPage titleTab = new TabPage("Title Construction")
            {
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White,
                AutoScroll = true // ✅ Allows scrolling if needed
            };
            titleTab.Controls.Add(CreateTitleConfigurationPanel());

            TabPage groupTab = new TabPage("Lyceum Group Selection")
            {
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White,
                AutoScroll = true // ✅ Allows scrolling if needed
            };

            Panel groupPanel = new Panel
            {
                Dock = DockStyle.Fill // ✅ Ensures panel fills the tab properly
            };
            CreateGroupSelectionPanel(groupPanel);
            groupTab.Controls.Add(groupPanel);

            tabControl.TabPages.Add(titleTab);
            tabControl.TabPages.Add(groupTab);
            Controls.Add(tabControl);
        }

        private Panel CreateTitleConfigurationPanel()
        {
            Panel panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(45, 45, 48) };

            Label label = new Label
            {
                Text = "Define an automated title format:",
                AutoSize = true,
                Top = 10,
                Left = 10,
                ForeColor = Color.White
            };

            titleFormatBox = new TextBox
            {
                Top = 40,
                Left = 10,
                Width = 500, // Increased width
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            Label globalPropertiesLabel = new Label
            {
                Text = "Global Properties:",
                AutoSize = true,
                Top = 80,
                Left = 10,
                ForeColor = Color.White
            };

            globalPropertiesList = new ListBox
            {
                Top = 100,
                Left = 10,
                Width = 500,  // Increased width
                Height = 250, // Increased height
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                SelectionMode = SelectionMode.MultiExtended
            };

            foreach (var prop in globalProperties.Keys)
            {
                globalPropertiesList.Items.Add(prop);
            }

            ContextMenuStrip contextMenu = new ContextMenuStrip();
            ToolStripMenuItem addMenuItem = new ToolStripMenuItem("Add", null, AddGlobalPropertyToTitleBox);
            contextMenu.Items.Add(addMenuItem);
            globalPropertiesList.ContextMenuStrip = contextMenu;

            Button testButton = new Button
            {
                Text = "Test",
                Top = 370, // Adjusted position for larger list
                Left = 10,
                Width = 150,
                Height = 50, // Increased height
                BackColor = Color.FromArgb(75, 110, 175), // Updated to blue color
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            testButton.FlatAppearance.BorderSize = 1;
            testButton.Click += (sender, e) =>
            {
                string constructedTitle = GetFormattedTitle();
                LogManager.AppendLog($"[DEBUG] Test Button - Constructed Title: {constructedTitle}");
                MessageBox.Show($"Constructed Title: {constructedTitle}", "Title Preview", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            Button saveButton = new Button
            {
                Text = "Save Title Format",
                Top = 370,
                Left = 170,
                Width = 200,
                Height = 50, // Increased height
                BackColor = Color.FromArgb(75, 110, 175), // Updated to blue color
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            saveButton.FlatAppearance.BorderSize = 1;
            saveButton.Click += (sender, e) =>
            {
                TitleFormat = titleFormatBox.Text;
                SaveTitleFormat();
                LogManager.AppendLog($"[DEBUG] Saved Title Format: {TitleFormat}");
                MessageBox.Show("Title format saved!", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            panel.Controls.Add(label);
            panel.Controls.Add(titleFormatBox);
            panel.Controls.Add(globalPropertiesLabel);
            panel.Controls.Add(globalPropertiesList);
            panel.Controls.Add(testButton);
            panel.Controls.Add(saveButton);

            return panel;
        }

        private string GetFormattedTitle()
        {
            string constructedTitle = titleFormatBox.Text;

            // ✅ Loop through ALL global properties, not just selected ones
            foreach (var property in globalProperties.Keys)
            {
                string placeholder = $"<{property}>";
                if (constructedTitle.Contains(placeholder))
                {
                    constructedTitle = constructedTitle.Replace(placeholder, globalProperties[property]);
                }
            }

            return constructedTitle;
        }

        private void SaveTitleFormat()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(configFilePath));
            var config = new { TitleFormat = TitleFormat };
            File.WriteAllText(configFilePath, JsonConvert.SerializeObject(config, Formatting.Indented));

            // ✅ Log the saved title format
            LogManager.AppendLog($"✅ Saved Title Format: {TitleFormat}");
        }

        private void LoadTitleFormat()
        {
            if (File.Exists(configFilePath))
            {
                string json = File.ReadAllText(configFilePath);
                var config = JsonConvert.DeserializeObject<JObject>(json);
                TitleFormat = config["TitleFormat"]?.ToString() ?? "";
                SelectedGroupId = config["SelectedGroupId"]?.ToString();
                SelectedGroupName = config["SelectedGroupName"]?.ToString();

                titleFormatBox.Text = TitleFormat;
                LogManager.AppendLog($"✅ Loaded Title Format: {TitleFormat}");
                LogManager.AppendLog($"✅ Loaded Selected Group: {SelectedGroupName} (ID: {SelectedGroupId})");
            }
        }

        // ✅ Right-click "Add" functionality
        private void AddGlobalPropertyToTitleBox(object sender, EventArgs e)
        {
            if (globalPropertiesList.SelectedItems.Count > 0)
            {
                string insertText = "";
                foreach (var item in globalPropertiesList.SelectedItems)
                {
                    insertText += $"<{item.ToString()}> ";
                }

                // ✅ Insert at current cursor position
                int selectionStart = titleFormatBox.SelectionStart;
                titleFormatBox.Text = titleFormatBox.Text.Insert(selectionStart, insertText.Trim());
                titleFormatBox.SelectionStart = selectionStart + insertText.Length;
            }
        }
        private Dictionary<string, string> GetGlobalPropertiesFromParent()
        {
            if (Application.OpenForms.Count > 0)
            {
                foreach (Form openForm in Application.OpenForms)
                {
                    if (openForm is FormAudioPrecision8 audioForm)
                    {
                        return audioForm.globalProperties ?? new Dictionary<string, string> { { "None Found", "" } };
                    }
                }
            }
            return new Dictionary<string, string> { { "None Found", "" } };
        }
        private async void CreateGroupSelectionPanel(Panel panel)
        {
            try
            {
                LogManager.AppendLog("🚀 Initializing Group Selection Panel...");

                panel.BackColor = Color.FromArgb(45, 45, 48);

                Label label = new Label
                {
                    Text = "Select Default Lyceum Group:",
                    AutoSize = true,
                    Top = 10,
                    Left = 10,
                    ForeColor = Color.White
                };

                TextBox searchBox = new TextBox
                {
                    Top = 40,
                    Left = 10,
                    Width = 500,
                    BackColor = Color.FromArgb(50, 50, 50),
                    ForeColor = Color.White
                };

                ListBox listBox = new ListBox
                {
                    Top = 80,
                    Left = 10,
                    Width = 500,
                    Height = 250,
                    BackColor = Color.FromArgb(50, 50, 50),
                    ForeColor = Color.White
                };

                Button saveButton = new Button
                {
                    Text = "Save Group Selection",
                    Top = 340,
                    Left = 10,
                    Width = 200,
                    Height = 40,
                    BackColor = Color.FromArgb(75, 110, 175),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat
                };

                saveButton.FlatAppearance.BorderSize = 1;
                saveButton.Click += (sender, e) =>
                {
                    try
                    {
                        if (listBox.SelectedItem != null)
                        {
                            string selectedGroupName = listBox.SelectedItem.ToString();
                            var selectedGroup = lyceumGroups.FirstOrDefault(g => g["name"].ToString() == selectedGroupName);
                            if (selectedGroup != null)
                            {
                                AppendGroupDetails(selectedGroup);
                            }
                        }
                        else
                        {
                            MessageBox.Show("Please select a group first.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogManager.AppendLog($"❌ ERROR in Save Group Selection: {ex.Message}");
                    }
                };

                panel.Controls.Add(label);
                panel.Controls.Add(searchBox);
                panel.Controls.Add(listBox);
                panel.Controls.Add(saveButton);

                await LoadLyceumGroupsAsync(searchBox, listBox);

                LogManager.AppendLog("✅ Group Selection Panel Initialized.");
            }
            catch (Exception ex)
            {
                LogManager.AppendLog($"❌ ERROR in CreateGroupSelectionPanel: {ex.Message}");
            }
        }

        private async Task LoadLyceumGroupsAsync(TextBox searchBox, ListBox listBox)
        {
            try
            {
                LogManager.AppendLog("🚀 Loading Lyceum Groups...");

                if (string.IsNullOrEmpty(accessToken))
                {
                    LogManager.AppendLog("❌ ERROR: AccessToken is null or empty.");
                    return;
                }

                string url = "https://api.thelyceum.io/api/organization/groups/";
                LogManager.AppendLog($"📡 API Request: {url}");

                if (client == null)
                {
                    LogManager.AppendLog("❌ ERROR: HttpClient is null.");
                    return;
                }

                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                var response = await client.GetAsync(url);
                LogManager.AppendLog($"📡 API Response Status: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    string responseContent = await response.Content.ReadAsStringAsync();
                    JArray groups = JArray.Parse(responseContent);

                    if (groups == null || !groups.Any())
                    {
                        LogManager.AppendLog("⚠ WARNING: No groups found.");
                        return;
                    }

                    lyceumGroups = groups.Select(g => (JObject)g).ToList();
                    var groupNames = lyceumGroups.Select(g => g["name"]?.ToString()).Where(name => !string.IsNullOrEmpty(name)).ToList();

                    listBox.Items.Clear();
                    listBox.Items.AddRange(groupNames.ToArray());

                    // ✅ Enable searching within the list
                    searchBox.TextChanged += (sender, e) =>
                    {
                        string searchText = searchBox.Text.ToLower();
                        listBox.Items.Clear();
                        listBox.Items.AddRange(groupNames.Where(g => g.ToLower().Contains(searchText)).ToArray());
                    };

                    LogManager.AppendLog($"✅ Successfully loaded {groups.Count} Lyceum Groups.");
                }
                else
                {
                    LogManager.AppendLog("❌ ERROR: Failed to fetch groups - Unauthorized or endpoint error.");
                }
            }
            catch (Exception ex)
            {
                LogManager.AppendLog($"❌ ERROR Fetching Groups: {ex.Message}");
            }
        }

        // ✅ Function to retrieve groups and show selection window
        private void FetchAndShowGroups()
        {
            try
            {
                JArray groups = FetchLyceumGroups();
                if (groups == null || !groups.Any())
                {
                    LogManager.AppendLog("⚠ WARNING: No Lyceum groups retrieved.");
                    MessageBox.Show("No groups found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                ShowGroupSelectionPrompt(groups);
            }
            catch (Exception ex)
            {
                LogManager.AppendLog($"❌ ERROR Fetching Lyceum Groups: {ex.Message}");
            }
        }

        // ✅ Fetch groups (API simulation)
        private JArray FetchLyceumGroups()
        {
            try
            {
                LogManager.AppendLog("🔍 Fetching Lyceum Groups...");

                // Simulated API call (replace with actual API request if needed)
                return new JArray
        {
            new JObject { { "id", "123" }, { "name", "Group A" } },
            new JObject { { "id", "456" }, { "name", "Group B" } },
            new JObject { { "id", "789" }, { "name", "Group C" } }
        };
            }
            catch (Exception ex)
            {
                LogManager.AppendLog($"❌ ERROR Fetching Groups: {ex.Message}");
                return new JArray();
            }
        }

        // ✅ Show selection window
        private void ShowGroupSelectionPrompt(JArray groups)
        {
            using (BaseForm prompt = new BaseForm())
            {
                prompt.Width = 600;
                prompt.Height = 400;
                prompt.BackColor = Color.FromArgb(30, 30, 30);
                prompt.ForeColor = Color.White;
                prompt.StartPosition = FormStartPosition.CenterScreen;
                prompt.Text = "Select Group";

                Label textLabel = new Label() { Left = 50, Top = 50, Text = "Search and select a group:" };
                TextBox searchBox = new TextBox() { Left = 50, Top = 80, Width = 500 };
                ListBox listBox = new ListBox() { Left = 50, Top = 110, Width = 500, Height = 200 };

                var filteredGroups = groups.Select(g => g["name"].ToString()).ToList();
                listBox.Items.AddRange(filteredGroups.ToArray());

                searchBox.TextChanged += (sender, e) =>
                {
                    string searchText = searchBox.Text.ToLower();
                    listBox.Items.Clear();
                    listBox.Items.AddRange(filteredGroups.Where(g => g.ToLower().Contains(searchText)).ToArray());
                };

                Button confirmation = new Button() { Text = "Ok", Left = 450, Width = 100, Top = 330, DialogResult = DialogResult.OK };
                confirmation.Click += (sender, e) => { prompt.Close(); };

                prompt.Controls.Add(searchBox);
                prompt.Controls.Add(listBox);
                prompt.Controls.Add(confirmation);
                prompt.Controls.Add(textLabel);
                prompt.AcceptButton = confirmation;

                if (prompt.ShowDialog() == DialogResult.OK && listBox.SelectedItem != null)
                {
                    string selectedGroupName = listBox.SelectedItem.ToString();
                    var selectedGroup = groups.FirstOrDefault(g => g["name"].ToString() == selectedGroupName);
                    if (selectedGroup != null)
                    {
                        AppendGroupDetails((JObject)selectedGroup);
                    }
                }
            }
        }

        private void AppendGroupDetails(JObject selectedGroup)
        {
            try
            {
                SelectedGroupId = selectedGroup["id"].ToString();
                SelectedGroupName = selectedGroup["name"].ToString();

                LogManager.AppendLog($"✅ Selected Lyceum Group: {SelectedGroupName} (ID: {SelectedGroupId})");

                var config = new JObject
                {
                    ["TitleFormat"] = TitleFormat,
                    ["SelectedGroupId"] = SelectedGroupId,
                    ["SelectedGroupName"] = SelectedGroupName
                };

                File.WriteAllText(configFilePath, config.ToString(Newtonsoft.Json.Formatting.Indented));

                MessageBox.Show($"Group Selected: {SelectedGroupName}", "Group Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                LogManager.AppendLog($"❌ ERROR in AppendGroupDetails: {ex.Message}");
            }
        }
        private Dictionary<string, string> GetLyceumGroupsFromParent()
        {
            return Application.OpenForms.OfType<BaseForm>().OfType<IGlobalPropertiesProvider>().FirstOrDefault()?.GetLyceumGroups() ?? new Dictionary<string, string>();
        }
    }

    public interface IGlobalPropertiesProvider
    {
        Dictionary<string, string> GetGlobalProperties();
        Dictionary<string, string> GetLyceumGroups();
    }
}

