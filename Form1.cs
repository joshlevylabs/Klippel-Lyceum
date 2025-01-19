using System;
using System.Windows.Forms;
using System.Drawing;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Text;
using System.Security.Cryptography;
using System.IO;

namespace LAPxv8
{
    public partial class Form1 : BaseForm
    {
        private ComboBox programListComboBox = new ComboBox();
        private TextBox usernameTextBox = new TextBox();
        private TextBox passwordTextBox = new TextBox();
        private Button loginButton = new Button();
        private static readonly HttpClient client = new HttpClient();
        private string accessToken;
        private string refreshToken;
        private TextBox apiResponseTextBox = new TextBox();
        private Label toggleLogsLabel = new Label();
        private bool logsVisible = false;

        public Form1()
        {
            InitializeForm1Components();
            InitializeOrLoadTokens();
            LoadEnvironmentVariables();
        }

        private void InitializeForm1Components()
        {
            int verticalOffset = 30;
            int verticalSpacing = 50;

            this.Text = "LAPx - Lyceum + Audio Precision Application";
            this.Font = new Font("Segoe UI", 10);
            this.BackColor = Color.FromArgb(45, 45, 45); // Dark Mode background
            this.Size = new Size(450, 600); // Increased size to accommodate spacing
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;

            // Username Label
            Label usernameLabel = new Label
            {
                Text = "Lycuem Username:",
                ForeColor = Color.White,
                Location = new Point(20, 20 + verticalSpacing),
                Width = 200
            };
            Controls.Add(usernameLabel);

            // Username TextBox
            usernameTextBox.Location = new Point(20, 50 + verticalSpacing);
            usernameTextBox.Width = 380;
            usernameTextBox.ForeColor = Color.Gray;
            usernameTextBox.Text = "Enter your username";
            usernameTextBox.GotFocus += RemoveText;
            usernameTextBox.LostFocus += AddText;
            usernameTextBox.BackColor = Color.FromArgb(60, 60, 60);
            usernameTextBox.BorderStyle = BorderStyle.FixedSingle;
            usernameTextBox.Font = new Font("Segoe UI", 10, FontStyle.Regular);
            Controls.Add(usernameTextBox);

            // Password Label
            Label passwordLabel = new Label
            {
                Text = "Lyceum Password:",
                ForeColor = Color.White,
                Location = new Point(20, 100 + verticalSpacing),
                Width = 200
            };
            Controls.Add(passwordLabel);

            // Password TextBox
            passwordTextBox.Location = new Point(20, 130 + verticalSpacing);
            passwordTextBox.Width = 380;
            passwordTextBox.PasswordChar = '*';
            passwordTextBox.ForeColor = Color.Gray;
            passwordTextBox.Text = "Enter your password";
            passwordTextBox.GotFocus += RemoveText;
            passwordTextBox.LostFocus += AddText;
            passwordTextBox.BackColor = Color.FromArgb(60, 60, 60);
            passwordTextBox.BorderStyle = BorderStyle.FixedSingle;
            passwordTextBox.Font = new Font("Segoe UI", 10, FontStyle.Regular);
            Controls.Add(passwordTextBox);

            // Login Button
            loginButton.Text = "Login";
            loginButton.Location = new Point(20, 180 + verticalSpacing);
            loginButton.Width = 380;
            loginButton.Height = 50; // Increased height
            loginButton.BackColor = Color.FromArgb(75, 110, 175); // Updated color for modern look
            loginButton.ForeColor = Color.White;
            loginButton.FlatStyle = FlatStyle.Flat;
            loginButton.FlatAppearance.BorderSize = 0;
            loginButton.Click += LoginButton_Click;
            loginButton.Font = new Font("Segoe UI", 10, FontStyle.Regular);
            loginButton.Cursor = Cursors.Hand;
            loginButton.Region = System.Drawing.Region.FromHrgn(CreateRoundRectRgn(0, 0, loginButton.Width, loginButton.Height, 20, 20));
            Controls.Add(loginButton);

            // Toggle Logs Label
            toggleLogsLabel.Text = "Show Logs";
            toggleLogsLabel.AutoSize = true;
            toggleLogsLabel.Location = new Point((this.ClientSize.Width - toggleLogsLabel.Width) / 2, 380 + verticalSpacing + 50);
            toggleLogsLabel.ForeColor = Color.Blue;
            toggleLogsLabel.Cursor = Cursors.Hand;
            toggleLogsLabel.Click += ToggleLogsLabel_Click;
            Controls.Add(toggleLogsLabel);

            // API Response TextBox
            apiResponseTextBox.Multiline = true;
            apiResponseTextBox.ScrollBars = ScrollBars.Vertical;
            apiResponseTextBox.Location = new Point(20, 430 + verticalSpacing);
            apiResponseTextBox.Size = new Size(380, 0); // Collapsed by default
            apiResponseTextBox.ReadOnly = true;
            apiResponseTextBox.BackColor = Color.FromArgb(60, 60, 60);
            apiResponseTextBox.ForeColor = Color.White;
            apiResponseTextBox.BorderStyle = BorderStyle.None;
            apiResponseTextBox.Visible = false; // Initially hidden
            Controls.Add(apiResponseTextBox);
        }
        private void LoadEnvironmentVariables()
        {
            string username = Environment.GetEnvironmentVariable("LYCEUM_LOGIN_USERNAME") ?? "Enter your username";
            string password = Environment.GetEnvironmentVariable("LYCEUM_LOGIN_PASSWORD") ?? "Enter your password";

            if (username != "Enter your username")
            {
                usernameTextBox.ForeColor = Color.White;
                usernameTextBox.Text = username;
            }

            if (password != "Enter your password")
            {
                passwordTextBox.ForeColor = Color.White;
                passwordTextBox.PasswordChar = '*';
                passwordTextBox.Text = password;
            }
        }

