// =============================================================================
// LayoutGenerator.cs
// Module 1 – Dynamic Scenario Generation
// Team Sentinels | University of Moratuwa | 2026
//
// Produces room graph, corridor connectivity, door placements, and entry
// points based on evaluator-defined layout parameters. Implements the
// constructive generation approach with constraint satisfaction justified
// by [15] and [17].
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TeamSentinels.ScenarioGeneration.DataModels;

namespace TeamSentinels.ScenarioGeneration.Generators
{
    /// <summary>
    /// Generates the room graph layout from mission structure parameters.
    /// Supports all four topology types: linear, branching, hub_and_spoke, loop.
    /// All randomised decisions flow through the provided <see cref="System.Random"/>
    /// instance for full seed-based reproducibility [22].
    /// </summary>
    public class LayoutGenerator
    {
        // ── Constants ────────────────────────────────────────────────────────

        private const float CorridorGap = 2.0f;

        private static readonly Dictionary<RoomSizeCategory, RoomSize> RoomSizes =
            new Dictionary<RoomSizeCategory, RoomSize>
            {
                { RoomSizeCategory.Small,  new RoomSize(4f, 4f, 3f) },
                { RoomSizeCategory.Medium, new RoomSize(6f, 6f, 3f) },
                { RoomSizeCategory.Large,  new RoomSize(8f, 8f, 3f) }
            };

        // Cardinal direction vectors used for BFS spatial placement.
        // Fixed order: east, north, west, south — shuffled per randomness level.
        private static readonly Vector3[] BaseDirections =
        {
            new Vector3( 1, 0,  0), // east
            new Vector3( 0, 0,  1), // north
            new Vector3(-1, 0,  0), // west
            new Vector3( 0, 0, -1)  // south
        };

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Generates a complete layout from the given scenario configuration.
        /// Runs the eight-stage pipeline: topology → positions → doors →
        /// metadata → entry points → layout metadata → reachability validation.
        /// </summary>
        /// <param name="config">Evaluator-defined scenario parameters.</param>
        /// <param name="rng">
        /// Seeded random instance shared across the full generation pipeline.
        /// All calls must go through this instance to preserve reproducibility.
        /// </param>
        /// <returns>Fully populated <see cref="LayoutData"/> ready for entity placement.</returns>
        /// <exception cref="Exception">Thrown if generated layout contains unreachable rooms.</exception>
        public LayoutData Generate(ScenarioConfig config, System.Random rng)
        {
            // Stage 1 — Parameters
            RoomSize roomSize       = RoomSizes[config.missionStructure.roomSize];
            int roomCount           = SelectRoomCount(config.missionStructure.roomCount,
                                                      config.executionControls.randomnessLevel, rng);
            LayoutType layoutType   = config.missionStructure.layoutType;
            EntryType entryType     = config.missionStructure.entryType;
            RandomnessLevel rand    = config.executionControls.randomnessLevel;

            // Stage 2 — Graph topology
            Dictionary<string, List<string>> adj = BuildTopology(layoutType, roomCount, rand, rng);
            const string entryRoomId = "room_01";

            // Stage 3 — Spatial positions (BFS outward from entry)
            List<RoomData> rooms = AssignRoomPositions(adj, roomSize, rand, rng);

            // Stage 4 — Doors as room attributes [20]
            PlaceDoors(rooms, adj);

            // Stage 5 — Room metadata
            AssignRoomDepths(rooms, entryRoomId);
            AssignRoomTypes(rooms);
            AssignZoneLabels(rooms);

            // Stage 6 — Entry points
            List<EntryPointData> entryPoints = CreateEntryPoints(rooms, entryType, roomSize, rng);

            // Stage 7 — Layout metadata
            LayoutMetadata metadata = ComputeLayoutMetadata(
                rooms, layoutType, config.missionStructure.roomSize, entryType, entryPoints);

            // Stage 8 — Reachability validation (two-stage design, stage 2 [17])
            ValidateReachability(rooms, entryRoomId);

            return new LayoutData
            {
                rooms        = rooms,
                entryPoints  = entryPoints,
                layoutMetadata = metadata
            };
        }

