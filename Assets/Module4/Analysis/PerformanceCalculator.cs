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

        /// <summary>
        /// Computes the trainee's own safety (distinct from hostage safety) and writes it into
        /// <paramref name="perf"/>. Grounded in survivability doctrine (minimise exposure) and the
        /// validated CQB assessment instrument (tactical behaviour, weapon handling, response time,
        /// mistakes). Equal weighting follows that instrument's equal-item structure. Call after the
        /// cognitive summary is built, so a real reaction time is available. See PLAYER_SAFETY_SCORE.md.
        /// </summary>
        public static void ComputeOperatorSafety(
            PerformanceSummary perf, List<MissionEvent> events,
            float avgReactionTime, int finalHealth, int maxHealth)
        {
            events ??= new List<MissionEvent>();

            string endReason = events.LastOrDefault(e => e.eventType == "MissionEnded")?.tags?.FirstOrDefault();
            bool died = endReason == "player_down";

            // ── Survivability — fraction of health kept; 0 if downed. ──────────
            float survivability;
            if (died)                                     survivability = 0f;
            else if (maxHealth > 0 && finalHealth >= 0)   survivability = Mathf.Clamp01(finalHealth / (float)maxHealth);
            else                                          survivability = 1f;   // health unknown → assume unhurt

            // ── Exposure control — time in an enemy's line of sight. ───────────
            float exposed = ExposedSeconds(events, perf.missionDuration, out float firstSeen, out int maxConcurrent);
            float exposureControl;
            if (firstSeen < 0f)
            {
                exposureControl = 1f;   // never detected = ideal
            }
            else
            {
                float engaged = Mathf.Max(1f, perf.missionDuration - firstSeen);
                float frac = exposed / (engaged * OP_TARGET_EXPOSURE_FRACTION);
                exposureControl = Mathf.Clamp01(1f - frac);
                if (maxConcurrent > 1)   // flanked — several enemies saw you at once
                    exposureControl = Mathf.Clamp01(exposureControl - 0.1f * (maxConcurrent - 1));
            }

            // ── Weapon discipline — friendly fire is the measurable failure. ───
            int friendlyFire = events.Count(e => e.tags != null && e.tags.Contains("friendly_fire"));
            float weaponDiscipline = Mathf.Clamp01(1f - 0.5f * friendlyFire);

            // ── Threat response — faster = safer; neutral 0.5 if no RT data. ───
            float threatResponse = avgReactionTime <= 0f
                ? 0.5f
                : Mathf.Clamp01(1f - (avgReactionTime - OP_FAST_RT) / (OP_SLOW_RT - OP_FAST_RT));

            // ── Equal-weight composite; capped low if the trainee was killed. ──
            float op = 0.25f * survivability
                     + 0.25f * exposureControl
                     + 0.25f * weaponDiscipline
                     + 0.25f * threatResponse;
            if (died) op = Mathf.Min(op, OP_DIED_CAP);

            perf.operatorSafetyScore = op;
            perf.opSurvivability     = survivability;
            perf.opExposureControl   = exposureControl;
            perf.opWeaponDiscipline  = weaponDiscipline;
            perf.opThreatResponse    = threatResponse;
            perf.opExposedSeconds    = exposed;
            perf.opFinalHealth       = finalHealth;
        }

        #endregion

        #region Private

        // Operator-safety tunable references (design defaults; see PLAYER_SAFETY_SCORE.md §4.6).
        private const float OP_TARGET_EXPOSURE_FRACTION = 0.5f;  // seen ≤ half the engaged time = full marks
        private const float OP_FAST_RT = 0.3f;                   // s — expert-fast reaction (RT literature)
        private const float OP_SLOW_RT = 1.5f;                   // s — slow reaction
        private const float OP_DIED_CAP = 0.20f;                 // can't be "safe" if killed

        /// <summary>
        /// Seconds the trainee spent inside at least one enemy's line of sight, from paired
        /// PlayerSeen(open)/PlayerLost(close) events (per NPC = sourceActorId). Overlapping
        /// windows are unioned, not double-counted. Also returns the time of first detection
        /// and the peak number of enemies seeing the trainee at once (a flanking signal).
        /// </summary>
        private static float ExposedSeconds(List<MissionEvent> events, float missionEnd,
            out float firstSeen, out int maxConcurrent)
        {
            firstSeen = -1f;
            maxConcurrent = 0;

            var ordered = events
                .Where(e => e.eventType == "PlayerSeen" || e.eventType == "PlayerLost")
                .OrderBy(e => e.timestamp)
                .ToList();

            var seeing = new HashSet<string>();
            float exposed  = 0f;
            float segStart = 0f;
            bool  open     = false;

            foreach (var e in ordered)
            {
                if (open) exposed += e.timestamp - segStart;

                string npc = e.sourceActorId ?? "?";
                if (e.eventType == "PlayerSeen")
                {
                    seeing.Add(npc);
                    if (firstSeen < 0f) firstSeen = e.timestamp;
                }
                else
                {
                    seeing.Remove(npc);
                }

                if (seeing.Count > maxConcurrent) maxConcurrent = seeing.Count;
                open     = seeing.Count > 0;
                segStart = e.timestamp;
            }

            // Close a still-open window at mission end (e.g. seen, then died with no PlayerLost).
            if (open && missionEnd > segStart) exposed += missionEnd - segStart;

            return exposed;
        }

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
