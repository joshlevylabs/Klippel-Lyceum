using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using LAPxv8;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Management;
using System.Diagnostics;
using DocumentFormat.OpenXml.Office2019.Excel.RichData2;
using System.Threading.Tasks;
using static LAPxv8.FormAudioPrecision8;
using System.Windows.Forms.DataVisualization.Charting;
using System.Net.NetworkInformation;
using AudioPrecision.API;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ListView;
using System.Net.Http;
using System.IO.Compression;

namespace LAPxv8
{
    public enum SessionMode
    {
        Save,
        View
    }

    public class FormSessionManager : BaseForm
    {
        private string accessToken;
        private string refreshToken;
        private SessionMode mode;
        private List<ProjectSession> sessions;
        private string environmentKey;
        private string systemKey;
        public event Action<ProjectSession> OnSessionDataCreated;
        private FormAudioPrecision8 formAudioPrecision8;
        private Action<string> externalLogger;

        // UI elements
        private Button createNewButton;
        private ListBox sessionsListBox;
        private TreeView resultsTreeView;
        private TextBox detailsTextBox;
        private Panel graphPanel;
        private ComboBox globalPropertyComboBox;
        private ComboBox resultDetailComboBox;
        private TextBox logTextBox;
        private TextBox searchTextBox;
        private ToolTip sessionToolTip;

        // Method that triggers the event
        public void TriggerSessionCreation(ProjectSession session)
        {
            OnSessionDataCreated?.Invoke(session);
        }

        public FormSessionManager(string jsonData, List<ProjectSession> data, SessionMode mode, FormAudioPrecision8 formAudioPrecision8, Action<string> logger, string accessToken, string refreshToken)
        {
            this.accessToken = accessToken;
            this.refreshToken = refreshToken;
            this.externalLogger = logger;
            this.mode = mode;
            this.sessions = data ?? new List<ProjectSession>();
            this.formAudioPrecision8 = formAudioPrecision8;

            // Set the title for the form, which will appear in the title bar
            this.Text = "Session Manager";  // This adds the title to the existing title bar

            if (!string.IsNullOrEmpty(jsonData))
            {
                string dataPreview = jsonData.Substring(0, Math.Min(jsonData.Length, 100)) + "...";

                if (logTextBox != null)
                {
                    LogManager.AppendLog($"Received data: " + dataPreview);
                }
                else
                {
                    Console.WriteLine("Received data: " + dataPreview);
                }
            }
            else
            {
                string message = "Received data is null or empty.";
                if (logTextBox != null)
                {
                    LogManager.AppendLog(logTextBox, message);
                }
                else
                {
                    Console.WriteLine(message);
                }
            }

            InitializeComponents();

            if (formAudioPrecision8 != null && mode == SessionMode.Save)
            {
                formAudioPrecision8.OnSessionDataCreated += (newSession) =>
                {
                    sessions.Add(newSession);
                    LoadSessions();
                    LogManager.AppendLog($"New session added: " + newSession.Title);
                };
            }

            LoadSessions();
            this.Load += FormSessionManager_Load;
            Console.WriteLine("FormSessionManager - Constructor: formAudioPrecision8 is " + (formAudioPrecision8 != null ? "not null" : "null"));
        }

        

        private void HandleNewSession(ProjectSession newSession)
        {
            sessions.Add(newSession);
            LoadSessions();
            LogManager.AppendLog($"New session added: " + newSession.Title);
        }

        private string currentSessionData;

        public ProjectSession CreatedSession { get; private set; }

        // Method to add a new session and refresh the list
        private void AddNewSessionAndLoad(ProjectSession newSession)
        {
            sessions.Add(newSession);
            LoadSessions(); // Refresh the list with the new session
        }

        private void InitializeComponents()
        {
            // Form settings
            this.BackColor = Color.FromArgb(45, 45, 45); // Dark mode background
            this.ForeColor = Color.White;
            this.Font = new Font("Segoe UI", 10);
            this.Size = new Size(1400, 900);
            this.FormBorderStyle = FormBorderStyle.None; // Remove default title bar
            this.MaximizeBox = false;

            // Assuming BaseForm provides title bar and menu strip, no need to add them here

            // If a MenuStrip already exists in the base form, use it
            MenuStrip menuStrip = this.MainMenuStrip;
            if (menuStrip != null)
            {
                // Add custom options menu
                ToolStripMenuItem optionsMenu = new ToolStripMenuItem("Options");

                // Create New option
                ToolStripMenuItem createNewMenuItem = new ToolStripMenuItem("Create New", null, (sender, e) => CreateNewButton_Click(sender, e));
                optionsMenu.DropDownItems.Add(createNewMenuItem);

                // Upload to Lyceum option
                ToolStripMenuItem uploadToLyceumMenuItem = new ToolStripMenuItem("Upload to Lyceum", null, (sender, e) => UploadToLyceumButton_Click(sender, e));
                optionsMenu.DropDownItems.Add(uploadToLyceumMenuItem);

                // Add Options menu to the existing menu strip
                menuStrip.Items.Add(optionsMenu);
            }

            // Vertical alignment and sizing
            int frameHeight = 220;
            int frameWidth = 300;
            int verticalSpacing = 20;

            // Create and add a group box for sessions
            GroupBox sessionsGroupBox = new GroupBox
            {
                Text = "Sessions",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(45, 45, 45),
                Location = new Point(20, 80),
                Size = new Size(frameWidth, frameHeight)
            };
            this.Controls.Add(sessionsGroupBox);

            // Add a "Search" label next to the search text box.
            Label searchLabel = new Label
            {
                Text = "Search",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(20, menuStrip.Bottom + 10),
                AutoSize = true
            };
            this.Controls.Add(searchLabel);

            // Initialize and set properties for the search text box
            searchTextBox = new TextBox
            {
                Location = new Point(searchLabel.Right + 10, menuStrip.Bottom + 10),
                Width = 300,
                //PlaceholderText = "Search sessions...",
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(45, 45, 45),
                Dock = DockStyle.Top
            };
            searchTextBox.TextChanged += SearchTextBox_TextChanged;
            // Add search box to the form
            this.Controls.Add(searchTextBox);

            sessionsListBox = new ListBox
            {
                Font = new Font("Segoe UI", 10),
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                HorizontalScrollbar = true,
                Dock = DockStyle.Fill
            };
            sessionsListBox.MouseMove += SessionsListBox_MouseMove;
            sessionsListBox.MouseLeave += SessionsListBox_MouseLeave;
            // Initialize ToolTip for displaying session names
            sessionToolTip = new ToolTip();

            sessionsListBox.SelectedIndexChanged += SessionsListBox_SelectedIndexChanged;
            sessionsGroupBox.Controls.Add(sessionsListBox);



            // Create and add a group box for results
            GroupBox resultsGroupBox = new GroupBox
            {
                Text = "Results",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(45, 45, 45),
                Location = new Point(20, 80 + frameHeight + verticalSpacing),
                Size = new Size(frameWidth, frameHeight)
            };
            this.Controls.Add(resultsGroupBox);

            resultsTreeView = new TreeView
            {
                Font = new Font("Segoe UI", 12),
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                Dock = DockStyle.Fill
            };
            resultsTreeView.AfterSelect += ResultsTreeView_AfterSelect;
            resultsGroupBox.Controls.Add(resultsTreeView);

            // Create and add a group box for details text box
            GroupBox detailsTextGroupBox = new GroupBox
            {
                Text = "Details Text",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(45, 45, 45),
                Location = new Point(20, 80 + 2 * (frameHeight + verticalSpacing)),
                Size = new Size(frameWidth, frameHeight)
            };
            this.Controls.Add(detailsTextGroupBox);

            detailsTextBox = new TextBox
            {
                Font = new Font("Consolas", 10),
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill,
                ReadOnly = true
            };
            detailsTextGroupBox.Controls.Add(detailsTextBox);

            // Create and add a group box for log text box
            GroupBox logGroupBox = new GroupBox
            {
                Text = "Log",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(45, 45, 45),
                Location = new Point(20, 80 + 3 * (frameHeight + verticalSpacing)),
                Size = new Size(frameWidth, frameHeight)
            };
            this.Controls.Add(logGroupBox);

            logTextBox = new TextBox
            {
                Font = new Font("Consolas", 10),
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill,
                ReadOnly = true
            };
            logGroupBox.Controls.Add(logTextBox);

            // Adjust the size of the GUI window to fit all elements vertically
            int totalHeight = 80 + 4 * (frameHeight + verticalSpacing);
            this.Size = new Size(this.Size.Width, totalHeight + 40);

            // Reduce the height of the graph panel
            int reducedGraphPanelHeight = totalHeight - 100; // Reduce the height by 100 pixels, adjust as necessary

            // Create and add a group box for the graph panel on the right-hand side
            GroupBox graphGroupBox = new GroupBox
            {
                Text = "Graph",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(45, 45, 45),
                Location = new Point(frameWidth + 60, 80), // Adjusted to start on the right side of other frames
                Size = new Size(this.Width - frameWidth - 100, reducedGraphPanelHeight) // Reduced height
            };
            this.Controls.Add(graphGroupBox);

            graphPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(45, 45, 45)
            };
            graphGroupBox.Controls.Add(graphPanel);

