// =============================================================================
// ScenarioData.cs
// Module 1 – Dynamic Scenario Generation
// Team Sentinels | University of Moratuwa | 2026
//
// Top-level Scenario.json output data model and ConfigurationMetadata.
// ScenarioData is the complete generation output from Module 1, consumed by
// Modules 2 (NPC Behaviour), 3 (Cognitive Analysis), and 4 (Logging/AAR).
//
// Schema version: 1.0.0 (single-floor, hostageCount = 1).
// =============================================================================

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace TeamSentinels.ScenarioGeneration.DataModels
{
    // =========================================================================
    // Validation Result
    // =========================================================================

    /// <summary>
    /// Records the outcome of the scenario validation pipeline. Embedded in
    /// <see cref="ConfigurationMetadata"/> to allow downstream modules and
    /// evaluators to verify generation quality without re-running validation.
    /// </summary>
    [Serializable]
    public class ValidationResult
    {
        /// <summary>True if all validation checks passed.</summary>
        [JsonProperty("passed")]
        public bool passed;

        /// <summary>Total number of validation checks executed.</summary>
        [JsonProperty("checksRun")]
        public int checksRun;

        /// <summary>Number of validation checks that passed.</summary>
        [JsonProperty("checksPassed")]
        public int checksPassed;

        /// <summary>
        /// Non-fatal warning messages from validation (e.g., suboptimal
        /// placement that does not prevent mission completion).
        /// </summary>
        [JsonProperty("warnings")]
        public List<string> warnings = new List<string>();

        /// <summary>
        /// Creates a passing validation result with no warnings.
        /// </summary>
        public static ValidationResult CreatePassed(int checksRun)
        {
            return new ValidationResult
            {
                passed = true,
                checksRun = checksRun,
                checksPassed = checksRun,
                warnings = new List<string>()
            };
        }

        /// <summary>
        /// Creates a failing validation result.
        /// </summary>
        public static ValidationResult CreateFailed(
            int checksRun, int checksPassed, List<string> warnings)
        {
            return new ValidationResult
            {
                passed = false,
                checksRun = checksRun,
                checksPassed = checksPassed,
                warnings = warnings ?? new List<string>()
            };
        }
    }

    // =========================================================================
    // Configuration Metadata
    // =========================================================================

    /// <summary>
    /// Embeds the complete original <see cref="ScenarioConfig"/> plus generation
    /// metadata. Enables full traceability from output back to evaluator inputs,
    /// satisfying [22]'s requirement for traceable PCG evaluation.
    /// </summary>
    [Serializable]
    public class ConfigurationMetadata
    {
        /// <summary>
        /// Complete copy of the ScenarioConfig.json used as input. Allows any
        /// downstream module to access the original evaluator parameters
        /// without requiring the input file.
        /// </summary>
        [JsonProperty("scenarioConfig")]
        public ScenarioConfig scenarioConfig;

        /// <summary>
        /// ISO 8601 timestamp of when generation completed (e.g.,
        /// "2026-04-21T14:30:00Z"). Used by Module 4 (AAR) for session
        /// identification.
        /// </summary>
        [JsonProperty("generationTimestamp")]
        public string generationTimestamp;

        /// <summary>
        /// Actual seed used for generation. If the evaluator specified a seed,
        /// this matches it. If seed was null (auto-generate), this records the
        /// system-generated seed for exact reproduction.
        /// </summary>
        [JsonProperty("seedUsed")]
        public int seedUsed;

        /// <summary>
        /// Semantic version of the generation pipeline (e.g., "1.0.0").
        /// Allows tracking which generator version produced a given scenario.
        /// </summary>
        [JsonProperty("generatorVersion")]
        public string generatorVersion = "1.0.0";

        /// <summary>Outcome of the validation pipeline.</summary>
        [JsonProperty("validationResult")]
        public ValidationResult validationResult;
    }

    // =========================================================================
    // Scenario Data (Top-Level Root)
    // =========================================================================

    /// <summary>
    /// Root data model for Scenario.json — the complete generation output from
    /// Module 1. This is the most critical shared artifact in the system,
    /// consumed by:
    /// <list type="bullet">
    ///   <item><b>Module 2 (NPC Behaviour):</b> roleAssignments, navigationContext,
    ///   entities, layout.rooms, configurationMetadata.difficultyLevel</item>
    ///   <item><b>Module 3 (Cognitive Analysis):</b> layout.rooms (zone IDs, depth,
    ///   zoneLabel), entities (actor IDs), configurationMetadata</item>
    ///   <item><b>Module 4 (Logging/AAR):</b> scenarioId, layout (room positions,
    ///   boundingBox, zoneLabels), entities, spawnPoints, configurationMetadata</item>
    /// </list>
    /// </summary>
    [Serializable]
    public class ScenarioData
    {
        /// <summary>
        /// Unique UUID v4 identifier for this generated scenario instance.
        /// Links session logs, replay data, and summaries across all modules.
        /// </summary>
        [JsonProperty("scenarioId")]
        public string scenarioId;

        /// <summary>
        /// Mission template used (mirrors ScenarioConfig input).
        /// Currently always "hostage_rescue".
        /// </summary>
        [JsonProperty("missionType")]
        public MissionType missionType = MissionType.HostageRescue;

        /// <summary>
        /// Complete room graph: rooms array, entry points, and layout metadata.
        /// The primary spatial data structure for the entire system.
        /// </summary>
        [JsonProperty("layout")]
        public LayoutData layout;

        /// <summary>
        /// Initial positions for all actors (trainee, hostages, terrorists).
        /// Used by the runtime scene loader and Module 4 (AAR) for replay
        /// start state reconstruction.
        /// </summary>
        [JsonProperty("spawnPoints")]
        public SpawnPointData spawnPoints;

        /// <summary>
        /// Full entity records for every actor. Provides stable entity IDs
        /// used across all modules for event correlation, behaviour routing,
        /// and cognitive analysis.
        /// </summary>
        [JsonProperty("entities")]
        public List<EntityRecord> entities = new List<EntityRecord>();

        /// <summary>
        /// One entry per terrorist NPC linking role, priority, and navigation
        /// context. Consumed by Module 2 (NPC Behaviour) to initialise
        /// behaviour trees.
        /// </summary>
        [JsonProperty("roleAssignments")]
        public List<RoleAssignment> roleAssignments = new List<RoleAssignment>();

        /// <summary>
        /// Dictionary keyed by navigationContextId. Each entry contains
        /// role-specific movement data consumed by Module 2 (NPC Behaviour)
        /// to drive patrol, guard, and roaming behaviours at runtime.
        /// </summary>
        [JsonProperty("navigationContext")]
        public Dictionary<string, NavigationContextEntry> navigationContext =
            new Dictionary<string, NavigationContextEntry>();

        /// <summary>
        /// Original ScenarioConfig, generation timestamp, seed used, generator
        /// version, and validation result. Enables full traceability from
        /// output back to evaluator inputs [22].
        /// </summary>
        [JsonProperty("configurationMetadata")]
        public ConfigurationMetadata configurationMetadata;
    }
}
