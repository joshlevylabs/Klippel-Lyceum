using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Newtonsoft.Json;
using AudioPrecision.API;

namespace LAPxv8
{
    public partial class FormAudioPrecision8 : BaseForm
    {
        private void LoadLimitsMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                OpenFileDialog openFileDialog = new OpenFileDialog
                {
                    Filter = "Lyceum Limit Files (*.lyc)|*.lyc",
                    Title = "Select Limit Family File"
                };

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string filePath = openFileDialog.FileName;
                    var fileContent = File.ReadAllText(filePath);
                    var limitData = JsonConvert.DeserializeObject<List<LimitEntry>>(fileContent);

                    if (limitData == null || !limitData.Any())
                    {
                        MessageBox.Show("No valid limit data found.", "Invalid File", MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                        return;
                    }

                    var signalPathMeasurements = RetrieveAvailableSignalPathsAndMeasurements();
                    var successfullyPaired = ApplyLimitsToResults(limitData, signalPathMeasurements);
                    var resultNames = GenerateResultNames(signalPathMeasurements);

                    // Calculate unpaired limits
                    var unpairedLimits = limitData
                        .Where(limitEntry => !successfullyPaired.Contains($"{limitEntry.SignalPathName} | {limitEntry.MeasurementName} | {limitEntry.ResultName}"))
                        .ToList(); // Ensure this remains as List<LimitEntry>

                    GenerateLoadLimitsSummary(successfullyPaired, signalPathMeasurements, resultNames, unpairedLimits);

                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading limits: {ex.Message}", "Error", MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }
        }

        private Dictionary<string, List<string>> GenerateResultNames(Dictionary<string, List<string>> signalPathMeasurements)
        {
            var resultNames = new Dictionary<string, List<string>>();

            foreach (var signalPath in signalPathMeasurements)
            {
                var signalPathName = signalPath.Key;

                foreach (var measurementName in signalPath.Value)
                {
                    try
                    {
                        var resultsCount = APx.Sequence.Results.GetResults(signalPathName, measurementName).Count;
                        var measurementResults = new List<string>();

                        for (int i = 0; i < resultsCount; i++)
                        {
                            var resultName = APx.Sequence.Results[i].Name;
                            measurementResults.Add(resultName);
                        }

                        resultNames[measurementName] = measurementResults;
                        LogToTextBox($"Measurement: {measurementName} | Results: {string.Join(", ", measurementResults)}");
                    }
                    catch (Exception ex)
                    {
                        LogToTextBox($"Error retrieving results for Measurement '{measurementName}' in Signal Path '{signalPathName}': {ex.Message}");
                    }
                }
            }

            return resultNames;
        }

        private Dictionary<string, List<string>> RetrieveAvailableSignalPathsAndMeasurements()
        {
            var signalPathMeasurements = new Dictionary<string, List<string>>();

            foreach (var signalPath in APx.Sequence.OfType<ISignalPath>())
            {
                try
                {
                    var measurementNames = new List<string>();
                    var signalPathName = signalPath.Name;

                    for (int i = 0; i < signalPath.Count; i++)
                    {
                        var measurement = signalPath.GetMeasurement(i);
                        if (measurement != null)
                        {
                            measurementNames.Add(measurement.Name);
                        }
                    }

                    signalPathMeasurements.Add(signalPathName, measurementNames);
                    LogToTextBox($"Signal Path: {signalPathName} | Measurements: {string.Join(", ", measurementNames)}");
                }
                catch (Exception ex)
                {
                    LogToTextBox($"Error retrieving measurements for Signal Path '{signalPath.Name}': {ex.Message}");
                }
            }

            return signalPathMeasurements;
        }

