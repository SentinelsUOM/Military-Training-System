// Module 4 | Sentinels | University of Moratuwa | 2026

using System.Collections.Generic;
using System.Linq;
using TeamSentinels.Module4.Data;
using UnityEngine;

namespace TeamSentinels.Module4.Analysis
{
    /// <summary>
    /// Pure static class. Computes a PerformanceSummary from raw session logs.
    /// No MonoBehaviour; safe to call from any context.
    /// </summary>
    public static class PerformanceCalculator
    {
        #region Public API

        /// <summary>
        /// Calculates all performance metrics and returns a fully populated PerformanceSummary.
        /// </summary>
        /// <param name="events">All MissionEvents recorded during the session.</param>
        /// <param name="hostageHistory">All hostage emotional-state entries.</param>
        /// <param name="missionDuration">Total session time in seconds.</param>
        /// <param name="hostagesTotal">Number of unique hostages in the scenario.</param>
        public static PerformanceSummary Calculate(
            List<MissionEvent>      events,
            List<HostageStateEntry> hostageHistory,
            float                   missionDuration,
            int                     hostagesTotal)
        {
            events         ??= new List<MissionEvent>();
            hostageHistory ??= new List<HostageStateEntry>();

            // ── Shot accuracy ────────────────────────────────────────────────
            int totalShots = events.Count(e => e.eventType == "ShotFired");
            int hits       = events.Count(e => e.eventType == "TerroristHit");
            int misses     = totalShots - hits;

            float accuracyScore = Mathf.Clamp01(hits / (float)Mathf.Max(totalShots, 1));

            // ── Hostage safety ───────────────────────────────────────────────
            int hostagesSaved = CountHostagesSaved(hostageHistory);
            hostagesTotal     = Mathf.Max(hostagesTotal, 1);

            float safetyScore = (float)hostagesSaved / hostagesTotal;

            // Friendly fire penalty: -0.2 per incident
            int friendlyFire = events.Count(e =>
                e.tags != null && e.tags.Contains("friendly_fire"));
            safetyScore -= friendlyFire * 0.2f;
            safetyScore  = Mathf.Clamp01(safetyScore);

            // ── Mission success ──────────────────────────────────────────────
            // A rescued hostage is NECESSARY but not SUFFICIENT. Judging success on
            // hostagesSaved alone let a mission that ended in disaster still report a win:
            // free the hostage, get shot dead on the way out, and the AAR proudly showed
            // "RESCUE COMPLETE" above the subtitle "You were killed in action." A training
            // debrief that contradicts itself teaches the wrong lesson, so the outcome must
            // agree with HOW the mission actually ended.
            //
            // Module4SessionController.EndSession() records the reason as the sole tag on a
            // final "MissionEnded" event — that is the authoritative outcome.
            string endReason = events
                .LastOrDefault(e => e.eventType == "MissionEnded")?
                .tags?.FirstOrDefault();

            bool endedInFailure =
                endReason == "player_down"        ||   // trainee killed in action
                endReason == "hostage_executed"   ||   // captor shot the hostage
                endReason == "hostage_killed";         // trainee's own round killed the hostage

            bool missionSuccess = hostagesSaved >= 1 && !endedInFailure;

            // ── Speed ────────────────────────────────────────────────────────
            // Benchmark: 300 s. Anything longer scores 0.
            float speedScore = 1f - Mathf.Clamp01(missionDuration / 300f);

            // ── Overall ──────────────────────────────────────────────────────
            float overall = safetyScore   * 0.4f
                          + accuracyScore * 0.3f
                          + speedScore    * 0.2f
                          + (missionSuccess ? 0.1f : 0f);

            return new PerformanceSummary
            {
                safetyScore      = safetyScore,
                accuracyScore    = accuracyScore,
                speedScore       = speedScore,
                missionSuccess   = missionSuccess,
                totalShots       = totalShots,
                hits             = hits,
                misses           = misses,
                hostagesSaved    = hostagesSaved,
                hostagesTotal    = hostagesTotal,
                missionDuration  = missionDuration,
                friendlyFireCount = friendlyFire,
                overallScore     = overall
            };
        }

        #endregion

        #region Private

        private static int CountHostagesSaved(List<HostageStateEntry> history)
        {
            // A hostage is "saved" only when their final recorded state is "Freed"
            // (reached the extraction zone). "Follow" means still being escorted.
            var finalStates = new Dictionary<string, string>();
            foreach (var entry in history)
                finalStates[entry.hostageId] = entry.state;

            return finalStates.Values.Count(s => s == "Freed");
        }

        #endregion
    }
}
