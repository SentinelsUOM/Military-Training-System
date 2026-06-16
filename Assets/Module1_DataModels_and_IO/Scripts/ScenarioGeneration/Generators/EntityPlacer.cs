// =============================================================================
// EntityPlacer.cs
// Module 1 – Dynamic Scenario Generation
// Team Sentinels | University of Moratuwa | 2026
//
// Positions trainee, hostage, and terrorist entities within the generated
// layout using hierarchical three-stage placement [14]: trainee at entry,
// hostage in a deep dead-end (strategy-aware), then terrorists constrained by
// placement strategy, hostage-risk level distance thresholds [16], and
// difficulty-aware zone selection [3]. Implementation follows
// Entity_Placement_Algorithm_Design.md.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TeamSentinels.ScenarioGeneration.DataModels;

namespace TeamSentinels.ScenarioGeneration.Generators
{
    /// <summary>
    /// Bundled output of <see cref="EntityPlacer.Place"/>: the flat list of
    /// entity records and the spawn-point container used by the runtime scene
    /// loader. Both collections are populated in the same placement pass to
    /// guarantee consistent IDs, positions, and room assignments.
    /// </summary>
    public class EntityPlacementResult
    {
        /// <summary>Trainee, hostage, and terrorist entity records.</summary>
        public List<EntityRecord> entities;

        /// <summary>Spawn points for all actors, ready for the scene loader.</summary>
        public SpawnPointData spawnPoints;
    }

    /// <summary>
    /// Places entities within rooms respecting placement strategy (clustered,
    /// dispersed, front_loaded, deep), hostage risk-level distance
    /// constraints [16], and difficulty-aware zone selection [3].
    /// Plain C# class with no <see cref="MonoBehaviour"/> dependency — all
    /// randomness flows through the caller-supplied <see cref="System.Random"/>
    /// instance for full seed-based reproducibility.
    /// </summary>
    public class EntityPlacer
    {
        // ── Constants ────────────────────────────────────────────────────────

        /// <summary>Minimum distance from entity centre to any wall (metres).</summary>
        public const float WallMargin = 0.8f;

        /// <summary>Minimum distance between any two entities (metres).</summary>
        public const float MinClearance = 1.5f;

        /// <summary>Offset from a door position for "behind door" placement (metres).</summary>
        public const float DoorOffset = 1.2f;

        /// <summary>Maximum retry attempts per entity before relaxing constraints.</summary>
        public const int MaxPlacementAttempts = 100;