        private List<string> ApplyLimitsToResults(List<LimitEntry> limitData, Dictionary<string, List<string>> signalPathMeasurements)
        {
            var successfullyPaired = new List<string>();

            foreach (var limitEntry in limitData)
            {
                ValidateAndSetLimitFlags(limitEntry);

                LogToTextBox($"Processing Limit Entry: {limitEntry.SignalPathName} | {limitEntry.MeasurementName} | {limitEntry.ResultName}");

                if (!signalPathMeasurements.ContainsKey(limitEntry.SignalPathName))
                {
                    LogToTextBox($"Signal Path not found: {limitEntry.SignalPathName}");
                    continue;
                }

                var measurementNames = signalPathMeasurements[limitEntry.SignalPathName];
                if (!measurementNames.Contains(limitEntry.MeasurementName))
                {
                    LogToTextBox($"Measurement not found: {limitEntry.MeasurementName}");
                    continue;
                }

                try
                {
                    ISignalPath matchingSignalPath = APx.Sequence.GetSignalPath(limitEntry.SignalPathName);
                    var matchingMeasurement = matchingSignalPath.GetMeasurement(limitEntry.MeasurementName);
                    APx.ShowMeasurement(matchingSignalPath.Index, matchingMeasurement.Index);
                    LogToTextBox($"Measurement activated: {limitEntry.MeasurementName}");

                    // Graph retrieval
                    IGraph matchingGraph = null;
                    foreach (var graph in APx.ActiveMeasurement.Graphs)
                    {
                        if (graph is IGraph castedGraph && castedGraph.Name.Equals(limitEntry.ResultName, StringComparison.OrdinalIgnoreCase))
                        {
                            matchingGraph = castedGraph;
                            break;
                        }
                    }

                    if (matchingGraph != null)
                    {
                        ApplyLimitsToGraph(matchingGraph, limitEntry);
                        successfullyPaired.Add($"{limitEntry.SignalPathName} | {limitEntry.MeasurementName} | {limitEntry.ResultName}");
                    }
                    else
                    {
                        LogToTextBox($"Graph not found: {limitEntry.ResultName}");
                    }
                }
                catch (Exception ex)
                {
                    LogToTextBox($"Error applying limits: {ex.Message}");
                }
            }

            return successfullyPaired;
        }

        private void GenerateLoadLimitsSummary(
    List<string> successfullyPaired,
    Dictionary<string, List<string>> signalPathMeasurements,
    Dictionary<string, List<string>> resultNames,
    List<LimitEntry> unpairedLimits) // Use List<LimitEntry>
        {
            var unpairedResults = new List<string>();

            // Identify unpaired results
            foreach (var signalPath in signalPathMeasurements)
            {
                foreach (var measurementName in signalPath.Value)
                {
                    if (resultNames.TryGetValue(measurementName, out var measurementResults))
                    {
                        foreach (var result in measurementResults)
                        {
                            var resultEntry = $"{signalPath.Key} | {measurementName} | {result}";
                            if (!successfullyPaired.Contains(resultEntry))
                            {
                                unpairedResults.Add(resultEntry);
                            }
                        }
                    }
                }
            }

            ShowEnhancedSummaryWindow(successfullyPaired, unpairedLimits, unpairedResults);
        }

        private void ShowEnhancedSummaryWindow(
    List<string> successfullyPaired,
    List<LimitEntry> unpairedLimits,
    List<string> unpairedResults)
        {
            // Create a new form to display the summary
            var summaryForm = new Form
            {
                Text = "Load Limits Summary",
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10),
                StartPosition = FormStartPosition.CenterScreen,
                Size = new Size(800, 600)
            };

            // Check if there are unpaired limits/results
            bool hasUnpaired = unpairedLimits.Any() || unpairedResults.Any();

