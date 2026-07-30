# Module 3 — Report Handover Brief

**To:** Report author
**From:** Manusha Dananjaya — Module 3 owner (Cognitive & Movement Tracking)
**Project:** Dynamic Scenario Generation and Evaluation for VR-Based Military Training
**Team:** Team Sentinels · University of Moratuwa
**Contribution period:** 18 July 2026 – 29 July 2026 (Module 3 proper). Earlier environment/base-camp work (Jan 2026) predates Module 3 and is out of scope for this brief.
**Prepared:** 29 July 2026

---

## 0. How to use this document

This is a self-contained brief covering my individual contribution to the project: `Assets/CognitiveTracking/` (`TeamSentinels.CognitiveTracking`, the project's Module 3) and the literature-grounded interpretation layer built on top of it. Everything needed to write the Module 3 sections — scope, design rationale, citations, evaluation, limitations — is here, and Section 6 is written directly from the shipped code (`lib/movementBenchmarks.js`, `lib/workloadReasoning.js`, `lib/simTlx.js`, `lib/cognitiveDerived.js`), not from a design plan.

**Two companion files** already exist in the repo root and back this brief directly:

| File | Contains | Use when |
|---|---|---|
| `MODULE4_MOVEMENT_VALIDATION_METHODOLOGY.md` | Full metric-by-metric derivation of the movement/reaction-time benchmark bands, confidence tiers, and citations | Writing the movement-metrics half of Design & Implementation / Evaluation |
| `MODULE4_WORKLOAD_REASONING_METHODOLOGY.md` | Full derivation of the SIM-TLX reasoning/prediction layer and citations | Writing the workload half of Design & Implementation / Evaluation |

### Three rules, please

1. **No number appears in the report unless it is in the `BENCHMARKS` table of `movementBenchmarks.js`, the `REASONING_RULES`/zone thresholds of `workloadReasoning.js`, or one of the two methodology docs above.** Ask me rather than estimate.
2. **No citation is added to a claim that doesn't already have one in the code's own `CITATIONS` objects** (§10 lists exactly these — every reference below is grep-able in the shipped source, nothing extra).
3. **Section 8 (notable engineering trade-offs) and Section 9 (limitations) are not optional.**

---

## 1. Module 3 in brief

Module 3 is the trainee **cognitive and movement measurement subsystem** of the VR hostage-rescue trainer. It does not generate the scenario (Module 1) and does not run the mission logic or host the after-action review (Module 4) — it sits alongside gameplay and *watches the trainee*: capturing body movement, weapon-hold state, and multi-channel reaction time to enemy-caused threat stimuli, entirely from the existing XR head/hand tracking already present for gameplay (no extra hardware, no per-system instrumentation of the rest of the game). It also gives the trainee live proprioceptive feedback in-headset via a mirrored-avatar HUD, and — going beyond pure capture — turns the raw numbers into an interpretable, literature-grounded assessment rather than a bare readout, automatically, on every session.

This work exists because, as of the 17 July 2026 engineering review of the After-Action Review system (`MODULE4_RESEARCH_REVIEW.md` / `MODULE4_REVIEW.md`), **the trainer had no genuine cognitive measurement at all**: `LogCognitiveUpdate` was only ever called with hardcoded test literals, so every real session's "Cognitive Analysis" dashboard section was empty and meaningless. Module 3, built over the following twelve days, is the fix — and its research contribution is not that movement can be logged, but that:

- movement/reaction data is captured **non-invasively and read-only** against the existing XRI rig (no gameplay or hand-pose assets are ever modified);
- reaction-time detection is **validated against false positives** by construction (motion-already-underway disqualification, a human-reaction-time floor, burst suppression) rather than a naive threshold crossing;
- every derived number — whether a movement metric or a SIM-TLX subjective-workload composite — is shown with an **explicit, honest confidence tier** grounded in cited research, and metrics with no literature backing are shown as raw values with no fabricated comparison, live, on every session, with no separate "run evaluation" step.

---

## 2. Scope — what Module 3 owns

**In scope (mine):**
- `Assets/CognitiveTracking/` in full: `CognitiveMovementRecorder.cs`, `CognitiveMirrorHud.cs`, `MirrorDummyAvatar.cs`, `HudWidgetKit.cs` (+ `HudDummy`/`HudPanel` shaders), `ReactionTimeTracker.cs`
- The movement/reaction data model (`MovementSample`, `MovementStats`, `ReactionSample`, `ReactionStats`, wrapped in `MovementTrack`) — the schema is Module 3's even though the C# file lives under `Assets/Module4/Data/` for serialisation alongside `SessionSummary`
- SIM-TLX subjective-workload integration end-to-end: in-app questionnaire (`app/simtlx/[id]/page.jsx`, `components/simtlx/SimTlxClient.jsx`), scoring (`lib/simTlx.js`), cognitive-fallback derivation from telemetry (`lib/cognitiveDerived.js`), API routes, and the `Session` schema extension that stores responses
- The literature-benchmark interpretation layer: `lib/movementBenchmarks.js` (`evaluateMovementAgainstExperts`) and `lib/workloadReasoning.js` (`REASONING_RULES`, `checkPrediction`)
- Dashboard delivery of all of the above: `MovementTab.jsx` (including the top-down movement-path map and reaction-time scatter plot), `SimTlxTab.jsx`, `SimTlxTrend.jsx`, `MetricCard.jsx`, and their stylesheets
- The two methodology documents (`MODULE4_MOVEMENT_VALIDATION_METHODOLOGY.md`, `MODULE4_WORKLOAD_REASONING_METHODOLOGY.md`) and the per-session trainee-vs-literature evaluation mechanism described in Section 6

**Out of scope (other members):**
- Scenario generation, layout/entity/role/navigation pipeline, and its 2000-scenario evaluation study — **Module 1** (Pramoth Dilshan)
- Runtime NPC behaviour/coordination, and the Module 4 host application — event bus, `SessionLogger` core, incident extraction, replay recorder, hostage distress index, dashboard shell — **Module 4** (primarily Navindu Chathuranga; per-file `git log` shows `SessionLogger.cs` and the `Module4Integration/` mission-flow scripts are majority his). Module 3 only *feeds* `SessionLogger` (`SetMovementTrack`, `LogReactionTime`); Module 4 serialises and hosts whatever Module 3 gives it.
- NPC-side cognitive/behaviour analysis (**Module 2** — a separate team member's scope; not touched by this work)

> **Note on boundary:** the dashboard/analysis files above live inside the `sentinels-aar` (Module 4) codebase because that's where all dashboards live — the same cross-boundary pattern Module 1's brief describes for its own `SceneBuilder`/HTTP bridge work. I wrote both the Module 3 capture layer *and* its Module 4-hosted display, so please don't split this contribution across two people's sections — see Open Item 1.

---

## 3. Research questions

| RQ | Question | Principally answered in | Evidence |
|---|---|---|---|
| **RQ1** | Can trainee body movement (position, scanning, crouch, weapon-hold) be captured accurately from existing XR tracking alone, without instrumenting any other gameplay system? | Design & Implementation §4.1 | `CognitiveMovementRecorder` + `CognitiveMirrorHud` read-only rig discovery; live in the shipping scene since 19 Jul |
| **RQ2** | Can multi-channel reaction time to threat stimuli be measured validly — i.e. without false positives from tracking jitter, continuing motion, or burst events — using only body-tracking signals? | Design & Implementation §4.2 | `ReactionTimeTracker`'s three validity rules |
| **RQ3** | In the absence of a recorded expert benchmark, can trainee movement/reaction performance be meaningfully and *honestly* evaluated against published research? | Evaluation §6.1–6.2 | `movementBenchmarks.js`'s 3-tier confidence system, computed live on every session with movement telemetry |
| **RQ4** | Can subjective workload (SIM-TLX) be automatically explained from session facts and cross-checked against the session's own objective outcomes, rather than shown as an unexplained number? | Evaluation §6.4 | `workloadReasoning.js`'s reasoning rules + self-consistency `checkPrediction()`, computed live on every completed questionnaire |

---

## 4. What was built

### 4.1 Movement capture & mirror HUD (18–19 Jul 2026) — PR #46

`CognitiveMovementRecorder` samples at a configurable rate (default **5 Hz**, ≈3,000 samples over a 10-minute mission) once a Module 4 session is active. Each `MovementSample` records world-space head/left-hand/right-hand position, head yaw/pitch, horizontal head speed, yaw angular speed, a **continuous crouch estimate** (0 standing → 1 fully crouched, derived from head height against a *running, self-calibrating* standing-height baseline rather than a fixed constant — so it adapts per player rather than assuming a canonical height), a weapon-in-hand bitmask (left/right), and health fraction. Session-level aggregates (`MovementStats` — total distance, avg/max speed, time moving/still/crouched, crouch-event count, head-yaw accumulation, avg/peak angular speed, per-hand travel, weapon-held time) update **incrementally on every sample** rather than being recomputed at session end, so the summary is current even if a session ends abnormally.

`CognitiveMirrorHud` is an in-headset "modern soldier" HUD, entirely built at runtime on translucent panels via `HudWidgetKit` (custom `HudDummy`/`HudPanel` shaders), rigidly locked to the player's view like a real HMD overlay:
- bottom-left — a live mirrored mannequin (`MirrorDummyAvatar`) reflecting head/hand/crouch/lean/turn, plus a vertical health bar;
- bottom-centre — a yaw-driven compass tape with a pitch marker;
- bottom-right — a held-weapon readout (silhouette, cleaned weapon name, live ammo count), shown only while a weapon is actually grabbed.

The XR rig (head/hand transforms, held-weapon interactors, player-health component) is discovered **read-only** via a breadth-first search over the rig hierarchy that name-matches "left/right" + "controller/hand" while excluding known decoys (teleport ray, poke interactor, offset/anchor transforms, stabilized/smoothing helpers) — necessary because the project's XRI Starter Kit rig has no single canonical hand-transform to bind to directly, and because modifying XRI kit assets directly was known to break existing hand poses ([[hostage-door-safezone-fixes]]).

### 4.2 Reaction-time tracking (19 Jul 2026) — PR #48

`ReactionTimeTracker` measures trainee response latency to three enemy-caused stimulus types — `PlayerSeen`, `GunshotHeard`, `TargetConfirmed` (filtered so the trainee's own gunfire is never misclassified as enemy-sourced) — across **four independent response channels**, each derived from signals the recorder already computes, with no additional sensors:

| Channel | Trigger condition |
|---|---|
| head | angular speed > 90°/s while gaze angle to the threat is closing, **or** the threat enters a 25° gaze cone from clearly outside it |
| hands | either hand's frame-to-frame speed exceeds 1.0 m/s (weapon raise) |
| movement | horizontal body speed exceeds 0.6 m/s (moving to cover) |
| trigger | the trainee's own `ShotFired` event |

Reaction time = first channel to cross its threshold − stimulus time. **Three validity rules** stop a naive threshold-crossing design from generating false positives:
1. A channel already above its threshold *at the moment the stimulus fires* is disqualified for that stimulus — motion already underway is not a reaction to it.
2. The first **80 ms** after a stimulus is never counted as a valid response — faster than plausible human reaction time (every published simple-RT figure the movement-validation methodology later cites is ≥250 ms), so anything under that is tracking noise, not a genuine reaction.
3. While a response window (default **3 s**) is open, new stimuli are ignored; after it closes, a **2 s** cooldown suppresses burst stimuli (e.g. automatic enemy fire) from spamming duplicate samples.

A stimulus with no response inside the window is recorded as an explicit **miss** (`reactionTime = -1`), not silently dropped, so response-rate can be computed later. Each measurement appends to `movementTrack.reactions`, and per-channel running statistics (mean/median/best/worst RT, per-channel response counts, average angle-to-threat) update incrementally in `ReactionStats`.

### 4.3 SIM-TLX subjective workload integration (25 Jul 2026) — PR #51

Brought the validated **SIM-TLX** (Simulation Task Load Index) questionnaire into the product end to end, as a self-report complement to the objective movement/reaction-time measures above:
- `lib/simTlx.js` — composite/subscale scoring and the `workloadLevel()` zone function
- `lib/cognitiveDerived.js` — fallback cognitive-score derivation directly from telemetry when explicit ratings are unavailable
- New API routes (`app/api/sessions/[id]/simtlx/route.js`, `pending-simtlx/route.js`, `seed/route.js`) and `Session.js` schema extension to store responses
- New UI: in-app questionnaire page, `SimTlxClient.jsx`, `SimTlxTab.jsx`, `SimTlxTrend.jsx` (trend chart across a trainee's sessions)

### 4.4 Movement & workload interpretation layer (29 Jul 2026) — PR #59

The research capstone: turning the raw numbers from §4.1–4.3 into something a trainee can actually judge themselves against, honestly, without a recorded expert dataset. Full mechanics in Section 6.
- `lib/movementBenchmarks.js` — a `BENCHMARKS` table (per-metric reference band + **confidence tier** + citations) and `evaluateMovementAgainstExperts(session)`
- `lib/workloadReasoning.js` — `REASONING_RULES` and `checkPrediction()`
- The two methodology documents backing every band and rule with citations (Section 5)

---

## 5. Design decisions and their citations

### 5.1 The confidence-tier system (applies to both movement and workload)

| Tier | Meaning |
|---|---|
| **A** | Direct empirical figure from a study measuring materially the same task |
| **B** | Literature proxy — closest available published construct, applied to a related-but-different task, explicitly flagged as a proxy in the UI |
| **C** | No quantified literature found. The trainee's value is shown as a plain metric with no expert range attached — never a fabricated number |

This tiering is the single design choice that makes RQ3 answerable honestly — it is what stops a rough proxy from being presented with the same authority as a direct measurement. In the shipped UI, tier-C metrics simply render with no benchmark line under them (§6.2) — the A/B/C legend is explained once, in a footer card, rather than repeated per metric.

### 5.2 Movement metrics (from `MODULE4_MOVEMENT_VALIDATION_METHODOLOGY.md`)

| Metric | Tier | Reference band | Cite |
|---|---|---|---|
| Avg speed | B | 0.8–1.4 m/s | **[1]** gait-speed meta-analysis (upper bound) + **[2]** CQB movement doctrine (lower bound) |
| Peak speed | C | none claimed | — |
| Time crouched / crouch count | C | none claimed | **[2]** establishes *that* cover matters, not *how much* |
| Head scanning (peak angular speed) | B | 30–100°/s | **[3]** saccadic-velocity range + **[4]** active-duty vs civilian scan-speed study |
| Weapon-in-hand (% of mission) | B | 90–100% | **[2]** weapon-ready-posture doctrine |
| Hand travel (combined) | C | none claimed | — |
| Head/orienting reaction | A | 0.25–0.40 s | **[5]** Force Science + **[6]** SCIRP visual simple-RT |
| Weapon-raise reaction | A | 0.40–0.80 s | **[7]** Nieuwenhuys et al. (2022) gaze-training cadet study |
| Move-to-cover reaction | B | 0.50–1.00 s | interpolated — no direct study found |
| Fire-back reaction | A | 0.80–1.80 s | **[8]** ALERRT/VirTra draw-and-fire analysis |
| Overall avg reaction | A | 0.40–1.00 s | **[7]** anchor, aggregate interpretation |
| Threats responded (%) | B | 90–100% | **[4]** target-fixation study + **[9]** CQB mistake-rate as expert discriminator |

**[9]** (Ibrahim et al., *Predicting closed quarters battle capability*) is the closest existing academic precedent for the whole approach: expert raters scoring trainee CQB performance on five dimensions (tactical behaviour, weapon handling, gaze behaviour, response time, mistakes) that separate special-forces from non-specialised soldiers with large effect sizes (Cohen's d −1.099 to −1.993). Module 3's metrics map onto essentially the same five dimensions — the justification for treating them as a valid expert/novice axis at all.

### 5.3 Workload reasoning & prediction (from `MODULE4_WORKLOAD_REASONING_METHODOLOGY.md`)

| Composite / claim | Theory or finding | Cite |
|---|---|---|
| Physical Demands rises with measured movement, independently corroborating self-report | cognitive load measurably changes gait (cadence, ground time) | **[10]** |
| Mental Demands rises with concurrent threat-event count | Multiple Resource Theory — concurrent tasks drawing the same processing resource interfere | **[11]** |
| Frustration rises with missed threats / friendly fire | direct construct match — SIM-TLX's own Frustration item, validated to move specifically on this manipulation | **[12]** |
| Temporal Demands rises with stimulus *rate*, not just count | multitasking workload study | **[13]** |
| Task Complexity rises with scenario element interactivity | Cognitive Load Theory — intrinsic load is a function of concurrently-held components | **[14]** |
| Situational Stress rises after a missed/mishandled engagement | Attentional Control Theory — anxiety impairs processing *efficiency* before *effectiveness* | **[15]** |
| Workload → performance-zone mapping (under-aroused / near-optimal / overload-risk) | inverted-U stress-performance relationship, measured in elite shooters | **[16]** |
| Physical-demand corroboration via movement | same marksmanship/cognitive-load study as above | **[10]** |

### 5.4 Claims with no citation — engineering decisions

Present as implementation decisions, not literature findings:
- The 80 ms minimum-valid-RT floor, per-channel thresholds (90°/s, 1.0 m/s, 0.6 m/s), 3 s response window, 2 s cooldown — calibrated by playtesting against the general shape of the RT literature in §5.2 (all cited simple-RT figures are ≥250 ms, so 80 ms is a conservative noise floor, not a literature-derived value itself)
- The read-only rig-discovery heuristic (name-matching + exclusion list) in `CognitiveMirrorHud`
- `workloadReasoning.js`'s specific rule thresholds (e.g. "≥3 rooms and ≥2 hostiles") — reasonable engineering judgment calibrated to this project's scenario scale, explicitly flagged as such in the methodology doc itself
- `cognitiveDerived.js`'s proxy-score weights and ceilings (0.7/0.3 split for stability, 0.6/0.4 for attention, the 120°/s and 500°/s angular-speed ceilings, the 2.5 s "fully inattentive" reaction-time ceiling) — engineering judgment, not derived from a study (§6.3)

---

## 6. Evaluation methodology — how a session is actually judged

Every derived assessment in Module 3 is a **pure function**: it takes one session document and returns a structured verdict, computed on the fly in the dashboard the moment the relevant tab renders — there is no offline batch job, no scheduled evaluation script, and no persisted "results" collection. All four live in `sentinels-aar/lib/` and are called directly from the tab components. Nothing here runs a statistical hypothesis test — no t-test, ANOVA, or significance test is computed anywhere for movement or cognitive data (the project's only such tooling, `eval-service/` with pandas/scipy/pingouin, belongs to Module 1's scenario-generation study and has no Module 3 caller). What Module 3 evaluates against is: (a) fixed literature-derived reference bands, and (b) each session's own internal consistency.

### 6.1 Movement & reaction-time benchmark comparison — `movementBenchmarks.js`

`evaluateMovementAgainstExperts(session)` reads `session.movementTrack.stats` and `.reactionStats` and, for each of the 12 metrics in §5.2's table, does two things:

1. **`clampVerdict(value, range)`** — `below` / `within` / `above` the literature band, or `unvalidated` when the metric has no band (tier C) or the session has no value for it.
2. **`severityFor(direction, verdict)`** — converts that verdict into a `good` / `watch` / `unvalidated` colour, using a per-metric `direction` flag (`band`, `higherBetter`, `lowerBetter`). This is the actual design point, not just a threshold check: for a `lowerBetter` metric like reaction time, a value *below* the band's minimum is `good`, not a warning — the code deliberately separates "which side of the band you're on" from "whether that's a bad result," so a trainee who reacts faster than the cited expert range is never shown a red flag for it.

Two values are derived inline before evaluation, since the raw telemetry stores absolutes rather than the percentages/sums the bands are defined against: `weaponPct = timeWeaponHeld / missionDuration × 100`, and `handTravel = leftHandDistance + rightHandDistance`. Per-channel reaction time is the mean of that channel's entries in `movementTrack.reactions`.

### 6.2 How the evaluation actually surfaces in the dashboard

`MetricCard` accepts an optional `benchmark` prop and renders `expert: {range} · {verdict}ᵗⁱᵉʳ` under the raw value — but only when `severity !== 'unvalidated'`. **Tier-C metrics (peak speed, time crouched, hand travel) render with no benchmark line at all**; the raw value stands alone. The A/B/C legend is explained once, in a single footer card (`BenchmarkSources`, at the bottom of the Movement tab), which also dynamically lists only the citations actually backing *this session's* evaluated metrics — collected into a `Set` of citation IDs so nothing unused is shown — each linked to its source URL. Reaction-time channels get an additional dedicated view (`ChannelBenchmarkStrip`): each channel's session-average RT placed next to its own tier band, alongside a scatter plot of every individual stimulus→response event colour-coded by which channel answered it.

### 6.3 Cognitive-state proxy scores — `cognitiveDerived.js`

`deriveCognitiveScores(session)` first checks whether Unity's own `SessionLogger.LogCognitiveUpdate` path produced a genuine nonzero `cognitiveSummary` (`stabilityScore`/`attentionScore`/`movementInitiationScore` > 0). In every real uploaded session this check fails — that call site exists in C# (`CognitiveSummary.cs`) but is never invoked outside test harnesses, so the field is always zero on real data. Rather than showing a flat 0%, the function derives three 0–1 proxy scores **directly from the same `movementTrack` telemetry Module 3 already captures**:

- `stabilityScore = clamp01(1 − (0.7·avgAngSpeed/120 + 0.3·peakAngSpeed/500))` — steadier gaze / less snap-turning scores higher; a peak spike is weighted lower than the sustained average so one fast turn doesn't dominate an otherwise steady session.
- `attentionScore = 0.6·(respondedCount/stimulusCount) + 0.4·(1 − avgReactionTime/2.5)` when threat stimuli occurred that session; falls back to the reaction-speed term alone when none did.
- `movementInitiationScore = timeMoving / (timeMoving + timeStill)` — share of the mission spent actively moving, as a proxy for decisiveness under pressure.

This is the path every real session actually takes (`source: 'derived'`) — a working substitute for a classifier the Unity side never wired to live gameplay, not a stub. It deliberately stops short of the Unity data model's `estimatedCognitiveLoad`/`estimatedStressLevel` low/medium/high labels: that classifier (`CognitiveSummary.DeriveLabels()`) exists in C# but is dead code on the same never-called path — and independently of ever running, it mixes reaction time in raw *seconds* directly against 0–1 scores at fixed 0.33/0.66 cutoffs (`load = RT×0.5 + (1−stability)×0.5`), so any reaction time at or above roughly 0.66 s — which covers most of the "good" end of the reaction-time literature cited in §5.2 — pins both load and stress to `"high"` regardless of everything else. `cognitiveDerived.js` does not reuse that formula for exactly this reason.

### 6.4 Workload reasoning and self-consistency check — `workloadReasoning.js`

`evaluateWorkloadReasoning(session)` requires `session.simTlx.derived` (the trainee must have completed the post-mission questionnaire) and produces two things:

- **Per-composite reasoning.** `REASONING_RULES` holds 8 `{test, factor, citationIds}` rules across the three tracked composites (2 for Mental & Physical Demands, 3 for Temporal Demands & Frustration, 3 for Task Complexity & Situational Stress). A rule contributes its explanatory sentence only if `test(session)` is true for that session's own numbers — e.g. Task Complexity's element-interactivity rule fires only when `roomCount(session) >= 3 && terroristCount(session) >= 2`, where both counts come from the session's own `layout.rooms` and the distinct terrorist actor IDs seen in `npcStateChanges` (deliberately not `scenarioConfig`, which a code comment notes is declared on the schema but never actually populated by the current Unity build). No rule ever renders generic filler text.
- **Zone prediction + falsifiable check.** `ZONE_BY_WORKLOAD()` buckets the session's `overallWorkload` (0–100, the same thresholds `simTlx.js`'s `workloadLevel()` uses) into under-aroused (<25) / near-optimal (25–74) / overload-risk (≥75), each with a one-line literature rationale. `checkPrediction()` then cross-checks that predicted zone against **the same session's own outcome fields** — `performance.accuracyScore`, `performance.friendlyFireCount`, `performance.missionSuccess` — and against the reaction-time/response-rate severities computed independently in §6.1 for that identical session (it calls `evaluateMovementAgainstExperts(session)` itself, rather than trusting a pre-computed value, so the Movement and Workload tabs can never silently disagree). The result is `{matched, evidence[], note}`; when a predicted overload risk *doesn't* show up in the outcome data, the note explicitly reads it as possible compensatory effort (citing Attentional Control Theory) instead of forcing a match.

### 6.5 SIM-TLX scoring itself — `simTlx.js`

`computeSimTlxDerived(ratings)` validates all 9 subscale ratings (0–20 each), takes the mean of each composite's two constituent dimensions, converts to a 0–100 scale (`toPct`), and takes the mean of all 9 for `overallWorkload` — a standard unweighted "Raw TLX" administration (Hart, 2006), not the SIM-TLX paper's original pairwise-comparison weighting step. `workloadLevel()`/`workloadColor()` bucket the overall score into Low/Moderate/High/Very High at the same 25/50/75 cutoffs `checkPrediction()` uses, so the two never disagree about what "high workload" means for a given number.

---

## 7. What this evaluation approach covers, in one pass

| Mechanism | Reads | Produces | Renders in |
|---|---|---|---|
| Movement/RT benchmark comparison (§6.1–6.2) | `movementTrack.stats`, `.reactionStats` | 12 tiered verdicts — 4× tier A, 5× tier B, 3× tier C | Movement tab: metric cards + channel benchmark strip |
| Cognitive proxy scores (§6.3) | `movementTrack.stats`, `.reactionStats` | stability / attention / movement-initiation, 0–1 | Summary tab |
| SIM-TLX composite scoring (§6.5) | 9 raw 0–20 self-report ratings | 3 composites + overall workload, 0–100 | Workload tab |
| Workload reasoning + prediction check (§6.4) | SIM-TLX derived scores, session outcome fields, movement benchmark severities | per-composite factors, predicted zone, matched/unmatched evidence | Workload tab |

Every row runs automatically per session, the instant the relevant input exists — movement telemetry for the first two rows, a completed SIM-TLX questionnaire for the last two — with no separate "run evaluation" step and no dependency on a second session to compare against. The three metrics with no literature band (peak speed, time crouched, hand travel) are shown as plain values by design, not as an incomplete feature (§5.1, §6.2). The workload-reasoning and movement-benchmark mechanisms are cross-wired rather than independent: `workloadReasoning.js` imports and calls `evaluateMovementAgainstExperts` directly, so the Workload tab's prediction check and the Movement tab's own verdicts are always computed from the same evaluation, never two separately-drifting copies of it.

---

## 8. Notable engineering trade-offs — please include these

| Decision | What it stops | Detail |
|---|---|---|
| Multi-channel reaction-time detector (head/hands/movement/trigger) with 3 validity rules, instead of a single velocity/hand-speed threshold | A naive single threshold would register tracking jitter or continuing motion as a false "reaction" | §4.2, §6.1 |
| `cognitiveDerived.js` computes its own normalized stability/attention/movement-initiation formulas rather than reusing `CognitiveSummary.DeriveLabels()` | The Unity-side classifier mixes reaction time in raw seconds with 0–1 scores at fixed cutoffs, so any RT ≥ ~0.66 s pins both load and stress to `"high"` regardless of anything else | §6.3 |
| Tier-C metrics (peak speed, time crouched, hand travel) are shown as plain values with no benchmark line, rather than a fabricated range or an "unvalidated" chip on every card | Repeating a caveat on every card would be noisier than explaining the tier system once, in one footer | §6.2 |
| `checkPrediction()` reads session outcome fields and calls `evaluateMovementAgainstExperts()` itself rather than accepting a pre-computed verdict as a parameter | Two tabs silently disagreeing about the same session's reaction-time severity | §6.4 |

---

## 9. Limitations

### 9.1 Constrain a reported result — must appear in Evaluation or Discussion

- **No expert baseline has been recorded through this simulator.** Every movement/RT band is a literature reference, not a measurement of real experts playing this scenario set.
- **Head yaw ≠ eye gaze.** The head-scanning benchmark compares a head-tracking metric to eye-saccade literature; a trainee could hold their eyes still while yawing their head, or vice versa.
- **Tier C metrics (peak speed, crouch behaviour, hand travel) have no expert comparison at all** — a deliberate choice over fabricating a number.
- **The cognitive proxy scores (§6.3) are heuristic formulas, not a validated psychometric instrument.** Their weights and ceilings are engineering judgment (§5.4), and they are not the same thing as the Unity-side `estimatedCognitiveLoad`/`estimatedStressLevel` labels, which remain unbuilt on any code path that actually runs.
- **SIM-TLX reasoning is self-report, not physiological measurement.** It explains plausible drivers; it does not prove causation for any individual session.
- **The elite-shooter inverted-U study (`workloadReasoning.js`'s zone thresholds) used competition stage as a stress proxy**, not a validated psychometric or physiological measure — a limitation the source study's own authors note.

### 9.2 Scope and future work

- No gaze/eye-tracking, despite Quest hardware supporting it — head yaw is used as a proxy throughout.
- Reaction-time thresholds and workload-reasoning rule thresholds are engineering judgment calibrated by playtesting, not values taken from a published study (§5.4).
- `checkPrediction()`'s evidence-matching logic (what counts as "the prediction held") is engineering judgment, not a validated diagnostic procedure.
- Everything in Section 6 evaluates one session in isolation against fixed literature bands; nothing in the codebase currently aggregates or compares *across* a trainee's own repeated sessions (e.g. to show improvement over time) — `SimTlxTrend.jsx` charts a trainee's SIM-TLX scores over time but does not evaluate the trend against anything.

---

## 10. References

Every entry below is one of the `CITATIONS` objects actually shipped in `lib/movementBenchmarks.js` ([1]–[9]) or `lib/workloadReasoning.js` ([10]–[16]) — nothing here is cited in the report without also being cited in the running code.

1. Bohannon RW, Williams Andrews A (2011). Normal walking speed: a descriptive meta-analysis. *Physiotherapy*, 97(3), 182–189.
2. US Army FM 3-06.11 / USMC MCWP 3-11.3 — Combined Arms Operations in Urban Terrain.
3. Scholarpedia — Human saccadic eye movements. http://www.scholarpedia.org/article/Human_saccadic_eye_movements
4. Evidence of elevated situational awareness for active duty soldiers during navigation of a virtual environment. PMC11086823.
5. Force Science Institute — Action vs. reaction: the shoot-first fallacy.
6. Comparison between Auditory and Visual Simple Reaction Times. SCIRP.
7. Nieuwenhuys A et al. (2022). Shoot or Don't Shoot? Tactical Gaze Control and Visual Attention Training Improves Police Cadets' Decision-Making Performance in Live-Fire Scenarios. *Frontiers in Psychology*, PMC8905363.
8. ALERRT / Texas State University; VirTra — Time Analysis of a Certified Peace Officer's draw-and-fire response time.
9. Ibrahim F, Feildboy E, Nagy D, Huber Y, Hennig J, Herzberg PY (2024). Predicting closed quarters battle capability. *Military Psychology*, 38(1), 1–12. (= PMC12785213)
10. Predicting Cognitive Load and Operational Performance in a Simulated Marksmanship Task. PMC7350508.
11. Wickens CD (2008). Multiple Resources and Mental Workload. *Human Factors*, 50(3), 449–455.
12. Harris D, Wilson M, Vine S (2020). Development and validation of a simulation workload measure: the simulation task load index (SIM-TLX). *Virtual Reality*, 24, 557–566.
13. Li et al. (2022). Evaluating mental workload during multitasking in simulated flight. *Brain and Behavior*, PMC9014989.
14. Sweller J (2010). Element Interactivity and Intrinsic, Extraneous, and Germane Cognitive Load. *Educational Psychology Review*, 22(2), 123–138.
15. Eysenck MW, Derakshan N, Santos R, Calvo MG (2007). Anxiety and Cognitive Performance: Attentional Control Theory. *Emotion*, 7(2), 336–353.
16. The inverted-U relationship between stress and performance in elite shooting. PMC12749011.

---

## 11. Open items — please confirm with me before drafting

| | Item | Status |
|---|---|---|
| 1 | **Cross-session analysis is out of scope of the current code.** Section 6/7 describe a real, live, per-session evaluation mechanism — if the report wants results that compare *across* sessions or trainees (trends, aggregate statistics), that needs a new aggregation step that does not exist yet; it is not something I can retroactively pull from existing code. Decide scope before drafting Evaluation. | Decision pending |
| 2 | **SIM-TLX's module boundary.** I've treated it as Module 3's self-report cognitive-workload extension (Section 2's note), but this isn't written down anywhere as an official boundary the way reaction-time/movement-track is in `MODULE4_RESEARCH_REVIEW.md` §2.4. Please confirm this framing is acceptable for the write-up, or tell me the correct attribution. | My assumption pending confirmation |
| 3 | **The Unity-side `CognitiveSummary.DeriveLabels()` load/stress classifier** (§6.3, §8) is real code with a real bug, but dead on every path that actually runs. Decide whether to present this in the report as "identified and worked around" or leave it out entirely. | My decision pending |

Anything ambiguous, please ask rather than infer.
