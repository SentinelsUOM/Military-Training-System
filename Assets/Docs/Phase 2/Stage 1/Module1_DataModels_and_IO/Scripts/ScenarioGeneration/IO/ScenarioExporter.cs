// =============================================================================
// ScenarioExporter.cs
// Module 1 – Dynamic Scenario Generation
// Team Sentinels | University of Moratuwa | 2026
//
// Serialises a completed ScenarioData object to Scenario.json and writes it
// to disk. Supports both indented (development) and compact (production)
// output formats. Also provides a loader for reading previously generated
// Scenario.json files back into ScenarioData objects.
// =============================================================================

using System;
using System.IO;
using Newtonsoft.Json;
using TeamSentinels.ScenarioGeneration.DataModels;
using UnityEngine;

namespace TeamSentinels.ScenarioGeneration.IO
{
    /// <summary>
    /// Exports completed <see cref="ScenarioData"/> to Scenario.json files
    /// and loads previously generated scenarios. This is the final step of the
    /// Module 1 generation pipeline — the output file is consumed by
    /// Modules 2, 3, and 4 at runtime.
    /// </summary>
    public static class ScenarioExporter
    {
        /// <summary>
        /// Default output directory relative to the Unity project root.
        /// </summary>
        public const string DefaultOutputDirectory = "Output/GeneratedScenarios";

        // =====================================================================
        // Export (Write)
        // =====================================================================

        /// <summary>
        /// Serialises a <see cref="ScenarioData"/> to JSON and writes it to
        /// the specified file path.
        /// </summary>
        /// <param name="scenario">The completed scenario data to export.</param>
        /// <param name="filePath">
        /// Full path for the output file. If null, a default path is generated
        /// using the scenario ID under <see cref="DefaultOutputDirectory"/>.
        /// </param>
        /// <param name="compact">
        /// If true, writes compact JSON (no indentation) for smaller file size.
        /// Default is false (indented for development readability).
        /// </param>
        /// <returns>The absolute path of the written file.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="scenario"/> is null.
        /// </exception>
        /// <exception cref="IOException">
        /// Thrown when the file cannot be written.
        /// </exception>
        public static string ExportToFile(
            ScenarioData scenario,
            string filePath = null,
            bool compact = false)
        {
            if (scenario == null)
            {
                throw new ArgumentNullException(nameof(scenario),
                    "ScenarioData is null. Cannot export.");
            }

            // Generate default path if none specified
            if (string.IsNullOrEmpty(filePath))
            {
                string directory = Path.Combine(
                    Application.dataPath, "..", DefaultOutputDirectory);
                filePath = Path.Combine(directory,
                    $"Scenario_{scenario.scenarioId}.json");
            }

            // Ensure directory exists
            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // Serialise
            string json = SerialiseToJson(scenario, compact);

            // Write
            File.WriteAllText(filePath, json);

            string absolutePath = Path.GetFullPath(filePath);
            Debug.Log($"[ScenarioExporter] Exported scenario '{scenario.scenarioId}' " +
                $"to: {absolutePath} ({json.Length} chars, " +
                $"{(compact ? "compact" : "indented")})");

            return absolutePath;
        }

        /// <summary>
        /// Serialises a <see cref="ScenarioData"/> to a JSON string without
        /// writing to disk. Useful for network transmission or in-memory
        /// processing.
        /// </summary>
        /// <param name="scenario">The scenario data to serialise.</param>
        /// <param name="compact">
        /// If true, produces compact JSON. Default is false (indented).
        /// </param>
        /// <returns>JSON string representation of the scenario.</returns>
        public static string SerialiseToJson(
            ScenarioData scenario, bool compact = false)
        {
            if (scenario == null)
            {
                throw new ArgumentNullException(nameof(scenario));
            }

            var settings = compact
                ? ScenarioJsonSettings.CompactWriterSettings
                : ScenarioJsonSettings.WriterSettings;

            return JsonConvert.SerializeObject(scenario, settings);
        }

        // =====================================================================
        // Import (Read)
        // =====================================================================

        /// <summary>
        /// Loads a previously generated <see cref="ScenarioData"/> from a
        /// Scenario.json file. Used by the runtime scene loader and by
        /// Module 4 (AAR) for scenario replay.
        /// </summary>
        /// <param name="filePath">Path to the Scenario.json file.</param>
        /// <returns>The deserialised <see cref="ScenarioData"/>.</returns>
        /// <exception cref="FileNotFoundException">
        /// Thrown when the file does not exist.
        /// </exception>
        /// <exception cref="JsonException">
        /// Thrown when the JSON is malformed or cannot be deserialised.
        /// </exception>
        public static ScenarioData LoadFromFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException(
                    $"Scenario file not found: {filePath}", filePath);
            }

            string json = File.ReadAllText(filePath);
            return LoadFromJson(json);
        }

        /// <summary>
        /// Loads a <see cref="ScenarioData"/> from a raw JSON string.
        /// </summary>
        /// <param name="json">Raw JSON string containing the Scenario data.</param>
        /// <returns>The deserialised <see cref="ScenarioData"/>.</returns>
        public static ScenarioData LoadFromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException(
                    "Scenario JSON string is null or empty.", nameof(json));
            }

            ScenarioData scenario = JsonConvert.DeserializeObject<ScenarioData>(
                json, ScenarioJsonSettings.ReaderSettings);

            if (scenario == null)
            {
                throw new JsonException(
                    "Failed to deserialise ScenarioData: result was null.");
            }

            Debug.Log($"[ScenarioExporter] Loaded scenario '{scenario.scenarioId}' " +
                $"with {scenario.entities?.Count ?? 0} entities, " +
                $"{scenario.layout?.rooms?.Count ?? 0} rooms.");

            return scenario;
        }

        /// <summary>
        /// Loads a <see cref="ScenarioData"/> from a Unity TextAsset.
        /// </summary>
        /// <param name="textAsset">TextAsset containing the Scenario JSON.</param>
        /// <returns>The deserialised <see cref="ScenarioData"/>.</returns>
        public static ScenarioData LoadFromTextAsset(TextAsset textAsset)
        {
            if (textAsset == null)
            {
                throw new ArgumentNullException(nameof(textAsset),
                    "Scenario TextAsset is null.");
            }

            return LoadFromJson(textAsset.text);
        }

        // =====================================================================
        // Utility
        // =====================================================================

        /// <summary>
        /// Returns the default output file path for a given scenario ID.
        /// </summary>
        /// <param name="scenarioId">The scenario's UUID.</param>
        /// <returns>Full file path under the default output directory.</returns>
        public static string GetDefaultOutputPath(string scenarioId)
        {
            string directory = Path.Combine(
                Application.dataPath, "..", DefaultOutputDirectory);
            return Path.Combine(directory, $"Scenario_{scenarioId}.json");
        }

        /// <summary>
        /// Lists all Scenario.json files in the default output directory.
        /// </summary>
        /// <returns>Array of file paths, or empty array if directory doesn't exist.</returns>
        public static string[] ListGeneratedScenarios()
        {
            string directory = Path.Combine(
                Application.dataPath, "..", DefaultOutputDirectory);

            if (!Directory.Exists(directory))
            {
                return Array.Empty<string>();
            }

            return Directory.GetFiles(directory, "Scenario_*.json");
        }
    }
}
