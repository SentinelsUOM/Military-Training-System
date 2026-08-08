import { NextResponse } from 'next/server'
import fs from 'fs/promises'
import path from 'path'
import { METRIC_GROUPS, DIVERSITY_MEASURES, FIGURE_FILES } from '@/lib/module1Results'
import { RESULTS_DIR, FIGURES_SUBDIR, TABLES_SUBDIR } from './resultsDir'

// Reads the filesystem, so it can never be statically evaluated at build time.
export const dynamic = 'force-dynamic'

// ── Module 1 evaluation dataset ──────────────────────────────────────────────
// Unlike /api/evaluation (which reads trainee Sessions out of MongoDB), Module 1's
// evaluation is an OFFLINE 2000-scenario batch study: Unity's BatchEvaluationRunner
// writes one CSV per experiment, and those CSVs are the ground truth. This route
// parses them and computes descriptive statistics on the fly.
//
// Reading the study output directly — rather than transcribing a snapshot into JS —
// means re-running the batch runner updates this page with no code change, and every
// number rendered is traceable to a committed CSV row. Inferential statistics (p, ε²)
// are NOT computed here; see the sourcing rule in lib/module1Results.js.

const FILES = {
  A:          'ExperimentA_Variability_Medium.csv',
  A2:         'ExperimentA2_Variability_High.csv',
  APairs:     'ExperimentA_PairwiseDiversity_Medium.csv',
  A2Pairs:    'ExperimentA2_PairwiseDiversity_High.csv',
  B:          'ExperimentB_FormParameters.csv',
  C:          'ExperimentC_RandomnessSensitivity.csv',
  D:          'ExperimentD_DifficultyDifferentiation.csv',
  E:          'ExperimentE_ValidationReliability.csv',
  BSummary:   path.join(TABLES_SUBDIR, 'B_SUMMARY_traceability_matrix.csv'),
}

// Which published figures are actually on disk. MODULE1_RESULTS.md's artifact index
// references more figures than were committed, so the UI needs to know what exists
// rather than rendering a broken image for the rest.
async function availableFigures() {
  const present = await Promise.all(FIGURE_FILES.map(async file => {
    try {
      await fs.access(path.join(RESULTS_DIR, FIGURES_SUBDIR, file))
      return file
    } catch {
      return null
    }
  }))
  return present.filter(Boolean)
}

const METRIC_KEYS = METRIC_GROUPS.flatMap(g => g.metrics.map(m => m.key))
const DIVERSITY_KEYS = DIVERSITY_MEASURES.map(m => m.key)

// ── CSV parsing ──────────────────────────────────────────────────────────────
// Hand-rolled rather than pulling in a dependency: the runner's own CsvField()
// escaping is plain RFC-4180 (quote a field containing a comma/quote/newline,
// double an embedded quote), which this mirrors exactly. validationWarnings is the
// column that actually needs it — it joins multiple warnings with "; " and the
// warning text itself contains commas.
function splitCsvLine(line) {
  const out = []
  let field = ''
  let inQuotes = false

  for (let i = 0; i < line.length; i++) {
    const ch = line[i]
    if (inQuotes) {
      if (ch !== '"') { field += ch; continue }
      // A doubled quote inside a quoted field is a literal quote, not the end of it.
      if (line[i + 1] === '"') { field += '"'; i++ } else { inQuotes = false }
    } else if (ch === '"') {
      inQuotes = true
    } else if (ch === ',') {
      out.push(field); field = ''
    } else {
      field += ch
    }
  }
  out.push(field)
  return out
}

function parseCsv(text) {
  // The runner writes UTF-8 without a BOM, but strip one defensively — a BOM left on
  // the first header cell would silently break every lookup of that first column.
  const clean = text.charCodeAt(0) === 0xfeff ? text.slice(1) : text
  const lines = clean.split(/\r?\n/).filter(l => l.length > 0)
  if (!lines.length) return { header: [], rows: [] }

  const header = splitCsvLine(lines[0])
  const rows = lines.slice(1).map(line => {
    const cells = splitCsvLine(line)
    const row = {}
    header.forEach((h, i) => { row[h] = cells[i] ?? '' })
    return row
  })
  return { header, rows }
}

async function readCsv(fileName) {
  const text = await fs.readFile(path.join(RESULTS_DIR, fileName), 'utf8')
  return parseCsv(text)
}

// Missing optional files (the committed tables/ dir may be partial) shouldn't take
// the whole tab down — the sections that depend on them degrade individually.
async function readCsvOptional(fileName) {
  try {
    return await readCsv(fileName)
  } catch {
    return null
  }
}

const num = (v) => {
  if (v == null || v === '') return null
  const n = Number(v)
  return Number.isFinite(n) ? n : null
}

