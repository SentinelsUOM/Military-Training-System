// =============================================================================
// ScenarioMetrics.cs
// Module 1 – Dynamic Scenario Generation
// Team Sentinels | University of Moratuwa | 2026
//
// Quantitative metrics extracted from a generated ScenarioData for the Module 1
// evaluation study. One Extract() call turns a scenario into a flat row of
// numbers (layout complexity, door states, entity distribution, navigation
// complexity, furniture density) plus an echo of the input parameters that
// produced it, so a batch of generated scenarios can be written straight to CSV
// and analysed for controllability, variability, and expressive range [22].
//
// Read-only: nothing here mutates the scenario. Plain C# (no MonoBehaviour) so
// it runs from an editor batch script, a test, or a headless build.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using TeamSentinels.ScenarioGeneration.DataModels;
using UnityEngine;

namespace TeamSentinels.ScenarioGeneration.Evaluation
{
    // =========================================================================
    // Metrics Result
    // =========================================================================

    /// <summary>
    /// A single scenario's measured metrics plus the input parameters that
    /// generated it. One instance corresponds to one CSV row; the input echo
    /// fields (group 6) are what the analysis groups and correlates on.
    /// </summary>
    public class ScenarioMetricsResult
    {
        // ── 1. Layout complexity ─────────────────────────────────────────────

        /// <summary>Number of rooms actually generated (may differ from the requested count).</summary>
        public int roomCount;

        /// <summary>Number of unique doors; reciprocal door records sharing an ID count once.</summary>
        public int doorCount;

        /// <summary>Mean number of connected neighbours per room (2 × edges / rooms).</summary>
        public float avgConnectivity;

        /// <summary>Longest shortest path between any two rooms, in doors traversed.</summary>
        public int graphDiameter;

        /// <summary>Greatest BFS depth from the entry room.</summary>
        public int maxDepth;

        /// <summary>
        /// Cyclomatic density of the room graph: (edges − nodes + 1) / edges.
        /// 0 for a tree (exactly one route to every room); higher values mean
        /// more alternative routes and therefore more flanking opportunities.
        /// </summary>
        public float cyclicityMeasure;

        /// <summary>Number of building entry points.</summary>
        public int entryPointCount;

        // ── 2. Door state ────────────────────────────────────────────────────

        /// <summary>Unique doors whose initial state is open.</summary>
        public int doorsOpen;

        /// <summary>Unique doors whose initial state is closed.</summary>
        public int doorsClosed;

        /// <summary>Unique doors whose initial state is locked.</summary>
        public int doorsLocked;

        /// <summary>Locked doors as a fraction of all doors; 0 when there are no doors.</summary>
        public float lockedDoorFraction;

        // ── 3. Entity distribution ───────────────────────────────────────────

        /// <summary>
        /// Mean Euclidean distance (metres) of every non-trainee entity from the
        /// entry room centre. Rises with front_loaded → deep placement.
        /// </summary>
        public float avgEntityDistanceFromEntry;

        /// <summary>
        /// How tightly non-trainee entities group, in [0, 1]. For each entity the
        /// number of other entities within the cluster radius (2 × mean room
        /// width) is counted, averaged, then divided by the maximum possible
        /// neighbour count. 1 = every entity is within radius of every other.
        /// </summary>
        public float entityClusteringCoefficient;

        /// <summary>Mean distance (metres) over all hostage/terrorist pairs; 0 when either side is empty.</summary>
        public float avgHostageTerroristDistance;

        /// <summary>Smallest distance (metres) over all hostage/terrorist pairs; 0 when either side is empty.</summary>
        public float minHostageTerroristDistance;

        /// <summary>Population variance of the room depths non-trainee entities occupy.</summary>
        public float entityDepthVariance;

        /// <summary>Mean room depth of non-trainee entities.</summary>
        public float avgEntityDepth;

        // ── 4. Navigation complexity ─────────────────────────────────────────

        /// <summary>Mean number of rooms per patrol route; 0 when no NPC patrols.</summary>
        public float avgPatrolRouteLength;

        /// <summary>NPCs assigned the stationary_guard role.</summary>
        public int stationaryGuardCount;

        /// <summary>NPCs assigned the patrol role.</summary>
        public int patrolCount;

