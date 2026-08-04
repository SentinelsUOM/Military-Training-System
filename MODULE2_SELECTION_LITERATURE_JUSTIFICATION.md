# Module 2 — NPC Responder Selection: Where the Four Parameters Come From

**Purpose of this document.** `NPCSelector` (the class that decides which NPC
responds to an event — see [MODULE2_ABLATION_STUDY_REPORT.md](MODULE2_ABLATION_STUDY_REPORT.md))
scores candidates on **four parameters**: distance, role, state-readiness, and
line-of-sight. This document answers, precisely and honestly, the question:
**where does each of those four parameters come from — is it from a research
paper, and if so, which one?** It also explains what the project did to fill
the gap where the literature could not supply an answer. Use this document as
the direct answer if an evaluator asks "why these four factors, and why these
numbers?"

**The short version, upfront:**
- The **four-factor combination itself, and the exact numbers used, are this
  project's own design** — no single paper describes this specific formula.
- **Each individual factor**, and the **general technique** (score every
  candidate on weighted factors, pick the highest), **is well-established and
  citable** across three separate, real bodies of literature: game-AI utility
  theory, multi-robot task allocation, and military/emergency dispatch
  operations research.
- Because the literature could not supply the exact weights, the project
  **validated them empirically after building them** — via the ablation study
  and the weight-sensitivity study — rather than citing a source that doesn't
  exist.
- Separately, **real trainees rated the AI tier that depends on this
  responder-selection mechanism as significantly more intelligent, life-like,
  and tactically realistic** than the tier without it (n=9 complete triads,
  all p≤0.004, large effect sizes) — corroborating evidence from outside the
  code, scoped precisely in §11.

---

## 1. What was actually checked

Two things were searched, thoroughly, before writing this:

1. **The project's own `Literature_Review_Module2.docx`** — the formal
   literature review for Module 2's enemy-AI behaviour. Read in full. It covers
   react-to-contact doctrine, shared situational awareness, search theory
   (Point-Last-Seen), building-clearing doctrine, suppressive fire, morale
   collapse, the hostage-guard's fixed-asset dilemma, semi-trained calibration,
   and the F.E.A.R. GOAP architecture. **It does not mention responder
   selection, distance-based scoring, role bonuses, state-readiness, or
   line-of-sight as selection criteria anywhere.** This is a genuine, confirmed
   gap — not an oversight in searching, an absence in the source document.
2. **A live web search** for research and industry sources that use these same
   four factors — or the general technique — for choosing which of several
   candidate agents/units should respond to something. That search is
   summarised in Sections 2-5 below.

---

## 2. The technique itself: utility-based multi-criteria scoring

The overall shape of `NPCSelector` — normalize several factors, weight and sum
them, pick the highest-scoring candidate — is not a novel invention. It is the
textbook definition of **Utility AI** (also called utility-based decision
making) in game AI:

- **Mark, D. (2009). *Behavioral Mathematics for Game AI*.** The foundational
  text on this exact technique: score every candidate action/agent on several
  independent "considerations," weight them, and act on the highest total.
  Already cited in the project's own `Module2_SlideContent.md` as the closest
  published analogue.
- **Dill, K. (2010). *"Improving AI Decision Making Using Utility Theory."*
  GDC.** The talk that popularized utility-based scoring as a mainstream game-AI
  architecture, as an alternative to hand-scripted FSMs/behaviour trees.
- **Dill, K. & Martin, L. (2011). *"A Game AI Approach to Autonomous Control of
  Virtual Characters."* I/ITSEC** (Interservice/Industry Training, Simulation
  and Education Conference). **This is the single most on-domain citation
  available** — it is a peer-reviewed conference paper about utility-based AI
  for virtual characters *specifically for military training simulators*,
  the same application domain as this project.
- **Dill, K. (2012). *"Design Patterns for the Configuration of Utility-Based
  AI."* I/ITSEC.** A follow-up paper on structuring utility-based scoring
  systems, from the same author/venue.
