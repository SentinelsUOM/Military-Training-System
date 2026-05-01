// Module 4 | Sentinels | University of Moratuwa | 2026

using Newtonsoft.Json;

namespace TeamSentinels.Module4.Data
{
    /// <summary>
    /// Records one emotional-state transition for a hostage NPC.
    /// Distress score is automatically derived from state name.
    /// States: Calm → Fearful → Panic → Freeze → Follow
    /// </summary>
    public class HostageStateEntry
    {
        #region Data

        [JsonProperty("hostageId")]
        public string hostageId;

        [JsonProperty("timestamp")]
        public float timestamp;

        [JsonProperty("state")]
        public string state;

        [JsonProperty("triggerEvent")]
        public string triggerEvent;

        [JsonProperty("distressScore")]
        public float distressScore;

        #endregion

        #region Public API

        /// <summary>Creates an entry, computing distressScore from the state name.</summary>
        public static HostageStateEntry Create(string hostageId, float timestamp,
            string state, string triggerEvent)
        {
            return new HostageStateEntry
            {
                hostageId    = hostageId,
                timestamp    = timestamp,
                state        = state,
                triggerEvent = triggerEvent,
                distressScore = ComputeDistress(state)
            };
        }

        /// <summary>Maps a hostage state string to its distress score (0–1 range).</summary>
        public static float ComputeDistress(string state)
        {
            switch (state)
            {
                case "Calm":    return 0.00f;
                case "Fearful": return 0.33f;
                case "Panic":   return 0.66f;
                case "Freeze":  return 0.50f;
                case "Follow":  return 0.10f;
                default:        return 0.00f;
            }
        }

        #endregion
    }
}