        /// <summary>NPCs assigned the roaming_guard role.</summary>
        public int roamingGuardCount;

        /// <summary>NPCs assigned the hostage_guardian role.</summary>
        public int hostageGuardianCount;

        /// <summary>Fraction of rooms visited by at least one patrol route.</summary>
        public float patrolCoverage;

        /// <summary>Mean waypoint count per NPC across all navigation contexts (roles without waypoints count as 0).</summary>
        public float avgWaypointCount;

        // ── 5. Furniture ─────────────────────────────────────────────────────

        /// <summary>Total furniture items across every room.</summary>
        public int totalFurnitureCount;

        /// <summary>Mean furniture items per room.</summary>
        public float avgFurniturePerRoom;

        /// <summary>Summed furniture footprint area divided by summed room floor area.</summary>
        public float furnitureFloorCoverage;

        /// <summary>Rooms holding at least one furniture item.</summary>
        public int furnishedRoomCount;

        // ── 6. Input parameter echo ──────────────────────────────────────────

        /// <summary>Requested room graph topology (linear, branching, hub_and_spoke, loop).</summary>
        public string layoutType;

        /// <summary>Requested room dimension category (small, medium, large).</summary>
        public string roomSizeCategory;

        /// <summary>Requested entry type (single, multiple).</summary>
        public string entryType;

        /// <summary>
        /// Requested room count, taken from <c>missionStructure.roomCount.min</c>
        /// (the production dashboard sends min = max, so min is the target).
        /// </summary>
        public int configRoomCount;

        /// <summary>Requested number of terrorist NPCs.</summary>
        public int configTerroristCount;

        /// <summary>Requested placement strategy (clustered, dispersed, front_loaded, deep).</summary>
        public string placementStrategy;

        /// <summary>
        /// Requested hostage risk level. Fixed in the production dashboard but
        /// varied programmatically by Experiments C and D, so the analysis needs
        /// the column to group on.
        /// </summary>
        public string hostageRiskLevel;

        /// <summary>Requested difficulty level (1–5). See <see cref="hostageRiskLevel"/> on why it is echoed.</summary>
        public int difficultyLevel;

        /// <summary>Requested randomness level. See <see cref="hostageRiskLevel"/> on why it is echoed.</summary>
        public string randomnessLevel;

        /// <summary>Seed the generator actually used (may differ from the requested seed after retries).</summary>
        public int seedUsed;

        /// <summary>Scenario UUID, for joining a metrics row back to its Scenario.json.</summary>
        public string scenarioId;

        // ── CSV ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Serialises this result as one CSV row whose column order matches
        /// <see cref="ScenarioMetrics.ToCsvHeader"/>. Floats use invariant
        /// culture so rows written on any machine parse identically; string
        /// fields are quoted when they contain a comma, quote, or newline.
        /// </summary>
        /// <returns>A single CSV record with no trailing newline.</returns>
        public string ToCsvRow()
        {
            var sb = new StringBuilder();

            // 1. Layout complexity
            ScenarioMetrics.AppendInt(sb, roomCount);
            ScenarioMetrics.AppendInt(sb, doorCount);
            ScenarioMetrics.AppendFloat(sb, avgConnectivity);
            ScenarioMetrics.AppendInt(sb, graphDiameter);
            ScenarioMetrics.AppendInt(sb, maxDepth);
            ScenarioMetrics.AppendFloat(sb, cyclicityMeasure);
            ScenarioMetrics.AppendInt(sb, entryPointCount);

            // 2. Door state
            ScenarioMetrics.AppendInt(sb, doorsOpen);
            ScenarioMetrics.AppendInt(sb, doorsClosed);
            ScenarioMetrics.AppendInt(sb, doorsLocked);
            ScenarioMetrics.AppendFloat(sb, lockedDoorFraction);

            // 3. Entity distribution
            ScenarioMetrics.AppendFloat(sb, avgEntityDistanceFromEntry);
            ScenarioMetrics.AppendFloat(sb, entityClusteringCoefficient);
            ScenarioMetrics.AppendFloat(sb, avgHostageTerroristDistance);
            ScenarioMetrics.AppendFloat(sb, minHostageTerroristDistance);
            ScenarioMetrics.AppendFloat(sb, entityDepthVariance);
            ScenarioMetrics.AppendFloat(sb, avgEntityDepth);

            // 4. Navigation complexity
            ScenarioMetrics.AppendFloat(sb, avgPatrolRouteLength);
            ScenarioMetrics.AppendInt(sb, stationaryGuardCount);
            ScenarioMetrics.AppendInt(sb, patrolCount);
            ScenarioMetrics.AppendInt(sb, roamingGuardCount);
            ScenarioMetrics.AppendInt(sb, hostageGuardianCount);
            ScenarioMetrics.AppendFloat(sb, patrolCoverage);
            ScenarioMetrics.AppendFloat(sb, avgWaypointCount);

            // 5. Furniture
            ScenarioMetrics.AppendInt(sb, totalFurnitureCount);
            ScenarioMetrics.AppendFloat(sb, avgFurniturePerRoom);
            ScenarioMetrics.AppendFloat(sb, furnitureFloorCoverage);
            ScenarioMetrics.AppendInt(sb, furnishedRoomCount);

            // 6. Input parameter echo
            ScenarioMetrics.AppendString(sb, layoutType);
            ScenarioMetrics.AppendString(sb, roomSizeCategory);
            ScenarioMetrics.AppendString(sb, entryType);
            ScenarioMetrics.AppendInt(sb, configRoomCount);
            ScenarioMetrics.AppendInt(sb, configTerroristCount);
            ScenarioMetrics.AppendString(sb, placementStrategy);
            ScenarioMetrics.AppendString(sb, hostageRiskLevel);
            ScenarioMetrics.AppendInt(sb, difficultyLevel);
            ScenarioMetrics.AppendString(sb, randomnessLevel);
            ScenarioMetrics.AppendInt(sb, seedUsed);
            ScenarioMetrics.AppendString(sb, scenarioId);

            return sb.ToString();
        }
    }