- **Lewis, M. (2015). *"Choosing Effective Utility-Based Considerations."* In
  *Game AI Pro 3*, ch. 13.** A practitioner reference (same publication series
  format as Orkin's GDC talk, already used elsewhere in the project's
  literature review as authoritative grey literature) that explicitly lists
  **distance** (with a linear falloff curve) and **line-of-sight** (as an
  on/off gating consideration) as standard scoring inputs — i.e., two of
  `NPCSelector`'s four factors appear by name as textbook examples in this
  source.

**Conclusion for this section:** the *architecture* — weighted multi-factor
scoring to pick a responder — is standard, well-documented practice, backed
by peer-reviewed and practitioner sources, including two papers from the
*exact* domain (military training virtual characters).

---

## 3. Distance — ubiquitous across every literature checked

Distance-to-event as a scoring factor is the single most universally supported
of the four parameters. It appears, independently, in:

- **Game AI**: linear or curved falloff functions over distance are a standard
  "consideration" in utility AI (Lewis, *Game AI Pro 3*, ch. 13).
- **Multi-robot task allocation (MRTA)**: in auction-based task allocation,
  "distance to the task location is the major cost factor" driving which robot
  wins the bid (see the COMSTAR auction-allocation paper and the 2024 ACM
  Computing Surveys systematic review of MRTA).
- **Military operations research**: the Weapon-Target Assignment (WTA)
  problem's real-time heuristics include "closest distance" as a direct
  assignment rule.
- **Emergency dispatch**: Computer-Aided Dispatch (CAD) systems for
  ambulance/police explicitly select "the closest, most appropriate unit,"
  using live location data.

---

## 4. Role — the capability/function-match factor

`NPCSelector`'s role-bonus table (Guard favoured for `RoomBreached`, Roamer for
`GunshotHeard`, Leader for `AllyDownSeen`) matches a factor called **capability
matching** or **function matching** in the wider literature:

- **MRTA literature**: task allocation is explicitly framed as "assign tasks to
  the most suitable robot based on its function and capability" alongside
  distance and availability — role/capability is one of the three canonical
  MRTA factors.
- **Emergency dispatch**: CAD systems dispatch based on **unit
  certification/capability** as well as distance — e.g. automatically routing
  to the nearest *Advanced Life Support*-certified unit rather than the
  nearest unit of any kind.
- **Weapon-Target Assignment**: capability constraints (not every weapon type
  can engage every target) are a core constraint of the WTA formulation.

---

## 5. State-readiness — the availability factor

The FSM-state-derived `StateScore` (e.g. Idle=1.0 down to Engage=0.0) matches
what MRTA and dispatch literature call **availability** or **status**:

- **MRTA literature**: "availability" is the third canonical factor alongside
  distance and capability in most task-allocation formulations.
- **Emergency dispatch**: CAD systems pick the best unit "based on location,
  crew certifications, and **current status**" — a unit already engaged
  elsewhere is filtered out or scored lower, exactly analogous to an `Engage`
  or `Retreat` NPC scoring near zero in `NPCSelector`.

---

## 6. Line-of-sight — the perception factor

Perception/visibility as a *selection* criterion (not just a *detection*
trigger) is specifically documented in the game-AI target-selection
literature:

- **Lewis (2015), *Game AI Pro 3*, ch. 13** — describes line-of-sight used as
  an on/off gating consideration in exactly this kind of multi-factor scoring
  formula, so an agent isn't scored highly for a target/event it cannot
  actually perceive.
- **Welsh, R., *"Crytek's Target Tracks Perception System."* Game AI Pro** — a
  related practitioner source on incorporating perception/visibility into
  target/response scoring in a shipped commercial game engine.

---

## 7. What the literature does NOT give you — stated plainly

No source found — in the project's own literature review or in the wider
search — specifies:
- This exact **four-factor combination** (distance + role + state + LOS
  together) for NPC responder selection in a hostage-rescue/CQB context.
- The specific **weight values** (`DistanceWeight=2.0, RoleWeight=1.0,
  StateWeight=1.0, LosWeight=1.0`) or the **role-bonus magnitudes**
  (`3.0 / 2.0 / 1.5`).

This combination and these numbers are an **original engineering design** for
this project — consistent with, but not derived from, the sources above. This
is exactly analogous to how the project's own `Literature_Review_Module2.docx`
already treats several other design choices (e.g. the hostage-guard's
fixed-asset behaviour, §8; the exact lost-contact persistence duration, §12) —
stating plainly where doctrine/literature ends and engineering judgement
begins is standard, expected practice in this project, not a weakness unique
to this one formula.

