// =============================================================================
// ScenarioValidator.cs
// Module 1 – Dynamic Scenario Generation
// Team Sentinels | University of Moratuwa | 2026
//
// Two-tier validation architecture following [20]:
//   Tier 1 – Local constraints (per-room/per-entity)
//   Tier 2 – Global constraints (whole-scenario)
//
// Runs the complete check suite against an assembled ScenarioData and reports
// pass/fail with itemised failure messages. The validator never throws on
// malformed scenarios — all problems are surfaced through ValidationResult.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TeamSentinels.ScenarioGeneration.DataModels;

namespace TeamSentinels.ScenarioGeneration.Validation
{
    /// <summary>
    /// Validates a completed <see cref="ScenarioData"/> for spatial coherence,
    /// mission readiness, and schema compliance. Runs ten independent checks
    /// across two tiers and returns a <see cref="ValidationResult"/> embedded
    /// in the output Scenario.json's <c>configurationMetadata</c>.
    ///
    /// <para><b>Tier 1 (local):</b> RoomBounds, EntityBounds, DoorConsistency,
    /// EntityMetadata.</para>
    /// <para><b>Tier 2 (global):</b> Reachability, HostagePath, EntityOverlap,
    /// NavigationContext, ReferentialIntegrity, RoleAssignmentCompleteness.</para>
    /// </summary>
    public class ScenarioValidator
    {
        // ── Constants ────────────────────────────────────────────────────────

        /// <summary>
        /// Minimum distance from an entity centre to any wall in metres.
        /// Mirrors <c>EntityPlacer.WallMargin</c> from the placement design doc.
        /// </summary>
        public const float WallMargin = 0.8f;

        /// <summary>
        /// Minimum allowable distance between any two entity positions in
        /// metres (XZ plane). Mirrors <c>EntityPlacer.MinClearance</c>.
        /// </summary>
        public const float MinClearance = 1.5f;

        /// <summary>Total number of validation checks executed by <see cref="Validate"/>.</summary>
        public const int TotalChecks = 10;

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Runs all ten validation checks against the given scenario. Every
        /// check executes regardless of earlier failures so the caller can see
        /// the full set of problems in a single pass. The result is considered
        /// passing only when all ten checks succeed.
        /// </summary>
        /// <param name="scenario">The scenario to validate. Must not be null.</param>
        /// <returns>
        /// A <see cref="ValidationResult"/> with pass/fail status, the number
        /// of checks run, the number passed, and a warnings list containing one
        /// entry per failed check.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="scenario"/> is null.</exception>
        public ValidationResult Validate(ScenarioData scenario)
        {
            if (scenario == null)
                throw new ArgumentNullException(nameof(scenario));

            var warnings = new List<string>();
            int checksPassed = 0;

            // ── Tier 1: Local constraints ────────────────────────────────────
            checksPassed += RunCheck("RoomBounds",            CheckRoomBounds(scenario),            warnings);
            checksPassed += RunCheck("EntityBounds",          CheckEntityBounds(scenario),          warnings);
            checksPassed += RunCheck("DoorConsistency",       CheckDoorConsistency(scenario),       warnings);
            checksPassed += RunCheck("EntityMetadata",        CheckEntityMetadata(scenario),        warnings);

            // ── Tier 2: Global constraints ───────────────────────────────────
            checksPassed += RunCheck("Reachability",          CheckReachability(scenario),          warnings);
            checksPassed += RunCheck("HostagePath",           CheckHostagePath(scenario),           warnings);
            checksPassed += RunCheck("EntityOverlap",         CheckEntityOverlap(scenario),         warnings);
            checksPassed += RunCheck("NavigationContext",     CheckNavigationContext(scenario),     warnings);
            checksPassed += RunCheck("ReferentialIntegrity",  CheckReferentialIntegrity(scenario),  warnings);
            checksPassed += RunCheck("RoleAssignmentCompleteness",
                                     CheckRoleAssignmentCompleteness(scenario),                    warnings);

            return new ValidationResult
            {
                passed       = checksPassed == TotalChecks,
                checksRun    = TotalChecks,
                checksPassed = checksPassed,
                warnings     = warnings
            };
        }

