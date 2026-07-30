# Module 1 — Report Handover Brief

**To:** Report author
**From:** Pramoth Dilshan — Module 1 owner (Dynamic Scenario Generation)
**Project:** Dynamic Scenario Generation and Evaluation for VR-Based Military Training
**Team:** Team Sentinels · University of Moratuwa
**Contribution period:** 23 April 2026 – 29 July 2026
**Prepared:** 29 July 2026

---

## 0. How to use this document

This is a self-contained brief covering my individual contribution to the project. Everything needed to write the Module 1 sections — scope, design rationale, citations, results, limitations — is here. You should not need to read the raw development log or the source papers to draft from it.

**Three companion files** back this brief. Keep all three to hand:

| File | Contains | Use when |
|---|---|---|
| `Module1_Reference_Master.md` | Canonical [1]–[22] list, PDF mapping, claim ledger | Checking any citation |
| `Module1_Design_Rationale_Ledger.md` | Every design decision joined to its citation, deviations, limitations register | Writing Design & Implementation |
| `MODULE1_RESULTS.md` | Statistical ground truth for all 2000 scenarios | Writing Evaluation & Results |

### Three rules, please

1. **No number appears in the report unless it is in `MODULE1_RESULTS.md` or a committed CSV.** If a figure is needed that isn't there, ask me — don't estimate.
2. **No citation is added to a claim that doesn't already have one here.** Section 5 marks every claim's citation status explicitly. A claim marked `—` has no literature basis and must not acquire one during drafting.
3. **Section 8 (deviations) and Section 9 (limitations) are not optional.** They are what makes the evaluation credible.

---

## 1. Module 1 in brief

Module 1 is the scenario generation pipeline for a VR hostage-rescue training system built in Unity 2022.3 LTS for Meta Quest 3. An evaluator specifies a handful of mission parameters through a form; Module 1 converts those parameters into a complete, validated, playable indoor mission — room graph, door states, entity positions, NPC role assignments, patrol and guard navigation context, and furniture — serialised as a single `Scenario.json` package that Modules 2, 3 and 4 consume.

The research contribution is not that a scenario can be generated. It is that the generated scenario is **traceable** (every structural property maps back to a parameter the evaluator set), **valid** (spatial and mission coherence guaranteed by a validation layer, not by luck), and **varied** (repeat generations at identical settings differ meaningfully), and that all three properties are demonstrated empirically over a 2000-scenario study rather than asserted.

---

## 2. Scope — what Module 1 owns

Per the project specification, Member 1's scope is the parameter-driven scenario generation pipeline, producing Scenario JSON, layout metadata, spawn allocations, NPC role assignments and navigation context.

**In scope (mine):**
- `ScenarioConfig.json` input schema — the contract between the evaluator UI and the pipeline
- `Scenario.json` output schema — the most heavily shared artifact in the system
- Layout generation (room graph, topology, doors, door states, entry points)
- Entity placement (trainee, hostage, terrorists) under spatial and distance constraints
- NPC role assignment and navigation context construction
- Procedural furniture placement
- The validation layer
- Unity scene realisation (`SceneBuilder`), evaluator UI, editor tooling, and the HTTP bridge to the AAR dashboard
- The full Phase 5 evaluation study and statistical analysis

**Out of scope (other members):** runtime NPC behaviour and coordination (Module 3), cognitive behaviour analysis (Module 2), logging/AAR/replay (Module 4). Where this brief mentions those modules it is describing an integration boundary, not my work.

---

## 3. Research questions

| RQ | Question | Principally answered in | Evidence |
|---|---|---|---|
| **RQ1** | Can evaluator-defined parameters be translated into valid, varied indoor layouts with correct spatial structure? | Design & Implementation | Experiment B (traceability), Experiment E (validity) |
| **RQ2** | Can NPC roles and navigation context be derived meaningfully from scenario structure rather than assigned arbitrarily? | Design & Implementation | Experiment B (guard/patrol metrics), Experiment D (role reconfiguration) |
| **RQ3** | Can generated scenarios be validated as coherent, mission-ready and loadable? | Validation layer | Experiment E (reliability and failure analysis) |
| **RQ4** | *(Primary)* How much do repeat generations vary, and how do difficulty and randomness settings affect the output? | Evaluation | Experiments A/A2 (variability), C and D (settings) |

---

## 4. What was built

### 4.1 Phase 1 — Schema and algorithm design