---

## 8. How the gap was filled: empirical validation instead of citation

Since no paper could supply the specific numbers, the project validated them
**empirically, after implementation**, with two internal studies — summarized
here, with full detail in their own reports:

1. **[MODULE2_ABLATION_STUDY_REPORT.md](MODULE2_ABLATION_STUDY_REPORT.md)** —
   switched each of the four factors fully on/off across 250 real selection
   decisions (50 events × 5 modes), establishing that **each factor
   measurably changes who gets picked**, and ranking their relative
   influence: Role (78% of decisions flip when removed) ≫ State (56%) > LOS
   (20%) > Distance (6%).
2. **[MODULE2_WEIGHT_SENSITIVITY_REPORT.md](MODULE2_WEIGHT_SENSITIVITY_REPORT.md)**
   — swept each factor's weight across a 0-4× range across 25,200 real
   selection decisions (9 independent NPC layouts × 4 weights × 7 values ×
   100 events), establishing that the chosen values are **not a fragile,
   arbitrary pick**: three of the four factors (Distance, Role, State) are
   stable across a wide range around their defaults, while the fourth (LOS)
   was identified as the most sensitive, and specifically flagged for future
   re-tuning rather than hidden.

Together, these two studies are the project's actual evidence for the
formula — not a citation, but **250 + 25,200 = 25,450 real, reproducible
selection decisions** run against the project's own compiled code.

---

## 9. The one-paragraph answer for an evaluator

> *"The specific combination of distance, role, state-readiness, and
> line-of-sight — and the exact weights used — is our own design; no single
> paper prescribes this formula. However, each factor individually, and the
> overall technique of weighted multi-factor scoring, is well-established in
> three separate literatures: game-AI utility theory (Mark 2009; Dill 2011/2012,
> the latter specifically about virtual characters for military training
> simulators — our exact application domain), multi-robot task allocation
> (distance + capability + availability is the canonical three-factor
> pattern), and military/emergency dispatch operations research (Weapon-Target
> Assignment; Computer-Aided Dispatch). Since no source specifies the exact
> numbers for our specific combination, we validated them ourselves: an
> ablation study proving each factor matters, and a sensitivity study proving
> the chosen weights aren't fragile. That combination — literature-grounded
> technique, empirically-validated parameters — is the actual justification,
> and we can show the data behind both halves."*

---

## 10. References

| # | Reference | What it grounds |
|---|---|---|
| 1 | Mark, D. (2009). *Behavioral Mathematics for Game AI*. | The utility-based multi-factor scoring technique itself |
| 2 | Dill, K. (2010). "Improving AI Decision Making Using Utility Theory." GDC. | Utility AI as mainstream game-AI architecture |
| 3 | Dill, K. & Martin, L. (2011). "A Game AI Approach to Autonomous Control of Virtual Characters." I/ITSEC. | Utility-based scoring for military-training virtual characters — the exact domain of this project |
| 4 | Dill, K. (2012). "Design Patterns for the Configuration of Utility-Based AI." I/ITSEC. | Structuring/configuring utility-based scoring systems |
| 5 | Lewis, M. (2015). "Choosing Effective Utility-Based Considerations." *Game AI Pro 3*, ch. 13. | Distance and line-of-sight as named standard considerations |
| 6 | Welsh, R. "Crytek's Target Tracks Perception System." *Game AI Pro*. | Perception/visibility incorporated into target/response scoring in a shipped engine |
| 7 | ACM Computing Surveys (2024). "A Systematic Literature Review on Multi-Robot Task Allocation." | Distance + capability + availability as the canonical MRTA factor set |
| 8 | Weapon-Target Assignment problem (operations research; see Wikipedia overview and cited primary sources). | Distance/closest-target and capability constraints in military assignment |
| 9 | Computer-Aided Dispatch (CAD) systems for EMS/police — industry documentation (e.g. Resgrid). [Grey/tertiary — practitioner documentation, not peer-reviewed; used to illustrate real-world practice, not as academic evidence.] | Location + capability/certification + status as real-world dispatch-selection factors |

