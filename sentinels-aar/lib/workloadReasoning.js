// Research-backed reasoning + prediction for the Workload (SIM-TLX) tab.
//
// lib/simTlx.js computes composite/overall workload scores but never explains
// *why* a given session's score is what it is, or what that score predicts
// about how the trainee actually performed. This module adds both, grounded
// in cited research — see MODULE4_WORKLOAD_REASONING_METHODOLOGY.md for the
// full derivation of every rule and threshold below.
//
// Only factors whose condition is actually true for a given session are
// surfaced (no generic filler text), and the performance-zone prediction is
// checked against this session's own outcome data so a claim is never shown
// without also showing whether it held up.

import { evaluateMovementAgainstExperts } from './movementBenchmarks'

export const CITATIONS = {
  sweller2010: {
    authors: 'Sweller J',
    year: 2010,
    title: 'Element Interactivity and Intrinsic, Extraneous, and Germane Cognitive Load',
    source: 'Educational Psychology Review, 22(2), 123–138',
    url: 'https://link.springer.com/article/10.1007/s10648-010-9128-5',
    finding: 'Intrinsic cognitive load rises with "element interactivity" — the number of interacting components that must be held in working memory simultaneously.',
  },
  wickens2008: {
    authors: 'Wickens CD',
    year: 2008,
    title: 'Multiple Resources and Mental Workload',
    source: 'Human Factors, 50(3), 449–455',
    url: 'https://journals.sagepub.com/doi/10.1518/001872008X288394',
    finding: 'Concurrent tasks drawing on the same processing resource (e.g. visual scanning + aiming + monitoring) cost more mental/temporal demand than task count alone predicts.',
  },
  liMultitask2022: {
    authors: 'Li et al.',
    year: 2022,
    title: 'Evaluating mental workload during multitasking in simulated flight',
    source: 'Brain and Behavior, PMC9014989',
    url: 'https://www.ncbi.nlm.nih.gov/pmc/articles/PMC9014989/',
    finding: 'Measured NASA-TLX workload rose with the number of concurrent subtasks in a controlled multitasking study.',
  },
  harris2020: {
    authors: 'Harris D, Wilson M, Vine S',
    year: 2020,
    title: 'Development and validation of a simulation workload measure: the simulation task load index (SIM-TLX)',
    source: 'Virtual Reality, 24, 557–566',
    url: 'https://doi.org/10.1007/s10055-019-00422-9',
    finding: 'Experimental manipulations changed the predicted subscale in 9 of 10 cases (p<.05) — Task Complexity, Situational Stress and Frustration are validated as distinct, sensitive constructs.',
  },
  eysenck2007: {
    authors: 'Eysenck MW, Derakshan N, Santos R, Calvo MG',
    year: 2007,
    title: 'Anxiety and Cognitive Performance: Attentional Control Theory',
    source: 'Emotion, 7(2), 336–353',
    url: 'https://pubmed.ncbi.nlm.nih.gov/17516812/',
    finding: 'Anxiety/stress impairs processing efficiency (effort spent) more than performance effectiveness (output quality) — compensatory effort can mask stress behind steady accuracy, up to a point.',
  },
  inverseUShooting: {
    authors: 'PMC12749011 authors',
    year: 2024,
    title: 'The inverted-U relationship between stress and performance in elite shooting',
    source: 'PMC12749011',
    url: 'https://pmc.ncbi.nlm.nih.gov/articles/PMC12749011/',
    finding: 'Rifle/pistol shooters scored highest in the moderately-decisive middle stages of competition and worst at the least- and most-decisive stages — performance peaks at moderate stress, not minimal or maximal stress.',
  },
  gaitCognitiveLoad: {
    authors: 'PMC7350508 authors',
    year: 2020,
    title: 'Predicting Cognitive Load and Operational Performance in a Simulated Marksmanship Task',
    source: 'PMC7350508',
    url: 'https://pmc.ncbi.nlm.nih.gov/articles/PMC7350508/',
    finding: 'High cognitive load measurably changed gait (cadence, time-on-ground) during a marksmanship task — movement telemetry is an independent corroborating signal for reported workload.',
  },
}

const has = (v) => typeof v === 'number' && !Number.isNaN(v)

