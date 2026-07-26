// =============================================================================
// ScenarioGenerationTests.cs
// Module 1 – Dynamic Scenario Generation
// Team Sentinels | University of Moratuwa | 2026
//
// MonoBehaviour-driven smoke tests for the full Module 1 pipeline. Designed to
// run from the Unity Inspector via the "Run All Tests" context-menu entry, so
// the project does not need to take a dependency on NUnit / Unity Test Runner.
//
// Categories: Configuration Loading, Layout Generation, Entity Placement,
// Role Assignment, End-to-End.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using TeamSentinels.ScenarioGeneration.DataModels;
using TeamSentinels.ScenarioGeneration.Generators;
using TeamSentinels.ScenarioGeneration.IO;

namespace TeamSentinels.ScenarioGeneration.Tests
{
    /// <summary>
    /// Attach to an empty GameObject in any scene and right-click the script
    /// header in the Inspector → "Run All Tests" to execute the full suite.
    /// Results are written to the Console as <c>[TEST] {name}: PASS|FAIL</c>
    /// followed by a final <c>[TESTS] {passed}/{total} tests passed</c> line.
    /// </summary>
    public class ScenarioGenerationTests : MonoBehaviour
    {
        // Shared placement constants mirrored from EntityPlacer / ScenarioValidator.
        private const float WallMargin = 0.8f;
        private const float MinClearance = 1.5f;
        private const float BoundsEpsilon = 1e-3f;

        private int _passed;
        private int _total;

        // ── Path Helpers ─────────────────────────────────────────────────────

        private static string ConfigPath(string filename)
        {
            return Application.dataPath
                   + "/Module1_DataModels_and_IO/Resources/ScenarioConfigs/"
                   + filename;
        }

        // ── Entry Point ──────────────────────────────────────────────────────

        [ContextMenu("Run All Tests")]
        public void RunAllTests()
        {
            _passed = 0;
            _total = 0;

            // Configuration Loading
            RunTest(nameof(LoadValidDefaultConfig),    LoadValidDefaultConfig);
            RunTest(nameof(LoadMinimalConfig),         LoadMinimalConfig);
            RunTest(nameof(RejectInvalidRoomCount),    RejectInvalidRoomCount);
            RunTest(nameof(RejectInvalidHostageCount), RejectInvalidHostageCount);

            // Layout Generation
            RunTest(nameof(GenerateLinearLayout),      GenerateLinearLayout);
            RunTest(nameof(GenerateBranchingLayout),   GenerateBranchingLayout);
            RunTest(nameof(GenerateHubAndSpokeLayout), GenerateHubAndSpokeLayout);
            RunTest(nameof(GenerateLoopLayout),        GenerateLoopLayout);
            RunTest(nameof(VerifyAllRoomsReachable),   VerifyAllRoomsReachable);
            RunTest(nameof(VerifySeedReproducibility), VerifySeedReproducibility);

            // Door realism
            RunTest(nameof(VerifyEveryRoomHasDoor),        VerifyEveryRoomHasDoor);
            RunTest(nameof(VerifyDoorStatePolicy),         VerifyDoorStatePolicy);
            RunTest(nameof(VerifyDoorStatesReciprocalAndDeterministic),
                    VerifyDoorStatesReciprocalAndDeterministic);

            // Entity Placement
            RunTest(nameof(VerifyEntityCounts),           VerifyEntityCounts);
            RunTest(nameof(VerifyNoEntityOverlap),        VerifyNoEntityOverlap);
            RunTest(nameof(VerifyEntitiesWithinRoomBounds), VerifyEntitiesWithinRoomBounds);
            RunTest(nameof(VerifyHostageInDeepRoom),      VerifyHostageInDeepRoom);
            RunTest(nameof(VerifyTraineeInEntryRoom),     VerifyTraineeInEntryRoom);

            // Furniture
            RunTest(nameof(VerifyFurnitureWithinRoomBounds), VerifyFurnitureWithinRoomBounds);
            RunTest(nameof(VerifyFurnitureClearsDoors),      VerifyFurnitureClearsDoors);
            RunTest(nameof(VerifyFurnitureClearsEntities),   VerifyFurnitureClearsEntities);
            RunTest(nameof(VerifyFurnitureNoOverlap),        VerifyFurnitureNoOverlap);
            RunTest(nameof(VerifyFurnitureSeedReproducible), VerifyFurnitureSeedReproducible);

            // Role Assignment
            RunTest(nameof(VerifyHostageGuardianExists),         VerifyHostageGuardianExists);
            RunTest(nameof(VerifyAllTerroristsHaveRoles),        VerifyAllTerroristsHaveRoles);
            RunTest(nameof(VerifyNavigationContextCompleteness), VerifyNavigationContextCompleteness);

            // End-to-End
            RunTest(nameof(FullPipelineDefaultConfig),  FullPipelineDefaultConfig);
            RunTest(nameof(FullPipelineAllLayoutTypes), FullPipelineAllLayoutTypes);
            RunTest(nameof(ExportAndReload),            ExportAndReload);

            Debug.Log($"[TESTS] {_passed}/{_total} tests passed");
        }

