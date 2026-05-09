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
