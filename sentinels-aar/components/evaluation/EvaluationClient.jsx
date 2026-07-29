'use client'
import { useEffect, useMemo, useState } from 'react'
import Link from 'next/link'
import { analyzeMeasure } from '@/lib/stats'
import styles from './EvaluationClient.module.css'

const LEVELS = ['basic', 'intermediate', 'advanced']
const LEVEL_LABEL = { basic: 'Basic', intermediate: 'Intermediate', advanced: 'Advanced' }

// Measures the significance tests run on: the three believability claims plus two
// key objective metrics. `source` = which sub-object the value lives in per play.
const STAT_MEASURES = [
  { key: 'perceivedIntelligence', label: 'Perceived Intelligence', source: 'aiEval' },
  { key: 'animacy',               label: 'Animacy (life-like)',    source: 'aiEval' },
  { key: 'realism',               label: 'Tactical realism',       source: 'aiEval' },
  { key: 'enemyAccuracy',         label: 'Enemy hit-rate (NPC)',   source: 'metrics' },
  { key: 'reactionTime',          label: 'Reaction time',          source: 'metrics' },
  { key: 'overallScore',          label: 'Overall score',          source: 'metrics' },
]

const fmtP = (p) => (p == null ? '—' : p < 0.001 ? '< 0.001' : p.toFixed(3))
const round2 = (n) => (n == null ? '—' : Math.round(n * 100) / 100)

// Display order + labels. `better` = which direction is "good" (for the arrow hint).
const SURVEY_ROWS = [
  { key: 'perceivedIntelligence', label: 'Perceived Intelligence', unit: '/5', better: 'up' },
  { key: 'animacy',               label: 'Animacy (life-like)',    unit: '/5', better: 'up' },
  { key: 'realism',               label: 'Tactical realism',       unit: '/5', better: 'up' },
  { key: 'ueqPragmatic',          label: 'UEQ — Pragmatic',        unit: '/7', better: 'up' },
  { key: 'ueqHedonic',            label: 'UEQ — Hedonic',          unit: '/7', better: 'up' },
]
const METRIC_ROWS = [
  { key: 'reactionTime',  label: 'Avg reaction time',    unit: 's', better: 'down' },
  { key: 'accuracy',      label: 'Shooting accuracy (you)', unit: '%', better: null },
  { key: 'enemyAccuracy', label: 'Enemy hit-rate (NPC)', unit: '%', better: 'up'   },
  { key: 'duration',      label: 'Mission duration',     unit: 's', better: null   },
  { key: 'overallScore',  label: 'Overall score',        unit: '',  better: null   },
]

const fmt = (v, unit = '') => (v == null ? '—' : `${v}${unit}`)

