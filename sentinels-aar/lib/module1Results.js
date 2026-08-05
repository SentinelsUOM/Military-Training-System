// ── Module 1 (Dynamic Scenario Generation) — evaluation study constants ─────────
//
// Module 1's evaluation is NOT session data. Where Modules 2/3/4 measure people
// playing missions (MongoDB `Session` documents), Module 1 measures the GENERATOR:
// a 2000-scenario offline batch study run from Unity's BatchEvaluationRunner, whose
// raw per-scenario CSVs live under
//   Assets/Module1_DataModels_and_IO/Output/EvaluationResults/
// That's why the Module 1 tab has no player selector, no AI-difficulty split and no
// live telemetry — its unit of analysis is a generated scenario, not a trainee.
//
// SOURCING RULE (from Module1_Report_Handover_Brief.md, §0 rule 1):
//   "No number appears in the report unless it is in MODULE1_RESULTS.md or a
//    committed CSV."
// This dashboard holds to the same rule, split two ways:
//
//   * DESCRIPTIVE statistics (mean, SD, CV%, pass rates, per-level group means,
//     failure counts) are computed live from the raw experiment CSVs by
//     app/api/module1-evaluation/route.js. Those are unambiguous arithmetic — they
//     come out identical in JS and in the pandas analysis, so recomputing them is
//     safe and keeps the page honest if the study is ever re-run.
//
//   * INFERENTIAL statistics (p-values, ε², rank-biserial) are transcribed verbatim
//     BELOW from MODULE1_RESULTS.md, NOT recomputed. The published analysis used
//     Shapiro-Wilk → Kruskal-Wallis + Dunn with Bonferroni correction (scipy +
//     scikit-posthocs); a hand-rolled JS reimplementation would risk quoting numbers
//     that disagree with the report by a decimal place, and the report is the citable
//     artifact. Experiment B is the exception — its effect sizes are read from the
//     committed tables/B_SUMMARY_traceability_matrix.csv instead of being copied here,
//     so that table stays the single source for its own numbers.
//
// If the study is re-run, the descriptives update themselves; the constants below
// must be re-transcribed from the regenerated MODULE1_RESULTS.md by hand.

/** Study-level facts, from MODULE1_RESULTS.md front-matter. */
export const STUDY = {
  totalScenarios: 2000,
  statsStack: 'pandas · scipy · scikit-posthocs',
  omnibus: 'Shapiro-Wilk per group → one-way ANOVA + Tukey HSD if all groups normal, else Kruskal-Wallis + Dunn (Bonferroni)',
  effectThresholds: 'negligible < 0.01 · small ≥ 0.01 · medium ≥ 0.06 · large ≥ 0.14',
  baseline: 'branching layout · 4 rooms · medium room size · single entry · 3 terrorists · dispersed placement · difficulty 3 · randomness medium · hostage risk medium',
}

/** The six experiments, in report order. `n` is the designed sample size. */
export const EXPERIMENTS = [
  { id: 'A',  n: 100, rq: 'RQ4', primary: true,  design: '100 scenarios from an identical production baseline; 4950 unique pairs', label: 'Variability at production settings' },
  { id: 'A2', n: 100, rq: 'RQ4', primary: false, design: 'Experiment A repeated at high randomness, as a diversity ceiling', label: 'Variability ceiling (high randomness)' },
  { id: 'B',  n: 1000, rq: 'RQ1, RQ2', primary: true, design: 'each of 6 form parameters varied in isolation, 50 scenarios per level', label: 'Form-parameter traceability' },
  { id: 'C',  n: 150, rq: 'RQ4', primary: false, design: '50 each at low / medium / high randomness, fixed config', label: 'Randomness sensitivity' },
  { id: 'D',  n: 150, rq: 'RQ4, RQ2', primary: false, design: '50 each at difficulty 1 / 3 / 5', label: 'Difficulty differentiation' },
  { id: 'E',  n: 500, rq: 'RQ3, RQ1', primary: true, design: '500 scenarios across the full pipeline parameter space, split by whether the config is reachable through the evaluator form', label: 'Validation reliability' },
]

