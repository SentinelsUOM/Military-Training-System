// =============================================================================
// SceneBuilder.cs
// Module 1 - Dynamic Scenario Generation (Stage 6)
// Team Sentinels | University of Moratuwa | 2026
//
// Bridge between Module 1 (data) and Module 2 (runtime). Takes a generated
// ScenarioData and instantiates the full mission scene in Unity: rooms,
// doors, hostage(s), terrorists. Configures each terrorist's IdleMode based
// on its Module 1 NpcRole, bakes the NavMesh, then raises ScenarioReady so
// Module 2's NPC FSMs become active.
//
// LOCATION NOTE:
//   This file lives at Assets/Module1_DataModels_and_IO/Scripts/SceneBuilder/,
//   one level ABOVE the TeamSentinels.ScenarioGeneration asmdef boundary, so
//   it compiles into Assembly-CSharp. That gives it direct access to Module
//   2's NPC scripts (TerroristController, HostageController, EventManager,
//   PatrolLine), which an asmdef-bound file cannot reference. Module 1's
//   data models remain visible because the asmdef has autoReferenced = true.
//
// DISCREPANCIES vs CLAUDE.md (resolved against the actual code):
//   * IdleMode is a top-level enum (global namespace), not TerroristController.IdleMode.
//   * PatrolLine supports multi-segment routes - the navigation context's full
//     ordered waypoints[] is passed via SetWaypoints() along with the looping
//     flag (loop vs ping-pong). pointA/pointB are still populated for legacy
//     tooling that reads the two-point fields.
//   * staticFaceTarget is a Transform; we create a child "FaceTarget" stub
//     positioned along navigationContext.facingDirection from the NPC.
//   * Hostage initial state is set on HostageController.currentState (enum),
//     not a free-form string field.
// =============================================================================

