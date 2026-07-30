// Expert-benchmark comparison for Module 4 movement telemetry.
//
// There is no recorded "expert session" dataset (see
// MODULE4_MOVEMENT_VALIDATION_METHODOLOGY.md for the full literature review),
// so every reference range here is either a direct empirical figure (tier A),
// the closest literature proxy available (tier B), or explicitly absent
// because no quantified source was found (tier C — trainee value is shown
// with no fabricated comparison). Tiers are surfaced in the UI so a tier-C
// "unvalidated" card is never presented with the same confidence as a tier-A
// one. Ranges are intentionally coarse bands, not precision targets — see the
// methodology doc for the reasoning behind each one.

export const CITATIONS = {
  bohannon2011: {
    authors: 'Bohannon RW & Williams Andrews A',
    year: 2011,
    title: 'Normal walking speed: a descriptive meta-analysis',
    source: 'Physiotherapy, 97(3), 182–189',
    url: 'https://www.sciencedirect.com/science/article/abs/pii/S0031940611000307',
    finding: 'Meta-analysis of 41 studies, n=23,111: healthy adult walking speed averages ~1.2–1.4 m/s.',
  },
  cqbDoctrine: {
    authors: 'US Army / USMC doctrine (FM 3-06.11; MCWP 3-11.3)',
    year: null,
    title: 'Cover, concealment and movement-technique guidance for CQB',
    source: 'Combined Arms Operations in Urban Terrain field manuals',
    url: 'https://www.globalsecurity.org/military/library/policy/army/fm/21-75/Ch1.htm',
    finding: 'Movement should be deliberate and weapon-ready throughout close-quarters clearing; qualitative, not numerically quantified.',
  },
  scholarpediaSaccades: {
    authors: 'Scholarpedia (Human saccadic eye movements)',
    year: null,
    title: 'Human saccadic eye movements',
    source: 'Scholarpedia',
    url: 'http://www.scholarpedia.org/article/Human_saccadic_eye_movements',
    finding: 'Saccade latency 100–150ms; saccade velocity roughly 30–100 deg/s of visual angle.',
  },
  situationalAwarenessVE: {
    authors: 'PMC11086823 authors',
    year: 2024,
    title: 'Evidence of elevated situational awareness for active duty soldiers during navigation of a virtual environment',
    source: 'PMC11086823',
    url: 'https://pmc.ncbi.nlm.nih.gov/articles/PMC11086823/',
    finding: 'Active-duty soldiers showed significantly faster peak saccade velocity and greater saccade magnitude than civilians (p<0.05), and fixated on more gaze-validated targets (median 15 vs 14, p=0.032).',
  },
  forceScienceRT: {
    authors: 'Force Science Institute',
    year: null,
    title: 'Action vs. reaction: reaction-time breakdowns for threat cues',
    source: 'Force Science / Police1',
    url: 'https://www.police1.com/officer-shootings/articles/action-vs-reaction-the-shoot-first-fallacy-AxWyVoHO0jU9ByeJ/',
    finding: 'Average ~0.25s to react to a simple threat cue and begin to act; ~0.56s when the reaction requires a shoot/no-shoot discrimination.',
  },
  scirpRT: {
    authors: 'SCIRP',
    year: null,
    title: 'Comparison between Auditory and Visual Simple Reaction Times',
    source: 'Scientific Research Publishing',
    url: 'https://www.scirp.org/html/4-2400003_2689.htm',
    finding: 'Mean simple visual reaction time ~331ms; mean simple auditory reaction time ~284ms.',
  },
  nieuwenhuys2022: {
    authors: 'Nieuwenhuys A et al.',
    year: 2022,
    title: "Shoot or Don't Shoot? Tactical Gaze Control and Visual Attention Training Improves Police Cadets' Decision-Making Performance in Live-Fire Scenarios",
    source: 'Frontiers in Psychology, PMC8905363',
    url: 'https://www.ncbi.nlm.nih.gov/pmc/articles/PMC8905363/',
    finding: 'Trained-group shoot-scenario response time improved from 1083ms (pretest) to 820ms (posttest); control group 780ms to 743ms.',
  },
  drawFireStudy: {
    authors: 'ALERRT / Texas State University',
    year: null,
    title: "Officer draw-and-fire response time research",
    source: 'ALERRT / VirTra "Time Analysis of a Certified Peace Officer"',
    url: 'https://www.virtra.com/time-analysis-of-a-certified-peace-officers-blog/',
    finding: 'Average draw-and-fire response time ~1.43–1.8s across studies (range 0.93–2.4s).',
  },
  cqbCapability2024: {
    authors: 'PMC12785213 authors',
    year: 2024,
    title: 'Predicting closed quarters battle capability – Examining the influence of personality, attentional ability, 2D:4D-ratio and mindfulness on tactical performance',
    source: 'PMC12785213',
    url: 'https://pmc.ncbi.nlm.nih.gov/articles/PMC12785213/',
    finding: 'Expert raters scored CQB performance on tactical behavior, weapon handling, gaze behavior, response time and mistakes; special forces significantly outperformed non-specialized soldiers (Cohen\'s d −1.1 to −2.0), validating these dimensions as expert/novice discriminators.',
  },
}