        // ── Stage 1: Room Count ───────────────────────────────────────────────

        private static int SelectRoomCount(RoomCountRange range, RandomnessLevel rand, System.Random rng)
        {
            if (rand == RandomnessLevel.Low) return range.min;
            return rng.Next(range.min, range.max + 1);
        }

        // ── Stage 2: Topology Generation ─────────────────────────────────────

        private static Dictionary<string, List<string>> BuildTopology(
            LayoutType layoutType, int roomCount, RandomnessLevel rand, System.Random rng)
        {
            switch (layoutType)
            {
                case LayoutType.Linear:      return GenerateLinearTopology(roomCount, rng);
                case LayoutType.Branching:   return GenerateBranchingTopology(roomCount, rng);
                case LayoutType.HubAndSpoke: return GenerateHubAndSpokeTopology(roomCount, rng);
                case LayoutType.Loop:        return GenerateLoopTopology(roomCount, rand, rng);
                default:
                    throw new ArgumentException($"Unknown layout type: {layoutType}");
            }
        }

        /// <summary>
        /// Sequential chain: each room connects to at most two neighbours.
        /// Entry is at one end; maximum depth = roomCount − 1.
        /// </summary>
        private static Dictionary<string, List<string>> GenerateLinearTopology(
            int roomCount, System.Random rng)
        {
            var adj = new Dictionary<string, List<string>>(roomCount);
            for (int i = 1; i <= roomCount; i++)
            {
                EnsureNode(adj, Rid(i));
                if (i > 1) AddEdge(adj, Rid(i - 1), Rid(i));
            }
            return adj;
        }

        /// <summary>
        /// Tree structure with random child counts 1–3 per node.
        /// Entry (room_01) is the root; creates decision-point branches.
        /// </summary>
        private static Dictionary<string, List<string>> GenerateBranchingTopology(
            int roomCount, System.Random rng)
        {
            var adj = new Dictionary<string, List<string>>(roomCount);
            EnsureNode(adj, Rid(1));

            var frontier = new Queue<string>();
            frontier.Enqueue(Rid(1));
            int placed = 1;
            int nextId = 2;

            while (placed < roomCount && frontier.Count > 0)
            {
                string parentId = frontier.Dequeue();
                int maxChildren = Math.Min(3, roomCount - placed);
                if (maxChildren <= 0) break;

                int numChildren = rng.Next(1, maxChildren + 1);

                // Ensure entry has at least 2 branches when there are enough rooms
                if (parentId == Rid(1) && roomCount >= 4 && numChildren < 2)
                    numChildren = 2;

                for (int c = 0; c < numChildren; c++)
                {
                    if (placed >= roomCount) break;
                    string childId = Rid(nextId);
                    EnsureNode(adj, childId);
                    AddEdge(adj, parentId, childId);
                    frontier.Enqueue(childId);
                    nextId++;
                    placed++;
                }
            }
            return adj;
        }

        /// <summary>
        /// Star topology: central hub (room_01) connects to all spoke rooms.
        /// Hub is the critical chokepoint; all spokes are depth-1 dead ends.
        /// </summary>
        private static Dictionary<string, List<string>> GenerateHubAndSpokeTopology(
            int roomCount, System.Random rng)
        {
            var adj = new Dictionary<string, List<string>>(roomCount);
            string hubId = Rid(1);
            EnsureNode(adj, hubId);
            for (int i = 2; i <= roomCount; i++)
            {
                string spokeId = Rid(i);
                EnsureNode(adj, spokeId);
                AddEdge(adj, hubId, spokeId);
            }
            return adj;
        }