Four design documents produced before any code: `ScenarioConfig` schema, `Scenario` output schema, and three algorithm designs (layout generation, entity placement, NPC role assignment). Each design decision was grounded in the literature review — see Section 5.

The output schema is the system's most consequential artifact: Module 2 consumes room/zone identifiers for location-tagged events, Module 3 consumes role assignments and navigation context to drive NPC behaviour, and Module 4 consumes layout metadata for perspective reconstruction during after-action review.

### 4.2 Phase 2 — Generation pipeline

A five-stage pipeline, all plain C# (no `MonoBehaviour`), so it runs headless and is testable:

**`LayoutGenerator`** — four topology generators (linear, branching, hub-and-spoke, loop); BFS spatial position assignment with overlap detection and ±0.5 m jitter at high randomness; Fisher-Yates direction shuffle, skipped at low randomness for deterministic placement; door placement as reciprocal room attributes with wall-side derived from relative delta; BFS depth assignment and room typing (entry / hostage_room / corridor / standard); zone label assignment with uniqueness guarantee; single and multiple entry points; all-pairs BFS graph diameter; reachability validation. A later stage (`AssignDoorStates`) applies a realism policy — entry doors open as breach points, the hostage-room door locked, interior doors mostly closed with a seeded open fraction scaled by randomness level.

**`EntityPlacer`** — three-stage hierarchical placement. Trainee at the entry-room centre; hostage by strategy-aware room selection (deepest dead-end for most strategies, mid-depth for front-loaded) at zone centre with jitter; terrorists by per-strategy room selection — clustered draws from the hostage room and adjacent rooms at depth ≥ maxDepth−1, dispersed cycles through unique depth buckets, front-loaded takes depth ≤ ceil(maxDepth/3), deep takes depth ≥ ceil(maxDepth/2). Distance constraints scale to room width: high risk 0–1.5·w, medium 0.5·w–3·w, low 2·w upward. A `FarthestValidPosition` fallback handles degenerate small-room cases where every placement zone collides.

**`RoleAssigner`** — depth-based role assignment. The hostage guardian is always assigned first (the terrorist already in the hostage room, ties broken by distance then id, else the globally nearest). Remaining terrorists are allocated across patrol / stationary guard / roaming guard by a decision matrix over remaining count and difficulty bracket. Depth zones are computed as shallowMax = ceil(maxDepth/3), deepMin = ceil(2·maxDepth/3), with a degenerate case for maxDepth ≤ 1 that converts guard allocation to roaming (covers hub-and-spoke and flat layouts).

**`NavigationContextBuilder`** — patrol routes, guard positions and facings, roaming areas, movement anchors. Stationary guards face their nearest door; hostage guardians face the primary door.

**`FurniturePlacer`** *(added July)* — wall-anchored, door-aware and entity-aware furniture that doubles as tactical cover, with companion items grouped as units. Written as pure data into `Scenario.json` so Module 4 can replay it.

**`ScenarioGenerator`** — the orchestrator. A single `System.Random` is threaded through every stage so the whole pipeline is reproducibly derived from one seed. Layout failures and validation failures share **one** unified retry loop (budget 3, i.e. 4 attempts); on retry a fresh `System.Random(seed + 1)` replaces the old one, and `seedUsed` records the seed that actually produced the output.

### 4.3 Phase 3 — Validation layer

`ScenarioValidator` — ten checks in two tiers, all of which always run:

- **Tier 1 (local):** `RoomBoundsCheck`, `EntityBoundsCheck` (within room interior at 0.8 m wall margin), `DoorConsistencyCheck` (reciprocal doors exist and share an id), `EntityMetadataCheck`
- **Tier 2 (global):** `ReachabilityCheck` (BFS from trainee spawn visits every room), `HostagePathCheck` (door-graph path trainee → hostage room), `EntityOverlapCheck` (no two entities within 1.5 m XZ), `NavigationContextCheck`, `ReferentialIntegrityCheck` (every room reference across spawn points, entities, role assignments and navigation context resolves), `RoleAssignmentCompletenessCheck`

Validation failures do not throw — the failed scenario is returned with `validationResult.passed = false`, so it can be persisted and inspected post-mortem. Layout exceptions still throw, because there is no scenario to return.

**Test suite:** 21 tests across five categories (configuration loading, layout generation, entity placement, role assignment, full pipeline), implemented as a single-file `MonoBehaviour` harness with `[ContextMenu]` execution rather than adding an NUnit dependency.

