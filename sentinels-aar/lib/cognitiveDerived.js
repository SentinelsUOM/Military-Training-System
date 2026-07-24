// Fallback Stability/Attention/Movement-Initiation scores.
//
// The Unity side has a real feed for these — SessionLogger.LogCognitiveUpdate(),
// meant to be driven by "Module 3" — but nothing calls it during live gameplay
// (grep shows callers only in AARTestRunner and SessionLogger's own unused demo
// method). Every real uploaded session therefore has cognitiveSummary.stabilityScore
// and .attentionScore hard-set to 0, even though the mission clearly wasn't a
// zero-attention run.
//
// Real sessions DO carry movementTrack.stats (head/hand motion aggregates from
// CognitiveMovementRecorder) and movementTrack.reactionStats (stimulus→response
// data from ReactionTimeTracker) for every session — see lib/models/Session.js.
// This derives proxy 0-1 scores from that telemetry so the dashboard shows a
// meaningful estimate instead of a flat 0%, while still preferring genuine
// Module 3 values whenever they are actually present (source: 'module3').

const clamp01 = (v) => Math.max(0, Math.min(1, v))

export function deriveCognitiveScores(session) {
  const cog = session.cognitiveSummary || {}
  const stats = session.movementTrack?.stats || {}
  const rx = session.movementTrack?.reactionStats || {}

  // Module 3 has never wired up a real 0 value in practice — any nonzero score
  // here means the feed is actually connected, so trust it outright.
  const hasModule3Data =
    (cog.stabilityScore || 0) > 0 ||
    (cog.attentionScore || 0) > 0 ||
    (cog.movementInitiationScore || 0) > 0

  if (hasModule3Data) {
    return {
      movementInitiationScore: cog.movementInitiationScore ?? null,
      stabilityScore: cog.stabilityScore ?? null,
      attentionScore: cog.attentionScore ?? null,
      source: 'module3'
    }
  }

  const hasTelemetry = (session.movementTrack?.samples?.length || 0) > 0
  if (!hasTelemetry) {
    return { movementInitiationScore: null, stabilityScore: null, attentionScore: null, source: 'unavailable' }
  }

  // Stability: steadier head aim -> higher score. Weighted mostly on average
  // yaw angular speed (deg/s), with a smaller penalty for the peak spike so one
  // fast snap-turn doesn't by itself tank an otherwise steady run.
  // 120 deg/s avg and 500 deg/s peak are treated as "fully unstable" ceilings.
  const avgAngSpeed = stats.avgAngSpeed ?? 0
  const peakAngSpeed = stats.peakAngSpeed ?? 0
  const stabilityScore = clamp01(1 - (0.7 * (avgAngSpeed / 120) + 0.3 * (peakAngSpeed / 500)))

  // Attention: blends stimulus response rate with reaction speed. Falls back to
  // reaction speed alone if no threat stimuli were recorded this session.
  // 2.5s average reaction time is treated as the "fully inattentive" ceiling.
  const avgReactionTime = rx.avgReactionTime ?? cog.averageReactionTime ?? 0
  const reactionFactor = clamp01(1 - avgReactionTime / 2.5)
  const stimulusCount = rx.stimulusCount ?? 0
  const attentionScore = stimulusCount > 0
    ? clamp01(0.6 * ((rx.respondedCount ?? 0) / stimulusCount) + 0.4 * reactionFactor)
    : reactionFactor

  // Movement initiation: share of the session spent actively moving rather than
  // frozen in place — a proxy for decisiveness under pressure.
  const timeMoving = stats.timeMoving ?? 0
  const timeStill = stats.timeStill ?? 0
  const totalTime = timeMoving + timeStill
  const movementInitiationScore = totalTime > 0 ? clamp01(timeMoving / totalTime) : null

  return { movementInitiationScore, stabilityScore, attentionScore, source: 'derived' }
}
