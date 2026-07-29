'use client'
import Link from 'next/link'
import {
  BarChart, Bar, XAxis, YAxis, Tooltip, ResponsiveContainer, Cell, LabelList
} from 'recharts'
import styles from './SimTlxTab.module.css'
import { useChartTheme } from '@/lib/useTheme'
import {
  SIM_TLX_DIMENSIONS, SIM_TLX_COMPOSITES, SIM_TLX_SCALE_MAX,
  SIM_TLX_COLORS, workloadColor
} from '@/lib/simTlx'
import { evaluateWorkloadReasoning, CITATIONS } from '@/lib/workloadReasoning'

export default function SimTlxTab({ session }) {
  const chartTheme = useChartTheme()
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

  const reasoning = evaluateWorkloadReasoning(session)

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

      {reasoning && <WorkloadReasoningCard reasoning={reasoning} />}
      {reasoning && <PredictionCard reasoning={reasoning} />}

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

      {reasoning && <WorkloadSources reasoning={reasoning} />}
    </div>
  )
}

const COMPOSITE_META = Object.fromEntries(SIM_TLX_COMPOSITES.map(c => [c.key, c]))

const ZONE_SEVERITY_CLASS = {
  'under-aroused': 'benchmarkOff',
  'optimal': 'benchmarkWithin',
  'overload-risk': 'benchmarkOff',
}

/** Session-specific "why" factors per composite, from lib/workloadReasoning.js. */
function WorkloadReasoningCard({ reasoning }) {
  const entries = Object.entries(reasoning.perComposite)
  if (!entries.some(([, factors]) => factors.length > 0)) return null

  return (
    <div className={styles.card}>
      <h3 className={styles.cardTitle}>Why This Session&apos;s Workload</h3>
      <div className={styles.reasonGrid}>
        {entries.map(([key, factors]) => {
          if (!factors.length) return null
          const meta = COMPOSITE_META[key]
          return (
            <div key={key} className={styles.reasonGroup}>
              <div className={styles.reasonGroupLabel} style={{ color: meta.color }}>{meta.label}</div>
              <ul className={styles.reasonList}>
                {factors.map((f, i) => (
                  <li key={i} className={styles.reasonItem}>
                    {f.text}
                    {f.citationIds.length > 0 && (
                      <span className={styles.reasonCite}>
                        {' '}({f.citationIds.map(id => `${CITATIONS[id].authors.split(/[, ]/)[0]} ${CITATIONS[id].year}`).join('; ')})
                      </span>
                    )}
                  </li>
                ))}
              </ul>
            </div>
          )
        })}
      </div>
    </div>
  )
}

/** Predicted performance zone from overall workload, checked against this session's own outcome. */
function PredictionCard({ reasoning }) {
  const { prediction, predictionCheck } = reasoning
  return (
    <div className={styles.card}>
      <h3 className={styles.cardTitle}>Predicted Performance Zone</h3>
      <div className={`${styles.predictionZone} ${styles[ZONE_SEVERITY_CLASS[prediction.zone]]}`}>
        {prediction.label}
      </div>
      <p className={styles.predictionRationale}>{prediction.rationale}</p>
      <p className={`${styles.predictionCheck} ${predictionCheck.matched ? styles.checkGood : styles.checkUnclear}`}>
        {predictionCheck.note}
      </p>
    </div>
  )
}

/** Lists only the citations actually backing a factor/prediction shown above. */
function WorkloadSources({ reasoning }) {
  const usedIds = new Set()
  Object.values(reasoning.perComposite).forEach(factors =>
    factors.forEach(f => f.citationIds.forEach(id => usedIds.add(id))))
  reasoning.prediction.citationIds.forEach(id => usedIds.add(id))
  if (!usedIds.size) return null

  return (
    <div className={styles.card}>
      <h3 className={styles.cardTitle}>Workload Research Sources</h3>
      <ul className={styles.sourceList}>
        {[...usedIds].map(id => {
          const c = CITATIONS[id]
          if (!c) return null
          return (
            <li key={id} className={styles.sourceItem}>
              <a href={c.url} target="_blank" rel="noopener noreferrer">
                {c.authors} ({c.year}) — {c.title}
              </a>
              <p className={styles.sourceFinding}>{c.finding}</p>
            </li>
          )
        })}
      </ul>
      <p className={styles.emptySub}>
        See MODULE4_WORKLOAD_REASONING_METHODOLOGY.md for the full derivation of every rule and threshold.
      </p>
    </div>
  )
}
