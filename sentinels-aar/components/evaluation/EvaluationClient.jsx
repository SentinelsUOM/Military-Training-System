'use client'
import { useEffect, useMemo, useState } from 'react'
import Link from 'next/link'
import { analyzeMeasure, oneSampleWilcoxon, oneWayAnova } from '@/lib/stats'
import { benchmarkPos } from '@/lib/expertBenchmarks'
import ThemeToggle from '@/components/ui/ThemeToggle'
import styles from './EvaluationClient.module.css'

const LEVELS = ['basic', 'intermediate', 'advanced']
const LEVEL_LABEL = { basic: 'Basic', intermediate: 'Intermediate', advanced: 'Advanced' }
const pctFmt = (v) => (v == null ? '—' : `${Math.round(v * 100)}%`)

// Measures the tier-by-tier significance test runs on — Module 2 ONLY. Module 2's whole
// research question IS whether AI difficulty (Basic/Intermediate/Advanced) changes the
// measure, so it's the one module that needs a per-tier comparison. Module 3 and Module 4
// don't have an AI-difficulty independent variable — see MODULE3_MEASURES/MODULE4_MEASURES
// below, which combine a player's sessions across ALL tiers instead.
const STAT_MEASURES = [
  { key: 'perceivedIntelligence', label: 'Perceived Intelligence', source: 'aiEval',   group: 'Module 2 — AI Believability' },
  { key: 'animacy',               label: 'Animacy (life-like)',    source: 'aiEval',   group: 'Module 2 — AI Believability' },
  { key: 'realism',               label: 'Tactical realism',       source: 'aiEval',   group: 'Module 2 — AI Believability' },
  { key: 'enemyAccuracy',         label: 'Enemy hit-rate (NPC)',   source: 'metrics',  group: 'Module 2 — AI Believability' },
]

// Module 3's only measure here (reaction time) — combined across all AI tiers per player,
// then compared to Hick's Law's expert-reaction benchmark. Module 3's full cognitive
// evaluation (movement, workload, stability/attention) lives in the per-session tabs.
const MODULE3_MEASURES = [
  {
    key: 'reactionTime', label: 'Reaction time', fmt: (v) => (v == null ? '—' : `${v.toFixed(2)} s`),
    bench: {
      higherIsBetter: false, expert: 0.30, novice: 0.50,
      fmt: (v) => `${v.toFixed(2)} s`, source: "Hick's Law — choice reaction time",
    },
  },
]

