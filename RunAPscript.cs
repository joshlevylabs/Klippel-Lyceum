using AudioPrecision.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using static LAPxv8.FormAudioPrecision8;

namespace LAPxv8
{
    public class RunAPscript
    {
        private readonly APx500 APx;

        public RunAPscript(APx500 apxInstance)
        {
            APx = apxInstance ?? throw new ArgumentNullException(nameof(apxInstance));
        }

        public void RunScript(FormAudioPrecision8 formAP8, bool extractData, bool saveSession, bool uploadToLyceum)
        {
            try
            {
                LogManager.AppendLog("🚀 Running APx500 script...");
                APx.Sequence.Run();
                LogManager.AppendLog("✅ APx500 script executed successfully.");

                // Step 1: Run GetCheckedDataButton_Click if extractData is true
                if (extractData)
                {
                    LogManager.AppendLog("🔄 Running 'GetCheckedDataButton_Click'...");
                    formAP8.RunGetCheckedData();
                }

                // Step 2: If saveSession is true and uploadToLyceum is false, run CreateSessionMenuItem_Click
                if (saveSession && !uploadToLyceum) // 🚨 SKIP CreateSession IF Upload to Lyceum is selected
                {
                    LogManager.AppendLog("💾 Running 'CreateSessionMenuItem_Click'...");
                    formAP8.RunCreateSession();
                }
                else if (uploadToLyceum)
                {
                    LogManager.AppendLog("⏩ Skipping 'CreateSessionMenuItem_Click' because Upload to Lyceum is selected.");
                }

                // Step 3: If uploadToLyceum is true, run UploadToLyceumMenuItem_Click
                if (uploadToLyceum)
                {
                    LogManager.AppendLog("📤 Running 'UploadToLyceumMenuItem_Click'...");
                    formAP8.RunUploadToLyceum();
                }

                MessageBox.Show("Sequence completed successfully!", "Success", MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                LogManager.AppendLog($"❌ ERROR running script: {ex.Message}");
            }
        }

        public Dictionary<string, string> GetPromptStepInputs()
        {
            Dictionary<string, string> inputData = new Dictionary<string, string>();

            try
            {
                if (APx?.Sequence?.PreSequenceSteps?.PromptSteps == null || APx.Sequence.PreSequenceSteps.PromptSteps.Count == 0)
                {
                    LogManager.AppendLog("⚠ No Prompt Steps found.");
                    return inputData;
                }

                LogManager.AppendLog("🔍 Retrieving Prompt Step Inputs...");

                foreach (var stepObj in APx.Sequence.PreSequenceSteps.PromptSteps)
                {
                    // Ensure proper casting
                    if (stepObj is AudioPrecision.API.IPromptStep promptStep)
                    {
                        if (promptStep.Inputs != null)
                        {
                            for (int i = 0; i < promptStep.Inputs.Count; i++)
                            {
                                var input = promptStep.Inputs[i];
                                inputData[input.Label] = input.DefaultResponse; // Save Label & Default Value
                            }
                        }
                    }
                    else
                    {
                        LogManager.AppendLog("⚠ Unable to cast stepObj to IPromptStep.");
                    }
                }

                LogManager.AppendLog($"✅ Retrieved {inputData.Count} input(s).");
            }
            catch (Exception ex)
            {
                LogManager.AppendLog($"❌ ERROR retrieving prompt step inputs: {ex.Message}");
            }

            return inputData;
        }

        public void LogPreSequenceSteps()
        {
            try
            {
                if (APx?.Sequence?.PreSequenceSteps == null)
                {
                    LogManager.AppendLog("⚠ No Pre-Sequence Steps found.");
                    return;
                }

                LogManager.AppendLog("🔍 Extracting Pre-Sequence Steps...");
                var preSequenceSteps = APx.Sequence.PreSequenceSteps.PromptSteps;

                if (preSequenceSteps == null || preSequenceSteps.Count == 0)
                {
                    LogManager.AppendLog("⚠ No Prompt Steps found in Pre-Sequence.");
                    return;
                }

                Dictionary<int, string> inputMap = new Dictionary<int, string>();

                int stepIndex = 0;
                foreach (var stepObj in preSequenceSteps)
                {
                    // Ensure stepObj is an IPromptStep before accessing properties
                    if (stepObj is AudioPrecision.API.IPromptStep promptStep)
                    {
                        LogManager.AppendLog($"🔍 Checking step {stepIndex + 1}: {promptStep.GetType().FullName}");
                        LogManager.AppendLog($"  ✅ Successfully cast to IPromptStep.");
                        LogManager.AppendLog($"    - Step Name: {promptStep.Name}");

                        if (promptStep.Inputs != null && promptStep.Inputs.Count > 0)
                        {
                            LogManager.AppendLog("    📌 Inputs:");

                            for (int i = 0; i < promptStep.Inputs.Count; i++)
                            {
                                var input = promptStep.Inputs[i];
                                string variableName = input.VariableName ?? "None";
                                inputMap[i] = variableName;

                                LogManager.AppendLog($"      - Index: {i}");
                                LogManager.AppendLog($"      - Label: {input.Label}");
                                LogManager.AppendLog($"        - Persist: {input.Persist}");
                                LogManager.AppendLog($"        - Required: {input.Required}");
                                LogManager.AppendLog($"        - Variable Name: {variableName}");

                                // Try retrieving default response
                                try
                                {
                                    string defaultValue = input.DefaultResponse ?? "Not Set";
                                    LogManager.AppendLog($"        - Default Value: {defaultValue}");
                                }
                                catch (Exception defaultEx)
                                {
                                    LogManager.AppendLog($"        - ⚠ Error retrieving default value: {defaultEx.Message}");
                                }
                            }
                        }
                        else
                        {
                            LogManager.AppendLog("    ⚠ No Inputs found.");
                        }
                    }
                    else
                    {
                        LogManager.AppendLog($"  ⚠ Could not cast step {stepIndex + 1} to IPromptStep.");
                    }

                    stepIndex++;
                }

                LogManager.AppendLog("✅ Finished logging Pre-Sequence Steps.");
            }
            catch (Exception ex)
            {
                LogManager.AppendLog($"❌ ERROR extracting Pre-Sequence Steps: {ex.Message}");
            }
        }

        public void SetDefaultValues(Dictionary<string, string> updatedValues)
        {
            try
            {
                if (APx?.Sequence?.PreSequenceSteps?.PromptSteps == null)
                {
                    LogManager.AppendLog("⚠ No Prompt Steps found.");
                    return;
                }

                LogManager.AppendLog("🔄 Updating Default Values...");
                foreach (var stepObj in APx.Sequence.PreSequenceSteps.PromptSteps)
                {
                    // Ensure proper casting
                    if (stepObj is AudioPrecision.API.IPromptStep promptStep)
                    {
                        if (promptStep.Inputs != null)
                        {
                            for (int i = 0; i < promptStep.Inputs.Count; i++)
                            {
                                var input = promptStep.Inputs[i];
                                if (updatedValues.ContainsKey(input.Label))
                                {
                                    input.DefaultResponse = updatedValues[input.Label]; // Update default value
                                    LogManager.AppendLog($"✅ Updated '{input.Label}' to '{input.DefaultResponse}'.");
                                }
                            }
                        }
                    }
                    else
                    {
                        LogManager.AppendLog("⚠ Unable to cast stepObj to IPromptStep.");
                    }
                }

                LogManager.AppendLog("✅ Default values successfully updated.");
            }
            catch (Exception ex)
            {
                LogManager.AppendLog($"❌ ERROR updating default values: {ex.Message}");
            }
        }

    }
}
