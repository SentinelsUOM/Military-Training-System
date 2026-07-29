---
doc_type: evaluation_results
module: "Module 1 — Dynamic Scenario Generation"
project: "VR-Based Hostage-Rescue Training System (Team Sentinels)"
research_questions_covered: [RQ1, RQ2, RQ3, RQ4]
primary_research_question: RQ4
total_scenarios: 2000
experiments: [A, A2, B, C, D, E]
stats_stack: [pandas, scipy, scikit-posthocs, matplotlib, seaborn]
effect_size_thresholds: { negligible: "<0.01", small: ">=0.01", medium: ">=0.06", large: ">=0.14" }
omnibus_tests: { normal: "one-way ANOVA + Tukey HSD", non_normal: "Kruskal-Wallis + Dunn (Bonferroni)" }
production_baseline:
  layoutType: branching
  roomCount: 4
  roomSize: medium
  entryType: single
  terroristCount: 3
  placementStrategy: dispersed
  difficulty: 3          # fixed default, removed from evaluator form
  randomness: medium     # fixed default, removed from evaluator form
  hostageRiskLevel: medium  # fixed default, removed from evaluator form
form_parameters: [layoutType, roomCount, roomSize, entryType, terroristCount, placementStrategy, seed]
form_ranges: { roomCount: [3, 5], terroristCount: [1, 4] }
headline_findings:
  form_reachable_validation_pass_rate_pct: 100.0
  overall_validation_pass_rate_pct: 83.2
  mean_pairwise_graph_edit_distance: 0.85
  mean_jaccard_connectivity_similarity: 0.79
  mean_entity_position_distance: 10.06
artifacts_dir: "./"          # figures/ and tables/ are relative to this file
figures_dir: "./figures"
tables_dir: "./tables"
---

# Module 1 — Dynamic Scenario Generation: Evaluation Results

> Machine-readable results summary for Claude Code / coding agents.
> Ground truth for the numbers below lives in `tables/*.csv`; figures in `figures/*.png`.
> Do not invent values — if a figure/table path is missing, report it rather than guessing.

## RQ_MAPPING

| rq  | focus                                                        | principally_addressed_in       | supporting_evidence_here                                  |
|-----|-------------------------------------------------------------|--------------------------------|----------------------------------------------------------|
| RQ1 | Parameter-to-layout translation with spatial validity       | Design & Implementation        | Exp. B (parameter traceability); Exp. E (validity pass)  |
| RQ2 | Meaningful NPC role & navigation context (not random)       | Design & Implementation        | Exp. B (guard/patrol metrics); Exp. D (role reconfig)    |
| RQ3 | Scenario validation: coherent, mission-ready, loadable      | Validation Layer (Phase 3)     | Exp. E (validation reliability + failure analysis)       |
| RQ4 | Repeat-generation variability; difficulty & randomness      | Evaluation (Phase 5) — PRIMARY | Exp. A/A2 (variability); Exp. C & D (settings)           |

## METHODOLOGY

- Omnibus: Shapiro-Wilk per group → one-way ANOVA + Tukey HSD if all groups normal, else Kruskal-Wallis + Dunn (Bonferroni).
- Effect size: `eta2` (η²) for ANOVA, `eps2` (ε²) for Kruskal-Wallis. Thresholds in front-matter.
- Variability (Exp. A vs A2): Mann-Whitney U on pairwise-diversity distributions; effect = rank-biserial.
- `constant` in a test cell = metric had a single value across all groups (no variance → no test).

---

## EXPERIMENT_A — Variability at Production Settings
<!-- anchor: experiment-a -->

- **rq:** RQ4 (clause 1: repeat-generation variability) — PRIMARY
- **design:** 100 scenarios from identical production baseline; 4950 unique pairs.
- **randomness:** medium (production). A2 repeats at high as a ceiling comparison.
- **source_data:** `tables/A_descriptive_medium.csv`, `tables/A_A2_pairwise_diversity_descriptive.csv`, `tables/A_vs_A2_diversity_tests.csv`, `tables/A_A2_CV_comparison.csv`
- **figure:** `figures/FigA1_diversity_histograms_medium.png`

