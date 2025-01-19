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
                    AppendLog(logTextBox, "Received data: " + dataPreview);
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
                    AppendLog(logTextBox, message);
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
                    AppendLog(LogTextBox, "New session added: " + newSession.Title);
                };
            }

            LoadSessions();
            this.Load += FormSessionManager_Load;
            Console.WriteLine("FormSessionManager - Constructor: formAudioPrecision8 is " + (formAudioPrecision8 != null ? "not null" : "null"));
        }

        public TextBox LogTextBox
        {
            get { return logTextBox; }
        }

        private void HandleNewSession(ProjectSession newSession)
        {
            sessions.Add(newSession);
            LoadSessions();
            AppendLog(LogTextBox, "New session added: " + newSession.Title);
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

            //resultsTreeView.ExpandAll(); // Optionally expand all nodes
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
                    AppendLog(logTextBox, "No result data found for the selected item.");
                }
            }
        }
        void CreateNewButton_Click(object sender, EventArgs e)
        {
            string sessionTitle = PromptForSessionName();
            if (!string.IsNullOrEmpty(sessionTitle))
            {
                if (sessions.Any(s => s.Title == sessionTitle))
                {
                    MessageBox.Show("A session with this name already exists.", "Error", MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                    return;
                }

                if (this.formAudioPrecision8 != null)
                {
                    string jsonData = this.formAudioPrecision8.GetCurrentFormData();

                    try
                    {
                        var tempObject = JsonConvert.DeserializeObject(jsonData); // Test deserialize
                    }
                    catch (JsonReaderException ex)
                    {
                        // Handle or log error
                        AppendLog(logTextBox, "Invalid JSON data: " + ex.Message);
                        return;
                    }

                    var sessionData = new
                    {
                        Title = sessionTitle,
                        Data = jsonData
                    };

                    string sessionDataJson = JsonConvert.SerializeObject(sessionData);
                    string encryptedData = EncryptString(systemKey, sessionDataJson, logTextBox);

                    if (encryptedData.StartsWith("Error in EncryptString"))
                    {
                        AppendLog(logTextBox, encryptedData);
                        return;
                    }

                    // Create new session instance here
                    ProjectSession newSession = new ProjectSession
                    {
                        Title = sessionTitle,
                        Data = encryptedData
                    };

                    // Add the new session to the sessions list
                    sessions.Add(newSession);

                    // Save the session
                    SaveSessionToFile(newSession, systemKey, true); // Save encrypted
                    SaveSessionToFile(newSession, systemKey, false, jsonData); // Save unencrypted

                    // Update sessions list box in the UI
                    sessionsListBox.Items.Add(newSession.Title); // Add this line

                    CreatedSession = newSession;
                    AppendLog(logTextBox, $"Session '{sessionTitle}' created successfully.");
                }
                else
                {
                    MessageBox.Show("FormAudioPrecision8 instance is not available.", "Error", MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                }
            }
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
                AppendLog(logTextBox, $"[SaveSessionToFile] {(encrypted ? "Encrypted" : "Unencrypted")} session '{session.Title}' saved.");
            }
            catch (Exception ex)
            {
                AppendLog(logTextBox, $"Error writing session file '{sessionFileName}': {ex.Message}\n");
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

            AppendLog(logTextBox, $"New project-session '{sessionName}' created and saved to Lyceum directory.\n");
        }
        private void SaveSessionsToFile(string encryptionKey)
        {
            if (!IsValidBase64String(encryptionKey))
            {
                AppendLog(logTextBox, "Encryption key is not a valid Base-64 string.\n");
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
                    AppendLog(logTextBox, $"Abbreviated unencrypted data: {abbreviatedJsonData}");

                    // Saving the unencrypted JSON file
                    File.WriteAllText(jsonFilePath, jsonData);
                    AppendLog(logTextBox, $"Successfully saved unencrypted session file '{jsonFilePath}'.\n");

                    // Start encryption process after saving unencrypted data
                    AppendLog(logTextBox, "Starting encryption process...");
                    var encryptedData = EncryptString(encryptionKey, jsonData, logTextBox);
                    if (encryptedData.StartsWith("Error in EncryptString"))
                    {
                        AppendLog(logTextBox, encryptedData + "\n");
                        continue;
                    }

                    // Log encrypted data
                    string abbreviatedEncryptedData = encryptedData.Substring(0, Math.Min(encryptedData.Length, 100)) + "...";
                    AppendLog(logTextBox, $"Abbreviated encrypted data: {abbreviatedEncryptedData}");

                    // Saving the encrypted data
                    File.WriteAllText(encryptedFilePath, encryptedData);
                    AppendLog(logTextBox, $"Successfully encrypted and saved '{encryptedFilePath}'.\n");
                }
                catch (Exception ex)
                {
                    AppendLog(logTextBox, $"Error writing session files '{sessionFileName}': {ex.Message}\n");
                }
            }
        }
        private void LoadSessions()
        {
            Log("Attempting to load sessions...");

            if (string.IsNullOrEmpty(systemKey))
            {
                Log("Encryption key is null or invalid. Aborting session load.");
                return;
            }

            string directoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Lyceum");

            if (Directory.Exists(directoryPath))
            {
                sessions.Clear(); // Clear existing sessions before loading new ones

                foreach (string file in Directory.EnumerateFiles(directoryPath, "*.lyc"))
                {
                    Log($"Loading session from file: {file}");

                    try
                    {
                        var encryptedData = File.ReadAllText(file);
                        Log($"Read encrypted data: {GetAbbreviatedData(encryptedData)}");

                        if (!IsValidBase64String(encryptedData))
                        {
                            Log($"Invalid Base-64 string in file: {file}");
                            continue;
                        }

                        var jsonData = DecryptString(systemKey, encryptedData, logTextBox);
                        if (jsonData == null)
                        {
                            Log($"Failed to decrypt data for file: {file}");
                            continue;
                        }

                        var sessionData = JsonConvert.DeserializeObject<ProjectSession>(jsonData);
                        if (sessionData != null)
                        {
                            sessions.Add(sessionData);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"Error reading or processing file '{file}': {ex.Message}");
                    }
                }

                Log("Sessions loaded successfully.");
            }
            else
            {
                Log("No Lyceum directory found. Starting with empty sessions.");
            }

            UpdateSessionListBox();
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
            Log("Attempting to load sessions...");

            string directoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Lyceum");

            if (Directory.Exists(directoryPath))
            {
                foreach (string file in Directory.EnumerateFiles(directoryPath, "*.lyc"))
                {
                    Log($"Loading session from file: {file}");

                    try
                    {
                        var encryptedData = File.ReadAllText(file);
                        Log($"Read encrypted data: {GetAbbreviatedData(encryptedData)}");

                        if (!IsValidBase64String(encryptedData))
                        {
                            Log($"Invalid Base-64 string in file: {file}");
                            continue;
                        }

                        var jsonData = DecryptString(encryptionKey, encryptedData, logTextBox);
                        if (jsonData == null)
                        {
                            Log("Failed to decrypt data or invalid JSON format.");
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
                                AppendLog(logTextBox, $"Failed to load session from file '{file}'. Invalid or missing title.");
                            }
                        }));
                    }
                    catch (Exception ex)
                    {
                        Log($"Error reading or processing file '{file}': {ex.Message}");
                    }
                }
                Log("Sessions loaded successfully.");
            }
            else
            {
                Log("No Lyceum directory found. Starting with empty sessions.");
            }

            this.Invoke(new MethodInvoker(() => LoadSessions())); // Refresh the list on the UI thread
        }
        public string GetOrCreateEncryptionKey(TextBox logTextBox)
        {
            // Define the fixed key
            string predefinedKey = "Lyceum2024";

            // Encrypt the predefined key
            string encryptedPredefinedKey = ConvertToSystemKeyFormat(predefinedKey);
            AppendLog(logTextBox, $"Encrypted predefined key: {encryptedPredefinedKey}\n");

            // Retrieve the raw key from the environment variable
            string environmentKey = Environment.GetEnvironmentVariable("LYCEUM_APP_KEY", EnvironmentVariableTarget.Machine);
            if (environmentKey == null)
            {
                AppendLog(logTextBox, "Environment variable 'LYCEUM_APP_KEY' is not set. Aborting key validation.");
                return null;
            }

            // Encrypt the retrieved environment key
            string encryptedEnvironmentKey = ConvertToSystemKeyFormat(environmentKey);
            AppendLog(logTextBox, $"Encrypted environment key: {encryptedEnvironmentKey}\n");

            // Compare the encrypted keys
            if (encryptedPredefinedKey != encryptedEnvironmentKey)
            {
                AppendLog(logTextBox, "Key validation failed. Please ensure the correct key is set in the environment variable.\n");
                return null;
            }

            AppendLog(logTextBox, "Encryption key retrieved and validated successfully.\n");
            return encryptedPredefinedKey; // Return the encrypted version of the predefined key
        }

        private string ConvertToSystemKeyFormat(string key)
        {
            if (string.IsNullOrEmpty(key))
                return null;

            // Encoding the key to a byte array
            byte[] keyBytes = Encoding.UTF8.GetBytes(key);

            // Encrypt the key as a Base64 string
            return Convert.ToBase64String(keyBytes);
        }

        private string GenerateEncryptionKey()
        {
            using (var rng = new RNGCryptoServiceProvider())
            {
                var randomBytes = new byte[32]; // For 256 bits key
                rng.GetBytes(randomBytes);
                return Convert.ToBase64String(randomBytes);
            }
        }
        private void StoreKey(string encryptionKey, string keyLocation)
        {
            try
            {
                Environment.SetEnvironmentVariable(keyLocation, encryptionKey, EnvironmentVariableTarget.Machine);
                AppendLog(logTextBox, $"Stored key in environment variable '{keyLocation}'.\n");
            }
            catch (Exception ex)
            {
                AppendLog(logTextBox, $"Error storing key: {ex.Message}\n");
            }
        }
        private string RetrieveKey(string keyLocation)
        {
            try
            {
                string retrievedKey = Environment.GetEnvironmentVariable(keyLocation, EnvironmentVariableTarget.Machine);
                if (string.IsNullOrEmpty(retrievedKey))
                {
                    AppendLog(logTextBox, $"Environment variable '{keyLocation}' is not set or empty.\n");
                }
                else
                {
                    AppendLog(logTextBox, $"Retrieved key from environment variable '{keyLocation}'.\n");
                }
                return retrievedKey;
            }
            catch (Exception ex)
            {
                AppendLog(logTextBox, $"Error retrieving key: {ex.Message}\n");
                return null;
            }
        }
        private bool KeyExists(string keyLocation)
        {
            var key = Environment.GetEnvironmentVariable(keyLocation, EnvironmentVariableTarget.User);
            return !string.IsNullOrEmpty(key);
        }
        private static string EncryptString(string key, string plainText, TextBox logTextBox)
        {
            AppendLog(logTextBox, "[EncryptString] Encryption process started.");

            byte[] keyBytes = Convert.FromBase64String(key);
            keyBytes = ResizeKey(keyBytes, 32);
            AppendLog(logTextBox, $"Key byte length for encryption: {keyBytes.Length * 8} bits");

            if (keyBytes.Length != 16 && keyBytes.Length != 24 && keyBytes.Length != 32)
            {
                string error = "Error: Invalid key size.";
                Console.WriteLine("FormSessionManager - EncryptString: " + error);
                return error;
            }

            try
            {
                byte[] iv = new byte[16]; // AES block size is 128 bits
                byte[] array;

                using (Aes aes = Aes.Create())
                {
                    aes.Key = keyBytes;
                    aes.IV = iv;

                    ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

                    using (MemoryStream memoryStream = new MemoryStream())
                    {
                        using (CryptoStream cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
                        {
                            using (StreamWriter streamWriter = new StreamWriter(cryptoStream))
                            {
                                streamWriter.Write(plainText);
                            }
                            array = memoryStream.ToArray();
                        }
                    }
                }

                string encryptedString = Convert.ToBase64String(array);
                AppendLog(logTextBox, "Encryption successful.");
                return encryptedString; // Moved inside try block
            }
            catch (Exception ex)
            {
                AppendLog(logTextBox, "[EncryptString] Error during encryption: " + ex.Message);
                return "Error in EncryptString: " + ex.Message;
            }
        }
        private static byte[] ResizeKey(byte[] originalKey, int sizeInBytes)
        {
            if (originalKey.Length == sizeInBytes)
                return originalKey;

            byte[] resizedKey = new byte[sizeInBytes];
            System.Array.Copy(originalKey, resizedKey, Math.Min(originalKey.Length, sizeInBytes));
            return resizedKey;
        }
        public static string DecryptString(string key, string cipherText, TextBox logTextBox)
        {
            AppendLog(logTextBox, "[DecryptString] Starting decryption process...");
            AppendLog(logTextBox, "[DecryptString] Key: " + (key != null ? "Present" : "Null"));
            AppendLog(logTextBox, "[DecryptString] CipherText length: " + (cipherText?.Length ?? 0));

            if (key == null)
            {
                AppendLog(logTextBox, "[DecryptString] Key is null. Aborting decryption.");
                return null;
            }

            // Convert key from Base64 to byte array
            byte[] keyBytes;
            try
            {
                keyBytes = Convert.FromBase64String(key);
            }
            catch (Exception ex)
            {
                AppendLog(logTextBox, "[DecryptString] Error converting key from Base64: " + ex.Message);
                return null;
            }

            keyBytes = ResizeKey(keyBytes, 32);
            AppendLog(logTextBox, "[DecryptString] Key byte length for decryption: " + keyBytes.Length * 8 + " bits");

            if (keyBytes.Length != 16 && keyBytes.Length != 24 && keyBytes.Length != 32)
            {
                AppendLog(logTextBox, "[DecryptString] Error: Invalid key size.");
                return "Error: Invalid key size.";
            }

            try
            {
                byte[] iv = new byte[16];
                byte[] buffer = Convert.FromBase64String(cipherText);

                using (Aes aes = Aes.Create())
                {
                    aes.Key = keyBytes;
                    aes.IV = iv;
                    ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

                    using (MemoryStream memoryStream = new MemoryStream(buffer))
                    {
                        using (CryptoStream cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read))
                        {
                            using (StreamReader streamReader = new StreamReader(cryptoStream))
                            {
                                string result = streamReader.ReadToEnd();
                                AppendLog(logTextBox, "[DecryptString] Decryption successful.");
                                return result;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppendLog(logTextBox, "[DecryptString] Error during decryption: " + ex.Message);
                return null;
            }
        }
        private bool IsValidBase64String(string base64)
        {
            // Log the key for debugging purposes. Remove this in production.
            //AppendLog($"Key being validated: {base64}\n");

            if (string.IsNullOrEmpty(base64) || base64.Length % 4 != 0
               || base64.Contains(" ") || base64.Contains("\t") || base64.Contains("\r") || base64.Contains("\n"))
                return false;

            try
            {
                Convert.FromBase64String(base64);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
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
                    AppendLog(logTextBox, "Access token is null or empty. Cannot proceed with verification.");
                    return false;
                }

                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Clear();
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
                    AppendLog(logTextBox, $"Authorization header set with access token for verification check.");

                    // Send GET request to the user verification endpoint
                    var response = await client.GetAsync("https://api.thelyceum.io/api/account/me/");
                    string content = await response.Content.ReadAsStringAsync();
                    AppendLog(logTextBox, $"Verification Response Status: {response.StatusCode}");
                    AppendLog(logTextBox, $"Verification Response Content: {content}");

                    if (response.IsSuccessStatusCode)
                    {
                        var json = JsonConvert.DeserializeObject<Dictionary<string, object>>(content);
                        if (json != null && json.ContainsKey("is_verified"))
                        {
                            bool isVerified = json["is_verified"].ToString().ToLower() == "true";
                            AppendLog(logTextBox, $"User verified status: {isVerified}");
                            return isVerified;
                        }
                        else
                        {
                            AppendLog(logTextBox, "Response JSON does not contain 'is_verified' field.");
                        }
                    }
                    else
                    {
                        AppendLog(logTextBox, "Failed to retrieve verification status. Non-success status code received.");
                        AppendLog(logTextBox, content);
                    }
                }
            }
            catch (Exception ex)
            {
                AppendLog(logTextBox, $"Exception occurred while checking Lyceum staff status: {ex.Message}");
            }

            AppendLog(logTextBox, "User is not verified or failed to retrieve verification status.");
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
                AppendLog(logTextBox, "Authorization failed. Cannot decrypt data.");
                return null;
            }

            return DecryptString(systemKey, encryptedData, logTextBox);
        }
        private async void FormSessionManager_Load(object sender, EventArgs e)
        {
            AppendLog(logTextBox, "Loading FormSessionManager...\n");

            try
            {
                // Retrieve and validate the encryption key
                systemKey = GetOrCreateEncryptionKey(logTextBox);
                if (string.IsNullOrEmpty(systemKey))
                {
                    AppendLog(logTextBox, "Encryption key could not be retrieved or validated.");
                    DisableFormControls();
                    return;
                }

                AppendLog(logTextBox, "Encryption key retrieved and validated successfully.\n");

                // Perform authorization check for decryption
                bool isAuthorized = await IsAuthorizedForDecryption();
                if (!isAuthorized)
                {
                    MessageBox.Show("User not authorized for decryption. Contact Lyceum support.", "Authorization Failed", MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                    DisableFormControls();
                    return;
                }

                AppendLog(logTextBox, "Authorization successful. Proceeding to load sessions...\n");

                // Load sessions now that the key is ready
                await Task.Run(() => LoadSessions());
            }
            catch (Exception ex)
            {
                AppendLog(logTextBox, $"Error during form load: {ex.Message}\n");
                MessageBox.Show($"An error occurred during form load: {ex.Message}", "Error", MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
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
                AppendLog(logTextBox, "Logs enabled for viewing.");
            }

            AppendLog(logTextBox, "Form controls have been disabled except for logs.");
        }
        private bool IsValidKeySize(string base64Key)
        {
            var keyBytes = Convert.FromBase64String(base64Key);
            return keyBytes.Length == 16 || keyBytes.Length == 24 || keyBytes.Length == 32; // Valid sizes for AES: 128, 192, or 256 bits
        }
        private string GenerateSystemSpecificKey()
        {
            string processorId = ExecuteCommand("wmic cpu get ProcessorId").Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)[1].Trim();
            string motherboardSerial = ExecuteCommand("wmic baseboard get SerialNumber").Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)[1].Trim();
            string biosSerial = ExecuteCommand("wmic bios get SerialNumber").Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)[1].Trim();

            string rawSystemKey = processorId + "-" + motherboardSerial + "-" + biosSerial + "-000001";
            //AppendLog($"Raw system-specific key: {rawSystemKey}\n");

            byte[] keyBytes = Encoding.UTF8.GetBytes(rawSystemKey);
            string encodedSystemKey = Convert.ToBase64String(keyBytes);
            //AppendLog($"Encoded system-specific key: {encodedSystemKey}\n");

            return encodedSystemKey;

        }
        // Remove this outdated method that references detailsListBox
        private void SessionsListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (sessionsListBox.SelectedIndex != -1)
            {
                string selectedTitle = sessionsListBox.SelectedItem.ToString();
                var selectedSession = sessions.FirstOrDefault(s => s.Title == selectedTitle);

                if (selectedSession != null)
                {
                    try
                    {
                        // Deserialize the session data to access its details
                        var sessionData = JsonConvert.DeserializeObject<SessionData>(selectedSession.Data);

                        if (sessionData != null)
                        {
                            PopulateResultsTreeView(sessionData.CheckedData);

                            // Clear existing items in ComboBoxes
                            globalPropertyComboBox.Items.Clear();
                            resultDetailComboBox.Items.Clear();

                            // Populate globalPropertyComboBox with global properties names
                            foreach (var prop in sessionData.GlobalProperties.Keys)
                            {
                                globalPropertyComboBox.Items.Add(prop);
                            }

                            // Populate resultDetailComboBox with ResultData variable names
                            if (sessionData.CheckedData.Any() && sessionData.CheckedData.First().Measurements.Any())
                            {
                                var firstResult = sessionData.CheckedData.First().Measurements.First().Results.FirstOrDefault();
                                if (firstResult != null)
                                {
                                    foreach (var prop in firstResult.GetType().GetProperties())
                                    {
                                        resultDetailComboBox.Items.Add(prop.Name);
                                    }
                                }
                            }
                        }
                        else
                        {
                            AppendLog(logTextBox, "Deserialized session data is null.");
                        }
                    }
                    catch (JsonReaderException ex)
                    {
                        AppendLog(logTextBox, $"Error deserializing session data: {ex.Message}");
                        MessageBox.Show("The selected session contains invalid or corrupted data. Try restarting the session manager.", "Deserialization Error", MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                    }
                    catch (Exception ex)
                    {
                        AppendLog(logTextBox, $"Unexpected error: {ex.Message}");
                        MessageBox.Show("An unexpected error occurred while processing the session.", "Error", MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                    }
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
                    AppendLog(logTextBox, "Displaying global properties.");
                    var sessionData = JsonConvert.DeserializeObject<SessionData>(selectedSession.Data);
                    DisplayGlobalProperties(sessionData.GlobalProperties);
                }
                else
                {
                    AppendLog(logTextBox, "Selected session is null or global properties are missing.");
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
                    AppendLog(logTextBox, "No result data found for the selected item.");
                }
            }
        }
        private void LogResultData(ResultData resultData)
        {
            AppendLog(logTextBox, $"Logging data for result: {resultData.Name}");
            AppendLog(logTextBox, $"ResultValueType: {resultData.ResultValueType}");
            AppendLog(logTextBox, $"HasXYValues: {resultData.HasXYValues}");
            AppendLog(logTextBox, $"HasMeterValues: {resultData.HasMeterValues}");

            if (resultData.ResultValueType == "XY Values")
            {
                AppendLog(logTextBox, "XY Values result type detected.");
                //AppendLog(logTextBox, $"X Values: {string.Join(", ", resultData.XValues)}");

            }
            else if (resultData.ResultValueType == "Meter Values")
            {
                AppendLog(logTextBox, "Meter Values result type detected.");
                //AppendLog(logTextBox, $"Meter Values: {string.Join(", ", resultData.MeterValues)}");
            }
            else
            {
                AppendLog(logTextBox, "No values found or unknown result type.");
            }
        }
        // Make sure the AppendLog method is as follows:
        public static void AppendLog(TextBox logTextBox, string message)
        {
            if (logTextBox == null)
            {
                Console.WriteLine("LogTextBox is null: " + message);
                return;
            }

            if (logTextBox.InvokeRequired)
            {
                logTextBox.Invoke(new Action(() => logTextBox.AppendText(message + Environment.NewLine)));
            }
            else
            {
                logTextBox.AppendText(message + Environment.NewLine);
            }
        }
        private void Log(string message)
        {
            if (logTextBox.InvokeRequired)
            {
                logTextBox.Invoke(new Action(() => logTextBox.AppendText(message + Environment.NewLine)));
            }
            else
            {
                logTextBox.AppendText(message + Environment.NewLine);
            }
            Console.WriteLine(message); // Also log to the console for debug purposes
        }
        private ResultData FindResultData(string path)
        {
            string[] parts = path.Split('\\');
            if (parts.Length < 3)
            {
                AppendLog(logTextBox, "Path does not have enough parts to match a result.");
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
                                AppendLog(logTextBox, $"Found matching result for {path}.");
                                return result;
                            }
                        }
                    }
                }
            }

            AppendLog(logTextBox, $"No matching result found for {path}.");
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
            AppendLog(logTextBox, "Attempting to display graph.");
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
                    AppendLog(logTextBox, "Preparing to display XY graph.");
                    DisplayXYGraph(chart, result);
                    break;
                case "Meter Values":
                    AppendLog(logTextBox, "Preparing to display Meter graph.");
                    DisplayMeterGraph(chart, result);
                    break;
                default:
                    AppendLog(logTextBox, $"Unknown ResultValueType: {result.ResultValueType}");
                    break;
            }

            graphPanel.Controls.Add(chart);
            AppendLog(logTextBox, "Graph display updated.");
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
            AppendLog(logTextBox, "XY graph displayed.");
        }
        private void DisplayMeterGraph(Chart chart, ResultData result)
        {
            string deviceId = GetDeviceId();
            AppendLog(logTextBox, "Displaying Meter graph.");
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
            AppendLog(logTextBox, "Meter graph displayed.");
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
                AppendLog(logTextBox, "No chart found for updating legends.");
                return;
            }

            var selectedSession = sessions.FirstOrDefault(s => s.Title == sessionsListBox.SelectedItem.ToString());
            if (selectedSession == null)
            {
                AppendLog(logTextBox, "Selected session not found during legend update.");
                return;
            }

            var sessionData = JsonConvert.DeserializeObject<SessionData>(selectedSession.Data);
            if (sessionData == null)
            {
                AppendLog(logTextBox, "Session data is invalid for legend update.");
                return;
            }

            string selectedGlobalPropertyName = globalPropertyComboBox.SelectedItem?.ToString();
            string globalPropertyValue = GetPropertyValue(sessionData.GlobalProperties, selectedGlobalPropertyName);

            string selectedResultDetailName = resultDetailComboBox.SelectedItem?.ToString();
            AppendLog(logTextBox, $"Updating legends with Global Property: {selectedGlobalPropertyName} = {globalPropertyValue}, and Result Detail: {selectedResultDetailName}");

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
                    AppendLog(logTextBox, $"Series: {series.Name}, Detail: {selectedResultDetailName}, Value: {resultDetailValue}");
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
