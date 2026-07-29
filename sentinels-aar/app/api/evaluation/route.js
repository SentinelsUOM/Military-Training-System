import { NextResponse } from 'next/server'
import { connectDB } from '@/lib/mongodb'
import Session from '@/lib/models/Session'

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
// Module 4's own 5 scores specifically (subset of METRIC_KEYS) — used to build the
// "Module 4 Score Validity" panel and the repeated-trial / expert-benchmark sections.
const MODULE4_SCORE_KEYS = ['overallScore', 'safetyScore', 'accuracy', 'speedScore', 'operatorSafetyScore']

const round3 = (n) => (typeof n === 'number' ? Math.round(n * 1000) / 1000 : n)

function metricsOf(s) {
  const p = s.performance || {}
  const c = s.cognitiveSummary || {}
  const accuracy      = p.totalShots > 0 ? Math.round((p.hits / p.totalShots) * 1000) / 10 : null
  // Enemy hit-rate: how well the terrorist AI shot the trainee (objective ablation measure).
  const enemyAccuracy = p.enemyShots > 0 ? Math.round((p.enemyHits / p.enemyShots) * 1000) / 10 : null
  return {
    reactionTime: c.averageReactionTime ?? null,
    accuracy,
    enemyAccuracy,
    duration:     p.missionDuration ?? null,
    overallScore: round3(p.overallScore) ?? null,
    missionSuccess: p.missionSuccess ?? null,
    friendlyFire: p.friendlyFireCount ?? null,
    // Module 4's own scores (0-1 scale, unlike the percentage-formatted accuracy above).
    safetyScore:         round3(p.safetyScore) ?? null,
    speedScore:          round3(p.speedScore) ?? null,
    operatorSafetyScore: round3(p.operatorSafetyScore) ?? null
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
      .select('sessionId playerId npcLevel performance cognitiveSummary aiEval.derived simTlx.derived createdAt trialIndex')
      .sort({ createdAt: -1 })
      .lean()

    // ── Group by player, keep the most-recent session per level ────────────
    const byPlayer = {}
    const perLevel = { basic: [], intermediate: [], advanced: [] } // for averages
    const byPlayerLevel = {} // ALL sessions per player+level, for the repeated-trial section

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

      const key = `${s.playerId}::${lvl}`
      byPlayerLevel[key] = byPlayerLevel[key] || { playerId: s.playerId, level: lvl, trials: [] }
      byPlayerLevel[key].trials.push(play)
    }

    const players = Object.values(byPlayer).sort((a, b) => a.playerId.localeCompare(b.playerId))

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

    // ── Repeated trials: same player + same level, played more than once ───
    // Sorted oldest→newest (trial 1, 2, 3…) since that's the natural reading order.
    // No filtering beyond "played the same level more than once" — an earlier attempt to
    // filter by "has a completed survey" turned out not to reliably separate real study
    // plays from leftover dev-test sessions (some dev sessions DO have a survey attached
    // from earlier UI testing), so this shows exactly what the data says, honestly.
    const retests = Object.values(byPlayerLevel)
      .filter(g => g.trials.length > 1)
      .map(g => ({ ...g, trials: [...g.trials].sort((a, b) => new Date(a.createdAt) - new Date(b.createdAt)) }))
      .sort((a, b) => a.playerId.localeCompare(b.playerId) || a.level.localeCompare(b.level))

    return NextResponse.json(
      { players, averages, retests, surveyKeys: SURVEY_KEYS, metricKeys: METRIC_KEYS, module4ScoreKeys: MODULE4_SCORE_KEYS },
      { headers: corsHeaders }
    )
  } catch (err) {
    console.error('[GET /api/evaluation]', err)
    return NextResponse.json({ error: err.message }, { status: 500, headers: corsHeaders })
  }
}
