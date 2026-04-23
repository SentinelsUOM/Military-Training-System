// =============================================================================
// ScenarioEnums.cs
// Module 1 – Dynamic Scenario Generation
// Team Sentinels | University of Moratuwa | 2026
// Unity 2022.3 LTS | Meta Quest 3
//
// Defines all enumeration types used across ScenarioConfig and Scenario data
// models. String-valued enums are serialised via Newtonsoft.Json's
// StringEnumConverter to match the JSON schema's snake_case string values.
// =============================================================================

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Runtime.Serialization;

namespace TeamSentinels.ScenarioGeneration.DataModels
{
    // =========================================================================
    // Mission Structure Enums
    // =========================================================================

    /// <summary>
    /// Mission template identifier. Currently only hostage_rescue is supported;
    /// reserved for future expansion to other tactical scenario types.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum MissionType
    {
        [EnumMember(Value = "hostage_rescue")]
        HostageRescue
    }

    /// <summary>
    /// Room dimension category mapped to Unity world units.
    /// small = 4×4 m, medium = 6×6 m, large = 8×8 m.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum RoomSizeCategory
    {
        [EnumMember(Value = "small")]
        Small,

        [EnumMember(Value = "medium")]
        Medium,

        [EnumMember(Value = "large")]
        Large
    }

    /// <summary>
    /// Room graph topology constraint.
    /// linear = sequential chain; branching = tree with decision points;
    /// hub_and_spoke = central room with radiating corridors;
    /// loop = cyclical paths allowing flanking.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum LayoutType
    {
        [EnumMember(Value = "linear")]
        Linear,

        [EnumMember(Value = "branching")]
        Branching,

        [EnumMember(Value = "hub_and_spoke")]
        HubAndSpoke,

        [EnumMember(Value = "loop")]
        Loop
    }

    /// <summary>
    /// Number of building entry points.
    /// single = one breach point; multiple = 2–3 entry points.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum EntryType
    {
        [EnumMember(Value = "single")]
        Single,

        [EnumMember(Value = "multiple")]
        Multiple
    }

    // =========================================================================
    // Entity Configuration Enums
    // =========================================================================

    /// <summary>
    /// Spatial distribution pattern for entity placement.
    /// clustered = entities in adjacent rooms (compounding tactical effects [5]);
    /// dispersed = spread across layout; front_loaded = near entry;
    /// deep = far from entry in high-depth rooms.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum PlacementStrategy
    {
        [EnumMember(Value = "clustered")]
        Clustered,

        [EnumMember(Value = "dispersed")]
        Dispersed,

        [EnumMember(Value = "front_loaded")]
        FrontLoaded,

        [EnumMember(Value = "deep")]
        Deep
    }

    /// <summary>
    /// Hostage-terrorist proximity constraint level using distance thresholds [16].
    /// high = small max distance (closely guarded);
    /// medium = moderate spacing;
    /// low = large min distance (separated from threats).
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum HostageRiskLevel
    {
        [EnumMember(Value = "low")]
        Low,

        [EnumMember(Value = "medium")]
        Medium,

        [EnumMember(Value = "high")]
        High
    }

    /// <summary>
    /// Structural variation between generated scenarios [8][13].
    /// low = minimal variation from base topology;
    /// medium = moderate randomisation;
    /// high = maximum variation in room positions, sizes, and connectivity.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum RandomnessLevel
    {
        [EnumMember(Value = "low")]
        Low,

        [EnumMember(Value = "medium")]
        Medium,

        [EnumMember(Value = "high")]
        High
    }

    // =========================================================================
    // Scenario Output Enums
    // =========================================================================

    /// <summary>
    /// Room type derived from generation context.
    /// entry = building entry room; corridor = connecting passage;
    /// standard = general-purpose room; hostage_room = room containing the hostage.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum RoomType
    {
        [EnumMember(Value = "entry")]
        Entry,

        [EnumMember(Value = "corridor")]
        Corridor,

        [EnumMember(Value = "standard")]
        Standard,

        [EnumMember(Value = "hostage_room")]
        HostageRoom
    }

    /// <summary>
    /// Wall on which a door is placed.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum WallSide
    {
        [EnumMember(Value = "north")]
        North,

        [EnumMember(Value = "south")]
        South,

        [EnumMember(Value = "east")]
        East,

        [EnumMember(Value = "west")]
        West
    }

    /// <summary>
    /// Entity actor type used across all modules for stable identification.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum EntityType
    {
        [EnumMember(Value = "trainee")]
        Trainee,

        [EnumMember(Value = "hostage")]
        Hostage,

        [EnumMember(Value = "terrorist")]
        Terrorist
    }

    /// <summary>
    /// NPC role type assigned based on spatial structure and difficulty [18].
    /// patrol = traverses a circuit of rooms;
    /// stationary_guard = holds a fixed position;
    /// roaming_guard = moves within a bounded set of adjacent rooms;
    /// hostage_guardian = guards the hostage directly.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum NpcRole
    {
        [EnumMember(Value = "patrol")]
        Patrol,

        [EnumMember(Value = "stationary_guard")]
        StationaryGuard,

        [EnumMember(Value = "roaming_guard")]
        RoamingGuard,

        [EnumMember(Value = "hostage_guardian")]
        HostageGuardian
    }

    /// <summary>
    /// Navigation context type discriminator, matching the role for polymorphic
    /// deserialisation of navigation context entries.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum NavigationContextType
    {
        [EnumMember(Value = "patrol")]
        Patrol,

        [EnumMember(Value = "stationary_guard")]
        StationaryGuard,

        [EnumMember(Value = "roaming_guard")]
        RoamingGuard,

        [EnumMember(Value = "hostage_guardian")]
        HostageGuardian
    }
}