// Module 4's own 5 scores, combined across all AI tiers per player, then compared to a
// published benchmark where one exists. Only Hostage Safety and Accuracy have an
// independent published benchmark (see MODULE4_EVALUATION.md) — Speed and Operator
// Safety's composite have none, and Overall is an internal weighted composite, not
// itself a literature value. `accuracyScore` is Unity's real distance-normalized score
// (hitRate ÷ expected rate at range) — NOT the raw hit-percentage `accuracy` field, which
// belongs to Module 2's own objective-telemetry table above.
const MODULE4_SCORES = [
  { key: 'overallScore',         label: 'Overall' },
  { key: 'safetyScore',          label: 'Hostage Safety' },
  { key: 'accuracyScore',        label: 'Accuracy' },
  { key: 'speedScore',           label: 'Speed' },
  { key: 'operatorSafetyScore',  label: 'Operator Safety' },
]
const MODULE4_BENCH = {
  safetyScore: {
    higherIsBetter: true, expert: 0.85, novice: 0.40,
    fmt: pctFmt, source: 'RAND hostage-rescue outcomes',
  },
  accuracyScore: {
    higherIsBetter: true, expert: 1.0, novice: 0.35,
    fmt: pctFmt, source: "NYPD SOP-9, normalized to this session's engagement range",
  },
}
const MODULE4_MEASURES = MODULE4_SCORES.map(s => ({ ...s, fmt: pctFmt, bench: MODULE4_BENCH[s.key] || null }))

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

                  {t.key === 'module2' && (
                    <>
                      {/* ── Per-player comparison, by AI tier — Module 2's own question ── */}
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

                      <section className={styles.section}>
                        <h2 className={styles.h2}>Overall averages (all players)</h2>
                        <AveragesTable averages={data.averages} surveyRows={surveyRows} metricRows={metricRows} />
                      </section>

                      <section className={styles.section}>
                        <h2 className={styles.h2}>Statistical significance</h2>
                        <StatsPanel players={data.players} measures={measures} />
                      </section>

                      <p className={styles.note}>
                        Survey scores are participant ratings after each play (higher = better for the AI).
                        Enemy hit-rate is an objective telemetry measure of the same thing (how competently
                        the AI fought). The significance section runs a Friedman test (any difference across
                        the three levels?) and Wilcoxon signed-rank post-hoc tests (which pairs differ?) on
                        complete-case participants — because AI difficulty IS Module 2's research question.
                      </p>
                    </>
                  )}

                  {(t.key === 'module3' || t.key === 'module4') && (
                    <>
                      {/* ── Combined per-player average — no AI-tier split ──────
                          Module 3/4 don't have an AI-difficulty independent variable (that's
                          Module 2's own question, above) — so a player's sessions are combined
                          across ALL tiers into one average, then compared to the expert value. ── */}
                      <section className={styles.section}>
                        <h2 className={styles.h2}>Per-player average </h2>
                        <p className={styles.note} style={{ marginTop: 0 }}>
                          {t.key === 'module3'
                            ? "Reaction time doesn't depend on which AI tier was played, so each player's sessions are averaged together regardless of level."
                            : "Module 4's five scores aren't about AI difficulty, so each player's sessions are averaged together across whichever levels they played."}
                        </p>
                        <CombinedPlayerTable players={data.players} measures={t.key === 'module3' ? MODULE3_MEASURES : MODULE4_MEASURES} />
                      </section>

                      {/* ── Pooled vs expert — one-sample test ──────────────────
                          Is the trainee population's average different from the published expert
                          value? Pools every individual session (any player, any tier). ── */}
                      <section className={styles.section}>
                        <h2 className={styles.h2}>vs Expert Value (all sessions pooled)</h2>
                        <p className={styles.note} style={{ marginTop: 0 }}>
                          One-sample Wilcoxon signed-rank test: does the pooled set of every recorded
                          session's value differ significantly from the published expert benchmark?
                          Only measures with an independent published benchmark are testable this way.
                        </p>
                        <PooledVsExpertPanel pooledValues={data.pooledValues} measures={t.key === 'module3' ? MODULE3_MEASURES : MODULE4_MEASURES} />
                      </section>

                      {/* ── ANOVA Table — a genuinely different question from the one above:
                          does the measure vary BETWEEN players, given the spread WITHIN each
                          player's own sessions? Standard one-way ANOVA terminology throughout. ── */}
                      <section className={styles.section}>
                        <h2 className={styles.h2}>ANOVA Table (between players)</h2>
                        <p className={styles.note} style={{ marginTop: 0 }}>
                          One-way analysis of variance, with each PLAYER as a group and their own
                          sessions as that group&apos;s replicates: does the mean differ between
                          players by more than their own session-to-session variability would explain?
                          Source / SS (sum of squares) / df (degrees of freedom) / MS (mean square) / F
                          (F-statistic) / p — standard ANOVA-table columns.
                        </p>
                        <AnovaPanel players={data.players} measures={t.key === 'module3' ? MODULE3_MEASURES : MODULE4_MEASURES} />
                      </section>
                    </>
                  )}

                  {t.key === 'module3' && (
                    <p className={styles.note}>
                      This page only surfaces reaction time as it relates to the trainee population.
                      Module 3&apos;s full cognitive evaluation (movement telemetry, workload
                      reasoning, stability/attention) lives in each session&apos;s own Movement and Workload
                      tabs on the dashboard, not here.
                    </p>
                  )}

                  {t.key === 'module4' && (
                    <>
                      {/* ── Repeated-play reliability ──────────────────── */}
                      <section className={styles.section}>
                        <h2 className={styles.h2}>Repeated-Play Reliability</h2>
                        <p className={styles.note} style={{ marginTop: 0 }}>
                          Is Module 4&apos;s score consistent when the SAME person plays more than once?
                          Shown per session, with mean and standard deviation across sessions — tight
                          clustering (low SD) means the score is reliable, not noisy.
                        </p>
                        <ReliabilityPanel players={data.players} />
                      </section>

                      {/* ── Expert-benchmark comparison, averaged across a player's sessions ── */}
                      <section className={styles.section}>
                        <h2 className={styles.h2}>vs Expert Benchmarks (averaged per player)</h2>
                        <p className={styles.note} style={{ marginTop: 0 }}>
                          Each person&apos;s sessions are averaged first (to smooth out single-session
                          noise), then compared to published expert/novice values where one exists.
                          Speed and Operator Safety have no independent published benchmark — shown
                          as-is, not scored against a source that doesn&apos;t exist.
                        </p>
                        <BenchmarkPanel players={data.players} />
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

// ── Combined per-player average (no AI-tier split) — Module 3 & 4 ──────────
// `measures`: [{ key, label, fmt, bench }]. `bench` is optional — a verdict chip only
// shows for measures that have one (see MODULE4_EVALUATION.md for which do and don't).
// The Expert/Novice reference values are shown as their own rows at the top, pinned above
// the player rows, so the benchmark a player is being judged against is always visible
// right there — not just implied by a colour.
function CombinedPlayerTable({ players, measures }) {
  const withData = players.filter(p => p.combined?.n > 0)
  const hasAnyBench = measures.some(m => m.bench)

  return (
    <table className={styles.table}>
      <thead>
        <tr>
          <th>Player</th>
          <th className={styles.num}>Sessions</th>
          {measures.map(m => <th key={m.key} className={styles.num}>{m.label}</th>)}
        </tr>
      </thead>
      <tbody>
        {hasAnyBench && (
          <>
            <tr className={styles.groupRow}><td colSpan={2 + measures.length}>Published reference values</td></tr>
            <tr>
              <td className={styles.rowLabel}><strong>Expert</strong></td>
              <td className={styles.num}>—</td>
              {measures.map(m => (
                <td key={m.key} className={styles.num} style={{ color: '#34d399', fontWeight: 700 }}>
                  {m.bench ? m.bench.fmt(m.bench.expert) : '—'}
                </td>
              ))}
            </tr>
            <tr>
              <td className={styles.rowLabel}>Novice</td>
              <td className={styles.num}>—</td>
              {measures.map(m => (
                <td key={m.key} className={styles.num} style={{ color: '#f87171' }}>
                  {m.bench ? m.bench.fmt(m.bench.novice) : '—'}
                </td>
              ))}
            </tr>
            <tr className={styles.groupRow}><td colSpan={2 + measures.length}>Trainees</td></tr>
          </>
        )}
        {!withData.length ? (
          <tr><td colSpan={2 + measures.length} className={styles.rowLabel}>No sessions recorded yet.</td></tr>
        ) : withData.map(p => (
          <tr key={p.playerId}>
            <td className={styles.rowLabel}>{p.playerId}</td>
            <td className={styles.num}>{p.combined.n}</td>
            {measures.map(m => {
              const v = p.combined.metrics[m.key]
              const pos = m.bench && v != null ? benchmarkPos(v, m.bench) : null
              return (
                <td key={m.key} className={styles.num} style={pos ? { color: pos.color, fontWeight: 700 } : undefined}>
                  {v == null ? '—' : m.fmt(v)}
                  {pos && <span style={{ fontSize: 9.5, marginLeft: 5, opacity: 0.85 }}>{pos.verdict}</span>}
                </td>
              )
            })}
          </tr>
        ))}
      </tbody>
    </table>
  )
}

// ── ANOVA Table — one-way analysis of variance, PLAYER as the grouping factor ──
// A correctly-computed one-way ANOVA (not the pooled one-sample test above, which is a
// different question): does the measure differ BETWEEN players, given the variability
// WITHIN each player's own sessions? Each player's sessions are that player's replicates.
// Reported with standard ANOVA-table terminology (Source / SS / df / MS / F / p) so it
// reads the way a stats textbook or a reviewer expects, not a relabeled different test.
function AnovaPanel({ players, measures }) {
  const testable = measures // ANOVA doesn't need a bench — it compares players to each other
  return (
    <div className={styles.stats}>
      {testable.map(m => {
        const groups = players
          .map(p => ({ label: p.playerId, values: (p.allTrials || []).map(t => t.metrics?.[m.key]).filter(v => typeof v === 'number') }))
          .filter(g => g.values.length > 0)
        const result = groups.length >= 2 ? oneWayAnova(groups) : null

        return (
          <div key={m.key} className={styles.statBlock}>
            <div className={styles.statHead}>
              <span className={styles.statTitle}>{m.label}</span>
              <span className={styles.statMeta}>k = {groups.length} players</span>
            </div>
            {!result || result.dfWithin <= 0 ? (
              <div className={styles.ns}>
                Need at least 2 players, with at least one having 2+ sessions, to estimate within-player
                variance (currently k = {groups.length}).
              </div>
            ) : (
              <>
                <table className={styles.pairTable}>
                  <thead>
                    <tr>
                      <td className={styles.rowLabel}>Source</td>
                      <td className={styles.num}>SS</td>
                      <td className={styles.num}>df</td>
                      <td className={styles.num}>MS</td>
                      <td className={styles.num}>F</td>
                      <td className={styles.num}>p</td>
                    </tr>
                  </thead>
                  <tbody>
                    <tr>
                      <td className={styles.rowLabel}>Between players</td>
                      <td className={styles.num}>{round2(result.ssBetween)}</td>
                      <td className={styles.num}>{result.dfBetween}</td>
                      <td className={styles.num}>{round2(result.msBetween)}</td>
                      <td className={styles.num}>{round2(result.F)}</td>
                      <td className={styles.num}>
                        {fmtP(result.p)}{' '}
                        {result.p != null && result.p < 0.05
                          ? <span className={styles.sig}>sig.</span>
                          : <span className={styles.ns}>n.s.</span>}
                      </td>
                    </tr>
                    <tr>
                      <td className={styles.rowLabel}>Within players</td>
                      <td className={styles.num}>{round2(result.ssWithin)}</td>
                      <td className={styles.num}>{result.dfWithin}</td>
                      <td className={styles.num}>{round2(result.msWithin)}</td>
                      <td className={styles.num}>—</td>
                      <td className={styles.num}>—</td>
                    </tr>
                    <tr>
                      <td className={styles.rowLabel}>Total</td>
                      <td className={styles.num}>{round2(result.ssTotal)}</td>
                      <td className={styles.num}>{result.dfBetween + result.dfWithin}</td>
                      <td className={styles.num}>—</td>
                      <td className={styles.num}>—</td>
                      <td className={styles.num}>—</td>
                    </tr>
                  </tbody>
                </table>
                <div className={styles.alphaNote}>
                  Grand mean {m.fmt ? m.fmt(result.grandMean) : round2(result.grandMean)} across N = {result.N} sessions.
                  {' '}F({result.dfBetween}, {result.dfWithin}) = {round2(result.F)}, p {result.p < 0.001 ? '< 0.001' : `= ${fmtP(result.p)}`}
                  {' '}— {result.p < 0.05 ? 'players differ significantly from each other' : 'no significant difference between players'} on {m.label.toLowerCase()}.
                  {m.bench && ` For reference, the published expert value is ${m.bench.fmt(m.bench.expert)} (${m.bench.source}).`}
                </div>
              </>
            )}
          </div>
        )
      })}
    </div>
  )
}

// ── Pooled vs expert — one-sample Wilcoxon test ─────────────────────────────
// Pools every individual session's value (any player, any tier) for a measure and asks:
// does this population differ significantly from the published expert constant?
function PooledVsExpertPanel({ pooledValues, measures }) {
  const testable = measures.filter(m => m.bench)
  if (!testable.length) {
    return <div className={styles.msg}>No measure in this module has an independent published benchmark to test against.</div>
  }
  return (
    <div className={styles.stats}>
      {testable.map(m => {
        const values = pooledValues?.[m.key] || []
        const n = values.length
        const meanV = n ? values.reduce((a, b) => a + b, 0) / n : null
        const result = n >= 1 ? oneSampleWilcoxon(values, m.bench.expert) : null
        return (
          <div key={m.key} className={styles.statBlock}>
            <div className={styles.statHead}>
              <span className={styles.statTitle}>{m.label}</span>
              <span className={styles.statMeta}>n = {n} sessions</span>
            </div>
            {n < 5 || !result ? (
              <div className={styles.ns}>Need at least 5 recorded sessions for a meaningful test (n = {n}).</div>
            ) : (
              <>
                <div className={styles.friedman}>
                  Trainee mean {m.fmt(meanV)} vs expert {m.fmt(m.bench.expert)} ({m.bench.source}) —
                  Wilcoxon p = {fmtP(result.p)}, r = {round2(result.r)}{' '}
                  {result.p < 0.05
                    ? <span className={styles.sig}>significantly different from expert</span>
                    : <span className={styles.ns}>not significantly different from expert</span>}
                </div>
                <div className={styles.alphaNote}>
                  One-sample Wilcoxon signed-rank, α = 0.05 · r = effect size (|z|/√n).
                </div>
              </>
            )}
          </div>
        )
      })}
    </div>
  )
}

// ── Repeated-trial reliability ────────────────────────────────────────────
// Any player with 2+ recorded sessions (ANY AI tier — Module 4 doesn't split by tier).
function ReliabilityPanel({ players }) {
  const withRepeats = players.filter(p => (p.allTrials?.length || 0) > 1)
  if (!withRepeats.length) {
    return (
      <div className={styles.msg}>
        No repeated plays yet. Have the same participant play more than once (same Player ID
        on the mission form) and this fills in automatically.
      </div>
    )
  }
  return (
    <div className={styles.stats}>
      {withRepeats.map(p => <RetestBlock key={p.playerId} player={p} />)}
    </div>
  )
}

function scoreSeries(trials, spec) {
  return trials.map(t => {
    const raw = t.metrics?.[spec.key]
    return typeof raw === 'number' ? raw : null
  })
}
function meanOf(nums) { return nums.length ? nums.reduce((a, b) => a + b, 0) / nums.length : null }
function stdDevOf(nums, m) {
  if (nums.length < 2) return null
  const variance = nums.reduce((a, b) => a + (b - m) ** 2, 0) / (nums.length - 1)
  return Math.sqrt(variance)
}
const pct = (v) => (v == null ? '—' : `${Math.round(v * 100)}%`)

function RetestBlock({ player }) {
  const trials = player.allTrials
  const rows = MODULE4_SCORES.map(spec => {
    const vals = scoreSeries(trials, spec)
    const nums = vals.filter(v => typeof v === 'number')
    const m = meanOf(nums)
    const sd = stdDevOf(nums, m)
    return { ...spec, vals, mean: m, sd }
  })
  return (
    <div className={styles.statBlock}>
      <div className={styles.statHead}>
        <span className={styles.statTitle}>{player.playerId}</span>
        <span className={styles.statMeta}>{trials.length} sessions</span>
      </div>
      <table className={styles.table}>
        <thead>
          <tr>
            <th></th>
            {trials.map((_, i) => <th key={i} className={styles.num}>Session {i + 1}</th>)}
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

// ── Module 4 vs expert benchmarks, averaged across a player's sessions ─────
function BenchmarkPanel({ players }) {
  const withRepeats = players.filter(p => (p.allTrials?.length || 0) > 1)
  if (!withRepeats.length) {
    return (
      <div className={styles.msg}>
        No repeated plays yet — this section averages a participant&apos;s sessions before
        comparing to a benchmark, to reduce single-session noise.
      </div>
    )
  }
  return (
    <div className={styles.stats}>
      {withRepeats.map(p => <BenchmarkBlock key={p.playerId} player={p} />)}
    </div>
  )
}

function BenchmarkBlock({ player }) {
  const trials = player.allTrials
  return (
    <div className={styles.statBlock}>
      <div className={styles.statHead}>
        <span className={styles.statTitle}>{player.playerId}</span>
        <span className={styles.statMeta}>avg of {trials.length} sessions</span>
      </div>
      <div className={styles.benchGrid}>
        {MODULE4_SCORES.map(spec => {
          const nums = scoreSeries(trials, spec).filter(v => typeof v === 'number')
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
