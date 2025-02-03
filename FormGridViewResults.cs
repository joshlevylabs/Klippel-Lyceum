using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using static LAPxv8.FormSessionManager;
using static LAPxv8.FormAudioPrecision8;

namespace LAPxv8
{
    public partial class FormGridViewResults : BaseForm
    {
        private SessionData currentSessionData;
        private TextBox txtLog;
        private string systemKey;

        // UI elements
        private TreeView resultsTreeView;
        private ComboBox globalPropertyComboBox;
        private ComboBox resultDetailComboBox;
        private Panel graphPanel;
        private ListBox globalPropertyListBox;
        private ListBox resultDetailListBox;
        private ContextMenuStrip resultsContextMenu;

        public FormGridViewResults(string filePath, string systemKey)
        {
            InitializeComponent();

            this.systemKey = systemKey;

            // Set form properties
            this.BackColor = Color.FromArgb(45, 45, 45);
            this.ForeColor = Color.White;
            this.Font = new Font("Segoe UI", 10);
            this.Size = new Size(1400, 900);
            this.FormBorderStyle = FormBorderStyle.None;
            this.Icon = this.Icon;

            InitializeComponents();

            // Ensure filePath is valid
            if (!File.Exists(filePath))
            {
                LogManager.AppendLog($"❌ ERROR: Provided file path does not exist: {filePath}");
                MessageBox.Show($"Invalid file path: {filePath}", "File Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Read file content
            string fileContent = File.ReadAllText(filePath);

            // Check if file is already decrypted
            if (fileContent.TrimStart().StartsWith("{"))
            {
                LogManager.AppendLog($"✅ Data is already decrypted, skipping decryption.");
                LoadSessionData(filePath, fileContent); // ✅ Now correctly calls the updated method
            }
            else
            {
                LogManager.AppendLog($"📂 Attempting to load and decrypt file...");
                string decryptedData = Cryptography.DecryptString(systemKey, fileContent);

                if (string.IsNullOrEmpty(decryptedData))
                {
                    LogManager.AppendLog($"❌ ERROR: Decryption failed.");
                    MessageBox.Show("Decryption failed. Unable to open session.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                LoadSessionData(filePath, decryptedData); // ✅ Now correctly calls the updated method
            }
        }

        private void InitializeComponents()
        {
            // Remove any existing labels named "titleLabel" if present
            Control titleControl = this.Controls.OfType<Label>().FirstOrDefault(lbl => lbl.Name == "titleLabel");
            if (titleControl != null)
            {
                this.Controls.Remove(titleControl);
            }

            // Remove any control named "Title" to avoid overlap
            Control titleText = this.Controls.OfType<Control>().FirstOrDefault(ctrl => ctrl.Text == "Title");
            if (titleText != null)
            {
                this.Controls.Remove(titleText);
            }

            // Menu strip - Inherited from BaseForm
            this.Controls.Add(this.MainMenuStrip);

            // Add 'View' menu with 'Passing' and 'Failing' options
            var viewMenu = new ToolStripMenuItem("View");
            var passingMenuItem = new ToolStripMenuItem("Passing", null, (s, e) => FilterResults(true));
            var failingMenuItem = new ToolStripMenuItem("Failing", null, (s, e) => FilterResults(false));
            viewMenu.DropDownItems.Add(passingMenuItem);
            viewMenu.DropDownItems.Add(failingMenuItem);
            this.MainMenuStrip.Items.Add(viewMenu);

            // Add 'Pinboard' menu with 'Open' and 'Add Selected Data' options
            var pinboardMenu = new ToolStripMenuItem("Pinboard");
            var openPinboardMenuItem = new ToolStripMenuItem("Open", null, OpenPinboardMenuItem_Click);
            var addSelectedDataMenuItem = new ToolStripMenuItem("Add Selected Data", null, AddSelectedDataMenuItem_Click);
            pinboardMenu.DropDownItems.Add(openPinboardMenuItem);
            pinboardMenu.DropDownItems.Add(addSelectedDataMenuItem);
            this.MainMenuStrip.Items.Add(pinboardMenu);

            // Initialize context menu for TreeView
            resultsContextMenu = new ContextMenuStrip();
            var addToPinboardMenuItem = new ToolStripMenuItem("Add to Pinboard", null, AddSelectedDataMenuItem_Click);
            resultsContextMenu.Items.Add(addToPinboardMenuItem);

            // SplitContainer for Results and Graph
            SplitContainer resultsGraphSplitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                BackColor = Color.FromArgb(45, 45, 45),
                FixedPanel = FixedPanel.None,
                IsSplitterFixed = false,
                BorderStyle = BorderStyle.FixedSingle
            };

            // Set the SplitterDistance to give the Graph panel 3 times the width of the Results panel
            resultsGraphSplitContainer.SplitterDistance = resultsGraphSplitContainer.Width / 4; // 1/4th for results, 3/4th for graph

            this.Controls.Add(resultsGraphSplitContainer);

            // GroupBox for TreeView (Results)
            GroupBox treeViewGroupBox = new GroupBox
            {
                Text = "Results",
                ForeColor = Color.White,
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(45, 45, 45),
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
            resultsGraphSplitContainer.Panel1.Controls.Add(treeViewGroupBox);

            // TreeView for results
            resultsTreeView = new TreeView
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12),
                ContextMenuStrip = resultsContextMenu // Assign the context menu to the TreeView
            };
            resultsTreeView.AfterSelect += ResultsTreeView_AfterSelect;
            resultsTreeView.MouseDown += ResultsTreeView_MouseDown; // Handle right-click events
            treeViewGroupBox.Controls.Add(resultsTreeView);

            // GroupBox for graph display
            GroupBox graphGroupBox = new GroupBox
            {
                Text = "Graph",
                ForeColor = Color.White,
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(45, 45, 45),
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
            resultsGraphSplitContainer.Panel2.Controls.Add(graphGroupBox);

            graphPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(45, 45, 45)
            };
            graphGroupBox.Controls.Add(graphPanel);

            // SplitContainer for Global Properties and Result Details
            SplitContainer splitContainer = new SplitContainer
            {
                Dock = DockStyle.Bottom,
                Height = 200, // Adjust this to control the height of both properties frames
                BackColor = Color.FromArgb(45, 45, 45),
                Orientation = Orientation.Vertical, // This makes the two panels split horizontally
                FixedPanel = FixedPanel.None,
                IsSplitterFixed = false,
                BorderStyle = BorderStyle.FixedSingle
            };

            // Set equal width for both panels
            splitContainer.SplitterDistance = splitContainer.Width / 2;

            this.Controls.Add(splitContainer);

            // GroupBox for Global Properties
            GroupBox globalPropertiesGroupBox = new GroupBox
            {
                Text = "Global Properties",
                ForeColor = Color.White,
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(45, 45, 45),
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
            splitContainer.Panel1.Controls.Add(globalPropertiesGroupBox);

            globalPropertyListBox = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10),
                SelectionMode = SelectionMode.None
            };
            globalPropertiesGroupBox.Controls.Add(globalPropertyListBox);

            // GroupBox for Result Details
            GroupBox resultDetailsGroupBox = new GroupBox
            {
                Text = "Result Details",
                ForeColor = Color.White,
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(45, 45, 45),
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
            splitContainer.Panel2.Controls.Add(resultDetailsGroupBox);

            resultDetailListBox = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10),
                SelectionMode = SelectionMode.None
            };
            resultDetailsGroupBox.Controls.Add(resultDetailListBox);

            // GroupBox for Log TextBox (Initially hidden)
            GroupBox logGroupBox = new GroupBox
            {
                Text = "Logs",
                ForeColor = Color.White,
                Dock = DockStyle.Bottom,
                Height = 120,  // Adjusted height for better fit
                BackColor = Color.FromArgb(45, 45, 45),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Visible = false // Default to hidden
            };
            this.Controls.Add(logGroupBox);

            // Log TextBox
            txtLog = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                Font = new Font("Consolas", 10)
            };
            logGroupBox.Controls.Add(txtLog);

