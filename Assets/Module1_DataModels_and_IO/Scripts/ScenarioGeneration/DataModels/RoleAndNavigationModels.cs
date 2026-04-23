// =============================================================================
// RoleAndNavigationModels.cs
// Module 1 – Dynamic Scenario Generation
// Team Sentinels | University of Moratuwa | 2026
//
// Data models for the Scenario.json "roleAssignments" and "navigationContext"
// sections. Role assignments link each terrorist to a role type and navigation
// context entry. Navigation context provides role-specific movement data
// consumed by Module 2 (NPC Behaviour) at runtime.
//
// Navigation context uses a single class with nullable role-specific fields
// rather than a class hierarchy, matching the JSON schema's flat
// additionalProperties structure with a "type" discriminator.
// =============================================================================

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace TeamSentinels.ScenarioGeneration.DataModels
{
    // =========================================================================
    // Role Assignment
    // =========================================================================

    /// <summary>
    /// Links a terrorist entity to its assigned role, priority level, primary
    /// room, and navigation context. Consumed by Module 2 (NPC Behaviour) to
    /// initialise behaviour trees for each terrorist NPC.
    /// </summary>
    [Serializable]
    public class RoleAssignment
    {
        /// <summary>
        /// References the terrorist in the entities array (e.g., "terrorist_01").
        /// </summary>
        [JsonProperty("entityId")]
        public string entityId;

        /// <summary>
        /// Assigned NPC role: patrol, stationary_guard, roaming_guard, or
        /// hostage_guardian. Derived from spatial structure and difficulty [18].
        /// </summary>
        [JsonProperty("role")]
        public NpcRole role;

        /// <summary>
        /// Response priority (1 = highest). Used by Module 2 for responder
        /// selection during alert events.
        /// </summary>
        [JsonProperty("priorityLevel")]
        public int priorityLevel;

        /// <summary>
        /// Primary room this NPC is responsible for. References a room in
        /// <see cref="LayoutData.rooms"/>.
        /// </summary>
        [JsonProperty("assignedRoomId")]
        public string assignedRoomId;

        /// <summary>
        /// Key into the <see cref="ScenarioData.navigationContext"/> dictionary
        /// for this NPC's movement data (e.g., "nav_terrorist_01").
        /// </summary>
        [JsonProperty("navigationContextId")]
        public string navigationContextId;
    }

    // =========================================================================
    // Navigation Context Entry
    // =========================================================================

    /// <summary>
    /// Role-specific navigation data for a single NPC. Uses a flat structure
    /// with a <see cref="type"/> discriminator field and nullable role-specific
    /// properties, matching the JSON schema's additionalProperties pattern.
    ///
    /// <para><b>Field usage by role:</b></para>
    /// <list type="bullet">
    ///   <item><b>patrol:</b> type, patrolRoute, waypoints, looping</item>
    ///   <item><b>stationary_guard:</b> type, guardRoomId, guardPosition, facingDirection</item>
    ///   <item><b>roaming_guard:</b> type, roamingRoomIds, waypoints, anchorRoomId</item>
    ///   <item><b>hostage_guardian:</b> type, guardedEntityId, hostageRoomId, guardPosition, facingDirection</item>
    /// </list>
    /// </summary>
    [Serializable]
    public class NavigationContextEntry
    {
        /// <summary>
        /// Navigation context type discriminator. Matches the NPC's assigned role.
        /// </summary>
        [JsonProperty("type")]
        public NavigationContextType type;

        // ── Patrol-specific fields ──────────────────────────────────────────

        /// <summary>
        /// Ordered list of room IDs defining the patrol circuit.
        /// Only used when <see cref="type"/> = patrol.
        /// </summary>
        [JsonProperty("patrolRoute", NullValueHandling = NullValueHandling.Ignore)]
        public List<string> patrolRoute;

        /// <summary>
        /// Whether the patrol loops back to the start (true) or reverses
        /// direction at endpoints (false).
        /// Only used when <see cref="type"/> = patrol.
        /// </summary>
        [JsonProperty("looping", NullValueHandling = NullValueHandling.Ignore)]
        public bool? looping;

        // ── Stationary guard–specific fields ────────────────────────────────

        /// <summary>
        /// Room ID where the stationary guard is stationed.
        /// Only used when <see cref="type"/> = stationary_guard.
        /// </summary>
        [JsonProperty("guardRoomId", NullValueHandling = NullValueHandling.Ignore)]
        public string guardRoomId;

        // ── Roaming guard–specific fields ───────────────────────────────────

        /// <summary>
        /// Set of adjacent room IDs defining the roaming area (max 3 rooms).
        /// Only used when <see cref="type"/> = roaming_guard.
        /// </summary>
        [JsonProperty("roamingRoomIds", NullValueHandling = NullValueHandling.Ignore)]
        public List<string> roamingRoomIds;

        /// <summary>
        /// Primary room the roaming guard returns to when not actively roaming.
        /// Only used when <see cref="type"/> = roaming_guard.
        /// </summary>
        [JsonProperty("anchorRoomId", NullValueHandling = NullValueHandling.Ignore)]
        public string anchorRoomId;

        // ── Hostage guardian–specific fields ────────────────────────────────

        /// <summary>
        /// Entity ID of the hostage being guarded.
        /// Only used when <see cref="type"/> = hostage_guardian.
        /// </summary>
        [JsonProperty("guardedEntityId", NullValueHandling = NullValueHandling.Ignore)]
        public string guardedEntityId;

        /// <summary>
        /// Room ID containing the hostage.
        /// Only used when <see cref="type"/> = hostage_guardian.
        /// </summary>
        [JsonProperty("hostageRoomId", NullValueHandling = NullValueHandling.Ignore)]
        public string hostageRoomId;

        // ── Shared fields (used by multiple roles) ──────────────────────────

        /// <summary>
        /// World positions the NPC visits in order. Used by patrol (circuit
        /// waypoints) and roaming_guard (movement anchor positions).
        /// </summary>
        [JsonProperty("waypoints", NullValueHandling = NullValueHandling.Ignore)]
        public List<SerializableVector3> waypoints;

        /// <summary>
        /// Guard/guardian exact position within the room. Used by
        /// stationary_guard and hostage_guardian.
        /// </summary>
        [JsonProperty("guardPosition", NullValueHandling = NullValueHandling.Ignore)]
        public SerializableVector3 guardPosition;

        /// <summary>
        /// Direction the guard faces. For stationary_guard, typically toward
        /// the nearest door. For hostage_guardian, faces toward the room's
        /// primary entry door.
        /// </summary>
        [JsonProperty("facingDirection", NullValueHandling = NullValueHandling.Ignore)]
        public SerializableVector3 facingDirection;

        // ── Factory Methods ─────────────────────────────────────────────────

        /// <summary>
        /// Creates a patrol navigation context.
        /// </summary>
        /// <param name="patrolRoute">Ordered room IDs for the patrol circuit.</param>
        /// <param name="waypoints">World positions visited in order.</param>
        /// <param name="looping">True to loop; false to reverse at endpoints.</param>
        public static NavigationContextEntry CreatePatrol(
            List<string> patrolRoute,
            List<SerializableVector3> waypoints,
            bool looping)
        {
            return new NavigationContextEntry
            {
                type = NavigationContextType.Patrol,
                patrolRoute = patrolRoute,
                waypoints = waypoints,
                looping = looping
            };
        }

        /// <summary>
        /// Creates a stationary guard navigation context.
        /// </summary>
        /// <param name="guardRoomId">Room where the guard is stationed.</param>
        /// <param name="guardPosition">Exact position within the room.</param>
        /// <param name="facingDirection">Direction the guard faces.</param>
        public static NavigationContextEntry CreateStationaryGuard(
            string guardRoomId,
            SerializableVector3 guardPosition,
            SerializableVector3 facingDirection)
        {
            return new NavigationContextEntry
            {
                type = NavigationContextType.StationaryGuard,
                guardRoomId = guardRoomId,
                guardPosition = guardPosition,
                facingDirection = facingDirection
            };
        }

        /// <summary>
        /// Creates a roaming guard navigation context.
        /// </summary>
        /// <param name="roamingRoomIds">Adjacent rooms defining roaming area.</param>
        /// <param name="waypoints">Movement anchor positions.</param>
        /// <param name="anchorRoomId">Primary return room.</param>
        public static NavigationContextEntry CreateRoamingGuard(
            List<string> roamingRoomIds,
            List<SerializableVector3> waypoints,
            string anchorRoomId)
        {
            return new NavigationContextEntry
            {
                type = NavigationContextType.RoamingGuard,
                roamingRoomIds = roamingRoomIds,
                waypoints = waypoints,
                anchorRoomId = anchorRoomId
            };
        }

        /// <summary>
        /// Creates a hostage guardian navigation context.
        /// </summary>
        /// <param name="guardedEntityId">Entity ID of the guarded hostage.</param>
        /// <param name="hostageRoomId">Room containing the hostage.</param>
        /// <param name="guardPosition">Position near the hostage.</param>
        /// <param name="facingDirection">Direction facing (toward entry door).</param>
        public static NavigationContextEntry CreateHostageGuardian(
            string guardedEntityId,
            string hostageRoomId,
            SerializableVector3 guardPosition,
            SerializableVector3 facingDirection)
        {
            return new NavigationContextEntry
            {
                type = NavigationContextType.HostageGuardian,
                guardedEntityId = guardedEntityId,
                hostageRoomId = hostageRoomId,
                guardPosition = guardPosition,
                facingDirection = facingDirection
            };
        }
    }
}
