using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using System.Windows.Forms.VisualStyles;


namespace LAPxv8
{
    public partial class FormPinboard : BaseForm
    {
        private static FormPinboard instance;
        public static FormPinboard Instance
        {
            get
            {
                if (instance == null || instance.IsDisposed)
                {
                    instance = new FormPinboard();
                }
                return instance;
            }
        }

        // Static storage for persistent data
        private static Dictionary<string, TreeNode> persistentDataTreeNodes = new Dictionary<string, TreeNode>();
        private static Dictionary<string, TreeNode> persistentLimitsTreeNodes = new Dictionary<string, TreeNode>();
        private static List<GraphForm> openXYGraphs = new List<GraphForm>();
        private static List<GraphForm> openMeterGraphs = new List<GraphForm>();

        private TabControl tabControl;
        private TreeView dataTreeView;

        public TreeView DataTreeView
        {
            get { return dataTreeView; }
        }

        private TreeView limitsTreeView;
        private TextBox logsTextBox; // Logs section
        private Button toggleLogsButton;

        public FormPinboard()
        {
            InitializeComponent();
            InitializePinboard();

            // Load persistent data and limits when the form is initialized
            LoadPersistentData();
        }

        private void InitializePinboard()
        {
            // Initialize TabControl
            tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10),
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White
            };

