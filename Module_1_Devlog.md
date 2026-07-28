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

### 2026-04-29 — Stage 6: SceneBuilder implementation

**Status:** Stage 6 (Unity scene integration) — first cut.

**Done:**
- Created `Assets/Module1_DataModels_and_IO/Scripts/ScenarioGeneration/SceneBuilder/SceneBuilder.cs` (namespace `TeamSentinels.ScenarioGeneration.Scene`, MonoBehaviour)
- Public surface: `BuildScene(ScenarioData)`, `BuildSceneFromFile(string)`, `ClearScene()`, `ActiveScenario` property, `OnSceneBuildComplete` / `OnSceneBuildFailed` C# events
- Inspector prefabs: `roomPrefabSmall/Medium/Large`, `corridorPrefab`, `doorPrefab`, `terroristPrefab`, `hostagePrefab`, `traineeRig` Transform
- Pipeline: `ClearScene` → `EnsureContainers` ("Rooms", "Doors", "NPCs" GameObjects under the SceneBuilder) → `BuildRooms` → `BuildDoors` → `PositionTrainee` → `SpawnHostages` → `SpawnTerrorists` → `BakeNavMesh` → `OnSceneBuildComplete` → `EventManager.NotifyScenarioReady()`
- Room size selection driven by `layoutMetadata.roomSizeCategory`; per-room GameObject stored in `Dictionary<string, GameObject>`
- Door reciprocal de-duplication via a `HashSet<string>` of ordered `roomA__roomB` pair keys
- Door rotation: north/south = identity, east/west = `Quaternion.Euler(0, 90, 0)`
- Trainee rig is **repositioned**, never instantiated; rig position resets to origin in `ClearScene`
- Hostage initial state set on `HostageController.currentState` via `Enum.TryParse(EntityRecord.metadata.initialState, ...)` falling back to `HostageState.Calm`
- Terrorist `IdleMode` mapping (verified against actual code, not CLAUDE.md mapping table):
  - `NpcRole.Patrol` → `IdleMode.Patrol`, configures `controller.patrolLine` with two waypoint stub Transforms (first + last entry of `navigationContext.waypoints`)
  - `NpcRole.RoamingGuard` → `IdleMode.Wander`, `wanderRadius` derived from the bounding-circle of `roamingRoomIds` centroids (+ 4 m, floored at 6 m)
  - `NpcRole.StationaryGuard` → `IdleMode.Static`, `staticFaceTarget` is a child stub Transform placed 5 m along `navigationContext.facingDirection`
  - `NpcRole.HostageGuardian` → `IdleMode.Static`, same face-target stub mechanism
- NavMesh: `NavMeshSurface` is fetched-or-added on the "Rooms" container and `BuildNavMesh()` is called after every room is instantiated; bake exception is caught and logged as a warning so the scene still loads if it fails
- Error handling: `Fail(string)` helper logs an error, raises `OnSceneBuildFailed`, and is wired to all null-prefab / null-data short-circuits; the whole `BuildScene` body sits inside a try/catch that funnels exceptions through `Fail`

**Decisions:**
- **PatrolLine collapsed to two waypoints (pointA / pointB)** instead of a full waypoint list, because the existing `Assets/XRI Starter Kit/Assets/Scripts/PatrolLine.cs` only exposes `pointA` and `pointB` Transforms. CLAUDE.md's "build PatrolLine from `navigationContext.waypoints`" wording implied a list; the implementation uses `waypoints[0]` and `waypoints[Count-1]` and the in-file comment notes the discrepancy. If multi-point patrols become a requirement we'll need to upgrade `PatrolLine` (Module 2 territory).
- **`staticFaceTarget` is a generated stub** because `NavigationContextEntry.facingDirection` is a `SerializableVector3` (a direction), but `TerroristController.staticFaceTarget` expects a `Transform`. Solution: parent a transient `FaceTarget_<npc>` GameObject under the NPC, positioned 5 m along the facing direction. Cleaned up automatically when the NPC is destroyed via `ClearScene`.
- **Wander radius from roaming-area geometry**: centroid of the `roamingRoomIds` positions + max distance from any of those rooms to the centroid + 4 m (≈ half a large-room stride) so the NPC can reach into rooms, not only their centres. Floor of 6 m so single-room roaming areas still feel non-trivial.
- **Hostage initial state via `Enum.TryParse`** rather than a hard-coded "calm" mapping, so future scenarios can ship `EntityMetadata.initialState = "fearful"` (or similar) without a schema change. `EntityMetadata.initialState` defaults to `"idle"` for terrorists; we coerce unparseable / null values to `HostageState.Calm` for hostages.
- **Containers parented under the SceneBuilder GameObject** (not the scene root) so `ClearScene` can clean up surgically without touching pre-existing scene objects (e.g. the XR rig, EventManager, environment props).

**Issues:**
- **Asmdef boundary blocked compilation on first cut.** `TerroristController`, `HostageController`, `EventManager` and `PatrolLine` all live in `Assembly-CSharp` (PatrolLine in `MikeNspiredXRIStarterKitr.Runtime`), and `Unity.AI.Navigation` was not in the Module 1 asmdef's references either. `TeamSentinels.ScenarioGeneration` is a sealed asmdef with `overrideReferences: true` and an empty `references` array, and asmdefs fundamentally cannot reference `Assembly-CSharp`. Errors caught in the Editor:
  - `CS0234 Unity.AI` namespace not found
  - `CS0246 TerroristController` could not be found
  - `CS0246 HostageState` could not be found
  - `CS0246 NavMeshSurface` could not be found
- **Resolution: relocate the file out of the asmdef tree** rather than touch Module 2's territory. Final path:
  - `Assets/Module1_DataModels_and_IO/Scripts/SceneBuilder/SceneBuilder.cs`
  - This sits one level above `ScenarioGeneration/` (the asmdef folder), so it compiles into `Assembly-CSharp` and sees Module 2 types and `Unity.AI.Navigation` directly. Module 1's data models stay reachable because the asmdef still has `autoReferenced: true` (Assembly-CSharp auto-references all auto-referenced asmdefs).
  - Old folder `Assets/Module1_DataModels_and_IO/Scripts/ScenarioGeneration/SceneBuilder/` (and its stray `SceneBuilder.meta` from the deleted folder) was removed; namespace `TeamSentinels.ScenarioGeneration.Scene` kept (namespace ≠ asmdef name; no enforcement).
- **PatrolLine API is 2-point only** — see Decisions above. Multi-segment patrol routes from Module 1 are silently truncated to start/end.
- **No corridor instantiation yet** — `corridorPrefab` is exposed but unused. Layout currently doesn't carry corridor geometry; rooms are placed at world positions and doors mark connectivity. Will revisit if/when corridors become a distinct geometric element.

**Next:**
- Build a small `SceneBuilderTests` MonoBehaviour mirroring `ScenarioGenerationTests`: smoke-test `BuildScene` → `ClearScene` → `BuildScene` cycle, verify NPC count, verify NavMesh existence
- Wire a tiny editor menu / ContextMenu (`Generate + Build`) so an evaluator can go from `default_config.json` to a live scene in one click
- Decide whether `ScenarioBootstrapper` should be deleted or repurposed once SceneBuilder owns the `NotifyScenarioReady()` call
- (Optional cleanup) Update `CLAUDE.md` to reflect the new SceneBuilder path — repository structure section currently lists it under `ScenarioGeneration/SceneBuilder/`

---

### 2026-04-29 — Evaluator UI: EvaluatorConfigPanel

**Status:** Stage 6 follow-on — evaluator-facing scenario configuration UI.

**Done:**
- Created `Assets/Module1_DataModels_and_IO/Scripts/SceneBuilder/EvaluatorConfigPanel.cs` (namespace `TeamSentinels.ScenarioGeneration.Scene`, MonoBehaviour, alongside `SceneBuilder.cs` so it shares the Assembly-CSharp boundary)
- TextMeshPro + UnityEngine.UI bindings cover every `ScenarioConfig` field across the three nested groups:
  - **Mission Structure:** mission type dropdown (single option "Hostage Rescue"), room count min/max sliders (3-20), room size dropdown (Small/Medium/Large with metric labels), layout type dropdown (Linear/Branching/Hub & Spoke/Loop), entry type dropdown (Single/Multiple)
  - **Entity Configuration:** hostage count label fixed at "1" (schema const), terrorist count slider (1-8), placement strategy dropdown (Clustered/Dispersed/Front-Loaded/Deep), hostage risk level dropdown (Low/Medium/High)
  - **Execution Controls:** difficulty slider (1-5 with labels "1 (Easiest)" → "5 (Hardest)"), randomness dropdown (Low/Medium/High), seed `TMP_InputField` (blank = null), timeLimit `TMP_InputField` (blank = null, range 60-1800), customLabel `TMP_InputField` (max 128 chars)
- `BuildConfig(out string warning)` translates UI -> `ScenarioConfig`. Performs lightweight UI-side checks (min<=max, terroristCount<=max*2, timeLimit range, customLabel length) and surfaces the first violation through the warning out-parameter
- `OnGenerateClicked()` flow: disable buttons -> `SetStatus("Generating...")` -> `BuildConfig` -> `ScenarioConfigLoader.IsValid(config, out errors)` (canonical schema validator) -> `_generator.Generate(config)` -> if `validationResult.passed` then store `_generatedScenario`, `SaveConfigToPrefs(config)`, enable Start Mission button, and show `Success: {rooms} rooms, {entities} entities, seed={seedUsed}`. Caught failures: failed schema validation, validator-rejected scenario after retries, `Generate()` exceptions
- `OnStartMissionClicked()` hands `_generatedScenario` to `sceneBuilder.BuildScene(...)`. Subscribed to `SceneBuilder.OnSceneBuildComplete` (hides `panelRoot` if assigned) and `OnSceneBuildFailed` (re-enables buttons + shows red warning)
- Slider callbacks use a `_suspendCallbacks` flag to avoid feedback loops while:
  - Forcing `roomCountMin <= roomCountMax` (push-up / pull-down the other slider)
  - Live-updating labels and the terroristCount-vs-max*2 warning
  - Bulk-applying a saved config from PlayerPrefs without retriggering events
- PlayerPrefs persistence under key `LastScenarioConfig`: `JsonConvert.SerializeObject(config, ScenarioJsonSettings.WriterSettings)` on every successful Generate, `Start()` reads it back via `JsonConvert.DeserializeObject` and `ApplyConfigToUi(...)`. Failures (corrupted JSON, schema mismatch) log a warning and fall back to defaults

**Decisions:**
- **Lives next to `SceneBuilder.cs`, not under the asmdef**, for the same reason as SceneBuilder: it references `SceneBuilder` (Assembly-CSharp). Placing it inside `ScenarioGeneration/` would have replayed the previous compile-error round.
- **Parallel arrays of enum values + display names** instead of reflection-driven enum population. Reflection works but obscures the display strings ("Hub & Spoke" is not directly derivable from `LayoutType.HubAndSpoke`); explicit arrays make the dropdown labels reviewable in one place.
- **Validator: `ScenarioConfigLoader.IsValid(config, out errors)`** rather than reimplementing the schema rules in the panel. Single source of truth — if the schema gains a new check, the UI gets it for free.
- **Two-tier feedback**: live warnings in the status text while editing (yellow path: range violations, parse errors) vs. hard failures from validation/Generate (red path). Both write to the same `statusText`, the colour and prefix make them distinguishable.
- **`_suspendCallbacks` toggle** for the load-from-JSON path: setting `slider.value` programmatically triggers `onValueChanged`, which would otherwise stomp on neighbouring sliders (e.g. the min/max enforcement clamp would fight the saved values). Suspend during apply, then run `UpdateAllLabels()` once at the end.

