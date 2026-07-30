import { NextResponse } from 'next/server'
import { connectDB } from '@/lib/mongodb'
import Session from '@/lib/models/Session'
import { evaluateMovementAgainstExperts } from '@/lib/movementBenchmarks'

const corsHeaders = {
  'Access-Control-Allow-Origin': '*',
  'Access-Control-Allow-Methods': 'GET, OPTIONS',
  'Access-Control-Allow-Headers': 'Content-Type'
}

const LEVELS = ['basic', 'intermediate', 'advanced']
// Map the old tier names onto the new ones so sessions tagged before the rename
// still land in the right bucket.
const LEVEL_ALIASES = { dumb: 'basic', medium: 'intermediate', full: 'advanced' }

// The survey sub-scale means we compare (from aiEval.derived, see lib/aiEval.js).
const SURVEY_KEYS = ['perceivedIntelligence', 'animacy', 'realism', 'ueqPragmatic', 'ueqHedonic']
// The objective metrics we compare (from the session's own telemetry). The last three
// (safetyScore/speedScore/operatorSafetyScore) are Module 4's OWN scores — everything
// before them belongs to Module 2/3 (AI behaviour, cognitive telemetry). See METRIC_KEYS
// vs MODULE4_SCORE_KEYS below.
const METRIC_KEYS = ['reactionTime', 'accuracy', 'enemyAccuracy', 'duration', 'overallScore', 'safetyScore', 'speedScore', 'operatorSafetyScore']
// Module 4's own 5 scores specifically — used to build the combined per-player table, the
// pooled vs-expert test, and the repeated-trial / expert-benchmark sections. Uses
// `accuracyScore` (Unity's distance-normalized 0-1 score), NOT `accuracy` (the raw
// hit/shots % above, which is a Module-2-style objective-telemetry number, not what Module
// 4's own accuracy score actually is — the two were conflated earlier and are now split).
const MODULE4_SCORE_KEYS = ['overallScore', 'safetyScore', 'accuracyScore', 'speedScore', 'operatorSafetyScore']
// Module 3's measures here: reaction time (from cognitiveSummary) plus body-movement and
// per-channel reaction metrics (from movementTrack, via the same literature bands used by
// the per-session Movement tab — see lib/movementBenchmarks.js). Workload reasoning and
// stability/attention proxies still live only in the per-session tabs, not this page.
// reactionHead is deliberately excluded: no session has ever recorded a head-channel
// reaction (Unity's ReactionTimeTracker never tags one in practice), so it's always null —
// including it would just render an empty column/row everywhere.
const MODULE3_SCORE_KEYS = [
  'reactionTime',
  'avgSpeed', 'maxSpeed', 'timeCrouched', 'headScanning', 'weaponHeldPct', 'handTravel',
  'reactionHands', 'reactionMovement', 'reactionTrigger', 'threatsResponded',
]

const round3 = (n) => (typeof n === 'number' ? Math.round(n * 1000) / 1000 : n)

function metricsOf(s) {
  const p = s.performance || {}
  const c = s.cognitiveSummary || {}
  const accuracy      = p.totalShots > 0 ? Math.round((p.hits / p.totalShots) * 1000) / 10 : null
  // Enemy hit-rate: how well the terrorist AI shot the trainee (objective ablation measure).
  const enemyAccuracy = p.enemyShots > 0 ? Math.round((p.enemyHits / p.enemyShots) * 1000) / 10 : null
  // Same literature-band evaluator the per-session Movement tab uses — reused here so the
  // pooled/per-player numbers are guaranteed to match what a trainee sees on their own
  // session page. Only .traineeValue (the raw metric) is used; verdicts are re-derived
  // client-side from the aggregated (multi-session) value instead of averaging verdicts.
  const mv = evaluateMovementAgainstExperts(s)
  return {
    reactionTime: c.averageReactionTime ?? null,
    accuracy,
    enemyAccuracy,
    duration:     p.missionDuration ?? null,
    overallScore: round3(p.overallScore) ?? null,
    missionSuccess: p.missionSuccess ?? null,
    friendlyFire: p.friendlyFireCount ?? null,
    // Module 4's own scores (0-1 scale, unlike the percentage-formatted `accuracy` above).
    // accuracyScore is the real distance-normalized score (hitRate ÷ expected rate at
    // range) — a different number from the raw `accuracy` hit percentage above.
    safetyScore:         round3(p.safetyScore) ?? null,
    accuracyScore:       round3(p.accuracyScore) ?? null,
    speedScore:          round3(p.speedScore) ?? null,
    operatorSafetyScore: round3(p.operatorSafetyScore) ?? null,
    // Module 3 body-movement & reaction-channel metrics (movementTrack.stats / .reactions).
    avgSpeed:         mv.avgSpeed.traineeValue,
    maxSpeed:         mv.maxSpeed.traineeValue,
    timeCrouched:     mv.timeCrouched.traineeValue,
    headScanning:     mv.headScanning.traineeValue,
    weaponHeldPct:    mv.weaponHeldPct.traineeValue,
    handTravel:       mv.handTravel.traineeValue,
    reactionHands:    mv.reactionHands.traineeValue,
    reactionMovement: mv.reactionMovement.traineeValue,
    reactionTrigger:  mv.reactionTrigger.traineeValue,
    threatsResponded: mv.threatsResponded.traineeValue,
  }
}

