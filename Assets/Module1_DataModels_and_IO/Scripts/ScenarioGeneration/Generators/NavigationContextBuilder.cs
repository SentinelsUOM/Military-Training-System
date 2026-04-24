// =============================================================================
// NavigationContextBuilder.cs
// Module 1 – Dynamic Scenario Generation
// Team Sentinels | University of Moratuwa | 2026
//
// Generates per-NPC navigation data (patrol routes, guard positions, roaming
// areas, hostage guardian context) based on assigned roles and the spatial
// layout. Implements the context builders from
// NPC_Role_Assignment_Algorithm_Design.md §5.
// =============================================================================

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TeamSentinels.ScenarioGeneration.DataModels;

namespace TeamSentinels.ScenarioGeneration.Generators
{
    /// <summary>
    /// Builds role-specific <see cref="NavigationContextEntry"/> objects for
    /// each terrorist NPC. Output is keyed by <c>RoleAssignment.navigationContextId</c>
    /// and consumed by Module 2 (NPC Behaviour) to drive runtime patrol, guard,
    /// and roaming behaviours. Plain C# class with no <see cref="MonoBehaviour"/>
    /// dependency.
    /// </summary>
    public class NavigationContextBuilder
    {
        /// <summary>
        /// Default facing direction when a room has no doors or the entity sits
        /// exactly on the target door position (degenerate zero-length vector).
        /// Matches the design doc's fallback: <c>(-1, 0, 0)</c>.
        /// </summary>
        private static readonly Vector3 FallbackFacing = new Vector3(-1f, 0f, 0f);

        /// <summary>Maximum rooms included in a roaming guard's area (self + 2 neighbours).</summary>
        private const int MaxRoamingRooms = 3;

        /// <summary>Maximum rooms included in a patrol circuit (excluding return leg).</summary>
        private const int MaxPatrolRooms = 3;

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Builds one <see cref="NavigationContextEntry"/> per role assignment.
        /// The returned dictionary is keyed by each assignment's
        /// <see cref="RoleAssignment.navigationContextId"/> and matches the
        /// Scenario.json <c>navigationContext</c> section exactly.
        /// </summary>
        /// <param name="layout">Generated room graph.</param>
        /// <param name="assignments">Role assignments from <see cref="RoleAssigner"/>.</param>
        /// <param name="entities">Entity records (provides placed positions).</param>
        /// <param name="spawnPoints">Spawn points (reserved for future use).</param>
        /// <param name="rng">Seeded random instance shared across the pipeline.</param>
        /// <returns>Navigation context entries keyed by navigationContextId.</returns>
        public Dictionary<string, NavigationContextEntry> Build(
            LayoutData layout,
            List<RoleAssignment> assignments,
            List<EntityRecord> entities,
            SpawnPointData spawnPoints,
            System.Random rng)
        {
            var contexts  = new Dictionary<string, NavigationContextEntry>(assignments.Count);
            var entityMap = entities.ToDictionary(e => e.id);
            var roomMap   = layout.rooms.ToDictionary(r => r.id);

            foreach (RoleAssignment assignment in assignments)
            {
                if (!entityMap.TryGetValue(assignment.entityId, out EntityRecord entity))
                    continue;
                if (!roomMap.TryGetValue(assignment.assignedRoomId, out RoomData room))
                    continue;

                NavigationContextEntry context;
                switch (assignment.role)
                {
                    case NpcRole.Patrol:
                        context = BuildPatrolContext(room, roomMap);
                        break;
                    case NpcRole.StationaryGuard:
                        context = BuildStationaryGuardContext(room, entity);
                        break;
                    case NpcRole.RoamingGuard:
                        context = BuildRoamingGuardContext(room, roomMap);
                        break;
                    case NpcRole.HostageGuardian:
                        context = BuildHostageGuardianContext(room, entity);
                        break;
                    default:
                        continue;
                }

                contexts[assignment.navigationContextId] = context;
            }

            return contexts;
        }

        // ── Patrol Context ───────────────────────────────────────────────────

        /// <summary>
        /// Builds a looping patrol circuit: assigned room → up to two adjacent
        /// non-entry rooms → return to assigned room. Waypoints are the room
        /// centres visited in order.
        /// </summary>
        private static NavigationContextEntry BuildPatrolContext(
            RoomData assignedRoom, Dictionary<string, RoomData> roomMap)
        {
            var route   = new List<string> { assignedRoom.id };
            var visited = new HashSet<string> { assignedRoom.id };

            foreach (string neighbourId in assignedRoom.connectedRoomIds)
            {
                if (!roomMap.TryGetValue(neighbourId, out RoomData neighbour)) continue;
                if (neighbour.depth == 0) continue;          // never patrol through entry
                if (!visited.Add(neighbourId)) continue;

                route.Add(neighbourId);
                if (route.Count >= MaxPatrolRooms) break;
            }

            // Close the loop by returning to the starting room.
            route.Add(assignedRoom.id);

            var waypoints = new List<SerializableVector3>(route.Count);
            foreach (string roomId in route)
            {
                waypoints.Add(new SerializableVector3(roomMap[roomId].position.ToVector3()));
            }

            return NavigationContextEntry.CreatePatrol(route, waypoints, looping: true);
        }

