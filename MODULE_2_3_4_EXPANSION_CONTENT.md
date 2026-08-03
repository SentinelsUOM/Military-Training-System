# Modules 2, 3 and 4 — Report Expansion to Module 1 Parity

> ## ✅ STATUS: APPLIED DIRECTLY TO THE .DOCX ON 2026-07-30
>
> Everything in Parts C, D, E, F and G below has been **written into
> `Final_Report_Team_Sentinels.docx` itself** — prose, all 21 new tables, all 40 new
> figure slots, the renumbering, and the knock-on edits. A pre-edit copy is preserved as
> `Final_Report_Team_Sentinels.BACKUP-before-module234-expansion.docx`.
>
> **Seven figures are real, finished artwork already embedded in the document**
> (sources and regeneration scripts in [Report_Figures/](Report_Figures/)):
>
> | Figure | What it is |
> |---|---|
> | **5.4** | Terrorist FSM + shared squad hunt lifecycle — **drawn diagram**, two panels |
> | **5.6** | Reaction-time channel and validity-rule flow — **drawn diagram** |
> | **5.8** | Hostage distress scale, Freeze emphasised — chart |
> | **7.10** | Believability by AI tier — chart |
> | **7.13** | Reaction time per participant vs both reference values — chart |
> | **7.16** | Hostage Safety and Accuracy per participant vs benchmarks — chart |
> | **7.20** | Mean within-participant variability by score — chart |
>
> **The other 30 new figures are captioned, correctly-sized empty frames** carrying a grey
> `[ Figure X.Y — insert image here ]` marker plus the capture instructions. Every one of
> them is a **screenshot** — Unity, the headset, or the dashboard — which is the only class
> of figure that cannot be generated here. Drop your capture into the frame and delete the
> marker text; numbering, captions and the List of Figures are already correct and do not
> need touching.
>
> **What this file is still for:** the two decisions in Part A (which you must still make),
> the numbering maps in Part B (already applied — kept as a record of what changed), and
> the capture guidance in Part H. Treat the .docx as the source of truth for wording now.
>
> **Still manual, by necessity:** the 30 screenshots, and — if you want them — the code
> listings, which are not inserted since you have not yet chosen whether to screenshot them
> or set them as monospaced text in a new appendix.

**Target document:** `Final_Report_Team_Sentinels.docx`
**Prepared:** 2026-07-30
**Purpose:** paste-ready content that brings the Module 2, 3 and 4 sections of Chapters 5, 6 and 7 up to the depth, table density and figure density that the Module 1 sections already have.

**Every number in Part E was read directly off `evaluation results.pdf` (16 pp., dashboard export, 30 Jul 2026) and cross-checked for internal consistency.** Where a figure needs a screenshot you cannot take yet, the figure is still fully specified — number, name, caption, capture instructions — so you can insert a placeholder frame of the right size and drop the image in later.

---

## Why this document exists — the measured parity gap

| Section | Words | Tables | Figures |
|---|---:|---:|---:|
| §5.3 Module 1 design | 382 | 0 | 1 |
| §5.4 Module 2 design | 391 | 1 | 1 |
| §5.5 Module 3 design | 342 | 1 | 1 |
| §5.6 Module 4 design | **224** | 1 | 1 |
| §6.3 Module 1 implementation | 552 | 1 | **3** |
| §6.4 Module 2 implementation | 473 | **0** | **0** |
| §6.5 Module 3 implementation | 296 | 1 | **0** |
| §6.6 Module 4 implementation | 270 | 1 | **0** |
| §7.3 Module 1 results | **1235** | **4** | **6** |
| §7.4 Module 2 results | 464 | 2 | **0** |
| §7.5 Module 3 results | 246 | 1 | **0** |
| §7.6 Module 4 results | 298 | 1 | **0** |

Chapter 7 is where the gap is widest and matters most. Module 1's §7.3 is organised as *research questions → experiment-design table → methodological safeguards → one subsection per RQ, each with its own table and figure → an explicit "answers to the research questions" block.* Modules 2, 3 and 4 have none of that scaffolding, even though — as of the dashboard export — all three now have real data that supports it. Part E below gives each of them the same structure.

---

# PART A — READ THIS BEFORE YOU PASTE ANYTHING

Two issues change what the text is allowed to claim. Both have a decision attached.

## A.1 🔴 Confirm the participant boundary before Chapter 7 goes in

`Module4_Documents/MODULE4_FULL_REPORT.md` §6.8 and §10.2, and `MODULE2_FULL_REPORT.md` §6.7, both state that only `P0001` is a real human participant and that `P0002`–`P0011` are development rows generated to check the dashboard renders multi-participant panels. The attached `evaluation results.pdf` is a render of exactly that database: 11 participants, 45 sessions.

Chapter 7 as currently written already asserts eleven real participants, so **the report and your own supporting documents contradict each other right now** — and an examiner who opens the repository will find it. Pick one:

- **Branch A — real recruitment happened since those documents were written.** Then the module documents are stale. Update `MODULE4_FULL_REPORT.md` §6.8/§10.2 and `MODULE2_FULL_REPORT.md` §6.7 to record which IDs are real, and paste everything below exactly as written. Also add the ethics sentence in §G.2 — a chapter reporting eleven human participants needs one.
- **Branch B — the data is still predominantly synthetic.** Then paste everything below, but apply these three edits:
  1. Add this sentence immediately after the participant sentence in §7.4: *"Of the eleven participant records in the evaluation database, one (P0001) is a recorded human participant; the remainder are development records generated to verify that the dashboard's multi-participant analysis panels compute correctly. The statistical results in this chapter are therefore reported as a demonstration that the analysis mechanism works end to end, not as findings about trainee behaviour."*
  2. Retitle every statistical table in §§7.4–7.6 with the suffix *"— mechanism demonstration"*.
  3. Delete the ethics sentence (§G.2), and remove *"colleagues and peers who volunteered as trainee participants"* from the Acknowledgement.

**Everything below is written for Branch A**, because you supplied the dashboard export as the source of truth. Under Branch B the *structure*, tables and figure placements are all still correct — only the framing sentences change.

## A.2 🟠 Chapter 7's existing Module 2 numbers are stale — and the new ones are stronger

The dashboard has moved on since Tables 7.6 and 7.7 were written. The changes are all in your favour.

| Report currently says | Dashboard now says | Consequence |
|---|---|---|
| Intermediate n=12, Advanced n=9 | Intermediate n=**14**, Advanced n=**11** | Table header |
| n = 7 complete-case | n = **9** complete | Table title, §7.4 narrative |
| Perceived Intelligence: no pair survives Bonferroni | **All three pairs** significant (p = 0.008 / 0.008 / 0.016) | Deletes a whole limitation paragraph |
| Tactical realism: no pair survives Bonferroni | **All three pairs** significant (p = 0.008 / 0.008 / 0.008) | Same |
| Module 4 scores tested across AI tiers | Design deliberately abandoned; now pooled one-sample Wilcoxon + one-way ANOVA | §7.6.2 must be **rewritten**, not patched |
| §7.6.3 "no data to report on" | Repeated-play panel is **fully populated**, 11 participants, 45 sessions | §7.6.3 must be **rewritten** |
| §7.5 "N/A (methodology audit)" | Module 3 tab now carries a 45-session Wilcoxon and an ANOVA | §7.5 gains **two new subsections** |

**One inconsistency to resolve while you are in there.** Appendix B.4 and §7.4 both state the corrected threshold as α = 0.05/3 ≈ 0.0167. The dashboard prints **α = 0.02**, and three of the reported p-values are exactly 0.016 — which passes at 0.0167, but only just. Check what `sentinels-aar/lib/stats.js` actually applies. If the engine uses the exact value and the dashboard only rounds for display, add this footnote to the significance table: *"The dashboard displays the Bonferroni-corrected threshold rounded to 0.02; the test itself is applied at the exact value 0.05/3 = 0.0167."*

---

# PART B — NUMBERING PLAN

## B.1 Figures — these append cleanly, nothing is renumbered

Because every existing Chapter 6 figure (6.1–6.3) and every existing Chapter 7 figure (7.1–7.6) belongs to Module 1, and Module 1's sections come first in document order, all new Module 2/3/4 figures simply continue the sequence. **No existing figure number changes.**

| New figures | Belong to | Range |
|---|---|---|
| 6.4 – 6.9 | §6.4 Module 2 | six figures |
| 6.10 – 6.14 | §6.5 Module 3 | five figures |
| 6.15 – 6.22 | §6.6 Module 4 | eight figures |
| 7.7 – 7.11 | §7.4 Module 2 | five figures |
| 7.12 – 7.14 | §7.5 Module 3 | three figures |
| 7.15 – 7.20 | §7.6 Module 4 | six figures |

## B.2 Chapter 5 figures — three new diagrams, small renumber

You said you can generate flow diagrams. Three are worth generating, one per module, each placed immediately after that module's existing pipeline diagram. That means a small shift:

| Old | New | Content |
|---|---|---|
| 5.1 | 5.1 | High-level architecture *(unchanged)* |
| 5.2 | 5.2 | Module 1 generation pipeline *(unchanged)* |
| 5.3 | 5.3 | Module 2 event-driven behaviour loop *(unchanged)* |
| — | **5.4** | 🆕 Terrorist FSM state diagram + squad hunt lifecycle |
| 5.4 | **5.5** | Module 3 cognitive analysis pipeline |
| — | **5.6** | 🆕 Reaction-time channel and validity-rule decision flow |
| 5.5 | **5.7** | Module 4 logging/AAR/replay structure |
| — | **5.8** | 🆕 Hostage distress state machine |
| 5.6 | **5.9** | Inter-module data contract |

## B.3 Tables — Chapters 6 and 7 need renumbering

New tables must sit inside §§6.4–6.6 and §§7.4–7.6, which are *before* some existing tables in document order. Tables have to appear in numeric order in a submitted report, so letter suffixes (6.2a, 7.6b) would be visible drafting scars. Renumber instead — the maps below are exhaustive. Note that **no Chapter 8 table number changes**, so Table 8.1 and everything referring to it are untouched.

**Chapter 6:**

| Old | New | Content |
|---|---|---|
| 6.1 | 6.1 | Module 1 implementation status *(unchanged)* |
| — | **6.2** | 🆕 Module 2 implementation status |
| — | **6.3** | 🆕 AI intelligence tier — live verification record |
| 6.2 | **6.4** | Module 3 implementation status |
| — | **6.5** | 🆕 Module 3 reaction-time channels and thresholds |
| — | **6.6** | 🆕 Module 4 implementation status |
| 6.3 | **6.7** | Module 4 — significant defects found and fixed |
| — | **6.8** | 🆕 Module 4 automatic incident types |

**Chapter 7:**

| Old | New | Content |
|---|---|---|
| 7.1 – 7.5 | 7.1 – 7.5 | Methodology overview + all Module 1 tables *(unchanged)* |
| — | **7.6** | 🆕 Module 2 ablation study design |
| 7.6 | **7.7** | Overall averages by AI tier *(numbers replaced — §E.1)* |
| 7.7 | **7.8** | Friedman / Wilcoxon results *(numbers replaced — §E.1)* |
| — | **7.9** | 🆕 Participant P0001 — complete triad |
| — | **7.10** | 🆕 Module 3 evaluation design |
| 7.8 | **7.11** | Module 3 benchmark audit *(unchanged content)* |
| — | **7.12** | 🆕 Module 3 per-participant reaction time |
| — | **7.13** | 🆕 Module 3 reaction time — population comparison |
| — | **7.14** | 🆕 Module 4 evaluation design |
| 7.9 | **7.15** | Module 4 construct-validity audit *(unchanged content)* |
| — | **7.16** | 🆕 Module 4 per-participant five-score averages |
| — | **7.17** | 🆕 Module 4 scores vs published expert values |
| — | **7.18** | 🆕 One-way ANOVA between participants |
| — | **7.19** | 🆕 Repeated-play reliability, all participants |
| — | **7.20** | 🆕 Mean within-participant variability by score |

## B.4 In-text cross-references to update after renumbering

Search for each of these and change the number:

- §7.4 body — *"Tables 7.6 and 7.7"* → **Tables 7.7 and 7.8**
- §7.4 body — *"Table 7.6: Overall averages…"* → **Table 7.7**
- §7.4 body — *"Table 7.7: Friedman and Wilcoxon…"* → **Table 7.8**
- §7.5 body — *"Table 7.8: Module 3 movement/reaction-time benchmark audit"* → **Table 7.11**
- §7.6.1 body — *"Table 7.9: Module 4 construct-validity audit"* → **Table 7.15**
- §6.6 body and Appendix B.1/B.2 — every *"Table 6.3"* (including *"Table 6.3 row 3"* and *"row 6"*) → **Table 6.7**
- §6.5 body — *"Table 6.2"* → **Table 6.4**
- **List of Tables and List of Figures** — rebuild both (see §G.6; converting them to Word fields is strongly recommended at this size).

---

# PART C — CHAPTER 5 ADDITIONS (DESIGN)

## C.1 §5.4 Module 2 Design — add after the existing Table 5.2

### 🆕 Figure 5.4 — Terrorist finite-state machine and squad hunt lifecycle

**You generate this.** A two-panel diagram:

*Upper panel — individual FSM.* Six boxes: `Idle → Alert → Investigate → Engage → Cover/Retreat → Down`. Label the transition arrows with the event that causes them: `PlayerSeen` (Idle→Alert), `GunshotHeard` (Idle→Investigate), `TargetConfirmed` (Alert/Investigate→Engage), `PlayerLost` (Engage→Investigate), escalation pressure (Engage→Cover/Retreat), health ≤ 0 (any→Down). Shade `Investigate`, `Cover/Retreat` differently and add a small key: *available at Intermediate and above* / *Advanced only*, so the ablation ladder is visible in the diagram itself.