        /// <summary>
        /// Difficulty-aware placement zone within a room. Higher difficulty
        /// levels favour tactically surprising positions [3].
        /// </summary>
        private enum PlacementZone
        {
            Centre,
            OpenArea,
            NearWall,
            Corner,
            BehindDoor,
            SightlineBlind
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Runs the three-stage placement pipeline and returns entity records
        /// together with the fully populated spawn-point container.
        /// </summary>
        /// <param name="layout">Generated room graph from <see cref="LayoutGenerator"/>.</param>
        /// <param name="config">Evaluator-defined scenario parameters.</param>
        /// <param name="rng">Seeded random instance shared across the pipeline.</param>
        /// <returns>Entities and spawn points bundled as <see cref="EntityPlacementResult"/>.</returns>
        public EntityPlacementResult Place(LayoutData layout, ScenarioConfig config, System.Random rng)
        {
            var allPositions = new List<Vector3>();
            var entities    = new List<EntityRecord>();
            var spawnPoints = new SpawnPointData();

            PlacementStrategy strategy = config.entityConfiguration.placementStrategy;
            HostageRiskLevel  riskLevel = config.entityConfiguration.hostageRiskLevel;
            int  difficulty = config.executionControls.difficultyLevel;
            float roomWidth = layout.rooms[0].size.width;

            // ── Stage 1: Trainee at Entry Room ───────────────────────────────
            RoomData entryRoom = layout.rooms.First(r => r.depth == 0);
            Vector3 traineePos    = entryRoom.position.ToVector3();
            Vector3 traineeFacing = new Vector3(1f, 0f, 0f);
            allPositions.Add(traineePos);

            entities.Add(new EntityRecord
            {
                id            = "trainee_01",
                type          = EntityType.Trainee,
                assignedRoom  = entryRoom.id,
                position      = new SerializableVector3(traineePos),
                metadata      = new EntityMetadata()
            });

            spawnPoints.trainee = new TraineeSpawnPoint
            {
                roomId          = entryRoom.id,
                position        = new SerializableVector3(traineePos),
                facingDirection = new SerializableVector3(traineeFacing)
            };

            // ── Stage 2: Hostage by Strategy ─────────────────────────────────
            RoomData hostageRoom = SelectHostageRoom(layout.rooms, strategy, rng);
            if (hostageRoom.type != RoomType.HostageRoom)
                hostageRoom.type = RoomType.HostageRoom;

            Vector3 hostagePos    = GeneratePositionInZone(hostageRoom, PlacementZone.Centre, rng);
            Vector3 hostageFacing = RandomFacingDirection(rng);
            allPositions.Add(hostagePos);

            entities.Add(new EntityRecord
            {
                id           = "hostage_01",
                type         = EntityType.Hostage,
                assignedRoom = hostageRoom.id,
                position     = new SerializableVector3(hostagePos),
                metadata     = new EntityMetadata { initialState = "calm" }
            });

            spawnPoints.hostages.Add(new NpcSpawnPoint
            {
                entityId        = "hostage_01",
                roomId          = hostageRoom.id,
                position        = new SerializableVector3(hostagePos),
                facingDirection = new SerializableVector3(hostageFacing)
            });

            // ── Stage 3: Terrorists by Strategy + Risk Constraints ───────────
            int terroristCount = config.entityConfiguration.terroristCount;
            List<RoomData> terroristRooms = SelectTerroristRooms(
                layout.rooms, strategy, terroristCount, hostageRoom.id, rng);

            (float minDist, float maxDist) = GetDistanceConstraints(riskLevel, roomWidth);
            bool riskSatisfied = false;

            for (int i = 0; i < terroristCount; i++)
            {
                RoomData room     = terroristRooms[i];
                string   entityId = $"terrorist_{(i + 1):00}";
                bool     placed   = false;
                Vector3  placedPos = Vector3.zero;

                for (int attempt = 0; attempt < MaxPlacementAttempts; attempt++)
                {
                    PlacementZone zone = SelectZone(difficulty, attempt);
                    Vector3 candidate = GeneratePositionInZone(room, zone, rng);

                    // Hard constraint: must sit within valid room bounds
                    if (!IsWithinBounds(candidate, room))
                        continue;

                    // Hard constraint: no entity overlap within MinClearance
                    if (!HasClearance(candidate, allPositions, MinClearance))
                        continue;

                    bool satisfiesRisk = SatisfiesRiskConstraint(
                        candidate, hostagePos, minDist, maxDist);

                    // Risk constraint is hard until at least one terrorist satisfies it.
                    // After half the attempts, relax to a soft preference for
                    // subsequent terrorists only (first terrorist still enforces).
                    if (!riskSatisfied && !satisfiesRisk
                        && attempt < MaxPlacementAttempts / 2)
                        continue;

                    if (satisfiesRisk) riskSatisfied = true;

                    placedPos = candidate;
                    placed    = true;
                    break;
                }

                // Fallback if all attempts failed: the in-bounds point farthest
                // from already-placed entities (NOT the room centre, which in a
                // Small room sits on top of the centre-placed hostage and breaks
                // the clearance constraint).
                if (!placed)
                    placedPos = FarthestValidPosition(room, allPositions);

                allPositions.Add(placedPos);
                Vector3 facing = CalculateFacingTowardDoor(room, placedPos, rng);

                entities.Add(new EntityRecord
                {
                    id           = entityId,
                    type         = EntityType.Terrorist,
                    assignedRoom = room.id,
                    position     = new SerializableVector3(placedPos),
                    metadata     = new EntityMetadata { initialState = "idle" }
                });

                spawnPoints.terrorists.Add(new NpcSpawnPoint
                {
                    entityId        = entityId,
                    roomId          = room.id,
                    position        = new SerializableVector3(placedPos),
                    facingDirection = new SerializableVector3(facing)
                });
            }

            return new EntityPlacementResult
            {
                entities    = entities,
                spawnPoints = spawnPoints
            };
        }

        // ── Stage 2 Helpers: Hostage Room Selection ──────────────────────────