// ── Descriptive statistics ───────────────────────────────────────────────────
function describe(values) {
  const nums = values.filter(v => typeof v === 'number' && Number.isFinite(v))
  const n = nums.length
  if (!n) return { n: 0, mean: null, sd: null, cv: null, min: null, max: null }

  const min = Math.min(...nums)
  const max = Math.max(...nums)

  // An invariant metric is the headline finding of Experiment A, so it has to be
  // detected exactly rather than inferred from the arithmetic. Summing 100 copies of
  // 0.3333 leaves ~1e-16 of floating-point residue, which would give lockedDoorFraction
  // a CV of ~3e-14 — displayed as "0.0%" but failing every `cv === 0` test downstream,
  // so the row would render as if it varied. Comparing the raw values sidesteps that.
  const constant = min === max
  const mean = constant ? nums[0] : nums.reduce((a, b) => a + b, 0) / n
  // Sample SD (n − 1), matching pandas' default ddof=1 — using the population
  // formula would make every CV in the table disagree with MODULE1_RESULTS.md.
  const sd = constant || n <= 1
    ? 0
    : Math.sqrt(nums.reduce((a, b) => a + (b - mean) ** 2, 0) / (n - 1))
  // CV is undefined at a zero mean (division by zero). A constant-zero column —
  // doorsOpen at the 4-room baseline is the real case — is genuinely invariant, so
  // report CV 0 rather than null; a non-zero spread about a zero mean has no
  // meaningful CV and is reported as null.
  const cv = constant ? 0 : (mean !== 0 ? Math.abs(sd / mean) * 100 : null)

  return { n, mean, sd, cv, min, max, constant }
}

/** Describes each metric key over a set of rows. */
function describeMetrics(rows, keys) {
  const out = {}
  for (const key of keys) out[key] = describe(rows.map(r => num(r[key])))
  return out
}

/** Groups rows by a column, preserving first-seen order, then describes each group. */
function describeByGroup(rows, groupColumn, keys) {
  const groups = new Map()
  for (const row of rows) {
    const g = row[groupColumn]
    if (!groups.has(g)) groups.set(g, [])
    groups.get(g).push(row)
  }
  return [...groups.entries()].map(([value, groupRows]) => ({
    value,
    n: groupRows.length,
    metrics: describeMetrics(groupRows, keys),
  }))
}

// ── Experiment E ─────────────────────────────────────────────────────────────
const passed = (row) => String(row.validationPassed).toLowerCase() === 'true'

function passRateOf(rows) {
  const pass = rows.filter(passed).length
  return { n: rows.length, pass, fail: rows.length - pass, passRate: rows.length ? (pass / rows.length) * 100 : null }
}

/** Pass rate per distinct value of a column, sorted numerically when possible. */
function passRateBy(rows, column) {
  const groups = new Map()
  for (const row of rows) {
    const key = row[column]
    if (!groups.has(key)) groups.set(key, [])
    groups.get(key).push(row)
  }
  return [...groups.entries()]
    .map(([value, groupRows]) => ({ value, ...passRateOf(groupRows) }))
    .sort((a, b) => {
      const na = Number(a.value), nb = Number(b.value)
      return Number.isFinite(na) && Number.isFinite(nb) ? na - nb : String(a.value).localeCompare(String(b.value))
    })
}

function analyseExperimentE(rows) {
  const withinForm = rows.filter(r => String(r.withinFormRange).toLowerCase() === 'true')
  const extended   = rows.filter(r => String(r.withinFormRange).toLowerCase() !== 'true')

  // Failure categories, from the runner's own ClassifyFailure() column — "none" is
  // the pass marker, so it's excluded here rather than shown as a failure mode.
  const failureCounts = new Map()
  for (const row of rows) {
    if (passed(row)) continue
    const category = row.failureCategory || 'unknown'
    failureCounts.set(category, (failureCounts.get(category) || 0) + 1)
  }
  const totalFailures = [...failureCounts.values()].reduce((a, b) => a + b, 0)
  const failures = [...failureCounts.entries()]
    .map(([category, count]) => ({ category, count, pctOfFailures: totalFailures ? (count / totalFailures) * 100 : null }))
    .sort((a, b) => b.count - a.count)

  // The pipeline silently retries a failed scenario (budget 3), so a bare pass rate
  // would report "passed EVENTUALLY". seedRetries splits first-time passes from
  // eventual ones — one of the four methodological safeguards the study reports.
  const retryCounts = new Map()
  for (const row of rows) {
    const r = num(row.seedRetries)
    if (r == null) continue
    retryCounts.set(r, (retryCounts.get(r) || 0) + 1)
  }
  const retries = [...retryCounts.entries()]
    .map(([count, scenarios]) => ({ retries: count, scenarios }))
    .sort((a, b) => a.retries - b.retries)

  return {
    scopes: [
      { scope: 'Form-reachable (roomCount 3–5, terrorists 1–4)', primary: true,  ...passRateOf(withinForm) },
      { scope: 'Extended range (outside the form)',              primary: false, ...passRateOf(extended) },
      { scope: 'Overall (full pipeline space)',                  primary: false, ...passRateOf(rows) },
    ],
    failures,
    retries,
    byRoomCount:      passRateBy(rows, 'configRoomCount'),
    byTerroristCount: passRateBy(rows, 'configTerroristCount'),
    byLayoutType:     passRateBy(rows, 'layoutType'),
  }
}

