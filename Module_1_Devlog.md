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

### 2026-04-24 — Stage 4: RoleAssigner + NavigationContextBuilder implementation

**Status:** Phase 2, Stage 4 complete.

**Done:**
- Replaced the `RoleAssigner.cs` stub with the full depth-based role assignment algorithm from `NPC_Role_Assignment_Algorithm_Design.md` §4
- Public entry point `Assign(LayoutData, List<EntityRecord>, SpawnPointData, ScenarioConfig, System.Random)` returning `List<RoleAssignment>`
- **Step 1 — Hostage Guardian:** always assigned first; single-terrorist case returns immediately, otherwise the terrorist already in the hostage room (ties broken by distance then id) or the globally nearest terrorist becomes guardian with priority = 1
- **Step 2 — Role quotas:** `GetRoleQuotas(remaining, difficulty)` uses the exact decision matrix from §3.1 via a switch over `remaining ∈ [1,7]` and three difficulty brackets (1-2, 3, 4-5). Remaining ≤ 7 is guaranteed by the schema's `terroristCount ≤ 8` constraint
- **Step 3 — Depth zones:** `shallowMax = ⌈maxDepth/3⌉`, `deepMin = ⌈2·maxDepth/3⌉`. Degenerate case `maxDepth ≤ 1` converts all `guardCount` allocation to `roamingCount` (covers hub-and-spoke and flat layouts)
- **Step 4 — Stable sort:** terrorists sorted by assigned-room depth ascending with id as tiebreaker, ensuring deterministic assignment under a fixed seed
- **Step 5 — Zone-matched assignment:** patrol (priority 3) → shallow, stationary_guard (priority 2) → deep, roaming_guard (priority 2) → mid/default, with overflow falling back to any slot that still has quota
- Replaced the `NavigationContextBuilder.cs` stub with the full §5 context builders: `BuildPatrolContext`, `BuildStationaryGuardContext`, `BuildRoamingGuardContext`, `BuildHostageGuardianContext`
- **Patrol:** route = assigned room + up to two adjacent non-entry rooms + return to start; waypoints = room centres along the route; `looping = true`
- **Stationary guard:** `guardPosition` = entity's already placed position; `facingDirection` = normalised vector to the **nearest** door in the room
- **Roaming guard:** `roamingRoomIds` = assigned room + up to two adjacent non-entry rooms (max 3 total); waypoints = room centres + explicit return waypoint at the anchor; `anchorRoomId` = assigned room
- **Hostage guardian:** `guardedEntityId = "hostage_01"`; `facingDirection` = normalised vector to the room's **primary (first)** door
- All four builders share a single `ComputeFacing` helper with a consistent fallback direction `(-1, 0, 0)` when the room has no doors or the source coincides with the target
- All navigation contexts constructed via the factory methods on `NavigationContextEntry` (CreatePatrol / CreateStationaryGuard / CreateRoamingGuard / CreateHostageGuardian) so the `type` discriminator and role-specific fields stay in sync