        /// <summary>
        /// Chooses the hostage room based on strategy.
        /// clustered/dispersed/deep prefer the deepest dead-end (1 connection);
        /// front_loaded targets a mid-depth room (ceil(maxDepth/2)) per design.
        /// </summary>
        private static RoomData SelectHostageRoom(
            List<RoomData> rooms, PlacementStrategy strategy, System.Random rng)
        {
            int maxDepth = rooms.Max(r => r.depth);
            List<RoomData> candidates;

            if (strategy == PlacementStrategy.FrontLoaded)
            {
                int targetDepth = Mathf.CeilToInt(maxDepth / 2f);
                candidates = rooms.Where(r => r.depth == targetDepth).ToList();
                if (candidates.Count == 0)
                    candidates = rooms.Where(r => r.depth == maxDepth).ToList();
            }
            else
            {
                candidates = rooms
                    .Where(r => r.depth == maxDepth && r.connectedRoomIds.Count == 1)
                    .ToList();
                if (candidates.Count == 0)
                    candidates = rooms.Where(r => r.depth == maxDepth).ToList();
            }

            return candidates[rng.Next(candidates.Count)];
        }

        // ── Stage 3 Helpers: Terrorist Room Selection ────────────────────────

        /// <summary>
        /// Selects one room per terrorist according to the placement strategy.
        /// Never places in the entry room (depth = 0).
        /// </summary>
        private static List<RoomData> SelectTerroristRooms(
            List<RoomData> rooms,
            PlacementStrategy strategy,
            int count,
            string hostageRoomId,
            System.Random rng)
        {
            int maxDepth = rooms.Max(r => r.depth);
            List<RoomData> available = rooms.Where(r => r.depth > 0).ToList();
            var selected = new List<RoomData>(count);

            // Defensive: empty available should never happen (validator enforces),
            // but fall back to any non-entry room to avoid NREs.
            if (available.Count == 0)
                available = rooms.ToList();

            switch (strategy)
            {
                case PlacementStrategy.Clustered:
                {
                    RoomData hostageRoom = rooms.First(r => r.id == hostageRoomId);
                    var cluster = new List<RoomData>();
                    if (hostageRoom.depth > 0) cluster.Add(hostageRoom);
                    foreach (string nId in hostageRoom.connectedRoomIds)
                    {
                        RoomData nb = rooms.First(r => r.id == nId);
                        if (nb.depth > 0 && nb.depth >= maxDepth - 1)
                            cluster.Add(nb);
                    }
                    if (cluster.Count == 0) cluster.AddRange(available);

                    for (int i = 0; i < count; i++)
                        selected.Add(cluster[i % cluster.Count]);
                    break;
                }

                case PlacementStrategy.Dispersed:
                {
                    var buckets = available
                        .GroupBy(r => r.depth)
                        .OrderBy(g => g.Key)
                        .Select(g => g.ToList())
                        .ToList();

                    for (int i = 0; i < count; i++)
                    {
                        List<RoomData> bucket = buckets[i % buckets.Count];
                        selected.Add(bucket[rng.Next(bucket.Count)]);
                    }
                    break;
                }

                case PlacementStrategy.FrontLoaded:
                {
                    int threshold = Mathf.CeilToInt(maxDepth / 3f);
                    List<RoomData> shallow = available
                        .Where(r => r.depth <= threshold).ToList();
                    if (shallow.Count == 0) shallow = available;

                    for (int i = 0; i < count; i++)
                        selected.Add(shallow[rng.Next(shallow.Count)]);
                    break;
                }

                case PlacementStrategy.Deep:
                {
                    int threshold = Mathf.CeilToInt(maxDepth / 2f);
                    List<RoomData> deep = available
                        .Where(r => r.depth >= threshold).ToList();
                    if (deep.Count == 0) deep = available;

                    for (int i = 0; i < count; i++)
                        selected.Add(deep[rng.Next(deep.Count)]);
                    break;
                }
            }

            return selected;
        }

        // ── Stage 3 Helpers: Distance Constraints ────────────────────────────

        /// <summary>
        /// Distance range (min, max) allowed between hostage and each terrorist
        /// for the given risk level. Values scale with room width so layouts of
        /// different room sizes share the same semantic spacing.
        /// </summary>
        private static (float min, float max) GetDistanceConstraints(
            HostageRiskLevel risk, float roomWidth)
        {
            switch (risk)
            {
                case HostageRiskLevel.High:   return (0f,                  roomWidth * 1.5f);
                case HostageRiskLevel.Medium: return (roomWidth * 0.5f,    roomWidth * 3f);
                case HostageRiskLevel.Low:    return (roomWidth * 2f,      float.MaxValue);
                default:                       return (0f,                  float.MaxValue);
            }
        }

        private static bool SatisfiesRiskConstraint(
            Vector3 terroristPos, Vector3 hostagePos, float minDist, float maxDist)
        {
            float dx = terroristPos.x - hostagePos.x;
            float dz = terroristPos.z - hostagePos.z;
            float dist = Mathf.Sqrt(dx * dx + dz * dz);
            return dist >= minDist && dist <= maxDist;
        }

