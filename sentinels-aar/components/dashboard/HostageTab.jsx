'use client'
import {
  LineChart, Line, XAxis, YAxis, Tooltip, ResponsiveContainer, ReferenceLine
} from 'recharts'
import styles from './HostageTab.module.css'
import { formatTime, stateColor, chartTheme } from '@/lib/utils'

const DISTRESS_MAP = {
  Calm: 0, Fearful: 0.33, Panic: 0.66, Freeze: 0.5, Follow: 0.1
}

export default function HostageTab({ session }) {
  const history = session.hostageHistory || []
  const duration = session.missionDuration || 1

  const byHostage = {}
  for (const entry of history) {
    const id = entry.hostageId || 'unknown'
    if (!byHostage[id]) byHostage[id] = []
    byHostage[id].push(entry)
  }

  const distressData = []
  if (history.length > 0) {
    const step = Math.max(1, Math.floor(duration / 60))
    for (let t = 0; t <= duration; t += step) {
      const point = { t: formatTime(t) }
      for (const id of Object.keys(byHostage)) {
        const entries = byHostage[id].filter(e => e.timestamp <= t)
        if (entries.length > 0) {
          const last = entries[entries.length - 1]
          point[id] = parseFloat(((DISTRESS_MAP[last.state] ?? 0) * 100).toFixed(1))
        } else {
          point[id] = 0
        }
      }
      distressData.push(point)
    }
  }

  const hostageIds = Object.keys(byHostage)
  const COLORS = [chartTheme.accent, '#fbbf24', '#a78bfa', '#34d399']

  return (
    <div className={styles.wrap}>
      {hostageIds.length === 0 ? (
        <div className={styles.empty}>No hostage data recorded for this session.</div>
      ) : (
        <>
          <div className={styles.card}>
            <h3 className={styles.cardTitle}>Distress Index Over Time</h3>
            <ResponsiveContainer width="100%" height={220}>
              <LineChart data={distressData} margin={{ top: 4, right: 8, left: -20, bottom: 0 }}>
                <XAxis dataKey="t" tick={{ fill: chartTheme.label, fontSize: 10 }} />
                <YAxis domain={[0, 70]} tick={{ fill: chartTheme.label, fontSize: 10 }} unit="%" />
                <Tooltip
                  contentStyle={{ background: chartTheme.tooltip, border: `1px solid ${chartTheme.grid}`, borderRadius: 6 }}
                  labelStyle={{ color: chartTheme.label }}
                  formatter={(v, name) => [`${v}%`, name]}
                />
                <ReferenceLine y={66} stroke="#f87171" strokeDasharray="4 2" label={{ value: 'Panic', fill: '#f87171', fontSize: 10 }} />
                {hostageIds.map((id, i) => (
                  <Line
                    key={id}
                    type="stepAfter"
                    dataKey={id}
                    stroke={COLORS[i % COLORS.length]}
                    strokeWidth={2}
                    dot={false}
                    name={id}
                  />
                ))}
              </LineChart>
            </ResponsiveContainer>
          </div>

          {hostageIds.map((id, hi) => {
            const entries = byHostage[id]
            const segments = []
            for (let i = 0; i < entries.length; i++) {
              const start = entries[i].timestamp
              const end   = i + 1 < entries.length ? entries[i + 1].timestamp : duration
              segments.push({ state: entries[i].state, start, end, dur: end - start })
            }
            const triggers = entries.filter(e => e.trigger)

            return (
              <div key={id} className={styles.card}>
                <h3 className={styles.cardTitle}>Hostage: {id}</h3>

                <div className={styles.journey}>
                  {segments.map((seg, i) => (
                    <div
                      key={i}
                      className={styles.segment}
                      title={`${seg.state} — ${formatTime(seg.start)} → ${formatTime(seg.end)}`}
                      style={{
                        flex: seg.dur,
                        background: stateColor(seg.state),
                        minWidth: 4,
                      }}
                    >
                      {seg.dur > duration / 8 && (
                        <span className={styles.segLabel}>{seg.state}</span>
                      )}
                    </div>
                  ))}
                </div>

                <div className={styles.statePills}>
                  {[...new Set(entries.map(e => e.state))].map(state => (
                    <span
                      key={state}
                      className={styles.pill}
                      style={{ background: `${stateColor(state)}22`, color: stateColor(state), borderColor: stateColor(state) }}
                    >
                      {state}
                    </span>
                  ))}
                </div>

                {triggers.length > 0 && (
                  <div className={styles.triggerTable}>
                    <div className={styles.triggerTitle}>State Triggers</div>
                    <table className={styles.table}>
                      <thead>
                        <tr>
                          <th>Time</th>
                          <th>New State</th>
                          <th>Trigger</th>
                        </tr>
                      </thead>
                      <tbody>
                        {entries.filter(e => e.trigger).map((e, i) => (
                          <tr key={i}>
                            <td className={styles.mono}>{formatTime(e.timestamp)}</td>
                            <td>
                              <span style={{ color: stateColor(e.state) }}>{e.state}</span>
                            </td>
                            <td>{e.trigger}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}
              </div>
            )
          })}
        </>
      )}
    </div>
  )
}
