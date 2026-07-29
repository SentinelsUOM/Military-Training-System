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

        // ── Enemy fire (Module-2 evaluation) ─────────────────────────────────
        // How well the terrorist AI shot the trainee — an OBJECTIVE behavioural
        // measure of the AI level (smarter AI should land more of its shots).
        [JsonProperty("enemyShots")]
        public int enemyShots;   // live rounds the terrorists fired at the trainee

        [JsonProperty("enemyHits")]
        public int enemyHits;    // those rounds that actually hit the trainee

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

        // ── Distance-aware accuracy ──────────────────────────────────────────
        // accuracyScore above is now hitRate ÷ expectedHitRateAtRange, NOT a raw ratio —
        // see PerformanceCalculator. These two fields make that transparent to the AAR.
        [JsonProperty("avgEngagementDistance")]
        public float avgEngagementDistance = -1f;   // metres; -1 = no hit-distance data

        [JsonProperty("accuracyExpertRate")]
        public float accuracyExpertRate;            // the NYPD SOP-9 rate this session was judged against

        // ── Scenario-normalized speed ────────────────────────────────────────
        // speedScore above is judged against speedTargetTime (rooms/terrorists/difficulty
        // aware), not a flat 300 s. Coefficients are design defaults — see PerformanceCalculator.cs.
        [JsonProperty("speedTargetTime")]
        public float speedTargetTime;

        [JsonProperty("speedRoomCount")]
        public int speedRoomCount;

        [JsonProperty("speedTerroristCount")]
        public int speedTerroristCount;

        // ── Operator (player) safety ─────────────────────────────────────────
        // How safely the TRAINEE conducted themselves — distinct from safetyScore,
        // which is the HOSTAGE's safety. Grounded in survivability doctrine (exposure)
        // and the validated CQB assessment instrument. See PLAYER_SAFETY_SCORE.md.
        [JsonProperty("operatorSafetyScore")]
        public float operatorSafetyScore;

        [JsonProperty("opSurvivability")]
        public float opSurvivability;      // health kept (0 if downed)

        [JsonProperty("opExposureControl")]
        public float opExposureControl;    // 1 − time spent in enemy line-of-sight

        [JsonProperty("opWeaponDiscipline")]
        public float opWeaponDiscipline;   // 1 − friendly-fire penalty

        [JsonProperty("opThreatResponse")]
        public float opThreatResponse;     // faster reaction = safer

        [JsonProperty("opExposedSeconds")]
        public float opExposedSeconds;     // raw seconds detected (for transparency)

        [JsonProperty("opFinalHealth")]
        public int opFinalHealth;          // trainee HP at mission end (−1 = not captured)

        // Negligent discharge — rounds fired with no terrorist visible at all. Folded into
        // opWeaponDiscipline alongside friendly fire. Benchmark: expert ≈17%, novice ≈61%.
        [JsonProperty("opNegligentDischarges")]
        public int opNegligentDischarges;

        [JsonProperty("opNegligentDischargeRate")]
        public float opNegligentDischargeRate;   // negligentDischarges / totalShots

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