            // Toggle Button for Logs
            Button toggleLogButton = new Button
            {
                Text = "Show Logs",
                Dock = DockStyle.Bottom,
                Height = 30,
                BackColor = Color.FromArgb(85, 160, 140),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 },
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
            toggleLogButton.Click += (s, e) =>
            {
                logGroupBox.Visible = !logGroupBox.Visible;
                toggleLogButton.Text = logGroupBox.Visible ? "Hide Logs" : "Show Logs";
            };
            this.Controls.Add(toggleLogButton);

            // Adjusting the order of controls to prevent overlap
            this.Controls.SetChildIndex(resultsGraphSplitContainer, 0);
            this.Controls.SetChildIndex(splitContainer, 1);
            this.Controls.SetChildIndex(toggleLogButton, 2);
            this.Controls.SetChildIndex(logGroupBox, 3);
            this.Controls.SetChildIndex(this.MainMenuStrip, 4);
        }

        // Method to handle right-click events on the TreeView
        private void ResultsTreeView_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                TreeNode node = resultsTreeView.GetNodeAt(e.X, e.Y);
                if (node != null)
                {
                    resultsTreeView.SelectedNode = node; // Select the node under the cursor
                }
            }
        }

        private void LoadSessionData(string filePath, string decryptedData)
        {
            LogManager.AppendLog($"📂 Attempting to load session data. File: {filePath}, Data Length: {decryptedData.Length}");

            try
            {
                if (string.IsNullOrEmpty(decryptedData))
                {
                    LogManager.AppendLog($"❌ ERROR: Decryption failed or JSON is empty.");
                    MessageBox.Show("Failed to process session data.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                LogManager.AppendLog($"🔍 Full JSON Data:\n{decryptedData}");

                // Deserialize JSON data
                JObject outerJson;
                try
                {
                    outerJson = JsonConvert.DeserializeObject<JObject>(decryptedData);
                }
                catch (JsonException jsonEx)
                {
                    LogManager.AppendLog($"❌ ERROR: JSON Parsing failed - {jsonEx.Message}");
                    MessageBox.Show("Failed to parse session data.", "JSON Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Ensure 'Data' field is properly parsed
                if (outerJson.ContainsKey("Data"))
                {
                    var rawData = outerJson["Data"];

                    if (rawData.Type == JTokenType.String)
                    {
                        try
                        {
                            LogManager.AppendLog($"🔍 Parsing 'Data' as JSON...");
                            outerJson = JsonConvert.DeserializeObject<JObject>(rawData.ToString());
                        }
                        catch (JsonException jsonEx)
                        {
                            LogManager.AppendLog($"❌ ERROR: Failed to parse 'Data' field - {jsonEx.Message}");
                            MessageBox.Show("Session data format is invalid.", "Data Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }
                    }
                    else if (rawData.Type == JTokenType.Object)
                    {
                        outerJson = rawData as JObject;
                    }
                }

                // Ensure 'CheckedData' is present
                if (!outerJson.ContainsKey("CheckedData") || outerJson["CheckedData"] == null)
                {
                    LogManager.AppendLog($"❌ ERROR: 'CheckedData' is missing or null. Available keys: {string.Join(", ", outerJson.Properties().Select(p => p.Name))}");
                    MessageBox.Show("Session data does not contain valid results.", "Data Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Ensure currentSessionData is initialized
                if (currentSessionData == null)
                {
                    LogManager.AppendLog("❌ ERROR: currentSessionData is null before data assignment.");
                    currentSessionData = new SessionData();
                }

                // Deserialize session data into the object
                currentSessionData = outerJson.ToObject<SessionData>();

                LogManager.AppendLog($"✅ Session data loaded successfully!");

                // Update UI
                PopulateGlobalProperties();
                PopulateResultsTreeView(currentSessionData.CheckedData);
            }
            catch (Exception ex)
            {
                LogManager.AppendLog($"❌ ERROR: Unexpected exception - {ex.Message}");
                MessageBox.Show("An unexpected error occurred while loading session data.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void PopulateGlobalProperties()
        {
            if (currentSessionData == null || currentSessionData.GlobalProperties == null)
            {
                LogManager.AppendLog("❌ ERROR: No global properties available.");
                return;
            }

            globalPropertyListBox.Items.Clear();

            foreach (var property in currentSessionData.GlobalProperties)
            {
                globalPropertyListBox.Items.Add($"{property.Key}: {property.Value}");
            }

            LogManager.AppendLog($"✅ Global properties updated. Total properties: {globalPropertyListBox.Items.Count}");
        }


        private void PopulateResultsTreeView(List<CheckedData> checkedDataList)
        {
            if (checkedDataList == null || checkedDataList.Count == 0)
            {
                LogManager.AppendLog("❌ ERROR: No results data found.");
                return;
            }

            resultsTreeView.Nodes.Clear();

            foreach (var signalPath in checkedDataList)
            {
                TreeNode signalPathNode = new TreeNode(signalPath.Name);

                foreach (var measurement in signalPath.Measurements)
                {
                    TreeNode measurementNode = new TreeNode(measurement.Name);

                    foreach (var result in measurement.Results)
                    {
                        TreeNode resultNode = new TreeNode(result.Name);
                        resultNode.Tag = result;  // 🔥 Store result data in the node's Tag property
                        measurementNode.Nodes.Add(resultNode);
                    }
                    signalPathNode.Nodes.Add(measurementNode);
                }
                resultsTreeView.Nodes.Add(signalPathNode);
            }

            LogManager.AppendLog($"✅ ResultsTreeView populated. Total paths: {checkedDataList.Count}");
        }

        private void FilterResults(bool showPassing)
        {
            if (currentSessionData == null || currentSessionData.CheckedData == null) return;

            resultsTreeView.Nodes.Clear();

            foreach (var signalPath in currentSessionData.CheckedData)
            {
                TreeNode signalPathNode = new TreeNode(signalPath.Name);

                foreach (var measurement in signalPath.Measurements)
                {
                    TreeNode measurementNode = new TreeNode(measurement.Name);

                    foreach (var result in measurement.Results.Where(r => r.Passed == showPassing))
                    {
                        TreeNode resultNode = new TreeNode(result.Name);
                        measurementNode.Nodes.Add(resultNode);
                    }

                    if (measurementNode.Nodes.Count > 0)
                    {
                        signalPathNode.Nodes.Add(measurementNode);
                    }
                }

                if (signalPathNode.Nodes.Count > 0)
                {
                    resultsTreeView.Nodes.Add(signalPathNode);
                }
            }
        }

        private void ResultsTreeView_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (e.Node?.Tag is AudioPrecisionResultData resultData)
            {
                string signalPathName = e.Node.Parent?.Parent?.Text ?? "Unknown Signal Path";
                string measurementName = e.Node.Parent?.Text ?? "Unknown Measurement";

                LogManager.AppendLog($"✅ Found result: {resultData.Name}");
                DisplayResultDetails(resultData);
                DisplayGraph(resultData, signalPathName, measurementName);
            }
            else
            {
                LogManager.AppendLog("❌ No result data found in selected node.");
            }
        }


        private void DisplayResultDetails(AudioPrecisionResultData result)
        {
            // Populate result details ListBox
            resultDetailListBox.Items.Clear();
            foreach (var prop in result.GetType().GetProperties())
            {
                resultDetailListBox.Items.Add($"{prop.Name}: {prop.GetValue(result)}");
            }
        }

        private void DisplayGraph(AudioPrecisionResultData result, string signalPathName, string measurementName)
        {
            LogManager.AppendLog("Attempting to display graph.");
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

            // Add title to the chart
            string title = $"{signalPathName} - {measurementName} - {result.Name}";
            chart.Titles.Add(new Title(title, Docking.Top, new Font("Segoe UI", 14, FontStyle.Bold), Color.White));

            switch (result.ResultValueType)
            {
                case "XY Values":
                    LogManager.AppendLog("Preparing to display XY graph.");
                    DisplayXYGraph(chart, result);
                    break;
                case "Meter Values":
                    LogManager.AppendLog("Preparing to display Meter graph.");
                    DisplayMeterGraph(chart, result);
                    break;
                default:
                    LogManager.AppendLog($"Unknown ResultValueType: {result.ResultValueType}");
                    break;
            }

            graphPanel.Controls.Add(chart);
            LogManager.AppendLog("Graph display updated.");
        }

        private void DisplayXYGraph(Chart chart, AudioPrecisionResultData result)
        {
            foreach (var channel in result.YValuesPerChannel)
            {
                Series series = new Series
                {
                    Name = channel.Key,
                    ChartType = SeriesChartType.Line
                };

                for (int i = 0; i < result.XValues.Length; i++)
                {
                    series.Points.AddXY(result.XValues[i], channel.Value[i]);
                }

                chart.Series.Add(series);
            }
            chart.Legends.Add(new Legend("Legend") { Docking = Docking.Top });
        }

        private void DisplayMeterGraph(Chart chart, AudioPrecisionResultData result)
        {
            Series series = new Series
            {
                Name = "Meter Values",
                ChartType = SeriesChartType.Bar
            };

            for (int i = 0; i < result.MeterValues.Length; i++)
            {
                string channelLabel = "Ch" + (i + 1);
                series.Points.Add(new DataPoint(i, result.MeterValues[i])
                {
                    AxisLabel = channelLabel,
                    LegendText = $"Channel {channelLabel}"
                });
            }

            chart.Series.Add(series);
            chart.Legends.Add(new Legend("Legend")
            {
                Docking = Docking.Top
            });
        }

        // Method to find result in session data
        private AudioPrecisionResultData FindResultInSessionData(SessionData sessionData, string path)
        {
            string[] parts = path.Split('\\');
            if (parts.Length < 3)
            {
                LogManager.AppendLog("Path does not have enough parts to match a result.");
                return null;
            }

            string signalPathName = parts[0].Trim();
            string measurementName = parts[1].Trim();
            string resultName = parts[2].Trim();

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

            LogManager.AppendLog($"No matching result found for {path}.");
            return null;
        }

        private string DecryptString(string key, string cipherText)
        {
            LogManager.AppendLog("[DecryptString] Starting decryption process...");

            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(cipherText))
            {
                LogManager.AppendLog("[DecryptString] Key or cipherText is null/empty. Aborting decryption.");
                return null;
            }

            try
            {
                byte[] keyBytes = Convert.FromBase64String(key);
                byte[] iv = new byte[16];
                byte[] buffer = Convert.FromBase64String(cipherText);

                using (Aes aes = Aes.Create())
                {
                    aes.Key = ResizeKey(keyBytes, 32);
                    aes.IV = iv;
                    ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

                    using (MemoryStream memoryStream = new MemoryStream(buffer))
                    {
                        using (CryptoStream cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read))
                        {
                            using (StreamReader streamReader = new StreamReader(cryptoStream))
                            {
                                return streamReader.ReadToEnd();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.AppendLog("[DecryptString] Error during decryption: " + ex.Message);
                return null;
            }
        }

        // Method to open the pinboard when the "Open" menu item is clicked
        private void OpenPinboardMenuItem_Click(object sender, EventArgs e)
        {
            // Open the pinboard form
            FormPinboard.Instance.Show();
        }

        // Method to add selected data to the pinboard when the "Add Selected Data" menu item is clicked
        private void AddSelectedDataMenuItem_Click(object sender, EventArgs e)
        {
            AddSelectedResultToPinboard(resultsTreeView.SelectedNode);
        }
        // Method to handle adding the selected TreeNode to the pinboard
        private void AddSelectedResultToPinboard(TreeNode selectedNode)
        {
            if (selectedNode != null && selectedNode.Level == 2) // Check if the node is a result node
            {
                string selectedResultPath = selectedNode.FullPath;
                var resultData = FindResultInSessionData(currentSessionData, selectedResultPath);

                if (resultData != null)
                {
                    string signalPathName = selectedNode.Parent.Parent.Text;
                    string measurementName = selectedNode.Parent.Text;
                    string resultName = selectedNode.Text;

                    // Prepare the "Properties" section
                    var properties = new Dictionary<string, string>();
                    foreach (var property in currentSessionData.GlobalProperties)
                    {
                        properties.Add(property.Key, property.Value);
                    }

                    foreach (var prop in resultData.GetType().GetProperties())
                    {
                        properties.Add(prop.Name, prop.GetValue(resultData)?.ToString());
                    }

                    // Prepare the "Data" section
                    string data = JsonConvert.SerializeObject(resultData, Formatting.Indented);

                    // Combine "Properties" and "Data"
                    var combinedData = new Dictionary<string, object>
                    {
                        { "Properties", properties },
                        { "Data", data }
                    };

                    // Serialize combined data for display
                    string pinnedData = JsonConvert.SerializeObject(combinedData, Formatting.Indented);

                    // Check if the result is from "Audio Precision" based on the presence of the "APxDir" global variable
                    bool isAudioPrecision = currentSessionData.GlobalProperties.ContainsKey("APxDir");

                    // Send to the Pinboard
                    FormPinboard.Instance.AddPinnedData(pinnedData, "Data", isAudioPrecision);
                    FormPinboard.Instance.Show();
                }
                else
                {
                    MessageBox.Show("No result data found to pin.", "Pin Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            else
            {
                MessageBox.Show("Please select a valid result to pin.", "Pin Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        private void AddSelectedResultsToPinboard(List<TreeNode> selectedNodes)
        {
            if (selectedNodes != null && selectedNodes.Count > 0)
            {
                foreach (var node in selectedNodes)
                {
                    if (node.Level == 2) // Check if the node is a result node
                    {
                        string selectedResultPath = node.FullPath;
                        var resultData = FindResultInSessionData(currentSessionData, selectedResultPath);

                        if (resultData != null)
                        {
                            string signalPathName = node.Parent.Parent.Text;
                            string measurementName = node.Parent.Text;
                            string resultName = node.Text;

                            // Prepare the "Properties" section
                            var properties = new Dictionary<string, string>();
                            foreach (var property in currentSessionData.GlobalProperties)
                            {
                                properties.Add(property.Key, property.Value);
                            }

                            foreach (var prop in resultData.GetType().GetProperties())
                            {
                                properties.Add(prop.Name, prop.GetValue(resultData)?.ToString());
                            }

                            // Prepare the "Data" section
                            string data = JsonConvert.SerializeObject(resultData, Formatting.Indented);

                            // Combine "Properties" and "Data"
                            var combinedData = new Dictionary<string, object>
                            {
                                { "Properties", properties },
                                { "Data", data }
                            };

                            // Serialize combined data for display
                            string pinnedData = JsonConvert.SerializeObject(combinedData, Formatting.Indented);

                            // Check if the result is from "Audio Precision" based on the presence of the "APxDir" global variable
                            bool isAudioPrecision = currentSessionData.GlobalProperties.ContainsKey("APxDir");

                            // Send to the Pinboard
                            FormPinboard.Instance.AddPinnedData(pinnedData, "Data", isAudioPrecision);
                        }
                    }
                }
                FormPinboard.Instance.Show();
            }
            else
            {
                MessageBox.Show("Please select results to pin.", "Pin Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private byte[] ResizeKey(byte[] originalKey, int sizeInBytes)
        {
            if (originalKey.Length == sizeInBytes)
                return originalKey;

            byte[] resizedKey = new byte[sizeInBytes];
            Array.Copy(originalKey, resizedKey, Math.Min(originalKey.Length, sizeInBytes));
            return resizedKey;
        }
    }

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
        public List<AudioPrecisionResultData> Results { get; set; } = new List<AudioPrecisionResultData>();
    }

    public class AudioPrecisionResultData
    {
        public string Name { get; set; } = string.Empty;
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
        public int SignalPathIndex { get; set; }
        public int MeasurementIndex { get; set; }
        public double[] XValues { get; set; }
        public Dictionary<string, double[]> YValuesPerChannel { get; set; } = new Dictionary<string, double[]>();

        public double[] MeterValues { get; set; }
        public bool HasMeterValues { get; set; }
        public bool HasRawTextResults { get; set; }
        public bool HasThieleSmallValues { get; set; }
        public bool HasXYValues { get; set; }
        public bool HasXYYValues { get; set; }
    }
}