        // ── Stationary Guard Context ─────────────────────────────────────────

        /// <summary>
        /// Builds a stationary guard context anchored at the entity's already
        /// placed position, facing the nearest door in the assigned room.
        /// </summary>
        private static NavigationContextEntry BuildStationaryGuardContext(
            RoomData room, EntityRecord entity)
        {
            Vector3 guardPos = entity.position.ToVector3();
            Vector3 facing   = ComputeFacing(room, guardPos, useNearestDoor: true);

            return NavigationContextEntry.CreateStationaryGuard(
                room.id,
                new SerializableVector3(guardPos),
                new SerializableVector3(facing));
        }

        // ── Roaming Guard Context ────────────────────────────────────────────

        /// <summary>
        /// Builds a roaming guard context covering the assigned room plus up to
        /// two adjacent non-entry rooms (max three rooms total). Waypoints are
        /// the room centres in order, with an additional return waypoint at the
        /// anchor room.
        /// </summary>
        private static NavigationContextEntry BuildRoamingGuardContext(
            RoomData assignedRoom, Dictionary<string, RoomData> roomMap)
        {
            var roamingIds = new List<string> { assignedRoom.id };

            foreach (string neighbourId in assignedRoom.connectedRoomIds)
            {
                if (!roomMap.TryGetValue(neighbourId, out RoomData neighbour)) continue;
                if (neighbour.depth == 0) continue;          // exclude entry room
                if (roamingIds.Contains(neighbourId)) continue;

                roamingIds.Add(neighbourId);
                if (roamingIds.Count >= MaxRoamingRooms) break;
            }

            var waypoints = new List<SerializableVector3>(roamingIds.Count + 1);
            foreach (string roomId in roamingIds)
            {
                waypoints.Add(new SerializableVector3(roomMap[roomId].position.ToVector3()));
            }
            // Return waypoint at the anchor room so the guard ends each cycle
            // back at its primary room.
            waypoints.Add(new SerializableVector3(assignedRoom.position.ToVector3()));

            return NavigationContextEntry.CreateRoamingGuard(
                roamingIds, waypoints, assignedRoom.id);
        }

        // ── Hostage Guardian Context ─────────────────────────────────────────

        /// <summary>
        /// Builds a hostage guardian context anchored at the entity's already
        /// placed position, guarding <c>hostage_01</c> and facing the room's
        /// primary (first) door.
        /// </summary>
        private static NavigationContextEntry BuildHostageGuardianContext(
            RoomData room, EntityRecord entity)
        {
            Vector3 guardPos = entity.position.ToVector3();
            Vector3 facing   = ComputeFacing(room, guardPos, useNearestDoor: false);

            return NavigationContextEntry.CreateHostageGuardian(
                "hostage_01",
                room.id,
                new SerializableVector3(guardPos),
                new SerializableVector3(facing));
        }

        // ── Shared Helpers ───────────────────────────────────────────────────

        /// <summary>
        /// Computes a normalised facing direction from <paramref name="fromPos"/>
        /// toward either the nearest door (stationary guard) or the primary
        /// first door (hostage guardian). Returns <see cref="FallbackFacing"/>
        /// when the room has no doors or the target position coincides with
        /// the source.
        /// </summary>
        private static Vector3 ComputeFacing(
            RoomData room, Vector3 fromPos, bool useNearestDoor)
        {
            if (room.doors == null || room.doors.Count == 0)
                return FallbackFacing;

            DoorData target;
            if (useNearestDoor)
            {
                target = room.doors[0];
                float bestSq = float.MaxValue;
                foreach (DoorData door in room.doors)
                {
                    float dx = door.position.x - fromPos.x;
                    float dz = door.position.z - fromPos.z;
                    float sq = dx * dx + dz * dz;
                    if (sq < bestSq)
                    {
                        bestSq = sq;
                        target = door;
                    }
                }
            }
            else
            {
                target = room.doors[0];
            }

            Vector3 dir = new Vector3(
                target.position.x - fromPos.x,
                0f,
                target.position.z - fromPos.z);

            return dir.sqrMagnitude < 1e-6f ? FallbackFacing : dir.normalized;
        }
    }
}