// scenarioConfig is declared on the schema but never actually populated by
// the current Unity build (confirmed empty across real uploaded sessions) —
// so scenario scale is derived instead from fields that ARE always present:
// the room layout snapshot, hostage count from performance, and distinct
// terrorist actors seen in the NPC state-change log.
function roomCount(session) {
  return session.layout?.rooms?.length || 0
}
function terroristCount(session) {
  const ids = new Set(
    (session.npcStateChanges || [])
      .filter(n => (n.actorType || '').toLowerCase() === 'terrorist')
      .map(n => n.actorId)
  )
  return ids.size
}
function hostageCount(session) {
  return session.performance?.hostagesTotal || 0
}

// Each rule fires only when its condition is true for this session; `factor`
// receives the session so its text can cite the session's own numbers.
const REASONING_RULES = {
  mentalPhysical: [
    {
      test: (s) => has(s.movementTrack?.stats?.totalDistance) && s.movementTrack.stats.totalDistance > 80,
      factor: (s) => `${s.movementTrack.stats.totalDistance.toFixed(0)} m of movement across the mission — sustained physical exertion drives the Physical Demands subscale, and is itself measurable in the movement telemetry rather than just self-reported.`,
      citationIds: ['gaitCognitiveLoad'],
    },
    {
      test: (s) => (s.movementTrack?.reactionStats?.stimulusCount || 0) >= 3,
      factor: (s) => `${s.movementTrack.reactionStats.stimulusCount} separate threat events each required perceiving, deciding and acting in sequence — concurrent-task mental demand compounds faster than the event count alone suggests.`,
      citationIds: ['wickens2008'],
    },
  ],
  temporalFrustration: [
    {
      test: (s) => (s.movementTrack?.reactionStats?.missedCount || 0) > 0,
      factor: (s) => `${s.movementTrack.reactionStats.missedCount} of ${s.movementTrack.reactionStats.stimulusCount} threats went unanswered — missed engagements are a documented driver of the Frustration subscale (insecure/discouraged/irritated).`,
      citationIds: ['harris2020'],
    },
    {
      test: (s) => (s.performance?.friendlyFireCount || 0) > 0,
      factor: (s) => `${s.performance.friendlyFireCount} friendly-fire incident(s) occurred — negative in-mission feedback events like this directly elevate reported frustration.`,
      citationIds: ['harris2020'],
    },
    {
      test: (s) => {
        const dur = s.performance?.missionDuration
        const count = s.movementTrack?.reactionStats?.stimulusCount
        return has(dur) && dur > 0 && count >= 3 && count / dur > 0.03
      },
      factor: (s) => `Threats arrived at a high rate (${s.movementTrack.reactionStats.stimulusCount} events over ${Math.round(s.performance.missionDuration)}s) — sustained time pressure is exactly what the Temporal Demands subscale measures, and matches studies where measured workload rose with concurrent-subtask rate.`,
      citationIds: ['liMultitask2022'],
    },
  ],
  complexityStress: [
    {
      test: (s) => roomCount(s) >= 3 && terroristCount(s) >= 2,
      factor: (s) => `${roomCount(s)} rooms and ${terroristCount(s)} hostiles to track simultaneously — per Cognitive Load Theory, more interacting elements that must be held in mind at once directly raises intrinsic cognitive load, which Task Complexity captures.`,
      citationIds: ['sweller2010'],
    },
    {
      test: (s) => hostageCount(s) >= 1 && terroristCount(s) >= 1,
      factor: (s) => `Protecting ${hostageCount(s)} hostage(s) while neutralizing ${terroristCount(s)} hostile(s) are competing objectives that must be juggled at once, adding element interactivity beyond either goal alone.`,
      citationIds: ['sweller2010'],
    },
    {
      test: (s) => (s.performance?.friendlyFireCount || 0) > 0 || (s.movementTrack?.reactionStats?.missedCount || 0) > 0,
      factor: () => `A missed or mishandled engagement occurred under threat — perceived failure in the moment is a direct driver of Situational Stress.`,
      citationIds: ['eysenck2007'],
    },
  ],
}

