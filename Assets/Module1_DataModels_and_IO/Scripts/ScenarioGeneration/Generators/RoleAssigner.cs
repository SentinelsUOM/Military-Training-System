// =============================================================================
// RoleAssigner.cs
// Module 1 – Dynamic Scenario Generation
// Team Sentinels | University of Moratuwa | 2026
//
// Derives NPC role assignments from the spatial structure (room depth,
// adjacency) following the depth-based approach justified by [18].
// Implements the algorithm from NPC_Role_Assignment_Algorithm_Design.md.
// =============================================================================

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TeamSentinels.ScenarioGeneration.DataModels;

namespace TeamSentinels.ScenarioGeneration.Generators
{
    /// <summary>
    /// Assigns roles (patrol, stationary_guard, roaming_guard, hostage_guardian)
    /// to terrorist NPCs based on room depth and a difficulty-driven role
    /// distribution matrix. Produces one <see cref="RoleAssignment"/> per
    /// terrorist entity. Plain C# class with no <see cref="MonoBehaviour"/>
    /// dependency; all randomness flows through the caller-supplied
    /// <see cref="System.Random"/> for seed-based reproducibility.
    /// </summary>
    public class RoleAssigner
    {
        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Assigns one <see cref="RoleAssignment"/> per terrorist entity. The
        /// hostage_guardian is assigned first (mandatory when terroristCount ≥ 1);
        /// remaining terrorists are distributed via the role quota matrix and
        /// matched to depth zones (shallow → patrol, deep → stationary_guard,
        /// mid → roaming_guard).
        /// </summary>
        /// <param name="layout">Generated room graph from <see cref="LayoutGenerator"/>.</param>
        /// <param name="entities">Entity records from <see cref="EntityPlacer"/>.</param>
        /// <param name="spawnPoints">Spawn points from <see cref="EntityPlacer"/>.</param>
        /// <param name="config">Evaluator-defined scenario parameters.</param>
        /// <param name="rng">Seeded random instance shared across the pipeline.</param>
        /// <returns>One role assignment per terrorist entity.</returns>
        public List<RoleAssignment> Assign(
            LayoutData layout,
            List<EntityRecord> entities,
            SpawnPointData spawnPoints,
            ScenarioConfig config,
            System.Random rng)
        {
            var assignments = new List<RoleAssignment>();

            List<EntityRecord> terrorists = entities
                .Where(e => e.type == EntityType.Terrorist)
                .ToList();
            if (terrorists.Count == 0) return assignments;

            EntityRecord hostage = entities.FirstOrDefault(e => e.type == EntityType.Hostage);
            string hostageRoomId = hostage != null ? hostage.assignedRoom : null;
            Vector3 hostagePos   = hostage != null
                ? hostage.position.ToVector3()
                : Vector3.zero;

            int difficulty = config.executionControls.difficultyLevel;
            int maxDepth   = layout.layoutMetadata.maxDepth;
            var roomMap    = layout.rooms.ToDictionary(r => r.id);

            // ── Step 1: Hostage Guardian (mandatory) ─────────────────────────
            if (terrorists.Count == 1)
            {
                EntityRecord sole = terrorists[0];
                assignments.Add(BuildAssignment(
                    sole.id,
                    NpcRole.HostageGuardian,
                    1,
                    hostageRoomId ?? sole.assignedRoom));
                return assignments;
            }

            EntityRecord guardian = SelectClosestTerrorist(terrorists, hostagePos, hostageRoomId);
            assignments.Add(BuildAssignment(
                guardian.id,
                NpcRole.HostageGuardian,
                1,
                hostageRoomId ?? guardian.assignedRoom));
            terrorists.Remove(guardian);

            // ── Step 2: Role quotas from decision matrix ─────────────────────
            int remaining = terrorists.Count;
            (int patrolCount, int roamingCount, int guardCount) =
                GetRoleQuotas(remaining, difficulty);

            // ── Step 3: Depth zone thresholds ────────────────────────────────
            int shallowMax = Mathf.CeilToInt(maxDepth / 3f);
            int deepMin    = Mathf.CeilToInt(2f * maxDepth / 3f);

            // Degenerate case: no deep zone exists (e.g., hub_and_spoke,
            // single-ring layouts). Convert guard allocation to roaming so
            // every remaining terrorist still receives a valid role.
            if (maxDepth <= 1)
            {
                shallowMax   = 1;
                deepMin      = maxDepth + 1;
                roamingCount = roamingCount + guardCount;
                guardCount   = 0;
            }

            // ── Step 4: Sort terrorists by assigned room depth ───────────────
            terrorists.Sort((a, b) =>
            {
                int da = roomMap[a.assignedRoom].depth;
                int db = roomMap[b.assignedRoom].depth;
                if (da != db) return da.CompareTo(db);
                return string.CompareOrdinal(a.id, b.id);
            });

            // ── Step 5: Assign roles by depth-zone matching ──────────────────
            int patrolAssigned  = 0;
            int roamingAssigned = 0;
            int guardAssigned   = 0;

            foreach (EntityRecord terrorist in terrorists)
            {
                int roomDepth = roomMap[terrorist.assignedRoom].depth;
                NpcRole role;
                int priority;

                if (roomDepth <= shallowMax && patrolAssigned < patrolCount)
                {
                    role = NpcRole.Patrol;
                    priority = 3;
                    patrolAssigned++;
                }
                else if (roomDepth >= deepMin && guardAssigned < guardCount)
                {
                    role = NpcRole.StationaryGuard;
                    priority = 2;
                    guardAssigned++;
                }
                else if (roamingAssigned < roamingCount)
                {
                    role = NpcRole.RoamingGuard;
                    priority = 2;
                    roamingAssigned++;
                }
                else if (patrolAssigned < patrolCount)
                {
                    role = NpcRole.Patrol;
                    priority = 3;
                    patrolAssigned++;
                }
                else if (guardAssigned < guardCount)
                {
                    role = NpcRole.StationaryGuard;
                    priority = 2;
                    guardAssigned++;
                }
                else
                {
                    role = NpcRole.RoamingGuard;
                    priority = 2;
                    roamingAssigned++;
                }

                assignments.Add(BuildAssignment(
                    terrorist.id, role, priority, terrorist.assignedRoom));
            }

            return assignments;
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        /// <summary>
        /// Constructs a <see cref="RoleAssignment"/> with the standard
        /// <c>"nav_{entityId}"</c> navigation context key.
        /// </summary>
        private static RoleAssignment BuildAssignment(
            string entityId, NpcRole role, int priority, string roomId)
        {
            return new RoleAssignment
            {
                entityId             = entityId,
                role                 = role,
                priorityLevel        = priority,
                assignedRoomId       = roomId,
                navigationContextId  = "nav_" + entityId
            };
        }

        /// <summary>
        /// Returns the role quota triple
        /// <c>(patrolCount, roamingCount, guardCount)</c> for the given remaining
        /// NPC count and difficulty bracket, following the decision matrix in
        /// design doc §3.1. Max terroristCount is 8 (remaining ≤ 7).
        /// </summary>
        private static (int patrol, int roaming, int guard) GetRoleQuotas(
            int remaining, int difficulty)
        {
            if (remaining <= 0) return (0, 0, 0);

            // Bracket: 0 = diff 1-2 (patrol-heavy),
            //          1 = diff 3   (balanced),
            //          2 = diff 4-5 (guard-heavy).
            int bracket = difficulty <= 2 ? 0 : difficulty == 3 ? 1 : 2;

            switch (remaining)
            {
                case 1:
                    if (bracket == 0) return (1, 0, 0);
                    if (bracket == 1) return (0, 1, 0);
                    return (0, 0, 1);
                case 2:
                    if (bracket == 0) return (1, 1, 0);
                    if (bracket == 1) return (1, 0, 1);
                    return (0, 1, 1);
                case 3:
                    if (bracket == 0) return (2, 1, 0);
                    return (1, 1, 1);
                case 4:
                    if (bracket == 0) return (2, 1, 1);
                    if (bracket == 1) return (1, 2, 1);
                    return (1, 1, 2);
                case 5:
                    if (bracket == 0) return (2, 2, 1);
                    if (bracket == 1) return (2, 1, 2);
                    return (1, 2, 2);
                case 6:
                    if (bracket == 0) return (3, 2, 1);
                    if (bracket == 1) return (2, 2, 2);
                    return (1, 2, 3);
                default: // 7 or more (schema caps remaining at 7)
                    if (bracket == 0) return (3, 2, 2);
                    if (bracket == 1) return (2, 3, 2);
                    return (1, 3, 3);
            }
        }

        /// <summary>
        /// Picks the terrorist best suited to guard the hostage: any terrorist
        /// already placed in the hostage room wins (ties broken by distance),
        /// otherwise the terrorist nearest the hostage position is chosen.
        /// </summary>
        private static EntityRecord SelectClosestTerrorist(
            List<EntityRecord> terrorists, Vector3 hostagePos, string hostageRoomId)
        {
            List<EntityRecord> sameRoom = hostageRoomId != null
                ? terrorists.Where(t => t.assignedRoom == hostageRoomId).ToList()
                : new List<EntityRecord>();

            List<EntityRecord> pool = sameRoom.Count > 0 ? sameRoom : terrorists;

            return pool
                .OrderBy(t => (t.position.ToVector3() - hostagePos).sqrMagnitude)
                .ThenBy(t => t.id, System.StringComparer.Ordinal)
                .First();
        }
    }
}