        // ── Zone Selection ───────────────────────────────────────────────────

        private static PlacementZone SelectZone(int difficulty, int attempt)
        {
            PlacementZone[] zones = GetPlacementZones(difficulty);
            return zones[attempt % zones.Length];
        }

        private static PlacementZone[] GetPlacementZones(int difficulty)
        {
            if (difficulty <= 2)
                return new[] { PlacementZone.Centre, PlacementZone.OpenArea };
            if (difficulty == 3)
                return new[] { PlacementZone.Centre, PlacementZone.NearWall, PlacementZone.Corner };
            // difficulty 4–5
            return new[]
            {
                PlacementZone.BehindDoor,
                PlacementZone.Corner,
                PlacementZone.SightlineBlind
            };
        }

        /// <summary>
        /// Last-resort placement when no attempt found a clear spot — common in
        /// Small rooms at low difficulty, where the Centre/OpenArea zones all
        /// collide with an entity already at the room centre. Returns the in-bounds
        /// candidate (corners, edge midpoints, centre) that maximises the distance
        /// to the nearest already-placed entity, so two entities are never stacked
        /// when the room geometry can avoid it. In a 4 m room the farthest corner
        /// is ~1.7 m from a centre-placed hostage, clearing the 1.5 m constraint.
        /// </summary>
        private static Vector3 FarthestValidPosition(RoomData room, List<Vector3> existing)
        {
            float cx = room.position.x;
            float cz = room.position.z;
            float hw = room.size.width * 0.5f - WallMargin;
            float hd = room.size.depth * 0.5f - WallMargin;

            var centre = new Vector3(cx, 0f, cz);
            if (hw <= 0f || hd <= 0f || existing == null || existing.Count == 0)
                return centre;

            float[] xs = { -hw, 0f, hw };
            float[] zs = { -hd, 0f, hd };

            Vector3 best = centre;
            float bestMinSq = -1f;
            foreach (float x in xs)
                foreach (float z in zs)
                {
                    var cand = new Vector3(cx + x, 0f, cz + z);
                    float minSq = float.MaxValue;
                    foreach (Vector3 e in existing)
                    {
                        float dx = cand.x - e.x;
                        float dz = cand.z - e.z;
                        float sq = dx * dx + dz * dz;
                        if (sq < minSq) minSq = sq;
                    }
                    if (minSq > bestMinSq) { bestMinSq = minSq; best = cand; }
                }
            return best;
        }

        // ── Zone Position Generation ─────────────────────────────────────────

        private static Vector3 GeneratePositionInZone(
            RoomData room, PlacementZone zone, System.Random rng)
        {
            float cx = room.position.x;
            float cz = room.position.z;
            float hw = room.size.width * 0.5f - WallMargin;
            float hd = room.size.depth * 0.5f - WallMargin;

            switch (zone)
            {
                case PlacementZone.Centre:
                {
                    const float jitter = 0.5f;
                    return new Vector3(
                        cx + RandRange(-jitter, jitter, rng),
                        0f,
                        cz + RandRange(-jitter, jitter, rng));
                }

                case PlacementZone.OpenArea:
                    return new Vector3(
                        cx + RandRange(-hw * 0.5f, hw * 0.5f, rng),
                        0f,
                        cz + RandRange(-hd * 0.5f, hd * 0.5f, rng));

                case PlacementZone.NearWall:
                {
                    int wall = rng.Next(4); // 0=N, 1=S, 2=E, 3=W
                    switch (wall)
                    {
                        case 0: return new Vector3(cx + RandRange(-hw * 0.5f, hw * 0.5f, rng), 0f, cz + hd);
                        case 1: return new Vector3(cx + RandRange(-hw * 0.5f, hw * 0.5f, rng), 0f, cz - hd);
                        case 2: return new Vector3(cx + hw, 0f, cz + RandRange(-hd * 0.5f, hd * 0.5f, rng));
                        default: return new Vector3(cx - hw, 0f, cz + RandRange(-hd * 0.5f, hd * 0.5f, rng));
                    }
                }

                case PlacementZone.Corner:
                {
                    float cornerX = rng.Next(2) == 0 ? cx - hw : cx + hw;
                    float cornerZ = rng.Next(2) == 0 ? cz - hd : cz + hd;
                    return new Vector3(cornerX, 0f, cornerZ);
                }

                case PlacementZone.BehindDoor:
                {
                    if (room.doors != null && room.doors.Count > 0)
                    {
                        DoorData door = room.doors[rng.Next(room.doors.Count)];
                        Vector2 off = GetBehindDoorOffset(door.wallSide, DoorOffset);
                        return new Vector3(
                            door.position.x + off.x, 0f, door.position.z + off.y);
                    }
                    return GeneratePositionInZone(room, PlacementZone.Corner, rng);
                }

                case PlacementZone.SightlineBlind:
                {
                    if (room.doors != null && room.doors.Count > 0)
                    {
                        DoorData primary = room.doors[0];
                        return GetBlindSpotPosition(room, primary, rng);
                    }
                    return GeneratePositionInZone(room, PlacementZone.Corner, rng);
                }
            }

            return room.position.ToVector3();
        }