**Decisions:**
- Used a switch over `remaining` rather than the design doc's formula-based `GetRoleQuotas` (⌈0.33⌉, ⌊0.33⌋ etc.) so output matches the §3.1 table verbatim — the formula rounds to the same values in most cases but diverges at `remaining = 3, difficulty = 3` (formula gives `(1,2,0)` via `⌊0.99⌋=0`; table gives `(1,1,1)`)
- Overflow handling after the primary zone-match pass always fills remaining quota slots in the order patrol → stationary_guard → roaming_guard (rather than the pseudocode's patrol → roaming → guard), which keeps distribution closer to the matrix when a layout can't provide the expected depth zones — `roaming_guard` remains the terminal fallback since it has the most permissive zone requirement
- Patrol waypoints use only room centres (per the implementation brief) rather than the pseudocode's room-centre + door-midpoint interleaving; door midpoints are already implicitly traversed by the NavMesh agent between adjacent room centres, so they would only add redundant path nodes
- Stationary guards face their **nearest** door (matches EntityPlacer's facing convention), while hostage guardians face the **primary/first** door (matches the design doc's "primary entry door" semantics for the guardian role)
- Facing-fallback constant lifted to a single static `FallbackFacing = (-1, 0, 0)` used by both guard and guardian builders — keeps the prompt's fallback contract consistent and trivially auditable
- `MaxPatrolRooms = 3` and `MaxRoamingRooms = 3` exposed as named constants rather than magic numbers for future tuning

**Issues:**
- None — both generators compile against the existing DataModels and integrate with `LayoutData`/`EntityRecord` contracts from stages 2 and 3

**Next:**
- Stage 5: Implement `ScenarioGenerator.cs` orchestrator (wires Layout → Entities → Roles → NavigationContext and stamps `ConfigurationMetadata`) and `ScenarioValidator.cs` (post-generation invariants: reachability, role coverage, `terroristCount ≤ roomCount.max × 2`, etc.)

---

### 2026-04-25 — Stage 5 (part 1): ScenarioGenerator orchestrator

**Status:** Stage 5 orchestrator complete. `ScenarioValidator.cs` still pending (Phase 3).

**Done:**
- Replaced the `NotImplementedException` stub in `Generators/ScenarioGenerator.cs` with the full pipeline: seed resolve → `LayoutGenerator.Generate` → `EntityPlacer.Place` → `RoleAssigner.Assign` → `NavigationContextBuilder.Build` → `ScenarioData` assembly
- Pipeline-stage instances (`LayoutGenerator`, `EntityPlacer`, `RoleAssigner`, `NavigationContextBuilder`) stored as `private readonly` fields and constructed once in the `ScenarioGenerator` constructor — the orchestrator is reusable across generations
- Seed resolution: `config.executionControls.seed ?? Environment.TickCount`; the resolved seed is written into `ConfigurationMetadata.seedUsed` (if retries bumped it, the final working seed is recorded, not the original)
- Retry logic: up to 3 retries on `LayoutGenerator` failures (unreachable rooms), seed incremented by 1 each retry, each retry logged via `Debug.LogWarning` with attempt index and old/new seed
- `ConfigurationMetadata` stamped with embedded `scenarioConfig`, ISO 8601 UTC timestamp via `DateTime.UtcNow.ToString("o")`, `seedUsed`, `generatorVersion = "1.0.0"`, and a placeholder `ValidationResult.CreatePassed(0)` (to be replaced in Phase 3 once `ScenarioValidator` is implemented)
- `scenarioId` generated via `Guid.NewGuid().ToString()` (UUID v4)
- Per-stage `Debug.Log` messages added at the exact key points requested (resolving seed, layout generated, entities placed, roles assigned, navigation context built, scenario assembled)
- Convenience methods retained/added: `GenerateAndExport(configPath, outputPath = null)` (load → generate → export) and `GenerateFromJson(configJson)` (parse raw JSON → generate)
- `public const string GeneratorVersion = "1.0.0"` preserved as a single source of truth for the version string

**Decisions:**
- Orchestrator is a plain C# class, not a `MonoBehaviour` — matches the convention for all non-Scene generators in Module 1
- Pipeline stages instantiated once in the constructor rather than on every `Generate` call — stages are stateless so reuse is safe, avoids per-generation allocation churn
- Retry loop uses `catch (Exception ex) when (attempt < MaxLayoutRetries)` filter so only retryable failures are swallowed; the final attempt's exception still propagates naturally. Defensive post-loop null check re-throws a descriptive `Exception` in case control somehow exits the loop without a layout (shouldn't happen, but belt-and-braces given the `when` filter semantics)
- A **single** `System.Random` instance is threaded through Layout → Entities → Roles → Navigation. On retry, a fresh `System.Random(seedInUse + 1)` replaces the old one so the entire downstream pipeline is reproducibly derived from the seed that actually produced the layout
- `seedUsed` records the seed that successfully produced the scenario (post-retry), not the originally requested seed — this preserves the reproducibility contract (same seed + same config = same output) for the output file
- Validation stub uses `ValidationResult.CreatePassed(0)` with `checksRun = 0` so downstream consumers can tell the validator has not yet been executed (Phase 3 will set `checksRun` to the real count)
- `MaxLayoutRetries = 3` kept as a named `private const` rather than a magic `3` — central tuning point if we observe real-world retry rates during Stage 6 integration testing