const ZONE_BY_WORKLOAD = (overallWorkload) => {
  if (overallWorkload < 25) {
    return {
      zone: 'under-aroused',
      label: 'Under-Aroused',
      rationale: 'Reported workload is low enough that vigilance/attention may lag rather than sharpen — the inverted-U model predicts underperformance at low arousal, not just at high arousal.',
      citationIds: ['inverseUShooting'],
    }
  }
  if (overallWorkload < 75) {
    return {
      zone: 'optimal',
      label: 'Likely Near-Optimal',
      rationale: 'This band matches the moderately-decisive stress range where elite shooters posted their best scores — moderate workload is where performance is predicted to peak, not dip.',
      citationIds: ['inverseUShooting'],
    }
  }
  return {
    zone: 'overload-risk',
    label: 'Overload Risk',
    rationale: 'Very high reported workload matches the range where the inverted-U model predicts a performance decline (though studies note the drop is usually a moderate dip, not a collapse).',
    citationIds: ['inverseUShooting', 'eysenck2007'],
  }
}

function checkPrediction(zone, session, reactionOverall, threatsResponded) {
  const evidence = []
  const accuracy = session.performance?.accuracyScore
  const friendlyFire = session.performance?.friendlyFireCount || 0
  const slowReaction = reactionOverall?.severity === 'watch'
  const fastReaction = reactionOverall?.severity === 'good'
  const lowResponseRate = threatsResponded?.severity === 'watch'

  if (zone === 'overload-risk') {
    if (friendlyFire > 0) evidence.push(`${friendlyFire} friendly-fire incident(s) this session`)
    if (has(accuracy) && accuracy < 0.5) evidence.push(`low accuracy score (${(accuracy * 100).toFixed(0)}%)`)
    if (lowResponseRate) evidence.push('a below-expert threat-response rate')
    if (slowReaction) evidence.push('slower-than-expert reaction times')
    return {
      matched: evidence.length > 0,
      evidence,
      note: evidence.length > 0
        ? `Consistent with the predicted overload effect: ${evidence.join(', ')}.`
        : 'Performance held up despite the predicted overload risk — possibly compensatory effort (Attentional Control Theory), which masks stress behind steady output up to a point.',
    }
  }

  if (zone === 'under-aroused') {
    if (lowResponseRate) evidence.push('a below-expert threat-response rate')
    if (slowReaction) evidence.push('slower-than-expert reaction times despite low reported demand')
    return {
      matched: evidence.length > 0,
      evidence,
      note: evidence.length > 0
        ? `Consistent with an under-arousal vigilance dip: ${evidence.join(', ')}.`
        : 'No sign of an under-arousal vigilance dip this session — performance metrics look normal despite low reported workload.',
    }
  }

  // optimal
  if (session.performance?.missionSuccess) evidence.push('mission success')
  if (has(accuracy) && accuracy >= 0.6) evidence.push(`solid accuracy (${(accuracy * 100).toFixed(0)}%)`)
  if (fastReaction) evidence.push('faster-than-expert reaction times')
  return {
    matched: evidence.length > 0,
    evidence,
    note: evidence.length > 0
      ? `Consistent with the predicted near-optimal zone: ${evidence.join(', ')}.`
      : 'Workload was in the predicted near-optimal band, though this session\'s outcome metrics don\'t clearly confirm strong performance.',
  }
}

/**
 * Pure function: given a session with `simTlx.derived`, `scenarioConfig`,
 * `performance` and `movementTrack`, returns per-composite contributing
 * factors plus a predicted performance zone checked against this session's
 * own outcome data.
 */
export function evaluateWorkloadReasoning(session) {
  const derived = session?.simTlx?.derived
  if (!derived) return null

  const perComposite = {}
  for (const key of Object.keys(REASONING_RULES)) {
    perComposite[key] = REASONING_RULES[key]
      .filter(rule => {
        try { return rule.test(session) } catch { return false }
      })
      .map(rule => ({ text: rule.factor(session), citationIds: rule.citationIds }))
  }

  const zoneInfo = ZONE_BY_WORKLOAD(derived.overallWorkload)
  const movementBench = evaluateMovementAgainstExperts(session)
  const predictionCheck = checkPrediction(
    zoneInfo.zone, session, movementBench.reactionOverall, movementBench.threatsResponded
  )

  return {
    perComposite,
    prediction: zoneInfo,
    predictionCheck,
  }
}