### KEY_FINDING
At production settings the room GRAPH IS DETERMINISTIC (roomCount, doorCount, avgConnectivity, cyclicity, entryPointCount, lockedDoorCount all CV=0%). Variation is tactical, concentrated in entity placement, guard/patrol roles, furniture, door states, and graph depth.

### A_variability_CV (across 100 repeats, medium randomness)

| metric                       | mean  | sd    | cv_pct |
|------------------------------|-------|-------|--------|
| roamingGuardCount            | 0.30  | 0.46  | 153.5  |
| entityDepthVariance          | 0.175 | 0.115 | 65.8   |
| stationaryGuardCount         | 0.70  | 0.46  | 65.8   |
| minHostageTerroristDistance  | 2.69  | 1.63  | 60.6   |
| entityClusteringCoefficient  | 0.698 | 0.256 | 36.6   |
| patrolCoverage               | 0.335 | 0.119 | 35.5   |
| avgHostageTerroristDistance  | 8.82  | 2.80  | 31.7   |
| maxDepth                     | 1.70  | 0.46  | 27.1   |
| furnitureFloorCoverage       | 0.068 | 0.018 | 26.6   |
| totalFurnitureCount          | 18.97 | 3.33  | 17.6   |
| structural_metrics (graph)   | —     | 0.00  | 0.0    |

### A_pairwise_diversity (4950 pairs, medium)

| metric                        | mean  | sd   | min  | max   |
|-------------------------------|-------|------|------|-------|
| graphEditDistance             | 0.85  | 0.99 | 0    | 2     |
| entityPositionDistance        | 10.06 | 3.20 | 0.34 | 19.64 |
| jaccardConnectivity (SIMILARITY) | 0.79 | 0.25 | 0.50 | 1.00 |
| roleDistributionDistance      | 0.85  | 0.99 | 0    | 2     |
| depthProfileDistance          | 0.85  | 0.99 | 0    | 2     |
| doorStateDistance             | 0.41  | 0.33 | 0    | 0.67  |
| furnitureCountDistance        | 0.17  | 0.11 | 0    | 0.54  |

- entityPositionDistance is the dominant, continuous (bell-shaped) axis of variation.
- graphEdit / role / depth distances co-vary (identical distributions).

### A2_medium_vs_high (Mann-Whitney U)

| metric                   | mean_med | mean_high | pct_change | p        | rank_biserial |
|--------------------------|----------|-----------|------------|----------|---------------|
| graphEditDistance        | 0.848    | 0.970     | +14.3%     | <0.0001  | 0.061         |
| roleDistributionDistance | 0.848    | 0.970     | +14.3%     | <0.0001  | 0.061         |
| depthProfileDistance     | 0.848    | 0.970     | +14.3%     | <0.0001  | 0.061         |
| jaccardConnectivity      | 0.788    | 0.758     | -3.8%      | <0.0001  | -0.061        |
| entityPositionDistance   | 10.06    | 9.89      | -1.7%      | 0.003    | -0.034        |
| doorStateDistance        | 0.407    | 0.420     | +3.2%      | 0.042    | 0.020         |
| furnitureCountDistance   | 0.168    | 0.146     | -12.8%     | <0.0001  | -0.103        |

- **interpretation:** High randomness → statistically detectable but practically marginal extra diversity (|rank_biserial| ≤ 0.10). Placement/furniture diversity slightly DECREASES. Re-exposing high randomness unlocks little.

---

## EXPERIMENT_B — Form Parameter Traceability
<!-- anchor: experiment-b -->

- **rq:** Traceability requirement [22]; supporting evidence for RQ1 and RQ2 — PRIMARY
- **design:** 1000 scenarios; each of 6 form parameters varied in isolation, 50/level.
- **tests:** Kruskal-Wallis + Dunn (distributions overwhelmingly non-normal).
- **source_data:** `tables/B_SUMMARY_traceability_matrix.csv`, `tables/B_target_metrics_detail.csv`, `tables/B_full_detail_all_metrics.csv`, `tables/B_additional_significant_findings.csv`, `tables/B_posthoc_details.txt`
- **figures:** `figures/FigB1_layoutType_boxplots.png` (layout), `figures/FigB5_terroristCount_boxplots.png` (terrorist count); B2–B4, B6 = roomCount, roomSize, placementStrategy, entryType.

