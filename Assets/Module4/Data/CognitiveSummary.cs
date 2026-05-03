// Module 4 | Sentinels | University of Moratuwa | 2026

using Newtonsoft.Json;

namespace TeamSentinels.Module4.Data
{
    /// <summary>
    /// Aggregated cognitive-performance metrics produced by Module 3 analysis.
    /// estimatedCognitiveLoad / estimatedStressLevel: "low" | "medium" | "high"
    /// </summary>
    public class CognitiveSummary
    {
        #region Data

        [JsonProperty("averageReactionTime")]
        public float averageReactionTime;

        [JsonProperty("peakReactionTime")]
        public float peakReactionTime;

        [JsonProperty("movementInitiationScore")]
        public float movementInitiationScore;

        [JsonProperty("stabilityScore")]
        public float stabilityScore;

        [JsonProperty("attentionScore")]
        public float attentionScore;

        [JsonProperty("estimatedCognitiveLoad")]
        public string estimatedCognitiveLoad;

        [JsonProperty("estimatedStressLevel")]
        public string estimatedStressLevel;

        #endregion

        #region Public API

        /// <summary>
        /// Derives load and stress level labels from numeric scores.
        /// Called once all samples have been accumulated.
        /// </summary>
        public void DeriveLabels()
        {
            estimatedCognitiveLoad = ClassifyLoad(averageReactionTime, stabilityScore);
            estimatedStressLevel   = ClassifyStress(averageReactionTime, attentionScore);
        }

        #endregion

        #region Private

        private static string ClassifyLoad(float reactionTime, float stability)
        {
            float load = reactionTime * 0.5f + (1f - stability) * 0.5f;
            if (load < 0.33f) return "low";
            if (load < 0.66f) return "medium";
            return "high";
        }

        private static string ClassifyStress(float reactionTime, float attention)
        {
            float stress = reactionTime * 0.6f + (1f - attention) * 0.4f;
            if (stress < 0.33f) return "low";
            if (stress < 0.66f) return "medium";
            return "high";
        }

        #endregion
    }
}
