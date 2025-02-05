using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using System.Windows.Forms;
using static LAPxv8.FormAudioPrecision8;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace LAPxv8
{
    public class AutoSessionCreator
    {
        private string accessToken;
        private string refreshToken;
        private List<SignalPathData> checkedData;
        private Dictionary<string, string> globalProperties;
        private string encryptionKey;
        private string sessionFolder;
        public string SessionTitle { get; private set; } // Store session title
        private string configFilePath;

        public AutoSessionCreator(FormAudioPrecision8 apxForm, string accessToken, string refreshToken, List<SignalPathData> checkedData, Dictionary<string, string> globalProperties)
        {
            this.accessToken = accessToken;
            this.refreshToken = refreshToken;
            this.checkedData = checkedData ?? new List<SignalPathData>();
            this.globalProperties = globalProperties ?? new Dictionary<string, string>();

            if (!this.checkedData.Any())
            {
                LogManager.AppendLog("⚠ WARNING: No checked data passed to AutoSessionCreator.");
            }

            LogManager.AppendLog($"AutoSessionCreator initialized with {this.checkedData.Count} signal paths.");

            sessionFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Lyceum");
            Directory.CreateDirectory(sessionFolder);

            encryptionKey = Cryptography.GetOrCreateEncryptionKey();
            if (string.IsNullOrEmpty(encryptionKey))
            {
                throw new Exception("Encryption key retrieval failed.");
            }

            // ✅ Set up config file path
            configFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LAPxv8", "config.json");
        }

        public void Run()
        {
            try
            {
                LogManager.AppendLog("AutoSessionCreator: Initiating session creation.");

                // ✅ Retrieve last saved title format from Automation Configs
                string titleTemplate = GetSavedSessionTitle();

                if (string.IsNullOrWhiteSpace(titleTemplate))
                {
                    LogManager.AppendLog("⚠ WARNING: No saved session title found. Retrieving from Automation Configs.");
                    titleTemplate = GetTitleFormatFromConfig();
                }

                // ✅ Log the retrieved template before processing
                LogManager.AppendLog($"[DEBUG] Retrieved Title Format from config: {titleTemplate}");

                // ✅ If no title format exists, always prompt the user
                if (string.IsNullOrWhiteSpace(titleTemplate))
                {
                    LogManager.AppendLog("⚠ WARNING: No automated session title format found. Prompting user for input.");
                    titleTemplate = PromptForSessionTitle();

                    if (string.IsNullOrWhiteSpace(titleTemplate))
                    {
                        titleTemplate = $"AutoSession_{DateTime.Now:yyyyMMdd_HHmmss}";
                        LogManager.AppendLog($"[DEBUG] AutoSessionCreator: No user input. Using fallback title -> {titleTemplate}");
                    }
                }

                // ✅ Log the original title template
                LogManager.AppendLog($"[DEBUG] AutoSessionCreator: Original session title template -> {titleTemplate}");

                // ✅ Replace placeholders with actual values
                string resolvedTitle = ReplacePlaceholdersWithValues(titleTemplate, globalProperties);

                // ✅ If the resolved title still contains placeholders, ask user for a manual title
                if (resolvedTitle.Contains("<"))
                {
                    LogManager.AppendLog("⚠ WARNING: Some placeholders were not replaced. Prompting user for a manual session title.");
                    resolvedTitle = PromptForSessionTitle();

                    if (string.IsNullOrWhiteSpace(resolvedTitle))
                    {
                        resolvedTitle = $"AutoSession_{DateTime.Now:yyyyMMdd_HHmmss}";
                        LogManager.AppendLog($"[DEBUG] AutoSessionCreator: No user input. Using fallback title -> {resolvedTitle}");
                    }
                }

                // ✅ Log the final resolved title
                LogManager.AppendLog($"[DEBUG] AutoSessionCreator: Resolved session title -> {resolvedTitle}");

                // ✅ Sanitize filename to remove illegal characters
                resolvedTitle = SanitizeFileName(resolvedTitle);
                LogManager.AppendLog($"[DEBUG] AutoSessionCreator: Sanitized session title -> {resolvedTitle}");

                // ✅ Save session
                var sessionData = new { Title = resolvedTitle, GlobalProperties = globalProperties, CheckedData = checkedData };
                SaveSession(sessionData, resolvedTitle);
                this.SessionTitle = resolvedTitle;

                LogManager.AppendLog($"✅ Session '{resolvedTitle}' created successfully.");
            }
            catch (Exception ex)
            {
                LogManager.AppendLog($"❌ ERROR in AutoSessionCreator.Run(): {ex.Message}");
            }
        }


        // ✅ Retrieve title format from Automation Configs file
        private string GetTitleFormatFromConfig()
        {
            try
            {
                if (File.Exists(configFilePath))
                {
                    string json = File.ReadAllText(configFilePath);
                    var config = JsonConvert.DeserializeObject<dynamic>(json);
                    string titleFormat = config.TitleFormat;
                    LogManager.AppendLog($"[DEBUG] Retrieved Title Format from config: {titleFormat}");
                    return titleFormat;
                }
            }
            catch (Exception ex)
            {
                LogManager.AppendLog($"❌ ERROR retrieving Title Format from config: {ex.Message}");
            }
            return string.Empty;
        }

        // ✅ Function to retrieve the last saved session title from Automation Configs
        private string GetSavedSessionTitle()
        {
            try
            {
                string configFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Lyceum", "session_title.txt");

                if (File.Exists(configFilePath))
                {
                    string savedTitle = File.ReadAllText(configFilePath).Trim();
                    LogManager.AppendLog($"[DEBUG] Retrieved saved session title -> {savedTitle}");
                    return savedTitle;
                }

                LogManager.AppendLog("⚠ WARNING: No saved session title found.");
                return string.Empty;
            }
            catch (Exception ex)
            {
                LogManager.AppendLog($"❌ ERROR retrieving saved session title: {ex.Message}");
                return string.Empty;
            }
        }


        // ✅ Function to replace placeholders (e.g., <DeviceId>) with actual values from globalProperties
        private string ReplacePlaceholdersWithValues(string title, Dictionary<string, string> properties)
        {
            if (properties == null || properties.Count == 0)
            {
                LogManager.AppendLog("⚠ WARNING: No global properties found for title replacement.");
                return title;
            }

            foreach (var kvp in properties)
            {
                string placeholder = $"<{kvp.Key}>";  // Example: <DeviceId>
                if (title.Contains(placeholder))
                {
                    title = title.Replace(placeholder, kvp.Value);
                    LogManager.AppendLog($"[DEBUG] Replaced {placeholder} with '{kvp.Value}'");
                }
            }

            return title;
        }

        // ✅ Function to remove illegal characters from filename
        private string SanitizeFileName(string title)
        {
            char[] invalidChars = Path.GetInvalidFileNameChars();
            foreach (char c in invalidChars)
            {
                title = title.Replace(c.ToString(), "_"); // Replace illegal chars with "_"
            }
            return title;
        }
        private string PromptForSessionTitle()
        {
            using (Form prompt = new Form())
            {
                prompt.Width = 400;
                prompt.Height = 180;
                prompt.Text = "Enter Session Title";
                prompt.StartPosition = FormStartPosition.CenterScreen;

                Label textLabel = new Label() { Left = 50, Top = 20, Text = "Session Title:" };
                TextBox inputBox = new TextBox() { Left = 50, Top = 50, Width = 300 };
                Button confirmation = new Button() { Text = "OK", Left = 150, Width = 100, Top = 80, DialogResult = DialogResult.OK };

                prompt.Controls.Add(textLabel);
                prompt.Controls.Add(inputBox);
                prompt.Controls.Add(confirmation);
                prompt.AcceptButton = confirmation;

                return prompt.ShowDialog() == DialogResult.OK ? inputBox.Text.Trim() : "";
            }
        }

        private void SaveSession(object sessionData, string title)
        {
            LogManager.AppendLog($"[DEBUG] AutoSessionCreator: Checking data before saving session...");

            if (sessionData == null)
            {
                LogManager.AppendLog("❌ ERROR: sessionData is NULL before saving session.");
                return;
            }

            // ✅ Wrap the session inside a structured format
            var sessionObject = new
            {
                Title = title,
                Data = JsonConvert.SerializeObject(sessionData, Formatting.Indented)  // **Wrap session data inside "Data"**
            };

            JObject jsonDataObject = JObject.Parse(JsonConvert.SerializeObject(sessionObject, Formatting.Indented));

            // Ensure `GlobalProperties` exists
            if (!jsonDataObject.ContainsKey("GlobalProperties") || jsonDataObject["GlobalProperties"] == null)
            {
                jsonDataObject["GlobalProperties"] = new JObject();
                LogManager.AppendLog("⚠ WARNING: 'GlobalProperties' was missing. Added empty object.");
            }

            // ✅ Ensure `Descriptors` exists
            if (!jsonDataObject.ContainsKey("Descriptors") || jsonDataObject["Descriptors"] == null)
            {
                jsonDataObject["Descriptors"] = new JArray();
                LogManager.AppendLog("⚠ WARNING: 'Descriptors' was missing. Added empty array.");
            }

            string jsonData = jsonDataObject.ToString();

            if (string.IsNullOrWhiteSpace(jsonData) || jsonData.Length < 20) // Prevent saving empty data
            {
                LogManager.AppendLog("❌ ERROR: JSON serialization resulted in empty or invalid data. Aborting session save.");
                return;
            }

            LogManager.AppendLog($"✅ AutoSessionCreator: Data looks good. Proceeding with session creation...");

            string jsonFilePath = Path.Combine(sessionFolder, title + ".json");
            string lycFilePath = Path.Combine(sessionFolder, title + ".lyc");

            try
            {
                File.WriteAllText(jsonFilePath, jsonData);
                LogManager.AppendLog($"✅ Unencrypted session data saved to: {jsonFilePath}");
            }
            catch (Exception ex)
            {
                LogManager.AppendLog($"❌ ERROR: Failed to write JSON file: {ex.Message}");
                return;
            }

            try
            {
                string encryptedData = Cryptography.EncryptString(encryptionKey, jsonData);
                if (string.IsNullOrEmpty(encryptedData))
                {
                    LogManager.AppendLog($"❌ ERROR: Encryption failed. Encrypted data is NULL or empty.");
                    return;
                }

                File.WriteAllText(lycFilePath, encryptedData);
                LogManager.AppendLog($"🔒 Encrypted session data saved to: {lycFilePath}");
            }
            catch (Exception ex)
            {
                LogManager.AppendLog($"❌ ERROR: Failed to write encrypted file: {ex.Message}");
            }
        }
    }
}
