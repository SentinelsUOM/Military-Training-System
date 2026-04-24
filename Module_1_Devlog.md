# Module 1 – Development Log

**Project:** VR-Based Hostage-Rescue Training System
**Module:** Module 1 – Dynamic Scenario Generation
**Developer:** Pramoth Dilshan | Team Sentinels | University of Moratuwa

---

## Format

Each entry follows this structure:

```
### [Date] — <short title>
**Status:** <what stage/task this session covered>
**Done:** bullet list of completed work
**Decisions:** any design or implementation choices made and why
**Issues:** problems encountered and how they were resolved
**Next:** what to do in the next session
```

---

## Log

---

### 2026-04-23 — Project analysis and planning

**Status:** Pre-implementation analysis. Phase 1 complete. Phase 2 not yet started.

**Done:**
- Full analysis of the existing Unity project template
- Read all Phase 1 algorithm design documents (Stages 3, 4, 5)
- Read all Phase 2 Stage 1 reference C# code in `Assets/Docs/Phase 2/Stage 1/`
- Confirmed `Assets/Module1_DataModels_and_IO/` already exists with data models and generator stubs in place
- Verified integration compatibility with existing Module 2 scripts (TerroristController, HostageController, EventManager)
- Created `CLAUDE.md` with accurate project structure, conventions, and integration notes
- Created `Module_1_Devlog.md` (this file)

**Decisions:**
- Module 1 working directory confirmed as `Assets/Module1_DataModels_and_IO/` — not `Assets/Scripts/ScenarioGeneration/` as originally assumed
- `SceneBuilder` will be added as a new subfolder `Assets/Module1_DataModels_and_IO/Scripts/ScenarioGeneration/SceneBuilder/`
- Integration handoff confirmed: `EventManager.NotifyScenarioReady()` is the Module 1 → Module 2 boundary
- NPC role mapping confirmed: `NpcRole.Patrol → IdleMode.Patrol`, `RoamingGuard → Wander`, `StationaryGuard → Static`, `HostageGuardian → Static`
- Runtime NavMesh baking required (`com.unity.ai.navigation` package, `NavMeshSurface.BuildNavMesh()`)
- Dynamic `PatrolLine` construction needed: SceneBuilder must create waypoint GameObjects from `navigationContext.waypoints` before NPC Awake()

**Issues:**
- None at this stage

**Next:**
- Stage 2: Implement `LayoutGenerator.cs` — translate pseudocode from `Layout_Generation_Algorithm_Design.md` into C#
  - All 4 topology methods (Linear, Branching, HubAndSpoke, Loop)
  - Spatial position assignment (BFS-based, randomness-controlled)
  - Door placement, room metadata, entry points, reachability validation

---

### 2026-04-24 — Stage 2: LayoutGenerator implementation

**Status:** Phase 2, Stage 2 complete.

**Done:**
- Implemented full `LayoutGenerator.cs` replacing the empty stub
- All four topology generators: `GenerateLinearTopology`, `GenerateBranchingTopology`, `GenerateHubAndSpokeTopology`, `GenerateLoopTopology`
- BFS spatial position assignment with overlap detection and ±0.5 m jitter at high randomness
- Fisher-Yates direction shuffle (skipped at low randomness for deterministic placement)
- Door placement as room attributes: midpoint position, wall-side from relative delta, reciprocal doors on both rooms
- BFS depth assignment from entry room; room type assignment (entry/hostage_room/corridor/standard)
- Zone label assignment from prefix pool with uniqueness guarantee and "Room NN" overflow fallback
- Single + multiple entry point creation
- All-pairs BFS graph diameter; bounding box including entry point positions
- Reachability validation (throws on unreachable rooms — ScenarioGenerator should retry on catch)
- Loop cross-connection conditioned on `randomnessLevel != Low` (matches section 8 table)