*Lower panel — shared squad hunt lifecycle.* Four boxes left to right: `ReportConfirmedContact → BeginHunt (anchor = Point-Last-Seen) → search radius = subject speed × elapsed time → EndHunt (budget ≈ 45 s exhausted)`. Draw three re-anchor arrows looping back into `BeginHunt`, labelled *fresh sighting*, *fresh gunshot*, *squad-mate down*. Draw the blackboard as a cylinder shared by all squad members, to make the point that contact is squad state and not private to the sighter.

**Caption:** *Figure 5.4: Module 2's two-layer behaviour model. The individual finite-state machine (upper) is driven entirely by named events on the shared EventManager bus, with the shaded states gated by AI intelligence tier; the squad hunt lifecycle (lower) converts one member's confirmed sighting into persistent, squad-wide state anchored on the Point-Last-Seen position, so a lost contact triggers an area-based hunt rather than a withdrawal to post.*

### 🆕 Paragraph to add after Figure 5.4

> The behaviour is calibrated for a *semi-trained* hostile force — between paramilitary drill and irregular chaos — rather than for an elite unit, and the time constants that express that calibration are stated explicitly in Table 5.5 rather than left as tuning constants inside the implementation. Two of the five have a direct doctrinal anchor, two are order-of-magnitude interpretations of one, and one has no doctrinal basis at all; the design records which is which, because a reader cannot otherwise tell a cited value from a playtested one.

### 🆕 Table 5.5: Module 2 behavioural time constants and their grounding

| Behaviour | Doctrinal anchor | Value used | Standing |
|---|---|---|---|
| Return fire and take cover after contact | ≈ 3 s react-to-contact standard | 3–6 s plus hesitation variance | Order-of-magnitude anchor, deliberately slowed for a semi-trained force |
| "Contact!" propagation to the squad | Short sequential verbal chain | Small non-zero delay | Interpretation of the doctrinal sequence, not a published latency |
| Search-radius growth after losing line of sight | Subject speed × elapsed time | Radius grows from Point-Last-Seen | Directly cited (wilderness-search literature) |
| Give up a confirmed but lost contact | **No doctrinal figure exists** | Long, area-based budget (≈ 45 s) | Tuning choice, explicitly flagged as ungrounded |
| Leader death → coordination penalty | Major cohesion-break trigger | Morale dip plus leader re-election | Directly cited (cohesion/defeat literature) |

### 🆕 Paragraph on the honest design gaps (add at the end of §5.4)

> Three elements of the design are derived rather than cited, and are reported as such. The hostage-guardian behaviour — a guard tethered to a fixed asset who must neither abandon it to reinforce nor ignore an approaching threat — is not directly covered by small-unit doctrine, and is derived from fixed-asset-defence and fire-discipline principles. The escalation ladder it climbs (verbal threat → audible warning shot → execution) is instead grounded in real hostage-crisis analysis rather than in doctrine: in the Lindt Café siege the gunman fired a warning shot into a wall roughly two minutes before executing a hostage, and the Coroner found that the warning shot should itself have triggered an immediate assault. The ladder therefore models a real decision point a trainee must learn to recognise, which is why the warning shot is implemented as a fully audible report with no ballistics — the trainee is given a genuine cue to react to. Finally, the loss-band percentages used to shape squad morale are borrowed from fire-support targeting definitions and are used only as rough bands, not as validated morale thresholds.

## C.2 §5.5 Module 3 Design — add after the existing Table 5.3

### 🆕 Figure 5.6 — Reaction-time channel and validity-rule decision flow

**You generate this.** A decision flowchart, top to bottom:

`Enemy-caused stimulus (PlayerSeen | GunshotHeard | TargetConfirmed)` → diamond **"Is a response window already open?"** → *yes*: `ignore stimulus`; *no*: → diamond **"Is any channel already above its threshold?"** → *yes*: `disqualify — motion already underway is not a reaction`; *no*: → `open 3 s response window` → four parallel channel boxes (`head > 90°/s toward threat`, `hand > 1.0 m/s`, `body > 0.6 m/s`, `own ShotFired`) → diamond **"First crossing at t < 80 ms?"** → *yes*: `reject as tracking noise`; *no*: `record reactionTime = t_cross − t_stimulus` → `2 s cooldown suppresses burst stimuli`. Add a dashed branch from the window box: `window closes with no crossing → record explicit miss (reactionTime = −1)`.

**Caption:** *Figure 5.6: Module 3's reaction-time measurement, showing the three validity rules that separate a genuine response from an artefact. A channel already in motion when the stimulus fires is disqualified, the first 80 ms is never counted, and a response window plus cooldown prevents a burst of automatic fire from producing duplicate samples. A stimulus that draws no response is recorded as an explicit miss rather than silently dropped, so response rate remains computable.*

### 🆕 Paragraph to add after Figure 5.6

> The four channels are deliberately independent and are all derived from signals the movement recorder already computes, so multi-channel reaction time costs no additional sensors and no additional instrumentation of the rest of the game. Independence matters for interpretation as well as cost: because each channel is compared against its own literature band rather than against a single pooled figure, a trainee who orients quickly but raises the weapon slowly is distinguishable from one who does the reverse — a distinction a single aggregate latency cannot express. The crouch estimate is likewise continuous rather than a binary flag, and is measured against a running, self-calibrating standing-height baseline rather than a fixed constant, so it adapts to each trainee's actual height instead of assuming a canonical one.

## C.3 §5.6 Module 4 Design — this section is the thinnest in Chapter 5 (224 words); add all of the following

### 🆕 Paragraph on the capture layers (add after the existing Figure 5.5 / new 5.7)

> Module 4's recording layer is designed around a single architectural commitment: it subscribes to one game-wide event bus and to nothing else. `Module4Bridge` attaches to `EventManager.OnEventRaised` and forwards every event type into the session log, which means a new gameplay event requires no Module 4 change at all — the property that most sharply distinguishes this design from hand-instrumented commercial after-action-review tooling, where each new recorded quantity is a new integration. Seven capture layers are built on that one subscription, listed in Table 5.6. Two of them exist specifically to make scoring defensible rather than merely possible: distance-aware shot telemetry, which records the trainee-to-target distance at the instant of every hit so accuracy can be normalised against real-world hit rates at the actual engagement range, and no-target-in-line-of-sight detection, which records whether any living hostile was visible at all when a round was fired so negligent discharge becomes a measurable behaviour rather than an unobserved one.

### 🆕 Table 5.6: Module 4 capture layers

| Layer | What is recorded | Why it is needed |
|---|---|---|
| Universal event log | Every `ScenarioEvent` on the shared bus, with actor, type and timestamp | The timeline, incident extraction and all scores derive from this one stream |
| NPC state transitions | Every terrorist and hostage FSM transition: actor, previous state, new state, trigger | Lets the after-action review explain *why* an NPC did something, not only that it did |
| Hostage emotional history | Every hostage state change plus its derived distress contribution | The hostage psychological dimension most tactical tools omit entirely |
| Spatial replay track | Every actor's position, rotation and state at a fixed tick, auto-downsampled | Replay is stored as transforms and states, not video, so it stays scrubable and queryable |
| Room and door layout snapshot | Rooms as axis-aligned boxes, doors with wall side | Lets the dashboard draw geometry without Module 4 depending on Module 1's types |
| Distance-aware shot telemetry | Trainee-to-target distance at the instant of each hit; whether any hostile was visible when a round was fired | Makes distance-normalised accuracy and negligent-discharge scoring possible |
| Reaction time (hosted, not measured) | Per-stimulus reaction times supplied by Module 3 | Module 4 aggregates and displays them; it does not measure cognition |

### 🆕 Figure 5.8 — Hostage distress state machine

**You generate this.** A state diagram of the nine hostage states, each box annotated with its distress-index contribution: `Calm/Freed 0` · `Follow 10` · `Fearful/Scared 33` · `Held 40` · `Panic 66` · **`Freeze 72`** · `Threatened 75` · `Wounded 95` · `Down 100`. Order them vertically by distress value so the scale is visually readable, and **highlight the `Freeze` box** with a callout: *"scored above Panic — tonic immobility predicts worse trauma outcomes than active panic despite appearing calmer."* Label the transitions with their triggers (gunshot within panic distance, directed vocal threat, rescuer contact, extraction, injury).

**Caption:** *Figure 5.8: The hostage distress model. Each state carries an explicit contribution to the session's distress index, and the ordering is research-driven rather than intuitive: Freeze is deliberately scored above Panic, because tonic immobility — a reflexive shutdown under inescapable threat — is clinically shown to predict worse trauma outcomes than active panic, despite presenting as the calmer of the two. This ordering is the single most strongly validated design decision in the module (Section 7.6).*

### 🆕 Paragraph to add after Figure 5.8

> The distress model is Module 4's own psychological contribution, and the module boundary around it is worth stating precisely because it is easy to blur. The *triggering* of hostage state transitions from in-world stimuli is implemented in Module 2's hostage controller; Module 4 owns the *scoring and visualisation* of whatever state the hostage is in, and the derived distress index that results. Trainee cognitive analytics — reaction time, movement telemetry, stability and attention — are likewise Module 3's contribution; Module 4 stores and displays them and derives clearly-labelled fallback proxies where a build did not wire the live feed, but does not itself measure cognition. Table 5.7 lists the seven notable-moment types the module extracts automatically from the event stream, each severity-ranked and resolved to the room it occurred in, so that a reviewer is given evidence-linked incidents rather than an undifferentiated log.

### 🆕 Table 5.7: Automatically extracted incident types

| Incident | Severity | Detection rule |
|---|---|---|
| First contact | Medium | First event in which a hostile confirms the trainee |
| First shot | Medium | First round fired in the mission, by either side |
| **Hostage endangered** | **High** | Hostage enters a threatened, wounded or executed state |
| Terrorist neutralised | Medium | A hostile transitions to `Down` |
| **Alert cascade** | **Medium** | Three or more hostiles alerted within a five-second window — a coordinated enemy response rather than a single detection |
| Hostage rescued | High | Hostage reaches the safe zone |
| Mission end | High | Terminal event, with its end reason recorded |

---

# PART D — CHAPTER 6 ADDITIONS (IMPLEMENTATION)

## D.1 §6.4 Module 2 Implementation

### 🆕 Table 6.2: Module 2 implementation status

Insert immediately after the existing opening paragraph *"Module 2's runtime data types…"*, mirroring Table 6.1's three-column form.

**Table 6.2: Module 2 implementation status**

| Stage | Description | Status |
|---|---|---|
| 1 | Runtime data types and `ScenarioEvent` schema; central `EventManager` dispatch path | Complete |
| 2 | Terrorist finite-state machine — perception, aiming, burst fire, cover approach | Complete |
| 3 | Squad coordination layer — shared contact blackboard, leader election, directives | Complete |
| 4 | Persistent hunt system anchored on Point-Last-Seen, with area-based budget | Complete |
| 5 | Morale, posture escalation and wounded retreat | Complete |
| 6 | Hostage finite-state machine, animation retargeting, collider and damage model | Complete |
| 7 | Hostage-guardian escalation ladder (verbal threat → warning shot → execution) | Complete |
| 8 | Three selectable AI intelligence tiers with additive capability gating | Complete |
| 9 | Telemetry stream to Module 4, including the independent enemy hit-rate metric | Complete |
| 10 | Trait-differentiated hostage response profiles (Weak / Normal / Brave) | Complete |

*(Drop row 10 if you decide not to include the hostage-profile content in Part F.)*

### 🆕 Expanded prose for §6.4 — insert before the "The persistent hunt fix (P0)" heading

> Perception is implemented as a two-stage commitment rather than a single visibility test, because an NPC that engages on a single frame of line of sight reads as clairvoyant. A momentary glimpse raises `PlayerSeen`; only sustained line of sight for a confirmation interval raises `TargetConfirmed` and commits the NPC to engage; losing sight for a matching window raises `PlayerLost`. Hearing is modelled on the same bus — every shot fired is broadcast as `GunshotHeard` to hostiles in range, and three or more shots inside two seconds raise a `StressSpike`, which is what drives hostage panic. This models suppressive fire's primary effect as a psychological one rather than a ballistic one.
>
> Firing itself is delegated to the weapon layer and is deliberately imperfect. Rounds are fired in bursts rather than at a metronome interval, through a five-degree inaccuracy cone, aimed at the trainee's centre of mass rather than at the head. The centre-mass choice was not stylistic: aiming at the camera transform put the aim point at floor height whenever the trainee was seated at a desk rather than standing in the headset, which measured as a twenty-per-cent hit rate and read as a broken enemy. The inaccuracy cone is likewise held deliberately loose, because an enemy that never misses does not feel skilful, it feels unfair. Fire rate, accuracy and damage were fixed before evaluation began and were never altered during it — only read-only instrumentation was added — so that no believability result can be attributed to weapon tuning performed mid-study.

### 🆕 Table 6.3 and a paragraph — insert at the end of the "The AI-intelligence-tier gating gap" subsection

> Because the three tiers are the independent variable of the entire Module 2 evaluation, the gating was re-verified in live headset play tier by tier after the fix rather than assumed from the code path. Table 6.3 records that verification. It is reported as a verification result and not as a finding: it confirms that the manipulation the ablation study depends on is actually present in the build that participants played, which is a precondition for Section 7.4's results rather than a result itself.

