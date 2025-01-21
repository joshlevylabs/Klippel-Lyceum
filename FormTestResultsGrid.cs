using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using OfficeOpenXml;
using System.ComponentModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;
using System.Net.Http;
using System.Threading;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Kernel.Colors;
using iText.Layout.Properties;
using iText.IO.Image;
using PdfColor = iText.Kernel.Colors.Color;
using PdfDeviceRgb = iText.Kernel.Colors.DeviceRgb;
using iText.IO.Font.Constants;
using iText.Kernel.Font;
using System.Windows.Media.TextFormatting;
using ScottPlot;
using ScottPlot.Styles;
using NAudio.Gui;

namespace LAPxv8
{
    public partial class TestResultsGrid : BaseForm
    {
        // Your existing variables and controls
        private ComboBox testSelectionComboBox;
        private List<string> unitNames;
        private List<string> propertyNames;
        private List<List<string>> propertyVariations;
        private List<string> testNames;
        private DataTable testResultsTable;
        private DataGridView dataGridView;
        private Chart resultsChart;
        private TextBox debugTextBox;
        private Panel debugPanel;
        private SplitContainer mainPanel;
        private GroupBox unitGroupBox;
        private GroupBox propertyGroupBox;
        private GroupBox testGroupBox;
        private ListBox addedTestsListBox;
        private bool isHandlingClick = false; // Debounce flag
        private Dictionary<string, string> testResultsStatus;
        private Button addButton;
        private FormSignalPathComparison comparisonForm;
        public Dictionary<(int RowIndex, int ColumnIndex), AttachedResult> attachedResults = new Dictionary<(int, int), AttachedResult>();

        private TextBox unitTextBox;
        private TextBox propertyTextBox;
        private TextBox propertyVariantsTextBox;
        private ListBox unitListBox;
        private ListBox propertyListBox;
        private ListBox testListBox;
        private ListBox propertyVariantListBox;
        private FormAudioPrecision8 mainForm;
        private TextBox customTestNameTextBox;
        private GroupBox aristotleFrame;
        private RichTextBox aristotleChatLog;
        private TextBox aristotleInputBox;
        private Button aristotleSubmitButton;
        //private static readonly HttpClient httpClient = new HttpClient();
        private bool hasIntroduced = false;
        private string analysisContext = "";
        private static event EventHandler<string> NewTestResultAdded;
        private static DateTime _lastApiCall = DateTime.MinValue;
        private static readonly TimeSpan _minTimeBetweenCalls = TimeSpan.FromSeconds(2);
        public string SystemKey { get; private set; }
        public TestResultsGrid(FormAudioPrecision8 form)

        {

            mainForm = form;
            InitializeDebugPanel(); // Ensure this is called first to avoid NullReferenceException
            //InitializeComponent();
            InitializeDynamicInput();
            attachedResults = new Dictionary<(int RowIndex, int ColumnIndex), AttachedResult>(); // Initialize here
            this.Load += new EventHandler(TestResultsGrid_Load);

            // Initialize the attachedResults dictionary
            attachedResults = new Dictionary<(int RowIndex, int ColumnIndex), AttachedResult>();

            // Set a larger size for the form
            this.Size = new Size(1400, 900); // Increase the width and height of the form

            // Create a top spacer panel
            var topSpacerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 0, // Adjust the height as needed to create the desired space
                BackColor = System.Drawing.Color.FromArgb(45, 45, 45) // Match the background color of the form
            };
            Controls.Add(topSpacerPanel);

            // Add the mainPanel after the top spacer panel
            Controls.Add(mainPanel);
            Controls.SetChildIndex(mainPanel, 0); // Ensure the main panel is placed after the spacer

            InitializeToggleDebugButton();  // Initialize the Toggle Debug Button

            LogDebug("Initialization complete.");
        }

        private void ShowComparisonVisualization_Click(object sender, EventArgs e)
        {
            CreateComparisonVisualization();
        }

        protected override void AddCustomMenuItems()
        {
            // Add Save, Load, and New menu items specific to TestResultsGrid
            var newMenuItem = new ToolStripMenuItem("New", null, NewMenuItem_Click);
            var saveMenuItem = new ToolStripMenuItem("Save", null, SaveMenuItem_Click);
            var loadMenuItem = new ToolStripMenuItem("Load", null, LoadMenuItem_Click);
            var pinboardMenuItem = new ToolStripMenuItem("Open Pinboard", null, OpenPinboardMenuItem_Click);


            var fileMenu = menuStrip.Items.OfType<ToolStripMenuItem>().FirstOrDefault(item => item.Text == "File");
            if (fileMenu != null)
            {
                fileMenu.DropDownItems.Add(newMenuItem);
                fileMenu.DropDownItems.Add(saveMenuItem);
                fileMenu.DropDownItems.Add(loadMenuItem);
                fileMenu.DropDownItems.Add(pinboardMenuItem);
            }

            // Add Report menu
            var reportMenuItem = new ToolStripMenuItem("Report");

            var exportSummaryMenuItem = new ToolStripMenuItem("Export Report Summary", null, ExportReportSummary_Click);
            var exportAllDataMenuItem = new ToolStripMenuItem("Export All Data", null, ExportAllData_Click);
            var exportFailureDataMenuItem = new ToolStripMenuItem("Export Failure Data", null, ExportFailureData_Click);

            reportMenuItem.DropDownItems.Add(exportSummaryMenuItem);
            reportMenuItem.DropDownItems.Add(exportAllDataMenuItem);
            reportMenuItem.DropDownItems.Add(exportFailureDataMenuItem);

            menuStrip.Items.Add(reportMenuItem);

            // Add View menu if it doesn't exist
            var viewMenu = menuStrip.Items.OfType<ToolStripMenuItem>()
                .FirstOrDefault(item => item.Text == "View");

            if (viewMenu == null)
            {
                viewMenu = new ToolStripMenuItem("View");
                menuStrip.Items.Add(viewMenu);
            }

            // Add Comparison Visualization menu item
            var showComparisonItem = new ToolStripMenuItem(
                "Show Comparison Visualization",
                null,
                ShowComparisonVisualization_Click);
            viewMenu.DropDownItems.Add(showComparisonItem);
            base.AddCustomMenuItems();

            // Add the Aristotle menu item
            //  var aristotleMenuItem = new ToolStripMenuItem("Aristotle", null, AristotleMenuItem_Click);

            /* var toolsMenu = menuStrip.Items.OfType<ToolStripMenuItem>().FirstOrDefault(item => item.Text == "Tools");
             if (toolsMenu == null)
             {
                 toolsMenu = new ToolStripMenuItem("Tools");
                 menuStrip.Items.Add(toolsMenu);
             }*/

            // toolsMenu.DropDownItems.Add(aristotleMenuItem);
        }


        private void OpenPinboardMenuItem_Click(object sender, EventArgs e)
        {
            FormPinboard.Instance.Show();
        }
        private void ExportReportSummary_Click(object sender, EventArgs e)
        {
            ExportReport(includeAllData: false, includeOnlyFailures: false);
        }

        private void ExportAllData_Click(object sender, EventArgs e)
        {
            ExportReport(includeAllData: true, includeOnlyFailures: false);
        }

        private void ExportFailureData_Click(object sender, EventArgs e)
        {
            ExportReport(includeAllData: true, includeOnlyFailures: true);
        }