**Issues:**
- None — orchestrator compiles against the existing generator signatures; no data-model or I/O changes required

**Next:**
- Stage 5 (part 2): implement `Validation/ScenarioValidator.cs` with post-generation invariants (reachability from entry room, every terrorist has a role assignment and a navigation context, `terroristCount ≤ roomCount.max × 2`, hostage room reachable, all referenced entity IDs exist). Wire its output into `ConfigurationMetadata.validationResult` in place of the current `CreatePassed(0)` placeholder
- Stage 6: `SceneBuilder.cs` — consume the generated `ScenarioData` to build the Unity scene (room prefabs, NavMesh bake, NPC instantiation, `EventManager.NotifyScenarioReady()` handoff)

---

### 2026-04-25 — Stage 5 (part 2): ScenarioValidator implementation

**Status:** `Validation/ScenarioValidator.cs` complete with two-tier validation (10 checks). Pipeline still wires `ValidationResult.CreatePassed(0)` placeholder — wiring step pending.

**Done:**
- Replaced the Phase 3 stub in `Validation/ScenarioValidator.cs` with the full validator. Public surface is unchanged: `ValidationResult Validate(ScenarioData scenario)`
- Tier 1 (local): `RoomBoundsCheck` (positive width/depth/height), `EntityBoundsCheck` (within room interior at 0.8 m wall margin), `DoorConsistencyCheck` (every `connectsToRoomId` exists, with reciprocal door pointing back, matching door IDs), `EntityMetadataCheck` (non-empty/unique ids, defined `EntityType` value)
- Tier 2 (global): `ReachabilityCheck` (BFS from trainee spawn visits every room), `HostagePathCheck` (door-graph path trainee → hostage room), `EntityOverlapCheck` (no two entities within 1.5 m XZ), `NavigationContextCheck` (every `roleAssignment.navigationContextId` resolves to a key), `ReferentialIntegrityCheck` (room IDs in `spawnPoints`, `entities`, `roleAssignments`, and `navigationContext` entries — `guardRoomId`, `hostageRoomId`, `anchorRoomId`, `roamingRoomIds`, `patrolRoute` — all exist in `layout.rooms`), `RoleAssignmentCompletenessCheck` (every terrorist has exactly one assignment, and assignments never reference non-terrorist entities)
- All ten checks run unconditionally — failures collected into `warnings`, never short-circuited. Each check logs `[ScenarioValidator] CHECK {name}: {PASS|FAIL} — {message}`
- `passed` is true only when `checksPassed == TotalChecks` (10). `TotalChecks` exposed as `public const int` so callers and tests can reference the canonical count
- Plain C# class in `TeamSentinels.ScenarioGeneration.Validation` namespace — no `MonoBehaviour`. XML doc comments on every public/protected member and on each private check method
- Constants `WallMargin = 0.8f` and `MinClearance = 1.5f` mirrored from `EntityPlacer` so the validator reads the same thresholds the placer enforces (kept as separate constants rather than referencing `EntityPlacer.WallMargin` to keep the `Validation` namespace independent of `Generators`)
- Helpers: `BuildRoomMap`, `BfsFrom(start, roomMap)`, `IsKnownRoom(roomId, set, out error)` factor the repeated lookup/BFS logic out of individual checks