### 4.4 Phase 4 — Runtime integration and tooling

- **`SceneBuilder`** — realises `Scenario.json` as a live Unity scene: rooms, doors, NPCs, trainee positioning, runtime NavMesh baking, then `EventManager.NotifyScenarioReady()`, which is the Module 1 → Module 3 handoff boundary
- **`EvaluatorConfigPanel`** — in-VR configuration UI covering every `ScenarioConfig` field, with live validation feedback
- **`ScenarioGeneratorEditor`** — five editor menu commands for one-click generation from canned configs
- **`ScenarioHttpServer`** — in-process HTTP listener exposing `/health`, `/status`, `/scenario/generate` and `/scenario/start`, so the external Next.js AAR dashboard drives the same pipeline as the in-headset UI
- **Environment realism work** — real base-map doors, walls and floors; interactive lockable doors with NavMeshObstacle carving; perimeter corridor, ceilings and roof; framed exterior windows with deliberately opaque glazing; outdoor trainee spawn with a guided approach path

### 4.5 Phase 5 — Evaluation

A 2000-scenario empirical study across six experiments, plus the measurement instruments built to support it: `ScenarioMetrics` (39-column per-scenario extraction), `BatchEvaluationRunner`, and `DiversityAnalyser` (seven pairwise structural measures). Full methodology in Section 6.

---

## 5. Design decisions and their citations

These are the rows to draw on when writing the Design & Implementation chapter. Every citation below already exists in a project design document — none is being invented here.

| Decision | Justification | Cite |
|---|---|---|
| Rooms modelled as graph nodes with **doors as room attributes**, not independent entities | Treating doors as separate elements requiring spatial alignment on shared edges makes connectivity substantially harder to achieve | **[20]** |
| **Constructive generation with constraints**, rejecting Wave Function Collapse | WFC cannot enforce global reachability without excessive backtracking — global reachability constraints produce hundreds of conflicts under a restart-only design | **[15]** |
| Four **strongly-typed topologies** rather than unconstrained random graphs | Graph topology directly determines tactical properties; representation choice dominates generation difficulty | **[11]**, **[20]** |
| **BFS depth from the entry room** drives room typing and NPC role zones | Space Syntax accessibility graphs place private rooms at deep nodes and public rooms at shallow nodes | **[11]**, **[18]** |
| Exactly one room typed `hostage_room` — the deepest dead-end | Maximally-defended position; satisfies the single-hostage schema constraint | **[11]** |
| `hostageRiskLevel` implemented as **inter-entity distance thresholds** | Distance constraints between element types as a first-class generation mechanism | **[16]** |
| **`clustered` placement strategy** offered, with `dispersed` as its contrast | Spatial proximity produces compounding tactical effects — clustered terrorists differ tactically from the same number spread out | **[5]** |
| **Behind-door and sightline-blind placement** at high difficulty | Stressor validation study — a stranger suddenly entering a room ranked highest for both stress (M = 41.45) and anxiety (M = 28.41) among 13 audio-visual stressors | **[3]** |
| **Two-tier validation architecture** | Tier-1 local constraints are separable from the tier-2 global connected-path constraint, which cannot be expressed as simple constraint satisfaction | **[20]** |
| **Reachability validation as non-negotiable**, with retry rather than best-effort output | Validity guarantees must be prioritised above novelty or diversity; reachability is expressible as Boolean variables over graph nodes and edges | **[10]**, **[17]**, **[18]** |
| Generation ordered **layout → entities → furniture** (coarse to fine) | Three-stage hierarchical optimisation: large objects, then medium, then small | **[14]** |
| **Seed-based reproducibility**; full input-parameter echo in output metadata | Traceability requirement — an evaluator must be able to trust that a generated scenario reflects their configured intent | **[22]** |
| Scenario emitted as a **single compact JSON package** consumed by three downstream modules | Room-specification-to-JSON as an established handoff pattern for procedurally generated scenes | **[13]** |
| `randomnessLevel: high` retained as the **diversity-maximising** setting | Novelty-search expands a small parent population into a diverse set without premature convergence | **[8]**, **[13]** |
| Furniture **companions placed as grouped units** | Semantic Asset Groups — dependent objects placed as units rather than individually | **[13]** |

### Claims with no citation — do not add one

The following are engineering or design choices with no literature basis. Present them as implementation decisions:

- Furniture wall-anchoring and door-awareness *(see Section 11, open item 2 — a citation may be added if that decision is taken)*
- Door state policy (entry open, hostage locked, interior mostly closed)
- Opaque window glazing *(a link to [22] on simulation-to-rendering coupling has been proposed but not verified — do not use it unless I confirm)*
- All Unity platform decisions: assembly definition boundaries, main-thread marshalling, NavMeshObstacle carving, prefab construction, HTTP bridge architecture

---

## 6. Evaluation methodology

**Dataset:** 2000 generated scenarios across six experiments.

| Exp | Design | n | Addresses |
|---|---|---|---|
| A | 100 scenarios from an identical production baseline; 4950 unique pairs | 100 | RQ4 (primary) |
| A2 | Experiment A repeated at high randomness as a ceiling comparison | 100 | RQ4 |
| B | Each of 6 form parameters varied in isolation, 50 scenarios per level | 1000 | RQ1, RQ2 |
| C | 50 each at low / medium / high randomness, fixed config | 150 | RQ4 |
| D | 50 each at difficulty 1 / 3 / 5 | 150 | RQ4, RQ2 |
| E | 500 scenarios across the full pipeline parameter space, split by whether the configuration is reachable through the evaluator form | 500 | RQ3, RQ1 |

**Production baseline:** branching layout, 4 rooms, medium room size, single entry, 3 terrorists, dispersed placement, difficulty 3, randomness medium, hostage risk medium.

**Statistical procedure:** Shapiro-Wilk per group, then one-way ANOVA with Tukey HSD when all groups are normal, otherwise Kruskal-Wallis with Dunn and Bonferroni correction. Effect sizes η² for ANOVA and ε² for Kruskal-Wallis (thresholds: negligible < 0.01, small ≥ 0.01, medium ≥ 0.06, large ≥ 0.14). Mann-Whitney U with rank-biserial for the A-versus-A2 diversity comparison. Stack: pandas, scipy, scikit-posthocs, matplotlib, seaborn.

**Four methodological safeguards worth reporting explicitly** — these are what make the numbers defensible:

1. **Explicit per-scenario seeds from a master `System.Random`**, not the pipeline's auto-seed. `Environment.TickCount` has roughly 15 ms resolution against a ~1 ms generation time, so a tight loop would have handed many scenarios the same seed and collapsed the very variability Experiment A exists to measure.
2. **Per-experiment seed-stream offsets.** Experiment A, Experiment C's medium batch and Experiment D's difficulty-3 batch all share the identical baseline config; without offsets they would have drawn the same seeds and been byte-identical repeats rather than independent samples.
3. **A `seedRetries` column in Experiment E.** The pipeline silently retries a failed scenario up to three times, so a bare pass rate would report "passed *eventually*". The breakdown: 355 scenarios passed first time, 38 needed one retry, 15 needed two, 92 exhausted all three.
4. **An eight-point audit of the evaluation tooling before the citable dataset was generated** — degenerate inputs, CSV quoting and column alignment, differing room counts, grouping columns, path handling, and field-name agreement. All eight passed; one latent defect was found and fixed (CSV row assembly switched from append-with-separator to format-and-join, which would have silently shifted every column had field order ever changed).

Metrics are derived from the exported `ScenarioData` alone and never from generator internals, so any batch of `Scenario.json` files can be re-measured later without re-running generation.

---

## 7. Results

All figures below are from `MODULE1_RESULTS.md`. Reproduce them exactly.

### 7.1 RQ4 — Variability at production settings (Experiment A)

**Headline: the room graph is deterministic; variation is tactical, not topological.**

`roomCount`, `doorCount`, `avgConnectivity`, `cyclicityMeasure`, `entryPointCount` and locked-door count all have CV = 0% across 100 repeats. Variation is concentrated in entity placement, guard and patrol roles, furniture and door states:

| Metric | Mean | SD | CV % |
|---|---|---|---|
| roamingGuardCount | 0.30 | 0.46 | 153.5 |
| entityDepthVariance | 0.175 | 0.115 | 65.8 |
| stationaryGuardCount | 0.70 | 0.46 | 65.8 |
| minHostageTerroristDistance | 2.69 | 1.63 | 60.6 |
| entityClusteringCoefficient | 0.698 | 0.256 | 36.6 |
| patrolCoverage | 0.335 | 0.119 | 35.5 |
| avgHostageTerroristDistance | 8.82 | 2.80 | 31.7 |
| maxDepth | 1.70 | 0.46 | 27.1 |
| furnitureFloorCoverage | 0.068 | 0.018 | 26.6 |
| totalFurnitureCount | 18.97 | 3.33 | 17.6 |
| structural (graph) metrics | — | 0.00 | 0.0 |

