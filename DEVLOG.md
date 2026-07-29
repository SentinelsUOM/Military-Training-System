# Module 1 Devlog

## 2026-04-25 — Audit of Scripts/ScenarioGeneration + dry-run

### Scope
Audited every `.cs` file under
`Assets/Module1_DataModels_and_IO/Scripts/ScenarioGeneration/` for:
1. Compile-time integrity (using statements, types, references)
2. XML doc coverage on public methods
3. Namespace conformance to `TeamSentinels.ScenarioGeneration.{DataModels|IO|Generators|Validation|Tests}`
4. `[JsonProperty]` camelCase against `Assets/Docs/Phase 1/Stage 1/ScenarioConfig.schema.json`
   and `Assets/Docs/Phase 1/Stage 2/Scenario.schema.json`
5. `ScenarioGenerator.Generate()` pipeline order: Layout → Entity → Role →
   NavContext → Validate
6. No `MonoBehaviour` on generators, validator, or data models (only the test
   suite is allowed to be `MonoBehaviour`)
7. All randomness flowing through the seeded `System.Random` instance —
   no `UnityEngine.Random` calls

### Files audited
DataModels: `ScenarioEnums.cs`, `SharedTypes.cs`, `ScenarioConfig.cs`,
`LayoutModels.cs`, `EntityModels.cs`, `RoleAndNavigationModels.cs`,
`ScenarioData.cs`.
IO: `ScenarioJsonSettings.cs`, `ScenarioConfigLoader.cs`, `ScenarioExporter.cs`.
Generators: `LayoutGenerator.cs`, `EntityPlacer.cs`, `RoleAssigner.cs`,
`NavigationContextBuilder.cs`, `ScenarioGenerator.cs`.
Validation: `ScenarioValidator.cs`.
Tests: `ScenarioGenerationTests.cs`.

### Verification results

| Check | Result |
|---|---|
| Compile-time integrity (refs, types, usings) | PASS — confirmed by clean compile of all 17 files into a standalone exe |
| XML doc coverage on public methods | PASS after fix (see below) |
| Namespaces | PASS — every file matches its folder convention |
| `[JsonProperty]` camelCase vs schema | PASS — every property name in both schemas appears verbatim in the data models |
| `ScenarioGenerator.Generate()` pipeline order | PASS — `_layoutGenerator.Generate` → `_entityPlacer.Place` → `_roleAssigner.Assign` → `_navigationContextBuilder.Build` → `_scenarioValidator.Validate`, with retry-on-failure that increments the seed |
| No `MonoBehaviour` on generators/validator/data models | PASS — only `ScenarioGenerationTests` is `MonoBehaviour` |
| `UnityEngine.Random` calls | PASS — `Grep` found zero matches; every randomness call is `System.Random.Next` / `NextDouble` on the rng instance threaded through the pipeline |

### Issues found and fixed
Six public constructors were missing XML doc comments. Added one-line
`<summary>` to each:

- [SharedTypes.cs](Assets/Module1_DataModels_and_IO/Scripts/ScenarioGeneration/DataModels/SharedTypes.cs)
  — `RoomSize()`, `RoomSize(float, float, float)`, `RoomCountRange()`,
  `RoomCountRange(int, int)`, `BoundingBox()`,
  `BoundingBox(SerializableVector3, SerializableVector3)`.
- [ScenarioConfigLoader.cs](Assets/Module1_DataModels_and_IO/Scripts/ScenarioGeneration/IO/ScenarioConfigLoader.cs)
  — `ScenarioConfigValidationException(string, List<string>)`.

No other defects detected. No type mismatches, no orphan references, no
schema drift, no `MonoBehaviour` leakage, no `UnityEngine.Random` usage.

### Dry-run
Compiled the entire ScenarioGeneration source tree (17 files) plus a
minimal `UnityEngine` stub (`Output/unity_stub.cs`) and a `DryRun.Main`
entry point (`Output/dryrun_main.cs`) into a netstandard 2.1 exe under
`Output/dryrun_build/dryrun.exe`. Linked against:

- `netstandard.dll` 2.1 reference assembly from
  `C:/Program Files/Unity/Hub/Editor/6000.3.3f1/Editor/Data/NetStandard/ref/2.1.0/`
- `Newtonsoft.Json.dll` from the project's package cache
  (`Library/PackageCache/com.unity.nuget.newtonsoft-json@4dfd81071c64/Runtime/AOT/`).

Executed on the .NET 8.0.16 runtime with `dotnet exec` against
`dryrun.runtimeconfig.json`.

The dry-run constructed a `ScenarioConfig` with default field initializers,
pinned `seed = 20260419`, called `ScenarioGenerator.Generate(config)`, and
serialised the result through `ScenarioExporter.SerialiseToJson`. Console
output:

```
=== Config: layout=Branching, rooms=[5-8], terrorists=4, difficulty=3, seed=20260419 ===
=== Generation complete ===
    scenarioId    : <new GUID per run>
    rooms         : 6
    entities      : 6
    roleAssignments: 4
    validation    : passed=True (10/10); warnings=(none)
=== JSON length: 12984 chars across 592 lines ===
```

Inspected the JSON output (`Output/dryrun_build/Scenario_dryrun.json`):

- Top-level keys present and in schema order: `scenarioId`, `missionType`,
  `layout`, `spawnPoints`, `entities`, `roleAssignments`, `navigationContext`,
  `configurationMetadata`.
- `missionType: "hostage_rescue"`, all enums emitted as snake_case strings
  (`"hostage_rescue"`, `"entry"`, `"corridor"`, `"standard"`, `"hostage_room"`,
  `"south"`, `"west"`, `"east"`, `"north"`, `"branching"`, `"medium"`,
  `"single"`, `"trainee"`, `"hostage"`, `"terrorist"`, `"patrol"`,
  `"stationary_guard"`, `"roaming_guard"`, `"hostage_guardian"`).
- `layout.rooms`: 6 rooms, each with `id`, `type`, `position {x,y,z}`,
  `size {width,depth,height}`, `connectedRoomIds`, `doors[]`, `depth`,
  `zoneLabel`. `room_05` correctly typed `hostage_room`. Doors reciprocal
  with matching IDs across both endpoints.
- `spawnPoints`: trainee at `room_01` (entry), 1 hostage at `room_05`,
  4 terrorists distributed clustered (per default
  `placementStrategy=dispersed`).
- `entities`: 6 records (1 trainee + 1 hostage + 4 terrorists), each with
  `id`, `type`, `assignedRoom`, `position`, `metadata.initialState`.
- `roleAssignments`: 4 entries, all four `NpcRole` values represented exactly
  once each (`hostage_guardian` priority 1, then patrol/roaming_guard/
  stationary_guard at priority 3/2/2). The terrorist closest to the hostage
  (`terrorist_02` in `room_05`) was correctly selected as the guardian.
- `navigationContext`: 4 entries keyed by `nav_<entityId>` matching every
  `RoleAssignment.navigationContextId`. Each entry's role-specific fields
  populate per the schema (patrol → `patrolRoute`/`waypoints`/`looping`;
  stationary_guard → `guardRoomId`/`guardPosition`/`facingDirection`;
  roaming_guard → `roamingRoomIds`/`anchorRoomId`/`waypoints`;
  hostage_guardian → `guardedEntityId`/`hostageRoomId`/`guardPosition`/
  `facingDirection`); irrelevant fields are correctly omitted via
  `NullValueHandling.Ignore`.
- `configurationMetadata`: full embedded `scenarioConfig`, ISO-8601
  `generationTimestamp`, `seedUsed: 20260419`, `generatorVersion: "1.0.0"`,
  and `validationResult { passed: true, checksRun: 10, checksPassed: 10,
  warnings: [] }`.

The dry-run harness lives under `Output/` (`unity_stub.cs`,
`dryrun_main.cs`, `dryrun_build/`) and is reusable for future audits — not
shipped with the Unity build.

## 2026-07-27 — Phase 5 evaluation tooling complete + audit

### Scope
Audited every `.cs` file under
`Assets/Module1_DataModels_and_IO/Scripts/ScenarioGeneration/Evaluation/` for:
1. `ScenarioMetrics.Extract()` handling 0 terrorists, 0 patrol routes,
   single-room layouts, null furniture lists, null navigation contexts
2. CSV output quoting string fields containing commas; column count matching
   the header exactly
3. `DiversityAnalyser.CompareAll()` handling scenarios with differing room counts
4. Experiment B writing `variedParameter` and `parameterValue` on every row
5. Experiment E writing `withinFormRange` correctly
   (roomCount 3–5 AND terroristCount 1–4)
6. All paths using `Path.Combine` and creating directories if missing
7. Only `BatchEvaluationRunner` being a `MonoBehaviour`
8. `FurnitureData` field names matching `LayoutModels.cs`

### Files audited
Evaluation: `ScenarioMetrics.cs`, `BatchEvaluationRunner.cs`,
`DiversityAnalyser.cs`.

### Verification results

| Check | Result |
|---|---|
| Degenerate inputs to `Extract()` | PASS — single-room, zero-room, null layout, null furniture, null navigation context and null entities all return a full 39-column row without throwing |
| CSV quoting + column count | PASS after fix (see below) — verified across all 8 output files with a quote-aware parser; every data row matches its header, including rows whose `validationWarnings` contain commas |
| `CompareAll()` with differing room counts | PASS — 1/3/8-room layouts compared cleanly; null list entries skipped; distances arithmetically correct (3 vs 8 rooms → shared 2 edges, union 7, GED 5, Jaccard 0.2857) |
| Experiment B grouping columns | PASS — 0 blank cells across 1000 rows; exactly 20 levels over the six parameters, 50 rows each |
| Experiment E `withinFormRange` | PASS — 43 of 500 rows flagged true, matching the ~11.5% expected from uniform sampling of roomCount 3–15 and terroristCount 1–8 |
| `Path.Combine` + directory creation | PASS — every experiment writes through one `WriteCsv` helper using `Path.Combine` and `Directory.CreateDirectory` |
| `MonoBehaviour` confined to the runner | PASS — `ScenarioMetrics` and `DiversityAnalyser` are static classes; `ScenarioMetricsResult` and `PairwiseDiversityResult` are plain classes |
| `FurnitureData` field names | PASS — `id`, `type`, `position`, `size`, `rotationY`, `againstWall` all match; metrics read `size.x`/`size.z` for the footprint |