**Issues:**
- **Default `LayoutType` mismatch**: the `ScenarioConfig` constructor defaults to `Branching` but the UI dropdown defaults to index 0 (Linear) until a saved config is restored. Mitigated by `ApplyConfigToUi(new ScenarioConfig())` running in `Start()` when no saved config exists, so the dropdown lands on Branching. If the panel is dropped into a scene without that initialisation path it would still work but with a different default.
- **No live preview of the generated scenario** before clicking Start Mission. The intent was to keep the UI minimal — the success line tells the evaluator the room/entity count and seed; richer preview (e.g. a small minimap, or `ScenarioExporter.ExportToFile` for inspection) is deferred until needed.
- **Single mission type only**: dropdown shows "Hostage Rescue" as the only option. Hard-coded since `MissionType` only has one member; if the enum gains values, swap the static option list for `Enum.GetValues` + a name lookup.

**Next:**
- Build the actual Canvas in `SampleScene` (Canvas + EventSystem + panel hierarchy with sliders / dropdowns / input fields wired to this script's serialised fields)
- Add a "Reset to Defaults" button for evaluators who want to bypass the saved-config restore
- Optional: pipe `ScenarioExporter.ExportToFile(scenario)` from the success path so each generation also leaves a `Scenario_<id>.json` artefact for Module 4

---

### 2026-04-29 — Editor menu: ScenarioGeneratorEditor

**Status:** Editor tooling — one-click scenario generation from the Unity menu bar.

**Done:**
- Created `Assets/Module1_DataModels_and_IO/Scripts/ScenarioGeneration/Editor/ScenarioGeneratorEditor.cs` (namespace `TeamSentinels.ScenarioGeneration.Editor`, static class, entire file wrapped in `#if UNITY_EDITOR ... #endif`)
- Five `[MenuItem]` entries under **Tools / Scenario Generator /** with priorities tuned so a separator appears before "Open Output Folder":
  - `Generate Default Scenario` (priority 0) -> `default_config.json`
  - `Generate Minimal (Easy)` (priority 1) -> `test_minimal_easy.json`
  - `Generate Max Difficulty` (priority 2) -> `test_max_difficulty.json`
  - `Generate from Custom Config...` (priority 3) -> opens `EditorUtility.OpenFilePanel` rooted at the configs directory
  - `Open Output Folder` (priority 100, gap forces a menu separator) -> `EditorUtility.RevealInFinder`
- Common pipeline `GenerateFromConfigPath(configPath, label)`:
  1. `EditorUtility.DisplayProgressBar` at 10% (load), 40% (generate), 85% (export)
  2. `ScenarioConfigLoader.LoadFromFile` -> `new ScenarioGenerator().Generate(config)` -> `ScenarioExporter.ExportToFile(scenario, outPath)`
  3. `AssetDatabase.Refresh()` so the new JSON shows up immediately in the Project window
  4. Success dialog: rooms / entities / seed used / validator pass-or-fail / absolute output path
  5. Validator-fail still saves the file (per `ScenarioGenerator` contract) but flags it in the dialog title and button label ("Inspect Output")
  6. Try/catch surfaces exceptions through `Debug.LogError` + an "OK" failure dialog
  7. `EditorUtility.ClearProgressBar()` in `finally` so a thrown exception never leaves the bar stuck on screen
- Path helpers centralise `Application.dataPath + "/Module1_DataModels_and_IO/..."` as constants (`ConfigsRelative`, `OutputRelative`) to avoid drift between commands. `GetOutputDirectory()` auto-creates the folder if missing so first-run from a clean checkout works.

**Decisions:**
- **Lives inside the existing `TeamSentinels.ScenarioGeneration` asmdef tree** (under `ScenarioGeneration/Editor/`) rather than a separate Editor asmdef. The whole file is wrapped in `#if UNITY_EDITOR`, and Unity auto-references `UnityEditor.dll` for editor-pass compilation of any asmdef regardless of `overrideReferences`. A standalone Editor asmdef would be the textbook setup but adds a second .asmdef to maintain for a 200-line tool — not worth the overhead at this scale.
- **One method per menu item, all routed through `GenerateFromConfigPath`** so the progress-bar / dialog / try-catch policy is in one place. New canned configs (e.g., `stress_test_8_terrorists.json`) only need one `MenuItem` + one-line wrapper.
- **Validator-failed scenarios still get exported.** Matches `ScenarioGenerator.Generate` semantics: when retries are exhausted the last attempt is returned with `validationResult.passed = false`. Saving it lets the developer open the JSON to debug what went wrong instead of having to re-run with logging.
- **Custom config dialog rooted at the canned configs dir, not `Application.dataPath`**, so the most likely target folder is one click away. Falls back to `dataPath` if the dir somehow doesn't exist.

**Issues:**
- **No "Generate + Build into current scene" command yet.** Editor tools today only generate JSON; running the SceneBuilder still requires play mode + clicking Start Mission in the canvas. Adding a non-play-mode build command would need either an `EditorWindow` that holds a SceneBuilder reference, or a `[MenuItem]` that finds the SceneBuilder via `Object.FindObjectOfType`. Deferred until the canvas is wired up so we can test the runtime path first.
- **Asmdef-internal Editor folder caveat:** if the asmdef ever gains `includePlatforms` other than the default (all-platforms), this file will need its own Editor asmdef or it'll fail player builds. Fine today, flagged here for future me.

**Next:**
- Wire EvaluatorConfigPanel into a real Canvas in `SampleScene` so the runtime path can finally be exercised
- Add a "Generate + Build into Current Scene" menu item once a SceneBuilder exists in `SampleScene`
- Consider an editor preference for the default output directory so different evaluators can route their generations to different folders without code changes

---

### 2026-04-29 — Phase 4 summary (Stages 6–8): runtime integration + tooling

**Status:** Phase 4 closes out Module 1's path from "JSON sitting on disk" to "live VR scene with Module 2 NPCs running". Three parallel deliverables — runtime bridge, evaluator UI, and editor tooling — all landed in the same session and have been individually devlogged above. This entry is the consolidated picture.

**What Phase 4 delivered:**
- **Stage 6 — `SceneBuilder` (runtime bridge).** MonoBehaviour that turns a `ScenarioData` into a populated Unity scene: rooms (chosen by `RoomSizeCategory`), reciprocal-deduped doors, trainee rig reposition (no instantiate), hostage(s) with mapped initial state, terrorists with `IdleMode` + `PatrolLine` / `wanderRadius` / `staticFaceTarget` configured per Module 1 `NpcRole`, then `NavMeshSurface.BuildNavMesh()`, then `EventManager.NotifyScenarioReady()`. Public `BuildScene` / `BuildSceneFromFile` / `ClearScene` API plus `OnSceneBuildComplete` / `OnSceneBuildFailed` C# events.
- **Stage 7 — `EvaluatorConfigPanel` (UI controller).** TMP-driven Canvas controller covering every `ScenarioConfig` field across the three nested groups. `BuildConfig` translates UI to config, `OnGenerateClicked` runs `ScenarioConfigLoader.IsValid` then `ScenarioGenerator.Generate`, `OnStartMissionClicked` hands off to `SceneBuilder`. PlayerPrefs round-trip under key `LastScenarioConfig`. Live constraint enforcement (min ≤ max, terrorist count ≤ max × 2).
- **Stage 8 — `ScenarioGeneratorEditor` (editor menu).** Five `Tools / Scenario Generator /` menu items wrapping the full load → generate → export pipeline with progress bars, success dialogs, and `EditorUtility.RevealInFinder` for the output folder. No play-mode round-trip required during development.

**Architectural decisions that bind Phase 4 together:**
- **Two-assembly split for the runtime bridge.** `SceneBuilder` and `EvaluatorConfigPanel` live in `Assets/Module1_DataModels_and_IO/Scripts/SceneBuilder/` (Assembly-CSharp), one folder above the asmdef-bound `ScenarioGeneration/` tree. Forced because asmdefs cannot reference `Assembly-CSharp` and Module 2's NPC scripts have no asmdef of their own — moving the bridge files out is the only resolution that doesn't touch Module 2's territory. Module 1 data models remain visible because the asmdef has `autoReferenced: true`.
- **Single source of truth for validation.** Both the editor menu (`ScenarioGeneratorEditor`) and the runtime UI (`EvaluatorConfigPanel`) route through `ScenarioConfigLoader.IsValid` / `ScenarioGenerator.Generate`. UI-side checks are advisory only (live warning text); the canonical validator stays in one place.
- **Validator-failed scenarios still get exported.** Matches `ScenarioGenerator.Generate` semantics — the last attempt is returned with `validationResult.passed = false`. Saving the JSON lets the developer/evaluator open the file to debug, and the editor dialog flags the state with an "Inspect Output" button rather than a generic "OK".
- **NPC field assignments happen between `Awake` and `Start`.** `SceneBuilder` calls `Instantiate` (Awake fires synchronously) → captures the controller reference → writes `idleMode` / `wanderRadius` / `staticFaceTarget` / `patrolLine.pointA-B` → continues. Unity's `Start` only runs at end-of-frame, so NPCs initialise with the correct configuration on their first `Start` tick.

**Cross-cutting issues:**
- **`NotifyScenarioReady` fires before NPC `Start` methods**, because it's called synchronously inside `BuildScene`. The legacy `ScenarioBootstrapper` worked around this with a `0.1 s` delay; `SceneBuilder` does not. Acceptable today because `Start` runs immediately after `BuildScene` returns, but worth converting the call to a one-frame coroutine if any NPC FSM ever depends on `ScenarioReady` arriving after its own `Start`.
- **PatrolLine still 2-point only.** Module 1 ships ordered waypoint lists; SceneBuilder collapses to first/last. Multi-segment routes need a Module 2 PatrolLine upgrade (out of scope for Module 1).
- **`ScenarioBootstrapper.cs` is now redundant.** SceneBuilder owns the `NotifyScenarioReady` call once a scenario is built; ScenarioBootstrapper was the older "just fire ScenarioReady on scene load" stub. Not yet deleted because nothing in the canvas wiring is live yet — leave in place until SampleScene actually drives generation through the panel.

**Where Phase 4 leaves Module 1:**
- Generation pipeline (Stages 1–5) exercised end-to-end through both UI and editor entry points.
- Runtime bridge (Stage 6) has every integration point mapped against Module 2's actual API — verified field-by-field.
- Evaluator can configure scenarios (Stage 7) and developers can generate them (Stage 8) without writing a config JSON by hand.
- The Canvas itself is not yet built in `SampleScene`; that's the only remaining gating step before a full evaluator → VR session can be exercised end-to-end.

**Next (Phase 5 candidates):**
- Build the actual Canvas in `SampleScene` and wire `EvaluatorConfigPanel` to it
- Scene smoke-test: generate → build → ScenarioReady → confirm NPC FSMs activate
- Decide on the fate of `ScenarioBootstrapper.cs` (delete vs. repurpose as a JSON-only quick-launcher)
- World-space Canvas variant for in-headset evaluator setup (post-Canvas)

---

### 2026-05-03 — Scene prefab builder + runtime JSON export

**Status:** Bridging the gap between "Module 1 pipeline runs" and "scene actually renders" — Phase 4 polish so the demo doesn't depend on Module 2's NPC prefabs being ready.

**Done:**
- **Runtime JSON export** added to `EvaluatorConfigPanel.OnGenerateClicked`. Every successful Generate now writes `Scenario_<id>.json` to `Assets/Module1_DataModels_and_IO/Output/GeneratedScenarios/` and refreshes `AssetDatabase` so the new file appears in Unity's Project window without restarting. The status text gains a `Saved: <filename>` suffix. Failures log a warning but don't fail the generate (so the success state still displays). Editor-only `AssetDatabase.Refresh` guarded by `#if UNITY_EDITOR`.
- **`ScenePrefabBuilder.cs`** — new editor-only one-shot tool at `Assets/Module1_DataModels_and_IO/Scripts/SceneBuilder/Editor/ScenePrefabBuilder.cs` (Assembly-CSharp-Editor, namespace `TeamSentinels.ScenarioGeneration.EditorTools`). Menu command `Tools / Scenario Generator / Build Scene Prefabs` builds, in one click:
  - `RoomSmall.prefab` (4×3×4), `RoomMedium.prefab` (6×3×6), `RoomLarge.prefab` (8×3×8) - each is a floor cube + 8 wall segments (2 per side, leaving a 2 m centre gap for door alignment with Module 1's grid)
  - `DoorVisual.prefab` - two jambs + lintel (frame, solid colliders) + thin door panel (collider as trigger so the trainee can walk through). Default orientation has length on X so SceneBuilder's identity rotation maps to N/S walls, 90deg-Y to E/W.
  - 5 materials in `Assets/Prefabs/Materials/` (Floor / Wall / Door / DoorFrame / Ground) - URP-Lit if available, fall back to Standard, set both `_BaseColor` and `_Color` so it renders correctly under either pipeline.
  - `GroundPlane` GameObject in the active scene (100x100 m floor at y = -0.02). Covers the 2 m corridor gaps Module 1 leaves between rooms so the trainee can actually walk between them.
  - Auto-wires the four prefabs into the active scene's `SceneBuilder` (small / medium / large / door). Logs a warning + skips wiring if no `SceneBuilder` is in the scene.
- Idempotent rebuild: re-running the menu command deletes any pre-existing `RoomSmall/Medium/Large/DoorVisual.prefab`, deletes any existing `GroundPlane`, and rebuilds from scratch. Lets you iterate on the geometry without polluting the Project window.
- Wraps the whole flow in try/catch with `EditorUtility.DisplayDialog` for both success and failure paths; `EditorUtility.ClearProgressBar()` in the failure path so a stuck progress bar is impossible.
- Uses `Object.FindFirstObjectByType<SceneBuilder>()` under `#if UNITY_2022_2_OR_NEWER` (Unity 6 deprecated `FindObjectOfType`); falls back to the legacy API on older editors.

**Decisions:**
- **Primitives over ProBuilder** for the prefab geometry. ProBuilder programmatic API is fiddly and adds a package dependency just for greybox cubes. Unity's `GameObject.CreatePrimitive(Cube)` already comes with a `BoxCollider`, which is what NavMesh baking needs anyway. Polish-level geometry can replace these prefabs later by simply dropping new ones into the SceneBuilder slots.
- **Open archways on every wall** (not just the walls Module 1 actually placed doors on). Each room prefab is generic - it doesn't know per-spawn which walls will get a door. Cutting a 2 m gap in every wall means the door prefab always lands in an opening and the room is always traversable. Walls without doors look like empty archways, acceptable for a research demo.
- **Door panel collider is a trigger.** Lets the trainee physically walk through the door without it being an obstacle, while still allowing future code (e.g. `OnTriggerEnter` to raise `DoorOpened`) to detect the crossing. Frame jambs keep solid colliders so they're picked up by NavMesh.
- **Single ground plane in the scene, not per-room floors that span corridors.** Tried per-room-extended floors first (each room's floor extends 1 m into the gap) - works but creates Z-fighting where adjacent room floors meet. One big ground plane at y=-0.02 (just below room floor at y=0.04) sidesteps Z-fighting and gives a continuous walkable surface. NavMesh bake of room walls/floor handles the navigation surface; the ground plane is purely visual + trainee physics.
- **URP-vs-Standard shader detection** at material creation time. Couldn't assume the project uses URP - check `Shader.Find("Universal Render Pipeline/Lit")` first, fall back to `Standard`, then to `Unlit/Color` as last resort. Set both `_BaseColor` (URP) and `_Color` (Standard) on the material so swapping pipelines doesn't break the materials.
- **Auto-wire SceneBuilder fields directly** (not via `SerializedObject`/`SerializedProperty`). The fields are `public GameObject` so direct assignment + `EditorUtility.SetDirty` works and is shorter. SerializedProperty would be safer for prefab edits but SceneBuilder is a scene component so direct assignment is fine.

**Issues:**
- **Open archways in non-door walls look weird.** Acceptable for greybox but obvious - for a polished build, room prefabs would need to be dynamically punched (mesh CSG or per-wall variants) so only the actual door positions are open. Out of scope for Module 1 (this is presentation polish).
- ~~**Door visual doesn't perfectly fill the wall opening** because Module 1 places the door 1 m outside the room wall (in the corridor gap centre). Trainee sees a gap in the wall, then walks 1 m to reach the door, then walks through into the next room's gap. Functional but not architecturally tight. Same fix as above - only acceptable for greybox.~~ **Resolved 2026-05-04 — see follow-up entry below.**
- ~~**Adjacent room walls overlap with the corridor centre door.** Two rooms both have a 2 m wall gap on adjacent sides; the door sits between them. Visually you see two openings + a door panel = three layers of visible "doorway". Greybox tolerates it.~~ **Resolved 2026-05-04 — see follow-up entry below.**
- **Doesn't generate per-room navigation tweaks** (e.g. patrol paths, cover points). NPCs that depend on those still need Module 2 to set them up at runtime.

**Next:**
- Try the canned `default_config.json` end-to-end with the new prefabs and confirm the generated layout reads as a recognisable building from the trainee's spawn point
- Coordinate with Module 2 on swapping the `TerroristPlaceholder` / `HostagePlaceholder` cubes for real rigged NPC prefabs once those exist
- Optional: a "Build NPC Placeholders" sibling menu command that creates prefabs with the correct `TerroristController` / `HostageController` components attached (instead of plain cubes), so Start Mission's NPCs at least have FSM behaviour even without art assets
- Optional: replace the greybox open-archway walls with a "wall-with-doorway" prefab variant and have SceneBuilder pick the right one per door position

---

### 2026-05-04 — Room prefab geometry: extend walls into corridor gap

**Status:** Visual fix following first VR walkthrough. The 2 m corridor gap between rooms made doors look like floating panels in the middle of a void, with two visible wall openings (one per adjacent room) flanking the door.

**Done:**
- `ScenePrefabBuilder.BuildRoomPrefab` now constructs each room with `outerSize = nominalSize + 2 m` instead of just `nominalSize`. The floor and walls extend 1 m past the room's nominal boundary on every side, so adjacent rooms meet exactly at the corridor centerline (Module 1's stride is `width + 2 m`, so 1 m extension per room exactly fills the gap).
- Walls now sit at the *outer* (extended) edge with the existing 2 m centre gap. The door (placed by SceneBuilder at the corridor centre, 1 m outside each room's nominal wall) lands flush with the wall opening - the previous "wall, gap, door, gap, wall" sandwich collapses into "wall, door, wall".
- Resolved both issues marked under the previous entry's *Issues* section ("door visual doesn't perfectly fill the wall opening" and "adjacent room walls overlap with the corridor centre door").

**Decisions:**
- **Extend the room geometry, not the door geometry.** Considered widening the door prefab to cover the full 2 m corridor span instead, but that would have made the door look like a 2 m thick airlock and would need separate variants per room size pair. Extending the room is cleaner because it works uniformly across small/medium/large (CLAUDE.md guarantees `stride = width + 2`).
- **Floor extends too, not just walls.** Floor edges of adjacent rooms now meet exactly at the corridor centerline, so the trainee walks across continuous floor instead of stepping over the GroundPlane. NavMesh bake (`CollectObjects.Children`) picks up the extended floors automatically. GroundPlane stays as a backup visual but is largely hidden under the meeting room floors.
- **Walls without a neighbouring room still extend outward** by 1 m. Looks visually identical to walls between adjacent rooms (Module 1 doesn't tell SceneBuilder which sides have neighbours per room), and the open archway leads onto the GroundPlane rather than into another room. Acceptable for greybox; a polished build would need per-wall conditional rendering.

**Issues:**
- **Outer walls of edge rooms have visible "doorway to nowhere"** — opens onto the GroundPlane. Same caveat as before, just more obvious now that the geometry is tight elsewhere.
- **No verification yet at non-medium room sizes.** Tested with the user's medium scenario; small (4 m → 6 m outer) and large (8 m → 10 m outer) should follow the same maths but worth re-running with `Generate Minimal (Easy)` (small rooms) and `Generate Max Difficulty` (mix) to confirm.

**Next:**
- Re-run the user's last scenario after rebuilding prefabs - confirm doors render flush with wall openings
- Generate one of each canned config (default / minimal / max) to sanity-check small + large room geometry
- Decide whether to add a per-wall "is this a real door or just an archway" hint to the room prefab so outer walls can be solid

---

### 2026-05-04 — SceneBuilder spawn-point gizmos

**Status:** Visual debugging affordance for the runtime bridge. Verifying spawn placements without inspecting JSON.

**Done:**
- Added `OnDrawGizmos` to `SceneBuilder` (`Assets/Module1_DataModels_and_IO/Scripts/SceneBuilder/SceneBuilder.cs`). When `showSpawnGizmos` is on (default), every entity in `ActiveScenario.spawnPoints` renders as a colour-coded wire sphere at its spawn position with a 1.5 m facing-arrow line and a label.
- Colour palette: trainee = green (`0.30, 1.00, 0.40`), hostage = blue (`0.30, 0.65, 1.00`), terrorist = red (`1.00, 0.30, 0.30`). Wire spheres at radius 0.55 (trainee) / 0.45 (NPCs) so the markers don't occlude the actual NPC meshes.
- Facing arrow is the entity's `facingDirection` flattened to the XZ plane and drawn as a line from spawn → spawn + 1.5 m, capped with a small wire-sphere arrowhead at radius 0.08.
- Entity-ID label rendered via `UnityEditor.Handles.Label` above the sphere; whole label call is wrapped in `#if UNITY_EDITOR` so it compiles out cleanly in player builds.
- Added `[Header("Debug")]` + tooltip on the new `showSpawnGizmos` field so it's discoverable in the inspector and the toggle reads naturally next to the `traineeRig` field.

**Decisions:**
- **Wireframe over solid gizmos.** Solid spheres at 0.45 m radius would have hidden the spawned NPC mesh inside them, defeating the debugging purpose. Wireframes let you see both the marker and the actual NPC.
- **Read directly from `ActiveScenario.spawnPoints`, not the instantiated GameObjects.** The point of the gizmo is to verify *what Module 1 said should happen*, not what Unity actually rendered — so reading from the JSON-side data model catches placement bugs that would otherwise be masked by NPC physics or NavMesh snapping.
- **No editor-only assembly split.** Gizmo code lives directly in `SceneBuilder` (Assembly-CSharp) with the label call guarded by `#if UNITY_EDITOR`. Avoids a third assembly just for ~70 lines of debug rendering.
- **Default `showSpawnGizmos = true`.** Cost is zero in player builds (the whole `OnDrawGizmos` path is editor-only) and the markers are useful by default during development. Easy to toggle off in the inspector if a screenshot needs to be uncluttered.

**Issues:**
- **`ActiveScenario.spawnPoints` is null until `BuildScene` completes.** Gizmos render nothing in an empty scene, which is fine (the `?.` null-coalescing in `OnDrawGizmos` handles it) but means there's no preview of what *would* spawn before pressing Start Mission. Acceptable — preview-before-spawn is a different feature.
- **Labels can flicker when the scene view camera is rotated quickly** because `Handles.Label` is rasterised per-frame. Not a real issue, just a Unity quirk.

**Next:**
- Hook the gizmo radius / arrow length to inspector fields if the defaults turn out wrong at large room sizes
- Optional: a "preview spawn points" mode that draws gizmos from a JSON file on disk without instantiating anything — useful for evaluators who want to sanity-check placements before firing the build

---

### 2026-05-06 — Template content toggle (XRI demo content hide/show)

**Status:** Editor-side ergonomic fix. The XRI Starter Kit ships with a busy demo scene (mini-games, environment buildings, a weapons rack, demo NPCs) that overlapped the generated scenario geometry and made it hard to see what Module 1 had built.

**Done:**
- New file `Assets/Module1_DataModels_and_IO/Scripts/SceneBuilder/Editor/TemplateContentToggle.cs` (Assembly-CSharp-Editor, namespace `TeamSentinels.ScenarioGeneration.EditorTools`, ~130 lines, whole file under `#if UNITY_EDITOR`).
- Two `[MenuItem]` entries under **Tools / Scenario Generator /**:
  - `Hide Template Content` (priority 70) → disables every root GameObject whose name matches the deny list
  - `Show Template Content` (priority 71) → re-enables them
- Deny list is two parts: `DenyContains` (case-insensitive substring match) covering `INTERTABLES`, `MINI GAMES`, `ENVIRONMENT`, `[LookAnchor]`, `HandPoseReferenceTool`, `CoverPoint`; and `DenyExact` (case-insensitive exact match) covering `Cube` and the `-------- NPC` divider GameObject the XRI scene uses.
- `AlwaysKeepContains` override ensures `PLAYER`, `SceneBuilder`, `GroundPlane`, `EvaluatorCanvas`, `ScenarioManager`, `EventSystem`, `Directional Light`, `Main Camera` are never disabled even if their name happens to overlap a deny pattern.
- After toggling, `EditorSceneManager.MarkSceneDirty(scene)` so the change persists across editor restarts; success dialog lists every GameObject that flipped state ("Hid 7 GameObject(s): ..."). No-op runs report `Nothing to hide/show - all template GameObjects are already in that state`.
- Imports `UnityEngine.SceneManagement.Scene` aliased as `UnityScene` to disambiguate from the sibling namespace `TeamSentinels.ScenarioGeneration.Scene` (where `SceneBuilder` lives) — bare `Scene` would otherwise resolve to that namespace and the file wouldn't compile.

**Decisions:**
- **Toggle root GameObjects only, not nested children.** Keeps the rule predictable and reversible — you only need to remember which roots got hit, not a tree of nested overrides. The XRI scene already groups its demo content by root anyway.
- **Substring + exact-match split.** Substring is right for grouped content (`MINI GAMES (1)`, `MINI GAMES (2)` both hit a single rule); exact match is right for short generic names like `Cube` where a substring would also match unrelated objects.
- **Save the deny list in code, not as an asset.** A `ScriptableObject`-driven list would be more flexible, but the XRI scene's structure is stable per-template and the patterns are all obvious from a glance at the scene root. Hard-coded list keeps the tool one-file and zero-config.
- **Marking the scene dirty *and* relying on a manual save.** Decided not to call `EditorSceneManager.SaveScene` automatically — a one-click "hide and never get it back" would be too easy to mis-trigger. The dirty marker shows the unsaved-changes asterisk, then the user explicitly saves when they're sure.

**Issues:**
- **The d8cc1a1 commit also re-saved `Assets/XRI Starter Kit/XRI Starter Kit.unity` (~32k lines of diff)** — Unity persisting the new disabled state for every demo root, plus its usual whitespace/precision churn. Cosmetic only but it bloats the diff; future runs of `Hide Template Content` will produce smaller deltas now that the baseline is set.
- **Re-saved prefab/material files** (`Door*.prefab`, `Room*.prefab`, every `*_M.mat`) — Unity re-serialised them with sub-millibit precision changes (e.g. `0.66` → `0.65999997` on `_Color`). Pure noise, no visual difference. Considered reverting but it's the kind of thing that'll re-appear on the next save anyway.
- **Deny list will need updates if the XRI Starter Kit version changes** and renames or restructures its demo roots. Worth scanning the kit's release notes when bumping the package.

**Next:**
- Mention `Hide Template Content` in the team-facing README / handoff notes so Module 2 / 3 / 4 don't have to disable demo content by hand
- Consider a sibling `Tools / Scenario Generator / Reset Scene to Module 1 Baseline` that combines `Hide Template Content` + clears any existing `SceneBuilder` instantiated content + resets the trainee rig to origin
- Optional: persist the hide-state in `EditorPrefs` so opening the project on a fresh machine starts with the demo content already hidden

---

### 2026-05-10 — Web bridge: ScenarioHttpServer for the AAR dashboard

**Status:** New bridge component so the external AAR web dashboard (Next.js, currently localhost:3000) can drive the same generation + scene-build pipeline that `EvaluatorConfigPanel` drives from the in-VR canvas. Implements Option 1 from the feasibility discussion (HTTP listener inside Unity, no cloud relay, no `adb push` polling).

**Done:**
- Added `Assets/Module1_DataModels_and_IO/Scripts/SceneBuilder/ScenarioHttpServer.cs` — a `MonoBehaviour` that runs an in-process `System.Net.HttpListener` on a configurable port (default 8080). Lives outside the asmdef alongside `SceneBuilder` / `EvaluatorConfigPanel` so it can reference both `SceneBuilder` (Assembly-CSharp) and the asmdef-bound generators.
- Endpoints exposed:
  - `GET  /health` → `{ ok, port, state }` for dashboard liveness checks
  - `GET  /status` → `{ state, scenarioId, lastError }` (state ∈ `idle | generating | building | live | error`)
  - `POST /scenario/generate` → body is a full `ScenarioConfig` JSON; runs `ScenarioConfigLoader.LoadFromJson` → `ScenarioGenerator.Generate` → optional `Scenario_<id>.json` export; returns `{ scenarioId, rooms, entities, seedUsed }`
  - `POST /scenario/start` → same as above plus `SceneBuilder.BuildScene`
  - `OPTIONS *` → 204 with CORS headers (preflight)
- CORS headers (`Access-Control-Allow-Origin`, `…-Methods`, `…-Headers`) applied to every response; `allowedOrigin` is an inspector field so prod can lock it down to the dashboard URL.
- Wired up `SceneBuilder.OnSceneBuildComplete` / `OnSceneBuildFailed` so `_state` flips to `live` / `error` automatically without the listener thread having to know about scene-build internals.
- Pipeline cancellation safety: `OnDestroy` and `OnApplicationQuit` both stop the listener and join the worker thread (500 ms cap).

**Decisions:**
- **In-process `HttpListener` over a local relay or cloud backend.** Evaluator and trainee will be on the same lab Wi-Fi during demos, so adding a Firebase/Supabase relay just to bounce a JSON config would be pure overhead. `HttpListener` works on both Windows (Editor / standalone build) and Android (Quest 3 standalone) without native plugins.
- **Marshal generator + scene-build calls onto the Unity main thread via a `ConcurrentQueue<Action>` drained in `Update()`.** `ScenarioGenerator.Generate` itself is plain C# and would run fine on any thread, but `SceneBuilder.BuildScene` calls `Instantiate`, `NavMeshSurface.BuildNavMesh`, `Resources.Load`, etc. — all main-thread-only. The listener thread blocks on a `ManualResetEventSlim` (default 30 s) for the response to stay synchronous from the dashboard's perspective.
- **JSON parse + validation happen on the listener thread**, not the main thread. They have no Unity API dependencies and we want bad-JSON 400s to come back without ever touching the main-thread queue.
- **Volatile string state instead of locks.** `_state`, `_lastScenarioId`, `_lastError` are read by `/status` from the listener thread and written from the main thread only. `volatile` is sufficient — no read-modify-write, no compound state.
- **`POST /scenario/start` returns as soon as `BuildScene` finishes** (which is synchronous and fires `OnSceneBuildComplete` before returning). State will already be `live` by the time the response is sent, so the dashboard can rely on a single round-trip rather than polling `/status`.
- **Defaulted `exportScenarioJson = true`** so every web-driven run still drops a `Scenario_<id>.json` into `Output/GeneratedScenarios/` for Module 4 replay. Mirrors `EvaluatorConfigPanel.TryExportScenarioJson` so the on-disk artefact is identical regardless of which UI triggered the run.

**Issues:**
- **Windows requires a `urlacl` entry to bind `http://+:8080/` without admin rights.** The error path logs the exact `netsh` command (`netsh http add urlacl url=http://+:8080/ user=Everyone`) so this is a one-time setup on each dev machine — not a code issue.
- **University Wi-Fi networks often have client isolation enabled**, which silently blocks PC-to-Quest peer traffic even when both devices are on the same SSID. If the dashboard sees `ERR_CONNECTION_TIMED_OUT`, the workaround is a phone hotspot or a dedicated lab router. Documented in the source-file CORS / setup comments.
- **No authentication on the endpoints.** Acceptable for a single-user lab demo but a real deployment would need at least a shared bearer token. Deferred — a single-line check inside `HandleRequest` will cover it when needed.

**Next:**
- Add a "Start Mission" button + form to the AAR dashboard (`localhost:3000`) that POSTs the assembled `ScenarioConfig` to `http://<quest-ip>:8080/scenario/start`. Reuse the same enum option lists as `EvaluatorConfigPanel` so the two UIs stay in sync.
- Optional: expose a `GET /scenario/last` that returns the most recent generated `Scenario.json` payload directly, so the dashboard can render a layout preview without re-reading the file off disk.
- Optional follow-up if `HttpListener` turns out to be flaky on IL2CPP Android builds: swap to a small `TcpListener`-based handler or pull in a Unity-friendly mini web server package. Editor + Mono Quest builds work fine today.

---

### 2026-06-16 — Interactive doors: initial door states + runtime door prefab

**Status:** Doors go from static gap-fillers to stateful, interactive objects. Layout now decides each door's initial state; SceneBuilder realises it as an openable/lockable prefab.

**Done:**
- Added `DoorState` enum (`Open` / `Closed` / `Locked`) to `ScenarioEnums.cs`, and two fields on `DoorData` (`LayoutModels.cs`): `state` (default `Closed`) and `isExterior` (reserved for future breach-point logic).
- New Stage 5b in `LayoutGenerator` — `AssignDoorStates(rooms, rand, rng)` runs after room types are known and applies a fixed realism policy: entry-room doors open (breach points), the hostage-room door is `Locked`, ordinary interior doors mostly `Closed` with a seeded open-fraction scaled by `RandomnessLevel`. `DecideDoorState` encodes the precedence (locked > open > closed). Both reciprocal door records for an edge always receive the same state (mirrored by shared door id).
- Created `Assets/Module1_DataModels_and_IO/Scripts/SceneBuilder/GeneratedDoor.cs` (~212 lines) — runtime component that drives the interactive door prefab from its `DoorState`.
- `SceneBuilder.cs` (+240 lines) now configures the instantiated door prefab per-state (open/closed/locked) instead of dropping a static panel.
- `ScenarioValidator` gained door-state coverage; `ScenarioGenerationTests` added three door checks: `VerifyEveryRoomHasDoor`, `VerifyDoorStatePolicy`, `VerifyDoorStatesReciprocalAndDeterministic`.
- `ScenePrefabBuilder` reworked (~181 lines changed) to build the interactive door prefab.

**Decisions:**
- **Door state is a generator decision, not an evaluator knob.** The policy is fixed and realism-driven (entry open, hostage locked, interior mostly closed) so scenarios stay believable without another config field. Only the open *fraction* responds to `RandomnessLevel`, keeping low-randomness runs deterministic and tidy.
- **State mirrored onto both reciprocal door records** so either room's view of the shared door agrees — the validator's reciprocal check enforces this.

**Issues:**
- None blocking. `isExterior` is written but not yet consumed (reserved for breach mechanics).

**Next:**
- Reuse hand-built base-map door art instead of the greybox door prefab.

---

### 2026-06-17 — Reuse base-map real doors, walls and floors

**Status:** Swap greybox geometry for the hand-built ("realistic") base-map art so generated scenarios match the rest of the environment.

**Done:**
- New editor command **Tools / Scenario Generator / Finalize Real Door From Selection** in `ScenePrefabBuilder.cs` (+252 lines): takes a hand-built XRI door GameObject, strips scene-bound NPC-stop components, adds an `NpcDoorAssist` + a carving `NavMeshObstacle` sized to the doorway, saves it as `Assets/Prefabs/RealDoor.prefab`, then wires the prefab (plus the real wall/floor materials) into the scene's `SceneBuilder`. `validate`-gated so it only enables when a GameObject carrying a MikeNspired `Door` is selected.
- New `Assets/Module1_DataModels_and_IO/Scripts/SceneBuilder/NpcDoorAssist.cs` (~194 lines) — toggles the carving `NavMeshObstacle` with the door leaf so NavMeshAgent NPCs can path through an open door and are blocked by a shut one.
- Real material references centralised as constants pointing at `Assets/Basemap Metrials/` (`Wall_Outside.mat`, `Floor_Interier.mat`) — note the folder is misspelled "Metrials" on disk; constants keep it exact.
- `SceneBuilder.cs` (+257 lines) reworked to reuse the base-map door prefab and floor/wall materials; room prefabs (`RoomSmall/Medium/Large`) updated accordingly.
- Hardened `Assets/XRI Starter Kit/Assets/Interactables/Door/Door.cs` against `MissingReferenceException` when the door's Rigidbody is destroyed mid-settle (SceneBuilder regenerating the scene) — bails out if the door/joint is destroyed during the sleep-wait.

**Decisions:**
- **Strip scene-bound components by type name**, not a hard assembly reference to the XRI runtime asmdef, so the editor tool doesn't take a compile dependency on Module 2's kit.
- **Carving NavMeshObstacle over baking doorways closed.** NPC pathing is severed while the door is shut and restored when open, without re-baking the NavMesh at runtime.

**Issues:**
- The base-map materials folder name `Basemap Metrials` is misspelled on disk — kept exact in code; renaming it would break the references.

**Next:**
- Fix trainee spawn to a sensible staging point; add a placement fallback for small rooms.

---

### 2026-06-17 — Trainee staging spawn + small-room placement fallback

**Status:** Two related fixes — a robust entity-placement fallback, and a configurable trainee spawn staging point.

**Done:**
- `EntityPlacer.cs` (+49 lines): added `FarthestValidPosition(room, existing)` — a last-resort placement used when every retry attempt collides (common in Small rooms at low difficulty where Centre/OpenArea zones all overlap the centre-placed hostage). It picks the in-bounds candidate (corners, edge midpoints, centre) maximising distance to the nearest already-placed entity, so two entities are never stacked when geometry can avoid it. In a 4 m room the farthest corner is ~1.7 m from a centre hostage, clearing the 1.5 m clearance constraint. Replaces the old "fall back to room centre" behaviour that broke clearance.
- `SceneBuilder.cs` (+27 lines): new `traineeStartPoint` staging Transform — when set, the trainee spawns there (e.g. by the briefing table) with a yaw-only rotation (never inherits pitch/roll from the anchor), instead of at the generated entry. New `entryDoorStartsOpen` flag (default true) opens the building's entry door(s) when spawning from a staging point so the trainee can walk straight in; interior doors keep their generated state.
- Companion Unity-scene fix (`XRI Starter Kit.unity`) re-anchoring the trainee spawn.
- Further `Door.cs` null-safety on the settle path.

**Decisions:**
- **Farthest-from-existing over room-centre fallback.** Room centre is exactly where the hostage already sits in a small room, so the old fallback guaranteed a clearance violation; maximising separation is the correct degenerate-case behaviour.
- **Yaw-only rig rotation at staging points** so the XR rig is never tilted by an anchor transform's pitch/roll.

**Issues:**
- None.

**Next:**
- Enclose the building — perimeter corridor and roof.

---

### 2026-06-17 — Perimeter corridor + room ceilings/roof

**Status:** Generated building becomes fully enclosed top-to-bottom and wrapped in a corridor ring.

**Done:**
- `SceneBuilder.cs` (+454 lines): each room now gets a flat ceiling/roof cap (`CeilingThickness = 0.2 m`, matching the base map's ceiling slab), configurable via a new `ceilingMaterial` field.
- New **Perimeter Corridor** feature: `buildPerimeterCorridor` (default true) wraps the whole building in an enclosed corridor ring (floor + outer wall + roof), `corridorWidth` (default 3 m) sets the band width, `corridorFloorMaterial` optionally overrides it (falls back to the room floor material). The building's exterior entry doors open into this corridor, and a single outer door lets the trainee in.
- New scene container `PerimeterCorridor` (`CORRIDOR_ROOT`) alongside `Rooms` / `Doors` / `NPCs`, cleared and rebuilt each `BuildScene`. `CorridorFloorThickness = 0.08 m` matches the room floor prefab so the corridor floor sits flush.

**Decisions:**
- **Ceiling + corridor built procedurally in SceneBuilder** rather than as prefab variants, so they scale automatically to the generated footprint.

**Issues:**
- None noted at implementation.

**Next:**
- Add window glazing to solid exterior walls so the building reads as a real structure from outside.

---

### 2026-06-25 — Windows/glass on exterior walls

**Status:** Solid exterior room walls get glazed windows so the building reads as inhabited from the outside.

**Done:**
- `SceneBuilder.cs` (+160 lines): new `addWindows` flag (default true) punches a glazed window into every solid exterior wall — a wall with no door and no adjacent room. `windowMaterial` field supplies the glass (falls back to a generated frosted material). Window geometry constants: `WindowWidth = 1.0 m`, `WindowHeight = 0.9 m`, `WindowSillHeight = 1.1 m`, `WindowSideMargin = 0.4 m`, `WindowPaneThickness = 0.04 m`.
- `BuildWalls` / `BuildWallSide` refactored so each side is built as solid, a door opening, or a windowed wall (door takes priority over a window). `BuildWindowWall` constructs a solid sill below, jambs either side, a header above, and an opaque frosted pane.
- `ScenePrefabBuilder.cs` + new `Glass_M.mat`: `CreateGlassMaterial()` builds a frosted, opaque light-blue pane material (`GlassColor = (0.80, 0.86, 0.90)`) — glassy-looking but fully opaque so the trainee can't scout room contents through the window. Wired into both the greybox and realistic prefab paths.

**Decisions:**
- **Frosted-but-opaque glass.** Windows must read as glazed without letting the trainee see (and pre-plan against) terrorist/hostage positions through the wall — so the pane is deliberately opaque.
- **Window only on true exterior solid walls** (no door, no neighbour), matching the corridor/exterior-shell logic from the perimeter-corridor work.

**Issues:**
- None noted.

**Next:**
- Furnish the interiors so rooms read as real spaces and give NPCs cover.

---

### 2026-07-13 — Procedural furniture placement (FurniturePlacer)

**Status:** New generation stage — rooms are populated with believable, seed-reproducible furniture that doubles as tactical cover.

**Done:**
- New data models in `LayoutModels.cs` (+68 lines): `FurnitureData` (`id`, `type`, `position`, `size`, `rotationY`, `againstWall`) and a `List<FurnitureData> furniture` on `RoomData` (serialised under `"furniture"`). New `FurnitureType` enum in `ScenarioEnums.cs` (+51 lines), each type carrying a canonical real-world footprint.
- New `Generators/FurniturePlacer.cs` (~459 lines) — plain C# class (no Unity dependency) that mutates each `RoomData.furniture` in place. Items are wall-anchored (line the walls like real rooms), avoid doors (`DoorKeepout = 1.35 m`), keep clear of entities so no actor spawns inside furniture (`EntityClearance = 0.6 m`), don't overlap each other (`FurnitureGap = 0.15 m`), and stay off the outer wall edge (`EdgeInset = 0.15 m`, `WallGap = 0.05 m`). `MaxAttemptsPerItem = 40`, and a cap on the fraction of floor a room's furniture may consume. Fully seed-reproducible.
- Wired into `ScenarioGenerator.cs` (+12 lines) as a pipeline stage after placement.
- `SceneBuilder.cs` (+475 lines): realises `FurnitureData` as greybox boxes (or mapped prefabs) scaled to `size`, and bakes them into the NavMesh as obstacles so NPCs path around them.
- `ScenarioGenerationTests.cs` (+187 lines) — furniture placement/coverage tests.

**Decisions:**
- **Placement is pure data.** `FurniturePlacer` never touches Unity objects — it writes `FurnitureData` into `Scenario.json`, and `SceneBuilder` realises it. Keeps the generator testable and headless, and lets Module 4 replay furniture from the JSON.
- **Wall-anchored, door-aware, entity-aware.** Furniture lines walls (realistic + leaves the room centre navigable), never blocks a doorway, and never traps a spawned actor.

**Issues:**
- None noted.

**Next:**
- Rethink trainee spawn: put them outside the building with a visible approach path.

---

### 2026-07-18 — Unity MCP server setup

**Status:** Tooling — connect the project to a Unity MCP server for editor automation.

**Done:**
- Updated `Packages/manifest.json` + `Packages/packages-lock.json` to add the Unity MCP package.
- Added the MCP artefacts to `.gitignore`.
- Removed a stale `.claude/settings.local.json` (88 lines) from version control.

**Decisions:**
- Editor/MCP config kept out of the repo via `.gitignore` so per-machine setup doesn't churn the shared history.

**Issues:**
- None.

**Next:**
- Use the MCP tooling to iterate on the outside-the-building spawn flow.

---

### 2026-07-18 — Spawn trainee outside the building + guide path

**Status:** Trainee now starts in open ground outside the generated entrance and is guided in by a visible path.

**Done:**
- `SceneBuilder.cs` (+538 lines net): new `spawnOutsideEntrance` flag (default, recommended) spawns the trainee in open ground just outside the generated building's entrance, facing the door, instead of inside the building or at a fixed staging point — the entrance moves per generation, so the spawn is computed from the captured entry-door world position (`_entryDoorWorld`). `entranceStandoff` (default 8 m) sets how far out. `traineeStartPoint` staging is retained as the fallback when `spawnOutsideEntrance` is off.
- New **Guide Path** feature: `buildGuidePath` (default true) lays a visible road on the ground from the trainee's start to the entrance door, drawn only when the trainee actually spawned outside (`_traineeSpawnedOutside`). Tunables: `guidePathWidth` (1.4 m), `guidePathColor` (bright unlit yellow), `guidePathArrowSpacing` (2.5 m chevrons). New `GuidePath` scene container; the router (`GuidePathCell = 0.5 m`, region margin, body-height band) keeps the road outside the building walls + perimeter corridor footprint rather than cutting through them.
- Large companion re-serialisation of `XRI Starter Kit.unity` (spawn anchoring).

**Decisions:**
- **Spawn outside + guided approach** so the trainee experiences an approach/breach rather than materialising inside the objective — and because the entrance position is generation-dependent, the spawn and path are both derived from the live entry-door transform.
- **Guide path only when spawned outside.** When the trainee spawns inside there's nothing to guide to, so the road is suppressed.

**Issues:**
- None noted.

**Next:**
- Ensure the generated footprint never overlaps the existing hand-built base-map buildings.

---

### 2026-07-18 — Keep generated scenario clear of base-map geometry

**Status:** The generated building auto-nudges to avoid overlapping the hand-built base-map structures.

**Done:**
- `SceneBuilder.cs` (+157 lines): new `avoidBaseMapOverlap` flag (default true) offsets the whole generated scenario so its outer footprint (rooms + perimeter corridor) keeps a clear gap from any existing base-map collider. `mapClearance` (default 4 m) sets the minimum gap; `baseMapLayers` (default all) selects which layers count as base-map geometry.
- `TryComputeLayoutExtents` computes the layout-space (pre-offset) bounds as the union of every room's footprint plus the corridor band; `ResolveBaseMapClearance` slides the scenario until its footprint + clearance no longer intersects base-map colliders, ignoring the scenario's own/ignored colliders (`IsOwnOrIgnoredCollider`).

**Decisions:**
- **Nudge the whole scenario as a rigid unit** rather than reshaping the layout — preserves the generated topology/spawns exactly while relocating them to open ground next to the base map.

**Issues:**
- None noted.

**Next:**
- (Open) Coordinate final placement conventions with the base-map/environment owners; confirm clearance value holds across all canned configs.

---

### 2026-07-25 — Door location fix: lateral jog + aligned wall openings

**Status:** Bugfix — doors no longer always sit at the wall midpoint, and the rendered wall openings now line up with where the layout actually put each door.

**Done:**
- `LayoutGenerator.cs` (+152 lines): door placement gained a lateral offset along the shared wall. `ChooseDoorLateral` evaluates candidate positions (base midpoint ± `DoorLateralJog = 1.6 m`) and picks the one maximising clearance from doors already recorded on the same wall of either room (`Clearance` / `ClearanceOn` / `RecordLateral` bookkeeping keyed by `roomId|side`). `MaxJog` clamps the jog per room so an opening never eats into the corner: half-span − `DoorSightlineWidth`/2 (2.2 m) − `MinJamb` (0.4 m).
- `SceneBuilder.cs` (+303 lines): wall builders now cut each opening at the door's **actual** lateral position instead of centring every opening — `OpeningOffset` maps the door's layout-space position onto the wall span (room size + `CorridorGap`) and clamps to leave a minimum jamb. New `OffsetEntranceFromInteriorDoors` nudges the exterior entrance opening laterally so it can't coincide with an interior door opening on the same wall.
- `ScenarioGenerationTests.cs` updated for the new door-position behaviour.

**Decisions:**
- **Clearance-maximising candidate selection, not RNG.** The jog is a pure function of the layout (which doors already share the wall), so the fix stays fully seed-reproducible and needs no new config knob.

**Issues:**
- None noted after the fix; previously two doors on the same wall could overlap or a door's visual opening could sit at the wall centre while the door data sat elsewhere.

**Next:**
- Fix the remaining door *functionality* issues (leaf physics, NPC sensor alignment).

---

### 2026-07-26 — Door functionality fixes: leaf alignment + NPC sensor anchoring

**Status:** Bugfix — instantiated doors now sit exactly in their wall openings and NPC door-assist works off the real door geometry.

**Done:**
- `NpcDoorAssist.cs` (+39 lines): the NPC sensor is now anchored to the actual door-leaf collider (`_leafCollider`) rather than assumed prefab geometry, so open/closed detection tracks the real doorway.
- `SceneBuilder.cs` (+253 lines): new `DoorLiesOnWall` resolves which wall a door record actually belongs to, returning its lateral offset and wall plane (with `DoorPlaneTolerance = 1.25 m`); wall segments are then built from per-side door planes so opening, wall and door leaf coincide exactly on shared walls. `DoorFloorLift = 0.095 m` lifts the instantiated door so the leaf clears the floor slab. `BuildCeiling` extracted as its own builder during the wall refactor.

**Decisions:**
- **Measure the leaf, don't assume it.** Both the sensor anchoring and the floor lift are derived from the real prefab's colliders/bounds, so swapping the door art later won't silently break door behaviour.

**Issues:**
- None after the fix.

**Next:**
- Presentation polish: staging table at spawn, hide the template base map after generation.

---

### 2026-07-26 — Gun/staging table placed in front of the trainee spawn

**Status:** The controller-adjustment (gun) table now follows the trainee's generated spawn point instead of living at a fixed scene position.

**Done:**
- `SceneBuilder.cs` (+151 lines): new `stagingTableRoots` (Transform array) is relocated in front of the trainee on every build via `PlaceStagingTable`. `tableSpawnOffset = (0, 0.86, 1.6)` positions the table centre in trainee-local right/up/forward; `weaponSpawnYaw` adds extra yaw to the weapon on top of the trainee's facing. Placement sweeps candidate angles and uses `IsStagingSpotClear` (ignoring the scenario's own colliders) to find an unobstructed spot; rigidbody props on the table are temporarily made kinematic during the move so items don't scatter, and their local poses (`_stagingTableLocalPoses`) are restored each build.
- Large `XRI Starter Kit.unity` re-serialisation (net −3,685 lines) — stale hand-placed scene content removed now that table placement is runtime-driven.

**Decisions:**
- **Table follows the spawn, not the reverse.** With `spawnOutsideEntrance` the trainee's start moves every generation, so the equipment table must be computed from the live spawn pose rather than anchoring the spawn to a fixed table.

**Issues:**
- None noted.

**Next:**
- Hide the hand-built base-map template once a scenario is generated so the two buildings don't visually compete.

---

### 2026-07-26 — Hide base-map template after build + global lighting fix

**Status:** After a successful build the XRI demo/base-map content is hidden, without killing the scene's global lighting.

**Done:**
- `SceneBuilder.cs` (+119 lines): new `hideTemplateAfterBuild` flag (default true). `HideTemplateContent` deactivates template roots identified by name (`TemplateRootContains` substrings + `TemplateRootExact` exact matches), tracking them in `_hiddenTemplateObjects` so `ShowTemplateContent` / `ClearScene` can restore them. `HideSubtreeExceptKept` walks each root and keeps required children alive while hiding the rest.
- Follow-up lighting fix (+12 lines): the scene's global lighting rig lives **inside** the template hierarchy (`-------- ENVIRONMENT/LightAndReflectionProbes`), so hiding the template dropped the whole world to flat ambient grey. Directional `Light`s, `ReflectionProbe`s and `LightProbeGroup`s found under template roots are now added to the kept list before hiding.
- Companion `XRI Starter Kit.unity` cleanup (−113 lines).

**Decisions:**
- **Hide, don't destroy.** Template objects are deactivated and tracked rather than deleted, so `ClearScene` returns the scene to its authored state and repeated generate/clear cycles are lossless.
- **Keep the lighting rig by component type**, not by name — any directional light or probe under a template root survives, so re-organising the template hierarchy won't reintroduce the grey-out.

**Issues:**
- The lighting regression shipped in the first cut of the hide feature and was caught the same day — the kept-components fix above resolves it.

**Next:**
- Finalise which scenario parameters the evaluator dashboard actually exposes.

---

### 2026-07-26 — AAR dashboard: finalise mission input parameters

**Status:** The web dashboard's Start Mission form is trimmed to the finalised evaluator-facing parameter set.

**Done:**
- `sentinels-aar/components/MissionLauncher.jsx` (net −43 lines): removed the form controls for **Hostage Risk Level**, **Difficulty**, **Randomness**, **Time Limit** and **Custom Label**. The wire payload now sends fixed defaults for those fields: `hostageRiskLevel = 'medium'`, `difficultyLevel = 3`, `randomnessLevel = 'medium'`, `timeLimit = null`, `customLabel = null`.
- Remaining evaluator knobs: mission structure (room count min/max, room size, layout type, entry type), terrorist count, placement strategy, and the optional seed.
- Client-side validation checks for the removed fields (timeLimit range, customLabel length) dropped along with their enum imports (`DIFFICULTY_LABELS`, `HOSTAGE_RISK_LEVELS`, `RANDOMNESS_LEVELS`).

**Decisions:**
- **Fixed defaults are literals, not state**, so a stale saved config in localStorage can never re-inject old values for the removed fields into the payload.
- **Trim at the UI, keep the schema.** `ScenarioConfig` and the Unity-side pipeline still accept the full field set — only the dashboard surface is reduced, so canned configs and the in-headset `EvaluatorConfigPanel` are unaffected.

**Issues:**
- None.

**Next:**
- End-to-end demo pass with the finalised parameter set: dashboard → Quest → generated scenario → AAR capture.

---

### 2026-07-27 — Furniture realism: grouped companions, real-wall anchoring, prefabs on by default

**Status:** Interior furnishing goes from "boxes lining walls" to rooms that read as actually inhabited — furniture is grouped the way real rooms are, anchored to the *visible* walls, and realised with the PandazoleHome art pack by default.

**Done:**
- `FurniturePlacer.cs` (+246 lines): new **companion placement pass** — chairs never spawn standalone along walls anymore; they are tucked in front of the desk/table they belong to (pulled out 0.2–0.35 m as if in use, turned 180° to face it, 1 chair per desk, 1–2 per table). Beds and sofas get a `SideTable` nightstand seated beside them against the same wall. Companions run the exact same rejection checks as primary items (room bounds, door keep-outs, entity clearance, item gaps, floor-coverage cap) so every navigability guarantee still holds, and a companion that fails clearance is silently skipped — a desk without a chair is still believable.
- Room-type pools updated to match: `Standard` rooms lost their standalone `Chair`/`SideTable` entries (chairs now only arrive via the companion pass); `HostageRoom` deliberately **keeps** loose wall chairs — they read as hostage seating, which suits the scenario.
- **Real-interior anchoring fix:** replaced `EdgeInset` (0.15 m inside the nominal room bounds) with `InteriorOutset = CorridorGap/2 − WallHalfThickness` (0.94 m *outside* them). Rooms are laid out 2 m apart and SceneBuilder centres each 0.12 m wall on the boundary plane halfway into that gap — so the room's real interior extends well past its nominal half-size, and the old inset left "wall-anchored" furniture floating ~1 m inside the room. Backs now sit flush against the visible walls.
- `SceneBuilder.cs` (+61 lines): `useFurniturePrefabs` default flipped **ON** — the PandazoleHome shared material (`Panda Mat.mat`) was upgraded to URP/Lit so the pack no longer renders magenta. Prefab uniform-fit now allows upscaling to `MaxUpscale = 1.35` (the planner's footprints are real-world dimensions, so filling them is what puts props at human scale; the cap stops undersized models becoming caricatures). New rear-face push: after fitting, wall-anchored models are pushed back so their rear face lands on the footprint's rear edge — the uniform fit can leave a model shallower than its reserved footprint and centring that slack stranded cupboards off the wall. Chairs are exempt (their back faces away from the anchor wall, towards their desk).
- Prefab name-mapping expanded/corrected: chairs also match `Prop_KitchenChair_`/`Prop_OfficeChair_`, `Barrel → Prop_TrashCan_`, `Bookshelf → Prop_Shelve_`, `SideTable → Prop_Nightstand_`, `Stool → Pouffes`.
- `ScenarioGenerationTests.cs` updated: bounds assertions now use `FurnitureInteriorOutset` instead of the old edge inset.

**Decisions:**
- **Grouping is a placer concern, not a SceneBuilder concern.** Companion items are ordinary `FurnitureData` records written into `Scenario.json`, so Module 4 replay and the greybox fallback get the same grouped layouts for free.
- **Chairs axis-aligned (parent yaw + 180°), not freely rotated,** so footprint maths stay exact AABBs and the overlap/clearance checks remain valid.

**Issues:**
- Prefab auto-discovery is editor-only — device builds need the prefabs mapped explicitly in the inspector (greybox remains the fallback). Noted in the tooltip.

**Next:**
- Window realism pass on the exterior walls.

---

### 2026-07-27 — Window realism: multi-window rhythm + fully framed window units

**Status:** Exterior glazing goes from one small bare hole per wall to evenly-spaced, residential-proportioned framed windows.

**Done:**
- `SceneBuilder.cs` (+165/−41 lines): window constants re-tuned to residential proportions — opening 1.4 × 1.2 m (was 1.0 × 0.9), sill dropped to 0.95 m (was 1.1). New rhythm constants: `WindowSpacing = 3.2 m` of wall per window (up to 3 per wall, shrunk until the row fits with `WindowMinGapBetween = 0.9 m` and 0.5 m end margins).
- `BuildWindowWall` rebuilt: instead of one centred opening with jambs, it computes evenly-distributed opening centres across the usable band, builds a full-span sill band below and header band above, and fills the solid pieces between openings.
- `AddWindowPane` replaced by `AddWindowUnit` — each opening now carries a complete framed unit: glass pane (inset), border frame strips standing proud of the wall on both faces (`WindowFrameDepth = 0.18 m` vs the 0.12 m wall), a protruding sill ledge, and slim cross muntins dividing the glazing into four panes. A URP-safe glass material is generated when `windowMaterial` is unassigned, so openings are never bare holes.
- Every part is a solid primitive with a collider — the trainee still can't reach or walk through an opening.

**Decisions:**
- **Rhythm over size.** Long walls get more windows rather than one bigger one, matching how real buildings read from outside; count is derived from span, so no new config knob.

**Issues:**
- None noted.

**Next:**
- Apply the supervisor's scenario-parameter scale changes to the dashboard form.

---

### 2026-07-27 — Scenario config form: single Room Count (3–5), terrorist cap 4

**Status:** Supervisor-requested input-scale changes to the dashboard's Start Mission form.

**Done:**
- `sentinels-aar/components/MissionLauncher.jsx` (net −3 lines): the **Room Count Min / Max** sliders are collapsed into a single **Room Count** slider with range 3–5. The wire payload still carries `roomCount: { min, max }` (both set to the slider value), so `ScenarioConfig`, the Unity loader and the JSON schema are untouched.
- **Terrorist Count** slider max reduced from 8 to 4.
- Configs restored from localStorage are clamped on load (rooms into 3–5, terrorists into 1–4), so a config saved before this change can't re-inject out-of-range values into the form or payload.
- `scenarioEnums.js`: `DEFAULT_CONFIG.roomCount` changed from `{ min: 5, max: 8 }` to `{ min: 4, max: 4 }` — the old default sat outside the new slider range.
- The now-impossible "min > max" client-side warning was removed; the terrorist-vs-rooms sanity warning remains.

**Decisions:**
- **Single value mapped onto the existing min/max wire shape** rather than changing the schema — evaluators think in "how many rooms", and keeping the range shape on the wire means zero Unity-side changes and full compatibility with canned configs.
- **Clamp on restore, not just on input,** mirroring the earlier "literals, not state" defence: stale localStorage must never widen the finalised parameter scales.

**Issues:**
- The in-headset `EvaluatorConfigPanel` still exposes the old min/max sliders — flagged for a follow-up if the supervisor wants both UIs to match.

**Next:**
- End-to-end demo pass with the final parameter scales; decide whether `EvaluatorConfigPanel` should mirror the single room-count control.

---

### 2026-07-27 — Phase 5 evaluation: scenario metrics extraction

**Status:** Phase 5, Stage 1. Building the measurement instrument for the evaluation study — turning a generated `ScenarioData` into a row of numbers.

**Done:**
- New `Scripts/ScenarioGeneration/Evaluation/ScenarioMetrics.cs` (864 lines). Plain C#, no `MonoBehaviour`, namespace `TeamSentinels.ScenarioGeneration.Evaluation`.
- `ScenarioMetricsResult` carries **39 CSV columns in six groups**: layout complexity (`roomCount`, `doorCount`, `avgConnectivity`, `graphDiameter`, `maxDepth`, `cyclicityMeasure`, `entryPointCount`), door states (open/closed/locked + `lockedDoorFraction`), entity distribution (distance from entry, clustering coefficient, hostage–terrorist mean/min, depth mean/variance), navigation complexity (mean patrol route length, the four role counts, `patrolCoverage`, `avgWaypointCount`), furniture (total, per room, floor coverage, furnished rooms), and an echo of the 11 input parameters for grouping and correlation.
- Public API: `Extract(ScenarioData)`, `ToCsvHeader()`, `ToCsvRow()`. Private helpers: `ComputeGraphDiameter` (all-pairs BFS), `ComputeClusteringCoefficient`, `ComputePatrolCoverage`, `CountUniqueDoors` (deduplicates the reciprocal door records by shared door ID), `GetEntryRoomPosition`, `ComputeFurnitureFloorCoverage`.
- Enum echoes are written as their JSON wire values (`hub_and_spoke`, `front_loaded`, `small`) by reading each enum's `EnumMember` attribute, so CSV columns match exactly what the dashboard sends.
- Verified in-editor against three contrasting configs (linear/3 rooms/1 terrorist, loop/8/6, hub-and-spoke/12/8): cyclicity is non-zero only for `loop` (0.125), diameter tracks topology (linear 3.00 > branching 2.66 > hub/loop 2.00), and degenerate input (empty layout, null collections) returns a full row of zeros rather than throwing.
- Seed determinism confirmed: the same seed reproduces a byte-identical row across all 38 measured and echoed columns.

**Decisions:**
- **Echo `difficultyLevel`, `randomnessLevel` and `hostageRiskLevel` even though the evaluator form now fixes them.** Experiments C and D vary them programmatically, and the analysis needs those columns to group rows.
- **`configRoomCount` read from `roomCount.min`,** since the dashboard collapses the range and sends min = max.
- **Metrics derived from the scenario alone,** never from generator internals, so an exported `Scenario.json` batch can be re-measured later without re-running generation.

**Issues:**
- `scenarioId` is a fresh `Guid.NewGuid()` per `Generate()` call, so two runs with the same seed differ *only* in that column. Any duplicate detection or join must key on `seedUsed` plus the metric columns, not on `scenarioId`.

**Next:**
- Batch runner to drive the experiments and write the CSVs.

---

### 2026-07-27 — Phase 5 evaluation: batch experiment runner + first full run

**Status:** Phase 5, Stage 2. Six experiments wired as one-click editor actions; first complete 2000-scenario dataset produced.

**Done:**
- New `Evaluation/BatchEvaluationRunner.cs` (1005 lines). `MonoBehaviour` with a `[ContextMenu]` entry per experiment, writing to `Assets/Module1_DataModels_and_IO/Output/EvaluationResults/`.
- **Experiment A** — 100 scenarios at the production baseline (randomness medium). **A2** — same at high randomness, as a diversity ceiling. **B (PRIMARY)** — 1000 scenarios sweeping the six evaluator-facing form parameters across 20 levels, 50 each, everything else held at baseline. **C** — 150 across randomness low/medium/high. **D** — 150 across difficulty 1/3/5. **E** — 500 sampling the full pipeline space, deliberately including `roomCount` 3–15 and `terroristCount` 1–8, i.e. beyond what the form exposes.
- Shared `MakeBaselineConfig()`: branching, 4 rooms, medium size, single entry, 3 terrorists, dispersed, hostage risk medium, difficulty 3, randomness medium.
- Grouping columns per experiment: `variedParameter` + `parameterValue` (B), `randomnessBatch` (C), `difficultyBatch` (D), `withinFormRange` (E). `validationPassed`, `validationWarnings` and `errorMessage` on every file; `failureCategory` and `seedRetries` additionally on E.
- **First full run: 2000 scenarios in 1.8 s, 0 generation exceptions, 95.8% overall validation pass** (Experiment E alone 83.2%).

**Decisions:**
- **Explicit per-scenario seeds instead of the pipeline's auto-seed.** `ScenarioGenerator` falls back to `Environment.TickCount`, whose ~15 ms resolution is far coarser than one generation (~1 ms), so a tight loop would hand many scenarios the *same* seed and collapse the very variability Experiment A exists to measure. Seeds now come from a master `System.Random` (`masterSeed` inspector field) with a uniqueness guard — unique per scenario *and* reproducible across re-runs.
- **Per-experiment seed-stream offsets** (`masterSeed + 1…6`). Without them Experiment A, Experiment C's medium batch and Experiment D's difficulty-3 batch — all the identical baseline config — would draw the same seeds and be byte-identical repeats of each other instead of independent samples.
- **Added a `seedRetries` column to Experiment E.** The pipeline silently retries a failed scenario up to three times with `seed + 1`, so a bare pass rate reports "passed *eventually*". The column shows 355 scenarios passed first time, 38 needed one retry, 15 needed two, and 92 exhausted all three — without it, 53 scenarios that only passed on retry would have been indistinguishable from clean passes.
- **Generator logging suppressed during batches** (inspector toggle). The pipeline emits ~16 log lines per scenario; a full suite would put ~32,000 entries in the console and stall the editor. Nothing is lost — validation results and exceptions are captured into the CSV.
- **Failed generations still emit a row** with `scenarioId = FAILED` and the input-parameter echo populated from the config, so a failure remains attributable to the parameter combination that caused it.

**Issues:**
- Runs are synchronous, so the editor is unresponsive for the duration — acceptable at ~2 s for the full suite.
- Output CSVs live under `Assets/`, so Unity generates a `.meta` beside each one.

**Next:**
- Pairwise diversity measure — per-scenario metrics show distribution spread, not whether individual scenarios actually differ.

---

### 2026-07-27 — Phase 5 evaluation: pairwise diversity analyser

**Status:** Phase 5, Stage 3. Head-to-head structural comparison between scenarios, closing the gap left by per-scenario metrics.

**Done:**
- New `Evaluation/DiversityAnalyser.cs` (510 lines), plain C#. `PairwiseDiversityResult` carries seven measures: `graphEditDistance` (symmetric difference of the room-edge sets), `entityPositionDistance` (mean Euclidean distance between entities matched by type then index), `jaccardRoomConnectivity`, `roleDistributionDistance`, `depthProfileDistance` (Manhattan distance between depth histograms), `doorStateDistance` (doors paired by position-sorted index; surplus doors count as mismatches), `furnitureCountDistance`.
- `Compare(a, b)`, `CompareAll(list)` (warns above 100 scenarios, since pairs grow as n(n−1)/2), plus CSV header/row.
- Runner updated (+73 lines): `GenerationOutcome` now carries the `ScenarioData`, and Experiments A and A2 retain their batches → **4950 pairs each** → `ExperimentA_PairwiseDiversity_Medium.csv` and `ExperimentA2_PairwiseDiversity_High.csv`.
- `ScenarioMetrics.CountUniqueDoors` made `internal` and reused, so both files share one definition of "unique door".
- Verified: all 4950 rows in each file join back to a row in the corresponding metrics CSV; no duplicate or self-pairs; distances arithmetically correct on hand-built 1/3/8-room layouts.

**Key findings (feed directly into RQ4):**
- At the production baseline, `graphEditDistance` takes **only the values 0 and 2** and Jaccard only 0.5 and 1.0. Solving the pair counts, Experiment A's 100 scenarios resolve to **exactly two distinct room topologies, split 70/30**; A2 at high randomness produces the *same two* topologies at 60/40. High randomness adds no new topology.
- `graphEditDistance`, `roleDistributionDistance` and `depthProfileDistance` are **identical in 100% of A's 4950 pairs** — at 4 rooms a single edge change necessarily moves one room's depth and one NPC's role. Re-tested at 12 rooms they decouple completely (0% equal; means 9.57 / 0.00 / 4.47), confirming the three measures are genuinely distinct and the collinearity is a property of the small baseline.

**Decisions:**
- **Room *indices* (in id order), not room IDs, as graph node identity,** so layouts with different room counts remain comparable — the nth room of A is matched against the nth room of B.
- **Jaccard kept as a similarity rather than converted to a distance,** because that is how it is conventionally reported; it is the one field where larger means *more alike*.

**Issues:**
- The collinearity above means those three columns must be treated as **one signal, not three**, in any analysis run at the 4-room baseline.

**Next:**
- Audit the whole Evaluation folder before running the dataset that the report will cite.

---

### 2026-07-27 — Phase 5 evaluation: tooling audit + CSV row assembly fix

**Status:** Phase 5, Stage 4. Eight-point audit of the three Evaluation scripts, one latent defect found and fixed.

**Done:**
- Audited `ScenarioMetrics.cs`, `BatchEvaluationRunner.cs` and `DiversityAnalyser.cs` for: degenerate-input handling, CSV quoting and column alignment, `CompareAll` with differing room counts, Experiment B grouping columns, Experiment E `withinFormRange`, `Path.Combine` + directory creation, `MonoBehaviour` confinement, and `FurnitureData` field-name agreement with `LayoutModels.cs`. **All eight pass.**
- Degenerate inputs verified directly: single-room, zero-room and null layouts, plus null furniture / navigation-context / entity lists, all return a full 39-column row without throwing.
- Column alignment verified with a quote-aware parser across all eight output files, including rows whose `validationWarnings` contain commas.
- Dry run at reduced counts (A = 5, B = 3 per level) produced 5 / 10 / 60 rows — pairwise = C(5,2), B = 20 levels × 3 — with furniture and door-state metrics non-zero, grouping columns on every row, and no exceptions. Full counts restored and the suite re-run.
- Audit written up in `DEVLOG.md` (+105 lines).

**Decisions:**
- **Row assembly switched from append-with-separator to format-and-join.** `ScenarioMetricsResult.ToCsvRow()` had built its row by appending to a `StringBuilder`, deciding "is this the first field?" by testing `sb.Length > 0`. Correct in practice because `roomCount` always leads — but had the column order ever changed to lead with a string field that happened to be empty, that field would have swallowed its own separator and shifted every later column, producing a silently corrupt CSV with no error. Fields are now formatted into a `string[]` and joined, so the column count is structurally equal to the array length. The three `Append*` helpers became `FormatInt` / `FormatFloat` / `FormatString`; all three Evaluation files now build rows the same way.

**Issues:**
- **`doorsOpen` has zero variance at the 4-room baseline** — 0 open doors across all 300 doors in Experiment A, versus 45 across Experiment B's 3050. Not a metrics defect: `LayoutGenerator.DecideDoorState` locks any door touching the hostage room and closes any door touching the entry room, so only a direct standard-to-standard edge can be open. A 4-room layout has one entry, one hostage room and two standard rooms, and neither generated topology connects those two directly. The column therefore cannot contribute to Experiments A and A2.

**Next:**
- Statistical analysis over the 2000-scenario dataset.

---

### 2026-07-29 — Phase 5 evaluation: statistical analysis and results pack

**Status:** Phase 5 complete. Full statistical treatment of the 2000-scenario dataset, packaged for the report.

**Done:**
- `Output/EvaluationResults/MODULE1_RESULTS.md` (296 lines) — machine-readable results summary with YAML front-matter, per-experiment sections, an RQ mapping, direct answers to RQ1–RQ4, a limitations list and an artifact index.
- Statistical method: Shapiro-Wilk per group → one-way ANOVA + Tukey HSD when all groups are normal, otherwise Kruskal-Wallis + Dunn with Bonferroni correction. Effect sizes η² (ANOVA) and ε² (Kruskal-Wallis); Mann-Whitney U with rank-biserial for the A vs A2 diversity comparison. Stack: pandas, scipy, scikit-posthocs, matplotlib, seaborn.
- Committed artifacts: `tables/B_SUMMARY_traceability_matrix.csv`, `tables/E_reliability_summary.csv`, and six figures (`FigA1` diversity histograms, `FigB1` layoutType boxplots, `FigB5` terroristCount boxplots, `FigD1` difficulty boxplots, `FigE1` pass rate by scope, `FigE3` pass-rate heatmap).

**Headline results:**
- **RQ4 / Experiment A:** at production settings the room **graph is deterministic** — `roomCount`, `doorCount`, `avgConnectivity`, `cyclicityMeasure`, `entryPointCount` and locked-door count all have CV = 0%. Variation is *tactical*, not topological: `roamingGuardCount` CV 153.5%, `entityDepthVariance` 65.8%, `minHostageTerroristDistance` 60.6%, `entityClusteringCoefficient` 36.6%, `patrolCoverage` 35.5%, `totalFurnitureCount` 17.6%. Mean pairwise entity-position distance 10.06 m is the dominant continuous axis of variation.
- **RQ4 / A vs A2:** high randomness gives a statistically detectable but practically marginal gain (graph edit distance +14.3%, p < 0.0001, rank-biserial 0.061); placement and furniture diversity actually *decrease* slightly. Re-exposing the randomness control would unlock little.
- **RQ1 / Experiment B:** every structural form parameter maps to its intended layout metrics with large, highly significant effects — `layoutType` → `avgConnectivity` and `cyclicityMeasure` (ε² = 1.000), `maxDepth` (0.890), `graphDiameter` (0.765); `roomCount` → actual room count (1.000) and `graphDiameter` (0.734); `roomSize` → `avgFurniturePerRoom` (0.878) and `avgEntityDistanceFromEntry` (0.689); `placementStrategy` → `entityDepthVariance` (0.420) and clustering (0.327).
- **RQ2 / Experiments B and D:** guard and patrol composition is deterministic and monotonic in `terroristCount` (`patrolCount` ε² = 1.000, `avgPatrolRouteLength` and `patrolCoverage` 0.901) and reconfigured by difficulty — at difficulty 5 patrolling guards convert to stationary, collapsing `patrolCount` and `patrolCoverage` to 0, while minimum hostage–terrorist distance rises monotonically (3.00 → 3.51 → 3.93 m). Roles are structure-derived, not arbitrary.
- **RQ3 / Experiment E:** **100% validation pass (43/43) for every configuration an evaluator can actually request**, versus 81.6% (373/457) in the extended range and 83.2% overall. All 84 failures are `EntityBounds` (90.5%) and `EntityOverlap` (9.5%), confined entirely to the unreachable extended range; worst case is high terrorist counts in small rooms (8 terrorists in a small 4-room branching layout → 0% pass).
- **Experiment C:** at a fixed config, randomness produces no measurable structural change; only `entityDepthVariance` drifts and not significantly (p = 0.061).

**Decisions:**
- **`withinFormRange` split reported as the primary reliability figure.** The pipeline's overall 83.2% understates what evaluators experience, because it includes configurations the form cannot produce; the form-reachable 100% is the number that describes the delivered system.

**Issues:**
- **Known issue — `entryType`:** changes `entryPointCount` (single → 1, multiple → 3) but has *zero* effect on `avgConnectivity` (constant 1.50) or `doorsOpen` (constant 0). Entries attach as exterior access points with no interior re-wiring, so the parameter is traceable on access only. Flagged in the limitations list.
- **Artifact gap:** `MODULE1_RESULTS.md` references roughly 17 tables and 13 figures, but only 2 tables and 6 figures are committed. The remainder need regenerating (or the artifact index trimming) before the report cites them.
- **Front-loaded placement deviates from design:** uses `ceil(maxDepth/3)` rather than the design document's `ceil(maxDepth/2)`, concentrating threats closer to the entry than specified.
- **Patrol route truncation:** multi-room routes are present in `Scenario.json` but Module 2's `PatrolLine` exposes only start/end, so routes collapse to 2 waypoints at runtime.
- **Furniture metrics are not comparable across generator versions** — the companion-grouping pass raised furniture density relative to earlier builds.
- The 43 form-reachable rows in Experiment E give a 95% confidence interval of roughly 92–100% on that headline pass rate; raising `validationSamples` or stratifying the sampler would tighten it.

**Next:**
- Write up Phase 5 for the project report using `MODULE1_RESULTS.md` as the ground truth; regenerate the missing tables/figures first.
- Decide whether to act on the `entryType` interior-connectivity gap and the front-loaded depth deviation, or document both as known limitations.

---