Across 4950 pairs, mean pairwise entity-position distance is **10.06 m** (SD 3.20, range 0.34–19.64) — the dominant continuous axis of variation. Mean graph edit distance is 0.85; mean Jaccard connectivity similarity 0.79.

**A versus A2 (high randomness):** statistically detectable but practically marginal. Graph edit distance rises 14.3% (p < 0.0001) but rank-biserial is only 0.061; placement diversity falls 1.7% and furniture diversity falls 12.8%. Conclusion: re-exposing the randomness control to evaluators would unlock very little.

*Figure: `FigA1_diversity_histograms_medium.png`*

### 7.2 RQ1 — Parameter traceability (Experiment B)

Every structural form parameter maps to its intended layout metrics with large, highly significant effects (all p < 0.0001):

| Parameter | Target metric | ε² |
|---|---|---|
| layoutType | avgConnectivity | 1.000 |
| layoutType | cyclicityMeasure | 1.000 |
| layoutType | maxDepth | 0.890 |
| layoutType | graphDiameter | 0.765 |
| layoutType | patrolCoverage | 0.758 |
| roomCount | roomCount (actual) | 1.000 |
| roomCount | graphDiameter | 0.734 |
| roomCount | totalFurnitureCount | 0.611 |
| roomSize | avgFurniturePerRoom | 0.878 |
| roomSize | avgEntityDistanceFromEntry | 0.689 |
| roomSize | minHostageTerroristDistance | 0.549 |
| placementStrategy | entityDepthVariance | 0.420 |
| placementStrategy | entityClusteringCoefficient | 0.327 |
| terroristCount | patrolCount | 1.000 |
| terroristCount | avgPatrolRouteLength / patrolCoverage | 0.901 |
| terroristCount | roamingGuardCount | 0.801 |
| entryType | entryPointCount | 1.000 |

`layoutType` is the most globally influential control — it reshapes door states, furniture, guard roles and entity depth in addition to its target metrics.

*Figures: `FigB1_layoutType_boxplots.png`, `FigB5_terroristCount_boxplots.png`*

### 7.3 RQ2 — NPC roles are structure-derived, not arbitrary

Two independent lines of evidence:

**Deterministic and monotonic in terrorist count (Experiment B).** `patrolCount` is perfectly determined (ε² = 1.000): no patrols at one or two terrorists, a patrol appears from three. Stationary and roaming guards scale with count (ε² = 0.546 and 0.801), and route length and coverage follow (0.901).

**Reconfigured by difficulty (Experiment D).** At difficulty 5, patrolling guards convert to stationary — `patrolCount` and `patrolCoverage` collapse to zero (ε² = 1.000 and 0.760) — while minimum hostage–terrorist distance rises monotonically across difficulty levels: 3.00 → 3.51 → 3.93 m (ε² = 0.287). Global spatial spread is unaffected, so the change is a role reconfiguration rather than a repositioning.

*Figure: `FigD1_difficulty_boxplots.png`*

### 7.4 RQ3 — Validation reliability (Experiment E)

| Scope | n | Pass | Fail | Pass rate |
|---|---|---|---|---|
| **Form-reachable** (roomCount 3–5, terrorists 1–4) — *primary* | 43 | 43 | 0 | **100.00%** |
| Extended range (outside the form) | 457 | 373 | 84 | 81.62% |
| Overall (full pipeline space) | 500 | 416 | 84 | 83.20% |

**Both numbers must be reported.** The overall 83.2% understates what evaluators experience because it includes configurations the form cannot produce; the form-reachable 100% describes the delivered system. Reporting only one of them is indefensible in either direction.

All 84 failures are confined to the extended range and fall into two categories: `EntityBounds` (76, 90.5%) and `EntityOverlap` (8, 9.5%). Pass rate stays at or above 89% for 3–6 rooms and falls to 78–81% for 7–15; at or above 88% for 1–4 terrorists, falling to 61% at 8. The worst combination is high terrorist count in small rooms — 8 terrorists in a small 4-room branching layout yields 0% pass. None of these is form-reachable.

Note that the 43 form-reachable rows give a 95% confidence interval of roughly 92–100% on the headline figure. **Report the interval alongside the 100%.**

