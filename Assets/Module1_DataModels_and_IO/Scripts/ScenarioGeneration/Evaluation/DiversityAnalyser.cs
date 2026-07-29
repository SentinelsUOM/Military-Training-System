// =============================================================================
// DiversityAnalyser.cs
// Module 1 – Dynamic Scenario Generation
// Team Sentinels | University of Moratuwa | 2026
//
// Pairwise structural comparison of generated scenarios. ScenarioMetrics scores
// one scenario at a time, which shows how much the metric DISTRIBUTION spreads
// but not whether individual scenarios actually differ from one another. This
// analyser closes that gap: it compares scenarios head to head across their room
// graph, entity layout, NPC roles, depth profile, door states, and furniture, so
// the variability experiments can report how distinct any two runs of the same
// config really are [8][13].
//
// Read-only and side-effect free: nothing here mutates the scenarios compared.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TeamSentinels.ScenarioGeneration.DataModels;
using UnityEngine;

namespace TeamSentinels.ScenarioGeneration.Evaluation
{
    // =========================================================================
    // Pairwise Result
    // =========================================================================

    /// <summary>
    /// Structural distance between one pair of scenarios. Every field except
    /// <see cref="jaccardRoomConnectivity"/> is a DISTANCE — larger means more
    /// different. Jaccard is a SIMILARITY, so larger means more alike; it is
    /// kept in that orientation because that is how Jaccard is conventionally
    /// reported.
    /// </summary>
    public class PairwiseDiversityResult
    {
        /// <summary>Scenario ID of the first scenario in the pair.</summary>
        public string scenarioId_A;

        /// <summary>Scenario ID of the second scenario in the pair.</summary>
        public string scenarioId_B;

        /// <summary>
        /// Graph edit distance over room connectivity: the number of edges in A
        /// but not B plus the number in B but not A, with rooms identified by
        /// their index in id order. 0 means the two room graphs are wired
        /// identically.
        /// </summary>
        public float graphEditDistance;

        /// <summary>
        /// Mean Euclidean distance (metres) between corresponding entities,
        /// matched by type and then by index within that type. When the two
        /// scenarios hold different numbers of a type, only the overlapping
        /// entities are matched.
        /// </summary>
        public float entityPositionDistance;

        /// <summary>
        /// Jaccard similarity of the two room-edge sets: shared edges over
        /// total distinct edges, in [0, 1]. 1 = identical connectivity.
        /// Two edgeless layouts are treated as identical.
        /// </summary>
        public float jaccardRoomConnectivity;

        /// <summary>
        /// Sum of absolute differences in NPC role counts across the four roles
        /// (patrol, stationary guard, roaming guard, hostage guardian). 0 means
        /// both scenarios assigned the same role mix.
        /// </summary>
        public float roleDistributionDistance;

        /// <summary>
        /// Manhattan distance between the two room-depth histograms: for each
        /// BFS depth, the absolute difference in how many rooms sit at that
        /// depth, summed. Captures differences in how deep the layout runs even
        /// when the edge count matches.
        /// </summary>
        public float depthProfileDistance;

        /// <summary>
        /// Fraction of doors whose initial state differs, in [0, 1]. Doors are
        /// paired by position-sorted index; when the scenarios have different
        /// door counts the surplus doors count as mismatches, and the total is
        /// divided by the larger door count.
        /// </summary>
        public float doorStateDistance;

        /// <summary>
        /// Relative difference in total furniture: |A − B| divided by the larger
        /// of the two counts, in [0, 1]. 0 when both scenarios are unfurnished.
        /// </summary>
        public float furnitureCountDistance;

        /// <summary>
        /// Serialises this pair as one CSV row matching
        /// <see cref="DiversityAnalyser.ToCsvHeader"/>. Floats use invariant
        /// culture; IDs are quoted if they contain a comma or quote.
        /// </summary>
        /// <returns>A single CSV record with no trailing newline.</returns>
        public string ToCsvRow()
        {
            return string.Join(",", new[]
            {
                DiversityAnalyser.CsvField(scenarioId_A),
                DiversityAnalyser.CsvField(scenarioId_B),
                Format(graphEditDistance),
                Format(entityPositionDistance),
                Format(jaccardRoomConnectivity),
                Format(roleDistributionDistance),
                Format(depthProfileDistance),
                Format(doorStateDistance),
                Format(furnitureCountDistance)
            });
        }

