// Module 4 | Sentinels | University of Moratuwa | 2026

using System.Collections.Generic;
using Newtonsoft.Json;

namespace TeamSentinels.Module4.Data
{
    /// <summary>
    /// A discrete event that occurred during a training session.
    /// Logged by Module 2 (NPC behaviour) and Module 3 (cognitive analysis).
    /// </summary>
    public class MissionEvent
    {
        #region Data

        [JsonProperty("eventType")]
        public string eventType;

        [JsonProperty("timestamp")]
        public float timestamp;

        [JsonProperty("sourceActorId")]
        public string sourceActorId;

        [JsonProperty("targetActorId")]
        public string targetActorId;

        [JsonProperty("roomId")]
        public string roomId;

        [JsonProperty("position")]
        public Vector3Serializable position;

        [JsonProperty("tags")]
        public List<string> tags = new List<string>();

        #endregion

        #region Public API

        /// <summary>Creates a MissionEvent with all core fields populated.</summary>
        public static MissionEvent Create(string eventType, float timestamp,
            string sourceActorId = null, string targetActorId = null,
            string roomId = null, Vector3Serializable position = default,
            List<string> tags = null)
        {
            return new MissionEvent
            {
                eventType     = eventType,
                timestamp     = timestamp,
                sourceActorId = sourceActorId,
                targetActorId = targetActorId,
                roomId        = roomId,
                position      = position,
                tags          = tags ?? new List<string>()
            };
        }

        #endregion
    }
}
