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

// ── One-way ANOVA (F-test) ──────────────────────────────────────────────────
// Standard parametric between-groups test: does the mean differ across groups (e.g.
// players), given the variability WITHIN each group? Needs the regularized incomplete
// beta function to get an exact F-distribution p-value — implemented below via the
// standard continued-fraction method (Numerical Recipes §6.4), not approximated.

function logGamma(x) {
  const cof = [76.18009172947146, -86.50532032941677, 24.01409824083091,
    -1.231739572450155, 0.1208650973866179e-2, -0.5395239384953e-5]
  let y = x, tmp = x + 5.5
  tmp -= (x + 0.5) * Math.log(tmp)
  let ser = 1.000000000190015
  for (let j = 0; j < 6; j++) { y += 1; ser += cof[j] / y }
  return -tmp + Math.log(2.5066282746310005 * ser / x)
}

// Continued fraction for the incomplete beta function (Numerical Recipes betacf).
function betacf(x, a, b) {
  const MAXIT = 200, EPS = 3e-9, FPMIN = 1e-30
  const qab = a + b, qap = a + 1, qam = a - 1
  let c = 1, d = 1 - qab * x / qap
  if (Math.abs(d) < FPMIN) d = FPMIN
  d = 1 / d
  let h = d
  for (let m = 1; m <= MAXIT; m++) {
    const m2 = 2 * m
    let aa = m * (b - m) * x / ((qam + m2) * (a + m2))
    d = 1 + aa * d; if (Math.abs(d) < FPMIN) d = FPMIN
    c = 1 + aa / c; if (Math.abs(c) < FPMIN) c = FPMIN
    d = 1 / d
    h *= d * c
    aa = -(a + m) * (qab + m) * x / ((a + m2) * (qap + m2))
    d = 1 + aa * d; if (Math.abs(d) < FPMIN) d = FPMIN
    c = 1 + aa / c; if (Math.abs(c) < FPMIN) c = FPMIN
    d = 1 / d
    const del = d * c
    h *= del
    if (Math.abs(del - 1) < EPS) break
  }
  return h
}

// Regularized incomplete beta function I_x(a, b).
function betai(a, b, x) {
  if (x <= 0) return 0
  if (x >= 1) return 1
  const bt = Math.exp(logGamma(a + b) - logGamma(a) - logGamma(b) + a * Math.log(x) + b * Math.log(1 - x))
  return x < (a + 1) / (a + b + 2)
    ? bt * betacf(x, a, b) / a
    : 1 - bt * betacf(1 - x, b, a) / b
}

// F-distribution survival function P(X > F) for df1, df2 degrees of freedom.
function fDistSf(F, df1, df2) {
  if (F <= 0) return 1
  const x = df2 / (df2 + df1 * F)
  return betai(df2 / 2, df1 / 2, x)
}

// One-way ANOVA. groups: [{ label, values: number[] }] — e.g. one group per player, each
// player's sessions as that group's replicates. Returns the full ANOVA-table breakdown
// (Between/Within/Total: SS, df, MS), the F-statistic, its exact p-value, and each
// group's own n/mean — or null if fewer than 2 groups have data.
export function oneWayAnova(groups) {
  const valid = groups
    .map(g => ({ label: g.label, values: (g.values || []).filter(v => typeof v === 'number' && !Number.isNaN(v)) }))
    .filter(g => g.values.length > 0)
  const k = valid.length
  const N = valid.reduce((s, g) => s + g.values.length, 0)
  if (k < 2) return null

  const grandMean = valid.flatMap(g => g.values).reduce((a, b) => a + b, 0) / N

  let ssBetween = 0, ssWithin = 0
  const groupStats = valid.map(g => {
    const n = g.values.length
    const mean = g.values.reduce((a, b) => a + b, 0) / n
    ssBetween += n * (mean - grandMean) ** 2
    ssWithin += g.values.reduce((s, v) => s + (v - mean) ** 2, 0)
    return { label: g.label, n, mean }
  })

  const dfBetween = k - 1
  const dfWithin = N - k
  const msBetween = ssBetween / dfBetween
  const msWithin = dfWithin > 0 ? ssWithin / dfWithin : null
  const F = msWithin != null && msWithin > 0 ? msBetween / msWithin : null
  const p = F != null && dfWithin > 0 ? fDistSf(F, dfBetween, dfWithin) : null

  return {
    k, N, grandMean, groupStats,
    ssBetween, ssWithin, ssTotal: ssBetween + ssWithin,
    dfBetween, dfWithin, msBetween, msWithin, F, p,
  }
}

// ── One-sample Wilcoxon signed-rank test ───────────────────────────────────
// Is this sample of values systematically different from a fixed constant (e.g. a
// published expert benchmark)? Reuses the paired test above by pairing every value
// against an array filled with the constant — the paired-difference machinery is
// identical, it's a one-sample test in disguise. Returns { n, wPlus, p, z, r }.
export function oneSampleWilcoxon(values, constant) {
  const nums = values.filter(v => typeof v === 'number' && !Number.isNaN(v))
  return wilcoxonSignedRank(nums, nums.map(() => constant))
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
