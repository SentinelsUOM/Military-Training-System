// =============================================================================
// ScenarioConfigLoader.cs
// Module 1 – Dynamic Scenario Generation
// Team Sentinels | University of Moratuwa | 2026
//
// Reads ScenarioConfig.json from a file path, deserialises it into a
// ScenarioConfig object, and runs schema validation rules before returning
// the config to the generation pipeline.
//
// Validation rules implemented (from ScenarioConfig Schema Design §9):
//   - roomCount.min ≤ roomCount.max
//   - terroristCount ≤ roomCount.max × 2
//   - hostageCount = 1
//   - seed range: ±2,147,483,647 (System.Random)
//   - customLabel max length: 128
//   - difficultyLevel ∈ [1, 5]
//   - terroristCount ∈ [1, 8]
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using TeamSentinels.ScenarioGeneration.DataModels;
using UnityEngine;

namespace TeamSentinels.ScenarioGeneration.IO
{
    /// <summary>
    /// Loads and validates ScenarioConfig.json files. This is the entry point
    /// for the Module 1 generation pipeline — every generation run begins with
    /// loading a config file through this class.
    /// </summary>
    public static class ScenarioConfigLoader
    {
        // =====================================================================
        // Public API
        // =====================================================================

        /// <summary>
        /// Loads a ScenarioConfig from a JSON file at the specified path.
        /// Performs schema validation and returns the deserialised config.
        /// </summary>
        /// <param name="filePath">
        /// Absolute or relative path to the ScenarioConfig.json file.
        /// In Unity, this is typically under Resources/ScenarioConfigs/.
        /// </param>
        /// <returns>A validated <see cref="ScenarioConfig"/> instance.</returns>
        /// <exception cref="FileNotFoundException">
        /// Thrown when the config file does not exist at the specified path.
        /// </exception>
        /// <exception cref="ScenarioConfigValidationException">
        /// Thrown when the config fails one or more validation rules.
        /// </exception>
        /// <exception cref="JsonException">
        /// Thrown when the JSON is malformed or cannot be deserialised.
        /// </exception>
        public static ScenarioConfig LoadFromFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException(
                    $"ScenarioConfig file not found: {filePath}", filePath);
            }

