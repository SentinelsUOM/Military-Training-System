// Module 4 | Sentinels | University of Moratuwa | 2026

using System.Collections.Generic;
using System.Linq;
using TeamSentinels.Module4.Data;
using UnityEngine;
using UnityEngine.UI;

namespace TeamSentinels.Module4.UI
{
    /// <summary>
    /// Displays each hostage's emotional-state journey as a colour-coded horizontal bar
    /// and a distress gauge (Slider). Hostages are grouped by hostageId.
    /// State colours: Calm=#7ED321, Fearful=#F5A623, Panic=#E94560, Freeze=#4A90E2, Follow=#27AE60
    /// </summary>
    public class HostagePerspectivePanel : MonoBehaviour
    {
        #region Data

        [SerializeField] private RectTransform hostageListContent;
        [SerializeField] private GameObject    hostageEntryPrefab;

        // Width of the full state bar in pixels (set in Editor to match prefab bar width)
        [SerializeField] private float stateBarWidth = 600f;

        private readonly List<GameObject> _entries = new List<GameObject>();

        private static readonly Dictionary<string, string> _stateColors = new Dictionary<string, string>
        {
            { "Calm",    "7ED321" },
            { "Fearful", "F5A623" },
            { "Panic",   "E94560" },
            { "Freeze",  "4A90E2" },
            { "Follow",  "27AE60" },
        };

        #endregion

        #region Unity Lifecycle

        private void OnDestroy()
        {
            ClearEntries();
        }

        #endregion

        #region Public API

        /// <summary>Groups hostage history by hostageId and builds one card per hostage.</summary>
        public void Populate(List<HostageStateEntry> hostageHistory)
        {
            ClearEntries();
            if (hostageHistory == null || hostageHistory.Count == 0) return;
            if (hostageListContent == null || hostageEntryPrefab == null) return;

            var byHostage = hostageHistory
                .GroupBy(h => h.hostageId)
                .OrderBy(g => g.Key);

            foreach (var group in byHostage)
                CreateHostageCard(group.Key, group.OrderBy(h => h.timestamp).ToList());
        }

        #endregion

        #region Private

        private void CreateHostageCard(string hostageId, List<HostageStateEntry> entries)
        {
            GameObject card = Instantiate(hostageEntryPrefab, hostageListContent);
            _entries.Add(card);

            // ── Header label ─────────────────────────────────────────────────
            var texts = card.GetComponentsInChildren<Text>();
            if (texts.Length > 0)
                texts[0].text = hostageId.ToUpper();

            // ── State colour segments on the timeline bar ─────────────────────
            // The prefab must contain a child named "StateBar" (RectTransform).
            // We instantiate coloured Image children to represent each state segment.
            var stateBarTransform = FindChildByName(card.transform, "StateBar");
            if (stateBarTransform != null && entries.Count > 0)
                BuildStateBar(stateBarTransform as RectTransform, entries);

            // ── Distress gauge (Slider) ──────────────────────────────────────
            float finalDistress = entries[entries.Count - 1].distressScore;
            var gauge = card.GetComponentInChildren<Slider>();
            if (gauge != null)
            {
                gauge.minValue = 0f;
                gauge.maxValue = 1f;
                gauge.value    = finalDistress;
            }

            // ── Trigger event list ───────────────────────────────────────────
            // Second Text element shows key trigger events
            if (texts.Length > 1)
            {
                var triggers = entries
                    .Where(e => !string.IsNullOrEmpty(e.triggerEvent))
                    .Select(e => $"{e.state}: {e.triggerEvent}");
                texts[1].text = string.Join("  |  ", triggers);
            }
        }

        private void BuildStateBar(RectTransform barRoot, List<HostageStateEntry> entries)
        {
            if (barRoot == null || entries.Count == 0) return;

            float totalTime = entries[entries.Count - 1].timestamp;
            if (totalTime <= 0f) totalTime = 1f;

            for (int i = 0; i < entries.Count; i++)
            {
                float segStart = entries[i].timestamp;
                float segEnd   = (i + 1 < entries.Count) ? entries[i + 1].timestamp : totalTime;
                float fraction = (segEnd - segStart) / totalTime;
                float segWidth = Mathf.Max(fraction * stateBarWidth, 4f);

                GameObject seg = new GameObject($"Seg_{entries[i].state}", typeof(RectTransform), typeof(Image));
                seg.transform.SetParent(barRoot, false);

                var rt = seg.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot     = new Vector2(0f, 0.5f);
                rt.anchoredPosition = new Vector2(segStart / totalTime * stateBarWidth, 0f);
                rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, segWidth);
                rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, barRoot.rect.height);

                var img = seg.GetComponent<Image>();
                img.color = GetStateColor(entries[i].state);
            }
        }

        private void ClearEntries()
        {
            foreach (var e in _entries)
            {
                if (e != null) Destroy(e);
            }
            _entries.Clear();
        }

        private static Transform FindChildByName(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child;
                var found = FindChildByName(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private static Color GetStateColor(string state)
        {
            if (_stateColors.TryGetValue(state, out string hex))
            {
                ColorUtility.TryParseHtmlString("#" + hex, out Color c);
                return c;
            }
            return Color.gray;
        }

        #endregion
    }
}
