// Module 4 | Sentinels | University of Moratuwa | 2026

using System;
using System.Collections.Generic;
using TeamSentinels.Module4.Data;
using UnityEngine;
using UnityEngine.UI;

namespace TeamSentinels.Module4.UI
{
    /// <summary>
    /// Vertically scrollable list of extracted IncidentRecords.
    /// Each row shows a severity-coloured icon, incident type, description, and timestamp.
    /// Clicking a row fires OnIncidentSelected.
    /// </summary>
    public class IncidentListPanel : MonoBehaviour
    {
        #region Data

        [SerializeField] private ScrollRect    incidentScrollRect;
        [SerializeField] private RectTransform incidentContent;
        [SerializeField] private GameObject    incidentEntryPrefab;

        private readonly List<GameObject> _entries = new List<GameObject>();

        private static readonly Dictionary<string, string> _severityColors = new Dictionary<string, string>
        {
            { "low",    "4A90E2" },   // blue
            { "medium", "F5A623" },   // orange
            { "high",   "E94560" },   // red
        };

        #endregion

        #region Events

        /// <summary>Fired when the user clicks an incident row.</summary>
        public event Action<IncidentRecord> OnIncidentSelected;

        #endregion

        #region Unity Lifecycle

        private void OnDestroy()
        {
            ClearEntries();
        }

        #endregion

        #region Public API

        /// <summary>Clears the list and re-populates it from the provided incidents.</summary>
        public void Populate(List<IncidentRecord> incidents)
        {
            ClearEntries();
            if (incidents == null || incidents.Count == 0) return;
            if (incidentContent == null || incidentEntryPrefab == null) return;

            foreach (var incident in incidents)
                CreateEntry(incident);
        }

        #endregion

        #region Private

        private void CreateEntry(IncidentRecord incident)
        {
            GameObject entry = Instantiate(incidentEntryPrefab, incidentContent);
            _entries.Add(entry);

            // Severity icon (first Image child)
            var images = entry.GetComponentsInChildren<Image>();
            if (images.Length > 0)
            {
                images[0].color = GetSeverityColor(incident.severity);
            }

            // Text fields — find by name or fall back to indexed Text components
            var texts = entry.GetComponentsInChildren<Text>();

            // Expected layout order: [0] type, [1] description, [2] timestamp
            if (texts.Length > 0) texts[0].text = incident.incidentType ?? "";
            if (texts.Length > 1) texts[1].text = incident.description  ?? "";
            if (texts.Length > 2) texts[2].text = $"t={incident.timestamp:F1}s";

            // Click
            var btn = entry.GetComponent<Button>();
            if (btn == null) btn = entry.AddComponent<Button>();
            var captured = incident;
            btn.onClick.AddListener(() => OnIncidentSelected?.Invoke(captured));
        }

        private void ClearEntries()
        {
            foreach (var e in _entries)
            {
                if (e != null) Destroy(e);
            }
            _entries.Clear();
        }

        private static Color GetSeverityColor(string severity)
        {
            string key = (severity ?? "low").ToLower();
            if (_severityColors.TryGetValue(key, out string hex))
            {
                ColorUtility.TryParseHtmlString("#" + hex, out Color c);
                return c;
            }
            return Color.gray;
        }

        #endregion
    }
}