/** RQ → what answers it. Verbatim from MODULE1_RESULTS.md §RQ_MAPPING / §DIRECT_ANSWERS. */
export const RESEARCH_QUESTIONS = [
  {
    id: 'RQ1',
    question: 'Can evaluator-defined parameters be translated into valid, varied indoor layouts with correct spatial structure?',
    evidence: 'Exp. B (traceability) · Exp. E (spatial validity)',
    answer: 'Every structural form parameter maps to its intended layout metrics with large, significant effects, and the resulting layouts pass spatial validity 100% of the time within the form-reachable range.',
  },
  {
    id: 'RQ2',
    question: 'Can NPC roles and navigation context be derived meaningfully from scenario structure rather than assigned arbitrarily?',
    evidence: 'Exp. B (guard/patrol metrics) · Exp. D (role reconfiguration)',
    // Deliberately weaker than MODULE1_RESULTS.md §B_notes, which claims role assignment is
    // "deterministic + monotonic" in terrorist count. Patrol metrics are (ε² = 1.000), but the
    // stationary/roaming split is neither — see CAVEATS.B. The RQ2 conclusion survives either
    // way: what the split tracks is layout structure, which is exactly the claim being made.
    answer: 'Patrol composition is exactly determined by terrorist count (ε² = 1.000) and the whole role mix is reconfigured by difficulty. The stationary/roaming split tracks the depth zones a given layout supplies — so roles follow scenario structure rather than being assigned arbitrarily.',
  },
  {
    id: 'RQ3',
    question: 'Can generated scenarios be validated as coherent, mission-ready and loadable?',
    evidence: 'Exp. E (reliability + failure analysis)',
    answer: '100% validation pass for every configuration an evaluator can actually request; all failures are confined to the unreachable extended range and fall into two well-characterised categories.',
  },
  {
    id: 'RQ4',
    question: 'How much do repeat generations vary, and how do difficulty and randomness settings affect the output?',
    evidence: 'Exp. A/A2 (variability) · Exp. C & D (settings)',
    answer: 'Repeat generation at production settings varies meaningfully in placement, roles, furniture, door states and depth (CV up to 153.5%) while the topology stays deterministic. Difficulty produces large structural change; randomness produces almost none.',
    primary: true,
  },
]

// ── Metric groups ────────────────────────────────────────────────────────────
// The 28 measured columns of ScenarioMetrics, grouped so Experiment A's headline
// reads off the table directly: the "Layout / graph" group is entirely CV = 0%
// (topology is deterministic for a fixed parameter set) while every other group
// varies. Config-echo columns (configRoomCount, difficultyLevel, seedUsed …) are
// deliberately excluded — they're the experiment's INPUTS, not measurements, so
// their spread would describe the study design rather than the generator.
export const METRIC_GROUPS = [
  {
    group: 'Layout / graph',
    metrics: [
      { key: 'roomCount',        label: 'Room count' },
      { key: 'doorCount',        label: 'Door count' },
      { key: 'avgConnectivity',  label: 'Avg connectivity' },
      { key: 'graphDiameter',    label: 'Graph diameter' },
      { key: 'maxDepth',         label: 'Max depth' },
      { key: 'cyclicityMeasure', label: 'Cyclicity' },
      { key: 'entryPointCount',  label: 'Entry points' },
    ],
  },
  {
    group: 'Door state',
    metrics: [
      { key: 'doorsOpen',          label: 'Doors open' },
      { key: 'doorsClosed',        label: 'Doors closed' },
      { key: 'doorsLocked',        label: 'Doors locked' },
      { key: 'lockedDoorFraction', label: 'Locked-door fraction' },
    ],
  },
  {
    group: 'Entity placement',
    metrics: [
      { key: 'avgEntityDistanceFromEntry',  label: 'Avg entity distance from entry', unit: ' m' },
      { key: 'entityClusteringCoefficient', label: 'Entity clustering' },
      { key: 'avgHostageTerroristDistance', label: 'Avg hostage–terrorist distance', unit: ' m' },
      { key: 'minHostageTerroristDistance', label: 'Min hostage–terrorist distance', unit: ' m' },
      { key: 'entityDepthVariance',         label: 'Entity depth variance' },
      { key: 'avgEntityDepth',              label: 'Avg entity depth' },
    ],
  },
  {
    group: 'NPC roles & navigation',
    metrics: [
      { key: 'avgPatrolRouteLength', label: 'Avg patrol route length' },
      { key: 'stationaryGuardCount', label: 'Stationary guards' },
      { key: 'patrolCount',          label: 'Patrols' },
      { key: 'roamingGuardCount',    label: 'Roaming guards' },
      { key: 'hostageGuardianCount', label: 'Hostage guardians' },
      { key: 'patrolCoverage',       label: 'Patrol coverage' },
      { key: 'avgWaypointCount',     label: 'Avg waypoints' },
    ],
  },
  {
    group: 'Furniture',
    metrics: [
      { key: 'totalFurnitureCount',    label: 'Total furniture' },
      { key: 'avgFurniturePerRoom',    label: 'Avg furniture per room' },
      { key: 'furnitureFloorCoverage', label: 'Furniture floor coverage' },
      { key: 'furnishedRoomCount',     label: 'Furnished rooms' },
    ],
  },
]

