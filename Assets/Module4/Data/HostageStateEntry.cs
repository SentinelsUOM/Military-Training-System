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

        /// <summary>
        /// This hostage's personality profile — "Weak" | "Normal" | "Brave" — or null on
        /// sessions recorded before profiles existed. Constant for a hostage across the whole
        /// mission, so it is repeated on every entry purely for convenience: the dashboard can
        /// read it off any single entry without a separate lookup, and it survives filtering.
        ///
        /// Stored as a plain string rather than the HostageProfile enum on purpose — that enum
        /// lives in Assembly-CSharp, and the TeamSentinels.Module4 assembly has zero references
        /// by design. See HostageProfile.cs / HOSTAGE_PROFILE_RESEARCH.md.
        /// </summary>
        [JsonProperty("hostageProfile")]
        public string hostageProfile;

        #endregion

        #region Public API

        /// <summary>Creates an entry, computing distressScore from the state name.</summary>
        public static HostageStateEntry Create(string hostageId, float timestamp,
            string state, string triggerEvent, string hostageProfile = null)
        {
            return new HostageStateEntry
            {
                hostageId    = hostageId,
                timestamp    = timestamp,
                state        = state,
                triggerEvent = triggerEvent,
                distressScore = ComputeDistress(state),
                hostageProfile = hostageProfile
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
