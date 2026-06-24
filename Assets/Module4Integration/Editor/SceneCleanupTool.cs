// Sentinels | University of Moratuwa | 2026
//
// Editor tooling (Assembly-CSharp-Editor). Safely removes the hand-placed test
// NPCs and runtime-leaked helper objects that pollute generated missions / AAR
// data, using Unity's own object deletion (so all references are cleaned up).

#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TeamSentinels.EditorTools
{
    public static class SceneCleanupTool
    {
        [MenuItem("Tools/Cleanup/Remove Hand-Placed Test NPCs", priority = 90)]
        public static void RemoveTestNpcs()
        {
            var toDelete = new List<GameObject>();

            // Any TerroristController / HostageController sitting in the OPEN scene at
            // EDIT time is a hand-placed leftover — the real ones are spawned from
            // prefabs at runtime, so they never exist in the saved scene.
            foreach (var t in Object.FindObjectsByType<TerroristController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t != null && !toDelete.Contains(t.gameObject)) toDelete.Add(t.gameObject);
            foreach (var h in Object.FindObjectsByType<HostageController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (h != null && !toDelete.Contains(h.gameObject)) toDelete.Add(h.gameObject);

            // Runtime-created helpers that got saved into the scene (leaked from a
            // play session). They're re-created on Play, so removing the saved copies
            // is safe and keeps the hierarchy clean.
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (go == null) continue;
                string n = go.name;
                if (n.StartsWith("[LookAnchor]") || n.StartsWith("Patrol_") ||
                    n.StartsWith("FaceTarget_") || n.StartsWith("SafeZone_Extraction"))
                    if (!toDelete.Contains(go)) toDelete.Add(go);
            }

            if (toDelete.Count == 0)
            {
                EditorUtility.DisplayDialog("Cleanup",
                    "No hand-placed NPCs or leftover helper objects found — the scene is already clean.", "OK");
                return;
            }

            var sb = new StringBuilder();
            foreach (var go in toDelete) sb.AppendLine("  - " + go.name);

            bool ok = EditorUtility.DisplayDialog("Remove Test NPCs",
                $"Delete these {toDelete.Count} object(s) from the scene?\n\n{sb}\n" +
                "The generated NPC PREFABS are not touched — these are only the hand-placed " +
                "scene copies and runtime leftovers.", "Delete", "Cancel");
            if (!ok) return;

            int removed = 0;
            foreach (var go in toDelete)
            {
                if (go == null) continue; // may have been removed as a child already
                Undo.DestroyObjectImmediate(go);
                removed++;
            }

            EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            Debug.Log($"[SceneCleanupTool] Removed {removed} hand-placed/leftover object(s). Save the scene (Ctrl+S).");
            EditorUtility.DisplayDialog("Cleanup",
                $"Removed {removed} object(s).\n\nNow SAVE the scene (Ctrl+S).", "OK");
        }
    }
}
#endif
