'use client'
import { useMemo } from 'react'
import {
  STUDY, EXPERIMENTS, RESEARCH_QUESTIONS, METRIC_GROUPS, METRIC_LABELS, DIVERSITY_MEASURES,
  A_VS_A2_TESTS, A_VS_A2_INTERPRETATION,
  C_FOCUS_STATS, C_INTERPRETATION, D_FOCUS_STATS, D_INTERPRETATION, CAVEATS,
  FIGURES, FIGURES_NOT_COMMITTED,
} from '@/lib/module1Results'
import styles from './EvaluationClient.module.css'

// ── Module 1 — Dynamic Scenario Generation ───────────────────────────────────
// The one tab on this page whose unit of analysis is a GENERATED SCENARIO rather
// than a trainee. Module 1 is evaluated offline: Unity's BatchEvaluationRunner
// generates 2000 scenarios across six controlled experiments and measures each one,
// so there is no player selector, no AI-difficulty split and no session telemetry
// here — those concepts belong to the modules that observe people playing.
//
// Descriptive numbers come from /api/module1-evaluation, which recomputes them from
// the study's raw CSVs on every request. Test statistics (p, ε²) come from
// lib/module1Results.js, transcribed from the published analysis. See that file's
// header for why the two are sourced differently.

const dec = (v, places = 2) => (v == null || Number.isNaN(v) ? '—' : v.toFixed(places))
const pct = (v, places = 1) => (v == null || Number.isNaN(v) ? '—' : `${v.toFixed(places)}%`)
const signed = (v, places = 1) => (v == null || Number.isNaN(v) ? '—' : `${v >= 0 ? '+' : ''}${v.toFixed(places)}%`)

/** ε² magnitude → colour. Mirrors the thresholds in the study front-matter. */
const MAGNITUDE_COLOR = {
  large: 'var(--success, #30a46c)',
  medium: '#d29922',
  small: '#d29922',
  negligible: 'var(--text-muted, #8b949e)',
}

function EffectCell({ p, eps2, magnitude, constant }) {
  if (constant) {
    return <td className={styles.num} title="Metric had a single value across every level — no variance to test.">
      <span className={styles.ns}>constant</span>
    </td>
  }
  const color = MAGNITUDE_COLOR[magnitude] || undefined
  return (
    <td className={styles.num} style={color ? { color } : undefined}>
      {eps2 == null ? '—' : dec(eps2, 3)}
      {magnitude && <span className={styles.effectTag}>{magnitude}</span>}
    </td>
  )
}

function PCell({ p }) {
  if (p == null) return <td className={styles.num}><span className={styles.ns}>n/a</span></td>
  const significant = p.startsWith('<') || Number(p) < 0.05
  return (
    <td className={styles.num}>
      <span className={significant ? styles.sig : styles.ns} style={{ marginLeft: 0 }}>
        {p.startsWith('<') ? p : `= ${p}`}
      </span>
    </td>
  )
}

// ── Published figures ────────────────────────────────────────────────────────
// Served byte-for-byte from the study's figures/ directory, so what's on screen is
// exactly what goes in the report. They're matplotlib output on a white canvas, so
// each sits on a white plate — that makes the light background read as a deliberate
// figure plate rather than a rendering failure on the dark page.

/** All committed figures belonging to one experiment, with captions. */
function Figures({ experiment, available }) {
  const mine = FIGURES.filter(f => f.experiment === experiment && available.includes(f.file))
  const missing = FIGURES_NOT_COMMITTED.filter(f => f.experiment === experiment)
  if (!mine.length && !missing.length) return null

  return (
    <div className={styles.figureBlock}>
      {mine.map(f => {
        const src = `/api/module1-evaluation/figure/${encodeURIComponent(f.file)}`
        return (
          <figure key={f.file} className={styles.figure}>
            {/* Opens full size in a new tab — these are dense multi-panel plots that
                need more than the column width to read properly. */}
            <a href={src} target="_blank" rel="noreferrer" className={styles.figurePlate}>
              <img src={src} alt={f.alt} className={styles.figureImg} loading="lazy" />
            </a>
            <figcaption className={styles.figureCaption}>
              <strong>{f.title}</strong> {f.caption}
            </figcaption>
          </figure>
        )
      })}
      {missing.map(m => (
        <div key={m.ids} className={styles.figureMissing}>
          <strong>{m.ids}</strong> {m.describes} — referenced by the study&apos;s artifact index but
          not committed, so nothing is shown here. Regenerate the figure or trim the index before
          citing it.
        </div>
      ))}
    </div>
  )
}

