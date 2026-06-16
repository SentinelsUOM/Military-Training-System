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

        [Header("NPC Prefabs")]
        [Tooltip("Terrorist prefab. Must have a TerroristController component.")]
        public GameObject terroristPrefab;

        [Tooltip("Hostage prefab. Must have a HostageController component.")]
        public GameObject hostagePrefab;

        [Header("Player")]
        [Tooltip("Existing XR Origin / trainee rig in the scene. We only " +
                 "reposition this; we never instantiate a new rig.")]
        public Transform traineeRig;

        [Header("Safe Zone / Extraction")]
        [Tooltip("Spawn a visible 'safe spot' (extraction zone) at the trainee's start " +
                 "position. Lead a rescued hostage back into it to complete the mission.")]
        public bool createSafeZone = true;

        [Tooltip("Radius (metres) of the safe-spot trigger and its floor marker.")]
        public float safeZoneRadius = 2.5f;

        [Tooltip("Colour of the safe-spot floor marker.")]
        public Color safeZoneColor = new Color(0.2f, 1f, 0.4f, 1f);

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

        private Transform _roomsRoot;
        private Transform _doorsRoot;
        private Transform _npcsRoot;

        private readonly Dictionary<string, GameObject> _roomObjects =
            new Dictionary<string, GameObject>();

        private readonly HashSet<string> _placedDoorPairs = new HashSet<string>();

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

                BuildRooms(scenario);
                BuildDoors(scenario);
                PositionTrainee(scenario);
                CreateSafeZone(scenario);
                // Bake BEFORE spawning NPCs: NavMeshAgent attaches to the mesh in
                // OnEnable, so spawning first throws "Failed to create agent because
                // it is not close enough to the NavMesh" and leaves agents dead
                // (NPCs can shoot but never walk).
                BakeNavMesh();
                SpawnHostages(scenario);
                SpawnTerrorists(scenario);

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

            if (_safeZone != null)
            {
                if (Application.isPlaying) Destroy(_safeZone); else DestroyImmediate(_safeZone);
                _safeZone = null;
            }

            _roomObjects.Clear();
            _placedDoorPairs.Clear();

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
                                            room.position.ToVector3(),
                                            Quaternion.identity,
                                            _roomsRoot);
                go.name = room.id;
                _roomObjects[room.id] = go;
            }
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

                    Quaternion rot = DoorRotation(door.wallSide);
                    GameObject go = Instantiate(doorPrefab,
                                                door.position.ToVector3(),
                                                rot,
                                                _doorsRoot);
                    go.name = door.id;
                }
            }
        }

        private void PositionTrainee(ScenarioData scenario)
        {
            if (traineeRig == null)
            {
                Debug.LogError("[SceneBuilder] traineeRig is not assigned - cannot position player.");
                return;
            }

            TraineeSpawnPoint t = scenario.spawnPoints?.trainee;
            if (t == null)
            {
                Debug.LogError("[SceneBuilder] spawnPoints.trainee is null - leaving rig at origin.");
                return;
            }

            traineeRig.position = t.position.ToVector3();
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
                                            sp.position.ToVector3(),
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
            }
        }

        /// <summary>
        /// Spawns a visible "safe spot" (extraction zone) at the trainee's start
        /// position. The trainee leads a rescued (Following) hostage back into this
        /// zone to complete the mission. A flat coloured disc marks it on the floor;
        /// a trigger SphereCollider + ExtractionZone component do the detection.
        /// </summary>
        private void CreateSafeZone(ScenarioData scenario)
        {
            if (!createSafeZone) return;

            TraineeSpawnPoint t = scenario.spawnPoints?.trainee;
            if (t == null) return;

            Vector3 pos = t.position.ToVector3();

            _safeZone = new GameObject("SafeZone_Extraction");
            _safeZone.transform.position = pos;

            // Trigger volume + detection logic.
            var sphere = _safeZone.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = safeZoneRadius;
            _safeZone.AddComponent<ExtractionZone>();

            // Flat floor disc so the trainee can SEE the safe spot from the start.
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "Marker";
            marker.transform.SetParent(_safeZone.transform, worldPositionStays: false);
            marker.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            marker.transform.localScale    = new Vector3(safeZoneRadius * 2f, 0.02f, safeZoneRadius * 2f);

            // The marker is purely visual — drop its collider so it can't block movement.
            var markerCol = marker.GetComponent<Collider>();
            if (markerCol != null)
            {
                if (Application.isPlaying) Destroy(markerCol); else DestroyImmediate(markerCol);
            }

            // Colour it (URP uses _BaseColor; set both for safety).
            var rend = marker.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = rend.material;
                mat.color = safeZoneColor;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", safeZoneColor);
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            Debug.Log($"[SceneBuilder] Safe zone created at trainee spawn {pos} (radius {safeZoneRadius}m).");
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
                                            sp.position.ToVector3(),
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

            // ── Module 2 combat role + squad (Module 2's territory per HANDOFF §4) ──
            // Without this, generated missions have no squads and no Leader, so
            // Ring-1 alert propagation and Converge/Flank directives never fire.
            // Mapping rationale:
            //   HostageGuardian → Leader (mission-critical, commands the squad)
            //   StationaryGuard → Guard  (role bonus on RoomBreached)
            //   Patrol / RoamingGuard → Roamer (mobile; role bonus on GunshotHeard)
            // All terrorists in a scenario share one squad so coordination spans
            // the whole site.
            NPCRole combatRole = role.role switch
            {
                NpcRole.HostageGuardian => NPCRole.Leader,
                NpcRole.StationaryGuard => NPCRole.Guard,
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

            _navMeshSurface = _roomsRoot.GetComponent<NavMeshSurface>();
            if (_navMeshSurface == null)
                _navMeshSurface = _roomsRoot.gameObject.AddComponent<NavMeshSurface>();

            // Only bake the children of the Rooms container, not the whole scene.
            // Without this scope, NavMeshSurface defaults to CollectObjects.All and
            // tries to voxelise every Renderer in the scene (XRI rig, environment,
            // etc.), which can hang the editor for tens of seconds on Start Mission.
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
                               GizmoTrainee, "trainee_01", 0.55f);

            if (sp.hostages != null)
                foreach (var h in sp.hostages)
                    DrawSpawnGizmo(h.position, h.facingDirection,
                                   GizmoHostage, h.entityId, 0.45f);

            if (sp.terrorists != null)
                foreach (var t in sp.terrorists)
                    DrawSpawnGizmo(t.position, t.facingDirection,
                                   GizmoTerrorist, t.entityId, 0.45f);
        }

        private static void DrawSpawnGizmo(SerializableVector3 pos,
                                           SerializableVector3 facing,
                                           Color color, string label, float radius)
        {
            if (pos == null) return;

            Vector3 p   = pos.ToVector3() + Vector3.up * 0.1f;
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