            string json = File.ReadAllText(filePath);
            return LoadFromJson(json);
        }

        /// <summary>
        /// Loads a ScenarioConfig from a raw JSON string. Useful for loading
        /// configs from Unity TextAssets or network responses.
        /// </summary>
        /// <param name="json">Raw JSON string containing the ScenarioConfig.</param>
        /// <returns>A validated <see cref="ScenarioConfig"/> instance.</returns>
        /// <exception cref="ScenarioConfigValidationException">
        /// Thrown when the config fails one or more validation rules.
        /// </exception>
        public static ScenarioConfig LoadFromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException(
                    "ScenarioConfig JSON string is null or empty.", nameof(json));
            }

            ScenarioConfig config = JsonConvert.DeserializeObject<ScenarioConfig>(
                json, ScenarioJsonSettings.ReaderSettings);

            if (config == null)
            {
                throw new JsonException("Failed to deserialise ScenarioConfig: result was null.");
            }

            // Apply defaults for any null sub-objects (defensive)
            config.missionStructure ??= new MissionStructure();
            config.entityConfiguration ??= new EntityConfiguration();
            config.executionControls ??= new ExecutionControls();
            config.missionStructure.roomCount ??= new RoomCountRange();

            // Validate
            List<string> errors = Validate(config);
            if (errors.Count > 0)
            {
                string message = "ScenarioConfig validation failed:\n  - " +
                    string.Join("\n  - ", errors);
                Debug.LogError(message);
                throw new ScenarioConfigValidationException(message, errors);
            }

            Debug.Log($"[ScenarioConfigLoader] Loaded valid config: " +
                $"schema={config.schemaVersion}, " +
                $"layout={config.missionStructure.layoutType}, " +
                $"rooms=[{config.missionStructure.roomCount.min}-{config.missionStructure.roomCount.max}], " +
                $"terrorists={config.entityConfiguration.terroristCount}, " +
                $"difficulty={config.executionControls.difficultyLevel}");

            return config;
        }

        /// <summary>
        /// Loads a ScenarioConfig from a Unity TextAsset (e.g., loaded via
        /// <c>Resources.Load&lt;TextAsset&gt;("ScenarioConfigs/default")</c>).
        /// </summary>
        /// <param name="textAsset">TextAsset containing the config JSON.</param>
        /// <returns>A validated <see cref="ScenarioConfig"/> instance.</returns>
        public static ScenarioConfig LoadFromTextAsset(TextAsset textAsset)
        {
            if (textAsset == null)
            {
                throw new ArgumentNullException(nameof(textAsset),
                    "ScenarioConfig TextAsset is null.");
            }

            return LoadFromJson(textAsset.text);
        }

        // =====================================================================
        // Validation
        // =====================================================================

        /// <summary>
        /// Validates a <see cref="ScenarioConfig"/> against all schema rules.
        /// Returns a list of error messages (empty if valid).
        /// </summary>
        /// <param name="config">The config to validate.</param>
        /// <returns>List of validation error messages.</returns>
        public static List<string> Validate(ScenarioConfig config)
        {
            var errors = new List<string>();

            // Schema version check
            if (config.schemaVersion != "1.0.0")
            {
                errors.Add($"Unsupported schemaVersion '{config.schemaVersion}'. " +
                    "Expected '1.0.0'.");
            }

            // ── Mission Structure ───────────────────────────────────────────
            var ms = config.missionStructure;

            // Room count range validation
            if (ms.roomCount != null)
            {
                if (!ms.roomCount.Validate(out string roomError))
                {
                    errors.Add(roomError);
                }
            }
            else
            {
                errors.Add("missionStructure.roomCount is required.");
            }

            // ── Entity Configuration ────────────────────────────────────────
            var ec = config.entityConfiguration;

            // hostageCount must be 1
            if (ec.hostageCount != 1)
            {
                errors.Add($"hostageCount must be 1 (got {ec.hostageCount}). " +
                    "Variable hostage count reserved for future versions.");
            }

            // terroristCount in [1, 8]
            if (ec.terroristCount < 1 || ec.terroristCount > 8)
            {
                errors.Add($"terroristCount ({ec.terroristCount}) must be in range [1, 8].");
            }

            // terroristCount ≤ roomCount.max × 2 (overcrowding check)
            if (ms.roomCount != null && ec.terroristCount > ms.roomCount.max * 2)
            {
                errors.Add($"terroristCount ({ec.terroristCount}) exceeds " +
                    $"roomCount.max × 2 ({ms.roomCount.max * 2}). " +
                    "This would cause overcrowding.");
            }

            // ── Execution Controls ──────────────────────────────────────────
            var ex = config.executionControls;

            // difficultyLevel in [1, 5]
            if (ex.difficultyLevel < 1 || ex.difficultyLevel > 5)
            {
                errors.Add($"difficultyLevel ({ex.difficultyLevel}) must be in range [1, 5].");
            }

            // seed range (32-bit signed integer)
            if (ex.seed.HasValue)
            {
                // int? already constrains to Int32 range, but explicit check
                // documents the requirement
                if (ex.seed.Value < int.MinValue || ex.seed.Value > int.MaxValue)
                {
                    errors.Add($"seed ({ex.seed.Value}) must fit within 32-bit " +
                        "signed integer range.");
                }
            }

            // timeLimit range [60, 1800] if specified
            if (ex.timeLimit.HasValue)
            {
                if (ex.timeLimit.Value < 60 || ex.timeLimit.Value > 1800)
                {
                    errors.Add($"timeLimit ({ex.timeLimit.Value}) must be in " +
                        "range [60, 1800] seconds.");
                }
            }

            // customLabel max length 128
            if (ex.customLabel != null && ex.customLabel.Length > 128)
            {
                errors.Add($"customLabel length ({ex.customLabel.Length}) exceeds " +
                    "maximum of 128 characters.");
            }

            return errors;
        }

        /// <summary>
        /// Convenience method that returns true if the config is valid.
        /// </summary>
        /// <param name="config">The config to validate.</param>
        /// <param name="errors">Output list of error messages.</param>
        /// <returns>True if the config passes all validation rules.</returns>
        public static bool IsValid(ScenarioConfig config, out List<string> errors)
        {
            errors = Validate(config);
            return errors.Count == 0;
        }
    }

    // =========================================================================
    // Custom Exception
    // =========================================================================

    /// <summary>
    /// Thrown when a ScenarioConfig fails validation. Contains the list of
    /// specific validation errors for diagnostic reporting.
    /// </summary>
    public class ScenarioConfigValidationException : Exception
    {
        /// <summary>Individual validation error messages.</summary>
        public List<string> ValidationErrors { get; }

        public ScenarioConfigValidationException(
            string message, List<string> errors)
            : base(message)
        {
            ValidationErrors = errors ?? new List<string>();
        }
    }
}
