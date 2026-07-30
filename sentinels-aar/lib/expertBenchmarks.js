// Real-world / research benchmark values for a trainee's key metrics, taken from
// `Expert Value Table.pdf`. The Summary tab uses these to show how a session compares
// to published expert and novice performance. See MODULE4_EVALUATION.md.
//
// Each benchmark says what "expert" and "novice/worst" look like for that metric, so
// we can place the trainee on a novice→expert scale with a cited source.

export const BENCHMARKS = [
  {
    key: 'reaction',
    label: 'Reaction time',
    higherIsBetter: false,
    expert: 0.30,   // Hick's Law: 1–2 choice reaction ≈ 0.26–0.33 s
    novice: 0.50,   // slower / many-choice / under stress
    source: "Hick's Law — choice reaction time",
    fmt: (v) => `${v.toFixed(2)} s`,
    value: (s, perf, cog) =>
      cog?.averageReactionTime ?? s.movementTrack?.reactionStats?.avgReactionTime ?? null,
  },
  // NOTE: shot accuracy is handled separately (distance-aware) — see expectedHitRate /
  // avgEngagementDistance below and the dedicated row in SummaryTab.
  {
    key: 'friendlyFire',
    label: 'Friendly fire',
    higherIsBetter: false,
    expert: 0,      // modern trained units <2% → ≈0 per mission
    novice: 1,      // any incident; historical ops ran 17–23% fratricide
    source: 'Fratricide trends since Desert Storm',
    fmt: (v) => `${v} incident${v === 1 ? '' : 's'}`,
    value: (s, perf) => perf?.friendlyFireCount ?? 0,
  },
]

// Where does `value` sit on the novice→expert scale (0 = novice, 1 = expert)?
export function benchmarkPos(value, b) {
  if (value == null || Number.isNaN(value)) return null
  let pct = b.higherIsBetter
    ? (value - b.novice) / (b.expert - b.novice)
    : (b.novice - value) / (b.novice - b.expert)
  pct = Math.max(0, Math.min(1, pct))
  const verdict = pct >= 0.9 ? 'Expert'
    : pct >= 0.65 ? 'Proficient'
    : pct >= 0.35 ? 'Developing'
    : 'Novice'
  const color = pct >= 0.9 ? '#34d399'
    : pct >= 0.65 ? '#a3e635'
    : pct >= 0.35 ? '#fbbf24'
    : '#f87171'
  return { pct, verdict, color }
}

// ── Distance-aware accuracy ──────────────────────────────────────────────────
// Real hit rates depend heavily on range. NYPD SOP-9 expected hit rate (real
// officers, under stress) by engagement distance:
export function expectedHitRate(distanceM) {
  if (distanceM == null) return null
  if (distanceM < 3)  return 0.50    // under 3 m: ~45–55%
  if (distanceM < 7)  return 0.125   // 3–7 m: ~10–15%
  if (distanceM < 15) return 0.06    // 7–15 m: ~4–8%
  if (distanceM < 25) return 0.045   // 15–25 m: ~3–6%
  return 0.03                        // 25 m+: ~2–4%
}
export function hitRateBandLabel(distanceM) {
  if (distanceM == null) return '—'
  if (distanceM < 3)  return 'under 3 m'
  if (distanceM < 7)  return '3–7 m'
  if (distanceM < 15) return '7–15 m'
  if (distanceM < 25) return '15–25 m'
  return '25 m+'
}

// Average engagement distance = mean of (trainee ↔ hit-terrorist) horizontal distance
// at each TerroristHit, taken from the replay positions. Returns { avg, samples } or null.
//
// Prefers the value Unity now computes directly per-shot (perf.avgEngagementDistance,
// exact — see PerformanceCalculator.cs) and falls back to reconstructing it from replay
// frames for sessions recorded before that field existed.
export function avgEngagementDistance(session) {
  const real = session.performance?.avgEngagementDistance
  if (real != null && real >= 0) {
    const hits = (session.events || []).filter(e => e.eventType === 'TerroristHit' && e.distance >= 0)
    return { avg: real, samples: hits.length || (session.performance?.hits ?? 1), exact: true }
  }

  const frames = session.replayFrames || []
  const hits = (session.events || []).filter(e => e.eventType === 'TerroristHit' && e.targetActorId)
  if (!hits.length || !frames.length) return null

  const byId = new Map()
  for (const f of frames) {
    if (!byId.has(f.actorId)) byId.set(f.actorId, [])
    byId.get(f.actorId).push(f)
  }
  for (const arr of byId.values()) arr.sort((a, b) => a.timestamp - b.timestamp)

  const posAt = (id, t) => {
    const arr = byId.get(id)
    if (!arr || !arr.length) return null
    let best = arr[0], bd = Math.abs(arr[0].timestamp - t)
    for (const f of arr) { const d = Math.abs(f.timestamp - t); if (d < bd) { bd = d; best = f } }
    return best.position
  }

  const traineeId = [...byId.keys()].find(k => (k || '').toLowerCase().startsWith('trainee'))
  if (!traineeId) return null

  const dists = []
  for (const h of hits) {
    const tp = posAt(traineeId, h.timestamp)
    const ep = posAt(h.targetActorId, h.timestamp)
    if (tp && ep && tp.x != null && ep.x != null) {
      dists.push(Math.hypot(tp.x - ep.x, tp.z - ep.z))
    }
  }
  if (!dists.length) return null
  return { avg: dists.reduce((a, b) => a + b, 0) / dists.length, samples: dists.length }
}

