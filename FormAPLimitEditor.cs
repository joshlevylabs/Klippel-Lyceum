using System;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms.DataVisualization.Charting;
using static LAPxv8.FormAudioPrecision8;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;
using AudioPrecision.API;
using System.ComponentModel;
using FormsMessageBoxIcon = System.Windows.Forms.MessageBoxIcon;
using static LAPxv8.FormAPLimitEditor;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Diagnostics;
using static LAPxv8.FormSessionManager;
using System.Reflection;
using DocumentFormat.OpenXml.Math;
using Newtonsoft.Json.Linq;
using System.Runtime.Remoting.Channels;
using DocumentFormat.OpenXml.Vml.Office;
using System.Web;
using System.Runtime.InteropServices;
using Amazon.Runtime.Internal.Util;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Xml.Linq;
using static LAPxv8.FormLyceumDataViewer;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Spreadsheet;
using Amazon.Runtime.Internal;
using DocumentFormat.OpenXml.Presentation;

namespace LAPxv8
{
    public partial class FormAPLimitEditor : Form
    {
        private APx500 APx = new APx500();
        private string accessToken;
        private string refreshToken;
        private List<SignalPathData> checkedData;
        private Panel graphPanel; // Panel to display graphs
        private TableLayoutPanel tableLayoutPanel;
        private TextBox limitDataTextBox;

        // Declare new variables
        private ResultData currentSelectedResult;
        private bool currentIsUpperLimit;
        private Button currentlyEditingButton;
        private HashSet<string> limitEntries;
        // Declare the controls here
        private System.Windows.Forms.CheckBox autoRangeXCheckBox;
        private System.Windows.Forms.CheckBox autoRangeYCheckBox;
        private TextBox xAxisStartTextBox;
        private TextBox xAxisEndTextBox;
        private TextBox yAxisStartTextBox;
        private TextBox yAxisEndTextBox;
        private ComboBox xScaleComboBox;
        private ComboBox yScaleComboBox;
        private Timer updateGraphTimer;

        private DataGridView limitValuesDataGridView;
        private BindingList<MeterValue> meterValuesBindingList;
        private TabControl limitValuesTabControl;
        private Button addButtonBefore;
        private Button addButtonAfter;
        private Dictionary<int, List<MeterValue>> channelData = new Dictionary<int, List<MeterValue>>();
        private Button importLimitButton;
        private Button deleteEntireLimitButton;
        private Button startExeButton;

        private System.Windows.Forms.CheckBox applyToAllCheckBox;
        private Button exportToAPxButton;
        private Button deleteLimitButton;

        public FormAPLimitEditor(List<SignalPathData> data, string accessToken, string refreshToken) : base()
        {
            this.checkedData = data;
            this.accessToken = accessToken;
            this.refreshToken = refreshToken;

            // Set the form's background to grey and text color to white
            this.BackColor = System.Drawing.Color.FromArgb(60, 60, 60); // Grey background for dark mode
            this.ForeColor = System.Drawing.Color.White;
            this.Font = new System.Drawing.Font("Segoe UI", 9);

            // Center the form on the screen
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Size = new Size(1575, 950);
            this.FormBorderStyle = FormBorderStyle.None; // Remove default title bar

            // Set the icon
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string iconPath = Path.Combine(basePath, "Resources", "LAPx.ico");
            if (File.Exists(iconPath))
            {
                this.Icon = new Icon(iconPath);
            }

            // Initialize components
            InitializeCustomTitleBar();
            InitializeMenuStrip();
            InitializeGraphPanel();
            InitializeGraphPreferences();
            InitializeTable();
            InitializeLimitEditorComponents();
            InitializeMeterValuesBindingList();
            InitializeChannelTabs();

            limitValuesTabControl.Selected += new TabControlEventHandler(limitValuesTabControl_Selected);

            // Initialize the timer
            updateGraphTimer = new Timer();
            updateGraphTimer.Interval = 500; // Delay in milliseconds (e.g., 500 ms)
            updateGraphTimer.Tick += UpdateGraphTimer_Tick;

            // Attach the TextChanged event handler for the TextBoxes
            xAxisStartTextBox.TextChanged += TextBox_TextChanged;
            xAxisEndTextBox.TextChanged += TextBox_TextChanged;
            yAxisStartTextBox.TextChanged += TextBox_TextChanged;
            yAxisEndTextBox.TextChanged += TextBox_TextChanged;

            SetupButtonEventHandlers();

            // Apply dark mode styling to all controls
            ApplyDarkModeToControls(this.Controls);
        }
        private void InitializeCustomTitleBar()
        {
            Panel titleBar = new Panel
            {
                Name = "TitleBarPanel", // Name for exclusion
                Dock = DockStyle.Top,
                Height = 30,
                BackColor = System.Drawing.Color.FromArgb(30, 30, 30),
            };

            Label titleLabel = new Label
            {
                Text = "LAPx Application",
                ForeColor = System.Drawing.Color.White,
                Font = new System.Drawing.Font("Segoe UI", 12, FontStyle.Bold),
                AutoSize = false,
                Dock = DockStyle.Left,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0),
                Width = 300
            };

            // Close button
            Button closeButton = new Button
            {
                Text = "X",
                ForeColor = System.Drawing.Color.Red, // Red text color
                BackColor = System.Drawing.Color.FromArgb(255, 0, 0),
                //FlatStyle = FlatStyle.Flat,
                Dock = DockStyle.Right,
                Width = 30
            };
            closeButton.FlatAppearance.BorderSize = 0; // Remove border
            closeButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(100, 0, 0); // Brighter red on hover
            closeButton.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(80, 0, 0); // Darker red on click
            closeButton.UseVisualStyleBackColor = false; // Ensure custom BackColor is respected
            closeButton.Click += (s, e) => this.Close();


            // Drag functionality for the custom title bar
            titleBar.MouseDown += TitleBar_MouseDown;
            titleBar.MouseMove += TitleBar_MouseMove;
            titleBar.MouseUp += TitleBar_MouseUp;

            titleBar.Controls.Add(titleLabel);
            titleBar.Controls.Add(closeButton);
            this.Controls.Add(titleBar);
        }

        // Variables for drag functionality
        private bool dragging = false;
        private Point dragCursorPoint;
        private Point dragFormPoint;