const clampVerdict = (value, range) => {
  if (value == null || Number.isNaN(value)) return 'unvalidated'
  if (value < range.min) return 'below'
  if (value > range.max) return 'above'
  return 'within'
}

// Which side of the range is "favorable" once you're outside it — this
// determines severity, not the factual verdict. E.g. a reaction time faster
// than the expert band's lower bound is still a good outcome, not a warning;
// only being slower than the upper bound should read as "needs improvement."
//   'band'         — both directions outside range are a deviation (avgSpeed)
//   'higherBetter' — above range is fine/good, only below range is a deviation
//   'lowerBetter'  — below range is fine/good, only above range is a deviation
function severityFor(direction, verdict) {
  if (verdict === 'unvalidated') return 'unvalidated'
  if (verdict === 'within') return 'good'
  if (direction === 'higherBetter') return verdict === 'above' ? 'good' : 'watch'
  if (direction === 'lowerBetter') return verdict === 'below' ? 'good' : 'watch'
  return 'watch' // 'band': any deviation either way is worth a look
}

const rangeText = (range, unit) => `${range.min}–${range.max}${unit}`

// One entry per Movement-tab metric. `range` is null for tier-C metrics with
// no quantified literature — evaluate() will mark those 'unvalidated'.
export const BENCHMARKS = {
  avgSpeed: {
    label: 'Avg Speed',
    tier: 'B',
    unit: ' m/s',
    range: { min: 0.8, max: 1.4 },
    direction: 'band',
    citationIds: ['bohannon2011', 'cqbDoctrine'],
  },
  maxSpeed: {
    label: 'Peak Speed',
    tier: 'C',
    unit: ' m/s',
    range: null,
    direction: 'band',
    citationIds: ['cqbDoctrine'],
  },
  timeCrouched: {
    label: 'Time Crouched',
    tier: 'C',
    unit: '',
    range: null,
    direction: 'band',
    citationIds: ['cqbDoctrine'],
  },
  headScanning: {
    label: 'Head Scanning (peak deg/s)',
    tier: 'B',
    unit: '°/s',
    range: { min: 30, max: 100 },
    direction: 'higherBetter',
    citationIds: ['scholarpediaSaccades', 'situationalAwarenessVE'],
  },
  weaponHeldPct: {
    label: 'Weapon In Hand',
    tier: 'B',
    unit: '%',
    range: { min: 90, max: 100 },
    direction: 'higherBetter',
    citationIds: ['cqbDoctrine'],
  },
  handTravel: {
    label: 'Hand Travel',
    tier: 'C',
    unit: ' m',
    range: null,
    direction: 'band',
    citationIds: [],
  },
  reactionHead: {
    label: 'Head Reaction',
    tier: 'A',
    unit: ' s',
    range: { min: 0.25, max: 0.40 },
    direction: 'lowerBetter',
    citationIds: ['forceScienceRT', 'scirpRT'],
  },
  reactionHands: {
    label: 'Weapon-Raise Reaction',
    tier: 'A',
    unit: ' s',
    range: { min: 0.40, max: 0.80 },
    direction: 'lowerBetter',
    citationIds: ['nieuwenhuys2022'],
  },
  reactionMovement: {
    label: 'Move-to-Cover Reaction',
    tier: 'B',
    unit: ' s',
    range: { min: 0.50, max: 1.00 },
    direction: 'lowerBetter',
    citationIds: ['forceScienceRT', 'drawFireStudy'],
  },
  reactionTrigger: {
    label: 'Fire-Back Reaction',
    tier: 'A',
    unit: ' s',
    range: { min: 0.80, max: 1.80 },
    direction: 'lowerBetter',
    citationIds: ['drawFireStudy'],
  },
  reactionOverall: {
    label: 'Avg Reaction Time',
    tier: 'A',
    unit: ' s',
    range: { min: 0.40, max: 1.00 },
    direction: 'lowerBetter',
    citationIds: ['nieuwenhuys2022', 'forceScienceRT'],
  },
  threatsResponded: {
    label: 'Threats Responded',
    tier: 'B',
    unit: '%',
    range: { min: 90, max: 100 },
    direction: 'higherBetter',
    citationIds: ['situationalAwarenessVE', 'cqbCapability2024'],
  },
}