/** Small caveat callout. These are not optional garnish — the study requires each of
 *  them to appear beside the result it qualifies. */
function Caveats({ items, title = 'Read this alongside the table' }) {
  return (
    <div className={styles.caveat}>
      <div className={styles.caveatTitle}>{title}</div>
      <ul className={styles.caveatList}>
        {items.map((text, i) => <li key={i}>{text}</li>)}
      </ul>
    </div>
  )
}

export default function Module1Panel({ data, error }) {
  // Ranks Experiment A's metrics by how much they vary, so the headline reads off the
  // top of the table: tactical properties move, the room graph does not.
  const topVarying = useMemo(() => {
    if (!data?.experimentA) return []
    return Object.entries(data.experimentA.medium)
      .filter(([, s]) => s.cv != null)
      .sort((a, b) => b[1].cv - a[1].cv)
      .slice(0, 3)
      .map(([key, s]) => ({ key, label: METRIC_LABELS[key]?.label || key, cv: s.cv }))
  }, [data])

  if (error) return <div className={styles.msg}>{error}</div>
  if (!data) return <div className={styles.msg}>Loading Module 1 study results…</div>

  if (!data.available) {
    return (
      <div className={styles.msg}>
        <strong>Module 1 study output not found.</strong>
        <p style={{ margin: '.6rem 0 0' }}>{data.reason}</p>
        <p style={{ margin: '.6rem 0 0', fontSize: '.8rem', opacity: .8 }}>
          Looked in: <code>{data.resultsDir}</code>
        </p>
      </div>
    )
  }

  const { experimentA: A, experimentB: B, experimentC: C, experimentD: D, experimentE: E } = data
  const formReachable = E.scopes.find(s => s.primary)
  const figs = data.figures || []

  return (
    <>
      {/* ── Study overview ──────────────────────────────────────────────── */}
      <section className={styles.section}>
        <h2 className={styles.h2}>The study</h2>
        <p className={styles.note} style={{ marginTop: 0 }}>
          Module 1 is evaluated differently from every other tab on this page. Modules 2–4
          measure <em>people playing missions</em>, so their data is trainee sessions. Module 1
          measures <em>the generator itself</em>: {data.totalScenarios.toLocaleString()} scenarios
          generated offline across six controlled experiments, each one measured on 28 structural,
          spatial and role metrics. That is why there is no player selector and no AI-difficulty
          split here — the unit of analysis is a generated scenario.
        </p>

        <div className={styles.tileRow}>
          <div className={styles.tile}>
            <div className={styles.tileValue}>{data.totalScenarios.toLocaleString()}</div>
            <div className={styles.tileLabel}>scenarios generated</div>
          </div>
          <div className={styles.tile}>
            <div className={styles.tileValue}>{pct(formReachable?.passRate, 0)}</div>
            <div className={styles.tileLabel}>validation pass, form-reachable configs</div>
          </div>
          <div className={styles.tile}>
            <div className={styles.tileValue}>{A.pairsMedium.pairs.toLocaleString()}</div>
            <div className={styles.tileLabel}>scenario pairs compared for diversity</div>
          </div>
          <div className={styles.tile}>
            <div className={styles.tileValue}>{topVarying[0] ? pct(topVarying[0].cv) : '—'}</div>
            <div className={styles.tileLabel}>highest coefficient of variation ({topVarying[0]?.label.toLowerCase()})</div>
          </div>
        </div>

        <table className={styles.table} style={{ marginTop: '1rem' }}>
          <thead>
            <tr>
              <th>Experiment</th>
              <th>What it establishes</th>
              <th className={styles.num}>n</th>
              <th>RQ</th>
              <th>Design</th>
            </tr>
          </thead>
          <tbody>
            {EXPERIMENTS.map(x => (
              <tr key={x.id}>
                <td className={styles.rowLabel}>
                  <strong>{x.id}</strong>
                  {x.primary && <span className={styles.primaryTag}>primary</span>}
                </td>
                <td className={styles.rowLabel}>{x.label}</td>
                <td className={styles.num}>{x.n}</td>
                <td className={styles.rowLabel}>{x.rq}</td>
                <td className={styles.rowLabel} style={{ fontSize: '.8rem', opacity: .85 }}>{x.design}</td>
              </tr>
            ))}
          </tbody>
        </table>

        <p className={styles.note}>
          <strong>Production baseline:</strong> {STUDY.baseline}.{' '}
          <strong>Statistical procedure:</strong> {STUDY.omnibus}. Effect size η² for ANOVA and ε²
          for Kruskal-Wallis ({STUDY.effectThresholds}). Stack: {STUDY.statsStack}.
        </p>
        <p className={styles.note}>
          Means, standard deviations, coefficients of variation and pass rates on this tab are
          recomputed from the study&apos;s raw per-scenario CSVs each time the page loads. The
          p-values and effect sizes are the published analysis&apos; own, not re-derived here.
        </p>
      </section>

      {/* ── Research questions ──────────────────────────────────────────── */}
      <section className={styles.section}>
        <h2 className={styles.h2}>Research questions</h2>
        <div className={styles.rqGrid}>
          {RESEARCH_QUESTIONS.map(rq => (
            <div key={rq.id} className={styles.rqCard}>
              <div className={styles.rqHead}>
                <span className={styles.rqId}>{rq.id}</span>
                {rq.primary && <span className={styles.primaryTag}>primary</span>}
              </div>
              <div className={styles.rqQuestion}>{rq.question}</div>
              <div className={styles.rqAnswer}>{rq.answer}</div>
              <div className={styles.rqEvidence}>{rq.evidence}</div>
            </div>
          ))}
        </div>
      </section>

      {/* ── Experiment A — variability ──────────────────────────────────── */}
      <section className={styles.section}>
        <h2 className={styles.h2}>RQ4 — Variability across repeat generations (Experiment A)</h2>
        <p className={styles.note} style={{ marginTop: 0 }}>
          {A.n} scenarios generated from an identical production configuration, differing only in
          seed. The coefficient of variation (CV = SD ÷ mean) says how much each measured property
          moved across those repeats. The <strong>A2</strong> column repeats the whole experiment at
          high randomness, as a ceiling on what the withheld randomness control could add.
        </p>
        <p className={styles.note} style={{ marginTop: 0, marginBottom: '.9rem' }}>
          <strong>Headline:</strong> the room graph&apos;s size and connectivity are fixed by the
          parameter set — room count, door count, connectivity, cyclicity and entry points all sit
          at CV&nbsp;=&nbsp;0%. Variation is tactical, concentrated in entity placement, guard and
          patrol roles, furniture, door states and graph depth.
        </p>

        <div className={styles.scrollX}>
          <table className={styles.table}>
            <thead>
              <tr>
                <th></th>
                <th className={styles.num}>Mean</th>
                <th className={styles.num}>SD</th>
                <th className={styles.num}>CV %</th>
                <th className={styles.num}>CV % (A2, high)</th>
              </tr>
            </thead>
            <tbody>
              {METRIC_GROUPS.map(group => (
                <MetricGroupRows key={group.group} group={group} medium={A.medium} high={A.high} />
              ))}
            </tbody>
          </table>
        </div>
        <div className={styles.alphaNote}>
          n = {A.n} scenarios at medium randomness, {A.nHigh} at high. SD is the sample standard
          deviation (n − 1). A metric at CV = 0% means its per-scenario <em>count</em> never moves,
          not that the scenarios are alike: the door-state counts are invariant here, yet the
          pairwise table below shows which particular door ends up locked does vary from scenario
          to scenario.
        </div>

        <Caveats items={CAVEATS.A} />
      </section>

      {/* ── Pairwise diversity ──────────────────────────────────────────── */}
      <section className={styles.section}>
        <h2 className={styles.h2}>Pairwise structural diversity ({A.pairsMedium.pairs.toLocaleString()} pairs)</h2>
        <p className={styles.note} style={{ marginTop: 0 }}>
          Per-scenario spread shows how far a metric&apos;s <em>distribution</em> stretches; only a
          head-to-head comparison shows whether individual scenarios actually differ. Every unique
          pair in the batch is compared on seven structural measures. The A-versus-A2 columns test
          whether high randomness produces a more diverse batch — Mann-Whitney U on the two
          distributions, with rank-biserial as the effect size.
        </p>

        <div className={styles.scrollX}>
          <table className={styles.table}>
            <thead>
              <tr>
                <th></th>
                <th className={styles.num}>Mean</th>
                <th className={styles.num}>SD</th>
                <th className={styles.num}>Min</th>
                <th className={styles.num}>Max</th>
                <th className={styles.num}>Mean (high)</th>
                <th className={styles.num}>Change</th>
                <th className={styles.num}>p</th>
                <th className={styles.num}>Rank-biserial</th>
              </tr>
            </thead>
            <tbody>
              {DIVERSITY_MEASURES.map(m => {
                const med = A.pairsMedium.measures[m.key]
                const high = A.pairsHigh.measures[m.key]
                const test = A_VS_A2_TESTS[m.key]
                const change = med?.mean ? ((high.mean - med.mean) / med.mean) * 100 : null
                return (
                  <tr key={m.key}>
                    <td className={styles.rowLabel}>
                      {m.label}
                      {m.note && <span className={styles.inlineNote}>{m.note}</span>}
                    </td>
                    <td className={styles.num}>{dec(med?.mean)}{m.unit || ''}</td>
                    <td className={styles.num}>{dec(med?.sd)}</td>
                    <td className={styles.num}>{dec(med?.min)}</td>
                    <td className={styles.num}>{dec(med?.max)}</td>
                    <td className={styles.num}>{dec(high?.mean)}{m.unit || ''}</td>
                    <td className={styles.num}>{signed(change)}</td>
                    <PCell p={test?.p ?? null} />
                    <td className={styles.num}>{test ? dec(test.rankBiserial, 3) : '—'}</td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
        <p className={styles.note}>{A_VS_A2_INTERPRETATION}</p>
        <Figures experiment="A" available={figs} />
      </section>

      {/* ── Experiment B — traceability ─────────────────────────────────── */}
      <section className={styles.section}>
        <h2 className={styles.h2}>RQ1 &amp; RQ2 — Form-parameter traceability (Experiment B)</h2>
        <p className={styles.note} style={{ marginTop: 0 }}>
          Each of the six evaluator-facing form parameters is swept across its levels with
          everything else held at baseline, 50 scenarios per level ({B.n.toLocaleString()} total),
          so every row is attributable to exactly one varied parameter. The question is whether an
          evaluator&apos;s setting actually reaches the layout property it is supposed to control.
          Level means below are computed from the raw scenarios; p and ε² are the published
          Kruskal-Wallis results.
        </p>

        {!B.traceabilityAvailable && (
          <div className={styles.msg} style={{ marginBottom: '1rem' }}>
            The committed traceability matrix (<code>tables/B_SUMMARY_traceability_matrix.csv</code>)
            is missing, so effect sizes cannot be shown. Level means below are still computed from
            the raw experiment CSV.
          </div>
        )}

        {B.parameters.map(param => (
          <div key={param.parameter} className={styles.statBlock} style={{ marginBottom: '.85rem' }}>
            <div className={styles.statHead}>
              <span className={styles.statTitle}>{param.parameter}</span>
              <span className={styles.statMeta}>
                {param.levels.length} levels · n = {param.levels.reduce((a, l) => a + l.n, 0)}
              </span>
            </div>
            {!param.metrics.length ? (
              <div className={styles.ns}>No target metrics listed for this parameter.</div>
            ) : (
              <div className={styles.scrollX}>
                <table className={styles.pairTable}>
                  <thead>
                    <tr>
                      <td className={styles.rowLabel}>Target metric</td>
                      {param.levels.map(l => <td key={l.value} className={styles.num}>{l.value}</td>)}
                      <td className={styles.num}>p</td>
                      <td className={styles.num}>ε²</td>
                    </tr>
                  </thead>
                  <tbody>
                    {param.metrics.map(mt => (
                      <tr key={mt.metric}>
                        <td className={styles.rowLabel}>{METRIC_LABELS[mt.metric]?.label || mt.metric}</td>
                        {param.levels.map(l => (
                          <td key={l.value} className={styles.num}>{dec(l.metrics[mt.metric]?.mean)}</td>
                        ))}
                        <PCell p={mt.p} />
                        <EffectCell {...mt} />
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        ))}

        <Caveats items={CAVEATS.B} />
        <Figures experiment="B" available={figs} />
      </section>

      {/* ── Experiments C & D — the withheld controls ───────────────────── */}
      <section className={styles.section}>
        <h2 className={styles.h2}>RQ4 — Randomness sensitivity (Experiment C)</h2>
        <p className={styles.note} style={{ marginTop: 0 }}>
          {C.n} scenarios at a fixed configuration, 50 each at low / medium / high randomness.
          Randomness is validated here but currently fixed at <code>medium</code> in the evaluator
          form.
        </p>
        <GroupedMetricTable
          groups={C.groups}
          stats={C_FOCUS_STATS}
          groupHeader={(g) => g.value}
        />
        <p className={styles.note}>{C_INTERPRETATION}</p>
        <Figures experiment="C" available={figs} />
      </section>

      <section className={styles.section}>
        <h2 className={styles.h2}>RQ4 &amp; RQ2 — Difficulty differentiation (Experiment D)</h2>
        <p className={styles.note} style={{ marginTop: 0 }}>
          {D.n} scenarios, 50 each at difficulty 1 / 3 / 5 with randomness held at medium. Difficulty
          is likewise validated but fixed at <code>3</code> in the current form. This is the second
          line of evidence for RQ2: an identical terrorist count yields a different role mix at a
          different difficulty, so roles track the scenario&apos;s parameters rather than being
          handed out arbitrarily.
        </p>
        <GroupedMetricTable
          groups={D.groups}
          stats={D_FOCUS_STATS}
          groupHeader={(g) => `Difficulty ${g.value}`}
        />
        <p className={styles.note}>{D_INTERPRETATION}</p>
        <Figures experiment="D" available={figs} />
      </section>

      {/* ── Experiment E — validation reliability ───────────────────────── */}
      <section className={styles.section}>
        <h2 className={styles.h2}>RQ3 — Validation reliability (Experiment E)</h2>
        <p className={styles.note} style={{ marginTop: 0 }}>
          {E.n} scenarios sampled across the <em>full</em> pipeline parameter space — deliberately
          including room and terrorist counts beyond what the evaluator form can request — with the
          validation outcome of every one recorded. The split matters: the form-reachable rate
          describes the delivered system, the overall rate describes the pipeline&apos;s outer limits.
        </p>

        <table className={styles.table}>
          <thead>
            <tr>
              <th>Scope</th>
              <th className={styles.num}>n</th>
              <th className={styles.num}>Pass</th>
              <th className={styles.num}>Fail</th>
              <th className={styles.num}>Pass rate</th>
              <th style={{ width: '28%' }}></th>
            </tr>
          </thead>
          <tbody>
            {E.scopes.map(s => (
              <tr key={s.scope}>
                <td className={styles.rowLabel}>
                  {s.scope}
                  {s.primary && <span className={styles.primaryTag}>primary</span>}
                </td>
                <td className={styles.num}>{s.n}</td>
                <td className={styles.num}>{s.pass}</td>
                <td className={styles.num}>{s.fail}</td>
                <td className={styles.num} style={s.primary ? { color: 'var(--success, #30a46c)', fontWeight: 700 } : undefined}>
                  {pct(s.passRate, 2)}
                </td>
                <td>
                  <div className={styles.rateBar}>
                    <div className={styles.rateBarFill} style={{ width: `${s.passRate ?? 0}%` }} />
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>

        <div className={styles.splitCols}>
          <div>
            <h3 className={styles.h3}>Failure categories</h3>
            <table className={styles.pairTable}>
              <thead>
                <tr>
                  <td className={styles.rowLabel}>Category</td>
                  <td className={styles.num}>Count</td>
                  <td className={styles.num}>% of failures</td>
                </tr>
              </thead>
              <tbody>
                {E.failures.map(f => (
                  <tr key={f.category}>
                    <td className={styles.rowLabel}>{f.category}</td>
                    <td className={styles.num}>{f.count}</td>
                    <td className={styles.num}>{pct(f.pctOfFailures)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
            <div className={styles.alphaNote}>Every failure falls outside the form-reachable range.</div>
          </div>

          <div>
            <h3 className={styles.h3}>Seed retries</h3>
            <table className={styles.pairTable}>
              <thead>
                <tr>
                  <td className={styles.rowLabel}>Retries needed</td>
                  <td className={styles.num}>Scenarios</td>
                </tr>
              </thead>
              <tbody>
                {E.retries.map(r => (
                  <tr key={r.retries}>
                    <td className={styles.rowLabel}>
                      {r.retries === 0 ? 'Passed first time' : `${r.retries} retr${r.retries === 1 ? 'y' : 'ies'}`}
                    </td>
                    <td className={styles.num}>{r.scenarios}</td>
                  </tr>
                ))}
              </tbody>
            </table>
            <div className={styles.alphaNote}>
              The pipeline retries a failed scenario with seed + 1 (budget 3). Without this column a
              bare pass rate would report &ldquo;passed <em>eventually</em>&rdquo;.
            </div>
          </div>
        </div>

        <h3 className={styles.h3} style={{ marginTop: '1.25rem' }}>Where reliability degrades</h3>
        <p className={styles.note} style={{ marginTop: 0 }}>
          Pass rate broken down by the parameters the form constrains. Each breakdown is{' '}
          <strong>marginal</strong> — it varies one parameter while every other parameter still
          ranges across the full pipeline space — which is why levels marked in-range here sit below
          100%. The 100% headline requires <em>all</em> the form&apos;s constraints to hold at once
          (3–5 rooms <em>and</em> 1–4 terrorists); these columns instead show how each parameter
          degrades on its own. The worst case is a high terrorist count crowded into small rooms.
        </p>
        <div className={styles.splitCols}>
          <PassRateBreakdown title="By room count" rows={E.byRoomCount} inRange={(v) => Number(v) >= 3 && Number(v) <= 5} />
          <PassRateBreakdown title="By terrorist count" rows={E.byTerroristCount} inRange={(v) => Number(v) >= 1 && Number(v) <= 4} />
        </div>
        <div className={styles.splitCols} style={{ marginTop: '1rem' }}>
          <PassRateBreakdown title="By layout type" rows={E.byLayoutType} inRange={() => true} />
        </div>

        <Caveats items={CAVEATS.E} />
        <Figures experiment="E" available={figs} />
      </section>
    </>
  )
}

/** One metric group's rows in the Experiment A variability table. */
function MetricGroupRows({ group, medium, high }) {
  // A group where every metric is invariant is the finding, not an empty row — flag it
  // in the group header so it's stated rather than left to be inferred. The tag is kept
  // purely factual ("all CV = 0%") rather than "deterministic": a zero CV means the
  // per-scenario COUNT never moves, which is not the same as the scenarios being alike
  // — see the note under the table.
  const allZero = group.metrics.every(m => medium[m.key]?.constant)
  return (
    <>
      <tr className={styles.groupRow}>
        <td colSpan={5}>
          {group.group}
          {allZero && <span className={styles.deterministicTag}>all CV = 0%</span>}
        </td>
      </tr>
      {group.metrics.map(m => {
        const s = medium[m.key]
        const h = high[m.key]
        const invariant = s?.constant
        return (
          <tr key={m.key}>
            <td className={styles.rowLabel} style={invariant ? { opacity: .65 } : undefined}>
              {m.label}
            </td>
            <td className={styles.num}>{dec(s?.mean, 3)}{m.unit || ''}</td>
            <td className={styles.num}>{dec(s?.sd, 3)}</td>
            <td className={styles.num} style={invariant ? { color: 'var(--text-muted, #8b949e)' } : { fontWeight: 600 }}>
              {pct(s?.cv)}
            </td>
            <td className={styles.num} style={{ color: 'var(--text-muted, #8b949e)' }}>{pct(h?.cv)}</td>
          </tr>
        )
      })}
    </>
  )
}

/** Metric rows × experiment groups (randomness levels / difficulty levels), joined to
 *  the published test statistic for each metric. */
function GroupedMetricTable({ groups, stats, groupHeader }) {
  return (
    <div className={styles.scrollX}>
      <table className={styles.table}>
        <thead>
          <tr>
            <th></th>
            {groups.map(g => <th key={g.value} className={styles.num}>{groupHeader(g)}<span className={styles.nBadge}> n={g.n}</span></th>)}
            <th className={styles.num}>Test</th>
            <th className={styles.num}>p</th>
            <th className={styles.num}>ε²</th>
          </tr>
        </thead>
        <tbody>
          {stats.map(stat => (
            <tr key={stat.key}>
              <td className={styles.rowLabel}>{METRIC_LABELS[stat.key]?.label || stat.key}</td>
              {groups.map(g => (
                <td key={g.value} className={styles.num}>{dec(g.metrics[stat.key]?.mean, 3)}</td>
              ))}
              <td className={styles.num} style={{ fontSize: '.78rem', color: 'var(--text-muted, #8b949e)' }}>{stat.test}</td>
              <PCell p={stat.p} />
              <EffectCell p={stat.p} eps2={stat.eps2} magnitude={stat.magnitude} constant={stat.test === 'constant'} />
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

/** Pass rate per level of one config parameter, with out-of-form-range levels marked. */
function PassRateBreakdown({ title, rows, inRange }) {
  return (
    <div>
      <h3 className={styles.h3}>{title}</h3>
      <table className={styles.pairTable}>
        <thead>
          <tr>
            <td className={styles.rowLabel}>Level</td>
            <td className={styles.num}>n</td>
            <td className={styles.num}>Pass rate</td>
          </tr>
        </thead>
        <tbody>
          {rows.map(r => {
            const within = inRange(r.value)
            return (
              <tr key={r.value}>
                <td className={styles.rowLabel} style={within ? undefined : { opacity: .6 }}>
                  {r.value}
                  {!within && <span className={styles.outOfRangeTag}>outside form</span>}
                </td>
                <td className={styles.num}>{r.n}</td>
                <td
                  className={styles.num}
                  style={{ color: r.passRate === 100 ? 'var(--success, #30a46c)' : r.passRate < 80 ? '#f87171' : undefined, fontWeight: 600 }}
                >
                  {pct(r.passRate)}
                </td>
              </tr>
            )
          })}
        </tbody>
      </table>
    </div>
  )
}
