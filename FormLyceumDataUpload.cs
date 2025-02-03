using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Runtime;
using Amazon;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System;
using static LAPxv8.FormAudioPrecision8;

namespace LAPxv8
{
    public partial class FormLyceumDataUpload : BaseForm
    {
        private string accessToken;
        private string refreshToken;
        private string sessionTitle;
        private string sessionData;
        private static readonly HttpClient client = new HttpClient(); // HttpClient instance
        private Dictionary<string, JObject> unitDetails = new Dictionary<string, JObject>();
        private Dictionary<string, string> unitMappings = new Dictionary<string, string>();

        public FormLyceumDataUpload(string accessToken, string refreshToken, string sessionTitle, string sessionData)
        {
            //InitializeComponents(); // Ensure components are initialized first
            this.accessToken = accessToken;
            this.refreshToken = refreshToken;
            this.sessionTitle = sessionTitle;
            this.sessionData = sessionData;

            // Start async initialization
            InitializeAsync(); // Ensure this is the only place it's called
        }

        private async void InitializeAsync()
        {
            if (IsDisposed)
                return;

            LogManager.AppendLog("Initializing upload form asynchronously.");
            await Task.Run(() => SaveDataToFile());
            await FetchAWSCredentials();
        }



        private string GetJsonFilePath()
        {
            string directoryPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Lyceum";
            string filePath = Path.Combine(directoryPath, "temp.json");
            return filePath;
        }

        private void SaveDataToFile()
        {
            try
            {
                string filePath = GetJsonFilePath();
                if (!Directory.Exists(Path.GetDirectoryName(filePath)))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                }
                File.WriteAllText(filePath, sessionData);
                LogManager.AppendLog($"Data saved to {filePath}");
            }
            catch (Exception ex)
            {
                LogManager.AppendLog($"Failed to save data: {ex.Message}");
            }

            LogManager.AppendLog($"Access Token: {accessToken}");
            LogManager.AppendLog($"Refresh Token: {refreshToken}");
        }

        private string GenerateUniqueFolderName()
        {
            return Guid.NewGuid().ToString();
        }

        private async Task FetchAWSCredentials()
        {
            try
            {
                string url = "https://api.thelyceum.io/api/account/get_aws_token/";
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                var response = await client.GetAsync(url);
                string responseContent = await response.Content.ReadAsStringAsync();

                if (IsDisposed)
                    return;

                LogManager.AppendLog($"HTTP Response Status: {response.StatusCode}");
                LogManager.AppendLog($"HTTP Response Headers: {response.Headers}");

                if (response.IsSuccessStatusCode)
                {
                    var awsTokenData = JObject.Parse(responseContent);
                    LogManager.AppendLog("Successfully fetched AWS credentials.");
                    LogManager.AppendLog($"AWS Access Key ID: {awsTokenData["Credentials"]["AccessKeyId"]}");
                    LogManager.AppendLog($"AWS Secret Access Key: {awsTokenData["Credentials"]["SecretAccessKey"]}");
                    LogManager.AppendLog($"AWS Session Token: {awsTokenData["Credentials"]["SessionToken"]}");

                    string projectName = PromptForProjectName();
                    if (!string.IsNullOrWhiteSpace(projectName))
                    {
                        ModifyJsonDataWithProjectName(projectName);
                        ModifyJsonData();
                        await UpdateDescriptorsWithUUIDs();

                        await ShowGroupSelectionForm();

                        await ProcessCheckedData();  // Process checked data right after appending group details

                        RemoveCheckedData();

                        string filePath = GetJsonFilePath();
                        string bucketName = "lyceum-prod";
                        string objectName = $"native-app-uploads/{sessionTitle}.json";
                        string accessKeyId = awsTokenData["Credentials"]["AccessKeyId"].ToString();
                        string secretAccessKey = awsTokenData["Credentials"]["SecretAccessKey"].ToString();
                        string sessionToken = awsTokenData["Credentials"]["SessionToken"].ToString();

                        await FetchAndStoreUnitDetails();  // This method should fetch the details and call StoreUnitDetails
                        await HandleUnmatchedUnits(); // Handle unmatched units
                        ReplaceUnitsInJson();

                        await UploadFileToS3(filePath, bucketName, accessKeyId, secretAccessKey, sessionToken, "us-west-1");
                    }
                    else
                    {
                        LogManager.AppendLog("No project name provided, cancelling operation.");
                    }
                }
                else
                {
                    LogManager.AppendLog("Failed to fetch AWS credentials.");
                    LogManager.AppendLog($"Response Content: {responseContent}");
                }
            }
            catch (Exception ex)
            {
                if (!IsDisposed)
                    LogManager.AppendLog($"Error fetching AWS credentials: {ex.Message}");
            }
        }