    // =========================================================================
    // Metrics Extraction
    // =========================================================================

    /// <summary>
    /// Extracts <see cref="ScenarioMetricsResult"/> values from generated
    /// scenarios. Every metric is derived from the scenario alone, so metrics
    /// can be recomputed from an exported Scenario.json without re-running the
    /// generator.
    /// </summary>
    public static class ScenarioMetrics
    {
        /// <summary>Cluster radius as a multiple of the mean room width.</summary>
        private const float ClusterRadiusRoomWidths = 2f;

        /// <summary>Room width assumed when a layout carries no usable room sizes.</summary>
        private const float FallbackRoomWidth = 6f;

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Measures a generated scenario across all six metric groups. Missing
        /// or empty sections (no terrorists, no patrols, no furniture, no
        /// navigation context) yield 0 rather than throwing, so a batch run can
        /// measure degenerate scenarios without special-casing them.
        /// </summary>
        /// <param name="scenario">The scenario to measure.</param>
        /// <returns>A populated result, ready for <see cref="ScenarioMetricsResult.ToCsvRow"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="scenario"/> is null.</exception>
        public static ScenarioMetricsResult Extract(ScenarioData scenario)
        {
            if (scenario == null)
                throw new ArgumentNullException(nameof(scenario));

            var result = new ScenarioMetricsResult { scenarioId = scenario.scenarioId ?? string.Empty };

            LayoutData layout = scenario.layout;
            List<RoomData> rooms = layout?.rooms ?? new List<RoomData>();
            List<DoorData> uniqueDoors = CountUniqueDoors(layout);

            // ── 1. Layout complexity ─────────────────────────────────────────
            result.roomCount = rooms.Count;
            result.doorCount = uniqueDoors.Count;

            int connectionSum = rooms.Sum(r => r.connectedRoomIds?.Count ?? 0);
            result.avgConnectivity = rooms.Count > 0 ? (float)connectionSum / rooms.Count : 0f;

            result.graphDiameter = ComputeGraphDiameter(layout);

            Dictionary<string, int> depthByRoom = ComputeDepths(layout);
            result.maxDepth = depthByRoom.Count > 0 ? depthByRoom.Values.Max() : 0;

            result.cyclicityMeasure = uniqueDoors.Count > 0
                ? (uniqueDoors.Count - rooms.Count + 1) / (float)uniqueDoors.Count
                : 0f;

            result.entryPointCount = layout?.entryPoints?.Count ?? 0;

            // ── 2. Door state ────────────────────────────────────────────────
            foreach (DoorData door in uniqueDoors)
            {
                switch (door.state)
                {
                    case DoorState.Open:   result.doorsOpen++;   break;
                    case DoorState.Locked: result.doorsLocked++; break;
                    default:               result.doorsClosed++; break;
                }
            }
            result.lockedDoorFraction = uniqueDoors.Count > 0
                ? result.doorsLocked / (float)uniqueDoors.Count
                : 0f;

            // ── 3. Entity distribution ───────────────────────────────────────
            List<EntityRecord> npcs = (scenario.entities ?? new List<EntityRecord>())
                .Where(e => e != null && e.type != EntityType.Trainee && e.position != null)
                .ToList();

            Vector3 entryPosition = GetEntryRoomPosition(layout);
            result.avgEntityDistanceFromEntry = npcs.Count > 0
                ? npcs.Average(e => Vector3.Distance(e.position.ToVector3(), entryPosition))
                : 0f;

            result.entityClusteringCoefficient =
                ComputeClusteringCoefficient(npcs, ClusterRadiusRoomWidths * MeanRoomWidth(rooms));

            List<Vector3> hostages = npcs
                .Where(e => e.type == EntityType.Hostage)
                .Select(e => e.position.ToVector3())
                .ToList();
            List<Vector3> terrorists = npcs
                .Where(e => e.type == EntityType.Terrorist)
                .Select(e => e.position.ToVector3())
                .ToList();

            if (hostages.Count > 0 && terrorists.Count > 0)
            {
                float distanceSum = 0f;
                float minDistance = float.MaxValue;
                foreach (Vector3 h in hostages)
                {
                    foreach (Vector3 t in terrorists)
                    {
                        float distance = Vector3.Distance(h, t);
                        distanceSum += distance;
                        if (distance < minDistance) minDistance = distance;
                    }
                }
                result.avgHostageTerroristDistance = distanceSum / (hostages.Count * terrorists.Count);
                result.minHostageTerroristDistance = minDistance;
            }

            List<int> npcDepths = npcs
                .Where(e => e.assignedRoom != null && depthByRoom.ContainsKey(e.assignedRoom))
                .Select(e => depthByRoom[e.assignedRoom])
                .ToList();
            if (npcDepths.Count > 0)
            {
                float meanDepth = (float)npcDepths.Average();
                result.avgEntityDepth = meanDepth;
                result.entityDepthVariance =
                    npcDepths.Sum(d => (d - meanDepth) * (d - meanDepth)) / npcDepths.Count;
            }

            // ── 4. Navigation complexity ─────────────────────────────────────
            Dictionary<string, NavigationContextEntry> navContext = scenario.navigationContext;
            List<NavigationContextEntry> navEntries = navContext?.Values
                .Where(n => n != null).ToList() ?? new List<NavigationContextEntry>();

            if (navEntries.Count > 0)
            {
                result.patrolCount = navEntries.Count(n => n.type == NavigationContextType.Patrol);
                result.stationaryGuardCount = navEntries.Count(n => n.type == NavigationContextType.StationaryGuard);
                result.roamingGuardCount = navEntries.Count(n => n.type == NavigationContextType.RoamingGuard);
                result.hostageGuardianCount = navEntries.Count(n => n.type == NavigationContextType.HostageGuardian);
            }
            else
            {
                // No navigation context (e.g. metrics taken from a partially
                // assembled scenario) — fall back to the role assignments.
                List<RoleAssignment> roles = scenario.roleAssignments ?? new List<RoleAssignment>();
                result.patrolCount = roles.Count(r => r != null && r.role == NpcRole.Patrol);
                result.stationaryGuardCount = roles.Count(r => r != null && r.role == NpcRole.StationaryGuard);
                result.roamingGuardCount = roles.Count(r => r != null && r.role == NpcRole.RoamingGuard);
                result.hostageGuardianCount = roles.Count(r => r != null && r.role == NpcRole.HostageGuardian);
            }

            List<NavigationContextEntry> patrols = navEntries
                .Where(n => n.type == NavigationContextType.Patrol && n.patrolRoute != null)
                .ToList();
            result.avgPatrolRouteLength = patrols.Count > 0
                ? (float)patrols.Average(n => n.patrolRoute.Count)
                : 0f;

            result.patrolCoverage = ComputePatrolCoverage(navContext, rooms.Count);

            result.avgWaypointCount = navEntries.Count > 0
                ? (float)navEntries.Average(n => n.waypoints?.Count ?? 0)
                : 0f;

            // ── 5. Furniture ─────────────────────────────────────────────────
            result.totalFurnitureCount = rooms.Sum(r => r.furniture?.Count ?? 0);
            result.avgFurniturePerRoom = rooms.Count > 0
                ? result.totalFurnitureCount / (float)rooms.Count
                : 0f;
            result.furnitureFloorCoverage = ComputeFurnitureFloorCoverage(rooms);
            result.furnishedRoomCount = rooms.Count(r => (r.furniture?.Count ?? 0) > 0);

            // ── 6. Input parameter echo ──────────────────────────────────────
            ConfigurationMetadata metadata = scenario.configurationMetadata;
            ScenarioConfig config = metadata?.scenarioConfig;

            result.seedUsed = metadata?.seedUsed ?? 0;

            if (config != null)
            {
                MissionStructure mission = config.missionStructure;
                if (mission != null)
                {
                    result.layoutType = EnumToString(mission.layoutType);
                    result.roomSizeCategory = EnumToString(mission.roomSize);
                    result.entryType = EnumToString(mission.entryType);
                    result.configRoomCount = mission.roomCount?.min ?? 0;
                }

                EntityConfiguration entityConfig = config.entityConfiguration;
                if (entityConfig != null)
                {
                    result.configTerroristCount = entityConfig.terroristCount;
                    result.placementStrategy = EnumToString(entityConfig.placementStrategy);
                    result.hostageRiskLevel = EnumToString(entityConfig.hostageRiskLevel);
                }

                ExecutionControls execution = config.executionControls;
                if (execution != null)
                {
                    result.difficultyLevel = execution.difficultyLevel;
                    result.randomnessLevel = EnumToString(execution.randomnessLevel);
                }
            }

            // A layout can be generated without a config echo (older files);
            // fall back to what the layout metadata recorded.
            LayoutMetadata layoutMetadata = layout?.layoutMetadata;
            if (layoutMetadata != null)
            {
                result.layoutType ??= EnumToString(layoutMetadata.layoutType);
                result.roomSizeCategory ??= EnumToString(layoutMetadata.roomSizeCategory);
                result.entryType ??= EnumToString(layoutMetadata.entryType);
            }

            result.layoutType ??= string.Empty;
            result.roomSizeCategory ??= string.Empty;
            result.entryType ??= string.Empty;
            result.placementStrategy ??= string.Empty;
            result.hostageRiskLevel ??= string.Empty;
            result.randomnessLevel ??= string.Empty;

            return result;
        }

