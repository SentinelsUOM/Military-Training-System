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
//   * PatrolLine accepts only two waypoints (pointA / pointB) - the navigation
//     context's waypoints[] is collapsed to first/last for the patrol segment.
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

        [Tooltip("Optional staging spawn. When set, the trainee spawns HERE (e.g. " +
                 "by the briefing table in your template) instead of inside the " +
                 "generated building, then walks/teleports over to it. Leave empty " +
                 "to spawn at the generated entry as before.")]
        public Transform traineeStartPoint;

        [Tooltip("When the trainee spawns at a staging point, start the building's " +
                 "entry door(s) open so the trainee can walk straight in. Interior " +
                 "doors keep their generated state.")]
        public bool entryDoorStartsOpen = true;

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

        private const string ROOMS_ROOT = "Rooms";
        private const string DOORS_ROOT = "Doors";
        private const string NPCS_ROOT  = "NPCs";

        // ── Wall geometry (mirrors ScenePrefabBuilder so walls, floors and doors
        //    line up exactly) ──────────────────────────────────────────────────
        // Module 1 leaves a 2 m corridor gap between adjacent room centres; each
        // room's outer wall sits half that gap (1 m) beyond its nominal extent so
        // neighbouring walls meet on a shared plane and the 2 m door opening lands
        // flush in it.
        private const float CorridorGap   = 2f;   // gap Module 1 leaves between rooms
        private const float DoorGap       = 2f;   // fallback door opening width (greybox door)
        private const float WallThickness = 0.12f;
        // Height of the door opening. Above this, a header (transom) fills the
        // wall up to the ceiling so doorways aren't open to the full wall height.
        // Matches the door prefab's leaf top and jamb height (2.1 m).
        private const float DoorOpeningHeight = 2.1f;
        // Extra width/height carved around the measured door so the real leaf
        // swings without scraping the jambs.
        private const float DoorClearance = 0.08f;

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

                MeasureDoorPrefab();
                ComputeEntryOpenings(scenario);
                BuildRooms(scenario);
                BuildDoors(scenario);
                BuildEntryDoors(scenario);
                PositionTrainee(scenario);
                SpawnHostages(scenario);
                SpawnTerrorists(scenario);
                BakeNavMesh();

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
            DestroyChildren(_roomsRoot);
            DestroyChildren(_doorsRoot);
            DestroyChildren(_npcsRoot);

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

        // ── Build pipeline ───────────────────────────────────────────────────

        private void EnsureContainers()
        {
            _roomsRoot = GetOrCreateChild(ROOMS_ROOT);
            _doorsRoot = GetOrCreateChild(DOORS_ROOT);
            _npcsRoot  = GetOrCreateChild(NPCS_ROOT);
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

                BuildWalls(room, go);
            }
        }

        /// <summary>
        /// Procedurally builds the four walls of a room from its door data. A
        /// wall side that carries a door gets two segments framing a 2 m opening;
        /// a side with no door gets a single solid wall. This makes the geometry
        /// match connectivity exactly — no more open holes on unconnected walls,
        /// and the building perimeter is fully enclosed.
        /// </summary>
        private void BuildWalls(RoomData room, GameObject roomGo)
        {
            Material mat = ResolveWallMaterial(roomGo);

            float height = room.size != null && room.size.height > 0f ? room.size.height : 3f;
            float width  = room.size != null && room.size.width  > 0f ? room.size.width  : 6f;
            float depth  = room.size != null && room.size.depth  > 0f ? room.size.depth  : 6f;

            // Outer extents: nominal size extended by half the corridor gap on
            // each side, so adjacent rooms' walls meet on the shared plane.
            float halfW = (width + CorridorGap) * 0.5f;
            float halfD = (depth + CorridorGap) * 0.5f;
            float outerW = width + CorridorGap;
            float outerD = depth + CorridorGap;

            var doorSides = new HashSet<WallSide>();
            if (room.doors != null)
                foreach (DoorData d in room.doors) doorSides.Add(d.wallSide);

            // Exterior building entrances also need an opening in the perimeter
            // wall, otherwise the trainee spawns outside a sealed building.
            if (_entryOpenings.TryGetValue(room.id, out List<EntryOpening> openings))
                foreach (EntryOpening e in openings) doorSides.Add(e.side);

            // Carve openings sized to the measured door so the real leaf fits.
            float openW = _doorOpeningWidth  + DoorClearance;
            float openH = _doorOpeningHeight + DoorClearance;

            // North / South run along X (span = outerW, thin in Z).
            BuildWallSide(roomGo, "South", doorSides.Contains(WallSide.South),
                          axisAlongX: true, fixedCoord: -halfD, span: outerW, height: height,
                          openW: openW, openH: openH, mat: mat);
            BuildWallSide(roomGo, "North", doorSides.Contains(WallSide.North),
                          axisAlongX: true, fixedCoord:  halfD, span: outerW, height: height,
                          openW: openW, openH: openH, mat: mat);

            // East / West run along Z (span = outerD, thin in X).
            BuildWallSide(roomGo, "East", doorSides.Contains(WallSide.East),
                          axisAlongX: false, fixedCoord:  halfW, span: outerD, height: height,
                          openW: openW, openH: openH, mat: mat);
            BuildWallSide(roomGo, "West", doorSides.Contains(WallSide.West),
                          axisAlongX: false, fixedCoord: -halfW, span: outerD, height: height,
                          openW: openW, openH: openH, mat: mat);
        }

        /// <summary>
        /// Builds one side of a room: either a single solid wall, or two segments
        /// framing a centred <see cref="DoorGap"/>-wide opening when a door is
        /// present. <paramref name="axisAlongX"/> selects whether the wall runs
        /// along X (north/south) or Z (east/west).
        /// </summary>
        private void BuildWallSide(GameObject roomGo, string sideName, bool hasDoor,
                                   bool axisAlongX, float fixedCoord, float span,
                                   float height, float openW, float openH, Material mat)
        {
            if (!hasDoor)
            {
                AddWallSegment(roomGo, $"Wall_{sideName}", axisAlongX, fixedCoord,
                               offset: 0f, length: span, height: height, mat: mat);
                return;
            }

            float segLen = (span - openW) * 0.5f;
            if (segLen <= 0f) return; // opening as wide as the wall — leave it open

            // Full-height segments either side of the opening.
            float segOffset = openW * 0.5f + segLen * 0.5f;
            AddWallSegment(roomGo, $"Wall_{sideName}_A", axisAlongX, fixedCoord,
                           offset: -segOffset, length: segLen, height: height, mat: mat);
            AddWallSegment(roomGo, $"Wall_{sideName}_B", axisAlongX, fixedCoord,
                           offset:  segOffset, length: segLen, height: height, mat: mat);

            // Header (transom) filling the wall above the door opening, so the
            // doorway isn't open all the way to the ceiling.
            float headerHeight = height - openH;
            if (headerHeight > 0.01f)
                AddWallSegment(roomGo, $"Wall_{sideName}_Header", axisAlongX, fixedCoord,
                               offset: 0f, length: openW, height: headerHeight,
                               mat: mat, baseY: openH);
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

                foreach (DoorData door in room.doors)
                {
                    string pairKey = MakeDoorPairKey(room.id, door.connectsToRoomId);
                    if (!_placedDoorPairs.Add(pairKey))
                        continue; // reciprocal already placed

                    PlaceDoor(door.id, World(door.position.ToVector3()), door.wallSide, door.state);
                }
            }
        }

        /// <summary>
        /// Maps each building entry point to the exterior wall it pierces, so
        /// BuildWalls can carve an opening there and BuildEntryDoors can place a
        /// door. Entry points sit exactly on the room's outer wall plane, so the
        /// wall side is just the dominant axis of (entryPos − roomCentre).
        /// </summary>
        private void ComputeEntryOpenings(ScenarioData scenario)
        {
            _entryOpenings.Clear();

            List<EntryPointData> entryPoints = scenario.layout?.entryPoints;
            if (entryPoints == null) return;

            foreach (EntryPointData ep in entryPoints)
            {
                if (ep == null || string.IsNullOrEmpty(ep.roomId)) continue;

                RoomData room = scenario.layout.rooms.Find(r => r.id == ep.roomId);
                if (room == null) continue;

                Vector3 delta = ep.position.ToVector3() - room.position.ToVector3();
                WallSide side = WallSideFromDelta(delta);

                // If a neighbouring room already sits on this side (interior door
                // present), the wall is not exterior — skip to avoid two doors
                // overlapping in the same opening.
                if (room.doors != null && room.doors.Exists(d => d.wallSide == side))
                    continue;

                if (!_entryOpenings.TryGetValue(ep.roomId, out List<EntryOpening> list))
                {
                    list = new List<EntryOpening>();
                    _entryOpenings[ep.roomId] = list;
                }
                list.Add(new EntryOpening
                {
                    side     = side,
                    position = World(ep.position.ToVector3()),
                    id       = ep.id
                });
            }
        }

        /// <summary>
        /// Places an exterior door at every building entry point. These are the
        /// breach points into the building; they start closed so the trainee
        /// opens them on the way in. The interior wall already has the matching
        /// opening carved by BuildWalls.
        /// </summary>
        private void BuildEntryDoors(ScenarioData scenario)
        {
            if (doorPrefab == null || _entryOpenings.Count == 0) return;

            foreach (KeyValuePair<string, List<EntryOpening>> kvp in _entryOpenings)
            {
                DoorState entryState = entryDoorStartsOpen ? DoorState.Open : DoorState.Closed;
                foreach (EntryOpening opening in kvp.Value)
                {
                    PlaceDoor($"door_{opening.id}", opening.position, opening.side, entryState);
                }
            }
        }

        private static WallSide WallSideFromDelta(Vector3 delta)
        {
            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.z))
                return delta.x > 0 ? WallSide.East : WallSide.West;
            return delta.z > 0 ? WallSide.North : WallSide.South;
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
        }

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
            if (traineeRig == null)
            {
                Debug.LogError("[SceneBuilder] traineeRig is not assigned - cannot position player.");
                return;
            }

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
                return;
            }

            TraineeSpawnPoint t = scenario.spawnPoints?.trainee;
            if (t == null)
            {
                Debug.LogError("[SceneBuilder] spawnPoints.trainee is null - leaving rig at origin.");
                return;
            }

            traineeRig.position = World(t.position.ToVector3());
            traineeRig.rotation = LookRotation(t.facingDirection);
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
            }
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

            foreach (NpcSpawnPoint sp in terrorists)
            {
                GameObject go = Instantiate(terroristPrefab,
                                            World(sp.position.ToVector3()),
                                            LookRotation(sp.facingDirection),
                                            _npcsRoot);
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
            }
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

            // PatrolLine only supports two waypoints; collapse to first/last.
            Vector3 first = World(nav.waypoints[0].ToVector3());
            Vector3 last  = World(nav.waypoints[nav.waypoints.Count - 1].ToVector3());

            patrol.pointA = MakeWaypointStub($"Patrol_A_{controller.gameObject.name}", first);
            patrol.pointB = MakeWaypointStub($"Patrol_B_{controller.gameObject.name}", last);
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

            try
            {
                _navMeshSurface.BuildNavMesh();
                Debug.Log("[SceneBuilder] NavMesh baked successfully");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SceneBuilder] NavMesh bake failed: {ex.Message}. " +
                                 $"NPCs will not navigate but the scene will still load.");
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
}