// ── Per-shot distance list (for the distance-band breakdown below) ──────────
// Returns every individual hit's distance as a plain array, e.g. [2.1, 6.4, 6.9, 11.0].
// Only HITS carry a recorded distance (a miss never touches NPCHitBox, so there is no
// position to measure) — this is a real, honestly-stated limit: the breakdown below shows
// WHERE a trainee's successful hits landed by range, not a true "hit rate per band" (that
// would need the range of every miss too, which isn't captured). Prefers the exact per-hit
// distance Unity now records (event.distance); falls back to reconstructing each hit's
// distance from replay-frame positions for sessions recorded before that field existed.
export function hitDistances(session) {
  const exact = (session.events || [])
    .filter(e => e.eventType === 'TerroristHit' && typeof e.distance === 'number' && e.distance >= 0)
    .map(e => e.distance)
  if (exact.length) return { distances: exact, exact: true }

  const frames = session.replayFrames || []
  const hits = (session.events || []).filter(e => e.eventType === 'TerroristHit' && e.targetActorId)
  if (!hits.length || !frames.length) return { distances: [], exact: false }

  const byId = new Map()
  for (const f of frames) {
    if (!byId.has(f.actorId)) byId.set(f.actorId, [])
    byId.get(f.actorId).push(f)
  }
  for (const arr of byId.values()) arr.sort((a, b) => a.timestamp - b.timestamp)

  const posAt = (id, t) => {
    const arr = byId.get(id)
    if (!arr || !arr.length) return null
    let best = arr[0], bd = Math.abs(arr[0].timestamp - t)
    for (const f of arr) { const d = Math.abs(f.timestamp - t); if (d < bd) { bd = d; best = f } }
    return best.position
  }

  const traineeId = [...byId.keys()].find(k => (k || '').toLowerCase().startsWith('trainee'))
  if (!traineeId) return { distances: [], exact: false }

  const distances = []
  for (const h of hits) {
    const tp = posAt(traineeId, h.timestamp)
    const ep = posAt(h.targetActorId, h.timestamp)
    if (tp && ep && tp.x != null && ep.x != null) distances.push(Math.hypot(tp.x - ep.x, tp.z - ep.z))
  }
  return { distances, exact: false }
}

// The 5 NYPD SOP-9 distance bands, in order, with their boundaries and expert/novice
// reference rates — used to bin hits and show WHERE a trainee's hits landed relative to
// how hard each range actually is.
export const DISTANCE_BANDS = [
  { label: 'Under 3 m',  min: 0,  max: 3,        expert: 0.50,  novice: 0.18 },
  { label: '3–7 m',      min: 3,  max: 7,        expert: 0.125, novice: 0.04 },
  { label: '7–15 m',     min: 7,  max: 15,       expert: 0.06,  novice: 0.02 },
  { label: '15–25 m',    min: 15, max: 25,       expert: 0.045, novice: 0.015 },
  { label: '25 m+',      min: 25, max: Infinity, expert: 0.03,  novice: 0.01 },
]

// Bins every recorded hit distance into the 5 bands above. Returns only the bands that
// actually have at least one hit, each with a count, % of this session's hits, and the
// expert/novice reference rate for that range (context, not a rate computed from this
// session — see hitDistances() for why a true per-band rate isn't derivable).
export function hitsByDistanceBand(session) {
  const { distances, exact } = hitDistances(session)
  if (!distances.length) return { bands: [], exact, totalHits: 0 }

  const counts = DISTANCE_BANDS.map(() => 0)
  for (const d of distances) {
    const i = DISTANCE_BANDS.findIndex(b => d >= b.min && d < b.max)
    if (i >= 0) counts[i]++
  }

  const bands = DISTANCE_BANDS
    .map((b, i) => ({ ...b, count: counts[i], pct: counts[i] / distances.length }))
    .filter(b => b.count > 0)

  return { bands, exact, totalHits: distances.length }
}

// Rescue-outcome context (binary per mission), from RAND hostage-rescue data.
export const RESCUE_CONTEXT = {
  source: 'RAND — hostage rescue outcomes',
  professional: '70–90% survive (tactical → negotiated)',
  mishandled: '<40% survive (no intervention)',
}