        // ── Test Runner ──────────────────────────────────────────────────────

        private void RunTest(string name, Action body)
        {
            _total++;
            try
            {
                body();
                _passed++;
                Debug.Log($"[TEST] {name}: PASS");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TEST] {name}: FAIL — {ex.Message}");
            }
        }

        // =====================================================================
        // Configuration Loading (4 tests)
        // =====================================================================

        private void LoadValidDefaultConfig()
        {
            ScenarioConfig cfg = ScenarioConfigLoader.LoadFromFile(ConfigPath("default_config.json"));
            AssertEqual("1.0.0",                cfg.schemaVersion,                       "schemaVersion");
            AssertEqual(5,                       cfg.entityConfiguration.terroristCount,  "terroristCount");
            AssertEqual(LayoutType.Branching,   cfg.missionStructure.layoutType,         "layoutType");
        }

        private void LoadMinimalConfig()
        {
            ScenarioConfig cfg = ScenarioConfigLoader.LoadFromFile(ConfigPath("test_minimal_easy.json"));
            AssertEqual(1, cfg.executionControls.difficultyLevel,       "difficultyLevel");
            AssertEqual(3, cfg.missionStructure.roomCount.min,          "roomCount.min");
        }

        private void RejectInvalidRoomCount()
        {
            // min > max should be rejected by RoomCountRange.Validate.
            string json = BuildConfigJson(roomMin: 10, roomMax: 5, hostageCount: 1);
            try
            {
                ScenarioConfigLoader.LoadFromJson(json);
                throw new Exception("expected ScenarioConfigValidationException, none was thrown");
            }
            catch (ScenarioConfigValidationException) { /* expected */ }
        }

        private void RejectInvalidHostageCount()
        {
            // hostageCount must be 1 (schema const).
            string json = BuildConfigJson(roomMin: 5, roomMax: 8, hostageCount: 2);
            try
            {
                ScenarioConfigLoader.LoadFromJson(json);
                throw new Exception("expected ScenarioConfigValidationException, none was thrown");
            }
            catch (ScenarioConfigValidationException) { /* expected */ }
        }

        // =====================================================================
        // Layout Generation (6 tests)
        // =====================================================================

        private void GenerateLinearLayout()
        {
            ScenarioConfig cfg = MakeConfig(LayoutType.Linear, rooms: 5, seed: 1, RandomnessLevel.Low);
            LayoutData layout  = new LayoutGenerator().Generate(cfg, new System.Random(1));

            AssertEqual(5, layout.rooms.Count, "room count");
            foreach (RoomData r in layout.rooms)
            {
                if (r.connectedRoomIds.Count > 2)
                    throw new Exception(
                        $"linear: room {r.id} has {r.connectedRoomIds.Count} connections (expected ≤ 2)");
            }
        }

        private void GenerateBranchingLayout()
        {
            // Branching topology should produce at least one node with 3+ connections
            // (parent + ≥2 children). Try a small fixed seed list — branching with
            // 6 rooms commonly satisfies this; if not, the test surfaces a real gap.
            int[] seeds = { 1, 7, 42, 100, 12345 };
            LayoutData passing = null;
            foreach (int seed in seeds)
            {
                ScenarioConfig cfg = MakeConfig(LayoutType.Branching, rooms: 6, seed: seed);
                LayoutData layout  = new LayoutGenerator().Generate(cfg, new System.Random(seed));
                if (layout.rooms.Any(r => r.connectedRoomIds.Count >= 3))
                {
                    passing = layout;
                    break;
                }
            }
            if (passing == null)
                throw new Exception(
                    "no test seed produced a 6-room branching layout with a 3+ connection room");

            AssertEqual(6, passing.rooms.Count, "room count");
        }

        private void GenerateHubAndSpokeLayout()
        {
            ScenarioConfig cfg = MakeConfig(LayoutType.HubAndSpoke, rooms: 7, seed: 1, RandomnessLevel.Low);
            LayoutData layout  = new LayoutGenerator().Generate(cfg, new System.Random(1));

            AssertEqual(7, layout.rooms.Count, "room count");
            RoomData hub = layout.rooms.First(r => r.id == "room_01");
            int expected = layout.rooms.Count - 1;
            if (hub.connectedRoomIds.Count != expected)
                throw new Exception(
                    $"hub room_01 has {hub.connectedRoomIds.Count} connections, expected {expected}");

            foreach (RoomData r in layout.rooms.Where(r => r.id != "room_01"))
            {
                if (!hub.connectedRoomIds.Contains(r.id))
                    throw new Exception($"hub does not connect to {r.id}");
            }
        }

