// Sub-parameter breakdowns for the three "outcome" scores — Hostage Safety, Accuracy,
// and Speed — mirroring how Operator Safety is decomposed into survivability / exposure /
// discipline / response. Each score is split into research-grounded components so the AAR
// shows *why* the trainee got the number, then compared to a published expert benchmark.
//
// All of this is computed CLIENT-SIDE from data every session already carries (performance,
// events, hostageHistory, movementTrack) — no Unity change, works on historical sessions.
// Sources are the Expert Value Table (Expert Value Table.pdf) — see MODULE4_EVALUATION.md.

import { expectedHitRate, hitRateBandLabel, avgEngagementDistance } from './expertBenchmarks'

const clamp01 = (v) => Math.max(0, Math.min(1, v))
const pctFmt = (v) => `${Math.round(v * 100)}%`

// Distress per hostage state (matches HostageTab's model so captivity reads as elevated
// stress). Grounded in Piece B of the table — SUDS / polyvagal escalation.
const DISTRESS = {
  Calm: 0, Follow: 0.10, Freed: 0, Fearful: 0.33, Scared: 0.33, Held: 0.40,
  Freeze: 0.50, Panic: 0.66, Threatened: 0.75, Wounded: 0.95, Down: 1.0,
}
function peakDistress(history) {
  if (!history || !history.length) return null
  let peak = 0
  for (const e of history) {
    const d = DISTRESS[e.state] != null ? DISTRESS[e.state] : (e.distressScore ?? 0)
    if (d > peak) peak = d
  }
  return peak
}

// ── Hostage Safety ───────────────────────────────────────────────────────────
// Piece A (outcome), Piece B (well-being / distress), Piece C (friendly fire).
export function hostageBreakdown(session) {
  const perf = session.performance || {}
  const history = session.hostageHistory || []

  const saved = perf.hostagesSaved ?? 0
  const total = Math.max(perf.hostagesTotal ?? 1, 1)
  const outcome = clamp01(saved / total)

  const peak = peakDistress(history)
  const wellbeing = peak == null ? null : clamp01(1 - peak)

  const ff = perf.friendlyFireCount ?? 0
  const weaponSafety = clamp01(Math.pow(0.61, ff))

  return {
    overall: perf.safetyScore ?? null,
    overallLabel: 'Hostage Safety (overall)',
    subtitle:
      'Was the hostage protected? Three research dimensions: did they survive the rescue, ' +
      'how much distress did they endure, and did you avoid putting rounds near them.',
    subs: [
      { label: 'Outcome — hostage rescued', value: outcome,
        hint: `${saved} of ${total} extracted` },
      { label: 'Well-being — kept calm', value: wellbeing,
        hint: peak == null ? 'no distress data' : `peak distress ${pctFmt(peak)}` },
      { label: 'Weapon safety — no friendly fire', value: weaponSafety,
        hint: ff === 0 ? 'clean' : `${ff} incident${ff === 1 ? '' : 's'}` },
    ],
    expert: {
      label: 'hostage survival rate',
      value: outcome,
      valueText: pctFmt(outcome),
      b: { higherIsBetter: true, expert: 0.85, novice: 0.40, fmt: pctFmt,
        source: 'RAND hostage-rescue outcomes' },
      note: 'Professional tactical rescues save ~70–90% of hostages; below ~40% mirrors a ' +
        'mishandled or abandoned incident.',
    },
  }
}

