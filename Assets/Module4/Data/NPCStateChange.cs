// Module 4 | Sentinels | University of Moratuwa | 2026

using Newtonsoft.Json;

namespace TeamSentinels.Module4.Data
{
    /// <summary>
    /// Records a single NPC finite-state-machine transition during a training session.
    /// Produced by Module 2 (TerroristController / HostageController).
    /// </summary>
    public class NPCStateChange
    {
        #region Data

        [JsonProperty("actorId")]
        public string actorId;

        [JsonProperty("actorType")]
        public string actorType;

        [JsonProperty("previousState")]
        public string previousState;

        [JsonProperty("newState")]
        public string newState;

        [JsonProperty("triggerEvent")]
        public string triggerEvent;

        [JsonProperty("timestamp")]
        public float timestamp;

        #endregion

        #region Public API

        /// <summary>Creates an NPCStateChange record.</summary>
        public static NPCStateChange Create(string actorId, string actorType,
            string previousState, string newState, string triggerEvent, float timestamp)
        {
            return new NPCStateChange
            {
                actorId       = actorId,
                actorType     = actorType,
                previousState = previousState,
                newState      = newState,
                triggerEvent  = triggerEvent,
                timestamp     = timestamp
            };
        }

        #endregion
    }
}