        /// <summary>
        /// Ring of rooms with optional cross-connection for 6+ rooms (40% chance
        /// at medium/high randomness). Cross-connections are never added at low
        /// randomness per the randomness control table.
        /// </summary>
        private static Dictionary<string, List<string>> GenerateLoopTopology(
            int roomCount, RandomnessLevel rand, System.Random rng)
        {
            var adj = new Dictionary<string, List<string>>(roomCount);
            int n = Math.Max(roomCount, 4);

            for (int i = 1; i <= n; i++)
                EnsureNode(adj, Rid(i));

            // Ring: room_01 → room_02 → ... → room_N → room_01
            for (int i = 1; i <= n; i++)
                AddEdge(adj, Rid(i), Rid((i % n) + 1));

            // Optional cross-connection across the ring (medium/high randomness only)
            if (n >= 6 && rand != RandomnessLevel.Low && rng.NextDouble() < 0.4)
            {
                string aId = Rid(1);
                string bId = Rid((n / 2) + 1);
                if (!adj[aId].Contains(bId))
                    AddEdge(adj, aId, bId);
            }

            return adj;
        }

        // ── Stage 3: Spatial Position Assignment ─────────────────────────────

        private static List<RoomData> AssignRoomPositions(
            Dictionary<string, List<string>> adj,
            RoomSize roomSize,
            RandomnessLevel rand,
            System.Random rng)
        {
            float stride = roomSize.width + CorridorGap;

            var placed  = new Dictionary<string, Vector3>(adj.Count);
            var visited = new HashSet<string>(adj.Count);
            var queue   = new Queue<string>();

            placed[Rid(1)] = Vector3.zero;
            visited.Add(Rid(1));
            queue.Enqueue(Rid(1));

            while (queue.Count > 0)
            {
                string parentId  = queue.Dequeue();
                Vector3 parentPos = placed[parentId];

                List<string> unvisited = adj[parentId]
                    .Where(n => !visited.Contains(n))
                    .ToList();
                if (unvisited.Count == 0) continue;

                Vector3[] dirs = ShuffleDirections(BaseDirections, rand, rng);
                int dirIdx = 0;

                foreach (string neighbourId in unvisited)
                {
                    Vector3 dir          = dirs[dirIdx % dirs.Length];
                    Vector3 candidatePos = parentPos + dir * stride;
                    dirIdx++;

                    // Try other directions if position overlaps an existing room
                    int attempts = 0;
                    while (OverlapsExisting(candidatePos, roomSize, placed) && attempts < 8)
                    {
                        dir          = dirs[dirIdx % dirs.Length];
                        candidatePos = parentPos + dir * stride;
                        dirIdx++;
                        attempts++;
                    }

                    // High randomness: apply ±0.5 m position jitter
                    if (rand == RandomnessLevel.High)
                    {
                        candidatePos.x += (float)(rng.NextDouble() - 0.5);
                        candidatePos.z += (float)(rng.NextDouble() - 0.5);
                    }

                    placed[neighbourId] = candidatePos;
                    visited.Add(neighbourId);
                    queue.Enqueue(neighbourId);
                }
            }

            var rooms = new List<RoomData>(placed.Count);
            foreach (var kvp in placed)
            {
                rooms.Add(new RoomData
                {
                    id              = kvp.Key,
                    position        = new SerializableVector3(kvp.Value),
                    size            = new RoomSize(roomSize.width, roomSize.depth, roomSize.height),
                    connectedRoomIds = new List<string>(adj[kvp.Key]),
                    doors           = new List<DoorData>(),
                    depth           = -1,
                    zoneLabel       = string.Empty
                });
            }

            rooms.Sort((a, b) => string.Compare(a.id, b.id, StringComparison.Ordinal));
            return rooms;
        }

        private static Vector3[] ShuffleDirections(Vector3[] dirs, RandomnessLevel rand, System.Random rng)
        {
            var result = (Vector3[])dirs.Clone();
            if (rand != RandomnessLevel.Low)
                FisherYates(result, rng);
            return result;
        }