        private void TitleBar_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                dragging = true;
                dragCursorPoint = System.Windows.Forms.Cursor.Position; // Explicit reference
                dragFormPoint = this.Location;
            }
        }
        private void TitleBar_MouseMove(object sender, MouseEventArgs e)
        {
            if (dragging)
            {
                Point diff = Point.Subtract(System.Windows.Forms.Cursor.Position, new Size(dragCursorPoint)); // Explicit reference
                this.Location = Point.Add(dragFormPoint, new Size(diff));
            }
        }
        private void TitleBar_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                dragging = false; // Stop dragging
            }
        }
        private void InitializeMenuStrip()
        {
            // Create a new MenuStrip
            MenuStrip menuStrip = new MenuStrip
            {
                Dock = DockStyle.Top, // Attach to the top of the form
                BackColor = System.Drawing.Color.FromArgb(30, 30, 30), // Dark mode background
                ForeColor = System.Drawing.Color.White, // Text color for menu items
                Font = new System.Drawing.Font("Segoe UI", 10) // Font style
            };

            // Create "File" menu
            ToolStripMenuItem fileMenu = new ToolStripMenuItem("File");

            ToolStripMenuItem downloadLimitFamilyMenuItem = new ToolStripMenuItem("Download Limit Family");
            downloadLimitFamilyMenuItem.Click += DownloadButton_Click; // Link to the DownloadButton_Click method
            fileMenu.DropDownItems.Add(downloadLimitFamilyMenuItem);

            ToolStripMenuItem loadLimitFamilyMenuItem = new ToolStripMenuItem("Load Limit Family");
            loadLimitFamilyMenuItem.Click += (s, e) => ImportJsonFile(); // Link to the ImportJsonFile method
            fileMenu.DropDownItems.Add(loadLimitFamilyMenuItem);

            ToolStripMenuItem exportToAPxMenuItem = new ToolStripMenuItem("Export Family to APx");
            exportToAPxMenuItem.Click += ExportToAPxButton_Click; // Link to the ExportToAPxButton_Click method
            fileMenu.DropDownItems.Add(exportToAPxMenuItem);

            fileMenu.DropDownItems.Add("Exit", null, (s, e) => this.Close());

            // Add the menus to the MenuStrip
            menuStrip.Items.Add(fileMenu);

            // Add the MenuStrip to the form
            this.Controls.Add(menuStrip);

            // Adjust layout so the title bar is below the MenuStrip
            menuStrip.BringToFront();
            menuStrip.Padding = new Padding(2, 2, 2, 2);
        }
        private void ApplyDarkModeToControls(System.Windows.Forms.Control.ControlCollection controls)
        {
            foreach (System.Windows.Forms.Control control in controls) // Explicitly specify System.Windows.Forms.Control
            {
                if (control is TabControl tabControl)
                {
                    tabControl.BackColor = System.Drawing.Color.FromArgb(30, 30, 30);
                    tabControl.ForeColor = System.Drawing.Color.White;
                    foreach (TabPage tabPage in tabControl.TabPages)
                    {
                        ApplyDarkModeToControls(tabPage.Controls);
                    }
                }
                else if (control is DataGridView dataGridView)
                {
                    dataGridView.BackgroundColor = System.Drawing.Color.FromArgb(30, 30, 30);
                    dataGridView.DefaultCellStyle.BackColor = System.Drawing.Color.FromArgb(30, 30, 30);
                    dataGridView.DefaultCellStyle.ForeColor = System.Drawing.Color.White;
                    dataGridView.ColumnHeadersDefaultCellStyle.BackColor = System.Drawing.Color.FromArgb(30, 30, 30);
                    dataGridView.ColumnHeadersDefaultCellStyle.ForeColor = System.Drawing.Color.White;
                }
                else if (control is TextBox textBox)
                {
                    textBox.BackColor = System.Drawing.Color.FromArgb(45, 45, 45);
                    textBox.ForeColor = System.Drawing.Color.White;
                }
                else if (control is Button button)
                {
                    button.BackColor = System.Drawing.Color.FromArgb(60, 60, 60);
                    button.ForeColor = System.Drawing.Color.White;
                    button.FlatStyle = FlatStyle.Flat;
                    button.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(85, 85, 85);
                }
                else if (control is System.Windows.Forms.Control) // Explicitly specify System.Windows.Forms.Control
                {
                    control.BackColor = System.Drawing.Color.FromArgb(30, 30, 30);
                    control.ForeColor = System.Drawing.Color.White;
                }

                if (control.Controls.Count > 0)
                {
                    ApplyDarkModeToControls(control.Controls);
                }
            }
        }
        private void AdjustControlPositions(int offset)
        {
            foreach (System.Windows.Forms.Control control in this.Controls)
            {
                // Skip the title bar itself
                if (control.Name != "TitleBarPanel") // Assuming you name the title bar panel "TitleBarPanel"
                {
                    control.Location = new Point(control.Location.X, control.Location.Y + offset);
                }
            }
        }
        private void InitializeChannelTabs()
        {
            tableLayoutPanel.BackColor = System.Drawing.Color.FromArgb(60, 60, 60); // Set background for the table layout
            limitValuesTabControl.TabPages.Clear();

            foreach (var channelIndex in channelData.Keys)
            {
                TabPage tabPage = new TabPage($"Channel {channelIndex + 1}")
                {
                    BackColor = System.Drawing.Color.FromArgb(45, 45, 45), // Set tab page background
                    ForeColor = System.Drawing.Color.White // Set text color
                };

                DataGridView dataGridView = new DataGridView
                {
                    Dock = DockStyle.Fill,
                    DataSource = new BindingList<MeterValue>(channelData[channelIndex]),
                    AutoGenerateColumns = true,
                    AllowUserToAddRows = false // Adjust based on your requirements
                };

                InitializeDataGridViewForDarkMode(dataGridView); // Apply dark mode styling

                tabPage.Controls.Add(dataGridView);
                limitValuesTabControl.TabPages.Add(tabPage);
            }
        }
        private void InitializeLimitEditorComponents()
        {
            this.dataGridView1 = new DataGridView();

            this.limitValuesTabControl = new TabControl();



            this.dataGridView1.Name = "dataGridView1";  // Ensure the DataGridView has the correct name
            this.dataGridView1.Columns.Add(new DataGridViewTextBoxColumn { Name = "SignalPath", HeaderText = "Signal Path" });
            this.dataGridView1.Columns.Add(new DataGridViewTextBoxColumn { Name = "MeasurementName", HeaderText = "Measurement Name" });
            this.dataGridView1.Columns.Add(new DataGridViewTextBoxColumn { Name = "ResultName", HeaderText = "Result Name" });
            // Add a test column for Measurement Name

            // Add some test data to dataGridView1 for testing purposes


            this.Controls.Add(this.dataGridView1);

            this.Controls.Add(this.limitValuesTabControl);

            this.dataGridView1.Location = new System.Drawing.Point(10, 10); // Set location
            this.dataGridView1.Size = new System.Drawing.Size(300, 200); // Set size


            // Initialize Limit Data TextBox
            limitDataTextBox = new TextBox
            {
                Location = new Point(10, 720),
                Size = new System.Drawing.Size(970, 200),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true
            };
            this.Controls.Add(limitDataTextBox);

            // Initialize Limit Values TabControl
            limitValuesTabControl = new TabControl
            {
                Location = new Point(1185, 270), // Example location, adjust as needed
                Size = new System.Drawing.Size(350, 200) // Example size, adjust as needed
            };
            this.Controls.Add(limitValuesTabControl);

            // Initialize Import Limit Button
            importLimitButton = new Button
            {
                Text = "Import Limit",
                Size = new System.Drawing.Size(120, 30),
                Location = new Point(1185, 560), // Adjust the location
                Visible = true, // Ensure it is visible
                Enabled = false // Grey out the button
            };
            importLimitButton.Click += ImportLimitButton_Click; // The event won't trigger when the button is disabled
            this.Controls.Add(importLimitButton);


            // Initialize "Apply to All" Checkbox
            applyToAllCheckBox = new System.Windows.Forms.CheckBox
            {
                Text = "Apply to All",
                Location = new Point(1320, 490), // Adjust the location as needed
                AutoSize = true,
                Visible = false, // Initially invisible
                Enabled = false, // Grey out the button
                Checked = true
            };
            this.Controls.Add(applyToAllCheckBox);

            // Initialize "Export to APx" Button
            exportToAPxButton = new Button
            {
                Text = "Export to APx",
                Size = new Size(120, 30),
                Location = new Point(importLimitButton.Location.X, importLimitButton.Location.Y + importLimitButton.Height + 10), // Position below the "Import Limit" button
                Visible = false // Initially hidden
            };
            exportToAPxButton.Click += ExportToAPxButton_Click;
            this.Controls.Add(exportToAPxButton);

            // Initialize "Delete Limit" Button independently
            deleteLimitButton = new Button
            {
                Text = "Delete Row",
                Size = new System.Drawing.Size(120, 30),
                Location = new Point(importLimitButton.Location.X, importLimitButton.Location.Y + importLimitButton.Height + exportToAPxButton.Height + 20), // Adjust the location
                Visible = false // Initially hidden
            };
            deleteLimitButton.Click += DeleteLimitButton_Click;
            this.Controls.Add(deleteLimitButton);

            deleteEntireLimitButton = new Button
            {
                Text = "Remove All",
                Size = new System.Drawing.Size(120, 30),
                Location = new Point(importLimitButton.Location.X, importLimitButton.Location.Y + importLimitButton.Height + exportToAPxButton.Height + 60), // Adjust the location
                Visible = false // Initially hidden
            };
            deleteEntireLimitButton.Click += DeleteEntireLimitButton_Click;
            this.Controls.Add(deleteEntireLimitButton);
        }

        private Button currentlySelectedButton;
        private Dictionary<Button, System.Drawing.Color> originalButtonColors = new Dictionary<Button, System.Drawing.Color>();
        private void UpdateButtonColor(Button button, System.Drawing.Color color)
        {
            // Update the color of the button in the dictionary
            if (originalButtonColors.ContainsKey(button))
            {
                originalButtonColors[button] = color;
            }
            else
            {
                originalButtonColors.Add(button, color);
            }
        }
        private void ChangeButtonColor(Button button)
        {
            // Reset the color of the previously selected button
            if (currentlySelectedButton != null)
            {
                currentlySelectedButton.BackColor = originalButtonColors[currentlySelectedButton];
            }

            // Store or update the original color of the button
            UpdateButtonColor(button, button.BackColor);
            UpdateButtonStatesForResult(currentSelectedResult);
            // Set the color of the currently selected button to green
            button.BackColor = System.Drawing.Color.Green;

            // Update the reference to the currently selected button
            currentlySelectedButton = button;
        }
        // Event handler for button clicks
        private void OnButtonClick(object sender, EventArgs e)
        {
            if (sender is Button button)
            {
                UpdateButtonStatesForResult(currentSelectedResult);
                ChangeButtonColor(button);

            }
        }

        // Set up button event handlers
        private void SetupButtonEventHandlers()
        {
            foreach (System.Windows.Forms.Control control in tableLayoutPanel.Controls)
            {
                if (control is Button button)
                {
                    button.Click += OnButtonClick;
                }
            }
        }

        private void ExportToAPx(int signalPathIndex, int measurementIndex, string resultName, bool isUpperLimit)
        {
            try
            {

                if (APx == null)
                {
                    LogToTextBox("APx object is null.");
                    return;
                }

                //APx.ShowMeasurement(signalPathIndex, measurementIndex);
                LogToTextBox($"Measurement shown on APx for SignalPathIndex: {signalPathIndex}, MeasurementIndex: {measurementIndex}.");

                IGraph graph = null;
                foreach (var g in APx.ActiveMeasurement.Graphs)
                {
                    if (g is IGraph currentGraph && currentGraph.Name == resultName)
                    {
                        graph = currentGraph;
                        break;
                    }
                }

                if (graph == null)
                {
                    LogToTextBox($"Graph '{resultName}' does not exist in the collection.");
                    return;
                }

                if (graph.Result == null)
                {
                    LogToTextBox("Graph result is null.");
                    return;
                }

                // Locate the ResultData object corresponding to the graph
                ResultData resultData = FindResultData(signalPathIndex, measurementIndex, resultName);
                //if (resultData.XValueUpperLimitValues.Length != 0 || resultData.XValueLowerLimitValues.Length != 0 || resultData.YValueLowerLimitValues.Length != 0 ||
                //     resultData.YValueUpperLimitValues.Length != 0 || resultData.MeterUnit.Length != 0 )
                //{


                if (resultData == null)
                {
                    LogToTextBox("ResultData object not found.");
                    return;
                }

                if (graph.Result.IsXYGraph)
                {


                    if (resultData.XValueUpperLimitValues.Length != 0 || resultData.XValueLowerLimitValues.Length != 0)
                    {
                        LogToTextBox($"Processing XY Graph for {(isUpperLimit ? "Upper" : "Lower")} Limits.");
                        ProcessXYGraph(graph, resultData, isUpperLimit);

                    }
                }
                else if (graph.Result.IsMeterGraph)
                {
                    LogToTextBox($"Processing Meter Graph for {(isUpperLimit ? "Upper" : "Lower")} Limits.");
                    ProcessMeterGraph(graph, resultData, isUpperLimit);
                }
                else
                {
                    //MessageBox.Show("The selected graph is not an XY or Meter graph.", "Export Error", MessageBoxButtons.OK, FormsMessageBoxIcon.Error);
                    LogToTextBox("Export error: Selected graph is not an XY or Meter graph.");
                }

                if (resultData != null &&
                resultData.XValueUpperLimitValues != null && resultData.XValueLowerLimitValues != null &&
                resultData.XValueUpperLimitValues.Length == 0 && resultData.XValueLowerLimitValues.Length == 0)
                {
                    ProcessXYGraph(graph, resultData, isUpperLimit);
                    UpdateButtonStatesForResult(resultData);
                }

            }
            catch (Exception ex)
            {
                // Update the channel data and currentSelectedResult based on selected points

                // Refresh the DataGridView to reflect the changes
                IGraph graph = null;
                foreach (var g in APx.ActiveMeasurement.Graphs)
                {
                    if (g is IGraph currentGraph && currentGraph.Name == resultName)
                    {
                        graph = currentGraph;
                        break;
                    }
                }

                ResultData resultData = FindResultData(signalPathIndex, measurementIndex, resultName);
                RefreshDataGridView(measurementIndex); // Refresh only the modified channel's DataGridView
                RefreshDataGridViewForAllChannels();
                UpdateLimit();
                DisplayGraph(currentSelectedResult, currentIsUpperLimit);
                ProcessXYGraph(graph, resultData, isUpperLimit);

            }
        }

        private void StartExeButton_Click(object sender, EventArgs e)
        {
            try
            {
                // Specify the path to the executable you want to start
                string exePath = @"C:\Users\davis\source\repos\LycProgram\LycProgram\bin\Debug\LycProgram.exe";

                // Start the process
                Process.Start(exePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while starting the executable: {ex.Message}", "Error", MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }
        }
        private ResultData FindResultData(int signalPathIndex, int measurementIndex, string resultName)
        {
            // Iterate through your data structure to find the ResultData object
            // This is just an example. Modify it according to your data structure
            foreach (var signalPath in checkedData)
            {
                if (signalPath.Index == signalPathIndex)
                {
                    foreach (var measurement in signalPath.Measurements)
                    {
                        if (measurement.Index == measurementIndex)
                        {
                            foreach (var result in measurement.Results)
                            {
                                if (result.Name == resultName)
                                {
                                    return result;
                                }
                            }
                        }
                    }
                }
            }
            return null;
        }
        private void ProcessXYGraph1(IGraph graph, ResultData result, bool isUpperLimit)
        {
            IXYGraph xyGraph = graph.Result.AsXYGraph();
            // Decide which limits to use based on isUpperLimit and apply to all check
            IEnumerable<MeterValue> limitValues = DetermineLimitValues(isUpperLimit);
            
            // Update the button state
            foreach (System.Windows.Forms.Control control in tableLayoutPanel.Controls)
            {
                if (control is Button button && button.Tag is ResultData buttonResultData && buttonResultData == result)
                {
                    Button limitButton = FindLimitButton(result);
                    UpdateSpecificEditButtonForLimit(result, isUpperLimit);
                }
            }

            LogToTextBox("Limits exported successfully to APx.");
        }
        private void ProcessXYGraph(IGraph graph, ResultData result, bool isUpperLimit)
        {
            IXYGraph xyGraph = graph.Result.AsXYGraph();
            // Decide which limits to use based on isUpperLimit and apply to all check
            IEnumerable<MeterValue> limitValues = DetermineLimitValues(isUpperLimit);

            List<double> xValuesList = new List<double>();
            List<double> yValuesList = new List<double>();

            foreach (var meterValue in limitValues)
            {
                xValuesList.Add(meterValue.XValue);
                yValuesList.Add(meterValue.YValue);
            }

            // Ensure X values are sequential
            if (!IsSequential(xValuesList))
            {
                MessageBox.Show("Limit X data values must be sequential.", "Export Error", MessageBoxButtons.OK, FormsMessageBoxIcon.Error);
                LogToTextBox("Export error: Non-sequential X values.");
                return;
            }

            if (isUpperLimit)
            {
                if (xValuesList.Count > 0 && yValuesList.Count > 0)
                {
                    // Further check if the first element of both lists is 0
                    if (xValuesList[0] == 0 && yValuesList[0] == 0)
                    {
                        // Remove the first element from both lists
                        xValuesList.RemoveAt(0);
                        yValuesList.RemoveAt(0);
                    }
                }
                xyGraph.UpperLimit.SetValues(0, xValuesList.ToArray(), yValuesList.ToArray());
            }
            else
            {
                if (xValuesList.Count > 0 && yValuesList.Count > 0)
                {
                    // Further check if the first element of both lists is 0
                    if (xValuesList[0] == 0 && yValuesList[0] == 0)
                    {
                        // Remove the first element from both lists
                        xValuesList.RemoveAt(0);
                        yValuesList.RemoveAt(0);
                    }
                }
                xyGraph.LowerLimit.SetValues(0, xValuesList.ToArray(), yValuesList.ToArray());
            }

            // Update the button state
            foreach (System.Windows.Forms.Control control in tableLayoutPanel.Controls)
            {
                if (control is Button button && button.Tag is ResultData buttonResultData && buttonResultData == result)
                {
                    Button limitButton = FindLimitButton(result);
                    UpdateSpecificEditButtonForLimit(result, isUpperLimit);
                }
            }

            LogToTextBox("Limits exported successfully to APx.");
        }
        private void ProcessMeterGraph(IGraph graph, ResultData result, bool isUpperLimit)
        {

            tableLayoutPanel.BackColor = System.Drawing.Color.FromArgb(60, 60, 60); // Dark grey background

            IMeterGraph meterGraph = graph.Result.AsMeterGraph();

            double[] limitValues = isUpperLimit ? result.MeterUpperLimitValues : result.MeterLowerLimitValues;

            // Check if the limit values are properly set
            if (limitValues == null || limitValues.Length == 0)
            {
                LogToTextBox($"Error: Meter {(isUpperLimit ? "Upper" : "Lower")} Limit Values are null or empty. Skipping export for this graph.");
                return;
            }

            double limitValue = limitValues[0]; // Assuming the first value is used for the entire graph
            if (!(limitValue.ToString() == "NaN"))
            {
                if (isUpperLimit)
                {
                    meterGraph.UpperLimit.SetValue(0, limitValue);
                }
                else
                {
                    meterGraph.LowerLimit.SetValue(0, limitValue);
                }

                // Update the button state
                foreach (System.Windows.Forms.Control control in tableLayoutPanel.Controls)
                {
                    if (control is Button button && button.Tag is ResultData buttonResultData && buttonResultData == result)
                    {
                        Button limitButton = FindLimitButton(result);
                        UpdateSpecificEditButtonForLimit(result, isUpperLimit);
                    }
                }
            }

            LogToTextBox("Meter value limit exported successfully to APx.");
        }
        private Button FindLimitButton(ResultData resultData)
        {
            foreach (System.Windows.Forms.Control control in tableLayoutPanel.Controls)
            {
                if (control is Button button && button.Tag is ResultData buttonResultData && buttonResultData == resultData)
                {
                    return button;
                }
            }
            return null;
        }
        private IEnumerable<MeterValue> DetermineLimitValues(bool isUpperLimit)
        {
            // Assuming channelData is a dictionary with channel index as key and list of MeterValue as value
            if (applyToAllCheckBox.Checked)
            {
                return isUpperLimit ? channelData[0] : channelData[0];
            }
            else
            {
                return isUpperLimit ? channelData.Values.First() : channelData.Values.SelectMany(c => c);
            }
        }
        private void EditButton_Click(object sender, EventArgs e)
        {
            // Assuming this method is called when an 'Edit' button is clicked
            Button editButton = sender as Button;
            if (editButton != null)
            {
                currentlyEditingButton = editButton; // Store the reference to the button
                                                     // Your existing code to initiate editing...
            }
        }
        private void UpdateLimit()
        {
            // This method should be called after a limit is updated
            if (currentlyEditingButton != null && currentSelectedResult != null)
            {
                currentlyEditingButton.Text = "View";
                // Use the same logic as in CreateLimitButton to determine the button color
                currentlyEditingButton.BackColor = currentIsUpperLimit ? (currentSelectedResult.UpperLimitEnabled ? System.Drawing.Color.Red : SystemColors.Control) : (currentSelectedResult.LowerLimitEnabled ? System.Drawing.Color.Blue : SystemColors.Control);

                // Reset the reference
                currentlyEditingButton = null;

                // Update all buttons corresponding to the currentSelectedResult
                UpdateButtonStatesForResult(currentSelectedResult);
                UpdateButtonStatesForResult(currentSelectedResult);
            }
        }
        private bool IsSequential(List<double> values)
        {
            for (int i = 1; i < values.Count; i++)
            {
                if (values[i] < values[i - 1])
                    return false;
            }
            return true;
        }
        private void ExportToAPxButton_Click(object sender, EventArgs e)
        {
            if (currentSelectedResult == null)
            {
                LogToTextBox("No result selected.");
                return;
            }

            int signalPathIndex = currentSelectedResult.SignalPathIndex; // Assuming this property exists
            int measurementIndex = currentSelectedResult.MeasurementIndex; // Assuming this property exists
            string resultName = currentSelectedResult.Name; // Assuming this property exists
            APx.ShowMeasurement(signalPathIndex, measurementIndex);
            ExportToAPx(signalPathIndex, measurementIndex, resultName, !currentIsUpperLimit);
            RefreshDataGridViewForAllChannels();
        }
        private void ExportLimitFamilyButton_Click(object sender, EventArgs e)
        {
            foreach (var signalPathData in checkedData)
            {
                foreach (var measurementData in signalPathData.Measurements)
                {

                    foreach (var resultData in measurementData.Results)
                    {

                        CreateTabsForResultData(resultData, true);
                        UpdateLimitDataTextBox(resultData, true);
                        UpdateLimitDataTextBox(resultData, true);
                        APx.ShowMeasurement(resultData.SignalPathIndex, resultData.MeasurementIndex);
                        ExportToAPx(signalPathData.Index, measurementData.Index, resultData.Name, true);

                    }
                }
            }
            foreach (var signalPathData in checkedData)
            {
                foreach (var measurementData in signalPathData.Measurements)
                {

                    foreach (var resultData in measurementData.Results)
                    {

                        CreateTabsForResultData(resultData, false);
                        UpdateLimitDataTextBox(resultData, false);
                        UpdateLimitDataTextBox(resultData, false);
                        APx.ShowMeasurement(resultData.SignalPathIndex, resultData.MeasurementIndex);
                        ExportToAPx(signalPathData.Index, measurementData.Index, resultData.Name, false);
                    }
                }
            }

        }
        private async void ExportLimitFamilyButton_Click1(object sender, EventArgs e)
        {
            int counter = 1;
            foreach (System.Windows.Forms.Control control in tableLayoutPanel.Controls)
            {

                if (control is Button button && button.Tag is ResultData buttonResultData)
                {

                    currentlyEditingButton = button;
                    // check wheter its meter or xy . then add a check for if ts diviable by row and then check if the resultdata has data in it. 
                    if (buttonResultData.MeterValues != null && buttonResultData.MeterValues.Any())
                    {
                        if (buttonResultData.MeterUpperLimitValues[0] != null && (counter % 2 != 0))
                        {
                            button.Text = "View";
                            button.BackColor = System.Drawing.Color.Red;
                            Debug.WriteLine("meter button upper " + counter);
                            counter++;
                        }
                        else if (buttonResultData.MeterLowerLimitValues[0] != null && (counter % 2 == 0))
                        {
                            button.Text = "View";
                            button.BackColor = System.Drawing.Color.Blue;
                            Debug.WriteLine("meter button lower" + counter);
                            counter++;
                        }
                    }
                    else
                    {
                        if (buttonResultData.XValueUpperLimitValues != null && buttonResultData.XValueUpperLimitValues.Any() && (counter % 2 != 0))
                        {
                            button.Text = "View";
                            button.BackColor = System.Drawing.Color.Red;
                            Debug.WriteLine("XY button upper " + counter);
                            counter++;
                        }
                        else if (buttonResultData.XValueLowerLimitValues != null && buttonResultData.XValueLowerLimitValues.Any() && (counter % 2 == 0))
                        {
                            button.Text = "View";
                            button.BackColor = System.Drawing.Color.Blue;
                            Debug.WriteLine("XY button upper " + counter);
                            counter++;
                        }
                        else
                        {

                            Debug.WriteLine("default button upper " + counter);
                            button.Text = "Edit";
                            button.BackColor = SystemColors.Control;
                            counter++;
                        }

                    }
                }
            }

            var missingMeasurements = missingLimit();
            if (missingMeasurements.Count > 0)
            {
                //MessageBox.Show("Missing measurements:\n" + string.Join("\n", missingMeasurements));
                missingLimitChoice(missingMeasurements);
            }
            else
            {
                MessageBox.Show("All measurements are present.");
                // Proceed with export functionality
            }

        }
        private void missingLimitChoice(List<string> measurements)
        {
            foreach (var measurement in measurements)
            {
                string firstPart = string.Empty;
                string secondPart = string.Empty;
                string thirdPart = string.Empty;
                using (var customMessageBox = new CustomMessageBox($"Measurement: {measurement}\nChoose an action:"))
                {

                    customMessageBox.ShowDialog();

                    string[] parts = measurement.Split('|');

                    if (parts.Length == 3)
                    {
                        firstPart = parts[0];
                        secondPart = parts[1];
                        thirdPart = parts[2];

                        /*Console.WriteLine($"First Part: {firstPart}");
                        Console.WriteLine($"Second Part: {secondPart}");
                        Console.WriteLine($"Third Part: {thirdPart}");*/
                    }
                    else
                    {
                        Console.WriteLine("The input string does not contain exactly three parts separated by '|'.");
                    }

                    switch (customMessageBox.Result)
                    {
                        case CustomMessageBox.CustomDialogResult.Remove:
                            // User chose "Remove"
                            HandleRemove(measurement, firstPart, secondPart, thirdPart);
                            break;

                        case CustomMessageBox.CustomDialogResult.Add:
                            // User chose "Add"
                            HandleAdd(measurement, firstPart, secondPart, thirdPart);
                            break;

                        case CustomMessageBox.CustomDialogResult.Match:
                            // User chose "Match"
                            HandleMatch(measurement, firstPart, secondPart, thirdPart);
                            break;
                    }
                }
            }
        }
        private void HandleRemove(string measurement, string signal, string measure, string result)
        {
            // Implement the remove action here
            Debug.WriteLine($"Discarded measurement: {measurement}");

        }
        private void HandleAdd(string measurements, string signal, string measure, string results)
        {
            Debug.WriteLine($"Added measurement: {measurements}");

            int currentRow = tableLayoutPanel.RowCount;
            InitializeTableadd1(6, signal, measure, results);
        }
        private void HandleMatch(string measurement, string signal, string measure, string result)
        {
            DisplayAndSelectControls(signal, measure, result);
            Debug.WriteLine($"Matched measurement: {measurement}");
        }
        private void ReplaceMeasurement(string replaceMeasurement, string signal, string measure, string result)
        {

            replaceMeasurement = replaceMeasurement.Replace("System.Windows.Forms.Label, Text: ", "").Trim();
            int currentRow = 1;
            string resultText;

            foreach (var signalPath in checkedData)
            {
                foreach (var measurement in signalPath.Measurements)
                {
                    foreach (var results in measurement.Results)
                    {
                        resultText = $"{signalPath.Name} | {measurement.Name} | {results.Name}";
                        if (resultText == replaceMeasurement)
                        {

                            limitEntries.Remove(($"{signal}|{measure}|{result}"));
                            resultText = $"{signalPath.Name}|{measurement.Name}|{results.Name}";
                            limitEntries.Add(resultText);
                            measurement.Results.Remove(results);
                            ResultData item = new ResultData();
                            item.Name = ($"{result}");
                            measurement.Results.Add(item);
                            Debug.WriteLine(results.Name);
                            Debug.WriteLine(item.Name);
                            break;
                        }


                        currentRow++;

                    }
                }
            }
        }
        private void DisplayAndSelectControls(string signal, string measure, string result)
        {
            // Create a new form to display the controls
            Form selectionForm = new Form();
            selectionForm.Text = "Select a Control";
            selectionForm.Size = new System.Drawing.Size(600, 300);

            // Create a ListBox to list all controls in the TableLayoutPanel
            ListBox controlsListBox = new ListBox();
            controlsListBox.Dock = DockStyle.Top;
            controlsListBox.Height = 200;
            tableLayoutPanel.BackColor = System.Drawing.Color.FromArgb(60, 60, 60);

            // Add the names of the controls to the ListBox
            foreach (var control in tableLayoutPanel.Controls)
            {
                string controlString = control.ToString();


                // Add the cleaned control string if it is not one of the excluded types
                if (//!controlString.Contains("System.Windows.Forms.Label") &&
                     !controlString.Contains("View") &&
                     !controlString.Contains("Edit"))
                {
                    controlString = controlString.Replace("System.Windows.Forms.Label, Text: ", "").Trim();
                    controlsListBox.Items.Add(controlString);
                }
            }

            // Create a button to confirm the selection
            Button selectButton = new Button();
            selectButton.Text = "Select";
            selectButton.Dock = DockStyle.Bottom;

            // Handle the button click event
            selectButton.Click += (sender, e) =>
            {
                if (controlsListBox.SelectedItem != null)
                {
                    string selectedControlName = controlsListBox.SelectedItem.ToString();
                    MessageBox.Show($"Selected Control: {selectedControlName}");
                    // Call the handleMatch function with the selected control
                    ReplaceMeasurement(selectedControlName, signal, measure, result);
                    selectionForm.Close();

                }
                else
                {
                    MessageBox.Show("Please select a control.");
                }
            };

            // Add the ListBox and Button to the form
            selectionForm.Controls.Add(controlsListBox);
            selectionForm.Controls.Add(selectButton);

            // Show the form as a dialog
            selectionForm.ShowDialog();

        }

        private DataGridView dataGridView1;
        private List<string> missingLimit()
        {
            // Load limit data from file (replace with actual file loading in your environment)
            string limitDataJson = System.IO.File.ReadAllText(getFile());
            var limitData = JArray.Parse(limitDataJson);

            limitEntries = new HashSet<string>();
            foreach (var item in limitData)
            {
                string signalPath = item["SignalPathName"].ToString();
                string measurementName = item["MeasurementName"].ToString();
                string resultName = item["ResultName"].ToString();
                string entry = $"{signalPath}|{measurementName}|{resultName}";
                limitEntries.Add(entry);
                // Debug.WriteLine("file limit " + entry);
            }


            HashSet<string> DataEntries = new HashSet<string>();
            foreach (var signalPathData in checkedData)
            {
                foreach (var measurementData in signalPathData.Measurements)
                {

                    foreach (var resultData in measurementData.Results)
                    {

                        string entry = (signalPathData.Name.ToString() + "|" + measurementData.Name.ToString() + "|" + resultData.Name.ToString());
                        DataEntries.Add(entry);
                        // Debug.WriteLine("data limit " + entry);
                    }
                }
            }


            List<string> missingEntries = new List<string>();
            foreach (string entry in limitEntries)
            {
                if (!limitEntries.Contains(entry) || !DataEntries.Contains(entry))
                {
                    missingEntries.Add(entry);
                    //Debug.WriteLine(" Missing " + entry);
                }
            }

            return missingEntries;
        }
        private void CopyDataGridView(DataGridView source, DataGridView destination)
        {
            // Clear destination DataGridView
            destination.Rows.Clear();
            destination.Columns.Clear();

            // Copy columns
            foreach (DataGridViewColumn column in source.Columns)
            {
                destination.Columns.Add((DataGridViewColumn)column.Clone());
            }

            // Copy rows
            foreach (DataGridViewRow row in source.Rows)
            {
                int rowIndex = destination.Rows.Add();
                foreach (DataGridViewCell cell in row.Cells)
                {
                    destination.Rows[rowIndex].Cells[cell.ColumnIndex].Value = cell.Value;
                }
            }
        }
        private void updateimportLimitFamilyButton_Click(object sender, EventArgs e)
        {
            foreach (var signalPathData in checkedData)
            {
                foreach (var measurementData in signalPathData.Measurements)
                {

                    foreach (var resultData in measurementData.Results)
                    {

                        CreateTabsForResultData(resultData, true);
                        UpdateLimitDataTextBox(resultData, true);
                        UpdateLimitDataTextBox(resultData, true);
                        APx.ShowMeasurement(resultData.SignalPathIndex, resultData.MeasurementIndex);
                        ExportToAPx(signalPathData.Index, measurementData.Index, resultData.Name, true);

                    }
                }
            }
            foreach (var signalPathData in checkedData)
            {
                foreach (var measurementData in signalPathData.Measurements)
                {

                    foreach (var resultData in measurementData.Results)
                    {

                        CreateTabsForResultData(resultData, false);
                        UpdateLimitDataTextBox(resultData, false);
                        UpdateLimitDataTextBox(resultData, false);
                        APx.ShowMeasurement(resultData.SignalPathIndex, resultData.MeasurementIndex);
                        ExportToAPx(signalPathData.Index, measurementData.Index, resultData.Name, false);
                    }
                }
            }

        }
        private void InitializeAddPointButtons()
        {

            addButtonBefore = new Button
            {
                Text = "Add Point Before",
                Size = new Size(120, 30),
                Location = new Point(1185, 530) // Adjust the location as needed
            };
            addButtonBefore.Click += AddButtonBefore_Click;
            this.Controls.Add(addButtonBefore);

            addButtonAfter = new Button
            {
                Text = "Add Point After",
                Size = new Size(120, 30),
                Location = new Point(1185, 570) // Adjust the location as needed
            };
            addButtonAfter.Click += AddButtonAfter_Click;
            this.Controls.Add(addButtonAfter);

            // Initially, hide these buttons
            addButtonBefore.Visible = false;
            addButtonAfter.Visible = false;
        }
        private void ImportLimitButton_Click(object sender, EventArgs e)
        {
            LogToTextBox("Import button clicked.");
            FormLyceumLimitImport importForm = new FormLyceumLimitImport(accessToken, refreshToken);
            importForm.LimitImported += ImportForm_LimitImported;
            LogToTextBox("FormLyceumLimitImport LimitImported event subscribed.");
            importForm.ShowDialog(); // Using ShowDialog to make it modal
        }
        private void ImportForm_LimitImported(List<string[]> limitData, bool applyToSpecificChannel, int selectedChannel)
        {
            LogToTextBox("Starting to process imported data.");

            // Check if the result data type is XY Values or Meter Values
            bool isXYValues = currentSelectedResult?.ResultValueType == "XY Values";
            LogToTextBox($"ResultValueType is {(isXYValues ? "XY Values" : "Meter Values")}");

            if (limitData == null || limitData.Count == 0)
            {
                LogToTextBox("Imported data is null or empty.");
                return;
            }
            if (currentSelectedResult == null)
            {
                //LogToTextBox("Current selected result is null.");
                return;
            }

            // When applying to a specific channel, keep the existing data for other channels
            var existingChannelData = new Dictionary<int, List<MeterValue>>(channelData);

            // Clear and then restore the existing data for channels not being updated
            channelData.Clear();

            if (applyToSpecificChannel)
            {
                foreach (var kvp in existingChannelData)
                {
                    if (kvp.Key != selectedChannel - 1) // 0-based index adjustment
                    {
                        channelData[kvp.Key] = kvp.Value;
                    }
                }
            }

            foreach (var row in limitData)
            {
                LogToTextBox($"Processing row: {string.Join(", ", row)}");
                if (isXYValues)
                {
                    if (!ProcessXYValuesRow(row, applyToSpecificChannel, selectedChannel))
                    {
                        LogToTextBox("Row skipped due to format mismatch or channel selection.");
                    }
                }
                else
                {
                    if (!ProcessMeterValuesRow(row, applyToSpecificChannel, selectedChannel))
                    {
                        LogToTextBox("Row skipped due to format mismatch or channel selection.");
                    }
                }
            }
            LogToTextBox($"channelData contents after processing:");
            foreach (var kvp in channelData)
            {
                LogToTextBox($"Channel {kvp.Key + 1}: {string.Join(", ", kvp.Value.Select(v => $"({v.XValue}, {v.YValue})"))}");
            }

            LogToTextBox("Imported data processing completed.");
            UpdateCurrentSelectedResultWithImportedData(applyToSpecificChannel, selectedChannel); // Make sure this method updates currentSelectedResult correctly

            // Refresh UI components to display new data
            CreateTabsForResultData(currentSelectedResult, currentIsUpperLimit);
            DisplayGraph(currentSelectedResult, currentIsUpperLimit);
            UpdateLimitDataTextBox(currentSelectedResult, currentIsUpperLimit);

            // Update all buttons for the currentSelectedResult
            UpdateButtonStatesForResult(currentSelectedResult);

        }
        private bool HasDataPoints(double[] limitValues)
        {
            // Check if the limitValues array has any data points
            return limitValues != null && limitValues.Length > 0;
        }
        private void UpdateButtonStatesForResult(ResultData result)
        {
            int counter = 0;
            foreach (System.Windows.Forms.Control control in tableLayoutPanel.Controls)
            {

                if (control is Button button && button.Tag is ResultData buttonResultData)
                {

                    currentlyEditingButton = button;
                    // check wheter its meter or xy . then add a check for if ts diviable by row and then check if the resultdata has data in it. 
                    if (buttonResultData.MeterValues != null && buttonResultData.MeterValues.Any())
                    {
                        if (buttonResultData.MeterUpperLimitValues[0] != null && (counter % 2 != 0))
                        {
                            button.Text = "View";
                            button.BackColor = System.Drawing.Color.Red;
                            //Debug.WriteLine("meter button upper " + counter);
                            counter++;
                        }
                        else if (buttonResultData.MeterLowerLimitValues[0] != null && (counter % 2 == 0))
                        {
                            button.Text = "View";
                            button.BackColor = System.Drawing.Color.Blue;
                            //Debug.WriteLine("meter button lower" + counter);
                            counter++;
                        }
                    }
                    else
                    {
                        if (buttonResultData.XValueUpperLimitValues != null && buttonResultData.XValueUpperLimitValues.Any() && (counter % 2 != 0))
                        {
                            button.Text = "View";
                            button.BackColor = System.Drawing.Color.Red;
                            // Debug.WriteLine("XY button upper " + counter);
                            counter++;
                        }
                        else if (buttonResultData.XValueLowerLimitValues != null && buttonResultData.XValueLowerLimitValues.Any() && (counter % 2 == 0))
                        {
                            button.Text = "View";
                            button.BackColor = System.Drawing.Color.Blue;
                            //Debug.WriteLine("XY button upper " + counter);
                            counter++;
                        }
                        else
                        {

                            //Debug.WriteLine("default button upper " + counter);
                            button.Text = "Edit";
                            button.BackColor = System.Drawing.Color.FromArgb(60, 60, 60); // Example dark grey
                            counter++;
                        }

                    }
                }
            }
        }
        private bool ProcessXYValuesRow(string[] row, bool applyToSpecificChannel, int selectedChannel)
        {
            if (row.Length < 2)
            {
                LogToTextBox("Row skipped: Not enough data columns.");
                return false;
            }

            double xValue, yValue;
            if (!double.TryParse(row[0], out xValue) || !double.TryParse(row[1], out yValue))
            {
                LogToTextBox($"Row skipped: Invalid numeric values '{row[0]}', '{row[1]}'.");
                return false;
            }

            int totalChannels = currentSelectedResult.ChannelCount;

            if (applyToSpecificChannel)
            {
                if (selectedChannel <= 0 || selectedChannel > totalChannels)
                {
                    LogToTextBox($"Row skipped: Invalid channel selection.");
                    return false;
                }
                AddToChannelData(selectedChannel - 1, xValue, yValue); // Adjust for 0-based index
            }
            else
            {
                for (int channelIndex = 0; channelIndex < totalChannels; channelIndex++)
                {
                    AddToChannelData(channelIndex, xValue, yValue);
                }
            }

            return true;
        }
        private bool ProcessMeterValuesRow(string[] row, bool applyToSpecificChannel, int selectedChannel)
        {
            if (row.Length < 1)
            {
                LogToTextBox("Row skipped: Not enough data columns for meter values.");
                return false;
            }

            double meterValue;
            if (!double.TryParse(row[0], out meterValue))
            {
                LogToTextBox($"Row skipped: Invalid numeric value '{row[0]}'.");
                return false;
            }

            int totalChannels = currentSelectedResult.ChannelCount;

            if (applyToSpecificChannel)
            {
                if (selectedChannel <= 0 || selectedChannel > totalChannels)
                {
                    LogToTextBox($"Row skipped: Invalid channel selection.");
                    return false;
                }
                AddToChannelData(selectedChannel - 1, meterValue, 0); // Adjust for 0-based index
            }
            else
            {
                for (int channelIndex = 0; channelIndex < totalChannels; channelIndex++)
                {
                    AddToChannelData(channelIndex, meterValue, 0);
                }
            }

            return true;
        }
        private void AddToChannelData(int channelIndex, double xValue, double yValue)
        {
            if (!channelData.ContainsKey(channelIndex))
            {
                channelData[channelIndex] = new List<MeterValue>();
            }
            channelData[channelIndex].Add(new MeterValue { XValue = xValue, YValue = yValue });
        }
        private void LoadDataIntoComponents(List<string[]> limitData)
        {
            if (limitData == null || limitData.Count == 0) return;

            // Clear current data
            channelData.Clear();

            // Assuming the limitData contains rows with format: [channelIndex, XValue, YValue]
            foreach (var row in limitData)
            {
                if (row.Length < 3) continue;
                if (!int.TryParse(row[0], out int channelIndex) ||
                    !double.TryParse(row[1], out double xValue) ||
                    !double.TryParse(row[2], out double yValue)) continue;

                if (!channelData.ContainsKey(channelIndex))
                {
                    channelData[channelIndex] = new List<MeterValue>();
                }
                channelData[channelIndex].Add(new MeterValue { XValue = xValue, YValue = yValue });
            }

            // Update the currentSelectedResult with the new limit values
            UpdateCurrentSelectedResultWithImportedData();

            // Refresh UI components
            CreateTabsForResultData(currentSelectedResult, currentIsUpperLimit);
            DisplayGraph(currentSelectedResult, currentIsUpperLimit);
            UpdateLimitDataTextBox(currentSelectedResult, currentIsUpperLimit);
        }
        private void UpdateCurrentSelectedResultWithImportedData(bool applyToSpecificChannel = false, int selectedChannel = -1)
        {
            if (currentSelectedResult == null)
            {
                //LogError("Current selected result is null.");
                return;
            }

            bool isXYValues = currentSelectedResult.ResultValueType == "XY Values";


            //test if import limit work
            //track channel is NOt selected
            //delete entire limit not exporting to APX


            if (isXYValues) // enable fist cvhannel tracker 
            {
                // Resetting the result data arrays to empty lists
                var newXValueUpperLimits = new List<double>();
                var newYValueUpperLimits = new List<double>();
                var newXValueLowerLimits = new List<double>();
                var newYValueLowerLimits = new List<double>();

                foreach (var kvp in channelData)
                {
                    int channelIndex = kvp.Key;
                    var channelList = kvp.Value;

                    // Apply changes only to the specific channel if flagged
                    if (applyToSpecificChannel && channelIndex != (selectedChannel - 1))
                    {
                        continue; // Skip non-target channels
                    }

                    // Append the points from this channel to the respective limit lists
                    foreach (var point in channelList)
                    {
                        newXValueUpperLimits.Add(point.XValue);
                        newYValueUpperLimits.Add(point.YValue); // Assuming the same points are used for both upper and lower limits
                        newXValueLowerLimits.Add(point.XValue); // Adjust this as per your actual logic
                        newYValueLowerLimits.Add(point.YValue);
                    }
                }

                // Convert lists to arrays and update the result data
                currentSelectedResult.XValueUpperLimitValues = newXValueUpperLimits.ToArray();
                currentSelectedResult.YValueUpperLimitValues = newYValueUpperLimits.ToArray();
                currentSelectedResult.XValueLowerLimitValues = newXValueLowerLimits.ToArray();
                currentSelectedResult.YValueLowerLimitValues = newYValueLowerLimits.ToArray();
            }
            else
            {
                LogError("Non-XY value types are not yet implemented.");
                // Handle other value types as needed
            }
        }
        private void AddButtonBefore_Click(object sender, EventArgs e)
        {
            if (!applyToAllCheckBox.Checked)
            {

                AddRowToAllDataGridViews(true);
            }
            else
            {

                AddRowToDataGridView(true);
            }
        }
        private void AddButtonAfter_Click(object sender, EventArgs e)
        {
            if (!applyToAllCheckBox.Checked)
            {
                AddRowToAllDataGridViews(false);


            }
            else
            {
                AddRowToDataGridView(false);
            }
        }
        private void AddRowToAllDataGridViews(bool addBefore)
        {
            // Get the index of the currently selected tab/channel
            int channelIndex = limitValuesTabControl.SelectedIndex;

            // Validate the selected channel index
            if (channelIndex < 0 || channelIndex >= channelData.Count)
            {
                LogToTextBox("Invalid channel index.");
                return;
            }

            // Get the DataGridView for the selected channel
            DataGridView dataGridView = limitValuesTabControl.TabPages[channelIndex].Controls[0] as DataGridView;

            // Get the index of the selected row, or default to the last row if no selection
            int selectedIndex = dataGridView.SelectedCells.Count > 0 ? dataGridView.SelectedCells[0].RowIndex : -1;
            if (selectedIndex == -1) selectedIndex = dataGridView.Rows.Count - 1;

            // Determine where to insert the new row
            int insertIndex = addBefore ? selectedIndex : selectedIndex + 1;

            // Create a new row with default values
            var newRow = new MeterValue { XValue = 0, YValue = 0 };

            // Add the new row to the channel data at the specified index
            channelData[channelIndex].Insert(insertIndex, newRow);

            // Refresh the DataGridView for the selected channel to show the new row
            RefreshDataGridView(channelIndex);
        }
        private void CreateTabsForResultData(ResultData resultData, bool isUpperLimit)
        {
            limitValuesTabControl.TabPages.Clear();
            channelData.Clear(); // Clear existing channel data

            int channelCount = resultData.ChannelCount;
            for (int channelIndex = 0; channelIndex < resultData.ChannelCount; channelIndex++)
            {
                if (!channelData.ContainsKey(channelIndex) || channelData[channelIndex].Count == 0)
                {
                    channelData[channelIndex] = new List<MeterValue>();
                    LogToTextBox($"Initializing channelData for Channel {channelIndex + 1}");
                }

                TabPage tabPage = new TabPage($"Channel {channelIndex + 1}");// possible way to fix here 
                DataGridView dataGridView = InitializeDataGridViewForChannel(resultData, channelIndex, isUpperLimit);
                tabPage.Controls.Add(dataGridView);
                limitValuesTabControl.TabPages.Add(tabPage);

                UpdateDataGridViewWithResultValues(dataGridView, resultData, channelIndex, isUpperLimit);
            }
            //Debug.WriteLine("CreateTabsForResultData:");
            foreach (var kvp in channelData)
            {
                int currentChannelIndex = kvp.Key;
                var meterValues = kvp.Value;
                //Debug.WriteLine($"Channel {currentChannelIndex + 1}:");
                foreach (var meterValue in meterValues)
                {
                    // Debug.WriteLine($"Point - X: {meterValue.XValue}, Y: {meterValue.YValue}");
                }
            }
        }
        private void LogToTextBox(string message)
        {
            if (limitDataTextBox != null)
            {
                // If the control is initialized, append the message
                limitDataTextBox.AppendText($"{DateTime.Now}: {message}\r\n");
            }
            else
            {
                // Handle the case where 'limitDataTextBox' is not initialized
                // You might want to log this to a file or take other appropriate action
                Debug.WriteLine($"LimitDataTextBox is null. Unable to log: {message}");
            }
        }
        private DataGridView InitializeDataGridViewForChannel(ResultData resultData, int channelIndex, bool isUpperLimit)
        {
            DataGridView dataGridView = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AllowUserToAddRows = false,
                // Remove the initialization of the DataSource here since it will be set in UpdateDataGridViewWithResultValues
            };

            dataGridView.CellValueChanged += LimitValuesDataGridView_CellValueChanged;


            // Populate data for the channel
            UpdateDataGridViewWithResultValues(dataGridView, resultData, channelIndex, isUpperLimit);

            return dataGridView;
        }
        private void InitializeDataGridViewColumns(DataGridView dataGridView, string resultValueType)
        {
            dataGridView.Columns.Clear(); // Clear existing columns to avoid duplication

            if (resultValueType == "Meter Values" && !dataGridView.Columns.Contains("Value"))
            {
                // Add a column for Meter Values
                dataGridView.Columns.Add(new DataGridViewTextBoxColumn
                {
                    HeaderText = "Meter Value",
                    DataPropertyName = "Value",
                    Name = "Value",
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill // Auto-resize column width
                });
            }
            else if (resultValueType == "XY Values" && !dataGridView.Columns.Contains("XValue") && !dataGridView.Columns.Contains("YValue"))
            {
                // Add columns for XY Values
                dataGridView.Columns.Add(new DataGridViewTextBoxColumn
                {
                    HeaderText = "X Values",
                    DataPropertyName = "XValue",
                    Name = "XValue",
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
                });
                dataGridView.Columns.Add(new DataGridViewTextBoxColumn
                {
                    HeaderText = "Y Values",
                    DataPropertyName = "YValue",
                    Name = "YValue",
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
                });
            }
        }

        private void UpdateDataGridViewWithResultValues(DataGridView dataGridView, ResultData resultData, int channelIndex, bool isUpperLimit)

        {


            {
                // Debug.WriteLine("Before updating DataGridView:");
                foreach (var kvp in channelData)
                {
                    int currentChannelIndex = kvp.Key;
                    var meterValues = kvp.Value;
                    // Debug.WriteLine($"Channel {currentChannelIndex + 1}:");
                    foreach (var meterValue in meterValues)
                    {
                        //Debug.WriteLine($"Point - X: {meterValue.XValue}, Y: {meterValue.YValue}");
                    }
                }

                if (dataGridView == null)
                {
                    LogError("DataGridView is null.");
                    return;
                }

                if (resultData == null)
                {
                    LogError("ResultData is null.");
                    return;
                }

                InitializeDataGridViewColumns(dataGridView, resultData.ResultValueType);

                dataGridView.Rows.Clear();
                dataGridView.AutoGenerateColumns = false;

                if (resultData.ResultValueType == "Meter Values")
                {
                    double meterValue = isUpperLimit ? resultData.MeterUpperLimitValues[channelIndex] : resultData.MeterLowerLimitValues[channelIndex];
                    LogToTextBox($"Setting Meter value for channel {channelIndex + 1}: {meterValue}");

                    if (!channelData.ContainsKey(channelIndex))
                    {
                        channelData[channelIndex] = new List<MeterValue>();
                    }
                    channelData[channelIndex].Clear();
                    channelData[channelIndex].Add(new MeterValue { Value = meterValue });
                }
                else if (resultData.ResultValueType == "XY Values")
                {
                    HashSet<string> seenValues = new HashSet<string>(); // To track unique MeterValue instances

                    double[] xValues = isUpperLimit ? resultData.XValueUpperLimitValues : resultData.XValueLowerLimitValues;
                    double[] yValues = isUpperLimit ? resultData.YValueUpperLimitValues : resultData.YValueLowerLimitValues;

                    channelData[channelIndex].Clear();
                    for (int i = 0; i < xValues.Length; i++)
                    {
                        string valueKey = $"{xValues[i]}_{yValues[i]}"; // Unique key for each MeterValue

                        if (!seenValues.Contains(valueKey))
                        {

                            channelData[channelIndex].Add(new MeterValue { XValue = xValues[i], YValue = yValues[i] });
                            seenValues.Add(valueKey);
                            //Debug.WriteLine(valueKey);
                        }
                    }

                }

                dataGridView.DataSource = new BindingList<MeterValue>(channelData[channelIndex]);
                LogToTextBox($"DataGridView updated with unique values for Channel {channelIndex + 1}.");

                // Debug.WriteLine("After updating DataGridView:");
                foreach (var kvp in channelData)
                {
                    int currentChannelIndex = kvp.Key;
                    var meterValues = kvp.Value;
                    //Debug.WriteLine($"Channel {currentChannelIndex + 1}:");
                    foreach (var meterValue in meterValues)
                    {
                        // Debug.WriteLine($"Point - X: {meterValue.XValue}, Y: {meterValue.YValue}");
                    }
                }

            }

        }
        private void InitializeDataGridViewForDarkMode(DataGridView dataGridView)
        {
            // Set general DataGridView styles
            dataGridView.BackgroundColor = System.Drawing.Color.FromArgb(45, 45, 45); // Dark grey background
            dataGridView.BorderStyle = BorderStyle.None;

            // Set column header styles
            dataGridView.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = System.Drawing.Color.FromArgb(60, 60, 60), // Slightly lighter dark grey
                ForeColor = System.Drawing.Color.Black, // White text
                Font = new System.Drawing.Font("Segoe UI", 10, FontStyle.Bold),
                Alignment = DataGridViewContentAlignment.MiddleCenter
            };

            dataGridView.EnableHeadersVisualStyles = false; // Disable default styles to apply custom styles

            // Set row styles
            dataGridView.DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = System.Drawing.Color.FromArgb(45, 45, 45), // Match background
                ForeColor = System.Drawing.Color.Black, // White text
                SelectionBackColor = System.Drawing.Color.FromArgb(80, 80, 80), // Highlight color when selected
                SelectionForeColor = System.Drawing.Color.White, // Highlighted text color
                Font = new System.Drawing.Font("Segoe UI", 10, FontStyle.Regular)
            };

            // Set grid styles
            dataGridView.GridColor = System.Drawing.Color.Gray; // Gridline color
            dataGridView.RowHeadersDefaultCellStyle.BackColor = System.Drawing.Color.FromArgb(60, 60, 60); // Row header background
            dataGridView.RowHeadersDefaultCellStyle.ForeColor = System.Drawing.Color.Black; // Row header text color
            dataGridView.RowHeadersDefaultCellStyle.SelectionBackColor = System.Drawing.Color.FromArgb(80, 80, 80); // Row header highlight
            dataGridView.RowHeadersVisible = false; // Hide row headers if not needed

            // Set alternating row styles
            dataGridView.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = System.Drawing.Color.FromArgb(50, 50, 50), // Slightly different grey for alternating rows
                ForeColor = System.Drawing.Color.Black
            };

            // Adjust auto-sizing and alignment
            dataGridView.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dataGridView.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
        }
        private void limitValuesTabControl_Selected(object sender, TabControlEventArgs e)
        {
            // Ensure that currentSelectedResult is not null
            if (currentSelectedResult == null)
            {
                //LogError("Current selected result is null.");
                return;
            }

            // Check if the selected tab page is valid and contains controls
            if (e.TabPage == null || e.TabPage.Controls.Count == 0)
            {
                // You can remove the error log if you don't want to show a message every time this happens
                // LogError("Selected tab is invalid or does not contain controls.");
                return;
            }


            // Ensure that the selected TabPage has a DataGridView control
            if (!(e.TabPage.Controls[0] is DataGridView dataGridView))
            {
                LogError("Selected tab does not contain a DataGridView control.");
                return;
            }
            else
            {
                int selectedChannelIndex = limitValuesTabControl.SelectedIndex;
                dataGridView.DataSource = new BindingList<MeterValue>(channelData[selectedChannelIndex]);
            }
            int channelIndex = limitValuesTabControl.TabPages.IndexOf(e.TabPage);

            // Check if the channelIndex is valid before proceeding
            if (channelIndex < 0 || channelIndex >= currentSelectedResult.ChannelCount)
            {
                LogError("Selected tab index is out of range.");
                return;
            }
            //Debug.WriteLine("limitValuesTabControl_Selected:");
            foreach (var kvp in channelData)
            {
                int currentChannelIndex = kvp.Key;
                var meterValues = kvp.Value;
                // Debug.WriteLine($"Channel {currentChannelIndex + 1}:");
                foreach (var meterValue in meterValues)
                {
                    //  Debug.WriteLine($"Point - X: {meterValue.XValue}, Y: {meterValue.YValue}");
                }
            }
            UpdateDataGridViewWithResultValues(dataGridView, currentSelectedResult, channelIndex, currentIsUpperLimit);
            dataGridView.Refresh(); // Refresh the DataGridView to update the display
        }
        private void AddPointToChannel(int channelIndex, MeterValue point)
        {
            if (channelData.ContainsKey(channelIndex))
            {
                channelData[channelIndex].Add(point);
                //efreshChannelDataGridView(channelIndex);
            }
        }
        private void RefreshChannelDataGridView(int channelIndex)
        {
            if (limitValuesTabControl.TabPages.Count > channelIndex)
            {
                TabPage tabPage = limitValuesTabControl.TabPages[channelIndex];
                if (tabPage.Controls[0] is DataGridView dataGridView)
                {
                    dataGridView.DataSource = null;
                    dataGridView.DataSource = new BindingList<MeterValue>(channelData[channelIndex]);
                }
            }
        }
        private void UpdateCurrentSelectedResultWithChannelData()
        {
            if (currentSelectedResult == null)
            {
                //LogError("Current selected result is null.");
                return;
            }

            if (currentSelectedResult.ResultValueType == "XY Values")
            {
                UpdateXYValuesForCurrentSelectedResult();
            }
            else if (currentSelectedResult.ResultValueType == "Meter Values")
            {
                UpdateMeterValuesForCurrentSelectedResult();
            }
            else
            {
                LogError("Unsupported result value type.");
            }
            DisplayGraph(currentSelectedResult, currentIsUpperLimit);
        }
        private void UpdateXYValuesForCurrentSelectedResult()
        {
            // Log initial X and Y values
            int x1 = 0;
            //Debug.WriteLine("Initial X Values (Upper Limit):");
            foreach (var x in currentSelectedResult.XValueUpperLimitValues)
            {
                x1 += 1;
                //Debug.WriteLine(x);
            }
            
            // Function logic to update X and Y values...
            List<double> newXValues = new List<double>();
            List<double> newYValues = new List<double>();

            foreach (var kvp in channelData)
            {
                newXValues.AddRange(kvp.Value.Select(mv => mv.XValue));
                newYValues.AddRange(kvp.Value.Select(mv => mv.YValue));
            }

            if (currentIsUpperLimit)
            {
                currentSelectedResult.XValueUpperLimitValues = newXValues.ToArray();
                currentSelectedResult.YValueUpperLimitValues = newYValues.ToArray();
            }
            else
            {
                currentSelectedResult.XValueLowerLimitValues = newXValues.ToArray();
                currentSelectedResult.YValueLowerLimitValues = newYValues.ToArray();
            }

            // Log updated X and Y values
            x1 = 0;
            //Debug.WriteLine("after update X Values:");
            foreach (var x in currentSelectedResult.XValueUpperLimitValues)
            {
                x1 += 1;
                // Debug.WriteLine(x);
            }
            // Debug.WriteLine(x1);

        }
        private void UpdateMeterValuesForCurrentSelectedResult()
        {
            if (currentSelectedResult.ResultValueType != "Meter Values")
            {
                LogError("Current selected result is not of type 'Meter Values'.");
                return;
            }

            // Update meter values for each channel
            for (int ch = 0; ch < currentSelectedResult.ChannelCount; ch++)
            {
                if (!channelData.ContainsKey(ch) || channelData[ch].Count == 0)
                {
                    LogError($"No data available for channel {ch + 1}.");
                    continue;
                }

                // Assuming only one meter value per channel
                double meterValue = channelData[ch][0].Value;

                if (currentIsUpperLimit)
                {
                    currentSelectedResult.MeterUpperLimitValues[ch] = meterValue;
                }
                else
                {
                    currentSelectedResult.MeterLowerLimitValues[ch] = meterValue;
                }
            }

            LogToTextBox("Updated meter values for current selected result.");
        }
        private void LimitValuesDataGridView_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            var dataGridView = sender as DataGridView;
            int channelIndex = FindChannelIndex(dataGridView);
            if (channelIndex == -1) return;

            bool isUpperLimitEdited = currentIsUpperLimit;
            // Check if we're editing Meter Values or XY Values
            if (dataGridView.Columns[e.ColumnIndex].DataPropertyName == "Value")
            {
                // Handle Meter Value updates
                HandleMeterValuesUpdate(dataGridView, channelIndex, e.RowIndex);
            }
            else if (dataGridView.Columns[e.ColumnIndex].DataPropertyName == "XValue" ||
                     dataGridView.Columns[e.ColumnIndex].DataPropertyName == "YValue")
            {
                // Handle XY Value updates
                HandleXYValuesUpdate(dataGridView, channelIndex, e.RowIndex);
            }

            // Update the graph after changes
            UpdateGraph();
            UpdateButtonStateForCurrentSelectedResult(currentSelectedResult.SignalPathIndex, currentSelectedResult.MeasurementIndex, currentSelectedResult.Index, currentIsUpperLimit);
        }
        private void HandleXYValuesUpdate(DataGridView dataGridView, int channelIndex, int rowIndex)
        {
            if (double.TryParse(dataGridView.Rows[rowIndex].Cells["XValue"].Value?.ToString(), out double newXValue) &&
                double.TryParse(dataGridView.Rows[rowIndex].Cells["YValue"].Value?.ToString(), out double newYValue))
            {
                // Check if we're editing an existing point
                if (rowIndex < channelData[channelIndex].Count)
                {
                    // Update the existing point in channelData
                    channelData[channelIndex][rowIndex].XValue = newXValue;
                    channelData[channelIndex][rowIndex].YValue = newYValue;

                    if (applyToAllCheckBox.Checked)
                    {
                        // Apply changes to all channels only if 'Apply to All' is checked
                        ApplyChangesToAllChannelsForXYValues(channelIndex, rowIndex, newXValue, newYValue);
                    }
                }
                else if (!applyToAllCheckBox.Checked)
                {

                    // If 'Apply to All' is checked and it's a new point, add it to all channels
                    var newRow = new MeterValue { XValue = newXValue, YValue = newYValue };

                    foreach (var kvp in channelData)
                    {
                        kvp.Value.Add(newRow);
                    }
                }
                // Note: If 'Apply to All' is unchecked and it's a new point beyond existing points, it won't be added.

                UpdateCurrentSelectedResultWithChannelData();

                // Refresh the DataGridView to reflect the changes
                dataGridView.Refresh();
            }
            else
            {
                LogToTextBox("Invalid input for XY values.");
            }
        }
        private void HandleMeterValuesUpdate(DataGridView dataGridView, int channelIndex, int rowIndex)
        {
            LogToTextBox($"HandleMeterValuesUpdate called for Channel {channelIndex + 1}, Row {rowIndex + 1}");

            if (double.TryParse(dataGridView.Rows[rowIndex].Cells["Value"].Value?.ToString(), out double newMeterValue))
            {
                LogToTextBox($"New Meter Value Parsed: {newMeterValue}");

                channelData[channelIndex][rowIndex].Value = newMeterValue;
                LogToTextBox($"Updated channelData for Channel {channelIndex + 1}, Row {rowIndex + 1}");

                if (applyToAllCheckBox.Checked)
                {
                    LogToTextBox("Apply to all channels is checked. Applying changes to all channels.");
                    ApplyChangesToAllChannelsForMeterValues(channelIndex, rowIndex, newMeterValue);
                }

                UpdateCurrentSelectedResultWithChannelData();
                LogToTextBox("CurrentSelectedResult updated with channel data.");

                if (currentIsUpperLimit)
                {
                    currentSelectedResult.UpperLimitEnabled = true;
                    LogToTextBox("Updating button for Upper Limit.");
                    UpdateSpecificEditButtonForLimit(currentSelectedResult, true);
                }
                else
                {
                    currentSelectedResult.LowerLimitEnabled = true;
                    LogToTextBox("Updating button for Lower Limit.");
                    UpdateSpecificEditButtonForLimit(currentSelectedResult, false);
                }
            }
            else
            {
                LogToTextBox("Invalid input for meter values.");
            }
        }
        private void UpdateSpecificEditButtonForLimit(ResultData resultData, bool isUpperLimit)
        {

            if (resultData == null)
            {

                LogToTextBox("Error: Result data is null in UpdateSpecificEditButtonForLimit.");
                return;
            }

            int targetColumnIndex = isUpperLimit ? 1 : 2; // Assuming column 1 for upper limit and column 2 for lower limit
            LogToTextBox($"Updating Edit Button for {(isUpperLimit ? "Upper" : "Lower")} Limit. Target Column Index: {targetColumnIndex}");

            int counter = 1;
            foreach (System.Windows.Forms.Control control in tableLayoutPanel.Controls)
            {

                if (control is Button button && button.Tag is ResultData buttonResultData)
                {

                    currentlyEditingButton = button;
                    // check wheter its meter or xy . then add a check for if ts diviable by row and then check if the resultdata has data in it. 
                    if (buttonResultData.MeterValues != null && buttonResultData.MeterValues.Any())
                    {
                        if (buttonResultData.MeterUpperLimitValues[0] != null && (counter % 2 != 0))
                        {
                            button.Text = "View";
                            button.BackColor = System.Drawing.Color.Red;
                            Debug.WriteLine("meter button upper " + counter);
                            counter++;
                        }
                        else if (buttonResultData.MeterLowerLimitValues[0] != null && (counter % 2 == 0))
                        {
                            button.Text = "View";
                            button.BackColor = System.Drawing.Color.Blue;
                            Debug.WriteLine("meter button lower" + counter);
                            counter++;
                        }
                    }
                    else
                    {
                        if (buttonResultData.XValueUpperLimitValues != null && buttonResultData.XValueUpperLimitValues.Any() && (counter % 2 != 0))
                        {
                            button.Text = "View";
                            button.BackColor = System.Drawing.Color.Red;
                            Debug.WriteLine("XY button upper " + counter);
                            counter++;
                        }
                        else if (buttonResultData.XValueLowerLimitValues != null && buttonResultData.XValueLowerLimitValues.Any() && (counter % 2 == 0))
                        {
                            button.Text = "View";
                            button.BackColor = System.Drawing.Color.Blue;
                            Debug.WriteLine("XY button upper " + counter);
                            counter++;
                        }
                        else
                        {

                            Debug.WriteLine("default button upper " + counter);
                            button.Text = "Edit";
                            button.BackColor = SystemColors.Control;
                            counter++;
                        }

                    }
                }
            }
        }
        private void UpdateButtonStateForCurrentSelectedResult(int signalPathIndex, int measurementIndex, int resultIndex, bool isUpperLimit)
        {
            currentIsUpperLimit = isUpperLimit;
            foreach (System.Windows.Forms.Control control in tableLayoutPanel.Controls)
            {
                if (control is Button button && button.Tag is ResultData buttonResultData)
                {
                    // Check if the button corresponds to the current edited result
                    if (buttonResultData.SignalPathIndex == signalPathIndex &&
                        buttonResultData.MeasurementIndex == measurementIndex &&
                        buttonResultData.Index == resultIndex)
                    {
                        // Determine if there are any data points in either limit
                        bool upperHasPoints = HasDataPoints(buttonResultData.XValueUpperLimitValues) || HasDataPoints(buttonResultData.YValueUpperLimitValues);
                        bool lowerHasPoints = HasDataPoints(buttonResultData.XValueLowerLimitValues) || HasDataPoints(buttonResultData.YValueLowerLimitValues);

                        bool upperHasMeter = HasDataPoints(buttonResultData.MeterUpperLimitValues);
                        bool lowerHasMeter = HasDataPoints(buttonResultData.MeterLowerLimitValues);
                        // Only reset the button to default if there are no points in both upper and lower limits

                        if (!upperHasPoints && !lowerHasPoints && !upperHasMeter && !lowerHasMeter)
                        {
                            button.BackColor = SystemColors.Control; // No data points in either limit
                            button.Text = "Edit";
                        }
                        else if (upperHasPoints || lowerHasPoints || upperHasMeter || lowerHasMeter)
                        {
                            //UpdateButtonStatesForResult(currentSelectedResult);
                            if (currentIsUpperLimit && upperHasMeter)
                            {
                                if (buttonResultData.MeterUpperLimitValues[0].ToString() == "NaN")
                                {
                                    button.BackColor = SystemColors.Control; // No data points in either limit
                                    button.Text = "Edit";
                                    return;
                                }
                                button.BackColor = System.Drawing.Color.Red;
                                return;
                            }
                            else if (!currentIsUpperLimit && lowerHasMeter)
                            {
                                if (buttonResultData.MeterLowerLimitValues[0].ToString() == "NaN")
                                {
                                    button.BackColor = SystemColors.Control; // No data points in either limit
                                    button.Text = "Edit";
                                    return;
                                }
                                button.BackColor = System.Drawing.Color.Blue;
                                return;
                            }
                        }

                        // If there are data points, maintain the existing color and text indicating data presence
                    }
                }
            }
        }
        private void ApplyChangesToAllChannelsForXYValues(int sourceChannelIndex, int rowIndex, double newXValue, double newYValue)
        {

            foreach (var kvp in channelData)
            {
                if (kvp.Value.Count > rowIndex)
                {
                    kvp.Value[rowIndex].XValue = newXValue;
                    kvp.Value[rowIndex].YValue = newYValue;
                }
            }
        }

        private void ApplyChangesToAllChannelsForMeterValues(int sourceChannelIndex, int rowIndex, double newMeterValue)
        {
            foreach (var kvp in channelData)
            {
                if (kvp.Value.Count > rowIndex)
                {
                    kvp.Value[rowIndex].Value = newMeterValue;
                }
            }
        }

        private void DataGridView_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            // Log error details
            LogError($"DataError in DataGridView: {e.Exception.Message}");
        }

        private void UpdateResultDataArrays()
        {
            if (currentSelectedResult == null)
            {
                //LogError("Current selected result is null.");
                return;
            }

            // Resetting the result data arrays to empty lists
            var newXValueUpperLimits = new List<double>();
            var newYValueUpperLimits = new List<double>();
            var newXValueLowerLimits = new List<double>();
            var newYValueLowerLimits = new List<double>();

            foreach (var kvp in channelData)
            {
                int channelIndex = kvp.Key;
                var channelList = kvp.Value;

                if (channelList == null)
                {
                    LogError($"Channel list for channel {channelIndex + 1} is null.");
                    continue;
                }

                // Append the points from this channel to the respective limit lists
                foreach (var point in channelList)
                {
                    newXValueUpperLimits.Add(point.XValue);
                    newYValueUpperLimits.Add(point.YValue);  // Assuming the same points are used for both upper and lower limits
                    newXValueLowerLimits.Add(point.XValue);  // You might want to adjust this depending on your actual use case
                    newYValueLowerLimits.Add(point.YValue);
                }
            }

            // Convert lists to arrays and update the result data
            currentSelectedResult.XValueUpperLimitValues = newXValueUpperLimits.ToArray();
            currentSelectedResult.YValueUpperLimitValues = newYValueUpperLimits.ToArray();
            currentSelectedResult.XValueLowerLimitValues = newXValueLowerLimits.ToArray();
            currentSelectedResult.YValueLowerLimitValues = newYValueLowerLimits.ToArray();

            DisplayGraph(currentSelectedResult, currentIsUpperLimit);
            UpdateLimitDataTextBox(currentSelectedResult, currentIsUpperLimit);
        }

        private void ValidateOrInitializeLimitArrays(int totalPoints)
        {
            // Initialize or resize the arrays if null or of incorrect size
            if (currentSelectedResult.XValueUpperLimitValues == null || currentSelectedResult.XValueUpperLimitValues.Length != totalPoints)
            {
                currentSelectedResult.XValueUpperLimitValues = new double[totalPoints];
            }
            if (currentSelectedResult.YValueUpperLimitValues == null || currentSelectedResult.YValueUpperLimitValues.Length != totalPoints)
            {
                currentSelectedResult.YValueUpperLimitValues = new double[totalPoints];
            }
            if (currentSelectedResult.XValueLowerLimitValues == null || currentSelectedResult.XValueLowerLimitValues.Length != totalPoints)
            {
                currentSelectedResult.XValueLowerLimitValues = new double[totalPoints];
            }
            if (currentSelectedResult.YValueLowerLimitValues == null || currentSelectedResult.YValueLowerLimitValues.Length != totalPoints)
            {
                currentSelectedResult.YValueLowerLimitValues = new double[totalPoints];
            }
        }


        // Utility method to find the channel index for a given DataGridView
        private int FindChannelIndex(DataGridView dataGridView)
        {
            for (int i = 0; i < limitValuesTabControl.TabPages.Count; i++)
            {
                if (limitValuesTabControl.TabPages[i].Controls.Contains(dataGridView))
                {
                    return i;
                }
            }
            return -1; // Channel index not found
        }

        private void UpdateCheckedDataFromDataGridView()
        {
            foreach (var signalPath in checkedData)
            {
                foreach (var measurement in signalPath.Measurements)
                {
                    foreach (var result in measurement.Results)
                    {
                        // Find the DataGridView row that matches the current result
                        foreach (DataGridViewRow row in dataGridView1.Rows)
                        {
                            if (row.Cells["SignalPath"].Value?.ToString() == signalPath.Name &&
                                row.Cells["MeasurementName"].Value?.ToString() == measurement.Name &&
                                row.Cells["ResultName"].Value?.ToString() == result.Name)
                            {
                                // Update the result properties from the DataGridView cells
                                result.UpperLimitEnabled = row.Cells["UpperLimitEnabled"].Value != null && Convert.ToBoolean(row.Cells["UpperLimitEnabled"].Value);
                                result.LowerLimitEnabled = row.Cells["LowerLimitEnabled"].Value != null && Convert.ToBoolean(row.Cells["LowerLimitEnabled"].Value);
                                result.MeterUpperLimitValues = row.Cells["MeterUpperLimitValues"].Value != null ? ParseDoubleArray(row.Cells["MeterUpperLimitValues"].Value.ToString()) : new double[0];
                                result.MeterLowerLimitValues = row.Cells["MeterLowerLimitValues"].Value != null ? ParseDoubleArray(row.Cells["MeterLowerLimitValues"].Value.ToString()) : new double[0];
                                result.XValueUpperLimitValues = row.Cells["XValueUpperLimitValues"].Value != null ? ParseDoubleArray(row.Cells["XValueUpperLimitValues"].Value.ToString()) : new double[0];
                                result.XValueLowerLimitValues = row.Cells["XValueLowerLimitValues"].Value != null ? ParseDoubleArray(row.Cells["XValueLowerLimitValues"].Value.ToString()) : new double[0];
                                result.YValueUpperLimitValues = row.Cells["YValueUpperLimitValues"].Value != null ? ParseDoubleArray(row.Cells["YValueUpperLimitValues"].Value.ToString()) : new double[0];
                                result.YValueLowerLimitValues = row.Cells["YValueLowerLimitValues"].Value != null ? ParseDoubleArray(row.Cells["YValueLowerLimitValues"].Value.ToString()) : new double[0];
                            }
                        }
                    }
                }
            }
        }

        private double[] ParseDoubleArray(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return new double[0];
            }
            return value.Split(',').Select(double.Parse).ToArray();
        }

        private void DownloadButton_Click(object sender, EventArgs e)
        {
            try
            {
                UpdateCheckedDataFromDataGridView();

                var limitData = checkedData.SelectMany(sp => sp.Measurements.SelectMany(m => m.Results.Select(r => new
                {
                    SignalPathName = sp.Name,
                    MeasurementName = m.Name,
                    ResultName = r.Name,
                    r.UpperLimitEnabled,
                    r.LowerLimitEnabled,
                    r.MeterUpperLimitValues,
                    r.MeterLowerLimitValues,
                    r.XValueUpperLimitValues,
                    r.XValueLowerLimitValues,
                    r.YValueUpperLimitValues,
                    r.YValueLowerLimitValues,
                    ResultValueType = r.ResultValueType
                }))).ToList();

                string serializedData = JsonConvert.SerializeObject(limitData, Formatting.Indented);

                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    FileName = "LimitData.lyc",
                    Filter = "LYC files (*.lyc)|*.lyc"
                };

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    File.WriteAllText(saveFileDialog.FileName, serializedData);
                    LogToTextBox("Limits .lyc file has been downloaded successfully.");
                }
            }
            catch (Exception ex)
            {
                LogToTextBox($"An error occurred during download: {ex.Message}");
            }
        }

        private void CloseButton_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void InitializeTable()
        {
            // Create a Panel to hold the TableLayoutPanel and allow for vertical scrolling
            Panel scrollPanel = new Panel
            {
                Location = new Point(10, 10), // Adjust as needed
                Size = new Size(600, 700), // Fixed size of the panel
                AutoScroll = true, // Enable automatic scrolling
                AutoScrollMinSize = new Size(580, 0),
                BackColor = System.Drawing.Color.FromArgb(45, 45, 45) // Set dark grey background for the panel

            };

            // Initialize the TableLayoutPanel within the scrollable Panel
            tableLayoutPanel = new TableLayoutPanel
            {
                ColumnCount = 3,
                Size = new Size(580, 2000), // Width less than the scrollPanel's width to avoid horizontal scrollbar
                AutoSize = false,
                Dock = DockStyle.Top, // Dock to the top and allow vertical expansion
                CellBorderStyle = TableLayoutPanelCellBorderStyle.Single,
                BackColor = System.Drawing.Color.FromArgb(60, 60, 60), // Set dark grey background for the table
                ForeColor = System.Drawing.Color.White // Set white text for all controls added to the table
            };

            // Add column headers with dark mode styling
            tableLayoutPanel.Controls.Add(new Label
            {
                Text = "Signal Path | Measurement | Result",
                AutoSize = true,
                ForeColor = System.Drawing.Color.White,
                BackColor = System.Drawing.Color.FromArgb(60, 60, 60) // Match the table's background
            }, 0, 0);

            tableLayoutPanel.Controls.Add(new Label
            {
                Text = "Upper Limit",
                AutoSize = true,
                ForeColor = System.Drawing.Color.White,
                BackColor = System.Drawing.Color.FromArgb(60, 60, 60)
            }, 1, 0);

            tableLayoutPanel.Controls.Add(new Label
            {
                Text = "Lower Limit",
                AutoSize = true,
                ForeColor = System.Drawing.Color.White,
                BackColor = System.Drawing.Color.FromArgb(60, 60, 60)
            }, 2, 0);

            int currentRow = 1;
            foreach (var signalPath in checkedData)
            {
                foreach (var measurement in signalPath.Measurements)
                {
                    foreach (var result in measurement.Results)
                    {
                        string resultText = $"{signalPath.Name} | {measurement.Name} | {result.Name}";
                        Button upperLimitButton = CreateLimitButton(result, true);
                        Button lowerLimitButton = CreateLimitButton(result, false);

                        tableLayoutPanel.Controls.Add(new Label
                        {
                            Text = resultText,
                            AutoSize = true,
                            ForeColor = System.Drawing.Color.White,
                            BackColor = System.Drawing.Color.FromArgb(60, 60, 60)
                        }, 0, currentRow);

                        tableLayoutPanel.Controls.Add(upperLimitButton, 1, currentRow);
                        tableLayoutPanel.Controls.Add(lowerLimitButton, 2, currentRow);

                        currentRow++;
                    }
                }
            }

            // Add the TableLayoutPanel to the scrollPanel instead of directly to the form
            scrollPanel.Controls.Add(tableLayoutPanel);
            this.Controls.Add(scrollPanel); // Add the scroll panel to the form's controls
        }

        private void clearTable()
        {
            tableLayoutPanel.Controls.Clear();
        }
        private int newMeasureCount = 1;
        private List<string> missSignal = new List<string>();
        private List<string> missMeasure = new List<string>();
        private List<string> missResult = new List<string>();
        private void InitializeTableadd1(int Row, string newSignal, string newMeasurement, string newResult)
        {
            clearTable();

            // Create a Panel to hold the TableLayoutPanel and allow for vertical scrolling
            Panel scrollPanel = new Panel
            {
                Location = new Point(10, 10), // Adjust as needed
                Size = new Size(600, 700), // Fixed size of the panel
                AutoScroll = true, // Enable automatic scrolling
                AutoScrollMinSize = new Size(580, 0),
                BackColor = System.Drawing.Color.FromArgb(45, 45, 45) // Set dark grey background for the panel

            };

            // Initialize the TableLayoutPanel within the scrollable Panel
            tableLayoutPanel = new TableLayoutPanel
            {
                ColumnCount = 3,
                Size = new Size(580, 2000), // Width less than the scrollPanel's width to avoid horizontal scrollbar
                AutoSize = false,
                Dock = DockStyle.Top, // Dock to the top and allow vertical expansion
                CellBorderStyle = TableLayoutPanelCellBorderStyle.Single,
                BackColor = System.Drawing.Color.FromArgb(60, 60, 60), // Set dark grey background for the table
                ForeColor = System.Drawing.Color.White // Set white text for all controls added to the table
            };


            // Add column headers with dark mode styling
            tableLayoutPanel.Controls.Add(new Label
            {
                Text = "Signal Path | Measurement | Result",
                AutoSize = true,
                ForeColor = System.Drawing.Color.White,
                BackColor = System.Drawing.Color.FromArgb(60, 60, 60) // Match the table's background
            }, 0, 0);

            tableLayoutPanel.Controls.Add(new Label
            {
                Text = "Upper Limit",
                AutoSize = true,
                ForeColor = System.Drawing.Color.White,
                BackColor = System.Drawing.Color.FromArgb(60, 60, 60)
            }, 1, 0);

            tableLayoutPanel.Controls.Add(new Label
            {
                Text = "Lower Limit",
                AutoSize = true,
                ForeColor = System.Drawing.Color.White,
                BackColor = System.Drawing.Color.FromArgb(60, 60, 60)
            }, 2, 0);

            string resultText;
            Button upperLimitButton;
            Button lowerLimitButton;
            int currentRow = 1;

            foreach (var signalPath in checkedData)
            {
                foreach (var measurement in signalPath.Measurements)
                {
                    foreach (var result in measurement.Results)
                    {

                        resultText = $"{signalPath.Name} | {measurement.Name} | {result.Name}";
                        upperLimitButton = CreateLimitButton(result, true);
                        lowerLimitButton = CreateLimitButton(result, false);

                        tableLayoutPanel.Controls.Add(new Label { Text = resultText, AutoSize = true }, 0, currentRow);
                        tableLayoutPanel.Controls.Add(upperLimitButton, 1, currentRow);
                        tableLayoutPanel.Controls.Add(lowerLimitButton, 2, currentRow);

                        currentRow++;
                        if (Row == currentRow)
                        {
                            resultText = $"{newSignal} | {newMeasurement} | {newResult}";
                            upperLimitButton = CreateLimitButton(result, true);
                            lowerLimitButton = CreateLimitButton(result, false);

                            tableLayoutPanel.Controls.Add(new Label { Text = resultText, AutoSize = true }, 0, currentRow);
                            tableLayoutPanel.Controls.Add(upperLimitButton, 1, currentRow);
                            tableLayoutPanel.Controls.Add(lowerLimitButton, 2, currentRow);

                            currentRow++;
                        }
                    }
                }
            }
            tableLayoutPanel.ResumeLayout(true);

        }
        private Button CreateLimitButton(ResultData result, bool isUpperLimit)
        {
            string buttonText = isUpperLimit ? (result?.UpperLimitEnabled ?? false ? "View" : "Edit") : (result?.LowerLimitEnabled ?? false ? "View" : "Edit");
            Button limitButton = new Button
            {
                Text = buttonText,
                AutoSize = true,
                BackColor = isUpperLimit ? (result?.UpperLimitEnabled ?? false ? System.Drawing.Color.Red : SystemColors.Control) : (result?.LowerLimitEnabled ?? false ? System.Drawing.Color.Blue : SystemColors.Control)
            };

            limitButton.Tag = result; // Tag the button with its result data
            limitButton.Click += (s, e) =>
            {
                if (result == null)
                {
                    LogError("Selected result is null.");
                    return;
                }
                currentSelectedResult = result;
                currentIsUpperLimit = isUpperLimit;

                // Ensure all the components are initialized before attempting to use them
                if (graphPanel != null && importLimitButton != null && applyToAllCheckBox != null && exportToAPxButton != null && deleteLimitButton != null)
                {
                    DisplayGraph(result, isUpperLimit);
                    importLimitButton.Visible = true;
                    applyToAllCheckBox.Visible = true;
                    exportToAPxButton.Visible = true;
                    deleteLimitButton.Visible = true; // Make the delete button visible
                    deleteEntireLimitButton.Visible = true;
                    DisplayGraph(result, isUpperLimit);
                }
                else
                {
                    LogError("One or more required UI components are null.");
                }
            };
            UpdateButtonStatesForResult(currentSelectedResult);
            return limitButton;
        }
        private void InitializeGraphPanel()
        {
            graphPanel = new Panel
            {
                Location = new Point(600, 10),
                Size = new Size(580, 700),
                BorderStyle = BorderStyle.FixedSingle
            };
            this.Controls.Add(graphPanel);
        }

        // Update the existing UpdateLimitDataTextBox method
        private void UpdateLimitDataTextBox(ResultData result, bool isUpperLimit)
        {
            //APx.ShowMeasurement(result.SignalPathIndex, result.MeasurementIndex);

            StringBuilder limitDataBuilder = new StringBuilder();
            string limitType = isUpperLimit ? "Upper" : "Lower";

            limitDataBuilder.AppendLine($"SP Index: {result?.SignalPathIndex}, M Index: {result?.MeasurementIndex}, R Index: {result?.Index}");
            limitDataBuilder.AppendLine($"Limit Data for '{result?.Name}' ({limitType} Limit):");

            for (int channelIndex = 0; channelIndex < result.ChannelCount; channelIndex++)
            {
                limitDataBuilder.AppendLine($"Channel: Channel {channelIndex + 1}");
                Debug.Write(result.ChannelCount);
                Debug.Write(channelIndex);
                if (channelData.ContainsKey(channelIndex))
                {
                    Debug.WriteLine(channelData[channelIndex].Count);
                    // Access the dictionary using channelIndex
                    for (int i = 0; i < channelData[channelIndex].Count; i++)
                    {
                        limitDataBuilder.AppendLine($"  Point {i + 1} (Channel Index {channelIndex}, Point Index {i}) - X Value: {channelData[channelIndex][i].XValue}, Y Value: {channelData[channelIndex][i].YValue}");
                    }
                }
                else
                {

                }

            }

            limitDataTextBox.Text = limitDataBuilder.ToString();
            //APx.ShowMeasurement(result.SignalPathIndex, result.MeasurementIndex);
        }
        private void DeleteEntireLimitButton_Click(object sender, EventArgs e)
        {
            if (currentSelectedResult == null)
            {
                MessageBox.Show("No result selected.", "Error", MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                return;
            }
            currentIsUpperLimit.ToString();
            // Confirm with the user before clearing the limits
            var confirmResult = MessageBox.Show("Are you sure you want to clear all limits for this result?", "Confirm Clear Limits", MessageBoxButtons.YesNo, System.Windows.Forms.MessageBoxIcon.Question);
            if (confirmResult != DialogResult.Yes)
            {
                return;
            }
            bool upperExport = false;
            bool lowerExport = false;
            if (currentIsUpperLimit.ToString() == "True")
            { upperExport = true; }
            if (currentIsUpperLimit.ToString() == "False")
            { lowerExport = true; }
            // Clear both upper and lower limits for the XY values if they exist


            // Clear meter values if they exist
            if (currentSelectedResult.ResultValueType == "Meter Values")
            {
                currentSelectedResult.MeterUpperLimitValues = new double[currentSelectedResult.ChannelCount];
                currentSelectedResult.MeterLowerLimitValues = new double[currentSelectedResult.ChannelCount];
            }
            APx.ShowMeasurement(currentSelectedResult.SignalPathIndex, currentSelectedResult.MeasurementIndex);

            Debug.WriteLine(APx.ActiveMeasurementName);
            String measure;
            measure = APx.ActiveMeasurementName;

            /*
            if (measure.Contains("Frequency Response"))
            {
                APx.FrequencyResponse.Level.ClearLimits();
            }
            if (measure.Contains("Acoustic Response"))
            {
                APx.AcousticResponse.ThdRatio.ClearLimits();
            }
            */


            // Update UI components to reflect the changes



            // Optionally, if you need to export this empty limit state back to APx500, call the export function here.
            // For example, this might look something like this (you will need to implement this according to your application's structure):
            if (upperExport)
            {
                currentSelectedResult.XValueUpperLimitValues = new double[0];
                currentSelectedResult.YValueUpperLimitValues = new double[0];
                RefreshUIComponents();

                ExportToAPx(currentSelectedResult.SignalPathIndex, currentSelectedResult.MeasurementIndex, currentSelectedResult.Name, true);

            } // For upper limit
            else if (lowerExport)
            {
                currentSelectedResult.XValueLowerLimitValues = new double[0];
                currentSelectedResult.YValueLowerLimitValues = new double[0];
                RefreshUIComponents();
                ExportToAPx(currentSelectedResult.SignalPathIndex, currentSelectedResult.MeasurementIndex, currentSelectedResult.Name, false);

            } // For lower limit
            RefreshUIComponents();
            UpdateButtonStatesForResult(currentSelectedResult);

        }
        private void DeleteLimitButton_Click(object sender, EventArgs e)
        {
            if (limitValuesTabControl.SelectedTab.Controls[0] is DataGridView dataGridView)
            {
                var selectedRows = dataGridView.SelectedRows;

                if (selectedRows.Count > 0)
                {
                    int channelIndex = limitValuesTabControl.SelectedIndex; // Assuming each tab corresponds to a channel

                    // Collect all selected MeterValues
                    List<MeterValue> selectedMeterValues = new List<MeterValue>();
                    foreach (DataGridViewRow selectedRow in selectedRows)
                    {
                        if (selectedRow.DataBoundItem is MeterValue meterValue && channelData[channelIndex].Contains(meterValue))
                        {
                            selectedMeterValues.Add(meterValue);
                        }
                    }

                    // Update the channel data and currentSelectedResult based on selected points
                    UpdateCurrentResultAndReduceSelectedPoints(channelIndex, selectedMeterValues);

                    // Refresh the DataGridView to reflect the changes
                    dataGridView.DataSource = null;
                    dataGridView.DataSource = new BindingList<MeterValue>(channelData[channelIndex]);
                    RefreshDataGridView(channelIndex); // Refresh only the modified channel's DataGridView
                    RefreshDataGridViewForAllChannels();
                    UpdateLimit();
                    DisplayGraph(currentSelectedResult, currentIsUpperLimit);
                }
                else
                {
                    //APx.ActiveMeasurement.SequenceMeasurement.SequenceSteps.ImportLimitsDataSteps.Add().FileName = "C:/Users/davis/Downloads/LimitData-test1.lyc";
                    MessageBox.Show("Please select one or more rows to delete.", "Selection Required", MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                }
            }
        }
        private void UpdateCurrentResultAndReduceSelectedPoints(int channelIndex, List<MeterValue> selectedMeterValues)
        {
            if (currentSelectedResult == null || !channelData.ContainsKey(channelIndex))
            {
                return;
            }

            // Move each selected point to the end of its channel list and then remove it
            foreach (var meterValue in selectedMeterValues)
            {
                channelData[channelIndex].Remove(meterValue);
                channelData[channelIndex].Add(meterValue);
            }

            // Remove points from the end of the list equal to the number of selected points
            int removeCount = selectedMeterValues.Count;
            if (channelData[channelIndex].Count >= removeCount)
            {
                channelData[channelIndex].RemoveRange(channelData[channelIndex].Count - removeCount, removeCount);
            }

            // Update currentSelectedResult's X and Y values based on the updated channelData
            List<double> newXValues = new List<double>();
            List<double> newYValues = new List<double>();

            foreach (var meterValue in channelData[channelIndex])
            {
                newXValues.Add(meterValue.XValue);
                newYValues.Add(meterValue.YValue);
            }

            // Assign the updated values to the appropriate currentSelectedResult properties
            if (currentIsUpperLimit)
            {
                currentSelectedResult.XValueUpperLimitValues = newXValues.ToArray();
                currentSelectedResult.YValueUpperLimitValues = newYValues.ToArray();
            }
            else
            {
                currentSelectedResult.XValueLowerLimitValues = newXValues.ToArray();
                currentSelectedResult.YValueLowerLimitValues = newYValues.ToArray();
            }
        }
        private void LogError(string message)
        {
            MessageBox.Show(message, "Error", MessageBoxButtons.OK, FormsMessageBoxIcon.Error);

            // Append the error message to the limitDataTextBox
            limitDataTextBox.AppendText($"Error: {message}\r\n");
        }
        private void DisplayGraph(ResultData result, bool isUpperLimit)
        {
            // Clear previous graph and legend
            graphPanel.Controls.Clear();
            Chart chart = new Chart { Dock = DockStyle.Fill };
            ChartArea chartArea = new ChartArea
            {
                BackColor = System.Drawing.Color.FromArgb(45, 45, 45), // Set background color of the chart area
                BorderColor = System.Drawing.Color.Gray,
                BorderDashStyle = ChartDashStyle.Solid
            };

            // Set axis properties for dark mode
            chartArea.AxisX.Title = result.ResultValueType == "XY Values" ? result.XUnit : result.MeterUnit;
            chartArea.AxisX.TitleForeColor = System.Drawing.Color.White;
            chartArea.AxisX.LabelStyle.ForeColor = System.Drawing.Color.White;
            chartArea.AxisX.LineColor = System.Drawing.Color.White;
            chartArea.AxisX.MajorGrid.LineColor = System.Drawing.Color.Gray;
            chartArea.AxisX.MinorGrid.LineColor = System.Drawing.Color.DimGray;

            chartArea.AxisY.Title = result.ResultValueType == "XY Values" ? result.YUnit : result.MeterUnit;
            chartArea.AxisY.TitleForeColor = System.Drawing.Color.White;
            chartArea.AxisY.LabelStyle.ForeColor = System.Drawing.Color.White;
            chartArea.AxisY.LineColor = System.Drawing.Color.White;
            chartArea.AxisY.MajorGrid.LineColor = System.Drawing.Color.Gray;
            chartArea.AxisY.MinorGrid.LineColor = System.Drawing.Color.DimGray;

            // Find the names of the Signal Path and Measurement associated with the result
            var signalPath = checkedData.FirstOrDefault(sp => sp.Measurements.Any(m => m.Results.Contains(result)));
            var measurement = signalPath?.Measurements.FirstOrDefault(m => m.Results.Contains(result));

            // Set the chart title with dark mode styling
            string chartTitleText = $"{signalPath?.Name ?? "N/A"} | {measurement?.Name ?? "N/A"} | {result.Name} - {(isUpperLimit ? "Upper" : "Lower")} Limit";
            Title chartTitle = new Title(chartTitleText)
            {
                ForeColor = System.Drawing.Color.White, // Title text color
                Font = new System.Drawing.Font("Segoe UI", 12, FontStyle.Bold) // Adjust font if needed
            };
            chart.Titles.Add(chartTitle);

            // Configure chart overall appearance for dark mode
            chart.BackColor = System.Drawing.Color.FromArgb(45, 45, 45); // Chart background
            chart.ForeColor = System.Drawing.Color.White; // General text color

            chart.Series.Clear();
            chart.Legends.Clear();
            Legend legend = new Legend
            {
                BackColor = System.Drawing.Color.FromArgb(45, 45, 45), // Legend background
                ForeColor = System.Drawing.Color.White // Legend text color
            };
            chart.Legends.Add(legend);
            chart.Legends[0].Enabled = false; // Disable legend by default

            // Initialize add point buttons if null
            if (addButtonBefore == null || addButtonAfter == null)
            {
                InitializeAddPointButtons();
            }

            // Show or hide add point buttons based on conditions
            if (result != null && result.ResultValueType == "XY Values")
            {
                if (result.XValueUpperLimitValues.Length == 0 && isUpperLimit)
                {
                    addButtonBefore.Visible = false;
                    addButtonAfter.Visible = true;
                    addButtonAfter.Text = "Add Point";
                }
                else if (result.XValueLowerLimitValues.Length == 0 && !isUpperLimit)
                {
                    addButtonBefore.Visible = false;
                    addButtonAfter.Visible = true;
                    addButtonAfter.Text = "Add Point";
                }
                else
                {
                    addButtonAfter.Text = "Add Point After";
                    addButtonBefore.Visible = addButtonAfter.Visible = true;
                }
            }
            else
            {
                addButtonBefore.Visible = addButtonAfter.Visible = false;
            }

            chart.ChartAreas.Add(chartArea);

            // Apply user-defined or auto-ranging
            SetAxisLimits(chartArea.AxisX, xAxisStartTextBox.Text, xAxisEndTextBox.Text, result.AutoRangeX);
            SetAxisLimits(chartArea.AxisY, yAxisStartTextBox.Text, yAxisEndTextBox.Text, result.AutoRangeY);

            // Determine if the result is meter values and enable/disable x-axis text boxes accordingly
            bool isMeterValues = result.ResultValueType == "Meter Values";
            xAxisStartTextBox.Enabled = !isMeterValues;
            xAxisEndTextBox.Enabled = !isMeterValues;

            if (result.ResultValueType == "Meter Values")
            {
                DisplayMeterValues(chart, result, isUpperLimit);
                UpdateMeterValuesDataGridView(result, isUpperLimit);
            }
            else if (result.ResultValueType == "XY Values")
            {
                DisplayXYValues(chart, result, isUpperLimit);
                ConfigureLegendForXY(chart); // Configure the legend for XY value charts
            }

            CreateTabsForResultData(result, isUpperLimit);
            graphPanel.Controls.Add(chart);
            UpdateLimitDataTextBox(result, isUpperLimit);
        }

        private void DisplayXYValues(Chart chart, ResultData result, bool isUpperLimit)
        {
            // Assuming channelData is a Dictionary<int, List<MeterValue>> where int is the channel index
            System.Drawing.Color[] limitColors = GenerateColors(isUpperLimit ? System.Drawing.Color.Red : System.Drawing.Color.Blue, channelData.Count);

            // Clear existing series to avoid duplication
            chart.Series.Clear();

            foreach (var kvp in channelData)
            {
                int channelIndex = kvp.Key;
                List<MeterValue> meterValues = kvp.Value;
                string seriesName = $"{(isUpperLimit ? "Upper" : "Lower")} Limit - Channel {channelIndex + 1}";

                Series series = new Series(seriesName)
                {
                    ChartType = SeriesChartType.Line,
                    Color = limitColors[channelIndex],
                    BorderWidth = 2
                };

                // Add points to the series from channelData
                foreach (var meterValue in meterValues)
                {
                    series.Points.AddXY(meterValue.XValue, meterValue.YValue);
                }

                // Add the series to the chart
                chart.Series.Add(series);
            }

            // Debug print after updating the chart
            foreach (var series in chart.Series)
            {
                //Debug.WriteLine($"{series.Name}:");
                foreach (var point in series.Points)
                {
                    //Debug.WriteLine($"Point - X: {point.XValue}, Y: {point.YValues[0]}");
                }
            }
        }
        private void ConfigureLegendForXY(Chart chart)
        {
            Legend legend = chart.Legends[0];
            legend.Enabled = true;
            legend.Docking = Docking.Bottom; // Position the legend at the bottom
            legend.Alignment = StringAlignment.Center;
            legend.IsDockedInsideChartArea = false; // Ensure the legend does not overlap with the chart area
        }
        private Series CreateSeriesForChannel(string seriesName, System.Drawing.Color color, double[] xValues, double[] yValues)
        {
            Series series = new Series(seriesName)
            {
                ChartType = SeriesChartType.Line,
                Color = color,
                BorderWidth = 2,
                LegendText = seriesName
            };

            for (int i = 0; i < xValues.Length; i++)
            {
                series.Points.AddXY(xValues[i], yValues[i]);
            }

            return series;
        }
        private System.Drawing.Color[] GenerateColors(System.Drawing.Color baseColor, int numberOfColors)
        {
            System.Drawing.Color[] colors = new System.Drawing.Color[numberOfColors];
            for (int i = 0; i < numberOfColors; i++)
            {
                // This will generate a color that is a lighter shade of the base color for each channel
                float correctionFactor = (float)(i + 1) / numberOfColors;
                colors[i] = ControlPaint.Light(baseColor, correctionFactor);
            }
            return colors;
        }
        private void DisplayMeterValues(Chart chart, ResultData result, bool isUpperLimit)
        {
            Series meterSeries = new Series
            {
                ChartType = SeriesChartType.Point,
                MarkerStyle = MarkerStyle.Cross,
                MarkerSize = 10,
                Color = isUpperLimit ? System.Drawing.Color.Red : System.Drawing.Color.Blue
            };

            double[] meterValues = isUpperLimit ? result.MeterUpperLimitValues : result.MeterLowerLimitValues;
            if (meterValues != null)
            {
                for (int i = 0; i < meterValues.Length; i++)
                {
                    meterSeries.Points.AddXY(i + 1, meterValues[i]);
                }
            }

            chart.Series.Add(meterSeries);
        }
        private void SetAxisLimits(Axis axis, string startText, string endText, bool isAutoRange)
        {
            // The maximum and minimum values that can fit into a decimal
            const double MaxDecimalValue = (double)decimal.MaxValue;
            const double MinDecimalValue = (double)decimal.MinValue;

            if (isAutoRange)
            {
                axis.Minimum = Double.NaN; // Let the chart control determine the appropriate minimum
                axis.Maximum = Double.NaN; // Let the chart control determine the appropriate maximum
            }
            else
            {
                if (double.TryParse(startText, out double startValue) && double.TryParse(endText, out double endValue))
                {
                    // Ensure that the values are within the range of a decimal to prevent overflow
                    axis.Minimum = Math.Max(startValue, MinDecimalValue);
                    axis.Maximum = Math.Min(endValue, MaxDecimalValue);
                }
                else
                {
                    MessageBox.Show("Please enter valid numerical values for axis ranges.", "Invalid Input", MessageBoxButtons.OK, FormsMessageBoxIcon.Warning);
                }
            }
        }
        private void InitializeGraphPreferences()
        {
            int labelOffsetX = 110; // Horizontal offset for labels to align with input fields
            int controlSpacing = 25; // Vertical spacing between controls

            GroupBox graphPreferencesGroup = new GroupBox
            {
                Text = "Graph Preferences",
                Location = new Point(1185, 10),
                Size = new Size(350, 250) // Adjusted for a wider layout
            };
            this.Controls.Add(graphPreferencesGroup);

            // Initialize Auto-Range X CheckBox and set it checked by default
            autoRangeXCheckBox = new System.Windows.Forms.CheckBox
            {
                Checked = true,
                Location = new Point(10 + labelOffsetX, 35),
                Width = 15
            };
            graphPreferencesGroup.Controls.Add(autoRangeXCheckBox);
            AddLabel(graphPreferencesGroup, "Auto-Range X:", new Point(10, 35));

            // Initialize X-Axis Start TextBox and set it disabled by default (since Auto-Range is on)
            xAxisStartTextBox = new TextBox
            {
                Enabled = false,
                Location = new Point(10 + labelOffsetX, 35 + controlSpacing),
                Width = 100
            };
            graphPreferencesGroup.Controls.Add(xAxisStartTextBox);
            AddLabel(graphPreferencesGroup, "X-Axis Start:", new Point(10, 35 + controlSpacing));

            // Initialize X-Axis End TextBox and set it disabled by default
            xAxisEndTextBox = new TextBox
            {
                Enabled = false,
                Location = new Point(10 + labelOffsetX, 35 + 2 * controlSpacing),
                Width = 100
            };
            graphPreferencesGroup.Controls.Add(xAxisEndTextBox);
            AddLabel(graphPreferencesGroup, "X-Axis End:", new Point(10, 35 + 2 * controlSpacing));

            // Initialize Auto-Range Y CheckBox and set it checked by default
            autoRangeYCheckBox = new System.Windows.Forms.CheckBox
            {
                Checked = true,
                Location = new Point(10 + labelOffsetX, 35 + 3 * controlSpacing),
                Width = 15
            };
            graphPreferencesGroup.Controls.Add(autoRangeYCheckBox);
            AddLabel(graphPreferencesGroup, "Auto-Range Y:", new Point(10, 35 + 3 * controlSpacing));

            // Initialize Y-Axis Start TextBox and set it disabled by default
            yAxisStartTextBox = new TextBox
            {
                Enabled = false,
                Location = new Point(10 + labelOffsetX, 35 + 4 * controlSpacing),
                Width = 100
            };
            graphPreferencesGroup.Controls.Add(yAxisStartTextBox);
            AddLabel(graphPreferencesGroup, "Y-Axis Start:", new Point(10, 35 + 4 * controlSpacing));

            // Initialize Y-Axis End TextBox and set it disabled by default
            yAxisEndTextBox = new TextBox
            {
                Enabled = false,
                Location = new Point(10 + labelOffsetX, 35 + 5 * controlSpacing),
                Width = 100
            };
            graphPreferencesGroup.Controls.Add(yAxisEndTextBox);
            AddLabel(graphPreferencesGroup, "Y-Axis End:", new Point(10, 35 + 5 * controlSpacing));

            // Initialize and add items to xScaleComboBox with event handler to update graph on selection change
            xScaleComboBox = new ComboBox
            {
                Location = new Point(10 + labelOffsetX, 35 + 6 * controlSpacing),
                Width = 180,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            xScaleComboBox.Items.AddRange(new object[] { "Linear", "Logarithmic" });
            xScaleComboBox.SelectedIndex = 0;
            graphPreferencesGroup.Controls.Add(xScaleComboBox);
            AddLabel(graphPreferencesGroup, "X Scale:", new Point(10, 35 + 6 * controlSpacing));

            // Initialize and add items to yScaleComboBox with event handler
            yScaleComboBox = new ComboBox
            {
                Location = new Point(10 + labelOffsetX, 35 + 7 * controlSpacing),
                Width = 180,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            yScaleComboBox.Items.AddRange(new object[] { "Linear", "Logarithmic" });
            yScaleComboBox.SelectedIndex = 0;
            graphPreferencesGroup.Controls.Add(yScaleComboBox);
            AddLabel(graphPreferencesGroup, "Y Scale:", new Point(10, 35 + 7 * controlSpacing));

            // Initialize default values for axis range text boxes
            SetDefaultAxisValues();

            // Now attach the event handlers for auto range checkbox changes
            autoRangeXCheckBox.CheckedChanged += AutoRangeXCheckBox_CheckedChanged;
            autoRangeYCheckBox.CheckedChanged += AutoRangeYCheckBox_CheckedChanged;

            // Set the scale combo boxes to update the graph on change
            xScaleComboBox.SelectedIndexChanged += (sender, e) => UpdateGraph();
            yScaleComboBox.SelectedIndexChanged += (sender, e) => UpdateGraph();

            xAxisStartTextBox.TextChanged += TextBox_TextChanged;
            xAxisEndTextBox.TextChanged += TextBox_TextChanged;
            yAxisStartTextBox.TextChanged += TextBox_TextChanged;
            yAxisEndTextBox.TextChanged += TextBox_TextChanged;

            xScaleComboBox.SelectedIndexChanged += (sender, e) =>
            {
                ValidateLogarithmicScale(xAxisStartTextBox.Text, xScaleComboBox);
                UpdateGraph();
            };
            yScaleComboBox.SelectedIndexChanged += (sender, e) =>
            {
                ValidateLogarithmicScale(yAxisStartTextBox.Text, yScaleComboBox);
                UpdateGraph();
            };
        }
        private void ValidateLogarithmicScale(string startValueText, ComboBox scaleComboBox)
        {
            if (scaleComboBox.SelectedItem.ToString() == "Logarithmic" && double.TryParse(startValueText, out double startValue) && startValue <= 0)
            {
                MessageBox.Show("Logarithmic scale cannot be used with non-positive start values.", "Invalid Scale", MessageBoxButtons.OK, FormsMessageBoxIcon.Warning);
                scaleComboBox.SelectedIndex = 0; // Reset to Linear
            }
        }
        private void SetDefaultAxisValues()
        {
            // These should be replaced with the actual default values or calculations
            xAxisStartTextBox.Text = "10";
            xAxisEndTextBox.Text = "20000";
            yAxisStartTextBox.Text = "0";
            yAxisEndTextBox.Text = "100";
        }
        private void AddLabel(System.Windows.Forms.Control parent, string text, Point location)
        {
            Label label = new Label
            {
                Text = text,
                Location = location,
                AutoSize = true
            };
            parent.Controls.Add(label);
        }
        private void AutoRangeXCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            bool isAutoRangeEnabled = autoRangeXCheckBox.Checked;
            xAxisStartTextBox.Enabled = !isAutoRangeEnabled;
            xAxisEndTextBox.Enabled = !isAutoRangeEnabled;

            if (isAutoRangeEnabled)
            {
                SetDefaultAxisValues();
            }

            UpdateGraph();
        }
        private void AutoRangeYCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            bool isAutoRangeEnabled = autoRangeYCheckBox.Checked;
            yAxisStartTextBox.Enabled = !isAutoRangeEnabled;
            yAxisEndTextBox.Enabled = !isAutoRangeEnabled;

            if (isAutoRangeEnabled)
            {
                SetDefaultAxisValues();
            }

            UpdateGraph();
        }
        private void UpdateGraph()
        {
            if (currentSelectedResult == null)
            {
                MessageBox.Show("No result selected to update the graph.", "Update Graph", MessageBoxButtons.OK, FormsMessageBoxIcon.Warning);
                return;
            }

            string xScale = xScaleComboBox.SelectedItem?.ToString();
            string yScale = yScaleComboBox.SelectedItem?.ToString();

            if (xScale == null || yScale == null)
            {
                MessageBox.Show("Please select a scale for both X and Y axes.", "Update Graph", MessageBoxButtons.OK, FormsMessageBoxIcon.Warning);
                return;
            }

            // Set Axis Limits only if AutoRange is not checked
            if (!autoRangeXCheckBox.Checked)
            {
                if (!double.TryParse(xAxisStartTextBox.Text, out double tempXAxisStart))
                {
                    MessageBox.Show("Invalid X-Axis Start value.", "Invalid Input", MessageBoxButtons.OK, FormsMessageBoxIcon.Warning);
                    return;
                }
                if (!double.TryParse(xAxisEndTextBox.Text, out double tempXAxisEnd))
                {
                    MessageBox.Show("Invalid X-Axis End value.", "Invalid Input", MessageBoxButtons.OK, FormsMessageBoxIcon.Warning);
                    return;
                }
                currentSelectedResult.XAxisStart = tempXAxisStart;
                currentSelectedResult.XAxisEnd = tempXAxisEnd;
            }
            else
            {
                currentSelectedResult.XAxisStart = double.NaN; // Reset to auto-range
                currentSelectedResult.XAxisEnd = double.NaN;
            }

            if (!autoRangeYCheckBox.Checked)
            {
                if (!double.TryParse(yAxisStartTextBox.Text, out double tempYAxisStart))
                {
                    MessageBox.Show("Invalid Y-Axis Start value.", "Invalid Input", MessageBoxButtons.OK, FormsMessageBoxIcon.Warning);
                    return;
                }
                if (!double.TryParse(yAxisEndTextBox.Text, out double tempYAxisEnd))
                {
                    MessageBox.Show("Invalid Y-Axis End value.", "Invalid Input", MessageBoxButtons.OK, FormsMessageBoxIcon.Warning);
                    return;
                }
                currentSelectedResult.YAxisStart = tempYAxisStart;
                currentSelectedResult.YAxisEnd = tempYAxisEnd;
            }
            else
            {
                currentSelectedResult.YAxisStart = double.NaN; // Reset to auto-range
                currentSelectedResult.YAxisEnd = double.NaN;
            }

            currentSelectedResult.AutoRangeX = autoRangeXCheckBox.Checked;
            currentSelectedResult.AutoRangeY = autoRangeYCheckBox.Checked;
            currentSelectedResult.XScale = xScale;
            currentSelectedResult.YScale = yScale;

            DisplayGraph(currentSelectedResult, currentIsUpperLimit);

            if (graphPanel.Controls.Count > 0 && graphPanel.Controls[0] is Chart chart)
            {
                ChartArea chartArea = chart.ChartAreas[0];
                chartArea.AxisX.IsLogarithmic = xScaleComboBox.SelectedItem.ToString() == "Logarithmic";
                chartArea.AxisY.IsLogarithmic = yScaleComboBox.SelectedItem.ToString() == "Logarithmic";

                // Redraw the chart
                chart.Invalidate();
            }
        }
        private void TextBox_TextChanged(object sender, EventArgs e)
        {
            // Restart the timer whenever text changes
            updateGraphTimer.Stop();
            updateGraphTimer.Start();
        }
        private void UpdateGraphTimer_Tick(object sender, EventArgs e)
        {
            // Stop the timer and update the graph
            updateGraphTimer.Stop();
            UpdateGraph();
        }
        private void InitializeMeterValuesBindingList()
        {
            AdjustControlPositions(50);
            foreach (TabPage tabPage in limitValuesTabControl.TabPages)
            {
                if (tabPage.Controls.Count > 0 && tabPage.Controls[0] is DataGridView dataGridView)
                {
                    BindingList<MeterValue> bindingList = new BindingList<MeterValue>();
                    dataGridView.DataSource = bindingList;
                }
            }
        }
        private void UpdateMeterValuesDataGridView(ResultData result, bool isUpperLimit)
        {
            try
            {
                double[] meterValues = isUpperLimit ? result.MeterUpperLimitValues : result.MeterLowerLimitValues;

                // Check if meter values are null or empty
                if (meterValues == null || meterValues.Length == 0)
                {
                    //LogError("Meter values are null or empty.");
                    return;
                }

                // Assuming each tab page corresponds to a channel
                for (int channelIndex = 0; channelIndex < limitValuesTabControl.TabPages.Count; channelIndex++)
                {
                    if (limitValuesTabControl.TabPages[channelIndex].Controls[0] is DataGridView dataGridView)
                    {
                        BindingList<MeterValue> bindingList = dataGridView.DataSource as BindingList<MeterValue>;

                        if (bindingList != null)
                        {
                            bindingList.Clear();
                            bindingList.Add(new MeterValue { Value = meterValues[channelIndex] });
                        }
                    }
                }

            }
            catch (Exception ex)
            {

                // LogError("Failed to update meter values in DataGridView: " + ex.Message);
            }
        }
        private void AddRowToDataGridView(bool addBefore)
        {
            int channelIndex = limitValuesTabControl.SelectedIndex;
            if (channelIndex < 0 || channelIndex >= channelData.Count)
            {
                LogToTextBox("Invalid channel index.");
                return;
            }
            LogToTextBox("here");
            DataGridView dataGridView = limitValuesTabControl.TabPages[channelIndex].Controls[0] as DataGridView;
            int selectedIndex = dataGridView.SelectedCells.Count > 0 ? dataGridView.SelectedCells[0].RowIndex : -1;

            // Handle no selection
            if (selectedIndex == -1) selectedIndex = dataGridView.Rows.Count - 1;
            int insertIndex = addBefore ? selectedIndex : selectedIndex + 1;

            var newRow = new MeterValue { XValue = 0, YValue = 0 };
            channelData[channelIndex].Insert(insertIndex, newRow);

            RefreshDataGridView(channelIndex);
            UpdateResultDataArraysForSpecificChannel(channelIndex);
            UpdateLimitDataTextBox(currentSelectedResult, currentIsUpperLimit);
        }
        private void UpdateResultDataArraysForSpecificChannel(int channelIndex)
        {
            if (currentSelectedResult == null || !channelData.ContainsKey(channelIndex))
            {
                return; // No data to update or invalid channel index
            }

            List<double> xValues = new List<double>();
            List<double> yValues = new List<double>();

            // Compile all X and Y values from the channel data
            foreach (var meterValue in channelData[channelIndex])
            {
                xValues.Add(meterValue.XValue);
                yValues.Add(meterValue.YValue);
            }

            // Temporary arrays for resizing
            double[] tempXValues, tempYValues;

            // Update and resize the arrays for currentSelectedResult
            if (currentIsUpperLimit)
            {
                tempXValues = currentSelectedResult.XValueUpperLimitValues;
                tempYValues = currentSelectedResult.YValueUpperLimitValues;

                Array.Resize(ref tempXValues, xValues.Count);
                Array.Resize(ref tempYValues, yValues.Count);

                xValues.CopyTo(tempXValues, 0);
                yValues.CopyTo(tempYValues, 0);

                currentSelectedResult.XValueUpperLimitValues = tempXValues;
                currentSelectedResult.YValueUpperLimitValues = tempYValues;
            }
            else
            {
                tempXValues = currentSelectedResult.XValueLowerLimitValues;
                tempYValues = currentSelectedResult.YValueLowerLimitValues;

                Array.Resize(ref tempXValues, xValues.Count);
                Array.Resize(ref tempYValues, yValues.Count);

                xValues.CopyTo(tempXValues, 0);
                yValues.CopyTo(tempYValues, 0);

                currentSelectedResult.XValueLowerLimitValues = tempXValues;
                currentSelectedResult.YValueLowerLimitValues = tempYValues;
            }
            RefreshDataGridViewForAllChannels();
        }
        private void RefreshDataGridView(int channelIndex)
        {
            // Assuming each TabPage corresponds to a channel and contains a DataGridView as its first control
            if (limitValuesTabControl.TabPages.Count > channelIndex)
            {
                var tabPage = limitValuesTabControl.TabPages[channelIndex];
                if (tabPage.Controls.Count > 0 && tabPage.Controls[0] is DataGridView dataGridView)
                {
                    dataGridView.DataSource = null; // Detach the DataSource to refresh
                    dataGridView.DataSource = new BindingList<MeterValue>(channelData[channelIndex]); // Reattach the DataSource
                }
            }
        }
        private void UpdateXYValuesTextBox(List<double> xValues, List<double> yValues)
        {
            TextBox xyValuesTextBox = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Location = new Point(10, 10), // Adjust as needed
                Size = new Size(300, 200) // Adjust as needed
            };
            this.Controls.Add(xyValuesTextBox);
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("X Values\tY Values");
            for (int i = 0; i < xValues.Count; i++)
            {
                sb.AppendLine($"{xValues[i]}\t{yValues[i]}");
            }
            xyValuesTextBox.Text = sb.ToString();
        }
        private void RefreshDataGridViewForAllChannels()
        {
            // Refresh DataGridView for all channels

            for (int channelIndex = 0; channelIndex < currentSelectedResult.ChannelCount - 1; channelIndex++)
            {
                if (limitValuesTabControl.TabPages.Count > channelIndex)
                {
                    TabPage tabPage = limitValuesTabControl.TabPages[channelIndex];
                    if (tabPage.Controls.Count > 0 && tabPage.Controls[0] is DataGridView dataGridView)
                    {
                        UpdateDataGridViewWithResultValues(dataGridView, currentSelectedResult, channelIndex, currentIsUpperLimit);
                    }
                }
            }
        }
        private string filepath;
        private string getFile()
        {
            return filepath;
        }
        private void setFile(string newfilepath)
        {
            filepath = newfilepath;
        }
        private void ImportJsonFile()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "LYC files (*.lyc)|*.lyc",
                Title = "Import LYC File"
            };

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                string filePath = openFileDialog.FileName;
                setFile(filePath);
                string jsonData = File.ReadAllText(filePath);
                List<JsonLimitData> importedData = JsonConvert.DeserializeObject<List<JsonLimitData>>(jsonData);

                LogToTextBox("A readable .lyc file has been selected.");
                CrossReferenceAndApplyLimits(importedData);
                LogToTextBox("The .lyc file has successfully loaded into the APLimitEditor.");

                /*int signalPathIndex = currentSelectedResult.SignalPathIndex; // Assuming this property exists
                int measurementIndex = currentSelectedResult.MeasurementIndex; // Assuming this property exists
                string resultName = currentSelectedResult.Name; // Assuming this property exists
                bool isUpperLimit = currentIsUpperLimit;
                ResultData resultData = FindResultData(signalPathIndex, measurementIndex, resultName);
                IGraph graph = null;
                foreach (var g in APx.ActiveMeasurement.Graphs)
                {
                    if (g is IGraph currentGraph && currentGraph.Name == resultName)
                    {
                        graph = currentGraph;
                        break;
                    }
                }

                UpdateButtonStatesForResult(currentSelectedResult);
                if (graph.Result.IsXYGraph)
                {


                    if (resultData.XValueUpperLimitValues.Length != 0 || resultData.XValueLowerLimitValues.Length != 0)
                    {
                        LogToTextBox($"Processing XY Graph for {(isUpperLimit ? "Upper" : "Lower")} Limits.");
                        ProcessXYGraph(graph, resultData, isUpperLimit);

                    }
                }
                else if (graph.Result.IsMeterGraph)
                {
                    LogToTextBox($"Processing Meter Graph for {(isUpperLimit ? "Upper" : "Lower")} Limits.");
                    ProcessMeterGraph(graph, resultData, isUpperLimit);
                }
                else
                {
                    //MessageBox.Show("The selected graph is not an XY or Meter graph.", "Export Error", MessageBoxButtons.OK, FormsMessageBoxIcon.Error);
                    LogToTextBox("Export error: Selected graph is not an XY or Meter graph.");
                }*/
            }

        }
        private async void CrossReferenceAndApplyLimits(List<JsonLimitData> importedData)
        {
            bool limitsUpdated = false;
            int count = 1;
            foreach (var data in importedData)
            {
                foreach (var signalPath in checkedData)
                {
                    if (signalPath.Name == data.SignalPathName)
                    {
                        foreach (var measurement in signalPath.Measurements)
                        {
                            if (measurement.Name == data.MeasurementName)
                            {
                                foreach (var result in measurement.Results)
                                {
                                    if (result.Name == data.ResultName)
                                    {




                                        UpdateResultWithJsonData(result, data);



                                        limitsUpdated = true;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (limitsUpdated)
            {
                LogToTextBox("Limits have been updated from .lyc file.");
                RefreshUIComponents();
            }
        }
        private void RefreshUIComponents()
        {
            if (currentSelectedResult != null)
            {
                // Assuming currentSelectedResult is part of the checkedData
                var signalPath = checkedData.FirstOrDefault(sp => sp.Measurements.Any(m => m.Results.Contains(currentSelectedResult)));
                var measurement = signalPath?.Measurements.FirstOrDefault(m => m.Results.Contains(currentSelectedResult));

                if (measurement != null)
                {
                    foreach (var result in measurement.Results)
                    {
                        // Assuming each tab corresponds to a channel in the limitValuesTabControl
                        for (int channelIndex = 0; channelIndex < limitValuesTabControl.TabPages.Count; channelIndex++)
                        {
                            if (limitValuesTabControl.TabPages[channelIndex].Controls[0] is DataGridView dataGridView)
                            {
                                UpdateDataGridViewWithResultValues(dataGridView, result, channelIndex, currentIsUpperLimit);
                            }
                        }

                        // Redraw the graph for each result
                        DisplayGraph(result, currentIsUpperLimit);
                    }
                    // Update text box with limit data
                    //UpdateLimitDataTextBox(currentSelectedResult, currentIsUpperLimit);

                    LogToTextBox("DataGridView and Graph have been updated.");
                }
                else
                {
                    LogToTextBox("Measurement or Signal Path not found for the selected result.");
                }
            }
            else
            {
                LogToTextBox("No result selected, unable to refresh DataGridView and Graph.");
            }
        }
        private void UpdateResultWithJsonData(ResultData result, JsonLimitData jsonData)
        {
            // Check if either result or jsonData is null
            if (result == null)
            {
                LogToTextBox("Error: 'result' is null in UpdateResultWithJsonData.");
                return;
            }

            if (jsonData == null)
            {
                LogToTextBox("Error: 'jsonData' is null in UpdateResultWithJsonData.");
                return;
            }

            result.UpperLimitEnabled = jsonData.UpperLimitEnabled;
            result.LowerLimitEnabled = jsonData.LowerLimitEnabled;

            // Update the properties only if the corresponding jsonData property is not null
            if (jsonData.MeterUpperLimitValues != null)
            {
                result.MeterUpperLimitValues = jsonData.MeterUpperLimitValues;
            }

            if (jsonData.MeterLowerLimitValues != null)
            {
                result.MeterLowerLimitValues = jsonData.MeterLowerLimitValues;
            }

            if (jsonData.XValueUpperLimitValues != null)
            {
                result.XValueUpperLimitValues = jsonData.XValueUpperLimitValues;
            }

            if (jsonData.XValueLowerLimitValues != null)
            {
                result.XValueLowerLimitValues = jsonData.XValueLowerLimitValues;
            }

            if (jsonData.YValueUpperLimitValues != null)
            {
                result.YValueUpperLimitValues = jsonData.YValueUpperLimitValues;
            }

            if (jsonData.YValueLowerLimitValues != null)
            {
                result.YValueLowerLimitValues = jsonData.YValueLowerLimitValues;
            }

            LogToTextBox($"Updated limits for '{result.Name}'.");
            // Call method to update the button states after loading data
            currentIsUpperLimit = true;
            UpdateButtonStatesForResult(result);
        }
        public delegate void LimitImportedHandler(List<string[]> limitData, bool applyToSpecificChannel, int selectedChannel);
        // Define a class for meter values that will be used for data binding
        public class MeterValue : INotifyPropertyChanged
        {
            private double value;
            private double xValue;
            private double yValue;

            public event PropertyChangedEventHandler PropertyChanged;

            protected void OnPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }

            public double Value
            {
                get => value;
                set
                {
                    if (this.value != value)
                    {
                        this.value = value;
                        OnPropertyChanged(nameof(Value));
                    }
                }
            }

            public double XValue
            {
                get => xValue;
                set
                {
                    if (xValue != value)
                    {
                        xValue = value;
                        OnPropertyChanged(nameof(XValue));
                    }
                }
            }

            public double YValue
            {
                get => yValue;
                set
                {
                    if (yValue != value)
                    {
                        yValue = value;
                        OnPropertyChanged(nameof(YValue));
                    }
                }
            }
            private int pointIndex;
            public int PointIndex
            {
                get => pointIndex;
                set
                {
                    if (pointIndex != value)
                    {
                        pointIndex = value;
                        OnPropertyChanged(nameof(PointIndex));
                    }
                }
            }
            private int measurementIndex;
            public int MeasurementIndex
            {
                get => measurementIndex;
                set
                {
                    if (measurementIndex != value)
                    {
                        measurementIndex = value;
                        OnPropertyChanged(nameof(MeasurementIndex));
                    }
                }
            }

            private string resultName;
            public string ResultName
            {
                get => resultName;
                set
                {
                    if (resultName != value)
                    {
                        resultName = value;
                        OnPropertyChanged(nameof(ResultName));
                    }
                }
            }
        }
        private void FormAPLimitEditor_Load(object sender, EventArgs e)
        {

        }
    }
    public class JsonLimitData
    {
        public string SignalPathName { get; set; }
        public string MeasurementName { get; set; }
        public string ResultName { get; set; }
        public bool UpperLimitEnabled { get; set; }
        public bool LowerLimitEnabled { get; set; }
        public double[] MeterUpperLimitValues { get; set; }
        public double[] MeterLowerLimitValues { get; set; }
        public double[] XValueUpperLimitValues { get; set; }
        public double[] XValueLowerLimitValues { get; set; }
        public double[] YValueUpperLimitValues { get; set; }
        public double[] YValueLowerLimitValues { get; set; }
        // Add other properties as per your JSON structure
    }
}
public class MeterValue
{
    public double Value { get; set; }
}

public partial class CustomMessageBox : Form
{
    public enum CustomDialogResult
    {
        Remove,
        Add,
        Match,
        None
    }

    public CustomDialogResult Result { get; private set; } = CustomDialogResult.None;

    public CustomMessageBox(string message)
    {
        InitializeComponent();
        lblMessage.Text = message;
    }

    private void btnRemove_Click(object sender, EventArgs e)
    {
        Result = CustomDialogResult.Remove;
        this.Close();
    }

    private void btnAdd_Click(object sender, EventArgs e)
    {
        Result = CustomDialogResult.Add;
        this.Close();
    }

    private void btnMatch_Click(object sender, EventArgs e)
    {
        Result = CustomDialogResult.Match;
        this.Close();
    }
}

partial class CustomMessageBox
{
    private System.ComponentModel.IContainer components = null;
    private System.Windows.Forms.Label lblMessage;
    private System.Windows.Forms.Button btnRemove;
    private System.Windows.Forms.Button btnAdd;
    private System.Windows.Forms.Button btnMatch;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        this.lblMessage = new System.Windows.Forms.Label();
        this.btnRemove = new System.Windows.Forms.Button();
        this.btnAdd = new System.Windows.Forms.Button();
        this.btnMatch = new System.Windows.Forms.Button();
        this.SuspendLayout();
        // 
        // lblMessage
        // 
        this.lblMessage.AutoSize = true;
        this.lblMessage.Location = new System.Drawing.Point(13, 13);
        this.lblMessage.Name = "lblMessage";
        this.lblMessage.Size = new System.Drawing.Size(50, 13);
        this.lblMessage.TabIndex = 0;
        this.lblMessage.Text = "Message";
        // 
        // btnRemove
        // 
        this.btnRemove.Location = new System.Drawing.Point(16, 50);
        this.btnRemove.Name = "btnRemove";
        this.btnRemove.Size = new System.Drawing.Size(75, 23);
        this.btnRemove.TabIndex = 1;
        this.btnRemove.Text = "Discard";
        this.btnRemove.UseVisualStyleBackColor = true;
        this.btnRemove.Click += new System.EventHandler(this.btnRemove_Click);
        // 
        // btnAdd
        // 
        this.btnAdd.Location = new System.Drawing.Point(97, 50);
        this.btnAdd.Name = "btnAdd";
        this.btnAdd.Size = new System.Drawing.Size(75, 23);
        this.btnAdd.TabIndex = 2;
        this.btnAdd.Text = "Add";
        this.btnAdd.UseVisualStyleBackColor = true;
        this.btnAdd.Click += new System.EventHandler(this.btnAdd_Click);
        // 
        // btnMatch
        // 
        this.btnMatch.Location = new System.Drawing.Point(178, 50);
        this.btnMatch.Name = "btnMatch";
        this.btnMatch.Size = new System.Drawing.Size(75, 23);
        this.btnMatch.TabIndex = 3;
        this.btnMatch.Text = "Match";
        this.btnMatch.UseVisualStyleBackColor = true;
        this.btnMatch.Click += new System.EventHandler(this.btnMatch_Click);
        // 
        // CustomMessageBox
        // 
        this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.ClientSize = new System.Drawing.Size(284, 91);
        this.Controls.Add(this.btnMatch);
        this.Controls.Add(this.btnAdd);
        this.Controls.Add(this.btnRemove);
        this.Controls.Add(this.lblMessage);
        this.Name = "CustomMessageBox";
        this.Text = "CustomMessageBox";
        this.ResumeLayout(false);
        this.PerformLayout();
    }

}
