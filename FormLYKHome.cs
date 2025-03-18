using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Drawing;
using System.IO;
using KLAUTOMATIONLib;

namespace LyceumKlippel
{
    public partial class LYKHome : BaseForm
    {
        private string accessToken;
        private string refreshToken;
        private TextBox logTextBox;
        private TreeView treeViewDatabase;
        private Button btnImportKdb;
        private Button btnExportCsv;
        private KlDatabase database;
        private string loadedFilePath;

        public LYKHome(string accessToken, string refreshToken)
        {
            this.accessToken = accessToken;
            this.refreshToken = refreshToken;
            LogManager.Initialize();
            InitializeComponent();
            InitializeControls();

            // Set form size and position
            this.Size = new Size(800, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
        }

        private void InitializeComponent()
        {
            // Add any additional form initialization if needed
        }

        /// <summary>
        /// Initialize additional controls: TreeView, Import Button, and Export Button
        /// </summary>
        private void InitializeControls()
        {
            int menuHeight = menuStrip != null ? menuStrip.Height : 0;

            // Initialize TreeView to display database structure
            treeViewDatabase = new TreeView
            {
                Location = new Point(10, 50 + menuHeight),
                Size = new Size(300, 400)
            };
            this.Controls.Add(treeViewDatabase);

            // Initialize Button to import .kdb file
            btnImportKdb = new Button
            {
                Text = "Import .kdb File",
                Location = new Point(320, 50 + menuHeight)
            };
            btnImportKdb.Click += BtnImportKdb_Click;
            this.Controls.Add(btnImportKdb);

            // Initialize Button to export to CSV
            btnExportCsv = new Button
            {
                Text = "Export to CSV",
                Location = new Point(420, 50 + menuHeight),
                Enabled = false // Disabled until a file is loaded
            };
            btnExportCsv.Click += BtnExportCsv_Click;
            this.Controls.Add(btnExportCsv);
        }

        /// <summary>
        /// Event handler for the Import .kdb File button
        /// </summary>
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
                btnExportCsv.Enabled = true; // Enable export button after successful load
            }
        }

        /// <summary>
        /// Event handler for the Export to CSV button
        /// </summary>
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