        /// <summary>
        /// The CSV header line matching the column order produced by
        /// <see cref="ScenarioMetricsResult.ToCsvRow"/>. Write it once at the
        /// top of a batch file.
        /// </summary>
        /// <returns>A single CSV header record with no trailing newline.</returns>
        public static string ToCsvHeader()
        {
            return string.Join(",", new[]
            {
                // 1. Layout complexity
                "roomCount", "doorCount", "avgConnectivity", "graphDiameter",
                "maxDepth", "cyclicityMeasure", "entryPointCount",
                // 2. Door state
                "doorsOpen", "doorsClosed", "doorsLocked", "lockedDoorFraction",
                // 3. Entity distribution
                "avgEntityDistanceFromEntry", "entityClusteringCoefficient",
                "avgHostageTerroristDistance", "minHostageTerroristDistance",
                "entityDepthVariance", "avgEntityDepth",
                // 4. Navigation complexity
                "avgPatrolRouteLength", "stationaryGuardCount", "patrolCount",
                "roamingGuardCount", "hostageGuardianCount", "patrolCoverage",
                "avgWaypointCount",
                // 5. Furniture
                "totalFurnitureCount", "avgFurniturePerRoom",
                "furnitureFloorCoverage", "furnishedRoomCount",
                // 6. Input parameter echo
                "layoutType", "roomSizeCategory", "entryType", "configRoomCount",
                "configTerroristCount", "placementStrategy", "hostageRiskLevel",
                "difficultyLevel", "randomnessLevel", "seedUsed", "scenarioId"
            });
        }