/** Flat key → label/unit lookup over METRIC_GROUPS. */
export const METRIC_LABELS = Object.fromEntries(
  METRIC_GROUPS.flatMap(g => g.metrics.map(m => [m.key, m]))
)

/** The 7 pairwise structural diversity measures from DiversityAnalyser. */
export const DIVERSITY_MEASURES = [
  { key: 'graphEditDistance',       label: 'Graph edit distance' },
  { key: 'entityPositionDistance',  label: 'Entity position distance', unit: ' m' },
  { key: 'jaccardRoomConnectivity', label: 'Jaccard connectivity', note: 'similarity — higher = MORE alike' },
  { key: 'roleDistributionDistance', label: 'Role distribution distance' },
  { key: 'depthProfileDistance',    label: 'Depth profile distance' },
  { key: 'doorStateDistance',       label: 'Door state distance' },
  { key: 'furnitureCountDistance',  label: 'Furniture count distance' },
]

// ── Transcribed inferential results ──────────────────────────────────────────
// Verbatim from MODULE1_RESULTS.md. Group MEANS are not copied here — the API
// recomputes those from the raw CSVs — only the test statistics are.

/**
 * Experiment A vs A2 — Mann-Whitney U on the pairwise-diversity distributions.
 * Source: MODULE1_RESULTS.md §A2_medium_vs_high.
 */
export const A_VS_A2_TESTS = {
  graphEditDistance:        { p: '<0.0001', rankBiserial: 0.061 },
  roleDistributionDistance: { p: '<0.0001', rankBiserial: 0.061 },
  depthProfileDistance:     { p: '<0.0001', rankBiserial: 0.061 },
  jaccardRoomConnectivity:  { p: '<0.0001', rankBiserial: -0.061 },
  entityPositionDistance:   { p: '0.003',   rankBiserial: -0.034 },
  doorStateDistance:        { p: '0.042',   rankBiserial: 0.020 },
  furnitureCountDistance:   { p: '<0.0001', rankBiserial: -0.103 },
}
// MODULE1_RESULTS.md words the bound as "|rank_biserial| ≤ 0.10"; furniture count distance is
// actually 0.103, so the bound is stated here as 0.11 to stay true to the table above it.
export const A_VS_A2_INTERPRETATION =
  'High randomness yields statistically detectable but practically marginal extra diversity — ' +
  'no |rank-biserial| exceeds 0.11, well inside the range normally read as a negligible effect. ' +
  'Placement and furniture diversity actually DECREASE. Re-exposing the randomness control to ' +
  'evaluators would unlock very little.'

/**
 * Experiment C — randomness sensitivity (Kruskal-Wallis).
 * `constant` = the metric had a single value across all three groups, so no test
 * was run (no variance to partition). Source: MODULE1_RESULTS.md §C_focus_stats.
 */