*Figures: `FigE1_passrate_by_scope.png`, `FigE3_passrate_heatmap.png`*

### 7.5 Randomness sensitivity (Experiment C)

At a fixed configuration, randomness produces no measurable structural change — `avgConnectivity`, `cyclicityMeasure`, `roomCount` and `lockedDoorFraction` are all constant across low, medium and high. Only `entityDepthVariance` drifts, and not significantly (p = 0.061, ε² = 0.025). Consistent with the A-versus-A2 result.

### 7.6 Direct answers to the research questions

- **RQ1** — Every structural form parameter maps to its intended layout metrics with large, significant effects, and the resulting layouts pass spatial validity 100% of the time within the form-reachable range. Evaluator parameters translate into valid, varied layouts.
- **RQ2** — Guard and patrol composition is deterministic and monotonic in terrorist count and is reconfigured by difficulty. Role and navigation context are derived from scenario structure, not assigned arbitrarily.
- **RQ3** — 100% validation pass for every configuration an evaluator can request; all failures confined to the unreachable extended range and falling into two well-characterised categories.
- **RQ4** — Repeat generation at production settings produces meaningful variation in placement, roles, furniture, door states and depth (CV up to 153.5%), while the topology is deterministic for a fixed parameter set. High randomness adds statistically detectable but practically marginal diversity. Difficulty produces large structural change (ε² up to 1.000); randomness produces little.

---

## 8. Deviations from the design documents — please include these

The implementation departs from the Phase 1 algorithm designs in six places. Each was a reasoned decision made during implementation. **Written up as deliberate refinements they strengthen the chapter; omitted and later noticed, they undermine it.** Please place them where the relevant algorithm is described, not buried in a limitations appendix.

| Design specifies | Implementation does | Reason |
|---|---|---|
| Front-loaded threshold `ceil(maxDepth/2)` | `ceil(maxDepth/3)` | Per the implementation brief; concentrates threats closer to the entry. **Note: this affects `entityDepthVariance` and `avgEntityDistanceFromEntry`, both of which are reported Experiment B metrics** |
| Formula-based role quotas (⌈0.33⌉, ⌊0.33⌋ …) | `switch` over remaining count, matching the §3.1 decision table verbatim | The two diverge at remaining = 3, difficulty = 3: the formula gives (1,2,0) via ⌊0.99⌋ = 0, the table gives (1,1,1). The table was judged authoritative |
| Overflow fill order patrol → roaming → guard | patrol → stationary guard → roaming guard | Keeps role distribution closer to the design matrix when a layout cannot supply the expected depth zones. Roaming remains the terminal fallback because it has the most permissive zone requirement |
| Patrol waypoints interleave room centres and door midpoints | Room centres only | Door midpoints are implicitly traversed by the NavMesh agent moving between adjacent room centres, so including them adds only redundant path nodes |
| `navigationContext.waypoints` as a full list | Module 3's `PatrolLine` component exposes only `pointA` and `pointB`, so routes collapse to first and last waypoint at runtime | **Integration-boundary limitation, not a generator defect.** Multi-room routes are present and correct in `Scenario.json`; they are truncated on consumption. Please state it that way |
| `GetBehindDoorOffset` per the §7.3 offset table | Kept matching the table exactly, although doors sit at the midpoint between room centres (outside the wall) | Out-of-bounds candidates fall through to the retry loop's next placement zone, so correctness is preserved |

---

## 9. Limitations

### 9.1 Constrain a reported result — must appear in Evaluation or Discussion