        // ── Check Runner ─────────────────────────────────────────────────────

        /// <summary>
        /// Logs a single check result and appends its failure message to
        /// <paramref name="warnings"/> when it fails. Returns 1 on pass (so the
        /// caller can sum into <c>checksPassed</c>) and 0 on fail.
        /// </summary>
        private static int RunCheck(string name, (bool passed, string message) result, List<string> warnings)
        {
            string status = result.passed ? "PASS" : "FAIL";
            Debug.Log($"[ScenarioValidator] CHECK {name}: {status} — {result.message}");
            if (!result.passed)
            {
                warnings.Add($"{name}: {result.message}");
                return 0;
            }
            return 1;
        }

        // ── Tier 1.1: RoomBoundsCheck ────────────────────────────────────────

        /// <summary>
        /// Checks that every room has strictly positive width, depth, and
        /// height. Zero or negative dimensions are unphysical and would break
        /// downstream geometry (NavMesh, prefab instantiation).
        /// </summary>
        private static (bool passed, string message) CheckRoomBounds(ScenarioData scenario)
        {
            if (scenario.layout?.rooms == null || scenario.layout.rooms.Count == 0)
                return (false, "layout.rooms is null or empty");

            foreach (RoomData room in scenario.layout.rooms)
            {
                if (room.size == null)
                    return (false, $"room {room.id} has null size");
                if (room.size.width <= 0f || room.size.depth <= 0f || room.size.height <= 0f)
                    return (false,
                        $"room {room.id} has non-positive size: " +
                        $"width={room.size.width}, depth={room.size.depth}, height={room.size.height}");
            }
            return (true, $"{scenario.layout.rooms.Count} rooms have valid positive dimensions");
        }

        // ── Tier 1.2: EntityBoundsCheck ──────────────────────────────────────

        /// <summary>
        /// Checks that every entity position lies within its assigned room's
        /// usable interior (room half-extents minus <see cref="WallMargin"/>).
        /// </summary>
        private static (bool passed, string message) CheckEntityBounds(ScenarioData scenario)
        {
            if (scenario.entities == null || scenario.entities.Count == 0)
                return (false, "entities list is null or empty");

            Dictionary<string, RoomData> roomMap = BuildRoomMap(scenario);

            foreach (EntityRecord entity in scenario.entities)
            {
                if (entity.position == null)
                    return (false, $"entity {entity.id} has null position");
                if (string.IsNullOrEmpty(entity.assignedRoom))
                    return (false, $"entity {entity.id} has null/empty assignedRoom");
                if (!roomMap.TryGetValue(entity.assignedRoom, out RoomData room))
                    return (false,
                        $"entity {entity.id} references unknown assignedRoom '{entity.assignedRoom}'");

                float hw = room.size.width * 0.5f - WallMargin;
                float hd = room.size.depth * 0.5f - WallMargin;
                float dx = Mathf.Abs(entity.position.x - room.position.x);
                float dz = Mathf.Abs(entity.position.z - room.position.z);

                if (dx > hw || dz > hd)
                    return (false,
                        $"entity {entity.id} at ({entity.position.x:F2}, {entity.position.z:F2}) " +
                        $"exceeds room {room.id} interior bounds " +
                        $"(centre=({room.position.x:F2}, {room.position.z:F2}), " +
                        $"half-extents=({hw:F2}, {hd:F2}))");
            }
            return (true,
                $"{scenario.entities.Count} entities placed within room interiors " +
                $"({WallMargin}m wall margin)");
        }

        // ── Tier 1.3: DoorConsistencyCheck ───────────────────────────────────

