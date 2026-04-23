# CLAUDE.md — Military Training System (Team Sentinels)

University of Moratuwa | VR-Based Hostage-Rescue Training System
Unity 2022.3 LTS | Meta Quest 3 | 2026

---

## Project Overview

A VR military training system for hostage-rescue scenarios. Procedurally generates unique indoor environments and NPC configurations from evaluator-defined parameters, then runs the training session in VR.

**Owner of this repo context: Pramoth — responsible for Module 1 (Dynamic Scenario Generation).**

---

## Module Responsibilities

| Module | Responsibility | Owner |
|--------|---------------|-------|
| **Module 1** | Dynamic Scenario Generation — PCG pipeline that produces `Scenario.json` | Pramoth |
| **Module 2** | NPC Behaviour — FSM controllers driven by `roleAssignments` + `navigationContext` | Other teammate |
| **Module 3** | Cognitive Analysis — uses `layout.rooms` zone IDs and entity actor IDs | Other teammate |
| **Module 4** | Logging / After-Action Review — uses `scenarioId`, `layout`, `spawnPoints`, `entities` | Other teammate |

Module 1's `Scenario.json` is the **shared contract** between all modules. Never change its schema without coordinating with the full team.

> Development progress is tracked in **[DEVLOG.md](DEVLOG.md)**. Add an entry at the end of every implementation session.

---

## Repository Structure

```
Assets/
├── Docs/                                 ← Reference only — do not modify
│   ├── Phase 1/                          ← Algorithm pseudocode
│   │   ├── Stage 1/ScenarioConfig.schema.json + example
│   │   ├── Stage 2/Scenario.schema.json + example
│   │   ├── Stage 3/Layout_Generation_Algorithm_Design.md
│   │   ├── Stage 4/Entity_Placement_Algorithm_Design.md
│   │   └── Stage 5/NPC_Role_Assignment_Algorithm_Design.md
│   ├── Phase 2/Stage 1/
│   │   └── Module1_DataModels_and_IO/    ← Mirror of working directory (reference copy)
│   ├── Literature_Review_Module1.docx
│   ├── Module1_Continuation_Plan.docx
│   ├── Module1_Phase_Prompts.docx
│   └── Project Proposal.docx
│
├── Module1_DataModels_and_IO/            ← Module 1 territory — all new code goes here
│   ├── Scripts/ScenarioGeneration/       ← LIVE implementation
│   │   ├── TeamSentinels.ScenarioGeneration.asmdef
│   │   ├── DataModels/                   ← ScenarioEnums, SharedTypes, ScenarioConfig,
│   │   │                                    LayoutModels, EntityModels,
│   │   │                                    RoleAndNavigationModels, ScenarioData
│   │   ├── IO/                           ← ScenarioJsonSettings, ScenarioConfigLoader,
│   │   │                                    ScenarioExporter
│   │   ├── Generators/                   ← ScenarioGenerator, LayoutGenerator,
│   │   │                                    EntityPlacer, RoleAssigner,
│   │   │                                    NavigationContextBuilder
│   │   ├── Validation/                   ← ScenarioValidator
│   │   └── SceneBuilder/                 ← SceneBuilder (Stage 6 — not yet created)
│   ├── Resources/ScenarioConfigs/        ← Evaluator JSON input files
│   │   ├── default_config.json
│   │   ├── test_minimal_easy.json
│   │   └── test_max_difficulty.json
│   └── Output/GeneratedScenarios/        ← Generated Scenario.json files (runtime output)
│
├── Scripts/                              ← Module 2 territory — do not modify
│   ├── NPC/
│   │   ├── TerroristController.cs
│   │   ├── HostageController.cs
│   │   ├── NPCRole.cs                    ← Guard/Roamer/Leader/Hostage (scoring enum only)
│   │   └── ...
│   ├── Events/
│   │   ├── EventManager.cs
│   │   ├── ScenarioEvent.cs
│   │   └── ScenarioEventType.cs
│   ├── Detectors/
│   ├── Selection/
│   ├── Telemetry/
│   └── ScenarioBootstrapper.cs           ← Will be superseded by SceneBuilder
│
├── Scenes/
│   ├── SampleScene.unity                 ← Main VR scene
│   └── BasicScene.unity
│
├── VRTemplateAssets/                     ← XR template prefabs, materials, models
├── Guns/                                 ← Weapon models and scripts
├── Textures/
└── _Recovery/                            ← Unity crash recovery files (ignore)
```