// ── Accuracy ─────────────────────────────────────────────────────────────────
// Raw hit rate, how that stacks up against a realistic hit rate at the actual engagement
// range, and how economically the trainee spent rounds (rounds-per-kill / "hit factor").
export function accuracyBreakdown(session) {
  const perf = session.performance || {}
  const events = session.events || []
  const shots = perf.totalShots ?? 0
  const hits = perf.hits ?? 0
  const terroristsDown = events.filter(e => e.eventType === 'TerroristDown').length

  if (shots === 0) {
    return {
      overall: perf.accuracyScore ?? 0,
      overallLabel: 'Accuracy (overall)',
      subtitle: 'How well you shot — hit rate, realism-at-range, and round economy.',
      subs: [],
      emptyNote: 'No rounds were fired this session, so there is nothing to score for accuracy.',
      expert: null,
    }
  }

  const hitRate = clamp01(hits / shots)

  // Distance-adjusted: 1.0 == you matched the realistic hit rate for that range.
  const eng = avgEngagementDistance(session)
  const expected = eng ? expectedHitRate(eng.avg) : 0.50
  const distanceAdj = clamp01(hitRate / expected)

  // Shot efficiency: ~3 rounds to neutralise a target = economical (score 1.0).
  const IDEAL_RPK = 3
  const efficiency = terroristsDown > 0
    ? clamp01((terroristsDown * IDEAL_RPK) / shots)
    : 0
  const rpk = terroristsDown > 0 ? (shots / terroristsDown) : null

  return {
    overall: perf.accuracyScore ?? hitRate,
    overallLabel: 'Accuracy (overall)',
    subtitle:
      'How well you shot. Raw hit rate, how that compares to what real officers achieve at ' +
      'your engagement range, and how economically you spent your rounds.',
    subs: [
      { label: 'Hit rate — rounds on target', value: hitRate,
        hint: `${hits}/${shots} hit` },
      { label: 'Realism at range — vs real hit rate', value: distanceAdj,
        hint: eng ? `~${eng.avg.toFixed(1)} m (${hitRateBandLabel(eng.avg)})` : 'close-range band' },
      { label: 'Shot economy — rounds per kill', value: efficiency,
        hint: rpk == null ? 'no kills' : `${rpk.toFixed(1)} rounds/kill` },
    ],
    expert: {
      label: 'shot accuracy at your engagement range',
      value: hitRate,
      valueText: pctFmt(hitRate),
      b: {
        higherIsBetter: true,
        expert: expected,
        novice: Math.max(0.02, expected * 0.35),
        fmt: pctFmt,
        source: eng ? `NYPD SOP-9 @ ${hitRateBandLabel(eng.avg)}` : 'NYPD SOP-9 (close-range)',
      },
      note: eng
        ? `${eng.exact ? 'Measured' : 'Estimated from replay'}: avg range ≈ ${eng.avg.toFixed(1)} m over ${eng.samples} hit${eng.samples === 1 ? '' : 's'}. Real officers hit ~${pctFmt(expected)} at this range under stress — so a "low" raw number can still be expert-level.`
        : 'No hit-distance data this session — judged against the close-range band.',
    },
  }
}

// ── Speed ────────────────────────────────────────────────────────────────────
// Overall completion pace, how fast the hostage was reached, and how decisively the trainee
// moved (less hesitation). There is NO published expert time for a hostage rescue, so this
// box shows components only — no expert-benchmark row (per the project's earlier decision).
export function speedBreakdown(session) {
  const perf = session.performance || {}
  const events = session.events || []
  const stats = session.movementTrack?.stats || {}
  const duration = perf.missionDuration ?? 0

  // Target time is scenario-normalized (rooms/terrorists/AI-tier aware) when Unity captured
  // that metadata, else falls back to the flat 300s cap for older sessions. Either way,
  // pace = 1 - duration/(2*target) — reaching the target exactly scores 0.5.
  const hasScenarioTarget = perf.speedTargetTime > 0 && (perf.speedRoomCount > 0 || perf.speedTerroristCount > 0)
  const targetTime = hasScenarioTarget ? perf.speedTargetTime : 300
  const pace = perf.speedScore ?? (duration > 0 ? clamp01(1 - duration / (2 * targetTime)) : null)

  // Time to objective — first moment the trainee reached the hostage.
  const contact = events.find(e => e.eventType === 'HostageContactStarted')
  const contactT = contact ? contact.timestamp : null
  // Reaching the hostage within ~120 s = full marks; 300 s+ = zero.
  const timeToObjective = contactT == null ? null : clamp01(1 - Math.max(0, contactT - 120) / 180)

  // Decisiveness — share of time actively moving vs standing still (hesitation).
  const timeMoving = stats.timeMoving ?? 0
  const timeStill = stats.timeStill ?? 0
  const totalMove = timeMoving + timeStill
  const decisiveness = totalMove > 0 ? clamp01(timeMoving / totalMove) : null

  return {
    overall: pace,
    overallLabel: 'Speed (overall)',
    subtitle:
      'How quickly you worked. Overall completion pace, how fast you reached the hostage, and ' +
      'how decisively you moved. Note: there is no published expert time for a hostage rescue ' +
      '(mission-time coefficients are design defaults, not research-sourced), so Speed is shown ' +
      'without an expert benchmark.',
    subs: [
      { label: 'Completion pace — total mission time', value: pace,
        hint: duration
          ? hasScenarioTarget
            ? `${Math.round(duration)} s vs ${Math.round(targetTime)} s target (${perf.speedRoomCount} rooms, ${perf.speedTerroristCount} terrorists)`
            : `${Math.round(duration)} s (300 s flat target — no scenario data)`
          : '—' },
      { label: 'Time to objective — reaching the hostage', value: timeToObjective,
        hint: contactT == null ? 'never reached' : `${Math.round(contactT)} s in` },
      { label: 'Decisiveness — active movement', value: decisiveness,
        hint: totalMove > 0 ? `${Math.round((timeMoving / totalMove) * 100)}% moving` : 'no motion data' },
    ],
    expert: null,
    expertNote: 'No research benchmark exists for hostage-rescue mission time, so Speed is scored on its own scale.',
  }
}
