// =============================================================================
// ScenarioConfig.cs
// Module 1 – Dynamic Scenario Generation
// Team Sentinels | University of Moratuwa | 2026
//
// Data model for ScenarioConfig.json — the evaluator-defined input parameters
// consumed by the Module 1 generation pipeline. Organised into three nested
// parameter groups: MissionStructure, EntityConfiguration, ExecutionControls.
//
// Schema version: 1.0.0 (single-floor, hostageCount fixed to 1).
// =============================================================================

using System;
using Newtonsoft.Json;

namespace TeamSentinels.ScenarioGeneration.DataModels
{
    // =========================================================================
    // Top-Level Config
    // =========================================================================

    /// <summary>
    /// Root data model for ScenarioConfig.json. Contains all evaluator-defined
    /// parameters that Module 1 consumes before generating a scenario.
    /// The complete config is embedded in the output Scenario.json under
    /// <see cref="ConfigurationMetadata.scenarioConfig"/> for traceability [22].
    /// </summary>
    [Serializable]
    public class ScenarioConfig
    {
        /// <summary>
        /// Schema version identifier for forward compatibility.
        /// Currently fixed to "1.0.0". Will increment when multi-floor support
        /// and variable hostage counts are added.
        /// </summary>
        [JsonProperty("schemaVersion")]
        public string schemaVersion = "1.0.0";

        /// <summary>
        /// Physical layout and building structure parameters.
        /// All scenarios are single-floor at this stage.
        /// </summary>
        [JsonProperty("missionStructure")]
        public MissionStructure missionStructure = new MissionStructure();

        /// <summary>
        /// NPC placement and distribution parameters.
        /// </summary>
        [JsonProperty("entityConfiguration")]
        public EntityConfiguration entityConfiguration = new EntityConfiguration();

        /// <summary>
        /// Generation behaviour, difficulty scaling, and reproducibility parameters.
        /// </summary>
        [JsonProperty("executionControls")]
        public ExecutionControls executionControls = new ExecutionControls();
    }

    // =========================================================================
    // Mission Structure
    // =========================================================================

    /// <summary>
    /// Physical layout and building structure parameters defining the indoor
    /// training environment. All scenarios are single-floor.
    /// </summary>
    [Serializable]
    public class MissionStructure
    {
        /// <summary>
        /// Mission template identifier. Currently only "hostage_rescue" is
        /// supported; reserved for future expansion (e.g., room_clearing,
        /// bomb_disposal).
        /// </summary>
        [JsonProperty("missionType")]
        public MissionType missionType = MissionType.HostageRescue;

        /// <summary>
        /// Min/max range for number of rooms. Generator selects within range
        /// based on randomness level and layout type constraints. Higher counts
        /// increase layout complexity and mission duration.
        /// </summary>
        [JsonProperty("roomCount")]
        public RoomCountRange roomCount = new RoomCountRange(5, 8);

        /// <summary>
        /// Room dimension category mapped to Unity world units:
        /// small = 4×4 m, medium = 6×6 m, large = 8×8 m.
        /// Affects entity placement density and movement space.
        /// </summary>
        [JsonProperty("roomSize")]
        public RoomSizeCategory roomSize = RoomSizeCategory.Medium;

        /// <summary>
        /// Room graph topology constraint. Controls how rooms connect:
        /// linear = sequential chain; branching = tree with decision points;
        /// hub_and_spoke = central room with radiating corridors;
        /// loop = cyclical paths allowing flanking.
        /// </summary>
        [JsonProperty("layoutType")]
        public LayoutType layoutType = LayoutType.Branching;

        /// <summary>
        /// Number of building entry points.
        /// single = one breach point; multiple = 2–3 entry points.
        /// </summary>
        [JsonProperty("entryType")]
        public EntryType entryType = EntryType.Single;
    }

    // =========================================================================
    // Entity Configuration
    // =========================================================================

    /// <summary>
    /// NPC placement and distribution parameters controlling how many entities
    /// are placed and their spatial distribution pattern.
    /// </summary>
    [Serializable]
    public class EntityConfiguration
    {
        /// <summary>
        /// Number of hostage NPCs. Fixed to 1 for the current implementation
        /// stage. Future versions will support range 1–5.
        /// Schema constraint: const = 1.
        /// </summary>
        [JsonProperty("hostageCount")]
        public int hostageCount = 1;

        /// <summary>
        /// Number of terrorist NPCs to place and assign roles (1–8).
        /// Each receives a role assignment (patrol, stationary_guard,
        /// roaming_guard, hostage_guardian) based on spatial structure and
        /// difficulty level.
        /// Schema constraint: terroristCount ≤ roomCount.max × 2.
        /// </summary>
        [JsonProperty("terroristCount")]
        public int terroristCount = 4;

        /// <summary>
        /// Spatial distribution pattern for entity placement.
        /// clustered = entities in adjacent rooms creating compounding tactical
        /// effects [5]; dispersed = spread across layout; front_loaded = near
        /// entry; deep = far from entry in high-depth rooms.
        /// </summary>
        [JsonProperty("placementStrategy")]
        public PlacementStrategy placementStrategy = PlacementStrategy.Dispersed;

        /// <summary>
        /// Proximity constraint between hostage and terrorists using distance
        /// thresholds [16]. high = small max distance (closely guarded);
        /// medium = moderate spacing; low = large min distance (separated).
        /// </summary>
        [JsonProperty("hostageRiskLevel")]
        public HostageRiskLevel hostageRiskLevel = HostageRiskLevel.Medium;
    }

    // =========================================================================
    // Execution Controls
    // =========================================================================

    /// <summary>
    /// Generation behaviour, difficulty scaling, variability, and
    /// reproducibility parameters.
    /// </summary>
    [Serializable]
    public class ExecutionControls
    {
        /// <summary>
        /// Overall difficulty scaling (1 = easiest, 5 = hardest). Higher values
        /// place NPCs in tactically challenging positions such as behind doors
        /// or at sightline intersections [3], increase role assignment complexity,
        /// and tighten navigation context.
        /// </summary>
        [JsonProperty("difficultyLevel")]
        public int difficultyLevel = 3;

        /// <summary>
        /// Structural variation between scenarios generated from the same config.
        /// low = minimal variation; medium = moderate; high = maximum variation
        /// in room positions, sizes, and connectivity [8][13].
        /// </summary>
        [JsonProperty("randomnessLevel")]
        public RandomnessLevel randomnessLevel = RandomnessLevel.Medium;

        /// <summary>
        /// Random seed for reproducible generation. Same seed + same config =
        /// identical output. Null = system-generated random seed.
        /// Must fit within 32-bit signed integer range (±2,147,483,647) to align
        /// with <see cref="System.Random"/>.
        /// The used seed is always recorded in output configurationMetadata.
        /// </summary>
        [JsonProperty("seed", NullValueHandling = NullValueHandling.Include)]
        public int? seed = null;

        /// <summary>
        /// Optional mission time limit in seconds (60–1800). Null = no time
        /// constraint. Passed through to runtime modules; does not affect
        /// generation logic.
        /// </summary>
        [JsonProperty("timeLimit", NullValueHandling = NullValueHandling.Include)]
        public int? timeLimit = null;

        /// <summary>
        /// Evaluator-defined label for tagging the scenario (e.g., session ID,
        /// training cohort). Max 128 characters. Stored in
        /// configurationMetadata for traceability [22].
        /// </summary>
        [JsonProperty("customLabel", NullValueHandling = NullValueHandling.Include)]
        public string customLabel = null;
    }
}
