# Evaluating Module 2 (Enemy AI) — Research Findings & a Concrete Methodology

**Sentinels VR Counter‑Terrorism / Hostage‑Rescue Trainer — University of Moratuwa**
How the literature says NPC / game‑AI behaviour should be evaluated, and a concrete, citable plan to evaluate Module 2 (the tactical "terrorist" AI) using our existing telemetry + ablation tooling.

*Method: multi‑source deep search (23 peer‑reviewed / primary sources fetched, 105 candidate claims, 25 adversarially verified — **25 confirmed, 0 refuted**).*

---

## Part A — What the research says (the findings)

The literature converges on a **mixed‑methods** evaluation built on **three pillars**. You should evaluate the AI on all three, because none alone is sufficient.

### Pillar 1 — Believability / human‑likeness (subjective, the dominant paradigm)

- **The core method is an in‑game Turing test.** Impartial judges decide whether an agent is human‑ or computer‑controlled; the headline metric is a **"humanness ratio" = (votes judged human) ÷ (max possible votes).** Established by the **2K BotPrize** (Unreal Tournament 2004; founded by Hingston 2008) — the 2014 edition used a **0.5 (50 %) pass threshold**; the original required a rating of "human" from **4 of 5 judges**. *(Springer 10.1007/978‑3‑319‑59147‑6_58; Frontiers Comp. Sci. 2021, Even/Bosser/Buche.)*
- **Distinguish two things** the field is careful about: **player believability** ("is this controlled by a human?" — the Turing‑test target) vs **character believability** ("does this character feel real?"). State which you're measuring.
- **For tactical shooters, human‑likeness — not win‑rate — is the recommended primary target.** Recent work rates AI as "human‑like" with **TrueSkill** against commercial bots and expert scripts, and compares behaviour **distributions to real human match data** (movement distributions, player lifetimes, kill locations), **counts common movement mistakes**, and **detects teamwork**. *(arXiv 2501.00078 "Human‑like Bots for Tactical Shooters", Justesen/Togelius/Yannakakis/Risi; arXiv 2408.13934 "Learning to Move Like Professional CS Players", SCA 2024.)*
- **Prefer external‑observer (spectator) judging over participatory judging.** Having the judge also play distorts both play and judgement; the **Mario AI Championship Turing‑Test track** (Togelius, Yannakakis, Karakovskiy, Shaker) used ~100 **bystanders** watching paired videos and judging human‑vs‑computer, metric = **% of spectators convinced a controller was human**. *(Togelius/Shaker 2013; Springer "Believable Bots" ch. 10.1007/978‑3‑642‑32323‑2_9.)*

