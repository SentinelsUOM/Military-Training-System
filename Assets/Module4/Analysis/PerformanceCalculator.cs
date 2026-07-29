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
        /// <param name="npcStateChanges">Optional — used to count distinct terrorists for the
        /// scenario-normalized speed target. Null/empty falls back to the flat 300 s target.</param>
        /// <param name="layout">Optional — used to count rooms for the same purpose.</param>
        /// <param name="npcLevel">Optional enemy-AI tier ("dumb"|"medium"|"full") used as the
        /// difficulty term — see EvaluationContext.NpcLevel.</param>
        public static PerformanceSummary Calculate(
            List<MissionEvent>      events,
            List<HostageStateEntry> hostageHistory,
            float                   missionDuration,
            int                     hostagesTotal,
            List<NPCStateChange>    npcStateChanges = null,
            LayoutSnapshot          layout = null,
            string                  npcLevel = null)
        {
            events         ??= new List<MissionEvent>();
            hostageHistory ??= new List<HostageStateEntry>();
            npcStateChanges ??= new List<NPCStateChange>();

            // ── Shot accuracy — distance-aware ──────────────────────────────
            // Raw hits/shots implicitly treats 100% as the target, but real officers under
            // stress hit ~50% at under 3 m and ~10-15% at 3-7 m (NYPD SOP-9). Scoring against
            // that curve means a "low" raw number at real range can still be expert-level.
            int totalShots = events.Count(e => e.eventType == "ShotFired");
            int hits       = events.Count(e => e.eventType == "TerroristHit");
            int misses     = totalShots - hits;

            float hitRate = totalShots > 0 ? hits / (float)totalShots : 0f;

            var hitDistances = events
                .Where(e => e.eventType == "TerroristHit" && e.distance >= 0f)
                .Select(e => e.distance)
                .ToList();
            float avgEngagementDistance = hitDistances.Count > 0 ? hitDistances.Average() : -1f;

            float accuracyExpertRate = ExpectedHitRate(avgEngagementDistance);
            float accuracyScore = totalShots > 0
                ? Mathf.Clamp01(hitRate / accuracyExpertRate)
                : 0f;

            // ── Enemy fire (Module-2 evaluation) ─────────────────────────────
            // Objective measure of how well the terrorist AI shot the trainee.
            int enemyShots = events.Count(e => e.eventType == "EnemyShotFired");
            int enemyHits  = events.Count(e => e.eventType == "EnemyHitPlayer");

            // ── Hostage safety ───────────────────────────────────────────────
            int hostagesSaved = CountHostagesSaved(hostageHistory);
            hostagesTotal     = Mathf.Max(hostagesTotal, 1);

            float safetyScore = (float)hostagesSaved / hostagesTotal;

            // Friendly-fire penalty — SATURATING, not linear. The Expert Value Table (Piece C)
            // endorses a steep first-incident penalty with diminishing returns: 39 % / 63 % / 78 %
            // for 1/2/3 incidents, which fits 1 − 0.61ⁿ. Backed by fratricide research (modern
            // trained units <2 %, so any incident is a serious red flag). See MODULE4_EVALUATION.md.
            int friendlyFire = events.Count(e =>
                e.tags != null && e.tags.Contains("friendly_fire"));
            if (friendlyFire > 0)
                safetyScore *= Mathf.Pow(0.61f, friendlyFire);   // 1→×0.61, 2→×0.372, 3→×0.227
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

            // ── Speed — scenario-normalized target, not a flat 300 s for every mission. ─
            // Shape follows GOMS/KLM (total task time ≈ sum of subtask times + fixed
            // overhead) — that model shape is research-backed, the four coefficients below
            // are NOT independently sourced; they are this project's design defaults (same
            // status as the earlier operator-safety weights). See MODULE4_EVALUATION.md.
            //
            // The terrorist term is deliberately LINEAR, not log-scaled. Hick's Law describes
            // reaction time for ONE decision among N SIMULTANEOUS options (worth tens–hundreds
            // of ms) — it does not describe the cumulative time cost of N threats encountered
            // sequentially over a multi-minute mission. Applying it here would misuse the law
            // to justify an unrelated coefficient, so it is not used for this term.
            int roomCount = layout?.rooms?.Count ?? 0;
            int terroristCount = npcStateChanges
                .Where(c => c.actorType == "Terrorist")
                .Select(c => c.actorId)
                .Distinct()
                .Count();

            float targetTime;
            if (roomCount > 0 || terroristCount > 0)
            {
                float difficultyBonus = npcLevel switch
                {
                    "medium" => SPEED_DIFFICULTY_STEP,
                    "full"   => SPEED_DIFFICULTY_STEP * 2f,
                    _        => 0f,   // "dumb", null, or unrecognised
                };
                targetTime = SPEED_BASE_SECONDS
                           + SPEED_PER_ROOM * roomCount
                           + SPEED_PER_TERRORIST * terroristCount
                           + difficultyBonus;
            }
            else
            {
                // No scenario metadata available (old session, ad-hoc test) — keep the
                // previous flat behaviour instead of dividing by a near-zero target.
                targetTime = SPEED_FALLBACK_TARGET;
            }

            float speedScore = 1f - Mathf.Clamp01(missionDuration / (2f * targetTime));

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
                enemyShots       = enemyShots,
                enemyHits        = enemyHits,
                hostagesSaved    = hostagesSaved,
                hostagesTotal    = hostagesTotal,
                missionDuration  = missionDuration,
                friendlyFireCount = friendlyFire,
                overallScore     = overall,
                avgEngagementDistance = avgEngagementDistance,
                accuracyExpertRate    = accuracyExpertRate,
                speedTargetTime = targetTime,
                speedRoomCount  = roomCount,
                speedTerroristCount = terroristCount
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

            // ── Weapon discipline — friendly fire AND negligent discharge. ─────
            // Friendly fire: same saturating penalty as hostage safety (Piece C): 1 − 0.61ⁿ.
            int friendlyFire = events.Count(e => e.tags != null && e.tags.Contains("friendly_fire"));
            float friendlyFireFactor = Mathf.Clamp01(Mathf.Pow(0.61f, friendlyFire)); // 0→1, 1→0.61, 2→0.372

            // Negligent discharge: rounds fired with no terrorist visible at all (tagged by
            // GunFireDetector). Benchmark: expert ≈17%, novice ≈61% (qualification-failure
            // studies) — so 17% still scores full marks, not 0%.
            int totalShots = events.Count(e => e.eventType == "ShotFired");
            int negligentDischarges = events.Count(e =>
                e.eventType == "ShotFired" && e.tags != null && e.tags.Contains("no_target_in_los"));
            float negligentDischargeRate = totalShots > 0 ? negligentDischarges / (float)totalShots : 0f;
            float negligentDischargeFactor = Mathf.Clamp01(
                1f - (negligentDischargeRate - OP_EXPERT_ND_RATE) / (OP_NOVICE_ND_RATE - OP_EXPERT_ND_RATE));

            float weaponDiscipline = Mathf.Clamp01(friendlyFireFactor * negligentDischargeFactor);

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
            perf.opNegligentDischarges     = negligentDischarges;
            perf.opNegligentDischargeRate  = negligentDischargeRate;
        }

        /// <summary>
        /// NYPD SOP-9 expected hit rate at the given engagement distance (metres). Banded, not
        /// a fitted curve — the source data itself is bucketed field statistics, so a smooth
        /// curve would imply precision the five bands don't have. -1 (no distance data) falls
        /// back to the close-range band, matching the dashboard's expectedHitRate().
        /// </summary>
        public static float ExpectedHitRate(float distanceM)
        {
            if (distanceM < 0f)  return 0.50f;
            if (distanceM < 3f)  return 0.50f;
            if (distanceM < 7f)  return 0.125f;
            if (distanceM < 15f) return 0.06f;
            if (distanceM < 25f) return 0.045f;
            return 0.03f;
        }

        #endregion

        #region Private

        // Operator-safety tunable references (design defaults; see PLAYER_SAFETY_SCORE.md §4.6).
        private const float OP_TARGET_EXPOSURE_FRACTION = 0.5f;  // seen ≤ half the engaged time = full marks
        private const float OP_FAST_RT = 0.3f;                   // s — expert reaction (Hick's Law 1–2 choice ≈ 0.26–0.33 s)
        private const float OP_SLOW_RT = 0.7f;                   // s — slow (even 8-choice ≈ 0.46 s, so 0.7 s is clearly slow)
        private const float OP_DIED_CAP = 0.20f;                 // can't be "safe" if killed
        private const float OP_EXPERT_ND_RATE = 0.17f;            // negligent-discharge rate, expert (qualification-failure studies)
        private const float OP_NOVICE_ND_RATE = 0.61f;            // negligent-discharge rate, novice

        // Speed target-time coefficients — design defaults, NOT independently sourced (the
        // GOMS/KLM shape is; these numbers are not). See PerformanceCalculator.Calculate().
        private const float SPEED_BASE_SECONDS    = 30f;
        private const float SPEED_PER_ROOM        = 5f;
        private const float SPEED_PER_TERRORIST   = 20f;
        private const float SPEED_DIFFICULTY_STEP = 30f;   // per NpcLevel tier above "dumb"
        private const float SPEED_FALLBACK_TARGET = 300f;  // used when no scenario metadata is available

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