- **`entryType` has no interior effect.** It sets `entryPointCount` deterministically (single → 1, multiple → 3, ε² = 1.000) but leaves `avgConnectivity` constant at 1.50 and `doorsOpen` constant at 0. Additional entries attach as exterior access points without re-wiring interior connectivity. This is the only null relationship in the Experiment B matrix; the RQ1 traceability claim must be qualified as *traceable on access only* for this parameter.
- **`doorsOpen` has zero variance at the 4-room baseline** — 0 open doors across all 300 doors in Experiment A, against 45 across Experiment B's 3050. This is **not a metrics defect**: the door-state policy locks any door touching the hostage room and closes any door touching the entry room, so only a direct standard-to-standard edge can be open, and a 4-room layout has one entry, one hostage room and two standard rooms that neither generated topology connects directly. Please include the explanation — without it, a reader will read the column as broken.
- **Three-way collinearity at the 4-room baseline.** `graphEditDistance`, `roleDistributionDistance` and `depthProfileDistance` are identical in 100% of Experiment A's 4950 pairs, because at four rooms a single edge change necessarily moves one room's depth and one NPC's role. **Re-tested at 12 rooms they decouple completely** (0% equal; means 9.57 / 0.00 / 4.47), confirming the three measures are genuinely distinct and the collinearity is a property of the small baseline. Include the 12-room re-test — it converts an apparent flaw into a demonstrated property. At the 4-room baseline the three must be treated as one signal, not three.
- **Experiment E's headline rests on 43 rows** — 95% CI roughly 92–100%.
- **Furniture metrics are not comparable across generator versions.** The companion-grouping pass raised density relative to earlier builds; any furniture figure needs a version label.
- **`scenarioId` is a fresh GUID per generation call**, so two runs with the same seed differ only in that column. Joins and duplicate detection in the analysis key on `seedUsed` plus metric columns. Worth one sentence in the methods section.

### 9.2 Scope and future work

- Single floor only; hostage count fixed at 1
- Difficulty, randomness and hostage risk level are validated but withheld from the current evaluator form (fixed at 3 / medium / medium)
- Patrol routes truncated to two waypoints at runtime by the Module 3 integration boundary
- No per-room navigation refinement (cover points, patrol tuning) — Module 3 handles these at runtime
- Single mission type; no live scenario preview before mission start
- Geometry verified at medium room size only; small and large follow the same arithmetic but are untested
- The in-headset configuration panel still exposes the old min/max room sliders while the dashboard has moved to a single room count (3–5) — the two UIs disagree
- The HTTP bridge has no authentication; acceptable for a single-user lab demo, not for deployment

---

## 10. References

Numbering is canonical for the whole report. **Note on [20]:** the entry below is the corrected one. An earlier version of the literature review listed [20] as Smith & Whitehead, *"Analyzing the Expressive Range of a Level Generator"* — that is the wrong paper. The work actually cited throughout the design documents (doors-as-room-attributes, two-tier constraint architecture, the player-skill parameter *M*) is Sorenson, Pasquier & DiPaola. Please use the entry below and disregard any older list.

[1] B. Xie et al., "A Review on Virtual Reality Skill Training Applications," *Frontiers in Virtual Reality*, vol. 2, art. 645153, 2021, doi: 10.3389/frvir.2021.645153.

[2] M. W. Boyce et al., "Enhancing Military Training Using Extended Reality: A Study of Military Tactics Comprehension," *Frontiers in Virtual Reality*, vol. 3, art. 754627, 2022, doi: 10.3389/frvir.2022.754627.

[3] O. Zechner, L. Kleygrewe, E. Jaspaert, H. Schrom-Feiertag, R. I. V. Hutter, and M. Tscheligi, "Enhancing Operational Police Training in High Stress Situations with Virtual Reality: Experiences, Tools and Guidelines," *Multimodal Technologies and Interaction*, vol. 7, no. 2, p. 14, 2023.

[4] L. Kleygrewe, R. I. V. Hutter, M. Koedijk, and R. R. D. Oudejans, "Changing Perspectives: Enhancing Learning Efficacy with the After-Action Review in Virtual Reality Training for Police," *Ergonomics*, vol. 67, no. 5, pp. 628–637, 2024, doi: 10.1080/00140139.2023.2236819.

[5] L. Luo, H. Yin, J. Zhong, W. Cai, M. Lees, and S. Zhou, "Mission-Based Scenario Modeling and Generation for Virtual Training," in *Proc. Ninth AAAI Conf. on Artificial Intelligence and Interactive Digital Entertainment (AIIDE-13)*, 2013, pp. 44–50.

[6] A. Zook, S. Lee-Urban, M. O. Riedl, H. K. Holden, R. A. Sottilare, and K. W. Brawner, "Automated Scenario Generation: Toward Tailored and Optimized Military Training in Virtual Environments," in *Proc. FDG '12*, 2012, pp. 164–171.

[7] J. Niehaus, B. Li, and M. O. Riedl, "Automated Scenario Adaptation in Support of Intelligent Tutoring Systems," in *Proc. FLAIRS-24*, 2011, pp. 531–536.

