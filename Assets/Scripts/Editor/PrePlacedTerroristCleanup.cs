using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TeamSentinels.EditorTools
{
    /// <summary>
    /// One-shot cleanup for the hand-placed terrorists that live directly in the
    /// scene (e.g. TerroristRoamer, TerroristLeader, walkingNPC). These are NOT the
    /// terrorists SceneBuilder spawns from TerroristNPC.prefab at Start Mission —
    /// they keep stale senses (20 m hearing/vision) and clutter the NPC registry.
    ///
    /// Because SceneBuilder's generated terrorists only exist as runtime clones,
    /// ANY TerroristController present in the open scene while stopped (edit mode)
    /// is pre-placed and safe to remove.
    ///
    /// Run from:  Tools ▸ Sentinels ▸ Delete Pre-placed Terrorists   (in EDIT mode)
    /// The deletion is registered with Undo, so Ctrl+Z restores it. Save the scene
    /// (Ctrl+S) afterwards to persist.
    /// </summary>
    public static class PrePlacedTerroristCleanup
    {
        private const string MenuPath = "Tools/Sentinels/Delete Pre-placed Terrorists";

        [MenuItem(MenuPath)]
        public static void DeletePrePlacedTerrorists()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog(
                    "Sentinels — Cleanup",
                    "Stop Play mode first. This only runs in edit mode so it removes " +
                    "the hand-placed terrorists, not the runtime-spawned ones.",
                    "OK");
                return;
            }

            // TerroristController has no namespace (it lives in Assembly-CSharp), so
            // it is referenced by its global name here.
            var terrorists = Object.FindObjectsByType<TerroristController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            if (terrorists == null || terrorists.Length == 0)
            {
                EditorUtility.DisplayDialog(
                    "Sentinels — Cleanup",
                    "No pre-placed terrorists found in the open scene. Nothing to delete.\n\n" +
                    "(The Module 1 terrorists spawn at Start Mission and are not saved in the scene.)",
                    "OK");
                return;
            }

            var names = new List<string>(terrorists.Length);
            foreach (var tc in terrorists)
                if (tc != null) names.Add(tc.gameObject.name);

            bool confirmed = EditorUtility.DisplayDialog(
                "Delete pre-placed terrorists?",
                $"Found {terrorists.Length} terrorist(s) saved in this scene:\n\n" +
                "• " + string.Join("\n• ", names) + "\n\n" +
                "These are hand-placed (not Module 1-generated) and keep the old " +
                "20 m senses. Delete them so only the spawned terrorist_01/02/03 run?",
                "Delete", "Cancel");
            if (!confirmed) return;

            Scene scene = default;
            int removed = 0;
            foreach (var tc in terrorists)
            {
                if (tc == null) continue;
                scene = tc.gameObject.scene;
                Debug.Log($"[PrePlacedTerroristCleanup] Deleting pre-placed terrorist '{tc.gameObject.name}'.");
                Undo.DestroyObjectImmediate(tc.gameObject);
                removed++;
            }

            if (scene.IsValid())
                EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log($"[PrePlacedTerroristCleanup] Removed {removed} pre-placed terrorist(s). " +
                      "Press Ctrl+S to save the scene. (Ctrl+Z to undo.)");
        }
    }
}