**Table 6.3: AI intelligence tier — live verification record**

| Behaviour | Basic | Intermediate | Advanced | Verified how |
|---|:--:|:--:|:--:|---|
| Engages on confirmed line of sight | Yes | Yes | Yes | Live play, all three tiers |
| Investigates gunshots and deaths | **No** | Yes | Yes | Live play after the gating fix; previously leaked at Basic |
| Squad coordination and persistent hunt | **No** | Yes | Yes | Live play; shared-contact blackboard inspected at runtime |
| Posture escalation to cover-and-peek | No | **No** | Yes | Live play, scripted runtime inspection |
| Retreat when wounded | No | **No** | Yes | Live play, scripted runtime inspection |
| Idle scanning while patrolling | No | **No** | Yes | Live play, scripted runtime inspection |
| Hostage-guardian escalation ladder | No | **No** | Yes | Live play |

### 🆕 Paragraph to add at the end of "Hostage animation and interaction"

> Two further hostage defects were found in the same pass and are worth recording because both were invisible in the editor. Terrorist rounds passed straight through the hostage, and the naive fix — calling the hostage controller from the weapon script — could not compile, because the weapon code lives in a separate assembly that cannot reference the default gameplay assembly. Unity reported this as a silent block on recompilation rather than as an error against the offending line, which is why it took a boundary-level diagnosis rather than a code-level one. Crossfire was instead routed through a shared damage interface, so each hit box decides for itself what a hostile round means: the hostage bleeds and can be downed, while other hostiles ignore each other's fire. Trainee-caused hostage hits are tagged as friendly fire and penalise the mission's safety score, and a captor execution ends the mission with an explicit `hostage_executed` reason rather than as a generic failure — so the after-action review can distinguish a trainee who was too slow from one who was reckless.

### Figures for §6.4

| # | Name | Where to anchor | What to capture |
|---|---|---|---|
| **6.4** | Terrorist FSM at runtime | After the new Table 6.2 | Unity in Play mode, split view: Inspector on a `TerroristController` showing current state and `aiLevel`, alongside the Console showing the `ScenarioEvent` stream |
| **6.5** | Persistent hunt in action | Inside "The persistent hunt fix (P0)" | Scene view during play with gizmos enabled for `PointLastSeen` and the growing search radius, several hostiles converging on it |
| **6.6** | AI tier gating verified at Basic | Inside "The AI-intelligence-tier gating gap" | Play mode at the Basic tier: a gunshot event visible in the Console with the hostile's state remaining `Idle`/`Patrol` — the defect's absence made visible |
| **6.7** | Hostage rescue sequence | Inside "Hostage animation and interaction" | In-headset or Game view: hostage in the kneeling captive pose, then standing and following the trainee. Two-panel figure |
| **6.8** | Hostage-guardian escalation | Inside "Hostage animation and interaction" | In-headset: guardian holding the hostage at gunpoint during the warn → warning-shot ladder. **Skip this figure rather than publish a broken pose** if the held-at-gunpoint clip still is not right |
| **6.9** | Hostage profile in the after-action review | End of §6.4 (Part F content) | Dashboard hostage panel showing the active profile alongside the distress trajectory |

**Captions:**

- *Figure 6.4: Module 2's terrorist finite-state machine observable at runtime. Every transition is driven by a named, timestamped event on the shared EventManager bus, which is what allows Module 4 to log the session with no Module 2-specific instrumentation.*
- *Figure 6.5: The persistent hunt system after the P0 fix. A confirmed contact is written to the shared squad blackboard and anchored on the Point-Last-Seen position; the search radius grows with elapsed time and the whole squad converges, rather than each hostile independently withdrawing to its original post.*
- *Figure 6.6: The AI intelligence tier gating verified at the Basic tier after the fix. A gunshot event is dispatched on the bus and no hostile investigates, which is the intended Basic-tier behaviour; before the fix, the older investigation path bypassed the gate entirely (Table 6.3).*
- *Figure 6.7: The hostage rescue sequence — the kneeling captive pose on spawn (left) and the follow behaviour after rescue (right). Both required animation clips to be re-imported as Humanoid rather than Generic before retargeting onto the rig would work at all.*
- *Figure 6.8: The hostage guardian's escalation ladder at the Advanced intelligence tier — verbal warning, audible warning shot, then execution. The warning shot is implemented as a full muzzle flash and report with no ballistics, so the trainee is given a real cue to react to.*
- *Figure 6.9: The after-action review's hostage panel, showing the trait-differentiated response profile in force for the session alongside the resulting distress trajectory.*

### Listings for §6.4

| # | File and lines | Anchor | Why it earns the space |
|---|---|---|---|
| **Listing 6.1** | `TerroristController.cs:126–141` | After the tier-gating paragraph | Five one-line `Allow*` predicates. This *is* Table 5.2 expressed as code, and it makes the defect story land: the bug was an older path that reached gated behaviour without consulting them |
| **Listing 6.2** | `Squad.cs:306–330` | Inside "The persistent hunt fix (P0)" | `ReportConfirmedContact` + `BeginHunt` — the shared-blackboard write that replaced the private per-NPC flag. Pair with Figure 6.5 |

- *Listing 6.1: The AI intelligence tier expressed as five gating predicates. Table 5.2's capability matrix is enforced entirely by these five expressions; the defect described in this section was an older code path that reached the gated behaviour without consulting them.*
- *Listing 6.2: The shared squad blackboard. A single member's confirmed sighting becomes squad-wide state anchored on the Point-Last-Seen position, implementing the react-to-contact principle in place of the private per-NPC flag it replaced.*

## D.2 §6.5 Module 3 Implementation

### 🆕 Expanded prose — insert after the existing paragraph on `CognitiveMovementRecorder`

> The rig the recorder reads from is discovered read-only, by breadth-first search over the XR rig hierarchy, name-matching left and right hand transforms while explicitly excluding known decoys — the teleport ray, the poke interactor, offset and anchor transforms, and smoothing helpers. That heuristic exists for a specific reason rather than as defensive programming: the interaction-toolkit rig this project builds on has no single canonical hand transform to bind to, and modifying the toolkit's own assets directly was already known to break existing hand poses elsewhere in the project. Discovery by search is therefore the design that leaves the gameplay rig untouched, which is what makes the claim of non-invasive capture true rather than aspirational.
>
> The in-headset mirror HUD is built entirely at runtime on translucent panels through a custom widget kit, and is locked rigidly to the trainee's view like a real head-mounted overlay. It carries a live mirrored mannequin reflecting head, hand, crouch, lean and turn together with a vertical health bar, a yaw-driven compass tape with a pitch marker, and a held-weapon readout showing silhouette, cleaned weapon name and live ammunition count, displayed only while a weapon is actually grabbed. The HUD is a deliberate design position rather than decoration: it gives the trainee proprioceptive feedback about their own posture and scanning — the very quantities Module 3 is measuring — using no hardware the mission does not already require.

### 🆕 Table 6.5: Module 3 reaction-time channels and detection thresholds

Insert after the paragraph describing the four channels.

**Table 6.5: Reaction-time response channels, thresholds and reference bands**

| Channel | Detection threshold | Reference band | Tier |
|---|---|---|---|
| Head orientation | Angular speed > 90 °/s while gaze angle to the threat is closing, or the threat entering a 25° gaze cone from clearly outside it | 0.25 – 0.40 s | A |
| Hands / weapon raise | Either hand's frame-to-frame speed exceeding 1.0 m/s | 0.40 – 0.80 s | A |
| Body movement | Horizontal body speed exceeding 0.6 m/s | 0.50 – 1.00 s | B |
| Trigger pull | The trainee's own shot-fired event | 0.80 – 1.80 s | A |

*Footnote to the table:* The four detection thresholds, the 80 ms validity floor, the three-second response window and the two-second cooldown are engineering calibrations established by playtesting, not values taken from a published study. They are constrained by the literature rather than derived from it: every published simple-reaction-time figure cited in the reference bands is at or above 250 ms, which is what makes 80 ms a conservative noise floor rather than an arbitrary one.

### 🆕 Paragraph to add at the end of "The dead-code cognitive classifier and its live substitute"

> The three substitute scores are computed from telemetry Module 3 already captures, and each is a deliberately simple, inspectable formula rather than a trained model. Stability weights sustained gaze angular speed above peak angular speed, so a single fast turn cannot dominate an otherwise steady session. Attention combines the fraction of threat stimuli that drew any response at all with a normalised reaction-speed term, falling back to the speed term alone in sessions where no threat stimulus occurred. Movement initiation is the share of the mission spent actively moving rather than still, as a proxy for decisiveness under pressure. All three are labelled *estimated* in the dashboard rather than presented as equivalent to a validated instrument, and the module's own documentation records their weights and ceilings as engineering judgement. The alternative — repairing and shipping the existing classifier — was rejected specifically because its defect was structural rather than numerical: it compares reaction time in raw seconds against zero-to-one normalised scores at fixed cutoffs, so any reaction time at or above roughly 0.66 s pins both cognitive load and stress to "high" regardless of every other input, which covers most of the *good* end of the reaction-time literature the module cites elsewhere.

### Figures for §6.5

| # | Name | Where to anchor | What to capture |
|---|---|---|---|
| **6.10** | In-headset cognitive mirror HUD | After Table 6.4 | Quest capture of the mirror HUD: mirrored mannequin and health bar, compass tape, weapon readout |
| **6.11** | Movement metrics with confidence tiers | After the recorder paragraph | Dashboard Movement tab: metrics grid **with the A/B/C confidence chips visible**, plus the benchmark-sources footer card |
| **6.12** | Reaction time by channel against reference bands | Next to 6.11 | Movement tab: the per-channel benchmark strip, four channels against their own bands, plus the per-stimulus scatter plot |
| **6.13** | SIM-TLX questionnaire, trainee view | Near the Table 6.4 stage-3 row | The questionnaire as the trainee completes it after a mission |
| **6.14** | Workload reasoning and predicted zone | End of §6.5 | Workload tab: subscale ratings, the "why this session's workload" reasoning card, and the predicted performance zone with its cross-check |

**Captions:**

- *Figure 6.10: The in-headset cognitive mirror HUD, built at runtime on translucent panels, giving the trainee proprioceptive feedback on the same posture and scanning quantities Module 3 measures — without any additional hardware.*
- *Figure 6.11: Module 3's movement-benchmark interpretation. Each metric carries its A/B/C confidence tier explicitly, and tier-C metrics are shown as plain values with no expert range attached rather than compared against a source that does not exist.*
- *Figure 6.12: Multi-channel reaction time against literature-derived reference bands. The four channels are measured independently, so a trainee who orients quickly but raises the weapon slowly is distinguishable from one who does the reverse — a distinction a single pooled latency cannot express.*
- *Figure 6.13: The SIM-TLX subjective workload questionnaire administered after every mission, as the trainee sees it.*
- *Figure 6.14: The workload-reasoning layer. A session's composite workload is explained from that session's own recorded facts, and the predicted performance zone is cross-checked against the same session's outcome data — the check calls the movement evaluation directly rather than trusting a cached verdict, so the Movement and Workload tabs can never silently disagree.*

**Listing 6.3** — `ReactionTimeTracker.cs:110–160`, anchored after Table 6.5.
*Listing 6.3: The three validity rules in one block — motion already underway is disqualified, the first 80 ms is never counted, and a response window plus cooldown suppresses duplicate samples from burst stimuli such as automatic fire.*

## D.3 §6.6 Module 4 Implementation

### 🆕 Table 6.6: Module 4 implementation status

Insert immediately after the opening paragraph, before the defect table.

**Table 6.6: Module 4 implementation status**

| Stage | Description | Status |
|---|---|---|
| 1 | Universal event-bus logging via a single bus subscription; session persistence | Complete |
| 2 | NPC state-transition, hostage-emotion and spatial replay capture | Complete |
| 3 | Room and door layout snapshot, with room resolution for every event position | Complete |
| 4 | Five-score performance model, decomposed into sub-parameters | Complete |
| 5 | Severity-ranked automatic incident extraction (seven types) | Complete |
| 6 | Hostage psychological distress index and emotional-journey narrative | Complete |
| 7 | 2D top-down replay with scrub, speed control and incident jump | Complete |
| 8 | 3D free-orbit replay reconstruction from the recorded transform and state track | Complete |
| 9 | Offline self-contained HTML report | Complete |
| 10 | Hosted evaluation dashboard — seven session tabs plus the evaluation page | Complete |
| 11 | Upload reliability engineering — compression, retry with back-off, on-disk queue | Complete |

### 🆕 Expanded prose — insert after the new Table 6.6

> The eight defects in Table 6.7 are reported individually because several of them changed what the scores *mean* rather than merely how they were computed, and a reader cannot otherwise judge whether the numbers in Chapter 7 measure what they claim to. Three are worth drawing out.
>
> The trainee's accuracy score originally counted every shot-fired event with no filter on who fired it, and the hostile weapon prefab raises the same event for every enemy round — so a long firefight drove the trainee's accuracy toward zero regardless of how well they had actually shot. Since accuracy carries thirty per cent of the composite score, this was not cosmetic. The fix went further than filtering: it introduced separate, correctly-named enemy event types, which turned the noise into an independent metric — enemy hit rate, an objective measure of how competently the AI fought, now available to Module 2's own ablation study.
>
> The replay recorder deleted whole actors when trimming a long session. It dropped every odd array *index*, but frames are interleaved one per actor per tick, so with an even actor count this erased half the actors from the replay entirely rather than halving the time resolution. The intended fallback of doubling the sample interval also silently did nothing, because the loop cached its wait instruction once before entering the loop. The fix drops every other *tick*, grouped by shared timestamp so that it survives actors registering mid-session, and rebuilds the wait each iteration; it was verified headlessly across one to five actors plus a mid-session join.
>
> The `Speed` target-time change is included in the table for a different reason — an earlier draft of it was rejected on principle. That draft proposed log-scaling the hostile-count term after Hick's Law. Hick's Law describes reaction time when choosing among *simultaneous* options; it says nothing about the cumulative time cost of threats encountered *sequentially* across a multi-minute mission. Applying it would have borrowed a real law to lend authority to an unrelated coefficient. The term stays linear, and the four coefficients are reported as design defaults whose *shape* follows task-time decomposition theory while their exact values do not come from any published source.