export default function EvaluationClient() {
  const [data, setData] = useState(null)
  const [error, setError] = useState(null)
  const [playerId, setPlayerId] = useState('')

  useEffect(() => {
    fetch('/api/evaluation')
      .then(r => r.ok ? r.json() : Promise.reject(r.status))
      .then(d => {
        setData(d)
        if (d.players?.length) setPlayerId(d.players[0].playerId)
      })
      .catch(() => setError('Could not load evaluation data.'))
  }, [])

  const player = useMemo(
    () => data?.players?.find(p => p.playerId === playerId) || null,
    [data, playerId]
  )

  const downloadCsv = () => {
    if (!data) return
    const cols = ['playerId', 'level', 'sessionId',
      ...SURVEY_ROWS.map(r => r.key), ...METRIC_ROWS.map(r => r.key), 'workload', 'missionSuccess']
    const lines = [cols.join(',')]
    for (const p of data.players) {
      for (const lvl of LEVELS) {
        const play = p.levels[lvl]
        if (!play) continue
        const row = [p.playerId, lvl, play.sessionId,
          ...SURVEY_ROWS.map(r => play.aiEval?.[r.key] ?? ''),
          ...METRIC_ROWS.map(r => play.metrics?.[r.key] ?? ''),
          play.workload ?? '', play.metrics?.missionSuccess ?? '']
        lines.push(row.join(','))
      }
    }
    const blob = new Blob([lines.join('\n')], { type: 'text/csv' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url; a.download = 'module2-evaluation.csv'; a.click()
    URL.revokeObjectURL(url)
  }

  if (error) return <div className={styles.page}><div className={styles.msg}>{error}</div></div>
  if (!data)  return <div className={styles.page}><div className={styles.msg}>Loading…</div></div>

  const hasData = data.players?.length > 0

  return (
    <div className={styles.page}>
      <div className={styles.wrap}>
        <header className={styles.head}>
          <div>
            <div className={styles.kicker}>Module 2 · Ablation Study</div>
            <h1 className={styles.title}>Enemy-AI Evaluation Results</h1>
          </div>
          <div className={styles.actions}>
            <Link href="/" className={styles.linkBtn}>← Dashboard</Link>
            <button className={styles.btn} onClick={downloadCsv} disabled={!hasData}>Download CSV</button>
            <button className={styles.btn} onClick={() => window.print()} disabled={!hasData}>Print / Save PDF</button>
          </div>
        </header>

        {!hasData ? (
          <div className={styles.msg}>
            No tagged sessions yet. Start missions from the mission form with a <strong>Player ID</strong>{' '}
            and an <strong>NPC Level</strong> (Basic / Intermediate / Advanced), complete the surveys, and results
            will appear here.
          </div>
        ) : (
          <>
            {/* ── Per-player comparison ─────────────────────────────── */}
            <section className={styles.section}>
              <div className={styles.sectionHead}>
                <h2 className={styles.h2}>Per-player comparison</h2>
                <label className={styles.selectWrap}>
                  Player&nbsp;
                  <select className={styles.select} value={playerId} onChange={e => setPlayerId(e.target.value)}>
                    {data.players.map(p => <option key={p.playerId} value={p.playerId}>{p.playerId}</option>)}
                  </select>
                </label>
              </div>
              {player && <CompareTable levels={player.levels} />}
            </section>

            {/* ── Overall averages ──────────────────────────────────── */}
            <section className={styles.section}>
              <h2 className={styles.h2}>Overall averages (all players)</h2>
              <AveragesTable averages={data.averages} />
            </section>

            {/* ── Statistical significance ──────────────────────────── */}
            <section className={styles.section}>
              <h2 className={styles.h2}>Statistical significance</h2>
              <StatsPanel players={data.players} />
            </section>

            <p className={styles.note}>
              Survey scores are participant ratings after each play (higher = better for the AI).
              Objective metrics come from the game telemetry. The significance section runs a
              Friedman test (any difference across the three levels?) and Wilcoxon signed-rank
              post-hoc tests (which pairs differ?) on complete-case participants.
            </p>
          </>
        )}
      </div>
    </div>
  )
}

function ValueCell({ play, row, isSurvey }) {
  const v = isSurvey ? play?.aiEval?.[row.key] : play?.metrics?.[row.key]
  return <td className={styles.num}>{fmt(v, row.unit)}</td>
}

function CompareTable({ levels }) {
  const Section = ({ title, rows, isSurvey }) => (
    <>
      <tr className={styles.groupRow}><td colSpan={4}>{title}</td></tr>
      {rows.map(row => (
        <tr key={row.key}>
          <td className={styles.rowLabel}>{row.label}{row.unit ? ` (${row.unit})` : ''}</td>
          {LEVELS.map(lvl => <ValueCell key={lvl} play={levels[lvl]} row={row} isSurvey={isSurvey} />)}
        </tr>
      ))}
    </>
  )
  return (
    <table className={styles.table}>
      <thead>
        <tr><th></th>{LEVELS.map(l => <th key={l} className={styles.num}>{LEVEL_LABEL[l]}</th>)}</tr>
      </thead>
      <tbody>
        <Section title="Believability (survey)" rows={SURVEY_ROWS} isSurvey />
        <Section title="Objective (telemetry)" rows={METRIC_ROWS} isSurvey={false} />
      </tbody>
    </table>
  )
}

function AveragesTable({ averages }) {
  const cell = (lvl, key, group, unit) => {
    const v = averages?.[lvl]?.[group]?.[key]
    return <td key={lvl} className={styles.num}>{fmt(v, unit)}</td>
  }
  return (
    <table className={styles.table}>
      <thead>
        <tr>
          <th></th>
          {LEVELS.map(l => (
            <th key={l} className={styles.num}>{LEVEL_LABEL[l]}<span className={styles.nBadge}> n={averages?.[l]?.n ?? 0}</span></th>
          ))}
        </tr>
      </thead>
      <tbody>
        <tr className={styles.groupRow}><td colSpan={4}>Believability (survey)</td></tr>
        {SURVEY_ROWS.map(r => (
          <tr key={r.key}>
            <td className={styles.rowLabel}>{r.label} ({r.unit})</td>
            {LEVELS.map(l => cell(l, r.key, 'survey', ''))}
          </tr>
        ))}
        <tr className={styles.groupRow}><td colSpan={4}>Objective (telemetry)</td></tr>
        {METRIC_ROWS.map(r => (
          <tr key={r.key}>
            <td className={styles.rowLabel}>{r.label} ({r.unit})</td>
            {LEVELS.map(l => cell(l, r.key, 'metrics', ''))}
          </tr>
        ))}
      </tbody>
    </table>
  )
}

// Build one series per level for a measure, aligned by participant index so the
// tests only use participants who have a value at every level (complete cases).
function buildSeries(players, measure) {
  return LEVELS.map(lvl => ({
    label: LEVEL_LABEL[lvl],
    values: players.map(p => {
      const play = p.levels[lvl]
      const v = play ? (measure.source === 'aiEval' ? play.aiEval?.[measure.key] : play.metrics?.[measure.key]) : undefined
      return typeof v === 'number' ? v : NaN
    }),
  }))
}

function StatsPanel({ players }) {
  const results = useMemo(
    () => STAT_MEASURES.map(m => ({ m, ...analyzeMeasure(buildSeries(players, m)) })),
    [players]
  )

  const maxN = results.reduce((mx, r) => Math.max(mx, r.n), 0)
  if (maxN < 3) {
    return (
      <div className={styles.msg}>
        Need at least <strong>3 participants</strong> who played <strong>all three</strong> levels
        (Basic, Intermediate, Advanced) to run the tests — currently {maxN}. Collect more study
        runs and this fills in automatically.
      </div>
    )
  }

  return (
    <div className={styles.stats}>
      <p className={styles.statsIntro}>
        Repeated-measures, non-parametric. <strong>Friedman</strong> asks whether the AI level
        changed the measure at all; <strong>Wilcoxon</strong> signed-rank post-hoc asks which pair
        differs (α is Bonferroni-corrected for the 3 pairs). A result is significant when p &lt; α.
      </p>
      {results.map(({ m, n, friedman: fr, pairs, alpha }) => (
        <div key={m.key} className={styles.statBlock}>
          <div className={styles.statHead}>
            <span className={styles.statTitle}>{m.label}</span>
            <span className={styles.statMeta}>n = {n} complete</span>
          </div>
          {n < 3 || !fr ? (
            <div className={styles.ns}>Not enough complete data for this measure (n = {n}).</div>
          ) : (
            <>
              <div className={styles.friedman}>
                Friedman χ²({fr.df}) = {round2(fr.chi2)}, p = {fmtP(fr.p)}, Kendall&apos;s W = {round2(fr.kendallW)}{' '}
                {fr.p < 0.05
                  ? <span className={styles.sig}>significant</span>
                  : <span className={styles.ns}>n.s.</span>}
              </div>
              <table className={styles.pairTable}>
                <tbody>
                  {pairs.map(pr => (
                    <tr key={`${pr.i}-${pr.j}`}>
                      <td className={styles.rowLabel}>{pr.labelA} vs {pr.labelB}</td>
                      <td className={styles.num}>p = {fmtP(pr.p)}</td>
                      <td className={styles.num}>r = {round2(pr.r)}</td>
                      <td className={styles.num}>
                        {pr.p < alpha
                          ? <span className={styles.sig}>✓ sig</span>
                          : <span className={styles.ns}>n.s.</span>}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
              <div className={styles.alphaNote}>Post-hoc α = {round2(alpha)} (Bonferroni · 3 pairs) · r = effect size.</div>
            </>
          )}
        </div>
      ))}
    </div>
  )
}
