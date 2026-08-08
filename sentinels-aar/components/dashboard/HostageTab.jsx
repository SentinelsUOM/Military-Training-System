'use client'
import { useState } from 'react'
import {
  LineChart, Line, XAxis, YAxis, Tooltip, ResponsiveContainer, ReferenceLine
} from 'recharts'
import styles from './HostageTab.module.css'
import { formatTime, stateColor } from '@/lib/utils'
import { useChartTheme } from '@/lib/useTheme'

// ── Research-ordered state model ────────────────────────────────────────────
//
// Each state carries three display fields, rendered together by <StateExplainer>:
//   • `desc`     — WHAT the state means in the mission, in plain language
//   • `whyRank`  — WHY it sits at this point on the scale, relative to the others
//   • `source`   — the basis for that RANKING (null where there isn't one; `unsourced: true`
//                  marks a state whose ranking is our judgement, not a paper's)
//   • `keyFinding: true` bolds the two counter-intuitive rankings worth an evaluator's
//                  attention — Freeze > Panic, and Threatened > Panic.
//
// IMPORTANT — what the citations below do and do not support:
//   • `whyRank` / `source` explain why a state RANKS where it does. That ordering IS
//     research-derived (see HOSTAGE_EMOTION_SOUND_RESEARCH.pdf).
//   • `pct` — the NUMBER — is the project's own calibration and is NOT a published value.
//     SUDS is a self-report scale (a person rates their own distress 0-100); no paper maps
//     a categorical state onto a number, so nothing says "Freeze = 72%". The Expert Value
//     Table called 0/10/33/50/66 "a reasonable compressed version" of SUDS — plausibility,
//     not derivation. Held/Threatened/Wounded/Down and the revised Freeze are entirely ours.
//
//   Do not let a source label sitting next to a percentage imply the paper produced that
//   percentage. See MODULE4_EVALUATION.md section 1.
//
// The scale is NOT just "how scary does this look" — the ordering follows specific findings:
//
//   - Freeze (72%) is scored ABOVE Panic (66%). This is the one correction driven directly
//     by research: Freeze here models tonic immobility (a reflexive shutdown under
//     sustained/inescapable threat), and peritraumatic tonic immobility is clinically shown
//     to predict WORSE trauma outcomes than active panic — so it should not sit below Panic
//     on a severity scale, even though physiologically it looks "calmer" (bradycardia).
//   - Threatened (75%) > Panic (66%): a directed vocal threat is a sharper fear trigger than
//     equally loud gunfire (scream/threat "roughness" hits the amygdala more directly).
//   - Hover any state (chart point, journey bar, legend, or the Distress column) to see the
//     same what/why/basis explainer — all four surfaces share <StateExplainer>, so the
//     wording can never drift between them.
const STATE_INFO = {
  Calm: {
    pct: 0,
    desc: 'No threat perceived.',
    whyRank: 'Floor of the scale — nothing to be distressed about.',
    source: null,
  },
  Fearful: {
    pct: 33,
    desc: 'Frightened by danger nearby — gunfire, or a captor approaching.',
    whyRank: 'Real fear, but the hostage can still think and act. Lower third of the scale.',
    source: 'Fear-potentiated startle research (PMC6162305)',
  },
  Scared: {
    pct: 33,
    desc: 'Frightened by danger nearby.',
    whyRank: 'Real fear, but the hostage can still think and act. Lower third of the scale.',
    source: 'Fear-potentiated startle research (PMC6162305)',
  },
  Held: {
    pct: 40,
    desc: "Under a captor's direct control — at gunpoint, or used as a shield.",
    whyRank: 'Above plain fear because escape is physically removed, not just frightening.',
    source: 'Project classification — no published source for this state',
    unsourced: true,
  },
  Panic: {
    pct: 66,
    desc: 'Overwhelmed — actively trying to flee.',
    whyRank: "Severe, active distress. Repeated gunfire doesn't calm people down; it compounds.",
    source: 'Failure-to-habituate startle study (PMC6387566)',
  },
  Freeze: {
    pct: 72,
    desc: 'Frozen — physically unable to move.',
    whyRank: 'Deliberately ranked ABOVE Panic. Freezing looks calmer from the outside, but under inescapable threat it predicts worse long-term psychological harm than actively panicking.',
    source: 'Tonic immobility & PTSD recovery (ScienceDirect 2019; Brazilian police officers study)',
    keyFinding: true,
  },
  Threatened: {
    pct: 75,
    desc: 'The captor is threatening them directly — "get back or he dies".',
    whyRank: 'Above Panic — a threat aimed at you by a person is a sharper fear trigger than general danger like gunfire.',
    source: 'Arnal et al. 2015, Current Biology (vocal "roughness")',
    keyFinding: true,
  },
  Wounded: {
    pct: 95,
    desc: 'Shot and bleeding.',
    whyRank: 'Near the top — pain, fear, and a real chance of dying at once.',
    source: 'Firearms/blast injury threshold (PMC9450760)',
  },
  Follow: {
    pct: 10,
    desc: 'Moving to safety with the trainee.',
    whyRank: "Just above calm — the danger isn't over, but having a rescuer and a visible way out sharply reduces fear.",
    source: 'Bracha (2004) — escape appraisal lowers arousal',
  },
  Freed: {
    pct: 0,
    desc: 'Safely extracted.',
    whyRank: 'Threat resolved; back to baseline.',
    source: null,
  },
  Down: {
    pct: 100,
    desc: 'The hostage was killed.',
    whyRank: 'Maximum by definition — the worst possible outcome.',
    source: null,
  },
}
const DISTRESS_MAP = Object.fromEntries(Object.entries(STATE_INFO).map(([k, v]) => [k, v.pct / 100]))

