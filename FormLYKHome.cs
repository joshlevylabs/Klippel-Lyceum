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
using static LyceumKlippel.LYKHome;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.ComponentModel;
using ScottPlot;
using Newtonsoft.Json.Linq;
using System.Net.NetworkInformation;


namespace LyceumKlippel
{
    public partial class LYKHome : BaseForm
    {
        private APx500 APx = new APx500();
        private TreeView resultsTreeView;
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
        public Dictionary<string, string> globalProperties = new Dictionary<string, string>(); 



        // This should be the only declaration of SessionDataHandler
        public delegate void SessionDataHandler(ProjectSession newSession);

        public LYKHome(string accessToken, string refreshToken)
        {
            this.accessToken = accessToken;
            this.refreshToken = refreshToken;

            LogManager.Initialize(); // Initialize the log system

        }
        private void AboutMenuItem_Click(object sender, EventArgs e)
        {
            FormAboutLAPx aboutForm = new FormAboutLAPx();
            aboutForm.ShowDialog();
        }

        private void ContactMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show("For support, please contact josh@thelyceum.io.", "Contact", MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information);
        }
        public TextBox GetLogTextBox()
        {
            return logTextBox;
        }

        private void LogWindowMenuItem_Click(object sender, EventArgs e)
        {
            // Try retrieving from both User and Machine scopes
            string adminPassUser = Environment.GetEnvironmentVariable("LYCEUM_ADMIN_PASS", EnvironmentVariableTarget.User);
            string adminPassMachine = Environment.GetEnvironmentVariable("LYCEUM_ADMIN_PASS", EnvironmentVariableTarget.Machine);

            string adminPass = adminPassUser ?? adminPassMachine; // Prefer User-level if both exist

            // Debugging output to console
            Console.WriteLine($"Environment Variable LYCEUM_ADMIN_PASS: {(adminPass != null ? "Found" : "Not Found")}");
            Console.WriteLine($"LYCEUM_ADMIN_PASS Value: {adminPass}");

            // Check if the environment variable matches the required password
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
                                "Permission Denied", MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }
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
            public static void AppendLog(TextBox logTextBox, string message)
            {
                if (logTextBox.InvokeRequired)
                {
                    logTextBox.Invoke(new Action(() => logTextBox.AppendText(message + Environment.NewLine)));
                }
                else
                {
                    logTextBox.AppendText(message + Environment.NewLine);
                }
            }
        }
        public class LogWindow : Form
        {
            public TextBox logTextBox;
            public TextBox GetLogTextBox()
            {
                return logTextBox;
            }

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