using System;
using System.Windows.Forms;
using System.Net.Http;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Drawing;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms.DataVisualization.Charting;
using System.Threading.Tasks;
using System.IO;
using ClosedXML.Excel;
using System.Net;
using AudioPrecision.API;
using System.Diagnostics;

namespace LAPxv8
{
    public partial class FormLyceumLimitImport : Form
    {
        private APx500 APx = new APx500();
        private readonly string accessToken;
        private readonly string refreshToken;
        private HttpClient httpClient;
        private ListBox limitsListBox;
        //private IContainer components = null;
        private TextBox logTextBox;
        private TextBox searchTextBox;
        private Button searchButton;
        private Chart limitChart;
        private Button importButton;
        private List<ProjectData> fetchedProjects = new List<ProjectData>();
        private Label graphTitle;
        private DataGridView csvDataGridView;
        private Label csvDataTitle;

        public delegate void LimitImportedHandler(List<string[]> limitData, bool applyToSpecificChannel, int selectedChannel);
        public event LimitImportedHandler LimitImported;
        private CheckBox applyToSpecificChannelCheckBox;
        private NumericUpDown channelNumericUpDown;
        private CheckBox applyToAllChannelsCheckBox;


        public FormLyceumLimitImport(string accessToken, string refreshToken)
        {
            this.accessToken = accessToken;
            this.refreshToken = refreshToken;

            InitializeComponent(); // Call designer-generated code
            InitializeLogTextBox(); // Initialize logTextBox
            InitializeHttpClient(); // Set up the HTTP client
            FetchAndDisplayLimits(); // Fetch the limits
        }

        private void InitializeLogTextBox()
        {
            logTextBox = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                Dock = DockStyle.Bottom, // Position it as per your design
                Height = 150,
                BackColor = Color.FromArgb(45, 45, 45), // Optional: match your dark theme
                ForeColor = Color.White // Optional: match your dark theme
            };
            this.Controls.Add(logTextBox); // Add to the form
        }

        private void ApplyToSpecificChannelCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            channelNumericUpDown.Enabled = applyToSpecificChannelCheckBox.Checked;
            if (applyToSpecificChannelCheckBox.Checked)
            {
                applyToAllChannelsCheckBox.Checked = false;
            }
        }

        private void ApplyToAllChannelsCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (applyToAllChannelsCheckBox.Checked)
            {
                applyToSpecificChannelCheckBox.Checked = false;
                channelNumericUpDown.Enabled = false;
            }
        }
        private void InitializeHttpClient()
        {
            try
            {
                httpClient = new HttpClient
                {
                    BaseAddress = new Uri("https://api.thelyceum.io/")
                };
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                Log("HttpClient initialized successfully.");
            }
            catch (Exception ex)
            {
                Log($"HttpClient initialization failed: {ex.Message}");
            }
        }

        private void Log(string message)
        {
            if (logTextBox != null)
            {
                logTextBox.AppendText($"{DateTime.Now}: {message}\r\n");
            }
            else
            {
                Debug.WriteLine($"{DateTime.Now}: {message}"); // Fallback to console/logging
            }
        }

        private void SearchButton_Click(object sender, EventArgs e)
        {
            string searchTerm = searchTextBox.Text.ToLower();
            var filteredLimits = fetchedProjects.Where(p => p.Name.ToLower().Contains(searchTerm) && p.data_type == "limit").ToList();
            DisplayLimits(filteredLimits);
        }
        private void DisplayLimits(List<ProjectData> limits)
        {
            limitsListBox.Items.Clear();
            foreach (var limit in limits)
            {
                limitsListBox.Items.Add(limit.Name);
            }
        }
        private async void FetchAndDisplayLimits()
        {
            try
            {
                Log("Attempting to fetch projects...");
                var response = await httpClient.GetAsync("https://api.thelyceum.io/api/project/");

                Log($"Response status: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    Log("Successful response received.");
                    var content = await response.Content.ReadAsStringAsync();
                    fetchedProjects = JsonConvert.DeserializeObject<List<ProjectData>>(content);
                    Log($"fetchedProjects populated. Count: {(fetchedProjects != null ? fetchedProjects.Count.ToString() : "null")}");

                    if (fetchedProjects == null)
                    {
                        Log("Deserialization returned null. Check the JSON structure.");
                        return;
                    }

                    if (!fetchedProjects.Any())
                    {
                        Log("fetchedProjects is empty.");
                        return;
                    }

                    Log($"Number of projects fetched: {fetchedProjects.Count}");
                    DisplayLimits(fetchedProjects.Where(p => p.data_type == "limit").ToList());
                    Log("Limits displayed in the ListBox.");
                }
                else
                {
                    Log($"Failed to fetch projects. Status code: {response.StatusCode}. Content: {await response.Content.ReadAsStringAsync()}");
                }
            }
            catch (Exception ex)
            {
                Log($"Error occurred in FetchAndDisplayLimits: {ex.Message}");
            }
        }

        // New method to fetch and parse CSV file, then display on graph
        private async Task FetchAndGraphCsvData(string csvFileUrl)
        {
            Log($"Fetching CSV data from: {csvFileUrl}");
            var filePath = await DownloadFileContent(csvFileUrl, "temp.csv");
            if (!string.IsNullOrEmpty(filePath))
            {
                var parsedData = ParseCsv(File.ReadAllText(filePath));
                Log("CSV data parsed successfully.");
                DisplayParsedDataInGridView(parsedData);
                DisplayParsedDataInGraph(parsedData);
            }
            else
            {
                Log("Failed to fetch CSV data.");
            }
        }
        private async Task<string> DownloadFileContent(string fileUrl, string fileName)
        {
            try
            {
                Log($"Attempting to download file from URL: {fileUrl}");

                using (WebClient client = new WebClient())
                {
                    string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
                    await client.DownloadFileTaskAsync(new Uri(fileUrl), localPath);
                    Log($"File downloaded successfully to {localPath}");
                    return localPath;
                }
            }
            catch (Exception ex)
            {
                Log($"Exception in DownloadFileContent: {ex.Message} for URL: {fileUrl}");
                return null;
            }
        }

        // Method to display parsed CSV data in DataGridView
        private void DisplayParsedDataInGridView(List<string[]> data)
        {
            Log("Displaying parsed data in DataGridView.");
            csvDataGridView.Rows.Clear();
            csvDataGridView.Columns.Clear();

            if (data.Any())
            {
                // Add columns
                foreach (var header in data.First())
                {
                    csvDataGridView.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = header });
                }

                // Add rows
                foreach (var row in data.Skip(1)) // Skip header row
                {
                    csvDataGridView.Rows.Add(row);
                }

                Log("CSV data displayed in DataGridView.");
            }
            else
            {
                Log("No CSV data to display.");
            }
            Log($"Rows added to DataGridView: {csvDataGridView.Rows.Count}");
        }

        private List<string[]> ParseCsv(string csvContent)
        {
            Log("Parsing CSV content.");
            var lines = csvContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var parsedData = new List<string[]>();
            foreach (var line in lines)
            {
                parsedData.Add(line.Split(','));
            }

            Log($"Parsed CSV rows: {parsedData.Count}");
            return parsedData;
        }
        private List<string[]> ParseXlsx(string filePath)
        {
            var parsedData = new List<string[]>();

            using (var workbook = new XLWorkbook(filePath))
            {
                var worksheet = workbook.Worksheet(1);
                var range = worksheet.RangeUsed();

                foreach (var row in range.Rows())
                {
                    var rowData = row.Cells().Select(cell => cell.Value.ToString()).ToArray();
                    parsedData.Add(rowData);
                }
            }

            return parsedData;
        }

        // Method to display parsed data on the graph
        private void DisplayParsedDataInGraph(List<string[]> data)
        {
            limitChart.Series.Clear();
            Series series = new Series
            {
                ChartType = SeriesChartType.Line,
                Name = "LimitSeries"
            };
            limitChart.Series.Add(series);

            foreach (var row in data.Skip(1)) // Skip header row
            {
                if (row.Length >= 2 && double.TryParse(row[0], out double x) && double.TryParse(row[1], out double y))
                {
                    series.Points.AddXY(x, y);
                }
            }

            Log("Graph updated with CSV data.");
        }

        private async void LimitsListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (limitsListBox.SelectedItem != null)
            {
                string selectedLimitName = limitsListBox.SelectedItem.ToString();
                Log($"Selected Limit: {selectedLimitName}, calling FetchAndDisplayLimitData");
                await FetchAndDisplayLimitData(selectedLimitName);
            }
        }
        private async Task FetchAndDisplayLimitData(string limitName)
        {
            Log($"FetchAndDisplayLimitData called. fetchedProjects is {(fetchedProjects != null ? "not null" : "null")}");

            if (fetchedProjects == null || !fetchedProjects.Any())
            {
                Log("fetchedProjects is null or empty in FetchAndDisplayLimitData.");
                return;
            }

            Log($"Fetching data for limit: {limitName}");

            var selectedLimit = fetchedProjects.FirstOrDefault(p => p.Name == limitName && p.data_type == "limit");

            if (selectedLimit == null)
            {
                Log($"Selected limit '{limitName}' not found in fetchedProjects.");
                return;
            }

            var requestUrl = $"https://api.thelyceum.io/api/project/{selectedLimit.Id}/";
            var response = await httpClient.GetAsync(requestUrl);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                Log($"Raw HTTP Response Content: {content}");

                try
                {
                    var projectData = JsonConvert.DeserializeObject<ProjectData>(content);
                    if (projectData == null)
                    {
                        Log("Deserialization of project data returned null.");
                        return;
                    }

                    Log($"Project Data Deserialized: {projectData.Name}");
                    Log($"Files Count: {projectData.clean_files?.Count ?? 0}");

                    foreach (var file in projectData.clean_files)
                    {
                        Log($"Found file: {file.FileName}, URL: {file.FileUrl}, Type: {file.ProjectFileType}");

                        if (file.ProjectFileType == "csv")
                        {
                            // Handle CSV file
                            var filePath = await DownloadFileContent(file.FileUrl, file.FileName);
                            if (!string.IsNullOrEmpty(filePath))
                            {
                                var parsedData = ParseCsv(File.ReadAllText(filePath));
                                DisplayParsedDataInGridView(parsedData);
                                DisplayParsedDataInGraph(parsedData);
                                Log($"CSV file processed: {file.FileName}");
                            }
                            else
                            {
                                Log($"Failed to download and process CSV file: {file.FileName}");
                            }
                        }
                    }
                }
                catch (JsonException je)
                {
                    Log($"JSON Deserialization Exception: {je.Message}");
                }
            }
            else
            {
                Log($"Failed to retrieve data for limit {selectedLimit.Id}. Status Code: {response.StatusCode}");
            }
        }

        // Add method to download and process the selected file
        private async Task HandleFileSelection(CleanFile selectedFile)
        {
            if (selectedFile.ProjectFileType == "csv")
            {
                var filePath = await DownloadFileContent(selectedFile.FileUrl, selectedFile.FileName);
                var parsedData = ParseCsv(File.ReadAllText(filePath));
                DisplayParsedDataInGridView(parsedData);
                DisplayParsedDataInGraph(parsedData);
            }
            // Handle other file types as needed
        }
        private void ImportButton_Click(object sender, EventArgs e)
        {
            Log("Import button clicked. Starting data import.");
            List<string[]> limitData = new List<string[]>();

            foreach (DataGridViewRow row in csvDataGridView.Rows)
            {
                if (!row.IsNewRow)
                {
                    string[] rowData = row.Cells.Cast<DataGridViewCell>()
                                        .Select(cell => cell.Value?.ToString() ?? string.Empty)
                                        .ToArray();
                    Log($"Row data: {string.Join(", ", rowData)}");

                    // Ensure each row has at least 2 elements (for X and Y values)
                    if (rowData.Length >= 2)
                    {
                        limitData.Add(rowData);
                    }
                }
            }

            Log($"Total rows imported: {limitData.Count}");

            bool applyToSpecificChannel = applyToSpecificChannelCheckBox.Checked;
            int selectedChannel = applyToSpecificChannel ? (int)channelNumericUpDown.Value : -1;

            if (applyToAllChannelsCheckBox.Checked)
            {
                applyToSpecificChannel = false;
                selectedChannel = -1;
            }

            LimitImported?.Invoke(limitData, applyToSpecificChannel, selectedChannel);
            Log($"Data import invoked. Rows: {limitData.Count}");
        }

        public class ProjectData
        {
            // Define properties of ProjectData based on the JSON structure
            [JsonProperty("id")]
            public string Id { get; set; }

            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("data_type")]
            public string data_type { get; set; }

            [JsonProperty("clean_files")]
            public List<CleanFile> clean_files { get; set; }
        }
        public class DataPoint
        {
            public double X { get; set; }
            public double Y { get; set; }
        }
        public class CleanFile
        {
            [JsonProperty("id")]
            public string Id { get; set; }

            [JsonProperty("file_name")]
            public string FileName { get; set; }

            [JsonProperty("file")]
            public string FileUrl { get; set; }

            [JsonProperty("project_file_type")]
            public string ProjectFileType { get; set; }
        }

    }


    public class CsvHandler
    {
        private HttpClient httpClient;
        private DataGridView csvDataGridView;
        private Chart limitChart;
        private string accessToken;

        public CsvHandler(HttpClient httpClient, DataGridView dataGridView, Chart chart, string accessToken)
        {
            this.httpClient = httpClient;
            this.csvDataGridView = dataGridView;
            this.limitChart = chart;
            this.accessToken = accessToken;
        }

        public async Task FetchAndGraphCsvData(string csvFileUrl)
        {
            var filePath = await DownloadFileContent(csvFileUrl, "temp.csv");
            if (!string.IsNullOrEmpty(filePath))
            {
                var parsedData = ParseCsv(File.ReadAllText(filePath));
                DisplayParsedDataInGridView(parsedData);
                DisplayParsedDataInGraph(parsedData);
            }
        }

        private async Task<string> DownloadFileContent(string fileUrl, string fileName)
        {
            try
            {
                using (WebClient client = new WebClient())
                {
                    string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
                    await client.DownloadFileTaskAsync(new Uri(fileUrl), localPath);
                    return localPath;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to download file: {ex.Message}", "Download Error", MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                return null;
            }
        }

        private List<string[]> ParseCsv(string csvContent)
        {
            var lines = csvContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var parsedData = new List<string[]>();
            foreach (var line in lines)
            {
                parsedData.Add(line.Split(','));
            }
            return parsedData;
        }

        private void DisplayParsedDataInGridView(List<string[]> data)
        {
            csvDataGridView.Rows.Clear();
            if (data.Any())
            {
                // Assuming the first row contains headers
                foreach (var header in data.First())
                {
                    csvDataGridView.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = header });
                }
                // Add rows
                foreach (var row in data.Skip(1))
                {
                    csvDataGridView.Rows.Add(row);
                }
            }
        }

        private void DisplayParsedDataInGraph(List<string[]> data)
        {
            limitChart.Series.Clear();
            Series series = new Series
            {
                ChartType = SeriesChartType.Line,
                Name = "LimitSeries"
            };
            limitChart.Series.Add(series);

            foreach (var row in data.Skip(1)) // Skip header
            {
                if (row.Length >= 2 && double.TryParse(row[0], out double x) && double.TryParse(row[1], out double y))
                {
                    series.Points.AddXY(x, y);
                }
            }
        }
    }

}