export const C_FOCUS_STATS = [
  { key: 'avgConnectivity',      test: 'constant',       p: null,    eps2: 0.000, magnitude: null },
  { key: 'cyclicityMeasure',     test: 'constant',       p: null,    eps2: 0.000, magnitude: null },
  { key: 'roomCount',            test: 'constant',       p: null,    eps2: 0.000, magnitude: null },
  { key: 'entityDepthVariance',  test: 'Kruskal-Wallis', p: '0.061', eps2: 0.025, magnitude: 'small' },
  { key: 'avgPatrolRouteLength', test: 'Kruskal-Wallis', p: '0.342', eps2: 0.001, magnitude: 'negligible' },
  { key: 'lockedDoorFraction',   test: 'constant',       p: null,    eps2: 0.000, magnitude: null },
]
export const C_INTERPRETATION =
  'At a fixed configuration, randomness produces no measurable structural change — the topology ' +
  'is pinned by the parameter set. Entity depth variance and patrol route length both drift ' +
  'downward as randomness rises, but neither shift is significant (p = 0.061 and 0.342). ' +
  'Consistent with the A-versus-A2 result.'

/**
 * Experiment D — difficulty differentiation (Kruskal-Wallis).
 * Source: MODULE1_RESULTS.md §D_focus_stats.
 */
export const D_FOCUS_STATS = [
  { key: 'patrolCount',                 test: 'Kruskal-Wallis', p: '<0.0001', eps2: 1.000, magnitude: 'large' },
  { key: 'avgPatrolRouteLength',        test: 'Kruskal-Wallis', p: '<0.0001', eps2: 0.760, magnitude: 'large' },
  { key: 'patrolCoverage',              test: 'Kruskal-Wallis', p: '<0.0001', eps2: 0.760, magnitude: 'large' },
  { key: 'stationaryGuardCount',        test: 'Kruskal-Wallis', p: '<0.0001', eps2: 0.365, magnitude: 'large' },
  // Not in the published §D_focus_stats table, so it carries no test statistic — but without
  // it the reallocation is invisible: this is the row that absorbs the disbanded patrol, and
  // the interpretation below cites it. Shown as means only rather than left out.
  { key: 'roamingGuardCount',           test: 'not reported',   p: null,      eps2: null,  magnitude: null },
  { key: 'minHostageTerroristDistance', test: 'Kruskal-Wallis', p: '<0.0001', eps2: 0.287, magnitude: 'large' },
  { key: 'avgEntityDistanceFromEntry',  test: 'Kruskal-Wallis', p: '0.323',   eps2: null,  magnitude: 'negligible' },
  { key: 'avgHostageTerroristDistance', test: 'Kruskal-Wallis', p: '0.360',   eps2: null,  magnitude: 'negligible' },
  { key: 'entityDepthVariance',         test: 'Kruskal-Wallis', p: '0.820',   eps2: null,  magnitude: 'negligible' },
]
// NOTE — this interpretation deliberately DIVERGES from MODULE1_RESULTS.md §D_focus_stats,
// whose KEY_BEHAVIOUR line reads "at difficulty 5, patrolling guards convert to stationary".
// The experiment's own group means contradict that: going from difficulty 3 to 5 the patrol
// (−1.00) is absorbed almost entirely by ROAMING guards (+1.04) while stationary guards
// barely move (−0.04). Stationary guards do rise, but on the earlier 1 → 3 step (+0.66,
// drawn from roaming). The test statistics above are untouched and correct — only the
// mechanism sentence was wrong, and it is restated here to match the data the page renders.
export const D_INTERPRETATION =
  'At difficulty 5 the patrol is disbanded — patrolCount and patrolCoverage collapse to zero — ' +
  'and the freed terrorist is reassigned as a roaming guard (roaming rises 0.34 → 1.38 while ' +
  'stationary guards hold at ≈0.6). Stationary guards instead appear on the earlier step from ' +
  'difficulty 1 to 3. Meanwhile the minimum hostage–terrorist distance rises monotonically ' +
  '(3.00 → 3.51 → 3.93 m) while global spatial spread is unaffected — so difficulty reconfigures ' +
  'who does what, it does not reposition the scenario.'

