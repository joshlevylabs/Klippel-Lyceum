using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Drawing;
using System.IO;
using KLAUTOMATIONLib;
using OxyPlot;
using OxyPlot.WindowsForms;
using System.Runtime.InteropServices;
using System.Data.SQLite;
using System.Data;
using System.Linq;

namespace LyceumKlippel
{
    public partial class LYKHome : BaseForm
    {
        private string accessToken;
        private string refreshToken;
        private TextBox logTextBox;
        private TreeView treeViewDatabase;
        private KlDatabase database;
        private string loadedFilePath;
        private PlotView plotView;
        private Label lblTreeViewTitle;
        private Label lblGraphTitle;
        private List<SignalPathData> signalPaths = new List<SignalPathData>();
        private ToolStripMenuItem exportMenuItem;
        private ChartPreferences chartPreferences = new ChartPreferences();

        private KlDBNode currentQcNode;     // To hold the QC node
        private IKlModuleQC currentQcModule; // To hold the QC module
        private IKlQCMeasure currentMeasure;

        public LYKHome(string accessToken, string refreshToken)
        {
            this.accessToken = accessToken;
            this.refreshToken = refreshToken;
            LogManager.Initialize();
            InitializeComponent();
            InitializeControls();

            this.Size = new Size(800, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(45, 45, 45);
        }

        private void InitializeComponent()
        {
            // Add any additional form initialization if needed
        }

        private void InitializeControls()
        {
            int menuHeight = menuStrip != null ? menuStrip.Height : 0;

            treeViewDatabase = new TreeView
            {
                Location = new Point(10, 50 + menuHeight),
                Size = new Size(300, 400),
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10)
            };
            treeViewDatabase.AfterSelect += TreeViewDatabase_AfterSelect;
            this.Controls.Add(treeViewDatabase);

            lblTreeViewTitle = new Label
            {
                Text = "Database Structure",
                Location = new Point(10, 30 + menuHeight),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12, FontStyle.Bold)
            };
            this.Controls.Add(lblTreeViewTitle);

            plotView = new PlotView
            {
                Location = new Point(330, 50 + menuHeight),
                Size = new Size(400, 400),
                BackColor = Color.FromArgb(45, 45, 45)
            };
            this.Controls.Add(plotView);

