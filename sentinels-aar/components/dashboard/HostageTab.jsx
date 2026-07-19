'use client'
import {
  LineChart, Line, XAxis, YAxis, Tooltip, ResponsiveContainer, ReferenceLine
} from 'recharts'
import styles from './HostageTab.module.css'
import { formatTime, stateColor, chartTheme } from '@/lib/utils'

// What each hostage state means and the distress level it carries. `pct` is the
// distress-index contribution shown on the chart; `desc` explains the state to an evaluator.
const STATE_INFO = {
  Calm:       { pct: 0,   desc: 'Composed — no immediate threat perceived.' },
  Fearful:    { pct: 33,  desc: 'Frightened by nearby danger — gunfire or an approaching captor.' },
  Scared:     { pct: 33,  desc: 'Frightened by nearby danger.' },
  Held:       { pct: 40,  desc: 'Under a captor’s direct control — held at gunpoint / used as a human shield.' },
  Freeze:     { pct: 50,  desc: 'Paralysed by fear, unable to move.' },
  Threatened: { pct: 75,  desc: 'The captor is threatening them directly — “get back or he dies”.' },
  Panic:      { pct: 66,  desc: 'Overwhelmed by extreme stress.' },
  Wounded:    { pct: 95,  desc: 'Shot and bleeding — near-total panic.' },
  Follow:     { pct: 10,  desc: 'Trusting the trainee and moving toward safety.' },
  Freed:      { pct: 0,   desc: 'Safely extracted — the rescue objective is met.' },
  Down:       { pct: 100, desc: 'The hostage was killed.' },
}
const DISTRESS_MAP = Object.fromEntries(Object.entries(STATE_INFO).map(([k, v]) => [k, v.pct / 100]))

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

export default function HostageTab({ session }) {
  const history = session.hostageHistory || []
  const duration = session.performance?.missionDuration || 1

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
        } else {
          point[id] = 0
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
              This estimates the hostage’s psychological distress, moment to moment, from their emotional
              state. It <strong>rises</strong> when something frightening happens — gunfire nearby, a captor
              threatening them — and <strong>falls</strong> when they are calmed, start following the trainee,
              or are freed. Each step in the line is a change of state; the dashed line marks the panic threshold.
            </p>
            <ResponsiveContainer width="100%" height={220}>
              <LineChart data={distressData} margin={{ top: 4, right: 8, left: -20, bottom: 0 }}>
                <XAxis dataKey="t" tick={{ fill: chartTheme.label, fontSize: 10 }} />
                <YAxis domain={[0, 100]} tick={{ fill: chartTheme.label, fontSize: 10 }} unit="%" />
                <Tooltip
                  contentStyle={{ background: chartTheme.tooltip, border: `1px solid ${chartTheme.grid}`, borderRadius: 6 }}
                  labelStyle={{ color: chartTheme.label }}
                  formatter={(v, name) => [`${v}%`, name]}
                />
                <ReferenceLine y={66} stroke="#f87171" strokeDasharray="4 2" label={{ value: 'Panic', fill: '#f87171', fontSize: 10 }} />
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
                      className={styles.segment}
                      title={`${seg.state} — ${formatTime(seg.start)} → ${formatTime(seg.end)} (${STATE_INFO[seg.state]?.desc || ''})`}
                      style={{ flex: seg.dur, background: stateColor(seg.state), minWidth: 4 }}
                    >
                      {seg.dur > duration / 8 && <span className={styles.segLabel}>{seg.state}</span>}
                    </div>
                  ))}
                </div>

                {/* legend: what each state this hostage went through means */}
                <div style={{ display: 'flex', flexWrap: 'wrap', gap: 10, margin: '14px 0 4px' }}>
                  {[...new Set(entries.map(e => e.state))].map(state => (
                    <div key={state} style={{ display: 'flex', alignItems: 'flex-start', gap: 8, flex: '1 1 260px', minWidth: 240 }}>
                      <span style={{ width: 12, height: 12, borderRadius: 3, background: stateColor(state), marginTop: 3, flexShrink: 0 }} />
                      <span style={{ color: chartTheme.label, fontSize: 12.5, lineHeight: 1.4 }}>
                        <strong style={{ color: stateColor(state) }}>{state}</strong>
                        {STATE_INFO[state] ? ` (${STATE_INFO[state].pct}% distress) — ${STATE_INFO[state].desc}` : ''}
                      </span>
                    </div>
                  ))}
                </div>

                {/* narrative: why the hostage entered each state, in order */}
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
                            <td><span style={{ color: stateColor(e.state) }}>{e.state}</span></td>
                            <td className={styles.mono}>{pct}%</td>
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
    </div>
  )
}