// A distinct colour per hostage state, roughly following the distress gradient:
// safe → green/cyan, rising stress → yellow → amber → orange → red. Kept local so it
// doesn't disturb stateColor(), which the NPC 3D replay relies on.
const STATE_COLOR = {
  Calm:       '#3fb950',  // green — composed
  Follow:     '#22d3ee',  // cyan — moving to safety
  Freed:      '#4ade80',  // bright green — safe
  Fearful:    '#facc15',  // yellow — frightened
  Scared:     '#facc15',
  Held:       '#f59e0b',  // amber — captive
  Panic:      '#f87171',  // red — panic
  Freeze:     '#818cf8',  // indigo — frozen (tonic immobility; distinct hue, ranked above Panic)
  Threatened: '#fb923c',  // orange — threatened at gunpoint
  Wounded:    '#dc2626',  // deep red — shot
  Down:       '#6b7280',  // grey — killed
}
const hColor = (s) => STATE_COLOR[s] || '#8b949e'

// Plain-English cause for each trigger event, so the "why" reads like a story.
const TRIGGER_INFO = {
  GunshotHeard:          'heard gunfire nearby',
  ShotFired:             'a shot was fired close by',
  StressSpike:           'repeated gunfire spiked their stress',
  TerroristEnteredRoom:  'a captor entered the room',
  HostageContactStarted: 'the trainee reached them',
  HostageFreed:          'the trainee freed them',
  HostageHit:            'was struck by a stray round',
  ScenarioReady:         'the mission began',
}
function triggerText(t) {
  if (!t) return 'their state changed'
  if (TRIGGER_INFO[t]) return TRIGGER_INFO[t]
  const clean = t.replace(/[^\x20-\x7E]/g, '').trim()   // strip any garbled non-ASCII
  return clean || 'a captor interaction'
}
// The distress-spike markers (Wounded / Threatened) are logged without a trigger event, so give
// them a self-explanatory cause instead of the generic "their state changed".
function whyText(entry) {
  if (entry.state === 'Wounded')    return 'was shot'
  if (entry.state === 'Threatened') return 'the captor threatened them — “get back!”'
  return triggerText(entry.triggerEvent)
}
const cap = (s) => s.charAt(0).toUpperCase() + s.slice(1)

