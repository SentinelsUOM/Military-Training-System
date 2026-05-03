// Module 4 | Sentinels | University of Moratuwa | 2026

using Newtonsoft.Json;

namespace TeamSentinels.Module4.Data
{
    /// <summary>
    /// Aggregated performance metrics for a completed training session.
    /// Computed by PerformanceCalculator; stored inside SessionSummary.
    /// overallScore = safety*0.4 + accuracy*0.3 + speed*0.2 + (missionSuccess ? 0.1 : 0)
    /// </summary>
    public class PerformanceSummary
    {
        #region Data

        [JsonProperty("safetyScore")]
        public float safetyScore;

        [JsonProperty("accuracyScore")]
        public float accuracyScore;

        [JsonProperty("speedScore")]
        public float speedScore;

        [JsonProperty("missionSuccess")]
        public bool missionSuccess;

        [JsonProperty("totalShots")]
        public int totalShots;

        [JsonProperty("hits")]
        public int hits;

        [JsonProperty("misses")]
        public int misses;

        [JsonProperty("hostagesSaved")]
        public int hostagesSaved;

        [JsonProperty("hostagesTotal")]
        public int hostagesTotal;

        [JsonProperty("missionDuration")]
        public float missionDuration;

        [JsonProperty("friendlyFireCount")]
        public int friendlyFireCount;

        [JsonProperty("overallScore")]
        public float overallScore;

        #endregion

        #region Public API

        /// <summary>Recomputes overallScore from the current component scores.</summary>
        public void RecalculateOverallScore()
        {
            overallScore = safetyScore * 0.4f
                         + accuracyScore * 0.3f
                         + speedScore   * 0.2f
                         + (missionSuccess ? 0.1f : 0f);
        }

        #endregion
    }
}