        /// <summary>Formats a metric, mapping non-finite values to 0.</summary>
        private static string Format(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) value = 0f;
            return value.ToString("F4", CultureInfo.InvariantCulture);
        }
    }

    // =========================================================================
    // Analyser
    // =========================================================================

    /// <summary>
    /// Computes pairwise structural diversity between generated scenarios.
    /// Every measure works off the scenario data alone, so a batch exported to
    /// Scenario.json can be re-analysed without re-running the generator.
    /// </summary>
    public static class DiversityAnalyser
    {
        /// <summary>
        /// Batch size above which <see cref="CompareAll"/> warns: the pair count
        /// grows as n(n−1)/2, so 100 scenarios is already 4,950 comparisons.
        /// </summary>
        private const int LargeBatchThreshold = 100;

        /// <summary>The four NPC roles, in a fixed order for role-count vectors.</summary>
        private static readonly NpcRole[] AllRoles =
        {
            NpcRole.Patrol, NpcRole.StationaryGuard,
            NpcRole.RoamingGuard, NpcRole.HostageGuardian
        };

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Compares two scenarios across all seven structural measures.
        /// Missing or empty sections are treated as empty rather than throwing,
        /// so a degenerate scenario can still be compared.
        /// </summary>
        /// <param name="a">First scenario.</param>
        /// <param name="b">Second scenario.</param>
        /// <returns>The populated pairwise result.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when either scenario is null.
        /// </exception>
        public static PairwiseDiversityResult Compare(ScenarioData a, ScenarioData b)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (b == null) throw new ArgumentNullException(nameof(b));

            var result = new PairwiseDiversityResult
            {
                scenarioId_A = a.scenarioId ?? string.Empty,
                scenarioId_B = b.scenarioId ?? string.Empty
            };

            // ── Room graph ───────────────────────────────────────────────────
            HashSet<(int, int)> edgesA = BuildEdgeSet(a.layout);
            HashSet<(int, int)> edgesB = BuildEdgeSet(b.layout);

            int shared = edgesA.Count(edge => edgesB.Contains(edge));
            result.graphEditDistance = (edgesA.Count - shared) + (edgesB.Count - shared);

            int union = edgesA.Count + edgesB.Count - shared;
            result.jaccardRoomConnectivity = union > 0 ? shared / (float)union : 1f;

            // ── Entities ─────────────────────────────────────────────────────
            result.entityPositionDistance = ComputeEntityPositionDistance(a, b);

            // ── Roles ────────────────────────────────────────────────────────
            int[] rolesA = CountRoles(a);
            int[] rolesB = CountRoles(b);
            int roleDelta = 0;
            for (int i = 0; i < AllRoles.Length; i++) roleDelta += Math.Abs(rolesA[i] - rolesB[i]);
            result.roleDistributionDistance = roleDelta;

            // ── Depth profile ────────────────────────────────────────────────
            result.depthProfileDistance = ComputeDepthProfileDistance(a.layout, b.layout);

            // ── Door states ──────────────────────────────────────────────────
            result.doorStateDistance = ComputeDoorStateDistance(a.layout, b.layout);

            // ── Furniture ────────────────────────────────────────────────────
            int furnitureA = CountFurniture(a.layout);
            int furnitureB = CountFurniture(b.layout);
            int furnitureMax = Math.Max(furnitureA, furnitureB);
            result.furnitureCountDistance = furnitureMax > 0
                ? Math.Abs(furnitureA - furnitureB) / (float)furnitureMax
                : 0f;

            return result;
        }

        /// <summary>
        /// Compares every unique unordered pair in the batch. Null entries are
        /// skipped, so a list containing failed generations can be passed
        /// directly.
        /// </summary>
        /// <param name="scenarios">Scenarios to compare.</param>
        /// <returns>
        /// One result per pair, in (i, j) order with i &lt; j. Empty when fewer
        /// than two comparable scenarios are supplied.
        /// </returns>
        public static List<PairwiseDiversityResult> CompareAll(List<ScenarioData> scenarios)
        {
            var results = new List<PairwiseDiversityResult>();
            if (scenarios == null) return results;

            List<ScenarioData> comparable = scenarios.Where(s => s != null).ToList();
            if (comparable.Count < 2) return results;

            if (comparable.Count > LargeBatchThreshold)
            {
                long pairCount = (long)comparable.Count * (comparable.Count - 1) / 2;
                Debug.LogWarning(
                    $"[DiversityAnalyser] Comparing {comparable.Count} scenarios produces " +
                    $"{pairCount} pairs. Pair count grows quadratically — expect a long " +
                    $"run and a large CSV.");
            }

            for (int i = 0; i < comparable.Count; i++)
            {
                for (int j = i + 1; j < comparable.Count; j++)
                {
                    results.Add(Compare(comparable[i], comparable[j]));
                }
            }
            return results;
        }

        /// <summary>
        /// The CSV header matching the column order of
        /// <see cref="PairwiseDiversityResult.ToCsvRow"/>.
        /// </summary>
        /// <returns>A single CSV header record with no trailing newline.</returns>
        public static string ToCsvHeader()
        {
            return string.Join(",", new[]
            {
                "scenarioId_A", "scenarioId_B",
                "graphEditDistance", "entityPositionDistance", "jaccardRoomConnectivity",
                "roleDistributionDistance", "depthProfileDistance", "doorStateDistance",
                "furnitureCountDistance"
            });
        }

        // ── Room graph helpers ───────────────────────────────────────────────

        /// <summary>
        /// Builds the set of undirected room edges, with each room identified by
        /// its index in id order rather than by its id string. Index identity is
        /// what lets layouts with different room counts still be compared: the
        /// nth room of A is matched against the nth room of B, and edges
        /// referencing rooms that only one layout has simply fail to match.
        /// </summary>
        private static HashSet<(int, int)> BuildEdgeSet(LayoutData layout)
        {
            var edges = new HashSet<(int, int)>();
            Dictionary<string, int> indexById = BuildRoomIndex(layout);
            if (indexById.Count == 0) return edges;

            foreach (RoomData room in layout.rooms)
            {
                if (room?.id == null || room.connectedRoomIds == null) continue;
                if (!indexById.TryGetValue(room.id, out int from)) continue;

                foreach (string neighbourId in room.connectedRoomIds)
                {
                    if (neighbourId == null) continue;
                    if (!indexById.TryGetValue(neighbourId, out int to)) continue;
                    if (from == to) continue;

                    edges.Add(from < to ? (from, to) : (to, from));
                }
            }
            return edges;
        }

        /// <summary>
        /// Maps each room id to its position in id order, giving stable numeric
        /// node identities across scenarios.
        /// </summary>
        private static Dictionary<string, int> BuildRoomIndex(LayoutData layout)
        {
            var indexById = new Dictionary<string, int>(StringComparer.Ordinal);
            List<RoomData> rooms = layout?.rooms;
            if (rooms == null) return indexById;

            int index = 0;
            foreach (RoomData room in rooms
                         .Where(r => r?.id != null)
                         .OrderBy(r => r.id, StringComparer.Ordinal))
            {
                if (!indexById.ContainsKey(room.id)) indexById[room.id] = index++;
            }
            return indexById;
        }

        // ── Entity helpers ───────────────────────────────────────────────────

        /// <summary>
        /// Mean distance between entities paired by type and index. Entities are
        /// ordered by id within each type (terrorist_01, terrorist_02, …), so
        /// the pairing is stable. All three types participate, including the
        /// trainee, whose spawn point also varies between generations.
        /// </summary>
        private static float ComputeEntityPositionDistance(ScenarioData a, ScenarioData b)
        {
            float distanceSum = 0f;
            int matched = 0;

            foreach (EntityType type in new[]
                     { EntityType.Trainee, EntityType.Hostage, EntityType.Terrorist })
            {
                List<Vector3> positionsA = OrderedPositions(a, type);
                List<Vector3> positionsB = OrderedPositions(b, type);

                int pairs = Math.Min(positionsA.Count, positionsB.Count);
                for (int i = 0; i < pairs; i++)
                {
                    distanceSum += Vector3.Distance(positionsA[i], positionsB[i]);
                    matched++;
                }
            }

            return matched > 0 ? distanceSum / matched : 0f;
        }

        /// <summary>
        /// Positions of one entity type, ordered by entity id so index-based
        /// matching lines the same actors up across scenarios.
        /// </summary>
        private static List<Vector3> OrderedPositions(ScenarioData scenario, EntityType type)
        {
            if (scenario.entities == null) return new List<Vector3>();

            return scenario.entities
                .Where(e => e != null && e.type == type && e.position != null)
                .OrderBy(e => e.id, StringComparer.Ordinal)
                .Select(e => e.position.ToVector3())
                .ToList();
        }

        // ── Role helpers ─────────────────────────────────────────────────────

        /// <summary>
        /// Counts NPCs per role in <see cref="AllRoles"/> order, reading the
        /// role assignments and falling back to the navigation context when the
        /// assignments are absent.
        /// </summary>
        private static int[] CountRoles(ScenarioData scenario)
        {
            var counts = new int[AllRoles.Length];

            if (scenario.roleAssignments != null && scenario.roleAssignments.Count > 0)
            {
                foreach (RoleAssignment assignment in scenario.roleAssignments)
                {
                    if (assignment == null) continue;
                    int index = Array.IndexOf(AllRoles, assignment.role);
                    if (index >= 0) counts[index]++;
                }
                return counts;
            }

            if (scenario.navigationContext != null)
            {
                foreach (NavigationContextEntry entry in scenario.navigationContext.Values)
                {
                    if (entry == null) continue;
                    switch (entry.type)
                    {
                        case NavigationContextType.Patrol:          counts[0]++; break;
                        case NavigationContextType.StationaryGuard: counts[1]++; break;
                        case NavigationContextType.RoamingGuard:    counts[2]++; break;
                        case NavigationContextType.HostageGuardian: counts[3]++; break;
                    }
                }
            }
            return counts;
        }

        // ── Depth helpers ────────────────────────────────────────────────────

        /// <summary>
        /// Manhattan distance between the two layouts' room-depth histograms,
        /// compared bin by bin over the deeper of the two profiles.
        /// </summary>
        private static float ComputeDepthProfileDistance(LayoutData a, LayoutData b)
        {
            Dictionary<int, int> histogramA = BuildDepthHistogram(a);
            Dictionary<int, int> histogramB = BuildDepthHistogram(b);

            var depths = new HashSet<int>(histogramA.Keys);
            depths.UnionWith(histogramB.Keys);

            int distance = 0;
            foreach (int depth in depths)
            {
                histogramA.TryGetValue(depth, out int countA);
                histogramB.TryGetValue(depth, out int countB);
                distance += Math.Abs(countA - countB);
            }
            return distance;
        }

        /// <summary>
        /// Rooms per BFS depth. Depths the generator left unset (negative) are
        /// folded into depth 0 so an unstamped layout still produces a profile.
        /// </summary>
        private static Dictionary<int, int> BuildDepthHistogram(LayoutData layout)
        {
            var histogram = new Dictionary<int, int>();
            List<RoomData> rooms = layout?.rooms;
            if (rooms == null) return histogram;

            foreach (RoomData room in rooms)
            {
                if (room == null) continue;
                int depth = room.depth > 0 ? room.depth : 0;
                histogram.TryGetValue(depth, out int count);
                histogram[depth] = count + 1;
            }
            return histogram;
        }

        // ── Door helpers ─────────────────────────────────────────────────────

        /// <summary>
        /// Fraction of doors whose initial state differs. Doors are deduplicated
        /// (each edge stores a record on both of its rooms) and then sorted by
        /// position so the nth door of A lines up with the nth door of B. Where
        /// one layout has more doors than the other, every surplus door counts
        /// as a mismatch.
        /// </summary>
        private static float ComputeDoorStateDistance(LayoutData a, LayoutData b)
        {
            List<DoorData> doorsA = PositionSortedDoors(a);
            List<DoorData> doorsB = PositionSortedDoors(b);

            int maxDoors = Math.Max(doorsA.Count, doorsB.Count);
            if (maxDoors == 0) return 0f;

            int compared = Math.Min(doorsA.Count, doorsB.Count);
            int mismatches = maxDoors - compared;   // surplus doors have no counterpart

            for (int i = 0; i < compared; i++)
                if (doorsA[i].state != doorsB[i].state) mismatches++;

            return mismatches / (float)maxDoors;
        }

        /// <summary>
        /// Unique doors ordered by position (x, then z, then id) so the ordering
        /// is stable and independent of room iteration order.
        /// </summary>
        private static List<DoorData> PositionSortedDoors(LayoutData layout)
        {
            return ScenarioMetrics.CountUniqueDoors(layout)
                .OrderBy(d => d.position?.x ?? 0f)
                .ThenBy(d => d.position?.z ?? 0f)
                .ThenBy(d => d.id, StringComparer.Ordinal)
                .ToList();
        }

        // ── Furniture helpers ────────────────────────────────────────────────

        /// <summary>Total furniture items across every room in the layout.</summary>
        private static int CountFurniture(LayoutData layout)
        {
            List<RoomData> rooms = layout?.rooms;
            if (rooms == null) return 0;

            int total = 0;
            foreach (RoomData room in rooms) total += room?.furniture?.Count ?? 0;
            return total;
        }

        // ── Formatting ───────────────────────────────────────────────────────

        /// <summary>
        /// Escapes a CSV field, quoting it when it contains a comma, double
        /// quote, or line break.
        /// </summary>
        internal static string CsvField(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            if (value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}