            // Create "Data" tab
            TabPage dataTab = new TabPage("Data")
            {
                BackColor = Color.FromArgb(45, 45, 45)
            };
            dataTreeView = new TreeView
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10),
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White
            };
            dataTreeView.MouseDown += DataTreeView_MouseDown; // Attach the MouseDown event
            dataTab.Controls.Add(dataTreeView);

            // Create "Limits" tab
            TabPage limitsTab = new TabPage("Limits")
            {
                BackColor = Color.FromArgb(45, 45, 45)
            };
            limitsTreeView = new TreeView
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10),
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White
            };
            limitsTab.Controls.Add(limitsTreeView);

            // Add tabs to TabControl
            tabControl.TabPages.Add(dataTab);
            tabControl.TabPages.Add(limitsTab);

            // Logs section
            logsTextBox = new TextBox
            {
                Multiline = true,
                Dock = DockStyle.Bottom,
                Height = 100,
                BackColor = Color.Black,
                ForeColor = Color.Lime,
                ReadOnly = true,
                Visible = false // Hidden by default
            };
            toggleLogsButton = new Button
            {
                Text = "Show Logs",
                Dock = DockStyle.Bottom,
                Height = 30
            };
            toggleLogsButton.Click += ToggleLogsButton_Click;

            this.Controls.Add(logsTextBox);
            this.Controls.Add(toggleLogsButton);

            // Ensure TabControl is placed correctly in the form's layout
            this.Controls.Add(tabControl);
            tabControl.BringToFront(); // Bring the TabControl to the front to avoid overlapping issues

            // Assign context menu to TreeView nodes using the new method
            dataTreeView.ContextMenuStrip = CreateContextMenu();
            limitsTreeView.ContextMenuStrip = CreateContextMenu();
        }

        private void LoadPersistentData()
        {
            // Load data for Audio Precision
            if (persistentDataTreeNodes != null)
            {
                // Find or create the Audio Precision node
                TreeNode audioPrecisionNode = dataTreeView.Nodes.Cast<TreeNode>().FirstOrDefault(n => n.Text == "Audio Precision");
                if (audioPrecisionNode == null)
                {
                    audioPrecisionNode = new TreeNode("Audio Precision");
                    dataTreeView.Nodes.Add(audioPrecisionNode);
                }

                foreach (var nodeKey in persistentDataTreeNodes.Keys)
                {
                    if (nodeKey.StartsWith("XY Values") || nodeKey.StartsWith("Meter Values"))
                    {
                        string[] parts = nodeKey.Split(':');
                        string resultValueType = parts[0];
                        string projectName = parts[1];

                        TreeNode resultTypeNode = audioPrecisionNode.Nodes.Cast<TreeNode>().FirstOrDefault(n => n.Text == resultValueType);
                        if (resultTypeNode == null)
                        {
                            resultTypeNode = new TreeNode(resultValueType);
                            audioPrecisionNode.Nodes.Add(resultTypeNode);
                        }

                        resultTypeNode.Nodes.Add((TreeNode)persistentDataTreeNodes[nodeKey].Clone());
                    }
                }
            }

            // Load limits data
            if (persistentLimitsTreeNodes != null)
            {
                foreach (var node in persistentLimitsTreeNodes.Values)
                {
                    limitsTreeView.Nodes.Add((TreeNode)node.Clone());
                }
            }
        }

        private ContextMenuStrip CreateContextMenu()
        {
            var contextMenu = new ContextMenuStrip();
            var removeMenuItem = new ToolStripMenuItem("Remove from Pinboard", null, RemoveDataFromPinboard);
            var addToNewGraphMenuItem = new ToolStripMenuItem("Add to New Graph", null, AddBranchToGraph);
            var addToExistingGraphMenuItem = new ToolStripMenuItem("Add to Existing Graph");
            addToExistingGraphMenuItem.DropDownOpening += (s, e) => PopulateExistingGraphsMenu(addToExistingGraphMenuItem, GraphType.XY);

            contextMenu.Items.Add(addToNewGraphMenuItem);
            contextMenu.Items.Add(addToExistingGraphMenuItem);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(removeMenuItem);

            return contextMenu;
        }

        // Updated AddPinnedData method
        public void AddPinnedData(string data, string category, bool isAudioPrecision)
        {
            // Ensure the TabControl and TreeView controls are initialized
            if (dataTreeView == null || limitsTreeView == null)
            {
                MessageBox.Show("TreeView controls are not initialized properly.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            TreeView targetTreeView = isAudioPrecision ? dataTreeView : limitsTreeView;

            TreeNode parentNode;
            if (isAudioPrecision)
            {
                // Find or create the parent node for "Audio Precision"
                parentNode = targetTreeView.Nodes.Cast<TreeNode>().FirstOrDefault(node => node.Text == "Audio Precision");
                if (parentNode == null)
                {
                    parentNode = new TreeNode("Audio Precision");
                    targetTreeView.Nodes.Add(parentNode);
                }

                // Determine the ResultValueType to create a sub-node
                var combinedData = JsonConvert.DeserializeObject<Dictionary<string, object>>(data);
                if (combinedData.ContainsKey("Properties"))
                {
                    var properties = JsonConvert.DeserializeObject<Dictionary<string, string>>(combinedData["Properties"].ToString());
                    string resultValueType = properties.ContainsKey("ResultValueType") ? properties["ResultValueType"] : "Unknown";

                    // Create or find the sub-node for ResultValueType
                    TreeNode resultTypeNode = parentNode.Nodes.Cast<TreeNode>().FirstOrDefault(node => node.Text == resultValueType);
                    if (resultTypeNode == null)
                    {
                        resultTypeNode = new TreeNode(resultValueType);
                        parentNode.Nodes.Add(resultTypeNode);
                    }

                    // Use the "Name" property for the project node name
                    string projectName = properties.ContainsKey("Name") ? properties["Name"] : "Project Entry";

                    // Create the project node and add properties and data under it
                    TreeNode projectNode = new TreeNode(projectName);

                    // Add the "Properties" under the project node
                    TreeNode propertiesNode = new TreeNode("Properties")
                    {
                        Name = "Properties" // Ensure the Name property is set
                    };
                    foreach (var property in properties)
                    {
                        propertiesNode.Nodes.Add(new TreeNode($"{property.Key}: {property.Value}"));
                    }
                    projectNode.Nodes.Add(propertiesNode);

                    // Add the "Data" under the project node based on ResultValueType
                    if (combinedData.ContainsKey("Data"))
                    {
                        TreeNode dataNode = new TreeNode("Data");
                        if (resultValueType == "XY Values")
                        {
                            // Include only channel names without displaying data
                            var resultData = JsonConvert.DeserializeObject<Dictionary<string, object>>(combinedData["Data"].ToString());
                            if (resultData.ContainsKey("YValuesPerChannel"))
                            {
                                var yValuesPerChannel = JsonConvert.DeserializeObject<Dictionary<string, double[]>>(resultData["YValuesPerChannel"].ToString());

                                foreach (var channel in yValuesPerChannel)
                                {
                                    TreeNode channelNode = new TreeNode(channel.Key)
                                    {
                                        Tag = channel.Value // Where channel.Value is a double[] or appropriate data type
                                    };
                                    dataNode.Nodes.Add(channelNode);

                                    // Add context menu for right-click options
                                    // Context menu for TreeView nodes
                                    var contextMenu = new ContextMenuStrip();
                                    var removeMenuItem = new ToolStripMenuItem("Remove from Pinboard", null, RemoveDataFromPinboard);
                                    var addToNewGraphMenuItem = new ToolStripMenuItem("Add to New Graph", null, AddBranchToGraph);
                                    var addToExistingGraphMenuItem = new ToolStripMenuItem("Add to Existing Graph");
                                    addToExistingGraphMenuItem.DropDownOpening += (s, e) => PopulateExistingGraphsMenu(addToExistingGraphMenuItem, GraphType.XY);

                                    contextMenu.Items.Add(addToNewGraphMenuItem);
                                    contextMenu.Items.Add(addToExistingGraphMenuItem);
                                    contextMenu.Items.Add(new ToolStripSeparator());
                                    contextMenu.Items.Add(removeMenuItem);

                                    // Assign context menu to TreeView nodes
                                    channelNode.ContextMenuStrip = contextMenu;

                                    // Enable drag-and-drop to a graph only
                                    channelNode.Tag = channel.Value; // Store the data in the node's Tag property
                                }
                            }
                        }
                        else if (resultValueType == "Meter Values")
                        {
                            // Include only channel names with meter values
                            var resultData = JsonConvert.DeserializeObject<Dictionary<string, object>>(combinedData["Data"].ToString());
                            if (resultData.ContainsKey("MeterValues"))
                            {
                                var meterValues = JsonConvert.DeserializeObject<double[]>(resultData["MeterValues"].ToString());

                                for (int i = 0; i < meterValues.Length; i++)
                                {
                                    TreeNode channelNode = new TreeNode($"CH{i + 1}");
                                    dataNode.Nodes.Add(channelNode);

                                    // Add context menu for right-click options
                                    var contextMenu = new ContextMenuStrip();
                                    var addToNewGraphMenuItem = new ToolStripMenuItem("Add to New Graph", null, (s, e) => AddChannelToGraph($"CH{i + 1}", new double[] { meterValues.ElementAtOrDefault(i) }, GetPropertiesFromNode(channelNode)));

                                    var addToExistingGraphMenuItem = new ToolStripMenuItem("Add to Existing Graph")
                                    {
                                        Tag = new double[] { meterValues.ElementAtOrDefault(i) } // Store the data in the menu item's Tag property
                                    };
                                    addToExistingGraphMenuItem.DropDownOpening += (s, e) => PopulateExistingGraphsMenu(addToExistingGraphMenuItem, GraphType.Meter);
                                    contextMenu.Items.Add(addToNewGraphMenuItem);
                                    contextMenu.Items.Add(addToExistingGraphMenuItem);
                                    channelNode.ContextMenuStrip = contextMenu;

                                    // Enable drag-and-drop to a graph only
                                    channelNode.Tag = new double[] { meterValues.ElementAtOrDefault(i) }; // Store the data in the node's Tag property
                                }
                            }
                        }

                        projectNode.Nodes.Add(dataNode);
                    }

                    resultTypeNode.Nodes.Add(projectNode);
                    resultTypeNode.Expand(); // Automatically expand the type node to show the new data

                    // Log the node structure for debugging
                    LogTreeNode(resultTypeNode);

                    // Save the project node to persistent storage
                    string nodeKey = $"{resultValueType}:{projectName}";
                    if (!persistentDataTreeNodes.ContainsKey(nodeKey))
                    {
                        persistentDataTreeNodes[nodeKey] = (TreeNode)projectNode.Clone();
                    }
                }
            }
            else
            {
                // Non-audio precision data is added directly under the category node
                parentNode = new TreeNode(category);
                targetTreeView.Nodes.Add(parentNode);

                // Handle data addition for non-audio precision
                var combinedData = JsonConvert.DeserializeObject<Dictionary<string, object>>(data);

                // Use the "Name" property for the project node name
                string projectName = combinedData.ContainsKey("Name") ? combinedData["Name"].ToString() : "Project Entry";

                // Create the project node and add properties and data under it
                TreeNode projectNode = new TreeNode(projectName);
                // Set the context menu for the project node
                projectNode.ContextMenuStrip = CreateContextMenu();

                // Add the "Properties" under the project node
                if (combinedData.ContainsKey("Properties"))
                {
                    TreeNode propertiesNode = new TreeNode("Properties")
                    {
                        Name = "Properties" // Ensure the Name property is set
                    };
                    var properties = JsonConvert.DeserializeObject<Dictionary<string, string>>(combinedData["Properties"].ToString());
                    foreach (var property in properties)
                    {
                        propertiesNode.Nodes.Add(new TreeNode($"{property.Key}: {property.Value}"));
                    }
                    projectNode.Nodes.Add(propertiesNode);
                }

                // Add the "Data" under the project node
                if (combinedData.ContainsKey("Data"))
                {
                    TreeNode dataNode = new TreeNode("Data");
                    dataNode.Nodes.Add(new TreeNode(combinedData["Data"].ToString()));
                    projectNode.Nodes.Add(dataNode);
                }

                parentNode.Nodes.Add(projectNode);
                parentNode.Expand(); // Automatically expand the category node to show the new data

                // Save the project node to persistent storage
                string nodeKey = $"{category}:{projectName}";
                if (!persistentLimitsTreeNodes.ContainsKey(nodeKey))
                {
                    persistentLimitsTreeNodes[nodeKey] = (TreeNode)projectNode.Clone();
                }
            }
        }

        private void RemoveDataFromPinboard(object sender, EventArgs e)
        {
            if (dataTreeView.SelectedNode != null)
            {
                dataTreeView.Nodes.Remove(dataTreeView.SelectedNode);
                // Also remove it from the persistent storage if needed
            }
            else if (limitsTreeView.SelectedNode != null)
            {
                limitsTreeView.Nodes.Remove(limitsTreeView.SelectedNode);
                // Also remove it from the persistent storage if needed
            }
        }

        private void AddBranchToGraph(object sender, EventArgs e)
        {
            if (dataTreeView.SelectedNode != null)
            {
                var selectedNode = dataTreeView.SelectedNode;
                double[] channelData = selectedNode.Tag as double[];
                var properties = GetPropertiesFromNode(selectedNode) ?? new Dictionary<string, string>(); // Ensure properties is initialized

                if (channelData != null)
                {
                    var newGraphForm = CreateGraphForm(selectedNode.Text, GraphType.XY);
                    AddChannelToExistingGraph(newGraphForm.Chart, selectedNode.Text, channelData, properties);
                    newGraphForm.Show(); // Display the graph window
                }
                else if (selectedNode.Nodes.Count > 0)
                {
                    var newGraphForm = CreateGraphForm(selectedNode.Text, GraphType.XY);

                    foreach (TreeNode childNode in selectedNode.Nodes)
                    {
                        channelData = childNode.Tag as double[];
                        properties = GetPropertiesFromNode(childNode) ?? new Dictionary<string, string>(); // Ensure properties is initialized
                        if (channelData != null)
                        {
                            AddChannelToExistingGraph(newGraphForm.Chart, childNode.Text, channelData, properties);
                        }
                    }

                    newGraphForm.Show(); // Display the graph window
                }
                else
                {
                    MessageBox.Show("No valid data found in the selected node.", "Invalid Selection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            else
            {
                MessageBox.Show("Please select a valid data node to add to a new graph.", "Invalid Selection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private Dictionary<string, string> GetPropertiesFromNode(TreeNode node)
        {
            var properties = new Dictionary<string, string>();

            // Traverse up the tree to find the nearest "Properties" node
            TreeNode currentNode = node;
            while (currentNode != null)
            {
                var propertiesNode = currentNode.Nodes.Cast<TreeNode>().FirstOrDefault(n => n.Name == "Properties" || n.Text == "Properties");

                if (propertiesNode != null)
                {
                    foreach (TreeNode propertyNode in propertiesNode.Nodes)
                    {
                        var keyValue = propertyNode.Text.Split(new[] { ':' }, 2);
                        if (keyValue.Length == 2)
                        {
                            properties[keyValue[0].Trim()] = keyValue[1].Trim();
                        }
                    }
                    break; // Stop once properties are found
                }

                // Move up one level in the tree
                currentNode = currentNode.Parent;
            }

            if (properties.Count == 0)
            {
                Log("Properties node not found in the selected node or its parents.");
            }

            return properties;
        }

        private TreeNode FindNodeByText(TreeNode rootNode, string text)
        {
            if (rootNode.Text == text || rootNode.Name == text)
            {
                return rootNode;
            }

            foreach (TreeNode childNode in rootNode.Nodes)
            {
                var foundNode = FindNodeByText(childNode, text);
                if (foundNode != null)
                {
                    return foundNode;
                }
            }

            return null;
        }

        // Event handler for showing/hiding the logs
        private void ToggleLogsButton_Click(object sender, EventArgs e)
        {
            logsTextBox.Visible = !logsTextBox.Visible;
            toggleLogsButton.Text = logsTextBox.Visible ? "Hide Logs" : "Show Logs";
        }

        private void LogTreeNode(TreeNode node, string indent = "")
        {
            Log($"{indent}Node: {node.Text} (Name: {node.Name})");
            foreach (TreeNode childNode in node.Nodes)
            {
                LogTreeNode(childNode, indent + "    ");
            }
        }

        // Helper method for logging
        private void Log(string message)
        {
            logsTextBox.AppendText($"{DateTime.Now:HH:mm:ss} - {message}{Environment.NewLine}");
        }

        // In the FormPinboard class

        public void UpdateGraphTitleInPinboard(GraphForm graphForm)
        {
            // Find the graph in the list of open graphs and update its title
            if (openXYGraphs.Contains(graphForm))
            {
                int index = openXYGraphs.IndexOf(graphForm);
                openXYGraphs[index].Text = graphForm.Text;
                Log($"Updated graph title in pinboard for XY graph: {graphForm.Text}");
            }
            else if (openMeterGraphs.Contains(graphForm))
            {
                int index = openMeterGraphs.IndexOf(graphForm);
                openMeterGraphs[index].Text = graphForm.Text;
                Log($"Updated graph title in pinboard for Meter graph: {graphForm.Text}");
            }
            else
            {
                Log($"Graph form not found in the list of open graphs: {graphForm.Text}");
            }
        }

        private void PopulateExistingGraphsMenu(ToolStripMenuItem parentMenuItem, GraphType graphType)
        {
            parentMenuItem.DropDownItems.Clear(); // Clear previous items

            List<GraphForm> targetGraphList = graphType == GraphType.XY ? openXYGraphs : openMeterGraphs;

            foreach (var graph in targetGraphList)
            {
                var graphMenuItem = new ToolStripMenuItem(graph.Text)
                {
                    Tag = new Tuple<double[], Dictionary<string, string>>(dataTreeView.SelectedNode?.Tag as double[], GetPropertiesFromNode(dataTreeView.SelectedNode))
                };

                graphMenuItem.Click += (s, e) =>
                {
                    var selectedGraphMenuItem = s as ToolStripMenuItem;
                    var dataAndProperties = selectedGraphMenuItem.Tag as Tuple<double[], Dictionary<string, string>>;
                    double[] dataToGraph = dataAndProperties.Item1;
                    var properties = dataAndProperties.Item2;

                    if (dataToGraph != null)
                    {
                        Log($"User selected graph '{graph.Text}' for channel '{dataTreeView.SelectedNode.Text}'.");
                        AddChannelToExistingGraph(graph.Chart, dataTreeView.SelectedNode.Text, dataToGraph, properties);
                    }
                    else
                    {
                        Log($"Failed: No data available for channel '{dataTreeView.SelectedNode.Text}'.");
                        MessageBox.Show("No data available for this channel.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                };

                parentMenuItem.DropDownItems.Add(graphMenuItem);
            }

            if (parentMenuItem.DropDownItems.Count == 0)
            {
                parentMenuItem.DropDownItems.Add(new ToolStripMenuItem("No open graphs available") { Enabled = false });
                Log("No open graphs available.");
            }
        }

        // Method to add a channel's data to an existing graph
        private void AddChannelToExistingGraph(Chart chart, string dataNodeTitle, double[] data, Dictionary<string, string> properties)
        {
            Log($"Attempting to add channel {dataNodeTitle} to the graph.");

            if (chart == null)
            {
                Log("Failed: The chart object is null.");
                throw new ArgumentNullException(nameof(chart), "The chart object cannot be null.");
            }

            if (data == null || data.Length == 0)
            {
                Log($"Failed: The data for channel {dataNodeTitle} is either null or empty.");
                MessageBox.Show("The data for this channel is either null or empty.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Replace GetValueOrDefault with a manual key check
            string nameProperty = properties.ContainsKey("Name") ? properties["Name"] : "Unnamed";
            string seriesName = $"{nameProperty} - {dataNodeTitle}";
            int suffix = 1;

            // Ensure unique series name
            while (chart.Series.Any(s => s.Name == seriesName))
            {
                seriesName = $"{nameProperty} - {dataNodeTitle} ({suffix++})";
            }

            // Create and add the series
            var series = new Series
            {
                Name = seriesName,
                ChartType = SeriesChartType.Line,
                Tag = properties // Attach properties to the series tag
            };

            for (int i = 0; i < data.Length; i++)
            {
                series.Points.AddXY(i, data[i]);
            }

            chart.Series.Add(series);
            Log($"Channel {dataNodeTitle} added successfully to the graph as '{seriesName}'.");
        }

        // Event handler for right-click "Add to New Graph"
        private void AddChannelToGraph(string channelName, double[] data, Dictionary<string, string> properties)
        {
            var newGraphForm = CreateGraphForm(channelName, GraphType.XY);
            AddChannelToExistingGraph(newGraphForm.Chart, channelName, data, properties);
        }

        // Helper method to create a graph form
        private GraphForm CreateGraphForm(string title, GraphType graphType)
        {
            var graphForm = new GraphForm(title);
            graphForm.FormClosed += (s, e) =>
            {
                if (graphType == GraphType.XY)
                {
                    openXYGraphs.Remove(graphForm);
                }
                else
                {
                    openMeterGraphs.Remove(graphForm);
                }
            };

            if (graphType == GraphType.XY)
            {
                openXYGraphs.Add(graphForm);
            }
            else
            {
                openMeterGraphs.Add(graphForm);
            }

            graphForm.Show();
            return graphForm;
        }

        // Method to handle the DataTreeView mouse down event
        private void DataTreeView_MouseDown(object sender, MouseEventArgs e)
        {
            TreeView treeView = sender as TreeView;
            TreeNode node = treeView.GetNodeAt(e.X, e.Y);
            if (node != null && e.Button == MouseButtons.Left)
            {
                // You can implement any specific behavior here
                // For example, you might want to select the node or initiate a drag operation
                treeView.SelectedNode = node; // Select the node on mouse down
            }
        }

        public Dictionary<string, string> GetPropertiesForSeries(string seriesName)
        {
            var properties = new Dictionary<string, string>();

            // Iterate through the nodes to find the matching series name
            foreach (TreeNode node in dataTreeView.Nodes)
            {
                var foundNode = FindNodeBySeriesName(node, seriesName);
                if (foundNode != null)
                {
                    Log($"Found node: {foundNode.Text}");

                    // Check for the Properties node within the found node
                    var propertiesNode = foundNode.Nodes.Cast<TreeNode>().FirstOrDefault(n => n.Name == "Properties" || n.Text == "Properties");
                    if (propertiesNode != null)
                    {
                        Log($"Found Properties node under: {foundNode.Text}");

                        // Extract the properties from the Properties node
                        foreach (TreeNode propertyNode in propertiesNode.Nodes)
                        {
                            var keyValue = propertyNode.Text.Split(':');
                            if (keyValue.Length == 2)
                            {
                                properties[keyValue[0].Trim()] = keyValue[1].Trim();
                            }
                        }
                    }
                    else
                    {
                        Log("Properties node not found.");
                    }
                    break;
                }
                else
                {
                    Log($"No matching node found for series: {seriesName}");
                }
            }

            return properties;
        }

        private TreeNode FindNodeBySeriesName(TreeNode rootNode, string seriesName)
        {
            if (rootNode.Text == seriesName)
            {
                return rootNode;
            }

            foreach (TreeNode childNode in rootNode.Nodes)
            {
                var foundNode = FindNodeBySeriesName(childNode, seriesName);
                if (foundNode != null)
                {
                    return foundNode;
                }
            }

            return null;
        }

        public class GraphForm : BaseForm
        {
            public Chart Chart { get; }
            private Title chartTitle;
            private Legend chartLegend;
            public Dictionary<string, Dictionary<string, string>> SeriesProperties { get; private set; } = new Dictionary<string, Dictionary<string, string>>();

            public GraphForm(string title)
            {
                // Set the form properties to match the dark theme
                Text = title;
                Size = new Size(800, 600);
                BackColor = Color.FromArgb(45, 45, 45);
                ForeColor = Color.White;
                Font = new Font("Segoe UI", 10);
                Icon = this.Icon;
                FormBorderStyle = FormBorderStyle.None;

                // Initialize Chart control
                Chart = new Chart
                {
                    Dock = DockStyle.Fill,
                    BackColor = Color.FromArgb(45, 45, 45)
                };

                var chartArea = new ChartArea
                {
                    BackColor = Color.FromArgb(45, 45, 45),
                    AxisX = { LabelStyle = { ForeColor = Color.White }, MajorGrid = { LineColor = Color.Gray } },
                    AxisY = { LabelStyle = { ForeColor = Color.White }, MajorGrid = { LineColor = Color.Gray } }
                };

                Chart.ChartAreas.Add(chartArea);

                chartTitle = new Title(title, Docking.Top, new Font("Segoe UI", 14, FontStyle.Bold), Color.White)
                {
                    BackColor = Color.Transparent,
                    ForeColor = Color.White,
                    IsDockedInsideChartArea = false,
                    Position = new ElementPosition(0, 0, 100, 5),
                    ToolTip = "Double-click to edit title"
                };
                Chart.Titles.Add(chartTitle);

                chartLegend = new Legend
                {
                    Docking = Docking.Right,
                    BackColor = Color.Transparent,
                    ForeColor = Color.White,
                    Title = "Legend",
                    TitleFont = new Font("Segoe UI", 12, FontStyle.Bold),
                    Font = new Font("Segoe UI", 10)
                };
                Chart.Legends.Add(chartLegend);

                Controls.Add(Chart);

                Chart.DoubleClick += Chart_DoubleClick;
                this.Controls.Add(this.MainMenuStrip);
                CreatePropertiesMenu();
            }

            private void Chart_DoubleClick(object sender, EventArgs e)
            {
                string newTitle = Prompt.ShowDialog("Enter new graph title:", "Edit Graph Title", chartTitle.Text);
                if (!string.IsNullOrEmpty(newTitle))
                {
                    chartTitle.Text = newTitle;
                    Text = newTitle;
                    UpdatePinboardGraphTitle();
                }
            }

            public void AddSeriesWithProperties(string seriesName, double[] data, Dictionary<string, string> properties)
            {
                SeriesProperties[seriesName] = properties;
                var series = new Series(seriesName) { ChartType = SeriesChartType.Line };
                for (int i = 0; i < data.Length; i++)
                {
                    series.Points.AddXY(i, data[i]);
                }
                Chart.Series.Add(series);
            }

            private void CreatePropertiesMenu()
            {
                var propertiesMenu = new ToolStripMenuItem("Properties");

                var xAxisMenuItem = new ToolStripMenuItem("X-Axis");
                var xMinMenuItem = new ToolStripMenuItem("Set X Minimum", null, (s, e) => SetAxisRange(AxisName.X, true));
                var xMaxMenuItem = new ToolStripMenuItem("Set X Maximum", null, (s, e) => SetAxisRange(AxisName.X, false));
                var xAutoScaleMenuItem = new ToolStripMenuItem("Auto-Scale X", null, (s, e) => AutoScaleAxis(AxisName.X));
                var xLogScaleMenuItem = new ToolStripMenuItem("Logarithmic X Scale", null, (s, e) => ToggleLogScale(AxisName.X));

                xAxisMenuItem.DropDownItems.Add(xMinMenuItem);
                xAxisMenuItem.DropDownItems.Add(xMaxMenuItem);
                xAxisMenuItem.DropDownItems.Add(xAutoScaleMenuItem);
                xAxisMenuItem.DropDownItems.Add(xLogScaleMenuItem);
                propertiesMenu.DropDownItems.Add(xAxisMenuItem);

                var yAxisMenuItem = new ToolStripMenuItem("Y-Axis");
                var yMinMenuItem = new ToolStripMenuItem("Set Y Minimum", null, (s, e) => SetAxisRange(AxisName.Y, true));
                var yMaxMenuItem = new ToolStripMenuItem("Set Y Maximum", null, (s, e) => SetAxisRange(AxisName.Y, false));
                var yAutoScaleMenuItem = new ToolStripMenuItem("Auto-Scale Y", null, (s, e) => AutoScaleAxis(AxisName.Y));
                var yLogScaleMenuItem = new ToolStripMenuItem("Logarithmic Y Scale", null, (s, e) => ToggleLogScale(AxisName.Y));

                yAxisMenuItem.DropDownItems.Add(yMinMenuItem);
                yAxisMenuItem.DropDownItems.Add(yMaxMenuItem);
                yAxisMenuItem.DropDownItems.Add(yAutoScaleMenuItem);
                yAxisMenuItem.DropDownItems.Add(yLogScaleMenuItem);
                propertiesMenu.DropDownItems.Add(yAxisMenuItem);

                MainMenuStrip.Items.Add(propertiesMenu);

                // Add context menu for the legend
                Chart.MouseDown += Chart_MouseDown;
            }

            private void SetAxisRange(AxisName axis, bool isMin)
            {
                var axisTitle = axis == AxisName.X ? "X" : "Y";
                var rangeType = isMin ? "minimum" : "maximum";
                var input = Prompt.ShowDialog($"Enter new {rangeType} value for {axisTitle}-Axis:", $"Set {axisTitle}-Axis {rangeType}");

                if (double.TryParse(input, out double value))
                {
                    if (axis == AxisName.X)
                    {
                        if (isMin) Chart.ChartAreas[0].AxisX.Minimum = value;
                        else Chart.ChartAreas[0].AxisX.Maximum = value;
                    }
                    else if (axis == AxisName.Y)
                    {
                        if (isMin) Chart.ChartAreas[0].AxisY.Minimum = value;
                        else Chart.ChartAreas[0].AxisY.Maximum = value;
                    }
                }
            }

            private void AutoScaleAxis(AxisName axis)
            {
                if (axis == AxisName.X)
                {
                    Chart.ChartAreas[0].AxisX.Minimum = double.NaN; // Reset to automatic scaling
                    Chart.ChartAreas[0].AxisX.Maximum = double.NaN;
                }
                else if (axis == AxisName.Y)
                {
                    Chart.ChartAreas[0].AxisY.Minimum = double.NaN; // Reset to automatic scaling
                    Chart.ChartAreas[0].AxisY.Maximum = double.NaN;
                }

                Chart.ChartAreas[0].RecalculateAxesScale(); // Ensure the chart is redrawn with the new settings
            }

            private void ToggleLogScale(AxisName axis)
            {
                var axisToToggle = axis == AxisName.X ? Chart.ChartAreas[0].AxisX : Chart.ChartAreas[0].AxisY;

                axisToToggle.IsLogarithmic = !axisToToggle.IsLogarithmic;
            }

            private void Chart_MouseDown(object sender, MouseEventArgs e)
            {
                var hit = Chart.HitTest(e.X, e.Y);
                if (hit.ChartElementType == ChartElementType.LegendItem && e.Button == MouseButtons.Right)
                {
                    var series = hit.Series;
                    if (series != null)
                    {
                        ShowLegendContextMenu(series, e.Location);
                    }
                }
            }

            private void ShowLegendContextMenu(Series series, Point location)
            {
                var contextMenu = new ContextMenuStrip();
                Dictionary<string, string> properties = new Dictionary<string, string>(); // Initialize properties

                // Access the properties from the series' tag (stored in AddChannelToExistingGraph)
                if (series.Tag is Dictionary<string, string> tagProperties)
                {
                    properties = tagProperties;
                    foreach (var property in properties)
                    {
                        var item = new ToolStripMenuItem($"{property.Key}: {property.Value}")
                        {
                            Tag = property
                        };
                        item.Click += (s, e) => AddPropertyToSeriesName(series, property);
                        contextMenu.Items.Add(item);
                    }
                }

                contextMenu.Items.Add(new ToolStripSeparator());

                var renameMenuItem = new ToolStripMenuItem("Rename Series");
                renameMenuItem.Click += (s, e) => RenameSeries(series, properties, series.Name); // Pass the series name here
                contextMenu.Items.Add(renameMenuItem);

                contextMenu.Show(Chart, location);
            }

            private void AddPropertyToSeriesName(Series series, KeyValuePair<string, string> property)
            {
                series.Name += $" - {property.Key}: {property.Value}";
            }

            private void RenameSeries(Series series, Dictionary<string, string> properties, string currentSeriesName)
            {
                var renameDialog = new RenameSeriesDialog(properties, currentSeriesName); // Pass the series name to the dialog
                if (renameDialog.ShowDialog() == DialogResult.OK)
                {
                    if (!string.IsNullOrEmpty(renameDialog.SelectedProperty))
                    {
                        series.Name = renameDialog.SelectedProperty; // Set the series name to the selected property
                    }
                    else
                    {
                        MessageBox.Show("No property selected. Series name not changed.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }

            private enum AxisName
            {
                X,
                Y
            }

            // Method to update the title in the pinboard's graph list
            private void UpdatePinboardGraphTitle()
            {
                FormPinboard.Instance.UpdateGraphTitleInPinboard(this);
            }
        }

        public class RenameSeriesDialog : Form
        {
            public string SelectedProperty { get; private set; }

            public RenameSeriesDialog(Dictionary<string, string> properties, string currentSeriesName)
            {
                Text = "Rename Series";
                Size = new Size(300, 200);

                var propertyList = new CheckedListBox
                {
                    Dock = DockStyle.Fill
                };

                // Add the current series name as an option
                propertyList.Items.Add($"Series Name: {currentSeriesName}");

                // Add the rest of the properties
                foreach (var property in properties)
                {
                    propertyList.Items.Add($"{property.Key}: {property.Value}");
                }

                if (propertyList.Items.Count > 0)
                {
                    propertyList.SetItemChecked(0, true); // Select the first item by default
                }

                var okButton = new Button
                {
                    Text = "OK",
                    Dock = DockStyle.Bottom
                };
                okButton.Click += (s, e) =>
                {
                    var selectedProperties = string.Join(" - ", propertyList.CheckedItems.Cast<string>());
                    if (!string.IsNullOrEmpty(selectedProperties))
                    {
                        SelectedProperty = selectedProperties;
                        DialogResult = DialogResult.OK;
                        Close();
                    }
                    else
                    {
                        MessageBox.Show("Please select at least one property before clicking OK.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                };

                Controls.Add(propertyList);
                Controls.Add(okButton);
            }
        }

        // A simple dialog box for input (could be placed in a utility class)
        public static class Prompt
        {
            public static string ShowDialog(string text, string caption, string defaultText = "")
            {
                Form prompt = new Form()
                {
                    Width = 400,
                    Height = 200,
                    FormBorderStyle = FormBorderStyle.FixedDialog,
                    Text = caption,
                    StartPosition = FormStartPosition.CenterScreen,
                    BackColor = Color.FromArgb(45, 45, 45), // Dark background
                    ForeColor = Color.White // White text color
                };

                Label textLabel = new Label() { Left = 20, Top = 20, Text = text, Width = 360, ForeColor = Color.White };
                TextBox inputBox = new TextBox() { Left = 20, Top = 60, Width = 360, Text = defaultText, BackColor = Color.FromArgb(60, 60, 60), ForeColor = Color.White };
                Button confirmation = new Button() { Text = "Ok", Left = 290, Width = 90, Top = 110, DialogResult = DialogResult.OK, BackColor = Color.FromArgb(85, 160, 140), ForeColor = Color.White };
                confirmation.Click += (sender, e) => { prompt.Close(); };

                prompt.Controls.Add(textLabel);
                prompt.Controls.Add(inputBox);
                prompt.Controls.Add(confirmation);
                prompt.AcceptButton = confirmation;

                return prompt.ShowDialog() == DialogResult.OK ? inputBox.Text : "";
            }
        }

        // Enumeration to differentiate graph types
        private enum GraphType
        {
            XY,
            Meter
        }
    }
}