### B_traceability_matrix (p | effect ε²; L=large)

| parameter         | metric                       | p        | effect_eps2 |
|-------------------|------------------------------|----------|-------------|
| layoutType        | avgConnectivity              | <0.0001  | 1.000 (L)   |
| layoutType        | cyclicityMeasure             | <0.0001  | 1.000 (L)   |
| layoutType        | graphDiameter                | <0.0001  | 0.765 (L)   |
| layoutType        | maxDepth                     | <0.0001  | 0.890 (L)   |
| layoutType        | patrolCoverage               | <0.0001  | 0.758 (L)   |
| roomCount         | roomCount (actual)           | <0.0001  | 1.000 (L)   |
| roomCount         | graphDiameter                | <0.0001  | 0.734 (L)   |
| roomCount         | maxDepth                     | <0.0001  | 0.182 (L)   |
| roomCount         | totalFurnitureCount          | <0.0001  | 0.611 (L)   |
| roomCount         | patrolCoverage               | <0.0001  | 0.190 (L)   |
| roomSize          | avgEntityDistanceFromEntry   | <0.0001  | 0.689 (L)   |
| roomSize          | avgFurniturePerRoom          | <0.0001  | 0.878 (L)   |
| roomSize          | furnitureFloorCoverage       | <0.0001  | 0.312 (L)   |
| roomSize          | minHostageTerroristDistance  | <0.0001  | 0.549 (L)   |
| placementStrategy | avgEntityDistanceFromEntry   | <0.0001  | 0.248 (L)   |
| placementStrategy | entityClusteringCoefficient  | <0.0001  | 0.327 (L)   |
| placementStrategy | entityDepthVariance          | <0.0001  | 0.420 (L)   |
| placementStrategy | avgEntityDepth               | <0.0001  | 0.278 (L)   |
| terroristCount    | patrolCount                  | <0.0001  | 1.000 (L)   |
| terroristCount    | stationaryGuardCount         | <0.0001  | 0.546 (L)   |
| terroristCount    | roamingGuardCount            | <0.0001  | 0.801 (L)   |
| terroristCount    | avgPatrolRouteLength         | <0.0001  | 0.901 (L)   |
| terroristCount    | patrolCoverage               | <0.0001  | 0.901 (L)   |
| entryType         | entryPointCount              | <0.0001  | 1.000 (L)   |
| entryType         | avgConnectivity              | constant | 0.000       |
| entryType         | doorsOpen                    | constant | 0.000       |

### B_notes
- **KNOWN_ISSUE (entryType):** changes entryPointCount (single→1, multiple→3) but has ZERO effect on avgConnectivity (const 1.50) and doorsOpen (const 0). Entries attach as exterior access points; no interior re-wiring. Traceable on access only.
- layoutType is the most globally influential control (reshapes door states, furniture, guard roles, entity depth).
- terroristCount role assignment is deterministic + monotonic → evidences RQ2 (non-random roles).

---

## EXPERIMENT_C — Randomness Sensitivity
<!-- anchor: experiment-c -->

- **rq:** RQ4 (clause 2) — SECONDARY
- **design:** 150 scenarios (50 each low/medium/high) at fixed production config.
- **status:** randomness is FIXED at `medium` in the evaluator form (removed control).
- **source_data:** `tables/C_focus_stats.csv`, `tables/C_posthoc.txt`
- **figure:** `figures/FigC1_randomness_boxplots.png`

### C_focus_stats (Kruskal-Wallis)

| metric                | test           | p       | effect_eps2   | mean_low | mean_med | mean_high |
|-----------------------|----------------|---------|---------------|----------|----------|-----------|
| avgConnectivity       | constant       | n/a     | 0.000         | 1.50     | 1.50     | 1.50      |
| cyclicityMeasure      | constant       | n/a     | 0.000         | 0.00     | 0.00     | 0.00      |
| roomCount             | constant       | n/a     | 0.000         | 4        | 4        | 4         |
| entityDepthVariance   | Kruskal-Wallis | 0.061   | 0.025 (small) | 0.190    | 0.185    | 0.140     |
| avgPatrolRouteLength  | Kruskal-Wallis | 0.342   | 0.001 (negl)  | 2.42     | 2.36     | 2.28      |
| lockedDoorFraction    | constant       | n/a     | 0.000         | 0.333    | 0.333    | 0.333     |