        // ── Graph helpers ────────────────────────────────────────────────────

        /// <summary>
        /// Longest shortest path in the room graph, computed by running a BFS
        /// from every room. Unreachable pairs are ignored, so a disconnected
        /// layout reports the diameter of its largest component. Returns 0 for
        /// empty or single-room layouts.
        /// </summary>
        private static int ComputeGraphDiameter(LayoutData layout)
        {
            Dictionary<string, List<string>> adjacency = BuildAdjacency(layout);
            if (adjacency.Count < 2) return 0;

            int diameter = 0;
            foreach (string source in adjacency.Keys)
            {
                Dictionary<string, int> distances = BreadthFirstDistances(adjacency, source);
                foreach (int distance in distances.Values)
                    if (distance > diameter) diameter = distance;
            }
            return diameter;
        }

        /// <summary>
        /// Builds the room adjacency list from <see cref="RoomData.connectedRoomIds"/>,
        /// dropping references to rooms that are not in the layout and treating
        /// every edge as undirected (both directions are recorded even if only
        /// one room lists the other).
        /// </summary>
        private static Dictionary<string, List<string>> BuildAdjacency(LayoutData layout)
        {
            var adjacency = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            List<RoomData> rooms = layout?.rooms;
            if (rooms == null) return adjacency;

            foreach (RoomData room in rooms)
                if (room?.id != null && !adjacency.ContainsKey(room.id))
                    adjacency[room.id] = new List<string>();

            foreach (RoomData room in rooms)
            {
                if (room?.id == null || room.connectedRoomIds == null) continue;
                foreach (string neighbourId in room.connectedRoomIds)
                {
                    if (neighbourId == null || !adjacency.ContainsKey(neighbourId)) continue;
                    if (string.Equals(neighbourId, room.id, StringComparison.Ordinal)) continue;

                    if (!adjacency[room.id].Contains(neighbourId))
                        adjacency[room.id].Add(neighbourId);
                    if (!adjacency[neighbourId].Contains(room.id))
                        adjacency[neighbourId].Add(room.id);
                }
            }
            return adjacency;
        }