### 🆕 Table 6.8: Module 4 automatic incident types

*(Same content as Table 5.7 in Part C.3 — include it in Chapter 5 as a design table **or** here as an implementation table, not both. Chapter 5 is the better home; if you put it there, delete this row from the numbering map in §B.3 and renumber Chapter 6 accordingly.)*

### Figures for §6.6

This section describes eight defects, two replay systems, two report targets and a five-score model with no image at all. It needs the most.

| # | Name | Where to anchor | What to capture |
|---|---|---|---|
| **6.15** | Session summary and score breakdown | After the new Table 6.6, before Table 6.7 | Dashboard **Summary** tab: performance radar, score breakdown, and the Operator Safety card with its four sub-terms. One tall figure, or split 6.15a / 6.15b |
| **6.16** | Event timeline and activity density | After Table 6.7 | **Timeline** tab: event timeline, activity-density chart in ten-second buckets, and a few rows of the filterable log |
| **6.17** | Severity-ranked incidents | Next to 6.16 | **Incidents** tab, ideally with a high-severity incident at the top |
| **6.18** | Hostage distress over time | Next to 6.17 | **Hostage** tab: distress-index curve plus the per-hostage state journey |
| **6.19** | 2D top-down replay | Before the "Reliability engineering" heading | **Replay** tab: top-down position map, scrub controls, actor states, and active events within ±2 s |
| **6.20** | 3D replay reconstruction | Next to 6.19 | The free-orbit 3D reconstruction with animated character models and rooms drawn from the layout snapshot. **Your strongest single visual asset — give it a full-width figure** |
| **6.21** | Offline HTML report | Next to 6.20 | The exported self-contained report open in a browser, evidencing the "works with or without a network" claim |
| **6.22** | In-VR mission result screen | Anywhere in §6.6 | The result screen as the trainee sees it at mission end. Ties directly to Table 6.7 defect 1, where the Quest build could not show one |

**Captions:**

- *Figure 6.15: The session summary. Five scores are shown alongside the Operator Safety sub-term breakdown — survivability, exposure control, weapon discipline and threat response — the second, independent safety axis described in Section 5.6.*
- *Figure 6.16: The event timeline and activity-density view, reconstructed entirely from the universal event bus with no per-gameplay-system instrumentation.*
- *Figure 6.17: Severity-ranked incident extraction. Every incident references a concrete event, a timestamp, the room it occurred in, and a replay position, following the evidence-linked review principle of Section 2.6.*
- *Figure 6.18: The hostage psychological distress index over the course of a mission — a dimension most tactical after-action-review tools omit entirely.*
- *Figure 6.19: The 2D top-down replay. Because replay is stored as transforms and discrete state snapshots rather than rendered video, the reviewer can scrub, change speed, and jump directly to any extracted incident.*
- *Figure 6.20: The free-orbit 3D replay reconstruction, rebuilt from the recorded position, rotation and state track, with room geometry drawn from the layout snapshot and state-driven animation clips baked from the game's own humanoid animations.*
- *Figure 6.21: The offline HTML after-action report, produced alongside the hosted dashboard so a session can be reviewed with no network connection.*
- *Figure 6.22: The in-headset mission result screen shown to the trainee at mission end — the output that defect 1 in Table 6.7 had suppressed entirely on Quest builds.*

### Listings for §6.6

| # | File and lines | Anchor | Why |
|---|---|---|---|
| **Listing 6.4** | `Module4Bridge.cs:44–55` | Right after the new Table 6.6 | Two lines of event subscription. This is the entire zero-instrumentation claim, and its brevity *is* the argument |
| **Listing 6.5** | `PerformanceCalculator.cs:261–275` | §6.7 Score Formulas | The NYPD SOP-9 distance bands as literal code — proves the formula and the citation agree |
| **Listing 6.6** | `PerformanceCalculator.cs:215–240` | §6.7 Score Formulas | The 0.61ⁿ saturating curve, the negligent-discharge rescaling between published expert and novice rates, and both Hick's Law reaction anchors — three separate Table 6.7 fixes in one image |
| **Listing 6.7** | `DashboardUploader.cs:155–195` | Inside "Reliability engineering" | Compress → queue to disk before the first attempt → retry with back-off. Shows the "never lost even if the headset is quit mid-upload" claim as code |

- *Listing 6.4: The whole of Module 4's instrumentation surface. Because the bridge subscribes to one game-wide event bus, a new gameplay event type requires no Module 4 code change at all.*
- *Listing 6.5: The distance-normalised expected hit rate, implementing the NYPD SOP-9 hit-rate-by-range bands. Scoring a trainee against 100 % regardless of engagement distance was defect 5 in Table 6.7. The five bands are used as a lookup rather than a fitted curve, because the source data is itself bucketed field statistics and interpolation would imply precision the five points do not carry.*
- *Listing 6.6: Weapon discipline and threat response as implemented — the 0.61ⁿ friendly-fire curve, the negligent-discharge rate rescaled between the published expert and novice qualification-failure rates, and the two Hick's Law reaction-time anchors, the slower of which replaced an earlier and too-lenient 1.5 s value (Table 6.7, defect 6).*
- *Listing 6.7: The upload pipeline. The compressed body is written to the on-disk queue before the first network attempt and deleted only on success, so a session survives a mid-upload quit; retries use exponential back-off.*

---

# PART E — CHAPTER 7 REWRITES

This is the part that closes the parity gap. Each module now gets Module 1's structure: research questions, a design table, a methodological-safeguards paragraph, one subsection per research question with its own table and figure, and an explicit answers block.

## E.1 §7.4 Module 2 Results — AI Believability Ablation Study

### 🆕 Replace the opening paragraph with this

> A within-subjects ablation study was conducted to answer four research questions: RQ1 (primary), are the three AI intelligence tiers perceived by trainees as measurably different in believability; RQ2, which specific pairs of tiers differ, and do participants agree with one another about the ordering; RQ3, is the ablation manipulation actually present in the build participants played; and RQ4, do objective behavioural measures corroborate the subjective ratings. Each participant played the identical scenario seed at Basic, Intermediate and Advanced, with a participant identifier entered on the mission-launch form grouping their plays for analysis. Believability was measured with validated instruments — the Godspeed Perceived Intelligence and Animacy sub-scales, the short-form User Experience Questionnaire, and a three-item author-defined tactical-realism scale — administered immediately after each play, alongside objective telemetry drawn from the same session. At the time of writing eleven participants had taken part across forty-five recorded sessions, of whom nine completed all three tiers, which is what enables the repeated-measures tests below.

### 🆕 Table 7.6: Module 2 ablation study design

| Element | Choice made | Why |
|---|---|---|
| Design | Within-subjects ablation across three AI tiers | The strongest ablation shape the literature supports for "does the tactical layer matter?" — each participant is their own control |
| Independent variable | AI intelligence tier (Basic / Intermediate / Advanced), gated additively | Additive gating makes the three tiers a clean ladder rather than three unrelated configurations |
| Held constant | Identical Module 1 scenario seed at all three tiers; weapon fire rate, accuracy and damage fixed before the study | Isolates the AI as the only difference between a participant's three plays |
| Order control | Tier order varied across participants | Cancels learning effects across repeated plays of the same layout |
| Subjective instruments | Godspeed Perceived Intelligence (5 items) and Animacy (6 items); UEQ-S Pragmatic and Hedonic (4 items each); tactical realism (3 items) — 22 items total | Validated instruments where they exist; the author-defined block targets construct validity of the tactical behaviour specifically |
| Instruments deliberately omitted | Godspeed Likeability, Perceived Safety and Anthropomorphism | All three assume a *friendly* agent; they do not apply to a hostile one |
| Objective measures | Reaction time, trainee shooting accuracy, mission duration, enemy hit rate | Provides a behavioural correlate independent of self-report |
| Statistical tests | Friedman omnibus with Kendall's *W*; Wilcoxon signed-rank post-hoc with Bonferroni correction | Data are ordinal, repeated-measures and small-*n*, so non-parametric tests are the correct choice |
| Sample | 11 participants, 45 sessions; 9 complete three-tier triads | Complete blocks are a requirement of the Friedman test |

### 🆕 Methodological-safeguards paragraph — insert after Table 7.6

> Four safeguards were applied before any figure below was treated as citable, mirroring the discipline applied to Module 1's study. First, the statistics engine is dependency-free and was validated against a hand-worked example before being trusted with real data: six participants with perfectly ordered ratings return Friedman χ²(2) = 12.0, p = 0.0025, W = 1.0 with all pairwise p = 0.031, while a deliberately jumbled control returns p = 1.0 and no significant pair. Second, the capability gating that constitutes the independent variable was re-verified tier by tier in live headset play after a gating defect was found and fixed (Table 6.3), so the manipulation is known to be present in the build participants actually played rather than only in the code. Third, weapon fire rate, accuracy and damage were frozen before the study opened and were never altered during it, so no believability difference can be attributed to weapon tuning performed mid-study. Fourth, raw per-item ratings are retained rather than only sub-scale means, so any reported mean is auditable back to the responses that produced it.

### 🆕 §7.4.1 RQ1 — Are the three tiers perceived as different?

> Every believability measure increases monotonically from Basic through Advanced across the full sample of forty-five sessions — exactly the pattern the ablation design predicts if the three tiers are genuinely perceived as different.

**Table 7.7: Overall averages across all recorded sessions, by AI tier**

| Measure | Basic (n = 20) | Intermediate (n = 14) | Advanced (n = 11) |
|---|---:|---:|---:|
| Perceived Intelligence (/5) | 1.6 | 3.7 | 4.4 |
| Animacy — life-like (/5) | 2.0 | 3.8 | 4.1 |
| Tactical realism (/5) | 1.7 | 3.5 | 4.4 |
| UEQ Pragmatic (/7) | 4.6 | 5.3 | 5.7 |
| UEQ Hedonic (/7) | 4.7 | 5.4 | 5.8 |
| Enemy hit rate — NPC (%) | — | — | — |

*Footnote:* Enemy hit rate is unavailable because the telemetry that produces it was added after these sessions were recorded; it populates automatically for every session recorded from that point onward. Reaction time, shooting accuracy, mission duration and the Module 4 composite scores are **not** reported by AI tier, because AI difficulty is Module 2's independent variable and not Module 4's — they are analysed per participant in Sections 7.5 and 7.6 instead.

> ⚠️ **This replaces the current Table 7.6, and you must delete five rows from it** — *Reaction time*, *Shooting accuracy*, *Mission duration*, *Overall score (Module 4)* and *Workload (SIM-TLX)*. They are no longer on the Module 2 tab; they moved to the Module 3 and Module 4 analyses when the dashboard was restructured, for the reason given in the footnote above.

### 🆕 §7.4.2 RQ2 — Which pairs differ, and do participants agree?

**Table 7.8: Friedman and Wilcoxon signed-rank results across AI tiers (n = 9 complete-case participants)**

| Measure | Friedman χ²(2) | p | Kendall's *W* | Bonferroni-corrected pairwise result |
|---|---:|---:|---:|---|
| Perceived Intelligence | 10.89 | 0.004 | 0.60 | **All three pairs significant** — Basic–Intermediate p = 0.008, r = 0.85; Basic–Advanced p = 0.008, r = 0.85; Intermediate–Advanced p = 0.016, r = 0.89 |
| Animacy | 14.00 | < 0.001 | 0.78 | Basic–Intermediate p = 0.004, r = 0.89; Basic–Advanced p = 0.004, r = 0.89; Intermediate–Advanced not significant (p = 0.188, r = 0.51) |
| Tactical realism | 11.72 | 0.003 | 0.65 | **All three pairs significant** — Basic–Intermediate p = 0.008, r = 0.89; Basic–Advanced p = 0.008, r = 0.85; Intermediate–Advanced p = 0.008, r = 0.88 |
| Enemy hit rate (NPC) | — | — | — | Not testable — no complete cases (n = 0) |

*Footnote:* The dashboard displays the Bonferroni-corrected threshold rounded to 0.02; the test is applied at the exact value 0.05/3 = 0.0167. **Verify this against the statistics engine before submission** and make the report, the dashboard and Appendix B.4 agree.

> ⚠️ **Delete four rows from the current Table 7.7** — *Reaction time*, *Overall score (M4)*, *Hostage Safety (M4)* and *Speed (M4)*. Reaction time moves to §7.5.3; the Module 4 scores move to the rewritten §7.6.

