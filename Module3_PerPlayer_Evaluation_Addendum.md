# Module 3 — Population-Level Evaluation Addendum

**To:** Report author **From:** Manusha Dananjaya — Module 3 owner (Cognitive & Movement Tracking) **Project:** Dynamic Scenario Generation and Evaluation for VR-Based Military Training **Team:** Team Sentinels · University of Moratuwa **Extends:** `Module3_Report_Handover_Brief.md` (29 Jul 2026) — specifically resolves that brief's **Open Item 1Prepared:** 30 Jul 2026

---

## 0. How to use this document

This is a short addendum, not a replacement for the main handover brief. It documents one specific piece of new work done *after* the brief was written: extending the `/evaluation` dashboard page's "Module 3 — Cognitive" tab from a single-metric (reaction time only) population comparison into a full per-player, multi-metric movement evaluation, reusing the exact literature bands and citations the main brief already established. Read this alongside the main brief's Section 6 (per-session evaluation) — this document covers the population/cross-session layer that Section 6 explicitly says does not exist yet.

**Same three rules as the main brief apply:** no number below is invented — every benchmark band reused here is already in `lib/movementBenchmarks.js`'s `BENCHMARKS` table, and every citation is already in that file's `CITATIONS` object. Nothing new was added to either.

---

## 1. What this addendum covers

The main brief's Section 6 built a **per-session** evaluation: one trainee, one mission, compared against literature bands, computed live, no aggregation across sessions or players. Its own Open Item 1 flags this directly:

> *"Cross-session analysis is out of scope of the current code. Section 6/7 describe a real, live, per-session evaluation mechanism — if the report wants results that compare across sessions or trainees (trends, aggregate statistics), that needs a new aggregation step that does not exist yet; it is not something I can retroactively pull from existing code."*

This addendum **is that aggregation step.** It was built on top of an existing but narrower population-level page — `sentinels-aar/app/evaluation/page.jsx` (`EvaluationClient.jsx`) already had a "Module 3 — Cognitive" tab, but it only ever evaluated **one measure**: reaction time, against a single Hick's-Law expert/novice constant (0.30 s / 0.50 s), using a one-sample Wilcoxon test and a one-way ANOVA. Every other movement metric Module 3 captures (speed, crouch time, weapon-hold, hand travel, per-channel reaction time) stayed trapped in the per-session Movement tab, never aggregated across a player's sessions or compared statistically at all.

---

## 2. What was built

### 2.1 API layer — `sentinels-aar/app/api/evaluation/route.js`

- Imported `evaluateMovementAgainstExperts` from `lib/movementBenchmarks.js` — the **same function** the per-session Movement tab uses (main brief §6.1) — rather than writing a second evaluator. This guarantees the population-level numbers can never silently drift from what a trainee sees on their own session page.
- Extended the Mongoose `.select()` projection to pull `movementTrack.stats`, `.reactionStats`, and `.reactions.{channel,reactionTime}` alongside the fields already selected — deliberately excluding `movementTrack.samples` (the raw 5 Hz trace) to keep the population query lean across all sessions.
- Extended `metricsOf(session)` to call `evaluateMovementAgainstExperts(session)` and pull the raw `.traineeValue` out of 10 of its 12 metrics (`avgSpeed`, `maxSpeed`, `timeCrouched`, `headScanning`, `weaponHeldPct`, `handTravel`, `reactionHands`, `reactionMovement`, `reactionTrigger`, `threatsResponded`). Two were deliberately left out: `reactionOverall` — it's the same figure as the pre-existing `reactionTime` measure from a different source field, and showing both would be a confusing near-duplicate, not new information — and `reactionHead`, because no session has ever recorded a head-channel reaction (Unity's `ReactionTimeTracker` never tags one in the live data), so it would only ever render as an empty column.
- Added those 10 keys to `MODULE3_SCORE_KEYS`, which already drove the page's existing "combine all of a player's sessions, any AI tier" logic and its pooled-across-players logic — so per-player averaging and population pooling for the new metrics came for free from code that already existed for reaction time.

### 2.2 Shared library — `sentinels-aar/lib/movementBenchmarks.js`

