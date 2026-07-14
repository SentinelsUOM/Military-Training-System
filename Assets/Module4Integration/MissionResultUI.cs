// Module 4 Integration | Sentinels | University of Moratuwa | 2026
//
// Lives in Assembly-CSharp so it can reference SessionLogger / SessionSummary
// (TeamSentinels.Module4.*) and the Module 2 types directly.

using System;
using TeamSentinels.Module4.Data;
using TeamSentinels.Module4.Logging;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Spawns a world-space mission-result panel in front of the trainee when the
/// session ends. Shows RESCUE COMPLETE / MISSION FAILED, the reason, the score,
/// hostages saved, and a Restart button.
///
/// Why world-space (not a screen overlay): a screen-space canvas does not render
/// inside an HMD. A world-space canvas placed in front of the head is readable in
/// both the headset and the desktop XR simulator.
///
/// Interaction:
///   - Desktop simulator: click Restart with the mouse (GraphicRaycaster).
///   - VR: point + trigger (a TrackedDeviceGraphicRaycaster is attached by
///     reflection if XRI is present, so there's no hard assembly dependency).
///   - Universal fallback: press R to restart.
///
/// Setup: add to the Module4Manager (the Module 4 setup tool does this). No scene
/// wiring needed — everything is built procedurally at runtime.
/// </summary>
public class MissionResultUI : MonoBehaviour
{
    [Tooltip("Distance in metres the panel floats in front of the trainee's head.")]
    [SerializeField] private float distance = 2.0f;

    [Tooltip("Colour of a SUCCESS title.")]
    [SerializeField] private Color successColor = new Color(0.30f, 0.85f, 0.40f);

    [Tooltip("Colour of a FAIL title.")]
    [SerializeField] private Color failColor = new Color(0.90f, 0.25f, 0.25f);

    private GameObject _panel;
    private Font _font;

    private void OnEnable()  => SessionLogger.OnSessionComplete += HandleSessionComplete;
    private void OnDisable() => SessionLogger.OnSessionComplete -= HandleSessionComplete;

