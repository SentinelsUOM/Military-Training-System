// Module 4 Integration | Sentinels | University of Moratuwa | 2026
//
// Editor tooling. Lives in Assets/Module4Integration/Editor → compiles into
// Assembly-CSharp-Editor, which can see both the Assembly-CSharp types
// (Module4Bridge, Module4SessionController, PlayerHealth) and the auto-referenced
// TeamSentinels.Module4 asmdef types (SessionLogger, ReplayRecorder,
// DashboardUploader, WebReportExporter).

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TeamSentinels.Module4.Logging;
using TeamSentinels.Module4.Export;

namespace TeamSentinels.Module4.EditorTools
{
    /// <summary>
    /// One-click (re)creation of the Module4Manager GameObject and its logging /
    /// upload stack, so the AAR dashboard receives sessions again if the manager
    /// was deleted from the scene.
    ///
    /// Components added (idempotent — only adds what's missing):
    ///   SessionLogger, ReplayRecorder, Module4Bridge, Module4SessionController,
    ///   DashboardUploader, WebReportExporter.
    /// </summary>
    public static class Module4ManagerSetup
    {
        [MenuItem("Tools/Module 4/Create or Repair Manager", priority = 80)]
        public static void CreateOrRepair()
        {
            // Reuse an existing manager (anything already carrying a SessionLogger),
            // else a GameObject literally named "Module4Manager", else make a new one.
            var existing = Object.FindFirstObjectByType<SessionLogger>();
            GameObject go = existing != null ? existing.gameObject
                                             : GameObject.Find("Module4Manager");
            if (go == null)
            {
                go = new GameObject("Module4Manager");
                Undo.RegisterCreatedObjectUndo(go, "Create Module4Manager");
            }

            Ensure<SessionLogger>(go);
            var replay     = Ensure<ReplayRecorder>(go);
            Ensure<Module4Bridge>(go);
            var controller = Ensure<Module4SessionController>(go);
            Ensure<DashboardUploader>(go);
            Ensure<WebReportExporter>(go);
            Ensure<MissionResultUI>(go); // world-space end-of-mission result panel

            // Wire the controller's private [SerializeField] references so player-death
            // detection and replay recording work. (The rescue→upload path works even
            // without these, but wiring them enables the full feature set.)
            var so = new SerializedObject(controller);
            SetRef(so, "replayRecorder", replay);

            var ph = Object.FindFirstObjectByType<PlayerHealth>();
            if (ph != null) SetRef(so, "playerHealth", ph);

            if (Camera.main != null) SetRef(so, "playerTransform", Camera.main.transform);

            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(go);
            if (controller != null) EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(go.scene);
            Selection.activeGameObject = go;

            string phMsg = ph != null ? $"Player Health wired to '{ph.name}'."
                                      : "No PlayerHealth found — assign it manually for player-death detection.";
            Debug.Log("[Module4ManagerSetup] Module4Manager ready. " + phMsg +
                      " DashboardUploader posts to http://localhost:3000 by default.");
            EditorUtility.DisplayDialog("Module 4 Manager",
                "Module4Manager created/repaired with:\n" +
                "  - SessionLogger\n  - ReplayRecorder\n  - Module4Bridge\n" +
                "  - Module4SessionController\n  - DashboardUploader\n  - WebReportExporter\n  - MissionResultUI\n\n" +
                phMsg + "\n\nNow SAVE the scene (Ctrl+S).", "OK");
        }

        private static T Ensure<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            if (c == null) c = Undo.AddComponent<T>(go);
            return c;
        }

        private static void SetRef(SerializedObject so, string propertyName, Object value)
        {
            var p = so.FindProperty(propertyName);
            if (p != null) p.objectReferenceValue = value;
        }
    }
}
#endif