            // Additional controls like ComboBoxes and Labels
            globalPropertyComboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 12),
                Location = new Point(20, totalHeight + 50), // Adjusted to be below other components
                Size = new Size(220, 30),
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White
            };
            this.Controls.Add(globalPropertyComboBox);

            resultDetailComboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 12),
                Location = new Point(260, totalHeight + 50), // Adjusted to be below other components
                Size = new Size(220, 30),
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White
            };
            this.Controls.Add(resultDetailComboBox);
        }
        // Method to filter sessions in the ListBox based on search query
        private void SearchTextBox_TextChanged(object sender, EventArgs e)
        {
            string searchQuery = searchTextBox.Text.ToLower();
            sessionsListBox.Items.Clear();
            foreach (var session in sessions)
            {
                if (session.Title.ToLower().Contains(searchQuery))
                {
                    sessionsListBox.Items.Add(session.Title);
                }
            }
        }
        // Show full session name in tooltip on hover
        private void SessionsListBox_MouseMove(object sender, MouseEventArgs e)
        {
            int index = sessionsListBox.IndexFromPoint(e.Location);
            if (index != ListBox.NoMatches)
            {
                string fullTitle = sessions[index].Title;
                if (sessionToolTip.GetToolTip(sessionsListBox) != fullTitle)
                {
                    sessionToolTip.SetToolTip(sessionsListBox, fullTitle);
                }
            }
        }
        // Hide tooltip when mouse leaves list box
        private void SessionsListBox_MouseLeave(object sender, EventArgs e)
        {
            sessionToolTip.SetToolTip(sessionsListBox, string.Empty);
        }
        private void PopulateResultsTreeView(List<CheckedData> checkedData)
        {
            if (resultsTreeView.InvokeRequired)
            {
                resultsTreeView.Invoke(new Action(() => PopulateResultsTreeView(checkedData)));
                return;
            }

            resultsTreeView.Nodes.Clear();

            foreach (var signalPath in checkedData)
            {
                TreeNode signalPathNode = new TreeNode(signalPath.Name);
                foreach (var measurement in signalPath.Measurements)
                {
                    TreeNode measurementNode = new TreeNode(measurement.Name);
                    foreach (var result in measurement.Results)
                    {
                        TreeNode resultNode = new TreeNode(result.Name);
                        measurementNode.Nodes.Add(resultNode);
                    }
                    signalPathNode.Nodes.Add(measurementNode);
                }
                resultsTreeView.Nodes.Add(signalPathNode);
            }

            // Optionally expand all nodes
            // resultsTreeView.ExpandAll();
        }

        private void ResultsTreeView_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (e.Node.Level == 2) // If a result node is selected
            {
                string selectedResultPath = e.Node.FullPath;
                var resultData = FindResultData(selectedResultPath);

                if (resultData != null)
                {
                    DisplayResultDetails(resultData);
                    LogResultData(resultData); // Ensure this method is called
                    DisplayGraph(resultData);
                }
                else
                {
                    LogManager.AppendLog($"No result data found for the selected item.");
                }
            }
        }
        void CreateNewButton_Click(object sender, EventArgs e)
        {
            string sessionTitle = PromptForSessionName();
            if (string.IsNullOrEmpty(sessionTitle))
                return;

            if (sessions.Any(s => s.Title == sessionTitle))
            {
                MessageBox.Show("A session with this name already exists.", "Error", MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                return;
            }

            if (formAudioPrecision8 == null)
            {
                MessageBox.Show("FormAudioPrecision8 instance is not available.", "Error", MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                return;
            }

            if (systemKey == null)
            {
                MessageBox.Show("Encryption key (systemKey) is NULL. Cannot create session.", "Error", MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                LogManager.AppendLog("❌ ERROR: systemKey is NULL. Cannot create session.");
                return;
            }

            string jsonData = formAudioPrecision8.GetCurrentFormData();
            var sessionData = new { Title = sessionTitle, Data = jsonData };
            string sessionDataJson = JsonConvert.SerializeObject(sessionData);
            string encryptedData = Cryptography.EncryptString(systemKey, sessionDataJson);

            if (string.IsNullOrEmpty(encryptedData))
            {
                LogManager.AppendLog("❌ ERROR: Encryption failed.");
                return;
            }

            ProjectSession newSession = new ProjectSession
            {
                Title = sessionTitle,
                Data = encryptedData
            };

            sessions.Add(newSession);

            SaveSessionToFile(newSession, systemKey, true);
            SaveSessionToFile(newSession, systemKey, false, jsonData);

            CreatedSession = newSession;
            LogManager.AppendLog($"✅ Session '{sessionTitle}' created successfully.");

            // Refresh session list in the UI
            LoadSessions();
        }
        private string GetAbbreviatedData(string data)
        {
            return data.Substring(0, Math.Min(data.Length, 100)) + "...";
        }
        private void SaveSessionToFile(ProjectSession session, string encryptionKey, bool encrypted, string data = null)
        {
            string directoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Lyceum");
            string sessionFileName = $"{session.Title.Replace(" ", "_")}";
            string filePath = Path.Combine(directoryPath, sessionFileName + (encrypted ? ".lyc" : ".json"));

            try
            {
                string dataToSave = encrypted ? session.Data : data ?? JsonConvert.SerializeObject(new { session.Title, session.Data });
                File.WriteAllText(filePath, dataToSave);
                LogManager.AppendLog($"[SaveSessionToFile] {(encrypted ? "Encrypted" : "Unencrypted")} session '{session.Title}' saved.");
            }
            catch (Exception ex)
            {
                LogManager.AppendLog($"Error writing session file '{sessionFileName}': {ex.Message}\n");
            }
        }
        private string PromptForSessionName()
        {
            using (Form prompt = new Form())
            {
                prompt.Width = 300;
                prompt.Height = 150;
                prompt.Text = "Session Name";

                Label textLabel = new Label() { Left = 50, Top = 20, Text = "Enter Session Name:" };
                TextBox textBox = new TextBox() { Left = 50, Top = 50, Width = 200 };
                Button confirmation = new Button() { Text = "Ok", Left = 150, Width = 100, Top = 70, DialogResult = DialogResult.OK };
                confirmation.Click += (sender, e) => { prompt.Close(); };

                prompt.Controls.Add(textBox);
                prompt.Controls.Add(confirmation);
                prompt.Controls.Add(textLabel);
                prompt.AcceptButton = confirmation;

                return prompt.ShowDialog() == DialogResult.OK ? textBox.Text : "";
            }
        }
        private void SaveSession(string sessionName, string data)
        {
            var newSession = new ProjectSession
            {
                Title = sessionName,
                Data = data
            };

            sessions.Add(newSession);
            SaveSessionsToFile(systemKey); // Save sessions after adding a new one
            LoadSessions();

            LogManager.AppendLog($"New project-session '{sessionName}' created and saved to Lyceum directory.\n");
        }
        private void SaveSessionsToFile(string encryptionKey)
        {
            if (!Cryptography.IsValidBase64String(encryptionKey))
            {
                LogManager.AppendLog($"Encryption key is not a valid Base-64 string.\n");
                return;
            }

            string directoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Lyceum");

            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            foreach (var session in sessions)
            {
                string sessionFileName = $"{session.Title.Replace(" ", "_")}";
                string jsonFilePath = Path.Combine(directoryPath, sessionFileName + ".json");
                string encryptedFilePath = Path.Combine(directoryPath, sessionFileName + ".lyc");

                try
                {
                    // Serializing the session data to JSON
                    var jsonData = JsonConvert.SerializeObject(session);

                    // Log unencrypted data
                    string abbreviatedJsonData = jsonData.Substring(0, Math.Min(jsonData.Length, 100)) + "...";
                    LogManager.AppendLog($"Abbreviated unencrypted data: {abbreviatedJsonData}");

                    // Saving the unencrypted JSON file
                    File.WriteAllText(jsonFilePath, jsonData);
                    LogManager.AppendLog($"Successfully saved unencrypted session file '{jsonFilePath}'.\n");

                    // Start encryption process after saving unencrypted data
                    LogManager.AppendLog("Starting encryption process...");
                    var encryptedData = Cryptography.EncryptString(encryptionKey, jsonData);
                    if (encryptedData.StartsWith("Error in EncryptString"))
                    {
                        LogManager.AppendLog(encryptedData + "\n");
                        continue;
                    }

                    // Log encrypted data
                    string abbreviatedEncryptedData = encryptedData.Substring(0, Math.Min(encryptedData.Length, 100)) + "...";
                    LogManager.AppendLog($"Abbreviated encrypted data: {abbreviatedEncryptedData}");

                    // Saving the encrypted data
                    File.WriteAllText(encryptedFilePath, encryptedData);
                    LogManager.AppendLog($"Successfully encrypted and saved '{encryptedFilePath}'.\n");
                }
                catch (Exception ex)
                {
                    LogManager.AppendLog($"Error writing session files '{sessionFileName}': {ex.Message}\n");
                }
            }
        }
        public void LoadSessions()
        {
            string directoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Lyceum");
            LogManager.AppendLog($"[LoadSessions] Attempting to load sessions from: {directoryPath}");

            sessions.Clear(); // Reset session list

            if (!Directory.Exists(directoryPath))
            {
                LogManager.AppendLog("[LoadSessions] ❌ No session directory found.");
                return;
            }

            if (string.IsNullOrEmpty(systemKey))
            {
                LogManager.AppendLog("[LoadSessions] ❌ ERROR: Encryption key (systemKey) is NULL. Attempting retrieval...");
                systemKey = Cryptography.GetOrCreateEncryptionKey();

                if (string.IsNullOrEmpty(systemKey))
                {
                    LogManager.AppendLog("[LoadSessions] ❌ ERROR: Encryption key retrieval failed. Aborting session load.");
                    return;
                }

                LogManager.AppendLog($"[LoadSessions] ✅ Retrieved encryption key successfully.");
            }

            List<string> loadedSessionTitles = new List<string>();

            foreach (string file in Directory.EnumerateFiles(directoryPath, "*.lyc"))
            {
                LogManager.AppendLog($"[LoadSessions] Found file: {file}");

                try
                {
                    string encryptedData = File.ReadAllText(file);
                    LogManager.AppendLog($"[LoadSessions] Read Encrypted Data: Length = {encryptedData.Length}");

                    if (string.IsNullOrEmpty(encryptedData))
                    {
                        LogManager.AppendLog($"[LoadSessions] ❌ ERROR: Encrypted data in {file} is empty.");
                        continue;
                    }

                    // Decrypt data
                    string decryptedData = Cryptography.DecryptString(systemKey, encryptedData);

                    if (string.IsNullOrEmpty(decryptedData))
                    {
                        LogManager.AppendLog($"[LoadSessions] ❌ ERROR: Decryption returned NULL or empty data for file: {file}");
                        continue;
                    }
                    //LogManager.AppendLog($"[LoadSessions] ✅ Full decrypted JSON for {file}:\n{decryptedData}");

                    LogManager.AppendLog($"[LoadSessions] ✅ Decryption data Length: {decryptedData.Length}");

                    // Deserialize session data
                    ProjectSession sessionData = null;
                    try
                    {
                        sessionData = JsonConvert.DeserializeObject<ProjectSession>(decryptedData);
                    }
                    catch (JsonException jsonEx)
                    {
                        LogManager.AppendLog($"❌ ERROR: JSON Deserialization failed for {file}: {jsonEx.Message}");
                        continue;
                    }

                    if (sessionData == null || string.IsNullOrEmpty(sessionData.Data))
                    {
                        LogManager.AppendLog($"[LoadSessions] ❌ ERROR: Session data is NULL or empty after deserialization for {file}.");
                        continue;
                    }

                    sessions.Add(sessionData);
                    loadedSessionTitles.Add(sessionData.Title);
                    LogManager.AppendLog($"[LoadSessions] ✅ Session '{sessionData.Title}' loaded successfully.");
                }
                catch (Exception ex)
                {
                    LogManager.AppendLog($"[LoadSessions] ❌ ERROR processing session file '{file}': {ex.Message}");
                }
            }

            // ✅ Ensure UI updates happen after the form handle is created
            if (this.IsHandleCreated)
            {
                this.Invoke((MethodInvoker)delegate
                {
                    sessionsListBox.Items.Clear();
                    foreach (var title in loadedSessionTitles)
                    {
                        sessionsListBox.Items.Add(title);
                    }
                    sessionsListBox.Refresh();
                });

                LogManager.AppendLog($"[LoadSessions] ✅ UI updated: {sessionsListBox.Items.Count} sessions displayed.");
            }
            else
            {
                LogManager.AppendLog("❌ ERROR: Form handle is not created. Skipping UI update.");
            }

            LogManager.AppendLog($"[LoadSessions] ✅ Completed loading {sessions.Count} sessions.");
        }
        
        private void UpdateSessionListBox()
        {
            if (sessionsListBox.IsHandleCreated)
            {
                sessionsListBox.Invoke(new MethodInvoker(() =>
                {
                    sessionsListBox.Items.Clear();
                    foreach (var session in sessions)
                    {
                        sessionsListBox.Items.Add(session.Title);
                    }
                }));
            }
        }
        private void LoadSessionsFromFile(string encryptionKey)
        {
            LogManager.AppendLog("Attempting to load sessions...");

            string directoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Lyceum");

            if (Directory.Exists(directoryPath))
            {
                foreach (string file in Directory.EnumerateFiles(directoryPath, "*.lyc"))
                {
                    LogManager.AppendLog($"Loading session from file: {file}");

                    try
                    {
                        var encryptedData = File.ReadAllText(file);
                        LogManager.AppendLog($"Read encrypted data: {GetAbbreviatedData(encryptedData)}");

                        if (!Cryptography.IsValidBase64String(encryptedData))
                        {
                            LogManager.AppendLog($"Invalid Base-64 string in file: {file}");
                            continue;
                        }

                        var jsonData = Cryptography.DecryptString(encryptionKey, encryptedData);
                        if (jsonData == null)
                        {
                            LogManager.AppendLog("Failed to decrypt data or invalid JSON format.");
                            continue;
                        }

                        this.Invoke(new MethodInvoker(() =>
                        {
                            var sessionData = JsonConvert.DeserializeObject<ProjectSession>(jsonData);
                            if (sessionData != null)
                            {
                                sessions.Add(sessionData);
                                sessionsListBox.Items.Add(sessionData.Title);
                            }
                            else
                            {
                                LogManager.AppendLog($"Failed to load session from file '{file}'. Invalid or missing title.");
                            }
                        }));
                    }
                    catch (Exception ex)
                    {
                        LogManager.AppendLog($"Error reading or processing file '{file}': {ex.Message}");
                    }
                }
                LogManager.AppendLog("Sessions loaded successfully.");
            }
            else
            {
                LogManager.AppendLog("No Lyceum directory found. Starting with empty sessions.");
            }

            this.Invoke(new MethodInvoker(() => LoadSessions())); // Refresh the list on the UI thread
        }
        

        
        
        
        
        private string ExecuteCommand(string command)
        {
            ProcessStartInfo processStartInfo = new ProcessStartInfo("cmd", "/c " + command)
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            string output;
            using (Process process = Process.Start(processStartInfo))
            {
                using (StreamReader reader = process.StandardOutput)
                {
                    output = reader.ReadToEnd();
                }
            }

            return output;
        }
        private async Task<bool> CheckStaffStatus()
        {
            try
            {
                if (string.IsNullOrEmpty(accessToken))
                {
                    LogManager.AppendLog($"Access token is null or empty. Cannot proceed with verification.");
                    return false;
                }

                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Clear();
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
                    LogManager.AppendLog($"Authorization header set with access token for verification check.");

                    // Send GET request to the user verification endpoint
                    var response = await client.GetAsync("https://api.thelyceum.io/api/account/me/");
                    string content = await response.Content.ReadAsStringAsync();
                    LogManager.AppendLog($"Verification Response Status: {response.StatusCode}");
                    LogManager.AppendLog($"Verification Response Content: {content}");

                    if (response.IsSuccessStatusCode)
                    {
                        var json = JsonConvert.DeserializeObject<Dictionary<string, object>>(content);
                        if (json != null && json.ContainsKey("is_verified"))
                        {
                            bool isVerified = json["is_verified"].ToString().ToLower() == "true";
                            LogManager.AppendLog($"User verified status: {isVerified}");
                            return isVerified;
                        }
                        else
                        {
                            LogManager.AppendLog($"Response JSON does not contain 'is_verified' field.");
                        }
                    }
                    else
                    {
                        LogManager.AppendLog($"Failed to retrieve verification status. Non-success status code received.");
                        LogManager.AppendLog(content);
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.AppendLog($"Exception occurred while checking Lyceum staff status: {ex.Message}");
            }

            LogManager.AppendLog(logTextBox, "User is not verified or failed to retrieve verification status.");
            return false;
        }
        private async Task<bool> IsAuthorizedForDecryption()
        {
            // Only check the verification status without requiring a key.
            bool hasStaffStatus = await CheckStaffStatus();
            return hasStaffStatus;
        }
        public async Task<string> DecryptDataAsync(string encryptedData)
        {
            bool isAuthorized = await IsAuthorizedForDecryption();
            if (!isAuthorized)
            {
                LogManager.AppendLog(logTextBox, "Authorization failed. Cannot decrypt data.");
                return null;
            }

            return Cryptography.DecryptString(systemKey, encryptedData);
        }
        private async void FormSessionManager_Load(object sender, EventArgs e)
        {
            LogManager.AppendLog($"[FormSessionManager] Loading...");

            try
            {
                systemKey = Cryptography.GetOrCreateEncryptionKey();
                if (string.IsNullOrEmpty(systemKey))
                {
                    LogManager.AppendLog($"[FormSessionManager] ❌ ERROR: Encryption key is NULL. Cannot proceed.");
                    DisableFormControls();
                    return;
                }

                LogManager.AppendLog($"[FormSessionManager] ✅ Encryption key retrieved successfully.");

                bool isAuthorized = await IsAuthorizedForDecryption();
                if (!isAuthorized)
                {
                    MessageBox.Show("User not authorized for decryption. Contact Lyceum support.", "Authorization Failed", MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                    DisableFormControls();
                    return;
                }

                LogManager.AppendLog($"[FormSessionManager] ✅ Authorization successful. Loading sessions...");
                await Task.Run(() => LoadSessions());

                // Adjust form position and size based on parent form
                AdjustWindowPosition();
            }
            catch (Exception ex)
            {
                LogManager.AppendLog($"[FormSessionManager] ❌ ERROR: {ex.Message}");
                MessageBox.Show($"An error occurred during form load: {ex.Message}", "Error", MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }
        }

        private void AdjustWindowPosition()
        {
            if (formAudioPrecision8 != null)
            {
                // Get the bounds of the parent form
                Rectangle parentBounds = formAudioPrecision8.Bounds;

                // Get the screen the parent form is on
                Screen parentScreen = Screen.FromControl(formAudioPrecision8);

                // Set the new form size based on available space
                int width = Math.Min(parentBounds.Width, 1400);
                int height = Math.Min(parentBounds.Height, 900);
                this.Size = new Size(width, height);

                // Center the new form relative to the parent
                this.StartPosition = FormStartPosition.Manual;
                this.Location = new Point(
                    parentBounds.Left + (parentBounds.Width - this.Width) / 2,
                    parentBounds.Top + (parentBounds.Height - this.Height) / 2
                );

                LogManager.AppendLog($"[AdjustWindowPosition] Form positioned at: {this.Location}, Size: {this.Size}");
            }
            else
            {
                // If no parent form, use the primary screen
                Screen primaryScreen = Screen.PrimaryScreen;
                this.StartPosition = FormStartPosition.CenterScreen;
                this.Size = new Size(1400, 900);
                LogManager.AppendLog($"[AdjustWindowPosition] No parent form detected. Defaulting to center screen.");
            }
        }


        private void DisableFormControls()
        {
            // Disable all controls except the log text box to allow viewing logs
            if (createNewButton != null)
                createNewButton.Enabled = false;

            if (sessionsListBox != null)
                sessionsListBox.Enabled = false;

            if (resultsTreeView != null)
                resultsTreeView.Enabled = false;

            if (detailsTextBox != null)
                detailsTextBox.Enabled = false;

            if (globalPropertyComboBox != null)
                globalPropertyComboBox.Enabled = false;

            if (resultDetailComboBox != null)
                resultDetailComboBox.Enabled = false;

            // Leave logTextBox enabled to display logs even if authorization fails
            if (logTextBox != null)
            {
                logTextBox.Enabled = true;
                LogManager.AppendLog($"Logs enabled for viewing.");
            }

            LogManager.AppendLog($"Form controls have been disabled except for logs.");
        }


        // Remove this outdated method that references detailsListBox
        private void SessionsListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (sessionsListBox.SelectedIndex != -1)
            {
                string selectedTitle = sessionsListBox.SelectedItem?.ToString();
                if (string.IsNullOrEmpty(selectedTitle))
                {
                    LogManager.AppendLog("❌ ERROR: Selected session title is null or empty.");
                    return;
                }

                var selectedSession = sessions.FirstOrDefault(s => s.Title == selectedTitle);
                if (selectedSession == null)
                {
                    LogManager.AppendLog($"❌ ERROR: No session found with title '{selectedTitle}'.");
                    return;
                }

                try
                {
                    LogManager.AppendLog($"[DEBUG] Selected session: {selectedTitle}");

                    if (string.IsNullOrEmpty(selectedSession.Data))
                    {
                        LogManager.AppendLog($"❌ ERROR: Session '{selectedTitle}' has no data.");
                        return;
                    }

                    var sessionData = JsonConvert.DeserializeObject<SessionData>(selectedSession.Data);
                    if (sessionData == null)
                    {
                        LogManager.AppendLog($"❌ ERROR: JSON deserialization failed for session '{selectedTitle}'.");
                        return;
                    }

                    // Validate Global Properties
                    if (sessionData.GlobalProperties == null || !sessionData.GlobalProperties.Any())
                    {
                        LogManager.AppendLog($"⚠ WARNING: GlobalProperties is NULL or EMPTY in session '{selectedTitle}'.");
                    }

                    // Validate CheckedData
                    if (sessionData.CheckedData == null || !sessionData.CheckedData.Any())
                    {
                        LogManager.AppendLog($"⚠ WARNING: CheckedData is NULL or EMPTY in session '{selectedTitle}'.");
                    }

                    // Populate UI
                    PopulateResultsTreeView(sessionData.CheckedData);
                    LogManager.AppendLog($"✅ Successfully populated ResultsTreeView.");
                }
                catch (JsonReaderException ex)
                {
                    LogManager.AppendLog($"❌ ERROR: JSON deserialization failed: {ex.Message}");
                }
                catch (Exception ex)
                {
                    LogManager.AppendLog($"❌ ERROR: Unexpected exception in session selection: {ex.Message}");
                }
            }

        }

        private void PopulateResultsList(List<CheckedData> checkedData)
        {
            detailsTextBox.Clear();
            foreach (var signalPath in checkedData)
            {
                foreach (var measurement in signalPath.Measurements)
                {
                    foreach (var result in measurement.Results)
                    {
                        string item = $"{signalPath.Name} | {measurement.Name} | {result.Name}";
                        detailsTextBox.AppendText(item + Environment.NewLine);
                    }
                }
            }
        }
        private void DisplayGlobalProperties(Dictionary<string, string> globalProperties)
        {
            detailsTextBox.AppendText($"Global Properties:{Environment.NewLine}");
            foreach (var prop in globalProperties)
            {
                detailsTextBox.AppendText($"{prop.Key}: {prop.Value}{Environment.NewLine}");
            }
        }
        private void DetailsListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (detailsTextBox.SelectedText.Length != 0)
            {
                // Clear existing details before displaying new ones
                detailsTextBox.Clear();

                // Display global properties if available
                var selectedSession = sessions.FirstOrDefault(s => s.Title == sessionsListBox.SelectedItem.ToString());
                if (selectedSession != null)
                {
                    LogManager.AppendLog($"Displaying global properties.");
                    var sessionData = JsonConvert.DeserializeObject<SessionData>(selectedSession.Data);
                    DisplayGlobalProperties(sessionData.GlobalProperties);
                }
                else
                {
                    LogManager.AppendLog($"Selected session is null or global properties are missing.");
                }

                string selectedItem = detailsTextBox.SelectedText.ToString();
                var resultData = FindResultData(selectedItem);

                // Now display result details if available
                if (resultData != null)
                {
                    DisplayResultDetails(resultData);
                    LogResultData(resultData); // Ensure this method is called
                    DisplayGraph(resultData);
                }
                else
                {
                    LogManager.AppendLog($"No result data found for the selected item.");
                }
            }
        }
        private void LogResultData(ResultData resultData)
        {
            LogManager.AppendLog($"Logging data for result: {resultData.Name}");
            LogManager.AppendLog($"ResultValueType: {resultData.ResultValueType}");
            LogManager.AppendLog($"HasXYValues: {resultData.HasXYValues}");
            LogManager.AppendLog($"HasMeterValues: {resultData.HasMeterValues}");

            if (resultData.ResultValueType == "XY Values")
            {
                LogManager.AppendLog($"XY Values result type detected.");
                //LogManager.AppendLog($$"X Values: {string.Join(", ", resultData.XValues)}");

            }
            else if (resultData.ResultValueType == "Meter Values")
            {
                LogManager.AppendLog($"Meter Values result type detected.");
                //LogManager.AppendLog($$"Meter Values: {string.Join(", ", resultData.MeterValues)}");
            }
            else
            {
                LogManager.AppendLog($"No values found or unknown result type.");
            }
        }
        // Make sure the LogManager.AppendLog method is as follows:
        
        private void Log(string message)
        {
            if (logTextBox.InvokeRequired)
            {
                logTextBox.Invoke(new Action(() => LogManager.AppendLog(message + Environment.NewLine)));
            }
            else
            {
                LogManager.AppendLog(message + Environment.NewLine);
            }
            Console.WriteLine(message); // Also log to the console for debug purposes
        }
        private ResultData FindResultData(string path)
        {
            string[] parts = path.Split('\\');
            if (parts.Length < 3)
            {
                LogManager.AppendLog($"Path does not have enough parts to match a result.");
                return null;
            }

            string signalPathName = parts[0].Trim();
            string measurementName = parts[1].Trim();
            string resultName = parts[2].Trim();

            foreach (var session in sessions)
            {
                var sessionData = JsonConvert.DeserializeObject<SessionData>(session.Data);
                foreach (var signalPath in sessionData.CheckedData)
                {
                    if (signalPath.Name != signalPathName) continue;
                    foreach (var measurement in signalPath.Measurements)
                    {
                        if (measurement.Name != measurementName) continue;
                        foreach (var result in measurement.Results)
                        {
                            if (result.Name == resultName)
                            {
                                LogManager.AppendLog($"Found matching result for {path}.");
                                return result;
                            }
                        }
                    }
                }
            }

            LogManager.AppendLog($"No matching result found for {path}.");
            return null;
        }
        private void DisplayResultDetails(ResultData result)
        {
            detailsTextBox.AppendText(Environment.NewLine);
            // Displaying result details dynamically
            detailsTextBox.AppendText($"Result Details{Environment.NewLine}");
            detailsTextBox.AppendText($"Result Name: {result.Name}{Environment.NewLine}");
            detailsTextBox.AppendText($"Measurement Type: {result.MeasurementType}{Environment.NewLine}");
            detailsTextBox.AppendText($"Result Values Type: {result.ResultValueType}{Environment.NewLine}");
            detailsTextBox.AppendText($"Number of Channels: {result.ChannelCount}{Environment.NewLine}");

            // Convert Pass/Fail status from boolean to "Pass" or "Fail"
            string passFailStatus = result.Passed ? "Pass" : "Fail";
            detailsTextBox.AppendText($"Pass/Fail Status: {passFailStatus}{Environment.NewLine}");

            // Displaying channel status and limits
            detailsTextBox.AppendText("Channel Status and Limits:" + Environment.NewLine);
            foreach (var channelStatus in result.ChannelPassFail)
            {
                string status = channelStatus.Value ? "Pass" : "Fail";
                detailsTextBox.AppendText($"  {channelStatus.Key}: {status}{Environment.NewLine}");
            }

            detailsTextBox.AppendText($"X-Units: {result.XUnit}{Environment.NewLine}");
            detailsTextBox.AppendText($"Y-Units: {result.YUnit}{Environment.NewLine}");
            detailsTextBox.AppendText($"Meter Units: {result.MeterUnit}{Environment.NewLine}");
        }
        private void DisplayGraph(ResultData result)
        {
            LogManager.AppendLog($"Attempting to display graph.");
            graphPanel.Controls.Clear();
            Chart chart = new Chart { Dock = DockStyle.Fill };

            // Set the chart background color to match dark mode
            chart.BackColor = Color.FromArgb(45, 45, 45); // Dark mode background color

            ChartArea chartArea = new ChartArea
            {
                BackColor = Color.FromArgb(45, 45, 45), // Dark mode background color for the chart area
                AxisX =
                {
                    LabelStyle = { ForeColor = Color.White }, // Set axis labels to white
                    MajorGrid = { LineColor = Color.Gray }, // Set grid lines to a gray color
                },
                AxisY =
                {
                    LabelStyle = { ForeColor = Color.White }, // Set axis labels to white
                    MajorGrid = { LineColor = Color.Gray }, // Set grid lines to a gray color
                }
            };

            chart.ChartAreas.Add(chartArea);

            switch (result.ResultValueType)
            {
                case "XY Values":
                    LogManager.AppendLog($"Preparing to display XY graph.");
                    DisplayXYGraph(chart, result);
                    break;
                case "Meter Values":
                    LogManager.AppendLog($"Preparing to display Meter graph.");
                    DisplayMeterGraph(chart, result);
                    break;
                default:
                    LogManager.AppendLog($"Unknown ResultValueType: {result.ResultValueType}");
                    break;
            }

            graphPanel.Controls.Add(chart);
            LogManager.AppendLog($"Graph display updated.");
        }
        private void DisplayXYGraph(Chart chart, ResultData result)
        {
            string deviceId = GetDeviceId();
            string selectedGlobalPropertyValue = GetSelectedGlobalPropertyValue();
            string selectedResultDetailValue = GetSelectedResultDetailValue();

            foreach (var channel in result.YValuesPerChannel)
            {
                string seriesName = $"{deviceId}_{channel.Key}";
                if (!string.IsNullOrEmpty(selectedGlobalPropertyValue))
                    seriesName += $"-{selectedGlobalPropertyValue}";
                if (!string.IsNullOrEmpty(selectedResultDetailValue))
                    seriesName += $"-{selectedResultDetailValue}";

                Series series = new Series
                {
                    Name = seriesName,
                    ChartType = SeriesChartType.Line
                };

                for (int i = 0; i < result.XValues.Length; i++)
                {
                    series.Points.AddXY(result.XValues[i], channel.Value[i]);
                }

                chart.Series.Add(series);
            }
            chart.Legends.Add(new Legend("Legend") { Docking = Docking.Top });
            LogManager.AppendLog($"XY graph displayed.");
        }
        private void DisplayMeterGraph(Chart chart, ResultData result)
        {
            string deviceId = GetDeviceId();
            LogManager.AppendLog($"Displaying Meter graph.");
            Series series = new Series
            {
                Name = $"{deviceId}",
                ChartType = SeriesChartType.Bar
            };

            for (int i = 0; i < result.MeterValues.Length; i++)
            {
                string channelLabel = "Ch" + (i + 1);
                series.Points.Add(new DataPoint(i, result.MeterValues[i])
                {
                    AxisLabel = channelLabel,
                    LegendText = $"{deviceId}_{channelLabel}" // This sets the legend text for each bar
                });
            }

            chart.Series.Add(series);
            chart.Legends.Add(new Legend("Legend") { Docking = Docking.Top });
            LogManager.AppendLog($"Meter graph displayed.");
        }
        private string GetDeviceId()
        {
            var selectedSession = sessions.FirstOrDefault(s => s.Title == sessionsListBox.SelectedItem.ToString());
            if (selectedSession != null)
            {
                var sessionData = JsonConvert.DeserializeObject<SessionData>(selectedSession.Data);
                if (sessionData?.GlobalProperties != null && sessionData.GlobalProperties.ContainsKey("DeviceId"))
                {
                    return sessionData.GlobalProperties["DeviceId"];
                }
            }
            return "NoSessionSelected";
        }
        private void RefreshGraph()
        {
            if (resultsTreeView.SelectedNode != null && resultsTreeView.SelectedNode.Level == 2) // Check if a result node is selected
            {
                string selectedItem = resultsTreeView.SelectedNode.FullPath;
                var resultData = FindResultData(selectedItem);
                if (resultData != null)
                {
                    DisplayGraph(resultData);
                }
            }
        }
        private string GetSelectedGlobalPropertyValue()
        {
            if (globalPropertyComboBox.SelectedItem != null)
            {
                string propName = globalPropertyComboBox.SelectedItem.ToString();
                var selectedSession = sessions.FirstOrDefault(s => s.Title == sessionsListBox.SelectedItem.ToString());
                if (selectedSession != null)
                {
                    var sessionData = JsonConvert.DeserializeObject<SessionData>(selectedSession.Data);
                    var propValue = sessionData.GlobalProperties.GetType().GetProperty(propName)?.GetValue(sessionData.GlobalProperties, null);
                    return propValue?.ToString() ?? "";
                }
            }
            return "";
        }
        private string GetSelectedResultDetailValue()
        {
            if (resultDetailComboBox.SelectedItem != null)
            {
                string detailIdentifier = resultDetailComboBox.SelectedItem.ToString();
                var parts = detailIdentifier.Split('|');
                if (parts.Length < 3)
                {
                    return ""; // Not enough information
                }

                string signalPathName = parts[0].Trim();
                string measurementName = parts[1].Trim();
                string resultName = parts[2].Trim();
                string detailName = parts.Length > 3 ? parts[3].Trim() : null; // Assuming the detail name is the fourth part

                var selectedSession = sessions.FirstOrDefault(s => s.Title == sessionsListBox.SelectedItem.ToString());
                if (selectedSession != null)
                {
                    var sessionData = JsonConvert.DeserializeObject<SessionData>(selectedSession.Data);
                    foreach (var signalPath in sessionData.CheckedData)
                    {
                        if (signalPath.Name != signalPathName) continue;
                        foreach (var measurement in signalPath.Measurements)
                        {
                            if (measurement.Name != measurementName) continue;
                            foreach (var result in measurement.Results)
                            {
                                if (result.Name == resultName)
                                {
                                    // Use reflection to get the value of the property dynamically
                                    var detailProperty = result.GetType().GetProperty(detailName);
                                    if (detailProperty != null)
                                    {
                                        var detailValue = detailProperty.GetValue(result, null);
                                        return detailValue?.ToString() ?? "";
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return "";
        }
        private void UpdateLegendButton_Click(object sender, EventArgs e)
        {
            var chart = graphPanel.Controls.OfType<Chart>().FirstOrDefault();
            if (chart == null)
            {
                LogManager.AppendLog($"No chart found for updating legends.");
                return;
            }

            var selectedSession = sessions.FirstOrDefault(s => s.Title == sessionsListBox.SelectedItem.ToString());
            if (selectedSession == null)
            {
                LogManager.AppendLog($"Selected session not found during legend update.");
                return;
            }

            var sessionData = JsonConvert.DeserializeObject<SessionData>(selectedSession.Data);
            if (sessionData == null)
            {
                LogManager.AppendLog($"Session data is invalid for legend update.");
                return;
            }

            string selectedGlobalPropertyName = globalPropertyComboBox.SelectedItem?.ToString();
            string globalPropertyValue = GetPropertyValue(sessionData.GlobalProperties, selectedGlobalPropertyName);

            string selectedResultDetailName = resultDetailComboBox.SelectedItem?.ToString();
            LogManager.AppendLog($"Updating legends with Global Property: {selectedGlobalPropertyName} = {globalPropertyValue}, and Result Detail: {selectedResultDetailName}");

            foreach (var series in chart.Series)
            {
                var legendText = series.Name;

                if (!string.IsNullOrEmpty(globalPropertyValue))
                {
                    legendText += $"-{globalPropertyValue}";
                }

                if (!string.IsNullOrEmpty(selectedResultDetailName))
                {
                    var resultDetailValue = FindResultDetailValue(sessionData, series.Name, selectedResultDetailName);
                    LogManager.AppendLog($"Series: {series.Name}, Detail: {selectedResultDetailName}, Value: {resultDetailValue}");
                    if (!string.IsNullOrEmpty(resultDetailValue))
                    {
                        legendText += $"-{resultDetailValue}";
                    }
                }

                series.LegendText = legendText;
            }

            chart.Invalidate(); // Refresh the chart
        }
        private string FindResultDetailValue(SessionData sessionData, string seriesName, string detailName)
        {
            foreach (var checkedData in sessionData.CheckedData)
            {
                foreach (var measurementData in checkedData.Measurements)
                {
                    foreach (var resultData in measurementData.Results)
                    {
                        if (seriesName.Contains(resultData.Name)) // Adjust this condition as needed
                        {
                            var propInfo = resultData.GetType().GetProperty(detailName);
                            if (propInfo != null)
                            {
                                return propInfo.GetValue(resultData, null)?.ToString();
                            }
                        }
                    }
                }
            }
            return string.Empty;
        }

        // Helper method to find specific result data by series name or identifier within SessionData
        private ResultData FindResultInSessionData(SessionData sessionData, string seriesName)
        {
            foreach (var checkedData in sessionData.CheckedData)
            {
                foreach (var measurementData in checkedData.Measurements)
                {
                    foreach (var resultData in measurementData.Results)
                    {
                        // Adjust this condition based on how your series names are structured
                        if (seriesName.Contains(resultData.Name))
                        {
                            return resultData;
                        }
                    }
                }
            }
            return null;
        }// Helper method to get the specific value of a result detail
         // Helper method to get the specific value of a result detail
        private string GetResultDetailValue(ResultData resultData, string detailName)
        {
            var propInfo = resultData.GetType().GetProperty(detailName);
            return propInfo != null ? propInfo.GetValue(resultData, null)?.ToString() : "N/A";
        }
        // Helper method to get the property value by name from an object
        private string GetPropertyValue(object obj, string propName)
        {
            if (obj == null || string.IsNullOrWhiteSpace(propName)) return string.Empty;

            var propInfo = obj.GetType().GetProperty(propName);
            return propInfo != null ? propInfo.GetValue(obj)?.ToString() : string.Empty;
        }
        // Add this method to handle the click event for the Upload button
        private void UploadToLyceumButton_Click(object sender, EventArgs e)
        {
            if (sessionsListBox.SelectedItem == null)
            {
                MessageBox.Show("Please select a session to upload.", "No Session Selected", MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                return;
            }

            string selectedSessionTitle = sessionsListBox.SelectedItem.ToString();
            ProjectSession selectedSession = sessions.FirstOrDefault(session => session.Title == selectedSessionTitle);
            if (selectedSession == null)
            {
                MessageBox.Show("Failed to find the selected session.", "Session Error", MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                return;
            }

            // Use the already decrypted data
            string decryptedData = selectedSession.Data; // Assuming data is already decrypted when selected
            if (string.IsNullOrEmpty(decryptedData))
            {
                MessageBox.Show("No data available to upload.", "Data Error", MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                return;
            }

            // Instantiate your upload form and pass the necessary data
            FormLyceumDataUpload uploadForm = new FormLyceumDataUpload(accessToken, refreshToken, selectedSessionTitle, decryptedData);
            uploadForm.Show();
        }
        // Define classes to deserialize JSON data
        public class SessionData
        {
            public Dictionary<string, string> GlobalProperties { get; set; }
            public List<CheckedData> CheckedData { get; set; }
        }
        public class CheckedData
        {
            public string Name { get; set; }
            public List<MeasurementData> Measurements { get; set; }
        }
        public class MeasurementData
        {
            public string Name { get; set; } = string.Empty;
            public List<ResultData> Results { get; set; } = new List<ResultData>();
            public int Index { get; set; }
            public string MeasurementType { get; set; } = string.Empty;
            public int ChannelCount { get; set; } = 0;
            public string ResultValueType { get; set; }
            public string XUnit { get; set; }
            public string YUnit { get; set; }
            public string MeterUnit { get; set; }
            public bool Passed { get; set; } = false;
            public Dictionary<string, bool> ChannelPassFail { get; set; } = new Dictionary<string, bool>();
            public Dictionary<string, int> ChannelNameToIndexMap { get; set; } = new Dictionary<string, int>();
            public double[] XValues { get; set; }
            public Dictionary<string, double[]> YValuesPerChannel { get; set; } = new Dictionary<string, double[]>();
            public double[] MeterValues { get; set; }
            public bool HasMeterValues { get; set; }
            public bool HasRawTextResults { get; set; }
            public bool HasThieleSmallValues { get; set; }
            public bool HasXYValues { get; set; }
            public bool HasXYYValues { get; set; }
            public bool UpperLimitEnabled { get; set; }
            public bool LowerLimitEnabled { get; set; }
            public double[] MeterLowerLimitValues { get; set; }
            public double[] MeterUpperLimitValues { get; set; }
            public double[] TheileSmallLowerLimitValues { get; set; }
            public double[] TheileSmallUpperLimitValues { get; set; }
            public double[] XValueLowerLimitValues { get; set; }
            public double[] XValueUpperLimitValues { get; set; }
            public double[] YValueLowerLimitValues { get; set; }
            public double[] YValueUpperLimitValues { get; set; }
            public string SignalPathName { get; set; }
            public string MeasurementName { get; set; }
        }
    }
}