[8] R. A. Sottilare, C. Ballinger, M. Litvinas, S. Hu, and C. McGroarty, "Using Genetic Algorithms to Automate Scenario Generation and Enhance the Training Value of Serious Games for Adaptive Instruction," *The International FLAIRS Conference Proceedings*, vol. 37, no. 1, 2024, doi: 10.32473/flairs.37.1.135536.

[9] M. Hendrikx, S. Meijer, J. van der Velden, and A. Iosup, "Procedural Content Generation for Games: A Survey," *ACM Transactions on Multimedia Computing, Communications, and Applications*, vol. 9, no. 1, art. 1, 2013, doi: 10.1145/2422956.2422957.

[10] J. Togelius, G. N. Yannakakis, K. O. Stanley, and C. Browne, "Search-Based Procedural Content Generation: A Taxonomy and Survey," *IEEE Transactions on Computational Intelligence and AI in Games*, vol. 3, no. 3, pp. 172–186, 2011.

[11] A. Dahl and L. Rinde, "Procedural Generation of Indoor Environments," M.Sc. thesis, Chalmers University of Technology, 2008.

[12] E. D. Feklisov, M. V. Zingerenko, V. A. Frolov, and M. A. Trofimov, "Procedural Interior Generation for Artificial Intelligence Training and Computer Graphics," in *Proc. CPT Workshop*, CEUR-WS vol. 2763, 2020.

[13] M. Deitke et al., "ProcTHOR: Large-Scale Embodied AI Using Procedural Generation," in *Proc. NeurIPS*, vol. 35, 2022, pp. 5982–5994.

[14] A. Raistrick et al., "Infinigen Indoors: Photorealistic Indoor Scenes using Procedural Generation," in *Proc. CVPR*, 2024.

[15] I. Karth and A. M. Smith, "WaveFunctionCollapse is Constraint Solving in the Wild," in *Proc. FDG*, 2017, doi: 10.1145/3102071.3110566.

[16] D. Cheng, H. Han, and G. Fei, "Automatic Generation of Game Levels Based on Controllable Wave Function Collapse Algorithm," in *Proc. ICEC*, 2020.

[17] S. Cooper, "Sturgeon: Tile-Based Procedural Level Generation via Learned and Designed Constraints," in *Proc. AIIDE*, 2022.

[18] I. Horswill and L. Foged, "Fast Procedural Level Population with Playability Constraints," in *Proc. AIIDE*, 2012.

[19] K. Hullett and M. Mateas, "Scenario Generation for Emergency Rescue Training Games," in *Proc. FDG '09*, 2009, pp. 99–106, doi: 10.1145/1536513.1536538.

[20] N. Sorenson, P. Pasquier, and S. DiPaola, "A Generic Approach to Challenge Modeling for the Procedural Creation of Video Game Levels," *IEEE Transactions on Computational Intelligence and AI in Games*, vol. 3, no. 3, pp. 229–244, 2011.

[21] E. Kalafatis, K. Mitsis, K. Zarkogianni, M. Athanasiou, and K. Nikita, "A Modular Framework for Automated Evaluation of Procedural Content Generation in Serious Games with Deep Reinforcement Learning Agents," arXiv preprint arXiv:2505.16801, 2025.

[22] M. Cook, "Procedural Generation and Information Games," in *Proc. IEEE Conf. on Games (CoG)*, 2020, pp. 253–260.

---

## 11. Open items — please confirm with me before drafting

| | Item | Status |
|---|---|---|
| 1 | **Artifact gap.** `MODULE1_RESULTS.md` references roughly 17 tables and 13 figures; 2 tables and 6 figures are currently committed. The rest need regenerating, or the index needs trimming | **Blocking** — do not cite an uncommitted figure |
| 2 | **Furniture citation.** Yu et al., *"Make it Home: Automatic Optimization of Furniture Arrangement"* (SIGGRAPH 2011) is in the project but unnumbered. Its ergonomic accessibility and visibility criteria match what was implemented. Either add it as [23] or present furniture placement as an unprecedented design choice | My decision pending |
| 3 | **`ceil(maxDepth/3)` deviation.** Whether to fix the code to match the design or amend the design to match the code. Deciding after drafting would mean re-running the Experiment B analysis | My decision pending |
| 4 | **`entryType` interior connectivity.** Implement re-wiring, or document as bounded-but-correct | Documenting is the current plan |
| 5 | **Proposed citations for opaque glazing and outdoor trainee spawn** are unverified. Do not use them unless I confirm | Unverified |

Anything ambiguous, please ask rather than infer — I would much rather answer a question than correct a claim.