- **interpretation:** At a fixed config, randomness produces NO measurable structural change (topology pinned). Only entityDepthVariance drifts, and non-significantly. Consistent with A2.

---

## EXPERIMENT_D — Difficulty Differentiation
<!-- anchor: experiment-d -->

- **rq:** RQ4 (clause 2) — SECONDARY; also evidences RQ2 (role reconfiguration)
- **design:** 150 scenarios (50 each difficulty 1/3/5).
- **status:** difficulty is FIXED at `3` in the evaluator form (removed control).
- **source_data:** `tables/D_focus_stats.csv`, `tables/D_posthoc.txt`
- **figure:** `figures/FigD1_difficulty_boxplots.png`

### D_focus_stats (Kruskal-Wallis)

| metric                       | p        | effect_eps2    | mean_d1 | mean_d3 | mean_d5 |
|------------------------------|----------|----------------|---------|---------|---------|
| patrolCount                  | <0.0001  | 1.000 (large)  | 1.00    | 1.00    | 0.00    |
| avgPatrolRouteLength         | <0.0001  | 0.760 (large)  | 2.36    | 2.38    | 0.00    |
| patrolCoverage               | <0.0001  | 0.760 (large)  | 0.340   | 0.345   | 0.000   |
| stationaryGuardCount         | <0.0001  | 0.365 (large)  | 0.00    | 0.66    | 0.62    |
| minHostageTerroristDistance  | <0.0001  | 0.287 (large)  | 3.00    | 3.51    | 3.93    |
| avgEntityDistanceFromEntry   | 0.323    | negligible     | 9.66    | 10.06   | 9.54    |
| avgHostageTerroristDistance  | 0.360    | negligible     | 8.87    | 9.72    | 9.37    |
| entityDepthVariance          | 0.820    | negligible     | 0.150   | 0.165   | 0.155   |

- **KEY_BEHAVIOUR:** at difficulty 5, patrolling guards convert to stationary → patrolCount & patrolCoverage collapse to 0. Min hostage-terrorist distance rises monotonically. Global spatial spread unaffected.

---

## FURNITURE_AND_DOOR_STATE
<!-- anchor: furniture-door -->

- totalFurnitureCount: mean 18.97, sd 3.33, range 13–28, CV 17.6%
- furnitureFloorCoverage: CV 26.6%
- doorStateDistance (pairs): mean 0.41, range 0–0.67
- lockedDoorFraction: 0.333 (one locked door consistently present)
- **NOTE:** companion grouping inflates furniture density vs earlier generator versions → furniture metrics NOT comparable across versions.

---

## EXPERIMENT_E — Validation Reliability
<!-- anchor: experiment-e -->

- **rq:** RQ3 (validation) — PRIMARY; also RQ1 spatial-validity clause
- **design:** 500 scenarios across full pipeline space, split by `withinFormRange`.
- **source_data:** `tables/E_reliability_summary.csv`, `tables/E_failure_categories.csv`, `tables/E_failure_by_scope.csv`, `tables/E_passrate_by_layoutType.csv`, `tables/E_passrate_by_roomCount.csv`, `tables/E_passrate_by_configTerroristCount.csv`, `tables/E_passrate_by_roomSizeCategory.csv`, `tables/E_problematic_combinations.csv`
- **figures:** `figures/FigE1_passrate_by_scope.png`, `figures/FigE3_passrate_heatmap.png`

### E_reliability_summary

| scope                                            | n   | pass | fail | pass_rate_pct |
|--------------------------------------------------|-----|------|------|---------------|
| Form-reachable (roomCount 3–5, terrorist 1–4) [PRIMARY] | 43  | 43   | 0    | 100.00        |
| Extended-range (outside form)                    | 457 | 373  | 84   | 81.62         |
| Overall (full pipeline space)                    | 500 | 416  | 84   | 83.20         |