        /// <summary>
        /// BFS hop counts from <paramref name="source"/> to every reachable room.
        /// </summary>
        private static Dictionary<string, int> BreadthFirstDistances(
            Dictionary<string, List<string>> adjacency, string source)
        {
            var distances = new Dictionary<string, int>(StringComparer.Ordinal) { [source] = 0 };
            var queue = new Queue<string>();
            queue.Enqueue(source);

            while (queue.Count > 0)
            {
                string current = queue.Dequeue();
                int nextDistance = distances[current] + 1;
                foreach (string neighbour in adjacency[current])
                {
                    if (distances.ContainsKey(neighbour)) continue;
                    distances[neighbour] = nextDistance;
                    queue.Enqueue(neighbour);
                }
            }
            return distances;
        }

        /// <summary>
        /// Room depths measured as BFS hops from the entry room. Rooms the BFS
        /// cannot reach keep the depth the generator stamped on them (or 0 if
        /// that is unset), so a disconnected layout still yields a full map.
        /// </summary>
        private static Dictionary<string, int> ComputeDepths(LayoutData layout)
        {
            var depths = new Dictionary<string, int>(StringComparer.Ordinal);
            List<RoomData> rooms = layout?.rooms;
            if (rooms == null || rooms.Count == 0) return depths;

            string entryRoomId = GetEntryRoomId(layout);
            if (entryRoomId != null)
            {
                Dictionary<string, List<string>> adjacency = BuildAdjacency(layout);
                if (adjacency.ContainsKey(entryRoomId))
                    depths = BreadthFirstDistances(adjacency, entryRoomId);
            }

            foreach (RoomData room in rooms)
            {
                if (room?.id == null || depths.ContainsKey(room.id)) continue;
                depths[room.id] = room.depth > 0 ? room.depth : 0;
            }
            return depths;
        }

        // ── Door helpers ─────────────────────────────────────────────────────