        private void GenerateLoopLayout()
        {
            // Low randomness disables the optional cross-connection so every
            // room sits on the simple ring with exactly two neighbours.
            ScenarioConfig cfg = MakeConfig(LayoutType.Loop, rooms: 5, seed: 1, RandomnessLevel.Low);
            LayoutData layout  = new LayoutGenerator().Generate(cfg, new System.Random(1));

            AssertEqual(5, layout.rooms.Count, "room count");
            foreach (RoomData r in layout.rooms)
            {
                if (r.connectedRoomIds.Count < 2)
                    throw new Exception(
                        $"loop: room {r.id} has {r.connectedRoomIds.Count} connections (expected ≥ 2)");
            }
        }

        private void VerifyAllRoomsReachable()
        {
            (LayoutType type, int rooms)[] cases =
            {
                (LayoutType.Linear,      5),
                (LayoutType.Branching,   6),
                (LayoutType.HubAndSpoke, 7),
                (LayoutType.Loop,        5)
            };

            foreach ((LayoutType type, int rooms) in cases)
            {
                ScenarioConfig cfg = MakeConfig(type, rooms: rooms, seed: 1, RandomnessLevel.Low);
                LayoutData layout  = new LayoutGenerator().Generate(cfg, new System.Random(1));

                Dictionary<string, RoomData> roomMap = layout.rooms.ToDictionary(r => r.id);
                var visited = new HashSet<string> { "room_01" };
                var queue   = new Queue<string>();
                queue.Enqueue("room_01");

                while (queue.Count > 0)
                {
                    string cur = queue.Dequeue();
                    foreach (string nb in roomMap[cur].connectedRoomIds)
                        if (visited.Add(nb)) queue.Enqueue(nb);
                }

                if (visited.Count != layout.rooms.Count)
                    throw new Exception(
                        $"{type}: only {visited.Count}/{layout.rooms.Count} rooms reachable from room_01");
            }
        }

        private void VerifySeedReproducibility()
        {
            ScenarioConfig cfg = MakeConfig(LayoutType.Branching, rooms: 7, seed: 99);
            LayoutData a = new LayoutGenerator().Generate(cfg, new System.Random(99));
            LayoutData b = new LayoutGenerator().Generate(cfg, new System.Random(99));

            AssertEqual(a.rooms.Count, b.rooms.Count, "room count");
            for (int i = 0; i < a.rooms.Count; i++)
            {
                RoomData ra = a.rooms[i];
                RoomData rb = b.rooms[i];
                if (ra.id != rb.id)
                    throw new Exception($"room[{i}] id differs: {ra.id} vs {rb.id}");
                if (Mathf.Abs(ra.position.x - rb.position.x) > 1e-4f
                    || Mathf.Abs(ra.position.z - rb.position.z) > 1e-4f)
                    throw new Exception(
                        $"room {ra.id} position differs: " +
                        $"({ra.position.x:F4},{ra.position.z:F4}) vs ({rb.position.x:F4},{rb.position.z:F4})");
                if (!ra.connectedRoomIds.SequenceEqual(rb.connectedRoomIds))
                    throw new Exception($"room {ra.id} connections differ");
            }
        }

        // ── Door realism ─────────────────────────────────────────────────────

        private void VerifyEveryRoomHasDoor()
        {
            (LayoutType type, int rooms)[] cases =
            {
                (LayoutType.Linear,      5),
                (LayoutType.Branching,   6),
                (LayoutType.HubAndSpoke, 7),
                (LayoutType.Loop,        5)
            };

            foreach ((LayoutType type, int rooms) in cases)
            {
                ScenarioConfig cfg = MakeConfig(type, rooms: rooms, seed: 1, RandomnessLevel.Low);
                LayoutData layout  = new LayoutGenerator().Generate(cfg, new System.Random(1));

                foreach (RoomData r in layout.rooms)
                {
                    if (r.doors == null || r.doors.Count == 0)
                        throw new Exception($"{type}: room {r.id} has no doors (would be sealed off)");

                    // Every door must sit on a wall side and target a real room.
                    foreach (DoorData d in r.doors)
                        if (string.IsNullOrEmpty(d.connectsToRoomId))
                            throw new Exception($"{type}: room {r.id} door {d.id} has no target room");
                }
            }
        }

