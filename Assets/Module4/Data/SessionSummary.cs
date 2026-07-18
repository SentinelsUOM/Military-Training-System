// Module 4 | Sentinels | University of Moratuwa | 2026

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace TeamSentinels.Module4.Data
{
    /// <summary>
    /// Root object written to session_{sessionId}.json at the end of a training session.
    /// Contains all logged data, computed metrics, extracted incidents, and replay frames.
    /// </summary>
    public class SessionSummary
    {
        #region Data

        [JsonProperty("sessionId")]
        public string sessionId;

        [JsonProperty("scenarioId")]
        public string scenarioId;

        [JsonProperty("timestamp")]
        public DateTime timestamp;

        [JsonProperty("performance")]
        public PerformanceSummary performance;

        [JsonProperty("cognitiveSummary")]
        public CognitiveSummary cognitiveSummary;

        [JsonProperty("events")]
        public List<MissionEvent> events = new List<MissionEvent>();

        [JsonProperty("npcStateChanges")]
        public List<NPCStateChange> npcStateChanges = new List<NPCStateChange>();

        [JsonProperty("hostageHistory")]
        public List<HostageStateEntry> hostageHistory = new List<HostageStateEntry>();

        [JsonProperty("incidents")]
        public List<IncidentRecord> incidents = new List<IncidentRecord>();

        [JsonProperty("replayFrames")]
        public List<ReplayFrame> replayFrames = new List<ReplayFrame>();

        /// <summary>Top-down room/door geometry of the played scenario, for the
        /// dashboard map + 3D replay. Null for sessions logged before this existed.</summary>
        [JsonProperty("layout")]
        public LayoutSnapshot layout;

        #endregion

        #region Public API

        /// <summary>Returns the total mission duration in seconds from the event list.</summary>
        public float GetMissionDuration()
        {
            if (events == null || events.Count == 0) return 0f;
            float max = 0f;
            foreach (var e in events)
                if (e.timestamp > max) max = e.timestamp;
            return max;
        }

        #endregion
    }
}
