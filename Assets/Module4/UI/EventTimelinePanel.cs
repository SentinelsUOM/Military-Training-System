// Module 4 | Sentinels | University of Moratuwa | 2026

using System;
using System.Collections.Generic;
using TeamSentinels.Module4.Data;
using UnityEngine;
using UnityEngine.UI;

namespace TeamSentinels.Module4.UI
{
    /// <summary>
    /// Scrollable horizontal event timeline. Each event is represented as a coloured
    /// dot placed at x = timestamp × timelineWidthPerSecond on the content rect.
    /// Dots are clickable; clicking fires OnEventSelected.
    /// </summary>
    public class EventTimelinePanel : MonoBehaviour
    {
        #region Data

        [SerializeField] private ScrollRect    timelineScrollRect;
        [SerializeField] private RectTransform timelineContent;
        [SerializeField] private GameObject    eventDotPrefab;
        [SerializeField] private float         timelineWidthPerSecond = 5f;

        private readonly List<GameObject> _dots = new List<GameObject>();

        // Event colour map
        private static readonly Dictionary<string, string> _categoryColors = new Dictionary<string, string>
        {
            { "ShotFired",             "E94560" },
            { "TerroristHit",          "E94560" },
            { "TerroristDown",         "E94560" },
            { "GunshotHeard",          "E94560" },
            { "DoorOpened",            "F5A623" },
            { "RoomBreached",          "F5A623" },
            { "HostageContactStarted", "7ED321" },
            { "HostageFreed",          "7ED321" },
            { "PlayerSeen",            "4A90E2" },
            { "AllyDownSeen",          "9B59B6" },
        };

        #endregion

        #region Events

        /// <summary>Fired when the user clicks an event dot.</summary>
        public event Action<MissionEvent> OnEventSelected;

        #endregion

        #region Unity Lifecycle

        private void OnDestroy()
        {
            ClearDots();
        }

        #endregion

        #region Public API

        /// <summary>Clears the timeline and re-populates it from the given event list.</summary>
        public void Populate(List<MissionEvent> events, float totalDuration)
        {
            ClearDots();
            if (events == null || events.Count == 0) return;
            if (timelineContent == null || eventDotPrefab == null) return;

            // Resize content rect so ScrollRect works correctly
            float contentWidth = Mathf.Max(totalDuration * timelineWidthPerSecond, 300f);
            timelineContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, contentWidth);

            foreach (var e in events)
                CreateDot(e, totalDuration);
        }

        #endregion

        #region Private

        private void CreateDot(MissionEvent e, float totalDuration)
        {
            if (eventDotPrefab == null || timelineContent == null) return;

            GameObject dot = Instantiate(eventDotPrefab, timelineContent);
            _dots.Add(dot);

            // Position along the horizontal axis
            var rt = dot.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = new Vector2(e.timestamp * timelineWidthPerSecond, 0f);
            }

            // Colour
            Color dotColor = GetEventColor(e.eventType);
            var img = dot.GetComponentInChildren<Image>();
            if (img != null) img.color = dotColor;

            // Label
            var label = dot.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.text  = e.eventType;
                label.color = Color.white;
            }

            // Click handler — capture e for the closure
            var btn = dot.GetComponent<Button>();
            if (btn == null) btn = dot.AddComponent<Button>();
            var captured = e;
            btn.onClick.AddListener(() => OnEventSelected?.Invoke(captured));
        }

        private void ClearDots()
        {
            foreach (var d in _dots)
            {
                if (d != null) Destroy(d);
            }
            _dots.Clear();
        }

        private static Color GetEventColor(string eventType)
        {
            if (_categoryColors.TryGetValue(eventType, out string hex))
            {
                ColorUtility.TryParseHtmlString("#" + hex, out Color c);
                return c;
            }
            // Default: blue for unknown
            ColorUtility.TryParseHtmlString("#4A90E2", out Color def);
            return def;
        }

        #endregion
    }
}