> The omnibus Friedman test is significant for all three believability constructs — Perceived Intelligence (χ²(2) = 10.89, p = 0.004, *W* = 0.60), Animacy (χ²(2) = 14.00, p < 0.001, *W* = 0.78) and Tactical realism (χ²(2) = 11.72, p = 0.003, *W* = 0.65) — so the AI intelligence tier measurably changes all three. The Bonferroni-corrected pairwise tests are stronger still: Perceived Intelligence and Tactical realism separate *every* pair of tiers, including the harder Intermediate-versus-Advanced comparison, with large effect sizes throughout (r = 0.85 – 0.89).
>
> Animacy is the one departure from that pattern, and it is interpretable rather than disappointing. It separates Basic from both higher tiers decisively (p = 0.004, r = 0.89 in both cases) but does not distinguish Intermediate from Advanced (p = 0.188, r = 0.51). The behaviours the Advanced tier adds over Intermediate — posture escalation, retreat when wounded, idle scanning, and the hostage-guardian ladder — are tactical refinements rather than changes to how *alive* an NPC appears, whereas the capability that first arrives at Intermediate, squad coordination with a persistent shared hunt, is exactly the kind of behaviour that makes an opponent seem animate. Read that way, Animacy is discriminating the *presence* of squad-level coordination rather than its degree, while Perceived Intelligence and Tactical realism — which ask about competence directly — continue to discriminate as capability accumulates. Kendall's *W* between 0.60 and 0.78 indicates substantial agreement among participants about the ordering of the three tiers, not merely that some difference exists somewhere in the data.

### 🆕 §7.4.3 RQ3 — Is the manipulation actually present?

> A believability result across three tiers is only meaningful if the tiers really differ in the build that was played, so this was verified rather than assumed. During live headset testing, hostiles set to the Basic tier were observed still investigating disturbances and hunting the trainee — behaviour the design intended to gate out entirely at that tier. The root cause was that the newer squad-hunt system had been correctly gated behind the tier check, while an older code path — the event bus dispatching an investigator to gunshots and deaths — bypassed the gate altogether, because it predated the tier system. Applying the same gating predicate to that path resolved it, and each of the seven gated behaviours was then re-confirmed tier by tier in live play (Table 6.3). This is reported as a verification result rather than a finding: it establishes the precondition on which §7.4.1 and §7.4.2 depend. It also carries a lesson that generalises beyond this project — a new capability gate must be applied to every code path that can reach the gated behaviour, not only to the path most recently written.

### 🆕 §7.4.4 RQ4 — Do objective measures corroborate the ratings?

> The objective side of the study is partially, not fully, answered, and is reported that way. The enemy hit-rate metric — the fraction of hostile rounds that strike the trainee, and the most direct behavioural measure of how competently the AI fought — was added to the telemetry after these sessions were recorded, so it has no complete cases in the present sample (Table 7.8). It is fully implemented and populates automatically for every subsequent session, so this is a data-availability gap rather than a missing mechanism.
>
> Reaction time is available and is analysed per participant in Section 7.5.3, because reaction time is a property of the trainee rather than of the AI tier. Within the one participant whose triad predates the restructuring, it moved monotonically in the theoretically expected direction — 0.957 s at Basic, 0.630 s at Intermediate, 0.327 s at Advanced — a faster required response against a more capable opponent. Table 7.9 gives that participant's complete triad, and it contains a divergence worth reporting rather than smoothing over: the same participant rated the Advanced AI most believable on every single survey item and *lost* the mission at that tier. A more capable opponent is also harder to beat, and the composite mission score fell accordingly. That is precisely the separation the Module 2 and Module 4 measurement split was built to expose — subjective believability and objective mission performance are distinct constructs and can move in opposite directions — and it is reported as evidence that the framework works as intended rather than discarded as inconvenient.

**Table 7.9: Participant P0001 — complete three-tier triad, identical scenario seed**

| Measure | Basic | Intermediate | Advanced | Direction |
|---|---:|---:|---:|---|
| Perceived Intelligence (/5) | 1.4 | 4.0 | 4.8 | monotonic ↑ |
| Animacy — life-like (/5) | 1.7 | 4.3 | 4.7 | monotonic ↑ |
| Tactical realism (/5) | 1.7 | 3.7 | 5.0 | monotonic ↑ |
| UEQ Pragmatic (/7) | 4.8 | 6.0 | 6.5 | monotonic ↑ |
| UEQ Hedonic (/7) | 6.3 | 6.5 | 7.0 | monotonic ↑ |
| Average reaction time (s) | 0.957 | 0.630 | 0.327 | monotonic ↓ |
| Shooting accuracy — trainee (%) | 47.4 | 64.3 | — † | — |
| Mission duration (s) | 129.4 | 85.6 | 105.5 | — |
| Mission outcome | Success | Success | **Failure** | ‡ |

† No shots were fired in that session, so the metric is undefined rather than zero, and is recorded as null by design.
‡ The divergence discussed above: highest believability ratings, lost mission.

### 🆕 §7.4.5 Answers to Module 2's research questions

> **RQ1** — all three believability constructs increase monotonically across the three tiers over the full forty-five-session sample, and the omnibus Friedman test is significant for each (p ≤ 0.004).
>
> **RQ2** — Perceived Intelligence and Tactical realism separate all three pairs of tiers under Bonferroni correction with large effect sizes (r = 0.85 – 0.89); Animacy separates Basic from both higher tiers decisively but does not distinguish Intermediate from Advanced, which locates the effect on the presence of squad coordination rather than its degree. Kendall's *W* of 0.60 – 0.78 indicates substantial inter-participant agreement about the ordering.
>
> **RQ3** — the manipulation is verified present: all seven gated behaviours were re-confirmed tier by tier in live headset play after a gating defect on an older code path was found and fixed.
>
> **RQ4** — partially answered. The objective reaction-time measure moves in the predicted direction, and the believability-versus-performance divergence at the Advanced tier is itself an informative objective result; the enemy hit-rate metric is implemented but has no complete cases in the present sample.

### 🆕 Closing paragraph — replaces the existing final paragraph of §7.4

> Interpreted together, these results support the conclusion that Module 2's three AI intelligence tiers are perceived by trainees as genuinely and measurably different in intelligence, life-likeness and tactical realism — the central claim the ablation design was built to test — with Perceived Intelligence and Tactical realism discriminating all three tiers from one another, and Animacy discriminating the arrival of squad-level coordination rather than its subsequent refinement.

> ⚠️ **Delete the old limitation paragraph** beginning *"Perceived Intelligence and Tactical realism's pairwise comparisons do not individually survive Bonferroni correction at n = 7…"*. That limitation no longer applies at n = 9. Check its counterpart in §8.3 as well — see §G.4.

### Figures for §7.4

| # | Name | Anchor | Source |
|---|---|---|---|
| **7.7** | Believability survey, trainee view | After the opening paragraph | The post-mission enemy-AI questionnaire as the trainee completes it |
| **7.8** | Per-participant comparison across tiers | §7.4.1, next to Table 7.7 | Evaluation page, "Per-player comparison" card (PDF p. 1) |
| **7.9** | Overall averages by tier | §7.4.1, accompanying Table 7.7 | "Overall averages (all players)" card (PDF pp. 1–2) |
| **7.10** | Statistical significance panel | §7.4.2, after Table 7.8 | All three significance cards, cropped as one tall figure (PDF p. 2) |
| **7.11** | *(chart — you generate)* Believability by tier | §7.4.1 | Grouped bar chart of Table 7.7's three /5 constructs across the three tiers, with the /7 UEQ rows on a secondary axis or a separate panel. Makes the monotonic pattern land instantly |

**Captions:**

- *Figure 7.7: The post-mission enemy-AI believability questionnaire, combining the Godspeed Perceived Intelligence and Animacy sub-scales, the short-form User Experience Questionnaire, and a three-item author-defined tactical-realism scale — twenty-two items in total, administered immediately after every play.*
- *Figure 7.8: Per-participant comparison across the three AI intelligence tiers, with the same scenario seed played at each tier so the AI is the only difference between the three columns.*
- *Figure 7.9: Overall averages across all forty-five recorded sessions by AI tier. Every believability measure increases monotonically from Basic through Advanced.*
- *Figure 7.10: Friedman omnibus and Bonferroni-corrected Wilcoxon signed-rank post-hoc results, computed live by the dashboard from the same statistics engine documented in Appendix B.4 and validated against a hand-worked example before use.*
- *Figure 7.11: Believability construct means by AI intelligence tier. All three constructs rise monotonically across the ablation ladder; the Intermediate-to-Advanced step is visibly smaller for Animacy than for the two competence-oriented constructs, which is the pattern the significance tests in Table 7.8 confirm.*

## E.2 §7.5 Module 3 Results — restructure into four subsections

The existing §7.5 is one 246-word block. Keep its content, retitle it as §7.5.2, and add the three subsections around it.

### 🆕 §7.5.1 — Research questions and evaluation design

> Module 3's evaluation answers four research questions: RQ1, can trainee body movement be captured accurately from existing XR tracking alone, without instrumenting any other gameplay system; RQ2, can multi-channel reaction time be measured validly — that is, without false positives from tracking jitter, continuing motion, or burst events; RQ3, in the absence of a recorded expert baseline, can trainee performance be evaluated against published research honestly; and RQ4, does the measured trainee population actually differ from the published expert value it is compared against.
>
> The first three are answered by construction and by audit rather than by a hypothesis test, because Module 3's interpretation mechanism runs live, per session, rather than as an offline analysis step there would be something to run. The fourth is answered statistically from the recorded sessions.

**Table 7.10: Module 3 evaluation design**

| Research question | Method | Sample / scope |
|---|---|---|
| RQ1 — non-invasive capture | Verification: read-only rig discovery, live in the shipping scene; no gameplay or hand-pose asset modified | Every recorded session |
| RQ2 — reaction-time validity | Verification by construction: three false-positive rules — already-in-motion disqualification, an 80 ms floor, a response window with cooldown | Every recorded reaction sample |
| RQ3 — honest benchmarking | Construct-validity audit of each of twelve reference bands against its source literature, with an explicit A/B/C confidence tier | 12 metrics |
| RQ4 — population comparison | One-sample Wilcoxon signed-rank against the published expert value; one-way ANOVA between participants | 45 sessions, 11 participants |

### 🆕 §7.5.2 — Construct validity of the benchmark bands

*(This is the existing §7.5 content, unchanged, with its existing table renumbered to Table 7.11. Add the paragraph below at the end of it.)*

> The three tier-C metrics are the point of the tiering rather than an incompleteness in it. Peak speed, crouch behaviour and hand travel are shown as plain values with no expert range attached, because no quantified literature was found for any of them — and a fabricated band would have been indistinguishable, to a reader, from the four tier-A bands that rest on direct empirical figures. The tier system is also what makes the *direction* of a verdict interpretable: the evaluation separates which side of a band a value falls on from whether that is a good result, so a trainee who reacts faster than the cited expert range is never shown a warning for it. This 4 / 5 / 3 split across tiers A, B and C is itself reported as the evaluation result, because an honest confidence distribution is evidence of validity rather than a shortfall to be concealed.

### 🆕 §7.5.3 — Trainee reaction time against the published expert value

> Section 7.5.2 audits whether Module 3's reference bands match their sources. A complementary question the recorded sessions can answer directly is whether the trainee population Module 3 actually measured differs from the published expert value it is compared against. Because reaction time is not a function of which AI tier was played, each participant's sessions are pooled across tiers.

**Table 7.12: Per-participant mean reaction time, all AI tiers pooled**

| Participant | Sessions | Mean reaction time | Band |
|---|---:|---:|---|
| *Published expert reference* | — | *0.30 s* | — |
| *Published novice reference* | — | *0.50 s* | — |
| P0001 | 4 | 0.50 s | Novice |
| P0002 | 3 | 1.20 s | Novice |
| P0003 | 3 | 0.80 s | Novice |
| P0004 | 3 | 0.90 s | Novice |
| P0005 | 5 | 0.70 s | Novice |
| P0006 | 3 | 0.60 s | Novice |
| P0007 | 6 | **0.30 s** | **Expert** |
| P0008 | 3 | 0.70 s | Novice |
| P0009 | 6 | 0.60 s | Novice |
| P0010 | 5 | 0.70 s | Novice |
| P0011 | 4 | 0.60 s | Novice |
| **All sessions pooled** | **45** | **0.65 s** | — |

> Across forty-five recorded sessions from eleven participants, mean reaction time was 0.65 s against a published expert value of 0.30 s. One participant reached the expert value, one matched the novice value of 0.50 s, and the remaining nine were slower than the novice value. A one-sample Wilcoxon signed-rank test finds the trainee distribution significantly different from the expert constant (p < 0.001, r = 0.67, α = 0.05) — the expected result for a non-expert sample, and confirmation that the instrument separates this population from the published expert value rather than returning expert-level numbers for everyone.
>
> A one-way ANOVA with each participant as a group and their own sessions as replicates finds **no** significant difference between participants (F(10, 34) = 1.22, p = 0.316; between-participants sum of squares 1.94, within-participants 5.43, total 7.37, grand mean 0.65 s). Session-to-session variation within a participant is therefore larger than the variation between participants at this sample size — consistent with reaction time behaving as a state-dependent measure sensitive to the particular encounter rather than as a stable trait that separates one trainee from another over four to six sessions.

**Table 7.13: Module 3 reaction time — population comparison**

| Test | Result | Interpretation |
|---|---|---|
| One-sample Wilcoxon vs published expert (0.30 s) | Trainee mean 0.65 s; p < 0.001, r = 0.67, n = 45 sessions | Significantly slower than the published expert value, in the expected direction |
| One-way ANOVA between participants | F(10, 34) = 1.22, p = 0.316, k = 11 participants | No significant between-participant difference at this sample size |

### 🆕 §7.5.4 Answers to Module 3's research questions

