'use client'
import { useState } from 'react'
import {
  LineChart, Line, XAxis, YAxis, Tooltip, ResponsiveContainer, ReferenceLine
} from 'recharts'
import styles from './HostageTab.module.css'
import { formatTime, stateColor, chartTheme } from '@/lib/utils'

// ── Research-grounded state model ───────────────────────────────────────────
// Each state's `pct` (distress-index contribution) and `justification` are grounded in
// the sound/emotion research review (see HOSTAGE_EMOTION_SOUND_RESEARCH.pdf). The scale
// is NOT just "how scary does this look" — it follows specific findings:
//
//   - Freeze (72%) is scored ABOVE Panic (66%). This is the one correction driven directly
//     by research: Freeze here models tonic immobility (a reflexive shutdown under
//     sustained/inescapable threat), and peritraumatic tonic immobility is clinically shown
//     to predict WORSE trauma outcomes than active panic — so it should not sit below Panic
//     on a severity scale, even though physiologically it looks "calmer" (bradycardia).
//   - Threatened (75%) > Panic (66%): a directed vocal threat is a sharper fear trigger than
//     equally loud gunfire (scream/threat "roughness" hits the amygdala more directly).
//   - `source` is a short citation label; hover any state (journey bar, legend, or the
//     Distress column) to see the full justification.
const STATE_INFO = {
  Calm: {
    pct: 0,
    desc: 'Composed — no immediate threat perceived.',
    justification: 'Baseline. Below the arousal threshold at which sound-driven stress responses begin.',
    source: 'WHO Environmental Noise Guidelines',
  },
  Fearful: {
    pct: 33,
    desc: 'Frightened by nearby danger — gunfire or an approaching captor.',
    justification: 'Fear-potentiated startle to a perceived but not-yet-imminent threat — audible danger the hostage cannot yet act on.',
    source: 'Fear-potentiated startle review, PMC6162305',
  },
  Scared: {
    pct: 33,
    desc: 'Frightened by nearby danger.',
    justification: 'Fear-potentiated startle to a perceived but not-yet-imminent threat.',
    source: 'Fear-potentiated startle review, PMC6162305',
  },
  Held: {
    pct: 40,
    desc: "Under a captor's direct control — held at gunpoint / used as a human shield.",
    justification: 'A captivity stressor, not a sound-driven one: agency and escape options are physically removed rather than self-suppressed.',
    source: 'Design classification (captor mechanic)',
  },
  Panic: {
    pct: 66,
    desc: 'Overwhelmed by extreme stress — actively fleeing.',
    justification: 'Active flight response to a confirmed, escalating threat (e.g. rapid/repeated gunfire). Repeated exposure does not reliably calm a person down — it can compound distress instead of fading.',
    source: 'Blast-exposed failure-to-habituate study, PMC6387566',
  },
  Freeze: {
    pct: 72,
    desc: 'Paralysed by fear, unable to move — tonic immobility.',
    justification: 'A reflexive shutdown under sustained or inescapable threat. Scored ABOVE Panic deliberately: peritraumatic tonic immobility predicts WORSE psychological outcomes than active panic, despite looking outwardly "calmer".',
    source: 'Tonic immobility predicts poorer PTSD recovery — ScienceDirect 2019 / Brazilian police officers study',
  },
  Threatened: {
    pct: 75,
    desc: 'The captor is threatening them directly — "get back or he dies".',
    justification: 'A directed vocal threat. Scream/threat "roughness" (fast loudness modulation) is judged more frightening than an equally loud gunshot — it is a sharper fear trigger than ambient danger.',
    source: 'Arnal et al. 2015, Current Biology',
  },
  Wounded: {
    pct: 95,
    desc: 'Shot and bleeding — near-total distress.',
    justification: 'Physical injury compounds fear with pain, crossing the injury threshold — near-maximal distress by any measure.',
    source: 'Firearms/blast SPL & injury threshold, PMC9450760',
  },
  Follow: {
    pct: 10,
    desc: 'Trusting the trainee and moving toward safety.',
    justification: 'Contact with a rescuer provides a visible escape route. Appraisal of an available escape sharply lowers arousal, even before the threat is fully gone.',
    source: 'Bracha (2004) — appraisal-driven de-escalation',
  },
  Freed: {
    pct: 0,
    desc: 'Safely extracted — the rescue objective is met.',
    justification: 'Threat resolved; returns to baseline.',
    source: '—',
  },
  Down: {
    pct: 100,
    desc: 'The hostage was killed.',
    justification: 'Terminal outcome — maximum distress by definition.',
    source: '—',
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

// ── Custom tooltip for the "Distress Index Over Time" chart ────────────────
// Shows, per hostage, not just the % at that moment but the research justification for
// why that state carries that distress weight.
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
            {info?.justification && <div className={styles.chartTipWhy}>{info.justification}</div>}
            {info?.source && <div className={styles.chartTipSource}>{info.source}</div>}
          </div>
        )
      })}
    </div>
  )
}

export default function HostageTab({ session }) {
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
      desc: info.desc,
      justification: info.justification,
      source: info.source,
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
              Distress column below for the research basis behind that score — including why{' '}
              <strong style={{ color: hColor('Freeze') }}>Freeze</strong> is now scored{' '}
              <em>above</em> Panic.
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

      {/* Floating research-justification tooltip, shared by the journey bar, legend, and table */}
      {hover && (
        <div className={styles.hoverTip} style={{ left: hover.x + 16, top: hover.y + 16 }}>
          <div className={styles.hoverTipTitle}>
            <span className={styles.chartTipDot} style={{ background: hover.color }} />
            {hover.title}
            {hover.pct != null ? <span className={styles.hoverTipPct}>{hover.pct}% distress</span> : null}
          </div>
          {hover.desc && <div className={styles.hoverTipDesc}>{hover.desc}</div>}
          {hover.justification && (
            <div className={styles.hoverTipWhy}><strong>Why this score:</strong> {hover.justification}</div>
          )}
          {hover.source && hover.source !== '—' && <div className={styles.hoverTipSource}>{hover.source}</div>}
        </div>
      )}
    </div>
  )
}
