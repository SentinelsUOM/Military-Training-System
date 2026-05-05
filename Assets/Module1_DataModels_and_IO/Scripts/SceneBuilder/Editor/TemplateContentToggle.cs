// =============================================================================
// TemplateContentToggle.cs
// Module 1 - Dynamic Scenario Generation (Editor tooling)
// Team Sentinels | University of Moratuwa | 2026
//
// Quick toggles to hide / show the XRI Starter Kit's demo content (buildings,
// minigames, weapons rack, demo NPCs, etc.) in the active scene so the
// generated mission can be viewed without overlap, then restored when you
// want to use the kit's interactions again.
//
// The deny list / always-keep list are name patterns matched against root
// GameObjects only - safe and predictable for the standard XRI scene.
// =============================================================================

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
// Alias to disambiguate from the sibling namespace TeamSentinels.ScenarioGeneration.Scene
// (where SceneBuilder lives) — bare `Scene` would otherwise resolve to that namespace.
using UnityScene = UnityEngine.SceneManagement.Scene;

namespace TeamSentinels.ScenarioGeneration.EditorTools
{
    /// <summary>
    /// Editor menu items that bulk-disable / re-enable the XRI Starter Kit's
    /// demo content in the active scene, so the generated training scenario
    /// is visible on its own.
    /// </summary>
    public static class TemplateContentToggle
    {
        // Substring match (case-insensitive) - any root GameObject whose name
        // contains one of these is treated as XRI demo content.
        private static readonly string[] DenyContains =
        {
            "INTERTABLES",
            "MINI GAMES",
            "ENVIRONMENT",
            "[LookAnchor]",
            "HandPoseReferenceTool",
            "CoverPoint",
        };

        // Exact match (case-insensitive) - useful for short generic names where
        // a substring match would be too broad.
        private static readonly string[] DenyExact =
        {
            "Cube",
            "-------- NPC",
        };

        // Override list - if a name matches one of these, it stays enabled even
        // if it would otherwise be hit by the deny list.
        private static readonly string[] AlwaysKeepContains =
        {
            "PLAYER",            // contains the XR rig
            "SceneBuilder",
            "GroundPlane",
            "EvaluatorCanvas",
            "ScenarioManager",
            "EventSystem",
            "Directional Light",
            "Main Camera",
        };

        // ── Menu items ───────────────────────────────────────────────────────

        [MenuItem("Tools/Scenario Generator/Hide Template Content", priority = 70)]
        public static void HideTemplateContent() => Apply(active: false);

        [MenuItem("Tools/Scenario Generator/Show Template Content", priority = 71)]
        public static void ShowTemplateContent() => Apply(active: true);

        // ── Implementation ───────────────────────────────────────────────────

        private static void Apply(bool active)
        {
            UnityScene scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                EditorUtility.DisplayDialog("Template Content",
                    "No valid active scene.", "OK");
                return;
            }

            var hits = new List<string>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root == null) continue;
                if (Matches(root.name, AlwaysKeepContains)) continue;
                if (!IsTemplate(root.name))                  continue;

                if (root.activeSelf != active)
                {
                    root.SetActive(active);
                    hits.Add(root.name);
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);

            string verb = active ? "Showed" : "Hid";
            string body = hits.Count == 0
                ? $"Nothing to {verb.ToLowerInvariant()} - all template " +
                  "GameObjects are already in that state."
                : $"{verb} {hits.Count} GameObject(s):\n  - " +
                  string.Join("\n  - ", hits);

            Debug.Log($"[TemplateContentToggle] {verb} {hits.Count} GameObjects in '{scene.name}'.");
            EditorUtility.DisplayDialog($"Template Content - {verb}", body, "OK");
        }

        private static bool IsTemplate(string name)
        {
            if (Matches(name, DenyContains)) return true;
            foreach (string exact in DenyExact)
                if (string.Equals(name, exact, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private static bool Matches(string name, string[] patterns)
        {
            if (string.IsNullOrEmpty(name)) return false;
            foreach (string p in patterns)
                if (name.IndexOf(p, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            return false;
        }
    }
}
#endif
