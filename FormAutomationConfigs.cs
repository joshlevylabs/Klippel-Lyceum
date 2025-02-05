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
        private Label defaultGroupLabel;

        private Dictionary<string, string> unitMappings = new Dictionary<string, string>();
        private List<string> lyceumUnitList = new List<string>(); // ✅ Stores fetched units for filtering
        private string unitMappingsFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LAPxv8", "unit_mappings.json");


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

            // ✅ Create the Unit Mappings tab
            TabPage unitMappingTab = new TabPage("Unit Mappings")
            {
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White,
                AutoScroll = true
            };
            unitMappingTab.Controls.Add(CreateUnitMappingsPanel());

            // ✅ Add all tabs to the tabControl
            tabControl.TabPages.Add(titleTab);
            tabControl.TabPages.Add(groupTab);
            tabControl.TabPages.Add(unitMappingTab); // 🔥 This was missing!

            // ✅ Add the tab control to the form
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

                // 🔥 Ensure the saved default group is displayed on startup
                if (!string.IsNullOrEmpty(SelectedGroupName))
                {
                    defaultGroupLabel.Text = $"📌 {SelectedGroupName}";
                    defaultGroupLabel.ForeColor = Color.LimeGreen;
                }
                else
                {
                    defaultGroupLabel.Text = "📌 No Default Group Selected";
                    defaultGroupLabel.ForeColor = Color.Gray;
                }

                LogManager.AppendLog($"✅ Loaded Title Format: {TitleFormat}");
                LogManager.AppendLog($"✅ Loaded Selected Group: {SelectedGroupName} (ID: {SelectedGroupId})");
            }
        }

        private void UpdateGroupSelectionUI()
        {
            if (!string.IsNullOrEmpty(SelectedGroupName))
            {
                LogManager.AppendLog($"🎯 Default Group: {SelectedGroupName}");

                // Display default group icon
                PictureBox defaultGroupIcon = new PictureBox
                {
                    //Image = Properties.Resources.default_icon, // 🔥 Ensure you have an icon in Resources
                    SizeMode = PictureBoxSizeMode.StretchImage,
                    Size = new Size(24, 24),
                    Location = new Point(520, 80)
                };

                Label defaultGroupLabel = new Label
                {
                    Text = $"Default Group: {SelectedGroupName}",
                    ForeColor = Color.White,
                    BackColor = Color.Transparent,
                    Location = new Point(550, 85),
                    AutoSize = true
                };

                Controls.Add(defaultGroupIcon);
                Controls.Add(defaultGroupLabel);
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

                // 🔥 Label styled to look like an "icon"
                defaultGroupLabel = new Label
                {
                    Text = "📌 No Default Group Selected", // Default text
                    AutoSize = true,
                    Font = new Font("Segoe UI", 12, FontStyle.Bold),
                    ForeColor = Color.FromArgb(255, 215, 0), // Gold color
                    Top = 10,
                    Left = 300
                };

                // 🔍 Search box for filtering groups
                TextBox searchBox = new TextBox
                {
                    Top = 50,
                    Left = 10,
                    Width = 500,
                    BackColor = Color.FromArgb(50, 50, 50),
                    ForeColor = Color.Gray, // Start with gray text
                    Text = "Search groups..." // Simulated placeholder
                };

                // Handle Enter event (Clear text when user clicks inside)
                searchBox.Enter += (sender, e) =>
                {
                    if (searchBox.Text == "Search groups...")
                    {
                        searchBox.Text = "";
                        searchBox.ForeColor = Color.White; // Normal text color
                    }
                };

                // Handle Leave event (Restore placeholder if empty)
                searchBox.Leave += (sender, e) =>
                {
                    if (string.IsNullOrWhiteSpace(searchBox.Text))
                    {
                        searchBox.Text = "Search groups...";
                        searchBox.ForeColor = Color.Gray; // Placeholder color
                    }
                };

                // 📋 ListBox to display available groups
                ListBox groupListBox = new ListBox
                {
                    Top = 80,
                    Left = 10,
                    Width = 500,
                    Height = 200,
                    BackColor = Color.FromArgb(50, 50, 50),
                    ForeColor = Color.White
                };

                // 💾 Save selection button
                Button saveButton = new Button
                {
                    Text = "Save Group Selection",
                    Top = 300,
                    Left = 10,
                    Width = 200,
                    Height = 40,
                    BackColor = Color.FromArgb(75, 110, 175),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat
                };
                saveButton.FlatAppearance.BorderSize = 1;

                // 🖱 Save selected group when button is clicked
                saveButton.Click += (sender, e) =>
                {
                    if (groupListBox.SelectedItem != null)
                    {
                        string selectedGroupName = groupListBox.SelectedItem.ToString();
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
                };

                panel.Controls.Add(label);
                panel.Controls.Add(defaultGroupLabel);
                panel.Controls.Add(searchBox);
                panel.Controls.Add(groupListBox);
                panel.Controls.Add(saveButton);

                // 🔄 Load Lyceum groups into the list
                await LoadLyceumGroupsAsync(searchBox, groupListBox);

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

                    // 🔍 Enable searching within the list
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

                // 🔥 Update the label to show the newly selected group
                defaultGroupLabel.Text = $"📌 {SelectedGroupName}";
                defaultGroupLabel.ForeColor = Color.LimeGreen;

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

        private Panel CreateUnitMappingsPanel()
        {
            Panel panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(45, 45, 48) };

            Label mappingLabel = new Label
            {
                Text = "Automate Unit Mappings:",
                AutoSize = true,
                Top = 10,
                Left = 10,
                ForeColor = Color.White
            };

            ListBox unitMappingList = new ListBox
            {
                Top = 40,
                Left = 10,
                Width = 500,
                Height = 200,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White
            };

            Label originalUnitLabel = new Label
            {
                Text = "Unmatched Data Session Unit:",
                AutoSize = true,
                Top = 250,
                Left = 10,
                ForeColor = Color.White
            };

            TextBox originalUnitBox = new TextBox
            {
                Top = 275,
                Left = 10,
                Width = 230,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White
            };

            Label replacementUnitLabel = new Label
            {
                Text = "Matching Lyceum Unit:",
                AutoSize = true,
                Top = 250,
                Left = 250,
                ForeColor = Color.White
            };

            ComboBox replacementUnitDropdown = new ComboBox
            {
                Top = 275,
                Left = 250,
                Width = 230,
                DropDownStyle = ComboBoxStyle.DropDown,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White
            };

            replacementUnitDropdown.TextChanged += (sender, e) =>
            {
                string searchText = replacementUnitDropdown.Text.ToLower();
                var filteredUnits = lyceumUnitList.Where(unit => unit.ToLower().Contains(searchText)).ToArray();

                replacementUnitDropdown.Items.Clear();
                replacementUnitDropdown.Items.AddRange(filteredUnits);
                replacementUnitDropdown.DroppedDown = true;
                Cursor.Current = Cursors.Default;
            };

            Button addMappingButton = new Button
            {
                Text = "Add Mapping",
                Top = 310,
                Left = 10,
                Width = 170,
                Height = 40,
                BackColor = Color.FromArgb(75, 110, 175),
                ForeColor = Color.White
            };

            Button removeMappingButton = new Button
            {
                Text = "Remove Selected",
                Top = 310,
                Left = 190,
                Width = 170,
                Height = 40,
                BackColor = Color.FromArgb(175, 75, 75),
                ForeColor = Color.White
            };

            addMappingButton.Click += (sender, e) =>
            {
                if (!string.IsNullOrWhiteSpace(originalUnitBox.Text) && !string.IsNullOrWhiteSpace(replacementUnitDropdown.Text))
                {
                    unitMappings[originalUnitBox.Text] = replacementUnitDropdown.Text;
                    unitMappingList.Items.Add($"{originalUnitBox.Text} → {replacementUnitDropdown.Text}");
                    SaveUnitMappings();
                }
            };

            removeMappingButton.Click += (sender, e) =>
            {
                if (unitMappingList.SelectedItem != null)
                {
                    string selectedMapping = unitMappingList.SelectedItem.ToString();
                    string originalUnit = selectedMapping.Split('→')[0].Trim();
                    unitMappings.Remove(originalUnit);
                    unitMappingList.Items.Remove(unitMappingList.SelectedItem);
                    SaveUnitMappings();
                }
            };

            panel.Controls.Add(mappingLabel);
            panel.Controls.Add(unitMappingList);
            panel.Controls.Add(originalUnitLabel);
            panel.Controls.Add(originalUnitBox);
            panel.Controls.Add(replacementUnitLabel);
            panel.Controls.Add(replacementUnitDropdown);
            panel.Controls.Add(addMappingButton);
            panel.Controls.Add(removeMappingButton);

            // Fetch Lyceum Units and Populate Dropdown
            Task.Run(async () => await FetchAndPopulateUnits(replacementUnitDropdown));

            // ✅ Load existing mappings from file
            LoadUnitMappings(unitMappingList);

            return panel;
        }

        private async Task FetchAndPopulateUnits(ComboBox unitDropdown)
        {
            try
            {
                string url = "https://api.thelyceum.io/api/project/metadata/";
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                var response = await client.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    string responseContent = await response.Content.ReadAsStringAsync();
                    var metadata = JObject.Parse(responseContent);

                    var xUnits = metadata["x_units"].Children<JObject>().ToList();
                    var yUnits = metadata["y_units"].Children<JObject>().ToList();

                    List<string> unitNames = new List<string>();

                    foreach (var unit in xUnits.Concat(yUnits))
                    {
                        string label = unit["label"].ToString();
                        string symbol = unit["symbol"]?.ToString() ?? "";
                        string magnitudeSymbol = unit["magnitude_symbol"]?.ToString() ?? "";
                        string fullSymbol = $"{magnitudeSymbol}{symbol}".Trim();

                        if (!string.IsNullOrEmpty(label)) unitNames.Add(label);
                        if (!string.IsNullOrEmpty(fullSymbol) && fullSymbol != label) unitNames.Add(fullSymbol);
                    }

                    lyceumUnitList = unitNames.Distinct().OrderBy(name => name).ToList(); // ✅ Store for filtering

                    unitDropdown.Invoke((MethodInvoker)(() =>
                    {
                        unitDropdown.Items.Clear();
                        unitDropdown.Items.AddRange(lyceumUnitList.ToArray());
                    }));

                    LogManager.AppendLog($"✅ Successfully fetched {lyceumUnitList.Count} units from Lyceum.");
                }
                else
                {
                    LogManager.AppendLog($"❌ Failed to fetch units. Status: {response.StatusCode}. Response: {await response.Content.ReadAsStringAsync()}");
                }
            }
            catch (Exception ex)
            {
                LogManager.AppendLog($"❌ Error fetching units: {ex.Message}");
            }
        }

        private void SaveUnitMappings()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(unitMappingsFilePath));
                var config = new JObject
                {
                    ["UnitMappings"] = JObject.FromObject(unitMappings)
                };

                File.WriteAllText(unitMappingsFilePath, config.ToString(Formatting.Indented));
                LogManager.AppendLog("✅ Unit mappings saved successfully.");
            }
            catch (Exception ex)
            {
                LogManager.AppendLog($"❌ ERROR saving unit mappings: {ex.Message}");
            }
        }

        private void LoadUnitMappings(ListBox unitMappingList)
        {
            try
            {
                if (File.Exists(unitMappingsFilePath))
                {
                    string json = File.ReadAllText(unitMappingsFilePath);
                    var config = JsonConvert.DeserializeObject<JObject>(json);
                    unitMappings = config["UnitMappings"]?.ToObject<Dictionary<string, string>>() ?? new Dictionary<string, string>();

                    unitMappingList.Items.Clear();
                    foreach (var mapping in unitMappings)
                    {
                        unitMappingList.Items.Add($"{mapping.Key} → {mapping.Value}");
                    }

                    LogManager.AppendLog($"✅ Loaded {unitMappings.Count} unit mappings from file.");
                }
            }
            catch (Exception ex)
            {
                LogManager.AppendLog($"❌ ERROR loading unit mappings: {ex.Message}");
            }
        }

    }

    public interface IGlobalPropertiesProvider
    {
        Dictionary<string, string> GetGlobalProperties();
        Dictionary<string, string> GetLyceumGroups();
    }
}