// Exported so callers that only have a raw aggregated value (e.g. a player's
// multi-session average, computed server-side) can get the same tiered verdict
// without needing a full session object — see evaluateMovementAgainstExperts()
// below, which is the per-session version of the same logic.
export function evalMetric(key, value) {
  const def = BENCHMARKS[key]
  const verdict = def.range ? clampVerdict(value, def.range) : 'unvalidated'
  return {
    key,
    label: def.label,
    tier: def.tier,
    unit: def.unit,
    traineeValue: value,
    range: def.range,
    rangeText: def.range ? rangeText(def.range, def.unit) : null,
    verdict,
    severity: severityFor(def.direction, verdict),
    citationIds: def.citationIds,
  }
}

/**
 * Compares one session's movementTrack telemetry against the literature
 * bands above. Pure function, computed at render time — mirrors the style of
 * deriveCognitiveScores() in lib/cognitiveDerived.js. Returns null fields
 * where the session has no movementTrack data.
 */
export function evaluateMovementAgainstExperts(session) {
  const track = session?.movementTrack
  const stats = track?.stats || {}
  const rx = track?.reactionStats || {}
  const duration = session?.performance?.missionDuration
    || (track?.samples?.length ? track.samples[track.samples.length - 1].t : 0)

  const weaponPct = duration > 0 ? ((stats.timeWeaponHeld || 0) / duration) * 100 : null
  const handTravel = (stats.leftHandDistance || 0) + (stats.rightHandDistance || 0)
  const respondedPct = rx.stimulusCount > 0 ? (rx.respondedCount / rx.stimulusCount) * 100 : null

  const channelReaction = (channel) => {
    const reactions = (track?.reactions || []).filter(r => r.channel === channel && r.reactionTime >= 0)
    if (!reactions.length) return null
    return reactions.reduce((sum, r) => sum + r.reactionTime, 0) / reactions.length
  }

  return {
    avgSpeed: evalMetric('avgSpeed', stats.avgSpeed ?? null),
    maxSpeed: evalMetric('maxSpeed', stats.maxSpeed ?? null),
    timeCrouched: evalMetric('timeCrouched', stats.timeCrouched ?? null),
    headScanning: evalMetric('headScanning', stats.peakAngSpeed ?? null),
    weaponHeldPct: evalMetric('weaponHeldPct', weaponPct),
    handTravel: evalMetric('handTravel', handTravel),
    reactionHead: evalMetric('reactionHead', channelReaction('head')),
    reactionHands: evalMetric('reactionHands', channelReaction('hands')),
    reactionMovement: evalMetric('reactionMovement', channelReaction('movement')),
    reactionTrigger: evalMetric('reactionTrigger', channelReaction('trigger')),
    reactionOverall: evalMetric('reactionOverall', rx.avgReactionTime ?? null),
    threatsResponded: evalMetric('threatsResponded', respondedPct),
  }
}
