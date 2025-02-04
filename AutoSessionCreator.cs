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
        }

        public void Run()
        {
            try
            {
                LogManager.AppendLog("AutoSessionCreator: Initiating session creation.");

                // ✅ Prompt user for a session title
                string title = PromptForSessionTitle();

                // ✅ If user cancels, fallback to timestamp
                if (string.IsNullOrWhiteSpace(title))
                {
                    title = $"AutoSession_{DateTime.Now:yyyyMMdd_HHmmss}";
                    LogManager.AppendLog($"Session title auto-generated: {title}");
                }

                var sessionData = new { Title = title, GlobalProperties = globalProperties, CheckedData = checkedData };
                SaveSession(sessionData, title);

                this.SessionTitle = title;
                LogManager.AppendLog($"✅ Session '{title}' created successfully.");
            }
            catch (Exception ex)
            {
                LogManager.AppendLog($"❌ ERROR in AutoSessionCreator.Run(): {ex.Message}");
            }
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