### E_failures (all in extended range only)

| failure_category | count | pct_of_failures |
|------------------|-------|-----------------|
| EntityBounds     | 76    | 90.5            |
| EntityOverlap    | 8     | 9.5             |

- Pass rate ≥89% for roomCount 3–6, falls to ~78–81% for 7–15.
- Pass rate ≥88% for terroristCount 1–4, falls to 61% at 8 terrorists.
- Worst combos: high terrorist count in small rooms (e.g. 8 terrorists / small 4-room branching → 0% pass). Not form-reachable.
- **HEADLINE:** 100% pass for every configuration an evaluator can actually request.

---

## DIRECT_ANSWERS
<!-- anchor: direct-answers -->

- **RQ1:** Exp. B → every structural form param maps to intended layout metrics (large, significant). Exp. E → resulting layouts pass spatial validity 100% in form-reachable range. ⇒ evaluator params translate into valid, varied layouts.
- **RQ2:** Guard/patrol composition is deterministic+monotonic in terroristCount (Exp. B) and reconfigured by difficulty (Exp. D). ⇒ role/navigation context derived from structure, not arbitrary.
- **RQ3:** Exp. E → 100% validation pass for form-reachable configs; all failures confined to unreachable extended range, two well-characterised categories.
- **RQ4:** Exp. A → meaningful variation at production (placement/roles/furniture/doors/depth, CV up to 154%); topology deterministic per fixed set; high randomness marginal. Exp. C/D → difficulty large significant structural change (ε² up to 1.0), randomness small; both validated though controls withheld from form.

---

## LIMITATIONS
<!-- anchor: limitations -->

- **param_range:** form limits roomCount 3–5, terroristCount 1–4; pipeline supports wider ranges evaluators can't reach; reliability degrades only there.
- **withheld_controls:** difficulty, randomness, hostageRiskLevel validated but not evaluator-accessible in current build.
- **front_loaded_depth:** uses ceil(maxDepth/3) not design's ceil(maxDepth/2) → threats concentrated closer to entry than specified.
- **patrol_route_truncation:** routes collapsed to 2 waypoints at runtime (Module 2 PatrolLine exposes only start/end); multi-room routes present in Scenario.json but not fully consumed.
- **structural_scope:** single floor only; hostageCount fixed at 1.
- **furniture_comparability:** companion grouping raises density vs earlier versions → not comparable across generator versions.
- **entryType_scope:** affects entryPointCount only, not interior connectivity.
- **fixed_set_topology:** at a fixed production param set the layout topology is deterministic; Exp. A diversity = placement/role/furniture/door/depth diversity, NOT topological.

---

## ARTIFACT_INDEX
<!-- anchor: artifacts -->

### figures/ (provided PNGs)
- `FigA1_diversity_histograms_medium.png` — Exp A pairwise diversity (6 panels)
- `FigB1_layoutType_boxplots.png` — Exp B layoutType effects
- `FigB5_terroristCount_boxplots.png` — Exp B terroristCount effects
- `FigD1_difficulty_boxplots.png` — Exp D difficulty (8 panels)
- `FigE1_passrate_by_scope.png` — Exp E pass rate by scope
- `FigE3_passrate_heatmap.png` — Exp E pass rate layoutType × roomCount
- (also referenced, may exist: FigA2, FigA3, FigB2–B4/B6, FigC1, FigE2)

### tables/ (CSV ground truth)
- `A_descriptive_medium.csv`, `A_A2_pairwise_diversity_descriptive.csv`, `A_vs_A2_diversity_tests.csv`, `A_A2_CV_comparison.csv`
- `B_SUMMARY_traceability_matrix.csv`, `B_target_metrics_detail.csv`, `B_full_detail_all_metrics.csv`, `B_additional_significant_findings.csv`
- `C_focus_stats.csv`, `D_focus_stats.csv`
- `E_reliability_summary.csv`, `E_failure_categories.csv`, `E_failure_by_scope.csv`, `E_problematic_combinations.csv`, `E_passrate_by_*.csv`
