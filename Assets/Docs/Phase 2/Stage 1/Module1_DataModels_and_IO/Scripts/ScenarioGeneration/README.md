# Module 1 – Dynamic Scenario Generation

## Folder Structure

```
Scripts/ScenarioGeneration/
├── TeamSentinels.ScenarioGeneration.asmdef   # Assembly definition
│
├── DataModels/                                # [System.Serializable] data classes
│   ├── ScenarioEnums.cs                       # All enumerations (MissionType, LayoutType, NpcRole, etc.)
│   ├── SharedTypes.cs                         # SerializableVector3, RoomSize, RoomCountRange, BoundingBox
│   ├── ScenarioConfig.cs                      # ScenarioConfig, MissionStructure, EntityConfiguration, ExecutionControls
│   ├── LayoutModels.cs                        # DoorData, RoomData, EntryPointData, LayoutMetadata, LayoutData
│   ├── EntityModels.cs                        # EntityRecord, EntityMetadata, SpawnPointData, TraineeSpawnPoint, NpcSpawnPoint
│   ├── RoleAndNavigationModels.cs             # RoleAssignment, NavigationContextEntry (with factory methods)
│   └── ScenarioData.cs                        # ScenarioData (top-level output), ConfigurationMetadata, ValidationResult
│
├── IO/                                        # Serialisation utilities
│   ├── ScenarioJsonSettings.cs                # Shared Newtonsoft.Json serialiser settings
│   ├── ScenarioConfigLoader.cs                # Reads & validates ScenarioConfig.json
│   └── ScenarioExporter.cs                    # Writes ScenarioData → Scenario.json, also loads existing scenarios
│
├── Generators/                                # Generation pipeline (Phase 2 stubs)
│   ├── ScenarioGenerator.cs                   # Orchestrator — calls all subsystems in order
│   ├── LayoutGenerator.cs                     # Room graph generation (linear/branching/hub/loop)
│   ├── EntityPlacer.cs                        # Entity placement with strategy constraints
│   ├── RoleAssigner.cs                        # Depth-based NPC role assignment
│   └── NavigationContextBuilder.cs            # Per-NPC navigation data (patrol/guard/roam/guardian)
│
└── Validation/                                # Validation layer (Phase 3 stubs)
    └── ScenarioValidator.cs                   # Two-tier validation (local + global constraints)

Resources/ScenarioConfigs/                     # Evaluator config files
├── default_config.json                        # Standard branching layout, difficulty 4
├── test_minimal_easy.json                     # Minimal 3-5 rooms, difficulty 1
└── test_max_difficulty.json                   # Maximum difficulty stress test

Output/GeneratedScenarios/                     # Generated Scenario.json output files
└── (generated files appear here)
```

## Dependencies

- **Unity 2022.3 LTS** — target platform
- **Newtonsoft.Json (Json.NET)** — via Unity Package Manager (`com.unity.nuget.newtonsoft-json`)

Newtonsoft.Json is required because Unity's built-in `JsonUtility` does not support:
1. `Dictionary<string, T>` (needed for `navigationContext`)
2. Nullable value types (`int?` for `seed` and `timeLimit`)
3. Custom enum serialisation with `[EnumMember]` attributes

### Installing Newtonsoft.Json in Unity

Open **Window → Package Manager → + → Add package by name** and enter:
```
com.unity.nuget.newtonsoft-json
```

## Usage Examples

### Loading a ScenarioConfig

```csharp
using TeamSentinels.ScenarioGeneration.DataModels;
using TeamSentinels.ScenarioGeneration.IO;

// From file path
ScenarioConfig config = ScenarioConfigLoader.LoadFromFile(
    "Assets/Resources/ScenarioConfigs/default_config.json");

// From TextAsset (loaded via Resources)
TextAsset asset = Resources.Load<TextAsset>("ScenarioConfigs/default_config");
ScenarioConfig config = ScenarioConfigLoader.LoadFromTextAsset(asset);

// Manual validation
if (ScenarioConfigLoader.IsValid(config, out var errors))
    Debug.Log("Config is valid");
else
    Debug.LogError(string.Join("\n", errors));
```

### Exporting a ScenarioData

```csharp
using TeamSentinels.ScenarioGeneration.DataModels;
using TeamSentinels.ScenarioGeneration.IO;

ScenarioData scenario = /* ... from generator pipeline ... */;

// Export with auto-generated path
string path = ScenarioExporter.ExportToFile(scenario);

// Export to specific path (compact for production)
ScenarioExporter.ExportToFile(scenario,
    "Builds/Scenarios/mission_01.json", compact: true);

// Serialise to string (for network/memory use)
string json = ScenarioExporter.SerialiseToJson(scenario);
```

### Loading a Previously Generated Scenario

```csharp
ScenarioData scenario = ScenarioExporter.LoadFromFile(
    "Output/GeneratedScenarios/Scenario_a1b2c3d4.json");

// Access layout
foreach (var room in scenario.layout.rooms)
    Debug.Log($"{room.zoneLabel} (depth {room.depth}): {room.type}");

// Access navigation context
foreach (var kvp in scenario.navigationContext)
    Debug.Log($"{kvp.Key}: {kvp.Value.type}");
```

### Creating NavigationContextEntry with Factory Methods

```csharp
using TeamSentinels.ScenarioGeneration.DataModels;

// Patrol context
var patrol = NavigationContextEntry.CreatePatrol(
    patrolRoute: new List<string> { "room_02", "room_01", "room_02" },
    waypoints: new List<SerializableVector3> {
        new(9f, 0f, -1.5f), new(5f, 0f, 0f), new(1f, 0f, 0f)
    },
    looping: true);

// Stationary guard
var guard = NavigationContextEntry.CreateStationaryGuard(
    guardRoomId: "room_04",
    guardPosition: new SerializableVector3(17.5f, 0f, -2f),
    facingDirection: new SerializableVector3(-1f, 0f, 0f));

// Hostage guardian
var guardian = NavigationContextEntry.CreateHostageGuardian(
    guardedEntityId: "hostage_01",
    hostageRoomId: "room_06",
    guardPosition: new SerializableVector3(23f, 0f, 0.5f),
    facingDirection: new SerializableVector3(-1f, 0f, 0f));
```

## Schema Alignment

Every field in these C# classes maps 1:1 to the JSON schemas designed in Phase 1:

| Schema Document                  | C# Class(es)                                         |
|----------------------------------|------------------------------------------------------|
| `ScenarioConfig_schema.json`     | `ScenarioConfig`, `MissionStructure`, `EntityConfiguration`, `ExecutionControls` |
| `Scenario_schema.json` → layout  | `LayoutData`, `RoomData`, `DoorData`, `EntryPointData`, `LayoutMetadata` |
| `Scenario_schema.json` → spawn   | `SpawnPointData`, `TraineeSpawnPoint`, `NpcSpawnPoint` |
| `Scenario_schema.json` → entities| `EntityRecord`, `EntityMetadata`                     |
| `Scenario_schema.json` → roles   | `RoleAssignment`                                     |
| `Scenario_schema.json` → nav     | `NavigationContextEntry`                             |
| `Scenario_schema.json` → meta    | `ConfigurationMetadata`, `ValidationResult`          |
