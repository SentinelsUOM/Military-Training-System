// Module 4 | Sentinels | University of Moratuwa | 2026

/*
 * ═══════════════════════════════════════════════════════════════════
 *  UNITY HIERARCHY — build this manually or via a custom Editor tool
 * ═══════════════════════════════════════════════════════════════════
 *
 * AARCanvas  [Canvas — Render Mode: World Space]
 *   ├── Width: 1800  Height: 1200  (units = 1 unit = 1 mm, scale to 0.001 in Transform)
 *   │
 *   ├── Background  [Image  color #1A1A2E]
 *   │
 *   ├── Header  [Image  color #0F3460  height: 120]
 *   │   ├── TitleText    [Text  "AFTER ACTION REVIEW"  bold  white  size 28]
 *   │   ├── SessionIdText [Text  "Session: ..."         white  size 14]
 *   │   └── DateText      [Text  "2026-04-25 UTC"       white  size 14]
 *   │
 *   ├── TabBar  [HorizontalLayoutGroup  color #0F3460  height: 60]
 *   │   ├── Tab_Summary   [Button  Text "SUMMARY"]
 *   │   ├── Tab_Timeline  [Button  Text "TIMELINE"]
 *   │   ├── Tab_Incidents [Button  Text "INCIDENTS"]
 *   │   ├── Tab_Hostage   [Button  Text "HOSTAGE"]
 *   │   └── Tab_Replay    [Button  Text "REPLAY"]
 *   │
 *   └── PanelContainer  [RectTransform — fills remaining height]
 *       ├── SummaryPanel          [active by default]   → SummaryPanel.cs
 *       ├── EventTimelinePanel    [inactive]            → EventTimelinePanel.cs
 *       ├── IncidentListPanel     [inactive]            → IncidentListPanel.cs
 *       ├── HostagePerspectivePanel [inactive]          → HostagePerspectivePanel.cs
 *       └── ReplayControlPanel    [inactive]            → ReplayControlPanel.cs
 *
 *  Canvas settings:
 *    - Event Camera: assign the VR camera (or Camera.main)
 *    - Dynamic Pixels Per Unit: 1
 *    - Reference Pixels Per Unit: 100
 *    - Scale: (0.001, 0.001, 0.001) so 1800×1200 px = 1.8m × 1.2m in world space
 * ═══════════════════════════════════════════════════════════════════
 */

using System;
using System.Collections.Generic;
using TeamSentinels.Module4.Data;
using TeamSentinels.Module4.Logging;
using UnityEngine;
using UnityEngine.UI;

namespace TeamSentinels.Module4.UI
{
    /// <summary>
    /// Master controller for the After-Action Review dashboard.
    /// Subscribes to SessionLogger.OnSessionComplete, then distributes
    /// session data to each panel. Tab buttons call ShowPanel().
    /// </summary>
    public class AARDashboardController : MonoBehaviour
    {
        #region Data

        [Header("Panels")]
        [SerializeField] private SummaryPanel             summaryPanel;
        [SerializeField] private EventTimelinePanel       timelinePanel;
        [SerializeField] private IncidentListPanel        incidentPanel;
        [SerializeField] private HostagePerspectivePanel  hostagePanel;
        [SerializeField] private ReplayControlPanel       replayPanel;

        [Header("Chrome")]
        [SerializeField] private GameObject tabBar;
        [SerializeField] private Text       sessionIdText;
        [SerializeField] private Text       dateText;

        private SessionSummary _currentSummary;

        private readonly Dictionary<string, GameObject> _panels =
            new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            RegisterPanel("Summary",   summaryPanel  != null ? summaryPanel.gameObject  : null);
            RegisterPanel("Timeline",  timelinePanel != null ? timelinePanel.gameObject : null);
            RegisterPanel("Incidents", incidentPanel != null ? incidentPanel.gameObject : null);
            RegisterPanel("Hostage",   hostagePanel  != null ? hostagePanel.gameObject  : null);
            RegisterPanel("Replay",    replayPanel   != null ? replayPanel.gameObject   : null);
        }

        private void Start()
        {
            SessionLogger.OnSessionComplete += OnSessionComplete;
            ShowPanel("Summary");
        }

        private void OnDestroy()
        {
            SessionLogger.OnSessionComplete -= OnSessionComplete;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Activates the named panel and deactivates all others.
        /// Valid names: "Summary", "Timeline", "Incidents", "Hostage", "Replay".
        /// </summary>
        public void ShowPanel(string panelName)
        {
            foreach (var kv in _panels)
            {
                if (kv.Value != null)
                    kv.Value.SetActive(
                        string.Equals(kv.Key, panelName, StringComparison.OrdinalIgnoreCase));
            }
        }

        // Called by Tab_Summary button OnClick
        public void ShowSummary()   => ShowPanel("Summary");
        public void ShowTimeline()  => ShowPanel("Timeline");
        public void ShowIncidents() => ShowPanel("Incidents");
        public void ShowHostage()   => ShowPanel("Hostage");
        public void ShowReplay()    => ShowPanel("Replay");

        #endregion

        #region Private

        private void OnSessionComplete(SessionSummary summary)
        {
            _currentSummary = summary;

            if (sessionIdText != null)
                sessionIdText.text = $"Session: {summary.sessionId}";
            if (dateText != null)
                dateText.text = summary.timestamp.ToString("yyyy-MM-dd HH:mm") + " UTC";

            float duration = summary.GetMissionDuration();

            summaryPanel? .Populate(summary.performance, summary.cognitiveSummary);
            timelinePanel?.Populate(summary.events, duration);
            incidentPanel?.Populate(summary.incidents);
            hostagePanel? .Populate(summary.hostageHistory);
            replayPanel?  .Populate(summary.replayFrames, summary.events, duration);

            gameObject.SetActive(true);
            ShowPanel("Summary");
        }

        private void RegisterPanel(string key, GameObject go)
        {
            if (go != null)
            {
                _panels[key] = go;
                go.SetActive(false);
            }
        }

        #endregion
    }
}
