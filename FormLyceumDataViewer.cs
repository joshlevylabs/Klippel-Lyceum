using System;
using System.Drawing;
using System.Windows.Forms;
using System.Net.Http;
using Newtonsoft.Json;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Net;
using System.Web;
using ClosedXML.Excel;
using System.IO;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms.DataVisualization.Charting;

namespace LAPxv8
{
    public partial class FormLyceumDataViewer : Form
    {
        private string accessToken;
        private readonly string refreshToken;
        private HttpClient httpClient;
        private ComboBox projectsComboBox;
        private ListBox projectDataListBox;
        private TextBox logTextBox;
        private TextBox requestDetailsTextBox;
        private Label projectsComboBoxTitle;
        private Label projectDataListBoxTitle;
        private Label logTextBoxTitle;
        private Label requestDetailsTextBoxTitle;
        private TextBox responseDetailsTextBox;
        private Label responseDetailsTextBoxTitle;
        private TextBox jsonPropertyDetailsTextBox;
        private Label jsonPropertyDetailsTextBoxTitle;
        private DataGridView fileParserDataGridView;
        private Label fileParserDataGridViewTitle;
        private Button MeasurementsButton;
        private Label limitsButton;
        private bool isFromJsonProperty = false;
        private TabControl sheetsTabControl;
        private Panel graphPanel;