        private void VerifyDoorStatePolicy()
        {
            // Low randomness => deterministic: hostage-room door locked, every
            // other door closed (entry doors included — the building stays sealed
            // until the trainee breaches it, so defenders get no sightline out).
            ScenarioConfig cfg = MakeConfig(LayoutType.Linear, rooms: 6, seed: 1, RandomnessLevel.Low);
            LayoutData layout  = new LayoutGenerator().Generate(cfg, new System.Random(1));
            Dictionary<string, RoomData> roomMap = layout.rooms.ToDictionary(r => r.id);

            foreach (RoomData room in layout.rooms)
            {
                foreach (DoorData door in room.doors)
                {
                    RoomData neighbour = roomMap[door.connectsToRoomId];
                    bool touchesHostage = room.type == RoomType.HostageRoom
                                       || neighbour.type == RoomType.HostageRoom;
                    bool touchesEntry   = room.type == RoomType.Entry
                                       || neighbour.type == RoomType.Entry;

                    if (touchesHostage)
                    {
                        if (door.state != DoorState.Locked)
                            throw new Exception(
                                $"hostage-room door {door.id} is {door.state}, expected Locked");
                    }
                    else if (touchesEntry)
                    {
                        if (door.state != DoorState.Closed)
                            throw new Exception(
                                $"entry door {door.id} is {door.state}, expected Closed");
                    }
                    else if (door.state != DoorState.Closed)
                    {
                        throw new Exception(
                            $"interior door {door.id} is {door.state}, expected Closed at low randomness");
                    }
                }
            }
        }

        private void VerifyDoorStatesReciprocalAndDeterministic()
        {
            ScenarioConfig cfg = MakeConfig(LayoutType.Branching, rooms: 7, seed: 99, RandomnessLevel.High);
            LayoutData a = new LayoutGenerator().Generate(cfg, new System.Random(99));
            LayoutData b = new LayoutGenerator().Generate(cfg, new System.Random(99));

            // Reciprocal door records within one layout must agree on state.
            Dictionary<string, RoomData> mapA = a.rooms.ToDictionary(r => r.id);
            foreach (RoomData room in a.rooms)
            {
                foreach (DoorData door in room.doors)
                {
                    RoomData neighbour = mapA[door.connectsToRoomId];
                    DoorData reciprocal = neighbour.doors.First(d => d.id == door.id);
                    if (reciprocal.state != door.state)
                        throw new Exception(
                            $"door {door.id} state disagrees across ends: " +
                            $"{door.state} vs {reciprocal.state}");
                }
            }

            // Same seed must reproduce identical door states.
            var statesA = a.rooms.SelectMany(r => r.doors)
                                 .GroupBy(d => d.id).ToDictionary(g => g.Key, g => g.First().state);
            var statesB = b.rooms.SelectMany(r => r.doors)
                                 .GroupBy(d => d.id).ToDictionary(g => g.Key, g => g.First().state);

            if (statesA.Count != statesB.Count)
                throw new Exception($"door count differs across runs: {statesA.Count} vs {statesB.Count}");
            foreach (KeyValuePair<string, DoorState> kvp in statesA)
            {
                if (!statesB.TryGetValue(kvp.Key, out DoorState other) || other != kvp.Value)
                    throw new Exception($"door {kvp.Key} state not reproducible: {kvp.Value} vs {other}");
            }
        }

        // =====================================================================
        // Entity Placement (5 tests)
        // =====================================================================

        private void VerifyEntityCounts()
        {
            ScenarioConfig cfg = ScenarioConfigLoader.LoadFromFile(ConfigPath("default_config.json"));
            (LayoutData _, EntityPlacementResult placement) = PlaceEntities(cfg);

            int trainees   = placement.entities.Count(e => e.type == EntityType.Trainee);
            int hostages   = placement.entities.Count(e => e.type == EntityType.Hostage);
            int terrorists = placement.entities.Count(e => e.type == EntityType.Terrorist);

            AssertEqual(1,                                       trainees,   "trainee count");
            AssertEqual(cfg.entityConfiguration.hostageCount,    hostages,   "hostage count");
            AssertEqual(cfg.entityConfiguration.terroristCount,  terrorists, "terrorist count");
        }

        private void VerifyNoEntityOverlap()
        {
            ScenarioConfig cfg = ScenarioConfigLoader.LoadFromFile(ConfigPath("default_config.json"));
            (LayoutData _, EntityPlacementResult placement) = PlaceEntities(cfg);

            for (int i = 0; i < placement.entities.Count; i++)
            {
                EntityRecord a = placement.entities[i];
                for (int j = i + 1; j < placement.entities.Count; j++)
                {
                    EntityRecord b = placement.entities[j];
                    float dx = a.position.x - b.position.x;
                    float dz = a.position.z - b.position.z;
                    float d  = Mathf.Sqrt(dx * dx + dz * dz);
                    if (d < MinClearance)
                        throw new Exception(
                            $"entities {a.id} and {b.id} are {d:F2}m apart (< {MinClearance}m clearance)");
                }
            }
        }

