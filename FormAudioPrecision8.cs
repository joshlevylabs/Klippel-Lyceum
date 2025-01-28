using LAPxv8;
using System;
using AudioPrecision.API;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.Windows.Forms.DataVisualization.Charting;
using System.Threading.Tasks;
using System.IO;
using System.Net.Http;
using ClosedXML.Excel;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Linq;
using DocumentFormat.OpenXml.Office2010.PowerPoint;
using static LAPxv8.FormAudioPrecision8;
using static LAPxv8.FormSessionManager;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.ComponentModel;



namespace LAPxv8
{
    public partial class FormAudioPrecision8 : BaseForm
    {
        private APx500 APx = new APx500();
        private TreeView resultsTreeView; // Declare TreeView at the class level
        private TextBox detailsTextBox;
        private List<SignalPathData> checkedData = new List<SignalPathData>();
        private Panel graphPanel;
        private ComboBox xScaleComboBox;
        private ComboBox yScaleComboBox;
        private TextBox xAxisStartTextBox, xAxisEndTextBox, yAxisStartTextBox, yAxisEndTextBox;
        private CheckBox autoRangeXCheckBox;
        private CheckBox autoRangeYCheckBox;
        private CheckBox showLimitsCheckBox;
        private string accessToken;
        private string refreshToken;
        private TextBox propertiesTextBox;
        public event Action<ProjectSession> OnSessionDataCreated;
        private TextBox logTextBox;
        private double xStart, xEnd, yStart, yEnd;
        private bool xStartValid, xEndValid, yStartValid, yEndValid;

        public List<ProjectSession> sessionList = new List<ProjectSession>();

        // This should be the only declaration of SessionDataHandler
        public delegate void SessionDataHandler(ProjectSession newSession);

        public FormAudioPrecision8(string accessToken, string refreshToken)
        {
            this.accessToken = accessToken;
            this.refreshToken = refreshToken;

            InitializeComponents();
            InitializeWindow();
            LogManager.Initialize(); // Initialize the log system

        }

        protected override void AddCustomMenuItems()
        {
            // APx500 Menu
            ToolStripMenuItem apx500Menu = new ToolStripMenuItem("AP Controls");

            ToolStripMenuItem runScriptMenuItem = new ToolStripMenuItem("Run Script");
            runScriptMenuItem.Click += RunScriptButton_Click;
            apx500Menu.DropDownItems.Add(runScriptMenuItem);

            ToolStripMenuItem getCheckedDataMenuItem = new ToolStripMenuItem("Get Checked Data");
            getCheckedDataMenuItem.Click += GetCheckedDataButton_Click;
            apx500Menu.DropDownItems.Add(getCheckedDataMenuItem);

            ToolStripMenuItem loadLimitsMenuItem = new ToolStripMenuItem("Load Limit Family");
            loadLimitsMenuItem.Click += LoadLimitsMenuItem_Click;
            apx500Menu.DropDownItems.Add(loadLimitsMenuItem);

            menuStrip.Items.Add(apx500Menu);

            // Download Menu
            ToolStripMenuItem downloadMenu = new ToolStripMenuItem("Download");

            ToolStripMenuItem downloadDataMenuItem = new ToolStripMenuItem("Download Data");
            downloadDataMenuItem.Click += DownloadJsonButton_Click;
            downloadMenu.DropDownItems.Add(downloadDataMenuItem);

            ToolStripMenuItem downloadLimitsMenuItem = new ToolStripMenuItem("Download Limits");
            downloadLimitsMenuItem.Click += DownloadLimitsJsonButton_Click;
            downloadMenu.DropDownItems.Add(downloadLimitsMenuItem);

            menuStrip.Items.Add(downloadMenu);

            // Open Menu
            ToolStripMenuItem openMenu = new ToolStripMenuItem("Open");

            ToolStripMenuItem limitEditorMenuItem = new ToolStripMenuItem("Limit Editor");
            limitEditorMenuItem.Click += LimitEditorButton_Click;
            openMenu.DropDownItems.Add(limitEditorMenuItem);

            ToolStripMenuItem sessionsMenuItem = new ToolStripMenuItem("Sessions");
            sessionsMenuItem.Click += SessionsButton_Click;
            openMenu.DropDownItems.Add(sessionsMenuItem);

            // Add "Test Results Grid" Menu Item
            ToolStripMenuItem testResultsGridMenuItem = new ToolStripMenuItem("Test Results Grid");
            testResultsGridMenuItem.Click += TestResultsGridMenuItem_Click;
            openMenu.DropDownItems.Add(testResultsGridMenuItem);

            // Add Log Window menu item
            ToolStripMenuItem logWindowMenuItem = new ToolStripMenuItem("Log Window");
            logWindowMenuItem.Click += LogWindowMenuItem_Click;
            openMenu.DropDownItems.Add(logWindowMenuItem);

            menuStrip.Items.Add(openMenu);
        }
        private void InitializeComponents()
        {
            // Set Form Properties
            this.Text = "Audio Precision Control";
            this.Font = new Font("Segoe UI", 10);
            this.BackColor = Color.FromArgb(45, 45, 45); // Dark Mode background
            this.Size = new Size(1800, 800);
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;

            // Adjust the MenuStrip to hug the top of the window
            menuStrip.Dock = DockStyle.Top; // Ensure the menu bar is docked to the top
            menuStrip.Padding = new Padding(0); // Remove any internal padding from the menu strip
            this.Padding = new Padding(0, 30, 0, 0); // Adding padding to avoid overlap with MenuStrip

            // Calculate new widths based on the 2/3 ratio
            int newWidth = (int)(this.ClientSize.Width * 1 / 3);
            int remainingWidth = this.ClientSize.Width - newWidth - 40; // Adjusting for padding/margins

            // Define Control Colors
            Color buttonBackColor = Color.FromArgb(75, 110, 175);
            Color buttonForeColor = Color.White;

            // Calculate dimensions for dynamic resizing
            int panelWidth = this.ClientSize.Width / 3 - 30; // Adjust for spacing and padding
            int totalHeight = this.ClientSize.Height - 80; // Account for menu strip and padding
            int resultsHeight = (int)(totalHeight * 0.5); // 50% height for Results
            int detailsHeight = (int)(totalHeight * 0.3); // 30% height for Details
            int propertiesHeight = totalHeight - resultsHeight - detailsHeight; // Remaining height for Global Properties

            // GroupBox for Results
            GroupBox resultsGroup = new GroupBox
            {
                Text = "Results",
                ForeColor = Color.White,
                Location = new Point(20, 60),
                Size = new Size(panelWidth, resultsHeight),
                BackColor = Color.FromArgb(45, 45, 45)
            };
            Controls.Add(resultsGroup);

            // Results TreeView
            resultsTreeView = new TreeView
            {
                Location = new Point(10, 30),
                Size = new Size(panelWidth - 20, resultsHeight - 40),
                Font = new Font("Segoe UI", 10),
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            resultsGroup.Controls.Add(resultsTreeView);
            resultsTreeView.AfterSelect += ResultsTreeView_AfterSelect;

            // GroupBox for Details
            GroupBox detailsGroup = new GroupBox
            {
                Text = "Details",
                ForeColor = Color.White,
                Location = new Point(20, 60 + resultsHeight + 10),
                Size = new Size(panelWidth, detailsHeight),
                BackColor = Color.FromArgb(45, 45, 45)
            };
            Controls.Add(detailsGroup);

            // Details TextBox
            detailsTextBox = new TextBox
            {
                Location = new Point(10, 30),
                Size = new Size(panelWidth - 20, detailsHeight - 40),
                Font = new Font("Segoe UI", 10),
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                ReadOnly = true,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            detailsGroup.Controls.Add(detailsTextBox);

            // GroupBox for Global Properties
            GroupBox propertiesGroup = new GroupBox
            {
                Text = "Global Properties",
                ForeColor = Color.White,
                Location = new Point(20, 60 + resultsHeight + detailsHeight + 20),
                Size = new Size(panelWidth, propertiesHeight),
                BackColor = Color.FromArgb(45, 45, 45)
            };
            Controls.Add(propertiesGroup);

            // Properties TextBox
            propertiesTextBox = new TextBox
            {
                Location = new Point(10, 30),
                Size = new Size(panelWidth - 20, propertiesHeight - 40),
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                ReadOnly = true,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            propertiesGroup.Controls.Add(propertiesTextBox);

            // Initialize Graph Preferences
            InitializeGraphPreferences(newWidth, remainingWidth);
        }

        private void InitializeGraphPreferences(int newWidth, int remainingWidth)
        {
            // GroupBox for Graph
            GroupBox graphGroup = new GroupBox
            {
                Text = "Graph",
                ForeColor = Color.White,
                Location = new Point(newWidth + 40, 60), // Adjusted to be placed to the right of other elements
                Size = new Size(remainingWidth, 600), // Adjust the height to make room for the Graph Preferences frame
                BackColor = Color.FromArgb(45, 45, 45)
            };
            Controls.Add(graphGroup);

            // Graph Panel
            graphPanel = new Panel
            {
                Location = new Point(10, 30),
                Size = new Size(remainingWidth - 20, 510), // Adjusted height to match the graph group
                BackColor = Color.FromArgb(45, 45, 45)
            };
            graphGroup.Controls.Add(graphPanel);

            // GroupBox for Graph Preferences
            GroupBox graphPreferencesGroup = new GroupBox
            {
                Location = new Point(newWidth + 40, 670), // Below graph
                Size = new Size(remainingWidth, 160),
                Text = "Graph Preferences",
                ForeColor = Color.White,
                BackColor = Color.FromArgb(45, 45, 45)
            };
            Controls.Add(graphPreferencesGroup);

            // Initialize other graph preference controls...
            InitializeGraphPreferenceControls(graphPreferencesGroup);
        }
        private void InitializeGraphPreferenceControls(GroupBox graphPreferencesGroup)
        {
            TableLayoutPanel tableLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 6, // 5 columns
                RowCount = 3,    // 3 rows
                BackColor = Color.FromArgb(45, 45, 45),
                Padding = new Padding(5) // Add some padding for alignment
            };

            // Set fixed width for each column
            for (int i = 0; i < 5; i++)
            {
                tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 175));
            }

            // Set equal row heights
            for (int i = 0; i < 3; i++)
            {
                tableLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33f));
            }