// ── Published figures ────────────────────────────────────────────────────────
// The matplotlib/seaborn figures committed alongside the study, served byte-for-byte
// from figures/ via /api/module1-evaluation/figure/<file>. These are the CITABLE
// artifacts — the same images that go in the report — so they are shown as-is rather
// than redrawn in the dashboard's chart library: a second rendering could disagree
// with the published analysis, and it is the published one a reviewer will check.
// They keep their white matplotlib background; the UI puts them on a white plate so
// that reads as deliberate rather than broken.
//
// `file` doubles as the allowlist for the image route — nothing outside this array
// can be requested, so the route cannot be walked out of the figures directory.
export const FIGURES = [
  {
    file: 'FigA1_diversity_histograms_medium.png',
    experiment: 'A',
    title: 'Figure A1 — Pairwise diversity distributions at production settings.',
    alt: 'Six histograms of pairwise diversity measures across 4,950 scenario pairs.',
    caption: 'One panel per diversity measure across all 4,950 pairs. This is what the summary table cannot show: entity position distance is a smooth, roughly bell-shaped distribution centred on 10.06 m — the dominant continuous axis of variation — while graph edit, role distribution and depth profile distance are discrete, taking only the values 0 and 2, and are visibly identical panels. That identity is the 4-room collinearity noted above, seen directly. Jaccard connectivity and door state distance are likewise two-valued.',
  },
  {
    file: 'FigB1_layoutType_boxplots.png',
    experiment: 'B',
    title: "Figure B1 — Effect of layoutType on its target metrics.",
    alt: 'Boxplots of five layout metrics across the four topology types.',
    caption: 'Most boxes collapse to a flat line, meaning the metric takes a single value for that topology — the parameter maps to structure exactly, not merely as a shift in average. Branching is the one topology with real within-level spread (graph diameter 2–3, max depth 1–2, patrol coverage 0.25–0.5), because its tree shape is randomised while its room and door counts are not.',
  },
  {
    file: 'FigB5_terroristCount_boxplots.png',
    experiment: 'B',
    title: 'Figure B5 — Effect of terroristCount on the role metrics.',
    alt: 'Boxplots of five NPC role metrics across terrorist counts 1 to 4.',
    caption: 'Patrol count is a clean step: flat at 0 for one or two terrorists, flat at 1 from three onward. The two guard panels are the evidence behind the caveat above — roaming guard count sits at 1 for two terrorists, drops to ~0 at three, then spans 1–2 at four, and stationary guard count is flat at 1 for three but spans 0–1 at four. The split is structure-dependent, not a trend in terrorist count.',
  },
  {
    file: 'FigD1_difficulty_boxplots.png',
    experiment: 'D',
    title: 'Figure D1 — Difficulty differentiation across eight metrics.',
    alt: 'Boxplots of eight scenario metrics at difficulty levels 1, 3 and 5.',
    caption: 'Patrol count, route length and coverage all collapse to a flat zero at difficulty 5. Minimum hostage–terrorist distance shows the monotonic rise — its box tightens and lifts across 1 → 3 → 5 above a stable set of high outliers — while the three global-spread metrics in the top row barely move. That contrast is the visual form of "reconfiguration, not repositioning".',
  },
  {
    file: 'FigE1_passrate_by_scope.png',
    experiment: 'E',
    title: 'Figure E1 — Validation pass rate by parameter scope.',
    alt: 'Bar chart of validation pass rate for overall, form-reachable and extended-range scopes.',
    caption: 'The same three figures as the scope table above. The form-reachable bar (100%) is the one describing the delivered system; the other two include configurations the evaluator form cannot produce.',
  },
  {
    file: 'FigE3_passrate_heatmap.png',
    experiment: 'E',
    title: 'Figure E3 — Pass rate by layout type × room count.',
    alt: 'Heatmap of validation pass rate across four layout types and room counts 3 to 15.',
    caption: 'The interaction the marginal breakdowns above cannot show. Hub-and-spoke holds up best across the range; linear is weakest, dipping to 50–62% at several room counts. The blank cell is loop at 3 rooms — the loop generator promotes a 3-room request to 4 (all 9 such configurations in this experiment emitted 4 rooms), so no loop scenario has 3 rooms. Note this cross-tab pairs layout with room count; the 0%-pass worst case the study reports is a different pairing (8 terrorists in small rooms) and does not appear here.',
  },
]