**Decisions:**
- Each check returns `(bool passed, string message)` and is dispatched by a small `RunCheck` runner that handles logging and warnings list mutation. This keeps every check self-contained and trivially unit-testable; the runner is the only place that knows about `Debug.Log` formatting
- Each check stops at the **first** offending item it finds (e.g. first out-of-bounds entity) and returns a descriptive message naming the specific item. The 10-check list itself never short-circuits, so the user still sees one failure per failing check. This matches the brief ("run ALL checks") while keeping individual failure messages actionable rather than dumping every single offender per check
- `EntityOverlapCheck` uses squared-distance comparison (`sq < minSq`) to avoid `Sqrt` per pair; only computes the actual distance when reporting a failure. O(N²) is acceptable since `terroristCount ≤ 8` per schema, so worst-case ~10 entities ⇒ 45 pairs
- `DoorConsistencyCheck` requires reciprocal doors to share the same `id` (matches `LayoutGenerator.PlaceDoors` which writes the same `door_AA_BB` id on both ends). Catches the realistic regression where one side's door is dropped or renamed
- `ReferentialIntegrityCheck` and `EntityBoundsCheck` are intentionally redundant on the "assignedRoom exists" rule — a missing assigned room is reported by both, but the messages disambiguate (bounds check fails earlier; ref-integrity sweeps the rest). Both are needed because the bounds check needs the room object up-front, and the ref-integrity check is the canonical guarantee that *every* room reference (not just those touched by other checks) resolves
- `RoleAssignmentCompletenessCheck` enforces exactly-one in **both** directions: every terrorist has ≥1 assignment (no orphan terrorists), no terrorist has >1 assignment (no duplicates), and no assignment references a non-terrorist entity (no stray rows). The brief only required the first two but the third is cheap to add and prevents a future regression where a hostage_01 row accidentally lands in `roleAssignments`
- Checks defensively handle `null` collections (rooms, entities, assignments) — they return a failure message rather than NRE so the validator can still produce a usable `ValidationResult` for partially-malformed scenarios. The only top-level `ArgumentNullException` is on `scenario` itself
- Validator does **not** mutate the scenario or set `validationResult` on it — that wiring belongs in `ScenarioGenerator`. The validator is a pure read-only function so it can be invoked independently (e.g. on a loaded Scenario.json from disk) for re-verification without changing the data

**Issues:**
- None during implementation. The orchestrator still hands callers a placeholder `ValidationResult.CreatePassed(0)` — the next step is to wire `new ScenarioValidator().Validate(scenario)` into the assembly stage

**Next:**
- Wire `ScenarioValidator` into `ScenarioGenerator.Generate`: instantiate alongside other pipeline stages, call `Validate(scenario)` after assembly, replace the `ValidationResult.CreatePassed(0)` placeholder in `ConfigurationMetadata.validationResult` with the real result. Decide whether a failed validation should throw or be returned as-is (current data model embeds it without halting)
- Stage 6: `SceneBuilder.cs` — consume the generated `ScenarioData` to build the Unity scene (room prefabs, NavMesh bake, NPC instantiation, `EventManager.NotifyScenarioReady()` handoff)

---

### 2026-04-25 — Stage 5 (part 3): ScenarioValidator wired into ScenarioGenerator

**Status:** `ScenarioGenerator.Generate` now runs the validator as the final pipeline step and retries on validation failure. Public API unchanged.