            if (!hasUnpaired)
            {
                // Single-tab view with successfully paired results
                var pairedListBox = new ListBox
                {
                    Dock = DockStyle.Fill,
                    BackColor = Color.FromArgb(45, 45, 45),
                    ForeColor = Color.White
                };

                foreach (var item in successfullyPaired)
                {
                    pairedListBox.Items.Add(item);
                }

                summaryForm.Controls.Add(pairedListBox);
            }
            else
            {
                // Tabbed view with paired and unpaired data
                var tabControl = new TabControl
                {
                    Dock = DockStyle.Fill,
                    BackColor = Color.FromArgb(45, 45, 45),
                    ForeColor = Color.White
                };

                // Successfully Paired Tab
                var pairedTab = new TabPage("Successfully Paired")
                {
                    BackColor = Color.FromArgb(30, 30, 30),
                    ForeColor = Color.White
                };
                var pairedListBox = new ListBox
                {
                    Dock = DockStyle.Fill,
                    BackColor = Color.FromArgb(45, 45, 45),
                    ForeColor = Color.White
                };
                foreach (var item in successfullyPaired)
                {
                    pairedListBox.Items.Add(item);
                }
                pairedTab.Controls.Add(pairedListBox);
                tabControl.TabPages.Add(pairedTab);

                // Unpaired Limits/Results Tab
                var unpairedTab = new TabPage("Unpaired Limits/Results")
                {
                    BackColor = Color.FromArgb(30, 30, 30),
                    ForeColor = Color.White
                };

                var splitContainer = new SplitContainer
                {
                    Dock = DockStyle.Fill,
                    Orientation = Orientation.Vertical,
                    BackColor = Color.FromArgb(30, 30, 30),
                    ForeColor = Color.White
                };

                // Left: Unpaired Limits
                var limitsListBox = new ListBox
                {
                    Dock = DockStyle.Fill,
                    BackColor = Color.FromArgb(45, 45, 45),
                    ForeColor = Color.White
                };
                foreach (var limit in unpairedLimits)
                {
                    limitsListBox.Items.Add($"{limit.SignalPathName} | {limit.MeasurementName} | {limit.ResultName}");
                }
                splitContainer.Panel1.Controls.Add(limitsListBox);

                // Right: Unpaired Results
                var resultsListBox = new ListBox
                {
                    Dock = DockStyle.Fill,
                    BackColor = Color.FromArgb(45, 45, 45),
                    ForeColor = Color.White
                };
                foreach (var result in unpairedResults)
                {
                    resultsListBox.Items.Add(result);
                }
                splitContainer.Panel2.Controls.Add(resultsListBox);

                // Button to pair limits and results
                var pairButton = new Button
                {
                    Text = "Pair Selected",
                    Dock = DockStyle.Bottom,
                    BackColor = Color.FromArgb(45, 45, 45),
                    ForeColor = Color.White
                };
                pairButton.Click += (sender, args) =>
                {
                    if (limitsListBox.SelectedItem != null && resultsListBox.SelectedItem != null)
                    {
                        var selectedLimitString = limitsListBox.SelectedItem.ToString();
                        var selectedResult = resultsListBox.SelectedItem.ToString();

                        var selectedLimit = unpairedLimits.FirstOrDefault(limit =>
                            $"{limit.SignalPathName} | {limit.MeasurementName} | {limit.ResultName}".Equals(selectedLimitString, StringComparison.OrdinalIgnoreCase));

                        if (selectedLimit != null)
                        {
                            ApplyPairingAndReload(selectedLimit, selectedResult, unpairedLimits);

                            // Update UI
                            successfullyPaired.Add($"{selectedLimit.SignalPathName} | {selectedLimit.MeasurementName} | {selectedLimit.ResultName} -> {selectedResult}");
                            pairedListBox.Items.Add($"{selectedLimit.SignalPathName} | {selectedLimit.MeasurementName} | {selectedLimit.ResultName} -> {selectedResult}");
                            limitsListBox.Items.Remove(selectedLimitString);
                            resultsListBox.Items.Remove(selectedResult);

                            MessageBox.Show("Pairing applied successfully!", "Success", MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information);
                        }
                        else
                        {
                            MessageBox.Show("The selected limit entry could not be found.", "Error", MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                        }
                    }
                    else
                    {
                        MessageBox.Show("Please select both a limit and a result to pair.", "Error", MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                    }
                };

                unpairedTab.Controls.Add(splitContainer);
                unpairedTab.Controls.Add(pairButton);

                tabControl.TabPages.Add(unpairedTab);
                summaryForm.Controls.Add(tabControl);
            }

            summaryForm.ShowDialog();
        }

        private void ApplyPairingAndReload(
    LimitEntry selectedLimit,
    string selectedResult,
    List<LimitEntry> unpairedLimits)
        {
            try
            {
                // Extract details from the selected result
                var resultParts = selectedResult.Split('|').Select(p => p.Trim()).ToArray();
                if (resultParts.Length < 3)
                {
                    throw new ArgumentException("Invalid result format. Expected format: 'SignalPath | Measurement | Result'");
                }

                var signalPathName = resultParts[0];
                var measurementName = resultParts[1];
                var resultName = resultParts[2];

                // Log override action
                LogToTextBox($"Overriding limit '{selectedLimit.ResultName}' with result '{resultName}'.");

                // Retrieve the signal path and measurement
                var signalPath = APx.Sequence.GetSignalPath(signalPathName);
                var measurement = signalPath.GetMeasurement(measurementName);

                // Activate the measurement
                APx.ShowMeasurement(signalPath.Index, measurement.Index);

                // Retrieve the graph by iterating through the collection
                IGraph matchingGraph = null;
                foreach (var graph in APx.ActiveMeasurement.Graphs)
                {
                    if (graph is IGraph castedGraph && castedGraph.Name.Equals(resultName, StringComparison.OrdinalIgnoreCase))
                    {
                        matchingGraph = castedGraph;
                        break;
                    }
                }

                if (matchingGraph != null)
                {
                    // Update the limit entry with the selected result name
                    selectedLimit.SignalPathName = signalPathName;
                    selectedLimit.MeasurementName = measurementName;
                    selectedLimit.ResultName = resultName;

                    // Apply the limit to the graph
                    ApplyLimitsToGraph(matchingGraph, selectedLimit);

                    // Remove the paired limit from the unpaired list
                    unpairedLimits.Remove(selectedLimit);

                    MessageBox.Show($"Successfully paired and loaded limit for result '{resultName}'.", "Success", MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show($"Result '{resultName}' not found in measurement '{measurementName}'.", "Error", MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error applying pairing: {ex.Message}", "Error", MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }
        }


        private LimitEntry GetLimitEntryFromUnpairedLimits(string selectedLimit, List<LimitEntry> unpairedLimitsData)
        {
            var limitParts = selectedLimit.Split('|').Select(p => p.Trim()).ToArray();
            if (limitParts.Length < 3) return null;

            var signalPathName = limitParts[0];
            var measurementName = limitParts[1];
            var resultName = limitParts[2];

            // Use the passed unpairedLimitsData to find the correct LimitEntry
            return unpairedLimitsData.FirstOrDefault(limit =>
                limit.SignalPathName.Equals(signalPathName, StringComparison.OrdinalIgnoreCase) &&
                limit.MeasurementName.Equals(measurementName, StringComparison.OrdinalIgnoreCase) &&
                limit.ResultName.Equals(resultName, StringComparison.OrdinalIgnoreCase));
        }

        private void ShowLoadLimitsSummary(List<string> successfullyPaired, List<string> unpairedLimits, List<string> unpairedResults)
        {
            if (unpairedLimits.Count == 0 && unpairedResults.Count == 0)
            {
                MessageBox.Show("All limits were successfully paired and loaded into APx.", "Success", MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information);
            }
            else
            {
                string summaryMessage = "Some limits or results were not paired successfully.\n\n";

                if (successfullyPaired.Count > 0)
                {
                    summaryMessage += "Successfully Paired:\n" + string.Join("\n", successfullyPaired) + "\n\n";
                }

                if (unpairedLimits.Count > 0)
                {
                    summaryMessage += "Unpaired Limits:\n" + string.Join("\n", unpairedLimits) + "\n\n";
                }

                if (unpairedResults.Count > 0)
                {
                    summaryMessage += "Unpaired Results:\n" + string.Join("\n", unpairedResults) + "\n\n";
                }

                MessageBox.Show(summaryMessage, "Load Limits Summary", MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
            }
        }
        private void ValidateAndSetLimitFlags(LimitEntry limitEntry)
        {
            if (limitEntry.XValueUpperLimitValues != null && limitEntry.XValueUpperLimitValues.Length > 0 &&
                limitEntry.YValueUpperLimitValues != null && limitEntry.YValueUpperLimitValues.Length > 0)
            {
                limitEntry.UpperLimitEnabled = true;
            }
            else
            {
                limitEntry.UpperLimitEnabled = false;
            }

            if (limitEntry.XValueLowerLimitValues != null && limitEntry.XValueLowerLimitValues.Length > 0 &&
                limitEntry.YValueLowerLimitValues != null && limitEntry.YValueLowerLimitValues.Length > 0)
            {
                limitEntry.LowerLimitEnabled = true;
            }
            else
            {
                limitEntry.LowerLimitEnabled = false;
            }

            LogToTextBox($"Limit flags validated for {limitEntry.SignalPathName} | {limitEntry.MeasurementName} | {limitEntry.ResultName}: " +
                         $"UpperLimitEnabled = {limitEntry.UpperLimitEnabled}, LowerLimitEnabled = {limitEntry.LowerLimitEnabled}");
        }

        private void ApplyLimitsToGraph(IGraph graph, LimitEntry limitEntry)
        {
            try
            {
                LogToTextBox($"Processing Graph '{limitEntry.ResultName}' of type '{limitEntry.ResultValueType}'.");

                if (limitEntry.ResultValueType.Equals("XY Values", StringComparison.OrdinalIgnoreCase))
                {
                    ApplyXYLimits(graph.Result.AsXYGraph(), limitEntry);
                }
                else if (limitEntry.ResultValueType.Equals("Meter Values", StringComparison.OrdinalIgnoreCase))
                {
                    ApplyMeterLimits(graph.Result.AsMeterGraph(), limitEntry);
                }
                else
                {
                    LogToTextBox($"Unsupported ResultValueType '{limitEntry.ResultValueType}' for Result: {limitEntry.ResultName}.");
                }
            }
            catch (Exception ex)
            {
                LogToTextBox($"Error applying limits to Graph '{limitEntry.ResultName}': {ex.Message}");
            }
        }

        private void ApplyXYLimits(IXYGraph xyGraph, LimitEntry limitEntry)
        {
            int channelCount = xyGraph.ChannelCount;

            bool isUpperLimitEnabled = limitEntry.UpperLimitEnabled &&
                                       limitEntry.XValueUpperLimitValues != null &&
                                       limitEntry.YValueUpperLimitValues != null;

            bool isLowerLimitEnabled = limitEntry.LowerLimitEnabled &&
                                       limitEntry.XValueLowerLimitValues != null &&
                                       limitEntry.YValueLowerLimitValues != null;

            LogToTextBox($"Processing XY Limits for Graph '{limitEntry.ResultName}' with {channelCount} channels.");
            LogToTextBox($"Upper Limit Enabled: {isUpperLimitEnabled}, Lower Limit Enabled: {isLowerLimitEnabled}");

            if (isUpperLimitEnabled)
            {
                ApplyLimitsPerChannel(xyGraph.UpperLimit, limitEntry.XValueUpperLimitValues, limitEntry.YValueUpperLimitValues, channelCount, "Upper");
            }

            if (isLowerLimitEnabled)
            {
                ApplyLimitsPerChannel(xyGraph.LowerLimit, limitEntry.XValueLowerLimitValues, limitEntry.YValueLowerLimitValues, channelCount, "Lower");
            }
        }

        private void ApplyLimitsPerChannel(IGraphLimit limit, double[] xValues, double[] yValues, int channelCount, string limitType)
        {
            int pointsPerChannel = xValues.Length / channelCount;

            for (int channel = 0; channel < channelCount; channel++)
            {
                double[] xChannelValues = xValues.Skip(channel * pointsPerChannel).Take(pointsPerChannel).ToArray();
                double[] yChannelValues = yValues.Skip(channel * pointsPerChannel).Take(pointsPerChannel).ToArray();

                LogToTextBox($"Channel {channel + 1} - {limitType} Limit XValues: [{string.Join(", ", xChannelValues)}]");
                LogToTextBox($"Channel {channel + 1} - {limitType} Limit YValues: [{string.Join(", ", yChannelValues)}]");

                if (!AreValuesSequential(xChannelValues))
                {
                    LogToTextBox($"Error applying {limitType.ToLower()} limits: XValues must be sequential.");
                    continue;
                }

                try
                {
                    limit.SetValues(channel, xChannelValues, yChannelValues);
                    LogToTextBox($"{limitType} limits successfully applied to Channel {channel + 1}.");
                }
                catch (Exception ex)
                {
                    // Handle the "Track First Channel" error gracefully
                    if (ex.Message.Contains("Track First Channel"))
                    {
                        LogToTextBox($"Warning: 'Track First Channel' setting detected. Limits from Channel 1 will be applied to all channels.");
                        if (channel > 0)
                        {
                            LogToTextBox($"Skipping limit application for Channel {channel + 1} due to 'Track First Channel' setting.");
                            break; // Exit loop as subsequent channels will inherit limits from Channel 1
                        }
                    }
                    else
                    {
                        LogToTextBox($"Error applying {limitType} limits to Channel {channel + 1}: {ex.Message}");
                    }
                }
            }
        }

        private bool AreValuesSequential(double[] values)
        {
            for (int i = 1; i < values.Length; i++)
            {
                if (values[i] < values[i - 1]) return false;
            }
            return true;
        }
        private void ApplyMeterLimits(IMeterGraph meterGraph, LimitEntry limitEntry)
        {
            // Log Meter Limit Type
            LogToTextBox($"Processing Meter Limits for Graph '{limitEntry.ResultName}'.");
            LogToTextBox($"Upper Limit Enabled: {limitEntry.UpperLimitEnabled}, Lower Limit Enabled: {limitEntry.LowerLimitEnabled}");

            // Handle Upper Limits
            if (limitEntry.UpperLimitEnabled && limitEntry.MeterUpperLimitValues != null)
            {
                LogToTextBox($"Applying upper Meter limits to Graph '{limitEntry.ResultName}' with value: {limitEntry.MeterUpperLimitValues[0]}.");
                meterGraph.UpperLimit.SetValue(0, limitEntry.MeterUpperLimitValues[0]);
                LogToTextBox($"Upper Meter limits successfully applied to Graph '{limitEntry.ResultName}'.");
            }

            // Handle Lower Limits
            if (limitEntry.LowerLimitEnabled && limitEntry.MeterLowerLimitValues != null)
            {
                LogToTextBox($"Applying lower Meter limits to Graph '{limitEntry.ResultName}' with value: {limitEntry.MeterLowerLimitValues[0]}.");
                meterGraph.LowerLimit.SetValue(0, limitEntry.MeterLowerLimitValues[0]);
                LogToTextBox($"Lower Meter limits successfully applied to Graph '{limitEntry.ResultName}'.");
            }
        }

        private void LogToTextBox(string message)
        {
            logTextBox.AppendText($"{message}{Environment.NewLine}");
        }
        private void LogAvailableSignalPaths()
        {
            foreach (ISignalPath signalPath in APx.Sequence)
            {
                LogToTextBox($"Available Signal Path: {signalPath.Name}");
            }
        }

        public class LimitEntry
        {
            public string SignalPathName { get; set; }
            public string MeasurementName { get; set; }
            public string ResultName { get; set; }
            public bool UpperLimitEnabled { get; set; }
            public bool LowerLimitEnabled { get; set; }
            public double[] MeterUpperLimitValues { get; set; }
            public double[] MeterLowerLimitValues { get; set; }
            public double[] XValueUpperLimitValues { get; set; }
            public double[] XValueLowerLimitValues { get; set; }
            public double[] YValueUpperLimitValues { get; set; }
            public double[] YValueLowerLimitValues { get; set; }
            public string ResultValueType { get; set; } // "XY Values" or "Meter Values"
        }
    }
}
