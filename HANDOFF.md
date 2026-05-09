# Module 1 → Module 2 Handoff

**From:** Pramoth (Module 1 — Dynamic Scenario Generation)
**To:** Module 2 owner (NPC Behaviour)
**Date:** 2026-05-06
**Repo state at handoff:** branch `feature/prefabs-mod1` → see PR for merge into `main`

This document is the integration contract. Read it once end-to-end, then keep it as a reference. For day-by-day decisions read [Module_1_Devlog.md](Module_1_Devlog.md). For architecture read [CLAUDE.md](CLAUDE.md). For algorithm pseudocode read [Assets/Docs/Phase 1/](Assets/Docs/Phase%201/) (do not modify).

---

## 1. The 60-second summary

Module 1 produces a `Scenario.json` (the shared contract) and a `SceneBuilder` that instantiates rooms / doors / NPCs into a Unity scene. After everything is placed and the NavMesh is baked, `SceneBuilder` raises `EventManager.NotifyScenarioReady()` — that single call is the handoff to Module 2. Every Module 2 NPC FSM should activate on `ScenarioReady`.

By the time `ScenarioReady` fires, every spawned `TerroristController` and `HostageController` already has its idle behaviour configured by `SceneBuilder` (see field mapping table below). Module 2's job is to take those configured controllers and run the FSMs.

---

## 2. The contract — `Scenario.json`

Schema: [Assets/Docs/Phase 1/Stage 2/Scenario.schema.json](Assets/Docs/Phase%201/Stage%202)
Generated examples: [Assets/Module1_DataModels_and_IO/Output/GeneratedScenarios/](Assets/Module1_DataModels_and_IO/Output/GeneratedScenarios/) (14 files committed, useful as regression fixtures).

Top-level sections Module 2 cares about:

| Section | What's in it | Module 2 use |
|---|---|---|
| `entities` | Every actor (trainee, hostage, terrorists) with id, role, position, metadata | Identifying NPCs, AAR/cognitive lookups |
| `roleAssignments` | Per-terrorist `NpcRole` + `navigationContextId` + `priorityLevel` + `assignedRoomId` | NPC selector scoring; idle-mode decision |
| `navigationContext` | Dictionary keyed by `navigationContextId`, role-typed payload (see §4) | Drives `idleMode`, `wanderRadius`, `staticFaceTarget`, `patrolLine` |
| `spawnPoints` | `{ trainee, hostages[], terrorists[] }` with id, position, facingDirection | Where SceneBuilder instantiates each prefab |
| `layout.rooms` | Room id, position, size, depth, type, doors | Used by Modules 3/4 too — Module 2 generally doesn't touch directly |

Note: the **hostage has no entry in `navigationContext`** by design (hostages are stationary; behaviour drives off `HostageController.currentState`).

---

## 3. The handoff signal

