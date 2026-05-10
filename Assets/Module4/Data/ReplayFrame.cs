// Module 4 | Sentinels | University of Moratuwa | 2026

using System.Collections.Generic;
using Newtonsoft.Json;

namespace TeamSentinels.Module4.Data
{
    /// <summary>
    /// A single snapshot of one actor's transform and FSM state at a point in time.
    /// Captured by ReplayRecorder at a fixed interval for data-visualisation replay.
    /// </summary>
    public class ReplayFrame
    {
        #region Data

        [JsonProperty("timestamp")]
        public float timestamp;

        [JsonProperty("actorId")]
        public string actorId;

        [JsonProperty("position")]
        public Vector3Serializable position;

        [JsonProperty("rotation")]
        public Vector3Serializable rotation;

        [JsonProperty("currentState")]
        public string currentState;

        [JsonProperty("annotations")]
        public List<string> annotations = new List<string>();

        #endregion

        #region Public API

        /// <summary>Creates a ReplayFrame from raw values.</summary>
        public static ReplayFrame Create(float timestamp, string actorId,
            Vector3Serializable position, Vector3Serializable rotation,
            string currentState, List<string> annotations = null)
        {
            return new ReplayFrame
            {
                timestamp    = timestamp,
                actorId      = actorId,
                position     = position,
                rotation     = rotation,
                currentState = currentState,
                annotations  = annotations ?? new List<string>()
            };
        }

        #endregion
    }
}
