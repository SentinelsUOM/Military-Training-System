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
        /// <param name="npcChanges">All NPC state transitions (used for terrorist count).</param>
        /// <param name="hostageHistory">All hostage emotional-state entries.</param>
        /// <param name="missionDuration">Total session time in seconds.</param>
        /// <param name="hostagesTotal">Number of unique hostages in the scenario.</param>
        public static PerformanceSummary Calculate(
            List<MissionEvent>      events,
            List<NPCStateChange>    npcChanges,
            List<HostageStateEntry> hostageHistory,
            float                   missionDuration,
            int                     hostagesTotal)
        {
            events         ??= new List<MissionEvent>();
            npcChanges     ??= new List<NPCStateChange>();
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
            int terroristsDown = events.Count(e => e.eventType == "TerroristDown");
            int terroristsExpected = CountTerrorists(npcChanges);
            bool missionSuccess = hostagesSaved >= 1 && terroristsDown >= terroristsExpected;

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
            // A hostage is "saved" if their final recorded state is "Follow"
            var finalStates = new Dictionary<string, string>();
            foreach (var entry in history)
                finalStates[entry.hostageId] = entry.state;

            return finalStates.Values.Count(s => s == "Follow");
        }

        private static int CountTerrorists(List<NPCStateChange> npcChanges)
        {
            // Infer distinct terrorist actor IDs from state change records
            var ids = npcChanges
                .Where(c => c.actorType == "Terrorist")
                .Select(c => c.actorId)
                .Distinct()
                .Count();

            // If no state changes were logged, assume at least 1 to avoid division issues
            return Mathf.Max(ids, 1);
        }

        #endregion
    }
}
