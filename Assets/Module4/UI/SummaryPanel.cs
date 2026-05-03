// Module 4 | Sentinels | University of Moratuwa | 2026

using TeamSentinels.Module4.Data;
using UnityEngine;
using UnityEngine.UI;

namespace TeamSentinels.Module4.UI
{
    /// <summary>
    /// Displays the PerformanceSummary and CognitiveSummary for a completed session.
    /// Attach to the SummaryPanel GameObject inside PanelContainer.
    /// Wire all SerializeField UI references in the Inspector.
    /// </summary>
    public class SummaryPanel : MonoBehaviour
    {
        #region Data

        [Header("Mission Outcome")]
        [SerializeField] private Text missionOutcomeText;
        [SerializeField] private Text overallScoreText;

        [Header("Score Bars")]
        [SerializeField] private Slider safetyBar;
        [SerializeField] private Text   safetyLabel;
        [SerializeField] private Slider accuracyBar;
        [SerializeField] private Text   accuracyLabel;
        [SerializeField] private Slider speedBar;
        [SerializeField] private Text   speedLabel;

        [Header("Shot Stats")]
        [SerializeField] private Text shotsText;
        [SerializeField] private Text hitsText;
        [SerializeField] private Text missesText;

        [Header("Hostage / Duration")]
        [SerializeField] private Text hostagesSavedText;
        [SerializeField] private Text missionDurationText;

        [Header("Cognitive")]
        [SerializeField] private Text cognitiveLoadText;
        [SerializeField] private Text stressLevelText;

        // Colours — dark theme accent / highlight
        private static readonly Color _successColor = HexColor("27AE60");
        private static readonly Color _failColor    = HexColor("E94560");
        private static readonly Color _accentColor  = HexColor("4A90E2");

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            // Panels start inactive; nothing to init.
        }

        #endregion

        #region Public API

        /// <summary>Fills all UI elements from the provided performance and cognitive data.</summary>
        public void Populate(PerformanceSummary perf, CognitiveSummary cog)
        {
            if (perf == null) return;

            // Mission outcome
            SetText(missionOutcomeText, perf.missionSuccess ? "MISSION SUCCESS" : "MISSION FAILED");
            if (missionOutcomeText != null)
                missionOutcomeText.color = perf.missionSuccess ? _successColor : _failColor;

            SetText(overallScoreText, $"{perf.overallScore * 100f:F0}%");

            // Score bars
            SetBar(safetyBar,   safetyLabel,   perf.safetyScore,   "Safety");
            SetBar(accuracyBar, accuracyLabel, perf.accuracyScore, "Accuracy");
            SetBar(speedBar,    speedLabel,    perf.speedScore,    "Speed");

            // Shot stats
            SetText(shotsText,  $"Shots: {perf.totalShots}");
            SetText(hitsText,   $"Hits: {perf.hits}");
            SetText(missesText, $"Misses: {perf.misses}");

            // Hostage / duration
            SetText(hostagesSavedText,   $"Hostages saved: {perf.hostagesSaved} / {perf.hostagesTotal}");
            SetText(missionDurationText, $"Duration: {FormatTime(perf.missionDuration)}");

            // Cognitive
            if (cog != null)
            {
                SetText(cognitiveLoadText, $"Cognitive Load: {cog.estimatedCognitiveLoad?.ToUpper() ?? "N/A"}");
                SetText(stressLevelText,   $"Stress Level: {cog.estimatedStressLevel?.ToUpper() ?? "N/A"}");
            }
            else
            {
                SetText(cognitiveLoadText, "Cognitive Load: N/A");
                SetText(stressLevelText,   "Stress Level: N/A");
            }
        }

        #endregion

        #region Private

        private static void SetText(Text t, string value)
        {
            if (t != null) t.text = value;
        }

        private static void SetBar(Slider bar, Text label, float value, string labelPrefix)
        {
            if (bar != null)
            {
                bar.minValue = 0f;
                bar.maxValue = 1f;
                bar.value    = value;
            }
            SetText(label, $"{labelPrefix}: {value * 100f:F0}%");
        }

        private static string FormatTime(float seconds)
        {
            int m = (int)(seconds / 60f);
            int s = (int)(seconds % 60f);
            return $"{m:D2}:{s:D2}";
        }

        private static Color HexColor(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out Color c);
            return c;
        }

        #endregion
    }
}
