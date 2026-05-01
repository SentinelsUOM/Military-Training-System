'use client'
import { useRef, useEffect, useState } from 'react'
import {
  AreaChart, Area, XAxis, YAxis, Tooltip, ResponsiveContainer
} from 'recharts'
import styles from './TimelineTab.module.css'
import { formatTime, categoryColor, chartTheme } from '@/lib/utils'

const CATEGORY_ORDER = ['Combat', 'Movement', 'Communication', 'Cognitive', 'System', 'Other']

export default function TimelineTab({ session }) {
  const events = session.events || []
  const duration = session.missionDuration || 1

  const svgRef = useRef(null)
  const [svgWidth, setSvgWidth] = useState(800)
  const [hovered, setHovered] = useState(null)

  useEffect(() => {
    if (!svgRef.current) return
    const ro = new ResizeObserver(entries => {
      const w = entries[0]?.contentRect.width
      if (w) setSvgWidth(w)
    })
    ro.observe(svgRef.current)
    return () => ro.disconnect()
  }, [])

  const PAD = 16
  const TIMELINE_W = svgWidth - PAD * 2
  const DOT_Y = 50
  const SVG_H = 100

  const dots = events.map(ev => ({
    ...ev,
    cx: PAD + (ev.timestamp / duration) * TIMELINE_W,
  }))

  const eventsPerMinute = []
  if (duration > 0) {
    const buckets = Math.ceil(duration / 10)
    for (let i = 0; i < buckets; i++) {
      const start = i * 10, end = start + 10
      eventsPerMinute.push({
        t: formatTime(start),
        count: events.filter(e => e.timestamp >= start && e.timestamp < end).length,
      })
    }
  }

  const [filter, setFilter] = useState('All')
  const categories = ['All', ...CATEGORY_ORDER.filter(c =>
    events.some(e => (e.category || 'Other') === c)
  )]
  const filteredEvents = filter === 'All'
    ? events
    : events.filter(e => (e.category || 'Other') === filter)

  return (
    <div className={styles.wrap}>
      <div className={styles.card}>
        <h3 className={styles.cardTitle}>Event Timeline</h3>
        <div ref={svgRef} className={styles.svgWrap}>
          <svg width="100%" height={SVG_H} viewBox={`0 0 ${svgWidth} ${SVG_H}`}>
            <line x1={PAD} y1={DOT_Y} x2={svgWidth - PAD} y2={DOT_Y} stroke="var(--border)" strokeWidth={1.5} />
            {[0, 0.25, 0.5, 0.75, 1].map(frac => {
              const x = PAD + frac * TIMELINE_W
              const t = frac * duration
              return (
                <g key={frac}>
                  <line x1={x} y1={DOT_Y - 6} x2={x} y2={DOT_Y + 6} stroke="var(--muted)" strokeWidth={1} />
                  <text x={x} y={DOT_Y + 20} textAnchor="middle" fill="var(--muted)" fontSize={10}>
                    {formatTime(t)}
                  </text>
                </g>
              )
            })}
            {dots.map((ev, i) => (
              <circle
                key={i}
                cx={ev.cx}
                cy={DOT_Y}
                r={hovered === i ? 7 : 5}
                fill={categoryColor(ev.category)}
                style={{ cursor: 'pointer', transition: 'r 0.1s' }}
                onMouseEnter={() => setHovered(i)}
                onMouseLeave={() => setHovered(null)}
              />
            ))}
          </svg>
          {hovered != null && (() => {
            const ev = dots[hovered]
            return (
              <div
                className={styles.tooltip}
                style={{ left: Math.min(ev.cx, svgWidth - 200), top: DOT_Y - 50 }}
              >
                <strong>{ev.eventType}</strong>
                <span>{formatTime(ev.timestamp)} · {ev.category || 'Other'}</span>
                {ev.sourceActorId && <span>Actor: {ev.sourceActorId}</span>}
                {ev.roomId && <span>Room: {ev.roomId}</span>}
              </div>
            )
          })()}
        </div>

        <div className={styles.legend}>
          {CATEGORY_ORDER.map(c => (
            <span key={c} className={styles.legendItem}>
              <span className={styles.dot} style={{ background: categoryColor(c) }} />
              {c}
            </span>
          ))}
        </div>
      </div>

      <div className={styles.card}>
        <h3 className={styles.cardTitle}>Activity Density (10s buckets)</h3>
        <ResponsiveContainer width="100%" height={160}>
          <AreaChart data={eventsPerMinute} margin={{ top: 4, right: 8, left: -24, bottom: 0 }}>
            <defs>
              <linearGradient id="areaGrad" x1="0" y1="0" x2="0" y2="1">
                <stop offset="5%"  stopColor={chartTheme.accent} stopOpacity={0.3} />
                <stop offset="95%" stopColor={chartTheme.accent} stopOpacity={0} />
              </linearGradient>
            </defs>
            <XAxis dataKey="t" tick={{ fill: chartTheme.label, fontSize: 10 }} />
            <YAxis tick={{ fill: chartTheme.label, fontSize: 10 }} allowDecimals={false} />
            <Tooltip
              contentStyle={{ background: chartTheme.tooltip, border: `1px solid ${chartTheme.grid}`, borderRadius: 6 }}
              labelStyle={{ color: chartTheme.label }}
            />
            <Area
              type="monotone"
              dataKey="count"
              stroke={chartTheme.accent}
              fill="url(#areaGrad)"
              strokeWidth={2}
            />
          </AreaChart>
        </ResponsiveContainer>
      </div>

      <div className={styles.card}>
        <div className={styles.tableHeader}>
          <h3 className={styles.cardTitle} style={{ margin: 0 }}>Event Log</h3>
          <div className={styles.filterRow}>
            {categories.map(c => (
              <button
                key={c}
                className={`${styles.filterBtn} ${filter === c ? styles.active : ''}`}
                style={filter === c ? { borderColor: categoryColor(c), color: categoryColor(c) } : {}}
                onClick={() => setFilter(c)}
              >
                {c}
              </button>
            ))}
          </div>
        </div>
        <div className={styles.tableWrap}>
          <table className={styles.table}>
            <thead>
              <tr>
                <th>Time</th>
                <th>Type</th>
                <th>Category</th>
                <th>Actor</th>
                <th>Target</th>
                <th>Room</th>
              </tr>
            </thead>
            <tbody>
              {filteredEvents.map((ev, i) => (
                <tr key={i}>
                  <td className={styles.mono}>{formatTime(ev.timestamp)}</td>
                  <td>{ev.eventType}</td>
                  <td>
                    <span className={styles.catBadge} style={{ color: categoryColor(ev.category), borderColor: categoryColor(ev.category) }}>
                      {ev.category || 'Other'}
                    </span>
                  </td>
                  <td className={styles.mono}>{ev.sourceActorId || '—'}</td>
                  <td className={styles.mono}>{ev.targetActorId || '—'}</td>
                  <td className={styles.mono}>{ev.roomId || '—'}</td>
                </tr>
              ))}
              {filteredEvents.length === 0 && (
                <tr><td colSpan={6} className={styles.empty}>No events for this filter.</td></tr>
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  )
}
