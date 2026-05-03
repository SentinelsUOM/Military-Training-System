// =============================================================================
// ScenarioGeneratorEditor.cs
// Module 1 - Dynamic Scenario Generation (Editor tooling)
// Team Sentinels | University of Moratuwa | 2026
//
// Editor-only menu commands for one-click scenario generation during
// development. No play-mode required.
//
// Menu items live under "Tools / Scenario Generator /".
// =============================================================================

#if UNITY_EDITOR
using System;
using System.IO;
using TeamSentinels.ScenarioGeneration.DataModels;
using TeamSentinels.ScenarioGeneration.Generators;
using TeamSentinels.ScenarioGeneration.IO;
using UnityEditor;
using UnityEngine;

namespace TeamSentinels.ScenarioGeneration.Editor
{
    /// <summary>
    /// Editor menu commands that drive the Module 1 generation pipeline from
    /// the Unity menu bar without entering play mode. Each command loads a
    /// canned <see cref="ScenarioConfig"/> (or one selected via file dialog),
    /// runs <see cref="ScenarioGenerator"/>, exports the result with
    /// <see cref="ScenarioExporter"/>, and surfaces success/failure through
    /// progress bars and dialogs.
    /// </summary>
    public static class ScenarioGeneratorEditor
    {
        // ── Constants ────────────────────────────────────────────────────────

        private const string ConfigsRelative = "Module1_DataModels_and_IO/Resources/ScenarioConfigs";
        private const string OutputRelative  = "Module1_DataModels_and_IO/Output/GeneratedScenarios";

        private const string DefaultConfig    = "default_config.json";
        private const string MinimalConfig    = "test_minimal_easy.json";
        private const string MaxConfig        = "test_max_difficulty.json";

        private const string MenuRoot = "Tools/Scenario Generator/";

        // ── Menu items: canned configs ──────────────────────────────────────

        [MenuItem(MenuRoot + "Generate Default Scenario", priority = 0)]
        public static void GenerateDefaultScenario()
            => GenerateFromConfigPath(GetConfigPath(DefaultConfig), "Default");

        [MenuItem(MenuRoot + "Generate Minimal (Easy)", priority = 1)]
        public static void GenerateMinimalScenario()
            => GenerateFromConfigPath(GetConfigPath(MinimalConfig), "Minimal (Easy)");

        [MenuItem(MenuRoot + "Generate Max Difficulty", priority = 2)]
        public static void GenerateMaxDifficultyScenario()
            => GenerateFromConfigPath(GetConfigPath(MaxConfig), "Max Difficulty");

        // ── Menu item: custom config via file dialog ────────────────────────

        [MenuItem(MenuRoot + "Generate from Custom Config...", priority = 3)]
        public static void GenerateFromCustomConfig()
        {
            string startDir = GetConfigsDirectory();
            if (!Directory.Exists(startDir)) startDir = Application.dataPath;

            string path = EditorUtility.OpenFilePanel(
                "Select ScenarioConfig.json", startDir, "json");

            if (string.IsNullOrEmpty(path))
                return; // user cancelled

            GenerateFromConfigPath(path, Path.GetFileName(path));
        }

        // ── Menu item: open the output folder ────────────────────────────────

        [MenuItem(MenuRoot + "Open Output Folder", priority = 100)]
        public static void OpenOutputFolder()
        {
            string outputDir = GetOutputDirectory();
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
                Debug.Log($"[ScenarioGeneratorEditor] Created missing output directory: {outputDir}");
            }
            EditorUtility.RevealInFinder(outputDir);
        }

        // ── Core: generate -> export -> report ───────────────────────────────

        private static void GenerateFromConfigPath(string configPath, string label)
        {
            try
            {
                if (!File.Exists(configPath))
                {
                    string err = $"Config file not found:\n{configPath}";
                    Debug.LogError($"[ScenarioGeneratorEditor] {err}");
                    EditorUtility.DisplayDialog("Scenario Generator - Failed", err, "OK");
                    return;
                }

                EditorUtility.DisplayProgressBar(
                    "Scenario Generator", $"Loading config '{label}'...", 0.1f);
                ScenarioConfig config = ScenarioConfigLoader.LoadFromFile(configPath);

                EditorUtility.DisplayProgressBar(
                    "Scenario Generator", $"Generating '{label}'...", 0.4f);
                var generator = new ScenarioGenerator();
                ScenarioData scenario = generator.Generate(config);

                EditorUtility.DisplayProgressBar(
                    "Scenario Generator", $"Exporting '{label}'...", 0.85f);
                string outPath = Path.Combine(GetOutputDirectory(),
                    $"Scenario_{scenario.scenarioId}.json");
                string absolutePath = ScenarioExporter.ExportToFile(scenario, outPath);

                AssetDatabase.Refresh();

                int rooms    = scenario.layout?.rooms?.Count ?? 0;
                int entities = scenario.entities?.Count ?? 0;
                int seedUsed = scenario.configurationMetadata?.seedUsed ?? 0;
                bool passed  = scenario.configurationMetadata?.validationResult?.passed ?? false;

                Debug.Log($"[ScenarioGeneratorEditor] Generated '{label}' -> {absolutePath}");

                string body =
                    $"Config:    {label}\n" +
                    $"Rooms:     {rooms}\n" +
                    $"Entities:  {entities}\n" +
                    $"Seed used: {seedUsed}\n" +
                    $"Validator: {(passed ? "PASSED" : "FAILED (saved anyway for inspection)")}\n\n" +
                    $"Output:\n{absolutePath}";

                EditorUtility.DisplayDialog(
                    passed ? "Scenario Generator - Success" : "Scenario Generator - Validation Warning",
                    body,
                    passed ? "OK" : "Inspect Output");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ScenarioGeneratorEditor] Generation '{label}' failed: " +
                               $"{ex.Message}\n{ex.StackTrace}");
                EditorUtility.DisplayDialog(
                    "Scenario Generator - Failed",
                    $"Generation '{label}' failed:\n\n{ex.Message}\n\n" +
                    $"See the Console for the full stack trace.",
                    "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        // ── Path helpers ─────────────────────────────────────────────────────

        private static string GetConfigsDirectory()
            => Path.Combine(Application.dataPath, ConfigsRelative);

        private static string GetConfigPath(string filename)
            => Path.Combine(GetConfigsDirectory(), filename);

        private static string GetOutputDirectory()
        {
            string dir = Path.Combine(Application.dataPath, OutputRelative);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return dir;
        }
    }
}
#endif
