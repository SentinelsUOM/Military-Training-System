'use client'
import Link from 'next/link'
import {
  BarChart, Bar, XAxis, YAxis, Tooltip, ResponsiveContainer, Cell, LabelList
} from 'recharts'
import styles from './SimTlxTab.module.css'
import { chartTheme } from '@/lib/utils'
import {
  SIM_TLX_DIMENSIONS, SIM_TLX_COMPOSITES, SIM_TLX_SCALE_MAX,
  SIM_TLX_COLORS, workloadColor
} from '@/lib/simTlx'

export default function SimTlxTab({ session }) {
  const simTlx = session.simTlx
  const ratings = simTlx?.ratings || {}
  const derived = simTlx?.derived || {}

  if (!simTlx?.completedAt) {
    return (
      <div className={styles.empty}>
        <h3 className={styles.emptyTitle}>No workload assessment recorded for this session</h3>
        <p className={styles.emptySub}>
          The post-mission workload assessment (based on the SIM-TLX instrument,
          Harris et&nbsp;al.&nbsp;2020) has not been completed yet.
        </p>
        <Link href={`/simtlx/${session.sessionId}`} className={styles.fillBtn}>
          Complete Assessment →
        </Link>
      </div>
    )
  }

  // Bar per subscale; dimensions belonging to one of the three tracked
  // composites carry that composite's color, remaining scales stay neutral.
  const dimColor = {}
  for (const c of SIM_TLX_COMPOSITES) for (const d of c.dims) dimColor[d] = c.color

  const barData = SIM_TLX_DIMENSIONS.map(d => ({
    name: d.label,
    value: ratings[d.key] ?? 0,
    fill: dimColor[d.key] || '#8b949e'
  }))

  return (
    <div className={styles.wrap}>
      <div className={styles.compositeGrid}>
        {SIM_TLX_COMPOSITES.map(c => (
          <div key={c.key} className={styles.compositeCard} style={{ borderTopColor: c.color }}>
            <div className={styles.compositeLabel}>{c.label}</div>
            <div className={styles.compositeValue} style={{ color: c.color }}>
              {derived[c.key] != null ? derived[c.key].toFixed(1) : '—'}
              <span className={styles.compositeUnit}>/ 100</span>
            </div>
            <div className={styles.compositeDesc}>{c.description}</div>
          </div>
        ))}
        <div className={styles.compositeCard} style={{ borderTopColor: SIM_TLX_COLORS.overall }}>
          <div className={styles.compositeLabel}>Overall Workload</div>
          <div className={styles.compositeValue} style={{ color: SIM_TLX_COLORS.overall }}>
            {derived.overallWorkload != null ? derived.overallWorkload.toFixed(1) : '—'}
            <span className={styles.compositeUnit}>/ 100</span>
          </div>
          <div className={styles.compositeDesc}>
            Mean of all nine workload subscales —{' '}
            <span style={{ color: workloadColor(derived.overallWorkload || 0), fontWeight: 600 }}>
              {derived.workloadLevel}
            </span>{' '}
            workload.
          </div>
        </div>
      </div>

      <div className={styles.card}>
        <div className={styles.cardHeader}>
          <h3 className={styles.cardTitle}>Subscale Ratings (0–{SIM_TLX_SCALE_MAX})</h3>
          <span className={styles.completedAt}>
            Completed {new Date(simTlx.completedAt).toLocaleString()}
          </span>
        </div>

        <div className={styles.legend}>
          {SIM_TLX_COMPOSITES.map(c => (
            <span key={c.key} className={styles.legendItem}>
              <span className={styles.legendSwatch} style={{ background: c.color }} />
              {c.short}
            </span>
          ))}
          <span className={styles.legendItem}>
            <span className={styles.legendSwatch} style={{ background: '#8b949e' }} />
            Other subscales
          </span>
        </div>

        <ResponsiveContainer width="100%" height={340}>
          <BarChart
            data={barData}
            layout="vertical"
            margin={{ top: 4, right: 40, left: 24, bottom: 4 }}
            barCategoryGap={6}
          >
            <XAxis
              type="number"
              domain={[0, SIM_TLX_SCALE_MAX]}
              tickCount={6}
              tick={{ fill: chartTheme.label, fontSize: 11 }}
              axisLine={{ stroke: chartTheme.grid }}
              tickLine={false}
            />
            <YAxis
              type="category"
              dataKey="name"
              width={130}
              tick={{ fill: chartTheme.label, fontSize: 12 }}
              axisLine={false}
              tickLine={false}
            />
            <Tooltip
              cursor={{ fill: 'rgba(139, 148, 158, 0.08)' }}
              contentStyle={{ background: chartTheme.tooltip, border: `1px solid ${chartTheme.grid}`, borderRadius: 6 }}
              labelStyle={{ color: chartTheme.label }}
              formatter={(v) => [`${v} / ${SIM_TLX_SCALE_MAX}`, 'Rating']}
            />
            <Bar dataKey="value" barSize={16} radius={[0, 4, 4, 0]}>
              {barData.map((d, i) => <Cell key={i} fill={d.fill} />)}
              <LabelList dataKey="value" position="right" style={{ fill: 'var(--text)', fontSize: 12 }} />
            </Bar>
          </BarChart>
        </ResponsiveContainer>
      </div>

      <div className={styles.card}>
        <h3 className={styles.cardTitle}>Raw Responses</h3>
        <div className={styles.tableWrap}>
          <table className={styles.table}>
            <thead>
              <tr>
                <th>Subscale</th>
                <th>Question</th>
                <th className={styles.num}>Rating</th>
              </tr>
            </thead>
            <tbody>
              {SIM_TLX_DIMENSIONS.map(d => (
                <tr key={d.key}>
                  <td className={styles.dimName}>{d.label}</td>
                  <td className={styles.dimQuestion}>{d.question}</td>
                  <td className={styles.num}>{ratings[d.key] ?? '—'} / {SIM_TLX_SCALE_MAX}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  )
}