        private void ExportReport(bool includeAllData, bool includeOnlyFailures)
        {
            OfficeOpenXml.ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;

            using (var saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Filter = "Excel Files|*.xlsx";
                saveFileDialog.Title = "Save an Excel File";
                saveFileDialog.FileName = includeOnlyFailures ? "FailureData.xlsx" : (includeAllData ? "AllData.xlsx" : "TestResultsReport.xlsx");

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    using (var package = new OfficeOpenXml.ExcelPackage())
                    {
                        // Export the main test results data
                        var summaryWorksheet = package.Workbook.Worksheets.Add("Test Results Summary");

                        // Add headers for the summary sheet
                        for (int col = 0; col < testResultsTable.Columns.Count; col++)
                        {
                            summaryWorksheet.Cells[1, col + 1].Value = testResultsTable.Columns[col].ColumnName;
                        }

                        // Add data to the summary sheet
                        int rowNumber = 2;
                        foreach (DataRow row in testResultsTable.Rows)
                        {
                            bool hasFailures = row.ItemArray.Any(item => item.ToString() == "Fail");

                            bool shouldIncludeRow = includeAllData || (includeOnlyFailures ? hasFailures : true);

                            if (shouldIncludeRow)
                            {
                                for (int col = 0; col < testResultsTable.Columns.Count; col++)
                                {
                                    summaryWorksheet.Cells[rowNumber, col + 1].Value = row[col];
                                }
                                rowNumber++;
                            }
                        }

                        // Export each test run's data into a separate sheet
                        foreach (var result in attachedResults)
                        {
                            string sheetName = $"TestRun_{result.Key.RowIndex}_{result.Key.ColumnIndex}";
                            var worksheet = package.Workbook.Worksheets.Add(sheetName);

                            // Deserialize and add the data to the worksheet
                            if (includeOnlyFailures)
                            {
                                AddDeserializedFailuresToWorksheet(result.Value.Data, worksheet);
                            }
                            else
                            {
                                AddDeserializedDataToWorksheet(result.Value.Data, worksheet);
                            }
                        }

                        // Save the Excel file
                        var file = new System.IO.FileInfo(saveFileDialog.FileName);
                        package.SaveAs(file);

                        MessageBox.Show("Report exported successfully!", "Export Report", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
        }

        public void ExportReportAsJson(string filePath)
        {
            var allData = new
            {
                TestResultsSummary = new List<Dictionary<string, object>>(),
                TestRuns = new Dictionary<string, object>()
            };

            // Add summary data
            foreach (DataRow row in testResultsTable.Rows)
            {
                var rowData = new Dictionary<string, object>();
                foreach (DataColumn column in testResultsTable.Columns)
                {
                    rowData[column.ColumnName] = row[column];
                }
                allData.TestResultsSummary.Add(rowData);
            }

            // Add detailed test run data
            foreach (var result in attachedResults)
            {
                allData.TestRuns[$"TestRun_{result.Key.RowIndex}_{result.Key.ColumnIndex}"] = JsonConvert.DeserializeObject(result.Value.Data);
            }

            // Serialize to JSON and save to the specified file path
            string jsonData = JsonConvert.SerializeObject(allData, Formatting.Indented);
            File.WriteAllText(filePath, jsonData);

            MessageBox.Show("Data exported successfully as JSON!", "Export Report", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void AddDeserializedDataToWorksheet(string jsonData, ExcelWorksheet worksheet)
        {
            try
            {
                LogDebug("Starting deserialization of JSON data.");
                LogDebug($"Raw JSON Data: {jsonData}");

                var topLevelData = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonData);

                if (topLevelData == null)
                {
                    LogDebug("Top-level deserialized data is null.");
                    return;
                }

                if (!topLevelData.ContainsKey("Data"))
                {
                    LogDebug("Key 'Data' not found in top-level deserialized data.");
                    return;
                }

                var nestedJsonData = topLevelData["Data"].ToString();

                // Now, deserialize the nested JSON string
                var deserializedData = JsonConvert.DeserializeObject<Dictionary<string, object>>(nestedJsonData);

                if (deserializedData == null)
                {
                    LogDebug("Deserialized nested data is null.");
                    return;
                }

                LogDebug("Nested data deserialized successfully.");

                int startCol = 3;  // Start from column C

                // Add GlobalProperties
                if (deserializedData.ContainsKey("GlobalProperties"))
                {
                    LogDebug("Processing GlobalProperties.");
                    worksheet.Cells[1, 1].Value = "Global Properties";
                    int row = 2;
                    var globalProperties = deserializedData["GlobalProperties"] as JObject;

                    foreach (var property in globalProperties.Properties())
                    {
                        worksheet.Cells[row, 1].Value = property.Name;
                        worksheet.Cells[row, 2].Value = property.Value.ToString();
                        row++;
                    }
                }
                else
                {
                    LogDebug("GlobalProperties not found in deserialized data.");
                }

                // Add CheckedData
                if (deserializedData.ContainsKey("CheckedData"))
                {
                    LogDebug("Processing CheckedData.");
                    var checkedDataList = deserializedData["CheckedData"] as JArray;

                    foreach (var checkedData in checkedDataList)
                    {
                        var dataObject = checkedData as JObject;

                        // Place the Name in the top row of the current column
                        worksheet.Cells[1, startCol].Value = $"Name: {dataObject["Name"]}";
                        int row = 2;

                        foreach (var measurement in dataObject["Measurements"])
                        {
                            var measurementObject = measurement as JObject;

                            worksheet.Cells[1, startCol + 1].Value = $"Measurement: {measurementObject["Name"]}";
                            row++;

                            foreach (var result in measurementObject["Results"])
                            {
                                var resultObject = result as JObject;

                                // Write the result name in the top row of the next column
                                worksheet.Cells[1, startCol + 2].Value = $"Result: {resultObject["Name"]}";
                                int dataRow = 2;

                                // Write out each key-value pair in the result, except XValues, YValues, and MeterValues
                                foreach (var item in resultObject)
                                {
                                    if (item.Key != "XValues" && item.Key != "YValuesPerChannel" && item.Key != "MeterValues")
                                    {
                                        worksheet.Cells[dataRow, startCol].Value = item.Key;
                                        worksheet.Cells[dataRow, startCol + 1].Value = item.Value?.ToString() ?? string.Empty;
                                        dataRow++;
                                    }
                                }

                                // Write XValues in a single column
                                if (resultObject.ContainsKey("XValues"))
                                {
                                    var xValues = resultObject["XValues"] as JArray;
                                    if (xValues != null)
                                    {
                                        worksheet.Cells[dataRow, startCol + 2].Value = "XValues";
                                        int xRowStart = dataRow + 1;
                                        foreach (var value in xValues)
                                        {
                                            worksheet.Cells[xRowStart, startCol + 2].Value = value.ToString();
                                            xRowStart++;
                                        }
                                        dataRow = xRowStart;
                                    }
                                }

                                // Write YValuesPerChannel in the next columns, aligned with XValues
                                if (resultObject.ContainsKey("YValuesPerChannel"))
                                {
                                    var yValuesPerChannel = resultObject["YValuesPerChannel"] as JObject;
                                    if (yValuesPerChannel != null)
                                    {
                                        int yColStart = startCol + 3; // Start writing YValues in the next column
                                        foreach (var channel in yValuesPerChannel)
                                        {
                                            worksheet.Cells[dataRow - 1, yColStart].Value = $"YValues ({channel.Key})";
                                            var yValuesArray = channel.Value as JArray;
                                            if (yValuesArray != null)
                                            {
                                                int yRowStart = dataRow - yValuesArray.Count; // Align with XValues
                                                foreach (var value in yValuesArray)
                                                {
                                                    worksheet.Cells[yRowStart, yColStart].Value = value.ToString();
                                                    yRowStart++;
                                                }
                                            }
                                            yColStart++; // Move to the next column for another channel
                                        }
                                    }
                                }

                                // Write MeterValues in the next available column
                                if (resultObject.ContainsKey("MeterValues"))
                                {
                                    var meterValues = resultObject["MeterValues"] as JArray;
                                    if (meterValues != null)
                                    {
                                        worksheet.Cells[dataRow, startCol + 4].Value = "MeterValues";
                                        int meterRowStart = dataRow + 1;
                                        foreach (var value in meterValues)
                                        {
                                            worksheet.Cells[meterRowStart, startCol + 4].Value = value.ToString();
                                            meterRowStart++;
                                        }
                                        dataRow = meterRowStart;
                                    }
                                }

                                // Move to the next column for the next result
                                startCol += 5;
                            }
                        }
                    }
                }
                else
                {
                    LogDebug("CheckedData not found in deserialized data.");
                }

                worksheet.Cells.AutoFitColumns();
                LogDebug("Finished writing data to worksheet and autofitting columns.");
            }
            catch (Exception ex)
            {
                LogDebug($"Failed to deserialize and add data to worksheet: {ex.Message}");
            }
        }

        private void AddDeserializedFailuresToWorksheet(string jsonData, ExcelWorksheet worksheet)
        {
            try
            {
                LogDebug("Starting deserialization of JSON data for failures only.");
                LogDebug($"Raw JSON Data: {jsonData}");

                var topLevelData = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonData);

                if (topLevelData == null)
                {
                    LogDebug("Top-level deserialized data is null.");
                    return;
                }

                if (!topLevelData.ContainsKey("Data"))
                {
                    LogDebug("Key 'Data' not found in top-level deserialized data.");
                    return;
                }

                var nestedJsonData = topLevelData["Data"].ToString();

                // Now, deserialize the nested JSON string
                var deserializedData = JsonConvert.DeserializeObject<Dictionary<string, object>>(nestedJsonData);

                if (deserializedData == null)
                {
                    LogDebug("Deserialized nested data is null.");
                    return;
                }

                LogDebug("Nested data deserialized successfully.");

                int startCol = 3;  // Start from column C

                // Add GlobalProperties
                if (deserializedData.ContainsKey("GlobalProperties"))
                {
                    LogDebug("Processing GlobalProperties.");
                    worksheet.Cells[1, 1].Value = "Global Properties";
                    int row = 2;
                    var globalProperties = deserializedData["GlobalProperties"] as JObject;

                    foreach (var property in globalProperties.Properties())
                    {
                        worksheet.Cells[row, 1].Value = property.Name;
                        worksheet.Cells[row, 2].Value = property.Value.ToString();
                        row++;
                    }
                }
                else
                {
                    LogDebug("GlobalProperties not found in deserialized data.");
                }

                // Add CheckedData
                if (deserializedData.ContainsKey("CheckedData"))
                {
                    LogDebug("Processing CheckedData for failures.");
                    var checkedDataList = deserializedData["CheckedData"] as JArray;

                    foreach (var checkedData in checkedDataList)
                    {
                        var dataObject = checkedData as JObject;

                        // Place the Name in the top row of the current column
                        worksheet.Cells[1, startCol].Value = $"Name: {dataObject["Name"]}";
                        int row = 2;

                        foreach (var measurement in dataObject["Measurements"])
                        {
                            var measurementObject = measurement as JObject;

                            // Filter for failed results only
                            var failedResults = measurementObject["Results"]
                                .Where(r => (r["Passed"]?.Value<bool>() == false))
                                .ToList();

                            if (failedResults.Count > 0)
                            {
                                worksheet.Cells[1, startCol + 1].Value = $"Measurement: {measurementObject["Name"]}";

                                foreach (var result in failedResults)
                                {
                                    var resultObject = result as JObject;

                                    // Write the result name in the top row of the next column
                                    worksheet.Cells[1, startCol + 2].Value = $"Result: {resultObject["Name"]}";
                                    int dataRow = 2;

                                    // Write out each key-value pair in the result, except XValues, YValues, and MeterValues
                                    foreach (var item in resultObject)
                                    {
                                        if (item.Key != "XValues" && item.Key != "YValuesPerChannel" && item.Key != "MeterValues")
                                        {
                                            worksheet.Cells[dataRow, startCol].Value = item.Key;
                                            worksheet.Cells[dataRow, startCol + 1].Value = item.Value?.ToString() ?? string.Empty;
                                            dataRow++;
                                        }
                                    }

                                    // Write XValues in a single column
                                    if (resultObject.ContainsKey("XValues"))
                                    {
                                        var xValues = resultObject["XValues"] as JArray;
                                        if (xValues != null)
                                        {
                                            worksheet.Cells[dataRow, startCol + 2].Value = "XValues";
                                            int xRowStart = dataRow + 1;
                                            foreach (var value in xValues)
                                            {
                                                worksheet.Cells[xRowStart, startCol + 2].Value = value.ToString();
                                                xRowStart++;
                                            }
                                            dataRow = xRowStart;
                                        }
                                    }

                                    // Write YValuesPerChannel in the next columns, aligned with XValues
                                    if (resultObject.ContainsKey("YValuesPerChannel"))
                                    {
                                        var yValuesPerChannel = resultObject["YValuesPerChannel"] as JObject;
                                        if (yValuesPerChannel != null)
                                        {
                                            int yColStart = startCol + 3; // Start writing YValues in the next column
                                            foreach (var channel in yValuesPerChannel)
                                            {
                                                worksheet.Cells[dataRow - 1, yColStart].Value = $"YValues ({channel.Key})";
                                                var yValuesArray = channel.Value as JArray;
                                                if (yValuesArray != null)
                                                {
                                                    int yRowStart = dataRow - yValuesArray.Count; // Align with XValues
                                                    foreach (var value in yValuesArray)
                                                    {
                                                        worksheet.Cells[yRowStart, yColStart].Value = value.ToString();
                                                        yRowStart++;
                                                    }
                                                }
                                                yColStart++; // Move to the next column for another channel
                                            }
                                        }
                                    }

                                    // Write MeterValues in the next available column
                                    if (resultObject.ContainsKey("MeterValues"))
                                    {
                                        var meterValues = resultObject["MeterValues"] as JArray;
                                        if (meterValues != null)
                                        {
                                            worksheet.Cells[dataRow, startCol + 4].Value = "MeterValues";
                                            int meterRowStart = dataRow + 1;
                                            foreach (var value in meterValues)
                                            {
                                                worksheet.Cells[meterRowStart, startCol + 4].Value = value.ToString();
                                                meterRowStart++;
                                            }
                                            dataRow = meterRowStart;
                                        }
                                    }

                                    // Move to the next column for the next result
                                    startCol += 5;
                                }
                            }
                        }
                    }
                }
                else
                {
                    LogDebug("CheckedData not found in deserialized data.");
                }

                worksheet.Cells.AutoFitColumns();
                LogDebug("Finished writing data to worksheet and autofitting columns.");
            }
            catch (Exception ex)
            {
                LogDebug($"Failed to deserialize and add data to worksheet: {ex.Message}");
            }
        }

        private Dictionary<string, object> DeserializeTestData(string jsonData)
        {
            try
            {
                var deserializedData = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonData);
                return deserializedData;
            }
            catch (Exception ex)
            {
                LogDebug($"Failed to deserialize test data: {ex.Message}");
                return null;
            }
        }

        // Event handlers for Save and Load
        private void SaveMenuItem_Click(object sender, EventArgs e)
        {
            using (var saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Filter = "Test Config Files|*.tcf";
                saveFileDialog.Title = "Save Test Configuration";
                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    SaveTestConfiguration(saveFileDialog.FileName);
                    MessageBox.Show("Configuration saved successfully!", "Save Configuration", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void LoadMenuItem_Click(object sender, EventArgs e)
        {
            using (var openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Test Config Files|*.tcf";
                openFileDialog.Title = "Load Test Configuration";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    LoadTestConfiguration(openFileDialog.FileName);
                    MessageBox.Show("Configuration loaded successfully!", "Load Configuration", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void NewMenuItem_Click(object sender, EventArgs e)
        {
            // Clear all data related to the test results grid
            unitNames.Clear();
            propertyNames.Clear();
            propertyVariations.Clear();
            testNames.Clear();
            testResultsStatus.Clear();

            // Reset the input fields and list boxes
            unitListBox.Items.Clear();
            propertyListBox.Items.Clear();
            propertyVariantListBox.Items.Clear();
            testListBox.Items.Clear();

            // Reinitialize the test results grid
            InitializeTestConfig();

            // Update the grid to reflect the cleared state
            DisplayHeatmap();

            // Clear the debug logs if necessary
            debugTextBox.Clear();

            LogDebug("Started a new test results grid.");
        }

        private void SaveTestConfiguration(string filePath)
        {
            var config = new TestConfig
            {
                UnitNames = unitNames,
                PropertyNames = propertyNames,
                PropertyVariations = propertyVariations,
                TestNames = testNames,
                TestResultsStatus = testResultsStatus,
                AttachedResults = attachedResults // Save attachments
            };

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                var formatter = new BinaryFormatter();
                formatter.Serialize(stream, config);
            }
        }

        private void LoadTestConfiguration(string filePath)
        {
            using (var stream = new FileStream(filePath, FileMode.Open))
            {
                var formatter = new BinaryFormatter();
                var config = (TestConfig)formatter.Deserialize(stream);

                unitNames = config.UnitNames;
                propertyNames = config.PropertyNames;
                propertyVariations = config.PropertyVariations;
                testNames = config.TestNames;
                testResultsStatus = config.TestResultsStatus;
                attachedResults = config.AttachedResults; // Load attachments

                PopulateUIElements(); // Populate the UI with the loaded data

                UpdateResultsGrid(); // Refresh the grid with the loaded configuration
            }
        }
        private void PopulateUIElements()
        {
            // Clear existing items
            unitListBox.Items.Clear();
            propertyListBox.Items.Clear();
            propertyVariantListBox.Items.Clear();
            addedTestsListBox.Items.Clear(); // Clear the added tests ListBox

            // Populate Unit ListBox
            foreach (var unit in unitNames)
            {
                unitListBox.Items.Add(unit);
            }

            // Populate Property ListBox and Variants ListBox
            for (int i = 0; i < propertyNames.Count; i++)
            {
                propertyListBox.Items.Add(propertyNames[i]);
                if (propertyVariations.Count > i)
                {
                    foreach (var variant in propertyVariations[i])
                    {
                        propertyVariantListBox.Items.Add(variant);
                    }
                }
            }

            // Populate Test ListBox
            foreach (var test in testNames)
            {
                addedTestsListBox.Items.Add(test); // Populate the added tests ListBox
            }
        }

        private void ClearCurrentConfiguration()
        {
            unitNames.Clear();
            propertyNames.Clear();
            propertyVariations.Clear();
            testNames.Clear();
            testResultsStatus.Clear();

            unitListBox.Items.Clear();
            propertyListBox.Items.Clear();
            propertyVariantListBox.Items.Clear();
            testListBox.Items.Clear();

            testResultsTable?.Clear(); // Clear the DataTable if it exists
            dataGridView?.Columns.Clear(); // Clear DataGridView columns if they exist
        }

        private void UpdateUIElements()
        {
            foreach (var unit in unitNames)
            {
                unitListBox.Items.Add(unit);
            }

            foreach (var property in propertyNames)
            {
                propertyListBox.Items.Add(property);
            }

            foreach (var test in testNames)
            {
                testListBox.Items.Add(test);
            }

            // Optionally, you can pre-select the first item in each list if desired
            if (unitListBox.Items.Count > 0) unitListBox.SelectedIndex = 0;
            if (propertyListBox.Items.Count > 0) propertyListBox.SelectedIndex = 0;
            if (testListBox.Items.Count > 0) testListBox.SelectedIndex = 0;
        }

        private void PopulateTestResultsStatus()
        {
            testResultsStatus.Clear(); // Clear existing status

            for (int rowIndex = 0; rowIndex < testResultsTable.Rows.Count; rowIndex++)
            {
                foreach (DataColumn column in testResultsTable.Columns)
                {
                    if (column.ColumnName.EndsWith("Result"))
                    {
                        string key = GenerateStatusKey(rowIndex, testResultsTable.Columns.IndexOf(column.ColumnName));
                        string value = testResultsTable.Rows[rowIndex][column.ColumnName].ToString();
                        testResultsStatus[key] = value;
                        LogDebug($"Saving status - Key: {key}, Value: {value}");
                    }
                }
            }
        }

        private void InitializeToggleDebugButton()
        {
            var toggleDebugButton = new Button
            {
                Text = "Show Logs", // Initial text for the button
                Width = 160, // Adjust width as needed
                Height = 30, // Adjust height as needed
                BackColor = System.Drawing.Color.FromArgb(85, 160, 140),
                ForeColor = System.Drawing.Color.White,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 },
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left, // Anchor it to the bottom-left
            };
            ApplyRoundedCorners(toggleDebugButton);

            // Set the position to the bottom left of the form
            toggleDebugButton.Location = new Point(20, this.ClientSize.Height - toggleDebugButton.Height - 20);

            Controls.Add(toggleDebugButton);
            toggleDebugButton.BringToFront();
            toggleDebugButton.Click += ToggleDebugPanel_Click;

            // Adjust button position on resize
            this.Resize += (s, e) =>
            {
                toggleDebugButton.Location = new Point(20, this.ClientSize.Height - toggleDebugButton.Height - 20);
            };
        }

        private void ToggleDebugPanel_Click(object sender, EventArgs e)
        {
            debugPanel.Visible = !debugPanel.Visible;

            var toggleDebugButton = sender as Button;
            if (debugPanel.Visible)
            {
                toggleDebugButton.Text = "Hide Logs";
            }
            else
            {
                toggleDebugButton.Text = "Show Logs";
            }

            // Reposition button after toggle
            toggleDebugButton.Location = new Point((this.ClientSize.Width - toggleDebugButton.Width) / 2, this.ClientSize.Height - toggleDebugButton.Height - 20);
        }

        private void TestResultsGrid_Load(object sender, EventArgs e)
        {
            mainPanel.SplitterDistance = 335; // Adjust this value to get the desired width for the left panel

            // Initialize and retrieve the system key automatically
            var formSessionManager = new FormSessionManager(null, null, SessionMode.View, null, null, null, null);
            if (string.IsNullOrEmpty(SystemKey))
            {
                SystemKey = formSessionManager.GetOrCreateEncryptionKey(GetLogTextBox());
                LogDebug($"SystemKey initialized on load: {(SystemKey != null ? "Success" : "Failed")}");
            }
            else
            {
                LogDebug("SystemKey already initialized.");
            }
        }

        private async void SubmitButton_Click(object sender, EventArgs e, RichTextBox chatLog, TextBox inputTextBox)
        {
            string userInput = inputTextBox.Text.Trim();
            if (!string.IsNullOrEmpty(userInput))
            {
                // Display user input in the chat log
                chatLog.AppendText($"You: {userInput}\n");
                chatLog.ScrollToCaret();
                inputTextBox.Clear();

                // Get response from Aristotle (Claude API)
                var aristotleInstance = new Aristotle(this);
                string aiResponse = await CallClaudeAPI(userInput);

                // Display the AI response
                chatLog.AppendText($"Aristotle: {aiResponse}\n");
                chatLog.ScrollToCaret();
            }
        }

        private void AttachButton_Click(object sender, EventArgs e, RichTextBox chatLog)
        {
            using (var openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Title = "Attach a File";
                openFileDialog.Filter = "All Files|*.*";

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    // Display file attachment in chat log
                    string filePath = openFileDialog.FileName;
                    chatLog.AppendText($"[Attached File: {Path.GetFileName(filePath)}]\n");
                    chatLog.ScrollToCaret();
                }
            }
        }


        private async Task<string> CallClaudeAPI(string prompt)
        {
            string apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
            if (string.IsNullOrEmpty(apiKey))
            {
                aristotleChatLog.AppendText("Error: API key is missing. Please set the ANTHROPIC_API_KEY environment variable.\n");
                return null;
            }

            try
            {
                // Create a new HttpClient for each request
                using (var httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
                    httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

                    var requestBody = new
                    {
                        model = "claude-3-haiku-20240307",
                        max_tokens = 4096,
                        messages = new[]
                        {
                    new { role = "user", content = prompt }
                }
                    };

                    string jsonContent = JsonConvert.SerializeObject(requestBody);
                    var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                    HttpResponseMessage response = await httpClient.PostAsync(
                        "https://api.anthropic.com/v1/messages",
                        content
                    );

                    if (!response.IsSuccessStatusCode)
                    {
                        string errorResponse = await response.Content.ReadAsStringAsync();
                        LogDebug($"API Error: {errorResponse}");
                        return $"API Error: {errorResponse}";
                    }

                    string responseString = await response.Content.ReadAsStringAsync();
                    dynamic jsonResponse = JsonConvert.DeserializeObject(responseString);

                    return jsonResponse.content[0].text.ToString();
                }
            }
            catch (Exception ex)
            {
                LogDebug($"Error calling Claude API: {ex.Message}");
                return $"Error: {ex.Message}";
            }
        }

        private async Task CreateMeasurementPlot(Document document, JToken result, string measurementName)
        {
            var plt = new ScottPlot.Plot(800, 600);
            plt.Title(measurementName);
            plt.XLabel("Frequency (Hz)");
            plt.YLabel(GetYAxisLabel(measurementName));

            var xValues = result["XValues"].ToObject<double[]>();
            var yValuesObj = result["YValuesPerChannel"] as JObject;

            var colorPalette = new[] {
        System.Drawing.Color.DodgerBlue,
        System.Drawing.Color.OrangeRed
    };

            int colorIndex = 0;
            foreach (var channel in yValuesObj.Properties())
            {
                var yValues = channel.Value.ToObject<double[]>();
                var color = colorPalette[colorIndex % colorPalette.Length];

                var scatter = plt.AddScatter(ScottPlot.Tools.Log10(xValues), yValues, color, label: $"Channel {channel.Name}");
                scatter.LineWidth = 2;
                scatter.MarkerSize = 0;

                colorIndex++;
            }

            ConfigurePlotStyle(plt);
            await SavePlotToPdf(document, plt, measurementName);
        }
        private async void AddFrequencyResponseGraph(Document document, JToken result)
        {
            try
            {
                LogDebug("Starting to create frequency response graphs");

                var measurementResults = result["Results"] as JArray;
                if (measurementResults != null)
                {
                    // Create separate frequency response plots for each measurement type
                    foreach (var measurementResult in measurementResults)
                    {
                        string measurementName = measurementResult["Name"].ToString();

                        if (measurementResult["XValues"] != null && measurementResult["YValuesPerChannel"] != null)
                        {
                            // Create a frequency response plot for this measurement
                            await CreateFrequencyResponsePlot(document, measurementResult, measurementName);
                        }
                        else if (measurementResult["MeterValues"] != null)
                        {
                            // Handle bar chart measurements
                            switch (measurementName)
                            {
                                case "RMS Level":
                                    await CreateRMSLevelPlot(document, measurementResult);
                                    break;
                                case "Gain":
                                    await CreateGainPlot(document, measurementResult);
                                    break;
                                case "Relative Level":
                                    await CreateRelativeLevelPlot(document, measurementResult);
                                    break;
                                case "Deviation":
                                    await CreateDeviationPlot(document, measurementResult);
                                    break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogDebug($"Error creating frequency response graphs: {ex.Message}");
            }
        }

        private async Task CreateFrequencyResponsePlot(Document document, JToken result, string measurementName)
        {
            var plt = new ScottPlot.Plot(800, 600);
            plt.Title($"{measurementName} Frequency Response");
            plt.XLabel("Frequency (Hz)");
            plt.YLabel("Amplitude (dB)");

            var xValues = result["XValues"].ToObject<double[]>();
            var yValuesObj = result["YValuesPerChannel"] as JObject;

            var colorPalette = new[] {
        System.Drawing.Color.DodgerBlue,
        System.Drawing.Color.OrangeRed
    };

            int colorIndex = 0;
            foreach (var channel in yValuesObj.Properties())
            {
                var yValues = channel.Value.ToObject<double[]>();
                var color = colorPalette[colorIndex % colorPalette.Length];

                var scatter = plt.AddScatter(ScottPlot.Tools.Log10(xValues), yValues, color, label: $"Channel {channel.Name}");
                scatter.LineWidth = 2;
                scatter.MarkerSize = 0;

                colorIndex++;
            }

            ConfigurePlotStyle(plt);
            await SavePlotToPdf(document, plt, $"{measurementName} Frequency Response");
        }

        private string GetYAxisLabel(string measurementName)
        {
            switch (measurementName)
            {
                case "RMS Level":
                    return "Level (dB)";
                case "Gain":
                    return "Gain (dB)";
                case "Frequency Response":
                    return "Amplitude (dB)";
                default:
                    return "Amplitude (dB)";
            }
        }

        private async Task CreateRMSLevelPlot(Document document, JToken result)
        {
            var plt = new ScottPlot.Plot(800, 600);
            plt.Title("RMS Level");
            plt.XLabel("Channel");
            plt.YLabel("Level (dB)");

            var meterValues = result["MeterValues"].ToObject<double[]>();

            // Create bar plot without positions first
            var bar = plt.AddBar(meterValues);
            bar.BarWidth = 0.5;  // Adjusted bar width
            bar.FillColor = System.Drawing.Color.DodgerBlue;

            // Create channel labels
            string[] labels = Enumerable.Range(1, meterValues.Length)
                                       .Select(i => $"Ch {i}")
                                       .ToArray();

            // Set the labels on the x-axis
            plt.XTicks(Enumerable.Range(0, meterValues.Length).Select(i => (double)i).ToArray(), labels);

            ConfigurePlotStyle(plt);
            await SavePlotToPdf(document, plt, "RMS Level");
        }

        private async Task CreateGainPlot(Document document, JToken result)
        {
            var plt = new ScottPlot.Plot(800, 600);
            plt.Title("Gain");
            plt.XLabel("Channel");
            plt.YLabel("Gain (dB)");

            var meterValues = result["MeterValues"].ToObject<double[]>();

            // Create bar plot without positions
            var bar = plt.AddBar(meterValues);
            bar.BarWidth = 0.5;  // Adjusted bar width
            bar.FillColor = System.Drawing.Color.LimeGreen;

            // Create channel labels
            string[] labels = Enumerable.Range(1, meterValues.Length)
                                       .Select(i => $"Ch {i}")
                                       .ToArray();

            // Set the labels on the x-axis
            plt.XTicks(Enumerable.Range(0, meterValues.Length).Select(i => (double)i).ToArray(), labels);

            ConfigurePlotStyle(plt);
            await SavePlotToPdf(document, plt, "Gain");
        }

        private async Task CreateRelativeLevelPlot(Document document, JToken result)
        {
            var plt = new ScottPlot.Plot(800, 600);
            plt.Title("Relative Level");
            plt.XLabel("Channel");
            plt.YLabel("Relative Level (dB)");

            var meterValues = result["MeterValues"].ToObject<double[]>();

            // Create bar plot without positions
            var bar = plt.AddBar(meterValues);
            bar.BarWidth = 0.5;  // Adjusted bar width
            bar.FillColor = System.Drawing.Color.Gold;

            // Create channel labels
            string[] labels = Enumerable.Range(1, meterValues.Length)
                                       .Select(i => $"Ch {i}")
                                       .ToArray();

            // Set the labels on the x-axis
            plt.XTicks(Enumerable.Range(0, meterValues.Length).Select(i => (double)i).ToArray(), labels);

            ConfigurePlotStyle(plt);
            await SavePlotToPdf(document, plt, "Relative Level");
        }

        private async Task CreateDeviationPlot(Document document, JToken result)
        {
            var plt = new ScottPlot.Plot(800, 600);
            plt.Title("Frequency Response Deviation");
            plt.XLabel("Channel");
            plt.YLabel("Deviation (dB)");

            var meterValues = result["MeterValues"].ToObject<double[]>();

            // Create bar plot without positions
            var bar = plt.AddBar(meterValues);
            bar.BarWidth = 0.5;  // Adjusted bar width
            bar.FillColor = System.Drawing.Color.OrangeRed;

            // Create channel labels
            string[] labels = Enumerable.Range(1, meterValues.Length)
                                       .Select(i => $"Ch {i}")
                                       .ToArray();

            // Set the labels on the x-axis
            plt.XTicks(Enumerable.Range(0, meterValues.Length).Select(i => (double)i).ToArray(), labels);

            ConfigurePlotStyle(plt);
            await SavePlotToPdf(document, plt, "Deviation");
        }

        private void ConfigurePlotStyle(ScottPlot.Plot plt)
        {
            // Style the plot
            plt.Style(figureBackground: System.Drawing.Color.FromArgb(45, 45, 45),
                      dataBackground: System.Drawing.Color.FromArgb(60, 60, 60));

            // Configure legend
            plt.Legend(true, ScottPlot.Alignment.UpperRight);
            plt.Legend().FontColor = System.Drawing.Color.White;

            // Style axes
            plt.XAxis.Color(System.Drawing.Color.White);
            plt.YAxis.Color(System.Drawing.Color.White);
            plt.XAxis.TickLabelStyle(color: System.Drawing.Color.White);
            plt.YAxis.TickLabelStyle(color: System.Drawing.Color.White);
        }

        private async Task SavePlotToPdf(Document document, ScottPlot.Plot plt, string plotTitle)
        {
            string tempFilePath = Path.Combine(Path.GetTempPath(), $"{plotTitle.ToLower().Replace(" ", "_")}_{Guid.NewGuid()}.png");

            try
            {
                // Save the plot to the temporary file
                plt.SaveFig(tempFilePath);

                // Add plot title to document
                document.Add(new Paragraph(plotTitle)
                    .SetFontSize(14)
                    .SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER));

                // Add the image to the PDF document
                ImageData image = ImageDataFactory.Create(tempFilePath);
                document.Add(new iText.Layout.Element.Image(image)
                    .SetWidth(400)
                    .SetHorizontalAlignment(iText.Layout.Properties.HorizontalAlignment.CENTER));

                // Add some spacing after the plot
                document.Add(new Paragraph("\n"));

                LogDebug($"Successfully added {plotTitle} graph to document");
            }
            finally
            {
                // Clean up the temporary file
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
            }
        }

        private void PlotResponseCurve(Table gridTable, double[] frequencies, double[] response,
            double[] freqScale, int[] dbScale, int gridCols, int gridRows)
        {
            // Convert frequency and response data to grid coordinates
            for (int i = 0; i < frequencies.Length - 1; i++)
            {
                int x1 = MapFrequencyToGrid(frequencies[i], freqScale, gridCols);
                int x2 = MapFrequencyToGrid(frequencies[i + 1], freqScale, gridCols);
                int y1 = MapResponseToGrid(response[i], dbScale, gridRows);
                int y2 = MapResponseToGrid(response[i + 1], dbScale, gridRows);

                // Draw line segment
                DrawLineSegment(gridTable, x1, y1, x2, y2, gridCols, gridRows);
            }
        }

        private int MapFrequencyToGrid(double freq, double[] freqScale, int gridCols)
        {
            double logFreq = Math.Log10(freq);
            double logMin = Math.Log10(freqScale[0]);
            double logMax = Math.Log10(freqScale[freqScale.Length - 1]);
            return (int)((logFreq - logMin) / (logMax - logMin) * gridCols);
        }

        private int MapResponseToGrid(double response, int[] dbScale, int gridRows)
        {
            double min = dbScale[0];
            double max = dbScale[dbScale.Length - 1];
            return gridRows - 1 - (int)((response - min) / (max - min) * gridRows);
        }

        private void DrawLineSegment(Table gridTable, int x1, int y1, int x2, int y2,
            int gridCols, int gridRows)
        {

            int dx = x2 - x1;
            int dy = y2 - y1;
            int steps = Math.Max(Math.Abs(dx), Math.Abs(dy));

            if (steps == 0) return;

            float xIncrement = dx / (float)steps;
            float yIncrement = dy / (float)steps;

            float x = x1;
            float y = y1;

            for (int i = 0; i <= steps; i++)
            {
                int gridX = (int)Math.Round(x);
                int gridY = (int)Math.Round(y);

                if (gridX >= 0 && gridX < gridCols && gridY >= 0 && gridY < gridRows)
                {
                    // Mark this cell in the grid
                    Cell cell = new Cell().SetBackgroundColor(ColorConstants.BLUE);
                    gridTable.AddCell(cell);
                }

                x += xIncrement;
                y += yIncrement;
            }
        }

        private void AddFrequencyResponseSection(Document document, PdfColor headerColor, PdfColor textColor)
        {
            try
            {
                document.Add(new Paragraph("Frequency Response Analysis")
                    .SetFontSize(16)
                    .SetFontColor(headerColor)
                    .SetMarginBottom(10));

                foreach (var result in attachedResults)
                {
                    var testRun = dataGridView?.Rows[result.Key.RowIndex]?.Cells["Test Run"]?.Value?.ToString() ?? "Unknown Test Run";

                    document.Add(new Paragraph($"Test Run: {testRun}")
                        .SetFontSize(14)
                        .SetFontColor(headerColor)
                        .SetMarginBottom(5));

                    var measurements = GetMeasurementsFromResult(result.Value);
                    if (measurements != null)
                    {
                        var analysisBuilder = new StringBuilder();
                        ProcessFrequencyResponseSection(measurements, analysisBuilder);

                        document.Add(new Paragraph(analysisBuilder.ToString())
                            .SetFontColor(textColor)
                            .SetMarginBottom(15));
                    }
                }
            }
            catch (Exception ex)
            {
                LogDebug($"Error in AddFrequencyResponseSection: {ex.Message}");
                throw;
            }
        }

        private void AddTableCell(Table table, string text, PdfColor color)
        {
            try
            {
                var cell = new Cell()
                    .Add(new Paragraph(text ?? "").SetFontColor(color))
                    .SetPadding(5);
                table.AddCell(cell);
            }
            catch (Exception ex)
            {
                LogDebug($"Error adding table cell: {ex.Message}");
                throw;
            }
        }
        private void AddComparisonSection(Document document, PdfColor headerColor, PdfColor textColor)
        {
            document.Add(new Paragraph("\nComparison Analysis")
                .SetFontSize(16)
                .SetFontColor(headerColor));

            if (attachedResults?.Count > 1)
            {
                // Add comparison analysis here
                // We'll implement this in the next step
            }
            else
            {
                document.Add(new Paragraph("Insufficient data for comparison analysis. At least two test results are required.")
                    .SetFontSize(12)
                    .SetFontColor(textColor));
            }
        }

        private JArray GetMeasurementsFromResult(AttachedResult result)
        {
            try
            {
                var testData = JObject.Parse(result.Data);
                var nestedData = JObject.Parse(testData["Data"].ToString());
                var checkedData = nestedData["CheckedData"] as JArray;
                var signalPath = checkedData?.FirstOrDefault(d => d["Name"]?.ToString() == "Signal Path1");
                return signalPath?["Measurements"] as JArray;
            }
            catch (Exception ex)
            {
                LogDebug($"Error extracting measurements: {ex.Message}");
                return null;
            }
        }
        private void AddComparisonGraphs(Document document, List<IGrouping<string, KeyValuePair<(int RowIndex, int ColumnIndex), AttachedResult>>> variantGroups)
        {
            try
            {
                document.Add(new Paragraph("Comparison Between Variants")
                    .SetFontSize(16)
                    .SetMarginBottom(10));

                // Create plots for each measurement type
                foreach (var measurementType in new[] { "Frequency Response", "RMS Level", "Gain" })
                {
                    var plt = new ScottPlot.Plot(800, 400);
                    plt.Title(measurementType);

                    // Add data for each variant
                    foreach (var variantGroup in variantGroups)
                    {
                        AddVariantDataToPlot(plt, variantGroup, measurementType);
                    }

                    // Save plot to temp file
                    string tempFile = Path.Combine(Path.GetTempPath(), $"comparison_{Guid.NewGuid()}.png");
                    plt.SaveFig(tempFile);

                    // Add to PDF
                    ImageData imageData = ImageDataFactory.Create(tempFile);
                    document.Add(new iText.Layout.Element.Image(imageData)
                        .SetWidth(400)
                        .SetHorizontalAlignment(iText.Layout.Properties.HorizontalAlignment.CENTER));

                    // Cleanup
                    File.Delete(tempFile);
                }
            }
            catch (Exception ex)
            {
                LogDebug($"Error generating comparison graphs: {ex.Message}");
            }
        }

        private void ProcessTestResult(Document document, JToken testResult)
        {
            try
            {
                var resultName = testResult["Name"]?.ToString();
                if (string.IsNullOrEmpty(resultName))
                {
                    LogDebug("Result name is null or empty");
                    return;
                }

                var xValues = testResult["XValues"] as JArray;
                var yValuesObj = testResult["YValuesPerChannel"] as JObject;

                if (xValues == null || yValuesObj == null || !xValues.Any())
                {
                    LogDebug($"Missing X or Y values for result {resultName}");
                    return;
                }

                // Create simple table representation
                Table dataTable = new Table(yValuesObj.Count + 1)
                    .UseAllAvailableWidth()
                    .SetFontSize(8);

                // Add headers
                dataTable.AddCell(new Cell().Add(new Paragraph("Frequency (Hz)")));
                foreach (var channel in yValuesObj.Properties())
                {
                    dataTable.AddCell(new Cell().Add(new Paragraph(channel.Name)));
                }

                // Add data (limit to 10 points for space)
                int pointLimit = Math.Min(10, xValues.Count());
                for (int i = 0; i < pointLimit; i++)
                {
                    dataTable.AddCell(new Cell().Add(new Paragraph(xValues[i].Value<double>().ToString("F2"))));
                    foreach (var channel in yValuesObj.Properties())
                    {
                        var channelData = channel.Value as JArray;
                        if (channelData != null && i < channelData.Count)
                        {
                            dataTable.AddCell(new Cell().Add(new Paragraph(channelData[i].Value<double>().ToString("F2"))));
                        }
                        else
                        {
                            dataTable.AddCell(new Cell().Add(new Paragraph("N/A")));
                        }
                    }
                }

                document.Add(new Paragraph(resultName)
                    .SetFontSize(12)
                    .SetMarginTop(10));
                document.Add(dataTable);

            }
            catch (Exception ex)
            {
                LogDebug($"Error processing test result: {ex.Message}");
            }
        }

        private (int TotalTests, int PassedTests, int FailedTests, double PassRate, double FailRate)

    CalculateTestStatistics()
        {
            int total = 0, passed = 0, failed = 0;

            foreach (DataRow row in testResultsTable.Rows)
            {
                foreach (DataColumn col in testResultsTable.Columns)
                {
                    if (col.ColumnName.EndsWith("Result"))
                    {
                        total++;
                        string result = row[col].ToString();
                        if (result == "Pass") passed++;
                        else if (result == "Fail") failed++;
                    }
                }
            }

            double passRate = total > 0 ? (passed * 100.0 / total) : 0;
            double failRate = total > 0 ? (failed * 100.0 / total) : 0;

            return (total, passed, failed, passRate, failRate);
        }
        private void AddTestResultSection(Document document, KeyValuePair<(int RowIndex, int ColumnIndex), AttachedResult> result)
        {
            try
            {
                var testRun = dataGridView.Rows[result.Key.RowIndex].Cells["Test Run"].Value?.ToString() ?? "Unknown Test";
                document.Add(new Paragraph(testRun)
                    .SetFontSize(14)
                    .SetMarginTop(10));

                if (!string.IsNullOrEmpty(result.Value?.Data))
                {
                    var testData = JObject.Parse(result.Value.Data);
                    ProcessTestData(document, testData);
                }
            }
            catch (Exception ex)
            {
                LogDebug($"Error processing test result section: {ex.Message}");
            }
        }

        private void ProcessTestData(Document document, JObject testData)
        {
            try
            {
                var nestedData = JObject.Parse(testData["Data"].ToString());
                var checkedData = nestedData["CheckedData"] as JArray;

                if (checkedData != null)
                {
                    foreach (var data in checkedData)
                    {
                        if (data["Name"].ToString() == "Signal Path1")
                        {
                            var measurements = data["Measurements"] as JArray;
                            if (measurements != null)
                            {
                                Table measurementTable = new Table(3).UseAllAvailableWidth();
                                measurementTable.AddHeaderCell("Measurement");
                                measurementTable.AddHeaderCell("Value");
                                measurementTable.AddHeaderCell("Status");

                                foreach (var measurement in measurements)
                                {
                                    ProcessMeasurement(measurementTable, measurement);
                                }

                                document.Add(measurementTable);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogDebug($"Error processing test data: {ex.Message}");
            }
        }

        private void ProcessMeasurement(Table table, JToken measurement)
        {
            string measurementName = measurement["Name"].ToString();
            var results = measurement["Results"] as JArray;

            if (results != null)
            {
                foreach (var result in results)
                {
                    table.AddCell(new Cell().Add(new Paragraph(result["Name"].ToString())));

                    // Handle different types of values
                    if (result["MeterValues"] != null)
                    {
                        var meterValues = result["MeterValues"] as JArray;
                        table.AddCell(new Cell().Add(new Paragraph(
                            string.Join(", ", meterValues.Select(v => v.Value<double>().ToString("F2"))))));
                    }
                    else
                    {
                        table.AddCell(new Cell().Add(new Paragraph("See frequency response")));
                    }

                    bool passed = result["Passed"]?.Value<bool>() ?? false;
                    table.AddCell(new Cell()
                        .SetBackgroundColor(passed ? ColorConstants.LIGHT_GRAY : ColorConstants.RED)
                        .Add(new Paragraph(passed ? "Pass" : "Fail")));
                }
            }
        }

        private void ProcessMeasurements(Document document, JToken data, string variant,
    Dictionary<string, List<(string variant, double[] values, double[] frequencies)>> comparisonData)
        {
            var measurements = data["Measurements"] as JArray;
            if (measurements == null) return;

            foreach (var measurement in measurements)
            {
                string measurementName = measurement["Name"].ToString();
                LogDebug($"Processing measurement: {measurementName}");

                var results = measurement["Results"] as JArray;
                if (results != null)
                {
                    foreach (var result in results)
                    {
                        string resultName = result["Name"].ToString();
                        LogDebug($"Processing result: {resultName}");

                        // Handle frequency response data
                        var xValues = result["XValues"] as JArray;
                        var yValuesObj = result["YValuesPerChannel"] as JObject;

                        if (xValues != null && yValuesObj != null)
                        {
                            LogDebug($"Found frequency response data for {measurementName} - {resultName}");
                            var frequencies = xValues.Values<double>().ToArray();
                            LogDebug($"Frequency range: {frequencies.Min()} - {frequencies.Max()} Hz");

                            foreach (var channel in yValuesObj.Properties())
                            {
                                string channelName = $"{measurementName} - {resultName}";
                                var values = channel.Value.Values<double>().ToArray();
                                LogDebug($"Channel {channel.Name}: {values.Length} values");

                                if (!comparisonData.ContainsKey(channelName))
                                {
                                    comparisonData[channelName] = new List<(string, double[], double[])>();
                                }
                                comparisonData[channelName].Add((variant, values, frequencies));
                                LogDebug($"Added data for {variant} to {channelName}");
                            }
                        }

                        // Handle meter values
                        var meterValues = result["MeterValues"] as JArray;
                        if (meterValues != null)
                        {
                            string meterName = $"{measurementName} - {resultName}";
                            var values = meterValues.Values<double>().ToArray();
                            LogDebug($"Found meter values for {meterName}: {values.Length} values");

                            if (!comparisonData.ContainsKey(meterName))
                            {
                                comparisonData[meterName] = new List<(string, double[], double[])>();
                            }
                            comparisonData[meterName].Add((variant, values, null));
                        }
                    }
                }
            }
        }



        private void AddFrequencyResponseAnalysis(Document document)
        {
            try
            {
                // Main heading
                document.Add(new Paragraph("Frequency Response Analysis")
                    .SetFontSize(16)
                    .SetMarginTop(20));

                foreach (var result in attachedResults)
                {
                    var testRun = dataGridView.Rows[result.Key.RowIndex].Cells["Test Run"].Value?.ToString() ?? "Unknown Test";

                    if (!string.IsNullOrEmpty(result.Value?.Data))
                    {
                        var testData = JObject.Parse(result.Value.Data);
                        var nestedData = JObject.Parse(testData["Data"].ToString());
                        var checkedData = nestedData["CheckedData"] as JArray;

                        foreach (var data in checkedData)
                        {
                            if (data["Name"].ToString() == "Signal Path1")
                            {
                                foreach (var measurement in data["Measurements"])
                                {
                                    if (measurement["Name"].ToString() == "Frequency Response")
                                    {
                                        AnalyzeFrequencyResponse(document, measurement, testRun);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogDebug($"Error in frequency response analysis: {ex.Message}");
                document.Add(new Paragraph("Error analyzing frequency response data")
                    .SetFontColor(ColorConstants.RED));
            }
        }

        private void AddFinalSummary(Document document,
            (int TotalTests, int PassedTests, int FailedTests, double PassRate, double FailRate) stats)
        {
            document.Add(new Paragraph("Final Summary")
                .SetFontSize(16)
                .SetMarginTop(20));

            // Add overall assessment
            string overallStatus = stats.FailedTests == 0 ? "All tests passed" :
                $"{stats.FailedTests} test(s) failed - requires attention";

            document.Add(new Paragraph(overallStatus)
                .SetFontSize(14)
                .SetFontColor(stats.FailedTests == 0 ? ColorConstants.GREEN : ColorConstants.RED)
                .SetMarginBottom(10));
        }

        private void AddMeasurementRow(Table table, JToken measurement, string testRun = "Unknown")
        {
            string name = measurement["Name"].ToString();
            string result = measurement["Results"]?.First?["Passed"]?.Value<bool>() ?? false ? "Pass" : "Fail";

            var cell1 = new Cell().Add(new Paragraph(testRun));
            var cell2 = new Cell().Add(new Paragraph(name));
            var cell3 = new Cell().Add(new Paragraph(result));

            if (result == "Pass")
            {
                cell3.SetBackgroundColor(new DeviceRgb(200, 255, 200));
            }
            else
            {
                cell3.SetBackgroundColor(new DeviceRgb(255, 200, 200));
            }

            table.AddCell(cell1);
            table.AddCell(cell2);
            table.AddCell(cell3);
        }

        private void AddVariantDataToPlot(ScottPlot.Plot plt, IGrouping<string, KeyValuePair<(int RowIndex, int ColumnIndex), AttachedResult>> variantGroup, string measurementType)
        {
            foreach (var result in variantGroup)
            {
                try
                {
                    var testData = JObject.Parse(result.Value.Data);
                    var nestedData = JObject.Parse(testData["Data"].ToString());
                    var checkedData = nestedData["CheckedData"] as JArray;

                    if (checkedData != null)
                    {
                        foreach (var data in checkedData)
                        {
                            if (data["Name"].ToString() == "Signal Path1")
                            {
                                var measurements = data["Measurements"] as JArray;
                                if (measurements != null)
                                {
                                    foreach (var measurement in measurements)
                                    {
                                        if (measurement["Name"].ToString().Contains(measurementType))
                                        {
                                            AddMeasurementDataToPlot(plt, measurement, variantGroup.Key);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogDebug($"Error adding variant data to plot: {ex.Message}");
                }
            }
        }

        private void AddMeasurementDataToPlot(ScottPlot.Plot plt, JToken measurement, string variantName)
        {
            foreach (var result in measurement["Results"])
            {
                if (measurement["Name"].ToString().Contains("Frequency Response"))
                {
                    var xValues = result["XValues"]?.Values<double>().ToArray();
                    var yValuesObj = result["YValuesPerChannel"] as JObject;

                    if (xValues != null && yValuesObj != null)
                    {
                        foreach (var channel in yValuesObj.Properties())
                        {
                            var yValues = channel.Value.Values<double>().ToArray();
                            plt.AddScatter(xValues, yValues, label: $"{variantName} - {channel.Name}");
                        }
                    }
                }
                else
                {
                    var meterValues = result["MeterValues"]?.Values<double>().ToArray();
                    if (meterValues != null)
                    {
                        double[] positions = Enumerable.Range(1, meterValues.Length).Select(x => (double)x).ToArray();
                        //plt.AddBar(meterValues, positions, label: variantName);
                    }
                }
            }
        }

        private void AddTableRow(Table table, string label, string value)
        {
            var cell1 = new Cell().Add(new Paragraph(label));
            var cell2 = new Cell().Add(new Paragraph(value));
            table.AddCell(cell1);
            table.AddCell(cell2);
        }

        private void GeneratePdfReport(string aiResponse)
        {
            try
            {
                using (var saveFileDialog = new SaveFileDialog())
                {
                    saveFileDialog.Filter = "PDF Files|*.pdf";
                    saveFileDialog.Title = "Save Test Report";
                    saveFileDialog.FileName = $"AudioTestReport_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.pdf";

                    if (saveFileDialog.ShowDialog() == DialogResult.OK)
                    {
                        using (var writer = new PdfWriter(saveFileDialog.FileName))
                        using (var pdf = new PdfDocument(writer))
                        using (var document = new Document(pdf))
                        {
                            // Title and Header
                            document.Add(new Paragraph("Audio Analysis Report")
                                .SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER)
                                .SetFontSize(20)
                                .SetMarginBottom(20));

                            document.Add(new Paragraph($"Generated: {DateTime.Now}")
                                .SetTextAlignment(iText.Layout.Properties.TextAlignment.RIGHT)
                                .SetFontSize(10)
                                .SetMarginBottom(20));

                            // Group results by variant
                            var variantGroups = attachedResults
                                .GroupBy(r => dataGridView.Rows[r.Key.RowIndex].Cells["Test Run"].Value?.ToString() ?? "Unknown")
                                .ToList();

                            foreach (var variantGroup in variantGroups)
                            {
                                // Variant Header
                                document.Add(new Paragraph($"\nVariant: {variantGroup.Key}")
                                    .SetFontSize(16)
                                    .SetMarginBottom(10));

                                // Results table for this variant
                                AddVariantResults(document, variantGroup);

                                // Graphs for this variant
                                AddVariantGraphs(document, variantGroup);
                            }

                            // Add comparison graphs if multiple variants exist
                            if (variantGroups.Count > 1)
                            {
                                document.Add(new Paragraph("\nComparative Analysis")
                                    .SetFontSize(16)
                                    .SetMarginBottom(10));

                                AddComparisonGraphs(document, variantGroups);
                            }

                            // AI Analysis
                            if (!string.IsNullOrEmpty(aiResponse))
                            {
                                document.Add(new Paragraph("\nAI Analysis")
                                    .SetFontSize(16)
                                    .SetMarginBottom(10));

                                document.Add(new Paragraph(aiResponse)
                                    .SetFontSize(12)
                                    .SetMarginBottom(20));
                            }
                        }

                        MessageBox.Show("Report generated successfully!", "Success",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                LogDebug($"Error generating PDF: {ex.Message}");
                MessageBox.Show("Error generating PDF report. See debug log for details.",
                    "PDF Generation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void AddVariantResults(Document document, IGrouping<string, KeyValuePair<(int RowIndex, int ColumnIndex), AttachedResult>> variantGroup)
        {
            Table resultTable = new Table(3).UseAllAvailableWidth();
            resultTable.AddHeaderCell("Measurement");
            resultTable.AddHeaderCell("Value");
            resultTable.AddHeaderCell("Result");

            foreach (var result in variantGroup)
            {
                try
                {
                    var testData = JObject.Parse(result.Value.Data);
                    var nestedData = JObject.Parse(testData["Data"].ToString());
                    var checkedData = nestedData["CheckedData"] as JArray;

                    if (checkedData != null)
                    {
                        foreach (var data in checkedData)
                        {
                            if (data["Name"].ToString() == "Signal Path1")
                            {
                                var measurements = data["Measurements"] as JArray;
                                if (measurements != null)
                                {
                                    foreach (var measurement in measurements)
                                    {
                                        AddMeasurementRow(resultTable, measurement);
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogDebug($"Error processing variant results: {ex.Message}");
                }
            }

            document.Add(resultTable);
        }

        private void AddVariantGraphs(Document document, IGrouping<string, KeyValuePair<(int RowIndex, int ColumnIndex), AttachedResult>> variantGroup)
        {
            foreach (var result in variantGroup)
            {
                try
                {
                    var testData = JObject.Parse(result.Value.Data);
                    var nestedData = JObject.Parse(testData["Data"].ToString());
                    var checkedData = nestedData["CheckedData"] as JArray;

                    if (checkedData != null)
                    {
                        foreach (var data in checkedData)
                        {
                            if (data["Name"].ToString() == "Signal Path1")
                            {
                                foreach (var measurement in data["Measurements"])
                                {
                                    // Add graph for each measurement type
                                    AddMeasurementGraph(document, measurement);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogDebug($"Error generating variant graphs: {ex.Message}");
                }
            }
        }

        private void AddMeasurementGraph(Document document, JToken measurement)
        {
            try
            {
                var plt = new ScottPlot.Plot(800, 400);
                string measurementName = measurement["Name"].ToString();

                // Configure plot based on measurement type
                if (measurementName.Contains("Frequency Response"))
                {
                    ConfigureFrequencyResponsePlot(plt, measurement);
                }
                else
                {
                    ConfigureStandardPlot(plt, measurement);
                }

                // Save plot to temp file
                string tempFile = Path.Combine(Path.GetTempPath(), $"graph_{Guid.NewGuid()}.png");
                plt.SaveFig(tempFile);

                // Add to PDF
                ImageData imageData = ImageDataFactory.Create(tempFile);
                document.Add(new iText.Layout.Element.Image(imageData)
                    .SetWidth(400)
                    .SetHorizontalAlignment(iText.Layout.Properties.HorizontalAlignment.CENTER));

                // Cleanup
                File.Delete(tempFile);
            }
            catch (Exception ex)
            {
                LogDebug($"Error adding measurement graph: {ex.Message}");
            }
        }

        private void ConfigureFrequencyResponsePlot(ScottPlot.Plot plt, JToken measurement)
        {
            foreach (var result in measurement["Results"])
            {
                var xValues = result["XValues"]?.Values<double>().ToArray();
                var yValuesObj = result["YValuesPerChannel"] as JObject;

                if (xValues != null && yValuesObj != null)
                {
                    foreach (var channel in yValuesObj.Properties())
                    {
                        var yValues = channel.Value.Values<double>().ToArray();
                        plt.AddScatter(xValues, yValues, label: $"Channel {channel.Name}");
                    }
                }
            }

            plt.XLabel("Frequency (Hz)");
            plt.YLabel("Amplitude (dB)");
            plt.Title(measurement["Name"].ToString());
            //plt.XAxis.Scale = ScottPlot.Scale.Log10;
        }

        private void ConfigureStandardPlot(ScottPlot.Plot plt, JToken measurement)
        {
            foreach (var result in measurement["Results"])
            {
                var meterValues = result["MeterValues"]?.Values<double>().ToArray();
                if (meterValues != null)
                {
                    double[] positions = Enumerable.Range(1, meterValues.Length).Select(x => (double)x).ToArray();
                    var bar = plt.AddBar(meterValues, positions);
                    bar.BarWidth = 0.15;  // Make bars thinner
                }
            }

            plt.XLabel("Channel");
            plt.YLabel("Value (dB)");
            plt.Title(measurement["Name"].ToString());
        }
        private async Task HandleAristotleInput(string input)
        {
            LogDebug($"HandleAristotleInput called with input: {input}");
            if (input.ToLower().Contains("generate") && input.ToLower().Contains("report") || input.ToLower().Contains("PDF"))
            {
                LogDebug("Generating PDF report...");
                try
                {
                    GeneratePdfReport(analysisContext);
                    aristotleChatLog.SelectionFont = new Font(aristotleChatLog.Font, System.Drawing.FontStyle.Bold);
                    aristotleChatLog.AppendText("Aristotle: ");
                    aristotleChatLog.SelectionFont = new Font(aristotleChatLog.Font, System.Drawing.FontStyle.Regular);
                    aristotleChatLog.AppendText("I've generated a PDF report of the test results.\n\n");
                }
                catch (Exception ex)
                {
                    LogDebug($"Error in report generation: {ex.Message}");
                    aristotleChatLog.AppendText("Sorry, there was an error generating the report. Please check the debug log for details.\n\n");
                }
                return;
            }
            if (!string.IsNullOrWhiteSpace(input))
            {
                try
                {
                    // Show user input with bold name and spacing
                    aristotleChatLog.SelectionFont = new Font(aristotleChatLog.Font, System.Drawing.FontStyle.Bold);
                    aristotleChatLog.AppendText("You: ");
                    aristotleChatLog.SelectionFont = new Font(aristotleChatLog.Font, System.Drawing.FontStyle.Regular);
                    aristotleChatLog.AppendText($"{input}\n\n");
                    aristotleChatLog.ScrollToCaret();

                    aristotleInputBox.Clear();

                    string fullPrompt =
                        $"You are Aristotle, an AI assistant specializing in analyzing audio acoustic fitting test results. " +
                        $"If I try to deviate to a subject that is not audio acoustic fitting tests, then bring the conversation back to audio acoustic fitting tests" +
                        $"Please make your answers short and concise, and don't use the phrase 'As an AI assistent at all." +
                        $"When analyzing the data that is provided, make sure to give your analysis on it before giving suggestions to the user." +
                        $"Make sure to provide numbers corresponding to the data with your analysis." +
                        $"If I want to learn more about audio acoustic fitting and the science behind it, please provide an explanation." +
                        $"Make sure to ask if I want to know more about the results." +
                        $"Make sure to explain EVERY result in the initial analysis." +
                        $"Current context about test results:\n{analysisContext}\n\n" +
                        $"User question: {input}";

                    LogDebug("Making API call...");
                    var aristotle = new Aristotle(this);
                    string response = await CallClaudeAPI(fullPrompt);

                    if (!string.IsNullOrEmpty(response))
                    {
                        // Show Aristotle's response with bold name and spacing
                        aristotleChatLog.SelectionFont = new Font(aristotleChatLog.Font, System.Drawing.FontStyle.Bold);
                        aristotleChatLog.AppendText("Aristotle: ");
                        aristotleChatLog.SelectionFont = new Font(aristotleChatLog.Font, System.Drawing.FontStyle.Regular);
                        aristotleChatLog.AppendText($"{response}\n\n");
                        aristotleChatLog.ScrollToCaret();
                    }
                    else
                    {
                        aristotleChatLog.SelectionFont = new Font(aristotleChatLog.Font, System.Drawing.FontStyle.Bold);
                        aristotleChatLog.AppendText("Aristotle: ");
                        aristotleChatLog.SelectionFont = new Font(aristotleChatLog.Font, System.Drawing.FontStyle.Regular);
                        aristotleChatLog.AppendText("I apologize, but I encountered an error processing your request. Please try again.\n\n");
                        aristotleChatLog.ScrollToCaret();
                    }

                    if (input.ToLower().Contains("show visualization") ||
                input.ToLower().Contains("visualize") && input.ToLower().Contains("data") || input.ToLower().Contains("create a graph"))
                    {
                        if (attachedResults?.Count >= 2)
                        {
                            CreateComparisonVisualization();
                            aristotleChatLog.SelectionFont = new Font(aristotleChatLog.Font, FontStyle.Bold);
                            aristotleChatLog.AppendText("Aristotle: ");
                            aristotleChatLog.SelectionFont = new Font(aristotleChatLog.Font, FontStyle.Regular);
                            aristotleChatLog.AppendText("I've created a visualization of the test results. You can now compare the data across different variants.\n\n");
                        }
                        else
                        {
                            aristotleChatLog.SelectionFont = new Font(aristotleChatLog.Font, FontStyle.Bold);
                            aristotleChatLog.AppendText("Aristotle: ");
                            aristotleChatLog.SelectionFont = new Font(aristotleChatLog.Font, FontStyle.Regular);
                            aristotleChatLog.AppendText("I need at least two test results to create a comparison visualization. Please attach more results first.\n\n");
                        }
                        return;
                    }
                }
                catch (Exception ex)
                {
                    LogDebug($"Error in HandleAristotleInput: {ex.Message}");
                    aristotleChatLog.SelectionFont = new Font(aristotleChatLog.Font, System.Drawing.FontStyle.Bold);
                    aristotleChatLog.AppendText("Aristotle: ");
                    aristotleChatLog.SelectionFont = new Font(aristotleChatLog.Font, System.Drawing.FontStyle.Regular);
                    aristotleChatLog.AppendText($"An error occurred: {ex.Message}\n\n");
                    aristotleChatLog.ScrollToCaret();
                }
            }

        }

        private void InitializeResultsHandler()
        {
            NewTestResultAdded += (sender, resultJson) =>
            {
                try
                {
                    dynamic result = JsonConvert.DeserializeObject(resultJson);

                    string notification =
                        $"New test result detected:\n" +
                        $"Test Run: {result.TestRun}\n" +
                        $"Test Name: {result.TestName}\n" +
                        $"Status: {result.Status}\n" +
                        $"Location: {result.Location}\n";

                    this.Invoke((MethodInvoker)delegate
                    {
                        aristotleChatLog.SelectionFont = new Font(aristotleChatLog.Font, System.Drawing.FontStyle.Bold);
                        aristotleChatLog.AppendText("Aristotle: ");
                        aristotleChatLog.SelectionFont = new Font(aristotleChatLog.Font, System.Drawing.FontStyle.Regular);
                        aristotleChatLog.AppendText($"{notification}Would you like me to analyze this result for you?\n\n");
                        aristotleChatLog.ScrollToCaret();

                        //CreateComparisonVisualization();
                        UpdateAnalysisContext();
                    });
                }
                catch (Exception ex)
                {
                    LogDebug($"Error processing test result: {ex.Message}");
                }
            };
        }

        private string InterpretMeasurement(string type, double value)
        {
            // First determine which section the measurement is from
            if (type.StartsWith("Signal Path Setup"))
            {
                // Signal Path Setup measurements
                if (type.Contains("RMS Level"))
                {
                    if (value < -60)
                        return "Initial signal path level is very low, check input settings.";
                    else if (value > 0)
                        return "Initial signal path level is high, verify input settings.";
                    return "Initial signal path level is nominal.";
                }
                else if (type.Contains("Gain"))
                {
                    if (value < -3)
                        return "Input path shows significant attenuation.";
                    else if (value > 3)
                        return "Input path shows significant gain.";
                    return "Input path gain is near unity.";
                }
            }
            else if (type.StartsWith("Level and Gain"))
            {
                // Level and Gain measurements
                if (type.Contains("RMS Level"))
                {
                    if (value < -60)
                        return "Signal level is very low in processing chain.";
                    else if (value > 0)
                        return "Signal level is high in processing chain.";
                    return "Signal level is nominal in processing chain.";
                }
                else if (type.Contains("Gain"))
                {
                    if (value < -3)
                        return "Processing chain shows attenuation.";
                    else if (value > 3)
                        return "Processing chain shows amplification.";
                    return "Processing chain gain is near unity.";
                }
                else if (type.Contains("Peak Level"))
                {
                    if (value > 0)
                        return "Peak levels may cause clipping.";
                    else if (value < -40)
                        return "Peak levels are very low.";
                    return "Peak levels are within normal range.";
                }
            }
            else if (type.StartsWith("Frequency Response"))
            {
                // Frequency Response measurements
                if (type.Contains("RMS Level"))
                {
                    if (value < -60)
                        return "Frequency response test signal level is very low.";
                    else if (value > 0)
                        return "Frequency response test signal level is high.";
                    return "Frequency response test signal level is nominal.";
                }
                else if (type.Contains("Gain"))
                {
                    if (value < -3)
                        return "Frequency response shows overall attenuation.";
                    else if (value > 3)
                        return "Frequency response shows overall gain.";
                    return "Frequency response shows nominal gain.";
                }
                else if (type.Contains("Relative Level"))
                {
                    if (Math.Abs(value) > 1.0)
                        return "Reference frequency (1 kHz) shows significant level deviation.";
                    else if (Math.Abs(value) < 0.1)
                        return "Reference frequency (1 kHz) level is very stable.";
                    return "Reference frequency (1 kHz) level is acceptable.";
                }
                else if (type.Contains("Deviation"))
                {
                    if (Math.Abs(value) > 3.0)
                        return "Significant frequency response variation detected.";
                    else if (Math.Abs(value) > 1.0)
                        return "Moderate frequency response variation present.";
                    else if (Math.Abs(value) < 0.5)
                        return "Very flat frequency response.";
                    return "Normal frequency response variation.";
                }
            }
            else if (type.StartsWith("Crosstalk"))
            {
                if (value > -60)
                    return "Poor channel separation detected.";
                else if (value < -80)
                    return "Excellent channel separation.";
                return "Acceptable channel separation.";
            }

            // Default case if no specific interpretation is found
            LogDebug($"No specific interpretation for measurement type: {type}");
            return "Measurement is present but requires interpretation.";
        }

        private void CreateComparisonVisualization()
        {
            try
            {
                if (attachedResults.Count < 2)
                {
                    LogDebug("Not enough results for comparison");
                    MessageBox.Show("At least two test results are needed for comparison.",
                                  "Not Enough Data",
                                  MessageBoxButtons.OK,
                                  MessageBoxIcon.Information);
                    return;
                }

                LogDebug("Creating comparison visualization...");

                if (comparisonForm == null || comparisonForm.IsDisposed)
                {
                    comparisonForm = new FormSignalPathComparison();
                }

                var measurementData = new Dictionary<string, List<(double value, int channel, string variant)>>();

                foreach (var result in attachedResults)
                {
                    try
                    {
                        string variant = dataGridView.Rows[result.Key.RowIndex].Cells["Test Run"].Value?.ToString() ?? "Unknown";

                        if (string.IsNullOrEmpty(result.Value?.Data))
                        {
                            LogDebug($"No data found for variant {variant}");
                            continue;
                        }

                        var testData = JObject.Parse(result.Value.Data);
                        var nestedData = JObject.Parse(testData["Data"].ToString());
                        var checkedData = nestedData["CheckedData"] as JArray;

                        if (checkedData != null)
                        {
                            foreach (var data in checkedData)
                            {
                                if (data["Name"].ToString() == "Signal Path1")
                                {
                                    ProcessMeasurementsForVisualization(data, variant, measurementData);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogDebug($"Error processing result for visualization: {ex.Message}");
                    }
                }

                if (!measurementData.Any())
                {
                    LogDebug("No measurement data found to visualize");
                    MessageBox.Show("No measurement data found to visualize.",
                                  "No Data",
                                  MessageBoxButtons.OK,
                                  MessageBoxIcon.Warning);
                    return;
                }

                comparisonForm.UpdateChart(measurementData);
                comparisonForm.Show();
                comparisonForm.BringToFront();

                LogDebug("Comparison visualization created successfully");
            }
            catch (Exception ex)
            {
                LogDebug($"Error creating comparison visualization: {ex.Message}");
                MessageBox.Show($"Error creating visualization: {ex.Message}",
                              "Error",
                              MessageBoxButtons.OK,
                              MessageBoxIcon.Error);
            }
        }

        private void ProcessMeasurementsForVisualization(JToken data, string variant,
    Dictionary<string, List<(double value, int channel, string variant)>> measurementData)
        {
            var measurements = data["Measurements"] as JArray;
            if (measurements == null) return;

            foreach (var measurement in measurements)
            {
                string measurementName = measurement["Name"].ToString();
                var results = measurement["Results"] as JArray;

                if (results != null)
                {
                    foreach (var result in results)
                    {
                        string resultName = result["Name"].ToString();
                        string fullName = $"{measurementName} - {resultName}";

                        // Process regular measurements (RMS, Gain, etc.)
                        var meterValues = result["MeterValues"] as JArray;
                        if (meterValues != null)
                        {
                            if (!measurementData.ContainsKey(fullName))
                            {
                                measurementData[fullName] = new List<(double, int, string)>();
                            }

                            for (int i = 0; i < meterValues.Count; i++)
                            {
                                measurementData[fullName].Add((meterValues[i].Value<double>(), i + 1, variant));
                            }
                        }

                        // Process frequency response data
                        var xValues = result["XValues"] as JArray;
                        var yValuesObj = result["YValuesPerChannel"] as JObject;

                        if (xValues != null && yValuesObj != null)
                        {
                            if (!measurementData.ContainsKey(fullName))
                            {
                                measurementData[fullName] = new List<(double, int, string)>();
                            }

                            foreach (var channel in yValuesObj.Properties())
                            {
                                var values = channel.Value.Values<double>().ToList();
                                var frequencies = xValues.Values<double>().ToList();

                                for (int i = 0; i < values.Count; i++)
                                {
                                    measurementData[fullName].Add(
                                        (values[i],
                                        int.Parse(channel.Name.Replace("Ch", "")),
                                        variant)
                                    );
                                }
                            }
                        }
                    }
                }
            }
        }


        private void ProcessFrequencyResponseSection(JArray measurements, StringBuilder context)
        {
            try
            {
                var freqResponseSection = measurements
                    .FirstOrDefault(m => m["Name"]?.ToString() == "Frequency Response");

                if (freqResponseSection != null)
                {
                    context.AppendLine("\n=== Frequency Response Analysis ===");
                    var results = freqResponseSection["Results"] as JArray;

                    if (results != null)
                    {
                        LogDebug($"Processing {results.Count} frequency response results");

                        foreach (var result in results)
                        {
                            var measurementName = result["Name"]?.ToString();
                            LogDebug($"Processing measurement: {measurementName}");

                            // Check if this result has frequency response data
                            var xValues = result["XValues"] as JArray;
                            var yValuesPerChannel = result["YValuesPerChannel"] as JObject;

                            if (xValues != null && yValuesPerChannel != null)
                            {
                                context.AppendLine($"\n► {measurementName}:");
                                var frequencies = xValues.Values<double>().ToList();

                                foreach (var channel in yValuesPerChannel.Properties())
                                {
                                    var channelData = channel.Value.Values<double>().ToList();
                                    if (channelData.Any())
                                    {
                                        context.AppendLine($"\nChannel {channel.Name}:");
                                        var maxValue = channelData.Max();
                                        var minValue = channelData.Min();
                                        var avgValue = channelData.Average();

                                        // Find frequencies at max and min points
                                        var maxFreq = frequencies[channelData.IndexOf(maxValue)];
                                        var minFreq = frequencies[channelData.IndexOf(minValue)];

                                        context.AppendLine($"  Peak Response: {maxValue:F1} dB at {maxFreq:F1} Hz");
                                        context.AppendLine($"  Minimum Response: {minValue:F1} dB at {minFreq:F1} Hz");
                                        context.AppendLine($"  Average Response: {avgValue:F1} dB");
                                        context.AppendLine($"  Total Variation: {maxValue - minValue:F1} dB");
                                    }
                                }
                            }
                            else if (result["MeterValues"] != null)
                            {
                                // Handle simple meter value measurements (RMS Level, etc)
                                var meterValues = result["MeterValues"] as JArray;
                                if (meterValues != null)
                                {
                                    context.AppendLine($"\n► {measurementName}:");
                                    for (int i = 0; i < meterValues.Count; i++)
                                    {
                                        var value = meterValues[i].Value<double>();
                                        context.AppendLine($"  Channel {i + 1}: {value:F2} dB");
                                    }
                                }
                            }
                            else
                            {
                                LogDebug($"No measurement data found for {measurementName}");
                            }
                        }
                    }
                    else
                    {
                        context.AppendLine("No results found in Frequency Response section.");
                    }
                }
                else
                {
                    context.AppendLine("Frequency Response section not found.");
                }
            }
            catch (Exception ex)
            {
                LogDebug($"Error in ProcessFrequencyResponseSection: {ex.Message}\n{ex.StackTrace}");
                context.AppendLine($"Error processing frequency response: {ex.Message}");
            }
        }

        private void ProcessMeasurement(JToken measurement, StringBuilder context)
        {
            var measurementName = measurement["Name"]?.ToString();
            if (measurementName != null)
            {
                context.AppendLine($"\n=== {measurementName} ===");
                var results = measurement["Results"] as JArray;

                if (results != null)
                {
                    foreach (var result in results)
                    {
                        var resultName = result["Name"]?.ToString();
                        if (!string.IsNullOrEmpty(resultName))
                        {
                            ProcessResult(resultName, result, context);
                        }
                    }
                }
            }
        }

        private void ProcessResult(string resultName, JToken result, StringBuilder context)
        {
            context.AppendLine($"\n► {resultName}:");

            var meterValues = result["MeterValues"] as JArray;
            var xValues = result["XValues"] as JArray;
            var yValuesPerChannel = result["YValuesPerChannel"] as JObject;

            if (meterValues != null)
            {
                ProcessMeterValues(meterValues, context);
            }

            if (xValues != null && yValuesPerChannel != null)
            {
                ProcessFrequencyData(xValues, yValuesPerChannel, context);
            }
        }

        private void ProcessMeterValues(JArray meterValues, StringBuilder context)
        {
            for (int i = 0; i < meterValues.Count; i++)
            {
                var value = meterValues[i].ToObject<double>();
                context.AppendLine($"  Channel {i + 1}: {value:F2} dB");
            }
        }

        private void ProcessFrequencyData(JArray xValues, JObject yValuesPerChannel, StringBuilder context)
        {
            var frequencies = xValues.Select(x => x.ToObject<double>()).ToList();
            context.AppendLine($"  Frequency Range: {frequencies.Min():F1} Hz to {frequencies.Max():F1} Hz");

            foreach (var channel in yValuesPerChannel.Properties())
            {
                var channelData = (channel.Value as JArray)?.Select(v => v.ToObject<double>()).ToList();
                if (channelData != null && channelData.Any())
                {
                    context.AppendLine($"\n  Channel {channel.Name}:");
                    context.AppendLine($"    Peak: {channelData.Max():F1} dB");
                    context.AppendLine($"    Minimum: {channelData.Min():F1} dB");
                    context.AppendLine($"    Average: {channelData.Average():F1} dB");
                    context.AppendLine($"    Deviation: ±{(channelData.Max() - channelData.Min()) / 2:F1} dB");
                }
            }
        }

        private void AnalyzeFrequencyResponse(Document document, JToken measurement, string testRun)
        {
            document.Add(new Paragraph($"Analysis for {testRun}")
        .SetFontSize(14)
        .SetMarginTop(10));

            var results = measurement["Results"] as JArray;
            if (results == null) return;

            foreach (var result in results)
            {
                var xValues = result["XValues"] as JArray;
                var yValuesObj = result["YValuesPerChannel"] as JObject;

                if (xValues != null && yValuesObj != null)
                {
                    AddFrequencyResponseGraph(document, result);
                    // Create frequency bands for analysis
                    var bands = new[]
                    {
                (name: "Sub", min: 20.0, max: 60.0),
                (name: "Low", min: 60.0, max: 250.0),
                (name: "Low-Mids", min: 250.0, max: 2000.0),
                (name: "High-Mids", min: 2000.0, max: 8000.0),
                (name: "Highs", min: 8000.0, max: 20000.0)
            };

                    document.Add(new Paragraph("Frequency Band Analysis")
                        .SetFontSize(12));

                    Table bandTable = new Table(4).UseAllAvailableWidth();
                    bandTable.AddHeaderCell("Frequency Band");
                    bandTable.AddHeaderCell("Average Response");
                    bandTable.AddHeaderCell("Deviation");
                    bandTable.AddHeaderCell("Assessment");

                    var recommendations = new StringBuilder();
                    foreach (var channel in yValuesObj.Properties())
                    {
                        document.Add(new Paragraph($"Channel {channel.Name}")
                            .SetFontSize(12)
                            .SetMarginTop(10));

                        var channelData = channel.Value.ToObject<double[]>();
                        var frequencies = xValues.ToObject<double[]>();

                        foreach (var band in bands)
                        {
                            AnalyzeFrequencyBand(bandTable, recommendations, frequencies, channelData, band);
                        }
                    }

                    document.Add(bandTable);

                    // Add overall assessment
                    document.Add(new Paragraph("Analysis Summary")
                        .SetFontSize(12)
                        .SetMarginTop(10));

                    // Extract key metrics
                    double overallFlatness = CalculateOverallFlatness(xValues, yValuesObj);
                    double phaseConsistency = CalculatePhaseConsistency(xValues, yValuesObj);

                    Table summaryTable = new Table(2).UseAllAvailableWidth();
                    AddTableRow(summaryTable, "Overall Response Flatness:", $"{overallFlatness:F2} dB");
                    AddTableRow(summaryTable, "Phase Consistency:", $"{phaseConsistency:F2}°");
                    document.Add(summaryTable);

                    // Add recommendations
                    document.Add(new Paragraph("Recommendations")
                        .SetFontSize(12)
                        .SetMarginTop(10));

                    document.Add(new Paragraph(recommendations.ToString()));

                    // Add "What Not to Change" section
                    AddWhatNotToChangeSection(document, overallFlatness);
                }
            }
        }

        private void AnalyzeFrequencyBand(Table bandTable, StringBuilder recommendations,
    double[] frequencies, double[] channelData, (string name, double min, double max) band)
        {
            var bandIndices = frequencies.Select((f, i) => (f, i))
                .Where(x => x.f >= band.min && x.f <= band.max)
                .Select(x => x.i)
                .ToList();

            if (bandIndices.Any())
            {
                var bandValues = bandIndices.Select(i => channelData[i]).ToList();
                double avg = bandValues.Average();
                double deviation = bandValues.Max() - bandValues.Min();

                string assessment = AssessFrequencyBand(avg, deviation);

                bandTable.AddCell(new Cell().Add(new Paragraph(band.name)));
                bandTable.AddCell(new Cell().Add(new Paragraph($"{avg:F1} dB")));
                bandTable.AddCell(new Cell().Add(new Paragraph($"±{deviation / 2:F1} dB")));
                bandTable.AddCell(new Cell().Add(new Paragraph(assessment)));

                // Add recommendations based on assessment
                if (Math.Abs(avg) > 3 || deviation > 6)
                {
                    recommendations.AppendLine($"• {band.name}: {GetRecommendation(band.name, avg, deviation)}");
                }
            }
        }

        private string AssessFrequencyBand(double average, double deviation)
        {
            if (Math.Abs(average) <= 1.5 && deviation <= 3)
                return "Excellent";
            else if (Math.Abs(average) <= 3 && deviation <= 6)
                return "Good";
            else if (Math.Abs(average) <= 6 && deviation <= 9)
                return "Fair";
            else
                return "Needs Attention";
        }

        private string GetRecommendation(string band, double average, double deviation)
        {
            var recommendations = new List<string>();

            if (average > 3)
                recommendations.Add($"Consider reducing the {band.ToLower()} frequencies by approximately {average:F1} dB");
            else if (average < -3)
                recommendations.Add($"Consider boosting the {band.ToLower()} frequencies by approximately {Math.Abs(average):F1} dB");

            if (deviation > 6)
                recommendations.Add($"Smooth out the response variation in the {band.ToLower()} region");

            return string.Join(". ", recommendations);
        }

        private double CalculateOverallFlatness(JArray xValues, JObject yValuesObj)
        {
            var deviations = new List<double>();
            foreach (var channel in yValuesObj.Properties())
            {
                var channelData = channel.Value.ToObject<double[]>();
                deviations.Add(channelData.Max() - channelData.Min());
            }
            return deviations.Average();
        }

        private double CalculatePhaseConsistency(JArray xValues, JObject yValuesObj)
        {
            // Simplified phase calculation - could be enhanced with actual phase data
            return 0.0; // Placeholder
        }

        private void AddWhatNotToChangeSection(Document document, double overallFlatness)
        {
            document.Add(new Paragraph("Analysis")
                .SetFontSize(12)
                .SetMarginTop(10));

            var doNotChange = new StringBuilder();

            if (overallFlatness <= 3.0)
                doNotChange.AppendLine("• Overall frequency balance is good - avoid major EQ changes");

            if (overallFlatness <= 1.5)
                doNotChange.AppendLine("• Current response is excellent - maintain current settings");

            doNotChange.AppendLine("• Preserve the current crossover points if they're working well");
            doNotChange.AppendLine("• Maintain the current phase alignment if no issues are detected");

            document.Add(new Paragraph(doNotChange.ToString()));
        }

        private void UpdateAnalysisContext()
        {
            StringBuilder context = new StringBuilder();
            context.AppendLine("Signal Path Analysis:");

            LogDebug("Starting data analysis...");
            LogDebug($"Number of attached results: {attachedResults?.Count ?? 0}");

            if (attachedResults?.Any() ?? false)
            {
                foreach (var result in attachedResults)
                {
                    try
                    {
                        LogDebug($"Processing result data: {result.Value.Data.Substring(0, Math.Min(100, result.Value.Data.Length))}...");

                        // First parse the outer JSON structure
                        var testData = JObject.Parse(result.Value.Data);
                        LogDebug($"Parsed outer JSON structure: {(testData != null ? "Success" : "Failed")}");

                        // Safely get the Data property and parse it
                        var dataProperty = testData.Property("Data");
                        if (dataProperty != null && dataProperty.Value != null)
                        {
                            string dataStr = dataProperty.Value.ToString();
                            LogDebug($"Extracted data string: {(dataStr.Length > 100 ? dataStr.Substring(0, 100) + "..." : dataStr)}");

                            var nestedData = JObject.Parse(dataStr);
                            LogDebug($"Parsed nested data structure: {(nestedData != null ? "Success" : "Failed")}");

                            if (nestedData != null)
                            {
                                LogDebug("Found nested data. Looking for CheckedData...");
                                // Safely access CheckedData
                                var checkedDataProperty = nestedData.Property("CheckedData");
                                if (checkedDataProperty != null && checkedDataProperty.Value is JArray checkedData)
                                {
                                    LogDebug($"Found CheckedData with {checkedData.Count} items");

                                    foreach (JToken data in checkedData)
                                    {
                                        var name = data["Name"]?.ToString();
                                        LogDebug($"Processing data section: {name}");

                                        if (name == "Signal Path1")
                                        {
                                            context.AppendLine("\nAnalyzing Signal Path Measurements:");
                                            var measurements = data["Measurements"] as JArray;

                                            if (measurements != null)
                                            {
                                                LogDebug($"Found {measurements.Count} measurements");

                                                // First look for Frequency Response section
                                                ProcessFrequencyResponseSection(measurements, context);

                                                // Then process other measurements
                                                foreach (var measurement in measurements)
                                                {
                                                    string measurementName = measurement["Name"]?.ToString();
                                                    if (measurementName != "Frequency Response")
                                                    {
                                                        ProcessMeasurement(measurement, context);
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                LogDebug("No measurements found in Signal Path1");
                                                context.AppendLine("\nNo measurement data found.");
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    LogDebug("No CheckedData found in nested data");
                                    context.AppendLine("\nNo measurement data found in the test results.");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogDebug($"Error parsing test result data: {ex.Message}");
                        context.AppendLine($"Error analyzing test data: {ex.Message}");
                    }
                }
            }
            else
            {
                context.AppendLine("\nNo test results attached.");
            }

            analysisContext = context.ToString();
            LogDebug("Updated analysis context with detailed measurements");
        }

        private string DetermineUnit(string testName)
        {
            if (testName.Contains("THD")) return "%";
            if (testName.Contains("Phase")) return "°";
            if (testName.Contains("Impedance")) return "Ω";
            if (testName.Contains("Level") || testName.Contains("SNR")) return "dB";
            if (testName.Contains("Frequency")) return "Hz";
            return "";
        }
        public void InitializeAristotleComponents(GroupBox aristotleTabPage)
        {
            // Create the main container that will hold all components
            var mainContainer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,  // We'll use 2 rows: one for chat, one for input
                BackColor = System.Drawing.Color.FromArgb(45, 45, 45),
                Padding = new Padding(10),
            };

            // Set up row styles - this is crucial for proper spacing
            mainContainer.RowStyles.Add(new RowStyle(SizeType.Percent, 85F));  // Chat area gets 85% of space
            mainContainer.RowStyles.Add(new RowStyle(SizeType.Percent, 15F));  // Input area gets 15% of space

            // Create and configure the chat log
            var chatLog = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = System.Drawing.Color.FromArgb(60, 60, 60),
                ForeColor = System.Drawing.Color.White,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 10),
                Margin = new Padding(0, 0, 0, 10)  // Add some space between chat and input
            };

            // Create a panel for the input area
            var inputPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Height = 40,
                BackColor = System.Drawing.Color.FromArgb(45, 45, 45)
            };

            // Create and configure the input textbox
            var inputTextBox = new TextBox
            {
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
                Height = 30,
                BackColor = System.Drawing.Color.FromArgb(60, 60, 60),
                ForeColor = System.Drawing.Color.White,
                Font = new Font("Segoe UI", 10)
            };

            // Create and configure the send button
            var submitButton = new Button
            {
                Text = "Send",
                Width = 80,
                Height = 30,
                Anchor = AnchorStyles.Right | AnchorStyles.Top,
                BackColor = System.Drawing.Color.FromArgb(85, 160, 140),
                ForeColor = System.Drawing.Color.White,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 },
                TextAlign = ContentAlignment.MiddleCenter,
                Padding = new Padding(0)
            };

            // Position the input components within the input panel
            inputTextBox.SetBounds(0, 0, inputPanel.Width - submitButton.Width - 10, inputTextBox.Height);
            submitButton.SetBounds(inputPanel.Width - submitButton.Width, 0, submitButton.Width, submitButton.Height);

            // Add controls to their respective containers
            inputPanel.Controls.Add(inputTextBox);
            inputPanel.Controls.Add(submitButton);

            mainContainer.Controls.Add(chatLog, 0, 0);
            mainContainer.Controls.Add(inputPanel, 0, 1);

            // Add the main container to the frame
            aristotleFrame.Controls.Add(mainContainer);

            // Handle input panel resizing
            inputPanel.Resize += (s, e) =>
            {
                // Update positions when panel is resized
                inputTextBox.Width = inputPanel.Width - submitButton.Width - 10;
                submitButton.Left = inputPanel.Width - submitButton.Width;
            };

            // Store references to our controls
            aristotleChatLog = chatLog;
            aristotleInputBox = inputTextBox;
            aristotleSubmitButton = submitButton;

            LogDebug("Setting up Aristotle input handlers");

            // Wire up the enter key event with debug logging
            inputTextBox.KeyDown += async (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    LogDebug($"Enter key pressed with text: {inputTextBox.Text}");
                    e.SuppressKeyPress = true;
                    if (!string.IsNullOrWhiteSpace(inputTextBox.Text))
                    {
                        await HandleAristotleInput(inputTextBox.Text);
                    }
                }
            };

            // Wire up the submit button with debug logging
            submitButton.Click += async (s, e) =>
            {
                LogDebug($"Submit button clicked with text: {inputTextBox.Text}");
                if (!string.IsNullOrWhiteSpace(inputTextBox.Text))
                {
                    await HandleAristotleInput(inputTextBox.Text);
                }
            };

            // Initialize the test results handler
            InitializeResultsHandler();

            // Show a simple welcome message without making an API call
            DisplayWelcomeMessage();

            LogDebug("Aristotle components initialization complete");
        }

        private void DisplayWelcomeMessage()
        {

            bool hasAttachedResults = attachedResults.Any();

            string introMessage = hasAttachedResults
                ? "I see that you have some test results loaded. I can help analyze them for you. Would you like me to look for any patterns or potential issues in your test data?"
                : "Hello, I'm Aristotle, an AI designed to help you analyze audio testing, and help you with any general questions. Once you attach some test data, I can help identify patterns, anomalies, or potential issues. I can also generate PDF reports and compare the test results visually.";

            aristotleChatLog.SelectionFont = new Font(aristotleChatLog.Font, System.Drawing.FontStyle.Bold);
            aristotleChatLog.AppendText("Aristotle: ");
            aristotleChatLog.SelectionFont = new Font(aristotleChatLog.Font, System.Drawing.FontStyle.Regular);
            aristotleChatLog.AppendText($"{introMessage}\n\n");
            aristotleChatLog.ScrollToCaret();

        }


        private void InitializeDynamicInput()
        {

            LogDebug("Initializing dynamic input...");

            unitNames = new List<string>();
            propertyNames = new List<string>();
            propertyVariations = new List<List<string>>();
            testNames = new List<string>();
            testResultsStatus = new Dictionary<string, string>();

            this.Text = "Test Results Grid";
            this.Font = new Font("Segoe UI", 8); // Smaller font size
            this.BackColor = System.Drawing.Color.FromArgb(45, 45, 45);
            this.Size = new Size(1200, 800);
            this.StartPosition = FormStartPosition.CenterScreen;


            mainPanel = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = System.Windows.Forms.Orientation.Vertical,
                SplitterDistance = 225,
                BackColor = System.Drawing.Color.FromArgb(45, 45, 45)
            };

            Controls.Add(mainPanel);

            // Configure Test Parameters Panel
            var leftPanelFrame = new GroupBox
            {
                Text = "Configure Test Parameters",
                ForeColor = System.Drawing.Color.White,
                Dock = DockStyle.Fill,
                Padding = new Padding(8), // Smaller padding
                Font = new Font(this.Font.FontFamily, 8), // Smaller font size
                BackColor = System.Drawing.Color.FromArgb(45, 45, 45)
            };
            mainPanel.Panel1.Controls.Add(leftPanelFrame);

            var leftPanelLayout = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = System.Drawing.Color.FromArgb(45, 45, 45)
            };
            leftPanelFrame.Controls.Add(leftPanelLayout);

            // Unit GroupBox
            unitGroupBox = CreateGroupBox("Unit", out unitTextBox, out unitListBox, AddUnit_Click);
            leftPanelLayout.Controls.Add(unitGroupBox);

            // Property GroupBox
            propertyGroupBox = CreatePropertyGroupBox(out propertyTextBox, out propertyListBox, out propertyVariantsTextBox, out propertyVariantListBox);
            leftPanelLayout.Controls.Add(propertyGroupBox);

            // Test GroupBox
            testGroupBox = CreateTestGroupBox(); // Updated to call the method without out parameter
            leftPanelLayout.Controls.Add(testGroupBox);

            // Results Matrix Panel
            var rightPanelSplitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = System.Windows.Forms.Orientation.Horizontal,
                BackColor = System.Drawing.Color.FromArgb(45, 45, 45)
            };

            // Add the results matrix to the top panel
            var resultsMatrixFrame = new GroupBox
            {
                Text = "Results Matrix",
                ForeColor = System.Drawing.Color.White,
                Dock = DockStyle.Fill,
                Padding = new Padding(8),
                Font = new Font(this.Font.FontFamily, 8),
                BackColor = System.Drawing.Color.FromArgb(45, 45, 45)
            };

            InitializeDataGridView(resultsMatrixFrame.Controls);
            InitializeDonutChart(resultsMatrixFrame.Controls);
            rightPanelSplitContainer.Panel1.Controls.Add(resultsMatrixFrame);

            // Add Aristotle's interface to the bottom panel
            aristotleFrame = new GroupBox
            {
                Text = "Aristotle Analysis",
                ForeColor = System.Drawing.Color.White,
                Dock = DockStyle.Fill,
                Padding = new Padding(8),
                Font = new Font(this.Font.FontFamily, 8),
                BackColor = System.Drawing.Color.FromArgb(45, 45, 45)
            };

            InitializeAristotleComponents(aristotleFrame);
            rightPanelSplitContainer.Panel2.Controls.Add(aristotleFrame);
            //ShowIntroduction();
            // Set the initial split position (70% for results, 30% for Aristotle)
            rightPanelSplitContainer.SplitterDistance = (int)(rightPanelSplitContainer.Height * 0.7);

            // Add the split container to the main panel
            mainPanel.Panel2.Controls.Add(rightPanelSplitContainer);

            LogDebug("Initialization complete.");
        }

        private void TestSelectionComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            addButton.Enabled = testSelectionComboBox.SelectedIndex != -1;
        }



        private GroupBox CreateTestGroupBox()
        {
            var groupBox = new GroupBox
            {
                Text = "Test",
                ForeColor = System.Drawing.Color.White,
                Dock = DockStyle.Top,
                Padding = new Padding(4), // Smaller padding
                Font = new Font(this.Font.FontFamily, 8), // Smaller font size
                BackColor = System.Drawing.Color.FromArgb(45, 45, 45),
                Margin = new Padding(4), // Smaller margin
                AutoSize = true,
                Width = 250 // Set a fixed width for the group box
            };

            // Initialize the custom test name TextBox
            customTestNameTextBox = new TextBox
            {
                Width = 200,
                BackColor = System.Drawing.Color.FromArgb(60, 60, 60),
                ForeColor = System.Drawing.Color.White,
                Margin = new Padding(0, 10, 0, 10) // Add some margin to create spacing
            };

            // Initialize the Add button
            addButton = new Button
            {
                Text = "Add Test",
                Width = 70, // Smaller width
                Height = 30, // Adjusted height for better visibility
                BackColor = System.Drawing.Color.FromArgb(85, 160, 140),
                ForeColor = System.Drawing.Color.White,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 },
                Margin = new Padding(0, 10, 0, 0), // Add margin to ensure spacing below the textbox
                Enabled = false // Initially disabled until a test name is entered
            };
            ApplyRoundedCorners(addButton);
            addButton.Click += (sender, e) => AddCustomTestName();

            // Create a ListBox for added tests
            addedTestsListBox = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = System.Drawing.Color.FromArgb(60, 60, 60),
                ForeColor = System.Drawing.Color.White
            };

            // Add a context menu for removing tests
            var contextMenu = new ContextMenuStrip();
            var removeItem = new ToolStripMenuItem("Remove", null, RemoveSelectedTest_Click);
            contextMenu.Items.Add(removeItem);
            addedTestsListBox.ContextMenuStrip = contextMenu;

            // Create a TableLayoutPanel to hold the controls vertically
            var tableLayoutPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                RowCount = 3,
                ColumnCount = 1
            };

            tableLayoutPanel.Controls.Add(customTestNameTextBox, 0, 0);
            tableLayoutPanel.Controls.Add(addButton, 0, 1);
            tableLayoutPanel.Controls.Add(addedTestsListBox, 0, 2); // Add the added tests ListBox

            // Add the TableLayoutPanel to the group box
            groupBox.Controls.Add(tableLayoutPanel);

            // Enable the Add button when text is entered in the custom test name TextBox
            customTestNameTextBox.TextChanged += (sender, e) =>
            {
                addButton.Enabled = !string.IsNullOrWhiteSpace(customTestNameTextBox.Text);
            };

            return groupBox;
        }

        // Add a test name based on user input
        private void AddCustomTestName()
        {
            string userInput = customTestNameTextBox.Text.Trim();
            if (!string.IsNullOrWhiteSpace(userInput))
            {
                string testName = $"{userInput} - Audio Precision 8.0";
                addedTestsListBox.Items.Add(testName);
                customTestNameTextBox.Clear(); // Clear the TextBox after adding
            }
        }

        private void InitializeDataGridView(Control.ControlCollection parentControls)
        {
            dataGridView = new DataGridView
            {
                Dock = DockStyle.Top,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AllowUserToAddRows = false,
                BackgroundColor = System.Drawing.Color.FromArgb(45, 45, 45),
                ForeColor = System.Drawing.Color.White,
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = System.Drawing.Color.FromArgb(75, 75, 75), ForeColor = System.Drawing.Color.White },
                RowHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = System.Drawing.Color.FromArgb(75, 75, 75), ForeColor = System.Drawing.Color.White },
                DefaultCellStyle = new DataGridViewCellStyle { BackColor = System.Drawing.Color.FromArgb(60, 60, 60), ForeColor = System.Drawing.Color.White },
                EnableHeadersVisualStyles = false,
                Height = 400 // Adjust based on the available space
            };

            parentControls.Add(dataGridView);
            dataGridView.CellClick += DataGridView_CellClick;
            dataGridView.CellPainting += DataGridView_CellPainting;
            dataGridView.CellMouseDown += DataGridView_CellMouseDown; // Add this line

            // Prevent cell text editing
            dataGridView.CellBeginEdit += DataGridView_CellBeginEdit;

            // Initialize context menu
            var contextMenu = new ContextMenuStrip();
            var attachTestResultsMenuItem = new ToolStripMenuItem("Attach Test Results", null, AttachTestResultsMenuItem_Click);
            var viewResultsMenuItem = new ToolStripMenuItem("View Attached Results", null, ViewResultsMenuItem_Click);
            contextMenu.Items.Add(attachTestResultsMenuItem);
            contextMenu.Items.Add(viewResultsMenuItem); // Add View Results option
            dataGridView.ContextMenuStrip = contextMenu;
        }

        private void DataGridView_CellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                dataGridView.ClearSelection();
                dataGridView.Rows[e.RowIndex].Cells[e.ColumnIndex].Selected = true;
            }
        }

        public void AttachTestResult(int rowIndex, int columnIndex, string status, string data, string fileName)
        {
            LogDebug($"Attaching test result at row {rowIndex}, column {columnIndex}...");

            var key = (RowIndex: rowIndex, ColumnIndex: columnIndex);

            if (attachedResults == null)
            {
                LogDebug("Error: 'attachedResults' is null. Initializing 'attachedResults'...");
                attachedResults = new Dictionary<(int RowIndex, int ColumnIndex), AttachedResult>();
            }

            LogDebug($"Status: {status}, Data: {data.Substring(0, Math.Min(data.Length, 500))}...");

            attachedResults[key] = new AttachedResult { Status = status, Data = data, FileName = fileName };

            // Create result context
            var resultContext = new
            {
                TestRun = dataGridView.Rows[rowIndex].Cells["Test Run"].Value?.ToString() ?? "Unknown Test Run",
                TestName = dataGridView.Columns[columnIndex].HeaderText,
                Status = status,
                FileName = fileName,
                Location = $"Row {rowIndex + 1}, Column {columnIndex + 1}"
            };


            // Convert to JSON and trigger the event
            string jsonContext = JsonConvert.SerializeObject(resultContext);
            NewTestResultAdded?.Invoke(null, jsonContext);
            /* if (attachedResults.Count > 1)
             {
                 CreateComparisonVisualization();
             }*/

            LogDebug($"Test result attached to row {rowIndex}, column {columnIndex} successfully.");
        }

        private void SendTestResultToAristotle(int rowIndex, int columnIndex, string status, string data, string fileName)
        {
            // Get test run information from the DataGridView
            string testRun = dataGridView.Rows[rowIndex].Cells["Test Run"].Value?.ToString() ?? "Unknown Test Run";
            string testName = dataGridView.Columns[columnIndex].HeaderText;

            // Create result context
            var resultContext = new
            {
                TestRun = testRun,
                TestName = testName,
                Status = status,
                FileName = fileName,
                Data = data,
                Location = $"Row {rowIndex + 1}, Column {columnIndex + 1}"
            };

            // Convert to JSON and trigger the event
            string jsonContext = JsonConvert.SerializeObject(resultContext);
            NewTestResultAdded?.Invoke(null, jsonContext);
        }

        private void AttachTestResultsMenuItem_Click(object sender, EventArgs e)
        {
            var selectedCell = dataGridView.SelectedCells[0];
            var resultStatus = selectedCell.Value?.ToString();
            var rowIndex = selectedCell.RowIndex;
            var columnIndex = selectedCell.ColumnIndex;

            LogDebug("Opening FormAttachTestResults...");

            // Open the FormAttachTestResults form
            var attachForm = new FormAttachTestResults(rowIndex, columnIndex, resultStatus, this);
            attachForm.ShowDialog();

            // Access the properties after the dialog is closed
            if (!string.IsNullOrEmpty(attachForm.SelectedFileName) && !string.IsNullOrEmpty(attachForm.DecryptedData))
            {
                // Attach the result with the file name
                AttachTestResult(rowIndex, columnIndex, resultStatus, attachForm.DecryptedData, attachForm.SelectedFileName);
            }
            else
            {
                LogDebug("No file was selected or attached.");
            }
        }

        public class FormAttachTestResults : Form
        {
            private int rowIndex;
            private int columnIndex;
            private string resultStatus;
            private TestResultsGrid parentForm;
            private ComboBox lycFileComboBox;
            private TextBox searchTextBox; // Add this for the search bar
            private List<string> allLycFiles; // Store all .lyc files for filtering

            // Add these properties
            public string DecryptedData { get; private set; }
            public string SelectedFileName { get; private set; }

            public FormAttachTestResults(int rowIndex, int columnIndex, string resultStatus, TestResultsGrid parentForm)
            {
                this.rowIndex = rowIndex;
                this.columnIndex = columnIndex;
                this.resultStatus = resultStatus;
                this.parentForm = parentForm;

                InitializeComponent();
                LoadLycFiles();
            }

            private void InitializeComponent()
            {
                // Set up the form properties
                this.Text = "Attach Test Results";
                this.Size = new Size(400, 300);
                this.BackColor = System.Drawing.Color.FromArgb(45, 45, 45); // Dark background color
                this.Font = new Font("Segoe UI", 8); // Smaller font size, consistent with the main application

                // Create a FlowLayoutPanel for better layout control
                var layoutPanel = new FlowLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    FlowDirection = FlowDirection.TopDown,
                    Padding = new Padding(10),
                    AutoSize = true,
                    BackColor = System.Drawing.Color.FromArgb(45, 45, 45) // Match the background color of the form
                };
                this.Controls.Add(layoutPanel);

                var searchLabel = new System.Windows.Forms.Label
                {
                    Text = "Search:",
                    ForeColor = System.Drawing.Color.White, // White text color for visibility in dark mode
                    AutoSize = true,
                    Margin = new Padding(0, 0, 0, 10), // Add margin to separate the label from the search bar
                    Font = new Font("Segoe UI", 8) // Set font to match the main application
                };
                layoutPanel.Controls.Add(searchLabel);

                searchTextBox = new TextBox
                {
                    Width = 350, // Adjust width as necessary
                    ForeColor = System.Drawing.Color.White,
                    BackColor = System.Drawing.Color.FromArgb(60, 60, 60), // Darker background for textbox
                    Font = new Font("Segoe UI", 8) // Set font to match the main application
                };
                searchTextBox.TextChanged += SearchTextBox_TextChanged; // Add event handler for text change
                layoutPanel.Controls.Add(searchTextBox);

                var selectLabel = new System.Windows.Forms.Label
                {
                    Text = "Select Test Result File:",
                    ForeColor = System.Drawing.Color.White, // White text color for visibility in dark mode
                    AutoSize = true,
                    Margin = new Padding(0, 10, 0, 10), // Add margin to separate the label from the dropdown
                    Font = new Font("Segoe UI", 8) // Set font to match the main application
                };
                layoutPanel.Controls.Add(selectLabel);

                lycFileComboBox = new ComboBox
                {
                    Width = 350, // Adjust width as necessary
                    ForeColor = System.Drawing.Color.White,
                    BackColor = System.Drawing.Color.FromArgb(60, 60, 60), // Darker background for dropdown
                    Font = new Font("Segoe UI", 8) // Set font to match the main application
                };
                layoutPanel.Controls.Add(lycFileComboBox);

                var attachButton = new Button
                {
                    Text = "Attach",
                    Width = 100,
                    Height = 30,
                    ForeColor = System.Drawing.Color.White,
                    BackColor = System.Drawing.Color.FromArgb(85, 160, 140),
                    FlatStyle = FlatStyle.Flat,
                    Margin = new Padding(0, 20, 0, 0), // Add margin to separate the button from the dropdown
                    Font = new Font("Segoe UI", 8) // Set font to match the main application
                };
                attachButton.FlatAppearance.BorderSize = 0; // Remove border
                attachButton.Click += AttachButton_Click;
                layoutPanel.Controls.Add(attachButton);
            }

            private void LoadLycFiles()
            {
                var lyceumDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Lyceum");
                allLycFiles = new List<string>(); // Initialize the list

                if (Directory.Exists(lyceumDir))
                {
                    parentForm.LogDebug("Loading .lyc files from: " + lyceumDir);
                    var lycFiles = Directory.GetFiles(lyceumDir, "*.lyc");
                    foreach (var file in lycFiles)
                    {
                        parentForm.LogDebug("Found .lyc file: " + file);
                        allLycFiles.Add(Path.GetFileName(file)); // Add to the full list
                    }

                    // Initially populate the ComboBox with all files
                    UpdateComboBoxItems(allLycFiles);
                }
                else
                {
                    parentForm.LogDebug("Lyceum directory does not exist.");
                }
            }

            private void UpdateComboBoxItems(List<string> items)
            {
                lycFileComboBox.Items.Clear();
                lycFileComboBox.Items.AddRange(items.ToArray());
            }

            private void SearchTextBox_TextChanged(object sender, EventArgs e)
            {
                string searchText = searchTextBox.Text.ToLower();
                var filteredItems = allLycFiles.Where(f => f.ToLower().Contains(searchText)).ToList();
                UpdateComboBoxItems(filteredItems);
            }

            private void AttachButton_Click(object sender, EventArgs e)
            {
                var formSessionManager = new FormSessionManager(null, null, SessionMode.View, null, null, null, null);

                if (string.IsNullOrEmpty(parentForm.SystemKey))
                {
                    parentForm.SystemKey = formSessionManager.GetOrCreateEncryptionKey(parentForm.GetLogTextBox());
                    parentForm.LogDebug($"SystemKey initialized: {(parentForm.SystemKey != null ? "Success" : "Failed")}");
                }

                if (lycFileComboBox.SelectedItem != null)
                {
                    SelectedFileName = lycFileComboBox.SelectedItem.ToString();
                    var lyceumDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Lyceum");
                    var filePath = Path.Combine(lyceumDir, SelectedFileName);

                    parentForm.LogDebug("Selected file: " + filePath);
                    parentForm.LogDebug("Attempting to decrypt file...");

                    if (string.IsNullOrEmpty(parentForm.SystemKey))
                    {
                        parentForm.LogDebug("SystemKey is null or empty. Cannot decrypt.");
                        MessageBox.Show("SystemKey is not set. Decryption cannot proceed.", "Decryption Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    // Validate and decrypt the file
                    DecryptedData = FormSessionManager.DecryptString(parentForm.SystemKey, File.ReadAllText(filePath), parentForm.GetLogTextBox());

                    if (DecryptedData != null)
                    {
                        parentForm.LogDebug("File decrypted successfully.");
                        this.Close();
                    }
                    else
                    {
                        parentForm.LogDebug("Failed to decrypt the file.");
                        MessageBox.Show("Failed to decrypt the file.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void ViewResultsMenuItem_Click(object sender, EventArgs e)
        {
            LogDebug("ViewResultsMenuItem_Click invoked.");

            if (attachedResults == null)
            {
                LogDebug("Error: attachedResults is null.");
                MessageBox.Show("Attached results data is not initialized.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (dataGridView.SelectedCells.Count > 0)
            {
                var selectedCell = dataGridView.SelectedCells[0];
                var rowIndex = selectedCell.RowIndex;
                var columnIndex = selectedCell.ColumnIndex;

                LogDebug($"Selected cell at row {rowIndex}, column {columnIndex}.");

                var selectedKey = (RowIndex: rowIndex, ColumnIndex: columnIndex);

                if (attachedResults.TryGetValue(selectedKey, out AttachedResult attachedResult))
                {
                    LogDebug($"Found attached result for key: {selectedKey}.");

                    try
                    {
                        // Use the stored file name to construct the file path
                        string directoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Lyceum");
                        string filePath = Path.Combine(directoryPath, attachedResult.FileName);

                        // Log the file path and attached result data
                        LogDebug($"Loading file: {filePath}");
                        LogDebug($"Attached result data: {attachedResult.Data.Substring(0, Math.Min(attachedResult.Data.Length, 500))}...");

                        // Instantiate the FormGridViewResults with the correct parameters
                        var viewResultsForm = new FormGridViewResults(filePath, this.SystemKey);

                        // Show the form non-modally to allow interaction with the parent form
                        viewResultsForm.Show();
                    }
                    catch (Exception ex)
                    {
                        LogDebug($"An unexpected error occurred: {ex.Message}");
                        MessageBox.Show("An unexpected error occurred while loading the result data.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                else
                {
                    LogDebug($"No attached result found for key: {selectedKey}.");
                    MessageBox.Show("No attached result found for the selected cell.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                LogDebug("No cell selected.");
                MessageBox.Show("Please select a cell with attached results to view.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        public TextBox GetLogTextBox()
        {
            return debugTextBox;
        }

        private void InitializeDonutChart(Control.ControlCollection parentControls)
        {
            resultsChart = new Chart
            {
                Dock = DockStyle.Bottom,
                Height = 300, // Set the height for the chart
                BackColor = System.Drawing.Color.FromArgb(45, 45, 45)
            };

            var chartArea = new ChartArea
            {
                BackColor = System.Drawing.Color.FromArgb(45, 45, 45),
                Area3DStyle = { Enable3D = true, Inclination = 60 }
            };
            resultsChart.ChartAreas.Add(chartArea);

            var series = new Series
            {
                ChartType = SeriesChartType.Doughnut,
                BackSecondaryColor = System.Drawing.Color.FromArgb(60, 60, 60),
                BorderColor = System.Drawing.Color.FromArgb(60, 60, 60),
                BorderWidth = 2,
                ShadowColor = System.Drawing.Color.Black,
                ShadowOffset = 2,
                IsValueShownAsLabel = true,
                Font = new Font("Segoe UI", 10, System.Drawing.FontStyle.Bold),
                LabelForeColor = System.Drawing.Color.White
            };
            resultsChart.Series.Add(series);

            // Add a title to the chart
            var chartTitle = new Title
            {
                Text = "Test Results Summary",
                Font = new Font("Segoe UI", 12, System.Drawing.FontStyle.Bold),
                ForeColor = System.Drawing.Color.White,
                Docking = Docking.Top, // Ensure the title is at the top
                Alignment = ContentAlignment.TopCenter
            };
            resultsChart.Titles.Add(chartTitle);

            // Add a legend to the chart, positioned below the title
            var legend = new System.Windows.Forms.DataVisualization.Charting.Legend
            {
                Docking = Docking.Top,
                Alignment = StringAlignment.Center,
                Font = new Font("Segoe UI", 10),
                ForeColor = System.Drawing.Color.White,
                BackColor = System.Drawing.Color.FromArgb(45, 45, 45)
            };
            resultsChart.Legends.Add(legend);

            parentControls.Add(resultsChart);
            UpdateDonutChart(); // Initial update to populate the chart
        }

        private void UpdateDonutChart()
        {
            int passCount = 0;
            int failCount = 0;
            int infoOnlyCount = 0;
            int noResultCount = 0;

            foreach (DataGridViewRow row in dataGridView.Rows)
            {
                foreach (DataGridViewCell cell in row.Cells)
                {
                    if (cell.Value != null && dataGridView.Columns[cell.ColumnIndex].Name.EndsWith("Result"))
                    {
                        switch (cell.Value.ToString())
                        {
                            case "Pass":
                                passCount++;
                                break;
                            case "Fail":
                                failCount++;
                                break;
                            case "Info Only":
                                infoOnlyCount++;
                                break;
                            case "No Result":
                                noResultCount++;
                                break;
                        }
                    }
                }
            }

            resultsChart.Series[0].Points.Clear();
            resultsChart.Series[0].Points.AddXY("Pass", passCount);
            resultsChart.Series[0].Points[0].Color = System.Drawing.Color.Green;
            resultsChart.Series[0].Points.AddXY("Fail", failCount);
            resultsChart.Series[0].Points[1].Color = System.Drawing.Color.Red;
            resultsChart.Series[0].Points.AddXY("Info Only", infoOnlyCount);
            resultsChart.Series[0].Points[2].Color = System.Drawing.Color.Yellow;
            resultsChart.Series[0].Points.AddXY("No Result", noResultCount);
            resultsChart.Series[0].Points[3].Color = System.Drawing.Color.Gray;

            resultsChart.Series[0].LegendText = "#VALX: #PERCENT{P0}"; // Show category and percentage
            resultsChart.Series[0].Label = "#PERCENT{P0}"; // Show only percentage on the chart

            resultsChart.Invalidate(); // Redraw the chart
        }

        private void DataGridView_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            // Cancel any edit attempt
            e.Cancel = true;
        }

        private GroupBox CreateGroupBox(string title, out TextBox inputTextBox, out ListBox listBox, EventHandler addButtonClick)
        {
            inputTextBox = new TextBox(); // Initialize out parameter
            listBox = new ListBox();       // Initialize out parameter

            var groupBox = new GroupBox
            {
                Text = $"{title} Name:",
                ForeColor = System.Drawing.Color.White,
                Dock = DockStyle.Top,
                Padding = new Padding(4), // Smaller padding
                Font = new Font(this.Font.FontFamily, 8), // Smaller font size
                BackColor = System.Drawing.Color.FromArgb(45, 45, 45),
                Margin = new Padding(4), // Smaller margin
                AutoSize = true,
                Width = 250 // Set a fixed width for the group box
            };

            var toggleButton = new Button
            {
                Text = "-",
                Dock = DockStyle.Top,
                Width = 20,
                Height = 20,
                BackColor = System.Drawing.Color.FromArgb(85, 160, 140),
                ForeColor = System.Drawing.Color.White,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 }
            };
            ApplyRoundedCorners(toggleButton);

            var contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoSize = true
            };

            var tableLayoutPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 4,
                AutoSize = true,
                Padding = new Padding(2) // Even smaller padding
            };
            tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));
            tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65F));
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 18F)); // Smaller row height
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 25F)); // Smaller row height
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 80F)); // Smaller row height for list box
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var inputLabel = new System.Windows.Forms.Label
            {
                Text = $"{title} Name:",
                ForeColor = System.Drawing.Color.White,
                Anchor = AnchorStyles.Left,
                AutoSize = true
            };
            tableLayoutPanel.Controls.Add(inputLabel, 0, 0);

            inputTextBox.Margin = new Padding(2); // Even smaller margin
            inputTextBox.Width = 120; // Set a narrower width for the input box
            tableLayoutPanel.Controls.Add(inputTextBox, 1, 0);

            var addButton = new Button
            {
                Text = $"Add {title}",
                Width = 70, // Smaller width
                Height = 20, // Smaller height
                BackColor = System.Drawing.Color.FromArgb(85, 160, 140),
                ForeColor = System.Drawing.Color.White,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 },
                Margin = new Padding(2),
                Anchor = AnchorStyles.Left
            };
            ApplyRoundedCorners(addButton);
            addButton.Click += addButtonClick;
            tableLayoutPanel.Controls.Add(addButton, 1, 1);

            listBox.Dock = DockStyle.Fill;
            listBox.Height = 80; // Smaller height for list box
            listBox.BackColor = System.Drawing.Color.FromArgb(60, 60, 60);
            listBox.ForeColor = System.Drawing.Color.White;
            listBox.Margin = new Padding(2); // Even smaller margin
            listBox.ContextMenuStrip = CreateContextMenuStrip(listBox, title); // Add removal option
            tableLayoutPanel.Controls.Add(listBox, 0, 2);
            tableLayoutPanel.SetColumnSpan(listBox, 2);

            contentPanel.Controls.Add(tableLayoutPanel);
            groupBox.Controls.Add(contentPanel);
            groupBox.Controls.Add(toggleButton);
            groupBox.Tag = contentPanel;

            toggleButton.Click += (sender, e) =>
            {
                contentPanel.Visible = !contentPanel.Visible;
                toggleButton.Text = contentPanel.Visible ? "-" : "+";
            };

            return groupBox;
        }

        private GroupBox CreatePropertyGroupBox(out TextBox propertyNameTextBox, out ListBox propertyListBox, out TextBox variantsTextBox, out ListBox variantListBox)
        {
            propertyNameTextBox = new TextBox(); // Initialize out parameter
            propertyListBox = new ListBox();     // Initialize out parameter
            variantsTextBox = new TextBox();     // Initialize out parameter
            variantListBox = new ListBox();      // Initialize out parameter

            var groupBox = new GroupBox
            {
                Text = "Properties",
                ForeColor = System.Drawing.Color.White,
                Dock = DockStyle.Top,
                Padding = new Padding(4), // Smaller padding
                Font = new Font(this.Font.FontFamily, 8), // Smaller font size
                BackColor = System.Drawing.Color.FromArgb(45, 45, 45),
                Margin = new Padding(4), // Smaller margin
                AutoSize = true,
                Width = 300 // Set a fixed width for the group box
            };

            var toggleButton = new Button
            {
                Text = "-",
                Dock = DockStyle.Top,
                Width = 20,
                Height = 20,
                BackColor = System.Drawing.Color.FromArgb(85, 160, 140),
                ForeColor = System.Drawing.Color.White,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 }
            };
            ApplyRoundedCorners(toggleButton);

            var contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoSize = true
            };

            var tableLayoutPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 6,
                AutoSize = true,
                MaximumSize = new Size(300, 0), // Constrain maximum width
                Padding = new Padding(2) // Even smaller padding
            };
            tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F)); // Fixed size for the first column
            tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F)); // Fill remaining space
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 18F)); // Smaller row height
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 25F)); // Smaller row height
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F)); // Smaller row height for list box
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F)); // Smaller row height
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 25F)); // Smaller row height
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var propertyNameLabel = new System.Windows.Forms.Label
            {
                Text = "Property Name:",
                ForeColor = System.Drawing.Color.White,
                Anchor = AnchorStyles.Left,
                AutoSize = true
            };
            tableLayoutPanel.Controls.Add(propertyNameLabel, 0, 0);

            propertyNameTextBox.Margin = new Padding(2); // Even smaller margin
            propertyNameTextBox.Width = 150; // Set a fixed width for the input box
            tableLayoutPanel.Controls.Add(propertyNameTextBox, 1, 0);

            var addPropertyButton = new Button
            {
                Text = "Add Property",
                Width = 70, // Smaller width
                Height = 20, // Smaller height
                BackColor = System.Drawing.Color.FromArgb(85, 160, 140),
                ForeColor = System.Drawing.Color.White,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 },
                Margin = new Padding(2), // Smaller margin
                Anchor = AnchorStyles.Left
            };
            ApplyRoundedCorners(addPropertyButton);
            addPropertyButton.Click += AddProperty_Click;
            tableLayoutPanel.Controls.Add(addPropertyButton, 1, 1);

            propertyListBox.Dock = DockStyle.Fill;
            propertyListBox.Height = 60; // Smaller height for list box
            propertyListBox.BackColor = System.Drawing.Color.FromArgb(60, 60, 60);
            propertyListBox.ForeColor = System.Drawing.Color.White;
            propertyListBox.Margin = new Padding(2); // Even smaller margin
            propertyListBox.ContextMenuStrip = CreateContextMenuStrip(propertyListBox, "Property"); // Add removal option
            propertyListBox.SelectedIndexChanged += PropertyListBox_SelectedIndexChanged;
            tableLayoutPanel.Controls.Add(propertyListBox, 0, 2);
            tableLayoutPanel.SetColumnSpan(propertyListBox, 2);

            var variantsLabel = new System.Windows.Forms.Label
            {
                Text = "Variants (for selected property):",
                ForeColor = System.Drawing.Color.White,
                Anchor = AnchorStyles.Left,
                AutoSize = true
            };
            tableLayoutPanel.Controls.Add(variantsLabel, 0, 3);

            variantsTextBox.Margin = new Padding(2); // Even smaller margin
            variantsTextBox.Width = 150; // Set a fixed width for the input box
            tableLayoutPanel.Controls.Add(variantsTextBox, 1, 3);

            var addVariantButton = new Button
            {
                Text = "Add Variant",
                Width = 70, // Smaller width
                Height = 20, // Smaller height
                BackColor = System.Drawing.Color.FromArgb(85, 160, 140),
                ForeColor = System.Drawing.Color.White,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 },
                Margin = new Padding(2), // Smaller margin
                Anchor = AnchorStyles.Left
            };
            ApplyRoundedCorners(addVariantButton);
            addVariantButton.Click += AddVariant_Click;
            tableLayoutPanel.Controls.Add(addVariantButton, 1, 4);

            variantListBox.Dock = DockStyle.Fill;
            variantListBox.Height = 50; // Smaller height for list box
            variantListBox.BackColor = System.Drawing.Color.FromArgb(60, 60, 60);
            variantListBox.ForeColor = System.Drawing.Color.White;
            variantListBox.Margin = new Padding(2); // Even smaller margin
            variantListBox.ContextMenuStrip = CreateContextMenuStrip(variantListBox, "Variant"); // Add removal option
            tableLayoutPanel.Controls.Add(variantListBox, 0, 5);
            tableLayoutPanel.SetColumnSpan(variantListBox, 2);

            contentPanel.Controls.Add(tableLayoutPanel);
            groupBox.Controls.Add(contentPanel);
            groupBox.Controls.Add(toggleButton);
            groupBox.Tag = contentPanel;

            toggleButton.Click += (sender, e) =>
            {
                contentPanel.Visible = !contentPanel.Visible;
                toggleButton.Text = contentPanel.Visible ? "-" : "+";
            };

            return groupBox;
        }

        private void DataGridView_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.ColumnIndex >= 0 && e.RowIndex >= 0 && e.Value != null)
            {
                if (dataGridView.Columns[e.ColumnIndex].Name.EndsWith("Result"))
                {
                    // Get the current cell key
                    var key = (RowIndex: e.RowIndex, ColumnIndex: e.ColumnIndex);

                    // Check if the result is attached
                    bool isResultAttached = attachedResults.ContainsKey(key);

                    // Set the color based on the current value
                    System.Drawing.Color cellColor = GetButtonColor(e.Value.ToString());
                    System.Drawing.Color textColor = GetButtonTextColor(e.Value.ToString());
                    e.PaintBackground(e.CellBounds, true);

                    // Draw the cell background
                    using (var b = new SolidBrush(cellColor))
                    {
                        e.Graphics.FillRectangle(b, e.CellBounds);
                    }

                    // If a result is attached, draw a border and bold text
                    if (isResultAttached)
                    {
                        // Draw border
                        using (Pen pen = new Pen(System.Drawing.Color.White, 2))
                        {
                            Rectangle rect = e.CellBounds;
                            rect.Width -= 2;
                            rect.Height -= 2;
                            ControlPaint.DrawBorder3D(e.Graphics, e.CellBounds, Border3DStyle.Raised);
                        }

                        // Draw bold text
                        Font boldFont = new Font(e.CellStyle.Font, System.Drawing.FontStyle.Bold);
                        TextRenderer.DrawText(e.Graphics, e.Value.ToString(), boldFont, e.CellBounds, textColor, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
                    }
                    else
                    {
                        // Draw regular text
                        TextRenderer.DrawText(e.Graphics, e.Value.ToString(), e.CellStyle.Font, e.CellBounds, textColor, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
                    }

                    e.Handled = true;
                }
            }
        }

        private void InitializeDebugPanel()
        {
            debugPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 200,
                Visible = false
            };
            Controls.Add(debugPanel);

            debugTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = System.Drawing.Color.FromArgb(45, 45, 45),
                ForeColor = System.Drawing.Color.White,
                Font = new Font("Consolas", 10)
            };
            debugPanel.Controls.Add(debugTextBox);
        }

        private void AddUnit_Click(object sender, EventArgs e)
        {
            string unitName = unitTextBox.Text.Trim();
            if (!string.IsNullOrEmpty(unitName))
            {
                unitNames.Add(unitName);
                LogDebug($"Adding unit '{unitName}' to Unit ListBox...");
                unitListBox.Items.Add(unitName); // Add to unitListBox
                LogDebug("Unit added to ListBox.");
                unitTextBox.Clear();
                UpdateResultsGrid();
            }
            else
            {
                LogDebug("Unit name was empty, nothing added.");
            }
        }

        private void AddProperty_Click(object sender, EventArgs e)
        {
            string propertyName = propertyTextBox.Text.Trim();
            if (!string.IsNullOrEmpty(propertyName))
            {
                propertyNames.Add(propertyName);
                propertyVariations.Add(new List<string>()); // Initialize the variant list for this property

                LogDebug($"Adding Property: {propertyName}");
                propertyListBox.Items.Add(propertyName); // Add to propertyListBox

                LogDebug($"Property '{propertyName}' added to ListBox.");
                propertyTextBox.Clear();
                UpdateResultsGrid();
            }
            else
            {
                LogDebug("No property name entered.");
            }
        }

        private void AddVariant_Click(object sender, EventArgs e)
        {
            if (propertyListBox.SelectedIndex != -1)
            {
                string variant = propertyVariantsTextBox.Text.Trim();
                if (!string.IsNullOrEmpty(variant))
                {
                    int selectedIndex = propertyListBox.SelectedIndex;
                    propertyVariations[selectedIndex].Add(variant);
                    LogDebug($"Adding variant '{variant}' to property '{propertyNames[selectedIndex]}'");
                    propertyVariantListBox.Items.Add(variant); // Add to variantListBox
                    propertyVariantsTextBox.Clear();
                    UpdateResultsGrid();
                }
                else
                {
                    LogDebug("Variant name was empty, nothing added.");
                }
            }
            else
            {
                LogDebug("No property selected for adding variant.");
            }
        }

        private void PropertyListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (propertyListBox.SelectedIndex != -1)
            {
                int selectedIndex = propertyListBox.SelectedIndex;

                // Debug logging to help diagnose the issue
                LogDebug($"Selected Index: {selectedIndex}, Property Variations Count: {propertyVariations.Count}");

                // Check to ensure the index is within bounds
                if (selectedIndex >= 0 && selectedIndex < propertyVariations.Count)
                {
                    propertyVariantListBox.Items.Clear();
                    foreach (var variant in propertyVariations[selectedIndex])
                    {
                        propertyVariantListBox.Items.Add(variant);
                    }
                }
                else
                {
                    LogDebug("Selected index is out of bounds.");
                }
            }
        }

        private void AddTest_Click(object sender, EventArgs e)
        {
            if (testResultsTable == null)
            {
                InitializeTestConfig(); // This will initialize the table and add necessary columns
            }

            string selectedTest = testSelectionComboBox.SelectedItem?.ToString();
            if (!string.IsNullOrEmpty(selectedTest))
            {
                string customTestName = customTestNameTextBox.Text.Trim();
                if (!string.IsNullOrEmpty(customTestName))
                {
                    string combinedTestName = $"{selectedTest} - {customTestName}";
                    testNames.Add(combinedTestName);
                    addedTestsListBox.Items.Add(combinedTestName); // Add the test to the ListBox

                    string resultColumnName = $"{combinedTestName} Result";

                    // Check if the result column already exists
                    if (!testResultsTable.Columns.Contains(resultColumnName))
                    {
                        var newColumn = new DataColumn(resultColumnName);
                        testResultsTable.Columns.Add(newColumn);

                        // Move the new column to the end of the DataTable
                        int newIndex = testResultsTable.Columns.Count - 1;
                        for (int i = 0; i < testResultsTable.Rows.Count; i++)
                        {
                            testResultsTable.Rows[i][newIndex] = "No Result"; // Set default value
                        }
                    }

                    // Update the numbering in the "No" column
                    for (int i = 0; i < testResultsTable.Rows.Count; i++)
                    {
                        testResultsTable.Rows[i]["No"] = i + 1;
                    }

                    UpdateResultsGrid(); // Refresh the DataGridView to reflect the changes
                    customTestNameTextBox.Clear();
                }
            }
        }

        private void InitializeAddedTestsListBox()
        {
            addedTestsListBox = new ListBox
            {
                Dock = DockStyle.Left, // Adjust the DockStyle and positioning as needed
                Width = 200, // Set a width appropriate for your layout
                BackColor = System.Drawing.Color.FromArgb(45, 45, 45),
                ForeColor = System.Drawing.Color.White,
                Font = new Font("Segoe UI", 8)
            };

            Controls.Add(addedTestsListBox);

            // Add a context menu for removing tests
            var contextMenu = new ContextMenuStrip();
            var removeItem = new ToolStripMenuItem("Remove", null, RemoveSelectedTest_Click);
            contextMenu.Items.Add(removeItem);

            // Populate the ListBox initially if there are already added tests
            PopulateAddedTestsListBox();
        }

        private void PopulateAddedTestsListBox()
        {
            addedTestsListBox.Items.Clear();
            foreach (var test in testNames)
            {
                addedTestsListBox.Items.Add(test);
            }
        }
        private void RemoveUnit(object sender, EventArgs e)
        {
            if (unitListBox.SelectedIndex != -1)
            {
                string selectedUnit = unitListBox.SelectedItem.ToString();
                LogDebug($"Removing unit '{selectedUnit}' from Unit ListBox...");
                unitNames.Remove(selectedUnit);
                unitListBox.Items.RemoveAt(unitListBox.SelectedIndex);
                UpdateResultsGrid();
            }
        }

        private void RemoveProperty(object sender, EventArgs e)
        {
            if (propertyListBox.SelectedIndex != -1)
            {
                string selectedProperty = propertyListBox.SelectedItem.ToString();
                LogDebug($"Removing property '{selectedProperty}' from Property ListBox...");
                int selectedIndex = propertyListBox.SelectedIndex;
                propertyNames.RemoveAt(selectedIndex);
                propertyVariations.RemoveAt(selectedIndex);
                propertyListBox.Items.RemoveAt(selectedIndex);
                UpdateResultsGrid();
                propertyVariantListBox.Items.Clear();
            }
        }

        private void RemoveVariant(object sender, EventArgs e)
        {
            if (propertyVariantListBox.SelectedIndex != -1 && propertyListBox.SelectedIndex != -1)
            {
                string selectedVariant = propertyVariantListBox.SelectedItem.ToString();
                LogDebug($"Removing variant '{selectedVariant}' from Property '{propertyNames[propertyListBox.SelectedIndex]}'...");
                int selectedPropertyIndex = propertyListBox.SelectedIndex;
                propertyVariations[selectedPropertyIndex].Remove(selectedVariant);
                propertyVariantListBox.Items.RemoveAt(propertyVariantListBox.SelectedIndex);
                UpdateResultsGrid();
            }
        }

        private void RemoveTest(object sender, EventArgs e)
        {
            if (testListBox.SelectedIndex != -1)
            {
                string selectedTest = testListBox.SelectedItem.ToString();
                LogDebug($"Removing test '{selectedTest}' from Test ListBox...");
                testNames.Remove(selectedTest);
                testListBox.Items.RemoveAt(testListBox.SelectedIndex);
                UpdateResultsGrid();
            }
        }
        private void RemoveSelectedTest_Click(object sender, EventArgs e)
        {
            if (addedTestsListBox.SelectedIndex != -1)
            {
                string selectedTest = addedTestsListBox.SelectedItem.ToString();
                testNames.Remove(selectedTest);
                addedTestsListBox.Items.Remove(selectedTest);

                // Remove the corresponding column from the DataGridView and DataTable
                string resultColumnName = $"{selectedTest} Result";
                if (testResultsTable.Columns.Contains(resultColumnName))
                {
                    testResultsTable.Columns.Remove(resultColumnName);
                }

                // Refresh the DataGridView to reflect the changes
                UpdateResultsGrid();
            }
        }

        private async void DataGridView_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            // Ensure the event is only handled for valid cells and prevent handling if already processing
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0 && !isHandlingClick)
            {
                // Only proceed if the clicked column is a "Result" column
                if (dataGridView.Columns[e.ColumnIndex].Name.EndsWith("Result"))
                {
                    isHandlingClick = true; // Set the debounce flag

                    try
                    {
                        LogDebug($"Button clicked at row {e.RowIndex}, column {e.ColumnIndex}.");

                        // Access the underlying DataTable directly
                        DataRow dataRow = ((DataRowView)dataGridView.Rows[e.RowIndex].DataBoundItem).Row;
                        string currentValue = dataRow[e.ColumnIndex].ToString();
                        LogDebug($"Current button text: {currentValue}");

                        // Determine the next value
                        string nextValue = GetNextResultValue(currentValue);
                        LogDebug($"Button text should change to: {nextValue}");

                        // Update the underlying data source (DataTable)
                        dataRow[e.ColumnIndex] = nextValue;

                        // Manually update the cell value and style
                        DataGridViewCell cell = dataGridView[e.ColumnIndex, e.RowIndex];
                        cell.Value = nextValue;
                        cell.Style.BackColor = GetButtonColor(nextValue);
                        cell.Style.ForeColor = System.Drawing.Color.White; // Ensure the text color is visible
                        LogDebug($"Manually updated cell style to: {nextValue}");

                        // Save the status in the dictionary
                        string key = GenerateStatusKey(e.RowIndex, e.ColumnIndex);
                        testResultsStatus[key] = nextValue;

                        // Force the DataGridView to refresh and display the updated cell
                        dataGridView.InvalidateCell(cell); // Invalidate the specific cell to force a repaint
                        dataGridView.Refresh(); // Refresh the entire DataGridView
                        LogDebug("DataGridView refreshed.");

                        // Update the donut chart
                        UpdateDonutChart(); // Add this line

                        // Log success
                        LogDebug($"Success: Button text updated to: {nextValue}");

                    }
                    catch (Exception ex)
                    {
                        LogDebug($"Error during cell click handling: {ex.Message}");
                    }
                    finally
                    {
                        await Task.Delay(10); // Introduce a slight delay to ensure the debounce flag is reset correctly
                        isHandlingClick = false; // Reset the debounce flag
                    }
                }
            }
        }
        private void DisplayHeatmap()
        {
            dataGridView.DataSource = testResultsTable;

            foreach (DataGridViewColumn column in dataGridView.Columns)
            {
                if (column.Name.EndsWith("Result"))
                {
                    column.CellTemplate = new DataGridViewTextBoxCell(); // Use TextBoxCell instead of ButtonCell
                }
            }

            // Ensure the correct order of columns
            MoveNoAndTestRunColumnsToFront();

            dataGridView.CellClick += DataGridView_CellClick;
            UpdateDonutChart(); // Update the chart after displaying the grid
        }

        private System.Drawing.Color GetButtonColor(string value)
        {
            switch (value)
            {
                case "Pass":
                    return System.Drawing.Color.Green;
                case "Fail":
                    return System.Drawing.Color.Red;
                case "Info Only":
                    return System.Drawing.Color.Yellow;
                case "Not Tested":
                    return System.Drawing.Color.Black;
                default:
                    return System.Drawing.Color.Gray;
            }
        }

        private System.Drawing.Color GetButtonTextColor(string value)
        {
            switch (value)
            {
                case "Info Only":
                    return System.Drawing.Color.Black;
                case "Not Tested":
                    return System.Drawing.Color.White;
                default:
                    return System.Drawing.Color.White;
            }
        }

        private string GetNextResultValue(string currentValue)
        {
            switch (currentValue)
            {
                case "Pass":
                    return "Fail";
                case "Fail":
                    return "Info Only";
                case "Info Only":
                    return "No Result";
                case "No Result":
                    return "Not Tested";
                default:
                    return "Pass";
            }
        }


        private void UpdateButtonCellStyle(DataGridViewButtonCell buttonCell, string value)
        {
            // Update button style based on the value
            switch (value)
            {
                case "Pass":
                    buttonCell.Style.BackColor = System.Drawing.Color.Green;
                    break;
                case "Fail":
                    buttonCell.Style.BackColor = System.Drawing.Color.Red;
                    break;
                case "Info Only":
                    buttonCell.Style.BackColor = System.Drawing.Color.Yellow;
                    break;
                case "No Result":
                    buttonCell.Style.BackColor = System.Drawing.Color.Gray;
                    break;
                case "Not Tested":
                    buttonCell.Style.BackColor = System.Drawing.Color.Black;
                    buttonCell.Style.ForeColor = System.Drawing.Color.White;
                    break;
            }

            // Log to verify the style has been applied
            LogDebug($"Button style updated based on value: {value}");
        }

        private void CycleButtonValue(DataGridViewButtonCell buttonCell)
        {
            // Default the state to "Pass" if it's "No Result" or null
            if (buttonCell.Value == null || buttonCell.Value.ToString() == "No Result")
            {
                buttonCell.Value = "Pass";
                buttonCell.Style.BackColor = System.Drawing.Color.Green;
            }
            else if (buttonCell.Value.ToString() == "Pass")
            {
                buttonCell.Value = "Fail";
                buttonCell.Style.BackColor = System.Drawing.Color.Red;
            }
            else if (buttonCell.Value.ToString() == "Fail")
            {
                buttonCell.Value = "Info Only";
                buttonCell.Style.BackColor = System.Drawing.Color.Yellow;
            }
            else if (buttonCell.Value.ToString() == "Info Only")
            {
                buttonCell.Value = "No Result";
                buttonCell.Style.BackColor = System.Drawing.Color.Gray;
            }
            else if (buttonCell.Value.ToString() == "No Result")
            {
                buttonCell.Value = "Not Tested";
                buttonCell.Style.BackColor = System.Drawing.Color.Black;
                buttonCell.Style.ForeColor = System.Drawing.Color.White;
            }
        }
        // Existing code for UpdateResultsGrid() and other methods
        private void UpdateResultsGrid()
        {
            LogDebug("Starting UpdateResultsGrid...");
            InitializeTestConfig();

            LogDebug("Applying stored statuses to the grid...");
            foreach (DataRow row in testResultsTable.Rows)
            {
                foreach (DataColumn column in testResultsTable.Columns)
                {
                    if (column.ColumnName.EndsWith("Result"))
                    {
                        int rowIndex = testResultsTable.Rows.IndexOf(row);
                        int columnIndex = testResultsTable.Columns.IndexOf(column);
                        string key = GenerateStatusKey(rowIndex, columnIndex);

                        if (testResultsStatus.ContainsKey(key))
                        {
                            row[column.ColumnName] = testResultsStatus[key]; // Restore the saved status
                            LogDebug($"Restored status - Key: {key}, Value: {testResultsStatus[key]}");
                        }
                        else
                        {
                            row[column.ColumnName] = "No Result"; // Default value if not found
                            LogDebug($"No saved status found for Key: {key}, setting default value.");
                        }
                    }
                }
            }

            // Remove 'Not Tested' tests from the total calculations
            int totalTests = testResultsTable.Rows.Count;
            int notTestedCount = testResultsTable.Rows.Cast<DataRow>().Count(row => row.ItemArray.Contains("Not Tested"));

            // Adjust the displayed results percentages based on the reduced total
            foreach (DataRow row in testResultsTable.Rows)
            {
                foreach (DataColumn column in testResultsTable.Columns)
                {
                    if (column.ColumnName.EndsWith("Result") && row[column].ToString() == "Not Tested")
                    {
                        row[column] = DBNull.Value; // Hide 'Not Tested' results from the grid
                    }
                }
            }

            totalTests -= notTestedCount;

            // Update DataGridView to reflect these changes
            dataGridView.DataSource = null;
            dataGridView.DataSource = testResultsTable;

            // Ensure the "No" and "Test Run" columns are in the correct position
            MoveNoAndTestRunColumnsToFront();

            dataGridView.Refresh(); // Force the grid to refresh and display updated data

            UpdateDonutChart(); // Update the chart whenever the grid is updated
            LogDebug("Finished updating the results grid.");
        }

        private void InitializeTestConfig()
        {
            int totalTestRuns = unitNames.Count;
            foreach (var variations in propertyVariations)
            {
                totalTestRuns *= variations.Count;
            }

            testResultsTable = new DataTable();

            // Add the "No" and "Test Run" columns first
            testResultsTable.Columns.Add("No");
            testResultsTable.Columns.Add("Test Run");

            // Add the parameter columns next
            foreach (string propertyName in propertyNames)
            {
                testResultsTable.Columns.Add(propertyName);
            }

            testResultsTable.Columns.Add("Unit Name");

            // Temporarily store the result columns to add them at the end
            List<DataColumn> resultColumns = new List<DataColumn>();
            foreach (string testName in testNames)
            {
                resultColumns.Add(new DataColumn($"{testName} Result"));
            }

            // Now add the result columns at the end
            foreach (var resultColumn in resultColumns)
            {
                testResultsTable.Columns.Add(resultColumn);
            }

            // Populate the DataTable with data and add the "No" column values
            int counter = 1;
            foreach (string unitName in unitNames)
            {
                foreach (var configCombination in GetAllConfigCombinations(propertyVariations))
                {
                    DataRow row = testResultsTable.NewRow();
                    row["No"] = counter++; // Set the value of the "No" column

                    // Build the "Test Run" string with "Unit Name" - "Property Name":"Property Variant"
                    List<string> runDetails = new List<string>();
                    for (int i = 0; i < configCombination.Count; i++)
                    {
                        runDetails.Add($"{propertyNames[i]}:{configCombination[i]}");
                    }
                    row["Test Run"] = $"{unitName} - {string.Join(", ", runDetails)}";

                    for (int i = 0; i < configCombination.Count; i++)
                    {
                        row[propertyNames[i]] = configCombination[i];
                    }
                    row["Unit Name"] = unitName;

                    // Populate result columns with default or existing values
                    foreach (string testName in testNames)
                    {
                        string key = GenerateStatusKey(testResultsTable.Rows.Count, testResultsTable.Columns.IndexOf($"{testName} Result"));
                        if (testResultsStatus.ContainsKey(key))
                        {
                            row[$"{testName} Result"] = testResultsStatus[key]; // Restore status
                        }
                        else
                        {
                            row[$"{testName} Result"] = "No Result"; // Default value
                        }
                    }
                    testResultsTable.Rows.Add(row);
                }
            }

            // Re-bind the DataTable to the DataGridView
            dataGridView.DataSource = testResultsTable;

            // Ensure the correct order of columns
            MoveNoAndTestRunColumnsToFront();
        }

        private void MoveNoAndTestRunColumnsToFront()
        {
            dataGridView.Columns["No"].DisplayIndex = 0;
            dataGridView.Columns["Test Run"].DisplayIndex = 1;
        }

        private string GenerateStatusKey(int rowIndex, int columnIndex)
        {
            return $"{rowIndex}-{columnIndex}";
        }

        private List<List<string>> GetAllConfigCombinations(List<List<string>> propertyVariations)
        {
            List<List<string>> combinations = new List<List<string>>();

            void GenerateCombinations(List<string> currentCombination, int propertyIndex)
            {
                if (propertyIndex == propertyVariations.Count)
                {
                    combinations.Add(new List<string>(currentCombination));
                    return;
                }

                foreach (string variation in propertyVariations[propertyIndex])
                {
                    currentCombination.Add(variation);
                    GenerateCombinations(currentCombination, propertyIndex + 1);
                    currentCombination.RemoveAt(currentCombination.Count - 1);
                }
            }

            GenerateCombinations(new List<string>(), 0);
            return combinations;
        }

        // Example of adding a log to the debug window
        private void LogDebug(string message)
        {
            debugTextBox.AppendText($"{DateTime.Now}: {message}\r\n");
        }


        // Utility method to create context menus for list boxes with remove option
        private ContextMenuStrip CreateContextMenuStrip(ListBox listBox, string type)
        {
            var contextMenu = new ContextMenuStrip();
            var removeItem = new ToolStripMenuItem("Remove", null, (sender, e) =>
            {
                if (listBox.SelectedIndex != -1)
                {
                    switch (type)
                    {
                        case "Unit":
                            RemoveUnit(sender, e);
                            break;
                        case "Property":
                            RemoveProperty(sender, e);
                            break;
                        case "Variant":
                            RemoveVariant(sender, e);
                            break;
                        case "Test":
                            RemoveTest(sender, e);
                            break;
                    }
                }
            });
            contextMenu.Items.Add(removeItem);
            return contextMenu;
        }

        // Utility method to apply rounded corners to a button
        private void ApplyRoundedCorners(Button button)
        {
            int radius = 10; // Adjust the radius to make the corners more rounded
            var roundedRectPath = new GraphicsPath();

            roundedRectPath.AddArc(0, 0, radius, radius, 180, 90);
            roundedRectPath.AddArc(button.Width - radius, 0, radius, radius, 270, 90);
            roundedRectPath.AddArc(button.Width - radius, button.Height - radius, radius, radius, 0, 90);
            roundedRectPath.AddArc(0, button.Height - radius, radius, radius, 90, 90);
            roundedRectPath.CloseAllFigures();

            button.Region = new Region(roundedRectPath);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
        }
    }
    [Serializable]
    public class TestConfig
    {
        public List<string> UnitNames { get; set; }
        public List<string> PropertyNames { get; set; }
        public List<List<string>> PropertyVariations { get; set; }
        public List<string> TestNames { get; set; }
        public Dictionary<string, string> TestResultsStatus { get; set; }
        public Dictionary<(int RowIndex, int ColumnIndex), AttachedResult> AttachedResults { get; set; } // Add this line
    }
    [Serializable]
    public class AttachedResult
    {
        public string Status { get; set; }
        public string Data { get; set; }
        public string FileName { get; set; }  // Add this property to store the file name
    }

}