        /// <summary>
        /// Offset vector placing an entity behind the door swing on the room's
        /// interior side. Follows the table in design §7.3.
        /// </summary>
        private static Vector2 GetBehindDoorOffset(WallSide wall, float offset)
        {
            switch (wall)
            {
                case WallSide.North: return new Vector2( offset, -offset);
                case WallSide.South: return new Vector2(-offset,  offset);
                case WallSide.East:  return new Vector2(-offset, -offset);
                case WallSide.West:  return new Vector2( offset,  offset);
                default:             return Vector2.zero;
            }
        }

        /// <summary>
        /// Position on the wall opposite the primary entry door — the side of
        /// the room least visible from that door.
        /// </summary>
        private static Vector3 GetBlindSpotPosition(
            RoomData room, DoorData primaryDoor, System.Random rng)
        {
            float cx = room.position.x;
            float cz = room.position.z;
            float hw = room.size.width * 0.5f - WallMargin;
            float hd = room.size.depth * 0.5f - WallMargin;

            switch (primaryDoor.wallSide)
            {
                case WallSide.North:
                    return new Vector3(rng.Next(2) == 0 ? cx - hw : cx + hw, 0f, cz - hd);
                case WallSide.South:
                    return new Vector3(rng.Next(2) == 0 ? cx - hw : cx + hw, 0f, cz + hd);
                case WallSide.East:
                    return new Vector3(cx - hw, 0f, rng.Next(2) == 0 ? cz - hd : cz + hd);
                case WallSide.West:
                    return new Vector3(cx + hw, 0f, rng.Next(2) == 0 ? cz - hd : cz + hd);
                default:
                    return room.position.ToVector3();
            }
        }

        // ── Geometry / Constraint Helpers ────────────────────────────────────

        private static bool IsWithinBounds(Vector3 pos, RoomData room)
        {
            float hw = room.size.width * 0.5f - WallMargin;
            float hd = room.size.depth * 0.5f - WallMargin;
            return Mathf.Abs(pos.x - room.position.x) <= hw
                && Mathf.Abs(pos.z - room.position.z) <= hd;
        }

        private static bool HasClearance(
            Vector3 candidate, List<Vector3> existing, float minDist)
        {
            float sq = minDist * minDist;
            foreach (Vector3 pos in existing)
            {
                float dx = candidate.x - pos.x;
                float dz = candidate.z - pos.z;
                if (dx * dx + dz * dz < sq) return false;
            }
            return true;
        }

        // ── Facing Direction Helpers ─────────────────────────────────────────

        private static Vector3 CalculateFacingTowardDoor(
            RoomData room, Vector3 fromPos, System.Random rng)
        {
            if (room.doors == null || room.doors.Count == 0)
                return RandomFacingDirection(rng);

            DoorData nearest = room.doors[0];
            float bestSq = float.MaxValue;
            foreach (DoorData door in room.doors)
            {
                float dx = door.position.x - fromPos.x;
                float dz = door.position.z - fromPos.z;
                float sq = dx * dx + dz * dz;
                if (sq < bestSq) { bestSq = sq; nearest = door; }
            }

            Vector3 dir = new Vector3(
                nearest.position.x - fromPos.x, 0f, nearest.position.z - fromPos.z);
            if (dir.sqrMagnitude < 1e-6f)
                return new Vector3(1f, 0f, 0f);
            return dir.normalized;
        }

        private static Vector3 RandomFacingDirection(System.Random rng)
        {
            double angle = rng.NextDouble() * Math.PI * 2.0;
            return new Vector3((float)Math.Cos(angle), 0f, (float)Math.Sin(angle));
        }

        private static float RandRange(float min, float max, System.Random rng)
        {
            return min + (float)rng.NextDouble() * (max - min);
        }
    }
}