        /// <summary>
        /// Collects one <see cref="DoorData"/> record per graph edge. Each door
        /// is stored twice — once on each room it connects — and both records
        /// share an ID and state, so the first record seen for an ID wins.
        /// </summary>
        internal static List<DoorData> CountUniqueDoors(LayoutData layout)
        {
            var unique = new List<DoorData>();
            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            List<RoomData> rooms = layout?.rooms;
            if (rooms == null) return unique;

            foreach (RoomData room in rooms)
            {
                if (room?.doors == null) continue;
                foreach (DoorData door in room.doors)
                {
                    if (door == null) continue;

                    // A door with no ID cannot be paired with its reciprocal;
                    // key it on the unordered room pair instead.
                    string key = !string.IsNullOrEmpty(door.id)
                        ? door.id
                        : UnorderedPairKey(room.id, door.connectsToRoomId);

                    if (seenIds.Add(key)) unique.Add(door);
                }
            }
            return unique;
        }

        /// <summary>Order-independent key for a pair of room IDs.</summary>
        private static string UnorderedPairKey(string a, string b)
        {
            a ??= string.Empty;
            b ??= string.Empty;
            return string.CompareOrdinal(a, b) <= 0 ? $"{a}|{b}" : $"{b}|{a}";
        }

        // ── Entity helpers ───────────────────────────────────────────────────

        /// <summary>
        /// Mean neighbour count within <paramref name="clusterRadius"/> metres,
        /// normalised by the maximum possible neighbour count so the result sits
        /// in [0, 1]. Returns 0 for fewer than two entities (no pair to measure).
        /// </summary>
        private static float ComputeClusteringCoefficient(
            List<EntityRecord> entities, float clusterRadius)
        {
            if (entities == null || entities.Count < 2 || clusterRadius <= 0f) return 0f;

            List<Vector3> positions = entities
                .Where(e => e?.position != null)
                .Select(e => e.position.ToVector3())
                .ToList();
            if (positions.Count < 2) return 0f;

            int neighbourTotal = 0;
            for (int i = 0; i < positions.Count; i++)
            {
                for (int j = 0; j < positions.Count; j++)
                {
                    if (i == j) continue;
                    if (Vector3.Distance(positions[i], positions[j]) <= clusterRadius)
                        neighbourTotal++;
                }
            }

            float maxNeighbours = positions.Count * (positions.Count - 1);
            return neighbourTotal / maxNeighbours;
        }

        /// <summary>
        /// Mean room width across the layout, used to scale the clustering
        /// radius so the coefficient means the same thing in small and large
        /// room layouts. Falls back to <see cref="FallbackRoomWidth"/> when no
        /// room carries a usable size.
        /// </summary>
        private static float MeanRoomWidth(List<RoomData> rooms)
        {
            List<float> widths = rooms?
                .Where(r => r?.size != null && r.size.width > 0f)
                .Select(r => r.size.width)
                .ToList();

            return widths != null && widths.Count > 0 ? widths.Average() : FallbackRoomWidth;
        }

        /// <summary>
        /// World position of the room the trainee enters through: the room named
        /// by the first entry point, else the first room typed
        /// <see cref="RoomType.Entry"/>, else the first room. Returns
        /// <see cref="Vector3.zero"/> for an empty layout.
        /// </summary>
        private static Vector3 GetEntryRoomPosition(LayoutData layout)
        {
            string entryRoomId = GetEntryRoomId(layout);
            RoomData entryRoom = layout?.rooms?
                .FirstOrDefault(r => r != null && string.Equals(r.id, entryRoomId, StringComparison.Ordinal));

            if (entryRoom?.position != null) return entryRoom.position.ToVector3();

            // No usable room record — fall back to the entry point itself.
            EntryPointData entryPoint = layout?.entryPoints?
                .FirstOrDefault(e => e?.position != null);
            return entryPoint != null ? entryPoint.position.ToVector3() : Vector3.zero;
        }

        /// <summary>
        /// Room ID the layout is entered through, or null when the layout has
        /// no rooms at all.
        /// </summary>
        private static string GetEntryRoomId(LayoutData layout)
        {
            List<RoomData> rooms = layout?.rooms;
            if (rooms == null || rooms.Count == 0) return null;

            EntryPointData entryPoint = layout.entryPoints?
                .FirstOrDefault(e => !string.IsNullOrEmpty(e?.roomId));
            if (entryPoint != null &&
                rooms.Any(r => string.Equals(r?.id, entryPoint.roomId, StringComparison.Ordinal)))
            {
                return entryPoint.roomId;
            }

            RoomData typedEntry = rooms.FirstOrDefault(r => r != null && r.type == RoomType.Entry);
            return typedEntry?.id ?? rooms[0]?.id;
        }