        /// <summary>
        /// Checks that every door's <c>connectsToRoomId</c> targets a room that
        /// exists in <c>layout.rooms</c>, and that the targeted room contains a
        /// reciprocal door pointing back. Door IDs must match across both ends.
        /// </summary>
        private static (bool passed, string message) CheckDoorConsistency(ScenarioData scenario)
        {
            if (scenario.layout?.rooms == null)
                return (false, "layout.rooms is null");

            Dictionary<string, RoomData> roomMap = BuildRoomMap(scenario);
            int doorEdges = 0;

            foreach (RoomData room in scenario.layout.rooms)
            {
                if (room.doors == null) continue;

                foreach (DoorData door in room.doors)
                {
                    doorEdges++;

                    if (string.IsNullOrEmpty(door.connectsToRoomId))
                        return (false, $"door {door.id} in room {room.id} has null/empty connectsToRoomId");
                    if (!roomMap.TryGetValue(door.connectsToRoomId, out RoomData neighbour))
                        return (false,
                            $"door {door.id} in room {room.id} references unknown room " +
                            $"'{door.connectsToRoomId}'");

                    bool reciprocalFound = neighbour.doors != null && neighbour.doors.Any(d =>
                        d.connectsToRoomId == room.id &&
                        string.Equals(d.id, door.id, StringComparison.Ordinal));

                    if (!reciprocalFound)
                        return (false,
                            $"door {door.id} from {room.id} → {neighbour.id} has no reciprocal " +
                            $"door pointing back from {neighbour.id} → {room.id}");
                }
            }
            return (true, $"{doorEdges} door endpoints are consistent and reciprocal");
        }

        // ── Tier 1.4: EntityMetadataCheck ────────────────────────────────────

        /// <summary>
        /// Checks that every entity has a non-null/non-empty id and a defined
        /// <see cref="EntityType"/> value (rejects undefined enum casts).
        /// </summary>
        private static (bool passed, string message) CheckEntityMetadata(ScenarioData scenario)
        {
            if (scenario.entities == null || scenario.entities.Count == 0)
                return (false, "entities list is null or empty");

            var seenIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (EntityRecord entity in scenario.entities)
            {
                if (string.IsNullOrEmpty(entity.id))
                    return (false, "found entity with null or empty id");
                if (!seenIds.Add(entity.id))
                    return (false, $"duplicate entity id '{entity.id}'");
                if (!Enum.IsDefined(typeof(EntityType), entity.type))
                    return (false, $"entity {entity.id} has invalid EntityType value '{entity.type}'");
            }
            return (true, $"{scenario.entities.Count} entities have valid ids and types");
        }

        // ── Tier 2.5: ReachabilityCheck ──────────────────────────────────────

        /// <summary>
        /// BFS from the trainee spawn room across <c>connectedRoomIds</c>. The
        /// scenario is unplayable if any room cannot be reached from the
        /// trainee start, since the trainee would never encounter those rooms.
        /// </summary>
        private static (bool passed, string message) CheckReachability(ScenarioData scenario)
        {
            if (scenario.layout?.rooms == null || scenario.layout.rooms.Count == 0)
                return (false, "layout.rooms is null or empty");
            if (scenario.spawnPoints?.trainee == null ||
                string.IsNullOrEmpty(scenario.spawnPoints.trainee.roomId))
                return (false, "trainee spawn point or roomId is missing");

            string startRoomId = scenario.spawnPoints.trainee.roomId;
            Dictionary<string, RoomData> roomMap = BuildRoomMap(scenario);

            if (!roomMap.ContainsKey(startRoomId))
                return (false, $"trainee spawn room '{startRoomId}' not found in layout.rooms");

            HashSet<string> visited = BfsFrom(startRoomId, roomMap);

            if (visited.Count == scenario.layout.rooms.Count)
                return (true, $"all {visited.Count} rooms reachable from trainee spawn '{startRoomId}'");

            var unreachable = scenario.layout.rooms
                .Where(r => !visited.Contains(r.id))
                .Select(r => r.id)
                .ToList();
            return (false,
                $"{unreachable.Count} unreachable room(s) from trainee spawn '{startRoomId}': " +
                string.Join(", ", unreachable));
        }

        // ── Tier 2.6: HostagePathCheck ───────────────────────────────────────

