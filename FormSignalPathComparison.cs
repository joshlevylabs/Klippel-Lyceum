using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace LAPxv8
{
    public partial class FormSignalPathComparison : Form
    {
        private Chart comparisonChart;
        private ComboBox measurementSelector;
        private Dictionary<string, List<(double value, int channel, string variant)>> currentData;

        public FormSignalPathComparison()
        {
            InitializeComponent();
            currentData = new Dictionary<string, List<(double value, int channel, string variant)>>();
        }

        private void InitializeComponent()
        {
            this.Text = "Signal Path Comparison";
            this.Size = new Size(800, 600);
            this.BackColor = Color.FromArgb(45, 45, 45);
            this.ForeColor = Color.White;

            // Create measurement type selector
            measurementSelector = new ComboBox
            {
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            this.Controls.Add(measurementSelector);

            // Create chart
            comparisonChart = new Chart
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(45, 45, 45),
                Palette = ChartColorPalette.Bright
            };

            var chartArea = new ChartArea("MainArea")
            {
                BackColor = Color.FromArgb(45, 45, 45),
                BorderColor = Color.White,
                BorderWidth = 1
            };

            // Configure axes
            chartArea.AxisX.LabelStyle.ForeColor = Color.White;
            chartArea.AxisY.LabelStyle.ForeColor = Color.White;
            chartArea.AxisX.LineColor = Color.White;
            chartArea.AxisY.LineColor = Color.White;
            chartArea.AxisX.MajorGrid.LineColor = Color.FromArgb(70, 70, 70);
            chartArea.AxisY.MajorGrid.LineColor = Color.FromArgb(70, 70, 70);

            comparisonChart.ChartAreas.Add(chartArea);
            this.Controls.Add(comparisonChart);

            // Add legend
            var legend = new Legend
            {
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                Docking = Docking.Right
            };
            comparisonChart.Legends.Add(legend);

            // Handle form closing
            this.FormClosing += (s, e) =>
            {
                if (e.CloseReason == CloseReason.UserClosing)
                {
                    e.Cancel = true;
                    this.Hide();
                }
            };
        }

        public void UpdateChart(Dictionary<string, List<(double value, int channel, string variant)>> measurementData)
        {
            if (measurementData == null || !measurementData.Any())
            {
                return;
            }

            currentData = measurementData;

            // Update measurement selector items
            measurementSelector.Items.Clear();
            measurementSelector.Items.AddRange(measurementData.Keys.ToArray());

            if (measurementSelector.Items.Count > 0)
            {
                measurementSelector.SelectedIndex = 0;
            }

            // Wire up the event handler after populating items
            measurementSelector.SelectedIndexChanged -= MeasurementSelector_SelectedIndexChanged;
            measurementSelector.SelectedIndexChanged += MeasurementSelector_SelectedIndexChanged;

            // Initial update
            if (measurementSelector.SelectedItem != null)
            {
                UpdateChartForMeasurement(measurementSelector.SelectedItem.ToString());
            }
        }

        private void MeasurementSelector_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (measurementSelector.SelectedItem != null)
            {
                UpdateChartForMeasurement(measurementSelector.SelectedItem.ToString());
            }
        }

        private void UpdateChartForMeasurement(string measurementType)
        {
            if (!currentData.ContainsKey(measurementType))
            {
                return;
            }

            // Clear everything
            comparisonChart.Series.Clear();
            comparisonChart.ChartAreas[0].AxisX.CustomLabels.Clear();
            comparisonChart.ChartAreas[0].RecalculateAxesScale();

            var data = currentData[measurementType];
            bool isFrequencyResponse = measurementType.Contains("Frequency Response");

            var area = comparisonChart.ChartAreas[0];

            // Configure axes first
            if (isFrequencyResponse)
            {
                // Do NOT set logarithmic scale, we'll handle it manually
                area.AxisX.IsLogarithmic = false;
                area.AxisX.Title = "Frequency (Hz)";
                area.AxisX.LabelStyle.Angle = 0;
                area.AxisX.MajorGrid.Interval = 1;
                area.AxisX.Interval = 1;

                // Add custom frequency labels
                double[] freqPoints = { 20, 5000, 10000, 15000, 20000 };
                foreach (double freq in freqPoints)
                {
                    var label = new CustomLabel();
                    label.FromPosition = freqPoints[0];
                    label.ToPosition = freqPoints[4];
                    label.Text = freq >= 1000 ? $"{freq / 1000}k" : freq.ToString();
                    area.AxisX.CustomLabels.Add(label);
                }
            }
            else
            {
                area.AxisX.Title = "Variants";
                area.AxisX.LabelStyle.Angle = -45;
                area.AxisX.Interval = 1;
            }

            // Find min and max values for Y-axis scaling
            double minValue = data.Min(x => x.value);
            double maxValue = data.Max(x => x.value);

            // Calculate range and padding
            double range = maxValue - minValue;
            double padding = range < 1.0 ? range * 0.05 : range * 0.1;

            // Set Y-axis range
            double yMin = minValue - (padding * 0.5);
            double yMax = maxValue + (padding * 0.5);

            if (Math.Abs(yMax - yMin) < 0.1)
            {
                yMin = minValue - 0.01;
                yMax = maxValue + 0.01;
            }

            area.AxisY.Minimum = yMin;
            area.AxisY.Maximum = yMax;
            area.AxisY.Title = GetUnitForMeasurement(measurementType);

            // Group data by channel to create series
            var channelGroups = data.GroupBy(x => x.channel)
                                  .OrderBy(x => x.Key);

            foreach (var channelGroup in channelGroups)
            {
                var series = new Series($"Channel {channelGroup.Key}")
                {
                    ChartType = isFrequencyResponse ? SeriesChartType.Line : SeriesChartType.Column,
                    IsValueShownAsLabel = !isFrequencyResponse,
                    LabelForeColor = Color.White,
                    BorderWidth = isFrequencyResponse ? 2 : 1
                };

                var variantGroups = channelGroup.GroupBy(x => x.variant)
                                              .OrderBy(x => x.Key);

                foreach (var variantGroup in variantGroups)
                {
                    foreach (var point in variantGroup)
                    {
                        if (isFrequencyResponse)
                        {
                            // For frequency response, we'll plot the values directly
                            try
                            {
                                double frequency = double.Parse(point.variant);
                                series.Points.AddXY(frequency, point.value);
                            }
                            catch
                            {
                                // If we can't parse the frequency, just use the variant as-is
                                series.Points.AddXY(point.variant, point.value);
                            }
                        }
                        else
                        {
                            series.Points.AddXY(variantGroup.Key, point.value);
                        }
                    }
                }

                comparisonChart.Series.Add(series);
            }

            // Force chart to recalculate layout
            area.RecalculateAxesScale();
            comparisonChart.Invalidate();
        }

        private string GetUnitForMeasurement(string measurementType)
        {
            if (measurementType.Contains("RMS Level")) return "dB";
            if (measurementType.Contains("Peak Level")) return "dB";
            if (measurementType.Contains("Gain")) return "dB";
            if (measurementType.Contains("Frequency Response")) return "dB";
            if (measurementType.Contains("Crosstalk")) return "dB";
            return "";
        }
    }
}