        // ── Navigation helpers ───────────────────────────────────────────────

        /// <summary>
        /// Fraction of rooms that appear on at least one patrol route. Returns 0
        /// when there are no rooms, no navigation context, or no patrols.
        /// </summary>
        private static float ComputePatrolCoverage(
            Dictionary<string, NavigationContextEntry> navContext, int totalRooms)
        {
            if (navContext == null || navContext.Count == 0 || totalRooms <= 0) return 0f;

            var covered = new HashSet<string>(StringComparer.Ordinal);
            foreach (NavigationContextEntry entry in navContext.Values)
            {
                if (entry == null || entry.type != NavigationContextType.Patrol) continue;
                if (entry.patrolRoute == null) continue;
                foreach (string roomId in entry.patrolRoute)
                    if (!string.IsNullOrEmpty(roomId)) covered.Add(roomId);
            }

            return Mathf.Min(covered.Count / (float)totalRooms, 1f);
        }

        // ── Furniture helpers ────────────────────────────────────────────────

        /// <summary>
        /// Summed furniture footprint area over summed room floor area. Footprint
        /// uses each item's local width (size.x) × depth (size.z); wall-anchored
        /// items only rotate by multiples of 90°, so the world footprint has the
        /// same area. Returns 0 when the layout has no floor area.
        /// </summary>
        private static float ComputeFurnitureFloorCoverage(List<RoomData> rooms)
        {
            if (rooms == null || rooms.Count == 0) return 0f;

            float floorArea = 0f;
            float furnitureArea = 0f;

            foreach (RoomData room in rooms)
            {
                if (room == null) continue;
                if (room.size != null) floorArea += room.size.width * room.size.depth;
                if (room.furniture == null) continue;

                foreach (FurnitureData item in room.furniture)
                    if (item?.size != null) furnitureArea += item.size.x * item.size.z;
            }

            return floorArea > 0f ? furnitureArea / floorArea : 0f;
        }

        // ── Formatting helpers ───────────────────────────────────────────────

        /// <summary>
        /// The enum's JSON wire value (its <see cref="EnumMemberAttribute"/>
        /// value, e.g. "hub_and_spoke") so CSV columns read exactly like the
        /// values in ScenarioConfig.json. Falls back to the C# member name.
        /// </summary>
        internal static string EnumToString<TEnum>(TEnum value) where TEnum : struct, Enum
        {
            string name = value.ToString();
            System.Reflection.MemberInfo[] members = typeof(TEnum).GetMember(name);
            if (members.Length > 0)
            {
                object[] attributes =
                    members[0].GetCustomAttributes(typeof(EnumMemberAttribute), false);
                if (attributes.Length > 0)
                {
                    string wireValue = ((EnumMemberAttribute)attributes[0]).Value;
                    if (!string.IsNullOrEmpty(wireValue)) return wireValue;
                }
            }
            return name;
        }

        /// <summary>Appends an integer field, prefixed by a comma unless it is the first field.</summary>
        internal static void AppendInt(StringBuilder sb, int value)
        {
            AppendSeparator(sb);
            sb.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Appends a float field with four decimal places in invariant culture.
        /// Non-finite values (which no metric should produce) are written as 0
        /// so the CSV always parses.
        /// </summary>
        internal static void AppendFloat(StringBuilder sb, float value)
        {
            AppendSeparator(sb);
            if (float.IsNaN(value) || float.IsInfinity(value)) value = 0f;
            sb.Append(value.ToString("F4", CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Appends a string field, quoting and escaping it when it contains a
        /// comma, double quote, or line break.
        /// </summary>
        internal static void AppendString(StringBuilder sb, string value)
        {
            AppendSeparator(sb);
            if (string.IsNullOrEmpty(value)) return;

            if (value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0)
            {
                sb.Append('"').Append(value.Replace("\"", "\"\"")).Append('"');
            }
            else
            {
                sb.Append(value);
            }
        }

        private static void AppendSeparator(StringBuilder sb)
        {
            if (sb.Length > 0) sb.Append(',');
        }
    }
}