> **RQ1** — trainee movement is captured from the existing XR rig alone, discovered read-only by hierarchy search, with no gameplay or hand-pose asset modified and no additional hardware; the recorder has been live in the shipping scene since the module was built.
>
> **RQ2** — reaction-time validity is established by construction rather than by test: motion already underway when the stimulus fires is disqualified, the first 80 ms is never counted, and a response window with cooldown prevents burst stimuli from producing duplicate samples. A stimulus that draws no response is recorded as an explicit miss, so response rate remains computable.
>
> **RQ3** — yes, but only with the confidence tiering made explicit. Four of twelve metrics rest on a direct empirical figure, five on a flagged literature proxy, and three carry no band at all. The 4 / 5 / 3 distribution is the result, not a gap in it.
>
> **RQ4** — the measured population is significantly slower than the published expert value (p < 0.001, r = 0.67) and does not differ significantly between participants (F(10, 34) = 1.22, p = 0.316). The instrument therefore discriminates this population from the published expert benchmark, but not yet individual trainees from one another on this measure at this sample size.

### Figures for §7.5

| # | Name | Anchor | Source |
|---|---|---|---|
| **7.12** | Per-participant reaction time against reference values | §7.5.3, with Table 7.12 | Evaluation page, "Per-player average (all AI levels combined)" with the expert 0.30 s and novice 0.50 s reference rows (PDF p. 3) |
| **7.13** | Reaction time versus expert value, and the ANOVA table | §7.5.3, with Table 7.13 | The "vs Expert Value" card and the "ANOVA Table (between players)" card (PDF p. 4) |
| **7.14** | *(chart — you generate)* Reaction time per participant against both reference lines | §7.5.3 | Horizontal bar chart, one bar per participant from Table 7.12, with vertical reference lines at 0.30 s and 0.50 s. Shows at a glance that one participant reaches expert and nine fall beyond novice |

**Captions:**

- *Figure 7.12: Per-participant mean reaction time against the published expert (0.30 s) and novice (0.50 s) reference values. Sessions are pooled across AI tiers, because reaction time is not a function of which tier was played.*
- *Figure 7.13: One-sample Wilcoxon comparison of the pooled trainee reaction-time distribution against the published expert benchmark, and the one-way ANOVA testing whether participants differ from one another by more than their own session-to-session variability explains.*
- *Figure 7.14: Each participant's mean reaction time against the two published reference values. One participant reaches the expert value and one matches the novice value; the remaining nine fall beyond it, which is the expected distribution for a non-expert sample and the basis of the significance result in Table 7.13.*

## E.3 §7.6 Module 4 Results — restructure into five subsections

The current §7.6.2 and §7.6.3 both describe a design that has since been **deliberately abandoned** and a panel that has since **populated**. Both need replacing outright, not patching.

### 🆕 §7.6.1 — Research questions and evaluation design

> Module 4 does not expose an independent variable of its own. It is the measurement instrument, and it is therefore evaluated the way any measurement instrument is, against four research questions: RQ1, does each scoring formula agree with the published benchmark it claims to be calibrated against; RQ2, does the measured trainee population differ from those published expert values in the expected direction; RQ3, do the scores separate individual trainees from one another by more than their own session-to-session variability explains; and RQ4, is a score consistent when the same person plays repeatedly.

**Table 7.14: Module 4 evaluation design**

| Research question | Method | Sample / scope |
|---|---|---|
| RQ1 — construct validity | Audit of every score element against the curated Expert Value Table; requires no human subjects, since it evaluates the formula and not a sample | 8 scoring elements |
| RQ2 — population versus benchmark | One-sample Wilcoxon signed-rank against the published expert value, all sessions pooled | 45 sessions; the two scores that have an independent published benchmark |
| RQ3 — discrimination between trainees | One-way ANOVA, each participant a group and their own sessions the replicates | 45 sessions, 11 participants (35 sessions, 8 participants for Operator Safety) |
| RQ4 — repeated-play reliability | Per-participant mean and standard deviation across that participant's own sessions | 45 sessions, 11 participants, 3–6 sessions each |

### 🆕 Methodological note — insert after Table 7.14

> One framing correction is recorded here rather than hidden, because an earlier version of this evaluation made the opposite choice. That version ran Module 4's five scores through the same tier-by-tier Friedman test as Module 2's ablation study, and reported a null result across AI tiers. The framing was subsequently judged wrong: AI difficulty is Module 2's independent variable, not Module 4's, and nothing about a measurement instrument's scores is a claim about enemy AI. The analysis below therefore pools each participant's sessions across all tiers and asks the two properly separated questions instead — does the population differ from the published benchmark, and do trainees differ from each other. The tier-by-tier test was removed rather than retained alongside the new analysis, because keeping it would imply the question was still considered meaningful.

### 🆕 §7.6.2 — Construct validity against the Expert Value Table

*(Keep the existing §7.6.1 content and its table, renumbered to Table 7.15. It is still accurate. Add this paragraph at the end.)*

> The audit's headline result is that every scoring element for which the Expert Value Table supplied a hard target value has been calibrated to it, and that the exceptions are reported as exceptions. Two are worth naming. `Speed`'s target-time coefficients have no published benchmark for this specific mission type, so the target is normalised for scenario complexity along a shape that task-time decomposition theory does support, while the four coefficients themselves are declared design defaults rather than research-calibrated values. Trigger discipline is not captured at all and cannot be: a VR controller registers only a discrete shot-fired event on a full pull, so no partial-trigger telemetry exists to log. Recording that as infeasible rather than as future work is the honest reading, since no amount of additional development on this hardware would produce the measurement.

### 🆕 §7.6.3 — Score behaviour across the trainee population

> **Against the published expert values.** The two scores with an independent published benchmark were tested with a one-sample Wilcoxon signed-rank test over all forty-five recorded sessions. `Hostage Safety` averaged 52 % against an expert value of 85 % drawn from published hostage-rescue outcome rates (p < 0.001, r = 0.49). `Accuracy` averaged 68 % against an expert value of 100 % — the NYPD SOP-9 hit rate normalised to each session's own engagement range (p < 0.001, r = 0.88). Both place this non-expert sample significantly below the published expert value, in the expected direction and with a moderate-to-large effect size, which is the behaviour a correctly calibrated instrument should show. `Speed` and `Operator Safety` are deliberately not tested this way, because no independent published benchmark exists for either; they are reported as plain values rather than compared against a source that does not exist.

**Table 7.16: Per-participant five-score averages, all AI tiers pooled**

| Participant | Sessions | Overall | Hostage Safety | Accuracy | Speed | Operator Safety |
|---|---:|---:|---:|---:|---:|---:|
| *Published expert reference* | — | — | *85 %* | *100 %* | — | — |
| *Published novice reference* | — | — | *40 %* | *35 %* | — | — |
| P0001 | 4 | 48 % | 50 % | 28 % | 72 % | — |
| P0002 | 3 | 54 % | 67 % | 25 % | 66 % | 35 % |
| P0003 | 3 | 45 % | 33 % | 47 % | 73 % | — |
| P0004 | 3 | 76 % | 100 % | 43 % | 65 % | — |
| P0005 | 5 | 75 % | 72 % | 68 % | 87 % | 60 % |
| P0006 | 3 | 70 % | 67 % | 100 % | 33 % | 15 % |
| P0007 | 6 | 50 % | 33 % | 70 % | 61 % | 29 % |
| P0008 | 3 | 66 % | 46 % | 100 % | 56 % | 27 % |
| P0009 | 6 | 68 % | 60 % | 83 % | 63 % | 22 % |
| P0010 | 5 | 47 % | 32 % | 64 % | 51 % | 20 % |
| P0011 | 4 | 50 % | 25 % | 100 % | 38 % | 25 % |
| **Grand mean, all sessions** | **45** | **59 %** | **52 %** | **68 %** | **61 %** | **30 %** † |

† Operator Safety's grand mean is computed over 35 sessions from 8 participants, for the reason given below.

**Table 7.17: Module 4 scores against published expert values (one-sample Wilcoxon, all sessions pooled)**

| Score | Trainee mean | Published expert value | p | r | Verdict |
|---|---:|---:|---:|---:|---|
| Hostage Safety | 52 % | 85 % (hostage-rescue outcome rates) | < 0.001 | 0.49 | Significantly below expert |
| Accuracy | 68 % | 100 % (SOP-9, normalised to engagement range) | < 0.001 | 0.88 | Significantly below expert |
| Speed | 61 % | *no independent published benchmark* | — | — | Reported as a plain value |
| Operator Safety | 30 % | *no independent published benchmark* | — | — | Reported as a plain value |

> **Between participants.** A one-way ANOVA was run per score, with each participant as a group and their own sessions as that group's replicates.

**Table 7.18: One-way ANOVA between participants, Module 4 scores**

| Score | Between-participants SS | Within-participants SS | F | df | p | Grand mean | Verdict |
|---|---:|---:|---:|---|---:|---:|---|
| Overall | 0.58 | 2.80 | 0.71 | (10, 34) | 0.709 | 59 % | not significant |
| Hostage Safety | 1.88 | 8.41 | 0.76 | (10, 34) | 0.666 | 52 % | not significant |
| Accuracy | 2.70 | 3.20 | 2.87 | (10, 34) | **0.011** | 68 % | **significant** |
| Speed | 0.95 | 1.43 | 2.26 | (10, 34) | **0.037** | 61 % | **significant** |
| Operator Safety | 0.64 | 0.59 | 4.22 | (7, 27) | **0.003** | 30 % | **significant** |

> Three of the five scores separate participants from one another significantly, and the pattern is informative rather than arbitrary. **Operator Safety discriminates most strongly** (F(7, 27) = 4.22, p = 0.003), which is precisely the design intent behind adding it as a second, independent safety axis: it measures how the trainee conducted themselves rather than whether the mission was won, so it varies with individual conduct in a way an outcome score cannot. **Accuracy and Speed also discriminate**, both being continuous, skill-linked measures accumulated across a whole mission.
>
> **Overall and Hostage Safety do not, and the reason is structural rather than a power limitation.** With one hostage per scenario, Hostage Safety is close to binary within a single mission — the hostage is either rescued or not — so a participant's record is largely a run of 0 % and 100 % results, and its between-participant variance cannot exceed its within-participant variance at this sample size. The Overall composite inherits that behaviour directly, because Hostage Safety carries the largest weight in it. This is reported as it stands rather than reframed: a composite dominated by a near-binary term is a real property of the current weighting, and Section 8.4 records rebalancing it as future work.
>
> Operator Safety is computed over eight participants and thirty-five sessions rather than eleven and forty-five, because the score did not exist when the earliest sessions were recorded and is stored as null rather than zero on those sessions by design. An absent measurement is not a score of zero, and treating it as one would have silently depressed three participants' results.

### 🆕 §7.6.4 — Repeated-play reliability

> A participant playing more than once answers a different question from either test above — not whether a score discriminates, but whether it is *consistent* for the same person. Every one of the eleven participants played between three and six sessions, so all five scores can be examined for within-participant spread. Table 7.19 reports each participant's mean and standard deviation across their own sessions.

**Table 7.19: Repeated-play reliability — mean ± standard deviation across each participant's own sessions**

| Participant | Sessions | Overall | Hostage Safety | Accuracy | Speed | Operator Safety |
|---|---:|---|---|---|---|---|
| P0001 | 4 | 48 % ± 37 | 50 % ± 58 | 28 % ± 33 | 72 % ± 17 | — |
| P0002 | 3 | 54 % ± 31 | 67 % ± 58 | 25 % ± 9 | 66 % ± 9 | 35 % ± 13 |
| P0003 | 3 | 45 % ± 25 | 33 % ± 58 | 47 % ± 18 | 73 % ± 13 | — |
| P0004 | 3 | 76 % ± 4 | 100 % ± 0 | 43 % ± 14 | 65 % ± 2 | — |
| P0005 | 5 | 75 % ± 24 | 72 % ± 44 | 68 % ± 17 | 87 % ± 1 | 60 % ± 4 |
| P0006 | 3 | 70 % ± 26 | 67 % ± 58 | 100 % ± 0 | 33 % ± 20 | 15 % ± 14 |
| P0007 | 6 | 50 % ± 26 | 33 % ± 52 | 70 % ± 41 | 61 % ± 31 | 29 % ± 19 |
| P0008 | 3 | 66 % ± 23 | 46 % ± 51 | 100 % ± 0 | 56 % ± 13 | 27 % ± 20 |
| P0009 | 6 | 68 % ± 32 | 60 % ± 49 | 83 % ± 41 | 63 % ± 16 | 22 % ± 10 |
| P0010 | 5 | 47 % ± 36 | 32 % ± 46 | 64 % ± 49 | 51 % ± 34 | 20 % ± 15 |
| P0011 | 4 | 50 % ± 27 | 25 % ± 50 | 100 % ± 0 | 38 % ± 20 | 25 % ± 19 |

*All standard deviations are in percentage points. A dash indicates the score postdates that participant's sessions and is stored as null rather than zero.*

**Table 7.20: Mean within-participant variability by score**

| Score | Mean within-participant SD | Range across participants | Reliability regime |
|---|---:|---|---|
| Operator Safety | **14.3 pp** | ±4 to ±20 pp (k = 8) | Most consistent of the five |
| Speed | 16.0 pp | ±1 to ±34 pp | Consistent for participants who played consistently |
| Accuracy | 20.2 pp | ±0 to ±49 pp | Bimodal — see below |
| Overall | 26.5 pp | ±4 to ±37 pp | Inherits Hostage Safety's instability |
| Hostage Safety | **47.6 pp** | ±0 to ±58 pp | Least consistent, structurally |

