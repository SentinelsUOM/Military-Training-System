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

                int wired = WireSceneBuilder(smallPath, medPath, largePath, doorPath);

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

        // Builds a square room of the given nominal width (matches Module 1's
        // room.size). Floor and walls are extended outward by CorridorHalf (1 m)
        // so adjacent rooms meet exactly in the middle of Module 1's 2 m corridor
        // gap and doors land flush with wall openings instead of floating in
        // empty corridor space. Saves to disk as a prefab and returns the path.
        private static string BuildRoomPrefab(string name, float nominalSize, Material floorMat, Material wallMat)
        {
            const float CorridorHalf = 1f;                     // half of the 2 m corridor gap Module 1 leaves between rooms
            float outerSize          = nominalSize + 2f * CorridorHalf;
            float halfSize           = outerSize * 0.5f;

            var root = new GameObject(name);

            // ── Floor (extended into half the corridor gap on every side) ───
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(root.transform, false);
            floor.transform.localPosition = new Vector3(0f, FloorThickness * 0.5f, 0f);
            floor.transform.localScale    = new Vector3(outerSize, FloorThickness, outerSize);
            floor.GetComponent<Renderer>().sharedMaterial = floorMat;

            // ── Walls: 8 segments (2 per side, around the central 2 m gap) ──
            // Walls live on the outer (extended) edge so adjacent rooms' walls
            // meet at the same plane, with the door slotting into the shared
            // 2 m opening.
            float seg           = (outerSize - DoorGap) * 0.5f; // length of each segment
            float segCenterDist = DoorGap * 0.5f + seg * 0.5f;  // distance from room centre to segment centre

            // South wall (z = -halfSize), runs along X
            AddWall(root, "South_Left",
                new Vector3(-segCenterDist, WallHeight * 0.5f, -halfSize),
                new Vector3(seg, WallHeight, WallThickness), wallMat);
            AddWall(root, "South_Right",
                new Vector3( segCenterDist, WallHeight * 0.5f, -halfSize),
                new Vector3(seg, WallHeight, WallThickness), wallMat);

            // North wall (z = +halfSize)
            AddWall(root, "North_Left",
                new Vector3(-segCenterDist, WallHeight * 0.5f,  halfSize),
                new Vector3(seg, WallHeight, WallThickness), wallMat);
            AddWall(root, "North_Right",
                new Vector3( segCenterDist, WallHeight * 0.5f,  halfSize),
                new Vector3(seg, WallHeight, WallThickness), wallMat);

            // East wall (x = +halfSize), runs along Z
            AddWall(root, "East_Front",
                new Vector3( halfSize, WallHeight * 0.5f, -segCenterDist),
                new Vector3(WallThickness, WallHeight, seg), wallMat);
            AddWall(root, "East_Back",
                new Vector3( halfSize, WallHeight * 0.5f,  segCenterDist),
                new Vector3(WallThickness, WallHeight, seg), wallMat);

            // West wall (x = -halfSize)
            AddWall(root, "West_Front",
                new Vector3(-halfSize, WallHeight * 0.5f, -segCenterDist),
                new Vector3(WallThickness, WallHeight, seg), wallMat);
            AddWall(root, "West_Back",
                new Vector3(-halfSize, WallHeight * 0.5f,  segCenterDist),
                new Vector3(WallThickness, WallHeight, seg), wallMat);

            string path = $"{PrefabsFolder}/{name}.prefab";
            AssetDatabase.DeleteAsset(path); // idempotent rebuild
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return path;
        }

        private static void AddWall(GameObject parent, string name,
                                    Vector3 localPos, Vector3 localScale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale    = localScale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        // ── Door prefab construction ─────────────────────────────────────────

        // Doorframe (two jambs + lintel) + a wide door panel that fills the
        // 2 m wall opening Module 1 carves on every wall. Default orientation:
        // length along X axis, so SceneBuilder's identity rotation maps to N/S
        // walls and 90deg-Y maps to E/W walls.
        private static string BuildDoorPrefab(Material doorMat, Material frameMat)
        {
            var root = new GameObject("DoorVisual");

            // Match the wall opening exactly so there are no visible gaps
            // either side of the door when placed at the corridor centre.
            float frameSpan      = DoorGap;             // 2 m - matches wall opening
            float halfFrame      = frameSpan * 0.5f;
            const float panelH   = 2.0f;
            const float jambH    = 2.2f;
            const float jambD    = 0.18f;
            const float jambW    = 0.12f;

            // Jambs sit at x = ±1 m so they're flush with the edges of the
            // wall opening (the wall has a 2 m gap centred on its midline).
            AddWall(root, "JambLeft",
                new Vector3(-halfFrame + jambW * 0.5f, jambH * 0.5f, 0f),
                new Vector3(jambW, jambH, jambD), frameMat);
            AddWall(root, "JambRight",
                new Vector3( halfFrame - jambW * 0.5f, jambH * 0.5f, 0f),
                new Vector3(jambW, jambH, jambD), frameMat);
            AddWall(root, "Lintel",
                new Vector3(0f, jambH + 0.05f, 0f),
                new Vector3(frameSpan, 0.15f, jambD), frameMat);

            // Door panel: spans the full opening between the jambs (just inside
            // them so the jambs are still visible). Trigger collider so the
            // trainee can walk through, but the panel renders as a solid door.
            float panelW = frameSpan - jambW * 2f - 0.02f;
            var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = "Panel";
            panel.transform.SetParent(root.transform, false);
            panel.transform.localPosition = new Vector3(0f, panelH * 0.5f, 0f);
            panel.transform.localScale    = new Vector3(panelW, panelH, 0.05f);
            panel.GetComponent<Renderer>().sharedMaterial = doorMat;
            var panelCollider = panel.GetComponent<BoxCollider>();
            if (panelCollider != null) panelCollider.isTrigger = true;

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
                                            string largePath, string doorPath)
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