---

## Phase Status

### Phase 1 — COMPLETE (algorithm design, done with Claude research AI)
All algorithm pseudocode is in `Assets/Docs/Phase 1/`. Do not modify these files — they are the reference specification.

### Phase 2 — IN PROGRESS (implementation with Claude Code)

**Stage 1 — Data Models + IO** ✅ COMPLETE
All files already exist in `Assets/Module1_DataModels_and_IO/Scripts/ScenarioGeneration/`.
Reference mirror: `Assets/Docs/Phase 2/Stage 1/Module1_DataModels_and_IO/`

**Stage 2 — LayoutGenerator implementation** ⬜ NEXT
File: `Assets/Module1_DataModels_and_IO/Scripts/ScenarioGeneration/Generators/LayoutGenerator.cs`
Reference: `Assets/Docs/Phase 1/Stage 3/Layout_Generation_Algorithm_Design.md`

**Stage 3 — EntityPlacer implementation** ⬜
File: `Assets/Module1_DataModels_and_IO/Scripts/ScenarioGeneration/Generators/EntityPlacer.cs`
Reference: `Assets/Docs/Phase 1/Stage 4/Entity_Placement_Algorithm_Design.md`

**Stage 4 — RoleAssigner + NavigationContextBuilder implementation** ⬜
Files: `Generators/RoleAssigner.cs`, `Generators/NavigationContextBuilder.cs`
Reference: `Assets/Docs/Phase 1/Stage 5/NPC_Role_Assignment_Algorithm_Design.md`

**Stage 5 — ScenarioGenerator orchestrator + ScenarioValidator** ⬜
Files: `Generators/ScenarioGenerator.cs`, `Validation/ScenarioValidator.cs`

**Stage 6 — SceneBuilder (Unity scene integration)** ⬜
New file: `Assets/Module1_DataModels_and_IO/Scripts/ScenarioGeneration/SceneBuilder/SceneBuilder.cs`

---

## Module 1 Generation Pipeline

```
ScenarioConfig.json
        ↓
ScenarioGenerator.Generate(config)
        ↓
  LayoutGenerator.Generate()          → LayoutData (rooms, doors, entry points)
        ↓
  EntityPlacer.PlaceEntities()        → SpawnPoints + List<EntityRecord>
        ↓
  RoleAssigner.AssignRoles()          → List<RoleAssignment>
        ↓
  NavigationContextBuilder.Build()    → Dictionary<string, NavigationContextEntry>
        ↓
  ScenarioValidator.Validate()        → ValidationResult
        ↓
ScenarioData (Scenario.json)
        ↓
SceneBuilder.BuildScene()             → Unity scene with rooms + NPCs spawned
        ↓
EventManager.NotifyScenarioReady()   → Hands off to Module 2
```

---

## C# Conventions for Module 1

### Namespace
All Module 1 code uses `TeamSentinels.ScenarioGeneration.*`:
- `TeamSentinels.ScenarioGeneration.DataModels`
- `TeamSentinels.ScenarioGeneration.IO`
- `TeamSentinels.ScenarioGeneration.Generators`
- `TeamSentinels.ScenarioGeneration.Validation`
- `TeamSentinels.ScenarioGeneration.Scene`

### Assembly
`TeamSentinels.ScenarioGeneration.asmdef` — references `Newtonsoft.Json.dll` via `precompiledReferences`. `autoReferenced: true` so existing scripts can see it. `noEngineReferences: false` (Unity types needed).