// ── Experiment B ─────────────────────────────────────────────────────────────
// The committed traceability matrix supplies the (parameter, metric) pairs worth
// showing and their published p / ε². Per-level MEANS for exactly those pairs are
// computed here from the raw CSV, so the table joins "which levels differ" (live
// arithmetic) to "is that difference significant" (the published analysis).
function parseTraceabilityMatrix(summary) {
  if (!summary) return []
  const effectColumn = summary.header.find(h => h.startsWith('p |')) || summary.header[2]

  return summary.rows.map(row => {
    const raw = row[effectColumn] || ''
    const [pRaw = '', effectRaw = ''] = raw.split('|').map(s => s.trim())
    const magnitude = effectRaw.match(/\(([^)]+)\)/)?.[1] ?? null
    const eps2 = num(effectRaw.replace(/\s*\([^)]*\)/, '').trim())
    return {
      parameter: row.parameter,
      metric: row.metric,
      // "n/a" marks a metric that was constant across every level — no test was run.
      p: pRaw === 'n/a' || pRaw === '' ? null : pRaw,
      eps2,
      magnitude,
      constant: pRaw === 'n/a',
    }
  })
}

function analyseExperimentB(rows, traceability) {
  // Per varied parameter: its levels in the order the runner swept them, plus the
  // level means of the metrics the traceability matrix flags for that parameter.
  const byParameter = new Map()
  for (const row of rows) {
    const param = row.variedParameter
    if (!byParameter.has(param)) byParameter.set(param, new Map())
    const levels = byParameter.get(param)
    if (!levels.has(row.parameterValue)) levels.set(row.parameterValue, [])
    levels.get(row.parameterValue).push(row)
  }

  return [...byParameter.entries()].map(([parameter, levels]) => {
    const entries = traceability.filter(t => t.parameter === parameter)
    // Fall back to the swept parameter's own echo columns if the matrix is missing,
    // so the section still renders something truthful without tables/.
    const metricKeys = entries.length ? [...new Set(entries.map(e => e.metric))] : []

    return {
      parameter,
      levels: [...levels.entries()].map(([value, levelRows]) => ({
        value,
        n: levelRows.length,
        metrics: describeMetrics(levelRows, metricKeys),
      })),
      metrics: entries,
    }
  })
}

// ── Pairwise diversity ───────────────────────────────────────────────────────
function analysePairs(rows) {
  return { pairs: rows.length, measures: describeMetrics(rows, DIVERSITY_KEYS) }
}

export async function GET() {
  try {
    const [a, a2, aPairs, a2Pairs, b, c, d, e, bSummary, figures] = await Promise.all([
      readCsv(FILES.A),
      readCsv(FILES.A2),
      readCsv(FILES.APairs),
      readCsv(FILES.A2Pairs),
      readCsv(FILES.B),
      readCsv(FILES.C),
      readCsv(FILES.D),
      readCsv(FILES.E),
      readCsvOptional(FILES.BSummary),
      availableFigures(),
    ])

    const traceability = parseTraceabilityMatrix(bSummary)

    return NextResponse.json({
      available: true,
      resultsDir: RESULTS_DIR,
      figures,
      totalScenarios: a.rows.length + a2.rows.length + b.rows.length + c.rows.length + d.rows.length + e.rows.length,
      experimentA: {
        n: a.rows.length,
        medium: describeMetrics(a.rows, METRIC_KEYS),
        high:   describeMetrics(a2.rows, METRIC_KEYS),
        nHigh:  a2.rows.length,
        pairsMedium: analysePairs(aPairs.rows),
        pairsHigh:   analysePairs(a2Pairs.rows),
      },
      experimentB: {
        n: b.rows.length,
        parameters: analyseExperimentB(b.rows, traceability),
        traceabilityAvailable: traceability.length > 0,
      },
      experimentC: { n: c.rows.length, groups: describeByGroup(c.rows, 'randomnessBatch', METRIC_KEYS) },
      experimentD: { n: d.rows.length, groups: describeByGroup(d.rows, 'difficultyBatch', METRIC_KEYS) },
      experimentE: { n: e.rows.length, ...analyseExperimentE(e.rows) },
    })
  } catch (err) {
    console.error('[GET /api/module1-evaluation]', err)

    // A missing results directory is the ordinary case on a machine that has the
    // dashboard but not the Unity study output — report it as "no data" with the
    // path we looked in, not as a server fault the user can't act on.
    if (err.code === 'ENOENT') {
      return NextResponse.json({
        available: false,
        resultsDir: RESULTS_DIR,
        reason: 'Module 1 evaluation CSVs not found. Run the Unity BatchEvaluationRunner (Run All Experiments), or set MODULE1_RESULTS_DIR to the directory holding the exported results.',
      })
    }
    return NextResponse.json({ error: err.message }, { status: 500 })
  }
}
