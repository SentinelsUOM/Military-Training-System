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
- Created `DEVLOG.md` (this file)

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