        private static bool OverlapsExisting(Vector3 candidate, RoomSize size, Dictionary<string, Vector3> placed)
        {
            foreach (Vector3 pos in placed.Values)
            {
                if (Mathf.Abs(candidate.x - pos.x) < size.width &&
                    Mathf.Abs(candidate.z - pos.z) < size.depth)
                    return true;
            }
            return false;
        }

        // ── Stage 4: Door Placement ───────────────────────────────────────────

        private static void PlaceDoors(List<RoomData> rooms, Dictionary<string, List<string>> adj)
        {
            var roomMap       = rooms.ToDictionary(r => r.id);
            var processed     = new HashSet<string>(StringComparer.Ordinal);

            foreach (RoomData room in rooms)
            {
                foreach (string neighbourId in room.connectedRoomIds)
                {
                    // Process each undirected edge once
                    string edgeKey = string.Compare(room.id, neighbourId, StringComparison.Ordinal) < 0
                        ? room.id + "|" + neighbourId
                        : neighbourId + "|" + room.id;

                    if (!processed.Add(edgeKey)) continue;

                    RoomData neighbour = roomMap[neighbourId];
                    Vector3 posA = room.position.ToVector3();
                    Vector3 posB = neighbour.position.ToVector3();

                    WallSide wallA = DetermineWallSide(posB - posA);
                    WallSide wallB = OppositeWall(wallA);

                    // Door sits at the midpoint of the two room centres
                    SerializableVector3 doorPos = new SerializableVector3((posA + posB) * 0.5f);

                    // ID uses the numeric suffixes: "door_01_02"
                    string numA  = room.id.Substring(5);      // "room_01" → "01"
                    string numB  = neighbourId.Substring(5);
                    string doorId = $"door_{numA}_{numB}";

                    room.doors.Add(new DoorData
                    {
                        id             = doorId,
                        connectsToRoomId = neighbourId,
                        position       = doorPos,
                        wallSide       = wallA
                    });
                    neighbour.doors.Add(new DoorData
                    {
                        id             = doorId,
                        connectsToRoomId = room.id,
                        position       = doorPos,
                        wallSide       = wallB
                    });
                }
            }
        }

