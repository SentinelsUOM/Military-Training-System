'use client'
import { useEffect, useMemo, useState } from 'react'
import Link from 'next/link'
import { analyzeMeasure } from '@/lib/stats'
import { benchmarkPos } from '@/lib/expertBenchmarks'
import ThemeToggle from '@/components/ui/ThemeToggle'
import styles from './EvaluationClient.module.css'

const LEVELS = ['basic', 'intermediate', 'advanced']
const LEVEL_LABEL = { basic: 'Basic', intermediate: 'Intermediate', advanced: 'Advanced' }

// Measures the significance tests run on, tagged by WHICH MODULE actually owns the
// underlying data (see MODULE4_EVALUATION.md's ownership table) — Perceived Intelligence/
// Animacy/Realism/Enemy hit-rate are Module 2 (AI believability/behaviour); Reaction Time
// is Module 3 (CognitiveTracking), just displayed here; the five Module 4 rows are
// Module 4's own scores (Overall/Hostage Safety/Accuracy/Speed/Operator Safety).
const STAT_MEASURES = [
  { key: 'perceivedIntelligence', label: 'Perceived Intelligence', source: 'aiEval',   group: 'Module 2 — AI Believability' },
  { key: 'animacy',               label: 'Animacy (life-like)',    source: 'aiEval',   group: 'Module 2 — AI Believability' },
  { key: 'realism',               label: 'Tactical realism',       source: 'aiEval',   group: 'Module 2 — AI Believability' },
  { key: 'enemyAccuracy',         label: 'Enemy hit-rate (NPC)',   source: 'metrics',  group: 'Module 2 — AI Believability' },
  { key: 'reactionTime',          label: 'Reaction time',          source: 'metrics',  group: 'Module 3 — Cognitive' },
  { key: 'overallScore',          label: 'Overall score',          source: 'metrics',  group: 'Module 4 — Score Validity' },
  { key: 'safetyScore',           label: 'Hostage Safety',         source: 'metrics',  group: 'Module 4 — Score Validity' },
  { key: 'accuracy',              label: 'Accuracy (you)',         source: 'metrics',  group: 'Module 4 — Score Validity' },
  { key: 'speedScore',            label: 'Speed',                  source: 'metrics',  group: 'Module 4 — Score Validity' },
  { key: 'operatorSafetyScore',   label: 'Operator Safety',        source: 'metrics',  group: 'Module 4 — Score Validity' },
]

// Module 4's own 5 scores, for the repeated-trial reliability and expert-benchmark
// sections. `accuracy` is stored 0-100 in `metrics` (see route.js) so it needs /100
// to line up with the other four, which are already 0-1.
const MODULE4_SCORES = [
  { key: 'overallScore',         label: 'Overall',         pctScale: false },
  { key: 'safetyScore',          label: 'Hostage Safety',  pctScale: false },
  { key: 'accuracy',             label: 'Accuracy',        pctScale: true  },
  { key: 'speedScore',           label: 'Speed',           pctScale: false },
  { key: 'operatorSafetyScore',  label: 'Operator Safety', pctScale: false },
]

// Only two of the five have an independent published benchmark to compare against
// (see MODULE4_EVALUATION.md) — Speed and Operator Safety's composite have none, and
// Overall is an internal weighted composite, not itself a literature value.
const MODULE4_BENCH = {
  safetyScore: {
    higherIsBetter: true, expert: 0.85, novice: 0.40,
    fmt: (v) => `${Math.round(v * 100)}%`, source: 'RAND hostage-rescue outcomes',
  },
  accuracy: {
    higherIsBetter: true, expert: 1.0, novice: 0.35,
    fmt: (v) => `${Math.round(v * 100)}%`, source: "NYPD SOP-9, normalized to this session's engagement range",
  },
}

const fmtP = (p) => (p == null ? '—' : p < 0.001 ? '< 0.001' : p.toFixed(3))
const round2 = (n) => (n == null ? '—' : Math.round(n * 100) / 100)