        /// <summary>
        /// Verifies a door-graph path exists from the trainee spawn room to the
        /// hostage room. Independent from the reachability check so a missing
        /// path is reported even when the broader reachability check is also
        /// failing.
        /// </summary>
        private static (bool passed, string message) CheckHostagePath(ScenarioData scenario)
        {
            if (scenario.spawnPoints?.trainee == null ||
                string.IsNullOrEmpty(scenario.spawnPoints.trainee.roomId))
                return (false, "trainee spawn point or roomId is missing");
            if (scenario.spawnPoints.hostages == null || scenario.spawnPoints.hostages.Count == 0)
                return (false, "no hostage spawn point in spawnPoints.hostages");

            string startRoomId   = scenario.spawnPoints.trainee.roomId;
            string hostageRoomId = scenario.spawnPoints.hostages[0].roomId;

            if (string.IsNullOrEmpty(hostageRoomId))
                return (false, "hostage spawn point has null/empty roomId");

            Dictionary<string, RoomData> roomMap = BuildRoomMap(scenario);

            if (!roomMap.ContainsKey(startRoomId))
                return (false, $"trainee spawn room '{startRoomId}' not found in layout.rooms");
            if (!roomMap.ContainsKey(hostageRoomId))
                return (false, $"hostage spawn room '{hostageRoomId}' not found in layout.rooms");

            HashSet<string> visited = BfsFrom(startRoomId, roomMap);
            if (visited.Contains(hostageRoomId))
                return (true,
                    $"hostage room '{hostageRoomId}' reachable from trainee spawn '{startRoomId}'");

            return (false,
                $"no door-graph path from trainee spawn '{startRoomId}' to hostage room '{hostageRoomId}'");
        }

        // ── Tier 2.7: EntityOverlapCheck ─────────────────────────────────────

        /// <summary>
        /// Checks that every pair of entities sits at least
        /// <see cref="MinClearance"/> apart in the XZ plane. Distances are
        /// compared squared to avoid the per-pair square root cost.
        /// </summary>
        private static (bool passed, string message) CheckEntityOverlap(ScenarioData scenario)
        {
            if (scenario.entities == null || scenario.entities.Count < 2)
                return (true, $"only {scenario.entities?.Count ?? 0} entities — overlap check trivially holds");

            float minSq = MinClearance * MinClearance;

            for (int i = 0; i < scenario.entities.Count; i++)
            {
                EntityRecord a = scenario.entities[i];
                if (a.position == null) continue;

                for (int j = i + 1; j < scenario.entities.Count; j++)
                {
                    EntityRecord b = scenario.entities[j];
                    if (b.position == null) continue;

                    float dx = a.position.x - b.position.x;
                    float dz = a.position.z - b.position.z;
                    float sq = dx * dx + dz * dz;

                    if (sq < minSq)
                    {
                        float dist = Mathf.Sqrt(sq);
                        return (false,
                            $"entities '{a.id}' and '{b.id}' are {dist:F2}m apart " +
                            $"(< minimum clearance {MinClearance}m)");
                    }
                }
            }
            return (true,
                $"all entity pairs satisfy the {MinClearance}m clearance constraint");
        }

        // ── Tier 2.8: NavigationContextCheck ─────────────────────────────────

        /// <summary>
        /// Checks that every <c>RoleAssignment.navigationContextId</c> resolves
        /// to a key in the <c>navigationContext</c> dictionary. An orphan
        /// reference here means Module 2 cannot drive that NPC at runtime.
        /// </summary>
        private static (bool passed, string message) CheckNavigationContext(ScenarioData scenario)
        {
            if (scenario.roleAssignments == null)
                return (false, "roleAssignments list is null");
            if (scenario.navigationContext == null)
                return (false, "navigationContext dictionary is null");

            foreach (RoleAssignment assignment in scenario.roleAssignments)
            {
                if (string.IsNullOrEmpty(assignment.navigationContextId))
                    return (false,
                        $"role assignment for '{assignment.entityId}' has null/empty navigationContextId");
                if (!scenario.navigationContext.ContainsKey(assignment.navigationContextId))
                    return (false,
                        $"role assignment for '{assignment.entityId}' references missing " +
                        $"navigationContextId '{assignment.navigationContextId}'");
            }
            return (true,
                $"{scenario.roleAssignments.Count} role assignments map to existing " +
                $"navigation context entries");
        }

        // ── Tier 2.9: ReferentialIntegrityCheck ──────────────────────────────