- `evalMetric(key, value)` — previously a module-private helper only used by `evaluateMovementAgainstExperts()` internally — was exported. This is the only change to this file. It lets the client apply the exact same tiered good/watch/unvalidated verdict logic to an *aggregated* value (a player's multi-session average) that the per-session evaluator already applies to a single session's value, without duplicating the verdict rules.

### 2.3 Dashboard UI — `sentinels-aar/components/evaluation/EvaluationClient.jsx`

Two new sections were added to the existing Module 3 tab, after the pre-existing reaction-time-only sections (which were left unchanged):

- **"Body movement & reaction-channel metrics (per player vs literature)"** — a new `MovementBenchmarkTable` component. Rows are players (sessions combined across all AI difficulty tiers, matching how the existing reaction-time table already worked — Module 3 has no AI-difficulty independent variable, that's Module 2's own research question); columns are the 10 metrics from §2.1, each showing the player's combined average, its confidence tier (A/B/C superscript), and a colour-coded verdict from `evalMetric()`. A reference row above the player rows shows the literature band itself, and a `MovementBenchmarkSources` footer lists only the citations actually backing the metrics shown — the same "only cite what's used, deduplicated" pattern the per-session Movement tab's `BenchmarkSources` component already used.
- **"ANOVA Table — movement metrics (between players)"** — the existing `AnovaPanel` component (originally written only for reaction time) reused as-is against the 10 new metrics. No new statistical code was written for this table; it is literally the same one-way ANOVA implementation, applied to more measures.

---

## 3. Statistical methodology

### 3.1 Two distinct questions, kept deliberately separate

Same principle the main brief's §6.1 already established for the per-session verdicts, now applied at population level:

1. **"Is this trainee's average inside the range the literature/CQB doctrine describes?"** — answered by `evalMetric()`'s tiered band verdict (`below`/`within`/`above`, converted to `good`/`watch`/`unvalidated` by direction), *not* a hypothesis test.
2. **"Do players genuinely differ from each other, beyond their own session-to-session noise?"** — answered by a real one-way ANOVA (`lib/stats.js`'s `oneWayAnova()` — exact F-distribution p-value via the regularized incomplete beta function, not a normal approximation), each player as a group, that player's own sessions as the group's replicates.

### 3.2 Why band metrics are not run through a one-sample hypothesis test

The pre-existing reaction-time section on this page *does* run a one-sample Wilcoxon test against a single expert constant (0.30 s). The 10 new metrics deliberately do **not** get the same treatment, for the same honesty reason the main brief's §5.1 tier system exists: the literature for these metrics gives a *range* (e.g. avg speed 0.8–1.4 m/s), not a single point estimate. Picking the midpoint (or either edge) to run a one-sample test against would be inventing a number the cited sources don't actually claim. The band-verdict approach avoids that without giving up a defensible comparison — it is the same tier-honesty principle from the main brief, extended rather than compromised for the sake of having one more p-value to report.

### 3.3 Zero new numbers introduced

Every band, direction, and tier used in §~~2.3's ta~~ble is read directly from the `BENCHMARKS` table already documented in the main brief's §5.2 — nothing here adds, adjusts, or reinterprets a single citation. The only thing that changed is *scope*: from one session at a time to a player's combined average, and from one player at a time to all players compared against each other.

---

## 4. Results on the live dataset (11 players, 45 sessions, as of 30 Jul 2026)

### 4.1 Reaction time (pre-existing section, included here for contrast)

- Population vs. expert: trainee mean **0.65 s vs. expert 0.30 s** (Hick's Law), one-sample Wilcoxon p &lt; 0.001, r = 0.67 — significantly slower than the benchmark.
- Between players: one-way ANOVA F(10, 34) = 1.22, **p = 0.316, not significant**. Only one player (P0007, 0.30 s exactly) lands in the "Expert" band on the novice→expert scale; every other player is "Novice." Read together, these two results say the same thing two ways: trainees are *uniformly* slower than the cited benchmark — it is a group-level effect, not a few weak individuals pulling the average down.

### 4.2 Movement & reaction-channel metrics (new)

| Metric | k (players) | N (sessions) | F | p | Verdict |
| --- | --- | --- | --- | --- | --- |
| Avg Speed | 8 | 34 | 1.43 | .238 | n.s. |
| Peak Speed | 8 | 34 | 0.50 | .828 | n.s. |
| Time Crouched | 8 | 34 | 1.58 | .187 | n.s. |
| Head Scanning (peak °/s) | 8 | 34 | 0.70 | .672 | n.s. |
| **Weapon In Hand** | 11 | 45 | **11.87** | **&lt; .001** | **significant** |
| **Hand Travel** | 11 | 45 | **6.68** | **&lt; .001** | **significant** |
| Weapon-Raise Reaction | 5 | 8 | 0.28 | .875 | n.s. |
| Move-to-Cover Reaction | 5 | 7 | 1.14 | .516 | n.s. |
| Fire-Back Reaction | 8 | 29 | 1.36 | .277 | n.s. |
| Threats Responded | 8 | 32 | 1.55 | .199 | n.s. |

**Weapon In Hand and Hand Travel are the only two metrics with a statistically significant between-player difference** at the current sample size. Every other metric returns a non-significant result — which, given k as low as 5 players and N as low as 7 sessions for the weapon-raise/move-to-cover channels, should be read as *not yet detected*, not *proven equal*.

---

## 5. Data-quality findings surfaced by this evaluation

Building the population view surfaced three issues in the underlying data/pipeline that were invisible at single-session granularity. None of these are new bugs introduced by this addendum — they are pre-existing characteristics of the captured data or of `evaluateMovementAgainstExperts()` itself, made visible by aggregating across players for the first time.

1. **Incomplete movement telemetry for some sessions.** 11 of the 45 sessions (all of P0003/P0004/P0005's sessions) have `reactionTime` recorded but no `movementTrack.stats` at all — `avgSpeed`/`maxSpeed`/`timeCrouched`/`headScanning` are `null` for those players. This is why the movement ANOVA table's `k`/`N` (8 players / 34 sessions) is smaller than the reaction-time table's (11 players / 45 sessions) — worth stating explicitly in the report rather than letting a reader assume the sample sizes match.

2. **A null-vs-zero inconsistency in** `evaluateMovementAgainstExperts()`**.** For the same three players, `weaponHeldPct` and `handTravel` render as **0%** / **0 m** rather than "no data" — because the function's fallback arithmetic (`stats.timeWeaponHeld || 0`, `(stats.leftHandDistance || 0) + (stats.rightHandDistance || 0)`) silently treats a missing `movementTrack.stats` object as *zero movement*, while the four other stats-derived metrics in the same function correctly fall back to `null`. This makes those three players look like they never held their weapon, when the real explanation is their sessions were never instrumented for movement at all. This is a pre-existing characteristic of the shared per-session evaluator (main brief §6.1), not something introduced by this addendum, but it now visibly affects the per-player table and is worth fixing before the number is quoted anywhere.

3. **Head Scanning is very likely measuring an artifact, not scanning skill.** Every player with data shows 273–642°/s peak angular head speed against a cited 30–100°/s saccadic-velocity band \[3\]\[4\] — 3 to 6× over the top, for all 8 players independently, all reading as "good" because the metric is `higherBetter`. Real saccadic/head-scan behaviour does not plausibly run that consistently over-range across an entire trainee population; the more likely explanation is a units mismatch or a VR snap-turn/frame-spike artifact in how `peakAngSpeed` is sampled. The same caution applies to **Peak Speed** (grand mean 6.49 m/s across the population, one player peaking at 18.4 m/s — not sustainable human indoor movement) — it's tier C so it carries no fabricated verdict, but it is still feeding a noisy, non-significant ANOVA result that shouldn't be over-interpreted.

---

## 6. Limitations specific to this addendum

- **Small per-metric sample sizes.** k ranges from 5 (Weapon-Raise/Move-to-Cover Reaction) to 11 (Weapon In Hand/reaction time), and several ANOVAs run on single-digit N. Non-significant results here are underpowered, not confirmatory.
- **No literature band exists for Hand Travel, Peak Speed, or Time Crouched** (tier C, per the main brief's §5.2 table) — their ANOVA results describe *whether players differ*, never *whether they're good*.
- **This addendum does not fix** the null-vs-zero inconsistency or the Head Scanning measurement concern described in §5 — both are flagged for a follow-up change to `lib/movementBenchmarks.js`, not yet applied.
- **Still no recorded expert baseline** — same limitation the main brief states in its §9.1; this addendum compares trainees to literature bands and to each other, never to a human expert who has played this simulator.
- **Reaction-time's Wilcoxon test and the movement metrics' band verdicts are not on the same statistical footing**, by design (§3.2) — a report should not present both as "the same kind of test," even though they appear in the same tab.

---

## 7. How this closes Open Item 1

The main handover brief left cross-session/cross-player aggregation as an explicit open question, deferred rather than built. This addendum is that missing aggregation step: it reuses the existing per-session evaluator and citation set without modification, adds a genuinely new statistical layer (population-level ANOVA across 10 additional metrics), and — in the process of actually running it against live data — surfaced three concrete data-quality issues that would not have been visible from any single session in isolation. If the report includes a "future work" or "what changed since the handover brief" note, this is the item to reference.

---

## 8. References

Identical to the main brief's §10, entries \[1\]–\[9\] — no new citation was added for this addendum; every benchmark reused here was already backing a per-session metric.

1. Bohannon RW, Williams Andrews A (2011). Normal walking speed: a descriptive meta-analysis. *Physiotherapy*, 97(3), 182–189.
2. US Army FM 3-06.11 / USMC MCWP 3-11.3 — Combined Arms Operations in Urban Terrain.
3. Scholarpedia — Human saccadic eye movements. <http://www.scholarpedia.org/article/Human_saccadic_eye_movements>
4. Evidence of elevated situational awareness for active duty soldiers during navigation of a virtual environment. PMC11086823.
5. Force Science Institute — Action vs. reaction: the shoot-first fallacy.
6. Comparison between Auditory and Visual Simple Reaction Times. SCIRP.
7. Nieuwenhuys A et al. (2022). Shoot or Don't Shoot? Tactical Gaze Control and Visual Attention Training Improves Police Cadets' Decision-Making Performance in Live-Fire Scenarios. *Frontiers in Psychology*, PMC8905363.
8. ALERRT / Texas State University; VirTra — Time Analysis of a Certified Peace Officer's draw-and-fire response time.
9. Ibrahim F, Feildboy E, Nagy D, Huber Y, Hennig J, Herzberg PY (2024). Predicting closed quarters battle capability. *Military Psychology*, 38(1), 1–12. (= PMC12785213)
