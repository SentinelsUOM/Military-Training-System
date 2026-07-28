'use client'
import { useEffect, useMemo, useState } from 'react'
import Link from 'next/link'
import styles from './EvaluationClient.module.css'

const LEVELS = ['basic', 'intermediate', 'advanced']
const LEVEL_LABEL = { basic: 'Basic', intermediate: 'Intermediate', advanced: 'Advanced' }

// Display order + labels. `better` = which direction is "good" (for the arrow hint).
const SURVEY_ROWS = [
  { key: 'perceivedIntelligence', label: 'Perceived Intelligence', unit: '/5', better: 'up' },
  { key: 'animacy',               label: 'Animacy (life-like)',    unit: '/5', better: 'up' },
  { key: 'realism',               label: 'Tactical realism',       unit: '/5', better: 'up' },
  { key: 'ueqPragmatic',          label: 'UEQ — Pragmatic',        unit: '/7', better: 'up' },
  { key: 'ueqHedonic',            label: 'UEQ — Hedonic',          unit: '/7', better: 'up' },
]
const METRIC_ROWS = [
  { key: 'reactionTime', label: 'Avg reaction time', unit: 's',  better: 'down' },
  { key: 'accuracy',     label: 'Enemy accuracy',    unit: '%',  better: null   },
  { key: 'duration',     label: 'Mission duration',  unit: 's',  better: null   },
  { key: 'overallScore', label: 'Overall score',     unit: '',   better: null   },
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

            <p className={styles.note}>
              Survey scores are participant ratings after each play (higher = better for the AI).
              Objective metrics come from the game telemetry. Use the CSV for statistical tests
              (e.g. Friedman / Wilcoxon across the three levels).
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