// Which module a row/tab belongs to — drives the tab bar below.
const MODULE_TABS = [
  { key: 'module2', label: 'Module 2 — AI Believability' },
  { key: 'module3', label: 'Module 3 — Cognitive' },
  { key: 'module4', label: 'Module 4 — Score Validity' },
]

// Display order + labels. `better` = which direction is "good" (for the arrow hint).
// All believability survey rows belong to Module 2 (the AI-behaviour survey).
const SURVEY_ROWS = [
  { key: 'perceivedIntelligence', label: 'Perceived Intelligence', unit: '/5', better: 'up', module: 'module2' },
  { key: 'animacy',               label: 'Animacy (life-like)',    unit: '/5', better: 'up', module: 'module2' },
  { key: 'realism',               label: 'Tactical realism',       unit: '/5', better: 'up', module: 'module2' },
  { key: 'ueqPragmatic',          label: 'UEQ — Pragmatic',        unit: '/7', better: 'up', module: 'module2' },
  { key: 'ueqHedonic',            label: 'UEQ — Hedonic',          unit: '/7', better: 'up', module: 'module2' },
]
// Objective telemetry rows are mixed — tagged by which module actually owns the
// underlying measurement (see MODULE4_EVALUATION.md's ownership table).
const METRIC_ROWS = [
  { key: 'reactionTime',        label: 'Avg reaction time',       unit: 's', better: 'down', module: 'module3' },
  { key: 'enemyAccuracy',       label: 'Enemy hit-rate (NPC)',     unit: '%', better: 'up',   module: 'module2' },
  { key: 'accuracy',            label: 'Accuracy (you)',          unit: '%', better: null,    module: 'module4' },
  { key: 'duration',            label: 'Mission duration',        unit: 's', better: null,    module: 'module4' },
  { key: 'overallScore',        label: 'Overall score',           unit: '',  better: null,    module: 'module4' },
  { key: 'safetyScore',         label: 'Hostage Safety',          unit: '',  better: null,    module: 'module4' },
  { key: 'speedScore',          label: 'Speed',                   unit: '',  better: null,    module: 'module4' },
  { key: 'operatorSafetyScore', label: 'Operator Safety',         unit: '',  better: null,    module: 'module4' },
]

const fmt = (v, unit = '') => (v == null ? '—' : `${v}${unit}`)

