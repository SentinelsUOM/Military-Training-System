// Module 4 | Sentinels | University of Moratuwa | 2026

using System.Collections.Generic;
using Newtonsoft.Json;

namespace TeamSentinels.Module4.Data
{
    /// <summary>
    /// An auto-detected notable event extracted from raw session logs by IncidentExtractor.
    /// severity: "low" | "medium" | "high"
    /// </summary>
    public class IncidentRecord
    {
        #region Data

        [JsonProperty("incidentId")]
        public string incidentId;

        [JsonProperty("incidentType")]
        public string incidentType;

        [JsonProperty("timestamp")]
        public float timestamp;

        [JsonProperty("description")]
        public string description;

        [JsonProperty("involvedActors")]
        public List<string> involvedActors = new List<string>();

        [JsonProperty("replayTimestamp")]
        public float replayTimestamp;

        [JsonProperty("severity")]
        public string severity;

        #endregion

        #region Public API

        /// <summary>Creates an IncidentRecord with a generated GUID-based id.</summary>
        public static IncidentRecord Create(string incidentType, float timestamp,
            string description, List<string> involvedActors, string severity,
            float replayTimestamp = -1f)
        {
            return new IncidentRecord
            {
                incidentId      = System.Guid.NewGuid().ToString(),
                incidentType    = incidentType,
                timestamp       = timestamp,
                description     = description,
                involvedActors  = involvedActors ?? new List<string>(),
                severity        = severity,
                replayTimestamp = replayTimestamp < 0f ? timestamp : replayTimestamp
            };
        }

        #endregion
    }
}