/** Filenames the image route will serve. Nothing else is reachable. */
export const FIGURE_FILES = FIGURES.map(f => f.file)

/**
 * Figures MODULE1_RESULTS.md's artifact index references but which are not committed.
 * Surfaced in the UI rather than silently omitted: the handover brief logs this as a
 * BLOCKING open item ("do not cite an uncommitted figure" — either regenerate them or
 * trim the index), and a dashboard that quietly showed only what happened to exist
 * would hide exactly the gap that needs deciding on.
 */
export const FIGURES_NOT_COMMITTED = [
  { experiment: 'A', ids: 'FigA2, FigA3', describes: 'further Experiment A / A2 variability views' },
  { experiment: 'B', ids: 'FigB2–FigB4, FigB6', describes: 'the roomCount, roomSize, placementStrategy and entryType boxplots — layoutType and terroristCount are the two that were committed' },
  { experiment: 'C', ids: 'FigC1', describes: 'the randomness-sensitivity boxplots' },
  { experiment: 'E', ids: 'FigE2', describes: 'a further Experiment E view' },
]

/**
 * Known issues and limitations that MUST be shown next to the results they qualify.
 * Source: MODULE1_RESULTS.md §B_notes, §LIMITATIONS, and the handover brief §9.1
 * ("Constrain a reported result — must appear in Evaluation or Discussion").
 */
export const CAVEATS = {
  B: [
    'entryType is the one null relationship in the matrix: it sets entryPointCount deterministically (single → 1, multiple → 3, ε² = 1.000) but leaves avgConnectivity constant at 1.50 and doorsOpen constant at 0. Extra entries attach as exterior access points with no interior re-wiring, so RQ1 traceability is qualified as traceable on access only for this parameter.',
    'layoutType is the most globally influential control — beyond its target metrics it also reshapes door states, furniture, guard roles and entity depth.',
    'Role assignment is exactly determined in its patrol metrics but NOT monotonic in its guard split. Patrols behave cleanly (none at 1–2 terrorists, one from 3 onward, ε² = 1.000), whereas stationary guards run 0 → 0 → 0.78 → 0.62 and roaming guards 0 → 1.00 → 0.22 → 1.38 across terrorist counts 1–4. The hostage guardian is assigned first and the remainder is allocated by a decision table, with roaming as the terminal fallback whenever a layout cannot supply the depth zone a stationary post needs — so the split reports how often that fallback fired, not a trend in terrorist count. Read those two rows as structure-dependent, not as a scaling law.',
  ],
  E: [
    'Both pass rates must be read together. The overall 83.2% understates what evaluators experience, because it includes configurations the form cannot produce; the form-reachable 100% describes the delivered system. Reporting either one alone is indefensible.',
    'The form-reachable headline rests on 43 rows — a 95% confidence interval of roughly 92–100%. Report the interval alongside the 100%.',
  ],
  A: [
    'Diversity here is placement, role, furniture, door-state and depth diversity — NOT topological. At a fixed production parameter set the layout topology is deterministic by design.',
    'doorsOpen has zero variance at the 4-room baseline. This is not a metrics defect: the door-state policy locks any door touching the hostage room and closes any door touching the entry room, so only a direct standard-to-standard edge can be open — and a 4-room layout has one entry, one hostage room and two standard rooms that neither generated topology connects directly.',
    'graphEditDistance, roleDistributionDistance and depthProfileDistance are identical across 100% of the 4950 pairs, because at four rooms a single edge change necessarily moves one room’s depth and one NPC’s role. Re-tested at 12 rooms they decouple completely, confirming the three are genuinely distinct measures and the collinearity is a property of the small baseline. At this baseline treat them as one signal, not three.',
    'Furniture metrics are not comparable across generator versions — the companion-grouping pass raised density relative to earlier builds.',
  ],
}