        private static WallSide DetermineWallSide(Vector3 delta)
        {
            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.z))
                return delta.x > 0 ? WallSide.East : WallSide.West;
            return delta.z > 0 ? WallSide.North : WallSide.South;
        }

        private static WallSide OppositeWall(WallSide side)
        {
            switch (side)
            {
                case WallSide.North: return WallSide.South;
                case WallSide.South: return WallSide.North;
                case WallSide.East:  return WallSide.West;
                default:             return WallSide.East; // West → East
            }
        }

        // ── Stage 5: Room Metadata ────────────────────────────────────────────

        private static void AssignRoomDepths(List<RoomData> rooms, string entryRoomId)
        {
            var roomMap = rooms.ToDictionary(r => r.id);
            var visited = new HashSet<string>(rooms.Count);
            var queue   = new Queue<RoomData>();

            RoomData entry = roomMap[entryRoomId];
            entry.depth = 0;
            visited.Add(entryRoomId);
            queue.Enqueue(entry);

            while (queue.Count > 0)
            {
                RoomData current = queue.Dequeue();
                foreach (string nId in current.connectedRoomIds)
                {
                    if (visited.Add(nId))
                    {
                        RoomData nb = roomMap[nId];
                        nb.depth = current.depth + 1;
                        queue.Enqueue(nb);
                    }
                }
            }
        }

        private static void AssignRoomTypes(List<RoomData> rooms)
        {
            int maxDepth = rooms.Max(r => r.depth);
            bool hostageAssigned = false;

            // Process in id order for consistent single hostage_room selection
            foreach (RoomData room in rooms.OrderBy(r => r.id))
            {
                if (room.depth == 0)
                {
                    room.type = RoomType.Entry;
                }
                else if (!hostageAssigned && room.depth == maxDepth && room.connectedRoomIds.Count == 1)
                {
                    room.type = RoomType.HostageRoom;
                    hostageAssigned = true;
                }
                else if (room.depth > 0 && room.connectedRoomIds.Count == 2)
                {
                    room.type = RoomType.Corridor;
                }
                else
                {
                    room.type = RoomType.Standard;
                }
            }
        }

        private static void AssignZoneLabels(List<RoomData> rooms)
        {
            // Pool of human-readable labels; "Entry Hall" and "Back Office" are reserved
            string[] pool =
            {
                "Front Room", "Side Room", "Inner Room", "Back Room",
                "Storage Room", "Office", "Utility Room", "Guard Post",
                "Checkpoint", "Storeroom", "Antechamber", "Vestibule"
            };

            var usedLabels = new HashSet<string>(StringComparer.Ordinal);
            int genericIdx = 1;

            foreach (RoomData room in rooms.OrderBy(r => r.depth).ThenBy(r => r.id))
            {
                string label;
                switch (room.type)
                {
                    case RoomType.Entry:
                        label = "Entry Hall";
                        break;
                    case RoomType.HostageRoom:
                        label = "Back Office";
                        break;
                    default:
                        label = null;
                        foreach (string candidate in pool)
                        {
                            if (!usedLabels.Contains(candidate))
                            {
                                label = candidate;
                                break;
                            }
                        }
                        if (label == null)
                        {
                            do { label = $"Room {genericIdx:00}"; genericIdx++; }
                            while (usedLabels.Contains(label));
                        }
                        break;
                }
                room.zoneLabel = label;
                usedLabels.Add(label);
            }
        }

        // ── Stage 6: Entry Points ─────────────────────────────────────────────

        private static List<EntryPointData> CreateEntryPoints(
            List<RoomData> rooms, EntryType entryType, RoomSize roomSize, System.Random rng)
        {
            var result    = new List<EntryPointData>();
            RoomData entry = rooms.First(r => r.depth == 0);

            // Primary entry: just outside the west wall of the entry room, facing east
            float primaryX = entry.position.x - (roomSize.width * 0.5f + 1.0f);
            result.Add(new EntryPointData
            {
                id             = "entry_main",
                roomId         = entry.id,
                position       = new SerializableVector3(primaryX, 0f, entry.position.z),
                facingDirection = new SerializableVector3(1f, 0f, 0f)
            });

            if (entryType == EntryType.Multiple)
            {
                var candidates = rooms
                    .Where(r => r.depth <= 1 && r.id != entry.id)
                    .OrderBy(r => r.id)
                    .ToList();

                int count = Math.Min(candidates.Count, 2);
                for (int i = 0; i < count; i++)
                {
                    RoomData room = candidates[i];
                    float sz = room.position.z - (roomSize.depth * 0.5f + 1.0f);
                    result.Add(new EntryPointData
                    {
                        id             = $"entry_secondary_{i + 1}",
                        roomId         = room.id,
                        position       = new SerializableVector3(room.position.x, 0f, sz),
                        facingDirection = new SerializableVector3(0f, 0f, 1f) // facing north into room
                    });
                }
            }

            return result;
        }

        // ── Stage 7: Layout Metadata ──────────────────────────────────────────

        private static LayoutMetadata ComputeLayoutMetadata(
            List<RoomData> rooms,
            LayoutType layoutType,
            RoomSizeCategory sizeCategory,
            EntryType entryType,
            List<EntryPointData> entryPoints)
        {
            int maxDepth = rooms.Max(r => r.depth);
            int diameter = ComputeGraphDiameter(rooms);
            BoundingBox bbox = ComputeBoundingBox(rooms, entryPoints);

            return new LayoutMetadata
            {
                totalRooms      = rooms.Count,
                layoutType      = layoutType,
                maxDepth        = maxDepth,
                graphDiameter   = diameter,
                roomSizeCategory = sizeCategory,
                entryType       = entryType,
                boundingBox     = bbox
            };
        }

        private static int ComputeGraphDiameter(List<RoomData> rooms)
        {
            var roomMap  = rooms.ToDictionary(r => r.id);
            int diameter = 0;

            // All-pairs BFS — O(V × (V+E))
            foreach (RoomData source in rooms)
            {
                var dist  = new Dictionary<string, int>(rooms.Count) { [source.id] = 0 };
                var queue = new Queue<string>();
                queue.Enqueue(source.id);

                while (queue.Count > 0)
                {
                    string cur = queue.Dequeue();
                    foreach (string nb in roomMap[cur].connectedRoomIds)
                    {
                        if (!dist.ContainsKey(nb))
                        {
                            int d = dist[cur] + 1;
                            dist[nb] = d;
                            if (d > diameter) diameter = d;
                            queue.Enqueue(nb);
                        }
                    }
                }
            }
            return diameter;
        }

        private static BoundingBox ComputeBoundingBox(List<RoomData> rooms, List<EntryPointData> entryPoints)
        {
            RoomSize size = rooms[0].size;
            float hw = size.width  * 0.5f;
            float hd = size.depth  * 0.5f;

            float minX = float.MaxValue, minZ = float.MaxValue;
            float maxX = float.MinValue, maxZ = float.MinValue;

            foreach (RoomData r in rooms)
            {
                minX = Mathf.Min(minX, r.position.x - hw);
                minZ = Mathf.Min(minZ, r.position.z - hd);
                maxX = Mathf.Max(maxX, r.position.x + hw);
                maxZ = Mathf.Max(maxZ, r.position.z + hd);
            }

            // Include entry point positions so they are within the bounding box
            foreach (EntryPointData ep in entryPoints)
            {
                minX = Mathf.Min(minX, ep.position.x);
                minZ = Mathf.Min(minZ, ep.position.z);
                maxX = Mathf.Max(maxX, ep.position.x);
                maxZ = Mathf.Max(maxZ, ep.position.z);
            }

            return new BoundingBox(
                new SerializableVector3(minX, 0f, minZ),
                new SerializableVector3(maxX, size.height, maxZ)
            );
        }

        // ── Stage 8: Reachability Validation ─────────────────────────────────

        private static void ValidateReachability(List<RoomData> rooms, string entryRoomId)
        {
            var roomMap = rooms.ToDictionary(r => r.id);
            var visited = new HashSet<string>(rooms.Count);
            var queue   = new Queue<string>();

            queue.Enqueue(entryRoomId);
            visited.Add(entryRoomId);

            while (queue.Count > 0)
            {
                string cur = queue.Dequeue();
                foreach (string nb in roomMap[cur].connectedRoomIds)
                {
                    if (visited.Add(nb))
                        queue.Enqueue(nb);
                }
            }

            if (visited.Count != rooms.Count)
                throw new Exception(
                    $"Layout generation failed: {visited.Count}/{rooms.Count} rooms reachable from {entryRoomId}. " +
                    "Retry with a different seed.");
        }

        // ── Utility Helpers ───────────────────────────────────────────────────

        private static string Rid(int n) => $"room_{n:00}";

        private static void EnsureNode(Dictionary<string, List<string>> adj, string id)
        {
            if (!adj.ContainsKey(id)) adj[id] = new List<string>();
        }

        private static void AddEdge(Dictionary<string, List<string>> adj, string a, string b)
        {
            EnsureNode(adj, a);
            EnsureNode(adj, b);
            if (!adj[a].Contains(b)) adj[a].Add(b);
            if (!adj[b].Contains(a)) adj[b].Add(a);
        }

        private static void FisherYates<T>(T[] arr, System.Random rng)
        {
            for (int i = arr.Length - 1; i > 0; i--)
            {
                int j   = rng.Next(i + 1);
                T   tmp = arr[i]; arr[i] = arr[j]; arr[j] = tmp;
            }
        }
    }
}