        private void VerifyEntitiesWithinRoomBounds()
        {
            ScenarioConfig cfg = ScenarioConfigLoader.LoadFromFile(ConfigPath("default_config.json"));
            (LayoutData layout, EntityPlacementResult placement) = PlaceEntities(cfg);

            Dictionary<string, RoomData> roomMap = layout.rooms.ToDictionary(r => r.id);
            foreach (EntityRecord e in placement.entities)
            {
                if (!roomMap.TryGetValue(e.assignedRoom, out RoomData room))
                    throw new Exception($"entity {e.id} references unknown room '{e.assignedRoom}'");

                float hw = room.size.width * 0.5f - WallMargin;
                float hd = room.size.depth * 0.5f - WallMargin;
                float dx = Mathf.Abs(e.position.x - room.position.x);
                float dz = Mathf.Abs(e.position.z - room.position.z);

                if (dx > hw + BoundsEpsilon || dz > hd + BoundsEpsilon)
                    throw new Exception(
                        $"entity {e.id} at ({e.position.x:F2},{e.position.z:F2}) outside " +
                        $"room {room.id} interior (centre=({room.position.x:F2},{room.position.z:F2}), " +
                        $"halfExtents=({hw:F2},{hd:F2}))");
            }
        }

        private void VerifyHostageInDeepRoom()
        {
            ScenarioConfig cfg = ScenarioConfigLoader.LoadFromFile(ConfigPath("default_config.json"));
            (LayoutData layout, EntityPlacementResult placement) = PlaceEntities(cfg);

            EntityRecord hostage = placement.entities.First(e => e.type == EntityType.Hostage);
            RoomData     room    = layout.rooms.First(r => r.id == hostage.assignedRoom);
            if (room.depth <= 0)
                throw new Exception($"hostage room {room.id} has depth {room.depth} (expected > 0)");
        }

        private void VerifyTraineeInEntryRoom()
        {
            ScenarioConfig cfg = ScenarioConfigLoader.LoadFromFile(ConfigPath("default_config.json"));
            (LayoutData layout, EntityPlacementResult placement) = PlaceEntities(cfg);

            EntityRecord trainee = placement.entities.First(e => e.type == EntityType.Trainee);
            RoomData     room    = layout.rooms.First(r => r.id == trainee.assignedRoom);
            if (room.type != RoomType.Entry)
                throw new Exception($"trainee room {room.id} type is {room.type}, expected Entry");
        }

        // =====================================================================
        // Furniture (5 tests)
        // =====================================================================

        // Mirror of FurniturePlacer's clearance constants, used to assert the
        // placement guarantees hold on the generated output.
        // InteriorOutset: rooms sit 2 m apart and SceneBuilder builds each wall
        // centred on the shared boundary plane (0.12 m thick), so the REAL
        // interior — and the furniture anchoring bound — extends this far past
        // the room's nominal half-size on every side.
        private const float FurnitureInteriorOutset = 2.0f * 0.5f - 0.06f;
        private const float FurnitureDoorKeepout    = 1.35f;
        private const float FurnitureEntityClear    = 0.6f;

        private void VerifyFurnitureWithinRoomBounds()
        {
            foreach (LayoutType type in AllLayoutTypes())
            {
                LayoutData layout = PlaceFurniture(type, seed: 7);
                foreach (RoomData room in layout.rooms)
                {
                    float hw = room.size.width * 0.5f + FurnitureInteriorOutset + BoundsEpsilon;
                    float hd = room.size.depth * 0.5f + FurnitureInteriorOutset + BoundsEpsilon;
                    foreach (FurnitureData f in room.furniture)
                    {
                        Rect r = Footprint(f);
                        if (Mathf.Abs(r.center.x - room.position.x) + r.width  * 0.5f > hw ||
                            Mathf.Abs(r.center.y - room.position.z) + r.height * 0.5f > hd)
                            throw new Exception(
                                $"{type}: furniture {f.id} footprint escapes room {room.id} interior");
                    }
                }
            }
        }

        private void VerifyFurnitureClearsDoors()
        {
            foreach (LayoutType type in AllLayoutTypes())
            {
                LayoutData layout = PlaceFurniture(type, seed: 7);
                foreach (RoomData room in layout.rooms)
                {
                    foreach (FurnitureData f in room.furniture)
                    {
                        Rect r = Footprint(f);
                        foreach (DoorData d in room.doors)
                        {
                            float dist = RectPointDistance(r, d.position.x, d.position.z);
                            if (dist < FurnitureDoorKeepout - BoundsEpsilon)
                                throw new Exception(
                                    $"{type}: furniture {f.id} is {dist:F2}m from door {d.id} " +
                                    $"(< {FurnitureDoorKeepout}m keep-out) — could block the doorway");
                        }
                    }
                }
            }
        }