using System;
using System.Collections.Generic;
using TeamSentinels.ScenarioGeneration.DataModels;
using TeamSentinels.ScenarioGeneration.IO;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace TeamSentinels.ScenarioGeneration.Scene
{
    /// <summary>
    /// Instantiates the full mission scene from a generated <see cref="ScenarioData"/>.
    /// This is the runtime hand-off point from Module 1 to Module 2: after
    /// <see cref="BuildScene"/> completes successfully, Module 2's NPC FSMs
    /// take over via <see cref="EventManager.NotifyScenarioReady"/>.
    /// </summary>
    public class SceneBuilder : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────────

        [Header("Room Prefabs")]
        [Tooltip("Prefab used for RoomSizeCategory.Small (4x4 m).")]
        public GameObject roomPrefabSmall;

        [Tooltip("Prefab used for RoomSizeCategory.Medium (6x6 m).")]
        public GameObject roomPrefabMedium;

        [Tooltip("Prefab used for RoomSizeCategory.Large (8x8 m).")]
        public GameObject roomPrefabLarge;

        [Tooltip("Optional connector prefab between adjacent rooms.")]
        public GameObject corridorPrefab;

        [Tooltip("Door prefab placed at every DoorData position.")]
        public GameObject doorPrefab;

        [Tooltip("Material applied to procedurally built wall segments. If left " +
                 "empty, the wall material is borrowed from the room prefab's floor.")]
        public Material wallMaterial;

        [Tooltip("Material applied to the procedurally built room ceiling/roof. If " +
                 "left empty, the ceiling reuses the wall material so every room is " +
                 "capped to match the hand-built base map.")]
        public Material ceilingMaterial;

        [Header("Windows")]
        [Tooltip("Punch a glazed window into every solid exterior room wall (one " +
                 "with no door and no adjacent room) to make the building read as a " +
                 "real structure rather than a sealed box.")]
        public bool addWindows = true;

        [Tooltip("Optional glass material for window panes. If left empty, a " +
                 "best-effort translucent material is generated at runtime.")]
        public Material windowMaterial;

        [Header("Perimeter Corridor")]
        [Tooltip("Wrap the generated building in an enclosed corridor ring " +
                 "(floor + outer wall + roof). The building's exterior entry doors " +
                 "open into this corridor, and a single outer door lets the trainee " +
                 "in from the staging area.")]
        public bool buildPerimeterCorridor = true;

        [Tooltip("Width (metres) of the corridor band between the building's outer " +
                 "wall and the new outer perimeter wall.")]
        public float corridorWidth = 3f;

        [Tooltip("Optional floor material for the corridor ring. If left empty, the " +
                 "corridor reuses the room floor material so it matches the building.")]
        public Material corridorFloorMaterial;

        [Header("Furniture")]
        [Tooltip("Render the furniture that Module 1 placed in each room. Items are " +
                 "seed-reproducible, already scaled to the room, and guaranteed to " +
                 "clear walls, doorways and NPC/hostage spawns. They bake into the " +
                 "NavMesh as obstacles so NPCs path around them.")]
        public bool addFurniture = true;

        [Tooltip("Material for the shaped greybox furniture. If empty, a neutral " +
                 "URP-safe tint is generated at runtime so furniture never renders " +
                 "as the magenta 'missing shader' colour.")]
        public Material furnitureMaterial;

        [Tooltip("Use imported furniture models instead of shaped greyboxes. OFF by " +
                 "default because the bundled prop packs (e.g. PandazoleHome) ship " +
                 "Built-in-RP materials that render MAGENTA under this project's URP " +
                 "pipeline. Only turn this on after upgrading those materials to URP " +
                 "(Edit ▸ Rendering ▸ Materials ▸ Convert Selected…), or after mapping " +
                 "your own URP-ready prefabs below.")]
        public bool useFurniturePrefabs = false;

        [Tooltip("OPTIONAL override, honoured whether or not 'Use Furniture Prefabs' " +
                 "is on. Force a specific prefab for a furniture type (use URP-ready " +
                 "prefabs to avoid magenta). Prefabs are uniformly scaled to the " +
                 "generated footprint, so the layout stays collision-correct whatever " +
                 "the source art's native size.")]
        public List<FurniturePrefabMapping> furniturePrefabs = new List<FurniturePrefabMapping>();

        [Header("NPC Prefabs")]
        [Tooltip("Terrorist prefab. Must have a TerroristController component.")]
        public GameObject terroristPrefab;

        [Tooltip("Hostage prefab. Must have a HostageController component.")]
        public GameObject hostagePrefab;

        [Header("Player")]
        [Tooltip("Existing XR Origin / trainee rig in the scene. We only " +
                 "reposition this; we never instantiate a new rig.")]
        public Transform traineeRig;

        [Header("Placement")]
        [Tooltip("RECOMMENDED. Drop an empty GameObject into the empty area where " +
                 "you want the scenario, then drag it here. The generated layout's " +
                 "origin is built at this transform's world position, so you place " +
                 "the whole scenario visually in the Scene view with no guesswork. " +
                 "Takes priority over Build Offset.")]
        public Transform buildAnchor;

        [Tooltip("Fallback when no Build Anchor is set: a world offset applied to " +
                 "the ENTIRE generated scenario (rooms, doors, NPCs, trainee, " +
                 "patrol points and the NavMesh bake) so it doesn't overlap " +
                 "existing geometry. Tune this live until the gap looks right.")]
        public Vector3 buildOffset = new Vector3(0f, 0f, 20f);

        [Tooltip("Automatically nudge the WHOLE generated scenario so its outer " +
                 "footprint (rooms + perimeter corridor) keeps a clear gap from any " +
                 "existing base-map collider — the hand-built buildings already in the " +
                 "scene — instead of letting the two overlap. Applies to both Build " +
                 "Anchor and Build Offset placement: the resolved origin is shifted " +
                 "horizontally until the footprint is clear. No-op if the base map has " +
                 "no colliders on the tested layers.")]
        public bool avoidBaseMapOverlap = true;

        [Tooltip("Minimum gap (metres) to keep between the generated scenario's outer " +
                 "footprint and the nearest base-map collider when Avoid Base Map " +
                 "Overlap is on.")]
        public float mapClearance = 4f;

        [Tooltip("Which layers count as base-map geometry to stay clear of. Leave as " +
                 "Everything to test against every existing collider in the scene.")]
        public LayerMask baseMapLayers = ~0;

        [Tooltip("RECOMMENDED. Spawn the trainee in open ground just outside the " +
                 "generated building's entrance (facing the door), instead of inside " +
                 "the building or at a fixed staging point. The entrance moves per " +
                 "scenario, so this is computed each build — giving a clear, walkable " +
                 "approach the guide path can follow. Takes priority over Trainee " +
                 "Start Point.")]
        public bool spawnOutsideEntrance = true;

        [Tooltip("How far (metres) outside the entrance door the trainee spawns when " +
                 "Spawn Outside Entrance is on.")]
        public float entranceStandoff = 8f;

        [Tooltip("Optional staging spawn. When set (and Spawn Outside Entrance is off), " +
                 "the trainee spawns HERE (e.g. by the briefing table in your template) " +
                 "instead of inside the generated building. Leave empty to spawn at the " +
                 "generated entry as before.")]
        public Transform traineeStartPoint;

        [Tooltip("Start SECONDARY entry doors open so the trainee can walk straight in. " +
                 "The main entrance and the compound's outer door are always closed " +
                 "regardless of this setting — an open front door gives the defenders a " +
                 "clear shot at the trainee while they are still outside. Interior doors " +
                 "keep their generated state.")]
        public bool entryDoorStartsOpen = true;

        [Header("Trainee Loadout")]
        [Tooltip("Weapon moved to the trainee's spawn point when Start Mission is " +
                 "pressed, so it is within reach the moment the scenario loads. " +
                 "Drag the weapon that is already in the scene (e.g. 'Rifle 2Hand- " +
                 "Auto Fire'). We MOVE that object rather than instantiating the base " +
                 "prefab, so its Inspector overrides (automatic firing, infinite ammo) " +
                 "are preserved and no duplicate guns pile up on regeneration.")]
        public Transform traineeWeapon;

        [Tooltip("Where the weapon lands relative to the trainee: (right, up, forward) " +
                 "in metres. It drops to the floor from here if it has gravity.")]
        public Vector3 weaponSpawnOffset = new Vector3(0.3f, 1.0f, 0.6f);

        [Tooltip("Extra yaw (degrees) applied to the weapon on top of the trainee's " +
                 "facing. 90 lays it sideways - e.g. along the staging table's length.")]
        public float weaponSpawnYaw = 0f;

        [Tooltip("Controller-adjustment table moved in front of the trainee's spawn " +
                 "point so its tools are in reach the moment the mission starts. " +
                 "Assign every scene root that makes up the table (tabletop mesh, " +
                 "interactables group, stray controls sitting on it) - they are moved " +
                 "together as one rigid group, so items keep their spot on the " +
                 "tabletop. The FIRST entry is the reference root: its position is " +
                 "the table centre and, at 0 yaw, the table's front faces -Z (the " +
                 "'Main Table' convention). Leave empty to leave the table alone.")]
        public Transform[] stagingTableRoots;

        [Tooltip("Where the table's centre lands relative to the trainee: (right, up, " +
                 "forward) in metres. The default puts the tabletop just within " +
                 "reach, facing the trainee.")]
        public Vector3 tableSpawnOffset = new Vector3(0f, 0.86f, 1.6f);

        [Header("Safe Zone / Extraction")]
        [Tooltip("Spawn a 'safe spot' (extraction zone) just inside the entrance. " +
                 "Lead a rescued hostage back into it to complete the mission. Its " +
                 "floor marker stays hidden until the hostage is actually being " +
                 "escorted, so it doesn't spoil the extraction point up front.")]
        public bool createSafeZone = true;

        [Tooltip("Radius (metres) of the safe-spot trigger and its floor marker.")]
        public float safeZoneRadius = 2.5f;

        [Tooltip("Colour of the safe-spot floor marker (GTA-style checkpoint circle).")]
        public Color safeZoneColor = new Color(0.2f, 1f, 0.4f, 1f);

        [Header("Template Cleanup")]
        [Tooltip("Hide the XRI Starter Kit's demo content (base-map building, " +
                 "mini games, interaction tables, demo NPCs) once the scenario " +
                 "has been built, so only the generated mission is visible. The " +
                 "trainee weapon and staging-table roots assigned above are kept " +
                 "even though they live inside that template content. Everything " +
                 "is restored by ClearScene.")]
        public bool hideTemplateAfterBuild = true;

        [Header("Debug")]
        [Tooltip("Draw coloured spheres + facing arrows + entity-ID labels at " +
                 "every spawn point in the Scene view after Start Mission. " +
                 "Trainee = green, hostage = blue, terrorist = red.")]
        public bool showSpawnGizmos = true;

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// The scenario currently driving the scene. Other modules (e.g. AAR)
        /// can read this to access the originating data without re-loading
        /// the JSON file.
        /// </summary>
        public ScenarioData ActiveScenario { get; private set; }

        /// <summary>Raised after a successful scene build, just before
        /// <see cref="EventManager.NotifyScenarioReady"/> fires.</summary>
        public event Action<ScenarioData> OnSceneBuildComplete;

        /// <summary>Raised when a scene build fails. The argument carries a
        /// short human-readable reason.</summary>
        public event Action<string> OnSceneBuildFailed;

        // ── Internal containers ──────────────────────────────────────────────

        private const string ROOMS_ROOT    = "Rooms";
        private const string DOORS_ROOT    = "Doors";
        private const string NPCS_ROOT     = "NPCs";
        private const string CORRIDOR_ROOT = "PerimeterCorridor";

        // ── Wall geometry (mirrors ScenePrefabBuilder so walls, floors and doors
        //    line up exactly) ──────────────────────────────────────────────────
        // Module 1 leaves a 2 m corridor gap between adjacent room centres; each
        // room's outer wall sits half that gap (1 m) beyond its nominal extent so
        // neighbouring walls meet on a shared plane and the 2 m door opening lands
        // flush in it.
        private const float CorridorGap   = 2f;   // gap Module 1 leaves between rooms
        private const float DoorGap       = 2f;   // fallback door opening width (greybox door)
        private const float WallThickness = 0.12f;
        // Thickness of the flat ceiling/roof cap laid over each room, matching the
        // base map's "Ceiling" slab (a thin flattened cube resting on the walls).
        private const float CeilingThickness = 0.2f;
        // Floor slab thickness for the perimeter corridor (matches the room floor
        // prefab's 0.08 m so the corridor floor sits flush with the building floor).
        private const float CorridorFloorThickness = 0.08f;

        // ── Window geometry (carved into solid exterior walls) ────────────────
        private const float WindowWidth      = 1.0f;   // opening width along the wall
        private const float WindowHeight     = 0.9f;   // opening height
        private const float WindowSillHeight = 1.1f;   // floor → bottom of opening
        private const float WindowSideMargin = 0.4f;   // min solid wall each side of the opening
        private const float WindowPaneThickness = 0.04f;
        // Height of the door opening. Above this, a header (transom) fills the
        // wall up to the ceiling so doorways aren't open to the full wall height.
        // Matches the door prefab's leaf top and jamb height (2.1 m).
        private const float DoorOpeningHeight = 2.1f;
        // Extra width/height carved around the measured door so the real leaf
        // swings without scraping the jambs.
        private const float DoorClearance = 0.08f;
        // Vertical lift for the measured (realistic) door so its leaf clears the
        // floor slab. Room/corridor floors are 0.08 m slabs whose TOP sits 0.08 m
        // above the room origin, but door positions are authored at origin height —
        // resting the leaf's lowest point there buried it 8 cm inside the floor
        // collider. PhysX depenetrates the thin leaf SIDEWAYS (cheaper than 8 cm
        // up), shoving it against its world-anchored hinge every frame: doors hung
        // ajar, tilted and juddered. Slab top (0.08) + a 1.5 cm reveal.
        private const float DoorFloorLift = 0.095f;
        // Minimum solid wall left at each end of a door opening. Module 1 jogs
        // doors sideways off the wall midpoint to break sightlines; this caps the
        // jog when the measured door turns out wider than the generator assumed,
        // so an opening can never eat through a wall corner.
        private const float MinJamb = 0.3f;
        // How far (m) a door's perpendicular coordinate may sit off a room's own
        // outer extent and still count as "on that wall". High-randomness layouts
        // jitter each room ±0.5 m on both axes, so the shared midplane between two
        // adjacent rooms can be up to ~0.5 m off either room's extent; anything
        // beyond this tolerance is a loop/cross edge between rooms that were never
        // placed adjacent, whose door cannot be realised as geometry at all.
        private const float DoorPlaneTolerance = 1.25f;

        // ── Measured door footprint (filled by MeasureDoorPrefab each build) ──
        // SceneBuilder now supports realistic doors of any size (e.g. the XRI
        // hinge door) by measuring the assigned doorPrefab's mesh bounds and
        // fitting every wall opening to it. _doorBaseRot rotates the prefab so
        // its widest horizontal axis runs along X; _doorCenterOffset is the
        // prefab-local horizontal offset from its root to its visual centre, so
        // the door can be aligned to the opening even when its pivot sits on the
        // hinge edge rather than the centre.
        private float      _doorOpeningWidth  = DoorGap;
        private float      _doorOpeningHeight = DoorOpeningHeight;
        private Quaternion _doorBaseRot       = Quaternion.identity;
        private Vector3    _doorCenterOffset  = Vector3.zero;
        // Prefab-local Y of the door's lowest mesh point. Subtracted at placement
        // so the door rests on the floor instead of sinking/floating (the XRI
        // door's geometry sits well below its root origin).
        private float      _doorBaseY         = 0f;

        // World offset applied to every generated position this build, resolved
        // from buildAnchor / buildOffset. Lets the scenario sit in empty space
        // instead of overlapping a hand-built template at the origin.
        private Vector3    _buildOffset       = Vector3.zero;

        /// <summary>Maps a Module 1 layout position into world space, applying
        /// the per-build placement offset.</summary>
        private Vector3 World(Vector3 layoutPos) => layoutPos + _buildOffset;

        private Transform _roomsRoot;
        private Transform _doorsRoot;
        private Transform _npcsRoot;
        private Transform _corridorRoot;

        // World position of the building's outer entrance door, captured while the
        // perimeter corridor is built. The guide path targets this so it always ends
        // at the door the trainee walks through. Reset false each build.
        private bool    _hasEntryDoorWorld;
        private Vector3 _entryDoorWorld;

        // True when the trainee was spawned in open ground outside the entrance this
        // build (via spawnOutsideEntrance); the guide path is drawn only then.
        private bool _traineeSpawnedOutside;

        // Guaranteed building entrance, resolved each build BEFORE walls/corridor are
        // built. The entry room is the BFS layout origin and is frequently ringed by
        // other rooms (hub-and-spoke, dense branching), so its own entry point often
        // sits INSIDE the footprint — carving no exterior opening and sealing the
        // building. We instead resolve a genuine exterior wall on the perimeter room
        // nearest the entry room, then align the room breach door, the corridor outer
        // door, and the trainee spawn to it so there is always a coherent way in.
        private bool     _entranceResolved;
        private string   _entranceRoomId;
        private WallSide _entranceSide;
        private Vector3  _entranceWallMidWorld;   // midpoint of the breach wall (world)

        // Outer footprint of the building INCLUDING the perimeter corridor, in world
        // space, captured during the corridor build. The guide path routes around
        // this rectangle so the road stays outside the walls instead of cutting
        // through the interior. Valid only when _hasEntryDoorWorld is true.
        private float _fpMinX, _fpMaxX, _fpMinZ, _fpMaxZ;

        private readonly Dictionary<string, GameObject> _roomObjects =
            new Dictionary<string, GameObject>();

        private readonly HashSet<string> _placedDoorPairs = new HashSet<string>();

        // An opening carved in a room's exterior wall for a building entry point,
        // plus the door placed there. Keyed by room id.
        private struct EntryOpening
        {
            public WallSide side;
            public Vector3  position;
            public string   id;
        }

        private readonly Dictionary<string, List<EntryOpening>> _entryOpenings =
            new Dictionary<string, List<EntryOpening>>();

        private NavMeshSurface _navMeshSurface;
        private GameObject _safeZone;   // visible extraction point spawned at the trainee start

        // ── Public methods ───────────────────────────────────────────────────

        /// <summary>
        /// Loads a Scenario.json file from disk and builds the scene from it.
        /// </summary>
        /// <param name="scenarioJsonPath">Absolute or project-relative path
        /// to a previously generated Scenario.json.</param>
        public void BuildSceneFromFile(string scenarioJsonPath)
        {
            if (string.IsNullOrWhiteSpace(scenarioJsonPath))
            {
                Fail("BuildSceneFromFile: path is null or empty.");
                return;
            }

            ScenarioData scenario;
            try
            {
                scenario = ScenarioExporter.LoadFromFile(scenarioJsonPath);
            }
            catch (Exception ex)
            {
                Fail($"BuildSceneFromFile: failed to load '{scenarioJsonPath}': {ex.Message}");
                return;
            }

            BuildScene(scenario);
        }

        /// <summary>
        /// Main entry point. Clears any previous scene objects, instantiates
        /// every room/door/NPC defined by <paramref name="scenario"/>,
        /// repositions the trainee rig, bakes the NavMesh, and finally raises
        /// <see cref="EventManager.NotifyScenarioReady"/> to start Module 2.
        /// </summary>
        /// <param name="scenario">The generated scenario data. Must be non-null
        /// and have a populated <see cref="LayoutData"/>.</param>
        public void BuildScene(ScenarioData scenario)
        {
            if (scenario == null)
            {
                Fail("BuildScene: scenario is null.");
                return;
            }

            if (scenario.layout == null || scenario.layout.rooms == null)
            {
                Fail("BuildScene: scenario.layout / scenario.layout.rooms is null.");
                return;
            }

            try
            {
                ClearScene();
                EnsureContainers();

                ActiveScenario = scenario;
                _buildOffset = buildAnchor != null ? buildAnchor.position : buildOffset;

                // Shift the whole layout off any existing base-map geometry BEFORE we
                // build, so every World()-mapped position (rooms, doors, NPCs, trainee,
                // NavMesh bake) inherits the cleared offset and the two never overlap.
                ResolveBaseMapClearance(scenario);

                // Doors record themselves as they're placed; BuildDoorNavLinks links them
                // after the bake. Reset per build so a rebuild doesn't re-link stale doors.
                _placedDoors.Clear();

                // Resolve a guaranteed exterior entrance now (needs the final build
                // offset from ResolveBaseMapClearance) so the wall carving, corridor
                // door, and trainee spawn that follow all agree on the same opening.
                ResolveEntrance(scenario);

                MeasureDoorPrefab();
                ComputeEntryOpenings(scenario);
                BuildRooms(scenario);
                BuildDoors(scenario);
                BuildEntryDoors(scenario);
                BuildPerimeterCorridor(scenario);
                // Module 1's furniture placer keeps clear of every door it KNOWS
                // about (room-to-room doors + layout entry points), but the entry
                // and perimeter-corridor doors above are invented by SceneBuilder
                // itself and never appear in scenario.layout — the generator has no
                // way to avoid them. Sweep every doorway now that _placedDoors holds
                // the true, final set (all three door-building calls above included)
                // and clear out anything left sitting in the opening.
                ClearFurnitureBlockingDoors();
                PositionTrainee(scenario);
                CreateSafeZone(scenario);
                // Bake BEFORE spawning NPCs: NavMeshAgent attaches to the mesh in
                // OnEnable, so spawning first throws "Failed to create agent because
                // it is not close enough to the NavMesh" and leaves agents dead
                // (NPCs can shoot but never walk).
                BakeNavMesh();
                BuildDoorNavLinks(scenario);
                LogNavMeshConnectivity(scenario);
                SpawnHostages(scenario);
                SpawnTerrorists(scenario);
                // Hide the base-map template last: the weapon / staging-table
                // moves above pull the kept objects out of harm's way first.
                HideTemplateContent();

                OnSceneBuildComplete?.Invoke(scenario);

                NotifyModule2Ready();

                Debug.Log($"[SceneBuilder] Scene built for scenario '{scenario.scenarioId}': " +
                          $"{_roomObjects.Count} rooms, " +
                          $"{(scenario.spawnPoints?.terrorists?.Count ?? 0)} terrorists, " +
                          $"{(scenario.spawnPoints?.hostages?.Count ?? 0)} hostages.");
            }
            catch (Exception ex)
            {
                Fail($"BuildScene threw: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Destroys every spawned room, door, and NPC, resets the trainee rig
        /// to the world origin, and clears <see cref="ActiveScenario"/>.
        /// Call before regenerating into the same scene.
        /// </summary>
        public void ClearScene()
        {
            ShowTemplateContent();

            DestroyChildren(_roomsRoot);
            DestroyChildren(_doorsRoot);
            DestroyChildren(_npcsRoot);

            if (_safeZone != null)
            {
                if (Application.isPlaying) Destroy(_safeZone); else DestroyImmediate(_safeZone);
                _safeZone = null;
            }

            _roomObjects.Clear();
            _placedDoorPairs.Clear();
            _entryOpenings.Clear();

            if (traineeRig != null)
            {
                traineeRig.position = Vector3.zero;
                traineeRig.rotation = Quaternion.identity;
            }

            ActiveScenario = null;
        }

        // ── Template cleanup ─────────────────────────────────────────────────

        // Root GameObjects whose name contains one of these (case-insensitive)
        // are XRI Starter Kit demo content. Mirrors the editor-only
        // TemplateContentToggle so runtime builds hide the same set.
        private static readonly string[] TemplateRootContains =
        {
            "INTERTABLES",
            "MINI GAMES",
            "ENVIRONMENT",
            "[LookAnchor]",
            "HandPoseReferenceTool",
            "CoverPoint",
        };

        // Exact name match (case-insensitive) for short generic names where a
        // substring match would be too broad.
        private static readonly string[] TemplateRootExact =
        {
            "Cube",
            "-------- NPC",
        };

        // Everything HideTemplateContent() deactivated, so ShowTemplateContent()
        // can restore exactly that set (the hide skips subtrees holding the
        // trainee weapon / staging table, so whole roots can't just be toggled).
        private readonly List<GameObject> _hiddenTemplateObjects = new List<GameObject>();

        /// <summary>
        /// Deactivates the XRI Starter Kit's demo content (base-map building,
        /// mini games, interaction tables, demo NPCs) so only the generated
        /// mission stays visible. The trainee weapon and staging-table roots
        /// are kept active even though they live inside that content — they
        /// have already been moved to the trainee's spawn by this point.
        /// </summary>
        public void HideTemplateContent()
        {
            if (!hideTemplateAfterBuild) return;

            var kept = new List<Transform>();
            if (traineeWeapon != null) kept.Add(traineeWeapon);
            if (stagingTableRoots != null)
                foreach (Transform t in stagingTableRoots)
                    if (t != null) kept.Add(t);

            int before = _hiddenTemplateObjects.Count;
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                if (root == null || !IsTemplateRoot(root.name)) continue;

                // The scene's global lighting rig lives INSIDE the template
                // hierarchy (-------- ENVIRONMENT/LightAndReflectionProbes):
                // hiding the sun/fill directional lights and the probes drops
                // the whole world to flat ambient grey, so keep them lit.
                foreach (Light l in root.GetComponentsInChildren<Light>(true))
                    if (l.type == LightType.Directional) kept.Add(l.transform);
                foreach (ReflectionProbe p in root.GetComponentsInChildren<ReflectionProbe>(true))
                    kept.Add(p.transform);
                foreach (LightProbeGroup g in root.GetComponentsInChildren<LightProbeGroup>(true))
                    kept.Add(g.transform);

                HideSubtreeExceptKept(root.transform, kept);
            }

            int hidden = _hiddenTemplateObjects.Count - before;
            if (hidden > 0)
                Debug.Log($"[SceneBuilder] Hid {hidden} base-map template GameObject(s).");
        }

        /// <summary>Re-activates everything <see cref="HideTemplateContent"/>
        /// hid. Called from <see cref="ClearScene"/> so a reset (and the start
        /// of every rebuild) returns the scene to its pristine state.</summary>
        public void ShowTemplateContent()
        {
            foreach (GameObject go in _hiddenTemplateObjects)
                if (go != null) go.SetActive(true);
            _hiddenTemplateObjects.Clear();
        }

        /// <summary>Deactivates <paramref name="node"/>'s subtree, but where a
        /// kept transform lives inside it, recurses instead so only the kept
        /// object's non-ancestor siblings are hidden.</summary>
        private void HideSubtreeExceptKept(Transform node, List<Transform> kept)
        {
            foreach (Transform k in kept)
                if (k == node) return;   // the kept object itself stays active

            bool containsKept = false;
            foreach (Transform k in kept)
                if (k.IsChildOf(node)) { containsKept = true; break; }

            if (!containsKept)
            {
                if (node.gameObject.activeSelf)
                {
                    node.gameObject.SetActive(false);
                    _hiddenTemplateObjects.Add(node.gameObject);
                }
                return;
            }

            foreach (Transform child in node)
                HideSubtreeExceptKept(child, kept);
        }

        private static bool IsTemplateRoot(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            foreach (string p in TemplateRootContains)
                if (name.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            foreach (string p in TemplateRootExact)
                if (string.Equals(name.Trim(), p, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        // ── Build pipeline ───────────────────────────────────────────────────

        private void EnsureContainers()
        {
            _roomsRoot     = GetOrCreateChild(ROOMS_ROOT);
            _doorsRoot     = GetOrCreateChild(DOORS_ROOT);
            _npcsRoot      = GetOrCreateChild(NPCS_ROOT);
            _corridorRoot  = GetOrCreateChild(CORRIDOR_ROOT);

            // The entrance door for this build hasn't been placed yet.
            _hasEntryDoorWorld = false;
        }

        private void BuildRooms(ScenarioData scenario)
        {
            RoomSizeCategory sizeCategory =
                scenario.layout.layoutMetadata?.roomSizeCategory ?? RoomSizeCategory.Medium;

            foreach (RoomData room in scenario.layout.rooms)
            {
                GameObject prefab = SelectRoomPrefab(sizeCategory);
                if (prefab == null)
                {
                    Debug.LogError($"[SceneBuilder] No room prefab assigned for size " +
                                   $"'{sizeCategory}' (room '{room.id}'). Skipping.");
                    continue;
                }

                GameObject go = Instantiate(prefab,
                                            World(room.position.ToVector3()),
                                            Quaternion.identity,
                                            _roomsRoot);
                go.name = room.id;
                _roomObjects[room.id] = go;

                BuildWalls(room, go, scenario.layout.rooms);
                BuildFurniture(room, go);
            }
        }

        // ── Furniture ────────────────────────────────────────────────────────

        /// <summary>
        /// Instantiates every <see cref="FurnitureData"/> item Module 1 placed in
        /// the room. Items are parented under the room object (a "Furniture" child)
        /// so they are voxelised in the room's NavMesh bake and become obstacles
        /// NPCs path around. Each item is a greybox box scaled to the generated
        /// footprint, unless a prefab is mapped for its type, in which case the
        /// prefab is instantiated and scaled to the same footprint so the layout
        /// stays collision-correct. Positions/rotations come straight from the data,
        /// so the rendered scene matches the seed-reproducible plan exactly.
        /// </summary>
        private void BuildFurniture(RoomData room, GameObject roomGo)
        {
            if (!addFurniture || room.furniture == null || room.furniture.Count == 0) return;

            var container = new GameObject("Furniture");
            container.transform.SetParent(roomGo.transform, worldPositionStays: false);

            Vector3 roomCentre = room.position.ToVector3();

            foreach (FurnitureData f in room.furniture)
            {
                if (f?.size == null || f.position == null) continue;

                Vector3 size = f.size.ToVector3();
                if (size.x <= 0f || size.y <= 0f || size.z <= 0f) continue;

                // Room-local position: the item's floor-plane centre relative to the
                // room centre (both in layout space), raised so the box rests on the
                // floor. Mirrors how the procedural walls are parented room-locally.
                Vector3 local = f.position.ToVector3() - roomCentre;
                local.y = size.y * 0.5f;

                Quaternion rot = Quaternion.Euler(0f, f.rotationY, 0f);

                GameObject prefab = ResolveFurniturePrefab(f.type, f.id);
                if (prefab != null)
                    BuildFurnitureFromPrefab(container.transform, prefab, f, rot, size);
                else
                    BuildFurnitureGreybox(container.transform, f, local, rot, size);
            }
        }

        /// <summary>
        /// Removes any furniture item left standing in a doorway. Module 1's
        /// FurniturePlacer keeps clear of every door it knows about at generation
        /// time (room-to-room doors + scenario.layout.entryPoints), but the main
        /// entrance and perimeter-corridor doors are invented afterward, purely on
        /// the Unity side (BuildEntryDoors / BuildPerimeterCorridor) — the generator
        /// has no way to avoid them, and occasionally a table/shelf/crate ends up
        /// sitting right in one of those openings. Called once _placedDoors holds
        /// every door actually built (room + entry + corridor), so this is a
        /// ground-truth sweep rather than a second guess at door positions.
        /// </summary>
        private void ClearFurnitureBlockingDoors()
        {
            if (_placedDoors.Count == 0 || _roomObjects.Count == 0) return;

            const float clearance = 1.4f; // keep the opening + door swing clear
            int removed = 0;

            foreach (KeyValuePair<string, GameObject> kvp in _roomObjects)
            {
                if (kvp.Value == null) continue;
                Transform furnitureRoot = kvp.Value.transform.Find("Furniture");
                if (furnitureRoot == null) continue;

                for (int i = furnitureRoot.childCount - 1; i >= 0; i--)
                {
                    Transform item = furnitureRoot.GetChild(i);
                    Bounds bounds = WorldBoundsOf(item);

                    bool blocking = false;
                    foreach (PlacedDoor door in _placedDoors)
                    {
                        Vector3 doorPoint = door.center;
                        doorPoint.y = bounds.center.y;
                        if (Vector3.Distance(bounds.ClosestPoint(doorPoint), doorPoint) < clearance)
                        {
                            blocking = true;
                            break;
                        }
                    }

                    if (blocking)
                    {
                        removed++;
                        if (Application.isPlaying) Destroy(item.gameObject);
                        else DestroyImmediate(item.gameObject);
                    }
                }
            }

            if (removed > 0)
                Debug.Log($"[SceneBuilder] Removed {removed} furniture item(s) blocking a doorway " +
                          "(entrance/corridor doors aren't known to Module 1's furniture generator).");
        }

        /// <summary>World-space AABB of every renderer under <paramref name="root"/>,
        /// falling back to a small box at its position if it has none.</summary>
        private static Bounds WorldBoundsOf(Transform root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(root.position, Vector3.one * 0.3f);

            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }

        /// <summary>
        /// Greybox item: a small composition of primitives shaped like the furniture
        /// type (e.g. a tabletop on four legs, a chair with a back, open shelving)
        /// rather than a featureless block, so rooms read believably without relying
        /// on any imported art/materials. Built in the item's local frame — X = width,
        /// Y = height from the floor, Z = depth — under a root placed at the planned
        /// footprint centre and turned to face into the room. Every part is a solid
        /// primitive with a collider, so the whole item still blocks the trainee and
        /// bakes into the NavMesh as an obstacle.
        /// </summary>
        private void BuildFurnitureGreybox(Transform parent, FurnitureData f,
                                           Vector3 local, Quaternion rot, Vector3 size)
        {
            var root = new GameObject(f.id);
            root.transform.SetParent(parent, worldPositionStays: false);
            root.transform.localRotation = rot;
            root.transform.localPosition = new Vector3(local.x, 0f, local.z); // sit on floor

            Material mat = ResolveFurnitureMaterial();
            BuildFurnitureShape(root.transform, f.type, size, mat);
        }

        /// <summary>Dispatches to a per-type primitive composition. Falls back to a
        /// plain box for any type without a bespoke shape.</summary>
        private void BuildFurnitureShape(Transform root, FurnitureType type, Vector3 s, Material mat)
        {
            switch (type)
            {
                case FurnitureType.Table:
                case FurnitureType.Desk:
                case FurnitureType.SideTable:
                    BuildTableShape(root, s, mat); break;
                case FurnitureType.Chair:
                    BuildSeatShape(root, s, mat, withBack: true); break;
                case FurnitureType.Stool:
                    BuildSeatShape(root, s, mat, withBack: false); break;
                case FurnitureType.Shelf:
                case FurnitureType.Bookshelf:
                    BuildShelfShape(root, s, mat); break;
                case FurnitureType.Cabinet:
                case FurnitureType.Locker:
                    BuildCabinetShape(root, s, mat); break;
                case FurnitureType.Bed:
                    BuildBedShape(root, s, mat); break;
                case FurnitureType.Sofa:
                    BuildSofaShape(root, s, mat); break;
                case FurnitureType.Barrel:
                    BuildBarrelShape(root, s, mat); break;
                case FurnitureType.Crate:
                default:
                    AddPrim(root, PrimitiveType.Cube, new Vector3(0f, s.y * 0.5f, 0f), s, mat); break;
            }
        }

        private void BuildTableShape(Transform root, Vector3 s, Material mat)
        {
            float top = Mathf.Clamp(s.y * 0.12f, 0.04f, 0.1f);
            float legT = Mathf.Clamp(Mathf.Min(s.x, s.z) * 0.12f, 0.05f, 0.12f);
            float legH = s.y - top;
            AddPrim(root, PrimitiveType.Cube, new Vector3(0f, s.y - top * 0.5f, 0f),
                    new Vector3(s.x, top, s.z), mat);
            float lx = s.x * 0.5f - legT * 0.5f, lz = s.z * 0.5f - legT * 0.5f;
            AddLegs(root, lx, lz, legH, legT, mat);
        }

        private void BuildSeatShape(Transform root, Vector3 s, Material mat, bool withBack)
        {
            float seatH = Mathf.Clamp(s.y * (withBack ? 0.5f : 0.9f), 0.28f, 0.5f);
            float seatT = 0.07f;
            float legT = Mathf.Clamp(Mathf.Min(s.x, s.z) * 0.14f, 0.04f, 0.1f);
            AddPrim(root, PrimitiveType.Cube, new Vector3(0f, seatH, 0f),
                    new Vector3(s.x, seatT, s.z), mat);
            float lx = s.x * 0.5f - legT * 0.5f, lz = s.z * 0.5f - legT * 0.5f;
            AddLegs(root, lx, lz, seatH - seatT * 0.5f, legT, mat);
            if (withBack && s.y > seatH + 0.15f)
            {
                float backT = 0.06f;
                AddPrim(root, PrimitiveType.Cube,
                        new Vector3(0f, seatH + (s.y - seatH) * 0.5f, -(s.z * 0.5f - backT * 0.5f)),
                        new Vector3(s.x, s.y - seatH, backT), mat);
            }
        }

        private void AddLegs(Transform root, float lx, float lz, float legH, float legT, Material mat)
        {
            if (legH <= 0.01f) return;
            var leg = new Vector3(legT, legH, legT);
            AddPrim(root, PrimitiveType.Cube, new Vector3(-lx, legH * 0.5f, -lz), leg, mat);
            AddPrim(root, PrimitiveType.Cube, new Vector3( lx, legH * 0.5f, -lz), leg, mat);
            AddPrim(root, PrimitiveType.Cube, new Vector3(-lx, legH * 0.5f,  lz), leg, mat);
            AddPrim(root, PrimitiveType.Cube, new Vector3( lx, legH * 0.5f,  lz), leg, mat);
        }

        private void BuildShelfShape(Transform root, Vector3 s, Material mat)
        {
            float panel = Mathf.Clamp(Mathf.Min(s.x, s.z) * 0.1f, 0.04f, 0.08f);
            // Back and two sides.
            AddPrim(root, PrimitiveType.Cube, new Vector3(0f, s.y * 0.5f, -(s.z * 0.5f - panel * 0.5f)),
                    new Vector3(s.x, s.y, panel), mat);
            AddPrim(root, PrimitiveType.Cube, new Vector3(-(s.x * 0.5f - panel * 0.5f), s.y * 0.5f, 0f),
                    new Vector3(panel, s.y, s.z), mat);
            AddPrim(root, PrimitiveType.Cube, new Vector3( s.x * 0.5f - panel * 0.5f, s.y * 0.5f, 0f),
                    new Vector3(panel, s.y, s.z), mat);
            // Horizontal shelves (incl. top and bottom).
            int levels = Mathf.Clamp(Mathf.RoundToInt(s.y / 0.4f), 2, 5);
            for (int i = 0; i <= levels; i++)
            {
                float y = Mathf.Lerp(0.02f, s.y - 0.02f, i / (float)levels);
                AddPrim(root, PrimitiveType.Cube, new Vector3(0f, y, 0f),
                        new Vector3(s.x - panel, 0.04f, s.z - panel), mat);
            }
        }

        private void BuildCabinetShape(Transform root, Vector3 s, Material mat)
        {
            // Solid body on a slight plinth with a small top overhang for shape.
            float plinth = Mathf.Min(0.08f, s.y * 0.1f);
            AddPrim(root, PrimitiveType.Cube, new Vector3(0f, plinth * 0.5f, 0f),
                    new Vector3(s.x * 0.96f, plinth, s.z * 0.9f), mat);
            AddPrim(root, PrimitiveType.Cube, new Vector3(0f, plinth + (s.y - plinth) * 0.5f, 0f),
                    new Vector3(s.x, s.y - plinth, s.z), mat);
        }

        private void BuildBedShape(Transform root, Vector3 s, Material mat)
        {
            float frame = Mathf.Min(0.3f, s.y * 0.55f);
            AddPrim(root, PrimitiveType.Cube, new Vector3(0f, frame * 0.5f, 0f),
                    new Vector3(s.x, frame, s.z), mat);                                   // base
            AddPrim(root, PrimitiveType.Cube, new Vector3(0f, frame + (s.y - frame) * 0.5f, 0f),
                    new Vector3(s.x * 0.98f, s.y - frame, s.z * 0.96f), mat);             // mattress
            AddPrim(root, PrimitiveType.Cube,
                    new Vector3(0f, s.y + 0.04f, -(s.z * 0.5f - s.z * 0.16f)),
                    new Vector3(s.x * 0.5f, 0.08f, s.z * 0.22f), mat);                    // pillow
        }

        private void BuildSofaShape(Transform root, Vector3 s, Material mat)
        {
            float baseH = s.y * 0.5f;
            float arm = Mathf.Clamp(s.x * 0.12f, 0.12f, 0.25f);
            float backT = Mathf.Clamp(s.z * 0.2f, 0.12f, 0.25f);
            AddPrim(root, PrimitiveType.Cube, new Vector3(0f, baseH * 0.5f, 0f),
                    new Vector3(s.x, baseH, s.z), mat);                                   // seat base
            AddPrim(root, PrimitiveType.Cube, new Vector3(0f, baseH + (s.y - baseH) * 0.5f, -(s.z * 0.5f - backT * 0.5f)),
                    new Vector3(s.x, s.y - baseH, backT), mat);                           // backrest
            AddPrim(root, PrimitiveType.Cube, new Vector3(-(s.x * 0.5f - arm * 0.5f), s.y * 0.45f, 0f),
                    new Vector3(arm, s.y * 0.9f, s.z), mat);                              // left arm
            AddPrim(root, PrimitiveType.Cube, new Vector3( s.x * 0.5f - arm * 0.5f, s.y * 0.45f, 0f),
                    new Vector3(arm, s.y * 0.9f, s.z), mat);                              // right arm
        }

        private void BuildBarrelShape(Transform root, Vector3 s, Material mat)
        {
            float dia = Mathf.Min(s.x, s.z);
            // Unity's default cylinder is 2 units tall and 1 wide, so scale Y by h/2.
            AddPrim(root, PrimitiveType.Cylinder, new Vector3(0f, s.y * 0.5f, 0f),
                    new Vector3(dia, s.y * 0.5f, dia), mat);
        }

        /// <summary>Adds one primitive part in the item's local frame.</summary>
        private void AddPrim(Transform root, PrimitiveType prim, Vector3 center, Vector3 scale, Material mat)
        {
            scale = new Vector3(Mathf.Max(0.01f, scale.x), Mathf.Max(0.01f, scale.y), Mathf.Max(0.01f, scale.z));
            var go = GameObject.CreatePrimitive(prim);
            go.transform.SetParent(root, worldPositionStays: false);
            go.transform.localPosition = center;
            go.transform.localScale    = scale;
            if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        /// <summary>Prefab item: instantiate, scale UNIFORMLY so the model fits
        /// inside the reserved footprint without distorting its proportions, turn it
        /// to face into the room, then drop it so it rests on the floor with its
        /// footprint centred on the planned position. Works in world space via the
        /// live renderer bounds, so any pivot / import scale / internal offset in the
        /// source art is handled automatically. Falls back to a greybox box if the
        /// prefab has no measurable renderers.</summary>
        private void BuildFurnitureFromPrefab(Transform parent, GameObject prefab, FurnitureData f,
                                              Quaternion rot, Vector3 size)
        {
            GameObject go = Instantiate(prefab, parent);
            go.name = f.id;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale    = prefab.transform.localScale;

            // Natural (unrotated) world size of the art as authored.
            if (!TryGetWorldBounds(go, out Bounds natural) ||
                natural.size.x <= 1e-4f || natural.size.z <= 1e-4f)
            {
                if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
                Vector3 local = World(f.position.ToVector3()) - parent.position;
                local.y = size.y * 0.5f;
                BuildFurnitureGreybox(parent, f, local, rot, size);
                return;
            }

            // Uniform fit: shrink (never stretch a single axis) so the model's
            // footprint fits within the reserved width×depth. Cap at 1 so small
            // props keep their real size rather than being blown up to fill a box.
            float fit = Mathf.Min(size.x / natural.size.x, size.z / natural.size.z);
            fit = Mathf.Min(fit, 1f);
            go.transform.localScale = prefab.transform.localScale * fit;

            // Face into the room, then re-measure the placed bounds.
            go.transform.localRotation = rot;
            if (!TryGetWorldBounds(go, out Bounds placed)) placed = natural;

            // Target: footprint centred on the planned world position, base on floor.
            Vector3 targetWorld = World(f.position.ToVector3());
            float floorY = parent.position.y;

            Vector3 delta = new Vector3(
                targetWorld.x - placed.center.x,
                floorY        - placed.min.y,
                targetWorld.z - placed.center.z);
            go.transform.position += delta;

            EnsureFurnitureCollider(go);
        }

        /// <summary>
        /// Resolves the prefab to spawn for a furniture item. Priority: (1) an
        /// explicit inspector mapping; (2) automatic discovery from the project's
        /// prop packs in the editor, choosing a variant deterministically from the
        /// item id so the same seed always yields the same look; (3) null → the
        /// caller draws a greybox box (device builds with no mapping assigned).
        /// </summary>
        private GameObject ResolveFurniturePrefab(FurnitureType type, string id)
        {
            // An explicit inspector mapping always wins (assumed URP-ready).
            if (furniturePrefabs != null)
                foreach (FurniturePrefabMapping m in furniturePrefabs)
                    if (m != null && m.prefab != null && m.type == type) return m.prefab;

            // Auto-discovery is opt-in: the bundled packs are Built-in-RP and would
            // render magenta under URP. Off ⇒ caller draws a shaped greybox instead.
            if (!useFurniturePrefabs) return null;

#if UNITY_EDITOR
            GameObject[] variants = GetAutoVariants(type);
            if (variants != null && variants.Length > 0)
                return variants[(int)(StableHash(id) % (uint)variants.Length)];
#endif
            return null;
        }

        /// <summary>Combined world-space renderer bounds of a hierarchy (skips
        /// particle renderers). Reflects live pivot, scale and rotation.</summary>
        private static bool TryGetWorldBounds(GameObject go, out Bounds bounds)
        {
            bounds = default;
            bool has = false;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer) continue;
                if (!has) { bounds = r.bounds; has = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return has;
        }

        /// <summary>Adds a footprint-sized box collider when the art ships without
        /// one, so the trainee can't walk through furniture. Placed on an unrotated
        /// child aligned to the world AABB (kept out of the way of any existing
        /// interaction colliders on the prefab).</summary>
        private static void EnsureFurnitureCollider(GameObject go)
        {
            if (go.GetComponentInChildren<Collider>() != null) return;
            if (!TryGetWorldBounds(go, out Bounds b)) return;

            var colGo = new GameObject("FurnitureCollider");
            colGo.transform.SetParent(go.transform.parent, worldPositionStays: true);
            colGo.transform.position = b.center;
            var bc = colGo.AddComponent<BoxCollider>();
            bc.size = b.size;
        }

        /// <summary>Stable FNV-1a hash so variant choice is reproducible across
        /// sessions (unlike <see cref="string.GetHashCode"/>, which is randomised).</summary>
        private static uint StableHash(string s)
        {
            uint hash = 2166136261u;
            if (s != null)
                foreach (char c in s) { hash ^= c; hash *= 16777619u; }
            return hash;
        }

#if UNITY_EDITOR
        // ── Automatic furniture prefab discovery (editor only) ───────────────
        // Maps each furniture type to the prop-prefab name prefixes to pull from
        // the project's art packs, so real furniture appears with no manual wiring.
        // Every matching variant is used, chosen deterministically per item id.
        private const string FurniturePrefabFolder =
            "Assets/XRI Starter Kit/Assets/PandazoleHome/Prefabs";

        private static readonly Dictionary<FurnitureType, string[]> AutoPrefabPrefixes =
            new Dictionary<FurnitureType, string[]>
            {
                { FurnitureType.Table,     new[] { "Prop_Table_" } },
                { FurnitureType.Desk,      new[] { "Prop_Desk_" } },
                { FurnitureType.Chair,     new[] { "Prop_Chair_" } },
                { FurnitureType.Crate,     new[] { "Prop_SmallStorageBox_" } },
                { FurnitureType.Barrel,    new[] { "Prop_SmallStorageBox_" } },
                { FurnitureType.Shelf,     new[] { "Prop_KitchenShelf_" } },
                { FurnitureType.Cabinet,   new[] { "Prop_Cabinet_" } },
                { FurnitureType.Bookshelf, new[] { "Prop_Cabinet_" } },
                { FurnitureType.Bed,       new[] { "Prop_Bed_" } },
                { FurnitureType.Sofa,      new[] { "Prop_Sofa_01", "Prop_Sofa_04" } },
                { FurnitureType.Locker,    new[] { "Prop_Wardrobe_" } },
                { FurnitureType.SideTable, new[] { "Prop_KidsTable" } },
                { FurnitureType.Stool,     new[] { "Prop_Chair_" } },
            };

        // Every prefab under the art folder, loaded and name-sorted once per build.
        private List<GameObject> _furniturePrefabCatalog;
        private Dictionary<FurnitureType, GameObject[]> _autoVariantCache;

        private GameObject[] GetAutoVariants(FurnitureType type)
        {
            _autoVariantCache ??= new Dictionary<FurnitureType, GameObject[]>();
            if (_autoVariantCache.TryGetValue(type, out GameObject[] cached)) return cached;

            EnsureFurniturePrefabCatalog();
            if (!AutoPrefabPrefixes.TryGetValue(type, out string[] prefixes))
                return _autoVariantCache[type] = System.Array.Empty<GameObject>();

            var matches = new List<GameObject>();
            foreach (GameObject go in _furniturePrefabCatalog)
                foreach (string prefix in prefixes)
                    if (go.name.StartsWith(prefix, System.StringComparison.Ordinal))
                    {
                        matches.Add(go);
                        break;
                    }

            // Sort by name so the deterministic per-id index maps to a stable variant.
            matches.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return _autoVariantCache[type] = matches.ToArray();
        }

        private void EnsureFurniturePrefabCatalog()
        {
            if (_furniturePrefabCatalog != null) return;
            _furniturePrefabCatalog = new List<GameObject>();

            if (!UnityEditor.AssetDatabase.IsValidFolder(FurniturePrefabFolder))
            {
                Debug.LogWarning($"[SceneBuilder] Furniture prefab folder not found " +
                                 $"('{FurniturePrefabFolder}'); furniture will render as greyboxes. " +
                                 $"Assign prefabs on the Furniture list to override.");
                return;
            }

            string[] guids = UnityEditor.AssetDatabase.FindAssets(
                "t:Prefab", new[] { FurniturePrefabFolder });
            foreach (string guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var go = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go != null) _furniturePrefabCatalog.Add(go);
            }
        }
#endif

        // Lazily-created neutral material so greybox furniture reads distinctly
        // from the walls without requiring the evaluator to assign one.
        private Material _furnitureMatCache;

        private Material ResolveFurnitureMaterial()
        {
            if (furnitureMaterial != null) return furnitureMaterial;
            if (_furnitureMatCache != null) return _furnitureMatCache;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) return null;

            _furnitureMatCache = new Material(shader) { name = "GeneratedFurnitureMat" };
            // Warm neutral tone, clearly different from typical wall greys. Set both
            // the built-in and URP colour properties so it tints under either pipeline.
            var tint = new Color(0.55f, 0.42f, 0.30f, 1f);
            _furnitureMatCache.color = tint;
            if (_furnitureMatCache.HasProperty("_BaseColor"))
                _furnitureMatCache.SetColor("_BaseColor", tint);
            return _furnitureMatCache;
        }

        /// <summary>
        /// Procedurally builds the four walls of a room from its door data. A
        /// wall side that carries a door gets two segments framing a 2 m opening;
        /// a side with no door gets a single solid wall. This makes the geometry
        /// match connectivity exactly — no more open holes on unconnected walls,
        /// and the building perimeter is fully enclosed.
        /// </summary>
        private void BuildWalls(RoomData room, GameObject roomGo, List<RoomData> allRooms)
        {
            Material mat = ResolveWallMaterial(roomGo);

            float height = room.size != null && room.size.height > 0f ? room.size.height : 3f;
            float width  = room.size != null && room.size.width  > 0f ? room.size.width  : 6f;
            float depth  = room.size != null && room.size.depth  > 0f ? room.size.depth  : 6f;

            // Outer extents: nominal size extended by half the corridor gap on
            // each side, so adjacent rooms' walls meet on the shared plane.
            float halfW = (width + CorridorGap) * 0.5f;
            float halfD = (depth + CorridorGap) * 0.5f;

            // Carve openings sized to the measured door so the real leaf fits.
            float openW = _doorOpeningWidth  + DoorClearance;
            float openH = _doorOpeningHeight + DoorClearance;

            // Per-side carve of the door opening: the lateral offset along the wall
            // AND the perpendicular plane the wall must be built at. Module 1 jogs
            // doors sideways off the wall midpoint (sightline control) and jitters
            // room positions at high randomness, so a shared wall's true plane is
            // the door midpoint between the two rooms — building it at this room's
            // own outer extent leaves the leaf floating between two offset wall
            // planes, visibly detached and scraping the misaligned openings.
            // Doors that cannot lie on this wall at all (loop/cross edges between
            // rooms the BFS never placed adjacent) are ignored: the wall stays
            // solid here and BuildDoors skips their leaf, keeping the room sealed.
            var doorOffsets = new Dictionary<WallSide, float>();
            var doorPlanes  = new Dictionary<WallSide, float>();
            if (room.doors != null)
                foreach (DoorData d in room.doors)
                    if (!doorOffsets.ContainsKey(d.wallSide) &&
                        DoorLiesOnWall(room, d.wallSide, d.position.ToVector3(), openW,
                                       out float lat, out float plane))
                    {
                        doorOffsets[d.wallSide] = lat;
                        doorPlanes[d.wallSide]  = plane;
                    }

            // Exterior building entrances also need an opening in the perimeter
            // wall, otherwise the trainee spawns outside a sealed building. They
            // sit on the room's own outer extent (nothing beyond to share with),
            // so only the lateral offset is needed.
            if (_entryOpenings.TryGetValue(room.id, out List<EntryOpening> openings))
                foreach (EntryOpening e in openings)
                    doorOffsets[e.side] =
                        OpeningOffset(room, e.side, e.position - _buildOffset, openW);

            float OffsetOn(WallSide side) =>
                doorOffsets.TryGetValue(side, out float o) ? o : 0f;
            float PlaneOn(WallSide side, float fallback) =>
                doorPlanes.TryGetValue(side, out float p) ? p : fallback;

            // Wall planes (room-local). A side carrying an interior door follows
            // the door's plane; every other side sits on the room's outer extent.
            // Each wall runs plane-to-plane so the corners stay sealed even when a
            // door plane pulls a wall off the nominal rectangle.
            float southPlane = PlaneOn(WallSide.South, -halfD);
            float northPlane = PlaneOn(WallSide.North,  halfD);
            float westPlane  = PlaneOn(WallSide.West,  -halfW);
            float eastPlane  = PlaneOn(WallSide.East,   halfW);

            // A solid wall gets a window when it faces the outside (no adjacent
            // room) - interior partitions between rooms stay solid.
            bool WindowOn(WallSide side) =>
                addWindows && !doorOffsets.ContainsKey(side) && IsExteriorWall(room, side, allRooms);

            // North / South run along X (thin in Z).
            BuildWallSide(roomGo, "South", doorOffsets.ContainsKey(WallSide.South), WindowOn(WallSide.South),
                          axisAlongX: true, fixedCoord: southPlane,
                          spanMin: westPlane, spanMax: eastPlane, height: height,
                          openW: openW, openH: openH, openOffset: OffsetOn(WallSide.South), mat: mat);
            BuildWallSide(roomGo, "North", doorOffsets.ContainsKey(WallSide.North), WindowOn(WallSide.North),
                          axisAlongX: true, fixedCoord: northPlane,
                          spanMin: westPlane, spanMax: eastPlane, height: height,
                          openW: openW, openH: openH, openOffset: OffsetOn(WallSide.North), mat: mat);

            // East / West run along Z (thin in X).
            BuildWallSide(roomGo, "East", doorOffsets.ContainsKey(WallSide.East), WindowOn(WallSide.East),
                          axisAlongX: false, fixedCoord: eastPlane,
                          spanMin: southPlane, spanMax: northPlane, height: height,
                          openW: openW, openH: openH, openOffset: OffsetOn(WallSide.East), mat: mat);
            BuildWallSide(roomGo, "West", doorOffsets.ContainsKey(WallSide.West), WindowOn(WallSide.West),
                          axisAlongX: false, fixedCoord: westPlane,
                          spanMin: southPlane, spanMax: northPlane, height: height,
                          openW: openW, openH: openH, openOffset: OffsetOn(WallSide.West), mat: mat);

            // Cap the room with a flat ceiling so it's enclosed top-to-bottom like
            // the hand-built base map (which uses a thin "Ceiling" slab on top of
            // the walls). Spans the actual wall planes so it still covers the room
            // when a door plane pulls a wall off the nominal rectangle.
            BuildCeiling(roomGo, westPlane, eastPlane, southPlane, northPlane, height, mat);
        }

        /// <summary>
        /// Lays a flat ceiling/roof slab across the top of a room, resting on the
        /// walls at <paramref name="height"/>. Mirrors the prefab floor: a thin
        /// flattened cube spanning the actual wall planes (room-local), so it still
        /// covers the room when a door plane pulls a wall off the nominal
        /// rectangle. Uses <see cref="ceilingMaterial"/> when assigned, otherwise
        /// the wall material so the cap visually matches the room.
        /// </summary>
        private void BuildCeiling(GameObject roomGo, float xMin, float xMax,
                                  float zMin, float zMax, float height, Material wallMat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Ceiling";
            go.transform.SetParent(roomGo.transform, worldPositionStays: false);

            // Bottom face sits on top of the walls (y = height); centre is half the
            // slab thickness above that.
            go.transform.localPosition = new Vector3((xMin + xMax) * 0.5f,
                                                     height + CeilingThickness * 0.5f,
                                                     (zMin + zMax) * 0.5f);
            go.transform.localScale    = new Vector3(xMax - xMin, CeilingThickness, zMax - zMin);

            Material mat = ceilingMaterial != null ? ceilingMaterial : wallMat;
            if (mat != null)
                go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        /// <summary>
        /// Builds one side of a room: a solid wall, two segments framing a door
        /// opening, or a wall with a glazed window punched into it.
        /// <paramref name="axisAlongX"/> selects whether the wall runs along X
        /// (north/south) or Z (east/west). A door takes priority over a window.
        /// <paramref name="openOffset"/> slides the door opening along the wall
        /// (0 = centred); the two flanking segments are sized independently so an
        /// off-centre opening is framed exactly. The wall runs from
        /// <paramref name="spanMin"/> to <paramref name="spanMax"/> (room-local) —
        /// the planes of the two perpendicular walls — so corners stay sealed even
        /// when a door plane pulls a wall off the nominal rectangle.
        /// </summary>
        private void BuildWallSide(GameObject roomGo, string sideName, bool hasDoor, bool hasWindow,
                                   bool axisAlongX, float fixedCoord, float spanMin, float spanMax,
                                   float height, float openW, float openH, float openOffset,
                                   Material mat)
        {
            if (!hasDoor)
            {
                if (hasWindow)
                    BuildWindowWall(roomGo, sideName, axisAlongX, fixedCoord, spanMin, spanMax,
                                    height, mat);
                else
                    AddWallSegment(roomGo, $"Wall_{sideName}", axisAlongX, fixedCoord,
                                   offset: (spanMin + spanMax) * 0.5f,
                                   length: spanMax - spanMin, height: height, mat: mat);
                return;
            }

            float openMin = openOffset - openW * 0.5f;
            float openMax = openOffset + openW * 0.5f;

            // Full-height segments either side of the opening. Either can be empty
            // when the opening reaches a wall end.
            float lenA = openMin - spanMin;   // spanMin → openMin
            float lenB = spanMax - openMax;   // openMax → spanMax
            if (lenA <= 0.01f && lenB <= 0.01f) return; // opening spans the wall

            if (lenA > 0.01f)
                AddWallSegment(roomGo, $"Wall_{sideName}_A", axisAlongX, fixedCoord,
                               offset: (spanMin + openMin) * 0.5f, length: lenA,
                               height: height, mat: mat);
            if (lenB > 0.01f)
                AddWallSegment(roomGo, $"Wall_{sideName}_B", axisAlongX, fixedCoord,
                               offset: (openMax + spanMax) * 0.5f, length: lenB,
                               height: height, mat: mat);

            // Header (transom) filling the wall above the door opening, so the
            // doorway isn't open all the way to the ceiling.
            float headerHeight = height - openH;
            if (headerHeight > 0.01f)
                AddWallSegment(roomGo, $"Wall_{sideName}_Header", axisAlongX, fixedCoord,
                               offset: openOffset, length: openW, height: headerHeight,
                               mat: mat, baseY: openH);
        }

        /// <summary>
        /// Lateral offset (metres, along the wall, room-local) at which the opening on
        /// <paramref name="side"/> must be carved so it lands on the door at
        /// <paramref name="layoutPos"/>. Clamped so a <paramref name="openW"/>-wide
        /// opening always keeps <see cref="MinJamb"/> of solid wall at each end — the
        /// measured door prefab can be wider than the jog Module 1 assumed.
        /// </summary>
        private static float OpeningOffset(RoomData room, WallSide side, Vector3 layoutPos, float openW)
        {
            bool alongX = side == WallSide.North || side == WallSide.South;

            float width  = room.size != null && room.size.width > 0f ? room.size.width : 6f;
            float depth  = room.size != null && room.size.depth > 0f ? room.size.depth : 6f;
            float span   = (alongX ? width : depth) + CorridorGap;

            float lateral = alongX ? layoutPos.x - room.position.x
                                   : layoutPos.z - room.position.z;

            float limit = Mathf.Max(0f, span * 0.5f - openW * 0.5f - MinJamb);
            return Mathf.Clamp(lateral, -limit, limit);
        }

        /// <summary>
        /// Decides whether a door at <paramref name="layoutPos"/> genuinely sits on
        /// this room's wall on <paramref name="side"/>, and where. On success,
        /// <paramref name="lateral"/> is the along-wall offset of the opening
        /// (clamped like <see cref="OpeningOffset"/>) and <paramref name="plane"/>
        /// is the perpendicular room-local coordinate the wall must be built at —
        /// the door midpoint between the two rooms, which under high-randomness
        /// position jitter is NOT the room's own outer extent. Returns false for
        /// doors that cannot lie on the wall at all: loop-closing / cross edges
        /// connect rooms the BFS never placed adjacent, leaving the door midpoint
        /// far off the wall plane or past the wall's end. Such doors get no
        /// geometry (wall stays solid, no leaf) instead of floating in space.
        /// </summary>
        private static bool DoorLiesOnWall(RoomData room, WallSide side, Vector3 layoutPos,
                                           float openW, out float lateral, out float plane)
        {
            bool alongX = side == WallSide.North || side == WallSide.South;

            float width = room.size != null && room.size.width > 0f ? room.size.width : 6f;
            float depth = room.size != null && room.size.depth > 0f ? room.size.depth : 6f;
            float span  = (alongX ? width : depth) + CorridorGap;                  // along the wall
            float half  = ((alongX ? depth : width) + CorridorGap) * 0.5f;         // to the wall
            float defaultPlane = side == WallSide.North || side == WallSide.East ? half : -half;

            Vector3 c = room.position.ToVector3();
            float rawLateral = alongX ? layoutPos.x - c.x : layoutPos.z - c.z;
            plane            = alongX ? layoutPos.z - c.z : layoutPos.x - c.x;

            float limit = Mathf.Max(0f, span * 0.5f - openW * 0.5f - MinJamb);
            lateral = Mathf.Clamp(rawLateral, -limit, limit);

            // Perpendicular: the door must sit on (or jitter-near) this wall's plane…
            if (Mathf.Abs(plane - defaultPlane) > DoorPlaneTolerance) return false;
            // …and laterally within the wall's run (a diagonal pair's midpoint lands
            // at the wall's very end — no opening can fit there).
            return Mathf.Abs(rawLateral) <= span * 0.5f - openW * 0.5f;
        }

        private void AddWallSegment(GameObject roomGo, string name, bool axisAlongX,
                                    float fixedCoord, float offset, float length,
                                    float height, Material mat, float baseY = 0f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(roomGo.transform, worldPositionStays: false);

            float centerY = baseY + height * 0.5f;
            if (axisAlongX)
            {
                // Runs along X at z = fixedCoord.
                go.transform.localPosition = new Vector3(offset, centerY, fixedCoord);
                go.transform.localScale    = new Vector3(length, height, WallThickness);
            }
            else
            {
                // Runs along Z at x = fixedCoord.
                go.transform.localPosition = new Vector3(fixedCoord, centerY, offset);
                go.transform.localScale    = new Vector3(WallThickness, height, length);
            }

            if (mat != null)
                go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        /// <summary>
        /// Returns the wall material: the explicit <see cref="wallMaterial"/> if
        /// assigned, otherwise the material on the room prefab's first renderer
        /// (e.g. its floor), so walls visually match the room even when the
        /// inspector slot is left empty.
        /// </summary>
        private Material ResolveWallMaterial(GameObject roomGo)
        {
            if (wallMaterial != null) return wallMaterial;
            Renderer r = roomGo.GetComponentInChildren<Renderer>();
            return r != null ? r.sharedMaterial : null;
        }

        /// <summary>
        /// Builds a solid wall with a centred window opening: a solid sill below,
        /// jambs either side, a header above, and (when <see cref="windowMaterial"/>
        /// is assigned) a thin pane filling the opening. Falls back to a plain solid
        /// wall when the opening can't fit the span or the room is too short.
        /// </summary>
        private void BuildWindowWall(GameObject roomGo, string sideName, bool axisAlongX,
                                     float fixedCoord, float spanMin, float spanMax,
                                     float height, Material mat)
        {
            float span   = spanMax - spanMin;
            float center = (spanMin + spanMax) * 0.5f;
            float winW   = Mathf.Min(WindowWidth, span - 2f * WindowSideMargin);
            float winTop = WindowSillHeight + WindowHeight;

            if (winW < 0.5f || winTop > height - 0.1f)
            {
                AddWallSegment(roomGo, $"Wall_{sideName}", axisAlongX, fixedCoord,
                               offset: center, length: span, height: height, mat: mat);
                return;
            }

            float segLen    = (span - winW) * 0.5f;
            float segOffset = winW * 0.5f + segLen * 0.5f;

            // Solid sill below the opening (full span).
            AddWallSegment(roomGo, $"Wall_{sideName}_Sill", axisAlongX, fixedCoord,
                           offset: center, length: span, height: WindowSillHeight, mat: mat);

            // Header above the opening (full span).
            float headerH = height - winTop;
            if (headerH > 0.01f)
                AddWallSegment(roomGo, $"Wall_{sideName}_Header", axisAlongX, fixedCoord,
                               offset: center, length: span, height: headerH, mat: mat, baseY: winTop);

            // Jambs either side of the opening.
            AddWallSegment(roomGo, $"Wall_{sideName}_A", axisAlongX, fixedCoord,
                           offset: center - segOffset, length: segLen, height: WindowHeight,
                           mat: mat, baseY: WindowSillHeight);
            AddWallSegment(roomGo, $"Wall_{sideName}_B", axisAlongX, fixedCoord,
                           offset: center + segOffset, length: segLen, height: WindowHeight,
                           mat: mat, baseY: WindowSillHeight);

            // Glass pane in the opening, only when a material is supplied.
            if (windowMaterial != null)
                AddWindowPane(roomGo, $"Window_{sideName}", axisAlongX, fixedCoord, center, winW);
        }

        /// <summary>
        /// Drops a thin pane into a window opening, centred on the wall span at
        /// sill height. Keeps its box collider so the trainee can't reach through.
        /// </summary>
        private void AddWindowPane(GameObject roomGo, string name, bool axisAlongX,
                                   float fixedCoord, float center, float width)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(roomGo.transform, worldPositionStays: false);

            float cy = WindowSillHeight + WindowHeight * 0.5f;
            if (axisAlongX)
            {
                go.transform.localPosition = new Vector3(center, cy, fixedCoord);
                go.transform.localScale    = new Vector3(width, WindowHeight, WindowPaneThickness);
            }
            else
            {
                go.transform.localPosition = new Vector3(fixedCoord, cy, center);
                go.transform.localScale    = new Vector3(WindowPaneThickness, WindowHeight, width);
            }

            go.GetComponent<Renderer>().sharedMaterial = windowMaterial;
        }

        /// <summary>
        /// True when the given wall side faces the outside - no other room sits
        /// directly beyond it. Probes a point just past the wall and tests it
        /// against every other room's footprint. Works in layout coordinates; the
        /// per-build offset cancels because all rooms share it.
        /// </summary>
        private static bool IsExteriorWall(RoomData room, WallSide side, List<RoomData> allRooms)
        {
            Vector3 c = room.position.ToVector3();
            float w = room.size != null && room.size.width > 0f ? room.size.width : 6f;
            float d = room.size != null && room.size.depth > 0f ? room.size.depth : 6f;
            float halfW = (w + CorridorGap) * 0.5f;
            float halfD = (d + CorridorGap) * 0.5f;

            Vector3 probe = c;
            switch (side)
            {
                case WallSide.North: probe.z += halfD + 0.5f; break;
                case WallSide.South: probe.z -= halfD + 0.5f; break;
                case WallSide.East:  probe.x += halfW + 0.5f; break;
                default:             probe.x -= halfW + 0.5f; break;
            }

            if (allRooms == null) return true;
            foreach (RoomData other in allRooms)
            {
                if (other == null || other.id == room.id) continue;
                Vector3 oc = other.position.ToVector3();
                float ow = other.size != null && other.size.width > 0f ? other.size.width : 6f;
                float od = other.size != null && other.size.depth > 0f ? other.size.depth : 6f;
                float ohW = (ow + CorridorGap) * 0.5f;
                float ohD = (od + CorridorGap) * 0.5f;
                if (Mathf.Abs(probe.x - oc.x) <= ohW && Mathf.Abs(probe.z - oc.z) <= ohD)
                    return false; // another room sits beyond this wall → interior
            }
            return true;
        }

        private void BuildDoors(ScenarioData scenario)
        {
            if (doorPrefab == null)
            {
                Debug.LogError("[SceneBuilder] doorPrefab is not assigned - skipping all doors.");
                return;
            }

            foreach (RoomData room in scenario.layout.rooms)
            {
                if (room.doors == null) continue;

                float openW = _doorOpeningWidth + DoorClearance;

                foreach (DoorData door in room.doors)
                {
                    string pairKey = MakeDoorPairKey(room.id, door.connectsToRoomId);
                    if (!_placedDoorPairs.Add(pairKey))
                        continue; // reciprocal already placed

                    // Land the leaf EXACTLY where BuildWalls carved the opening:
                    // same clamped lateral and same wall plane (under position
                    // jitter the shared plane is off this room's outer extent).
                    // Doors that lie on no wall at all — loop/cross edges between
                    // rooms the BFS never placed adjacent — get no leaf; their
                    // walls stayed solid, so nothing is left floating in space.
                    Vector3 pos = door.position.ToVector3();
                    if (!DoorLiesOnWall(room, door.wallSide, pos, openW,
                                        out float lateral, out float plane))
                    {
                        Debug.LogWarning(
                            $"[SceneBuilder] Door '{door.id}' ({room.id} → {door.connectsToRoomId}) " +
                            $"does not lie on {room.id}'s {door.wallSide} wall — the rooms were " +
                            "not placed adjacent (loop/cross edge). Skipping its leaf; the wall " +
                            "stays solid.");
                        continue;
                    }

                    if (door.wallSide == WallSide.North || door.wallSide == WallSide.South)
                    {
                        pos.x = room.position.x + lateral;
                        pos.z = room.position.z + plane;
                    }
                    else
                    {
                        pos.z = room.position.z + lateral;
                        pos.x = room.position.x + plane;
                    }

                    PlaceDoor(door.id, World(pos), door.wallSide, door.state);
                }
            }
        }

        /// <summary>
        /// Resolves a guaranteed building entrance on a TRUE perimeter room — the
        /// extreme room on one of the four axes (north-/south-/east-/west-most). The
        /// layout's entry room is the BFS origin, so in hub-and-spoke, loop and dense
        /// branching layouts it sits in the interior (or inside a courtyard/notch) with
        /// no outward face on the building boundary. A plain "no adjacent room" test is
        /// not enough — a room can have an open side that faces an INTERIOR notch, where
        /// there is no corridor outer wall to cut a door into (this is why Loop layouts
        /// were coming out sealed). An extreme room's outward wall is guaranteed to sit
        /// on the building's real boundary, so the perimeter corridor always wraps
        /// straight past it and a door can always be framed there. We pick the extreme
        /// room nearest the entry room so the breach lands as close to the intended
        /// start as the geometry allows, and align the breach door, the corridor outer
        /// door, and the trainee spawn to it.
        /// </summary>
        private void ResolveEntrance(ScenarioData scenario)
        {
            _entranceResolved = false;

            List<RoomData> rooms = scenario?.layout?.rooms;
            if (rooms == null || rooms.Count == 0) return;

            RoomData entryRoom = rooms.Find(r => r.depth == 0) ?? rooms[0];
            Vector3 entryC = entryRoom.position.ToVector3();

            // The four perimeter rooms: the extreme room on each axis is guaranteed to
            // lie on the building's outer boundary, so its outward wall always faces the
            // corridor ring (never an interior notch) and a door can always be cut.
            RoomData nR = null, sR = null, eR = null, wR = null;
            float nMax = float.MinValue, sMin = float.MaxValue, eMax = float.MinValue, wMin = float.MaxValue;
            foreach (RoomData r in rooms)
            {
                Vector3 c = r.position.ToVector3();
                float hw = HalfExtentW(r), hd = HalfExtentD(r);
                if (c.z + hd > nMax) { nMax = c.z + hd; nR = r; }
                if (c.z - hd < sMin) { sMin = c.z - hd; sR = r; }
                if (c.x + hw > eMax) { eMax = c.x + hw; eR = r; }
                if (c.x - hw < wMin) { wMin = c.x - hw; wR = r; }
            }

            RoomData[] cr = { nR, sR, eR, wR };
            WallSide[] cs = { WallSide.North, WallSide.South, WallSide.East, WallSide.West };

            // Pick the perimeter entrance nearest the entry room so we breach close to
            // the intended start (ties broken by side order N,S,E,W).
            RoomData bestRoom = null; WallSide bestSide = WallSide.West; float bestDist = float.MaxValue;
            for (int i = 0; i < 4; i++)
            {
                if (cr[i] == null) continue;
                float d = (cr[i].position.ToVector3() - entryC).sqrMagnitude;
                if (d < bestDist) { bestDist = d; bestRoom = cr[i]; bestSide = cs[i]; }
            }
            if (bestRoom == null) return;

            _entranceRoomId       = bestRoom.id;
            _entranceSide         = bestSide;
            _entranceWallMidWorld = World(WallMidpoint(bestRoom, bestSide));
            _entranceResolved     = true;

            if (bestRoom.id != entryRoom.id)
                Debug.Log($"[SceneBuilder] Entry room '{entryRoom.id}' is not on the building " +
                          $"perimeter; entrance resolved to '{bestRoom.id}' on its {bestSide} wall.");
        }

        /// <summary>Half-extent (incl. corridor gap) of a room along X.</summary>
        private static float HalfExtentW(RoomData r)
        {
            float w = r.size != null && r.size.width > 0f ? r.size.width : 6f;
            return (w + CorridorGap) * 0.5f;
        }

        /// <summary>Half-extent (incl. corridor gap) of a room along Z.</summary>
        private static float HalfExtentD(RoomData r)
        {
            float d = r.size != null && r.size.depth > 0f ? r.size.depth : 6f;
            return (d + CorridorGap) * 0.5f;
        }

        /// <summary>Layout-space midpoint of a room's outer wall on the given side.</summary>
        private static Vector3 WallMidpoint(RoomData room, WallSide side)
        {
            Vector3 c = room.position.ToVector3();
            float w = room.size != null && room.size.width > 0f ? room.size.width : 6f;
            float d = room.size != null && room.size.depth > 0f ? room.size.depth : 6f;
            float halfW = (w + CorridorGap) * 0.5f;
            float halfD = (d + CorridorGap) * 0.5f;
            switch (side)
            {
                case WallSide.North: return new Vector3(c.x, c.y, c.z + halfD);
                case WallSide.South: return new Vector3(c.x, c.y, c.z - halfD);
                case WallSide.East:  return new Vector3(c.x + halfW, c.y, c.z);
                default:             return new Vector3(c.x - halfW, c.y, c.z); // West
            }
        }

        /// <summary>
        /// Maps building entry openings to the exterior walls they pierce, so
        /// BuildWalls can carve an opening there and BuildEntryDoors can place a door.
        /// The resolved guaranteed entrance is always included first; the layout's own
        /// entry points are added only where they land on a genuine exterior wall, so
        /// an interior/mis-placed entry point can never punch a hole between two rooms.
        /// </summary>
        private void ComputeEntryOpenings(ScenarioData scenario)
        {
            _entryOpenings.Clear();

            void Add(string roomId, WallSide side, Vector3 worldPos, string id)
            {
                if (!_entryOpenings.TryGetValue(roomId, out List<EntryOpening> list))
                {
                    list = new List<EntryOpening>();
                    _entryOpenings[roomId] = list;
                }
                if (list.Exists(e => e.side == side)) return; // one opening per side
                list.Add(new EntryOpening { side = side, position = worldPos, id = id });
            }

            // 1) Guaranteed exterior breach (the primary way in). Slide it along the
            //    wall first so it isn't in line with the entrance room's own interior
            //    door — otherwise the two openings form a tunnel and a terrorist in
            //    the next room can engage the trainee before they ever reach the
            //    building. Updating the field keeps the corridor's outer opening and
            //    the trainee spawn aligned to where the breach actually ends up.
            if (_entranceResolved)
            {
                _entranceWallMidWorld = OffsetEntranceFromInteriorDoors(scenario);
                Add(_entranceRoomId, _entranceSide, _entranceWallMidWorld, "main");
            }

            // 2) Optional extra entry points from the layout — only when genuinely exterior.
            List<EntryPointData> entryPoints = scenario.layout?.entryPoints;
            if (entryPoints != null)
            {
                foreach (EntryPointData ep in entryPoints)
                {
                    if (ep == null || string.IsNullOrEmpty(ep.roomId)) continue;

                    RoomData room = scenario.layout.rooms.Find(r => r.id == ep.roomId);
                    if (room == null) continue;

                    WallSide side = WallSideFromDelta(ep.position.ToVector3() - room.position.ToVector3());

                    // Never carve an interior wall: skip if a neighbouring room sits
                    // beyond this side, or an interior door already occupies it.
                    if (!IsExteriorWall(room, side, scenario.layout.rooms)) continue;
                    if (room.doors != null && room.doors.Exists(d => d.wallSide == side)) continue;

                    Add(ep.roomId, side, World(ep.position.ToVector3()), ep.id);
                }
            }
        }

        /// <summary>
        /// Slides the resolved main entrance along its wall until it is clear of the
        /// entrance room's interior doors — the ones on the wall facing it, which
        /// would otherwise line up with it into a straight sightline right through
        /// the room. Returns the (possibly unchanged) world position of the opening.
        /// Runs after <see cref="MeasureDoorPrefab"/> so the real leaf width is known.
        /// </summary>
        private Vector3 OffsetEntranceFromInteriorDoors(ScenarioData scenario)
        {
            Vector3 current = _entranceWallMidWorld;

            RoomData room = scenario?.layout?.rooms?.Find(r => r.id == _entranceRoomId);
            if (room == null || room.doors == null || room.doors.Count == 0) return current;

            bool alongX = _entranceSide == WallSide.North || _entranceSide == WallSide.South;
            WallSide facing = OppositeWall(_entranceSide);

            // Laterals of the doors that share this wall's axis. A door on the facing
            // wall lines up straight through the room; one on the entrance wall itself
            // would collide with the opening outright.
            var blockers = new List<float>();
            foreach (DoorData d in room.doors)
            {
                if (d.wallSide != facing && d.wallSide != _entranceSide) continue;
                Vector3 w = World(d.position.ToVector3());
                blockers.Add(alongX ? w.x : w.z);
            }
            if (blockers.Count == 0) return current;

            float openW = _doorOpeningWidth + DoorClearance;

            // Already off to one side of every blocker by more than a doorway — the
            // opening is fine where it is, so leave it on the tidy wall midpoint.
            float currentLateral = alongX ? current.x : current.z;
            if (NearestBlocker(blockers, currentLateral) >= openW + 0.5f) return current;

            float width = room.size != null && room.size.width > 0f ? room.size.width : 6f;
            float depth = room.size != null && room.size.depth > 0f ? room.size.depth : 6f;
            float span  = (alongX ? width : depth) + CorridorGap;
            float limit = Mathf.Max(0f, span * 0.5f - openW * 0.5f - MinJamb);

            Vector3 roomWorld = World(room.position.ToVector3());
            float centre = alongX ? roomWorld.x : roomWorld.z;

            // Sweep the usable width of the wall and keep the position furthest from
            // every blocker — beats the centred default whenever a facing door exists,
            // and degrades gracefully on a wall that is already crowded.
            float best = centre, bestClearance = -1f;
            const int Steps = 8;
            for (int i = 0; i <= Steps; i++)
            {
                float candidate  = centre + Mathf.Lerp(-limit, limit, i / (float)Steps);
                float clearance  = NearestBlocker(blockers, candidate);
                if (clearance > bestClearance) { bestClearance = clearance; best = candidate; }
            }

            if (alongX) current.x = best; else current.z = best;

            Debug.Log($"[SceneBuilder] Main entrance on '{room.id}' was in line with an interior " +
                      $"door; slid {Mathf.Abs(best - currentLateral):F1} m along the wall so the " +
                      "trainee can't be shot through it from outside.");
            return current;
        }

        /// <summary>Distance from <paramref name="lateral"/> to the nearest blocker.</summary>
        private static float NearestBlocker(List<float> blockers, float lateral)
        {
            float nearest = float.MaxValue;
            foreach (float b in blockers)
                nearest = Mathf.Min(nearest, Mathf.Abs(b - lateral));
            return nearest;
        }

        /// <summary>
        /// Places an exterior door at every building entry point. These are the
        /// breach points into the building. The MAIN entrance is always closed — an
        /// open front door lets the defenders see and engage the trainee while they
        /// are still crossing the open ground outside, which is exactly the death
        /// the standoff spawn is meant to avoid. Secondary entry points still honour
        /// <see cref="entryDoorStartsOpen"/>. The interior wall already has the
        /// matching opening carved by BuildWalls.
        /// </summary>
        private void BuildEntryDoors(ScenarioData scenario)
        {
            if (doorPrefab == null || _entryOpenings.Count == 0) return;

            float openW = _doorOpeningWidth + DoorClearance;

            foreach (KeyValuePair<string, List<EntryOpening>> kvp in _entryOpenings)
            {
                RoomData room = scenario.layout.rooms.Find(r => r.id == kvp.Key);

                foreach (EntryOpening opening in kvp.Value)
                {
                    bool isMain = string.Equals(opening.id, "main", StringComparison.Ordinal);
                    DoorState entryState = !isMain && entryDoorStartsOpen
                        ? DoorState.Open
                        : DoorState.Closed;

                    // Place the leaf on the SAME clamped lateral BuildWalls carved the
                    // opening at (see OpeningOffset) — an entry point from the layout can
                    // sit further along the wall than a MinJamb-safe opening allows, and
                    // without re-clamping here the door would land off to one side of the
                    // hole actually cut in the wall.
                    Vector3 pos = opening.position;
                    if (room != null)
                    {
                        float offset = OpeningOffset(room, opening.side, pos - _buildOffset, openW);
                        Vector3 roomWorld = World(room.position.ToVector3());
                        bool alongX = opening.side == WallSide.North || opening.side == WallSide.South;
                        if (alongX) pos.x = roomWorld.x + offset;
                        else        pos.z = roomWorld.z + offset;
                    }

                    PlaceDoor($"door_{opening.id}", pos, opening.side, entryState);
                }
            }
        }

        private static WallSide OppositeWall(WallSide side)
        {
            switch (side)
            {
                case WallSide.North: return WallSide.South;
                case WallSide.South: return WallSide.North;
                case WallSide.East:  return WallSide.West;
                default:             return WallSide.East; // West → East
            }
        }

        private static WallSide WallSideFromDelta(Vector3 delta)
        {
            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.z))
                return delta.x > 0 ? WallSide.East : WallSide.West;
            return delta.z > 0 ? WallSide.North : WallSide.South;
        }

        // ── Perimeter corridor ───────────────────────────────────────────────

        // Grid resolution (metres) the corridor is rasterised at. 1 m divides
        // every room extent (rooms are even-sized on an even grid), so building
        // cells tile the room floors exactly and corridor cells abut them flush.
        private const float CorridorGrid = 1f;

        /// <summary>
        /// Wraps the finished building in an enclosed corridor that hugs its true
        /// outline. The building is rasterised onto a grid, dilated outward by the
        /// corridor width, and the dilated-but-not-building cells become corridor
        /// (floor + roof + an outer wall on every corridor/outside boundary). This
        /// follows an L / T / U footprint instead of squaring it off to the
        /// bounding box. A single opening (with a door) is left on the building's
        /// entry side. Lives under its own container, kept out of the NavMesh bake
        /// so interior NPCs stay inside the building.
        /// </summary>
        private void BuildPerimeterCorridor(ScenarioData scenario)
        {
            if (!buildPerimeterCorridor) return;
            if (corridorWidth <= 0.01f) return;

            if (!TryComputeFootprint(scenario, out float minX, out float maxX,
                                     out float minZ, out float maxZ, out float height))
            {
                Debug.LogWarning("[SceneBuilder] Perimeter corridor skipped - no rooms to wrap.");
                return;
            }

            // Outer footprint (world space) including the corridor band, so the guide
            // path can route around it and stay outside the building walls.
            _fpMinX = minX - corridorWidth; _fpMaxX = maxX + corridorWidth;
            _fpMinZ = minZ - corridorWidth; _fpMaxZ = maxZ + corridorWidth;

            const float g = CorridorGrid;
            int cwCells = Mathf.Max(1, Mathf.RoundToInt(corridorWidth / g));

            // Grid spans the footprint plus the corridor band plus a one-cell
            // "outside" margin, so corridor cells on the very edge still see an
            // outside neighbour and get an outer wall.
            float originX = minX - corridorWidth - g;
            float originZ = minZ - corridorWidth - g;
            int nx = Mathf.CeilToInt((maxX + corridorWidth + g - originX) / g) + 1;
            int nz = Mathf.CeilToInt((maxZ + corridorWidth + g - originZ) / g) + 1;

            // building[i,j]: a room floor covers this cell.
            var building = new bool[nx, nz];
            foreach (RoomData room in scenario.layout.rooms)
            {
                float w = room.size != null && room.size.width > 0f ? room.size.width : 6f;
                float d = room.size != null && room.size.depth > 0f ? room.size.depth : 6f;
                float halfW = (w + CorridorGap) * 0.5f;
                float halfD = (d + CorridorGap) * 0.5f;
                Vector3 c = World(room.position.ToVector3());

                int i0 = Mathf.Clamp(Mathf.FloorToInt((c.x - halfW - originX) / g), 0, nx - 1);
                int i1 = Mathf.Clamp(Mathf.CeilToInt ((c.x + halfW - originX) / g) - 1, 0, nx - 1);
                int j0 = Mathf.Clamp(Mathf.FloorToInt((c.z - halfD - originZ) / g), 0, nz - 1);
                int j1 = Mathf.Clamp(Mathf.CeilToInt ((c.z + halfD - originZ) / g) - 1, 0, nz - 1);
                for (int i = i0; i <= i1; i++)
                    for (int j = j0; j <= j1; j++) building[i, j] = true;
            }

            // near[i,j]: within the corridor band of the building (Chebyshev
            // dilation by cwCells). corridor = near and not building.
            var near = new bool[nx, nz];
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    if (!building[i, j]) continue;
                    for (int di = -cwCells; di <= cwCells; di++)
                        for (int dj = -cwCells; dj <= cwCells; dj++)
                        {
                            int ni = i + di, nj = j + dj;
                            if (ni >= 0 && ni < nx && nj >= 0 && nj < nz) near[ni, nj] = true;
                        }
                }

            var corridor = new bool[nx, nz];
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                    corridor[i, j] = near[i, j] && !building[i, j];

            float baseY = _buildOffset.y;
            Material floorMat = ResolveCorridorFloorMaterial();
            Material wallMat  = wallMaterial    != null ? wallMaterial    : floorMat;
            Material roofMat  = ceilingMaterial != null ? ceilingMaterial : wallMat;

            // ── Floor + roof: greedy-merge corridor cells into rectangles.
            //    Corridor cells never overlap building cells, so the slabs sit
            //    flush with the room floors / ceilings - no z-fighting, no step.
            var used = new bool[nx, nz];
            for (int j = 0; j < nz; j++)
                for (int i = 0; i < nx; i++)
                {
                    if (!corridor[i, j] || used[i, j]) continue;

                    int w = 1;
                    while (i + w < nx && corridor[i + w, j] && !used[i + w, j]) w++;

                    int h = 1;
                    bool grow = true;
                    while (grow && j + h < nz)
                    {
                        for (int k = i; k < i + w; k++)
                            if (!corridor[k, j + h] || used[k, j + h]) { grow = false; break; }
                        if (grow) h++;
                    }

                    for (int a = i; a < i + w; a++)
                        for (int b = j; b < j + h; b++) used[a, b] = true;

                    float x0 = originX + i * g, x1 = originX + (i + w) * g;
                    float z0 = originZ + j * g, z1 = originZ + (j + h) * g;
                    BuildCorridorSlab("Floor", x0, x1, z0, z1, baseY, CorridorFloorThickness, floorMat);
                    BuildCorridorSlab("Roof",  x0, x1, z0, z1, baseY + height, CeilingThickness, roofMat);
                }

            // ── Outer walls: a segment on every corridor/outside boundary edge.
            //    Edges inside the entrance opening are skipped and their span is
            //    accumulated, so we can frame the doorway to the exact door width
            //    afterwards (the raw grid gap is quantised to whole cells and
            //    would otherwise be wider than the door, leaving side gaps). ─────
            GetCorridorEntrance(scenario, minX, maxX, minZ, maxZ,
                                out WallSide entranceSide, out Rect openingRect);

            bool entranceAlongX = entranceSide == WallSide.North || entranceSide == WallSide.South;
            float gapMin = float.MaxValue, gapMax = float.MinValue, gapPerp = 0f;
            bool haveGap = false;

            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    if (!corridor[i, j]) continue;
                    float x0 = originX + i * g, x1 = originX + (i + 1) * g;
                    float z0 = originZ + j * g, z1 = originZ + (j + 1) * g;

                    TryAddOuterWall(near, i + 1, j, nx, nz, $"Wall_{i}_{j}_E", false, x1, z0, z1,
                                    baseY, height, wallMat, entranceSide, openingRect,
                                    ref haveGap, ref gapMin, ref gapMax, ref gapPerp);
                    TryAddOuterWall(near, i - 1, j, nx, nz, $"Wall_{i}_{j}_W", false, x0, z0, z1,
                                    baseY, height, wallMat, entranceSide, openingRect,
                                    ref haveGap, ref gapMin, ref gapMax, ref gapPerp);
                    TryAddOuterWall(near, i, j + 1, nx, nz, $"Wall_{i}_{j}_N", true, z1, x0, x1,
                                    baseY, height, wallMat, entranceSide, openingRect,
                                    ref haveGap, ref gapMin, ref gapMax, ref gapPerp);
                    TryAddOuterWall(near, i, j - 1, nx, nz, $"Wall_{i}_{j}_S", true, z0, x0, x1,
                                    baseY, height, wallMat, entranceSide, openingRect,
                                    ref haveGap, ref gapMin, ref gapMax, ref gapPerp);
                }

            // ── Frame the entrance to the exact door width + place the door ──
            if (haveGap)
            {
                float openW    = _doorOpeningWidth  + DoorClearance;
                float openH    = _doorOpeningHeight + DoorClearance;
                float doorLat  = (gapMin + gapMax) * 0.5f;   // centre the door in the quantised gap
                float halfOpen = openW * 0.5f;

                // Jambs filling the quantised gap down to the door width; they abut
                // the neighbouring grid walls (no overlap, so no z-fighting).
                BuildCorridorWallRun("Wall_Entry_A", entranceAlongX, gapPerp,
                                     gapMin, doorLat - halfOpen, baseY, height, wallMat);
                BuildCorridorWallRun("Wall_Entry_B", entranceAlongX, gapPerp,
                                     doorLat + halfOpen, gapMax, baseY, height, wallMat);

                float headerH = height - openH;
                if (headerH > 0.01f)
                    BuildCorridorWallRun("Wall_Entry_Header", entranceAlongX, gapPerp,
                                         doorLat - halfOpen, doorLat + halfOpen,
                                         baseY + openH, headerH, wallMat);

                if (doorPrefab != null)
                {
                    Vector3 doorPos = entranceAlongX
                        ? new Vector3(doorLat, baseY, gapPerp)
                        : new Vector3(gapPerp, baseY, doorLat);
                    // The compound's outer door is the trainee's first obstacle and is
                    // ALWAYS closed — an open one leaves a straight line from the open
                    // ground outside all the way into the building.
                    PlaceDoor("door_corridor_entry", doorPos, entranceSide, DoorState.Closed);

                    // Remember where the entrance ended up so the guide path can run to it.
                    _entryDoorWorld    = doorPos;
                    _hasEntryDoorWorld = true;
                }
            }
        }

        /// <summary>
        /// Adds an outer-wall segment for a corridor cell face when the neighbour
        /// is outside. Faces inside the entrance opening (matched by orientation +
        /// the opening rect) are skipped, and their span/plane is accumulated so
        /// the caller can frame the doorway precisely.
        /// </summary>
        private void TryAddOuterWall(bool[,] near, int ni, int nj, int nx, int nz,
                                     string name, bool axisAlongX, float fixedCoord,
                                     float runMin, float runMax, float baseY, float height,
                                     Material mat, WallSide entranceSide, Rect openingRect,
                                     ref bool haveGap, ref float gapMin, ref float gapMax,
                                     ref float gapPerp)
        {
            if (!IsOutsideCell(near, ni, nj, nx, nz)) return; // neighbour is building/corridor

            float runCentre = (runMin + runMax) * 0.5f;
            Vector2 mid = axisAlongX ? new Vector2(runCentre, fixedCoord)
                                     : new Vector2(fixedCoord, runCentre);
            bool sideMatches = axisAlongX
                ? entranceSide == WallSide.North || entranceSide == WallSide.South
                : entranceSide == WallSide.East  || entranceSide == WallSide.West;

            if (sideMatches && openingRect.Contains(mid))
            {
                gapMin  = Mathf.Min(gapMin, runMin);
                gapMax  = Mathf.Max(gapMax, runMax);
                gapPerp = fixedCoord;
                haveGap = true;
                return;
            }

            BuildCorridorWallRun(name, axisAlongX, fixedCoord, runMin, runMax, baseY, height, mat);
        }

        /// <summary>A grid cell counts as "outside" (gets an outer wall) when it
        /// is off the grid or neither building nor corridor.</summary>
        private static bool IsOutsideCell(bool[,] near, int i, int j, int nx, int nz)
        {
            if (i < 0 || i >= nx || j < 0 || j >= nz) return true;
            return !near[i, j];
        }

        /// <summary>
        /// Computes the building's outer footprint (the union of every room's
        /// outer wall extents, which sit half the corridor gap beyond each room's
        /// nominal size) and the wall height to match. Returns false if there are
        /// no rooms.
        /// </summary>
        private bool TryComputeFootprint(ScenarioData scenario, out float minX, out float maxX,
                                         out float minZ, out float maxZ, out float height)
        {
            minX = minZ = float.MaxValue;
            maxX = maxZ = float.MinValue;
            height = 3f;
            bool any = false;

            foreach (RoomData room in scenario.layout.rooms)
            {
                float w = room.size != null && room.size.width  > 0f ? room.size.width  : 6f;
                float d = room.size != null && room.size.depth  > 0f ? room.size.depth  : 6f;
                float h = room.size != null && room.size.height > 0f ? room.size.height : 3f;

                float halfW = (w + CorridorGap) * 0.5f;
                float halfD = (d + CorridorGap) * 0.5f;

                Vector3 c = World(room.position.ToVector3());
                minX = Mathf.Min(minX, c.x - halfW); maxX = Mathf.Max(maxX, c.x + halfW);
                minZ = Mathf.Min(minZ, c.z - halfD); maxZ = Mathf.Max(maxZ, c.z + halfD);
                height = Mathf.Max(height, h);
                any = true;
            }
            return any;
        }

        /// <summary>
        /// Layout-space (pre-offset) bounds of the building: the union of every room's
        /// outer wall extents, centred on the layout origin. Mirrors
        /// <see cref="TryComputeFootprint"/> but WITHOUT the world offset, so callers
        /// can test candidate placements. Y half-extent is returned via
        /// <paramref name="height"/>.
        /// </summary>
        private bool TryComputeLayoutExtents(ScenarioData scenario, out Vector3 centre,
                                             out Vector3 halfExtents, out float height)
        {
            centre = Vector3.zero;
            halfExtents = Vector3.zero;
            height = 3f;

            float minX = float.MaxValue, minZ = float.MaxValue;
            float maxX = float.MinValue, maxZ = float.MinValue;
            bool any = false;

            foreach (RoomData room in scenario.layout.rooms)
            {
                float w = room.size != null && room.size.width  > 0f ? room.size.width  : 6f;
                float d = room.size != null && room.size.depth  > 0f ? room.size.depth  : 6f;
                float h = room.size != null && room.size.height > 0f ? room.size.height : 3f;

                float halfW = (w + CorridorGap) * 0.5f;
                float halfD = (d + CorridorGap) * 0.5f;

                Vector3 c = room.position.ToVector3();
                minX = Mathf.Min(minX, c.x - halfW); maxX = Mathf.Max(maxX, c.x + halfW);
                minZ = Mathf.Min(minZ, c.z - halfD); maxZ = Mathf.Max(maxZ, c.z + halfD);
                height = Mathf.Max(height, h);
                any = true;
            }

            if (!any) return false;

            centre = new Vector3((minX + maxX) * 0.5f, 0f, (minZ + maxZ) * 0.5f);
            halfExtents = new Vector3((maxX - minX) * 0.5f, 0f, (maxZ - minZ) * 0.5f);
            return true;
        }

        /// <summary>
        /// Nudges <see cref="_buildOffset"/> horizontally until the scenario's outer
        /// footprint (rooms + perimeter corridor + <see cref="mapClearance"/> gap) no
        /// longer overlaps any existing base-map collider. Runs before the build so
        /// every World()-mapped position inherits the cleared offset. Uses
        /// <see cref="Physics.ComputePenetration"/> against each overlapping collider
        /// to push out along the shortest separation, iterating a few times so corners
        /// (two adjacent walls) resolve to a diagonal escape. A no-op when the feature
        /// is off, when the base map has no colliders on <see cref="baseMapLayers"/>,
        /// or when the placement is already clear.
        /// </summary>
        private void ResolveBaseMapClearance(ScenarioData scenario)
        {
            if (!avoidBaseMapOverlap) return;
            if (!TryComputeLayoutExtents(scenario, out Vector3 layoutCentre,
                                         out Vector3 halfExtents, out float height))
                return;

            // Outer footprint half-extents: room extents, plus the perimeter corridor
            // band (only when it's built), plus the requested clearance gap.
            float band = (buildPerimeterCorridor ? corridorWidth : 0f) + Mathf.Max(0f, mapClearance);
            Vector3 half = new Vector3(halfExtents.x + band,
                                       Mathf.Max(0.5f, height * 0.5f),
                                       halfExtents.z + band);

            // Temp probe collider representing the footprint. Never rendered/saved.
            var probeGo = new GameObject("~ScenarioClearanceProbe") { hideFlags = HideFlags.HideAndDontSave };
            var probe = probeGo.AddComponent<BoxCollider>();
            probe.size      = half * 2f;
            probe.isTrigger = true;

            Vector3 offset = _buildOffset;
            const int maxIterations = 32;
            try
            {
                for (int iter = 0; iter < maxIterations; iter++)
                {
                    Vector3 centre = layoutCentre + offset;
                    centre.y = offset.y + half.y;   // box rests on the ground plane
                    probeGo.transform.SetPositionAndRotation(centre, Quaternion.identity);

                    Collider[] hits = Physics.OverlapBox(centre, half, Quaternion.identity,
                                                         baseMapLayers, QueryTriggerInteraction.Ignore);

                    Vector3 correction = Vector3.zero;
                    bool overlapped = false;
                    foreach (Collider col in hits)
                    {
                        if (col == probe) continue;
                        if (IsOwnOrIgnoredCollider(col)) continue;

                        if (Physics.ComputePenetration(
                                probe, centre, Quaternion.identity,
                                col, col.transform.position, col.transform.rotation,
                                out Vector3 dir, out float dist))
                        {
                            dir.y = 0f;   // keep the scenario on the ground
                            if (dir.sqrMagnitude < 1e-6f || dist <= 0f) continue;
                            correction += dir.normalized * dist;
                            overlapped = true;
                        }
                    }

                    if (!overlapped) break;
                    offset += correction;
                }
            }
            finally
            {
                if (Application.isPlaying) Destroy(probeGo); else DestroyImmediate(probeGo);
            }

            Vector3 shift = offset - _buildOffset;
            if (shift.sqrMagnitude > 1e-4f)
            {
                Debug.Log($"[SceneBuilder] Shifted scenario by {shift} (|{shift.magnitude:0.0}| m) " +
                          $"to keep a {mapClearance:0.#} m gap from base-map geometry.");
                _buildOffset = offset;
            }
        }

        /// <summary>True for colliders the clearance probe must ignore: anything under
        /// this SceneBuilder, the trainee rig, or the trainee weapon — none of which are
        /// "base map" and all of which we reposition during the build anyway.</summary>
        private bool IsOwnOrIgnoredCollider(Collider col)
        {
            Transform t = col.transform;
            if (t.IsChildOf(transform)) return true;
            if (traineeRig    != null && t.IsChildOf(traineeRig))    return true;
            if (traineeWeapon != null && t.IsChildOf(traineeWeapon)) return true;
            return false;
        }

        /// <summary>
        /// Resolves the corridor entrance: the outer wall side facing the
        /// building's primary entry point and an opening rectangle (in the X/Z
        /// plane) where outer-wall segments are suppressed. The opening lines up
        /// with the building's entry so the trainee walks a straight line staging →
        /// outer door → building entry. Falls back to the south side at the
        /// footprint midpoint when the layout defines no entry points. The exact
        /// door position is derived later from the suppressed wall span.
        /// </summary>
        private void GetCorridorEntrance(ScenarioData scenario,
                                         float minX, float maxX, float minZ, float maxZ,
                                         out WallSide side, out Rect openingRect)
        {
            float openW = _doorOpeningWidth + DoorClearance;
            float cw    = corridorWidth;
            float g     = CorridorGrid;

            float lateralX, lateralZ;

            // Prefer the guaranteed entrance resolved from real exterior geometry, so
            // the corridor's outer door lines up with the room breach door across the
            // ring. Fall back to the layout entry point only if resolution failed.
            if (_entranceResolved)
            {
                side     = _entranceSide;
                lateralX = _entranceWallMidWorld.x;
                lateralZ = _entranceWallMidWorld.z;
            }
            else
            {
                List<EntryPointData> entryPoints = scenario.layout?.entryPoints;
                if (entryPoints != null && entryPoints.Count > 0)
                {
                    Vector3 ep = World(entryPoints[0].position.ToVector3());
                    var centre = new Vector3((minX + maxX) * 0.5f, ep.y, (minZ + maxZ) * 0.5f);
                    side = WallSideFromDelta(ep - centre);
                    lateralX = ep.x;
                    lateralZ = ep.z;
                }
                else
                {
                    side = WallSide.South;
                    lateralX = (minX + maxX) * 0.5f;
                    lateralZ = minZ;
                }
            }

            // The opening rect spans the door width laterally and reaches from just
            // inside the building edge out past the corridor's outer wall (using the
            // true footprint bound on the entrance side), so the outer-wall segment at
            // the entrance lateral coordinate is always suppressed — even when the
            // entrance room is not on the extreme edge of the footprint.
            switch (side)
            {
                case WallSide.North:
                    openingRect = Rect.MinMaxRect(lateralX - openW * 0.5f, maxZ - g,
                                                  lateralX + openW * 0.5f, maxZ + cw + g);
                    break;
                case WallSide.South:
                    openingRect = Rect.MinMaxRect(lateralX - openW * 0.5f, minZ - cw - g,
                                                  lateralX + openW * 0.5f, minZ + g);
                    break;
                case WallSide.East:
                    openingRect = Rect.MinMaxRect(maxX - g, lateralZ - openW * 0.5f,
                                                  maxX + cw + g, lateralZ + openW * 0.5f);
                    break;
                default: // West
                    openingRect = Rect.MinMaxRect(minX - cw - g, lateralZ - openW * 0.5f,
                                                  minX + g, lateralZ + openW * 0.5f);
                    break;
            }
        }

        /// <summary>
        /// Lays a flat axis-aligned slab (floor or roof strip) spanning
        /// [xMin,xMax] × [zMin,zMax] with its bottom face at <paramref name="baseY"/>.
        /// </summary>
        private void BuildCorridorSlab(string name, float xMin, float xMax,
                                       float zMin, float zMax, float baseY,
                                       float thickness, Material mat)
        {
            float sizeX = xMax - xMin;
            float sizeZ = zMax - zMin;
            if (sizeX <= 0.001f || sizeZ <= 0.001f) return;

            var centre = new Vector3((xMin + xMax) * 0.5f,
                                     baseY + thickness * 0.5f,
                                     (zMin + zMax) * 0.5f);
            AddCorridorBox(name, centre, new Vector3(sizeX, thickness, sizeZ), mat);
        }

        /// <summary>
        /// Adds a single solid wall cube running between <paramref name="runMin"/>
        /// and <paramref name="runMax"/> along the wall's axis, at world plane
        /// <paramref name="fixedCoord"/>, rising <paramref name="height"/> from
        /// <paramref name="yBase"/>. <paramref name="axisAlongX"/> selects whether
        /// the wall runs along X (north/south face) or Z (east/west face).
        /// </summary>
        private void BuildCorridorWallRun(string name, bool axisAlongX, float fixedCoord,
                                          float runMin, float runMax, float yBase,
                                          float height, Material mat)
        {
            float length = runMax - runMin;
            if (length <= 0.001f) return;

            float runCentre = (runMin + runMax) * 0.5f;
            float cy = yBase + height * 0.5f;
            Vector3 centre = axisAlongX
                ? new Vector3(runCentre, cy, fixedCoord)
                : new Vector3(fixedCoord, cy, runCentre);
            Vector3 size = axisAlongX
                ? new Vector3(length, height, WallThickness)
                : new Vector3(WallThickness, height, length);

            AddCorridorBox(name, centre, size, mat);
        }

        /// <summary>
        /// Instantiates a primitive cube at a world centre/size under the corridor
        /// container. Assumes the SceneBuilder transform is unscaled (as the room
        /// roots are), matching how rooms are placed in world space.
        /// </summary>
        private void AddCorridorBox(string name, Vector3 worldCentre, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(_corridorRoot, worldPositionStays: true);
            go.transform.position   = worldCentre;
            go.transform.localScale = size;

            if (mat != null)
                go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        /// <summary>
        /// Corridor floor material: the explicit <see cref="corridorFloorMaterial"/>
        /// if set, otherwise the material on the first built room's floor so the
        /// corridor floor matches the building.
        /// </summary>
        private Material ResolveCorridorFloorMaterial()
        {
            if (corridorFloorMaterial != null) return corridorFloorMaterial;
            foreach (KeyValuePair<string, GameObject> kvp in _roomObjects)
            {
                Renderer r = kvp.Value != null ? kvp.Value.GetComponentInChildren<Renderer>() : null;
                if (r != null) return r.sharedMaterial;
            }
            return wallMaterial;
        }

        /// <summary>
        /// Instantiates the door prefab into a wall opening: rotates it so its
        /// widest horizontal axis runs along the wall, offsets the root so the
        /// door's measured visual centre lands on <paramref name="openingCenter"/>
        /// (the XRI door's pivot sits on the hinge edge, not the centre), then
        /// applies the initial <see cref="DoorState"/>.
        /// </summary>
        private void PlaceDoor(string name, Vector3 openingCenter, WallSide side, DoorState state)
        {
            Quaternion rot = DoorRotation(side) * _doorBaseRot;

            // Align the measured visual centre to the opening horizontally, and
            // rest the door's lowest mesh point on the floor (openingCenter.y).
            Vector3 pos = openingCenter - rot * _doorCenterOffset;
            pos.y = openingCenter.y - _doorBaseY;

            GameObject go = Instantiate(doorPrefab, pos, rot, _doorsRoot);
            go.name = name;

            // ApplyDoorState runs the same frame as Instantiate, before any
            // component's Start(), so the XRI Door reads the right startOpened.
            ApplyDoorState(go, state);

            // Record EVERY door that actually gets built. The NavMeshLinks are created from
            // this list AFTER the bake (see BuildDoorNavLinks) — a link made before the bake
            // has no NavMesh to attach to. Recording here (rather than walking the scenario's
            // room-to-room door list) is what finally covers the perimeter-corridor entry,
            // which SceneBuilder builds itself and which never appears in scenario.layout.
            // Without it the corridor was baked but unreachable: measured 0/6 corridor floor
            // points pathable from a terrorist (all PathPartial).
            _placedDoors.Add(new PlacedDoor { name = name, center = openingCenter, side = side });
        }

        /// <summary>A doorway that was actually built, so it can be NavMesh-linked post-bake.</summary>
        private struct PlacedDoor
        {
            public string   name;
            public Vector3  center;   // world-space centre of the opening
            public WallSide side;     // which wall it sits in (decides the link's axis)
        }

        private readonly List<PlacedDoor> _placedDoors = new List<PlacedDoor>();

        /// <summary>
        /// Drives a freshly instantiated door's initial state. Prefers the
        /// realistic-door <see cref="NpcDoorAssist"/>; falls back to the greybox
        /// <see cref="GeneratedDoor"/> so either door prefab still works.
        /// </summary>
        private static void ApplyDoorState(GameObject go, DoorState state)
        {
            NpcDoorAssist assist = go.GetComponent<NpcDoorAssist>();
            if (assist != null)
            {
                assist.ApplyState(state);
                return;
            }

            GeneratedDoor gd = go.GetComponent<GeneratedDoor>();
            if (gd != null) gd.state = state;
        }

        /// <summary>
        /// Measures the assigned door prefab once per build so wall openings can
        /// be sized and the door aligned to any door art (greybox or the XRI
        /// hinge door). Fills <see cref="_doorOpeningWidth"/>,
        /// <see cref="_doorOpeningHeight"/>, <see cref="_doorBaseRot"/> and
        /// <see cref="_doorCenterOffset"/>. Falls back to the 2 m greybox defaults
        /// when no prefab is set or it has no meshes.
        /// </summary>
        private void MeasureDoorPrefab()
        {
            _doorOpeningWidth  = DoorGap;
            _doorOpeningHeight = DoorOpeningHeight;
            _doorBaseRot       = Quaternion.identity;
            _doorCenterOffset  = Vector3.zero;
            _doorBaseY         = 0f;

            if (doorPrefab == null) return;

            // Instantiate under an inactive parent so no Awake/Start side effects
            // fire (XR interactables, door springs); mesh bounds work regardless.
            var temp = new GameObject("~DoorProbe");
            temp.hideFlags = HideFlags.HideAndDontSave;
            temp.SetActive(false);
            GameObject probe = Instantiate(doorPrefab, temp.transform);

            // Measure only the swinging leaf (the door panel) so frame / threshold
            // extras that extend well below or beside it don't inflate the opening
            // size or float the door up off the floor. Fall back to the whole
            // prefab for doors that have no NpcDoorAssist (e.g. the greybox door).
            Transform leaf = null;
            NpcDoorAssist assist = probe.GetComponent<NpcDoorAssist>();
            if (assist != null)
                leaf = assist.leafBody != null ? assist.leafBody.transform
                     : assist.hinge    != null ? assist.hinge.transform
                     : null;
            GameObject measureRoot = leaf != null ? leaf.gameObject : probe;
            // Remember before the probe is destroyed — DestroyImmediate (edit
            // mode) would make `leaf != null` false below.
            bool leafMeasured = leaf != null;

            bool found = ComputeLocalBounds(measureRoot, probe.transform, out Bounds b);

            if (Application.isPlaying) Destroy(temp);
            else                       DestroyImmediate(temp);

            if (!found) return;

            bool spanAlongX = b.size.x >= b.size.z;
            _doorOpeningWidth  = Mathf.Max(0.5f, spanAlongX ? b.size.x : b.size.z);
            _doorOpeningHeight = Mathf.Max(0.5f, b.size.y);
            _doorBaseRot       = spanAlongX ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f);
            _doorCenterOffset  = new Vector3(b.center.x, 0f, b.center.z);
            _doorBaseY         = b.center.y - b.size.y * 0.5f; // lowest mesh point (root-local)

            // Realistic door (leaf measured separately): float the leaf clear of
            // the 0.08 m floor slab instead of resting it at the room origin,
            // where it spawned embedded in the floor collider and physics shoved
            // it against its hinge (tilted, juddering, half-open doors). Folding
            // the lift into _doorBaseY raises the whole prefab at placement; the
            // opening grows by the same amount so the raised leaf still clears
            // the wall header. The greybox prefab keeps its own baked-in
            // floorClear and needs neither adjustment.
            if (leafMeasured)
            {
                _doorBaseY         -= DoorFloorLift;
                _doorOpeningHeight += DoorFloorLift;
            }
        }

        /// <summary>
        /// Computes the combined mesh bounds of <paramref name="meshRoot"/>'s
        /// hierarchy, expressed in <paramref name="frame"/>'s local space. Uses
        /// shared meshes so it works on an inactive instance. Returns false when
        /// the hierarchy has no meshes.
        /// </summary>
        private static bool ComputeLocalBounds(GameObject meshRoot, Transform frame, out Bounds bounds)
        {
            bounds = new Bounds();
            bool has = false;
            Matrix4x4 toFrame = frame.worldToLocalMatrix;

            foreach (MeshFilter mf in meshRoot.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                EncapsulateMesh(toFrame, mf.transform, mf.sharedMesh.bounds, ref bounds, ref has);
            }
            foreach (SkinnedMeshRenderer sm in meshRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (sm.sharedMesh == null) continue;
                EncapsulateMesh(toFrame, sm.transform, sm.sharedMesh.bounds, ref bounds, ref has);
            }
            return has;
        }

        private static void EncapsulateMesh(Matrix4x4 toRoot, Transform meshTf,
                                            Bounds meshBounds, ref Bounds acc, ref bool has)
        {
            Matrix4x4 m = toRoot * meshTf.localToWorldMatrix;
            Vector3 c = meshBounds.center;
            Vector3 e = meshBounds.extents;

            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    c.x + (((i & 1) == 0) ? -e.x : e.x),
                    c.y + (((i & 2) == 0) ? -e.y : e.y),
                    c.z + (((i & 4) == 0) ? -e.z : e.z));
                Vector3 p = m.MultiplyPoint3x4(corner);
                if (!has) { acc = new Bounds(p, Vector3.zero); has = true; }
                else       acc.Encapsulate(p);
            }
        }

        private void PositionTrainee(ScenarioData scenario)
        {
            _traineeSpawnedOutside = false;

            if (traineeRig == null)
            {
                Debug.LogError("[SceneBuilder] traineeRig is not assigned - cannot position player.");
                return;
            }

            // Preferred and, when this mode is on, MANDATORY: spawn in open ground
            // just outside the generated entrance, facing the door, at the head of
            // the yellow guide path. The trainee must NEVER start inside the building
            // surrounded by terrorists, so TryComputeEntranceSpawn is written to
            // always resolve an outside spot (it derives the entrance from the layout
            // entry point when no corridor door was captured, and keeps the trainee
            // outside at max standoff when no perfectly clear ground is found). The
            // entrance moves per scenario, so this is computed each build.
            if (spawnOutsideEntrance &&
                TryComputeEntranceSpawn(scenario, out Vector3 outsidePos, out Quaternion outsideRot))
            {
                traineeRig.SetPositionAndRotation(outsidePos, outsideRot);
                _traineeSpawnedOutside = true;
                Debug.Log($"[SceneBuilder] Trainee spawned outside entrance at {outsidePos:F1}, " +
                          $"at the head of the guide path facing the door.");
                PlaceTraineeWeapon();
                PlaceStagingTable();
                return;
            }

            if (spawnOutsideEntrance)
                Debug.LogWarning("[SceneBuilder] spawnOutsideEntrance is on but no entrance " +
                                 "could be resolved (no corridor door and no layout entry point). " +
                                 "Falling back to staging / interior spawn.");

            // Staging spawn: start the trainee at a fixed point (e.g. by the
            // briefing table) and let them move to the generated building. Use a
            // yaw-only rotation so the rig never inherits pitch/roll from the
            // anchor transform.
            if (traineeStartPoint != null)
            {
                Quaternion yaw = Quaternion.Euler(0f, traineeStartPoint.eulerAngles.y, 0f);
                traineeRig.SetPositionAndRotation(traineeStartPoint.position, yaw);
                Debug.Log("[SceneBuilder] Trainee spawned at staging point " +
                          $"'{traineeStartPoint.name}'.");
                PlaceTraineeWeapon();
                PlaceStagingTable();
                return;
            }

            TraineeSpawnPoint t = scenario.spawnPoints?.trainee;
            if (t == null)
            {
                Debug.LogError("[SceneBuilder] spawnPoints.trainee is null - leaving rig at origin.");
                return;
            }

            Vector3 spawnPos = World(t.position.ToVector3());
            traineeRig.position = spawnPos;
            traineeRig.rotation = LookRotation(t.facingDirection);
            Debug.Log($"[SceneBuilder] Trainee spawned at Module 1 position {spawnPos:F1} " +
                      $"(entry room, facing {t.facingDirection.ToVector3():F1}).");
            PlaceTraineeWeapon();
            PlaceStagingTable();
        }

        /// <summary>
        /// Computes a spawn just outside the entrance, at the head of the yellow guide
        /// path: step out along the entrance's outward wall normal by
        /// <see cref="entranceStandoff"/> into open ground, facing back at the door. If
        /// that spot is obstructed (a template structure sits outside the door), step
        /// further out until clear.
        ///
        /// This is deliberately robust so the trainee is NEVER dropped inside the
        /// building among the terrorists when <see cref="spawnOutsideEntrance"/> is on:
        ///  • the entrance target comes from the captured corridor door when available,
        ///    otherwise it is derived from the layout's primary entry point;
        ///  • if no perfectly clear ground is found within the search window, the spawn
        ///    is still placed OUTSIDE at maximum standoff rather than giving up.
        /// Returns false only when there is no usable entrance reference at all
        /// (no corridor door AND no layout entry point).
        /// </summary>
        private bool TryComputeEntranceSpawn(ScenarioData scenario, out Vector3 pos, out Quaternion rot)
        {
            pos = Vector3.zero; rot = Quaternion.identity;

            // ── Resolve the entrance target + outward direction ──────────────────
            Vector3 door;
            Vector2 outward;

            // Resolve the outer footprint (building + corridor ring). Set during the
            // corridor build; recompute here if the corridor was skipped so the
            // boundary-based fallback below still has real bounds to work with.
            float fpMinX = _fpMinX, fpMaxX = _fpMaxX, fpMinZ = _fpMinZ, fpMaxZ = _fpMaxZ;
            bool haveFootprint = fpMaxX > fpMinX && fpMaxZ > fpMinZ;
            if (!haveFootprint &&
                TryComputeFootprint(scenario, out float bMinX, out float bMaxX,
                                    out float bMinZ, out float bMaxZ, out float _))
            {
                fpMinX = bMinX - corridorWidth; fpMaxX = bMaxX + corridorWidth;
                fpMinZ = bMinZ - corridorWidth; fpMaxZ = bMaxZ + corridorWidth;
                haveFootprint = fpMaxX > fpMinX && fpMaxZ > fpMinZ;
            }

            if (_hasEntryDoorWorld && haveFootprint)
            {
                // Best case: step straight out from the captured corridor door.
                door = _entryDoorWorld;
                Vector2 centre = new Vector2((fpMinX + fpMaxX) * 0.5f, (fpMinZ + fpMaxZ) * 0.5f);
                outward = OutwardNormal(new Vector2(door.x, door.z) - centre);
            }
            else if (haveFootprint)
            {
                // No corridor door captured (a fully enclosed layout). The layout entry
                // point can sit INSIDE the footprint (the entry room is the BFS origin
                // and is often ringed by other rooms), so never step out from it.
                // Instead, project the entry room onto the NEAREST outer footprint edge
                // — a guaranteed-exterior boundary — and step out from there. This keeps
                // the trainee outside the building envelope in every layout.
                RoomData entryRoom = scenario?.layout?.rooms?.Find(r => r.depth == 0)
                                     ?? scenario?.layout?.rooms?[0];
                Vector3 er = entryRoom != null ? World(entryRoom.position.ToVector3())
                                               : new Vector3((fpMinX + fpMaxX) * 0.5f, 0f,
                                                             (fpMinZ + fpMaxZ) * 0.5f);

                float dW = er.x - fpMinX, dE = fpMaxX - er.x;
                float dS = er.z - fpMinZ, dN = fpMaxZ - er.z;
                float min = Mathf.Min(Mathf.Min(dW, dE), Mathf.Min(dS, dN));

                if (min == dW)      { door = new Vector3(fpMinX, er.y, er.z); outward = new Vector2(-1f, 0f); }
                else if (min == dE) { door = new Vector3(fpMaxX, er.y, er.z); outward = new Vector2( 1f, 0f); }
                else if (min == dS) { door = new Vector3(er.x, er.y, fpMinZ); outward = new Vector2(0f, -1f); }
                else                { door = new Vector3(er.x, er.y, fpMaxZ); outward = new Vector2(0f,  1f); }
            }
            else
            {
                // No footprint at all (no rooms) — nothing sensible to resolve.
                return false;
            }
            if (outward.sqrMagnitude < 1e-4f)
                outward = new Vector2(-1f, 0f);   // safe default: step out to the west

            float groundY = Mathf.Max(traineeRig.position.y, _buildOffset.y);
            rot = Quaternion.LookRotation(new Vector3(-outward.x, 0f, -outward.y), Vector3.up);

            Physics.SyncTransforms();
            Vector3 half  = new Vector3(0.4f, 0.9f, 0.4f);          // ~person-sized probe
            float boxCy   = groundY + 1.0f;

            // Walk outward from the door until we find clear ground (cap the search).
            const float extraSearch = 12f;
            for (float dist = entranceStandoff; dist <= entranceStandoff + extraSearch; dist += 1f)
            {
                Vector3 cand = new Vector3(door.x + outward.x * dist, groundY,
                                           door.z + outward.y * dist);
                if (!Physics.CheckBox(new Vector3(cand.x, boxCy, cand.z), half,
                                      Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                {
                    pos = cand;
                    return true;
                }
            }

            // No fully clear spot on the entrance side — keep the trainee OUTSIDE at
            // max standoff rather than falling back to the interior spawn. Being just
            // outside near an obstacle is always preferable to spawning among the
            // terrorists inside.
            pos = new Vector3(door.x + outward.x * (entranceStandoff + extraSearch), groundY,
                              door.z + outward.y * (entranceStandoff + extraSearch));
            Debug.LogWarning("[SceneBuilder] No fully clear ground found outside the entrance; " +
                             "spawning at max standoff so the trainee still starts outside.");
            return true;
        }

        /// <summary>
        /// Drops the trainee's weapon beside them at the spawn point so it is in
        /// reach as soon as the mission starts.
        /// </summary>
        private void PlaceTraineeWeapon()
        {
            if (traineeWeapon == null || traineeRig == null)
                return;

            Vector3 pos = traineeRig.TransformPoint(weaponSpawnOffset);
            Quaternion rot = Quaternion.Euler(0f, traineeRig.eulerAngles.y + weaponSpawnYaw, 0f);

            var rb = traineeWeapon.GetComponent<Rigidbody>();

            // Rooms are built BEFORE the trainee is positioned, so on a rebuild the new
            // room colliders are instantiated straight through wherever the weapon was
            // left standing. PhysX queues a depenetration impulse for that overlap and
            // applies it on the next step - AFTER we teleport - which flings the weapon
            // metres away. Going kinematic across the teleport and syncing the transform
            // into PhysX discards the stale contact so it wakes up cleanly at the spawn.
            bool wasKinematic = rb != null && rb.isKinematic;
            if (rb != null)
                rb.isKinematic = true;

            traineeWeapon.SetPositionAndRotation(pos, rot);
            Physics.SyncTransforms();

            if (rb != null)
            {
                rb.isKinematic     = wasKinematic;
                rb.linearVelocity  = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            Debug.Log($"[SceneBuilder] Trainee weapon '{traineeWeapon.name}' placed at {pos:F1}.");
        }

        // Layout of the staging-table group relative to its reference root,
        // captured on the first placement so repeated rebuilds keep every item
        // in the same spot on the tabletop.
        private Pose[] _stagingTableLocalPoses;

        /// <summary>
        /// Moves the controller-adjustment table (a rigid group of scene roots)
        /// in front of the trainee's spawn, facing them, so they can tune their
        /// controllers before moving out. Same teleport rules as the weapon:
        /// rigidbodies go kinematic across the move so stale PhysX contacts from
        /// rebuilt room colliders can't fling the items off the table.
        /// </summary>
        private void PlaceStagingTable()
        {
            if (traineeRig == null || stagingTableRoots == null || stagingTableRoots.Length == 0)
                return;

            Transform primary = stagingTableRoots[0];
            if (primary == null)
            {
                Debug.LogWarning("[SceneBuilder] stagingTableRoots[0] (the reference root) " +
                                 "is missing - table not moved.");
                return;
            }

            if (_stagingTableLocalPoses == null ||
                _stagingTableLocalPoses.Length != stagingTableRoots.Length)
            {
                _stagingTableLocalPoses = new Pose[stagingTableRoots.Length];
                for (int i = 0; i < stagingTableRoots.Length; i++)
                {
                    Transform t = stagingTableRoots[i];
                    if (t == null) continue;
                    _stagingTableLocalPoses[i] = new Pose(
                        primary.InverseTransformPoint(t.position),
                        Quaternion.Inverse(primary.rotation) * t.rotation);
                }
            }

            // The reference root at trainee yaw puts the table's front toward the
            // trainee (front faces -Z at identity, and the offset is ahead of them).
            // The spawn can sit in a narrow gap (e.g. between the generated building
            // and the base map), so don't blindly drop the table dead ahead - it can
            // end up inside a wall. Sweep directions around the trainee, keeping the
            // table facing them from each, and take the first unobstructed one.
            Quaternion baseYaw = Quaternion.Euler(0f, traineeRig.eulerAngles.y, 0f);
            Quaternion yaw = baseYaw;
            Vector3 centre = traineeRig.position + yaw * tableSpawnOffset;
            float[] sweep = { 0f, 45f, -45f, 90f, -90f, 135f, -135f, 180f };
            bool clear = false;
            foreach (float angle in sweep)
            {
                Quaternion q = baseYaw * Quaternion.Euler(0f, angle, 0f);
                Vector3 c = traineeRig.position + q * tableSpawnOffset;
                if (IsStagingSpotClear(c, q))
                {
                    yaw = q; centre = c; clear = true;
                    if (angle != 0f)
                        Debug.Log($"[SceneBuilder] Staging table spot dead ahead is blocked; " +
                                  $"rotated {angle:F0}° around the trainee to open ground.");
                    break;
                }
            }
            if (!clear)
                Debug.LogWarning("[SceneBuilder] No unobstructed spot found for the staging " +
                                 "table around the trainee - placing it dead ahead anyway.");

            var bodies = new List<(Rigidbody rb, bool wasKinematic)>();
            foreach (Transform root in stagingTableRoots)
            {
                if (root == null) continue;
                foreach (Rigidbody rb in root.GetComponentsInChildren<Rigidbody>(true))
                {
                    bodies.Add((rb, rb.isKinematic));
                    rb.isKinematic = true;
                }
            }

            for (int i = 0; i < stagingTableRoots.Length; i++)
            {
                Transform t = stagingTableRoots[i];
                if (t == null) continue;
                Pose local = _stagingTableLocalPoses[i];
                t.SetPositionAndRotation(centre + yaw * local.position, yaw * local.rotation);
            }
            Physics.SyncTransforms();

            foreach ((Rigidbody rb, bool wasKinematic) in bodies)
            {
                rb.isKinematic = wasKinematic;
                if (!wasKinematic)
                {
                    rb.linearVelocity  = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }

            Debug.Log($"[SceneBuilder] Staging table placed at {centre:F1}, " +
                      "facing the trainee spawn.");
        }

        /// <summary>
        /// True when the staging table's volume at the candidate pose overlaps no
        /// foreign colliders (walls, generated rooms, furniture). Tests a box a
        /// little larger than the tabletop, lifted off the ground so floor slabs
        /// don't count as obstructions. The table's own colliders, the trainee rig
        /// and the already-placed trainee weapon are ignored.
        /// </summary>
        private bool IsStagingSpotClear(Vector3 centre, Quaternion yaw)
        {
            var boxCentre = new Vector3(centre.x, traineeRig.position.y + 0.85f, centre.z);
            var half = new Vector3(2.15f, 0.55f, 0.6f); // (length, height, depth) half-extents
            foreach (Collider c in Physics.OverlapBox(boxCentre, half, yaw, ~0,
                                                      QueryTriggerInteraction.Ignore))
            {
                Transform t = c.transform;
                if (t.IsChildOf(traineeRig)) continue;
                if (traineeWeapon != null && t.IsChildOf(traineeWeapon)) continue;
                bool own = false;
                foreach (Transform root in stagingTableRoots)
                    if (root != null && t.IsChildOf(root)) { own = true; break; }
                if (own) continue;
                return false;
            }
            return true;
        }

        private void SpawnHostages(ScenarioData scenario)
        {
            if (hostagePrefab == null)
            {
                Debug.LogError("[SceneBuilder] hostagePrefab is not assigned - skipping hostages.");
                return;
            }

            List<NpcSpawnPoint> hostages = scenario.spawnPoints?.hostages;
            if (hostages == null) return;

            foreach (NpcSpawnPoint sp in hostages)
            {
                GameObject go = Instantiate(hostagePrefab,
                                            World(sp.position.ToVector3()),
                                            LookRotation(sp.facingDirection),
                                            _npcsRoot);
                go.name = sp.entityId;

                HostageController controller = go.GetComponent<HostageController>();
                if (controller == null)
                {
                    Debug.LogWarning($"[SceneBuilder] Hostage prefab '{hostagePrefab.name}' " +
                                     $"is missing a HostageController component.");
                    continue;
                }

                EntityRecord record = FindEntity(scenario, sp.entityId);
                controller.currentState = ParseHostageState(record?.metadata?.initialState);

                // Ensure the hostage can detect the trainee for the escort.
                // HostageContactZone polls for the player and raises
                // HostageContactStarted → the hostage enters Follow. Add it here so
                // rescue works even if the prefab doesn't already carry the component.
                if (go.GetComponentInChildren<HostageContactZone>() == null)
                    go.AddComponent<HostageContactZone>();

                // Make the hostage shootable (trainee friendly-fire → injured/dead).
                if (go.GetComponent<HostageHitBox>() == null)
                    go.AddComponent<HostageHitBox>();
            }
        }

        /// <summary>
        /// Spawns a visible "safe spot" (extraction zone) JUST INSIDE the building,
        /// near the entrance door. The trainee leads a rescued (Following) hostage
        /// back into this zone to complete the mission. A flat coloured disc marks
        /// it on the floor; a trigger SphereCollider + ExtractionZone component do
        /// the detection.
        ///
        /// Deliberately kept INSIDE (not at the trainee's outdoor staging spawn):
        /// an outdoor zone needs a walkable NavMesh apron connecting the interior to
        /// the exterior, and once that connection exists every NavMeshAgent — not
        /// just the escorted hostage — can path through it, so terrorists ended up
        /// wandering outside the building. Keeping the zone (and therefore all
        /// NavMesh-reachable ground) entirely indoors removes that leak entirely.
        /// </summary>
        private void CreateSafeZone(ScenarioData scenario)
        {
            if (!createSafeZone) return;

            Vector3 pos = ResolveInteriorSafeZonePosition(scenario);

            _safeZone = new GameObject("SafeZone_Extraction");
            _safeZone.transform.position = pos;

            // Trigger volume + detection logic.
            var sphere = _safeZone.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = safeZoneRadius;
            _safeZone.AddComponent<ExtractionZone>();

            // Game-visible pink checkpoint marker (disc + pulsing ring + label).
            // The beacon builds its visuals the first time it's enabled. Starts
            // DISABLED so the marker is invisible until ExtractionZone reveals it
            // (once a hostage is actually being escorted) — the trainee has to find
            // the hostage first; the extraction point only then becomes the goal.
            var beacon = _safeZone.AddComponent<SafeZoneBeacon>();
            beacon.radius = safeZoneRadius;
            beacon.color  = safeZoneColor;
            beacon.enabled = false;

            Debug.Log($"[SceneBuilder] Safe zone created just inside the entrance at {pos} (radius {safeZoneRadius}m).");
        }

        /// <summary>
        /// Finds a point just INSIDE the building, stepped in from the entrance
        /// doorway by <see cref="safeZoneRadius"/> plus a small buffer so the disc
        /// clears the threshold. Walks further in if that spot is obstructed
        /// (furniture, a wall corner), same probing approach as the exterior
        /// staging spawn. Falls back to the trainee's own start position only when
        /// no entrance can be resolved at all (no corridor door, no layout entry
        /// point) — that case has no "inside near the door" reference to use.
        /// </summary>
        private Vector3 ResolveInteriorSafeZonePosition(ScenarioData scenario)
        {
            float groundY = traineeRig != null ? traineeRig.position.y : _buildOffset.y;

            Vector3 door;
            bool haveDoor;
            if (_hasEntryDoorWorld)
            {
                door = _entryDoorWorld;
                haveDoor = true;
            }
            else
            {
                List<EntryPointData> eps = scenario?.layout?.entryPoints;
                haveDoor = eps != null && eps.Count > 0;
                door = haveDoor ? World(eps[0].position.ToVector3()) : Vector3.zero;
            }

            if (!haveDoor)
            {
                // No entrance reference at all — fall back to wherever the trainee
                // actually starts (old behaviour), rather than failing outright.
                Vector3 fallback = traineeRig != null ? traineeRig.position
                                  : World((scenario?.spawnPoints?.trainee?.position)?.ToVector3() ?? Vector3.zero);
                fallback.y = SnapToFloorY(fallback, groundY);
                return fallback;
            }

            float fpMinX = _fpMinX, fpMaxX = _fpMaxX, fpMinZ = _fpMinZ, fpMaxZ = _fpMaxZ;
            bool haveFootprint = fpMaxX > fpMinX && fpMaxZ > fpMinZ;
            if (!haveFootprint &&
                TryComputeFootprint(scenario, out float bMinX, out float bMaxX,
                                    out float bMinZ, out float bMaxZ, out float _))
            {
                fpMinX = bMinX - corridorWidth; fpMaxX = bMaxX + corridorWidth;
                fpMinZ = bMinZ - corridorWidth; fpMaxZ = bMaxZ + corridorWidth;
                haveFootprint = fpMaxX > fpMinX && fpMaxZ > fpMinZ;
            }

            // Inward = the reverse of the entrance's OUTWARD wall normal, i.e.
            // straight into the building from the doorway.
            Vector2 inward = new Vector2(-1f, 0f);
            if (haveFootprint)
            {
                Vector2 centre = new Vector2((fpMinX + fpMaxX) * 0.5f, (fpMinZ + fpMaxZ) * 0.5f);
                Vector2 outward = OutwardNormal(new Vector2(door.x, door.z) - centre);
                if (outward.sqrMagnitude > 1e-4f) inward = -outward;
            }

            Physics.SyncTransforms();
            Vector3 half = new Vector3(safeZoneRadius, 0.9f, safeZoneRadius);
            float boxCy = groundY + 1.0f;
            float baseDist = safeZoneRadius + 1.0f;

            const float extraSearch = 6f;
            for (float dist = baseDist; dist <= baseDist + extraSearch; dist += 0.5f)
            {
                Vector3 cand = new Vector3(door.x + inward.x * dist, groundY, door.z + inward.y * dist);
                if (!Physics.CheckBox(new Vector3(cand.x, boxCy, cand.z), half,
                                      Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                {
                    cand.y = SnapToFloorY(cand, groundY);
                    return cand;
                }
            }

            // No fully clear spot found — still place it just inside rather than
            // giving up; a snug fit against furniture is better than no zone.
            Vector3 snug = new Vector3(door.x + inward.x * baseDist, groundY, door.z + inward.y * baseDist);
            snug.y = SnapToFloorY(snug, groundY);
            return snug;
        }

        /// <summary>
        /// Raycasts straight down onto the actual floor mesh at the candidate XZ
        /// position and returns its surface height. The room/trainee-rig Y used
        /// everywhere else in this method is the room's LOGICAL floor reference,
        /// not necessarily the rendered floor's top surface — the floor slab has
        /// real thickness, so a marker placed at the logical Y can end up
        /// centimetres UNDER the visible floor (invisible, z-fighting into the
        /// slab) rather than sitting on top of it. Falls back to
        /// <paramref name="fallbackY"/> if nothing is hit.
        /// </summary>
        private static float SnapToFloorY(Vector3 candidateXZ, float fallbackY)
        {
            Vector3 origin = new Vector3(candidateXZ.x, fallbackY + 3f, candidateXZ.z);
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 6f,
                                 ~0, QueryTriggerInteraction.Ignore))
                return hit.point.y;
            return fallbackY;
        }

        /// <summary>Snaps a direction to the dominant cardinal axis (±X or ±Z),
        /// giving the outward wall normal for the entrance side.</summary>
        private static Vector2 OutwardNormal(Vector2 delta)
        {
            return Mathf.Abs(delta.x) >= Mathf.Abs(delta.y)
                ? new Vector2(Mathf.Sign(delta.x), 0f)
                : new Vector2(0f, Mathf.Sign(delta.y));
        }

        private void SpawnTerrorists(ScenarioData scenario)
        {
            if (terroristPrefab == null)
            {
                Debug.LogError("[SceneBuilder] terroristPrefab is not assigned - skipping terrorists.");
                return;
            }

            List<NpcSpawnPoint> terrorists = scenario.spawnPoints?.terrorists;
            if (terrorists == null) return;

            var spawned = new List<TerroristController>();

            foreach (NpcSpawnPoint sp in terrorists)
            {
                ResolveTerroristSpawn(scenario, sp, out Vector3 spawnPos, out Quaternion spawnRot);

                GameObject go = Instantiate(terroristPrefab, spawnPos, spawnRot, _npcsRoot);
                go.name = sp.entityId;

                TerroristController controller = go.GetComponent<TerroristController>();
                if (controller == null)
                {
                    Debug.LogWarning($"[SceneBuilder] Terrorist prefab '{terroristPrefab.name}' " +
                                     $"is missing a TerroristController component.");
                    continue;
                }

                RoleAssignment role = FindRoleAssignment(scenario, sp.entityId);
                NavigationContextEntry nav = FindNavigationContext(scenario, role);

                ConfigureTerrorist(controller, role, nav, scenario);
                spawned.Add(controller);
            }

            // Elect ONE squad Leader from the non-guardian terrorists. The guardian
            // never leads (it must stay on the hostage). Prefer a mobile Roamer so
            // the leader can actually move up to coordinate; fall back to any
            // non-guardian. If every terrorist is a guardian, there's simply no
            // leader and the squad just defends.
            TerroristController leader = null;
            foreach (var t in spawned)
                if (t != null && !t.isHostageGuardian && t.role == NPCRole.Roamer) { leader = t; break; }
            if (leader == null)
                foreach (var t in spawned)
                    if (t != null && !t.isHostageGuardian) { leader = t; break; }
            if (leader != null)
            {
                leader.role = NPCRole.Leader;
                Debug.Log($"[SceneBuilder] Squad leader elected: {leader.NPCId}.");
            }
        }

        /// <summary>
        /// Resolves where a terrorist actually spawns. Module 1 keeps terrorists out of
        /// the layout's ENTRY room, but the exterior breach is resolved from real
        /// geometry and often lands on a different perimeter room — one that may well
        /// have a terrorist standing in it. Anyone sitting in the doorway's line
        /// (visible straight through the entrance from outside) is slid to the far side
        /// of the room and turned to face its interior, so the trainee is never engaged
        /// through the front door before they have crossed the open ground.
        /// </summary>
        private void ResolveTerroristSpawn(ScenarioData scenario, NpcSpawnPoint sp,
                                           out Vector3 pos, out Quaternion rot)
        {
            pos = World(sp.position.ToVector3());
            rot = LookRotation(sp.facingDirection);

            if (!_entranceResolved || sp.roomId != _entranceRoomId) return;

            RoomData room = scenario?.layout?.rooms?.Find(r => r.id == sp.roomId);
            if (room == null) return;

            bool alongX = _entranceSide == WallSide.North || _entranceSide == WallSide.South;

            // Half-width of the cone the trainee can see through the doorway, plus a
            // margin so a shoulder poking into it doesn't count as cover.
            float band = (_doorOpeningWidth + DoorClearance) * 0.5f + 1.0f;

            float entranceLat = alongX ? _entranceWallMidWorld.x : _entranceWallMidWorld.z;
            float npcLat      = alongX ? pos.x : pos.z;
            if (Mathf.Abs(npcLat - entranceLat) >= band) return; // already out of the line

            Vector3 roomWorld = World(room.position.ToVector3());
            float roomCentre  = alongX ? roomWorld.x : roomWorld.z;

            float nominal = room.size == null ? 6f
                          : alongX ? room.size.width : room.size.depth;
            if (nominal <= 0f) nominal = 6f;
            // Same wall margin Module 1's placer uses, so the moved NPC stays off the wall.
            const float WallMargin = 0.8f;
            float halfExtent = Mathf.Max(0f, nominal * 0.5f - WallMargin);

            // Push to whichever side of the room is further from the doorway.
            float sign = entranceLat <= roomCentre ? 1f : -1f;
            float moved = roomCentre + sign * halfExtent;

            if (alongX) pos.x = moved; else pos.z = moved;

            // Face the room's interior rather than staring down the entrance.
            Vector3 inward = roomWorld - pos;
            inward.y = 0f;
            if (inward.sqrMagnitude > 0.0001f) rot = Quaternion.LookRotation(inward);

            Debug.Log($"[SceneBuilder] Terrorist '{sp.entityId}' stood in the entrance line of " +
                      $"'{room.id}'; moved {Mathf.Abs(moved - npcLat):F1} m aside and turned inward " +
                      "so the trainee isn't engaged through the front door.");
        }

        private void ConfigureTerrorist(TerroristController controller,
                                        RoleAssignment role,
                                        NavigationContextEntry nav,
                                        ScenarioData scenario)
        {
            if (role == null)
            {
                Debug.LogWarning($"[SceneBuilder] No RoleAssignment for terrorist " +
                                 $"'{controller.gameObject.name}'. Leaving prefab defaults.");
                return;
            }

            switch (role.role)
            {
                case NpcRole.Patrol:
                    controller.idleMode = IdleMode.Patrol;
                    ConfigurePatrolLine(controller, nav);
                    break;

                case NpcRole.RoamingGuard:
                    controller.idleMode = IdleMode.Wander;
                    controller.wanderRadius = ComputeWanderRadius(nav, scenario);
                    break;

                case NpcRole.StationaryGuard:
                    controller.idleMode = IdleMode.Static;
                    controller.staticFaceTarget = MakeFaceTargetStub(controller.transform, nav);
                    break;

                case NpcRole.HostageGuardian:
                    controller.idleMode = IdleMode.Static;
                    controller.staticFaceTarget = MakeFaceTargetStub(controller.transform, nav);
                    break;
            }

            // ── Module 2 combat role + squad (Module 2's territory per HANDOFF §4) ──
            // All terrorists share one squad so coordination spans the whole site.
            // The HostageGuardian is special: it STAYS on the hostage and never
            // joins the squad's roaming/investigation/flanking — it only engages
            // what it personally sees (set isHostageGuardian = true). The squad
            // Leader is elected separately from a NON-guardian (see SpawnTerrorists)
            // so the guardian never gets pulled off post by leading directives.
            controller.isHostageGuardian = role.role == NpcRole.HostageGuardian;

            NPCRole combatRole = role.role switch
            {
                NpcRole.StationaryGuard => NPCRole.Guard,
                NpcRole.HostageGuardian => NPCRole.Guard,
                NpcRole.Patrol          => NPCRole.Roamer,
                NpcRole.RoamingGuard    => NPCRole.Roamer,
                _                       => NPCRole.Guard,
            };

            controller.AssignRoleAndSquad(combatRole, "squad_alpha");
        }

        private void ConfigurePatrolLine(TerroristController controller, NavigationContextEntry nav)
        {
            if (nav == null || nav.waypoints == null || nav.waypoints.Count == 0)
            {
                Debug.LogWarning($"[SceneBuilder] Patrol terrorist '{controller.gameObject.name}' " +
                                 $"has no waypoints in its navigation context.");
                return;
            }

            PatrolLine patrol = controller.patrolLine;
            if (patrol == null) patrol = controller.GetComponent<PatrolLine>();
            if (patrol == null) patrol = controller.gameObject.AddComponent<PatrolLine>();
            controller.patrolLine = patrol;

            // Hand the FULL ordered waypoint list to PatrolLine (multi-segment
            // routes supported since the Module 2 PatrolLine upgrade). The
            // navigationContext 'looping' flag picks loop vs ping-pong traversal.
            var stubs = new List<Transform>(nav.waypoints.Count);
            for (int i = 0; i < nav.waypoints.Count; i++)
            {
                stubs.Add(MakeWaypointStub(
                    $"Patrol_{i}_{controller.gameObject.name}",
                    nav.waypoints[i].ToVector3()));
            }

            patrol.SetWaypoints(stubs, nav.looping ?? false);

            // Keep the legacy two-point fields populated so older tooling /
            // inspector checks that read pointA/pointB still see a valid line.
            patrol.pointA = stubs[0];
            patrol.pointB = stubs[stubs.Count - 1];
        }

        private float ComputeWanderRadius(NavigationContextEntry nav, ScenarioData scenario)
        {
            const float fallback = 6f;

            if (nav?.roamingRoomIds == null || nav.roamingRoomIds.Count == 0 ||
                scenario.layout?.rooms == null)
                return fallback;

            // Build a centroid of the roaming rooms and take the max distance
            // from that centroid to any of them as the radius.
            Vector3 centroid = Vector3.zero;
            int count = 0;
            var roomPositions = new List<Vector3>(nav.roamingRoomIds.Count);

            foreach (string roomId in nav.roamingRoomIds)
            {
                RoomData room = scenario.layout.rooms.Find(r => r.id == roomId);
                if (room == null) continue;
                Vector3 pos = room.position.ToVector3();
                centroid += pos;
                roomPositions.Add(pos);
                count++;
            }

            if (count == 0) return fallback;

            centroid /= count;
            float maxDist = 0f;
            foreach (Vector3 p in roomPositions)
            {
                float d = Vector3.Distance(centroid, p);
                if (d > maxDist) maxDist = d;
            }

            // Add half a room's radius so the NPC can move within the rooms,
            // not just to their centres. 4 m matches the 8 m large-room half-stride.
            return Mathf.Max(fallback, maxDist + 4f);
        }

        private void BakeNavMesh()
        {
            if (_roomsRoot == null) return;

            // The perimeter-corridor floor bridges the rooms at every doorway, but
            // it is built under its own root (PerimeterCorridor), a SIBLING of Rooms.
            // CollectObjects.Children on the Rooms surface would exclude it, leaving
            // each room's NavMesh an isolated island that NPCs can't path across —
            // which is exactly why terrorists couldn't move room-to-room / through
            // doors. Parent the corridor under Rooms for the bake so it's voxelised
            // too and the rooms connect. (It's cleared each rebuild via Rooms.)
            if (_corridorRoot != null && _corridorRoot.parent != _roomsRoot)
                _corridorRoot.SetParent(_roomsRoot, worldPositionStays: true);

            _navMeshSurface = _roomsRoot.GetComponent<NavMeshSurface>();
            if (_navMeshSurface == null)
                _navMeshSurface = _roomsRoot.gameObject.AddComponent<NavMeshSurface>();

            // Only bake the children of the Rooms container, not the whole scene.
            // Without this scope, NavMeshSurface defaults to CollectObjects.All and
            // tries to voxelise every Renderer in the scene (XRI rig, environment,
            // etc.), which can hang the editor for tens of seconds on Start Mission.
            //
            // This recursively includes each room's procedurally built wall
            // segments (so solid walls block pathing) while the 2 m door openings
            // stay walkable. Doors live under the separate Doors container and are
            // intentionally excluded from the bake: each door leaf carries a
            // carving NavMeshObstacle that severs the navmesh across the doorway
            // only while closed/locked, and GeneratedDoor disables it when open.
            _navMeshSurface.collectObjects = CollectObjects.Children;

            // The safe zone now sits just INSIDE the entrance (see
            // ResolveInteriorSafeZonePosition), so the whole escort route — hostage
            // contact through to extraction — stays entirely within the Rooms
            // bake. No exterior walkable patch is added here on purpose: an earlier
            // version laid a NavMesh apron out to the trainee's outdoor spawn so a
            // following hostage could reach an outdoor zone, but that apron was
            // walkable by every NavMeshAgent, not just the hostage — terrorists
            // ended up wandering outside the building through it.
            try
            {
                _navMeshSurface.BuildNavMesh();
                Debug.Log("[SceneBuilder] NavMesh baked successfully.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SceneBuilder] NavMesh bake failed: {ex.Message}. " +
                                 $"NPCs will not navigate but the scene will still load.");
            }
        }

        /// <summary>
        /// Drops a NavMeshLink across every doorway so the two rooms' NavMeshes are
        /// explicitly stitched together. The generated rooms sit with a ~2 m gap
        /// between their floors (walls meet on a shared plane, but each room's floor
        /// slab stops short), so the baked NavMesh is a set of disconnected islands
        /// and agents can't path room-to-room. A NavMeshLink bridges each opening
        /// regardless of the floor gap — the robust, geometry-independent fix.
        ///
        /// Runs AFTER the bake so the link endpoints land on the freshly-baked mesh.
        /// One link per undirected door pair; parented to Rooms so it's cleared on
        /// rebuild. Links carry every door (incl. locked) — terrorists traverse all
        /// doors; a locked door only blocks the trainee (its hinge stays clamped).
        /// </summary>
        private void BuildDoorNavLinks(ScenarioData scenario)
        {
            // Drive this from the doors we ACTUALLY built (_placedDoors), not from the
            // scenario's room list. The scenario only describes room-to-room doors (the
            // validator requires every door to have a connectsToRoomId pointing at a real
            // room), so walking it silently skipped the perimeter-corridor entry door that
            // SceneBuilder creates on its own — leaving the corridor baked but severed.
            int count = 0;

            foreach (PlacedDoor door in _placedDoors)
            {
                Vector3 p = door.center;
                p.y = _buildOffset.y; // floor level

                // The link spans PERPENDICULAR to the wall the door sits in:
                // North/South walls run along X → cross along Z; East/West → along X.
                bool crossAlongZ = door.side == WallSide.North || door.side == WallSide.South;
                Vector3 span = crossAlongZ ? Vector3.forward : Vector3.right;

                // Reach far enough past the wall plane to land on the NavMesh either side.
                float reach = CorridorGap * 0.5f + 1.5f;

                var go = new GameObject($"DoorNavLink_{door.name}");
                go.transform.SetParent(_roomsRoot, worldPositionStays: true);
                go.transform.position = p;

                var link = go.AddComponent<NavMeshLink>();
                link.startPoint    = -span * reach; // local space (identity rotation)
                link.endPoint      =  span * reach;
                link.width         = Mathf.Max(1.2f, _doorOpeningWidth);
                link.bidirectional = true;
                link.area          = 0; // built-in Walkable
                link.UpdateLink();

                count++;
            }

            Debug.Log($"[SceneBuilder] Placed {count} NavMeshLink(s) across EVERY built doorway " +
                      $"(rooms + perimeter-corridor entry), from {_placedDoors.Count} recorded door(s).");
        }

        /// <summary>
        /// One-shot NavMesh connectivity probe, logged right after the bake (before
        /// any combat/spam). For each terrorist spawn it asks the NavMesh whether a
        /// path exists to the trainee spawn. PathComplete = rooms are linked and the
        /// terrorist CAN walk to you; PathPartial/Invalid = the NavMesh is severed
        /// between their room and yours (they physically cannot reach you no matter
        /// what the AI decides). This is the definitive "are the doors connected?"
        /// answer, independent of any behaviour.
        /// </summary>
        private void LogNavMeshConnectivity(ScenarioData scenario)
        {
            TraineeSpawnPoint t = scenario.spawnPoints?.trainee;
            List<NpcSpawnPoint> terrorists = scenario.spawnPoints?.terrorists;
            if (t == null || terrorists == null) return;

            Vector3 traineeWorld = World(t.position.ToVector3());
            if (!NavMesh.SamplePosition(traineeWorld, out NavMeshHit traineeHit, 4f, NavMesh.AllAreas))
            {
                Debug.LogWarning($"[NavCheck] Trainee spawn {traineeWorld:F1} is NOT on the NavMesh — cannot test connectivity.");
                return;
            }

            foreach (NpcSpawnPoint sp in terrorists)
            {
                Vector3 tWorld = World(sp.position.ToVector3());
                if (!NavMesh.SamplePosition(tWorld, out NavMeshHit tHit, 4f, NavMesh.AllAreas))
                {
                    Debug.LogWarning($"[NavCheck] {sp.entityId} spawn {tWorld:F1} is NOT on the NavMesh " +
                                     "(agent will fail to attach / can't move).");
                    continue;
                }

                var path = new NavMeshPath();
                NavMesh.CalculatePath(tHit.position, traineeHit.position, NavMesh.AllAreas, path);
                float dist = Vector3.Distance(tHit.position, traineeHit.position);
                string verdict = path.status == NavMeshPathStatus.PathComplete
                    ? "CONNECTED — can walk to trainee"
                    : "BLOCKED — NavMesh severed between this room and the trainee's";
                Debug.Log($"[NavCheck] {sp.entityId} → trainee: {path.status} " +
                          $"(straight-line {dist:F1}m, path corners={path.corners.Length}) → {verdict}");
            }
        }

        private void NotifyModule2Ready()
        {
            EventManager mgr = EventManager.Instance;
            if (mgr == null)
            {
                Debug.LogError("[SceneBuilder] EventManager.Instance is null - cannot raise " +
                               "ScenarioReady. Make sure the ScenarioManager GameObject is in " +
                               "the scene.");
                return;
            }
            mgr.NotifyScenarioReady();
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private GameObject SelectRoomPrefab(RoomSizeCategory category)
        {
            switch (category)
            {
                case RoomSizeCategory.Small:  return roomPrefabSmall;
                case RoomSizeCategory.Medium: return roomPrefabMedium;
                case RoomSizeCategory.Large:  return roomPrefabLarge;
                default:                      return roomPrefabMedium;
            }
        }

        private static Quaternion DoorRotation(WallSide side)
        {
            // North/south doors lie along the X axis (0deg);
            // east/west doors lie along the Z axis (90deg).
            switch (side)
            {
                case WallSide.North:
                case WallSide.South:
                    return Quaternion.identity;
                case WallSide.East:
                case WallSide.West:
                    return Quaternion.Euler(0f, 90f, 0f);
                default:
                    return Quaternion.identity;
            }
        }

        private static Quaternion LookRotation(SerializableVector3 dir)
        {
            if (dir == null) return Quaternion.identity;
            Vector3 v = dir.ToVector3();
            v.y = 0f;
            return v.sqrMagnitude < 0.0001f
                ? Quaternion.identity
                : Quaternion.LookRotation(v);
        }

        private static string MakeDoorPairKey(string a, string b)
        {
            if (string.IsNullOrEmpty(b)) return a ?? "";
            return string.CompareOrdinal(a, b) < 0 ? $"{a}__{b}" : $"{b}__{a}";
        }

        private Transform GetOrCreateChild(string name)
        {
            Transform t = transform.Find(name);
            if (t != null) return t;
            var go = new GameObject(name);
            go.transform.SetParent(transform, worldPositionStays: false);
            return go.transform;
        }

        private static void DestroyChildren(Transform parent)
        {
            if (parent == null) return;
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                GameObject child = parent.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child);
                else                       DestroyImmediate(child);
            }
        }

        private static EntityRecord FindEntity(ScenarioData scenario, string entityId)
        {
            if (scenario.entities == null) return null;
            return scenario.entities.Find(e => e.id == entityId);
        }

        private static RoleAssignment FindRoleAssignment(ScenarioData scenario, string entityId)
        {
            if (scenario.roleAssignments == null) return null;
            return scenario.roleAssignments.Find(r => r.entityId == entityId);
        }

        private static NavigationContextEntry FindNavigationContext(
            ScenarioData scenario, RoleAssignment role)
        {
            if (role == null || string.IsNullOrEmpty(role.navigationContextId)) return null;
            if (scenario.navigationContext == null) return null;
            scenario.navigationContext.TryGetValue(role.navigationContextId, out var entry);
            return entry;
        }

        private static HostageState ParseHostageState(string raw)
        {
            // EntityMetadata.initialState defaults to "idle" for terrorists,
            // but hostages should start "calm" per the scenario contract.
            if (string.IsNullOrEmpty(raw)) return HostageState.Calm;
            if (Enum.TryParse(raw, ignoreCase: true, out HostageState parsed))
                return parsed;
            return HostageState.Calm;
        }

        private static Transform MakeFaceTargetStub(Transform npc, NavigationContextEntry nav)
        {
            Vector3 dir = nav?.facingDirection?.ToVector3() ?? npc.forward;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = npc.forward;

            var go = new GameObject($"FaceTarget_{npc.name}");
            go.transform.SetParent(npc, worldPositionStays: false);
            go.transform.position = npc.position + dir.normalized * 5f;
            return go.transform;
        }

        private static Transform MakeWaypointStub(string name, Vector3 worldPos)
        {
            // Parent to the scene root (not the NPC) so the waypoint stays put
            // as the NPC walks toward it.
            var go = new GameObject(name);
            go.transform.position = worldPos;
            return go.transform;
        }

        private void Fail(string reason)
        {
            Debug.LogError($"[SceneBuilder] {reason}");
            OnSceneBuildFailed?.Invoke(reason);
        }

        // ── Gizmo overlay for spawn points ───────────────────────────────────

        // Colour palette for the spawn-point markers.
        private static readonly Color GizmoTrainee   = new Color(0.30f, 1.00f, 0.40f);
        private static readonly Color GizmoHostage   = new Color(0.30f, 0.65f, 1.00f);
        private static readonly Color GizmoTerrorist = new Color(1.00f, 0.30f, 0.30f);

        private void OnDrawGizmos()
        {
            if (!showSpawnGizmos || ActiveScenario?.spawnPoints == null) return;
            var sp = ActiveScenario.spawnPoints;

            if (sp.trainee != null)
                DrawSpawnGizmo(sp.trainee.position, sp.trainee.facingDirection,
                               GizmoTrainee, "trainee_01", 0.55f, _buildOffset);

            if (sp.hostages != null)
                foreach (var h in sp.hostages)
                    DrawSpawnGizmo(h.position, h.facingDirection,
                                   GizmoHostage, h.entityId, 0.45f, _buildOffset);

            if (sp.terrorists != null)
                foreach (var t in sp.terrorists)
                    DrawSpawnGizmo(t.position, t.facingDirection,
                                   GizmoTerrorist, t.entityId, 0.45f, _buildOffset);
        }

        private static void DrawSpawnGizmo(SerializableVector3 pos,
                                           SerializableVector3 facing,
                                           Color color, string label, float radius,
                                           Vector3 offset)
        {
            if (pos == null) return;

            Vector3 p   = pos.ToVector3() + offset + Vector3.up * 0.1f;
            Vector3 dir = facing != null ? facing.ToVector3() : Vector3.forward;
            dir.y = 0f;

            // Wireframe sphere is more readable than solid - solid gizmos
            // can occlude the actual NPCs.
            Gizmos.color = color;
            Gizmos.DrawWireSphere(p, radius);

            // Facing arrow: a 1.5 m line in the facing direction, capped with
            // a small wire sphere so it reads as an arrowhead.
            if (dir.sqrMagnitude > 0.0001f)
            {
                Vector3 tip = p + dir.normalized * 1.5f;
                Gizmos.DrawLine(p, tip);
                Gizmos.DrawWireSphere(tip, 0.08f);
            }

#if UNITY_EDITOR
            // Labels are editor-only; this whole gizmo path stops compiling
            // out in player builds via the #if guard so it's free at runtime.
            var style = new GUIStyle
            {
                normal = { textColor = color },
                fontStyle = FontStyle.Bold,
            };
            UnityEditor.Handles.Label(p + Vector3.up * (radius + 0.4f), label, style);
#endif
        }
    }

    /// <summary>
    /// Inspector-editable mapping from a Module 1 <see cref="FurnitureType"/> to a
    /// real prefab. Wire entries on the <see cref="SceneBuilder"/> to replace the
    /// greybox box for that type; any unmapped type keeps its scaled box. Mapped
    /// prefabs are re-scaled to the generated footprint at build time, so the
    /// placement stays collision-correct whatever the source art's native size is.
    /// </summary>
    [Serializable]
    public class FurniturePrefabMapping
    {
        [Tooltip("Furniture category this prefab represents.")]
        public FurnitureType type;

        [Tooltip("Prefab to spawn for this type. Scaled to the generated footprint.")]
        public GameObject prefab;
    }
}
