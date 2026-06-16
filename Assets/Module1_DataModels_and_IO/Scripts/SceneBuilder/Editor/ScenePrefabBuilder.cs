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
        private const string PrefabsFolder   = "Assets/Prefabs";
        private const string MaterialsFolder = "Assets/Prefabs/Materials";
        private const string GroundPlaneName = "GroundPlane";

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

                string smallPath = BuildRoomPrefab("RoomSmall",  4f, floorMat, wallMat);
                string medPath   = BuildRoomPrefab("RoomMedium", 6f, floorMat, wallMat);
                string largePath = BuildRoomPrefab("RoomLarge",  8f, floorMat, wallMat);
                string doorPath  = BuildDoorPrefab(doorMat, frameMat);

                EnsureGroundPlane(groundMat);

                int wired = WireSceneBuilder(smallPath, medPath, largePath, doorPath, wallMat);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

                EditorUtility.DisplayDialog(
                    "Build Scene Prefabs - Success",
                    "Created prefabs in " + PrefabsFolder + "/:\n" +
                    "    RoomSmall, RoomMedium, RoomLarge, DoorVisual\n\n" +
                    "Materials in " + MaterialsFolder + "/:\n" +
                    "    Floor_M, Wall_M, Door_M, DoorFrame_M, Ground_M\n\n" +
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
                                            Material wallMat)
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
