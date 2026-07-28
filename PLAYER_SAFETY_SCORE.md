# Player (Operator) Safety Score — Research & Design

**Sentinels VR Hostage-Rescue Trainer · Module 4 (AAR) · University of Moratuwa · 2026**
**Purpose:** design a *second* safety dimension for the AAR — the **trainee's own safety / survivability** — distinct from the existing hostage-safety score, grounded in the tactical-training literature and implementable from the data Module 4 already captures.

---

## 1. Motivation — two different "safety" questions

The current `safetyScore` in `PerformanceCalculator` answers **"was the *hostage* kept safe?"** (hostages saved / total, minus a friendly-fire penalty). That is a **hostage-outcome** metric.

It says nothing about **"did the *trainee* conduct themselves safely and survive?"** A trainee can rescue the hostage (high hostage-safety) while being **reckless** — standing in the open, getting shot repeatedly, surviving only by luck. A good AAR must surface that, because in reality that operator would be dead.

So we add a distinct **Operator Safety Score** and relabel the two clearly:

| Metric | Question it answers | Basis |
|---|---|---|
| **Hostage Safety** (existing) | Was the hostage kept safe? | hostages saved, friendly fire |
| **Operator Safety** (new) | Did the trainee survive and act safely? | survivability, exposure, discipline, response |

They are **complementary**, not redundant — a core pedagogical point ("you won, but you'd have died").

---

## 2. What the research/practice says about operator safety

Operator/officer safety in tactical training is assessed along a consistent set of dimensions:

- **Survivability = minimising exposure to threat.** Military survivability doctrine frames safety as *vulnerability = exposure*: the less a unit is exposed to a threat's line of fire, the more survivable it is ([survivability-planning systems, US patents 9,240,001 / 8,831,793](https://image-ppubs.uspto.gov/dirsearch-public/print/downloadPdf/9240001)). Translated to CQB: time spent detected and in an enemy's line of sight is the primary risk signal.
- **A VALIDATED CQB assessment instrument exists — cite and evaluate against this.** Nieuwenhuys et al. (*Military Psychology*, 2024) use a standardised CQB tactical-performance instrument built by independent military/police experts: **5 scales, 9 items, rated 1–7** by two raters from multi-camera + eye-tracking video, with **inter-rater reliability ICC = .834** (good). Its five scales are **(1) Tactical behaviour** — identifying threat points, *angle coverage, withdrawal from the danger zone*; **(2) Weapon handling** — weapon orientation, *trigger behaviour*; **(3) Gaze behaviour** — fixations (≥200 ms), search efficiency; **(4) Response time** — target-identification-to-response; **(5) Mistakes** — errors such as *shooting civilians, −7 points each*. ([Tandfonline](https://www.tandfonline.com/doi/full/10.1080/08995605.2024.2430578) · [PMC full text](https://pmc.ncbi.nlm.nih.gov/articles/PMC12785213/)). **Four of these five scales map directly onto the Operator-Safety metrics below** — so the parameter choice is grounded in a peer-reviewed, reliability-tested instrument, and the "−7 per mistake" convention justifies a heavy friendly-fire penalty.
- **How to build/validate such an instrument** is itself documented ([Observational Behavior Assessment methodology, *Frontiers in Psychology* 2021](https://pmc.ncbi.nlm.nih.gov/articles/PMC7959728/)).
- **Deadly-force performance metrics.** The OJP/NIJ effort built validated scales (DFJDM / P-scale) to score officer performance in deadly-force encounters, including outcome-relevant behaviour — the quality of what the officer *did* under threat ([OJP final report](https://www.ojp.gov/ncjrs/virtual-library/abstracts/final-report-developing-common-metric-evaluating-police-performance)).
- **CQB best practice** emphasises use of cover, not lingering in fatal funnels (doorways/corridors), controlled movement, and muzzle/trigger discipline ([Chase Tactical guide](https://www.chasetactical.com/guides/best-practices-for-law-enforcement-tactical-training)).

**Distilled parameters that recur across the literature:**
1. **Getting hit / survivability** (did you take fire, did you go down)
2. **Exposure to threat** (time detected / in enemy LOS; getting flanked/surrounded)
3. **Response time to threats** (how fast you reacted when targeted)
4. **Weapon handling** (muzzle & trigger discipline; friendly fire)
5. **Tactical movement / use of cover** (avoiding the open and fatal funnels)

---

## 3. Mapping parameters to *our* captured data

| Parameter (from research) | Signal in our system | Captured now? |
|---|---|---|
| Getting hit / went down | `player_down` end reason; `PlayerHealth.health` | ⚠️ death yes; **graded damage needs a small addition** |
| Exposure to threat | `PlayerSeen → TargetConfirmed → PlayerLost` event windows (per NPC) | ✅ in the event log |
| Getting flanked / surrounded | # of NPCs with concurrent LOS (overlapping PlayerSeen windows) | ✅ derivable from events |
| Response time to threats | `averageReactionTime` (Module 3) | ✅ |
| Weapon discipline (friendly fire) | `HostageHit` → `friendly_fire` tag | ✅ |
| Use of cover / in-the-open | player vs enemy positions + room walls (LOS) | ⚠️ approximate / future |

**The only real gap is graded "damage taken."** Two options, cheapest first:
- **(A, minimal):** at `EndSession`, log the trainee's **final health** (`PlayerHealth.health`) into the summary → `damageTaken = maxHealth − finalHealth`.
- **(B, richer):** raise a `PlayerHit` scenario event in `PlayerHealth.TakeDamage` (or from `PlayerHealthHitbox`) so hits appear on the timeline with timestamps and source position — enabling a "damage over time" view and exposure-correlated analysis.

Recommend **(A) now, (B) as a follow-up** for the timeline.

---

## 4. Proposed Operator Safety Score

A weighted composite of four sub-scores, each normalised to 0–1. Weights reflect the research emphasis (survivability first, exposure second).

```
OperatorSafety =  0.35 · Survivability
                + 0.30 · ExposureControl
                + 0.20 · WeaponDiscipline
                + 0.15 · ThreatResponse

Hard rule: if the trainee was downed (player_down),
           OperatorSafety = min(OperatorSafety, 0.20)
           — you cannot be judged "safe" if you were killed.
```

### 4.1 Survivability (0.35) — *did you avoid taking fire?*
```
if died:            Survivability = 0
else:               Survivability = 1 − clamp01(damageTaken / maxHealth)
                    (damageTaken = maxHealth − finalHealth; 100 HP, dies in ~10 hits)
```
*Justification:* the core survivability signal — vulnerability is being hit; being downed is the definitive safety failure. (Survivability doctrine; CQB "hits taken".)

### 4.2 Exposure Control (0.30) — *did you minimise time in the enemy's sights?*
```
exposedTime      = total seconds the trainee was inside any PlayerSeen→PlayerLost window
                   (union of per-NPC windows, so overlapping exposure isn't double-counted)
engagedTime      = time from first PlayerSeen to mission end (the "under threat" phase)
ExposureControl  = 1 − clamp01(exposedTime / (engagedTime · TARGET_EXPOSURE_FRACTION))
                   with TARGET_EXPOSURE_FRACTION ≈ 0.5 (being seen ≤ half the engaged time = full marks)

Flanking penalty: subtract 0.1 per additional NPC that had LOS *simultaneously*
                  beyond the first (getting surrounded), floored at 0.
```
*Justification:* military survivability = minimise exposure to threat; CQB = don't linger in the open / fatal funnels; being seen by several enemies at once is being flanked. Normalising against *engaged* time (not total mission time) is fair to stealthy approaches.

### 4.3 Weapon Discipline (0.20) — *muzzle & trigger control*
```
WeaponDiscipline = 1 − clamp01(0.5 · friendlyFireCount + 0.1 · negligentDischarges)
                   (negligentDischarges = shots fired with no enemy in LOS — needs shooter+LOS, optional v2)
```
*Justification:* CQB assessment scores weapon handling; friendly fire is both a hostage-endangerment and an operator-discipline failure — here it counts toward the *operator's* discipline lens (it already counts toward hostage safety separately).

### 4.4 Threat Response (0.15) — *how fast did you react when targeted?*
```
ThreatResponse = 1 − clamp01((avgReactionTime − FAST_RT) / (SLOW_RT − FAST_RT))
                 with FAST_RT ≈ 0.3 s, SLOW_RT ≈ 1.5 s
```
*Justification:* CQB assessment includes response time; reacting faster than the enemy can engage is a direct survivability factor. Data comes from Module 3.

### 4.5 Difficulty scaling
Reference constants (`TARGET_EXPOSURE_FRACTION`, `SLOW_RT`) should scale with scenario difficulty/terrorist count so the score is comparable across generated scenarios (more enemies ⇒ more exposure is expected ⇒ a gentler denominator).

### 4.6 Where the constants come from — and how to justify them (be honest)

**The exact weights (0.35 / 0.30 / 0.20 / 0.15), the death cap (0.20), and the exposure target (0.5) are design defaults, NOT values taken from any study.** Do not present them as research-derived. What the literature actually supplies is:
- the **choice of parameters** (survivability, exposure, discipline, response) — grounded; and
- their **ordinal priority** (survivability and exposure dominate) — grounded.

The literature does **not** supply exact numbers. What *is* partly groundable:
- **RT thresholds** `FAST_RT ≈ 0.3 s`, `SLOW_RT ≈ 1.5 s` — defensible from human reaction-time psychology (simple visual RT ≈ 0.20–0.25 s; choice RT ≈ 0.3–0.5 s; >1 s is slow).

**To make the weights defensible, choose one (state it in the write-up):**
1. **Equal weights (0.25 each) + sensitivity analysis** — simplest and hardest to attack; show the trainee *ranking* is stable as weights vary, so the exact values don't drive conclusions.
2. **Expert elicitation (AHP / Delphi)** — have instructors assign the weights via pairwise comparison or consensus; now the weights are empirically derived by your own method (publishable).
3. **Data calibration** — fit the weights (regression) so the composite predicts an expert safety rating or actual survival across many runs (strongest, needs data).

**Treat every constant as a tunable parameter with a sensible default**, not a universal truth — exactly as the existing `overallScore` weights (`0.4/0.3/0.2/0.1`) are also designer-chosen defaults. Recommended for this project: **equal-weight baseline + sensitivity analysis**, or **expert-elicited weights** if instructor time is available. The death cap and exposure target should likewise be reported as choices, ideally with a one-line rationale (death = dominant failure ⇒ cap low; ≤50 % exposed during contact ≈ competent movement).

---

### 4.7 Grounding & evaluation against the validated CQB instrument

**Parameter grounding** — our automated metrics align with the Nieuwenhuys et al. (2024) instrument:

| Our metric | CQB instrument scale (ICC = .834) |
|---|---|
| Exposure Control | Scale 1 — Tactical behaviour (angle coverage, withdrawal from danger zone) |
| Weapon Discipline | Scale 2 — Weapon handling (trigger behaviour) + Scale 5 — Mistakes (shooting civilians, −7) |
| Threat Response | Scale 4 — Response time |
| Survivability | (getting hit ≈ outcome of Scales 1–4; also captured as `player_down`) |
| *(not covered)* | Scale 3 — Gaze behaviour — needs eye tracking (our honest gap) |

**Two things this borrows from the instrument to reduce arbitrariness:**
- an **equal-item** structure (the instrument sums equal 1–7 items, it does not weight scales) — supports the **equal-weight** default over hand-picked weights;
- a **fixed heavy penalty per mistake** (the instrument deducts **−7 for shooting a civilian**) — justifies a large friendly-fire penalty rather than an invented coefficient.

**Evaluation / validation path (this is what makes the score *verifiable*):**
1. Record N sessions.
2. Have 1–2 CQB-experienced raters score each with the instrument's safety-relevant scales (1, 2, 4, 5).
3. Compute our automated Operator-Safety score for the same sessions.
4. **Report the correlation / ICC** between automated and expert scores (the paper itself reports ICC = .834 between raters as the benchmark for "good").
5. A strong correlation validates the automated metric against a peer-reviewed instrument — a citable, evaluable result rather than an unverified formula.

---

## 5. Worked interpretation (what a trainee sees)
- **High operator safety (~0.85):** survived unhurt, rarely seen, no friendly fire, quick reactions — moved like a professional.
- **Mid (~0.5):** rescued the hostage but took several hits and was exposed a lot — "you won, but you were lucky."
- **Low / capped 0.20 (died):** downed — the mission may still have partially succeeded, but the operator did not survive.

This lets the AAR say something the hostage score can't: *"Hostage safe (100%), but Operator safety 42% — you spent 70% of the fight exposed and took 6 hits. Use cover."*

---

## 6. Implementation plan

**Unity (Module 4 / integration):**
1. **Capture (A):** log trainee `finalHealth` at `EndSession` (one line in `Module4SessionController`/`SessionLogger`). *(Optional (B): raise a `PlayerHit` event for the timeline.)*
2. **Compute:** add `CalculateOperatorSafety(events, reaction, finalHealth, died)` to `PerformanceCalculator`, returning the composite + the four sub-scores.
3. **Model:** add `operatorSafetyScore` + sub-scores to `PerformanceSummary`.

**Dashboard (web):**
4. Add `operatorSafetyScore` to the Mongoose `Session` schema.
5. On the **Summary** tab, show it as a second safety gauge next to hostage safety, with the four sub-scores and the same **explainable** breakdown ("Exposure 30% — seen 70% of the fight"). Relabel the existing one "Hostage Safety".

**Scope tiers:**
- **Tier 1 (now):** Survivability (via final health) + ExposureControl (events) + WeaponDiscipline (friendly fire) + ThreatResponse (Module 3). All data available with the one-line health capture.
- **Tier 2 (future):** negligent-discharge detection (shooter + LOS), use-of-cover from room walls, per-hit damage timeline, and difficulty-calibrated references validated against real runs.

---

## 7. Novelty / research framing
- Adds an **operator-survivability** dimension to the AAR that most *tactical* systems keep separate from *victim* outcomes — here both are surfaced together in one explainable debrief.
- It is computed **automatically from an event log** (no instructor scoring), extending the project's **explainable, self-guided AAR** theme to operator safety.
- It is **exposure-based** (grounded in survivability doctrine) rather than merely outcome-based, so it rewards *safe process* even on a lucky run — closer to how expert CQB assessors score behaviour, not just results.

---

## References
- Developing a Common Metric for Evaluating Police Performance in Deadly Force Situations (DFJDM / P-scale). OJP/NIJ. https://www.ojp.gov/ncjrs/virtual-library/abstracts/final-report-developing-common-metric-evaluating-police-performance
- **Nieuwenhuys et al., "Predicting closed-quarters-battle capability…," *Military Psychology*, 2024** — the **validated CQB tactical-performance instrument** (5 scales, 9 items, 1–7, inter-rater ICC = .834) this design maps to and should be evaluated against. https://www.tandfonline.com/doi/full/10.1080/08995605.2024.2430578 · full text: https://pmc.ncbi.nlm.nih.gov/articles/PMC12785213/
- Observational Behavior Assessment for Psychological Competencies in Police Officers — methodology for developing/validating such instruments. *Frontiers in Psychology*, 2021. https://pmc.ncbi.nlm.nih.gov/articles/PMC7959728/
- Systems and methods for vehicle survivability planning (exposure-to-threat as the survivability metric). US Patent 9,240,001. https://image-ppubs.uspto.gov/dirsearch-public/print/downloadPdf/9240001
- Best Practices for Law Enforcement Tactical Training (cover, fatal funnels, weapon discipline). Chase Tactical. https://www.chasetactical.com/guides/best-practices-for-law-enforcement-tactical-training
- InVeris fats® AR — multi-viewpoint AAR with eye/head/muzzle tracking (industry reference). https://www.inveristraining.com/virtual-training/military-virtual-tactical-small-arms-training-marksmanship/

*Companion documents: `MODULE4_RESEARCH_REVIEW.md`, `MODULE4_REVIEW.md`.*