            lblGraphTitle = new Label
            {
                Text = "Graph Panel",
                Location = new Point(330, 30 + menuHeight),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12, FontStyle.Bold)
            };
            this.Controls.Add(lblGraphTitle);
        }

        protected override void AddCustomMenuItems()
        {
            ToolStripMenuItem dataMenu = new ToolStripMenuItem("Data");

            ToolStripMenuItem importItem = new ToolStripMenuItem("Import .kdb File");
            importItem.Click += BtnImportKdb_Click;
            dataMenu.DropDownItems.Add(importItem);

            exportMenuItem = new ToolStripMenuItem("Export to CSV");
            exportMenuItem.Click += BtnExportCsv_Click;
            exportMenuItem.Enabled = false;
            dataMenu.DropDownItems.Add(exportMenuItem);

            ToolStripMenuItem queryDatabaseItem = new ToolStripMenuItem("Query Database");
            queryDatabaseItem.Click += QueryDatabaseItem_Click;
            dataMenu.DropDownItems.Add(queryDatabaseItem);

            menuStrip.Items.Add(dataMenu);

            ToolStripMenuItem graphMenu = new ToolStripMenuItem("Graph");
            ToolStripMenuItem chartPreferencesItem = new ToolStripMenuItem("Chart Preferences");
            chartPreferencesItem.Click += ChartPreferencesItem_Click;
            graphMenu.DropDownItems.Add(chartPreferencesItem);
            menuStrip.Items.Add(graphMenu);
        }

        private void LoadDatabase(string filePath)
        {
            LogManager.AppendLog($"Starting to load database: {filePath}");
            try
            {
                // Release previous resources if they exist
                if (currentQcNode != null && currentQcModule != null)
                {
                    currentQcNode.ReleaseInstance();
                    Marshal.ReleaseComObject(currentQcModule);
                    currentQcModule = null;
                    Marshal.ReleaseComObject(currentQcNode);
                    currentQcNode = null;
                }
                if (database != null)
                {
                    database.Close();
                    Marshal.ReleaseComObject(database);
                    database = null;
                }

                // Open the new database
                database = new KlDatabase();
                database.Open(filePath);
                LogManager.AppendLog($"Opened database file: {filePath}");

                // Store the QC node and module for later use
                currentQcNode = database.GetNode(@"\QC\QC");
                if (currentQcNode != null && currentQcNode.TypeID == "{63AB89D5-AE84-11D5-B6D0-525405F7AE84}")
                {
                    currentQcModule = (IKlModuleQC)currentQcNode.LoadInstance();
                }
                else
                {
                    LogManager.AppendLog("QC node or module not found.");
                    MessageBox.Show("QC node or module not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                treeViewDatabase.Nodes.Clear();
                PopulateTreeViewWithWindowsAndCurves();
                LogManager.AppendLog($"Successfully loaded database: {filePath}");
            }
            catch (Exception ex)
            {
                LogManager.AppendLog($"Error loading database: {ex.Message}");
                MessageBox.Show($"Error loading database: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                exportMenuItem.Enabled = false;
            }
        }

        private void PopulateTreeViewWithWindowsAndCurves()
        {
            if (database == null)
            {
                LogManager.AppendLog("Database not initialized.");
                return;
            }

            string dbName = Path.GetFileNameWithoutExtension(loadedFilePath);
            TreeNode rootNode = new TreeNode(dbName);
            treeViewDatabase.Nodes.Add(rootNode);
            LogManager.AppendLog($"Added root database node: '{dbName}'");

            try
            {
                KlDBNode rootDbNode = database.Root; // Get the root node
                PopulateFolderNodes(rootNode, rootDbNode);
            }
            catch (Exception ex)
            {
                LogManager.AppendLog($"Error populating TreeView: {ex.Message}");
            }

            rootNode.Expand();
            LogManager.AppendLog("TreeView population completed");
        }

        private void PopulateFolderNodes(TreeNode parentNode, KlDBNode dbNode)
        {
            // Add current node to TreeView
            TreeNode currentNode = new TreeNode(dbNode.Name);
            parentNode.Nodes.Add(currentNode);
            LogManager.AppendLog($"Added folder node: '{dbNode.Name}'");

            // Access the collection of child nodes using the Children property
            IKlDBNodeCollection children = dbNode.Children;

            // Iterate over the child nodes
            foreach (IKlDBNode childNode in children)
            {
                PopulateFolderNodes(currentNode, (KlDBNode)childNode);
            }

            // Check if this node contains a QC module
            if (dbNode.TypeID == "{63AB89D5-AE84-11D5-B6D0-525405F7AE84}") // QC module type ID
            {
                IKlModuleQC qcModule = (IKlModuleQC)dbNode.LoadInstance();
                if (qcModule != null)
                {
                    PopulateQCModuleNodes(currentNode, qcModule);
                }
            }
        }
        private void PopulateQCModuleNodes(TreeNode parentNode, IKlModule module)
        {
            if (module is IKlModuleQC qcModule)
            {
                HashSet<string> existingNodes = new HashSet<string>();
                List<IKlQCMeasure> uncategorizedMeasures = new List<IKlQCMeasure>();

                foreach (IKlWindow window in qcModule.Results.Windows)
                {
                    string windowName = window.Name;
                    LogManager.AppendLog($"Result Window: {windowName}");
                    TreeNode windowNode = new TreeNode(windowName);
                    parentNode.Nodes.Add(windowNode);

                    foreach (IKlQCTask task in qcModule.Tasks)
                    {
                        foreach (IKlQCMeasure measure in task.Measures)
                        {
                            KlippelDataProcessor.MeasureInfo info = KlippelDataProcessor.GetMeasureInfo(measure.Name);
                            if (info.Category == windowName)
                            {
                                KlippelDataProcessor.PopulateMeasureNodes(windowNode, measure, database, existingNodes);
                            }
                            else if (info.Category == "Uncategorized")
                            {
                                uncategorizedMeasures.Add(measure);
                            }
                        }
                    }
                }

                // Add Uncategorized node if there are uncategorized measures
                if (uncategorizedMeasures.Count > 0)
                {
                    TreeNode uncategorizedNode = new TreeNode("Uncategorized");
                    parentNode.Nodes.Add(uncategorizedNode);
                    foreach (var measure in uncategorizedMeasures)
                    {
                        KlippelDataProcessor.PopulateMeasureNodes(uncategorizedNode, measure, database, existingNodes);
                    }
                }
            }
            else
            {
                LogManager.AppendLog("Module is not a QC module.");
            }
        }

        // Helper method to find or create category nodes
        private TreeNode FindOrCreateCategoryNode(TreeNode parentNode, string categoryName)
        {
            foreach (TreeNode node in parentNode.Nodes)
            {
                if (node.Text == categoryName)
                {
                    return node;
                }
            }
            TreeNode newNode = new TreeNode(categoryName);
            parentNode.Nodes.Add(newNode);
            return newNode;
        }

        private double? GetScalarValue(IKlQCMeasure measure)
        {
            try
            {
                return measure.ScalarValue;
            }
            catch (Exception)
            {
                return null; // Indicates the measure is not a scalar
            }
        }

        private double[,] GetCurveData(IKlQCMeasure measure)
        {
            try
            {
                if (!measure.IsDataAvailable)
                {
                    LogManager.AppendLog($"No data available for measure '{measure.Name}'");
                    return null;
                }

                double? scalar = GetScalarValue(measure);
                if (scalar.HasValue)
                {
                    LogManager.AppendLog($"Measure '{measure.Name}' is scalar, no curve data");
                    return null;
                }

                if (measure.Curve == null)
                {
                    LogManager.AppendLog($"Curve is null for measure '{measure.Name}'");
                    return null;
                }

                object curveDataObj = measure.Curve.Data;
                if (curveDataObj is float[,] floatData)
                {
                    int rows = floatData.GetLength(0);
                    int cols = floatData.GetLength(1);
                    double[,] data = new double[rows, cols];
                    for (int i = 0; i < rows; i++)
                    {
                        for (int j = 0; j < cols; j++)
                        {
                            data[i, j] = floatData[i, j];
                        }
                    }
                    return data;
                }
                else if (curveDataObj is double[,] doubleData)
                {
                    return doubleData;
                }
                else
                {
                    LogManager.AppendLog($"Curve data for measure '{measure.Name}' is of unsupported type");
                    return null;
                }
            }
            catch (Exception ex)
            {
                LogManager.AppendLog($"Exception in GetCurveData for measure '{measure.Name}': {ex.Message}");
                return null;
            }
        }

        private void TreeViewDatabase_AfterSelect(object sender, TreeViewEventArgs e)
        {
            try
            {
                if (e.Node.Tag is IKlQCMeasure measure) // Measure selected
                {
                    LogManager.AppendLog($"Selected Measure: {measure.Name}");
                    if (measure.IsDataAvailable)
                    {
                        double? scalarValue = GetScalarValue(measure);
                        double[,] curveData = GetCurveData(measure);
                        UpdateGraphWithData(measure, curveData, scalarValue);
                    }
                    else
                    {
                        LogManager.AppendLog("No data available for this measure.");
                        plotView.Model = null;
                    }
                }
                else if (e.Node.Tag is IKlQCTask task) // Task selected
                {
                    LogManager.AppendLog($"Selected Task: {task.Name}");
                    if (e.Node.Nodes.Count > 0) // Assuming the first child is the subcategory
                    {
                        TreeNode subcategoryNode = e.Node.Nodes[0];
                        PlotAllMeasuresUnderNode(subcategoryNode);
                    }
                    else
                    {
                        plotView.Model = null;
                    }
                }
                else if (e.Node.Parent != null && e.Node.Parent.Tag is IKlQCTask) // Subcategory selected
                {
                    LogManager.AppendLog($"Selected Subcategory: {e.Node.Text}");
                    PlotAllMeasuresUnderNode(e.Node);
                }
                else
                {
                    LogManager.AppendLog($"Selected item: {e.Node.Text} (Database or unknown type)");
                    plotView.Model = null;
                }
            }
            catch (Exception ex)
            {
                LogManager.AppendLog($"Error in TreeViewDatabase_AfterSelect: {ex.Message}");
                MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void PlotAllMeasuresUnderNode(TreeNode node)
        {
            var model = new PlotModel { Title = node.Text };
            foreach (TreeNode measureNode in node.Nodes)
            {
                if (measureNode.Tag is IKlQCMeasure m && m.IsDataAvailable)
                {
                    double[,] curveData = GetCurveData(m);
                    if (curveData != null && curveData.GetLength(1) >= 2)
                    {
                        var series = new OxyPlot.Series.LineSeries { Title = m.Name };
                        for (int i = 0; i < curveData.GetLength(0); i++)
                        {
                            series.Points.Add(new DataPoint(curveData[i, 0], curveData[i, 1]));
                        }
                        model.Series.Add(series);
                    }
                }
            }
            plotView.Model = model;
        }

        private void UpdateGraphWithData(IKlQCMeasure measure, double[,] curveData, double? scalarValue)
        {
            var model = new PlotModel
            {
                Title = measure.Name,
                Background = OxyColor.FromArgb(255, 45, 45, 45),
                TextColor = OxyColors.White,
                TitleColor = OxyColors.White,
                PlotAreaBorderColor = OxyColors.Gray
            };

            if (curveData != null && curveData.GetLength(1) >= 2)
            {
                // Handle matrix data (e.g., impedance curve)
                var series = new OxyPlot.Series.LineSeries { Title = "Measurement", Color = OxyColors.White };
                for (int i = 0; i < curveData.GetLength(0); i++)
                {
                    series.Points.Add(new DataPoint(curveData[i, 0], curveData[i, 1]));
                }
                model.Series.Add(series);

                // Axis configuration (unchanged)
                OxyPlot.Axes.Axis xAxis = chartPreferences.XScaling == "Logarithmic"
                    ? (OxyPlot.Axes.Axis)new OxyPlot.Axes.LogarithmicAxis { Position = OxyPlot.Axes.AxisPosition.Bottom, Title = "Frequency (Hz)" }
                    : (OxyPlot.Axes.Axis)new OxyPlot.Axes.LinearAxis { Position = OxyPlot.Axes.AxisPosition.Bottom, Title = "Frequency (Hz)" };
                xAxis.TextColor = OxyColors.White;
                xAxis.TicklineColor = OxyColors.Gray;
                xAxis.MajorGridlineStyle = chartPreferences.ShowMajorTicksX ? LineStyle.Solid : LineStyle.None;
                xAxis.MinorGridlineStyle = chartPreferences.ShowMinorTicksX ? LineStyle.Solid : LineStyle.None;
                if (chartPreferences.XMin.HasValue) xAxis.Minimum = chartPreferences.XMin.Value;
                if (chartPreferences.XMax.HasValue) xAxis.Maximum = chartPreferences.XMax.Value;
                model.Axes.Add(xAxis);

                OxyPlot.Axes.Axis yAxis = chartPreferences.YScaling == "Logarithmic"
                    ? (OxyPlot.Axes.Axis)new OxyPlot.Axes.LogarithmicAxis { Position = OxyPlot.Axes.AxisPosition.Left, Title = "Magnitude" }
                    : (OxyPlot.Axes.Axis)new OxyPlot.Axes.LinearAxis { Position = OxyPlot.Axes.AxisPosition.Left, Title = "Magnitude" };
                yAxis.TextColor = OxyColors.White;
                yAxis.TicklineColor = OxyColors.Gray;
                yAxis.MajorGridlineStyle = chartPreferences.ShowMajorTicksY ? LineStyle.Solid : LineStyle.None;
                yAxis.MinorGridlineStyle = chartPreferences.ShowMinorTicksY ? LineStyle.Solid : LineStyle.None;
                if (chartPreferences.YMin.HasValue) yAxis.Minimum = chartPreferences.YMin.Value;
                if (chartPreferences.YMax.HasValue) yAxis.Maximum = chartPreferences.YMax.Value;
                model.Axes.Add(yAxis);
            }
            else if (scalarValue.HasValue)
            {
                // Handle scalar data
                var barSeries = new OxyPlot.Series.BarSeries { Title = "Scalar Value", FillColor = OxyColors.White };
                barSeries.Items.Add(new OxyPlot.Series.BarItem { Value = scalarValue.Value });
                model.Series.Add(barSeries);

                // Axis configuration (unchanged)
                var categoryAxis = new OxyPlot.Axes.CategoryAxis
                {
                    Position = OxyPlot.Axes.AxisPosition.Bottom,
                    Title = "Measure",
                    Labels = { measure.Name },
                    TextColor = OxyColors.White,
                    TicklineColor = OxyColors.Gray
                };
                model.Axes.Add(categoryAxis);

                OxyPlot.Axes.Axis yAxis = chartPreferences.YScaling == "Logarithmic"
                    ? (OxyPlot.Axes.Axis)new OxyPlot.Axes.LogarithmicAxis { Position = OxyPlot.Axes.AxisPosition.Left, Title = "Value" }
                    : (OxyPlot.Axes.Axis)new OxyPlot.Axes.LinearAxis { Position = OxyPlot.Axes.AxisPosition.Left, Title = "Value" };
                yAxis.TextColor = OxyColors.White;
                yAxis.TicklineColor = OxyColors.Gray;
                yAxis.MajorGridlineStyle = chartPreferences.ShowMajorTicksY ? LineStyle.Solid : LineStyle.None;
                yAxis.MinorGridlineStyle = chartPreferences.ShowMinorTicksY ? LineStyle.Solid : LineStyle.None;
                if (chartPreferences.YMin.HasValue) yAxis.Minimum = chartPreferences.YMin.Value;
                if (chartPreferences.YMax.HasValue) yAxis.Maximum = chartPreferences.YMax.Value;
                model.Axes.Add(yAxis);
            }
            else
            {
                model.Title += " (No data available)";
                LogManager.AppendLog($"No data available for measure '{measure.Name}'");
            }

            plotView.Model = model;
            LogManager.AppendLog($"Graph updated for: {measure.Name}");
        }

        private void BtnImportKdb_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Klippel Database Files (*.kdb;*.kdbx)|*.kdb;*.kdbx",
                Title = "Select a Klippel Database File"
            };

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                loadedFilePath = openFileDialog.FileName;
                LoadDatabase(loadedFilePath);
                exportMenuItem.Enabled = true;
            }
        }

        private void BtnExportCsv_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(loadedFilePath))
            {
                MessageBox.Show("No database file loaded.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string csvFilePath = Path.ChangeExtension(loadedFilePath, ".csv");
            ConvertKdbToCSV(database, csvFilePath);
            MessageBox.Show($"Data exported to {csvFilePath}", "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void QueryDatabaseItem_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(loadedFilePath))
            {
                MessageBox.Show("No database loaded.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            QueryDatabaseForm queryForm = new QueryDatabaseForm(loadedFilePath);
            queryForm.Show();
        }

        private void ChartPreferencesItem_Click(object sender, EventArgs e)
        {
            using (ChartPreferencesForm preferencesForm = new ChartPreferencesForm(chartPreferences))
            {
                if (preferencesForm.ShowDialog() == DialogResult.OK)
                {
                    chartPreferences = preferencesForm.GetPreferences();
                    if (currentMeasure != null)
                    {
                        UpdateGraphWithData(currentMeasure, GetCurveData(currentMeasure), GetScalarValue(currentMeasure));
                    }
                }
            }
        }

        private void ConvertKdbToCSV(KlDatabase kdb, string targetFile)
        {
            LogManager.AppendLog($"Starting CSV export to: {targetFile}");
            try
            {
                KlDBNode dbNode = kdb.GetNode(@"\QC\QC");
                if (dbNode == null) return;

                if (dbNode.TypeID != "{63AB89D5-AE84-11D5-B6D0-525405F7AE84}") return;

                IKlModuleQC qcModule = (IKlModuleQC)dbNode.LoadInstance();
                using (StreamWriter sw = File.CreateText(targetFile))
                {
                    sw.WriteLine("Measure/Limit Name,Value");

                    if (qcModule.Tasks.Exists("Impedance"))
                    {
                        IKlQCTask task = qcModule.Tasks["Impedance"];
                        IKlQCMeasure meas = task.Measures["re"];
                        if (meas.IsDataAvailable)
                        {
                            sw.WriteLine($"Re,{meas.ScalarValue}");
                            foreach (IKlQCLimit limit in meas.Limits)
                            {
                                if (limit.IsDataAvailable)
                                    sw.WriteLine($"{limit.Name},{limit.ScalarValue}");
                            }
                        }
                    }

                    string splTaskName = qcModule.Tasks.Exists("Sound Pressure (NI)") ? "Sound Pressure (NI)" :
                                        qcModule.Tasks.Exists("Sound Pressure") ? "Sound Pressure" : null;
                    if (splTaskName != null)
                    {
                        IKlQCTask task = qcModule.Tasks[splTaskName];
                        IKlQCMeasure meas = task.Measures["resp"];
                        if (meas.IsDataAvailable && meas.Curve?.Data != null)
                        {
                            double[,] respData = meas.Curve.Data;
                            sw.WriteLine("Frequency Response (Hz),Magnitude (dB)");
                            for (int i = 0; i < respData.GetLength(1); i++)
                            {
                                sw.WriteLine($"{respData[0, i]},{respData[1, i]}");
                            }
                            foreach (IKlQCLimit limit in meas.Limits)
                            {
                                if (limit.IsDataAvailable && limit.Curve?.Data != null)
                                {
                                    double[,] limitData = limit.Curve.Data;
                                    sw.WriteLine($"{limit.Name} (Hz),Magnitude (dB)");
                                    for (int i = 0; i < limitData.GetLength(1); i++)
                                    {
                                        sw.WriteLine($"{limitData[0, i]},{limitData[1, i]}");
                                    }
                                }
                            }
                        }
                    }
                }
                dbNode.ReleaseInstance();
                LogManager.AppendLog($"Data exported to {targetFile}");
            }
            catch (Exception ex)
            {
                LogManager.AppendLog($"Error exporting to CSV: {ex.Message}");
                MessageBox.Show($"Error exporting to CSV: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #region Existing Event Handlers
        private void AboutMenuItem_Click(object sender, EventArgs e)
        {
            FormAboutLAPx aboutForm = new FormAboutLAPx();
            aboutForm.ShowDialog();
        }

        private void ContactMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show("For support, please contact josh@thelyceum.io.", "Contact",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void LogWindowMenuItem_Click(object sender, EventArgs e)
        {
            string adminPassUser = Environment.GetEnvironmentVariable("LYCEUM_ADMIN_PASS", EnvironmentVariableTarget.User);
            string adminPassMachine = Environment.GetEnvironmentVariable("LYCEUM_ADMIN_PASS", EnvironmentVariableTarget.Machine);
            string adminPass = adminPassUser ?? adminPassMachine;

            Console.WriteLine($"Environment Variable LYCEUM_ADMIN_PASS: {(adminPass != null ? "Found" : "Not Found")}");
            Console.WriteLine($"LYCEUM_ADMIN_PASS Value: {adminPass}");

            if (adminPass == "LyceumAdmin2025")
            {
                LogManager.ShowLogWindow();
                Console.WriteLine("Log window opened successfully.");
            }
            else
            {
                Console.WriteLine("Access Denied: Invalid admin password.");
                MessageBox.Show($"Access Denied: Invalid admin password.\n\n"
                              + $"Environment Variable Found: {(adminPass != null ? "Yes" : "No")}\n"
                              + $"Environment Variable Value: {adminPass}",
                              "Permission Denied", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        #endregion

        #region Data Structures (Unchanged)
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
        #endregion

        #region LogManager and LogWindow (Unchanged)
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
            public TextBox logTextBox;

            public LogWindow()
            {
                this.Text = "Logs";
                this.Size = new Size(800, 600);
                this.BackColor = Color.FromArgb(45, 45, 45);
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
                    logTextBox.ScrollToCaret();
                }
            }
        }
        #endregion

        public TextBox GetLogTextBox()
        {
            return logTextBox;
        }
    }
}