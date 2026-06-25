// =============================================================================
// ScenePrefabBuilder.cs
// Module 1 - Dynamic Scenario Generation (Editor tooling)
// Team Sentinels | University of Moratuwa | 2026
//
// One-shot editor command that constructs the room / door prefabs SceneBuilder
// needs and a ground plane in the active scene, all from Unity primitives.
// Re-running is idempotent: existing prefabs of the same name are deleted and
// rebuilt, and any pre-existing GroundPlane is replaced.
//
// Lives in Assembly-CSharp-Editor (SceneBuilder/Editor/) so it can reference
// SceneBuilder directly.
// =============================================================================

#if UNITY_EDITOR
using System.IO;
using MikeNspired.XRIStarterKit;
using TeamSentinels.ScenarioGeneration.Scene;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace TeamSentinels.ScenarioGeneration.EditorTools
{
    /// <summary>
    /// Builds primitive-based room / door prefabs and a ground plane, then
    /// wires the prefabs into the active scene's <see cref="SceneBuilder"/>.
    /// Triggered by the "Tools / Scenario Generator / Build Scene Prefabs"
    /// menu item.
    /// </summary>
    public static class ScenePrefabBuilder
    {
        // ── Constants ────────────────────────────────────────────────────────

        private const string MenuPath        = "Tools/Scenario Generator/Build Scene Prefabs";
        private const string FinalizeDoorMenuPath = "Tools/Scenario Generator/Finalize Real Door From Selection";
        private const string PrefabsFolder   = "Assets/Prefabs";
        private const string MaterialsFolder = "Assets/Prefabs/Materials";
        private const string GroundPlaneName = "GroundPlane";

        // Hand-built ("realistic") art the generator should reuse. Folder name is
        // spelled "Metrials" on disk — keep it exact.
        private const string RealMaterialsFolder = "Assets/Basemap Metrials";
        private const string RealWallMaterial    = "Assets/Basemap Metrials/Wall_Outside.mat";
        private const string RealFloorMaterial   = "Assets/Basemap Metrials/Floor_Interier.mat";
        private const string RealDoorPrefabPath  = "Assets/Prefabs/RealDoor.prefab";

        // Geometry constants - match Module 1's room grid (CLAUDE.md "Room Size Constants").
        private const float DoorGap       = 2f;     // 2 m centre gap on every wall
        private const float WallHeight    = 3f;
        private const float WallThickness = 0.12f;
        private const float FloorThickness = 0.08f;

        // Greyboxing palette
        private static readonly Color FloorColor  = new Color(0.28f, 0.28f, 0.30f);
        private static readonly Color WallColor   = new Color(0.65f, 0.66f, 0.68f);
        private static readonly Color DoorColor   = new Color(0.42f, 0.28f, 0.18f);
        private static readonly Color FrameColor  = new Color(0.20f, 0.16f, 0.12f);
        private static readonly Color GroundColor = new Color(0.16f, 0.18f, 0.20f);
        // Frosted-glass pane: light and glassy-looking but fully opaque, so the
        // trainee can't see room contents through the window.
        private static readonly Color GlassColor  = new Color(0.80f, 0.86f, 0.90f);

        // ── Menu entry ───────────────────────────────────────────────────────

        [MenuItem(MenuPath, priority = 60)]
        public static void BuildAll()
        {
            try
            {
                EnsureFolder(PrefabsFolder);
                EnsureFolder(MaterialsFolder);

                Material floorMat  = CreateOrUpdateMaterial("Floor_M",     FloorColor);
                Material wallMat   = CreateOrUpdateMaterial("Wall_M",      WallColor);
                Material doorMat   = CreateOrUpdateMaterial("Door_M",      DoorColor);
                Material frameMat  = CreateOrUpdateMaterial("DoorFrame_M", FrameColor);
                Material groundMat = CreateOrUpdateMaterial("Ground_M",    GroundColor);
                Material glassMat  = CreateGlassMaterial();

                string smallPath = BuildRoomPrefab("RoomSmall",  4f, floorMat, wallMat);
                string medPath   = BuildRoomPrefab("RoomMedium", 6f, floorMat, wallMat);
                string largePath = BuildRoomPrefab("RoomLarge",  8f, floorMat, wallMat);
                string doorPath  = BuildDoorPrefab(doorMat, frameMat);

                EnsureGroundPlane(groundMat);

                int wired = WireSceneBuilder(smallPath, medPath, largePath, doorPath, wallMat, glassMat);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

                EditorUtility.DisplayDialog(
                    "Build Scene Prefabs - Success",
                    "Created prefabs in " + PrefabsFolder + "/:\n" +
                    "    RoomSmall, RoomMedium, RoomLarge, DoorVisual\n\n" +
                    "Materials in " + MaterialsFolder + "/:\n" +
                    "    Floor_M, Wall_M, Door_M, DoorFrame_M, Ground_M, Glass_M\n\n" +
                    "Ground plane added to scene: " + GroundPlaneName + "\n\n" +
                    "SceneBuilder slots auto-wired: " + wired + "/4\n" +
                    (wired < 4
                        ? "(No SceneBuilder found in scene - drag the prefabs onto its slots manually.)"
                        : "Press Play, click Generate Scenario, click Start Mission."),
                    "OK");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ScenePrefabBuilder] {ex.Message}\n{ex.StackTrace}");
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog(
                    "Build Scene Prefabs - Failed",
                    "Error: " + ex.Message + "\n\nSee the Console for the full stack trace.",
                    "OK");
            }
        }

        // ── Room prefab construction ─────────────────────────────────────────

        // Builds a square room floor of the given nominal width (matches Module
        // 1's room.size). The floor is extended outward by CorridorHalf (1 m) so
        // adjacent rooms meet exactly in the middle of Module 1's 2 m corridor
        // gap. Walls are no longer baked into the prefab: SceneBuilder builds them
        // procedurally at runtime from each room's door data, so a wall is solid
        // unless a door is defined on it. Saves to disk as a prefab and returns
        // the path. (wallMat is kept for signature symmetry / future use.)
        private static string BuildRoomPrefab(string name, float nominalSize, Material floorMat, Material wallMat)
        {
            const float CorridorHalf = 1f;                     // half of the 2 m corridor gap Module 1 leaves between rooms
            float outerSize          = nominalSize + 2f * CorridorHalf;

            var root = new GameObject(name);

            // ── Floor (extended into half the corridor gap on every side) ───
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(root.transform, false);
            floor.transform.localPosition = new Vector3(0f, FloorThickness * 0.5f, 0f);
            floor.transform.localScale    = new Vector3(outerSize, FloorThickness, outerSize);
            floor.GetComponent<Renderer>().sharedMaterial = floorMat;

            string path = $"{PrefabsFolder}/{name}.prefab";
            AssetDatabase.DeleteAsset(path); // idempotent rebuild
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return path;
        }

        private static GameObject AddWall(GameObject parent, string name,
                                          Vector3 localPos, Vector3 localScale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale    = localScale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        // Removes the auto-created BoxCollider from a primitive used as pure
        // decoration (door frame pieces), so it cannot push the physics leaf.
        private static void StripCollider(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
        }

        // ── Door prefab construction ─────────────────────────────────────────

        // Interactive hinged door: doorframe (two decorative jambs + lintel) + a
        // physics swing leaf the trainee grabs and pushes. The leaf is the only
        // Rigidbody (no nested rigidbodies — those made the leaf sag and tilt);
        // NPC proximity is detected by GeneratedDoor via Physics.OverlapBox, so
        // the root needs no Rigidbody or trigger. The leaf carries a carving
        // NavMeshObstacle (blocks NPC pathing while shut). Default orientation:
        // span along X, so SceneBuilder's identity rotation maps to N/S walls and
        // 90deg-Y maps to E/W walls.
        private static string BuildDoorPrefab(Material doorMat, Material frameMat)
        {
            var root = new GameObject("DoorVisual");

            // Match the wall opening exactly so there are no visible gaps
            // either side of the door when placed at the corridor centre.
            float frameSpan      = DoorGap;             // 2 m - matches wall opening
            float halfFrame      = frameSpan * 0.5f;
            const float panelH   = 2.0f;
            const float jambH    = 2.1f;                // = floorClear + panelH; matches wall opening height
            const float jambD    = 0.18f;
            const float jambW    = 0.12f;
            const float floorClear = 0.1f;              // lift leaf off the floor to avoid penetration

            // Side jambs only — the wall header (built by SceneBuilder above the
            // opening) forms the top of the doorway, so no lintel is needed here.
            // Jambs are decoration; strip their colliders so they never push the
            // physics leaf (the room wall segments do the real blocking).
            StripCollider(AddWall(root, "JambLeft",
                new Vector3(-halfFrame + jambW * 0.5f, jambH * 0.5f, 0f),
                new Vector3(jambW, jambH, jambD), frameMat));
            StripCollider(AddWall(root, "JambRight",
                new Vector3( halfFrame - jambW * 0.5f, jambH * 0.5f, 0f),
                new Vector3(jambW, jambH, jambD), frameMat));

            // ── Swing leaf: the leaf's PIVOT is placed on the hinge edge (inner
            //    face of the left jamb) and the mesh/collider is offset across
            //    the opening from there. This way the leaf rotates about its own
            //    transform origin = the hinge edge, so the physics hinge and the
            //    kinematic NPC/test drive both swing about the same line. (If the
            //    pivot were at the centre, the kinematic drive would spin the
            //    door about its middle instead of its edge.) ───────────────────
            float panelW    = frameSpan - jambW * 2f;     // 1.76 m for a 2 m opening
            float halfPanel = panelW * 0.5f;
            float leafCY    = floorClear + panelH * 0.5f; // leaf centre height (bottom at floorClear)

            var leaf = new GameObject("Leaf");
            leaf.transform.SetParent(root.transform, false);
            leaf.transform.localPosition = new Vector3(-halfPanel, 0f, 0f); // pivot on the hinge edge

            var leafBody = leaf.AddComponent<Rigidbody>();
            leafBody.useGravity  = false;   // vertical hinge; gravity would only add drift
            leafBody.isKinematic = false;
            leafBody.mass        = 10f;
            leafBody.angularDamping  = 3f;     // damp wobble after a push
            // Hinge fixes position; freezing X/Z rotation stops any forward/side
            // tilt so the leaf can only swing about its vertical hinge.
            leafBody.constraints = RigidbodyConstraints.FreezeRotationX |
                                   RigidbodyConstraints.FreezeRotationZ;
            leafBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            leafBody.interpolation          = RigidbodyInterpolation.Interpolate;

            var hinge = leaf.AddComponent<HingeJoint>();
            hinge.anchor    = new Vector3(0f, leafCY, 0f); // at the leaf pivot = hinge edge
            hinge.axis      = new Vector3(0f, 1f, 0f);     // swing about vertical
            hinge.useLimits = true;
            hinge.limits    = new JointLimits { min = 0f, max = 90f };

            var grab = leaf.AddComponent<XRGrabInteractable>();
            grab.movementType   = XRGrabInteractable.MovementType.VelocityTracking; // keep the joint in charge
            grab.throwOnDetach  = false;
            grab.useDynamicAttach = true;   // grab anywhere on the leaf

            var obstacle = leaf.AddComponent<NavMeshObstacle>();
            obstacle.shape   = NavMeshObstacleShape.Box;
            obstacle.carving = true;
            obstacle.center  = new Vector3(halfPanel, leafCY, 0f); // mesh spans from the pivot edge
            obstacle.size    = new Vector3(panelW, panelH, 0.4f);  // widen carve depth past the thin leaf

            // Visual + solid collider child, offset so it spans from the hinge
            // edge (the leaf pivot) across the opening.
            var leafMesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leafMesh.name = "LeafMesh";
            leafMesh.transform.SetParent(leaf.transform, false);
            leafMesh.transform.localPosition = new Vector3(halfPanel, leafCY, 0f);
            leafMesh.transform.localScale    = new Vector3(panelW, panelH, 0.06f);
            leafMesh.GetComponent<Renderer>().sharedMaterial = doorMat;
            var leafCollider = leafMesh.GetComponent<BoxCollider>();
            if (leafCollider != null) leafCollider.isTrigger = false;

            var door = root.AddComponent<GeneratedDoor>();
            door.leafBody    = leafBody;
            door.hinge       = hinge;
            door.grab        = grab;
            door.navObstacle = obstacle;
            door.sensorCenter      = new Vector3(0f, leafCY, 0f);
            door.sensorHalfExtents = new Vector3(halfFrame + 0.4f, panelH * 0.5f, 1.2f);

            string path = $"{PrefabsFolder}/DoorVisual.prefab";
            AssetDatabase.DeleteAsset(path);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return path;
        }

        // ── Ground plane (scene object, not a prefab) ────────────────────────

        private static void EnsureGroundPlane(Material groundMat)
        {
            var existing = GameObject.Find(GroundPlaneName);
            if (existing != null) Object.DestroyImmediate(existing);

            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = GroundPlaneName;
            plane.transform.position   = new Vector3(0f, -0.02f, 0f); // just below room floors
            plane.transform.localScale = new Vector3(10f, 1f, 10f);   // Plane primitive is 10 m, so this = 100 m
            plane.GetComponent<Renderer>().sharedMaterial = groundMat;
        }

        // ── SceneBuilder wiring ──────────────────────────────────────────────

        private static int WireSceneBuilder(string smallPath, string mediumPath,
                                            string largePath, string doorPath,
                                            Material wallMat, Material glassMat)
        {
#if UNITY_2022_2_OR_NEWER
            var sb = Object.FindFirstObjectByType<SceneBuilder>();
#else
            var sb = Object.FindObjectOfType<SceneBuilder>();
#endif
            if (sb == null)
            {
                Debug.LogWarning("[ScenePrefabBuilder] No SceneBuilder in scene; " +
                                 "prefabs created but not auto-wired. Drag them onto " +
                                 "the SceneBuilder slots manually.");
                return 0;
            }

            var smallPrefab  = AssetDatabase.LoadAssetAtPath<GameObject>(smallPath);
            var mediumPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(mediumPath);
            var largePrefab  = AssetDatabase.LoadAssetAtPath<GameObject>(largePath);
            var doorPrefab   = AssetDatabase.LoadAssetAtPath<GameObject>(doorPath);

            int wired = 0;
            if (smallPrefab  != null) { sb.roomPrefabSmall  = smallPrefab;  wired++; }
            if (mediumPrefab != null) { sb.roomPrefabMedium = mediumPrefab; wired++; }
            if (largePrefab  != null) { sb.roomPrefabLarge  = largePrefab;  wired++; }
            if (doorPrefab   != null) { sb.doorPrefab       = doorPrefab;   wired++; }
            if (wallMat      != null) { sb.wallMaterial     = wallMat;            }
            if (glassMat     != null) { sb.windowMaterial   = glassMat;          }

            EditorUtility.SetDirty(sb);
            return wired;
        }

        // ── Finalize a realistic (XRI) door from the current selection ───────

        // Validate: only enabled when a GameObject carrying a MikeNspired Door is
        // selected (a scene instance or a project prefab).
        [MenuItem(FinalizeDoorMenuPath, validate = true)]
        private static bool ValidateFinalizeRealDoor()
        {
            GameObject sel = Selection.activeGameObject;
            return sel != null && sel.GetComponentInChildren<Door>(true) != null;
        }

        /// <summary>
        /// Turns a hand-built XRI door into a generator-ready prefab: strips the
        /// scene-bound NPC-stop trigger, adds an <see cref="NpcDoorAssist"/> plus a
        /// carving NavMeshObstacle so generated NavMeshAgent NPCs can pass through,
        /// saves it to <see cref="RealDoorPrefabPath"/>, then wires it (and the real
        /// wall/floor materials) into the scene's SceneBuilder.
        /// </summary>
        [MenuItem(FinalizeDoorMenuPath, priority = 61)]
        public static void FinalizeRealDoor()
        {
            GameObject sel = Selection.activeGameObject;
            if (sel == null || sel.GetComponentInChildren<Door>(true) == null)
            {
                EditorUtility.DisplayDialog("Finalize Real Door",
                    "Select a door GameObject (one containing a MikeNspired 'Door' component) " +
                    "in the Hierarchy or Project first.", "OK");
                return;
            }

            // Work on a copy so the original scene object / prefab is untouched.
            GameObject copy = Object.Instantiate(sel);
            try
            {
                copy.name = "RealDoor";
                copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                copy.transform.localScale = sel.transform.lossyScale;

                int strangers = StripSceneBoundComponents(copy);
                NavMeshObstacle obstacle = AddDoorwayObstacle(copy);
                WireDoorAssist(copy, obstacle);

                EnsureFolder(PrefabsFolder);
                AssetDatabase.DeleteAsset(RealDoorPrefabPath); // idempotent rebuild
                PrefabUtility.SaveAsPrefabAsset(copy, RealDoorPrefabPath);

                int wired = WireRealDoorIntoScene();

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

                EditorUtility.DisplayDialog("Finalize Real Door - Success",
                    "Saved " + RealDoorPrefabPath + "\n\n" +
                    "  • Stripped " + strangers + " scene-bound component(s) (e.g. StopNpcOnEnter)\n" +
                    "  • Added NpcDoorAssist + carving NavMeshObstacle\n" +
                    "  • SceneBuilder slots wired: " + wired + "/4\n" +
                    (wired < 4
                        ? "(No SceneBuilder found in scene — drag RealDoor onto its Door Prefab slot manually.)"
                        : "Door, wall material and floor material are wired. Press Play, " +
                          "Generate Scenario, Start Mission."),
                    "OK");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ScenePrefabBuilder] Finalize Real Door failed: {ex.Message}\n{ex.StackTrace}");
                EditorUtility.DisplayDialog("Finalize Real Door - Failed",
                    "Error: " + ex.Message + "\n\nSee the Console for the full stack trace.", "OK");
            }
            finally
            {
                Object.DestroyImmediate(copy);
            }
        }

        // Removes components that reference specific scene NPC instances and would
        // dangle in a reusable prefab. Matched by type name so we don't take a hard
        // assembly dependency on the XRI runtime asmdef.
        private static int StripSceneBoundComponents(GameObject root)
        {
            int removed = 0;
            foreach (MonoBehaviour mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue; // missing script
                if (mb.GetType().Name == "StopNpcOnEnter")
                {
                    Object.DestroyImmediate(mb);
                    removed++;
                }
            }
            return removed;
        }

        // Adds a carving NavMeshObstacle on the door root, sized to the doorway, so
        // NPC pathing is severed while the door is shut. NpcDoorAssist toggles it.
        private static NavMeshObstacle AddDoorwayObstacle(GameObject root)
        {
            NavMeshObstacle obstacle = root.GetComponent<NavMeshObstacle>();
            if (obstacle == null) obstacle = root.AddComponent<NavMeshObstacle>();
            obstacle.shape   = NavMeshObstacleShape.Box;
            obstacle.carving = true;

            // Root is at identity/origin here, so renderer world bounds == root-local.
            if (TryGetRendererBounds(root, out Bounds b))
            {
                bool spanX = b.size.x >= b.size.z;
                obstacle.center = b.center;
                obstacle.size = spanX
                    ? new Vector3(b.size.x, b.size.y, 0.4f)
                    : new Vector3(0.4f, b.size.y, b.size.z);
            }
            else
            {
                obstacle.center = new Vector3(0f, 1.05f, 0f);
                obstacle.size   = new Vector3(2f, 2.1f, 0.4f);
            }
            return obstacle;
        }

        private static void WireDoorAssist(GameObject root, NavMeshObstacle obstacle)
        {
            NpcDoorAssist assist = root.GetComponent<NpcDoorAssist>();
            if (assist == null) assist = root.AddComponent<NpcDoorAssist>();

            Door door = root.GetComponentInChildren<Door>(true);
            HingeJoint leafHinge = FindLeafHinge(root, door);

            assist.door        = door;
            assist.hinge       = leafHinge;
            assist.leafBody    = leafHinge != null ? leafHinge.GetComponent<Rigidbody>() : null;
            assist.knobs       = root.GetComponentsInChildren<XRKnob>(true);
            assist.navObstacle = obstacle;

            // If the leaf hinge's authored limits describe the open swing, use
            // them; otherwise leave the sensible 90° default.
            if (leafHinge != null && leafHinge.useLimits)
            {
                float max = Mathf.Max(Mathf.Abs(leafHinge.limits.min),
                                      Mathf.Abs(leafHinge.limits.max));
                if (max > 1f) assist.openAngle =
                    leafHinge.limits.max != 0f ? leafHinge.limits.max : max;
            }

            // Stop the door's outer (non-leaf) gravity bodies from tumbling once
            // removed from their original wall context.
            StabilizeBodies(root, assist.leafBody);
        }

        // The real swinging leaf is the hinge the XRI Door script controls
        // (its private m_DoorJoint). A door can contain several hinges/rigidbodies
        // (e.g. an outer "Door Main" pivot), so we read the script's serialized
        // reference; failing that, fall back to the most-vertical hinge.
        private static HingeJoint FindLeafHinge(GameObject root, Door door)
        {
            if (door != null)
            {
                var so = new SerializedObject(door);
                SerializedProperty p = so.FindProperty("m_DoorJoint");
                if (p != null && p.objectReferenceValue is HingeJoint hj) return hj;
            }

            HingeJoint best = null;
            float bestVertical = -1f;
            foreach (HingeJoint h in root.GetComponentsInChildren<HingeJoint>(true))
            {
                float v = Mathf.Abs(h.axis.normalized.y);
                if (v > bestVertical) { bestVertical = v; best = h; }
            }
            return best;
        }

        // Freezes every gravity-driven body that isn't the swinging leaf (e.g. the
        // door's outer "Door Main" body, which has a free horizontal hinge and
        // would fall in a generated scene). Gravity-off helper bodies (knobs,
        // door puller) are left untouched so the trainee interaction still works.
        private static void StabilizeBodies(GameObject root, Rigidbody leaf)
        {
            foreach (Rigidbody rb in root.GetComponentsInChildren<Rigidbody>(true))
            {
                if (rb == leaf) continue;
                if (rb.useGravity) rb.isKinematic = true;
            }
        }

        private static bool TryGetRendererBounds(GameObject root, out Bounds bounds)
        {
            bounds = new Bounds();
            bool has = false;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
                if (!has) { bounds = r.bounds; has = true; }
                else      bounds.Encapsulate(r.bounds);
            }
            return has;
        }

        // Wires RealDoor + the real wall/floor materials into the scene SceneBuilder,
        // rebuilding the room floor prefabs so they use the realistic floor material.
        private static int WireRealDoorIntoScene()
        {
#if UNITY_2022_2_OR_NEWER
            var sb = Object.FindFirstObjectByType<SceneBuilder>();
#else
            var sb = Object.FindObjectOfType<SceneBuilder>();
#endif
            if (sb == null)
            {
                Debug.LogWarning("[ScenePrefabBuilder] No SceneBuilder in scene; RealDoor saved " +
                                 "but not auto-wired. Drag it onto the Door Prefab slot manually.");
                return 0;
            }

            var realDoor  = AssetDatabase.LoadAssetAtPath<GameObject>(RealDoorPrefabPath);
            var wallMat   = AssetDatabase.LoadAssetAtPath<Material>(RealWallMaterial);
            var floorMat  = AssetDatabase.LoadAssetAtPath<Material>(RealFloorMaterial);

            int wired = 0;
            if (realDoor != null) { sb.doorPrefab   = realDoor; wired++; }
            if (wallMat  != null) { sb.wallMaterial = wallMat;  wired++; }

            // Frosted window glass so the realistic path gets glazed windows too.
            Material glassMat = CreateGlassMaterial();
            if (glassMat != null) { sb.windowMaterial = glassMat; }

            // Rebuild the room floor prefabs with the realistic floor material and
            // re-wire them, so floors match the hand-built map too.
            if (floorMat != null)
            {
                Material wm = wallMat != null ? wallMat
                    : CreateOrUpdateMaterial("Wall_M", WallColor);
                string smallPath = BuildRoomPrefab("RoomSmall",  4f, floorMat, wm);
                string medPath   = BuildRoomPrefab("RoomMedium", 6f, floorMat, wm);
                string largePath = BuildRoomPrefab("RoomLarge",  8f, floorMat, wm);

                var smallPrefab  = AssetDatabase.LoadAssetAtPath<GameObject>(smallPath);
                var mediumPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(medPath);
                var largePrefab  = AssetDatabase.LoadAssetAtPath<GameObject>(largePath);
                if (smallPrefab  != null) { sb.roomPrefabSmall  = smallPrefab;  }
                if (mediumPrefab != null) { sb.roomPrefabMedium = mediumPrefab; }
                if (largePrefab  != null) { sb.roomPrefabLarge  = largePrefab;  wired++; }
            }

            EditorUtility.SetDirty(sb);
            return wired;
        }

        // ── Material + folder helpers ────────────────────────────────────────

        // URP/Lit if the project uses URP, otherwise built-in Standard.
        private static Material CreateOrUpdateMaterial(string name, Color color)
        {
            string path = $"{MaterialsFolder}/{name}.mat";

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Unlit/Color");

            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                existing.shader = shader;
                SetColor(existing, color);
                EditorUtility.SetDirty(existing);
                return existing;
            }

            var mat = new Material(shader);
            SetColor(mat, color);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // Frosted window pane material: an opaque, smooth, light-blue material so
        // windows read as glazed glass while still hiding the room behind them.
        private static Material CreateGlassMaterial()
        {
            Material mat = CreateOrUpdateMaterial("Glass_M", GlassColor);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.8f);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.8f);
            if (mat.HasProperty("_Metallic"))   mat.SetFloat("_Metallic",   0.0f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void SetColor(Material mat, Color color)
        {
            // URP/Lit uses _BaseColor; Standard uses _Color. Set both, the
            // unused property is harmless.
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color"))     mat.SetColor("_Color",     color);
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath)) return;
            string parent     = Path.GetDirectoryName(folderPath).Replace('\\', '/');
            string folderName = Path.GetFileName(folderPath);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
#endif
