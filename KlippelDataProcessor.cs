using System;
using System.Collections.Generic;
using System.Windows.Forms;
using KLAUTOMATIONLib;

namespace LyceumKlippel
{
    public class KlippelDataProcessor
    {
        // Structure to hold measure information
        public class MeasureInfo
        {
            public string Category { get; set; }
            public string DisplayName { get; set; }
            public bool HasMax { get; set; }
            public bool HasMin { get; set; }
        }

        // Comprehensive mapping dictionary
        private static readonly Dictionary<string, MeasureInfo> MeasureMappings = new Dictionary<string, MeasureInfo>
        {
            { "resp", new MeasureInfo { Category = "Frequency Response", DisplayName = "Frequency Response", HasMax = true, HasMin = true } },
            { "rthd", new MeasureInfo { Category = "Distortion", DisplayName = "THD", HasMax = true, HasMin = false } },
            { "harm2", new MeasureInfo { Category = "Distortion", DisplayName = "2nd Harmonic", HasMax = true, HasMin = false } },
            { "harm3", new MeasureInfo { Category = "Distortion", DisplayName = "3rd Harmonic", HasMax = true, HasMin = false } },
            { "rbz", new MeasureInfo { Category = "Distortion", DisplayName = "Rub+Buzz", HasMax = true, HasMin = false } },
            { "imp", new MeasureInfo { Category = "Impedance", DisplayName = "Impedance", HasMax = true, HasMin = true } },
            { "pol", new MeasureInfo { Category = "Phase", DisplayName = "Phase", HasMax = false, HasMin = false } }
        };

        // Method to get measure information
        public static MeasureInfo GetMeasureInfo(string measureAbbr)
        {
            return MeasureMappings.ContainsKey(measureAbbr) ? MeasureMappings[measureAbbr] : new MeasureInfo { Category = "Uncategorized", DisplayName = measureAbbr, HasMax = false, HasMin = false };
        }

        // Placeholder method to calculate "Max" value based on settings
        public static double CalculateMax(IKlQCMeasure measure, KlDatabase database)
        {
            // Example: Extract settings from database and calculate
            // For actual implementation, parse settings like Tolerance Mask or Shift Mask
            // This is a placeholder; replace with real logic based on your database structure
            return 0.0; // Replace with actual calculation
        }

        // Placeholder method to calculate "Min" value based on settings
        public static double CalculateMin(IKlQCMeasure measure, KlDatabase database)
        {
            // Example: Extract settings from database and calculate
            // This is a placeholder; replace with real logic based on your database structure
            return 0.0; // Replace with actual calculation
        }

        // Method to populate TreeView nodes for a measure
        public static void PopulateMeasureNodes(TreeNode categoryNode, IKlQCMeasure measure, KlDatabase database, HashSet<string> existingNodes)
        {
            string abbr = measure.Name;
            MeasureInfo info = GetMeasureInfo(abbr);
            string measureKey = $"{categoryNode.FullPath}/{info.DisplayName}";

            if (!existingNodes.Contains(measureKey))
            {
                TreeNode measureNode = new TreeNode(info.DisplayName) { Tag = measure };
                categoryNode.Nodes.Add(measureNode);
                existingNodes.Add(measureKey);
            }

            if (info.HasMax)
            {
                string maxKey = $"{categoryNode.FullPath}/{info.DisplayName} Max";
                if (!existingNodes.Contains(maxKey))
                {
                    TreeNode maxNode = new TreeNode($"{info.DisplayName} Max") { Tag = measure };
                    categoryNode.Nodes.Add(maxNode);
                    existingNodes.Add(maxKey);
                }
            }

            if (info.HasMin)
            {
                string minKey = $"{categoryNode.FullPath}/{info.DisplayName} Min";
                if (!existingNodes.Contains(minKey))
                {
                    TreeNode minNode = new TreeNode($"{info.DisplayName} Min") { Tag = measure };
                    categoryNode.Nodes.Add(minNode);
                    existingNodes.Add(minKey);
                }
            }
        }
    }
}