        private void VerifyFurnitureClearsEntities()
        {
            foreach (LayoutType type in AllLayoutTypes())
            {
                (LayoutData layout, List<EntityRecord> entities) = PlaceFurnitureWithEntities(type, seed: 7);
                var byRoom = entities
                    .GroupBy(e => e.assignedRoom)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.position.ToVector3()).ToList());

                foreach (RoomData room in layout.rooms)
                {
                    if (!byRoom.TryGetValue(room.id, out var pts)) continue;
                    foreach (FurnitureData f in room.furniture)
                    {
                        Rect r = Footprint(f);
                        foreach (Vector3 p in pts)
                        {
                            if (p.x >= r.xMin - FurnitureEntityClear + BoundsEpsilon &&
                                p.x <= r.xMax + FurnitureEntityClear - BoundsEpsilon &&
                                p.z >= r.yMin - FurnitureEntityClear + BoundsEpsilon &&
                                p.z <= r.yMax + FurnitureEntityClear - BoundsEpsilon)
                                throw new Exception(
                                    $"{type}: entity at ({p.x:F2},{p.z:F2}) sits inside furniture " +
                                    $"{f.id} in room {room.id}");
                        }
                    }
                }
            }
        }

        private void VerifyFurnitureNoOverlap()
        {
            foreach (LayoutType type in AllLayoutTypes())
            {
                LayoutData layout = PlaceFurniture(type, seed: 7);
                foreach (RoomData room in layout.rooms)
                {
                    List<FurnitureData> items = room.furniture;
                    for (int i = 0; i < items.Count; i++)
                        for (int j = i + 1; j < items.Count; j++)
                        {
                            Rect a = Footprint(items[i]);
                            Rect b = Footprint(items[j]);
                            if (a.xMin < b.xMax - BoundsEpsilon && a.xMax > b.xMin + BoundsEpsilon &&
                                a.yMin < b.yMax - BoundsEpsilon && a.yMax > b.yMin + BoundsEpsilon)
                                throw new Exception(
                                    $"{type}: furniture {items[i].id} and {items[j].id} overlap in " +
                                    $"room {room.id}");
                        }
                }
            }
        }

        private void VerifyFurnitureSeedReproducible()
        {
            LayoutData a = PlaceFurniture(LayoutType.Branching, seed: 12345);
            LayoutData b = PlaceFurniture(LayoutType.Branching, seed: 12345);

            var itemsA = a.rooms.SelectMany(r => r.furniture).OrderBy(f => f.id).ToList();
            var itemsB = b.rooms.SelectMany(r => r.furniture).OrderBy(f => f.id).ToList();

            if (itemsA.Count != itemsB.Count)
                throw new Exception($"furniture count differs across runs: {itemsA.Count} vs {itemsB.Count}");
            if (itemsA.Count == 0)
                throw new Exception("no furniture generated — expected at least some items");

            for (int i = 0; i < itemsA.Count; i++)
            {
                FurnitureData x = itemsA[i], y = itemsB[i];
                if (x.id != y.id || x.type != y.type ||
                    Mathf.Abs(x.position.x - y.position.x) > 1e-4f ||
                    Mathf.Abs(x.position.z - y.position.z) > 1e-4f ||
                    Mathf.Abs(x.rotationY - y.rotationY) > 1e-4f ||
                    Mathf.Abs(x.size.x - y.size.x) > 1e-4f ||
                    Mathf.Abs(x.size.z - y.size.z) > 1e-4f)
                    throw new Exception($"furniture {x.id} not reproducible for a fixed seed");
            }
        }

        // =====================================================================
        // Role Assignment (3 tests)
        // =====================================================================

        private void VerifyHostageGuardianExists()
        {
            ScenarioConfig cfg = ScenarioConfigLoader.LoadFromFile(ConfigPath("default_config.json"));
            ScenarioData scenario = new ScenarioGenerator().Generate(cfg);

            int guardians = scenario.roleAssignments.Count(a => a.role == NpcRole.HostageGuardian);
            if (guardians != 1)
                throw new Exception($"expected exactly 1 hostage_guardian, got {guardians}");
        }

        private void VerifyAllTerroristsHaveRoles()
        {
            ScenarioConfig cfg = ScenarioConfigLoader.LoadFromFile(ConfigPath("default_config.json"));
            ScenarioData scenario = new ScenarioGenerator().Generate(cfg);

            List<EntityRecord> terrorists = scenario.entities
                .Where(e => e.type == EntityType.Terrorist).ToList();
            HashSet<string> assignedIds = scenario.roleAssignments
                .Select(a => a.entityId).ToHashSet();

            foreach (EntityRecord t in terrorists)
                if (!assignedIds.Contains(t.id))
                    throw new Exception($"terrorist {t.id} has no role assignment");

            if (terrorists.Count != scenario.roleAssignments.Count)
                throw new Exception(
                    $"terrorist count ({terrorists.Count}) ≠ " +
                    $"roleAssignment count ({scenario.roleAssignments.Count})");
        }

        private void VerifyNavigationContextCompleteness()
        {
            ScenarioConfig cfg = ScenarioConfigLoader.LoadFromFile(ConfigPath("default_config.json"));
            ScenarioData scenario = new ScenarioGenerator().Generate(cfg);

            foreach (RoleAssignment a in scenario.roleAssignments)
            {
                if (string.IsNullOrEmpty(a.navigationContextId))
                    throw new Exception($"role assignment {a.entityId} has empty navigationContextId");
                if (!scenario.navigationContext.ContainsKey(a.navigationContextId))
                    throw new Exception(
                        $"role assignment {a.entityId} references missing " +
                        $"navigationContextId '{a.navigationContextId}'");
            }
        }

        // =====================================================================
        // End-to-End (3 tests)
        // =====================================================================

        private void FullPipelineDefaultConfig()
        {
            ScenarioConfig cfg = ScenarioConfigLoader.LoadFromFile(ConfigPath("default_config.json"));
            ScenarioData scenario = new ScenarioGenerator().Generate(cfg);

            ValidationResult vr = scenario.configurationMetadata.validationResult;
            if (!vr.passed)
            {
                string warnings = vr.warnings != null && vr.warnings.Count > 0
                    ? string.Join("; ", vr.warnings)
                    : "(none)";
                throw new Exception(
                    $"validation failed ({vr.checksPassed}/{vr.checksRun} checks). Warnings: {warnings}");
            }
        }

        private void FullPipelineAllLayoutTypes()
        {
            LayoutType[] types =
            {
                LayoutType.Linear, LayoutType.Branching,
                LayoutType.HubAndSpoke, LayoutType.Loop
            };

            foreach (LayoutType type in types)
            {
                ScenarioConfig cfg = MakeConfig(type, rooms: 6, seed: 1);
                cfg.entityConfiguration.terroristCount   = 3;
                cfg.entityConfiguration.placementStrategy = PlacementStrategy.Dispersed;
                cfg.entityConfiguration.hostageRiskLevel  = HostageRiskLevel.Medium;

                ScenarioData scenario = new ScenarioGenerator().Generate(cfg);
                ValidationResult vr = scenario.configurationMetadata.validationResult;
                if (!vr.passed)
                {
                    string warnings = vr.warnings != null && vr.warnings.Count > 0
                        ? string.Join("; ", vr.warnings)
                        : "(none)";
                    throw new Exception($"{type}: validation failed. Warnings: {warnings}");
                }
            }
        }

        private void ExportAndReload()
        {
            ScenarioConfig cfg = ScenarioConfigLoader.LoadFromFile(ConfigPath("default_config.json"));
            ScenarioData scenario = new ScenarioGenerator().Generate(cfg);

            string path = Path.Combine(
                Application.temporaryCachePath,
                $"test_export_{Guid.NewGuid():N}.json");

            try
            {
                ScenarioExporter.ExportToFile(scenario, path);
                ScenarioData reloaded = ScenarioExporter.LoadFromFile(path);

                AssertEqual(scenario.scenarioId,      reloaded.scenarioId,      "scenarioId");
                AssertEqual(scenario.entities.Count,  reloaded.entities.Count,  "entity count");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        /// <summary>
        /// Builds a minimal valid <see cref="ScenarioConfig"/> with overridable
        /// layout type, room count, and seed. Other fields are sensible defaults
        /// that satisfy the validator on every layout type.
        /// </summary>
        private static ScenarioConfig MakeConfig(
            LayoutType type,
            int rooms,
            int seed,
            RandomnessLevel randomness = RandomnessLevel.Medium)
        {
            return new ScenarioConfig
            {
                schemaVersion = "1.0.0",
                missionStructure = new MissionStructure
                {
                    missionType = MissionType.HostageRescue,
                    roomCount   = new RoomCountRange(rooms, rooms),
                    roomSize    = RoomSizeCategory.Medium,
                    layoutType  = type,
                    entryType   = EntryType.Single
                },
                entityConfiguration = new EntityConfiguration
                {
                    hostageCount      = 1,
                    terroristCount    = 4,
                    placementStrategy = PlacementStrategy.Dispersed,
                    hostageRiskLevel  = HostageRiskLevel.Medium
                },
                executionControls = new ExecutionControls
                {
                    difficultyLevel  = 3,
                    randomnessLevel  = randomness,
                    seed             = seed,
                    timeLimit        = null,
                    customLabel      = null
                }
            };
        }

        /// <summary>
        /// Runs the layout + entity placement stages of the pipeline against the
        /// given config and returns both products. Uses a single RNG so the
        /// result mirrors what <see cref="ScenarioGenerator"/> produces.
        /// </summary>
        private static (LayoutData layout, EntityPlacementResult placement) PlaceEntities(ScenarioConfig cfg)
        {
            int seed = cfg.executionControls.seed ?? 0;
            var rng        = new System.Random(seed);
            LayoutData lay = new LayoutGenerator().Generate(cfg, rng);
            EntityPlacementResult res = new EntityPlacer().Place(lay, cfg, rng);
            return (lay, res);
        }

        private static LayoutType[] AllLayoutTypes() => new[]
        {
            LayoutType.Linear, LayoutType.Branching, LayoutType.HubAndSpoke, LayoutType.Loop
        };

        /// <summary>
        /// Runs layout → entity placement → furniture placement on one shared RNG
        /// (mirroring <see cref="ScenarioGenerator"/>'s ordering) and returns the
        /// layout with each room's furniture populated.
        /// </summary>
        private static LayoutData PlaceFurniture(LayoutType type, int seed)
        {
            return PlaceFurnitureWithEntities(type, seed).layout;
        }

        private static (LayoutData layout, List<EntityRecord> entities) PlaceFurnitureWithEntities(
            LayoutType type, int seed)
        {
            ScenarioConfig cfg = MakeConfig(type, rooms: 6, seed: seed);
            var rng = new System.Random(seed);
            LayoutData layout = new LayoutGenerator().Generate(cfg, rng);
            EntityPlacementResult placement = new EntityPlacer().Place(layout, cfg, rng);
            new FurniturePlacer().Place(layout, placement.entities, cfg, rng);
            return (layout, placement.entities);
        }

        /// <summary>
        /// World-space X/Z footprint of a furniture item. A yaw of 90/270° turns
        /// the item, swapping its width and depth extents. (Rect.y is the Z axis.)
        /// </summary>
        private static Rect Footprint(FurnitureData f)
        {
            bool turned = Mathf.Abs(Mathf.DeltaAngle(f.rotationY, 90f)) < 1f
                       || Mathf.Abs(Mathf.DeltaAngle(f.rotationY, 270f)) < 1f;
            float halfX = (turned ? f.size.z : f.size.x) * 0.5f;
            float halfZ = (turned ? f.size.x : f.size.z) * 0.5f;
            return new Rect(f.position.x - halfX, f.position.z - halfZ, halfX * 2f, halfZ * 2f);
        }

        private static float RectPointDistance(Rect r, float x, float z)
        {
            float nx = Mathf.Clamp(x, r.xMin, r.xMax);
            float nz = Mathf.Clamp(z, r.yMin, r.yMax);
            float dx = x - nx, dz = z - nz;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>
        /// Hand-rolled JSON for the rejection tests so we can deliberately set
        /// schema-violating fields without going through the validating data
        /// model defaults.
        /// </summary>
        private static string BuildConfigJson(int roomMin, int roomMax, int hostageCount)
        {
            return
                "{" +
                  "\"schemaVersion\":\"1.0.0\"," +
                  "\"missionStructure\":{" +
                    "\"missionType\":\"hostage_rescue\"," +
                    $"\"roomCount\":{{\"min\":{roomMin},\"max\":{roomMax}}}," +
                    "\"roomSize\":\"medium\"," +
                    "\"layoutType\":\"branching\"," +
                    "\"entryType\":\"single\"" +
                  "}," +
                  "\"entityConfiguration\":{" +
                    $"\"hostageCount\":{hostageCount}," +
                    "\"terroristCount\":4," +
                    "\"placementStrategy\":\"dispersed\"," +
                    "\"hostageRiskLevel\":\"medium\"" +
                  "}," +
                  "\"executionControls\":{" +
                    "\"difficultyLevel\":3," +
                    "\"randomnessLevel\":\"medium\"," +
                    "\"seed\":1," +
                    "\"timeLimit\":null," +
                    "\"customLabel\":null" +
                  "}" +
                "}";
        }

        private static void AssertEqual<T>(T expected, T actual, string field)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new Exception($"{field}: expected '{expected}', got '{actual}'");
        }
    }
}