// ── Shared "what / why / basis" body, used by BOTH hover surfaces ──────────
// Kept as one component so the chart tooltip and the floating tooltip (journey bar, legend,
// trigger table) can never drift apart — an evaluator sees identical wording wherever they
// hover. Structure is deliberate: WHAT the state means in the mission, then WHY it ranks
// where it does, then the source — which backs the RANKING, not the percentage.
function StateExplainer({ info, compact }) {
  if (!info) return null
  const labelStyle = {
    fontSize: 9.5, fontWeight: 700, letterSpacing: '.05em', textTransform: 'uppercase',
    opacity: 0.65, display: 'block', marginBottom: 1,
  }
  return (
    <>
      {info.desc && (
        <div className={styles.chartTipWhy} style={{ marginTop: compact ? 4 : 6 }}>
          <span style={labelStyle}>What it means</span>
          {info.desc}
        </div>
      )}
      {info.whyRank && (
        <div className={styles.chartTipWhy} style={{ marginTop: 6 }}>
          <span style={labelStyle}>Why it ranks here</span>
          {info.keyFinding
            ? <strong style={{ fontWeight: 600 }}>{info.whyRank}</strong>
            : info.whyRank}
        </div>
      )}
      {info.source && (
        <div className={styles.chartTipSource} style={{ marginTop: 6 }}>
          {info.unsourced ? '⚠ ' : 'Basis: '}{info.source}
        </div>
      )}
    </>
  )
}

// ── Custom tooltip for the "Distress Index Over Time" chart ────────────────
// Shows, per hostage, the % at that moment plus what the state means and why it ranks there.
function DistressChartTooltip({ active, payload, label }) {
  if (!active || !payload || payload.length === 0) return null
  return (
    <div className={styles.chartTip}>
      <div className={styles.chartTipTime}>{label}</div>
      {payload.map((p) => {
        const state = p.payload[`${p.dataKey}__state`]
        const info = STATE_INFO[state]
        return (
          <div key={p.dataKey} className={styles.chartTipRow}>
            <div className={styles.chartTipHeader}>
              <span className={styles.chartTipDot} style={{ background: hColor(state) }} />
              <span className={styles.chartTipId}>{p.dataKey}</span>
              <strong style={{ color: hColor(state) }}>{state}</strong>
              <span className={styles.chartTipPct}>{p.value}%</span>
            </div>
            <StateExplainer info={info} compact />
          </div>
        )
      })}
    </div>
  )
}

