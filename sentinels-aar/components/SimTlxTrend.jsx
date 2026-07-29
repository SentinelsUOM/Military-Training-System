'use client'
import {
  LineChart, Line, XAxis, YAxis, Tooltip, Legend, ResponsiveContainer, CartesianGrid
} from 'recharts'
import styles from './SimTlxTrend.module.css'
import { useChartTheme } from '@/lib/useTheme'
import { SIM_TLX_COMPOSITES, SIM_TLX_COLORS } from '@/lib/simTlx'

// Workload trend across the sessions currently loaded on the overview page.
// One line per tracked SIM-TLX composite plus the overall workload index.
export default function SimTlxTrend({ sessions }) {
  const chartTheme = useChartTheme()
  const rated = (sessions || [])
    .filter(s => s.simTlx?.derived?.overallWorkload != null)
    // sessions arrive newest-first; the time axis reads left → right
    .slice()
    .reverse()

  const data = rated.map(s => ({
    session: s.sessionId,
    date: s.timestamp || s.createdAt,
    mentalPhysical:      s.simTlx.derived.mentalPhysical,
    temporalFrustration: s.simTlx.derived.temporalFrustration,
    complexityStress:    s.simTlx.derived.complexityStress,
    overallWorkload:     s.simTlx.derived.overallWorkload
  }))

  return (
    <section className={styles.card}>
      <div className={styles.header}>
        <h2 className={styles.title}>Workload Trend</h2>
        <span className={styles.sub}>
          Post-mission subjective workload (0–100) · {rated.length} rated session{rated.length === 1 ? '' : 's'}
        </span>
      </div>

      {data.length === 0 ? (
        <p className={styles.empty}>
          No workload assessments recorded yet. The assessment opens automatically
          on this dashboard when a mission ends.
        </p>
      ) : (
        <ResponsiveContainer width="100%" height={280}>
          <LineChart data={data} margin={{ top: 8, right: 16, left: -16, bottom: 0 }}>
            <CartesianGrid stroke={chartTheme.grid} strokeDasharray="3 3" vertical={false} />
            <XAxis
              dataKey="session"
              tick={{ fill: chartTheme.label, fontSize: 10 }}
              axisLine={{ stroke: chartTheme.grid }}
              tickLine={false}
              interval="preserveStartEnd"
            />
            <YAxis
              domain={[0, 100]}
              tick={{ fill: chartTheme.label, fontSize: 11 }}
              axisLine={false}
              tickLine={false}
            />
            <Tooltip
              contentStyle={{ background: chartTheme.tooltip, border: `1px solid ${chartTheme.grid}`, borderRadius: 6, fontSize: 12 }}
              labelStyle={{ color: chartTheme.label }}
              formatter={(value, name) => [Number(value).toFixed(1), name]}
            />
            <Legend
              wrapperStyle={{ fontSize: 12, color: chartTheme.label }}
              iconType="plainline"
            />
            {SIM_TLX_COMPOSITES.map(c => (
              <Line
                key={c.key}
                type="monotone"
                dataKey={c.key}
                name={c.label}
                stroke={c.color}
                strokeWidth={2}
                dot={{ r: 3, fill: c.color, strokeWidth: 0 }}
                activeDot={{ r: 5, stroke: 'var(--surface)', strokeWidth: 2 }}
                isAnimationActive={false}
              />
            ))}
            <Line
              type="monotone"
              dataKey="overallWorkload"
              name="Overall Workload"
              stroke={SIM_TLX_COLORS.overall}
              strokeWidth={2}
              strokeDasharray="6 3"
              dot={{ r: 3, fill: SIM_TLX_COLORS.overall, strokeWidth: 0 }}
              activeDot={{ r: 5, stroke: 'var(--surface)', strokeWidth: 2 }}
              isAnimationActive={false}
            />
          </LineChart>
        </ResponsiveContainer>
      )}
    </section>
  )
}