        private string PromptForProjectName()
        {
            using (BaseForm prompt = new BaseForm(false)) // No menu strip
            {
                prompt.Width = 600;
                prompt.Height = 250;
                prompt.BackColor = Color.FromArgb(30, 30, 30);
                prompt.ForeColor = Color.White;
                prompt.StartPosition = FormStartPosition.CenterScreen;
                prompt.Font = new Font("Segoe UI", 10);
                prompt.Text = "Enter Project Name"; // Window title

                int centerX = prompt.Width / 2;
                int paddingTop = 30; // Spacing adjustment

                // Title Label
                Label titleLabel = new Label()
                {
                    Left = centerX - 100,
                    Top = paddingTop,
                    Width = 200,
                    Text = "Enter Project Name",
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 12, FontStyle.Bold),
                    TextAlign = ContentAlignment.MiddleCenter
                };

                // Project Name Label
                Label textLabel = new Label()
                {
                    Left = centerX - 200,
                    Top = paddingTop + 50,
                    Width = 400,
                    Text = "Project Name:",
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 10, FontStyle.Bold),
                    TextAlign = ContentAlignment.MiddleLeft
                };

                // Project Name Input Box
                TextBox textBox = new TextBox()
                {
                    Left = centerX - 200,
                    Top = paddingTop + 80,
                    Width = 400,
                    BackColor = Color.FromArgb(50, 50, 50),
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 10)
                };

                // Confirmation Button (Centered & Larger)
                Button confirmation = new Button()
                {
                    Text = "OK",
                    Left = centerX - 50,
                    Width = 100,
                    Top = paddingTop + 130,
                    Height = 40, // Increased height for better UX
                    DialogResult = DialogResult.OK,
                    BackColor = Color.FromArgb(70, 70, 70),
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 10, FontStyle.Bold)
                };

                confirmation.Click += (sender, e) => { prompt.Close(); };

                // Add Controls
                prompt.Controls.Add(titleLabel);
                prompt.Controls.Add(textLabel);
                prompt.Controls.Add(textBox);
                prompt.Controls.Add(confirmation);
                prompt.AcceptButton = confirmation;

                return prompt.ShowDialog() == DialogResult.OK ? textBox.Text : null;
            }
        }

        private void ModifyJsonDataWithProjectName(string projectName)
        {
            try
            {
                var jsonData = JObject.Parse(sessionData);
                jsonData["ProjectName"] = projectName;
                sessionData = jsonData.ToString(Newtonsoft.Json.Formatting.Indented);

                File.WriteAllText(GetJsonFilePath(), sessionData);
                LogManager.AppendLog($"Updated data saved to {GetJsonFilePath()} with Project Name: {projectName}");
            }
            catch (Exception ex)
            {
                LogManager.AppendLog($"Error updating JSON data: {ex.Message}");
            }
        }

        private void ModifyJsonData()
        {
            try
            {
                var jsonData = JObject.Parse(sessionData);
                var properties = jsonData["GlobalProperties"].ToObject<JObject>();
                jsonData.Remove("GlobalProperties"); // Remove the old section

                // Rename to Descriptors and remove specified keys
                var descriptors = new JObject();
                foreach (var prop in properties.Properties())
                {
                    if (!new string[] { "SequenceName", "ProjectDir", "APxDir", "Date", "Time", "Day", "Month", "Year", "Hour", "Minute", "Second", "Millisecond" }.Contains(prop.Name))
                    {
                        descriptors.Add(prop.Name, prop.Value);
                    }
                }
                jsonData["descriptor"] = descriptors; // Add the modified section

                sessionData = jsonData.ToString(Newtonsoft.Json.Formatting.Indented); // Update the session data
                File.WriteAllText(GetJsonFilePath(), sessionData); // Save the changes back to file
                LogManager.AppendLog("JSON data modified successfully.");
            }
            catch (Exception ex)
            {
                LogManager.AppendLog($"Error modifying JSON data: {ex.Message}");
            }
        }

        private async Task UpdateDescriptorsWithUUIDs()
        {
            string url = "https://api.thelyceum.io/api/project/metadata/";
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            var response = await client.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                string responseContent = await response.Content.ReadAsStringAsync();
                var descriptorsFromAPI = JObject.Parse(responseContent)["descriptor"].ToObject<List<JObject>>();
                var jsonData = JObject.Parse(sessionData);
                var properties = jsonData["descriptor"].ToObject<JObject>();
                JArray descriptorArray = new JArray();

                foreach (var prop in properties.Properties())
                {
                    var matchedDescriptor = descriptorsFromAPI.FirstOrDefault(d => d["label"].ToString() == prop.Name);
                    if (matchedDescriptor != null)
                    {
                        JObject descriptorObject = new JObject
                        {
                            {"label", prop.Value.ToString()}, // Setting label to the input value
                            {"value", matchedDescriptor["value"].ToString()} // Using 'value' instead of 'UUID'
                        };
                        descriptorArray.Add(descriptorObject);
                    }
                    else
                    {
                        LogManager.AppendLog($"Descriptor '{prop.Name}' does not exist in Lyceum metadata and will not be included.");
                    }
                }

                jsonData["descriptor"] = descriptorArray; // Apply the updated structure to the JSON
                sessionData = jsonData.ToString(Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(GetJsonFilePath(), sessionData);
                LogManager.AppendLog("Descriptors updated with new format and saved to file.");
            }
            else
            {
                LogManager.AppendLog($"Failed to fetch descriptor UUIDs from Lyceum. Status: {response.StatusCode}. Response: {await response.Content.ReadAsStringAsync()}");
            }
        }

        private async Task ShowGroupSelectionForm()
        {
            string url = "https://api.thelyceum.io/api/organization/groups/";
            LogManager.AppendLog($"Request sent to: {url}");
            LogManager.AppendLog($"Using token: {accessToken.Substring(0, 15)}...");  // LogManager.AppendLog part of the token for security

            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            try
            {
                var response = await client.GetAsync(url);
                LogManager.AppendLog($"Response Status: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    string responseContent = await response.Content.ReadAsStringAsync();
                    var groups = JArray.Parse(responseContent);

                    var selectedGroup = await PromptUserForGroupSelection(groups);
                    if (selectedGroup != null)
                    {
                        await AppendGroupDetails(selectedGroup);
                    }
                    else
                    {
                        LogManager.AppendLog("No group selected.");
                    }
                }
                else
                {
                    LogManager.AppendLog("Failed to fetch groups - Unauthorized or endpoint error");
                }
            }
            catch (Exception ex)
            {
                LogManager.AppendLog($"Error during group details fetch: {ex.Message}");
            }
        }

        private async Task<JObject> PromptUserForGroupSelection(JArray groups)
        {
            var tcs = new TaskCompletionSource<JObject>();

            using (BaseForm prompt = new BaseForm(false)) // No menu strip
            {
                prompt.Width = 600;
                prompt.Height = 520;
                prompt.BackColor = Color.FromArgb(30, 30, 30);
                prompt.ForeColor = Color.White;
                prompt.StartPosition = FormStartPosition.CenterScreen;
                prompt.Font = new Font("Segoe UI", 10);
                prompt.Text = "Select Group";

                int paddingTop = 40; // Move elements downward

                // Title Labels
                Label searchTitle = new Label()
                {
                    Left = 50,
                    Top = paddingTop + 10, // Adjusted downward
                    Width = 500,
                    Text = "Search for a Group:",
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 10, FontStyle.Bold)
                };

                Label listTitle = new Label()
                {
                    Left = 50,
                    Top = paddingTop + 80, // Adjusted downward
                    Width = 500,
                    Text = "Matching Groups:",
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 10, FontStyle.Bold)
                };

                // Search Box
                TextBox searchBox = new TextBox()
                {
                    Left = 50,
                    Top = paddingTop + 40, // Adjusted downward
                    Width = 500,
                    BackColor = Color.FromArgb(50, 50, 50),
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 10)
                };

                // Dropdown List (ListBox)
                ListBox listBox = new ListBox()
                {
                    Left = 50,
                    Top = paddingTop + 110, // Adjusted downward
                    Width = 500,
                    Height = 200,
                    BackColor = Color.FromArgb(50, 50, 50),
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 10)
                };

                // Populate the ListBox initially
                var groupNames = groups.Select(g => g["name"].ToString()).ToList();
                listBox.Items.AddRange(groupNames.ToArray());

                // Search Functionality
                searchBox.TextChanged += (sender, e) =>
                {
                    string searchText = searchBox.Text.ToLower();
                    listBox.Items.Clear();
                    var filteredGroups = groupNames.Where(g => g.ToLower().Contains(searchText)).ToArray();
                    listBox.Items.AddRange(filteredGroups);
                };

                // Confirmation Button (Larger Height for Better UX)
                Button confirmation = new Button()
                {
                    Text = "OK",
                    Left = 450,
                    Width = 100,
                    Top = paddingTop + 330, // Adjusted downward
                    Height = 40,  // Increased height
                    DialogResult = DialogResult.OK,
                    BackColor = Color.FromArgb(70, 70, 70),
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 10, FontStyle.Bold)
                };

                confirmation.Click += (sender, e) =>
                {
                    if (listBox.SelectedItem != null)
                    {
                        string selectedGroupName = listBox.SelectedItem.ToString();
                        var selectedGroup = groups.FirstOrDefault(g => g["name"].ToString() == selectedGroupName);
                        tcs.SetResult(selectedGroup as JObject);
                        prompt.Close();
                    }
                };

                // Add Controls
                prompt.Controls.Add(searchTitle);
                prompt.Controls.Add(searchBox);
                prompt.Controls.Add(listTitle);
                prompt.Controls.Add(listBox);
                prompt.Controls.Add(confirmation);

                prompt.AcceptButton = confirmation;
                prompt.ShowDialog();
            }

            return await tcs.Task;
        }

        private async Task AppendGroupDetails(JObject matchingGroup)
        {
            LogManager.AppendLog($"Group found: {matchingGroup["name"]}");
            LogManager.AppendLog($"Group ID: {matchingGroup["id"]}");
            LogManager.AppendLog($"Description: {matchingGroup["description"] ?? "No description provided."}");

            var jsonData = JObject.Parse(sessionData);
            jsonData["GroupDetails"] = new JObject
            {
                {"group_name", matchingGroup["name"].ToString()},
                {"group_id", matchingGroup["id"].ToString()}
            };

            sessionData = jsonData.ToString(Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(GetJsonFilePath(), sessionData);
        }

        private void ShowGroupSelectionPrompt(JArray groups)
        {
            using (BaseForm prompt = new BaseForm())
            {
                prompt.Width = 600;
                prompt.Height = 400;

                prompt.BackColor = Color.FromArgb(30, 30, 30);
                prompt.ForeColor = Color.White;
                this.StartPosition = FormStartPosition.CenterScreen; // Center the window

                prompt.Text = "Select Group";

                Label textLabel = new Label() { Left = 50, Top = 50, Text = "Search and select a group:" };
                TextBox searchBox = new TextBox() { Left = 50, Top = 80, Width = 500 };
                ListBox listBox = new ListBox() { Left = 50, Top = 110, Width = 500, Height = 200 };

                var filteredGroups = groups.Select(g => g["name"].ToString()).ToList();
                listBox.Items.AddRange(filteredGroups.ToArray());

                searchBox.TextChanged += (sender, e) =>
                {
                    string searchText = searchBox.Text.ToLower();
                    listBox.Items.Clear();
                    listBox.Items.AddRange(filteredGroups.Where(g => g.ToLower().Contains(searchText)).ToArray());
                };

                Button confirmation = new Button() { Text = "Ok", Left = 450, Width = 100, Top = 330, DialogResult = DialogResult.OK };
                confirmation.Click += (sender, e) => { prompt.Close(); };

                prompt.Controls.Add(searchBox);
                prompt.Controls.Add(listBox);
                prompt.Controls.Add(confirmation);
                prompt.Controls.Add(textLabel);
                prompt.AcceptButton = confirmation;

                if (prompt.ShowDialog() == DialogResult.OK && listBox.SelectedItem != null)
                {
                    string selectedGroupName = listBox.SelectedItem.ToString();
                    var selectedGroup = groups.FirstOrDefault(g => g["name"].ToString() == selectedGroupName);
                    if (selectedGroup != null)
                    {
                        AppendGroupDetails((JObject)selectedGroup);
                    }
                }
            }
        }

        private async Task ProcessCheckedData()
        {
            try
            {
                LogManager.AppendLog("Starting processing of checked data.");

                // Step 1: Setup and get initial data
                (string dataType, string deviceLabel) = SetupDataProcessing();

                // Step 2: Process the checked data into a new format
                var jsonData = JObject.Parse(sessionData);
                var checkedData = jsonData["CheckedData"] as JArray ?? new JArray();
                JArray processedCheckedData = ProcessCheckedDataCore(checkedData, dataType, deviceLabel);

                // Step 3: Update session data with processed information
                UpdateSessionData(processedCheckedData);

                LogManager.AppendLog("Checked data processed and saved to file.");
            }
            catch (Exception ex)
            {
                LogManager.AppendLog($"Error processing checked data: {ex.Message}");
            }
        }

        private (string dataType, string deviceLabel) SetupDataProcessing()
        {
            string dataType = PromptForDataType();  // Get the data type from the user
            string deviceLabel = GetDeviceLabel(JObject.Parse(sessionData));  // Retrieve the device label from descriptors
            return (dataType, deviceLabel);
        }
        private string PromptForDataType()
        {
            return "Measurement"; // Automatically select "Measurement" without user input
        }


        private JArray ProcessCheckedDataCore(JArray checkedData, string dataType, string deviceLabel)
        {
            JArray processedCheckedData = new JArray();

            foreach (JObject pathData in checkedData)
            {
                string pathName = pathData["Name"].ToString();
                foreach (JObject measurement in pathData["Measurements"])
                {
                    string measurementName = measurement["Name"].ToString();
                    foreach (JObject result in measurement["Results"])
                    {
                        JObject newResult = CreateResultObject(result, pathName, measurementName, dataType, deviceLabel);
                        processedCheckedData.Add(newResult);
                    }
                }
            }

            return processedCheckedData;
        }

        private JObject CreateResultObject(JObject result, string pathName, string measurementName, string dataType, string deviceLabel)
        {
            string resultName = result["Name"].ToString();
            string fullName = $"{pathName} - {measurementName} - {resultName}";

            JObject details = new JObject
            {
                ["Name"] = fullName,
                ["ResultValueType"] = result["ResultValueType"],
                ["ChannelCount"] = result["ChannelCount"],
                ["Passed"] = result["Passed"],
                ["data_types"] = dataType
            };

            JObject newResult = new JObject
            {
                ["Details"] = details,
                ["Units"] = new JObject(),
                ["Properties"] = new JObject(),
                ["Data"] = new JObject()
            };

            PopulateUnitsAndData(result, newResult, deviceLabel);
            PopulateProperties(result, newResult, deviceLabel);

            return newResult;
        }

        private void PopulateUnitsAndData(JObject result, JObject newResult, string deviceLabel)
        {
            // Populate Units
            if (result["ResultValueType"].ToString() == "XY Values")
            {
                newResult["Units"]["XUnit"] = result["XUnit"];
                newResult["Units"]["YUnit"] = result["YUnit"];
            }
            else if (result["ResultValueType"].ToString() == "Meter Values")
            {
                newResult["Units"]["MeterUnit"] = result["MeterUnit"];
            }

            // Populate Data and rename channels
            if (result["ResultValueType"].ToString() == "XY Values")
            {
                newResult["Data"]["XValues"] = result["XValues"];
                newResult["Data"]["YValuesPerChannel"] = new JObject();
                foreach (JProperty channel in result["YValuesPerChannel"])
                {
                    string newChannelName = $"{deviceLabel} - {channel.Name}";
                    newResult["Data"]["YValuesPerChannel"][newChannelName] = channel.Value;
                }
            }
            else if (result["ResultValueType"].ToString() == "Meter Values")
            {
                int channelCount = (int)result["ChannelCount"];
                if (result["MeterValues"] is JArray meterValues && meterValues.Count == channelCount)
                {
                    for (int i = 0; i < channelCount; i++)
                    {
                        string channelLabel = $"Ch{i + 1}";
                        string newChannelName = $"{deviceLabel} - {channelLabel}";
                        newResult["Data"][newChannelName] = meterValues[i];
                    }
                }
            }
        }

        private void PopulateProperties(JObject sourceResult, JObject targetResult, string deviceLabel)
        {
            var properties = new JObject();
            var elements = new JArray();

            // Define a list of property keys to exclude from the "Properties" section
            var excludedProperties = new HashSet<string> { "Index", "SignalPathIndex", "MeasurementIndex", "YValuesPerChannel", "MeasurementType" };

            foreach (var property in sourceResult.Properties())
            {
                // Check if the property is not in the excluded list and also not in other defined keys to be excluded
                if (!excludedProperties.Contains(property.Name) &&
                    !new[] { "Name", "ResultValueType", "ChannelCount", "Passed", "XValues", "YValues", "MeterValues", "Measurements", "Results", "XUnit", "YUnit", "MeterUnit", "Data" }.Contains(property.Name))
                {
                    if (!string.IsNullOrEmpty(property.Value.ToString()))
                    {
                        // Modify the channel keys with deviceLabel
                        if (property.Name == "ChannelPassFail" || property.Name == "SerialNumbers")
                        {
                            var modifiedChannelValues = new JObject();
                            foreach (var channelProperty in property.Value.ToObject<JObject>().Properties())
                            {
                                var newChannelName = $"{deviceLabel} - {channelProperty.Name}";
                                modifiedChannelValues[newChannelName] = channelProperty.Value;
                            }

                            var element = new JObject
                            {
                                ["Element name"] = property.Name,
                                ["Dimensions"] = sourceResult["ResultValueType"],
                                ["DataType"] = "Boolean",
                                ["Data"] = modifiedChannelValues,
                                ["Units"] = new JObject() // Initialize Units to be filled later
                            };

                            properties["Name"] = property.Name;
                            elements.Add(element);
                        }
                        else
                        {
                            properties[property.Name] = property.Value;
                        }
                    }
                }
            }

            properties["Elements"] = elements;
            targetResult["Properties"] = properties;

            // Populate Units for Properties
            foreach (JObject element in elements)
            {
                JObject units = new JObject();
                foreach (JProperty unitProperty in targetResult["Units"])
                {
                    if (unitDetails.TryGetValue(unitProperty.Value.ToString(), out var unitDetail))
                    {
                        units[unitProperty.Name] = unitDetail;
                    }
                }
                element["Units"] = units;
            }
        }

        private async Task UpdateSessionData(JArray processedData)
        {
            var jsonData = JObject.Parse(sessionData);
            jsonData["ProcessedCheckedData"] = processedData;
            sessionData = jsonData.ToString(Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(GetJsonFilePath(), sessionData);

            // Fetch and LogManager.AppendLog unit metadata before final log message
            await FetchAndLogUnitMetadata();

            LogManager.AppendLog("Checked data processed, units logged, and saved to file.");
        }

        private string GetDeviceLabel(JObject jsonData)
        {
            string deviceLabel = "";
            foreach (JObject descriptor in jsonData["descriptor"])
            {
                if (descriptor["value"].ToString() == "9f7da84a-14f3-4407-90e2-40aad2ba81cb")
                {
                    deviceLabel = descriptor["label"].ToString();
                    break;
                }
            }
            return deviceLabel;
        }

        private async Task FetchAndLogUnitMetadata()
        {
            try
            {
                string url = "https://api.thelyceum.io/api/project/metadata/";
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                var response = await client.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    string responseContent = await response.Content.ReadAsStringAsync();
                    var metadata = JObject.Parse(responseContent);

                    var xUnits = metadata["x_units"].Children<JObject>().ToList();
                    var yUnits = metadata["y_units"].Children<JObject>().ToList();

                    var jsonData = JObject.Parse(sessionData);
                    HashSet<string> allUsedUnits = new HashSet<string>();
                    List<JObject> matchedXUnits = new List<JObject>();
                    List<JObject> matchedYUnits = new List<JObject>();

                    // Local function to combine magnitude symbol and symbol
                    string GetFullSymbol(JObject unit) => $"{unit["magnitude_symbol"]}{unit["symbol"]}";

                    if (jsonData.TryGetValue("ProcessedCheckedData", out JToken checkedData))
                    {
                        foreach (JObject item in checkedData)
                        {
                            string xUnit = item.SelectToken("Units.XUnit")?.ToString();
                            string meterUnit = item.SelectToken("Units.MeterUnit")?.ToString();
                            string yUnit = item.SelectToken("Units.YUnit")?.ToString();

                            // Add to all used units set
                            if (xUnit != null) allUsedUnits.Add(xUnit);
                            if (meterUnit != null) allUsedUnits.Add(meterUnit);
                            if (yUnit != null) allUsedUnits.Add(yUnit);

                            // Check for matches based on label or combined magnitude symbol and symbol
                            matchedXUnits.AddRange(xUnits.Where(x => x["label"].ToString() == xUnit || GetFullSymbol(x) == xUnit || x["label"].ToString() == meterUnit || GetFullSymbol(x) == meterUnit));
                            matchedYUnits.AddRange(yUnits.Where(y => y["label"].ToString() == yUnit || GetFullSymbol(y) == yUnit));
                        }

                        // Logging matched X Units
                        LogManager.AppendLog("Matched X Units:");
                        foreach (var unit in matchedXUnits.Distinct())
                        {
                            LogManager.AppendLog($"{unit["label"]}: {unit.ToString(Newtonsoft.Json.Formatting.None)}");
                        }

                        // Logging matched Y Units
                        LogManager.AppendLog("Matched Y Units:");
                        foreach (var unit in matchedYUnits.Distinct())
                        {
                            LogManager.AppendLog($"{unit["label"]}: {unit.ToString(Newtonsoft.Json.Formatting.None)}");
                        }

                        // LogManager.AppendLog unmatched units
                        var matchedUnitLabelsAndSymbols = new HashSet<string>(matchedXUnits.Concat(matchedYUnits).SelectMany(u => new[] { u["label"].ToString(), GetFullSymbol(u) }).Where(s => s != null));
                        var unmatchedUnits = allUsedUnits.Except(matchedUnitLabelsAndSymbols);

                        if (unmatchedUnits.Any())
                        {
                            LogManager.AppendLog("Unmatched Units in JSON Data:");
                            foreach (string unit in unmatchedUnits)
                            {
                                LogManager.AppendLog(unit);
                            }
                        }
                    }
                }
                else
                {
                    LogManager.AppendLog($"Failed to fetch units metadata. Status: {response.StatusCode}. Response: {await response.Content.ReadAsStringAsync()}");
                }
            }
            catch (Exception ex)
            {
                LogManager.AppendLog($"Error fetching units metadata: {ex.Message}");
            }
        }

        private async Task FetchAndStoreUnitDetails()
        {
            string url = "https://api.thelyceum.io/api/project/metadata/"; // Assuming this is the URL to fetch unit metadata
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            var response = await client.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                string responseContent = await response.Content.ReadAsStringAsync();
                var metadata = JObject.Parse(responseContent);

                var xUnits = metadata["x_units"].Children<JObject>().ToList();
                var yUnits = metadata["y_units"].Children<JObject>().ToList();

                StoreUnitDetails(xUnits, yUnits);
                LogManager.AppendLog("Unit details fetched and stored successfully.");
            }
            else
            {
                LogManager.AppendLog($"Failed to fetch unit details. Status: {response.StatusCode}. Response: {await response.Content.ReadAsStringAsync()}");
            }
        }

        private void StoreUnitDetails(List<JObject> xUnits, List<JObject> yUnits)
        {
            foreach (var unit in xUnits.Concat(yUnits))
            {
                string unitLabel = unit["label"].ToString();
                string symbol = unit["symbol"]?.ToString() ?? "";
                string magnitudeSymbol = unit["magnitude_symbol"]?.ToString() ?? "";
                string combinedKey = (magnitudeSymbol + symbol).Trim();

                if (!unitDetails.ContainsKey(unitLabel))
                    unitDetails[unitLabel] = unit;
                if (!string.IsNullOrEmpty(combinedKey) && !unitDetails.ContainsKey(combinedKey))
                    unitDetails[combinedKey] = unit;
            }
        }
        private void ReplaceUnitsInJson()
        {
            var jsonData = JObject.Parse(sessionData);
            var processedData = jsonData["ProcessedCheckedData"] as JArray;

            if (processedData != null)
            {
                foreach (JObject item in processedData)
                {
                    JObject units = item["Units"] as JObject;
                    if (units != null)
                    {
                        ReplaceUnitWithDetails(units, "XUnit");
                        ReplaceUnitWithDetails(units, "YUnit");
                        ReplaceUnitWithDetails(units, "MeterUnit");
                    }

                    // Also update the "Units" section in the "Elements" portion of the "Properties" section
                    JObject properties = item["Properties"] as JObject;
                    if (properties != null && properties["Elements"] is JArray elements)
                    {
                        foreach (JObject element in elements)
                        {
                            JObject elementUnits = new JObject();
                            if (units != null)
                            {
                                foreach (var unit in units.Properties())
                                {
                                    // Assign only the UUID to the Units section of the elements
                                    elementUnits[unit.Name] = unit.Value["value"].ToString();
                                }
                            }
                            element["Units"] = elementUnits;
                        }
                    }
                }
            }

            sessionData = jsonData.ToString(Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(GetJsonFilePath(), sessionData);
            LogManager.AppendLog("Units in JSON data replaced with detailed information and copied to Elements section.");
        }

        private void ReplaceUnitWithDetails(JObject units, string unitKey)
        {
            string unitValue = units[unitKey]?.ToString();
            if (!string.IsNullOrEmpty(unitValue))
            {
                if (unitMappings.TryGetValue(unitValue, out string selectedUnit))
                {
                    unitValue = selectedUnit;
                }

                if (unitDetails.TryGetValue(unitValue, out JObject unitDetail))
                {
                    units[unitKey] = unitDetail; // Replace with detailed unit information
                    LogManager.AppendLog($"Unit {unitKey} replaced with detailed information.");
                }
                else
                {
                    LogManager.AppendLog($"No detailed information found for unit {unitKey} with value {unitValue}.");
                }
            }
        }

        private async Task HandleUnmatchedUnits()
        {
            var unmatchedUnits = GetUnmatchedUnits();
            if (unmatchedUnits.Any())
            {
                await PromptUserForUnitMappings(unmatchedUnits);
                ReplaceUnitsInJson(); // Re-run the unit replacement after user provides mappings
            }
        }

        private List<string> GetUnmatchedUnits()
        {
            var jsonData = JObject.Parse(sessionData);
            var processedData = jsonData["ProcessedCheckedData"] as JArray;
            HashSet<string> allUsedUnits = new HashSet<string>();

            if (processedData != null)
            {
                foreach (JObject item in processedData)
                {
                    string xUnit = item.SelectToken("Units.XUnit")?.ToString();
                    string meterUnit = item.SelectToken("Units.MeterUnit")?.ToString();
                    string yUnit = item.SelectToken("Units.YUnit")?.ToString();

                    if (xUnit != null) allUsedUnits.Add(xUnit);
                    if (meterUnit != null) allUsedUnits.Add(meterUnit);
                    if (yUnit != null) allUsedUnits.Add(yUnit);
                }
            }

            var matchedUnitLabelsAndSymbols = new HashSet<string>(unitDetails.Keys);
            return allUsedUnits.Except(matchedUnitLabelsAndSymbols).ToList();
        }
        private async Task PromptUserForUnitMappings(List<string> unmatchedUnits)
        {
            var availableUnits = unitDetails.Keys.ToList();

            foreach (var unmatchedUnit in unmatchedUnits)
            {
                string selectedUnit = await ShowUnitSelectionPrompt(unmatchedUnit, availableUnits);
                if (!string.IsNullOrEmpty(selectedUnit))
                {
                    unitMappings[unmatchedUnit] = selectedUnit;
                }
            }
        }

        private Task<string> ShowUnitSelectionPrompt(string unmatchedUnit, List<string> availableUnits)
        {
            var tcs = new TaskCompletionSource<string>();

            using (BaseForm prompt = new BaseForm(false)) // Ensure menu strip is disabled
            {
                prompt.Width = 500;
                prompt.Height = 450; // Increased height for better spacing
                prompt.BackColor = Color.FromArgb(30, 30, 30);
                prompt.ForeColor = Color.White;
                prompt.StartPosition = FormStartPosition.CenterScreen;
                prompt.Font = new Font("Segoe UI", 10); // Apply consistent font
                prompt.Text = "Select Matching Unit";

                // Title Label
                Label titleLabel = new Label
                {
                    Left = 50,
                    Top = 50, // Adjusted to be lower and centered
                    Width = 400,
                    Text = $"Select a matching unit for: {unmatchedUnit}",
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 10, FontStyle.Bold) // Consistent font
                };

                // Search Box
                TextBox searchBox = new TextBox
                {
                    Left = 50,
                    Top = 80, // Lowered to avoid overlapping
                    Width = 400,
                    BackColor = Color.FromArgb(50, 50, 50),
                    ForeColor = Color.White
                };

                // Dropdown List
                ComboBox comboBox = new ComboBox
                {
                    Left = 50,
                    Top = 120, // Lowered for better spacing
                    Width = 400,
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    BackColor = Color.FromArgb(50, 50, 50),
                    ForeColor = Color.White
                };
                comboBox.Items.AddRange(availableUnits.ToArray());

                // Adjust the search box to filter results dynamically
                searchBox.TextChanged += (sender, e) =>
                {
                    string searchText = searchBox.Text.ToLower();
                    var filteredUnits = availableUnits.Where(u => u.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
                    comboBox.Items.Clear();
                    comboBox.Items.AddRange(filteredUnits);
                };

                // Confirmation Button (Larger, properly spaced)
                Button confirmation = new Button
                {
                    Text = "OK",
                    Left = 350,
                    Width = 100,
                    Height = 40, // Increased height for better visibility
                    Top = 180, // Lowered to avoid overlap
                    DialogResult = DialogResult.OK,
                    BackColor = Color.FromArgb(70, 70, 70),
                    ForeColor = Color.White
                };

                confirmation.Click += (sender, e) =>
                {
                    tcs.SetResult(comboBox.SelectedItem?.ToString());
                    prompt.Close();
                };

                // Add Controls
                prompt.Controls.Add(titleLabel);
                prompt.Controls.Add(searchBox);
                prompt.Controls.Add(comboBox);
                prompt.Controls.Add(confirmation);
                prompt.AcceptButton = confirmation;

                prompt.ShowDialog();
            }

            return tcs.Task;
        }

        private string GetUnitDetails(string unit)
        {
            if (unitDetails.TryGetValue(unit, out var details))
            {
                return details.ToString();
            }
            return "No details available.";
        }

        private void RemoveCheckedData()
        {
            try
            {
                var jsonData = JObject.Parse(sessionData);
                if (jsonData.ContainsKey("CheckedData"))
                {
                    jsonData.Remove("CheckedData");
                    sessionData = jsonData.ToString(Newtonsoft.Json.Formatting.Indented);
                    File.WriteAllText(GetJsonFilePath(), sessionData);
                    LogManager.AppendLog("CheckedData removed from session data.");
                }
            }
            catch (Exception ex)
            {
                LogManager.AppendLog($"Error removing CheckedData: {ex.Message}");
            }
        }

        private AmazonS3Client CreateS3Client(string accessKeyId, string secretAccessKey, string sessionToken, string region)
        {
            var awsCredentials = new Amazon.Runtime.SessionAWSCredentials(accessKeyId, secretAccessKey, sessionToken);
            return new AmazonS3Client(awsCredentials, Amazon.RegionEndpoint.GetBySystemName(region));
        }

        public async Task<bool> UploadFileToS3(string filePath, string bucketName, string accessKeyId, string secretAccessKey, string sessionToken, string region)
        {
            try
            {
                using (var client = CreateS3Client(accessKeyId, secretAccessKey, sessionToken, region))
                {
                    string timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
                    string uniqueFileName = $"{sessionTitle}_{timestamp}.json";
                    string uniqueFolder = Guid.NewGuid().ToString();

                    var jsonData = JObject.Parse(sessionData);
                    string projectName = jsonData["ProjectName"].ToString();

                    string s3ObjectName = $"media/native-app-uploads/{projectName}/{uniqueFolder}/{uniqueFileName}";

                    var uploadResult = await UploadFileAsync(client, bucketName, s3ObjectName, filePath);
                    if (uploadResult.Item1)
                    {
                        LogManager.AppendLog($"Successfully uploaded {s3ObjectName} to {bucketName}.");
                        LogManager.AppendLog($"File URL: {uploadResult.Item2}");

                        // Call the API to notify about the upload
                        await NotifyProjectUpload(sessionData, uploadResult.Item2);
                        return true;
                    }
                    else
                    {
                        LogManager.AppendLog("File upload to S3 failed.");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.AppendLog($"Error uploading file to S3: {ex.Message}");
                return false;
            }
        }

        private async Task NotifyProjectUpload(string jsonData, string fileUrl)
        {
            var jsonObject = JObject.Parse(jsonData);
            string projectName = jsonObject["ProjectName"].ToString();

            JArray formattedDescriptors = new JArray();
            foreach (JObject desc in (JArray)jsonObject["descriptor"])
            {
                formattedDescriptors.Add(new JObject
                {
                    ["descriptor"] = desc["value"]
                });
            }

            JArray groupIds = new JArray();
            groupIds.Add(((JObject)jsonObject["GroupDetails"])["group_id"].ToString());

            JObject payload = new JObject
            {
                ["file_url"] = Uri.EscapeUriString(fileUrl),
                ["v2_json_file_url"] = Uri.EscapeUriString(fileUrl),
                ["name"] = projectName,
                ["tags"] = new JArray(), // Empty array for tags
                ["descriptors"] = formattedDescriptors,
                ["groups"] = groupIds
            };

            string url = "https://api.thelyceum.io/api/project/v2/upload/native/";
            using (var httpContent = new StringContent(payload.ToString(), Encoding.UTF8, "application/json"))
            {
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                var response = await client.PostAsync(url, httpContent);
                string responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    LogManager.AppendLog("Successfully notified project upload via API.");
                }
                else
                {
                    LogManager.AppendLog($"Failed to notify project upload. Status: {response.StatusCode}. Response: {responseContent}");
                }
            }
        }

        public static async Task<(bool, string)> UploadFileAsync(IAmazonS3 client, string bucketName, string objectName, string filePath)
        {
            var request = new PutObjectRequest
            {
                BucketName = bucketName,
                Key = objectName,
                FilePath = filePath,
            };

            try
            {
                var response = await client.PutObjectAsync(request);
                if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
                {
                    string fileUrl = $"https://{bucketName}.s3.amazonaws.com/{objectName}";
                    Console.WriteLine($"Successfully uploaded {objectName} to {bucketName}.");
                    Console.WriteLine($"File URL: {fileUrl}");
                    return (true, fileUrl);
                }
                else
                {
                    Console.WriteLine($"Failed to upload {objectName} to {bucketName}. Status code: {response.HttpStatusCode}");
                    return (false, null);
                }
            }
            catch (AmazonS3Exception e)
            {
                Console.WriteLine($"Error encountered on server. Message:'{e.Message}' when writing an object");
                return (false, null);
            }
        }
    }
}
