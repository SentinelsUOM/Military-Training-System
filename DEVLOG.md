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