[Assets/Module1_DataModels_and_IO/Scripts/SceneBuilder/SceneBuilder.cs:499](Assets/Module1_DataModels_and_IO/Scripts/SceneBuilder/SceneBuilder.cs#L499)

```csharp
mgr.NotifyScenarioReady();
```

Fires synchronously inside `BuildScene` after rooms, doors, NPCs, and NavMesh are all in place. **It fires *before* NPC `Start()` methods run** (see Caveat #1 in §6) — fine for FSMs that initialise *on* `Start`, but if any FSM ever needs to *react* to `ScenarioReady` after its own `Start` has run, ping me and I'll convert the call to a one-frame coroutine.

---

## 4. Field-by-field NPC configuration

When `SpawnTerrorists` instantiates a terrorist prefab, `SceneBuilder.ConfigureTerrorist` writes the following fields on `TerroristController` between `Awake()` and `Start()`:

| Module 1 `NpcRole` | `idleMode` | Other fields written | Source in `navigationContext` |
|---|---|---|---|
| `Patrol` | `IdleMode.Patrol` | `patrolLine.pointA`, `patrolLine.pointB` (added or fetched from prefab) | `waypoints[0]` and `waypoints[last]` |
| `RoamingGuard` | `IdleMode.Wander` | `wanderRadius` | Computed from centroid of `roamingRoomIds` + 4 m padding (min 6 m) |
| `StationaryGuard` | `IdleMode.Static` | `staticFaceTarget` (child Transform stub) | `facingDirection` |
| `HostageGuardian` | `IdleMode.Static` | `staticFaceTarget` (child Transform stub) | `facingDirection` |

For hostages, `SpawnHostages` writes:

| Field | Source |
|---|---|
| `HostageController.currentState` | `entities[].metadata.initialState` (parsed enum) |

**Important:** `TerroristController.NPCRole` (Guard / Roamer / Leader) is a *separate* enum used by NPCSelector for scoring. Module 1 does NOT set this field — that's Module 2's territory and is independent of `NpcRole` from `roleAssignments`.

NavMesh is baked before `NotifyScenarioReady`, scoped to children of `_roomsRoot` only ([SceneBuilder.cs:475](Assets/Module1_DataModels_and_IO/Scripts/SceneBuilder/SceneBuilder.cs#L475)) — full-scene baking would hang the editor on the XRI rig.

---

## 5. How to run a scenario end-to-end

**Editor-only path** (no play mode):
- `Tools / Scenario Generator / Generate Default Scenario` — runs the full pipeline, writes JSON to `Output/GeneratedScenarios/`, shows a result dialog.
- `Tools / Scenario Generator / Generate Minimal (Easy)` / `Generate Max Difficulty` / `Generate from Custom Config...` — same, with different inputs.
- `Tools / Scenario Generator / Open Output Folder` — reveals the output folder in Explorer.

**Runtime path** (full integration test):
1. `Tools / Scenario Generator / Build Scene Prefabs` (one-shot — builds Room + Door prefabs and wires SceneBuilder).
2. Open `SampleScene`, optionally `Tools / Scenario Generator / Hide Template Content` to clear the XRI demo content.
3. Press Play.
4. Use the EvaluatorConfigPanel canvas → click `Generate` → click `Start Mission`.
5. Watch the colour-coded spawn-point gizmos (green=trainee, blue=hostage, red=terrorist) confirm placement, then NPCs activate after `ScenarioReady`.

For a quick smoke test the canned configs at [Assets/Module1_DataModels_and_IO/Resources/ScenarioConfigs/](Assets/Module1_DataModels_and_IO/Resources/ScenarioConfigs/) are deterministic when seeded.

---

## 6. Open caveats (non-blocking, but Module 2 should know)

1. **`NotifyScenarioReady` fires synchronously, before NPC `Start()` methods.** OK because FSM init runs in `Start` itself; flag if any future FSM needs to *react* to the event.
2. **`PatrolLine` is 2-point only.** Module 1 ships ordered `waypoints[]` arrays in `navigationContext`, but `SceneBuilder` collapses them to first/last because the existing `PatrolLine` component only has `pointA` / `pointB`. Multi-segment patrol routes need a Module 2-side `PatrolLine` upgrade — Module 1 already has the data.
3. **`ScenarioBootstrapper.cs` is now redundant.** `SceneBuilder` owns the `NotifyScenarioReady` call. Pending Module 2's confirmation, it can be deleted.
4. **Terrorist / hostage prefabs are placeholder cubes** with the controllers attached. Module 2 swaps in rigged art; no Module 1 changes needed — just drop new prefabs into the `SceneBuilder` slots.

---

## 7. Schema-change protocol

`Scenario.json` is consumed by Modules 2, 3, and 4. Therefore:

- **Adding fields:** safe. Module 1 will accommodate.
- **Renaming or removing fields:** requires coordination with all four modules. Open a discussion before touching the schema.
- **Changing field semantics** (e.g. `position` y-axis convention): same as renaming — coordinate first.
- **Bug reports:** wrong waypoints, unreachable rooms, role-distribution oddities → Pramoth (Module 1). NPC FSM behaviour after `NotifyScenarioReady` → Module 2.

---

## 8. References

| Purpose | File |
|---|---|
| Architecture, file layout, conventions | [CLAUDE.md](CLAUDE.md) |
| Day-by-day implementation decisions | [Module_1_Devlog.md](Module_1_Devlog.md) |
| Algorithm pseudocode (read-only reference) | [Assets/Docs/Phase 1/](Assets/Docs/Phase%201/) |
| Scenario.json schema | [Assets/Docs/Phase 1/Stage 2/Scenario.schema.json](Assets/Docs/Phase%201/Stage%202) |
| ScenarioConfig schema (evaluator input) | [Assets/Docs/Phase 1/Stage 1/ScenarioConfig.schema.json](Assets/Docs/Phase%201/Stage%201) |
| Runtime bridge | [Assets/Module1_DataModels_and_IO/Scripts/SceneBuilder/SceneBuilder.cs](Assets/Module1_DataModels_and_IO/Scripts/SceneBuilder/SceneBuilder.cs) |
| Evaluator UI | [Assets/Module1_DataModels_and_IO/Scripts/SceneBuilder/EvaluatorConfigPanel.cs](Assets/Module1_DataModels_and_IO/Scripts/SceneBuilder/EvaluatorConfigPanel.cs) |
| Editor menu commands | [Assets/Module1_DataModels_and_IO/Scripts/ScenarioGeneration/Editor/ScenarioGeneratorEditor.cs](Assets/Module1_DataModels_and_IO/Scripts/ScenarioGeneration/Editor/ScenarioGeneratorEditor.cs) |
| Greybox prefab builder | [Assets/Module1_DataModels_and_IO/Scripts/SceneBuilder/Editor/ScenePrefabBuilder.cs](Assets/Module1_DataModels_and_IO/Scripts/SceneBuilder/Editor/ScenePrefabBuilder.cs) |
| Test fixtures (regression inputs) | [Assets/Module1_DataModels_and_IO/Output/GeneratedScenarios/](Assets/Module1_DataModels_and_IO/Output/GeneratedScenarios/) |

---

## 9. First 30 minutes for Module 2

A suggested onboarding path:

1. **(5 min)** Read §§1–4 of this doc.
2. **(5 min)** Open [Module_1_Devlog.md](Module_1_Devlog.md) and skim the Phase 4 summary entry (2026-04-29) for the architectural overview.
3. **(5 min)** Run `Tools / Scenario Generator / Generate Default Scenario`. Open the resulting JSON. Find `entities`, `roleAssignments`, `navigationContext`, `spawnPoints`. Cross-reference with §2 of this doc.
4. **(10 min)** Open [SceneBuilder.cs](Assets/Module1_DataModels_and_IO/Scripts/SceneBuilder/SceneBuilder.cs). Read `BuildScene`, `SpawnTerrorists`, `ConfigureTerrorist`, `NotifyModule2Ready` in that order.
5. **(5 min)** Press Play → Generate → Start Mission. Confirm spawn-point gizmos render and NPCs reach the right positions.

After that, Module 2's first task is usually swapping the placeholder terrorist / hostage prefabs for the rigged versions and verifying the FSMs activate cleanly on `ScenarioReady`.

---

## 10. Contact

- **Module 1:** Pramoth Dilshan (`pramothdilshan2001@gmail.com`)
- **Repo:** [SentinelsUOM/Military-Training-System](https://github.com/SentinelsUOM/Military-Training-System)