        private void RemoveText(object sender, EventArgs e)
        {
            TextBox textBox = (TextBox)sender;
            if (textBox.ForeColor == Color.Gray)
            {
                textBox.Text = "";
                textBox.ForeColor = Color.White;
                if (textBox == passwordTextBox)
                {
                    textBox.PasswordChar = '*';
                }
            }
        }

        private void AddText(object sender, EventArgs e)
        {
            TextBox textBox = (TextBox)sender;
            if (string.IsNullOrWhiteSpace(textBox.Text))
            {
                textBox.ForeColor = Color.Gray;
                if (textBox == usernameTextBox)
                {
                    textBox.Text = "Enter your username";
                }
                else if (textBox == passwordTextBox)
                {
                    textBox.PasswordChar = '\0';
                    textBox.Text = "Enter your password";
                }
            }
        }

        private async void LoginButton_Click(object? sender, EventArgs e)
        {
            var username = usernameTextBox.Text;
            var password = passwordTextBox.Text;
            var success = await AttemptLogin(username, password);

            if (success)
            {
                var verificationSuccess = await CheckVerificationStatus();
                if (verificationSuccess)
                {
                    OpenFormAudioPrecision();
                }
                else
                {
                    MessageBox.Show("Verification failed. Access denied.");
                }
            }
            else
            {
                MessageBox.Show("Login failed. Please try again.");
            }
        }
        private async Task<bool> AttemptLogin(string email, string password)
        {
            var json = JsonConvert.SerializeObject(new { email, password });
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                LogMessage("Sending login request...");
                LogMessage($"Endpoint: https://api.thelyceum.io/api/account/login");
                LogMessage($"Request Body: {json}");

                HttpResponseMessage response = await client.PostAsync("https://api.thelyceum.io/api/account/login", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                LogMessage($"Login Response Status: {response.StatusCode}");
                LogMessage($"Login Response Content: {responseContent}");

                if (response.IsSuccessStatusCode)
                {
                    var tokenData = JsonConvert.DeserializeObject<dynamic>(responseContent);
                    accessToken = tokenData.access;
                    refreshToken = tokenData.refresh;
                    LogMessage("Login successful.");
                    return true;
                }
                else
                {
                    LogError($"Login failed. Status code: {response.StatusCode}, Response: {responseContent}");
                    return false;
                }
            }
            catch (HttpRequestException httpEx)
            {
                LogError($"Network error during login: {httpEx.Message}");
                return false;
            }
            catch (Exception ex)
            {
                LogError($"An error occurred during login: {ex.Message}");
                return false;
            }
        }
        private async Task<bool> CheckVerificationStatus()
        {
            try
            {
                // Clear headers and set Authorization explicitly
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue
                {
                    NoCache = true,
                    NoStore = true,
                    MustRevalidate = true
                };

                // Set a common User-Agent header (mimicking a browser request)
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/58.0.3029.110 Safari/537.3");

                if (!string.IsNullOrEmpty(accessToken))
                {
                    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                    LogMessage($"Authorization Header (confirmed at send time): Bearer {accessToken}");
                }
                else
                {
                    LogError("Access token is null or empty. Cannot proceed with verification.");
                    return false;
                }

                LogMessage("Sending verification request...");
                LogMessage("Endpoint: https://api.thelyceum.io/api/account/me/");

                // Send GET request
                HttpResponseMessage response = await client.GetAsync("https://api.thelyceum.io/api/account/me/");
                var responseContent = await response.Content.ReadAsStringAsync();

                LogMessage($"Verification Response Status: {response.StatusCode}");
                LogMessage($"Verification Response Content: {responseContent}");

                if (response.IsSuccessStatusCode)
                {
                    var userData = JsonConvert.DeserializeObject<dynamic>(responseContent);
                    bool isVerified = userData.is_verified;

                    if (isVerified)
                    {
                        LogMessage("User is verified.");
                        return true;
                    }
                    else
                    {
                        LogError("User is not verified.");
                        return false;
                    }
                }
                else
                {
                    LogError($"Verification check failed. Status code: {response.StatusCode}, Response: {responseContent}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogError($"Error during verification check: {ex.Message}");
                return false;
            }
        }
        private void OpenFormAudioPrecision()
        {
            this.Hide();
            using (var formAudioPrecision = new FormAudioPrecision8(accessToken, refreshToken))
            {
                formAudioPrecision.ShowDialog();
            }
            this.Close();
        }
        private void LogMessage(string message)
        {
            if (apiResponseTextBox.InvokeRequired)
            {
                apiResponseTextBox.Invoke(new Action(() => {
                    apiResponseTextBox.AppendText($"{DateTime.Now} - {message}{Environment.NewLine}");
                }));
            }
            else
            {
                apiResponseTextBox.AppendText($"{DateTime.Now} - {message}{Environment.NewLine}");
            }
        }

        private void LogError(string message)
        {
            if (apiResponseTextBox.InvokeRequired)
            {
                apiResponseTextBox.Invoke(new Action(() => {
                    apiResponseTextBox.AppendText($"{DateTime.Now} - {message}{Environment.NewLine}");
                }));
            }
            else
            {
                apiResponseTextBox.AppendText($"{DateTime.Now} - {message}{Environment.NewLine}");
            }
        }

        public string GetAccessToken()
        {
            return accessToken;
        }

        public async Task<bool> RefreshAccessToken()
        {
            var json = JsonConvert.SerializeObject(new { refresh = refreshToken });
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                HttpResponseMessage response = await client.PostAsync("https://api.thelyceum.io/api/account/token/refresh", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var tokenData = JsonConvert.DeserializeObject<dynamic>(responseContent);
                    accessToken = tokenData.access;
                    SecureStorage.Save("AccessToken", accessToken);
                    LogMessage("Access token refreshed successfully.");
                    return true;
                }
                else
                {
                    LogError($"Refresh token failed. Status code: {response.StatusCode}, Response: {responseContent}");
                    return false;
                }
            }
            catch (HttpRequestException httpEx)
            {
                LogError($"Network error during token refresh: {httpEx.Message}");
                return false;
            }
            catch (Exception ex)
            {
                LogError($"An error occurred during token refresh: {ex.Message}");
                return false;
            }
        }

        public static class SecureStorage
        {
            public static void Save(string key, string data)
            {
                byte[] encryptedData = ProtectedData.Protect(
                    Encoding.Unicode.GetBytes(data),
                    null,
                    DataProtectionScope.CurrentUser);

                File.WriteAllBytes($"./{key}.bin", encryptedData);
            }

            public static string Load(string key)
            {
                try
                {
                    byte[] encryptedData = File.ReadAllBytes($"./{key}.bin");
                    byte[] decryptedData = ProtectedData.Unprotect(
                        encryptedData,
                        null,
                        DataProtectionScope.CurrentUser);

                    return Encoding.Unicode.GetString(decryptedData);
                }
                catch
                {
                    return null;
                }
            }
        }

        private void InitializeOrLoadTokens()
        {
            var loadedAccessToken = SecureStorage.Load("AccessToken");
            if (!string.IsNullOrEmpty(loadedAccessToken))
            {
                accessToken = loadedAccessToken;
                LogMessage("Access token loaded successfully.");
            }
            else
            {
                LogError("No access token found.");
            }

            var loadedRefreshToken = SecureStorage.Load("RefreshToken");
            if (!string.IsNullOrEmpty(loadedRefreshToken))
            {
                refreshToken = loadedRefreshToken;
                LogMessage("Refresh token loaded successfully.");
            }
            else
            {
                LogError("No refresh token found.");
            }
        }

        private void ToggleLogsLabel_Click(object sender, EventArgs e)
        {
            logsVisible = !logsVisible;
            apiResponseTextBox.Visible = logsVisible;
            apiResponseTextBox.Size = logsVisible ? new Size(380, 150) : new Size(380, 0);
            toggleLogsLabel.Text = logsVisible ? "Hide Logs" : "Show Logs";
        }

        public List<string> GetAvailablePrograms()
        {
            return new List<string> {
                "Audio Precision 8.0",
            };
        }

        [System.Runtime.InteropServices.DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(
            int nLeftRect, int nTopRect,
            int nRightRect, int nBottomRect,
            int nWidthEllipse, int nHeightEllipse);

    }
}