        public FormLyceumDataViewer(string accessToken, string refreshToken)
        {
            this.accessToken = accessToken;
            this.refreshToken = refreshToken;

            var handler = new HttpClientHandler
            {
                Proxy = new WebProxy("http://127.0.0.1:8081", false),
                UseProxy = false,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true,
                AllowAutoRedirect = true // Enable automatic redirection
            };

            httpClient = new HttpClient(handler);

            InitializeComponent();
            Load += async (sender, e) => await FetchAndDisplayProjects();
        }
        private void InitializeComponent()
        {
            // Adjust the vertical spacing constant if needed
            int verticalSpacing = 25;

            // Projects ComboBox Title
            projectsComboBoxTitle = CreateLabel("Projects", new Point(10, 10));
            Controls.Add(projectsComboBoxTitle);

            // Projects ComboBox
            projectsComboBox = new ComboBox
            {
                Location = new Point(10, projectsComboBoxTitle.Bottom + 5), // Position below the title
                Size = new Size(280, 20),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            projectsComboBox.SelectedIndexChanged += ProjectsComboBox_SelectedIndexChanged;
            Controls.Add(projectsComboBox);

            // Measurements Button
            Button measurementsButton = new Button
            {
                Text = "Measurements",
                Location = new Point(120, projectsComboBox.Bottom + 10),
                Size = new Size(100, 30)
            };
            measurementsButton.Click += MeasurementsButton_Click;
            Controls.Add(measurementsButton);

            // Limits Button
            Button limitsButton = new Button
            {
                Text = "Limits",
                Location = new Point(230, projectsComboBox.Bottom + 10),
                Size = new Size(100, 30)
            };
            limitsButton.Click += LimitsButton_Click;
            Controls.Add(limitsButton);

            // Project Data ListBox Title
            projectDataListBoxTitle = CreateLabel("Project Data", new Point(10, projectsComboBox.Bottom + verticalSpacing));
            Controls.Add(projectDataListBoxTitle);

            // Project Data ListBox
            projectDataListBox = new ListBox
            {
                Location = new Point(10, projectDataListBoxTitle.Bottom + 5), // Position below the title
                Size = new Size(460, 300),
                ScrollAlwaysVisible = true
            };
            projectDataListBox.SelectedIndexChanged += ProjectDataListBox_SelectedIndexChanged;
            Controls.Add(projectDataListBox);

            // Request Details TextBox Title
            requestDetailsTextBoxTitle = CreateLabel("Request Details", new Point(480, 10));
            Controls.Add(requestDetailsTextBoxTitle);

            // Request Details TextBox
            requestDetailsTextBox = new TextBox
            {
                Location = new Point(480, requestDetailsTextBoxTitle.Bottom + 5), // Position below the title
                Size = new Size(460, 320),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true
            };
            Controls.Add(requestDetailsTextBox);

            // Log TextBox Title
            logTextBoxTitle = CreateLabel("Log", new Point(10, projectDataListBox.Bottom + verticalSpacing));
            Controls.Add(logTextBoxTitle);

            // Log TextBox
            logTextBox = new TextBox
            {
                Location = new Point(10, logTextBoxTitle.Bottom + 5), // Position below the title
                Size = new Size(460, 300),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true
            };
            Controls.Add(logTextBox);

            // Response Details TextBox Title
            responseDetailsTextBoxTitle = CreateLabel("Response Details", new Point(480, requestDetailsTextBox.Bottom + verticalSpacing));
            Controls.Add(responseDetailsTextBoxTitle);

            // Response Details TextBox
            responseDetailsTextBox = new TextBox
            {
                Location = new Point(480, responseDetailsTextBoxTitle.Bottom + 5),
                Size = new Size(460, 350),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true
            };
            Controls.Add(responseDetailsTextBox);

            // Add a placeholder for Graph
            // You will need to integrate a specific graphing library for actual visualization
            Label graphLabel = CreateLabel("Graph", new Point(960, verticalSpacing));
            Controls.Add(graphLabel);

            // Initialize graphPanel
            graphPanel = new Panel
            {
                Location = new Point(960, graphLabel.Bottom + 5),
                Size = new Size(460, 300),
                BorderStyle = BorderStyle.FixedSingle,
                Name = "graphPanel"  // Added name for reference
            };
            Controls.Add(graphPanel);

            jsonPropertyDetailsTextBoxTitle = CreateLabel("JSON Property Details", new Point(960, graphPanel.Bottom + 5));
            Controls.Add(jsonPropertyDetailsTextBoxTitle);

            // JSON Property Details TextBox
            jsonPropertyDetailsTextBox = new TextBox
            {
                Location = new Point(960, jsonPropertyDetailsTextBoxTitle.Bottom + 5),
                Size = new Size(460, 220),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true
            };
            Controls.Add(jsonPropertyDetailsTextBox);

            // File Parser Details TextBox Title
            fileParserDataGridViewTitle = CreateLabel("File Parser Details", new Point(960, jsonPropertyDetailsTextBox.Bottom + 5));
            Controls.Add(fileParserDataGridViewTitle);

            // File Parser Details DataGridView
            fileParserDataGridView = new DataGridView
            {
                Location = new Point(960, fileParserDataGridViewTitle.Bottom + 5),
                Size = new Size(460, 220),
                ReadOnly = true,
                AllowUserToAddRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            Controls.Add(fileParserDataGridView);

            // TabControl Initialization
            sheetsTabControl = new TabControl
            {
                Location = new Point(960, fileParserDataGridViewTitle.Bottom + 5), // Adjusted location
                Size = new Size(460, 220), // Adjusted size
                Visible = true // Ensure it's visible
            };
            Controls.Add(sheetsTabControl);

            // Form Setup
            Size = new Size(1500, logTextBox.Bottom + 60); // Adjust form size to fit content
            Text = "Lyceum Data Viewer";
        }
        private async void MeasurementsButton_Click(object sender, EventArgs e)
        {
            await FilterProjectsByDataType("measurement");
        }

        private async void LimitsButton_Click(object sender, EventArgs e)
        {
            await FilterProjectsByDataType("limit");
        }
        private async Task FilterProjectsByDataType(string dataType)
        {
            try
            {
                var response = await httpClient.GetAsync("https://api.thelyceum.io/api/project/");
                var content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var projects = JsonConvert.DeserializeObject<List<ProjectData>>(content);
                    var filteredProjects = projects.Where(p => p.data_type == dataType).ToList();

                    projectsComboBox.DataSource = filteredProjects;
                    projectsComboBox.DisplayMember = "Name";
                    projectsComboBox.ValueMember = "Id";
                }
                else
                {
                    Log("Failed to fetch projects. Status Code: " + response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                Log("Exception in FilterProjectsByDataType: " + ex.Message);
            }
        }


        private Label CreateLabel(string text, Point location)
        {
            return new Label
            {
                Text = text,
                Location = location,
                AutoSize = true
            };
        }
        private async Task FetchAndDisplayProjects()
        {
            Log("Fetching projects...");

            if (await IsTokenExpiredOrInvalid() && !await RefreshAccessToken())
            {
                Log("Failed to refresh access token.");
                return;
            }

            UpdateHttpClientAuthorizationHeader();

            try
            {
                var response = await httpClient.GetAsync("https://api.thelyceum.io/api/project/");
                var content = await response.Content.ReadAsStringAsync();

                Log("Response content: " + content); // Log raw response content

                if (response.IsSuccessStatusCode)
                {
                    var projects = JsonConvert.DeserializeObject<List<ProjectData>>(content);

                    projectsComboBox.DataSource = projects;
                    projectsComboBox.DisplayMember = "Name";
                    projectsComboBox.ValueMember = "Id";

                    // Populate projectDataListBox with file names from the first project, if any
                    if (projects.Any())
                    {
                        var firstProjectData = projects.First();
                        projectDataListBox.Items.Clear();
                        foreach (var cleanFile in firstProjectData.CleanFiles)
                        {
                            projectDataListBox.Items.Add(cleanFile.FileName);
                        }
                    }

                    Log("Projects loaded.");
                }
                else
                {
                    Log("Failed to fetch projects. Status Code: " + response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                Log("Exception in FetchAndDisplayProjects: " + ex.Message);
            }
        }
        private void ProjectDataListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (projectDataListBox.SelectedItem == null)
            {
                Log("No item selected in project data list box.");
                return;
            }

            if (projectsComboBox.SelectedItem == null)
            {
                Log("No project selected in projects combo box.");
                return;
            }

            if (projectsComboBox.SelectedItem is ProjectData selectedProject)
            {
                string selectedItem = projectDataListBox.SelectedItem.ToString();
                Log($"Selected item in project data list box: {selectedItem}");

                // Handle 'Back to Project Properties' selection
                if (isFromJsonProperty && selectedItem == "Back to Project Properties")
                {
                    RefreshProjectDataList(selectedProject);
                    isFromJsonProperty = false; // Reset the flag
                    return;
                }

                if (selectedItem.EndsWith(".csv") || selectedItem.EndsWith(".xlsx"))
                {
                    HandleFileSelection(selectedItem, selectedProject.CleanFiles);
                }
                else
                {
                    DisplaySelectedPropertyDetails(selectedItem, selectedProject);
                    isFromJsonProperty = true; // Set the flag when a property is selected
                }
            }
        }

        private void DisplaySelectedProperty(string property, ProjectData projectData)
        {
            var propertyInfo = projectData.GetType().GetProperty(property);
            if (propertyInfo == null)
            {
                requestDetailsTextBox.Text = "Property not found.";
                return;
            }

            var propertyValue = propertyInfo.GetValue(projectData);
            if (property == "clean_files" && propertyValue is List<CleanFile> files)
            {
                projectDataListBox.Items.Clear();
                projectDataListBox.Items.Add("Back");
                foreach (var file in files)
                {
                    projectDataListBox.Items.Add(file.FileName);
                }
            }
            else
            {
                // Handle other properties (if required)
                requestDetailsTextBox.Text = JsonConvert.SerializeObject(propertyValue, Formatting.Indented);
            }
        }

        private void DisplaySelectedPropertyDetails(string selectedItem, ProjectData projectData)
        {
            Log($"Attempting to display details for property: {selectedItem}");
            isFromJsonProperty = true;

            if (selectedItem == "clean_files")
            {
                DisplayCleanFilesDetails(projectData.CleanFiles);
            }
            else
            {
                // Deserialize the ProjectData to a JObject for better handling of nested properties
                JObject projectDataJson = JObject.FromObject(projectData);
                JToken selectedPropertyToken = projectDataJson.SelectToken(selectedItem);

                if (selectedPropertyToken != null)
                {
                    Log($"Property found: {selectedItem}");
                    jsonPropertyDetailsTextBox.Text = selectedPropertyToken.ToString();
                }
                else
                {
                    Log($"Property not found: {selectedItem}");
                    jsonPropertyDetailsTextBox.Text = "Property not found or not accessible.";
                }
            }
        }
        private void HandlePropertyOrFileSelection(string selectedItem, ProjectData selectedProject)
        {
            var propertyInfo = selectedProject.GetType().GetProperty(selectedItem);

            if (propertyInfo != null)
            {
                var propertyValue = propertyInfo.GetValue(selectedProject);
                if (propertyValue is IEnumerable<CleanFile> files)
                {
                    DisplayFileList(files);
                }
                else if (propertyValue is JToken token)
                {
                    DisplayJsonTokenContents(token);
                }
                else
                {
                    requestDetailsTextBox.Text = JsonConvert.SerializeObject(propertyValue, Formatting.Indented);
                }
            }
            else
            {
                requestDetailsTextBox.Text = "Property not found.";
            }
        }

        private void DisplayFileList(IEnumerable<CleanFile> files)
        {
            projectDataListBox.Items.Clear();
            projectDataListBox.Items.Add("Back");
            foreach (var file in files)
            {
                projectDataListBox.Items.Add(file.FileName);
            }
        }

        private void DisplayJsonTokenContents(JToken token)
        {
            projectDataListBox.Items.Clear();
            projectDataListBox.Items.Add("Back");

            if (token.Type == JTokenType.Object)
            {
                foreach (var child in token.Children<JProperty>())
                {
                    projectDataListBox.Items.Add(child.Name);
                }
            }
            else if (token.Type == JTokenType.Array)
            {
                foreach (var item in token.Children())
                {
                    projectDataListBox.Items.Add(item.ToString());
                }
            }
            else
            {
                projectDataListBox.Items.Add(token.ToString());
            }
        }

        private void HandleFileOrDynamicPropertySelection(string selectedItem, ProjectData projectData)
        {
            // Implement logic to handle file selection or dynamic property display
            // This is a placeholder implementation
            Log($"Selected item: {selectedItem}");
        }
        private void RefreshProjectDataList(ProjectData projectData)
        {
            projectDataListBox.Items.Clear();
            foreach (var property in projectData.GetType().GetProperties())
            {
                projectDataListBox.Items.Add(property.Name);
            }
        }
        private void DisplayCleanFilesDetails(List<CleanFile> cleanFiles)
        {
            projectDataListBox.Items.Clear();
            projectDataListBox.Items.Add("Back to Project Properties");

            foreach (var file in cleanFiles)
            {
                projectDataListBox.Items.Add(file.FileName);
            }
        }
        private async void HandleFileSelection(string fileName, List<CleanFile> cleanFiles)
        {
            var selectedFile = cleanFiles.FirstOrDefault(f => f.FileName == fileName);
            if (selectedFile != null)
            {
                string filePath;
                if (selectedFile.FileUrl.StartsWith("https://s3.us-west-1.amazonaws.com"))
                {
                    filePath = await DownloadFileFromS3(selectedFile.FileUrl, fileName);
                }
                else
                {
                    filePath = await DownloadFileContent(selectedFile.FileUrl, fileName);
                }

                if (string.IsNullOrEmpty(filePath))
                {
                    Log("File path is null or empty. File download may have failed.");
                    return;
                }

                Log($"File path obtained: {filePath}");

                if (selectedFile.ProjectFileType == "csv")
                {
                    var parsedData = ParseCsv(File.ReadAllText(filePath));
                    sheetsTabControl.Visible = false;
                    fileParserDataGridView.Visible = true; // Make the DataGridView visible
                    DisplayParsedDataInDataGridView(parsedData, fileParserDataGridView);
                }
                else if (selectedFile.ProjectFileType == "xlsx")
                {
                    fileParserDataGridView.Visible = false; // Hide the DataGridView for .xlsx files
                    DisplayXlsxData(filePath, sheetsTabControl); // Modified to pass in TabControl
                }
            }
            else
            {
                Log("Selected file not found in the clean files list.");
            }
        }
        private void DisplayXlsxData(string filePath, TabControl tabControl)
        {
            try
            {
                using (var workbook = new XLWorkbook(filePath))
                {
                    Log("Workbook loaded successfully.");
                    tabControl.TabPages.Clear();

                    foreach (var worksheet in workbook.Worksheets)
                    {
                        Log($"Processing worksheet: {worksheet.Name}");
                        var parsedData = ParseXlsxSheet(worksheet);
                        if (parsedData.Any())
                        {
                            tabControl.Visible = true; // Make sure the TabControl is visible
                            var tab = new TabPage(worksheet.Name);
                            var dataGridView = new DataGridView
                            {
                                Dock = DockStyle.Fill,
                                ReadOnly = true,
                                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                                AllowUserToAddRows = false
                            };

                            DisplayParsedDataInDataGridView(parsedData, dataGridView);
                            tab.Controls.Add(dataGridView);
                            tabControl.TabPages.Add(tab);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Error while processing .xlsx file: {ex.Message}");
            }
        }
        private void DisplayXlsxInTabs(string filePath, TabControl tabControl)
        {
            try
            {
                using (var workbook = new XLWorkbook(filePath))
                {
                    Log("Workbook loaded successfully.");

                    tabControl.TabPages.Clear();

                    foreach (var worksheet in workbook.Worksheets)
                    {
                        Log($"Processing worksheet: {worksheet.Name}");

                        var tab = new TabPage(worksheet.Name);
                        var dataGridView = new DataGridView
                        {
                            Dock = DockStyle.Fill,
                            ReadOnly = true,
                            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                            AllowUserToAddRows = false
                        };

                        var parsedData = ParseXlsxSheet(worksheet);
                        if (parsedData.Any())
                        {
                            DisplayParsedDataInDataGridView(parsedData, dataGridView);
                            tab.Controls.Add(dataGridView); // Ensure DataGridView is added to the tab
                            tabControl.TabPages.Add(tab); // Add the tab to TabControl
                        }
                        else
                        {
                            Log($"No data found in worksheet: {worksheet.Name}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Error while processing .xlsx file: {ex.Message}");
            }
        }

        private List<string[]> ParseXlsxSheet(IXLWorksheet worksheet)
        {
            var parsedData = new List<string[]>();
            var range = worksheet.RangeUsed();

            if (range != null)
            {
                foreach (var row in range.Rows())
                {
                    var rowData = row.Cells().Select(cell => cell.Value.ToString()).ToArray();
                    Log($"Row data: {string.Join(", ", rowData)}"); // Log each row's data
                    parsedData.Add(rowData);
                }
            }
            else
            {
                Log("No used range found in the worksheet.");
            }

            return parsedData;
        }

        private void DisplayParsedDataInDataGridView(List<string[]> data, DataGridView dataGridView = null)
        {
            dataGridView ??= fileParserDataGridView;

            dataGridView.Rows.Clear();
            dataGridView.Columns.Clear();

            if (data.Any())
            {
                // Add columns
                foreach (var header in data.First())
                {
                    dataGridView.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = header });
                }

                // Add rows
                foreach (var row in data.Skip(1)) // Skip header row
                {
                    dataGridView.Rows.Add(row);
                }

                // Once the data is displayed, update the graph
                UpdateGraph(dataGridView);
            }
            else
            {
                Log("No data to display.");
            }
        }

        // Modified DisplayParsedData method to accept a TextBox parameter
        private void DisplayParsedData(List<string[]> data, TextBox displayTextBox)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var row in data)
            {
                sb.AppendLine(string.Join(", ", row));
            }
            displayTextBox.Text = sb.ToString();
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

        private async Task<string> DownloadFileContent(string fileUrl, string fileName)
        {
            try
            {
                Log($"Attempting to download file from URL: {fileUrl}");

                using (var response = await httpClient.GetAsync(fileUrl))
                {
                    Log($"HTTP GET Response: Status Code: {response.StatusCode}, Reason: {response.ReasonPhrase}");

                    if (response.IsSuccessStatusCode)
                    {
                        byte[] fileBytes = await response.Content.ReadAsByteArrayAsync();
                        string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
                        File.WriteAllBytes(localPath, fileBytes);
                        Log($"File downloaded successfully to {localPath}");
                        return localPath;
                    }
                    else
                    {
                        Log($"Failed to download file. Status code: {response.StatusCode}. URL: {fileUrl}");
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Exception in DownloadFileContent: {ex.Message} for URL: {fileUrl}");
                return null;
            }
        }
        private async Task<string> DownloadFileFromS3(string fileUrl, string fileName)
        {
            try
            {
                var s3Client = new HttpClient();
                var response = await s3Client.GetAsync(fileUrl);

                if (response.IsSuccessStatusCode)
                {
                    byte[] fileBytes = await response.Content.ReadAsByteArrayAsync();
                    string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
                    File.WriteAllBytes(localPath, fileBytes);
                    return localPath;
                }
                else
                {
                    Log($"S3 Download Failed: {response.StatusCode}, {response.ReasonPhrase}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Log($"Exception in S3 download: {ex.Message}");
                return null;
            }
        }

        // This is a placeholder for displaying the parsed data
        private void DisplayParsedData(List<string[]> data)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var row in data)
            {
                sb.AppendLine(string.Join(", ", row));
            }
            requestDetailsTextBox.Text = sb.ToString();
        }

        private void UpdateHttpClientAuthorizationHeader()
        {
            // Clear previous headers to avoid duplication and conflicts
            httpClient.DefaultRequestHeaders.Clear();

            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            httpClient.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue { NoCache = true };
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("PostmanRuntime/7.32.3");
            httpClient.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
            httpClient.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("deflate"));
            httpClient.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("br"));

            Log("Default headers updated with new token.");
        }

        private async void ProjectsComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (projectsComboBox.SelectedItem is ProjectData selectedProject)
            {
                Log($"Selected project: {selectedProject.Name}");
                await FetchAndDisplayProjectData(selectedProject);
                UpdateRequestDetailsForProject(selectedProject.Id);
            }
        }
        private void UpdateRequestDetailsForProject(string projectId)
        {
            var requestDetails = new StringBuilder();
            requestDetails.AppendLine($"Request Method: GET");
            requestDetails.AppendLine($"Request URI: https://api.thelyceum.io/api/project/{projectId}");

            foreach (var header in httpClient.DefaultRequestHeaders)
            {
                requestDetails.AppendLine($"Header: {header.Key}: {string.Join(", ", header.Value)}");
            }

            requestDetailsTextBox.Text = requestDetails.ToString();
        }
        private async Task FetchAndDisplayProjectData(ProjectData projectData)
        {
            // Ensure HttpClient is not disposed between requests
            if (httpClient == null)
            {
                var handler = new HttpClientHandler
                {
                    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                    ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
                };
                httpClient = new HttpClient(handler);
                UpdateHttpClientAuthorizationHeader();
            }

            string requestUrl = $"https://api.thelyceum.io/api/project/{projectData.Id}/";
            LogHttpRequestDetails(new HttpRequestMessage(HttpMethod.Get, requestUrl));

            try
            {
                var response = await httpClient.GetAsync(requestUrl);
                var content = await response.Content.ReadAsStringAsync();
                LogResponseDetails(response, content);

                if (response.IsSuccessStatusCode)
                {
                    dynamic expandedProjectData = JsonConvert.DeserializeObject(content);
                    projectDataListBox.Items.Clear();
                    foreach (var property in ((JObject)expandedProjectData).Properties())
                    {
                        projectDataListBox.Items.Add(property.Name);
                    }
                }
                else
                {
                    Log($"Failed to retrieve data for project {projectData.Id}. Status Code: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Log($"Exception when fetching data for project {projectData.Id}: {ex.Message}");
            }

            projectDataListBox.Items.Insert(0, "Back to Project Properties");
        }

        private void LogHttpRequestDetails(HttpRequestMessage request)
        {
            Log("Preparing to send request:");
            Log($"Request Method: {request.Method}");
            Log($"Request URI: {request.RequestUri}");

            // Log headers just before sending the request
            foreach (var header in httpClient.DefaultRequestHeaders)
            {
                Log($"Header: {header.Key}: {string.Join(", ", header.Value)}");
            }
        }

        private void LogResponseDetails(HttpResponseMessage response, string content)
        {
            // Prepare the response details string
            var responseDetails = new StringBuilder();
            responseDetails.AppendLine($"Response Status Code: {response.StatusCode}");

            // Add headers to the response details
            foreach (var header in response.Headers)
            {
                responseDetails.AppendLine($"Header: {header.Key}: {string.Join(", ", header.Value)}");
            }

            // Add content headers if they are available
            if (response.Content?.Headers != null)
            {
                foreach (var header in response.Content.Headers)
                {
                    responseDetails.AppendLine($"Content Header: {header.Key}: {string.Join(", ", header.Value)}");
                }
            }

            // Append the actual content
            responseDetails.AppendLine();
            responseDetails.AppendLine("Response Content:");
            responseDetails.AppendLine(content);

            // Update the Response Details TextBox
            responseDetailsTextBox.Text = responseDetails.ToString();
        }
        private async Task<bool> IsTokenExpiredOrInvalid()
        {
            var testResponse = await httpClient.GetAsync("https://api.thelyceum.io/api/account/me/");
            return testResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized;
        }
        private async Task<bool> RefreshAccessToken()
        {
            var json = JsonConvert.SerializeObject(new { refresh = refreshToken });
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync("https://api.thelyceum.io/api/account/token/refresh", content);
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                dynamic tokenData = JsonConvert.DeserializeObject(responseContent);
                accessToken = tokenData.access; // Assuming 'access' is the correct token property

                // Immediately update headers with the new token
                UpdateHttpClientAuthorizationHeader();

                Log("Access token refreshed successfully.");
                return true;
            }
            else
            {
                Log($"Failed to refresh token. Status code: {response.StatusCode}");
                return false;
            }
        }

        private void UpdateGraph(DataGridView dataGridView)
        {
            // Clear existing series and chart areas
            graphPanel.Controls.Clear();
            Chart chart = new Chart
            {
                Dock = DockStyle.Fill
            };
            chart.ChartAreas.Add(new ChartArea());

            // Assuming the first column is the X-axis and the second column is the Y-axis
            Series series = new Series
            {
                ChartType = SeriesChartType.Line // You can change this to another chart type if needed
            };
            chart.Series.Add(series);

            // Add points to the series
            for (int i = 0; i < dataGridView.Rows.Count; i++)
            {
                if (dataGridView.Rows[i].Cells[0].Value != null && dataGridView.Rows[i].Cells[1].Value != null)
                {
                    double x = double.Parse(dataGridView.Rows[i].Cells[0].Value.ToString());
                    double y = double.Parse(dataGridView.Rows[i].Cells[1].Value.ToString());
                    series.Points.AddXY(x, y);
                }
            }

            // Add the chart to the panel
            graphPanel.Controls.Add(chart);
        }

        private void Log(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => Log(message)));
            }
            else
            {
                if (message.StartsWith("Response content: "))
                {
                    // Show only the first 100 characters of the response content
                    message = message.Substring(0, Math.Min(message.Length, 100)) + "...";
                }
                logTextBox.AppendText($"{DateTime.Now}: {message}{Environment.NewLine}");
            }
        }
        public class Project
        {
            public string Id { get; set; }
            public string Name { get; set; }
        }
        public class ProjectData
        {
            [JsonProperty("id")]
            public string Id { get; set; }

            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("tags")]
            public List<Tag> Tags { get; set; }

            [JsonProperty("groups")]
            public List<Group> Groups { get; set; }

            [JsonProperty("clean_files")]
            public List<CleanFile> CleanFiles { get; set; }

            // Add other properties based on the Postman response
            [JsonProperty("created_at")]
            public DateTime CreatedAt { get; set; }

            [JsonProperty("updated_at")]
            public DateTime UpdatedAt { get; set; }

            [JsonProperty("status")]
            public string Status { get; set; }

            [JsonProperty("file_type")]
            public string file_type { get; set; }

            [JsonProperty("data_type")]
            public string data_type { get; set; }

            [JsonProperty("metadata")]
            public Dictionary<string, object> Metadata { get; set; } // Add Metadata property if needed

            [JsonProperty("file")]
            public string File { get; set; }

            [JsonProperty("v2_json_file")]
            public string V2JsonFile { get; set; }
        }
        public class Tag
        {
            [JsonProperty("id")]
            public string Id { get; set; }

            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("slug")]
            public string Slug { get; set; }

            [JsonProperty("created_at")]
            public DateTime CreatedAt { get; set; }

            [JsonProperty("updated_at")]
            public DateTime UpdatedAt { get; set; }
        }
        public class Group
        {
            [JsonProperty("id")]
            public string Id { get; set; }

            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("group_name")]
            public string GroupName { get; set; }
        }
        public class MetadataItem
        {
            [JsonProperty("limits")]
            public List<string> Limits { get; set; }

            [JsonProperty("sheetName")]
            public string SheetName { get; set; }

            [JsonProperty("measurements")]
            public List<Measurement> Measurements { get; set; }
        }

        public class Measurement
        {
            [JsonProperty("title")]
            public string Title { get; set; }

            [JsonProperty("xUnit")]
            public string XUnit { get; set; }

            [JsonProperty("yUnit")]
            public string YUnit { get; set; }

            [JsonProperty("primary")]
            public string Primary { get; set; }

            [JsonProperty("dimension")]
            public string Dimension { get; set; }

            [JsonProperty("secondary")]
            public List<string> Secondary { get; set; }
        }

        public class CleanFile
        {
            [JsonProperty("file_name")]
            public string FileName { get; set; }

            [JsonProperty("file")]
            public string FileUrl { get; set; }

            [JsonProperty("project_file_type")]
            public string ProjectFileType { get; set; }
            // Add other properties if needed
        }
    }
}