export default function HostageTab({ session }) {
  const chartTheme = useChartTheme()
  const history = session.hostageHistory || []
  const duration = session.performance?.missionDuration || 1

  // Shared floating tooltip state for the journey bar, legend, and trigger table — all three
  // hover surfaces reuse this so "why this score" reads identically everywhere.
  const [hover, setHover] = useState(null)
  const showHover = (e, state, title) => {
    const info = STATE_INFO[state]
    if (!info) return
    setHover({
      x: e.clientX, y: e.clientY,
      title: title || state,
      pct: info.pct,
      info,                     // full record — rendered by the shared StateExplainer
      color: hColor(state),
    })
  }
  const moveHover = (e) => setHover(h => (h ? { ...h, x: e.clientX, y: e.clientY } : h))
  const hideHover = () => setHover(null)
  const hoverProps = (state, title) => ({
    onMouseEnter: (e) => showHover(e, state, title),
    onMouseMove: moveHover,
    onMouseLeave: hideHover,
  })

  const byHostage = {}
  for (const entry of history) {
    const id = entry.hostageId || 'unknown'
    if (!byHostage[id]) byHostage[id] = []
    byHostage[id].push(entry)
  }
  for (const id of Object.keys(byHostage)) byHostage[id].sort((a, b) => a.timestamp - b.timestamp)

  const distressData = []
  if (history.length > 0) {
    const step = Math.max(1, Math.floor(duration / 60))
    for (let t = 0; t <= duration; t += step) {
      const point = { t: formatTime(t) }
      for (const id of Object.keys(byHostage)) {
        const entries = byHostage[id].filter(e => e.timestamp <= t)
        if (entries.length > 0) {
          const last = entries[entries.length - 1]
          // Prefer the dashboard's state model (so captivity/Held reads as elevated stress)
          // and fall back to the value Unity stored only for states we don't classify.
          const d = DISTRESS_MAP[last.state] != null ? DISTRESS_MAP[last.state] : (last.distressScore ?? 0)
          point[id] = parseFloat((d * 100).toFixed(1))
          point[`${id}__state`] = last.state
        } else {
          point[id] = 0
          point[`${id}__state`] = 'Calm'
        }
      }
      distressData.push(point)
    }
  }

  const hostageIds = Object.keys(byHostage)
  const COLORS = [chartTheme.accent, '#fbbf24', '#a78bfa', '#34d399']
  const infoStyle = { color: chartTheme.label, fontSize: 13, lineHeight: 1.5, margin: '2px 0 14px' }

  return (
    <div className={styles.wrap}>
      {hostageIds.length === 0 ? (
        <div className={styles.empty}>No hostage data recorded for this session.</div>
      ) : (
        <>
          <div className={styles.card}>
            <h3 className={styles.cardTitle}>Distress Index Over Time</h3>
            <p style={infoStyle}>
              This estimates the hostage's psychological distress, moment to moment, from their emotional
              state. It <strong>rises</strong> when something frightening happens — gunfire nearby, a captor
              threatening them — and <strong>falls</strong> when they are calmed, start following the trainee,
              or are freed. Each step in the line is a change of state. Hover any point, state pill, or the
              Distress column below for the research basis behind each state&apos;s <em>ranking</em> — including
              why <strong style={{ color: hColor('Freeze') }}>Freeze</strong> ranks{' '}
              <em>above</em> Panic. See the note under the chart for how to read the percentages.
            </p>
            <ResponsiveContainer width="100%" height={220}>
              <LineChart data={distressData} margin={{ top: 4, right: 8, left: -20, bottom: 0 }}>
                <XAxis dataKey="t" tick={{ fill: chartTheme.label, fontSize: 10 }} />
                <YAxis domain={[0, 100]} tick={{ fill: chartTheme.label, fontSize: 10 }} unit="%" />
                <Tooltip content={<DistressChartTooltip />} />
                <ReferenceLine y={66} stroke="#f87171" strokeDasharray="4 2" label={{ value: 'High distress', fill: '#f87171', fontSize: 10 }} />
                {hostageIds.map((id, i) => (
                  <Line key={id} type="stepAfter" dataKey={id} stroke={COLORS[i % COLORS.length]} strokeWidth={2} dot={false} name={id} />
                ))}
              </LineChart>
            </ResponsiveContainer>

            {/* Methodological note — placed BELOW the chart so it reads as a footnote to the
                figure rather than a disclaimer before it. Distinguishes what the research
                fixes (the ordering) from what the project chose (the numbers). */}
            <div style={{
              marginTop: 12, paddingTop: 10, borderTop: '1px solid var(--border)',
              fontSize: 11.5, lineHeight: 1.55, color: 'var(--muted)',
            }}>
              <span style={{
                fontSize: 10, fontWeight: 700, letterSpacing: '.06em', textTransform: 'uppercase',
                color: 'var(--muted)', display: 'block', marginBottom: 4,
              }}>
                How to read these percentages
              </span>
              The <strong>order</strong> of the states is research-derived, and the 0–100 scale follows the
              clinical <strong>SUDS</strong> (Subjective Units of Distress) convention. The{' '}
              <strong>specific percentages are assigned for readability</strong> — they space the states
              legibly on one axis so the curve can be read at a glance. They are a calibration, not measured
              values: SUDS is self-reported, so no study assigns a number to a state. Each cited source
              justifies <em>where a state sits relative to the others</em> (for example, why{' '}
              <strong style={{ color: hColor('Freeze') }}>Freeze</strong> ranks above{' '}
              <strong style={{ color: hColor('Panic') }}>Panic</strong>) — not the figure itself.
              <br />
              <span style={{ opacity: 0.8 }}>
                Planned validation: expert elicitation with CQB/clinical raters to replace the assigned
                values with elicited ones, and a sensitivity check confirming the distress <em>ranking</em>
                of a session is stable when the values are varied.
              </span>
            </div>
          </div>

          {hostageIds.map((id) => {
            const entries = byHostage[id]
            const segments = []
            for (let i = 0; i < entries.length; i++) {
              const start = entries[i].timestamp
              const end   = i + 1 < entries.length ? entries[i + 1].timestamp : duration
              segments.push({ state: entries[i].state, start, end, dur: end - start })
            }

            return (
              <div key={id} className={styles.card}>
                <h3 className={styles.cardTitle}>Hostage: {id}</h3>

                <div className={styles.journey}>
                  {segments.map((seg, i) => (
                    <div
                      key={i}
                      className={`${styles.segment} ${styles.hoverable}`}
                      aria-label={`${seg.state} — ${formatTime(seg.start)} to ${formatTime(seg.end)}`}
                      style={{ flex: seg.dur, background: hColor(seg.state), minWidth: 4 }}
                      {...hoverProps(seg.state, `${seg.state} · ${formatTime(seg.start)} → ${formatTime(seg.end)}`)}
                    >
                      {seg.dur > duration / 8 && <span className={styles.segLabel}>{seg.state}</span>}
                    </div>
                  ))}
                </div>

                {/* legend: what each state this hostage went through means — hover for research basis */}
                <div style={{ display: 'flex', flexWrap: 'wrap', gap: 10, margin: '14px 0 4px' }}>
                  {[...new Set(entries.map(e => e.state))].map(state => (
                    <div
                      key={state}
                      className={styles.hoverable}
                      style={{ display: 'flex', alignItems: 'flex-start', gap: 8, flex: '1 1 260px', minWidth: 240 }}
                      {...hoverProps(state)}
                    >
                      <span style={{ width: 12, height: 12, borderRadius: 3, background: hColor(state), marginTop: 3, flexShrink: 0 }} />
                      <span style={{ color: chartTheme.label, fontSize: 12.5, lineHeight: 1.4 }}>
                        <strong style={{ color: hColor(state) }}>{state}</strong>
                        {STATE_INFO[state] ? ` (${STATE_INFO[state].pct}% distress) — ${STATE_INFO[state].desc}` : ''}
                      </span>
                    </div>
                  ))}
                </div>

                {/* narrative: why the hostage entered each state, in order — Distress column
                    explains the *trigger*; hover it for why that score is *weighted* that way */}
                <div className={styles.triggerTable}>
                  <div className={styles.triggerTitle}>What happened, and why</div>
                  <table className={styles.table}>
                    <thead>
                      <tr><th>Time</th><th>State</th><th>Distress</th><th>Why</th></tr>
                    </thead>
                    <tbody>
                      {entries.map((e, i) => {
                        const pct = STATE_INFO[e.state] ? STATE_INFO[e.state].pct : (e.distressScore != null ? Math.round(e.distressScore * 100) : 0)
                        return (
                          <tr key={i}>
                            <td className={styles.mono}>{formatTime(e.timestamp)}</td>
                            <td><span style={{ color: hColor(e.state) }}>{e.state}</span></td>
                            <td className={`${styles.mono} ${styles.hoverable}`} {...hoverProps(e.state)}>{pct}%</td>
                            <td>{cap(whyText(e))} → became <strong>{e.state}</strong></td>
                          </tr>
                        )
                      })}
                    </tbody>
                  </table>
                </div>
              </div>
            )
          })}
        </>
      )}

      {/* Floating explainer, shared by the journey bar, legend, and trigger table. Renders the
          same what/why/basis body as the chart tooltip via StateExplainer, so the wording an
          evaluator reads is identical on every hover surface. */}
      {hover && (
        <div className={styles.hoverTip} style={{ left: hover.x + 16, top: hover.y + 16 }}>
          <div className={styles.hoverTipTitle}>
            <span className={styles.chartTipDot} style={{ background: hover.color }} />
            {hover.title}
            {hover.pct != null ? <span className={styles.hoverTipPct}>{hover.pct}% distress</span> : null}
          </div>
          <StateExplainer info={hover.info} />
        </div>
      )}
    </div>
  )
}