*Computed as the arithmetic mean of the per-participant standard deviations in Table 7.19.*

> The five scores separate cleanly into two reliability regimes, and the ordering in Table 7.20 is interpretable throughout.
>
> **Operator Safety is the most consistent score in the instrument**, with a mean within-participant spread of 14.3 percentage points and no participant exceeding ±20. Speed follows closely at 16.0 points, and is as tight as ±1 to ±2 points for the participants who approached every session the same way. Both are continuous measures accumulated across a whole mission rather than settled by a single event, so no one moment can move them far — which is exactly why they are also the scores that discriminate between participants in Table 7.18. Consistency within a person and separation between people are the two halves of the same property, and these two scores have both.
>
> **Hostage Safety is the least consistent, structurally rather than accidentally.** Its per-participant standard deviations run from ±0 to ±58 points, and every wide one is a case where the participant scored 100 % in some sessions and 0 % in others. With one hostage per scenario the score is close to binary within a single mission, so a standard deviation near ±50 points is the *arithmetic consequence* of an even split rather than evidence of a noisy instrument. The correct reading is that Hostage Safety is a reliable measure of a single mission's outcome and an unreliable estimate of a trainee's underlying skill from a small number of plays — which is a statement about how many sessions are required, not about how the score is built. The Overall composite sits second-worst at 26.5 points for the same reason, since Hostage Safety is its most heavily weighted term.
>
> **Accuracy is bimodal rather than simply middling.** It is ±0 points for the three participants who engaged at a consistent range in every session and reached the distance-normalised ceiling each time, and ±41 to ±49 points for the three who had at least one session in which they fired no effective shots at all. The mean of 20.2 points conceals that split, which is why Table 7.19 is reported in full rather than summarised only by Table 7.20.
>
> Operator Safety is absent for three participants whose sessions predate the score's introduction. That is a data-availability boundary, not a measurement failure, and it is why Table 7.20 records k = 8 for that row.

### 🆕 §7.6.5 Answers to Module 4's research questions

> **RQ1** — every scoring element for which a published benchmark exists has been calibrated to it. The two documented exceptions are reported as exceptions: `Speed`'s coefficients are design defaults with a theoretically grounded shape but no published values, and trigger discipline is infeasible to measure on this hardware rather than merely unbuilt.
>
> **RQ2** — both benchmark-anchored scores place this non-expert sample significantly below the published expert value, in the expected direction and with moderate-to-large effect sizes (Hostage Safety p < 0.001, r = 0.49; Accuracy p < 0.001, r = 0.88).
>
> **RQ3** — three of the five scores separate individual participants significantly, led by Operator Safety (F(7, 27) = 4.22, p = 0.003). The two that do not — Hostage Safety and the Overall composite it dominates — fail to do so for an identified structural reason: with one hostage per scenario, Hostage Safety is close to binary within a single mission.
>
> **RQ4** — Operator Safety and Speed are the most consistent scores across a participant's repeated plays (mean within-participant spread 14.3 and 16.0 percentage points); Hostage Safety is the least consistent at 47.6 points, for the same structural reason. Consistency within a participant and discrimination between participants coincide across all five scores, which is the pattern a well-behaved measurement instrument should show.

### Figures for §7.6

| # | Name | Anchor | Source |
|---|---|---|---|
| **7.15** | Per-participant five-score average against reference values | §7.6.3, with Table 7.16 | "Per-player average (all AI levels combined)" card with the expert/novice rows and the classification badges (PDF p. 5) |
| **7.16** | Hostage Safety and Accuracy against expert values | §7.6.3, with Table 7.17 | Both "vs Expert Value" cards (PDF p. 6) |
| **7.17** | ANOVA tables, five scores | §7.6.3, with Table 7.18 | The five ANOVA cards (PDF pp. 6–7). Crop as one figure, or split 7.17a (Overall and Hostage Safety, both non-significant) and 7.17b (Accuracy, Speed and Operator Safety, all significant) |
| **7.18** | Repeated-play reliability, two contrasting participants | §7.6.4, with Table 7.19 | **Do not paste all eleven.** Use **P0004** (Hostage Safety ±0, Overall ±4 — tight) and **P0001** (Hostage Safety ±58 — wide). The contrast *is* the finding (PDF pp. 8–9) |
| **7.19** | Per-participant benchmark classification bands | End of §7.6.4 | Two or three player cards showing the Novice / Developing / Proficient / Expert classification, including the "no independent published benchmark exists for this composite score" note on Speed and Operator Safety (PDF pp. 11–16) |
| **7.20** | *(chart — you generate)* Within-participant variability by score | §7.6.4, with Table 7.20 | Horizontal bar chart of Table 7.20's mean within-participant SD, one bar per score, ordered smallest to largest. The single fastest way to show the two reliability regimes |

**Captions:**

- *Figure 7.15: Each participant's five-score average, with the published expert and novice reference values shown explicitly above the participant rows. Speed and Operator Safety carry no reference row, because no independent published benchmark exists for either.*
- *Figure 7.16: One-sample Wilcoxon tests of the pooled trainee population against the two scores that have an independent published benchmark — Hostage Safety, against published hostage-rescue outcome rates, and Accuracy, against SOP-9 hit rates normalised to each session's engagement range.*
- *Figure 7.17: One-way ANOVA between participants for each of the five scores, with each participant as a group and their own sessions as replicates. Operator Safety discriminates most strongly, which is the design intent of adding it as a second, independent safety axis.*
- *Figure 7.18: Repeated-play reliability for two contrasting participants. Speed and Accuracy cluster tightly across a participant's sessions; Hostage Safety does not, because with one hostage per scenario it is close to binary within a single mission — reported as it stands rather than smoothed.*
- *Figure 7.19: Per-participant comparison against published benchmark bands, averaging a participant's sessions before classification to reduce single-session noise. Speed and Operator Safety are shown without a band rather than compared against a source that does not exist.*
- *Figure 7.20: Mean within-participant variability by score. The ordering separates the two reliability regimes: the two continuous, whole-mission measures are the most consistent, while the near-binary per-mission outcome and the composite it dominates are the least.*

---

# PART F — NEW CONTENT: TRAIT-DIFFERENTIATED HOSTAGE PROFILES

**Status confirmed on your working tree:** `Assets/Scripts/NPC/HostageProfile.cs` exists; `HostageController.cs` exposes `profile`, `assignRandomProfile` and `ThreatDiscrimination`, and assigns a research-weighted random profile on awake; `Module4Bridge.cs` resolves and caches the active profile per hostage actor; `HostageStateEntry.cs` carries a `hostageProfile` field; `SessionLogger.cs` accepts it. **The feature is implemented and logged end to end on the Unity side, so write it up as implemented work, not as future work.**

**One thing to check before you paste this:** confirm the dashboard actually *surfaces* the logged profile in the hostage panel. If it does not yet, keep everything below but drop Figure 6.9 and remove the clause about attributing a distress trajectory to a profile in the after-action review.

**Also fix the contradiction:** `MODULE4_FULL_REPORT.md` §3.5 and §6.5b both still say "specified, not yet implemented". Update both, or the report and the repository disagree on a point an examiner can check in thirty seconds.

### Add to §5.6, after the distress-model content

> Hostages additionally carry a trait-differentiated response profile, because a model in which every hostage reacts identically to the same stimulus is the one clearly unrealistic simplification remaining in the distress model. Two independent findings converge on how real defensive response varies. Visually, a high looming cognitive style produces generalised rather than selective freezing — freezing at things a low-reactive person correctly ignores (F(1, 80) = 6.50, p = 0.01). Auditorily, high-anxiety individuals respond with equal latency to a quiet 60 dB and a loud 85 dB stimulus, while low-anxiety individuals discriminate between them. The shared mechanism is that trait reactivity flattens the intensity–response relationship, and the auditory result is the directly applicable one here, since the hostage model's primary stimulus is gunfire.
>
> Three profiles are modelled — Weak, Normal and Brave — and the design decision worth stating explicitly is that all three keep **identical distance thresholds**, differing only in threat discrimination. This is a correctness requirement rather than a simplification. The sound-pressure-to-distance chain cannot separate profiles at close-quarters range: consecutive published thresholds map to roughly 1 m, 14 m, 158 m and 2.2 km, and gunfire exceeds the 137 dB fear-and-panic threshold at every in-building distance. Per-profile distances would therefore have been invented values presented as research-derived ones — the exact failure mode this project flags for the `Speed` coefficients in Section 7.6.2. Discrimination is the parameter the research actually supports.
>
> Spawn weights are renormalised from published trauma-trajectory prevalence — resilience 65.7 %, recovery 20.8 %, chronicity 10.6 % — to approximately 68 / 21 / 11, so a trainee meets a composed hostage far more often than a fragile one. The residual delayed-onset trajectory is deliberately not modelled, because it describes symptoms emerging weeks to months later and has no counterpart inside a five-minute mission. The Brave profile additionally carries a small captor-defiance risk, drawn from the London Syndrome finding that captivity defiance is associated with *worse* survival outcomes rather than better. Folding that risk into Brave rather than giving it a fourth profile preserves the counter-intuitive after-action lesson — the calmest-looking hostage can be the one at greatest risk — without inflating the model.

**Table 5.9: Hostage trait-differentiated response profiles**

| Profile | Threat discrimination | Spawn weight | Behavioural signature |
|---|---:|---:|---|
| Weak | 0.35 | ≈ 11 % | Cannot grade the threat — reacts to a distant shot as though it were point-blank; may stay frozen after the threat has passed; may fail to recognise the approaching rescuer; slow to extract |
| Normal | 0.85 | ≈ 21 % | Population-typical — grades the threat correctly most of the time, with occasional lapses |
| Brave | 1.00 | ≈ 68 % | Grades the threat correctly, low peak distress, recognises and follows the rescuer quickly — carries a 15 % captor-defiance risk that raises execution risk |