            // Initialize controls
            xScaleComboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            xScaleComboBox.Items.AddRange(new[] { "Linear", "Logarithmic" });
            xScaleComboBox.SelectedIndex = 0;

            yScaleComboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            yScaleComboBox.Items.AddRange(new[] { "Linear", "Logarithmic" });
            yScaleComboBox.SelectedIndex = 0;

            autoRangeXCheckBox = new CheckBox
            {
                Text = "Auto-Range X",
                ForeColor = Color.White,
                BackColor = Color.FromArgb(45, 45, 45),
                Checked = true,
                AutoSize = true
            };
            autoRangeXCheckBox.CheckedChanged += AutoRangeXCheckBox_CheckedChanged;

            autoRangeYCheckBox = new CheckBox
            {
                Text = "Auto-Range Y",
                ForeColor = Color.White,
                BackColor = Color.FromArgb(45, 45, 45),
                Checked = true,
                AutoSize = true
            };
            autoRangeYCheckBox.CheckedChanged += AutoRangeYCheckBox_CheckedChanged;

            showLimitsCheckBox = new CheckBox
            {
                Text = "Show Limits",
                ForeColor = Color.White,
                BackColor = Color.FromArgb(45, 45, 45),
                Checked = true,
                AutoSize = true
            };

            xAxisStartTextBox = new TextBox
            {
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White
            };

            xAxisEndTextBox = new TextBox
            {
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White
            };

            yAxisStartTextBox = new TextBox
            {
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White
            };

            yAxisEndTextBox = new TextBox
            {
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White
            };
            CheckBox showMinorTicksXCheckBox = new CheckBox
            {
                Text = "Show Minor Ticks (X)",
                ForeColor = Color.White,
                BackColor = Color.FromArgb(45, 45, 45),
                Checked = true,
                AutoSize = true
            };
            showMinorTicksXCheckBox.CheckedChanged += (sender, e) =>
            {
                var selectedResult = resultsTreeView.SelectedNode?.Tag as ResultData;
                if (selectedResult != null)
                {
                    selectedResult.ShowMinorTicksX = showMinorTicksXCheckBox.Checked;
                    UpdateGraph();
                }
            };

            CheckBox showMinorTicksYCheckBox = new CheckBox
            {
                Text = "Show Minor Ticks (Y)",
                ForeColor = Color.White,
                BackColor = Color.FromArgb(45, 45, 45),
                Checked = true,
                AutoSize = true
            };
            showMinorTicksYCheckBox.CheckedChanged += (sender, e) =>
            {
                var selectedResult = resultsTreeView.SelectedNode?.Tag as ResultData;
                if (selectedResult != null)
                {
                    selectedResult.ShowMinorTicksY = showMinorTicksYCheckBox.Checked;
                    UpdateGraph();
                }
            };
            // 'Update Graph' Button
            Button updateGraphButton = new Button
            {
                Text = "Update Graph",
                BackColor = Color.FromArgb(75, 110, 175),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Dock = DockStyle.Fill
            };
            updateGraphButton.Click += (sender, e) => UpdateGraph();

            // Add controls to the table layout in the specified order

            // Row 1
            tableLayout.Controls.Add(updateGraphButton, 5, 0);
            tableLayout.Controls.Add(new Label { Text = "X-Axis Scale:", ForeColor = Color.White, TextAlign = ContentAlignment.MiddleLeft }, 1, 0);
            tableLayout.Controls.Add(xScaleComboBox, 2, 0);
            tableLayout.Controls.Add(new Label { Text = "Y-Axis Scale:", ForeColor = Color.White, TextAlign = ContentAlignment.MiddleLeft }, 3, 0);
            tableLayout.Controls.Add(yScaleComboBox, 4, 0);
            tableLayout.Controls.Add(showLimitsCheckBox, 0, 0);


            // Row 2
            tableLayout.Controls.Add(autoRangeXCheckBox, 0, 1);
            tableLayout.Controls.Add(new Label { Text = "X-Axis Low:", ForeColor = Color.White, TextAlign = ContentAlignment.MiddleLeft }, 1, 1);
            tableLayout.Controls.Add(xAxisStartTextBox, 2, 1);
            tableLayout.Controls.Add(new Label { Text = "X-Axis High:", ForeColor = Color.White, TextAlign = ContentAlignment.MiddleLeft }, 3, 1);
            tableLayout.Controls.Add(xAxisEndTextBox, 4, 1);
            tableLayout.Controls.Add(showMinorTicksXCheckBox, 5, 1);

            // Row 3
            tableLayout.Controls.Add(autoRangeYCheckBox, 0, 2);
            tableLayout.Controls.Add(new Label { Text = "Y-Axis Low:", ForeColor = Color.White, TextAlign = ContentAlignment.MiddleLeft }, 1, 2);
            tableLayout.Controls.Add(yAxisStartTextBox, 2, 2);
            tableLayout.Controls.Add(new Label { Text = "Y-Axis High:", ForeColor = Color.White, TextAlign = ContentAlignment.MiddleLeft }, 3, 2);
            tableLayout.Controls.Add(yAxisEndTextBox, 4, 2);
            tableLayout.Controls.Add(showMinorTicksYCheckBox, 5, 2); // Row 3, Column 6