### JSON Serialisation
Always use **Newtonsoft.Json**, never `JsonUtility`. Shared settings are in `ScenarioJsonSettings.cs`.
- All enums serialise as snake_case strings via `[JsonConverter(typeof(StringEnumConverter))]` + `[EnumMember(Value = "...")]`
- Nullable types (`int?`) use `NullValueHandling = NullValueHandling.Include`
- `SerializableVector3` wraps `Vector3` for JSON serialisation (Unity's Vector3 is not directly serialisable by Newtonsoft)

### No Unity MonoBehaviour in generators
`LayoutGenerator`, `EntityPlacer`, `RoleAssigner`, `NavigationContextBuilder`, `ScenarioValidator` are all plain C# classes — no `MonoBehaviour`. Only `SceneBuilder` and Editor tools are `MonoBehaviour` / `Editor`.

---

## Key Design Constraints

- **Single floor only** — all Y positions are 0. Multi-floor reserved for future versions.
- **hostageCount is always 1** — schema const. Do not add multi-hostage logic.
- **terroristCount ≤ roomCount.max × 2** — enforced by ScenarioValidator.
- **Seed-based reproducibility** — same seed + same config = identical Scenario.json every time. All randomness flows through a single `System.Random(seed)` instance passed through the pipeline.
- **Traceability** — every generated value must trace back to an evaluator parameter in `ScenarioConfig`. This satisfies research requirement [22].

---

## Room Size Constants

| Category | Width | Depth | Height | Stride (w + gap) |
|----------|-------|-------|--------|-----------------|
| small    | 4.0m  | 4.0m  | 3.0m   | 6.0m            |
| medium   | 6.0m  | 6.0m  | 3.0m   | 8.0m            |
| large    | 8.0m  | 8.0m  | 3.0m   | 10.0m           |

Gap between rooms = 2.0m (corridor space).

---

## NPC Role Mapping (Module 1 → Module 2)

When `SceneBuilder` spawns a terrorist prefab it maps Module 1's `NpcRole` to `TerroristController.IdleMode`:

| Module 1 `NpcRole` | `TerroristController.IdleMode` | Notes |
|---|---|---|
| `Patrol` | `Patrol` | Requires `PatrolLine` component built from `navigationContext.waypoints` |
| `RoamingGuard` | `Wander` | Uses `wanderRadius` derived from roaming area size |
| `StationaryGuard` | `Static` | `staticFaceTarget` set from `navigationContext.facingDirection` |
| `HostageGuardian` | `Static` | Same as stationary; positioned near hostage spawn |

**Note:** `TerroristController.NPCRole` (Guard/Roamer/Leader) is a **separate enum** used only for NPCSelector scoring — it is NOT the same as Module 1's `NpcRole`. Both are set independently during spawning.

---

## Integration Handoff Point

```csharp
// In SceneBuilder, after all rooms and NPCs are spawned and NavMesh is baked:
EventManager.Instance.NotifyScenarioReady();
```

This is the exact handoff from Module 1 to Module 2. After this call, all NPC FSMs become active.

---

## Dependencies

| Package | Purpose | Install |
|---------|---------|---------|
| `com.unity.nuget.newtonsoft-json` | JSON serialisation (required — JsonUtility cannot handle `Dictionary<>` or nullable types) | Package Manager → Add by name |
| `com.unity.ai.navigation` | Runtime NavMesh baking (`NavMeshSurface.BuildNavMesh()`) | Package Manager → Add by name |

---

## Files to Never Modify

These are Module 2's territory or locked reference documents:

- `Assets/Scripts/NPC/` — all files
- `Assets/Scripts/Events/` — all files
- `Assets/Scripts/Detectors/` — all files
- `Assets/Scripts/Selection/` — all files
- `Assets/Scripts/Telemetry/` — all files
- `Assets/Docs/` — all files (read-only reference)

---

## Config Files (Evaluator Input)

Located at `Assets/Module1_DataModels_and_IO/Resources/ScenarioConfigs/`:

| File | Purpose |
|------|---------|
| `default_config.json` | Standard scenario: branching, 6–10 rooms, difficulty 4, clustered, seed 20260419 |
| `test_minimal_easy.json` | Minimal: 3–5 rooms, difficulty 1 — fast iteration during development |
| `test_max_difficulty.json` | Stress test: max rooms, difficulty 5, high randomness |

---

## Output

Generated scenarios are written to `Assets/Module1_DataModels_and_IO/Output/GeneratedScenarios/Scenario_<id>.json`.
The `scenarioId` is a UUID v4 generated per run.