### Issues found and fixed
One latent defect in `ScenarioMetricsResult.ToCsvRow()`. Fields were appended
to a `StringBuilder`, and the separator helper decided whether a field was the
first one by testing `sb.Length > 0`. Because the first column (`roomCount`) is
always a non-empty integer, the row was correct in practice — but had the
column order ever been changed to lead with a string field that happened to be
empty, that field would have swallowed its own separator and shifted every
later column, producing a silently corrupt CSV with no error.

Replaced the append-with-inline-separator approach with a `string[]` of
formatted fields joined by `string.Join(",", …)`, so the column count is now
structurally equal to the number of entries in the array. The three
`Append*(StringBuilder, …)` helpers became `FormatInt`/`FormatFloat`/
`FormatString` returning strings; `using System.Text` dropped as unused. This
also makes all three Evaluation files build rows the same way.

No other defects detected. No column drift, no unquoted delimiters, no
`MonoBehaviour` leakage, no schema drift against `LayoutModels.cs`.

### Dry-run
Temporarily reduced Experiment A to 5 scenarios and Experiment B to 3 per level
(injected into the serialised fields; source defaults left at 100 and 50), then
ran both:

```
ExperimentA_Variability_Medium.csv          5 rows   42 cols   misaligned=0
ExperimentA_PairwiseDiversity_Medium.csv   10 rows    9 cols   misaligned=0
ExperimentB_FormParameters.csv             60 rows   44 cols   misaligned=0
```

Pairwise output is 10 rows = C(5,2), and Experiment B is 60 = 20 levels × 3.
Furniture metrics non-zero on every row (16–21 items per scenario, 5.2–8.7%
floor coverage, 4 furnished rooms); door-state metrics non-zero
(2 closed + 1 locked per scenario, `lockedDoorFraction` 0.3333). Grouping
columns populated on all 60 rows across all 20 levels. No exceptions.

Restored the full counts and re-ran the whole suite (2.3 s): A=100, A2=100,
B=1000, C=150, D=150, E=500, plus 4950 pairwise rows for each of A and A2.

### Observations from the full run
- **Overall:** 2000 scenarios, 0 generation exceptions, 95.8% validation pass.
- **Experiment E:** 100% pass (43/43) within the dashboard's exposed ranges
  versus 81.6% (373/457) outside them. All 84 failures are `EntityBounds` (76)
  and `EntityOverlap` (8), and all occur at room or terrorist counts beyond the
  form limits — evidence that the 3–5 room and 1–4 terrorist caps are load-
  bearing rather than arbitrary.
- **Retry masking:** 355 scenarios passed first time, 38 needed one retry, 15
  needed two, 92 exhausted all three. Without the `seedRetries` column the
  headline pass rate would have hidden 53 scenarios that only passed because
  the pipeline silently retried them with `seed + 1`.
- **Baseline topology is nearly fixed:** across Experiment A's 100 scenarios
  `graphEditDistance` takes only the values 0 and 2 and Jaccard only 0.5 and
  1.0, which resolves to exactly **two** distinct room topologies split 70/30.
  Experiment A2 at high randomness yields the same two topologies at 60/40 —
  no new topology is produced. This corroborates the zero variance measured
  independently in `avgConnectivity` and `cyclicityMeasure`.
- **Collinear measures at 4 rooms:** `graphEditDistance`,
  `roleDistributionDistance` and `depthProfileDistance` are identical in 100%
  of A's 4950 pairs, because one edge change necessarily moves one room's depth
  and one NPC's role. They decouple completely at 12 rooms (0% equal; means
  9.57 / 0.00 / 4.47), confirming the measures are genuinely distinct. At the
  baseline they should be treated as one signal, not three.
- **`doorsOpen` is structurally zero at the baseline:** 0 open doors across all
  300 doors in Experiment A, versus 45 across Experiment B's 3050. Not a metrics
  defect — `LayoutGenerator.DecideDoorState` locks any door touching the hostage
  room and closes any door touching the entry room, so only a direct
  standard-to-standard edge is eligible to be open. A 4-room layout has one
  entry room, one hostage room and two standard rooms, and the generated
  topologies never connect those two directly. The column therefore has zero
  variance in A and A2 and cannot contribute to those experiments.
