using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using NAudio.Wave;
using Newtonsoft.Json;
using Tensorflow;
using Tensorflow.NumPy;
using static Tensorflow.Binding;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace LAPxv8
{
    public partial class Aristotle : BaseForm
    {
        private TestResultsGrid parentForm;
        private RichTextBox chatLogTextBox;  // Changed from TextBox to RichTextBox for better formatting
        private TextBox inputTextBox;
        private Button executeButton;
        private Chart frequencyChart;
        private static readonly HttpClient httpClient = new HttpClient();
        private bool hasIntroduced = false;
        private string analysisContext = "";
        private static List<string> testResults = new List<string>();
        private static event EventHandler<string> NewTestResultAdded;


        public Aristotle(TestResultsGrid parent)
        {
            parentForm = parent;
            InitializeComponent();
            InitializeAristotleComponents();
            InitializeResultsHandler();
            ShowIntroduction();
        }
        private async void ShowIntroduction()
        {
            if (!hasIntroduced)
            {
                string introMessage = "Welcome! My name is Aristotle, an AI assistant taylored to analyze all things audio. To get started, right-click on any cell in the Test Results Grid and select 'Attach Test Results'. Once you've uploaded your data, I can help analyze it for you.";
                hasIntroduced = true;
            }
        }

        public async Task<string> CallClaudeAPI(string prompt)
        {
            string apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
            if (string.IsNullOrEmpty(apiKey))
            {
                return "Error: API key is missing. Please set the ANTHROPIC_API_KEY environment variable.";
            }

            try
            {
                // Set required headers
                httpClient.DefaultRequestHeaders.Clear();
                httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
                httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

                // Construct the request payload with updated schema
                var requestBody = new
                {
                    model = "claude-3-opus-20240229", // Updated to latest Claude 3 model
                    max_tokens = 4096, // Increased token limit
                    messages = new[]
                    {
                new { role = "user", content = prompt }
            }
                };

                // Serialize payload to JSON
                string jsonContent = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                // Send the POST request to the messages endpoint
                HttpResponseMessage response = await httpClient.PostAsync("https://api.anthropic.com/v1/messages", content);

                if (!response.IsSuccessStatusCode)
                {
                    string errorResponse = await response.Content.ReadAsStringAsync();
                    return $"API Error: {errorResponse}";
                }

                // Read and parse response content
                string responseString = await response.Content.ReadAsStringAsync();
                dynamic jsonResponse = JsonConvert.DeserializeObject(responseString);

                return jsonResponse.content[0].text.ToString(); // Updated response parsing
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }

        public void InitializeAristotleComponents()
        {
            // Set form properties
            this.Text = "Aristotle AI Agent";
            this.Size = new Size(800, 600);

            int yOffset = 50;

            // Initialize Chat Log (changed to RichTextBox)
            chatLogTextBox = new RichTextBox
            {
                Width = 760,
                Height = 400,
                Location = new Point(20, yOffset),
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 10)
            };
            this.Controls.Add(chatLogTextBox);

            // Initialize Input TextBox
            inputTextBox = new TextBox
            {
                Width = 640,
                Height = 30,
                Location = new Point(20, yOffset + 420),
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10)
            };
            this.Controls.Add(inputTextBox);
            inputTextBox.KeyDown += InputTextBox_KeyDown;

            // Initialize Execute Button
            executeButton = new Button
            {
                Text = "Send",
                Width = 100,
                Height = 30,
                Location = new Point(680, yOffset + 420),
                BackColor = Color.FromArgb(85, 160, 140),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 },
            };
            executeButton.Click += ExecuteButton_Click;
            this.Controls.Add(executeButton);
        }

        public static void AddTestResult(string resultJson)
        {
            testResults.Add(resultJson);
            NewTestResultAdded?.Invoke(null, resultJson);
        }

        private void InitializeResultsHandler()
        {
            NewTestResultAdded += async (sender, resultJson) =>
            {
                // Parse the new result
                dynamic result = JsonConvert.DeserializeObject(resultJson);

                // Create a message about the new result
                string notification = $"New test result received:\n" +
                    $"Test Run: {result.TestRun}\n" +
                    $"Test Name: {result.TestName}\n" +
                    $"Status: {result.Status}\n" +
                    $"Location: {result.Location}\n";

                // Add to chat log
                chatLogTextBox.Invoke((MethodInvoker)delegate
                {
                    chatLogTextBox.AppendText($"Aristotle: {notification}\n");
                    chatLogTextBox.AppendText("Would you like me to analyze this result for you?\n");
                });

                // Update the context for future analysis
                UpdateAnalysisContext();
            };
        }

        private void UpdateAnalysisContext()
        {
            // Create comprehensive context from all results
            StringBuilder context = new StringBuilder();
            context.AppendLine("Current Test Results Summary:");

            foreach (string resultJson in testResults)
            {
                dynamic result = JsonConvert.DeserializeObject(resultJson);
                context.AppendLine($"- {result.TestRun}: {result.Status}");
            }

            // Store this context for use in analysis
            analysisContext = context.ToString();
        }

        private async void ExecuteButton_Click(object sender, EventArgs e)
        {
            string userQuery = inputTextBox.Text.Trim();

            if (!string.IsNullOrEmpty(userQuery))
            {
                chatLogTextBox.AppendText($"You: {userQuery}\n");

                string fullPrompt = $"Context about the current test results:\n{analysisContext}\n\nUser question: {userQuery}";
                string aiResponse = await CallClaudeAPI(fullPrompt);

                chatLogTextBox.AppendText($"Aristotle: {aiResponse}\n");
                inputTextBox.Clear();
            }
        }

        private string GetTestResultsContext()
        {
            StringBuilder context = new StringBuilder();

            if (parentForm.attachedResults.Any())
            {
                context.AppendLine("Available test results:");
                foreach (var result in parentForm.attachedResults)
                {
                    context.AppendLine($"- Test at row {result.Key.RowIndex}, column {result.Key.ColumnIndex}");
                    context.AppendLine($"  Status: {result.Value.Status}");
                    context.AppendLine($"  Data: {result.Value.Data}");
                }
            }
            else
            {
                context.AppendLine("No test results have been attached yet.");
            }

            return context.ToString();
        }





        private void InputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                ExecuteButton_Click(sender, e); // Trigger the same logic as the Execute button
                e.SuppressKeyPress = true; // Prevent the newline character in the input box
            }
        }

    }

}

/*
        private string InterpretCommand(string query)
        private void ProcessQuery(string query)
        private void ExportAndProcessDataForAristotle()
        private void ProcessJsonDataForGraphing(dynamic data)
        private string[] ExtractUnitsFromQuery(string query)
        private void PlotFrequencyResponseDifference(string unitAPath, string unitBPath)
        private float[] PerformFFT(AudioFileReader reader)
        private Tensor AnalyzeFrequencyResponse(Tensor inputTensor)
        private void DisplayResultOnChart(float[] fftA, float[] fftB)
        */