            // Add the table layout to the group box
            graphPreferencesGroup.Controls.Add(tableLayout);
        }

        private void InitializeWindow()
        {
            this.SuspendLayout();
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1850, 850);
            this.MinimumSize = new System.Drawing.Size(1850, 850); 
            this.Name = "FormAudioPrecision8";
            this.Text = "Audio Precision Control";
            this.BackColor = Color.FromArgb(45, 45, 45);
            this.ForeColor = Color.White;

            // Adjust padding to accommodate the title bar only
            this.Padding = new Padding(0, 30, 0, 0); // Top padding of 30 to accommodate the title bar

            this.ResumeLayout(false);
            this.PerformLayout();
        }
        
        public class RoundedRectangleHelper
        {
            [DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
            public static extern IntPtr CreateRoundRectRgn(
                int nLeftRect,     // x-coordinate of upper-left corner
                int nTopRect,      // y-coordinate of upper-left corner
                int nRightRect,    // x-coordinate of lower-right corner
                int nBottomRect,   // y-coordinate of lower-right corner
                int nWidthEllipse, // width of ellipse
                int nHeightEllipse // height of ellipse
            );
        }
        private void GetCheckedDataButton_Click(object sender, EventArgs e)
        {
            LogManager.AppendLog("Get Checked Data button clicked.");
            try
            {
                checkedData = GetCheckedData();
                LogManager.AppendLog($"Checked data retrieved: {checkedData.Count} signal paths.");
                foreach (var sp in checkedData)
                {
                    LogManager.AppendLog($"Signal Path: {sp.Name}, Measurements: {sp.Measurements.Count}");
                }

                FillResultsTreeView();
                LogManager.AppendLog("Results TreeView populated successfully.");
                DisplayProperties();
            }
            catch (Exception ex)
            {
                LogManager.AppendLog($"Error in GetCheckedDataButton_Click: {ex.Message}");
            }
        }

        private void LimitEditorButton_Click(object sender, EventArgs e)
        {
            if (checkedData == null || !checkedData.Any())
            {
                MessageBox.Show("No data selected.");
                return;
            }

            FormAPLimitEditor limitEditor = new FormAPLimitEditor(checkedData, accessToken, refreshToken);
            limitEditor.ShowDialog();
        }
        private void AddLoadLimitsMenuItem(object sender, EventArgs e)
        {
            ToolStripMenuItem loadLimitsMenuItem = new ToolStripMenuItem("Load Limits");
            loadLimitsMenuItem.Click += LoadLimitsMenuItem_Click;

            // Find the "AP Controls" menu and add the "Load Limits" option
            foreach (ToolStripMenuItem menuItem in menuStrip.Items)
            {
                if (menuItem.Text == "AP Controls")
                {
                    menuItem.DropDownItems.Add(loadLimitsMenuItem);
                    LogToTextBox("Load Limits option added to the AP Controls menu.");
                    break;
                }
            }
        }
        private void DownloadLimitsJsonButton_Click(object sender, EventArgs e)
        {
            try
            {
                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    Filter = "JSON files (*.json)|*.json|Excel files (*.xlsx)|*.xlsx",
                    Title = "Save Limits Data"
                };

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string filePath = saveFileDialog.FileName;
                    string fileExtension = Path.GetExtension(filePath).ToLower();

                    if (fileExtension == ".json")
                    {
                        DownloadLimitsAsJson(filePath);
                    }
                    else if (fileExtension == ".xlsx")
                    {
                        DownloadLimitsAsExcel(filePath);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}");
            }
        }
        private void DownloadLimitsAsJson(string filePath)
        {
            // Extract the limit data from checkedData
            var limitData = checkedData.SelectMany(sp => sp.Measurements.SelectMany(m => m.Results.Select(r =>
            {
                var sequenceResult = GetSequenceResult(r); // Get the corresponding ISequenceResult
                r.ResultValueType = DetermineResultValuesType(sequenceResult);
                return new
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
                    ResultValueType = DetermineResultValuesType(sequenceResult) // Include result value type
                };
            }))).ToList();

            string serializedData = JsonConvert.SerializeObject(limitData, Formatting.Indented);

            // Save the serialized data to a file
            File.WriteAllText(filePath, serializedData);
            MessageBox.Show("Limit data has been downloaded successfully.");
        }
        private void DownloadLimitsAsExcel(string filePath)
        {
            using (var workbook = new XLWorkbook())
            {
                var sheetNames = new HashSet<string>(); // To ensure unique sheet names

                foreach (var signalPath in checkedData)
                {
                    foreach (var measurement in signalPath.Measurements)
                    {
                        foreach (var result in measurement.Results)
                        {
                            var sequenceResult = GetSequenceResult(result);
                            result.ResultValueType = DetermineResultValuesType(sequenceResult);

                            // Ensure limit values arrays are not null
                            if (result.XValueUpperLimitValues == null)
                            {
                                result.XValueUpperLimitValues = new double[0];
                            }
                            if (result.YValueUpperLimitValues == null)
                            {
                                result.YValueUpperLimitValues = new double[0];
                            }
                            if (result.XValueLowerLimitValues == null)
                            {
                                result.XValueLowerLimitValues = new double[0];
                            }
                            if (result.YValueLowerLimitValues == null)
                            {
                                result.YValueLowerLimitValues = new double[0];
                            }

                            // Abbreviate the sheet name and ensure uniqueness
                            string upperLimitSheetName = AbbreviateSheetName(result.Name + "_UpperLimits", sheetNames);
                            string lowerLimitSheetName = AbbreviateSheetName(result.Name + "_LowerLimits", sheetNames);
                            sheetNames.Add(upperLimitSheetName);
                            sheetNames.Add(lowerLimitSheetName);

                            // Upper Limits
                            if (result.UpperLimitEnabled)
                            {
                                var upperSheet = workbook.Worksheets.Add(upperLimitSheetName);
                                upperSheet.Cell(1, 1).Value = "Full Name:";
                                upperSheet.Cell(1, 2).Value = result.Name + "_UpperLimits";
                                upperSheet.Cell(2, 1).Value = "X Values";
                                upperSheet.Cell(2, 2).Value = "Y Values";
                                for (int i = 0; i < result.XValueUpperLimitValues.Length; i++)
                                {
                                    upperSheet.Cell(i + 3, 1).Value = result.XValueUpperLimitValues[i];
                                    upperSheet.Cell(i + 3, 2).Value = result.YValueUpperLimitValues[i];
                                }
                            }

                            // Lower Limits
                            if (result.LowerLimitEnabled)
                            {
                                var lowerSheet = workbook.Worksheets.Add(lowerLimitSheetName);
                                lowerSheet.Cell(1, 1).Value = "Full Name:";
                                lowerSheet.Cell(1, 2).Value = result.Name + "_LowerLimits";
                                lowerSheet.Cell(2, 1).Value = "X Values";
                                lowerSheet.Cell(2, 2).Value = "Y Values";
                                for (int i = 0; i < result.XValueLowerLimitValues.Length; i++)
                                {
                                    lowerSheet.Cell(i + 3, 1).Value = result.XValueLowerLimitValues[i];
                                    lowerSheet.Cell(i + 3, 2).Value = result.YValueLowerLimitValues[i];
                                }
                            }
                        }
                    }
                }

                workbook.SaveAs(filePath);
                MessageBox.Show("Limit data has been downloaded successfully.");
            }
        }
        private string AbbreviateSheetName(string sheetName, HashSet<string> existingNames)
        {
            if (sheetName.Length > 31)
            {
                sheetName = sheetName.Substring(0, 28) + "...";
            }

            // Ensure the sheet name is unique
            string originalSheetName = sheetName;
            int counter = 1;
            while (existingNames.Contains(sheetName))
            {
                string suffix = $"_{counter}";
                int maxLength = 31 - suffix.Length;
                sheetName = originalSheetName.Substring(0, Math.Min(originalSheetName.Length, maxLength)) + suffix;
                counter++;
            }

            return sheetName;
        }
        private void DisplayProperties()
        {
            var sb = new StringBuilder();

            // APx Measurement Variables
            var measurementVariables = APx.Variables.GetAPxMeasurementVariables();
            if (measurementVariables.Any(varName => !string.IsNullOrEmpty(APx.Variables.GetAPxMeasurementVariable(varName))))
            {
                sb.AppendLine("APx Measurement Variables:");
                foreach (var varName in measurementVariables)
                {
                    var value = APx.Variables.GetAPxMeasurementVariable(varName);
                    if (!string.IsNullOrEmpty(value))
                    {
                        sb.AppendLine($"{varName}: {value}");
                    }
                }
                sb.AppendLine();
            }

            // APx System Variables
            sb.AppendLine("APx System Variables:");
            var systemVariables = APx.Variables.GetAPxSystemVariables();
            var excludedVariables = new HashSet<string> { "SignalPathName", "MeasurementName", "ResultName" };
            foreach (var varName in systemVariables)
            {
                if (!excludedVariables.Contains(varName))
                {
                    var value = APx.Variables.GetAPxSystemVariable(varName);
                    if (!string.IsNullOrEmpty(value))
                    {
                        sb.AppendLine($"{varName}: {value}");
                    }
                }
            }
            sb.AppendLine();

            // User Defined Variables
            sb.AppendLine("User Defined Variables:");
            var userDefinedVariables = APx.Variables.GetUserDefinedVariables();
            foreach (var varName in userDefinedVariables)
            {
                var value = APx.Variables.GetUserDefinedVariable(varName);
                if (!string.IsNullOrEmpty(value))
                {
                    sb.AppendLine($"{varName}: {value}");
                }
            }
            sb.AppendLine();

            propertiesTextBox.Text = sb.ToString();
        }
        private void FillResultsTreeView()
        {
            if (resultsTreeView == null)
            {
                LogManager.AppendLog("Error: resultsTreeView is not initialized.");
                return;
            }

            resultsTreeView.Nodes.Clear(); // Clear any existing nodes

            foreach (var signalPath in checkedData)
            {
                // Create a node for each SignalPath
                TreeNode signalPathNode = new TreeNode(signalPath.Name)
                {
                    Tag = signalPath // Store the SignalPathData object in the node's Tag property
                };

                // Add Measurement nodes to the SignalPath node
                foreach (var measurement in signalPath.Measurements)
                {
                    TreeNode measurementNode = new TreeNode(measurement.Name)
                    {
                        Tag = measurement // Store the MeasurementData object in the node's Tag property
                    };

                    // Add Result nodes to the Measurement node
                    foreach (var result in measurement.Results)
                    {
                        TreeNode resultNode = new TreeNode(result.Name)
                        {
                            Tag = result // Store the ResultData object in the node's Tag property
                        };
                        measurementNode.Nodes.Add(resultNode); // Add Result node to Measurement node
                    }

                    signalPathNode.Nodes.Add(measurementNode); // Add Measurement node to SignalPath node
                }

                resultsTreeView.Nodes.Add(signalPathNode); // Add the SignalPath node to the TreeView
                signalPathNode.Collapse(); // Collapse the node to hide its children by default
            }
        }
        private void ResultsTreeView_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (e.Node?.Tag is ResultData selectedResult)
            {
                // Restore preferences for this result
                xScaleComboBox.SelectedItem = selectedResult.XScale;
                yScaleComboBox.SelectedItem = selectedResult.YScale;
                autoRangeXCheckBox.Checked = selectedResult.AutoRangeX;
                autoRangeYCheckBox.Checked = selectedResult.AutoRangeY;
                xAxisStartTextBox.Text = selectedResult.XAxisStart.ToString();
                xAxisEndTextBox.Text = selectedResult.XAxisEnd.ToString();
                yAxisStartTextBox.Text = selectedResult.YAxisStart.ToString();
                yAxisEndTextBox.Text = selectedResult.YAxisEnd.ToString();
                xScaleComboBox.SelectedItem = selectedResult.XScale;
                yScaleComboBox.SelectedItem = selectedResult.YScale;

                DisplayGraph(selectedResult);
            }
            else
            {
                LogManager.AppendLog("Error: No result selected or result tag is null.");
            }
        }

        private void DisplayGraph(ResultData result)
        {
            if (result == null)
            {
                LogManager.AppendLog("Error: result is null in DisplayGraph.");
                return;
            }
            if (graphPanel == null)
            {
                LogManager.AppendLog("Error: graphPanel is not initialized.");
                return;
            }

            LogManager.AppendLog($"Displaying graph for result: {result.Name}");

            graphPanel.Controls.Clear();
            var sequenceResult = GetSequenceResult(result);
            if (sequenceResult == null)
            {
                LogManager.AppendLog($"Sequence result for {result.Name} is null. DisplayGraph aborted.");
                return;
            }

            // Handle graph types
            switch (DetermineResultValuesType(sequenceResult))
            {
                case "Meter Values":
                    DisplayMeterGraph(sequenceResult, result);
                    xScaleComboBox.Enabled = false; // Disable X-axis scale change for meter values
                    break;

                case "XY Values":
                    // Ensure limits and data arrays are initialized
                    if (result.XValues == null || result.YValuesPerChannel == null || result.YValuesPerChannel.Count == 0)
                    {
                        LogManager.AppendLog($"Result data for {result.Name} does not contain valid XY data.");
                        MessageBox.Show($"Invalid XY data for result {result.Name}.", "Error", MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                        return;
                    }
                    DisplayXYGraph(result, showLimitsCheckBox.Checked);
                    xScaleComboBox.Enabled = true; // Enable X-axis scale change for XY values
                    break;

                default:
                    LogManager.AppendLog($"Unsupported result type for {result.Name}. DisplayGraph aborted.");
                    MessageBox.Show($"Unsupported result type for {result.Name}.", "Error", MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                    return;
            }
        }

        private ISequenceResult GetSequenceResult(ResultData result)
        {
            if (result == null)
            {
                MessageBox.Show("Result data is null.", "Error", MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                return null;
            }

            // Check if the SignalPathIndex is within range
            if (result.SignalPathIndex < 0 || result.SignalPathIndex >= APx.Sequence.Count)
            {
                MessageBox.Show("Signal path index is out of range.", "Error", MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                return null;
            }

            var signalPath = APx.Sequence[result.SignalPathIndex] as ISignalPath;
            if (signalPath == null)
            {
                MessageBox.Show("Signal path is null or invalid.", "Error", MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                return null;
            }

            // Check if the MeasurementIndex is within range
            if (result.MeasurementIndex < 0 || result.MeasurementIndex >= signalPath.Count)
            {
                MessageBox.Show("Measurement index is out of range.", "Error", MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                return null;
            }

            var measurement = signalPath[result.MeasurementIndex] as ISequenceMeasurement;
            if (measurement == null)
            {
                MessageBox.Show("Measurement is null or invalid.", "Error", MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                return null;
            }

            // Check if the Result Index is within range
            if (result.Index < 0 || result.Index >= measurement.SequenceResults.Count)
            {
                MessageBox.Show("Result index is out of range.", "Error", MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                return null;
            }

            var sequenceResult = measurement.SequenceResults[result.Index] as ISequenceResult;
            if (sequenceResult == null)
            {
                MessageBox.Show("Sequence result is null or invalid.", "Error", MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }

            return sequenceResult;
        }
        private void DisplayMeterGraph(ISequenceResult sequenceResult, ResultData resultData)
        {
            if (sequenceResult == null || resultData == null)
            {
                LogManager.AppendLog("Error: Sequence result or result data is null in DisplayMeterGraph.");
                return;
            }

            double[] meterValues = sequenceResult.GetMeterValues();
            if (meterValues == null || meterValues.Length == 0)
            {
                LogManager.AppendLog("Error: Meter values are null or empty.");
                return;
            }

            // Disable X-Axis preferences for Meter Values
            autoRangeXCheckBox.Enabled = false;
            xAxisStartTextBox.Enabled = false;
            xAxisEndTextBox.Enabled = false;

            // Disable logarithmic scaling options
            xScaleComboBox.Enabled = false;
            yScaleComboBox.Enabled = false;

            // Calculate Y-axis min and max values
            double yMin = meterValues.Min();
            double yMax = meterValues.Max();

            // Update Y-axis range text boxes
            yAxisStartTextBox.Text = yMin.ToString("G6");
            yAxisEndTextBox.Text = yMax.ToString("G6");

            Chart chart = new Chart
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White
            };
            ChartArea chartArea = new ChartArea
            {
                BackColor = Color.FromArgb(45, 45, 45),
                AxisX =
                {
                    LineColor = Color.White,
                    MajorGrid = { LineColor = Color.Gray },
                    MinorGrid = { Enabled = true, LineColor = Color.Gray, LineDashStyle = ChartDashStyle.Dot }, // Minor tick marks
                    LabelStyle = { ForeColor = Color.White }
                },
                AxisY =
                {
                    LineColor = Color.White,
                    MajorGrid = { LineColor = Color.Gray },
                    MinorGrid = { Enabled = true, LineColor = Color.Gray, LineDashStyle = ChartDashStyle.Dot }, // Minor tick marks
                    LabelStyle = { ForeColor = Color.White },
                    Title = "Meter Value",
                    TitleForeColor = Color.White
                }
            };

            // Auto-range Y-axis and disable manual input
            chartArea.AxisY.IsLogarithmic = resultData.YScale == "Logarithmic";
            if (resultData.AutoRangeY)
            {
                chartArea.AxisY.Minimum = double.NaN; // Enable auto-range
                chartArea.AxisY.Maximum = double.NaN;
            }
            else
            {
                chartArea.AxisY.Minimum = resultData.YAxisStart;
                chartArea.AxisY.Maximum = resultData.YAxisEnd;
            }

            chart.ChartAreas.Add(chartArea);

            // Generate unique colors for each bar
            var colors = new[] { Color.CornflowerBlue, Color.Orange, Color.Green, Color.Red, Color.Purple };

            Series series = new Series
            {
                ChartType = SeriesChartType.Column,
                Color = Color.CornflowerBlue
            };

            for (int i = 0; i < meterValues.Length; i++)
            {
                series.Points.AddXY(i + 1, meterValues[i]);
            }

            chart.Series.Add(series);

            Legend legend = new Legend
            {
                Docking = Docking.Top,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(45, 45, 45)
            };
            chart.Legends.Add(legend);

            // Add title
            Title title = new Title($"{resultData.SignalPathName} - {resultData.MeasurementName} - {resultData.Name}")
            {
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = Color.White
            };
            chart.Titles.Add(title);

            graphPanel.Controls.Add(chart);
            LogManager.AppendLog("Meter graph displayed successfully.");
        }

        // Enable the controls for other graph types
        private void EnableAxisControls(bool enable)
        {
            autoRangeXCheckBox.Enabled = enable;
            xAxisStartTextBox.Enabled = enable;
            xAxisEndTextBox.Enabled = enable;
        }

        private void DisplayXYGraph(ResultData result, bool showLimits)
        {
            if (result == null)
            {
                MessageBox.Show("Result data is null.", "Error", MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                return;
            }

            var sequenceResult = GetSequenceResult(result);
            if (sequenceResult == null)
            {
                MessageBox.Show("Sequence result is null.", "Error", MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                return;
            }

            if (graphPanel == null)
            {
                MessageBox.Show("Graph panel is not initialized.", "Error", MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                return;
            }

            // Enable all graph preferences for XY graph
            autoRangeXCheckBox.Enabled = true;
            xAxisStartTextBox.Enabled = true;
            xAxisEndTextBox.Enabled = true;
            xScaleComboBox.Enabled = true;
            yScaleComboBox.Enabled = true;

            Chart chart = new Chart
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White
            };
            ChartArea chartArea = new ChartArea
            {
                BackColor = Color.FromArgb(45, 45, 45),
                AxisX =
        {
            LineColor = Color.White,
            MajorGrid = { LineColor = Color.Gray },
            LabelStyle = { ForeColor = Color.White },
            Title = "X-Axis",
            TitleForeColor = Color.White
        },
                AxisY =
        {
            LineColor = Color.White,
            MajorGrid = { LineColor = Color.Gray },
            LabelStyle = { ForeColor = Color.White },
            Title = "Y-Axis",
            TitleForeColor = Color.White
        }
            };

            chart.ChartAreas.Add(chartArea);

            double xMin = double.MaxValue, xMax = double.MinValue;
            double yMin = double.MaxValue, yMax = double.MinValue;

            for (int ch = 0; ch < sequenceResult.ChannelCount; ch++)
            {
                double[] xValues = sequenceResult.GetXValues((InputChannelIndex)ch, VerticalAxis.Left, SourceDataType.Measured, 0);
                double[] yValues = sequenceResult.GetYValues((InputChannelIndex)ch, VerticalAxis.Left, SourceDataType.Measured, 0);

                if (xValues == null || yValues == null)
                {
                    MessageBox.Show($"X or Y values are null for channel {ch + 1}.", "Error", MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                    continue;
                }

                Series dataSeries = new Series($"Channel {ch + 1}")
                {
                    ChartType = SeriesChartType.Line,
                    BorderWidth = 2
                };

                for (int i = 0; i < xValues.Length; i++)
                {
                    dataSeries.Points.AddXY(xValues[i], yValues[i]);
                    xMin = Math.Min(xMin, xValues[i]);
                    xMax = Math.Max(xMax, xValues[i]);
                    yMin = Math.Min(yMin, yValues[i]);
                    yMax = Math.Max(yMax, yValues[i]);
                }

                chart.Series.Add(dataSeries);
            }

            // Add legend
            Legend legend = new Legend
            {
                Docking = Docking.Top,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(45, 45, 45)
            };
            chart.Legends.Add(legend);

            // Update text boxes with calculated ranges
            xAxisStartTextBox.Text = xMin.ToString("G6");
            xAxisEndTextBox.Text = xMax.ToString("G6");
            yAxisStartTextBox.Text = yMin.ToString("G6");
            yAxisEndTextBox.Text = yMax.ToString("G6");

            // Add title
            Title title = new Title($"{result.SignalPathName} - {result.MeasurementName} - {result.Name}")
            {
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = Color.White
            };
            chart.Titles.Add(title);

            graphPanel.Controls.Add(chart);
            LogManager.AppendLog("XY graph displayed successfully.");
        }

        private void AutoRangeXCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (resultsTreeView.SelectedNode?.Tag is ResultData selectedResult)
            {
                // Update the AutoRangeX property for the selected result
                selectedResult.AutoRangeX = autoRangeXCheckBox.Checked;

                // Enable or disable X-axis input fields based on Auto-Range status
                xAxisStartTextBox.Enabled = !autoRangeXCheckBox.Checked;
                xAxisEndTextBox.Enabled = !autoRangeXCheckBox.Checked;

                LogManager.AppendLog($"Auto-Range X changed to {autoRangeXCheckBox.Checked} for {selectedResult.Name}. No graph update yet.");
            }
            else
            {
                LogManager.AppendLog("Auto-Range X change event triggered, but no result is selected.");
            }
        }
        private void AutoRangeYCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (resultsTreeView.SelectedNode?.Tag is ResultData selectedResult)
            {
                // Update the AutoRangeY property for the selected result
                selectedResult.AutoRangeY = autoRangeYCheckBox.Checked;

                // Enable or disable Y-axis input fields based on Auto-Range status
                yAxisStartTextBox.Enabled = !autoRangeYCheckBox.Checked;
                yAxisEndTextBox.Enabled = !autoRangeYCheckBox.Checked;

                LogManager.AppendLog($"Auto-Range Y changed to {autoRangeYCheckBox.Checked} for {selectedResult.Name}. No graph update yet.");
            }
            else
            {
                LogManager.AppendLog("Auto-Range Y change event triggered, but no result is selected.");
            }
        }

        private void UpdateGraph()
        {
            if (resultsTreeView.SelectedNode?.Tag is ResultData selectedResult)
            {
                // Validate user inputs for X-axis
                xStartValid = double.TryParse(xAxisStartTextBox.Text, out xStart) && xStart > 0;
                xEndValid = double.TryParse(xAxisEndTextBox.Text, out xEnd) && xEnd > xStart;

                // Validate user inputs for Y-axis
                yStartValid = double.TryParse(yAxisStartTextBox.Text, out yStart) && yStart > 0;
                yEndValid = double.TryParse(yAxisEndTextBox.Text, out yEnd) && yEnd > yStart;

                // Update graph preferences only if valid
                if (!selectedResult.AutoRangeX && (!xStartValid || !xEndValid))
                {
                    MessageBox.Show("Invalid X-axis range. Ensure values are positive and the end value is greater than the start value.", "Error", MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                    return;
                }

                if (!selectedResult.AutoRangeY && (!yStartValid || !yEndValid))
                {
                    MessageBox.Show("Invalid Y-axis range. Ensure values are positive and the end value is greater than the start value.", "Error", MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                    return;
                }

                // Save user preferences
                selectedResult.XScale = xScaleComboBox.SelectedItem?.ToString() ?? "Linear";
                selectedResult.YScale = yScaleComboBox.SelectedItem?.ToString() ?? "Linear";
                selectedResult.AutoRangeX = autoRangeXCheckBox.Checked;
                selectedResult.AutoRangeY = autoRangeYCheckBox.Checked;
                selectedResult.XAxisStart = xStartValid ? xStart : double.NaN;
                selectedResult.XAxisEnd = xEndValid ? xEnd : double.NaN;
                selectedResult.YAxisStart = yStartValid ? yStart : double.NaN;
                selectedResult.YAxisEnd = yEndValid ? yEnd : double.NaN;

                // Display the updated graph
                DisplayGraph(selectedResult);

                LogManager.AppendLog($"Graph updated for {selectedResult.Name} with new preferences.");
            }
            else
            {
                LogManager.AppendLog("UpdateGraph clicked, but no result is selected.");
            }
        }


        private void LogGraphSettings(ResultData result)
        {
            if (result == null)
            {
                LogManager.AppendLog("No result data available to log graph settings.");
                return;
            }

            LogManager.AppendLog($"Graph Settings for Result: {result.Name}");
            LogManager.AppendLog($"  X-Scale: {result.XScale}");
            LogManager.AppendLog($"  Y-Scale: {result.YScale}");
            LogManager.AppendLog($"  Auto-Range X: {result.AutoRangeX}");
            LogManager.AppendLog($"  Auto-Range Y: {result.AutoRangeY}");
            LogManager.AppendLog($"  X-Axis Start: {result.XAxisStart}");
            LogManager.AppendLog($"  X-Axis End: {result.XAxisEnd}");
            LogManager.AppendLog($"  Y-Axis Start: {result.YAxisStart}");
            LogManager.AppendLog($"  Y-Axis End: {result.YAxisEnd}");
            LogManager.AppendLog($"  Show Limits: {showLimitsCheckBox.Checked}");
        }

        private void ValidateScaleOptions(ResultData result)
        {
            bool hasNegativeX = result.XValues?.Any(x => x <= 0) ?? false;
            bool hasNegativeY = result.YValuesPerChannel.Values.SelectMany(y => y).Any(y => y <= 0);

            xScaleComboBox.Items.Clear();
            xScaleComboBox.Items.Add("Linear");
            if (!hasNegativeX)
            {
                xScaleComboBox.Items.Add("Logarithmic");
            }

            yScaleComboBox.Items.Clear();
            yScaleComboBox.Items.Add("Linear");
            if (!hasNegativeY)
            {
                yScaleComboBox.Items.Add("Logarithmic");
            }
        }

        private void ApplyScaleType(ResultData result)
        {
            if (result.XScale == "Logarithmic" && (xStart <= 0 || xEnd <= 0))
            {
                xScaleComboBox.SelectedIndex = 0;
                LogManager.AppendLog("Logarithmic scale for x-axis disabled due to non-positive values.");
            }

            if (result.YScale == "Logarithmic" && (yStart <= 0 || yEnd <= 0))
            {
                yScaleComboBox.SelectedIndex = 0;
                LogManager.AppendLog("Logarithmic scale for y-axis disabled due to non-positive values.");
            }

            result.XScale = xScaleComboBox.SelectedItem?.ToString() ?? "Linear";
            result.YScale = yScaleComboBox.SelectedItem?.ToString() ?? "Linear";
        }

        private void DisplayResultDetails(ResultData result)
        {
            var details = new StringBuilder();

            try
            {
                // Check if Signal Path Index is within the valid range
                if (result.SignalPathIndex >= APx.Sequence.Count)
                {
                    details.AppendLine($"Error: Signal Path Index ({result.SignalPathIndex}) out of range. Max index: {APx.Sequence.Count - 1}");
                    return;
                }

                var signalPath = this.APx.Sequence[result.SignalPathIndex] as ISignalPath;
                if (signalPath == null || !signalPath.Checked)
                {
                    details.AppendLine($"Error: Invalid or unchecked Signal Path at index {result.SignalPathIndex}.");
                    return;
                }

                // Check if Measurement Index is within the valid range
                if (result.MeasurementIndex >= signalPath.Count)
                {
                    details.AppendLine($"Error: Measurement Index ({result.MeasurementIndex}) out of range in Signal Path ({result.SignalPathIndex}). Max index: {signalPath.Count - 1}");
                    return;
                }

                var measurement = signalPath[result.MeasurementIndex] as ISequenceMeasurement;
                if (measurement == null || !measurement.Checked || !measurement.IsValid)
                {
                    details.AppendLine($"Error: Invalid or unchecked Measurement at index {result.MeasurementIndex} in Signal Path {result.SignalPathIndex}.");
                    return;
                }

                // Check if Result Index is within the valid range
                if (result.Index >= measurement.SequenceResults.Count)
                {
                    details.AppendLine($"Error: Result Index ({result.Index}) out of range in Measurement ({result.MeasurementIndex}) of Signal Path ({result.SignalPathIndex}). Max index: {measurement.SequenceResults.Count - 1}");
                    return;
                }

                var sequenceResult = measurement.SequenceResults[result.Index] as ISequenceResult;

                if (result.UpperLimitEnabled)
                {
                    bool upperLimitCheck = sequenceResult.LimitCheckEnabled(LimitType.Upper, VerticalAxis.Left);
                    details.AppendLine($"Upper Limit Enabled: {upperLimitCheck}");
                }

                if (result.LowerLimitEnabled)
                {
                    bool lowerLimitCheck = sequenceResult.LimitCheckEnabled(LimitType.Lower, VerticalAxis.Left);
                    details.AppendLine($"Lower Limit Enabled: {lowerLimitCheck}");
                }
                if (sequenceResult == null)
                {
                    details.AppendLine($"Error: Invalid Result at index {result.Index} in Measurement {result.MeasurementIndex} of Signal Path {result.SignalPathIndex}.");
                    return;
                }

                // Display details
                details.AppendLine($"Result Name: {sequenceResult.Name}");
                details.AppendLine($"Measurement Type: {sequenceResult.ResultType}");
                details.AppendLine($"Number of Channels: {sequenceResult.ChannelCount}");
                details.AppendLine($"Pass/Fail Status: {(sequenceResult.PassedResult ? "Passed" : "Failed")}");
                details.AppendLine($"Signal Path Index: {result.SignalPathIndex}");
                details.AppendLine($"Measurement Index: {result.MeasurementIndex}");
                details.AppendLine($"Result Index: {result.Index}");

                // Displaying the limit check statuses
                details.AppendLine($"Upper Limit Enabled: {result.UpperLimitEnabled}");
                details.AppendLine($"Lower Limit Enabled: {result.LowerLimitEnabled}");
                string resultValuesType = DetermineResultValuesType(sequenceResult);
                details.AppendLine($"Result Values Type: {resultValuesType}");

                // Display units based on Result Values Type
                details.AppendLine("Units:");
                switch (resultValuesType)
                {
                    case "Meter Values":
                        details.AppendLine($"  Meter Units: {sequenceResult.MeterUnit}");
                        break;
                    case "XY Values":
                        details.AppendLine($"  X-Units: {sequenceResult.XUnit}");
                        details.AppendLine($"  Y-Units: {sequenceResult.YUnit}");
                        break;
                    case "XYY Values":
                        // Assuming similar methods exist for LeftUnit and RightUnit
                        details.AppendLine($"  X-Units: {sequenceResult.XUnit}");
                        details.AppendLine($"  Y-Units: {sequenceResult.YUnit}"); // Or use LeftUnit and RightUnit as applicable
                        break;
                    default:
                        details.AppendLine("  Not applicable for this result type.");
                        break;
                }

                details.AppendLine("Channel Status and Limits:");
                for (int ch = 0; ch < sequenceResult.ChannelCount; ch++)
                {
                    string channelName = sequenceResult.ChannelNames[ch];
                    bool channelPassed = sequenceResult.PassedLimitCheckOnChannel((InputChannelIndex)ch, LimitType.Upper, VerticalAxis.Left) &&
                                         sequenceResult.PassedLimitCheckOnChannel((InputChannelIndex)ch, LimitType.Lower, VerticalAxis.Left);
                    details.AppendLine($"  Channel {channelName}: {(channelPassed ? "Passed" : "Failed")}");
                }
            }
            catch (Exception ex)
            {
                details.AppendLine($"An error occurred: {ex.Message}");
            }

            detailsTextBox.Text = details.ToString();
        }
        private static string DetermineResultValuesType(ISequenceResult sequenceResult)
        {
            if (sequenceResult.HasMeterValues)
                return "Meter Values";
            if (sequenceResult.HasRawTextResults)
                return "Raw Text Results";
            if (sequenceResult.HasThieleSmallValues)
                return "Thiele Small Values";
            if (sequenceResult.HasXYValues)
                return "XY Values";
            if (sequenceResult.HasXYYValues)
                return "XYY Values";
            return "Unknown";
        }
        private ResultData FindResultByIdentifier(string identifier)
        {
            foreach (var signalPath in checkedData)
            {
                foreach (var measurement in signalPath.Measurements)
                {
                    foreach (var result in measurement.Results)
                    {
                        string resultIdentifier = $"{signalPath.Name} (SP Index: {signalPath.Index}) | " +
                                                  $"{measurement.Name} (M Index: {measurement.Index}) | " +
                                                  $"{result.Name} (R Index: {result.Index})";
                        if (identifier == resultIdentifier)
                        {
                            return result;
                        }
                    }
                }
            }
            return null;
        }
        public List<SignalPathData> GetCheckedData()
        {
            var checkedSignalPaths = new List<SignalPathData>();

            if (APx == null)
            {
                Console.WriteLine("APx is None. Launch AP Software first.");
                return checkedSignalPaths;
            }

            try
            {
                for (int spIdx = 0; spIdx < APx.Sequence.Count; spIdx++)
                {
                    var signalPath = APx.Sequence[spIdx] as ISignalPath;
                    if (signalPath == null || !signalPath.Checked)
                        continue;

                    var currentSP = new SignalPathData
                    {
                        Name = signalPath.Name,
                        Index = spIdx
                    };

                    for (int mIdx = 0; mIdx < signalPath.Count; mIdx++)
                    {
                        var measurement = signalPath[mIdx] as ISequenceMeasurement;
                        if (measurement == null || !measurement.Checked || !measurement.IsValid)
                            continue;

                        var currentMeasurement = new MeasurementData
                        {
                            Name = measurement.Name,
                            Index = mIdx
                        };

                        for (int resultIdx = 0; resultIdx < measurement.SequenceResults.Count; resultIdx++)
                        {
                            var sequenceResult = measurement.SequenceResults[resultIdx] as ISequenceResult;

                            string measurementType = sequenceResult.ResultType.ToString();
                            int channelCount = sequenceResult.ChannelCount; // Updated API call
                            bool passed = sequenceResult.PassedResult;

                            var currentResult = new ResultData
                            {
                                Name = sequenceResult.Name,
                                Index = resultIdx,
                                MeasurementType = measurementType,
                                XUnit = sequenceResult.HasXYValues ? sequenceResult.XUnit : "",
                                YUnit = sequenceResult.HasXYValues ? sequenceResult.YUnit : "",
                                MeterUnit = sequenceResult.HasMeterValues ? sequenceResult.MeterUnit : "",
                                ChannelCount = channelCount,
                                Passed = passed,
                                ChannelPassFail = new Dictionary<string, bool>(),
                                SignalPathIndex = signalPath.Index, // Set the correct Signal Path Index
                                MeasurementIndex = measurement.Index, // Set the correct Measurement Index
                                SignalPathName = signalPath.Name,  // Set Signal Path Name
                                MeasurementName = measurement.Name // Set Measurement Name
                            };


                            currentResult.ResultValueType = DetermineResultValuesType(sequenceResult);
                            currentResult.UpperLimitEnabled = sequenceResult.UpperLimitCheckEnabled;
                            currentResult.LowerLimitEnabled = sequenceResult.LowerLimitCheckEnabled;

                            if (sequenceResult.HasXYValues)
                            {
                                // Initialize the arrays to store XY limit values
                                List<double> xUpperLimits = new List<double>();
                                List<double> yUpperLimits = new List<double>();
                                List<double> xLowerLimits = new List<double>();
                                List<double> yLowerLimits = new List<double>();

                                // Fetch XValues and YValues
                                var xValues = new List<double>();
                                var yValues = new List<double>();

                                // Retrieve and store limit values for XY type results
                                for (int ch = 0; ch < sequenceResult.ChannelCount; ch++)
                                {
                                    InputChannelIndex channelIndex = (InputChannelIndex)ch;
                                    xLowerLimits.AddRange(sequenceResult.GetXYLowerLimit(VerticalAxis.Left).GetXValues(channelIndex));
                                    yLowerLimits.AddRange(sequenceResult.GetXYLowerLimit(VerticalAxis.Left).GetYValues(channelIndex));
                                    xUpperLimits.AddRange(sequenceResult.GetXYUpperLimit(VerticalAxis.Left).GetXValues(channelIndex));
                                    yUpperLimits.AddRange(sequenceResult.GetXYUpperLimit(VerticalAxis.Left).GetYValues(channelIndex));
                                }

                                // Convert lists to arrays and store in currentResult
                                currentResult.XValueLowerLimitValues = xLowerLimits.ToArray();
                                currentResult.YValueLowerLimitValues = yLowerLimits.ToArray();
                                currentResult.XValueUpperLimitValues = xUpperLimits.ToArray();
                                currentResult.YValueUpperLimitValues = yUpperLimits.ToArray();
                                // Fetch and assign XValues and YValues
                                currentResult.XValues = sequenceResult.GetXValues(InputChannelIndex.Ch1, VerticalAxis.Left, SourceDataType.Measured, 0);
                                Console.WriteLine($"Debug: XValues assigned for {currentResult.Name}");

                                // Fetch and assign YValues for each channel
                                for (int ch = 0; ch < sequenceResult.ChannelCount; ch++)
                                {
                                    var channelYValues = sequenceResult.GetYValues((InputChannelIndex)ch, VerticalAxis.Left, SourceDataType.Measured, 0);
                                    currentResult.YValuesPerChannel.Add($"Ch{ch + 1}", channelYValues);
                                    Console.WriteLine($"Debug: YValues assigned for {currentResult.Name}, Channel {ch + 1}");
                                }
                            }
                            else if (sequenceResult.HasMeterValues)
                            {
                                currentResult.MeterValues = sequenceResult.GetMeterValues();
                                currentResult.MeterUpperLimitValues = sequenceResult.GetMeterUpperLimitValues();
                                currentResult.MeterLowerLimitValues = sequenceResult.GetMeterLowerLimitValues();
                            }

                            // Add the result to the list
                            currentMeasurement.Results.Add(currentResult);

                            // Fetching channel names and pass/fail status based on enabled limits
                            for (int ch = 0; ch < channelCount; ch++)
                            {
                                string channelName = sequenceResult.ChannelNames[ch];
                                currentResult.ChannelNameToIndexMap[channelName] = ch; // Map channel name to index
                                bool upperLimitEnabled = sequenceResult.LimitCheckEnabled(LimitType.Upper, VerticalAxis.Left); // Assuming Left axis
                                bool lowerLimitEnabled = sequenceResult.LimitCheckEnabled(LimitType.Lower, VerticalAxis.Left); // Assuming Left axis

                                bool channelPassed = true; // Default to true

                                if (upperLimitEnabled)
                                {
                                    channelPassed &= sequenceResult.PassedLimitCheckOnChannel((InputChannelIndex)ch, LimitType.Upper, VerticalAxis.Left);
                                }

                                if (lowerLimitEnabled)
                                {
                                    channelPassed &= sequenceResult.PassedLimitCheckOnChannel((InputChannelIndex)ch, LimitType.Lower, VerticalAxis.Left);
                                }

                                currentResult.ChannelPassFail.Add(channelName, channelPassed);
                            }
                        }

                        currentSP.Measurements.Add(currentMeasurement);
                    }

                    checkedSignalPaths.Add(currentSP);
                }
            }
            catch (IndexOutOfRangeException ex)
            {
                Console.WriteLine($"Index out of range error: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An unexpected error occurred: {ex.Message}");
            }

            return checkedSignalPaths;
        }
        private void DownloadJsonButton_Click(object sender, EventArgs e)
        {
            try
            {
                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    Filter = "JSON files (*.json)|*.json|Excel files (*.xlsx)|*.xlsx",
                    Title = "Save Data"
                };

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string filePath = saveFileDialog.FileName;
                    string fileExtension = Path.GetExtension(filePath).ToLower();

                    if (fileExtension == ".json")
                    {
                        DownloadDataAsJson(filePath);
                    }
                    else if (fileExtension == ".xlsx")
                    {
                        DownloadDataAsExcel(filePath);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}");
            }
        }
        private void DownloadDataAsJson(string filePath)
        {
            // Parse properties text into a dictionary
            var propertiesDictionary = ParsePropertiesToDictionary(propertiesTextBox.Text);

            // Creating an object that includes the properties dictionary and checked data
            var dataToDownload = new
            {
                GlobalProperties = propertiesDictionary,
                CheckedData = checkedData.Select(sp => new
                {
                    sp.Name,
                    sp.Index,
                    Measurements = sp.Measurements.Select(m => new
                    {
                        m.Name,
                        m.Index,
                        Results = m.Results.Select(r => CreateResultObject(r))
                    })
                })
            };

            // Custom serializer settings to ignore null and empty fields
            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                DefaultValueHandling = DefaultValueHandling.Ignore
            };

            // Serializing the combined data to JSON with custom settings
            string serializedData = JsonConvert.SerializeObject(dataToDownload, Formatting.Indented, settings);

            // Save the serialized data to a file
            File.WriteAllText(filePath, serializedData);
            MessageBox.Show("Data has been downloaded successfully.");
        }
        private void DownloadDataAsExcel(string filePath)
        {
            using (var workbook = new XLWorkbook())
            {
                // Create a worksheet for global properties
                var propertiesSheet = workbook.Worksheets.Add("GlobalProperties");
                int rowIndex = 1;
                foreach (var kvp in ParsePropertiesToDictionary(propertiesTextBox.Text))
                {
                    propertiesSheet.Cell(rowIndex, 1).Value = kvp.Key;
                    propertiesSheet.Cell(rowIndex, 2).Value = kvp.Value;
                    rowIndex++;
                }

                var sheetNames = new HashSet<string>(); // To ensure unique sheet names

                foreach (var signalPath in checkedData)
                {
                    foreach (var measurement in signalPath.Measurements)
                    {
                        foreach (var result in measurement.Results)
                        {
                            var sequenceResult = GetSequenceResult(result);
                            result.ResultValueType = DetermineResultValuesType(sequenceResult);

                            // Abbreviate the sheet name and ensure uniqueness
                            string abbreviatedSheetName = AbbreviateSheetName(result.Name, sheetNames);
                            sheetNames.Add(abbreviatedSheetName);

                            // Create a worksheet for each result
                            var resultSheet = workbook.Worksheets.Add(abbreviatedSheetName);

                            // Add full name at the top of the sheet
                            resultSheet.Cell(1, 1).Value = "Full Name:";
                            resultSheet.Cell(1, 2).Value = result.Name;

                            // Ensure XValues and YValuesPerChannel are not null
                            if (result.XValues == null)
                            {
                                result.XValues = new double[0];
                            }
                            if (result.YValuesPerChannel == null)
                            {
                                result.YValuesPerChannel = new Dictionary<string, double[]>();
                            }

                            // Add columns for X and Y values
                            resultSheet.Cell(2, 1).Value = "X Values";
                            int colIndex = 2;
                            foreach (var channel in result.YValuesPerChannel.Keys)
                            {
                                resultSheet.Cell(2, colIndex).Value = $"Y Values ({channel})";
                                colIndex++;
                            }

                            // Add data to worksheet
                            for (int i = 0; i < result.XValues.Length; i++)
                            {
                                resultSheet.Cell(i + 3, 1).Value = result.XValues[i];
                                colIndex = 2;
                                foreach (var yValues in result.YValuesPerChannel.Values)
                                {
                                    if (i < yValues.Length)
                                    {
                                        resultSheet.Cell(i + 3, colIndex).Value = yValues[i];
                                    }
                                    colIndex++;
                                }
                            }
                        }
                    }
                }

                workbook.SaveAs(filePath);
                MessageBox.Show("Data has been downloaded successfully.");
            }
        }
        private void RunScriptButton_Click(object sender, EventArgs e)
        {
            try
            {
                APx.Sequence.Run();
                MessageBox.Show("Sequence run successfully.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while running the sequence: {ex.Message}");
            }
        }
        public string GetCurrentFormData()
        {
            var propertiesDictionary = ParsePropertiesToDictionary(propertiesTextBox.Text);
            var dataToDownload = new
            {
                GlobalProperties = propertiesDictionary,
                CheckedData = checkedData.Select(sp => new
                {
                    sp.Name,
                    sp.Index,
                    Measurements = sp.Measurements.Select(m => new
                    {
                        m.Name,
                        m.Index,
                        Results = m.Results.Select(r => CreateResultObject(r))
                    })
                })
            };

            string jsonData = JsonConvert.SerializeObject(dataToDownload, Formatting.Indented);
            string abbreviatedData = jsonData.Substring(0, Math.Min(jsonData.Length, 100)) + "..."; // Abbreviating to 100 chars
            LogManager.AppendLog("GetCurrentFormData: Returning abbreviated JSON data: " + abbreviatedData);
            return jsonData;
        }
        private Dictionary<string, string> ParsePropertiesToDictionary(string propertiesText)
        {
            var dictionary = new Dictionary<string, string>();
            var lines = propertiesText.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var parts = line.Split(new[] { ": " }, 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    dictionary[parts[0].Trim()] = parts[1].Trim();
                }
            }

            return dictionary;
        }
        private object CreateResultObject(ResultData result)
        {
            // Only include relevant fields, excluding limits
            return new
            {
                result.Name,
                result.Index,
                result.MeasurementType,
                result.ChannelCount,
                result.ResultValueType,
                result.XUnit,
                result.YUnit,
                result.MeterUnit,
                result.Passed,
                result.ChannelPassFail,
                result.SignalPathIndex,
                result.MeasurementIndex,
                XValues = result.ResultValueType == "XY Values" ? result.XValues : null,
                YValuesPerChannel = result.ResultValueType == "XY Values" ? result.YValuesPerChannel : null,
                MeterValues = result.ResultValueType == "Meter Values" ? result.MeterValues : null
            };
        }
        private void SessionsButton_Click(object sender, EventArgs e)
        {
            string sessionData = GetCurrentFormData();
            var sessionForm = new FormSessionManager(sessionData, sessionList, SessionMode.View, this, LogManager.AppendLog, accessToken, refreshToken);
            sessionForm.ShowDialog();
        }
        private void TestResultsGridMenuItem_Click(object sender, EventArgs e)
        {
            var testResultsGridForm = new FormTestResultsGrid(this); // Pass the current form if required
            testResultsGridForm.ShowDialog();
        }
        private void LogWindowMenuItem_Click(object sender, EventArgs e)
        {
            LogManager.ShowLogWindow();
            LogManager.AppendLog("Log window opened.");
        }

        public class SignalPathData
        {
            public string Name { get; set; } = string.Empty;
            public List<MeasurementData> Measurements { get; set; } = new List<MeasurementData>();
            public int Index { get; set; }
        }
        public class MeasurementData
        {
            public string Name { get; set; } = string.Empty;
            public List<ResultData> Results { get; set; } = new List<ResultData>();
            public int Index { get; set; }
        }
        public class ResultData
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

            // Graph settings properties
            public bool AutoRangeX { get; set; } = true;
            public bool AutoRangeY { get; set; } = true;
            public double XAxisStart { get; set; } = double.NaN;
            public double XAxisEnd { get; set; } = double.NaN;
            public double YAxisStart { get; set; } = double.NaN;
            public double YAxisEnd { get; set; } = double.NaN;
            public string XScale { get; set; } = "Linear";
            public string YScale { get; set; } = "Linear";
            public bool ShowMinorTicksX { get; set; } = true;
            public bool ShowMinorTicksY { get; set; } = true;

            // Limit properties
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
        public static class LogManager
        {
            private static LogWindow logWindow;

            public static void Initialize()
            {
                if (logWindow == null)
                {
                    logWindow = new LogWindow();
                }
            }

            public static void ShowLogWindow()
            {
                if (logWindow == null)
                {
                    Initialize();
                }
                logWindow.Show();
            }

            public static void AppendLog(string message)
            {
                if (logWindow == null)
                {
                    Initialize();
                }
                logWindow.AppendLog(message);
            }
        }
        public class LogWindow : Form
        {
            private TextBox logTextBox;

            public LogWindow()
            {
                this.Text = "Logs";
                this.Size = new Size(800, 600);
                this.BackColor = Color.FromArgb(45, 45, 45); // Dark mode background
                this.FormBorderStyle = FormBorderStyle.Sizable;

                InitializeComponents();
            }

            private void InitializeComponents()
            {
                logTextBox = new TextBox
                {
                    Multiline = true,
                    ReadOnly = true,
                    ScrollBars = ScrollBars.Vertical,
                    Font = new Font("Consolas", 10),
                    Dock = DockStyle.Fill,
                    BackColor = Color.FromArgb(30, 30, 30),
                    ForeColor = Color.White
                };

                Controls.Add(logTextBox);
            }

            public void AppendLog(string message)
            {
                if (logTextBox.InvokeRequired)
                {
                    logTextBox.Invoke(new Action(() => AppendLog(message)));
                }
                else
                {
                    logTextBox.AppendText($"{DateTime.Now}: {message}{Environment.NewLine}");
                    logTextBox.ScrollToCaret(); // Ensures the log window scrolls to the latest entry
                }
            }

        }

    }

    public class ProjectSession
    {
        public string Title { get; set; }
        public string Data { get; set; }
        public List<SignalPathData> SignalPaths { get; set; }
        public Dictionary<string, string> GlobalProperties { get; set; }
    }
}