export default function EvaluationClient() {
  const [data, setData] = useState(null)
  const [error, setError] = useState(null)
  const [playerId, setPlayerId] = useState('')
  const [activeTab, setActiveTab] = useState('module2')

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
            <ThemeToggle />
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
            {/* ── Module tab bar (hidden in print — all tabs print sequentially) ── */}
            <div className={styles.tabBar}>
              {MODULE_TABS.map(t => (
                <button
                  key={t.key}
                  className={activeTab === t.key ? `${styles.tabBtn} ${styles.tabBtnActive}` : styles.tabBtn}
                  onClick={() => setActiveTab(t.key)}
                >
                  {t.label}
                </button>
              ))}
            </div>

            {MODULE_TABS.map(t => {
              const surveyRows = SURVEY_ROWS.filter(r => r.module === t.key)
              const metricRows = METRIC_ROWS.filter(r => r.module === t.key)
              const measures = STAT_MEASURES.filter(m => m.group === t.label)
              const bodyClass = activeTab === t.key ? styles.tabBody : `${styles.tabBody} ${styles.tabBodyHidden}`

              return (
                <div key={t.key} className={bodyClass}>
                  <h2 className={styles.printOnlyHeading}>{t.label}</h2>

                  {/* ── Per-player comparison ─────────────────────────── */}
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
                    {player && <CompareTable levels={player.levels} surveyRows={surveyRows} metricRows={metricRows} />}
                  </section>

                  {/* ── Overall averages ──────────────────────────────── */}
                  <section className={styles.section}>
                    <h2 className={styles.h2}>Overall averages (all players)</h2>
                    <AveragesTable averages={data.averages} surveyRows={surveyRows} metricRows={metricRows} />
                  </section>

                  {/* ── Statistical significance ──────────────────────── */}
                  <section className={styles.section}>
                    <h2 className={styles.h2}>Statistical significance</h2>
                    <StatsPanel players={data.players} measures={measures} />
                  </section>

                  {t.key === 'module2' && (
                    <p className={styles.note}>
                      Survey scores are participant ratings after each play (higher = better for the AI).
                      Enemy hit-rate is an objective telemetry measure of the same thing (how competently
                      the AI fought). The significance section runs a Friedman test (any difference across
                      the three levels?) and Wilcoxon signed-rank post-hoc tests (which pairs differ?) on
                      complete-case participants.
                    </p>
                  )}

                  {t.key === 'module3' && (
                    <p className={styles.note}>
                      This page only surfaces reaction time as it relates to the Module 2 AI-difficulty
                      ablation study. Module 3&apos;s full cognitive evaluation (movement telemetry, workload
                      reasoning, stability/attention) lives in each session&apos;s own Movement and Workload
                      tabs on the dashboard, not here.
                    </p>
                  )}

                  {t.key === 'module4' && (
                    <>
                      {/* ── Repeated-trial reliability ──────────────────── */}
                      <section className={styles.section}>
                        <h2 className={styles.h2}>Repeated-Trial Reliability</h2>
                        <p className={styles.note} style={{ marginTop: 0 }}>
                          Is Module 4&apos;s score consistent when the SAME person plays the SAME scenario/AI
                          level more than once? Shown per trial, with mean and standard deviation across
                          trials — tight clustering (low SD) means the score is reliable, not noisy.
                        </p>
                        <ReliabilityPanel retests={data.retests} />
                      </section>

                      {/* ── Expert-benchmark comparison, averaged across trials ── */}
                      <section className={styles.section}>
                        <h2 className={styles.h2}>vs Expert Benchmarks (averaged across trials)</h2>
                        <p className={styles.note} style={{ marginTop: 0 }}>
                          Each person&apos;s repeated-trial scores are averaged first (to smooth out
                          single-session noise), then compared to published expert/novice values where one
                          exists. Speed and Operator Safety have no independent published benchmark — shown
                          as-is, not scored against a source that doesn&apos;t exist.
                        </p>
                        <BenchmarkPanel retests={data.retests} />
                      </section>
                    </>
                  )}
                </div>
              )
            })}
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

function CompareTable({ levels, surveyRows, metricRows }) {
  const Section = ({ title, rows, isSurvey }) => rows.length === 0 ? null : (
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
        <Section title="Believability (survey)" rows={surveyRows} isSurvey />
        <Section title="Objective (telemetry)" rows={metricRows} isSurvey={false} />
      </tbody>
    </table>
  )
}

function AveragesTable({ averages, surveyRows, metricRows }) {
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
        {surveyRows.length > 0 && <tr className={styles.groupRow}><td colSpan={4}>Believability (survey)</td></tr>}
        {surveyRows.map(r => (
          <tr key={r.key}>
            <td className={styles.rowLabel}>{r.label} ({r.unit})</td>
            {LEVELS.map(l => cell(l, r.key, 'survey', ''))}
          </tr>
        ))}
        {metricRows.length > 0 && <tr className={styles.groupRow}><td colSpan={4}>Objective (telemetry)</td></tr>}
        {metricRows.map(r => (
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

function StatsPanel({ players, measures }) {
  const results = useMemo(
    () => measures.map(m => ({ m, ...analyzeMeasure(buildSeries(players, m)) })),
    [players, measures]
  )

  if (measures.length === 0) {
    return <div className={styles.msg}>No measures defined for this module yet.</div>
  }

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

// ── Repeated-trial reliability ────────────────────────────────────────────
// `retests` = groups of 2+ sessions sharing the same playerId + AI level, oldest→newest.
function ReliabilityPanel({ retests }) {
  if (!retests?.length) {
    return (
      <div className={styles.msg}>
        No repeated-scenario trials yet. Have the same participant play the same AI level more
        than once (same Player ID + Level on the mission form) and this fills in automatically.
      </div>
    )
  }
  return (
    <div className={styles.stats}>
      {retests.map(g => <RetestBlock key={`${g.playerId}::${g.level}`} group={g} />)}
    </div>
  )
}

function scoreSeries(group, spec) {
  return group.trials.map(t => {
    const raw = t.metrics?.[spec.key]
    return typeof raw === 'number' ? (spec.pctScale ? raw / 100 : raw) : null
  })
}
function meanOf(nums) { return nums.length ? nums.reduce((a, b) => a + b, 0) / nums.length : null }
function stdDevOf(nums, m) {
  if (nums.length < 2) return null
  const variance = nums.reduce((a, b) => a + (b - m) ** 2, 0) / (nums.length - 1)
  return Math.sqrt(variance)
}
const pct = (v) => (v == null ? '—' : `${Math.round(v * 100)}%`)

function RetestBlock({ group }) {
  const rows = MODULE4_SCORES.map(spec => {
    const vals = scoreSeries(group, spec)
    const nums = vals.filter(v => typeof v === 'number')
    const m = meanOf(nums)
    const sd = stdDevOf(nums, m)
    return { ...spec, vals, mean: m, sd }
  })
  return (
    <div className={styles.statBlock}>
      <div className={styles.statHead}>
        <span className={styles.statTitle}>{group.playerId} — {LEVEL_LABEL[group.level]}</span>
        <span className={styles.statMeta}>{group.trials.length} trials</span>
      </div>
      <table className={styles.table}>
        <thead>
          <tr>
            <th></th>
            {group.trials.map((_, i) => <th key={i} className={styles.num}>Trial {i + 1}</th>)}
            <th className={styles.num}>Mean</th>
            <th className={styles.num}>SD</th>
          </tr>
        </thead>
        <tbody>
          {rows.map(r => (
            <tr key={r.key}>
              <td className={styles.rowLabel}>{r.label}</td>
              {r.vals.map((v, i) => <td key={i} className={styles.num}>{pct(v)}</td>)}
              <td className={styles.num}>{pct(r.mean)}</td>
              <td className={styles.num}>{r.sd == null ? '—' : `±${Math.round(r.sd * 100)}pp`}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

// ── Module 4 vs expert benchmarks, averaged across repeated trials ─────────
function BenchmarkPanel({ retests }) {
  if (!retests?.length) {
    return (
      <div className={styles.msg}>
        No repeated-scenario trials yet — this section averages a participant&apos;s repeats before
        comparing to a benchmark, to reduce single-session noise.
      </div>
    )
  }
  return (
    <div className={styles.stats}>
      {retests.map(g => <BenchmarkBlock key={`${g.playerId}::${g.level}`} group={g} />)}
    </div>
  )
}

function BenchmarkBlock({ group }) {
  return (
    <div className={styles.statBlock}>
      <div className={styles.statHead}>
        <span className={styles.statTitle}>{group.playerId} — {LEVEL_LABEL[group.level]}</span>
        <span className={styles.statMeta}>avg of {group.trials.length} trials</span>
      </div>
      <div className={styles.benchGrid}>
        {MODULE4_SCORES.map(spec => {
          const nums = scoreSeries(group, spec).filter(v => typeof v === 'number')
          if (!nums.length) return null
          const m = meanOf(nums)
          const b = MODULE4_BENCH[spec.key]
          const pos = b ? benchmarkPos(m, b) : null
          return (
            <div key={spec.key} className={styles.benchRow}>
              <div className={styles.benchLabelRow}>
                <span className={styles.benchLabel}>{spec.label}</span>
                <span className={styles.benchValue} style={pos ? { color: pos.color } : undefined}>{pct(m)}</span>
                {pos && <span className={styles.benchVerdict} style={{ color: pos.color, borderColor: pos.color }}>{pos.verdict}</span>}
              </div>
              {b ? (
                <>
                  <div className={styles.benchBar}>
                    <div className={styles.benchBarFill} style={{ width: `${(pos.pct * 100).toFixed(0)}%` }} />
                  </div>
                  <div className={styles.benchMeta}>Novice {b.fmt(b.novice)} · Expert {b.fmt(b.expert)} · {b.source}</div>
                </>
              ) : (
                <div className={styles.benchMeta}>No independent published benchmark exists for this composite score.</div>
              )}
            </div>
          )
        })}
      </div>
    </div>
  )
}
