// =============================================================================
// EntityModels.cs
// Module 1 – Dynamic Scenario Generation
// Team Sentinels | University of Moratuwa | 2026
//
// Data models for the Scenario.json "entities" and "spawnPoints" sections.
// Entities are the full actor records; spawn points record initial positions
// with room context and facing direction.
// =============================================================================

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace TeamSentinels.ScenarioGeneration.DataModels
{
    // =========================================================================
    // Entity Metadata
    // =========================================================================

    /// <summary>
    /// Entity-specific metadata. Currently stores the NPC's initial behavioural
    /// state (e.g., "idle", "alert"). Extensible for future properties.
    /// </summary>
    [Serializable]
    public class EntityMetadata
    {
        /// <summary>
        /// Initial behavioural state of the entity (e.g., "idle", "alert").
        /// Consumed by Module 2 (NPC Behaviour) to set starting behaviour tree
        /// state.
        /// </summary>
        [JsonProperty("initialState")]
        public string initialState = "idle";
    }

    // =========================================================================
    // Entity Record
    // =========================================================================

    /// <summary>
    /// Full entity record for a single actor (trainee, hostage, or terrorist).
    /// Provides stable entity IDs used across all modules for event correlation,
    /// behaviour routing, and cognitive analysis.
    /// </summary>
    [Serializable]
    public class EntityRecord
    {
        /// <summary>
        /// Stable entity ID used across all modules (e.g., "trainee_01",
        /// "hostage_01", "terrorist_01"). Module 3 uses this for correlating
        /// cognitive events with specific NPCs.
        /// </summary>
        [JsonProperty("id")]
        public string id;

        /// <summary>
        /// Entity actor type: trainee, hostage, or terrorist.
        /// </summary>
        [JsonProperty("type")]
        public EntityType type;

        /// <summary>
        /// Room ID where the entity is initially placed. References a room in
        /// <see cref="LayoutData.rooms"/>.
        /// </summary>
        [JsonProperty("assignedRoom")]
        public string assignedRoom;

        /// <summary>
        /// Entity position in Unity world coordinates within the assigned room.
        /// </summary>
        [JsonProperty("position")]
        public SerializableVector3 position;

        /// <summary>
        /// Entity-specific metadata (e.g., initial behavioural state).
        /// </summary>
        [JsonProperty("metadata")]
        public EntityMetadata metadata = new EntityMetadata();
    }

    // =========================================================================
    // Spawn Point Data
    // =========================================================================

    /// <summary>
    /// Spawn position for an NPC (hostage or terrorist) with room context,
    /// world position, and initial facing direction.
    /// </summary>
    [Serializable]
    public class NpcSpawnPoint
    {
        /// <summary>
        /// Entity ID this spawn point belongs to (e.g., "hostage_01",
        /// "terrorist_01"). References an entry in the entities array.
        /// </summary>
        [JsonProperty("entityId")]
        public string entityId;

        /// <summary>Room ID where the entity spawns.</summary>
        [JsonProperty("roomId")]
        public string roomId;

        /// <summary>Spawn position in Unity world coordinates.</summary>
        [JsonProperty("position")]
        public SerializableVector3 position;

        /// <summary>
        /// Direction the entity faces at spawn. For terrorists, this is
        /// typically toward the nearest door or tactically significant position.
        /// </summary>
        [JsonProperty("facingDirection")]
        public SerializableVector3 facingDirection;
    }

    /// <summary>
    /// Trainee spawn position with room context and facing direction.
    /// The trainee always spawns in an entry room facing into the building.
    /// </summary>
    [Serializable]
    public class TraineeSpawnPoint
    {
        /// <summary>Room ID of the entry room where the trainee spawns.</summary>
        [JsonProperty("roomId")]
        public string roomId;

        /// <summary>Trainee spawn position in Unity world coordinates.</summary>
        [JsonProperty("position")]
        public SerializableVector3 position;

        /// <summary>
        /// Direction the trainee faces at spawn (typically into the building).
        /// </summary>
        [JsonProperty("facingDirection")]
        public SerializableVector3 facingDirection;
    }

    /// <summary>
    /// Container for all actor spawn points. Consumed by the runtime scene
    /// loader to instantiate actors at their initial positions. Module 4 (AAR)
    /// uses these for replay start state reconstruction.
    /// </summary>
    [Serializable]
    public class SpawnPointData
    {
        /// <summary>Trainee (player) spawn point in the entry room.</summary>
        [JsonProperty("trainee")]
        public TraineeSpawnPoint trainee;

        /// <summary>
        /// Hostage spawn points. Currently always a single-element list
        /// (hostageCount = 1).
        /// </summary>
        [JsonProperty("hostages")]
        public List<NpcSpawnPoint> hostages = new List<NpcSpawnPoint>();

        /// <summary>Terrorist spawn points, one per terrorist NPC.</summary>
        [JsonProperty("terrorists")]
        public List<NpcSpawnPoint> terrorists = new List<NpcSpawnPoint>();
    }
}