**Done:**
- Added `private readonly ScenarioValidator _scenarioValidator` field, initialised in the constructor alongside the other pipeline stages
- Imported `TeamSentinels.ScenarioGeneration.Validation`
- Restructured `Generate` to wrap the **entire** pipeline (layout → entities → roles → navigation → assembly → validation) in a single retry loop, replacing the previous layout-only retry. The retry budget remains `MaxLayoutRetries = 3` (4 total attempts including the initial)
- After assembly, calls `_scenarioValidator.Validate(scenario)` and stamps the result onto `scenario.configurationMetadata.validationResult` (replacing the old `ValidationResult.CreatePassed(0)` placeholder)
- Logs the summary line on every attempt: `[ScenarioGenerator] Validation: {checksPassed}/{checksRun} checks passed. Warnings: [{warnings joined by "; "}]`
- On validation failure, surfaces each individual warning via `Debug.LogWarning("[ScenarioGenerator] Validation warning: …")` before deciding whether to retry
- On retry-budget exhaustion: if validation never passed, returns the **last** assembled scenario with `validationResult.passed = false` and logs `Debug.LogError` summarising the situation (per brief — does not throw). The pre-existing throw on layout-only failure is preserved (last-attempt layout exceptions still propagate naturally because `catch … when (attempt < MaxLayoutRetries)` only swallows non-final attempts)
- `seedUsed` continues to record the seed that produced the (final) scenario, whether validated or not — preserves the reproducibility contract

**Decisions:**
- Folded layout retries and validation retries into a **single** unified loop rather than nesting two retry mechanisms. Rationale: the downstream stages (placement, roles, nav) thread the same `System.Random` instance; a validation failure is a property of the whole pipeline output, so the only sane "retry" is to re-run from layout with a fresh seeded RNG. Two separate loops would be more code for identical behaviour
- Validation failures do **not** throw — the brief explicitly asks for the failed scenario to be returned. This matches the data model intent (`ConfigurationMetadata.validationResult` is the canonical place for validation status; throwing would prevent persistence and post-mortem inspection). Layout exceptions still throw because there is no scenario to return
- Each in-loop validation failure logs both the per-warning lines (`Debug.LogWarning`) and the summary line (`Debug.Log`), so the Unity console shows actionable detail without duplicating the orchestrator's success summary on retries
- Final exhausted-validation log uses `Debug.LogError` (not `LogWarning`) to make the "we returned an invalid scenario" outcome visually distinct from the per-attempt retry warnings — callers who don't read `validationResult.passed` will still see a red console entry
- `MaxLayoutRetries` constant comment updated to reflect that it now governs both retry causes; the value (3) and the public API are unchanged

**Issues:**
- None — no data model, IO, or generator-stage changes required

**Next:**
- Optional follow-up: surface validation outcome through a return-type wrapper or out-parameter so callers don't need to reach into `configurationMetadata.validationResult` to check pass/fail. Deferred until a real consumer needs it
- Stage 6: `SceneBuilder.cs` — consume the generated `ScenarioData` to build the Unity scene (room prefabs, NavMesh bake, NPC instantiation, `EventManager.NotifyScenarioReady()` handoff)

---

### 2026-04-25 — Test suite: ScenarioGenerationTests MonoBehaviour

**Status:** Added a single-file MonoBehaviour test harness covering the full Module 1 pipeline. No NUnit / Unity Test Runner dependency — runs from the Inspector via `[ContextMenu("Run All Tests")]`.

**Done:**
- Created `Assets/Module1_DataModels_and_IO/Scripts/ScenarioGeneration/Tests/ScenarioGenerationTests.cs` (namespace `TeamSentinels.ScenarioGeneration.Tests`)
- 21 tests across five categories:
  - **Configuration Loading (4):** `LoadValidDefaultConfig`, `LoadMinimalConfig`, `RejectInvalidRoomCount` (min=10/max=5 ⇒ expects `ScenarioConfigValidationException`), `RejectInvalidHostageCount` (hostageCount=2 ⇒ expects rejection)
  - **Layout Generation (6):** `GenerateLinearLayout` (≤2 connections per room), `GenerateBranchingLayout` (≥1 room with 3+ connections), `GenerateHubAndSpokeLayout` (hub connects to all others), `GenerateLoopLayout` (≥2 connections per room), `VerifyAllRoomsReachable` (BFS from `room_01` for each topology), `VerifySeedReproducibility` (identical room ids/positions/connections from twin RNG)
  - **Entity Placement (5):** `VerifyEntityCounts` (1 trainee + 1 hostage + N terrorists), `VerifyNoEntityOverlap` (≥1.5m pairwise), `VerifyEntitiesWithinRoomBounds` (room half-extents minus `WallMargin`), `VerifyHostageInDeepRoom` (depth>0), `VerifyTraineeInEntryRoom` (room type = `Entry`)
  - **Role Assignment (3):** `VerifyHostageGuardianExists` (exactly one guardian), `VerifyAllTerroristsHaveRoles` (every terrorist has a `RoleAssignment`), `VerifyNavigationContextCompleteness` (every `navigationContextId` resolves)
  - **End-to-End (3):** `FullPipelineDefaultConfig` (`ValidationResult.passed = true` on `default_config.json`), `FullPipelineAllLayoutTypes` (one scenario per topology, all pass validation), `ExportAndReload` (export → reload → match `scenarioId` and entity count)