**Decisions:**
- Only ONE room is assigned `hostage_room` type (first dead-end at maxDepth by id sort) to satisfy the "single hostage" schema constraint
- Loop topology's `GenerateLoopTopology` uses a private overload with `RandomnessLevel` parameter
- Bounding box includes entry point positions so the entry point lies inside the box
- `OverlapsExisting` checks physical overlap (`|dx| < width AND |dz| < depth`), not stride proximity

**Issues:**
- None

**Next:**
- Stage 3: Implement `EntityPlacer.cs` — translate `Entity_Placement_Algorithm_Design.md` into C#

---

### 2026-04-24 — Stage 3: EntityPlacer implementation

**Status:** Phase 2, Stage 3 complete.

**Done:**
- Replaced the `EntityPlacer.cs` stub with the full three-stage hierarchical placement algorithm from `Entity_Placement_Algorithm_Design.md`
- Added public wrapper `EntityPlacementResult { entities, spawnPoints }` and entry point `Place(LayoutData, ScenarioConfig, System.Random)`
- **Stage 1 — Trainee:** placed at entry-room centre, facing `(1,0,0)`, recorded as both `EntityRecord` and `TraineeSpawnPoint`
- **Stage 2 — Hostage:** strategy-aware room selection (front_loaded → mid-depth `ceil(maxDepth/2)`; all other strategies → deepest dead-end with 1-connection fallback), placed at `ZONE_CENTRE` with ±0.5 m jitter, random facing direction, `initialState = "calm"`, and promotes the room's type to `HostageRoom` if not already set
- **Stage 3 — Terrorists:** per-strategy room selection (clustered = hostage room + adjacent rooms at depth ≥ maxDepth-1; dispersed = cycle through unique depth buckets; front_loaded = depth ≤ `ceil(maxDepth/3)`; deep = depth ≥ `ceil(maxDepth/2)`)
- Distance constraints scaled to room width: high = `0..1.5·w`, medium = `0.5·w..3·w`, low = `2·w..∞`
- Retry loop (`MaxPlacementAttempts = 100`) with hard bounds + clearance checks, hard risk constraint until at least one terrorist satisfies, then relaxed for remaining terrorists after half the attempts; fallback to room centre if all attempts fail
- Difficulty-aware zone cycling: difficulty 1–2 → `Centre`/`OpenArea`; difficulty 3 → `Centre`/`NearWall`/`Corner`; difficulty 4–5 → `BehindDoor`/`Corner`/`SightlineBlind`
- Zone position generators for all six zones including `GetBehindDoorOffset` table and `GetBlindSpotPosition` (opposite-wall corner from the primary door)
- Terrorist facing direction = toward **nearest** door in the room (not just the first), with degenerate-case fallback
- Constants exposed publicly: `WallMargin = 0.8`, `MinClearance = 1.5`, `DoorOffset = 1.2`, `MaxPlacementAttempts = 100`

**Decisions:**
- Used a wrapper class `EntityPlacementResult` instead of tuples/out parameters — keeps the public signature self-documenting and XML-doc-friendly
- Kept `GetBehindDoorOffset` exactly matching the design doc's §7.3 table even though doors sit at the midpoint between room centres (outside the wall) — when a behind-door candidate falls outside room bounds, the retry loop naturally moves on to the next zone, so correctness is preserved
- Front-loaded terrorist threshold uses `ceil(maxDepth/3)` per the implementation brief (tighter than the design doc's `ceil(maxDepth/2)`), concentrating threats closer to the entry
- `RandomFacingDirection` uses a uniform angle over `[0, 2π)` rather than a random unit vector, so facings are uniformly distributed over the horizontal plane
- Trainee `EntityRecord.metadata` left at the default (`initialState = "idle"`) — the field is unused for the trainee but keeping a non-null metadata object avoids JSON-serialisation asymmetry between entity types

**Issues:**
- None

**Next:**
- Stage 4: Implement `RoleAssigner.cs` and `NavigationContextBuilder.cs` — translate `NPC_Role_Assignment_Algorithm_Design.md` into C#

---