        /// <summary>
        /// Checks that every room ID referenced anywhere in <c>spawnPoints</c>,
        /// <c>entities</c>, <c>roleAssignments</c>, or <c>navigationContext</c>
        /// entries (guard, hostage, anchor, roaming, patrol route) exists in
        /// <c>layout.rooms</c>.
        /// </summary>
        private static (bool passed, string message) CheckReferentialIntegrity(ScenarioData scenario)
        {
            if (scenario.layout?.rooms == null)
                return (false, "layout.rooms is null");

            var roomIds = new HashSet<string>(
                scenario.layout.rooms.Select(r => r.id), StringComparer.Ordinal);

            // spawnPoints
            if (scenario.spawnPoints != null)
            {
                if (scenario.spawnPoints.trainee != null &&
                    !IsKnownRoom(scenario.spawnPoints.trainee.roomId, roomIds, out string err))
                    return (false, $"spawnPoints.trainee {err}");

                if (scenario.spawnPoints.hostages != null)
                {
                    for (int i = 0; i < scenario.spawnPoints.hostages.Count; i++)
                    {
                        if (!IsKnownRoom(scenario.spawnPoints.hostages[i].roomId, roomIds, out string e))
                            return (false, $"spawnPoints.hostages[{i}] {e}");
                    }
                }
                if (scenario.spawnPoints.terrorists != null)
                {
                    for (int i = 0; i < scenario.spawnPoints.terrorists.Count; i++)
                    {
                        if (!IsKnownRoom(scenario.spawnPoints.terrorists[i].roomId, roomIds, out string e))
                            return (false, $"spawnPoints.terrorists[{i}] {e}");
                    }
                }
            }

            // entities
            if (scenario.entities != null)
            {
                foreach (EntityRecord entity in scenario.entities)
                {
                    if (!IsKnownRoom(entity.assignedRoom, roomIds, out string e))
                        return (false, $"entity '{entity.id}' assignedRoom {e}");
                }
            }

            // roleAssignments
            if (scenario.roleAssignments != null)
            {
                foreach (RoleAssignment assignment in scenario.roleAssignments)
                {
                    if (!IsKnownRoom(assignment.assignedRoomId, roomIds, out string e))
                        return (false, $"roleAssignment for '{assignment.entityId}' assignedRoomId {e}");
                }
            }

            // navigationContext
            if (scenario.navigationContext != null)
            {
                foreach (KeyValuePair<string, NavigationContextEntry> kvp in scenario.navigationContext)
                {
                    string ctxKey = kvp.Key;
                    NavigationContextEntry ctx = kvp.Value;
                    if (ctx == null) continue;

                    if (ctx.guardRoomId != null &&
                        !IsKnownRoom(ctx.guardRoomId, roomIds, out string e1))
                        return (false, $"navigationContext['{ctxKey}'].guardRoomId {e1}");

                    if (ctx.hostageRoomId != null &&
                        !IsKnownRoom(ctx.hostageRoomId, roomIds, out string e2))
                        return (false, $"navigationContext['{ctxKey}'].hostageRoomId {e2}");

                    if (ctx.anchorRoomId != null &&
                        !IsKnownRoom(ctx.anchorRoomId, roomIds, out string e3))
                        return (false, $"navigationContext['{ctxKey}'].anchorRoomId {e3}");

                    if (ctx.roamingRoomIds != null)
                    {
                        for (int i = 0; i < ctx.roamingRoomIds.Count; i++)
                        {
                            if (!IsKnownRoom(ctx.roamingRoomIds[i], roomIds, out string e4))
                                return (false, $"navigationContext['{ctxKey}'].roamingRoomIds[{i}] {e4}");
                        }
                    }
                    if (ctx.patrolRoute != null)
                    {
                        for (int i = 0; i < ctx.patrolRoute.Count; i++)
                        {
                            if (!IsKnownRoom(ctx.patrolRoute[i], roomIds, out string e5))
                                return (false, $"navigationContext['{ctxKey}'].patrolRoute[{i}] {e5}");
                        }
                    }
                }
            }

            return (true, $"all room references resolve to one of {roomIds.Count} known rooms");
        }

