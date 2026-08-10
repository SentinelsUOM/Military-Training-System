'use client'
import { useEffect, useMemo, useState } from 'react'
import Link from 'next/link'
import {
  BarChart, Bar, LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer, Cell,
  RadarChart, Radar, PolarGrid, PolarAngleAxis, PolarRadiusAxis, ErrorBar,
} from 'recharts'
import { analyzeMeasure, oneSampleWilcoxon, oneWayAnova } from '@/lib/stats'
import { benchmarkPos } from '@/lib/expertBenchmarks'
import { BENCHMARKS as MOVEMENT_BENCHMARKS, CITATIONS as MOVEMENT_CITATIONS, evalMetric as evalMovementMetric } from '@/lib/movementBenchmarks'
import { useChartTheme } from '@/lib/useTheme'
import ThemeToggle from '@/components/ui/ThemeToggle'
import Module1Panel from './Module1Panel'
import { METRIC_GROUPS as M1_METRIC_GROUPS, METRIC_LABELS as M1_METRIC_LABELS } from '@/lib/module1Results'
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

// Module 3's body-movement & reaction-channel measures — the SAME literature bands the
// per-session Movement tab evaluates each session against (lib/movementBenchmarks.js),
// applied here to each player's combined (all-tiers) average instead of a single session.
// Unlike MODULE3_MEASURES above, these don't have a single expert/novice constant — they're
// bands (min–max) — so they use evalMovementMetric()'s tiered verdict (good/watch/
// unvalidated) rather than benchmarkPos()'s novice→expert percentile placement.
// `reactionOverall` is intentionally omitted: it's the same reaction-time figure as
// MODULE3_MEASURES' `reactionTime` above (different aggregation source — movementTrack vs
// cognitiveSummary — showing both would just be a confusing near-duplicate). `reactionHead`
// is also omitted: no session has ever recorded a head-channel reaction, so it's always
// empty — showing it would just be a permanently blank column/row.
const MOVEMENT_MEASURE_KEYS = [
  'avgSpeed', 'maxSpeed', 'timeCrouched', 'headScanning', 'weaponHeldPct', 'handTravel',
  'reactionHands', 'reactionMovement', 'reactionTrigger', 'threatsResponded',
]
const MOVEMENT_MEASURES = MOVEMENT_MEASURE_KEYS.map(key => {
  const def = MOVEMENT_BENCHMARKS[key]
  return {
    key, label: def.label, unit: def.unit, tier: def.tier,
    fmt: (v) => (v == null ? '—' : `${Math.round(v * 100) / 100}${def.unit}`),
  }
})

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
// `sessionBased` marks the tabs whose data comes from trainee Sessions in MongoDB
// (/api/evaluation). Module 1 is the exception: its evaluation is an offline batch
// study of the scenario generator served by /api/module1-evaluation, so it renders
// even when no missions have been played, and it survives a database outage.
const MODULE_TABS = [
  { key: 'module1', label: 'Module 1 — Scenario Generation', sessionBased: false },
  { key: 'module2', label: 'Module 2 — AI Believability',    sessionBased: true },
  { key: 'module3', label: 'Module 3 — Cognitive',           sessionBased: true },
  { key: 'module4', label: 'Module 4 — Score Validity',      sessionBased: true },
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
  const [activeTab, setActiveTab] = useState('module1')
  // Module 1's study data is fetched separately and kept in its own state: it comes
  // from the filesystem rather than MongoDB, so neither source should be able to
  // blank out the other's tab.
  const [m1, setM1] = useState(null)
  const [m1Error, setM1Error] = useState(null)

  useEffect(() => {
    fetch('/api/evaluation')
      .then(r => r.ok ? r.json() : Promise.reject(r.status))
      .then(d => {
        setData(d)
        if (d.players?.length) setPlayerId(d.players[0].playerId)
      })
      .catch(() => setError('Could not load session evaluation data.'))

    fetch('/api/module1-evaluation')
      .then(r => r.ok ? r.json() : Promise.reject(r.status))
      .then(setM1)
      .catch(() => setM1Error('Could not load the Module 1 scenario-generation study.'))
  }, [])

  const player = useMemo(
    () => data?.players?.find(p => p.playerId === playerId) || null,
    [data, playerId]
  )

  const saveCsv = (lines, fileName) => {
    const blob = new Blob([lines.join('\n')], { type: 'text/csv' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url; a.download = fileName; a.click()
    URL.revokeObjectURL(url)
  }

  const downloadSessionCsv = () => {
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
    saveCsv(lines, 'module2-4-session-evaluation.csv')
  }

  // Module 1's export is a different shape entirely (per-scenario study aggregates,
  // not per-player session rows), so the button exports whichever dataset the active
  // tab is actually showing rather than silently handing over the other module's data.
  const downloadModule1Csv = () => {
    if (!m1?.available) return
    const lines = ['experiment,section,group,metric,statistic,value']
    const push = (exp, section, group, metric, statistic, value) =>
      lines.push([exp, section, group, metric, statistic, value ?? ''].join(','))

    for (const g of M1_METRIC_GROUPS) {
      for (const metric of g.metrics) {
        for (const [exp, set] of [['A', m1.experimentA.medium], ['A2', m1.experimentA.high]]) {
          const s = set[metric.key]
          if (!s) continue
          push(exp, 'variability', g.group, metric.key, 'mean', s.mean)
          push(exp, 'variability', g.group, metric.key, 'sd', s.sd)
          push(exp, 'variability', g.group, metric.key, 'cv_pct', s.cv)
        }
      }
    }
    for (const [exp, pairs] of [['A', m1.experimentA.pairsMedium], ['A2', m1.experimentA.pairsHigh]]) {
      for (const [key, s] of Object.entries(pairs.measures)) {
        for (const stat of ['mean', 'sd', 'min', 'max']) push(exp, 'pairwise_diversity', `${pairs.pairs} pairs`, key, stat, s[stat])
      }
    }
    for (const param of m1.experimentB.parameters) {
      for (const level of param.levels) {
        for (const [key, s] of Object.entries(level.metrics)) push('B', 'traceability', `${param.parameter}=${level.value}`, key, 'mean', s.mean)
      }
    }
    for (const [exp, block, label] of [['C', m1.experimentC, 'randomness'], ['D', m1.experimentD, 'difficulty']]) {
      for (const g of block.groups) {
        for (const [key, s] of Object.entries(g.metrics)) push(exp, label, `${label}=${g.value}`, key, 'mean', s.mean)
      }
    }
    for (const s of m1.experimentE.scopes) {
      for (const stat of ['n', 'pass', 'fail', 'passRate']) push('E', 'validation', `"${s.scope}"`, 'validation', stat, s[stat])
    }
    for (const f of m1.experimentE.failures) push('E', 'failures', 'all', f.category, 'count', f.count)

    saveCsv(lines, 'module1-scenario-generation-study.csv')
  }

  const onModule1 = activeTab === 'module1'
  const downloadCsv = onModule1 ? downloadModule1Csv : downloadSessionCsv
  const hasSessionData = data?.players?.length > 0
  // The CSV button only ever exports the active tab's dataset, so it's enabled on
  // whichever of the two sources has actually loaded.
  const canDownload = onModule1 ? !!m1?.available : hasSessionData

  return (
    <div className={styles.page}>
      <div className={styles.wrap}>
        <header className={styles.head}>
          <div>
            <div className={styles.kicker}>Team Sentinels · Module Evaluations</div>
            <h1 className={styles.title}>Evaluation Results</h1>
          </div>
          <div className={styles.actions}>
            <ThemeToggle />
            <Link href="/" className={styles.linkBtn}>← Dashboard</Link>
            <button className={styles.btn} onClick={downloadCsv} disabled={!canDownload}>
              Download CSV{onModule1 ? ' (Module 1)' : ''}
            </button>
            <button className={styles.btn} onClick={() => window.print()}>Print / Save PDF</button>
          </div>
        </header>

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

              {t.key === 'module1' && <Module1Panel data={m1} error={m1Error} />}

              {/* The session-driven tabs share one empty/loading/error state — kept
                  per-tab rather than replacing the whole page, so a database problem
                  or an unstarted study can't also hide Module 1's offline results. */}
              {t.sessionBased && !hasSessionData && (
                <div className={styles.msg}>
                  {error ? error
                    : !data ? 'Loading session data…'
                    : <>
                        No tagged sessions yet. Start missions from the mission form with a{' '}
                        <strong>Player ID</strong> and an <strong>NPC Level</strong> (Basic /
                        Intermediate / Advanced), complete the surveys, and results will appear here.
                      </>}
                </div>
              )}

                  {t.key === 'module2' && hasSessionData && (
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
                        {player && <PlayerTierChart levels={player.levels} surveyRows={surveyRows} metricRows={metricRows} />}
                        {player && <PlayerReliabilityChart player={player} surveyRows={surveyRows} metricRows={metricRows} />}
                      </section>

                      <section className={styles.section}>
                        <h2 className={styles.h2}>Overall averages (all players)</h2>
                        <AveragesTable averages={data.averages} surveyRows={surveyRows} metricRows={metricRows} />
                        <TierAveragesChart averages={data.averages} surveyRows={surveyRows} metricRows={metricRows} />
                      </section>

                      <section className={styles.section}>
                        <h2 className={styles.h2}>Statistical significance</h2>
                        <StatsPanel players={data.players} measures={measures} />
                      </section>

                      <section className={styles.section}>
                        <h2 className={styles.h2}>Distribution across all sessions (pooled)</h2>
                        <p className={styles.note} style={{ marginTop: 0 }}>
                          Every recorded session&apos;s value for each measure, any player, any tier — shows
                          the spread behind the averages above (e.g. a wide, flat histogram means high
                          disagreement between participants; a tall narrow one means consensus).
                        </p>
                        <PoolDistributionChart pooledValues={data.pooledValues} measures={measures} />
                      </section>

                      <section className={styles.section}>
                        <h2 className={styles.h2}>Ablation study (Unity telemetry)</h2>
                        <AblationChart />
                      </section>

                      <section className={styles.section}>
                        <h2 className={styles.h2}>Ablation study — evaluation-result diagrams</h2>
                        <p className={styles.note} style={{ marginTop: 0, marginBottom: '.75rem' }}>
                          Pre-rendered results from the live Unity ablation run (
                          <code>AblationExperimentRunner</code>, 750 real selection decisions across
                          GunshotHeard/RoomBreached/AllyDownSeen) — not the weight-sensitivity (0–4×)
                          sweep, which is a separate study. Full method in
                          MODULE2_ABLATION_STUDY_REPORT.md.
                        </p>
                        <DiagramGallery items={MODULE2_ABLATION_DIAGRAMS} />
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

                  {(t.key === 'module3' || t.key === 'module4') && hasSessionData && (
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
                        {t.key === 'module4' && player && <PlayerScoresRadarChart player={player} measures={MODULE4_MEASURES} />}
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

                  {t.key === 'module3' && hasSessionData && (
                    <>
                      {/* ── Body-movement & reaction-channel metrics vs literature bands ──
                          Same evalMetric() verdict logic as the per-session Movement tab
                          (lib/movementBenchmarks.js), applied to each player's combined
                          (all-tiers) average instead of a single session. ── */}
                      <section className={styles.section}>
                        <h2 className={styles.h2}>Body movement & reaction-channel metrics (per player vs literature)</h2>
                        <p className={styles.note} style={{ marginTop: 0 }}>
                          Each player&apos;s sessions are averaged together (all AI tiers combined), then
                          compared against the same literature-derived bands the per-session Movement tab
                          uses. Tier A = direct empirical match, B = literature proxy, C = no quantified
                          source (shown with no verdict rather than a fabricated one).
                        </p>
                        <MovementBenchmarkTable players={data.players} measures={MOVEMENT_MEASURES} />
                        <MovementBenchmarkSources measures={MOVEMENT_MEASURES} />
                      </section>

                      {/* ── ANOVA — same one-way, between-player test as reaction time above,
                          applied to each movement/reaction-channel metric. ── */}
                      <section className={styles.section}>
                        <h2 className={styles.h2}>ANOVA Table — movement metrics (between players)</h2>
                        <p className={styles.note} style={{ marginTop: 0 }}>
                          Does each movement/reaction-channel metric differ between players by more than
                          their own session-to-session variability would explain? Same one-way ANOVA as the
                          reaction-time table above (Source / SS / df / MS / F / p), one player = one group,
                          that player&apos;s sessions = that group&apos;s replicates.
                        </p>
                        <AnovaPanel players={data.players} measures={MOVEMENT_MEASURES} />
                      </section>

                      <p className={styles.note}>
                        Workload reasoning and the stability/attention proxy scores still live only in each
                        session&apos;s own Movement and Workload tabs on the dashboard, not here — those are
                        per-session diagnostics, not population-level measures with a literature benchmark.
                      </p>
                    </>
                  )}

                  {t.key === 'module4' && hasSessionData && (
                    <>
                      {/* ── Repeated-play reliability — one player at a time, picked below ── */}
                      <section className={styles.section}>
                        <div className={styles.sectionHead}>
                          <h2 className={styles.h2}>Repeated-Play Reliability</h2>
                          <label className={styles.selectWrap}>
                            Player&nbsp;
                            <select className={styles.select} value={playerId} onChange={e => setPlayerId(e.target.value)}>
                              {data.players.map(p => <option key={p.playerId} value={p.playerId}>{p.playerId}</option>)}
                            </select>
                          </label>
                        </div>
                        <p className={styles.note} style={{ marginTop: 0 }}>
                          Is Module 4&apos;s score consistent when the SAME person plays more than once?
                          Shown per session, with mean and standard deviation across sessions — tight
                          clustering (low SD) means the score is reliable, not noisy.
                        </p>
                        <ReliabilityPanel players={data.players} playerId={playerId} />
                      </section>

                      {/* ── Expert-benchmark comparison, averaged across a player's sessions ── */}
                      <section className={styles.section}>
                        <div className={styles.sectionHead}>
                          <h2 className={styles.h2}>vs Expert Benchmarks (averaged per player)</h2>
                          <label className={styles.selectWrap}>
                            Player&nbsp;
                            <select className={styles.select} value={playerId} onChange={e => setPlayerId(e.target.value)}>
                              {data.players.map(p => <option key={p.playerId} value={p.playerId}>{p.playerId}</option>)}
                            </select>
                          </label>
                        </div>
                        <p className={styles.note} style={{ marginTop: 0 }}>
                          Each person&apos;s sessions are averaged first (to smooth out single-session
                          noise), then compared to published expert/novice values where one exists.
                          Speed and Operator Safety have no independent published benchmark — shown
                          as-is, not scored against a source that doesn&apos;t exist.
                        </p>
                        <BenchmarkPanel players={data.players} playerId={playerId} />
                      </section>

                      <section className={styles.section}>
                        <h2 className={styles.h2}>Expert-benchmark diagrams (poster / presentation)</h2>
                        <p className={styles.note} style={{ marginTop: 0, marginBottom: '.75rem' }}>
                          The same "trainee value vs. published expert" figures shown on the
                          Team Sentinels poster and presentation deck, reproduced here alongside
                          the live per-player benchmark panel above. Full method in
                          Module4_Documents/MODULE4_EVALUATION.md.
                        </p>
                        <DiagramGallery items={MODULE4_EXPERT_DIAGRAMS} />
                      </section>

                      {/* ── Distribution across all sessions (pooled) — same pattern as Module 2's ── */}
                      <section className={styles.section}>
                        <h2 className={styles.h2}>Distribution across all sessions (pooled)</h2>
                        <p className={styles.note} style={{ marginTop: 0 }}>
                          Every recorded session&apos;s value for each of Module 4&apos;s 5 scores, any
                          player — shows the spread behind the averages above.
                        </p>
                        <ScoreDistributionChart pooledValues={data.pooledValues} measures={MODULE4_SCORES} />
                      </section>
                    </>
                  )}
            </div>
          )
        })}
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

// Survey sub-scales are on different raw scales (Godspeed 1-5, UEQ-S 1-7) — parse the
// row's own `/5` or `/7` unit label so every measure can be normalized onto a common
// 0-100% axis and plotted side-by-side. Objective metric rows (enemyAccuracy, unit '%')
// are already 0-100 and pass through unchanged.
function measureMax(unit) {
  const m = /\/(\d+)/.exec(unit || '')
  return m ? parseInt(m[1], 10) : 100
}
const toPct100 = (raw, max) => (typeof raw === 'number' ? Math.round((raw / max) * 1000) / 10 : null)

// ── Overall averages, by AI tier — grouped bar chart ────────────────────────
// Mirrors AveragesTable one-for-one (same `averages` prop, same rows) so it recalculates
// automatically the moment a new tagged session lands — no separate data path to keep in sync.
function TierAveragesChart({ averages, surveyRows, metricRows }) {
  const chartTheme = useChartTheme()
  const rows = [
    ...surveyRows.map(r => ({ ...r, group: 'survey' })),
    ...metricRows.map(r => ({ ...r, group: 'metrics' })),
  ]
  const chartData = rows.map(r => {
    const max = measureMax(r.unit)
    const entry = { name: r.label }
    for (const lvl of LEVELS) {
      const raw = averages?.[lvl]?.[r.group]?.[r.key]
      entry[lvl] = r.group === 'survey' ? toPct100(raw, max) : (typeof raw === 'number' ? raw : null)
    }
    return entry
  })
  if (!rows.length) return null
  const totalN = LEVELS.reduce((sum, lvl) => sum + (averages?.[lvl]?.n || 0), 0)
  if (!totalN) return null

  return (
    <div style={{ marginTop: '1rem' }}>
      <div className={styles.note} style={{ marginTop: 0, marginBottom: '.5rem' }}>
        All measures normalized to 0–100% so survey scales (Godspeed /5, UEQ-S /7) and the
        objective enemy hit-rate (%) can be compared on one axis.
      </div>
      <ResponsiveContainer width="100%" height={Math.max(220, rows.length * 46)}>
        <BarChart data={chartData} layout="vertical" margin={{ top: 4, right: 16, left: 8, bottom: 0 }}>
          <CartesianGrid stroke={chartTheme.grid} strokeDasharray="3 3" horizontal={false} />
          <XAxis type="number" domain={[0, 100]} tick={{ fill: chartTheme.label, fontSize: 11 }} unit="%" />
          <YAxis type="category" dataKey="name" width={150} tick={{ fill: chartTheme.label, fontSize: 11 }} />
          <Tooltip
            contentStyle={{ background: chartTheme.tooltip, border: `1px solid ${chartTheme.grid}`, borderRadius: 6 }}
            labelStyle={{ color: chartTheme.label }}
            formatter={(v) => (v == null ? '—' : `${v}%`)}
          />
          <Legend wrapperStyle={{ fontSize: 11, color: chartTheme.label }} formatter={(v) => LEVEL_LABEL[v] || v} />
          {LEVELS.map((lvl, i) => (
            <Bar key={lvl} dataKey={lvl} name={lvl} fill={chartTheme.colors[i % chartTheme.colors.length]} radius={[0, 3, 3, 0]} />
          ))}
        </BarChart>
      </ResponsiveContainer>
    </div>
  )
}

// ── Per-player trend across AI tiers — line chart ───────────────────────────
// Same `levels` prop as CompareTable, for whichever player is selected above — swapping
// the player dropdown re-renders this instantly since it's derived straight from props.
function PlayerTierChart({ levels, surveyRows, metricRows }) {
  const chartTheme = useChartTheme()
  const rows = [
    ...surveyRows.map(r => ({ ...r, group: 'survey' })),
    ...metricRows.map(r => ({ ...r, group: 'metrics' })),
  ]
  const chartData = LEVELS.map(lvl => {
    const play = levels[lvl]
    const entry = { tier: LEVEL_LABEL[lvl] }
    for (const r of rows) {
      const max = measureMax(r.unit)
      const raw = r.group === 'survey' ? play?.aiEval?.[r.key] : play?.metrics?.[r.key]
      entry[r.key] = r.group === 'survey' ? toPct100(raw, max) : (typeof raw === 'number' ? raw : null)
    }
    return entry
  })
  const hasAnyValue = chartData.some(d => rows.some(r => d[r.key] != null))
  if (!rows.length || !hasAnyValue) return null

  return (
    <div style={{ marginTop: '1rem' }}>
      <div className={styles.note} style={{ marginTop: 0, marginBottom: '.5rem' }}>
        This player&apos;s believability ratings and enemy hit-rate as AI difficulty rises
        (normalized to 0–100% — see chart above). A rising line means the AI felt/performed
        better at harder tiers; missing points mean that tier hasn&apos;t been played yet.
      </div>
      <ResponsiveContainer width="100%" height={240}>
        <LineChart data={chartData} margin={{ top: 4, right: 16, left: -12, bottom: 0 }}>
          <CartesianGrid stroke={chartTheme.grid} strokeDasharray="3 3" />
          <XAxis dataKey="tier" tick={{ fill: chartTheme.label, fontSize: 11 }} />
          <YAxis domain={[0, 100]} tick={{ fill: chartTheme.label, fontSize: 11 }} unit="%" />
          <Tooltip
            contentStyle={{ background: chartTheme.tooltip, border: `1px solid ${chartTheme.grid}`, borderRadius: 6 }}
            labelStyle={{ color: chartTheme.label }}
            formatter={(v) => (v == null ? '—' : `${v}%`)}
          />
          <Legend wrapperStyle={{ fontSize: 11, color: chartTheme.label }} />
          {rows.map((r, i) => (
            <Line
              key={r.key}
              type="monotone"
              dataKey={r.key}
              name={r.label}
              stroke={chartTheme.colors[i % chartTheme.colors.length]}
              strokeWidth={2}
              dot={{ r: 3 }}
              connectNulls
            />
          ))}
        </LineChart>
      </ResponsiveContainer>
    </div>
  )
}

// ── Per-player reliability — repeated plays at the SAME tier ────────────────
// Unlike PlayerTierChart above (one point per tier, the newest play), this walks
// `player.allTrials` — every session this player ever played, oldest→newest — grouped
// by npcLevel, so a tier the player replayed shows a consistency/learning-curve line
// across attempts #1, #2, … instead of just the latest value.
function PlayerReliabilityChart({ player, surveyRows, metricRows }) {
  const chartTheme = useChartTheme()
  const rows = [
    ...surveyRows.map(r => ({ ...r, group: 'survey' })),
    ...metricRows.map(r => ({ ...r, group: 'metrics' })),
  ]
  const byTier = {}
  for (const lvl of LEVELS) {
    const trials = (player.allTrials || []).filter(t => t.npcLevel === lvl)
    if (trials.length > 1) byTier[lvl] = trials
  }
  const tiersWithRepeats = LEVELS.filter(lvl => byTier[lvl])
  if (!rows.length || !tiersWithRepeats.length) return null

  return (
    <div style={{ marginTop: '1rem' }}>
      <div className={styles.note} style={{ marginTop: 0, marginBottom: '.5rem' }}>
        Reliability — how this player&apos;s ratings/hit-rate changed each time they replayed the
        <strong> same</strong> AI tier. A flat line means consistent scoring; a rising one may mean
        the player was learning the mission rather than judging the AI.
      </div>
      <div style={{ display: 'grid', gridTemplateColumns: tiersWithRepeats.length > 1 ? '1fr 1fr' : '1fr', gap: '1rem' }}>
        {tiersWithRepeats.map(lvl => {
          const chartData = byTier[lvl].map((t, i) => {
            const entry = { attempt: `#${i + 1}` }
            for (const r of rows) {
              const max = measureMax(r.unit)
              const raw = r.group === 'survey' ? t.aiEval?.[r.key] : t.metrics?.[r.key]
              entry[r.key] = r.group === 'survey' ? toPct100(raw, max) : (typeof raw === 'number' ? raw : null)
            }
            return entry
          })
          return (
            <div key={lvl}>
              <div style={{ fontSize: 12, fontWeight: 600, color: 'var(--text-dim)', marginBottom: 4 }}>
                {LEVEL_LABEL[lvl]} <span className={styles.nBadge}>{byTier[lvl].length} plays</span>
              </div>
              <ResponsiveContainer width="100%" height={180}>
                <LineChart data={chartData} margin={{ top: 4, right: 12, left: -16, bottom: 0 }}>
                  <CartesianGrid stroke={chartTheme.grid} strokeDasharray="3 3" />
                  <XAxis dataKey="attempt" tick={{ fill: chartTheme.label, fontSize: 10 }} />
                  <YAxis domain={[0, 100]} tick={{ fill: chartTheme.label, fontSize: 10 }} unit="%" />
                  <Tooltip
                    contentStyle={{ background: chartTheme.tooltip, border: `1px solid ${chartTheme.grid}`, borderRadius: 6 }}
                    labelStyle={{ color: chartTheme.label }}
                    formatter={(v) => (v == null ? '—' : `${v}%`)}
                  />
                  <Legend wrapperStyle={{ fontSize: 10, color: chartTheme.label }} />
                  {rows.map((r, i) => (
                    <Line
                      key={r.key}
                      type="monotone"
                      dataKey={r.key}
                      name={r.label}
                      stroke={chartTheme.colors[i % chartTheme.colors.length]}
                      strokeWidth={2}
                      dot={{ r: 3 }}
                      connectNulls
                    />
                  ))}
                </LineChart>
              </ResponsiveContainer>
            </div>
          )
        })}
      </div>
    </div>
  )
}

// ── Distribution / spread across all pooled sessions — histogram ────────────
// One mini-histogram per measure, built from `pooledValues` (every tagged session,
// any player, any tier). Survey scores are normalized to 0-100% the same way the
// other Module 2 charts are, so all four measures share one x-axis.
function buildHistogramBins(values, binCount = 8, domainMax = 100) {
  const bins = Array.from({ length: binCount }, (_, i) => ({
    label: `${Math.round((i * domainMax) / binCount)}-${Math.round(((i + 1) * domainMax) / binCount)}`,
    count: 0,
  }))
  for (const v of values) {
    let idx = Math.floor((v / domainMax) * binCount)
    if (idx >= binCount) idx = binCount - 1
    if (idx < 0) idx = 0
    bins[idx].count++
  }
  return bins
}

function PoolDistributionChart({ pooledValues, measures }) {
  const items = measures.map(m => {
    const raw = pooledValues?.[m.key] || []
    // STAT_MEASURES don't carry their own scale — reuse SURVEY_ROWS/METRIC_ROWS' unit
    // (same source AveragesTable/TierAveragesChart already read) to normalize.
    const row = SURVEY_ROWS.find(r => r.key === m.key) || METRIC_ROWS.find(r => r.key === m.key)
    const max = measureMax(row?.unit)
    const isRatio = !!row?.unit && row.unit.startsWith('/')
    const values = raw.map(v => (isRatio ? toPct100(v, max) : v)).filter(v => typeof v === 'number')
    return { key: m.key, label: m.label, n: values.length, bins: buildHistogramBins(values) }
  }).filter(it => it.n >= 5)
  return <HistogramGrid items={items} />
}

// Module 4's own 5 scores are already 0-1 ratios (not a /5 or /7 survey scale like Module
// 2's), so they only need a straight ×100 — reuses toPct100(v, 1) rather than the
// unit-string lookup PoolDistributionChart needs for Module 2's mixed scales.
function ScoreDistributionChart({ pooledValues, measures }) {
  const items = measures.map(m => {
    const raw = pooledValues?.[m.key] || []
    const values = raw.map(v => toPct100(v, 1)).filter(v => typeof v === 'number')
    return { key: m.key, label: m.label, n: values.length, bins: buildHistogramBins(values) }
  }).filter(it => it.n >= 5)
  return <HistogramGrid items={items} />
}

// Shared renderer for both distribution charts above — one mini-histogram card per
// pre-built item ({ key, label, n, bins }), so the two callers can't visually drift apart.
function HistogramGrid({ items }) {
  const chartTheme = useChartTheme()
  if (!items.length) {
    return <div className={styles.msg}>Need at least 5 pooled sessions for a measure to plot its distribution.</div>
  }
  return (
    <div className={styles.stats}>
      {items.map(it => (
        <div key={it.key} className={styles.statBlock}>
          <div className={styles.statHead}>
            <span className={styles.statTitle}>{it.label}</span>
            <span className={styles.statMeta}>n = {it.n} sessions</span>
          </div>
          <ResponsiveContainer width="100%" height={140}>
            <BarChart data={it.bins} margin={{ top: 4, right: 8, left: -20, bottom: 0 }}>
              <XAxis dataKey="label" tick={{ fill: chartTheme.label, fontSize: 9 }} unit="%" />
              <YAxis allowDecimals={false} tick={{ fill: chartTheme.label, fontSize: 10 }} width={24} />
              <Tooltip
                contentStyle={{ background: chartTheme.tooltip, border: `1px solid ${chartTheme.grid}`, borderRadius: 6 }}
                labelStyle={{ color: chartTheme.label }}
                formatter={(v) => [`${v} session${v === 1 ? '' : 's'}`, 'Count']}
              />
              <Bar dataKey="count" radius={[3, 3, 0, 0]} fill={chartTheme.accent} />
            </BarChart>
          </ResponsiveContainer>
        </div>
      ))}
    </div>
  )
}

// ── Static evaluation-result diagrams (pre-rendered PNGs, not live charts) ──
// These come from Report_Figures/ (copied into public/report-figures/) — the same
// print-quality figures used in MODULE2_ABLATION_STUDY_REPORT.md, MODULE2_COMPLETE_
// EXPLAINER, MODULE4_EVALUATION.md, the poster, and the presentation deck. They are
// NOT recomputed from live session data (unlike the Recharts panels elsewhere on this
// page) — they're evidence snapshots from a specific dated experiment run, so the
// module/date is always shown in the caption to make that explicit.
const MODULE2_ABLATION_DIAGRAMS = [
  { src: 'fig_ablation_1_role_distribution.png',
    caption: 'Figure 1. Which role gets picked, by ablation mode, for GunshotHeard events. ' +
      'Roamers win almost every event under Full/NoDistance/NoState/NoLOS; only removing Role ' +
      'itself changes the picture.' },
  { src: 'fig_ablation_2_agreement_with_full.png',
    caption: 'Figure 2. % of GunshotHeard events where each ablated mode picked the same NPC ' +
      'as the full formula — Role (22%) > State (44%) > LOS (80%) > Distance (94%). Lower % = ' +
      'that term matters more.' },
  { src: 'fig_ablation_3_mean_distance.png',
    caption: 'Figure 3. Mean distance of the selected NPC, by mode. Full (11.4m) vs. ' +
      'NoDistance (12.3m) — removing distance sends a responder about 0.9m farther away on average.' },
  { src: 'fig_ablation_4_mean_state_score.png',
    caption: 'Figure 4. Mean state-readiness of the selected NPC. Full (0.71) vs NoState (0.40): ' +
      'removing the state term nearly halves the average readiness of who gets sent.' },
  { src: 'fig_ablation_5_los_hit_rate.png',
    caption: 'Figure 5. % of selections with clear line-of-sight to the event. Full (88%) vs ' +
      'NoLOS (68%): without the perception term, the selector sends a "blind" responder almost 3x more often.' },
  { src: 'fig_ablation_roombreached_1_role_distribution.png',
    caption: 'Figure 5a. Who gets picked for RoomBreached (favours Guard). Guard wins 49/50 ' +
      'events under Full — same dominance pattern as Roamer under GunshotHeard, now confirmed ' +
      'for a second role-bonus entry.' },
  { src: 'fig_ablation_roombreached_2_agreement.png',
    caption: 'Selection agreement vs. Full — RoomBreached. NoRole drops agreement to 42%, the ' +
      'largest swing of the three event types, consistent with Guard carrying the largest bonus (3.0).' },
  { src: 'fig_ablation_allydownseen_1_role_distribution.png',
    caption: 'Figure 5b. Who gets picked for AllyDownSeen (favours Leader). Leader wins 46/50 ' +
      'events under Full.' },
  { src: 'fig_ablation_allydownseen_2_agreement.png',
    caption: 'Selection agreement vs. Full — AllyDownSeen. NoRole drops agreement only to 50%, ' +
      'the smallest swing of the three event types, consistent with Leader carrying the smallest bonus (1.5).' },
  { src: 'fig_ablation_6_bonus_vs_dominance.png',
    caption: 'Does bonus magnitude track measured dominance? Guard (3.0) 98%, Roamer (2.0) 96%, ' +
      'Leader (1.5) 92% — monotonic, real evidence the RELATIVE ordering of the three bonus ' +
      'magnitudes produces the intended relative behaviour (the absolute values themselves are ' +
      'still not literature-derived — see MODULE2_SELECTION_LITERATURE_JUSTIFICATION.md).' },
]

const MODULE4_EXPERT_DIAGRAMS = [
  { src: 'fig_7_14_reaction_time_per_participant.png',
    caption: "Mean reaction time per participant (11 total, sorted fastest→slowest) vs the " +
      "Hick's Law expert/novice reference lines Module 4's FAST_RT/SLOW_RT anchors are " +
      'calibrated to. Same chart used in the poster and presentation deck.' },
  { src: 'fig_7_17_scores_vs_benchmarks.png',
    caption: 'Hostage Safety & Accuracy — all 11 participants plotted individually against the ' +
      'novice/expert benchmark bands from the Expert Value Table. Same chart used in the poster ' +
      'and presentation deck.' },
]

function DiagramGallery({ items }) {
  return (
    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(320px, 1fr))', gap: '1rem' }}>
      {items.map(it => (
        <div key={it.src} className={styles.statBlock}>
          <img
            src={`/report-figures/${it.src}`}
            alt={it.caption}
            style={{ width: '100%', height: 'auto', borderRadius: 6, display: 'block', background: '#fff' }}
          />
          <div className={styles.note} style={{ marginTop: '.5rem', marginBottom: 0 }}>{it.caption}</div>
        </div>
      ))}
    </div>
  )
}

// ── Ablation study — client-parsed Unity CSV ─────────────────────────────────
// AblationExperimentRunner.cs writes AblationResults_*.csv to
// Application.persistentDataPath/Telemetry/ — that data never reaches Mongo, so this
// reads the file straight in the browser (FileReader, nothing uploaded anywhere) and
// graphs it. Not tied to the live DB feed like the charts above: re-load a fresh CSV
// export any time you want to see updated numbers.
function parseAblationCsv(text) {
  const lines = text.trim().split(/\r?\n/)
  if (lines.length < 2) throw new Error('File is empty.')
  const header = lines[0].split(',').map(h => h.trim())
  const modeIdx = header.indexOf('ablation_mode')
  const scoreIdx = header.indexOf('state_score')
  if (modeIdx === -1 || scoreIdx === -1) {
    throw new Error('Not a recognized AblationResults CSV (missing ablation_mode / state_score columns).')
  }
  const rows = []
  for (let i = 1; i < lines.length; i++) {
    if (!lines[i].trim()) continue
    const cols = lines[i].split(',')
    const score = parseFloat(cols[scoreIdx])
    const mode = cols[modeIdx]?.trim()
    if (mode && !Number.isNaN(score)) rows.push({ mode, score })
  }
  if (!rows.length) throw new Error('No usable rows found in this CSV.')
  return rows
}

function AblationChart() {
  const chartTheme = useChartTheme()
  const [rows, setRows] = useState(null)
  const [fileName, setFileName] = useState('')
  const [error, setError] = useState('')

  const onFile = (e) => {
    const file = e.target.files?.[0]
    if (!file) return
    setFileName(file.name)
    setError('')
    const reader = new FileReader()
    reader.onload = () => {
      try {
        setRows(parseAblationCsv(String(reader.result)))
      } catch (err) {
        setRows(null)
        setError(err.message)
      }
    }
    reader.onerror = () => { setRows(null); setError('Could not read this file.') }
    reader.readAsText(file)
  }

  const chartData = useMemo(() => {
    if (!rows) return []
    const byMode = {}
    for (const r of rows) {
      byMode[r.mode] = byMode[r.mode] || { mode: r.mode, sum: 0, n: 0 }
      byMode[r.mode].sum += r.score
      byMode[r.mode].n += 1
    }
    return Object.values(byMode).map(g => ({ mode: g.mode, avgScore: round2(g.sum / g.n), n: g.n }))
  }, [rows])

  return (
    <div>
      <p className={styles.note} style={{ marginTop: 0 }}>
        Loads an <strong>AblationResults_*.csv</strong> file exported by Unity&apos;s
        AblationExperimentRunner (<code>Application.persistentDataPath/Telemetry/</code>) and graphs
        the average responder-selection <code>state_score</code> per ablation mode — how much each
        scoring factor (line-of-sight, distance, role) actually mattered. This data lives only in
        the exported file, not the trainee database, so re-load a new export to see updated numbers.
      </p>
      <input type="file" accept=".csv" onChange={onFile} className={styles.select} style={{ marginBottom: '.75rem' }} />
      {fileName && !error && <span className={styles.nBadge}>{fileName} · {rows?.length ?? 0} rows loaded</span>}
      {error && <div className={styles.msg} style={{ marginTop: '.5rem' }}>{error}</div>}
      {chartData.length > 0 && (
        <ResponsiveContainer width="100%" height={220}>
          <BarChart data={chartData} margin={{ top: 4, right: 16, left: -8, bottom: 0 }}>
            <CartesianGrid stroke={chartTheme.grid} strokeDasharray="3 3" />
            <XAxis dataKey="mode" tick={{ fill: chartTheme.label, fontSize: 11 }} />
            <YAxis tick={{ fill: chartTheme.label, fontSize: 11 }} />
            <Tooltip
              contentStyle={{ background: chartTheme.tooltip, border: `1px solid ${chartTheme.grid}`, borderRadius: 6 }}
              labelStyle={{ color: chartTheme.label }}
              formatter={(v, name, props) => [`${v} (n=${props.payload.n})`, 'Avg state_score']}
            />
            <Bar dataKey="avgScore" radius={[3, 3, 0, 0]}>
              {chartData.map((_, i) => <Cell key={i} fill={chartTheme.colors[i % chartTheme.colors.length]} />)}
            </Bar>
          </BarChart>
        </ResponsiveContainer>
      )}
    </div>
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
      <StatsEffectChart results={results} />
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

// ── Effect size (r) per tier pair, across measures — grouped bar chart ──────
// Same `results` StatsPanel already computed (analyzeMeasure per measure) — just
// re-plotted, so it can never drift from the table underneath it. Solid bars mark a
// pair that cleared the Bonferroni-corrected α; faded bars did not.
function StatsEffectChart({ results }) {
  const chartTheme = useChartTheme()
  const usable = results.filter(r => r.n >= 3 && r.friedman && r.pairs.length > 0)
  if (!usable.length) return null

  const pairLabels = usable[0].pairs.map(pr => `${pr.labelA} vs ${pr.labelB}`)
  const chartData = usable.map(({ m, pairs, alpha }) => {
    const entry = { name: m.label }
    pairs.forEach((pr, i) => {
      const key = pairLabels[i]
      entry[key] = round2(pr.r)
      entry[`${key}__p`] = pr.p
      entry[`${key}__sig`] = pr.p < alpha
    })
    return entry
  })

  return (
    <div style={{ marginBottom: '.5rem' }}>
      <div className={styles.note} style={{ marginTop: 0 }}>
        Wilcoxon effect size (r, 0–1) per tier pair. Solid bars are statistically
        significant (p &lt; Bonferroni-corrected α); faded bars are not.
      </div>
      <ResponsiveContainer width="100%" height={Math.max(180, usable.length * 60)}>
        <BarChart data={chartData} layout="vertical" margin={{ top: 4, right: 16, left: 8, bottom: 0 }}>
          <CartesianGrid stroke={chartTheme.grid} strokeDasharray="3 3" horizontal={false} />
          <XAxis type="number" domain={[0, 1]} tick={{ fill: chartTheme.label, fontSize: 11 }} />
          <YAxis type="category" dataKey="name" width={150} tick={{ fill: chartTheme.label, fontSize: 11 }} />
          <Tooltip
            contentStyle={{ background: chartTheme.tooltip, border: `1px solid ${chartTheme.grid}`, borderRadius: 6 }}
            labelStyle={{ color: chartTheme.label }}
            formatter={(value, name, props) => {
              const p = props.payload[`${name}__p`]
              const sig = props.payload[`${name}__sig`]
              return [`r = ${value}, p = ${fmtP(p)} (${sig ? 'sig.' : 'n.s.'})`, name]
            }}
          />
          <Legend wrapperStyle={{ fontSize: 11, color: chartTheme.label }} />
          {pairLabels.map((key, i) => (
            <Bar key={key} dataKey={key} name={key} fill={chartTheme.colors[i % chartTheme.colors.length]}>
              {chartData.map((d, di) => <Cell key={di} fillOpacity={d[`${key}__sig`] ? 1 : 0.3} />)}
            </Bar>
          ))}
        </BarChart>
      </ResponsiveContainer>
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

// ── Module 4's 5 scores, one player's combined average, as a radar shape ────────────
// Same axis/value/radar recipe as the per-session Performance Radar on the dashboard's
// own Summary tab (components/dashboard/SummaryTab.jsx) — reused here for the *combined*
// (all-tiers-averaged) score instead of a single session, for whichever player is
// selected via the Player dropdown above CombinedPlayerTable.
function PlayerScoresRadarChart({ player, measures }) {
  const chartTheme = useChartTheme()
  if (!player?.combined?.n) return null
  const radarData = measures
    .map(m => ({ axis: m.label, value: player.combined.metrics[m.key] }))
    .filter(d => typeof d.value === 'number')
    .map(d => ({ ...d, value: Math.round(d.value * 100) }))
  if (radarData.length < 3) return null // a radar needs 3+ axes to read as a shape

  return (
    <div style={{ marginTop: '1rem' }}>
      <div className={styles.note} style={{ marginTop: 0, marginBottom: '.5rem' }}>
        {player.playerId}&apos;s combined score shape, averaged across all {player.combined.n} sessions.
      </div>
      <ResponsiveContainer width="100%" height={260}>
        <RadarChart data={radarData}>
          <PolarGrid stroke={chartTheme.grid} />
          <PolarAngleAxis dataKey="axis" tick={{ fill: chartTheme.label, fontSize: 12 }} />
          <PolarRadiusAxis domain={[0, 100]} tick={false} axisLine={false} />
          <Tooltip
            contentStyle={{ background: chartTheme.tooltip, border: `1px solid ${chartTheme.grid}`, borderRadius: 6 }}
            labelStyle={{ color: chartTheme.label }}
            formatter={(v) => `${v}%`}
          />
          <Radar dataKey="value" stroke={chartTheme.accent} fill={chartTheme.accent} fillOpacity={0.25} dot={{ r: 3, fill: chartTheme.accent }} />
        </RadarChart>
      </ResponsiveContainer>
    </div>
  )
}

// ── Movement/reaction-channel metrics vs literature bands, per player ──────────────
// Unlike CombinedPlayerTable's benchmarkPos() (novice→expert percentile placement,
// used for measures with a single expert constant), these metrics only have a band
// (min–max), so verdicts come from evalMovementMetric()'s tiered good/watch/unvalidated
// logic — the same one the per-session Movement tab uses (lib/movementBenchmarks.js).
const MOVEMENT_SEVERITY_COLOR = { good: '#3fb950', watch: '#d29922' }
const MOVEMENT_SEVERITY_LABEL = { good: 'OK', watch: 'WATCH' }

function MovementBenchmarkTable({ players, measures }) {
  const withData = players.filter(p => p.combined?.n > 0)
  return (
    <table className={styles.table}>
      <thead>
        <tr>
          <th>Player</th>
          <th className={styles.num}>Sessions</th>
          {measures.map(m => <th key={m.key} className={styles.num}>{m.label}<sup style={{ marginLeft: 3, opacity: .6 }}>{m.tier}</sup></th>)}
        </tr>
      </thead>
      <tbody>
        <tr className={styles.groupRow}><td colSpan={2 + measures.length}>Literature band (expert range)</td></tr>
        <tr>
          <td className={styles.rowLabel}><strong>Expert range</strong></td>
          <td className={styles.num}>—</td>
          {measures.map(m => {
            const range = MOVEMENT_BENCHMARKS[m.key]?.range
            return (
              <td key={m.key} className={styles.num} style={{ color: '#34d399', fontWeight: 700 }}>
                {range ? `${range.min}–${range.max}${m.unit}` : 'no source (C)'}
              </td>
            )
          })}
        </tr>
        <tr className={styles.groupRow}><td colSpan={2 + measures.length}>Trainees</td></tr>
        {!withData.length ? (
          <tr><td colSpan={2 + measures.length} className={styles.rowLabel}>No sessions recorded yet.</td></tr>
        ) : withData.map(p => (
          <tr key={p.playerId}>
            <td className={styles.rowLabel}>{p.playerId}</td>
            <td className={styles.num}>{p.combined.n}</td>
            {measures.map(m => {
              const v = p.combined.metrics[m.key]
              const verdict = v != null ? evalMovementMetric(m.key, v) : null
              const color = verdict && verdict.severity !== 'unvalidated' ? MOVEMENT_SEVERITY_COLOR[verdict.severity] : undefined
              return (
                <td key={m.key} className={styles.num} style={color ? { color, fontWeight: 700 } : undefined}>
                  {m.fmt(v)}
                  {color && <span style={{ fontSize: 9.5, marginLeft: 5, opacity: 0.85 }}>{MOVEMENT_SEVERITY_LABEL[verdict.severity]}</span>}
                </td>
              )
            })}
          </tr>
        ))}
      </tbody>
    </table>
  )
}

// Dedupes and lists only the citations actually backing the metrics shown above.
function MovementBenchmarkSources({ measures }) {
  const ids = [...new Set(measures.flatMap(m => MOVEMENT_BENCHMARKS[m.key]?.citationIds || []))]
  const cites = ids.map(id => MOVEMENT_CITATIONS[id]).filter(Boolean)
  if (!cites.length) return null
  return (
    <div className={styles.note} style={{ marginTop: '.75rem' }}>
      <strong>Sources: </strong>
      {cites.map((c, i) => (
        <span key={i}>
          {c.authors}{c.year ? ` (${c.year})` : ''} — {c.title}{i < cites.length - 1 ? '; ' : ''}
        </span>
      ))}
    </div>
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
                <AnovaChart groups={groups} />
              </>
            )}
          </div>
        )
      })}
    </div>
  )
}

// Each player's mean ± 1 SD for the measure the ANOVA table above just tested — the same
// `groups` array (one entry per player, that player's sessions as values), just plotted
// instead of reduced to a table. Error bars make the "between vs within" question visual:
// tall bars with barely-overlapping error whiskers = players really do differ.
function AnovaChart({ groups }) {
  const chartTheme = useChartTheme()
  const chartData = groups
    .map(g => {
      const m = meanOf(g.values)
      const sd = stdDevOf(g.values, m)
      return { name: g.label, mean: m == null ? null : round2(m), sd: sd == null ? 0 : round2(sd) }
    })
    .filter(d => d.mean != null)
  if (chartData.length < 2) return null

  return (
    <ResponsiveContainer width="100%" height={160}>
      <BarChart data={chartData} margin={{ top: 8, right: 16, left: 4, bottom: 0 }}>
        <CartesianGrid stroke={chartTheme.grid} strokeDasharray="3 3" />
        <XAxis dataKey="name" tick={{ fill: chartTheme.label, fontSize: 10 }} />
        <YAxis tick={{ fill: chartTheme.label, fontSize: 10 }} />
        <Tooltip
          contentStyle={{ background: chartTheme.tooltip, border: `1px solid ${chartTheme.grid}`, borderRadius: 6 }}
          labelStyle={{ color: chartTheme.label }}
          formatter={(v, name, props) => [`${v} ± ${props.payload.sd}`, 'Mean ± SD']}
        />
        <Bar dataKey="mean" fill={chartTheme.accent} radius={[3, 3, 0, 0]}>
          <ErrorBar dataKey="sd" width={4} strokeWidth={1.5} stroke={chartTheme.label} />
        </Bar>
      </BarChart>
    </ResponsiveContainer>
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
// Filtered to the single player picked in the section header — showing every player's
// block at once got unwieldy as the study grew, so this renders one at a time instead.
function ReliabilityPanel({ players, playerId }) {
  const withRepeats = players.filter(p => (p.allTrials?.length || 0) > 1)
  if (!withRepeats.length) {
    return (
      <div className={styles.msg}>
        No repeated plays yet. Have the same participant play more than once (same Player ID
        on the mission form) and this fills in automatically.
      </div>
    )
  }
  const player = withRepeats.find(p => p.playerId === playerId)
  if (!player) {
    return (
      <div className={styles.msg}>
        <strong>{playerId || 'This player'}</strong> hasn&apos;t played more than once yet.
        Players with repeat sessions: {withRepeats.map(p => p.playerId).join(', ')}.
      </div>
    )
  }
  return (
    <div className={styles.stats}>
      <RetestBlock player={player} />
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
// Same one-player-at-a-time filtering as ReliabilityPanel above, same reason.
function BenchmarkPanel({ players, playerId }) {
  const withRepeats = players.filter(p => (p.allTrials?.length || 0) > 1)
  if (!withRepeats.length) {
    return (
      <div className={styles.msg}>
        No repeated plays yet — this section averages a participant&apos;s sessions before
        comparing to a benchmark, to reduce single-session noise.
      </div>
    )
  }
  const player = withRepeats.find(p => p.playerId === playerId)
  if (!player) {
    return (
      <div className={styles.msg}>
        <strong>{playerId || 'This player'}</strong> hasn&apos;t played more than once yet.
        Players with repeat sessions: {withRepeats.map(p => p.playerId).join(', ')}.
      </div>
    )
  }
  return (
    <div className={styles.stats}>
      <BenchmarkBlock player={player} />
    </div>
  )
}

// Grouped Novice / Trainee / Expert bar chart across all 5 scores — the numeric version
// of the gauge rows below (kept — the gauge is still the clearest single-metric readout,
// this chart is for comparing all 5 scores against each other at a glance). Scores with
// no published benchmark (Speed, Operator Safety, Overall) still show their trainee bar,
// just with no novice/expert bars alongside — same "don't invent a source" rule as the
// gauge rows use.
function BenchmarkBarChart({ trials }) {
  const chartTheme = useChartTheme()
  const chartData = MODULE4_SCORES.map(spec => {
    const nums = scoreSeries(trials, spec).filter(v => typeof v === 'number')
    if (!nums.length) return null
    const m = meanOf(nums)
    const b = MODULE4_BENCH[spec.key]
    return {
      name: spec.label,
      trainee: Math.round(m * 100),
      novice: b ? Math.round(b.novice * 100) : null,
      expert: b ? Math.round(b.expert * 100) : null,
    }
  }).filter(Boolean)
  if (!chartData.length) return null

  return (
    <ResponsiveContainer width="100%" height={220}>
      <BarChart data={chartData} margin={{ top: 4, right: 16, left: -8, bottom: 0 }}>
        <CartesianGrid stroke={chartTheme.grid} strokeDasharray="3 3" />
        <XAxis dataKey="name" tick={{ fill: chartTheme.label, fontSize: 10 }} />
        <YAxis domain={[0, 100]} unit="%" tick={{ fill: chartTheme.label, fontSize: 11 }} />
        <Tooltip
          contentStyle={{ background: chartTheme.tooltip, border: `1px solid ${chartTheme.grid}`, borderRadius: 6 }}
          labelStyle={{ color: chartTheme.label }}
          formatter={(v) => (v == null ? '—' : `${v}%`)}
        />
        <Legend wrapperStyle={{ fontSize: 11, color: chartTheme.label }} />
        <Bar dataKey="novice" name="Novice" fill="#f87171" radius={[3, 3, 0, 0]} />
        <Bar dataKey="trainee" name="Trainee" fill={chartTheme.accent} radius={[3, 3, 0, 0]} />
        <Bar dataKey="expert" name="Expert" fill="#34d399" radius={[3, 3, 0, 0]} />
      </BarChart>
    </ResponsiveContainer>
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
      <BenchmarkBarChart trials={trials} />
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