    private void Update()
    {
        // New Input System: Keyboard.current is null (no throw) if the legacy-only
        // backend is active, so this is safe regardless of project input settings.
        if (_panel != null && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            Restart();
    }

    private void HandleSessionComplete(SessionSummary summary)
    {
        if (_panel != null) return;          // only one panel
        if (summary == null) return;

        bool success = summary.performance != null && summary.performance.missionSuccess;
        string reason = ExtractReason(summary);
        BuildPanel(summary, success, reason);
    }

    /// <summary>Pull the end reason from the trailing "MissionEnded" event's first tag.</summary>
    private static string ExtractReason(SessionSummary summary)
    {
        if (summary.events == null) return "ended";
        for (int i = summary.events.Count - 1; i >= 0; i--)
        {
            var e = summary.events[i];
            if (e != null && e.eventType == "MissionEnded" && e.tags != null && e.tags.Count > 0)
                return e.tags[0];
        }
        return "ended";
    }

    private static string FriendlyReason(string reason)
    {
        switch (reason)
        {
            case "hostages_rescued": return "All hostages extracted safely.";
            case "player_down":      return "You were killed in action.";
            case "hostage_killed":   return "A hostage was killed — friendly fire is a mission failure.";
            case "hostage_executed": return "The captor executed the hostage — you were too slow or too aggressive.";
            case "timeout":          return "Time expired before extraction.";
            default:                 return "Session ended.";
        }
    }

    // ── UI construction ─────────────────────────────────────────────────────────

    private void BuildPanel(SessionSummary summary, bool success, string reason)
    {
        EnsureFont();
        EnsureEventSystem();

        var cam = Camera.main;
        Vector3 pos = cam != null ? cam.transform.position + cam.transform.forward * distance
                                  : new Vector3(0, 1.6f, 2f);
        Quaternion rot = cam != null
            ? Quaternion.LookRotation(pos - cam.transform.position, Vector3.up)
            : Quaternion.identity;

        _panel = new GameObject("MissionResultCanvas");
        _panel.transform.SetPositionAndRotation(pos, rot);

        var canvas = _panel.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = cam; // required for pointer raycasting against a world-space canvas
        var rt = canvas.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(1200, 820);
        _panel.transform.localScale = Vector3.one * 0.0016f; // ~1.9m x 1.3m

        _panel.AddComponent<CanvasScaler>();
        _panel.AddComponent<GraphicRaycaster>();
        TryAddTrackedDeviceRaycaster(_panel); // VR pointer support, optional

        // Dim background card
        var bg = AddImage(_panel.transform, new Color(0.05f, 0.06f, 0.09f, 0.96f));
        Stretch(bg.rectTransform);

        // Coloured accent bar at the top
        var accent = AddImage(_panel.transform, success ? successColor : failColor);
        var abar = accent.rectTransform;
        abar.anchorMin = new Vector2(0, 1); abar.anchorMax = new Vector2(1, 1);
        abar.pivot = new Vector2(0.5f, 1); abar.sizeDelta = new Vector2(0, 16);
        abar.anchoredPosition = Vector2.zero;

        // Title
        AddText(_panel.transform, success ? "RESCUE COMPLETE" : "MISSION FAILED",
                72, FontStyle.Bold, success ? successColor : failColor,
                TextAnchor.MiddleCenter, new Vector2(0.05f, 0.72f), new Vector2(0.95f, 0.92f));

        // Reason line
        AddText(_panel.transform, FriendlyReason(reason),
                34, FontStyle.Normal, new Color(0.85f, 0.87f, 0.92f),
                TextAnchor.MiddleCenter, new Vector2(0.05f, 0.58f), new Vector2(0.95f, 0.70f));

        // Stats block
        var perf = summary.performance;
        int saved = perf != null ? perf.hostagesSaved : 0;
        int total = perf != null ? Mathf.Max(perf.hostagesTotal, 1) : 1;
        float score = perf != null ? perf.overallScore : 0f;
        string stats =
            $"Hostages Saved   {saved} / {total}\n" +
            $"Overall Score    {score:P0}\n" +
            $"Accuracy         {(perf != null ? perf.accuracyScore : 0f):P0}\n" +
            $"Safety           {(perf != null ? perf.safetyScore : 0f):P0}\n" +
            $"Duration         {FormatTime(perf != null ? perf.missionDuration : 0f)}";
        AddText(_panel.transform, stats,
                30, FontStyle.Normal, new Color(0.78f, 0.80f, 0.86f),
                TextAnchor.UpperCenter, new Vector2(0.18f, 0.26f), new Vector2(0.82f, 0.56f));

        // Restart button
        BuildRestartButton(_panel.transform);

        // Hint
        AddText(_panel.transform, "Full after-action review available on the dashboard.",
                22, FontStyle.Italic, new Color(0.55f, 0.58f, 0.66f),
                TextAnchor.MiddleCenter, new Vector2(0.05f, 0.02f), new Vector2(0.95f, 0.09f));

        // The whole panel must live on the UI layer. On a player_down ending, PlayerDamageFeedback
        // blacks the world out by culling every layer EXCEPT UI — anything left on the Default
        // layer (which is what `new GameObject()` gives you) would be culled away with it, and the
        // trainee would fade to black and then just sit there staring at nothing.
        SetLayerRecursively(_panel, LayerMask.NameToLayer("UI"));

        Debug.Log($"[MissionResultUI] Shown — {(success ? "SUCCESS" : "FAIL")} ({reason}).");
    }

    private void BuildRestartButton(Transform parent)
    {
        var go = new GameObject("RestartButton", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var brt = go.GetComponent<RectTransform>();
        brt.anchorMin = new Vector2(0.34f, 0.11f);
        brt.anchorMax = new Vector2(0.66f, 0.21f);
        brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;

        var img = go.AddComponent<Image>();
        img.color = new Color(0.16f, 0.42f, 0.85f);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.26f, 0.55f, 0.95f);
        colors.pressedColor = new Color(0.10f, 0.30f, 0.65f);
        btn.colors = colors;
        btn.onClick.AddListener(Restart);

        AddText(go.transform, "RESTART  (R)",
                30, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one);
    }

    private void Restart()
    {
        Debug.Log("[MissionResultUI] Restart requested — reloading scene.");
        if (_panel != null) Destroy(_panel);
        _panel = null;
        var scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.buildIndex >= 0 ? scene.buildIndex : 0);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static string FormatTime(float seconds)
    {
        int s = Mathf.Max(0, Mathf.RoundToInt(seconds));
        return $"{s / 60}:{s % 60:00}";
    }

    private void EnsureFont()
    {
        if (_font != null) return;
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    private void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return; // XRI rigs usually have one
        var es = new GameObject("EventSystem", typeof(EventSystem));
        // Prefer the new Input System UI module if present; fall back to legacy.
        var ism = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (ism != null) es.AddComponent(ism);
        else es.AddComponent<StandaloneInputModule>();
    }

    private static void TryAddTrackedDeviceRaycaster(GameObject go)
    {
        // Added by reflection so this script doesn't hard-depend on the XRI assembly.
        var t = Type.GetType("UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster, Unity.XR.Interaction.Toolkit");
        if (t != null) go.AddComponent(t);
    }

    private Image AddImage(Transform parent, Color color)
    {
        var go = new GameObject("Image", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    private Text AddText(Transform parent, string content, int size, FontStyle style,
                         Color color, TextAnchor anchor, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        var txt = go.AddComponent<Text>();
        txt.text = content;
        txt.font = _font;
        txt.fontSize = size;
        txt.fontStyle = style;
        txt.color = color;
        txt.alignment = anchor;
        txt.horizontalOverflow = HorizontalWrapMode.Wrap;
        txt.verticalOverflow = VerticalWrapMode.Overflow;
        txt.supportRichText = true;
        return txt;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private static void SetLayerRecursively(GameObject go, int layer)
    {
        if (go == null || layer < 0) return;
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursively(child.gameObject, layer);
    }
}