        // ── Tier 2.10: RoleAssignmentCompletenessCheck ───────────────────────

        /// <summary>
        /// Checks that every entity of type <c>terrorist</c> has exactly one
        /// corresponding entry in <c>roleAssignments</c>. A missing or
        /// duplicate assignment leaves Module 2 without (or with conflicting)
        /// FSM initialisation data.
        /// </summary>
        private static (bool passed, string message) CheckRoleAssignmentCompleteness(ScenarioData scenario)
        {
            if (scenario.entities == null)
                return (false, "entities list is null");
            if (scenario.roleAssignments == null)
                return (false, "roleAssignments list is null");

            List<EntityRecord> terrorists = scenario.entities
                .Where(e => e.type == EntityType.Terrorist)
                .ToList();

            var assignmentCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (RoleAssignment assignment in scenario.roleAssignments)
            {
                if (string.IsNullOrEmpty(assignment.entityId)) continue;
                assignmentCounts.TryGetValue(assignment.entityId, out int count);
                assignmentCounts[assignment.entityId] = count + 1;
            }

            foreach (EntityRecord terrorist in terrorists)
            {
                if (!assignmentCounts.TryGetValue(terrorist.id, out int count))
                    return (false, $"terrorist '{terrorist.id}' has no role assignment");
                if (count > 1)
                    return (false, $"terrorist '{terrorist.id}' has {count} role assignments (expected 1)");
            }

            // Reverse direction: assignments referencing non-terrorist entities are also a defect.
            var terroristIds = new HashSet<string>(terrorists.Select(t => t.id), StringComparer.Ordinal);
            foreach (RoleAssignment assignment in scenario.roleAssignments)
            {
                if (!string.IsNullOrEmpty(assignment.entityId) && !terroristIds.Contains(assignment.entityId))
                    return (false,
                        $"role assignment references '{assignment.entityId}' which is not a terrorist entity");
            }

            return (true, $"each of {terrorists.Count} terrorist(s) has exactly one role assignment");
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        /// <summary>
        /// Builds an id → <see cref="RoomData"/> lookup for the scenario's
        /// rooms list. Returns an empty dictionary when the rooms collection is
        /// null so callers can iterate without null-guarding every access.
        /// </summary>
        private static Dictionary<string, RoomData> BuildRoomMap(ScenarioData scenario)
        {
            var map = new Dictionary<string, RoomData>(StringComparer.Ordinal);
            if (scenario.layout?.rooms == null) return map;
            foreach (RoomData room in scenario.layout.rooms)
            {
                if (!string.IsNullOrEmpty(room.id))
                    map[room.id] = room;
            }
            return map;
        }

        /// <summary>
        /// BFS over <c>connectedRoomIds</c> starting from <paramref name="startRoomId"/>.
        /// Returns the set of room ids visited.
        /// </summary>
        private static HashSet<string> BfsFrom(string startRoomId, Dictionary<string, RoomData> roomMap)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal);
            if (!roomMap.ContainsKey(startRoomId)) return visited;

            var queue = new Queue<string>();
            queue.Enqueue(startRoomId);
            visited.Add(startRoomId);

            while (queue.Count > 0)
            {
                string current = queue.Dequeue();
                RoomData room = roomMap[current];
                if (room.connectedRoomIds == null) continue;

                foreach (string neighbourId in room.connectedRoomIds)
                {
                    if (!roomMap.ContainsKey(neighbourId)) continue;
                    if (visited.Add(neighbourId)) queue.Enqueue(neighbourId);
                }
            }
            return visited;
        }

        /// <summary>
        /// Returns true when <paramref name="roomId"/> is non-empty and present
        /// in <paramref name="roomIds"/>. Sets <paramref name="error"/> to a
        /// descriptive message when it returns false.
        /// </summary>
        private static bool IsKnownRoom(string roomId, HashSet<string> roomIds, out string error)
        {
            if (string.IsNullOrEmpty(roomId))
            {
                error = "is null or empty";
                return false;
            }
            if (!roomIds.Contains(roomId))
            {
                error = $"references unknown room '{roomId}'";
                return false;
            }
            error = null;
            return true;
        }
    }
}