Sources consulted 2026-08-02 via live web search; full URLs available on
request if a citation needs to be checked directly.

---

## 11. Layer 5 — Independent human evaluation (added 2026-08-02)

The three internal sources of evidence above (literature grounding, ablation,
sensitivity) are all either citations or tests run against the project's own
code. This section adds a fourth, genuinely independent source: **real
trainees' ratings**, from `evaluation results.pdf` (dashboard export, 30 Jul
2026), now reported in full in `MODULE2_FULL_REPORT.md` §6.7.

### 11.1 How it connects to `NPCSelector`

`TerroristController.cs` gates squad coordination behind the AI difficulty
tier: `AllowTeam => aiLevel >= AILevel.Intermediate`. Tracing where `AllowTeam`
is used shows it gates the exact investigation dispatch that `NPCSelector`
drives — the code comment states plainly: *"this is also what EventManager
dispatches an investigator through — so gating it here stops a Dumb terrorist
from ever coming to find you."* Concretely: at **Basic** tier, `NPCSelector`
still runs and picks an investigator, but that NPC silently declines to act on
it. At **Intermediate/Advanced**, the picked NPC actually moves, converges,
and joins the squad hunt. **The AI-tier system is a switch on top of
`NPCSelector`'s output, not a replacement for it.**

### 11.2 The results (statistically significant, not a small pilot)

n = 20 Basic / 14 Intermediate / 11 Advanced sessions; n = 9 participants
completed all three tiers (the set the significance tests run on):

| Measure | Basic | Intermediate | Advanced | Friedman p | Effect size |
|---|---:|---:|---:|---|---|
| Perceived Intelligence (/5) | 1.6 | 3.7 | 4.4 | **0.004** | r = 0.85–0.89 (all pairs sig.) |
| Animacy — life-like (/5) | 2.0 | 3.8 | 4.1 | **<0.001** | r = 0.89 (Basic pairs sig.; Int.-vs-Adv. n.s., p=0.188) |
| Tactical realism (/5) | 1.7 | 3.5 | 4.4 | **0.003** | r = 0.85–0.89 (all pairs sig.) |

Full statistical detail (Kendall's W, all pairwise Wilcoxon tests, Bonferroni
correction) is in `MODULE2_FULL_REPORT.md` §6.7.

### 11.3 What this does and does not add — the precise scope

**It legitimately adds this:** real human evaluators rated the tier where
responder-selection-driven behavior is switched **on** (Intermediate/Advanced)
as significantly more intelligent, life-like, and tactically realistic than
the tier where it's switched **off** (Basic) — with large, statistically
significant effects, not a directional trend in a small pilot.

**It does NOT let this study claim:** that this proves the specific 4-factor
formula or its exact weights are correct. `AllowTeam` gates the *entire*
squad-coordination package at once — `NPCSelector`'s dispatch, the
persistent-hunt search algorithm, leader directives, and converge-on-contact
all switch on together at the same tier boundary. The evaluation compared
*"all of that on"* vs. *"all of that off,"* not `NPCSelector`'s scoring
weights in isolation. So this is evidence that **a responder-selection
mechanism, as part of a coordinated squad system, meaningfully and measurably
improves perceived intelligence** — it is not evidence that *these particular
numbers* (`2.0/1.0/1.0/1.0`; role bonuses `3.0/2.0/1.5`) are the right ones.
That narrower claim remains the job of the ablation and sensitivity studies
(§8), which isolate `NPCSelector` specifically; this layer corroborates from
the outside that the mechanism they're testing is worth having at all.

### 11.4 The combined, four-layer answer for an evaluator

> *"Our weighted-scoring technique is grounded in the literature (§2–§6); our
> specific weights were validated empirically via ablation and sensitivity
> testing (§8), which isolate the formula itself; and independently, real
> trainees rated the AI tier that depends on this responder-selection
> mechanism as significantly more intelligent, life-like, and tactically
> realistic than the tier without it (χ²(2)=10.89–14.00, all p≤0.004, large
> effect sizes r=0.85–0.89, n=9 complete triads). Three separate kinds of
> evidence — literature grounding, internal algorithmic validation, and
> external human judgment — all point the same direction, and each is scoped
> to claim only what it actually shows."*