- Each test logs `[TEST] {name}: PASS` or `[TEST] {name}: FAIL — {reason}`; suite summary `[TESTS] {passed}/{total} tests passed`
- Failures are caught per-test via `RunTest(name, Action)` so one failure doesn't abort the suite
- Configs loaded from `Application.dataPath + "/Module1_DataModels_and_IO/Resources/ScenarioConfigs/{filename}"` per task brief
- Synthetic configs built via `MakeConfig(LayoutType, rooms, seed, randomness)` helper (locks min=max=rooms for predictable counts); rejection tests use a hand-rolled JSON helper to deliberately violate schema rules
- `ExportAndReload` writes to `Application.temporaryCachePath` and cleans up via `try/finally` so tests don't litter the project's Output folder

**Decisions:**
- **MonoBehaviour over NUnit:** brief explicitly asked to keep it simple and avoid extra packages. `[ContextMenu]` gives one-click execution from the Inspector header
- **Tests folder lives inside the existing `TeamSentinels.ScenarioGeneration.asmdef` scope** — no separate test asmdef needed since we're not using the Test Framework. Sub-folders inherit the parent assembly definition automatically
- **Branching test uses a fixed seed list (`{1, 7, 42, 100, 12345}`)** rather than a single seed: the topology guarantees ≥2 children at root but the depth/breadth distribution is RNG-driven, so a small search across known-good seeds is more robust than gambling on one. First seed that produces a 3+ connection room wins; if none do, the test surfaces a real regression
- **Entity placement tests reuse `default_config.json`** rather than synthetic configs — lets the tests double-check that the canonical evaluator config still round-trips through the placement pipeline
- **Pipeline tests call `new ScenarioGenerator().Generate(cfg)`** (not direct stage invocation) so we exercise the orchestrator's seeded retry loop and validator integration in addition to the underlying stages
- **Bounds check uses an epsilon of `1e-3f`** to avoid floating-point off-by-fraction-of-a-millimetre failures when the placement zone returns a position exactly on the half-extent boundary (e.g. `PlacementZone.Corner`, `NearWall`)
- **Asserts throw `Exception` with descriptive messages** rather than using `Debug.Assert` — the harness catches them in `RunTest` and surfaces the message in the FAIL line

**Issues:**
- `GenerateBranchingLayout` is the only test whose pass/fail depends on RNG draws (the others are deterministic given the seed). The seed search (5 seeds) is the mitigation; if branching ever regresses to never producing 3+ connections, the test will fail loudly instead of flaking
- `FullPipelineDefaultConfig` depends on `default_config.json` actually passing all 10 validator checks under the configured seed `20260419` (or a seed in the retry window `20260419..20260422`). Should be fine given current generator behaviour, but if the validator gains a stricter check this test is the canary

**Next:**
- Run the suite in the Editor (attach `ScenarioGenerationTests` to a GameObject in `BasicScene` or `SampleScene`, right-click → "Run All Tests") and capture any failures
- Stage 6: `SceneBuilder.cs` — consume the generated `ScenarioData` to build the Unity scene (room prefabs, NavMesh bake, NPC instantiation, `EventManager.NotifyScenarioReady()` handoff)

---