**Two ready‑to‑adopt believability instruments:**
1. **Gorman, Thurau, Bauckhage & Humphrys (SAB 2006)** — an anonymous survey rating video clips on a **5‑point gradient (1 Human · 2 Probably Human · 3 Don't Know · 4 Probably Artificial · 5 Artificial)**, producing a **believability index in (0,1) weighted by each observer's self‑rated experience**, plus a **confidence index `c = avg(experience)/max(experience)`** where **c ≥ 0.6** means the sample is experienced enough to trust.
2. **Even, Bosser & Buche (2021)** — a protocol defining the **seven design choices of any believability assessment**: *game type, assessment perspective (first‑ vs third‑person), duration, number of judges, judge expertise level, information given to judges, questionnaire type.* Use it as your checklist so the study is defensible.

**Behaviour dimensions the literature rates** (assemble your rubric from these): situational awareness, spatial awareness / movement, reactive behaviour, decision‑making, consistency, avoiding obvious mistakes, contextual appropriateness.

### Pillar 2 — Objective / computational metrics (from telemetry logs)

- A peer‑reviewed **systematic review of FPS AI bots** (Almeida et al., *Algorithms* 2023, 16(7):323) groups objective metrics into **Reward, Time (training time), and Game‑based** (points, kills, deaths, shots hit/missed). **Important caveat: cumulative reward is NOT comparable across studies** (reward functions differ), so **game‑based outcome metrics are preferable** for comparison. *(Reward/Time are RL‑training metrics — largely irrelevant to our FSM agent; the game‑based metrics and the survey design transfer.)*
- The same review documents a **reusable human‑survey design**: 20 participants each played 5 games across 4 agent "stages", rating each enemy on **believability, overall game difficulty, and playability**.
- Tactical‑shooter papers additionally report **behaviour‑distribution comparisons to human data**, **movement‑mistake counts**, and **teamwork evidence** (Pillar 1 sources).

### Pillar 3 — Player experience & training‑simulation validity

- **Validated player‑experience questionnaires** exist but carry psychometric caveats you must cite: **PENS** and the **GEQ** were validated only later by **Johnson, Gardner & Perry (2018)** (N = 571, EFA + CFA; the theorised structures were only *partially* supported). **Denisova, Nordin & Cairns (CHI PLAY 2016)** found the **IEQ, Game Engagement Questionnaire, and PENS converge strongly** (IEQ–GEQ r = 0.80) — i.e. immersion/engagement instruments largely tap the same construct, so **don't over‑stack them.**
  - ⚠ **Naming collision to get right in your citations:** the **Game Experience Questionnaire (GEQ)** you want is **IJsselsteijn, de Kort & Poels (2007)**. The "GEQ" in Denisova et al. is a *different* instrument — Brockmyer's **Game Engagement Questionnaire.** They are not the same; cite carefully.
  - The **IEQ‑SF** is a validated **11‑item short form** of Jennett et al.'s original 31‑item Immersion Experience Questionnaire — good for keeping the battery short.
- **Training‑simulation validity — the single most adoptable framework: Harris, Bird, Smart, Wilson & Vine (2020, *Frontiers in Psychology*, PMC7136518).** It separates **four fidelity subtypes**:
  - **Physical** (visual realism/physics → self‑report realism + presence),
  - **Psychological** (perceptual‑cognitive demands match reality → gaze + mental effort),
  - **Affective** (realistic stress/fear → psychophysiology),
  - **Ergonomic/biomechanical** (realistic motor patterns → motion tracking).
  - **The key insight for your defence:** **face validity** (how realistic it *looks/feels* to users) *"likely has no correlation with actual learning"*, whereas **construct validity** (does it accurately represent the real task?) *"is crucial for achieving transfer of learning."* → **An SME review must target construct validity, not just "does it look real."**
- **VR‑specific measures** (well‑established, cite directly — not re‑verified in this pass): a **presence questionnaire** and the **Simulator Sickness Questionnaire (SSQ)** as a safety/comfort control; **NASA‑TLX** for cognitive workload.

---

## Part B — The concrete evaluation methodology for Module 2

A **within‑subjects ablation / A–B study** with three data streams (objective telemetry, subjective believability, player experience) plus a separate **SME construct‑validity review**. This is the standard mixed‑methods shape the literature supports.

### B1. Independent variable — what you compare (Ablation / A–B)

Compare the **full Module‑2 AI** against **degraded baseline(s)** by switching components off. Our repo already has an **`AblationExperimentRunner`** and per‑component toggles — use them:

| Condition | What's on |
|---|---|
| **A — Full AI** | perception + FSM + squad coordination + persistent hunt + morale + guardian ladder |
| **B — No squad/hunt** | individual FSM only (no shared contact, no converge/flank, no persistent hunt) |
| **C — Naïve baseline** | perception + shoot‑on‑sight only (the "greedy" baseline the review used) |

This directly answers "does the tactical layer *matter*?" — the strongest kind of evidence for a supervisor.

### B2. Objective metrics to log (from `TelemetryLogger` / Module 4 AAR)

Log per session, per condition, and compare A vs B vs C:

- **Reaction time** — LOS‑acquired → first shot (we already model `targetConfirmTime`).
- **Accuracy / hit‑rate** — shots hit ÷ fired; **engagement distance** distribution.
- **Search / hunt effectiveness** — time‑to‑reacquire after you break contact, **search‑area coverage**, whether the squad **converges** on a fresh gunshot (converge success + time‑to‑converge).
- **Coordination** — flank executed, leader re‑election fired, support‑arrival time.
- **Doctrine adherence** — % of contacts *pressed* vs *abandoned* (our fix), guardian escalation‑ladder correctness.
- **Behaviour diversity** — spread of states/paths used (avoids robotic repetition).
- Use **game‑based outcome metrics** (kills/deaths/shots hit‑missed), **not RL reward** (not comparable, and we're FSM‑based).

### B3. Believability measurement (spectator Turing‑style)

- **Record gameplay videos** of each condition; show them to a **judge panel** (mix of experienced and novice players).
- Rate each clip on **Gorman's 5‑point Human↔Artificial gradient**; compute the **experience‑weighted believability index** and report the **confidence index c (target ≥ 0.6)**.
- Also collect a **per‑dimension Likert rubric** (situational awareness, movement, decision‑making, consistency, contextual appropriateness, obvious mistakes).
- Document your study against **Even/Bosser/Buche's 7 characteristics** (third‑person spectator, clip duration, #judges, expertise, info given, questionnaire type). Use **external‑observer** judging, not participatory.

### B4. Player‑experience battery (trainee participants, after each condition)

Keep it short (instruments converge, per Denisova et al.):
- **GEQ (IJsselsteijn, de Kort & Poels 2007)** core module — competence, challenge, tension, flow, immersion.
- **Perceived difficulty / fairness / frustration** items (tie enemy‑AI quality to experience).
- **NASA‑TLX** — cognitive workload.
- **IEQ‑SF (11‑item)** *or* GEQ immersion — **not both** (they overlap).
- **SSQ** + a **presence** item set — VR safety/comfort controls.

### B5. SME construct‑validity review (the training‑validity pillar)

- Recruit **subject‑matter experts** (military/police CQB instructors). Following the surgical‑training precedent (cross‑sectional SME survey, stratified by experience), have them rate whether the terrorist behaviour **accurately represents real tactical behaviour** — react‑to‑contact, search, coordination, hostage‑guard escalation.
- Frame it explicitly as **construct validity** (drives transfer), and report face validity separately (Harris et al. 2020). This is what lets you claim training relevance, not just "it looks real."

### B6. Study design, sample size, statistics

- **Design:** within‑subjects, **counterbalanced** order of conditions (to cancel learning effects); mixed methods (telemetry + questionnaires + short **think‑aloud / interview**).
- **Sample (pragmatic, precedent‑based — see caveats):** ~**20–30 trainee participants** (the review used N = 20); ~**10–15 believability judges** (weight by experience); ~**5–10 SMEs**.
- **Statistics:**
  - Objective metrics & Likert across A/B/C → **repeated‑measures ANOVA** (parametric) or **Friedman + Wilcoxon** (non‑parametric, safer for Likert).
  - Two‑condition comparisons → paired **t‑test / Wilcoxon**.
  - **Inter‑rater reliability** for believability/SME ratings → **Cohen's / Fleiss' κ** (or ICC).
  - **Convergent validity** of questionnaires → Pearson/Spearman correlations.

---

## Part C — Honest limitations (state these to your supervisor)

1. **The strongest believability benchmarks are arena/deathmatch or platformer contexts** (BotPrize/UT2004, Mario, CS:GO), **not squad‑based hostage AI.** The *methods* transfer, but **no retrieved paper directly measures the tactical‑team behaviours we care about** — squad coordination, doctrine adherence, search‑coverage, the guardian escalation ladder. **You must operationalise those metrics yourself** (Part B2 proposes concrete ones); flag them as author‑defined.
2. **RL metrics (reward, training time) don't apply** to our finite‑state‑machine agent — only the game‑based outcomes and the survey design transfer from the FPS review.
3. **GEQ naming collision** (Part A, Pillar 3) — cite the right one.
4. **NASA‑TLX, Jennett IEQ, SSQ, presence questionnaires, and κ** are well‑established and citable but were **not re‑verified in this research pass** — verify the exact references yourself before citing.
5. **Sample sizes are precedents, not prescriptions** — the cited studies used small samples (N = 20 survey; ~100 spectators). Justify your N with a power analysis if the supervisor wants rigour.
6. **No retrieved source directly links enemy‑AI believability to measured training transfer** — so claim *construct validity*, not proven transfer, unless you run a transfer study.

---

## Part D — Key references to cite

| # | Reference | Use for |
|---|---|---|
| 1 | Hingston (2008) & **2K BotPrize** / Schrum, "Optimising Humanness: … UT2004" (Springer 2017) | The believability Turing‑test paradigm + humanness ratio |
| 2 | **Gorman, Thurau, Bauckhage & Humphrys (SAB 2006)** | Experience‑weighted believability index (your main instrument) |
| 3 | **Even, Bosser & Buche (Frontiers Comp. Sci. 2021)** | 7‑characteristic believability‑assessment protocol |
| 4 | **Togelius, Yannakakis, Karakovskiy, Shaker** — Mario AI Turing‑Test track ("Believable Bots", Springer 2012/2013) | Spectator (external‑observer) judging; % convinced human |
| 5 | **Justesen, Togelius, Yannakakis et al. (arXiv 2501.00078)** — Human‑like Bots for Tactical Shooters | Human‑likeness > win‑rate for tactical shooters |
| 6 | **Durst et al. (SCA 2024, arXiv 2408.13934)** — Learning to Move Like Pro CS Players | TrueSkill human‑like rating; distribution/mistake/teamwork metrics |
| 7 | **Almeida et al. (Algorithms 2023, 16(7):323)** — systematic review of FPS AI | Objective metric taxonomy + reusable participant survey |
| 8 | **Johnson, Gardner & Perry (2018, IJHCS)** | PENS/GEQ validation + caveats |
| 9 | **Denisova, Nordin & Cairns (CHI PLAY 2016)** | Convergence of IEQ/GEQ/PENS (don't over‑stack) |
| 10 | **IJsselsteijn, de Kort & Poels (2007)** | The Game Experience Questionnaire (GEQ) itself |
| 11 | **Jennett et al. (2008) + IEQ‑SF** | Immersion questionnaire (short form) |
| 12 | **Harris, Bird, Smart, Wilson & Vine (2020, Frontiers in Psychology, PMC7136518)** | Fidelity subtypes + construct‑vs‑face validity (transfer) |

---

*Everything in Part A is backed by primary/peer‑reviewed sources that passed 3‑vote adversarial verification. Part B maps it onto our actual tooling (`AblationExperimentRunner`, `TelemetryLogger`, Module 4 AAR). The full source list with URLs is in the research output; ask and I'll append them.*