        /// <summary>
        /// Load the .kdb or .kdbx database and populate the TreeView with its elements
        /// </summary>
        private void LoadDatabase(string filePath)
        {
            LogManager.AppendLog($"Starting to load database: {filePath}");
            try
            {
                // Create and open the database
                database = new KlDatabase();
                LogManager.AppendLog("Created KlDatabase instance.");
                database.Open(filePath);
                LogManager.AppendLog($"Opened database file: {filePath}");

                // Get the QC node (\QC\QC)
                KlDBNode qcNode = database.GetNode(@"\QC\QC");
                if (qcNode == null)
                {
                    LogManager.AppendLog("QC node not found in the database.");
                    MessageBox.Show("QC node not found in the database.", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                LogManager.AppendLog("QC node found.");

                // Check if the node contains QC data
                if (qcNode.TypeID == "{63AB89D5-AE84-11D5-B6D0-525405F7AE84}")
                {
                    LogManager.AppendLog("QC node contains QC data.");
                    // Load the QC module instance
                    IKlModuleQC qcModule = (IKlModuleQC)qcNode.LoadInstance();
                    LogManager.AppendLog("Loaded QC module instance.");

                    // Clear existing nodes and populate the TreeView
                    treeViewDatabase.Nodes.Clear();
                    LogManager.AppendLog("Cleared TreeView nodes.");
                    PopulateTreeView(qcModule, treeViewDatabase.Nodes);
                    LogManager.AppendLog("Populated TreeView with QC tasks and measures.");

                    // Release the module instance
                    qcNode.ReleaseInstance();
                    LogManager.AppendLog("Released QC module instance.");

                    LogManager.AppendLog($"Successfully loaded database: {filePath}");
                }
                else
                {
                    LogManager.AppendLog("Selected node does not contain QC data.");
                    MessageBox.Show("Selected node does not contain QC data.", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                LogManager.AppendLog($"Error loading database: {ex.GetType().Name} - {ex.Message}");
                if (ex.InnerException != null)
                {
                    LogManager.AppendLog($"Inner exception: {ex.InnerException.Message}");
                }
                MessageBox.Show($"Error loading database: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                btnExportCsv.Enabled = false;
            }
        }

        /// <summary>
        /// Recursively populate the TreeView with QC module tasks and measures
        /// </summary>
        private void PopulateTreeView(IKlModuleQC qcModule, TreeNodeCollection parentNodes)
        {
            foreach (IKlQCTask task in qcModule.Tasks)
            {
                TreeNode taskNode = new TreeNode(task.Name);
                parentNodes.Add(taskNode);

                foreach (IKlQCMeasure measure in task.Measures)
                {
                    TreeNode measureNode = new TreeNode(measure.Name);
                    taskNode.Nodes.Add(measureNode);
                }
            }
        }

        /// <summary>
        /// Convert QC data from the database to a CSV file
        /// </summary>
        private void ConvertKdbToCSV(KlDatabase kdb, string targetFile)
        {
            LogManager.AppendLog($"Starting CSV export to: {targetFile}");
            try
            {
                KlDBNode dbNode = kdb.GetNode(@"\QC\QC");
                if (dbNode == null)
                {
                    LogManager.AppendLog("QC node not found for export.");
                    return;
                }
                LogManager.AppendLog("QC node found for export.");

                if (dbNode.TypeID != "{63AB89D5-AE84-11D5-B6D0-525405F7AE84}")
                {
                    LogManager.AppendLog("Invalid QC node for export.");
                    return;
                }
                LogManager.AppendLog("QC node is valid for export.");

                IKlModuleQC qcModule = (IKlModuleQC)dbNode.LoadInstance();
                LogManager.AppendLog("Loaded QC module instance for export.");

                using (StreamWriter sw = File.CreateText(targetFile))
                {
                    sw.WriteLine("Measure/Limit Name,Value");
                    LogManager.AppendLog("Wrote CSV header.");

                    // Extract Re (input resistance) from Impedance task
                    if (qcModule.Tasks.Exists("Impedance"))
                    {
                        LogManager.AppendLog("Impedance task found.");
                        IKlQCTask task = qcModule.Tasks["Impedance"];
                        IKlQCMeasure meas = task.Measures["re"];
                        if (meas.IsDataAvailable)
                        {
                            double reValue = meas.ScalarValue;
                            sw.WriteLine($"Re,{reValue}");
                            LogManager.AppendLog($"Wrote Re value: {reValue}");

                            foreach (IKlQCLimit limit in meas.Limits)
                            {
                                if (limit.IsDataAvailable)
                                {
                                    sw.WriteLine($"{limit.Name},{limit.ScalarValue}");
                                    LogManager.AppendLog($"Wrote limit: {limit.Name} = {limit.ScalarValue}");
                                }
                            }
                        }
                        else
                        {
                            LogManager.AppendLog("Re measure data not available.");
                        }
                    }
                    else
                    {
                        LogManager.AppendLog("Impedance task not found.");
                    }

                    // Extract Frequency Response from Sound Pressure task
                    string splTaskName = null;
                    if (qcModule.Tasks.Exists("Sound Pressure (NI)"))
                    {
                        splTaskName = "Sound Pressure (NI)";
                        LogManager.AppendLog("Sound Pressure (NI) task found.");
                    }
                    else if (qcModule.Tasks.Exists("Sound Pressure"))
                    {
                        splTaskName = "Sound Pressure";
                        LogManager.AppendLog("Sound Pressure task found.");
                    }
                    else
                    {
                        LogManager.AppendLog("No Sound Pressure task found.");
                    }

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
                            LogManager.AppendLog("Wrote Frequency Response data.");

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
                                    LogManager.AppendLog($"Wrote limit curve: {limit.Name}");
                                }
                            }
                        }
                        else
                        {
                            LogManager.AppendLog("Frequency Response data not available.");
                        }
                    }
                }

                dbNode.ReleaseInstance();
                LogManager.AppendLog($"Data exported to {targetFile}");
            }
            catch (Exception ex)
            {
                LogManager.AppendLog($"Error exporting to CSV: {ex.Message}");
                MessageBox.Show($"Error exporting to CSV: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
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

    public class ProjectSession
    {
        public string Title { get; set; }
        public string Data { get; set; }
        public List<LYKHome.SignalPathData> SignalPaths { get; set; }
        public Dictionary<string, string> GlobalProperties { get; set; }
    }
}