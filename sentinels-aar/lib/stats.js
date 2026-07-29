// Non-parametric significance tests for the Module-2 ablation study.
//
// The design is repeated-measures: the SAME participant plays all three AI levels
// (Basic / Intermediate / Advanced) and rates each. The ratings are ordinal (1–5 /
// 1–7 scales), the samples are small, and they're related — so the correct tests are:
//   • Friedman         — is there ANY difference across the 3 related conditions?
//   • Wilcoxon signed-rank — which specific PAIR of conditions differs (post-hoc)?
//
// Everything here is self-contained (no external stats dependency) and runs in the
// browser on the already-fetched per-player data.

// ── helpers ────────────────────────────────────────────────────────────────

// Rank an array ascending, assigning the average rank to ties (1-based).
function averageRanks(values) {
  const order = values.map((v, i) => ({ v, i })).sort((a, b) => a.v - b.v)
  const ranks = new Array(values.length)
  let i = 0
  while (i < order.length) {
    let j = i
    while (j + 1 < order.length && order[j + 1].v === order[i].v) j++
    const avg = (i + j) / 2 + 1 // mean of ranks (i+1) … (j+1)
    for (let k = i; k <= j; k++) ranks[order[k].i] = avg
    i = j + 1
  }
  return ranks
}

// Standard normal CDF via an erf approximation (Abramowitz & Stegun 7.1.26).
function normalCdf(z) {
  const s = z < 0 ? -1 : 1
  const x = Math.abs(z) / Math.SQRT2
  const t = 1 / (1 + 0.3275911 * x)
  const y = 1 - (((((1.061405429 * t - 1.453152027) * t) + 1.421413741) * t - 0.284496736) * t + 0.254829592) * t * Math.exp(-x * x)
  return 0.5 * (1 + s * y)
}

// Chi-square survival function P(X > x). df = 2 (always, for 3 conditions) has an
// exact closed form; a Wilson–Hilferty approximation covers other df for safety.
function chiSquareSf(x, df) {
  if (x <= 0) return 1
  if (df === 2) return Math.exp(-x / 2)
  const t = Math.pow(x / df, 1 / 3)
  const m = 1 - 2 / (9 * df)
  const s = Math.sqrt(2 / (9 * df))
  return 1 - normalCdf((t - m) / s)
}

// ── Friedman test ──────────────────────────────────────────────────────────
// matrix: rows = participants, cols = conditions (in a fixed order).
// Returns { n, k, chi2, df, p, rankSums, kendallW } or null if too little data.
export function friedman(matrix) {
  const rows = matrix.filter(r => r.every(v => typeof v === 'number' && !Number.isNaN(v)))
  const n = rows.length
  if (n < 2) return null
  const k = rows[0].length
  const rankSums = new Array(k).fill(0)
  for (const row of rows) {
    const r = averageRanks(row)
    for (let j = 0; j < k; j++) rankSums[j] += r[j]
  }
  const sumSq = rankSums.reduce((a, b) => a + b * b, 0)
  const chi2 = (12 / (n * k * (k + 1))) * sumSq - 3 * n * (k + 1)
  const df = k - 1
  const p = chiSquareSf(chi2, df)
  const kendallW = chi2 / (n * (k - 1)) // effect size, 0..1
  return { n, k, chi2, df, p, rankSums, kendallW }
}

// ── Wilcoxon signed-rank test (paired, two-sided) ──────────────────────────
// a, b: equal-length arrays of paired values. Returns { n, wPlus, p, z, r }.
// Exact permutation p-value for small n (≤ 20); normal approximation above that.
export function wilcoxonSignedRank(a, b) {
  const diffs = []
  for (let i = 0; i < a.length; i++) {
    const da = a[i], db = b[i]
    if (typeof da !== 'number' || typeof db !== 'number' || Number.isNaN(da) || Number.isNaN(db)) continue
    const d = da - db
    if (d !== 0) diffs.push(d) // zeros are dropped (standard Wilcoxon)
  }
  const n = diffs.length
  if (n === 0) return { n: 0, wPlus: 0, p: 1, z: 0, r: 0 }

  const ranks = averageRanks(diffs.map(Math.abs))
  let wPlus = 0
  const total = ranks.reduce((s, r) => s + r, 0)
  diffs.forEach((d, i) => { if (d > 0) wPlus += ranks[i] })

  // z + effect size (from the normal approximation, used for reporting either way)
  const meanW = total / 2
  const sd = Math.sqrt(ranks.reduce((s, r) => s + r * r, 0) / 4)
  const z = sd === 0 ? 0 : (wPlus - meanW) / sd
  const r = Math.abs(z) / Math.sqrt(n)

  let p
  if (n <= 20) {
    // Exact: every rank is equally likely to carry a + or − sign under H0.
    const center = total / 2
    const obsDev = Math.abs(wPlus - center)
    const N = 1 << n
    let count = 0
    for (let mask = 0; mask < N; mask++) {
      let s = 0
      for (let i = 0; i < n; i++) if (mask & (1 << i)) s += ranks[i]
      if (Math.abs(s - center) >= obsDev - 1e-9) count++
    }
    p = count / N
  } else {
    p = 2 * (1 - normalCdf(Math.abs(z)))
  }
  return { n, wPlus, p: Math.min(1, p), z, r }
}

// ── convenience: run Friedman + all pairwise Wilcoxon for one measure ───────
// series: array of { label, values: number[] } in condition order (length k).
// Only participants with a value in EVERY condition are used (complete cases).
export function analyzeMeasure(series) {
  const k = series.length
  const nParticipants = series[0]?.values.length ?? 0
  const matrix = []
  for (let p = 0; p < nParticipants; p++) {
    const row = series.map(s => s.values[p])
    if (row.every(v => typeof v === 'number' && !Number.isNaN(v))) matrix.push(row)
  }
  const fr = friedman(matrix)

  const pairs = []
  for (let i = 0; i < k; i++) {
    for (let j = i + 1; j < k; j++) {
      const a = [], b = []
      for (const row of matrix) { a.push(row[i]); b.push(row[j]) }
      pairs.push({ i, j, labelA: series[i].label, labelB: series[j].label, ...wilcoxonSignedRank(b, a) })
      // note: wilcoxon(b, a) so a positive wPlus means condition j > condition i
    }
  }
  // Bonferroni-corrected alpha for the 3 pairwise tests.
  const alpha = 0.05 / Math.max(1, pairs.length)
  return { n: matrix.length, friedman: fr, pairs, alpha }
}