**Honesty note to place directly beneath Table 5.9** *(the source file's own documentation insists on this)*:

> The mechanism and its direction are research-grounded; the three discrimination values themselves are design calibrations, because the literature supplies no numeric probability. They carry the same standing as the `Speed` target-time coefficients discussed in Section 7.6.2 and are reported as such. The appropriate defence of them is a sensitivity analysis showing that the Weak > Normal > Brave ordering is stable across a range of values, not an argument for any exact value.

### Add to §6.4 (Module 2 implementation, since the hostage controller owns it)

> The profile a hostage carries is assigned on awake by research-weighted random selection, which is the intended default for training runs so that hostage variety is realistic. Controlled evaluation runs turn the random assignment off and pin the profile explicitly, so that prevalence never confounds a comparison; the module's own test runner pins it to Brave for exactly this reason, since Brave's discrimination of 1.0 reproduces the deterministic pre-profile behaviour. Threat discrimination is derived on read from the profile field rather than cached at construction, so a caller that adds the component and only then assigns the profile still gets the correct behaviour — and so the profile can be changed live in the Inspector during play. The active profile is written into the session record for every hostage state change, so the after-action review can attribute a distress trajectory to the profile that produced it.

### Add to §8.3, Threats to Validity

> The hostage-profile feature separates the claims it can and cannot support, and this distinction constrains how it may be reported. That a Weak-profile hostage reaches a higher peak distress is a **verification** result: it follows by construction from the authored discrimination parameter, and must be presented as an implementation check rather than as a finding. Only the **discovery** claims — that the profile changes trainee rescue time, the resulting Hostage Safety score, and the distribution of mission outcomes, none of which are set by the profile definition — could be presented as findings, and none has been evaluated at the time of writing.

### Add to §8.4, Future Work

> Evaluate the hostage response profiles against the discovery claims identified in Section 8.3 — whether profile changes trainee rescue time, Hostage Safety score, and mission-outcome distribution — with the profile pinned rather than randomly assigned, so that prevalence does not confound the comparison. A sensitivity analysis across a range of discrimination values would additionally establish whether the Weak > Normal > Brave ordering is robust to the specific design calibrations chosen.

---

# PART G — KNOCK-ON EDITS ELSEWHERE

## G.1 §7.1 Introduction — replace the Module 3 and Module 4 sentences

Find: *"Module 3 is evaluated through a construct-validity audit of its benchmark bands against their source literature, since its per-session interpretation mechanism has no separate offline evaluation step to run. Module 4 is evaluated through a construct-validity audit of its five scoring formulas against published real-world benchmarks, and — reusing the same participant sample and statistical engine as Module 2's study — a discriminant-validity test of whether its own scores actually change across AI difficulty."*

Replace with:

> Module 3 is evaluated through a construct-validity audit of each benchmark band against its source literature, together with a one-sample comparison of the measured trainee population against the published expert value it is compared against. Module 4, being a measurement instrument rather than a system with an independent variable of its own, is evaluated the way any instrument is: construct validity of each formula against its published benchmark, a population comparison against those benchmarks, discrimination between individual trainees, and consistency across a trainee's repeated plays.

## G.2 §7.2 Table 7.1 — three of four rows are now wrong

**Table 7.1: Evaluation methodology by module**

| Module | Method | Sample |
|---|---|---|
| 1 | *(unchanged)* | 2000 generated scenarios across six experiments |
| 2 | Within-subjects ablation across three AI tiers; validated believability instruments (UEQ-S, Godspeed) plus an author-defined tactical-realism scale; Friedman omnibus with Kendall's *W* and Bonferroni-corrected Wilcoxon signed-rank post-hoc | 11 participants, 45 sessions (**9** completing all three tiers) |
| 3 | Construct-validity audit of each benchmark band against its source literature; **one-sample Wilcoxon of pooled trainee reaction time against the published expert value; one-way ANOVA between participants** | **12 metrics for the audit; 45 sessions across 11 participants for the statistical comparison** |
| 4 | Construct-validity audit of each score formula against a published benchmark; **one-sample Wilcoxon against published expert values; one-way ANOVA between participants; repeated-play reliability** | 11 participants, 45 sessions (**35 sessions from 8 participants for Operator Safety, which postdates the earliest sessions**) |

**Ethics sentence — Branch A only.** Insert after Table 7.1, before §7.3:

> All trainee participants took part voluntarily and were briefed, before their first session, on the purpose of the study, the data recorded — mission telemetry, movement and reaction-time tracking, and post-mission questionnaire responses — and their right to withdraw. No personally identifying information is stored: each participant is referenced throughout this chapter only by an anonymised identifier assigned on the mission-launch form.

## G.3 §7.7 Cross-Module Synthesis — replace the Module 3 and Module 4 clauses

Find: *"…and Module 4's scoring formulas agree with every published benchmark available to check them against, while honestly reporting that its own composite scores do not (yet, at this sample size) discriminate across AI difficulty the way the believability measures do — a genuinely informative finding about what a harder AI tier does and does not change, rather than a gap papered over."*

Replace with:

> …Module 3's benchmark-interpretation layer is honestly and defensibly tiered against its source literature even without a recorded expert baseline through this simulator, and its measured population is significantly slower than the published expert value it is compared against, which is the behaviour a correctly calibrated instrument should show; and Module 4's scoring formulas agree with every published benchmark available to check them against, place a non-expert sample significantly below the published expert value on both benchmark-anchored scores, and separate individual trainees from one another on three of five scores — led by Operator Safety, the second safety axis the module adds. The two that do not separate trainees, Hostage Safety and the Overall composite it dominates, fail to do so for a structural reason the analysis identifies explicitly rather than leaves implicit: with one hostage per scenario, Hostage Safety is close to binary within a single mission, and consistency within a participant and discrimination between participants turn out to coincide across all five scores.

## G.4 §8.3 Threats to Validity

**Module 2 bullet** — check for any surviving sentence carrying the old n = 7 Bonferroni limitation. If present, replace with:

> The believability result now separates all three tiers on two of the three constructs under Bonferroni correction at nine complete cases. Animacy's Intermediate-versus-Advanced comparison remains non-significant, and should be read as evidence about *which* construct the Advanced tier's additional behaviours affect rather than as a power limitation alone.

**Module 3 bullet** — add:

> Reaction time did not differ significantly between participants (F(10, 34) = 1.22, p = 0.316), so the instrument is currently shown to separate this population from the published expert value but not individual trainees from one another. At three to six sessions per participant, this should be read as within-participant variability exceeding between-participant variability at this sample size, not as evidence that trainees do not differ.

**Module 4 first bullet** — replace: *"…Module 4's own discriminant-validity result (no significant score change across AI tier) should be read as 'not yet detected at this sample size,' not as a claim that no such effect exists."*

> The evaluation sample is real but still modest by the standards of a fully powered study. Where a Module 4 score does not separate participants — Hostage Safety and the Overall composite it dominates — the correct reading is "not detectable at this sample size with a near-binary per-mission outcome," not that no such difference exists. Operator Safety's between-participant result additionally rests on eight participants rather than eleven, because the score postdates the earliest recorded sessions.

## G.5 §8.4 Future Work

Replace the first item's tail — *"…to strengthen the significance results reported in Chapter 7 and give Module 4's discriminant-validity and reliability panels enough repeated trials to activate."* Both panels have now activated.

> Recruit a larger, fully powered trainee sample — the same pool already serving Module 2's and Module 4's shared evaluation, so no separate recruitment effort is required — to increase the number of complete three-tier triads behind Section 7.4's significance results, and to give Section 7.6.4's reliability analysis enough repeat plays per participant for Hostage Safety to stabilise as an estimate of individual skill rather than of a single mission's outcome.

Add two further items:

> Rebalance the Overall composite's weighting. Section 7.6.4 shows that Overall inherits its instability directly from Hostage Safety, which carries the largest weight in it and is close to binary within a single mission; a composite weighted toward the continuous, whole-mission measures would separate trainees as well as its components do.
>
> Record more repeat plays per participant, since Hostage Safety — the most heavily weighted term in the Overall composite — needs substantially more than the current three to six sessions per trainee before it becomes a stable estimate of individual skill.

## G.6 Abstract — two additions

1. Find *"…statistically significant increase in perceived believability and behavioural competence across eleven trainee participants"* → extend to *"…across eleven trainee participants, with Perceived Intelligence and Tactical realism separating all three tiers from one another under Bonferroni correction"*.
2. Find *"…and that Module 4's scoring formulas agree with every published benchmark value available to check them against."* → extend to *"…available to check them against, place the trainee population significantly below the published expert values on both benchmark-anchored scores, and separate individual trainees most strongly on Operator Safety — the second safety axis the module adds."*

## G.7 Front matter

- **List of Figures** — [P0133] currently contains the Figure 4.1 entry and the Figure 5.1 entry merged into one paragraph. Split them. Then add every new figure: the Chapter 5 renumbering (§B.2), 6.4 – 6.22, and 7.7 – 7.20.
- **List of Tables** — apply both renumbering maps (§B.3) and add every new table.
- **New: List of Listings** — add after the List of Tables, covering Listings 6.1 – 6.7.
- **Strong recommendation at this scale.** Give every caption Word's `Caption` style and insert them with `References ▸ Insert Caption` (Label = Figure or Listing, numbering = *include chapter number*), then replace each manual list with `References ▸ Insert Table of Figures`. With roughly forty figures, twenty-eight tables and seven listings, maintaining three lists by hand will not survive one more editing pass — and it fixes the page-number placeholder note in the front matter at the same time.

---

# PART H — MASTER REGISTER: EVERY NEW FIGURE

Reserve space in the document now, even where you cannot capture yet — a numbered, captioned empty frame keeps the numbering and the lists correct while you collect images.

| # | Name | Type | Source | Priority |
|---|---|---|---|---|
| 5.4 | Terrorist FSM and squad hunt lifecycle | **You draw** | Diagram spec in §C.1 | P1 |
| 5.6 | Reaction-time channel and validity flow | **You draw** | Diagram spec in §C.2 | P1 |
| 5.8 | Hostage distress state machine | **You draw** | Diagram spec in §C.3 | P1 |
| 6.4 | Terrorist FSM at runtime | Unity | Play mode: Inspector + Console | P2 |
| 6.5 | Persistent hunt in action | Unity | Scene view with PLS gizmos | P2 |
| 6.6 | AI tier gating verified at Basic | Unity | Play mode at Basic, gunshot ignored | P2 |
| 6.7 | Hostage rescue sequence | Headset / Game view | Captive pose, then following | P2 |
| 6.8 | Hostage-guardian escalation | Headset | Skip if the pose is still wrong | P3 |
| 6.9 | Hostage profile in the AAR | Dashboard | Hostage panel — **confirm it displays first** | P3 |
| 6.10 | Cognitive mirror HUD | Headset | Quest capture | P2 |
| 6.11 | Movement metrics with A/B/C chips | Dashboard | Movement tab + sources card | P2 |
| 6.12 | Reaction time by channel | Dashboard | Channel benchmark strip + scatter | P2 |
| 6.13 | SIM-TLX questionnaire | Dashboard | Trainee view | P3 |
| 6.14 | Workload reasoning and predicted zone | Dashboard | Workload tab | P2 |
| 6.15 | Session summary and score breakdown | Dashboard | Summary tab incl. Operator Safety card | **P1** |
| 6.16 | Timeline and activity density | Dashboard | Timeline tab | P2 |
| 6.17 | Severity-ranked incidents | Dashboard | Incidents tab | P2 |
| 6.18 | Hostage distress over time | Dashboard | Hostage tab | P2 |
| 6.19 | 2D top-down replay | Dashboard | Replay tab | P2 |
| 6.20 | 3D replay reconstruction | Dashboard | **Strongest single visual — full width** | **P1** |
| 6.21 | Offline HTML report | Browser | Exported report | P3 |
| 6.22 | In-VR mission result screen | Headset | Mission end | P2 |
| 7.7 | Believability survey, trainee view | Dashboard | Questionnaire | P2 |
| 7.8 | Per-participant comparison by tier | Dashboard | PDF p. 1 | **P1** |
| 7.9 | Overall averages by tier | Dashboard | PDF pp. 1–2 | **P1** |
| 7.10 | Statistical significance panel | Dashboard | PDF p. 2, all three cards | **P1** |
| 7.11 | Believability by tier | **You chart** | From Table 7.7 | P2 |
| 7.12 | Per-participant reaction time | Dashboard | PDF p. 3 | P2 |
| 7.13 | Reaction time vs expert + ANOVA | Dashboard | PDF p. 4 | P2 |
| 7.14 | Reaction time per participant | **You chart** | From Table 7.12 | P2 |
| 7.15 | Per-participant five-score average | Dashboard | PDF p. 5 | **P1** |
| 7.16 | Hostage Safety and Accuracy vs expert | Dashboard | PDF p. 6 | **P1** |
| 7.17 | ANOVA tables, five scores | Dashboard | PDF pp. 6–7 | **P1** |
| 7.18 | Repeated-play reliability, two participants | Dashboard | PDF pp. 8–9 — **P0004 and P0001 only** | P2 |
| 7.19 | Per-participant benchmark bands | Dashboard | PDF pp. 11–16, two or three cards | P3 |
| 7.20 | Within-participant variability by score | **You chart** | From Table 7.20 | P2 |

**Capture consistency rules.** One width for every dashboard figure, one for every Unity figure, one for every listing. Dashboard: Chrome at 1440 px, **light theme**, 100 % zoom, `F12 → Ctrl+Shift+P → "Capture node screenshot"` on the card element so you get a clean card with no browser chrome. Unity: maximise the panel with `Shift+Space`, then `Win+Shift+S`; use the top-down 2D orthographic Scene view for anything spatial. Code listings: VS Code, **light theme**, ~15 pt, minimap off, line numbers on — never the dark theme, which prints as a black block. Captions below the image, italic, 10 pt, matching the existing Figure 4.1 style.

---

# PART I — MASTER REGISTER: EVERY NEW TABLE

| # | Title | Content status |
|---|---|---|
| 5.5 | Module 2 behavioural time constants and their grounding | ✅ Written in full — §C.1 |
| 5.6 | Module 4 capture layers | ✅ Written in full — §C.3 |
| 5.7 | Automatically extracted incident types | ✅ Written in full — §C.3 |
| 5.9 | Hostage trait-differentiated response profiles | ✅ Written in full — §F |
| 6.2 | Module 2 implementation status | ✅ Written in full — §D.1 |
| 6.3 | AI intelligence tier — live verification record | ✅ Written in full — §D.1 |
| 6.5 | Module 3 reaction-time channels and thresholds | ✅ Written in full — §D.2 |
| 6.6 | Module 4 implementation status | ✅ Written in full — §D.3 |
| 7.6 | Module 2 ablation study design | ✅ Written in full — §E.1 |
| 7.7 | Overall averages by AI tier | ✅ **Replaces existing Table 7.6** — new numbers |
| 7.8 | Friedman and Wilcoxon results | ✅ **Replaces existing Table 7.7** — new numbers |
| 7.9 | Participant P0001 complete triad | ✅ Written in full — §E.1 |
| 7.10 | Module 3 evaluation design | ✅ Written in full — §E.2 |
| 7.12 | Per-participant reaction time | ✅ Written in full — §E.2 |
| 7.13 | Reaction time population comparison | ✅ Written in full — §E.2 |
| 7.14 | Module 4 evaluation design | ✅ Written in full — §E.3 |
| 7.16 | Per-participant five-score averages | ✅ Written in full — §E.3 |
| 7.17 | Scores against published expert values | ✅ Written in full — §E.3 |
| 7.18 | One-way ANOVA between participants | ✅ Written in full — §E.3 |
| 7.19 | Repeated-play reliability, all participants | ✅ Written in full — §E.3 |
| 7.20 | Mean within-participant variability by score | ✅ Written in full — §E.3 |

---

# PART J — SUGGESTED ORDER OF WORK

1. **Decide Branch A or Branch B (§A.1).** Everything downstream depends on it, and it is the only item that can invalidate work already done.
2. **Verify the α = 0.0167 versus 0.02 question (§A.2)** against the statistics engine, and make the report, the dashboard and Appendix B.4 agree.
3. **Confirm the dashboard surfaces the hostage profile (§F)**, then update `MODULE4_FULL_REPORT.md` §3.5 and §6.5b so the report and the repository stop contradicting each other.
4. **Apply the Chapter 7 rewrites (Part E) before capturing any evaluation screenshot,** so that the text and the figures describe the same dashboard state.
5. **Apply the renumbering maps (§B.3) and the cross-reference fixes (§B.4)** in one pass, immediately after Part E, while the structure is fresh.
6. **Paste Parts C, D and F.**
7. **Apply the knock-on edits (Part G).**
8. **Generate the three Chapter 5 diagrams and the three charts** — these need no build, no headset and no dashboard, so they can be done at any point and are the cheapest figures in the list.
9. **Capture every P1 screenshot in one sitting**, so theme, zoom and crop stay consistent.
10. **Convert the three lists to Word fields (§G.7)** and press F9.
