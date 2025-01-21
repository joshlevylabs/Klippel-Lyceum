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

        public List<ProjectSession> sessionList = new List<ProjectSession>();

        // This should be the only declaration of SessionDataHandler
        public delegate void SessionDataHandler(ProjectSession newSession);

        public FormAudioPrecision8(string accessToken, string refreshToken)
        {
            this.accessToken = accessToken;
            this.refreshToken = refreshToken;

            // Hide the Lyceum logo
            //LogoPictureBox.Visible = false;

            InitializeComponents();
            InitializeWindow();
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

            menuStrip.Items.Add(openMenu);
        }
        private void InitializeComponents()
        {
            // Set Form Properties
            this.Text = "Audio Precision Control";
            this.Font = new Font("Segoe UI", 10);
            this.BackColor = Color.FromArgb(45, 45, 45); // Dark Mode background
            this.Size = new Size(1800, 1000);
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

            // GroupBox for Properties
            GroupBox propertiesGroup = new GroupBox
            {
                Text = "Properties",
                ForeColor = Color.White,
                Location = new Point(20, 60),
                Size = new Size(newWidth, 200), // Adjusted width to 2/3
                BackColor = Color.FromArgb(45, 45, 45)
            };
            Controls.Add(propertiesGroup);

            // Properties TextBox
            propertiesTextBox = new TextBox
            {
                Location = new Point(10, 30),
                Size = new Size(newWidth - 20, 140), // Adjusted width
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                ReadOnly = true,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            propertiesGroup.Controls.Add(propertiesTextBox);

            // GroupBox for Results
            GroupBox resultsGroup = new GroupBox
            {
                Text = "Results",
                ForeColor = Color.White,
                Location = new Point(20, 280),
                Size = new Size(newWidth, 200), // Adjusted width to 2/3
                BackColor = Color.FromArgb(45, 45, 45)
            };
            Controls.Add(resultsGroup);

            // Results TreeView
            resultsTreeView = new TreeView
            {
                Location = new Point(10, 30),
                Size = new Size(newWidth - 20, 150), // Adjusted width
                Font = new Font("Segoe UI", 10),
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            resultsTreeView.AfterSelect += ResultsTreeView_AfterSelect;
            resultsGroup.Controls.Add(resultsTreeView);

            // GroupBox for Details
            GroupBox detailsGroup = new GroupBox
            {
                Text = "Details",
                ForeColor = Color.White,
                Location = new Point(20, 500),
                Size = new Size(newWidth, 250), // Adjusted width to 2/3
                BackColor = Color.FromArgb(45, 45, 45)
            };
            Controls.Add(detailsGroup);

            // Details TextBox
            detailsTextBox = new TextBox
            {
                Location = new Point(10, 30),
                Size = new Size(newWidth - 20, 200), // Adjusted width
                Font = new Font("Segoe UI", 10),
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                ReadOnly = true,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            detailsGroup.Controls.Add(detailsTextBox);

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

            // Adjusted Graph Preferences GroupBox height and position
            GroupBox graphPreferencesGroup = new GroupBox
            {
                Location = new Point(newWidth + 40, 670), // Position it below the adjusted graph group
                Size = new Size(remainingWidth, 160), // Increased height to fit all elements
                Text = "Graph Preferences",
                ForeColor = Color.White,
                BackColor = Color.FromArgb(45, 45, 45)
            };
            Controls.Add(graphPreferencesGroup);

            // X-Axis Scale ComboBox
            xScaleComboBox = new ComboBox
            {
                Location = new Point(10, 30),
                Width = 160,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Items = { "Linear", "Logarithmic" },
                SelectedIndex = 0
            };
            xScaleComboBox.SelectedIndexChanged += (sender, e) => UpdateGraph();
            graphPreferencesGroup.Controls.Add(xScaleComboBox);

            // Y-Axis Scale ComboBox
            yScaleComboBox = new ComboBox
            {
                Location = new Point(180, 30), // Moved to the right of X-Axis Scale
                Width = 160,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Items = { "Linear", "Logarithmic" },
                SelectedIndex = 0
            };
            yScaleComboBox.SelectedIndexChanged += (sender, e) => UpdateGraph();
            graphPreferencesGroup.Controls.Add(yScaleComboBox);

            // Auto-Range X CheckBox
            autoRangeXCheckBox = new CheckBox
            {
                Text = "Auto-Range X",
                Location = new Point(10, 70), // Moved below the X-Axis Scale ComboBox
                Checked = true,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(45, 45, 45)
            };
            autoRangeXCheckBox.CheckedChanged += AutoRangeXCheckBox_CheckedChanged;
            graphPreferencesGroup.Controls.Add(autoRangeXCheckBox);

            // Auto-Range Y CheckBox
            autoRangeYCheckBox = new CheckBox
            {
                Text = "Auto-Range Y",
                Location = new Point(180, 70), // Moved to the right of Auto-Range X
                Checked = true,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(45, 45, 45)
            };
            autoRangeYCheckBox.CheckedChanged += AutoRangeYCheckBox_CheckedChanged;
            graphPreferencesGroup.Controls.Add(autoRangeYCheckBox);

            // Show Limits CheckBox
            showLimitsCheckBox = new CheckBox
            {
                Text = "Show Limits",
                Location = new Point(360, 70), // Moved to the right of Auto-Range Y
                Checked = true,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(45, 45, 45)
            };
            graphPreferencesGroup.Controls.Add(showLimitsCheckBox);

            // X-Axis Start TextBox
            xAxisStartTextBox = new TextBox
            {
                Location = new Point(10, 110), // Moved below Auto-Range X
                Width = 70,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            graphPreferencesGroup.Controls.Add(xAxisStartTextBox);

            // X-Axis End TextBox
            xAxisEndTextBox = new TextBox
            {
                Location = new Point(90, 110), // Moved to the right of X-Axis Start
                Width = 70,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            graphPreferencesGroup.Controls.Add(xAxisEndTextBox);

            // Log TextBox
            logTextBox = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 10),
                Location = new Point(20, 850), // Reposition to ensure visibility
                Size = new Size(newWidth, 120), // Adjust size as needed
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            Controls.Add(logTextBox);
        }
        private void InitializeWindow()
        {
            this.SuspendLayout();
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1800, 1000); // Increased size
            this.MinimumSize = new System.Drawing.Size(1800, 1000); // Adjusted minimum size
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
            checkedData = GetCheckedData();
            FillResultsTreeView(); // Populate the TreeView instead of the ListBox
            DisplayProperties();
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
            if (e.Node.Tag is ResultData selectedResult)
            {
                DisplayResultDetails(selectedResult);
                DisplayGraph(selectedResult);
            }
        }
        private void DisplayGraph(ResultData result)
        {
            if (result == null)
            {
                MessageBox.Show("Result data is null.", "Error", MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                return;
            }

            var sequenceResult = GetSequenceResult(result);
            if (sequenceResult == null)
            {
                MessageBox.Show("Invalid sequence result.", "Error", MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                return;
            }

            if (graphPanel == null)
            {
                MessageBox.Show("Graph panel is not initialized.", "Error", MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                return;
            }

            // Clear previous graph
            graphPanel.Controls.Clear();

            switch (DetermineResultValuesType(sequenceResult))
            {
                case "Meter Values":
                    DisplayMeterGraph(sequenceResult, result); // Pass the ResultData object as well
                    xScaleComboBox.Enabled = false; // Disable X-axis scale change for meter values (bar chart)
                    break;

                case "XY Values":
                    DisplayXYGraph(result, showLimitsCheckBox.Checked); // Updated call with limits
                    xScaleComboBox.Enabled = true; // Enable X-axis scale change for XY values
                    break;

                    // Add other cases as needed
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
                MessageBox.Show("Sequence result or result data is null.", "Error", MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                return;
            }

            double[] meterValues = sequenceResult.GetMeterValues();
            if (meterValues == null)
            {
                MessageBox.Show("Meter values are null.", "Error", MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                return;
            }

            Chart chart = new Chart
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(45, 45, 45), // Set background color to dark mode
                ForeColor = Color.White
            };
            ChartArea chartArea = new ChartArea
            {
                BackColor = Color.FromArgb(45, 45, 45), // Match chart area background color to the dark theme
                BorderColor = Color.Gray, // Optional: set border color to contrast with the dark background
                AxisX =
        {
            LineColor = Color.White, // Set axis line color to white
            MajorGrid = { LineColor = Color.Gray }, // Set grid line color
            LabelStyle = { ForeColor = Color.White } // Set label text color
        },
                AxisY =
        {
            LineColor = Color.White, // Set axis line color to white
            MajorGrid = { LineColor = Color.Gray }, // Set grid line color
            LabelStyle = { ForeColor = Color.White } // Set label text color
        }
            };
            chart.ChartAreas.Add(chartArea);

            Series series = new Series
            {
                ChartType = SeriesChartType.Column
            };

            for (int i = 0; i < meterValues.Length; i++)
            {
                series.Points.AddXY(i + 1, meterValues[i]);
            }

            chart.Series.Add(series);

            // Set the title of the chart
            Title title = new Title($"{resultData.SignalPathName} - {resultData.MeasurementName} - {resultData.Name}")
            {
                Font = new Font("Segoe UI", 14, FontStyle.Bold), // Change font here
                ForeColor = Color.White
            };
            chart.Titles.Add(title);

            // Set font for X and Y axis labels
            chartArea.AxisX.LabelStyle.Font = new Font("Segoe UI", 12); // Change font here
            chartArea.AxisX.LabelStyle.ForeColor = Color.White;
            chartArea.AxisY.LabelStyle.Font = new Font("Segoe UI", 12); // Change font here
            chartArea.AxisY.LabelStyle.ForeColor = Color.White;

            // Set font for X and Y axis titles (if applicable)
            chartArea.AxisX.TitleFont = new Font("Segoe UI", 12, FontStyle.Bold); // Change font here
            chartArea.AxisX.TitleForeColor = Color.White;
            chartArea.AxisY.TitleFont = new Font("Segoe UI", 12, FontStyle.Bold); // Change font here
            chartArea.AxisY.TitleForeColor = Color.White;

            if (resultData.UpperLimitEnabled || resultData.LowerLimitEnabled)
            {
                double[] meterUpperLimitValues = sequenceResult.GetMeterUpperLimitValues();
                double[] meterLowerLimitValues = sequenceResult.GetMeterLowerLimitValues();

                if (meterUpperLimitValues == null || meterLowerLimitValues == null)
                {
                    MessageBox.Show("Meter limit values are null.", "Error", MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                }
                else
                {
                    Series upperLimitMarkers = new Series
                    {
                        ChartType = SeriesChartType.Point,
                        MarkerStyle = MarkerStyle.Cross,
                        MarkerSize = 12,
                        MarkerColor = Color.Red,
                        Color = Color.Transparent // No line color
                    };

                    Series lowerLimitMarkers = new Series
                    {
                        ChartType = SeriesChartType.Point,
                        MarkerStyle = MarkerStyle.Cross,
                        MarkerSize = 12,
                        MarkerColor = Color.Blue,
                        Color = Color.Transparent // No line color
                    };

                    for (int i = 0; i < meterValues.Length; i++)
                    {
                        if (i < meterUpperLimitValues.Length)
                        {
                            upperLimitMarkers.Points.AddXY(i + 1, meterUpperLimitValues[i]);
                        }
                        if (i < meterLowerLimitValues.Length)
                        {
                            lowerLimitMarkers.Points.AddXY(i + 1, meterLowerLimitValues[i]);
                        }
                    }

                    chart.Series.Add(upperLimitMarkers);
                    chart.Series.Add(lowerLimitMarkers);
                }
            }

            graphPanel.Controls.Add(chart); // Adding the chart to graphPanel
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

            Chart chart = new Chart
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(45, 45, 45), // Set background color to dark mode
                ForeColor = Color.White
            };
            ChartArea chartArea = new ChartArea
            {
                BackColor = Color.FromArgb(45, 45, 45), // Match chart area background color to the dark theme
                BorderColor = Color.Gray, // Optional: set border color to contrast with the dark background
                AxisX =
        {
            LineColor = Color.White, // Set axis line color to white
            MajorGrid = { LineColor = Color.Gray }, // Set grid line color
            LabelStyle = { ForeColor = Color.White } // Set label text color
        },
                AxisY =
        {
            LineColor = Color.White, // Set axis line color to white
            MajorGrid = { LineColor = Color.Gray }, // Set grid line color
            LabelStyle = { ForeColor = Color.White } // Set label text color
        }
            };

            chartArea.AxisX.IsLogarithmic = result.XScale == "Logarithmic";
            chartArea.AxisY.IsLogarithmic = result.YScale == "Logarithmic";

            if (result.AutoRangeX)
            {
                chartArea.AxisX.Minimum = double.NaN;
                chartArea.AxisX.Maximum = double.NaN;
            }
            else
            {
                chartArea.AxisX.Minimum = result.XAxisStart;
                chartArea.AxisX.Maximum = result.XAxisEnd;
            }

            if (result.AutoRangeY)
            {
                chartArea.AxisY.Minimum = double.NaN;
                chartArea.AxisY.Maximum = double.NaN;
            }
            else
            {
                chartArea.AxisY.Minimum = result.YAxisStart;
                chartArea.AxisY.Maximum = result.YAxisEnd;
            }

            chart.ChartAreas.Add(chartArea);

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
                    ChartType = SeriesChartType.Line
                };

                for (int i = 0; i < xValues.Length; i++)
                {
                    dataSeries.Points.AddXY(xValues[i], yValues[i]);
                }

                chart.Series.Add(dataSeries);
            }

            if (showLimits)
            {
                for (int ch = 0; ch < sequenceResult.ChannelCount; ch++)
                {
                    var xyLowerLimit = sequenceResult.GetXYLowerLimit(VerticalAxis.Left);
                    var xyUpperLimit = sequenceResult.GetXYUpperLimit(VerticalAxis.Left);

                    if (xyLowerLimit == null || xyUpperLimit == null)
                    {
                        MessageBox.Show("XY limit values are null.", "Error", MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                        continue;
                    }

                    var xLowerLimitValues = xyLowerLimit.GetXValues((InputChannelIndex)ch);
                    var yLowerLimitValues = xyLowerLimit.GetYValues((InputChannelIndex)ch);
                    var xUpperLimitValues = xyUpperLimit.GetXValues((InputChannelIndex)ch);
                    var yUpperLimitValues = xyUpperLimit.GetYValues((InputChannelIndex)ch);

                    if (xLowerLimitValues == null || yLowerLimitValues == null || xUpperLimitValues == null || yUpperLimitValues == null)
                    {
                        MessageBox.Show("Limit values are null for channel " + (ch + 1), "Error", MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                        continue;
                    }

                    Series lowerLimitSeries = new Series($"Lower Limit Channel {ch + 1}")
                    {
                        ChartType = SeriesChartType.Line,
                        BorderDashStyle = ChartDashStyle.Dash,
                        Color = Color.Blue
                    };

                    for (int i = 0; i < xLowerLimitValues.Length; i++)
                    {
                        lowerLimitSeries.Points.AddXY(xLowerLimitValues[i], yLowerLimitValues[i]);
                    }

                    Series upperLimitSeries = new Series($"Upper Limit Channel {ch + 1}")
                    {
                        ChartType = SeriesChartType.Line,
                        BorderDashStyle = ChartDashStyle.Dash,
                        Color = Color.Red
                    };

                    for (int i = 0; i < xUpperLimitValues.Length; i++)
                    {
                        upperLimitSeries.Points.AddXY(xUpperLimitValues[i], yUpperLimitValues[i]);
                    }

                    chart.Series.Add(lowerLimitSeries);
                    chart.Series.Add(upperLimitSeries);
                }
            }

            // Set the title of the chart
            Title title = new Title($"{result.SignalPathName} - {result.MeasurementName} - {result.Name}")
            {
                Font = new Font("Segoe UI", 14, FontStyle.Bold), // Change font here
                ForeColor = Color.White
            };
            chart.Titles.Add(title);

            // Set font for X and Y axis labels
            chartArea.AxisX.LabelStyle.Font = new Font("Segoe UI", 12); // Change font here
            chartArea.AxisX.LabelStyle.ForeColor = Color.White;
            chartArea.AxisY.LabelStyle.Font = new Font("Segoe UI", 12); // Change font here
            chartArea.AxisY.LabelStyle.ForeColor = Color.White;

            // Set font for X and Y axis titles (if applicable)
            chartArea.AxisX.TitleFont = new Font("Segoe UI", 12, FontStyle.Bold); // Change font here
            chartArea.AxisX.TitleForeColor = Color.White;
            chartArea.AxisY.TitleFont = new Font("Segoe UI", 12, FontStyle.Bold); // Change font here
            chartArea.AxisY.TitleForeColor = Color.White;

            graphPanel.Controls.Add(chart); // Adding the chart to graphPanel
        }

        private void AutoRangeXCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            xAxisStartTextBox.Enabled = !autoRangeXCheckBox.Checked;
            xAxisEndTextBox.Enabled = !autoRangeXCheckBox.Checked;
            UpdateGraph();
        }

        private void AutoRangeYCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            yAxisStartTextBox.Enabled = !autoRangeYCheckBox.Checked;
            yAxisEndTextBox.Enabled = !autoRangeYCheckBox.Checked;
            UpdateGraph();
        }

        private void UpdateGraph()
        {
            if (resultsTreeView.SelectedNode?.Tag is ResultData selectedResult)
            {
                selectedResult.AutoRangeX = autoRangeXCheckBox.Checked;
                selectedResult.AutoRangeY = autoRangeYCheckBox.Checked;

                bool xStartValid = double.TryParse(xAxisStartTextBox.Text, out double tempXAxisStart);
                bool xEndValid = double.TryParse(xAxisEndTextBox.Text, out double tempXAxisEnd);
                bool yStartValid = double.TryParse(yAxisStartTextBox.Text, out double tempYAxisStart);
                bool yEndValid = double.TryParse(yAxisEndTextBox.Text, out double tempYAxisEnd);

                // Check if negative values are present and reset scale to Linear if needed
                if ((xStartValid && tempXAxisStart < 0) || (xEndValid && tempXAxisEnd < 0) ||
                    (yStartValid && tempYAxisStart < 0) || (yEndValid && tempYAxisEnd < 0))
                {
                    xScaleComboBox.SelectedIndex = 0; // Linear
                    yScaleComboBox.SelectedIndex = 0; // Linear
                }

                selectedResult.XScale = xScaleComboBox.SelectedItem.ToString();
                selectedResult.YScale = yScaleComboBox.SelectedItem.ToString();

                if (xStartValid && xEndValid && tempXAxisStart < tempXAxisEnd)
                {
                    selectedResult.XAxisStart = tempXAxisStart;
                    selectedResult.XAxisEnd = tempXAxisEnd;
                }

                if (yStartValid && yEndValid && tempYAxisStart < tempYAxisEnd)
                {
                    selectedResult.YAxisStart = tempYAxisStart;
                    selectedResult.YAxisEnd = tempYAxisEnd;
                }

                ApplyScaleType(selectedResult);

                DisplayGraph(selectedResult);
            }
        }

        private void ApplyScaleType(ResultData result)
        {
            double xStart, xEnd, yStart, yEnd;
            bool xStartValid = double.TryParse(xAxisStartTextBox.Text, out xStart);
            bool xEndValid = double.TryParse(xAxisEndTextBox.Text, out xEnd);
            bool yStartValid = double.TryParse(yAxisStartTextBox.Text, out yStart);
            bool yEndValid = double.TryParse(yAxisEndTextBox.Text, out yEnd);

            // If there are negative values or the result type is "Meter Values", force the scale to Linear
            if (xStart <= 0 || xEnd <= 0 || yStart <= 0 || yEnd <= 0 || result.ResultValueType == "Meter Values")
            {
                xScaleComboBox.SelectedIndex = 0; // Linear
                yScaleComboBox.SelectedIndex = 0; // Linear
            }

            result.XScale = xScaleComboBox.SelectedItem.ToString();
            result.YScale = yScaleComboBox.SelectedItem.ToString();

            // ...
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
            AppendLog(logTextBox, "GetCurrentFormData: Returning abbreviated JSON data: " + abbreviatedData);
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
            var sessionForm = new FormSessionManager(sessionData, sessionList, SessionMode.View, this, (message) => AppendLog(logTextBox, message), accessToken, refreshToken);
            sessionForm.ShowDialog();
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

    }

    public class ProjectSession
    {
        public string Title { get; set; }
        public string Data { get; set; }
        public List<SignalPathData> SignalPaths { get; set; }
        public Dictionary<string, string> GlobalProperties { get; set; }
    }
}