const round1 = (n) => Math.round(n * 10) / 10
function mean(values) {
  const nums = values.filter(v => typeof v === 'number' && !Number.isNaN(v))
  return nums.length ? round1(nums.reduce((a, b) => a + b, 0) / nums.length) : null
}

export async function OPTIONS() {
  return NextResponse.json({}, { headers: corsHeaders })
}

// Returns the Module-2 evaluation dataset:
//   players[]  — each player's most-recent play at each level (survey means + metrics)
//   averages   — per-level averages across ALL study sessions (+ n)
// Only sessions tagged with playerId + a recognised npcLevel are included.
export async function GET() {
  try {
    await connectDB()

    const sessions = await Session.find({
      playerId: { $nin: [null, ''] },
      npcLevel: { $nin: [null, ''] }
    })
      .select('sessionId playerId npcLevel performance cognitiveSummary aiEval.derived simTlx.derived createdAt trialIndex movementTrack.stats movementTrack.reactionStats movementTrack.reactions.channel movementTrack.reactions.reactionTime')
      .sort({ createdAt: -1 })
      .lean()

    // ── Group by player, keep the most-recent session per level ────────────
    const byPlayer = {}
    const perLevel = { basic: [], intermediate: [], advanced: [] } // for averages — Module 2 ONLY
    const byPlayerAll = {} // ALL sessions per player, ANY level — Module 3 & 4 (no tier concept)

    for (const s of sessions) {
      let lvl = String(s.npcLevel).toLowerCase()
      lvl = LEVEL_ALIASES[lvl] || lvl
      if (!LEVELS.includes(lvl)) continue

      const play = {
        sessionId: s.sessionId,
        aiEval:    s.aiEval?.derived || null,
        workload:  s.simTlx?.derived?.overallWorkload ?? null,
        metrics:   metricsOf(s),
        createdAt: s.createdAt
      }
      perLevel[lvl].push(play)

      byPlayer[s.playerId] = byPlayer[s.playerId] || { playerId: s.playerId, levels: {} }
      if (!byPlayer[s.playerId].levels[lvl]) byPlayer[s.playerId].levels[lvl] = play // newest first

      byPlayerAll[s.playerId] = byPlayerAll[s.playerId] || []
      byPlayerAll[s.playerId].push(play)
    }

    const players = Object.values(byPlayer).sort((a, b) => a.playerId.localeCompare(b.playerId))

    // ── Module 3 & 4 don't have an AI-difficulty independent variable — that's Module
    // 2's own question. So instead of splitting Basic/Intermediate/Advanced, pool ALL of
    // a player's sessions (any level) into one combined average per player, for a
    // per-player "your average vs expert" comparison. `pooledValues` also collects every
    // individual session value across ALL players for the same measures, so a one-sample
    // test (client-side, lib/stats.js oneSampleWilcoxon) can ask "does the trainee
    // population's average differ from the expert benchmark?" rather than "does level
    // change the score?" (that second question stays Module 2-only, §averages/retests above).
    const combinedKeys = [...new Set([...MODULE3_SCORE_KEYS, ...MODULE4_SCORE_KEYS])]
    for (const p of players) {
      const plays = [...(byPlayerAll[p.playerId] || [])].sort((a, b) => new Date(a.createdAt) - new Date(b.createdAt))
      const combinedMetrics = {}
      for (const k of combinedKeys) combinedMetrics[k] = mean(plays.map(pl => pl.metrics?.[k]))
      p.combined = { n: plays.length, metrics: combinedMetrics }
      // Every session this player played, ANY tier, oldest→newest — used by Module 4's
      // reliability/benchmark sections instead of the old same-tier-only grouping, since
      // Module 3/4 don't split by AI level at all (that distinction is Module 2-only).
      p.allTrials = plays
    }

    const pooledValues = {}
    for (const k of combinedKeys) {
      pooledValues[k] = sessions
        .map(s => metricsOf(s)[k])
        .filter(v => typeof v === 'number' && !Number.isNaN(v))
    }

    // ── Per-level averages across all study sessions ───────────────────────
    const averages = {}
    for (const lvl of LEVELS) {
      const plays = perLevel[lvl]
      const survey = {}
      for (const k of SURVEY_KEYS) survey[k] = mean(plays.map(p => p.aiEval?.[k]))
      const metrics = {}
      for (const k of METRIC_KEYS) metrics[k] = mean(plays.map(p => p.metrics?.[k]))
      averages[lvl] = { n: plays.length, survey, metrics, workload: mean(plays.map(p => p.workload)) }
    }

    return NextResponse.json(
      {
        players, averages, pooledValues,
        surveyKeys: SURVEY_KEYS, metricKeys: METRIC_KEYS,
        module3ScoreKeys: MODULE3_SCORE_KEYS, module4ScoreKeys: MODULE4_SCORE_KEYS
      },
      { headers: corsHeaders }
    )
  } catch (err) {
    console.error('[GET /api/evaluation]', err)
    return NextResponse.json({ error: err.message }, { status: 500, headers: corsHeaders })
  }
}
