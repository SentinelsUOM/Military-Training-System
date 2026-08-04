# Module 2 — Full End‑to‑End Report
## Enemy / Defender NPC Artificial Intelligence and its Evaluation

**Project:** Sentinels — VR Counter‑Terrorism / Hostage‑Rescue Training Simulator
**Institution:** University of Moratuwa (final‑year project)
**Platform:** Unity 6.3 (Meta Quest 3, VR) + Next.js After‑Action‑Review (AAR) dashboard
**Scope of this document:** the complete record of Module 2 — the tactical "terrorist" AI and its evaluation system — covering design, every gap/problem/root‑cause/solution, the full implementation, the evaluation methodology actually built, the technologies used, and correctly formatted (IEEE) citations.

> **Purpose.** This is the master reference for Module 2. It is written so that a later pass (or a chat assistant) can generate the formal project‑report chapter from it without re‑deriving anything. It consolidates and supersedes the two earlier notes, `MODULE2_BEHAVIOR_RESEARCH.md` (doctrinal grounding of the behaviour) and `MODULE2_EVALUATION_METHODOLOGY.md` (the research survey of evaluation methods), and adds the as‑built implementation, the engineering narrative, and the statistical layer.

---

## Table of Contents
1. Scope and objectives
2. System context and technology stack
3. Behaviour design — the AI itself
4. Engineering narrative — gaps, problems, root causes, solutions
5. The three AI intelligence levels (the ablation independent variable)
6. Evaluation methodology (as built)
   6.7 Results (statistically significant, full-sample — updated 2026-08-02)
7. Data pipeline, integrity and reliability engineering
8. Dashboard implementation
9. Threats to validity and limitations
10. Future work
11. References (IEEE)
12. Appendices (questionnaire items, event types, score formulas, file map)

---

## 1. Scope and objectives

Module 2 owns the **hostile NPC behaviour** in the simulator: the terrorists that guard, patrol, detect, engage, coordinate, hunt, take cover, break morale, and hold a hostage at gunpoint — plus the **hostage NPC** that they guard and that the trainee must rescue. Module 2 also owns the **evaluation apparatus** that lets us claim, with evidence, that this AI is believable and behaves like a real hostile force.

**Objectives.**
1. **Behavioural realism** — model a *semi‑trained* hostile force (between paramilitary drill and irregular chaos) whose reactions to the trainee are doctrinally defensible, not arcade‑like.
2. **Measurable believability** — provide an evaluation that produces defensible, citable evidence that the AI is perceived as intelligent and life‑like, using validated instruments plus objective telemetry.
3. **A controllable independent variable** — expose the AI at three intelligence levels so the *same* scenario (same seed) can be replayed at each level, giving a clean within‑subjects **ablation** study.
4. **End‑to‑end data capture** — every play is tagged, uploaded, surveyed, aggregated, and statistically tested with no manual data handling.

---

## 2. System context and technology stack

| Layer | Technology | Role in Module 2 |
|---|---|---|
| Simulation | **Unity 6.3**, C#, IL2CPP (Quest build) | Runs the NPC AI, physics, animation, VR |
| Navigation | **`NavMeshAgent`** + runtime‑baked NavMesh | NPC movement, pathing, cover approach |
| Behaviour | Hand‑authored **finite‑state machine (FSM)** + coroutines | Per‑NPC decision logic |
| Coordination | Event bus (`EventManager`) + shared **`Squad`** blackboard | Squad‑level shared knowledge, orders |
| Animation | Mecanim **Humanoid** animator controllers, blend trees | Firing, movement, hostage poses |
| VR | OpenXR / XR Interaction Toolkit (MikeNspired XRI Starter Kit) | Head/hand tracking, weapons, damage |
| Telemetry | Module 4 `SessionLogger` → `SessionSummary` JSON | Records every mission event & metric |
| Transport | `UnityWebRequest` (gzip + retry + offline queue) | Uploads a finished session to the dashboard |
| Dashboard | **Next.js 14** (App Router), React client components | Mission form, AAR, surveys, results |
| Database | **MongoDB** via **Mongoose** | Stores every session, survey, and derived metric |
| Hosting | **Vercel** (dashboard) — shares one MongoDB with local dev | Portable capture from any PC/headset |
| Tooling | CoplayDev **MCP for Unity** (HTTP bridge 127.0.0.1:8080) | Live inspection / scripted verification |

**Assembly boundaries (a recurring design constraint).** Unity assembly‑definition files (`.asmdef`) partition the C# code. The gameplay/AI code (`TerroristController`, `SceneBuilder`, `EventManager`, `PlayerHealthHitbox`, `Module4Bridge`) lives in the default **`Assembly‑CSharp`**. Module 4 (`SessionLogger`, `PerformanceCalculator`, `SessionSummary`, `DashboardUploader`) lives in the auto‑referenced **`TeamSentinels.Module4`** asmdef. The XRI weapon/damage code (`NpcShooterRaycast`) lives in **`MikeNspiredXRIStarterKit.Runtime`**. The hard rule — *an asmdef cannot reference `Assembly‑CSharp`, but `Assembly‑CSharp` auto‑references auto‑referenced asmdefs* — dictated several design choices below (routing terrorist crossfire through the `IDamageable` interface; placing the evaluation context holder inside the Module 4 assembly; forwarding weapon telemetry through plain C# events).

---

## 3. Behaviour design — the AI itself

### 3.1 Architecture — a two‑layer model (individual FSM + squad coordination)

The design follows the most directly implementable published squad‑AI model: **F.E.A.R.'s two‑layer GOAP squad AI** [1]. We keep an FSM at the individual layer (simpler to author and audit than full GOAP) and add a squad coordination layer on top:

- **Individual layer** — each `TerroristController` runs its own FSM: *Idle → Alert → Investigate → Engage → (Cover/Retreat) → Down*, plus perception, aiming, and firing.
- **Squad coordination layer** — a shared **`Squad`** object per group holds the blackboard (last confirmed contact, hunt state, leader) and issues/monitors directives (converge, search, suppress), exactly as F.E.A.R.'s coordination layer "fills participant slots and monitors members until they fulfil the order or die" [1].
- **Communication** — all cross‑actor signals travel through an **`EventManager`** bus as `ScenarioEvent`s (e.g. `ShotFired`, `PlayerSeen`, `TerroristDown`). `Module4Bridge` mirrors every event into the telemetry log, which is what the whole evaluation is later computed from.

### 3.2 Perception
- **Line of sight (LOS)** with a confirmation delay: a sighting must be *sustained* (`targetConfirmTime`) before it commits the NPC to *Engage* (`TargetConfirmed`). Momentary glimpses raise `PlayerSeen`; sustained LOS raises `TargetConfirmed`; losing LOS for a confirmation window raises `PlayerLost`.
- **Hearing** — `ShotFired` is broadcast by `EventManager` as `GunshotHeard` to all NPCs in range, and `3+ ShotFired` within 2 s raises a `StressSpike` (used to panic hostages). This models suppressive fire's primary *psychological* effect [23].

### 3.3 Combat engagement
- On confirmed contact the NPC returns fire and takes the nearest cover **within ~3–6 s** (semi‑trained calibration of the doctrinal ~3 s standard [23]).
- Firing is delegated to `NpcShooterRaycast`: **burst fire** (`shotsPerBurst`, `burstPause`) rather than a metronome, a **5° inaccuracy cone** (`spreadDegrees`, deliberately loose so an enemy that never misses does not feel unfair), and **centre‑mass aiming** at the trainee's body collider (not the head/camera, which on a flat screen sits at floor height and caused a measured 20 % hit rate).
- **Fire/accuracy/damage settings are treated as fixed and were never altered during evaluation work** (a standing project constraint) — only *read‑only* instrumentation was added.

### 3.4 Squad coordination and shared contact (the doctrinal core)
Real forces convert a single member's sighting into **shared team knowledge** and **converge on the element in contact** rather than dispersing [23], [24]. Implemented as:
- The instant any defender confirms the trainee, its position is written to the **shared squad blackboard** (`ReportConfirmedContact` → `PointLastSeen`), which every squad member reads — it is **not** private to the sighter.
- A leader issues directives (`IssueDirectiveIfLeader` / `FollowActiveDirective`); nearby members converge; a small propagation delay models the "call" not being instantaneous.

### 3.5 Persistent hunt system — the P0 fix
The original behaviour ("see the intruder, lose him, walk back to post") is **doctrinally wrong** [22]: a sighting is a trigger to *actively hunt*, not to withdraw. The fix (in `Squad.cs`) is a **shared, persistent hunt**:
- `ReportConfirmedContact` / `BeginHunt` open a squad‑wide hunt anchored on the **Point‑Last‑Seen (PLS)**. The search area is a circle whose radius grows with elapsed time (`radius = subject‑speed × time`), collapsing back when a **fresh** sighting or gunshot resets the anchor [25].
- The hunt persists for a long, area‑based budget (`HuntBudgetSeconds ≈ 45 s`, `NotifySearcherExhausted`/`EndHunt`) rather than a few‑second give‑up timer, because doctrine gives **no fixed give‑up number** for a *lost* contact — so persistence is intentionally long and area‑based, not a short clock.
- Triggers that (re)start the hunt: player lost after confirmed contact, converging on a fresh gunshot, seeing an ally go down (`AllyDownSeen`), and a squad‑mate's death (`TerroristDown`).

### 3.6 Morale, cover and retreat
- A per‑defender / squad **morale** variable is driven by *leader‑alive, relative numbers, casualties‑over‑time, surprise* — not a fixed casualty count [22]. It **spikes on leader death** (paired with leader re‑election).
- Posture escalates `HoldAndShoot → CoverAndPeek` under mounting pressure (`AddEscalation`); a wounded NPC may **retreat** under cover rather than stroll off.

### 3.7 Patrol / idle / scan
- Idle modes: `Patrol`, `Wander`, `Static`. Idle NPCs **scan** their view (`StartIdleScan`) and have an anti‑wall safety net so a spawn rotation or waypoint tucked against masonry does not leave them facing a wall.

### 3.8 Hostage guardian and the leverage (escalation) ladder
A guard tethered to a fixed asset is **not directly covered by doctrine** (an honest, flagged derivation from fixed‑asset‑defence + fire‑discipline principles). The guardian holds position, watches the chokepoint, and does not abandon the hostage to reinforce. Under threat it climbs an **escalation ladder** grounded in real hostage‑crisis analysis — verbal threat → **warning shot** → **execution** — mirroring the Lindt Café siege, where the gunman fired a warning shot into a wall ~2 minutes before executing a hostage and the Coroner found that shot should itself have triggered an immediate assault. `NpcShooterRaycast.FireWarningShot()` and `FireExecutionShot()` provide a fully *audible* gunshot (muzzle flash + report) with no ballistics, so the trainee has a real cue to react to.

### 3.9 Hostage NPC behaviour
- **Initial pose** — kneeling captive (not a T‑pose); `Captive` / `CaptiveCower` animator states, driven by a `Rescued` parameter.
- **Following** — on rescue the hostage stands (`StandUp`) and walks with the trainee to the safe zone; **healthy** hostages use `hostageWork‑NotInjured.fbx`, **injured** use `injureddwalking.fbx`; a `walkingStop.fbx` idle prevents the T‑pose when stopped or freed.
- **Damage & fratricide** — the hostage has a real `CapsuleCollider` and `TakeHit(damage, hitPoint, byTrainee)`; **both the player and the terrorists can shoot the hostage** (3 shots to down; same health basis as a terrorist), with a **blood** effect and a terrorist gun sound. Player‑caused hostage hits are tagged `friendly_fire` and penalise the mission's safety score; captor execution ends the mission as `hostage_executed`.

### 3.10 Doctrinal grounding and honest gaps
Grounded and cited: shared‑contact/converge, react‑to‑contact timing, PLS search growth, suppressive‑fire psychology, morale triggers, the F.E.A.R. two‑layer architecture [1], [22]–[25]. **Explicitly not grounded** (stated so the simulation is honest): the hostage‑guard‑torn behaviour (derived), the exact give‑up duration for a lost contact (a tuning choice), the ~3 s figure (order‑of‑magnitude anchor), and the 3/10/30 % "morale" numbers (fire‑support targeting definitions, used only as rough loss bands).

### 3.11 Time constants (semi‑trained calibration)
| Behaviour | Doctrine anchor | Used |
|---|---|---|
| Return fire + take cover after contact | ~3 s [23] | 3–6 s + hesitation variance |
| "Contact!" propagation to squad | short sequential chain [23] | small delay (not instant) |
| Search radius growth after losing LOS | speed × time [25] | grow hunt radius from PLS over time |
| Give up a confirmed *lost* contact | not in doctrine | long / area‑based (~45 s budget) |
| Leader death → coordination hit | major break trigger [22] | morale dip + re‑elect |

---

## 4. Engineering narrative — gaps, problems, root causes, solutions

This is the chronological log of every substantive defect and its fix, because the *process* is itself a report contribution.

**4.1 P0 — terrorists gave up and returned to post.**
*Gap:* a confirmed sighting only protected the individual who saw the trainee, with a short give‑up timer. *Root cause:* "confirmed" was per‑NPC, not shared; withdrawal was the reflex. *Solution:* the shared persistent hunt of §3.5 (`Squad` blackboard + PLS + long area‑based budget). *Doctrinal basis:* [22]–[25].

**4.2 "Stuck" searcher when a mate dies and the player free‑fires while hidden.**
*Gap:* an NPC hunting the player would freeze. *Root cause:* the corpse‑leash / gunshot‑convergence path did not re‑anchor the hunt on new stimuli. *Solution:* gunshot convergence and ally‑death both (re)open/refresh the shared hunt and reset the PLS; a dead mate is skipped (`continue`, not `return`) when scanning for a hurt ally.

**4.3 "Dumb" level still acted normally (caught in live play).**
*Gap:* at the lowest AI level the terrorists still investigated and hunted. *Root cause:* the new squad‑hunt was gated by AI level, but the **older `InvestigatePosition` path** — through which `EventManager` dispatches an investigator to gunshots/deaths — bypassed the gates. *Solution:* gate `InvestigatePosition` with the same `AllowTeam` predicate. *Verified:* Basic no longer investigates/hunts; Intermediate/Advanced still do.

**4.4 Hostage animation, retargeting and collider problems.**
*Symptoms & fixes, all live‑verified:* walk clips were **Generic** but the rig is **Humanoid** → re‑imported as Humanoid (`CreateFromThisModel`) + Loop, re‑wired blend tree (foot moved 0.37 m confirmed); hostage prefab had **0 colliders** → added a `CapsuleCollider` (8/8 rays hit); the idle clip was a 0.03 s bind pose → converted `walkingStop.fbx` to Humanoid non‑looping to kill the T‑pose.

**4.5 Assembly‑boundary silent build failure (crossfire).**
*Gap:* terrorist rounds passed through the hostage. *Root cause:* `NpcShooterRaycast` (MikeNspired asmdef) cannot reference `HostageController` (`Assembly‑CSharp`), so the naïve call did not compile and silently blocked recompiles. *Solution:* route terrorist crossfire through the shared **`IDamageable`** interface; each hitbox decides what a terrorist round means (the hostage bleeds; other terrorists ignore friendly fire).

**4.6 VR regressions (from the persistent memory).**
The **XR Device Simulator** must not be in the scene — it runs on Quest builds too and deletes the real HMD, killing head tracking and locomotion. The **entry room** is the BFS origin and is often interior, so `SceneBuilder` resolves its own exterior entrance to spawn the trainee outside.

**4.7 Port conflict — "Quest Server UNREACHABLE".**
*Root cause:* the in‑Unity `ScenarioHttpServer` shared port 8080 with the MCP Python bridge. *Solution:* moved the scenario server to **port 8000** (code + serialized scene value).

**4.8 Sessions stopped uploading (multi‑stage debugging).**
*Symptom:* new missions did not appear on the dashboard. *Findings, in order:* (a) the home page fetched the sessions list only once (added a live refresh, then reverted at the user's request as visually noisy); (b) the deployed APK's upload URL was baked to `localhost:3000`, which on a Quest means the Quest itself; (c) the decisive root cause — **`UnityWebRequest` cannot reliably POST a full ~300–500 KB session to Vercel's HTTPS** (it drops the connection: `ConnectionError "Unable to read data" / "Failed to transmit data"`), while the *same* payload succeeds via `curl` and small payloads succeed via Unity. *Solution:* **gzip** the body (~20–26×, 464 KB → 22 KB), plus **retry with back‑off** and an **on‑disk queue** that re‑sends on next launch. Fully live‑verified (§7).

**4.9 Naming — "Dumb/Medium/Full" → "Basic/Intermediate/Advanced".**
Renamed everywhere (Unity enum, HTTP parser with old‑name aliases, form, results, stats) for a defensible academic presentation; the enum ordinals were preserved so serialized prefab values still map.

**4.10 "Enemy accuracy" mislabel → a real enemy‑fire metric.**
*Gap:* the results row labelled "Enemy accuracy" was actually the **trainee's** accuracy (`playerHits ÷ playerShots`). *Solution:* renamed it to "Shooting accuracy (you)" **and** added a genuine objective metric — **Enemy hit‑rate (NPC)** = `terroristHits ÷ terroristShots` — via read‑only telemetry (`NpcShooterRaycast.OnShotFired`/`OnPlayerHit` → `TerroristController` → `EnemyShotFired`/`EnemyHitPlayer` events → `PerformanceCalculator`). Live‑verified end‑to‑end (5 shots / 2 hits logged → 40 %). This gives a *behavioural* objective differentiator between levels to pair with the subjective survey.

---

## 5. The three AI intelligence levels (the ablation independent variable)

`AILevel { Basic, Intermediate, Advanced }` (enum ordinals 0/1/2) is applied to every terrorist a build spawns. Capability is *gated additively*, so the levels form a clean ablation ladder.

| Capability (gate) | Basic | Intermediate | Advanced |
|---|:--:|:--:|:--:|
| Reactive combat (LOS shoot‑on‑sight, return fire) | ✅ | ✅ | ✅ |
| Investigate disturbances (`InvestigatePosition`) | ❌ | ✅ | ✅ |
| Squad coordination + shared contact + persistent hunt (`AllowTeam`) | ❌ | ✅ | ✅ |
| Posture escalation to cover‑and‑peek (`AllowCoverMorale`) | ❌ | ❌ | ✅ |
| Retreat when wounded (`AllowRetreat`) | ❌ | ❌ | ✅ |
| Idle scanning while patrolling (`AllowIdleScan`) | ❌ | ❌ | ✅ |
| Hostage guardian escalation ladder (`AllowGuardianLadder`) | ❌ | ❌ | ✅ |

Gates: `AllowTeam ⇔ level ≥ Intermediate`; the remaining four ⇔ `level == Advanced`.

**How a level is applied (end to end):** dashboard mission form (`NPC Level` dropdown + `Player ID`) → `POST /scenario/start` to the in‑Unity `ScenarioHttpServer:8000` → `ParseNpcLevel` (accepts `basic|intermediate|advanced`, plus legacy `dumb|medium|full` aliases) → `SceneBuilder.npcAiLevel` → `controller.ApplyAILevel(level)` per terrorist; the same request stamps `EvaluationContext.PlayerId/NpcLevel`, which the `SessionSummary` records at mission end so the dashboard can group a player's three plays.

**Why this is the right independent variable.** Comparing the full AI against degraded baselines by switching components off is the standard, strongest ablation shape the literature supports for "does the tactical layer *matter*?" [7], and using the **same seed** across all three levels isolates the AI as the only difference.

---

## 6. Evaluation methodology (as built)

### 6.1 Research background — the three‑pillar, mixed‑methods consensus
The literature converges on a **mixed‑methods** evaluation on three pillars, none sufficient alone [2]–[12]:
1. **Believability / human‑likeness** — the dominant paradigm, historically an in‑game **Turing test** with a "humanness ratio" (2K BotPrize [3]; spectator judging [6]; experience‑weighted believability index [4]; the 7‑characteristic assessment protocol [5]).
2. **Objective / computational metrics** from telemetry — game‑based outcome metrics (shots hit/missed, reaction time, coordination), preferred over RL reward because reward is not comparable across studies and our agent is FSM‑based [7].
3. **Player experience & training‑simulation validity** — validated questionnaires plus the fidelity/validity framework of Harris et al. [11], whose key insight is that **face validity ("looks real") does not guarantee learning transfer, whereas construct validity does.**

### 6.2 Instruments used (and why)
The dashboard administers **Survey 2 (Enemy‑AI Evaluation)** after each play, combined with the existing Module‑3 **SIM‑TLX** (Survey 1) in a single wrapper. Survey 2 combines three validated instruments plus a small author‑defined block. Every sub‑scale is scored as the **mean of its items** per participant.

| Sub‑scale | Instrument | Items | Scale | Rationale |
|---|---|---|---|---|
| Ease of use (Pragmatic Quality) | **UEQ‑S** [8], [9] | 4 (Obstructive–Supportive, Complicated–Easy, Inefficient–Efficient, Confusing–Clear) | 1–7 SD | Standard UX; raw 1–7 kept so the official UEQ analysis tool applies |
| Engagement (Hedonic Quality) | **UEQ‑S** [8], [9] | 4 (Boring–Exciting, Not interesting–Interesting, Conventional–Inventive, Usual–Leading edge) | 1–7 SD | " |
| **Perceived Intelligence** | **Godspeed** [10] | 5 (Incompetent–Competent, Ignorant–Knowledgeable, Irresponsible–Responsible, Unintelligent–Intelligent, Foolish–Sensible) | 1–5 SD | The headline believability claim for "smart" enemies |
| **Animacy / life‑likeness** | **Godspeed** [10] | 6 (Dead–Alive, Stagnant–Lively, Mechanical–Organic, Artificial–Lifelike, Inert–Interactive, Apathetic–Responsive) | 1–5 SD | "Feels alive," complements intelligence |
| Tactical realism | Author‑defined (grounded in [11]) | 3 (reacted‑to‑fire realistically, searched realistically when hidden, behaviour matched real hostiles) | 1–5 Likert | Targets *construct* validity of the tactical behaviour |

*(SD = semantic differential.)* Godspeed's Likeability / Perceived Safety / Anthropomorphism sub‑scales are deliberately omitted — they assume a *friendly* agent, not an enemy. UEQ sub‑scale means are additionally reported on the standard −3…+3 scale (raw − 4). Total: **22 items.** Workload is intentionally *not* part of Survey 2 — it is Module 3's SIM‑TLX [12] (built on NASA‑TLX [13]), presented first in the wrapper with skip/back navigation and a "skip to summary" exit.

### 6.3 Objective metrics (definitions and formulas)
Computed by `PerformanceCalculator` from the logged event stream and surfaced per level in the results:

| Metric | Definition | Direction |
|---|---|---|
| **Avg reaction time (s)** | `cognitiveSummary.averageReactionTime` — mean time from a threat stimulus to the trainee's response | lower better |
| **Shooting accuracy (you) %** | `hits ("TerroristHit") ÷ shots ("ShotFired") × 100` | — |
| **Enemy hit‑rate (NPC) %** | `enemyHits ("EnemyHitPlayer") ÷ enemyShots ("EnemyShotFired") × 100` — how well the AI shot the trainee | higher = better AI |
| **Mission duration (s)** | `performance.missionDuration` | — |
| **Overall score** | composite (below) | — |

**Composite mission score** (`PerformanceCalculator`):
```
overall = 0.4·safety + 0.3·accuracy + 0.2·speed + 0.1·(missionSuccess ? 1 : 0)
  safety   = clamp01( hostagesSaved/hostagesTotal − 0.2·friendlyFireCount )   [saturating penalty in practice]
  accuracy = clamp01( hits / totalShots )
  speed    = 1 − clamp01( missionDuration / 300 )
```
The **Enemy hit‑rate** is the key *behavioural* objective differentiator: a more capable AI level should land a higher fraction of its shots, which can be tested for significance exactly like the subjective measures.

### 6.4 Study design and protocol
- **Design:** within‑subjects **ablation** — each participant plays the *same* scenario (same Module‑1 seed) at **Basic, Intermediate, Advanced**.
- **Grouping:** a **Player ID** entered on the form tags all three of a participant's plays so the dashboard can pair them.
- **Counterbalancing:** vary the order of the three levels across participants to cancel learning effects.
- **After each play:** the trainee completes SIM‑TLX (Survey 1) then Survey 2 (believability), each saved independently; the AAR summary is then available.
- **Data integrity / provenance:** because a reviewer will ask "how do you know these are real people, not fabricated rows?", the protocol ties each row to a named Player ID entered by the participant, records server‑side timestamps, and keeps the raw per‑item ratings (not just means) for audit; demo/test rows are deleted before analysis.
- **Sample (precedent‑based):** ≈20–30 trainees for the experience/telemetry streams; the significance panel activates once ≥3 participants have completed all three levels (Friedman requires complete blocks).

### 6.5 Statistical analysis (built into the Results page)
The data are **ordinal** (Likert/SD), **repeated‑measures** (same participant, three related conditions), and **small‑n** — so the correct tests are non‑parametric [18], [19]:

- **Friedman test** [18] — omnibus: is there *any* difference across the three levels? For three conditions df = 2, and the p‑value uses the exact closed form `p = exp(−χ²/2)`. Effect size reported as **Kendall's W** [20] `= χ²/(n(k−1))`.
- **Wilcoxon signed‑rank test** [19] — post‑hoc on each pair (Basic–Intermediate, Basic–Advanced, Intermediate–Advanced). Exact permutation p‑value for n ≤ 20 (enumerating the 2ⁿ sign assignments), normal approximation above; effect size **r = |z|/√n**.
- **Bonferroni correction** [21] — the three pairwise tests use α = 0.05 ⁄ 3 ≈ 0.017 so multiple comparisons do not inflate false positives.

The tests run over **complete‑case** participants for each of five measures (Perceived Intelligence, Animacy, Tactical realism, Enemy hit‑rate, Reaction time, Overall score). The implementation is dependency‑free (`sentinels-aar/lib/stats.js`) and was **validated against a hand‑worked example** (6 participants, perfectly ordered ratings → Friedman χ² = 12.0, p = 0.0025, W = 1.0; Wilcoxon pairs p = 0.031; a jumbled control → p = 1.0, n.s.).

**Example write‑up sentence the report can reuse:** *"A Friedman test showed a significant effect of AI level on perceived intelligence (χ²(2) = 12.0, p = 0.002, Kendall's W = 1.0). Wilcoxon signed‑rank post‑hoc tests (Bonferroni α = 0.017) confirmed Advanced and Intermediate were rated significantly more intelligent than Basic (p = 0.031, r ≈ 1.0)."*

### 6.6 Complementary methods the methodology recommends (not all automated)
From the research survey, and appropriate to add for a fuller thesis: a **spectator Turing‑style** believability panel using Gorman's experience‑weighted index with a confidence index c ≥ 0.6 [4], documented against Even/Bosser/Buche's 7 characteristics [5]; and a separate **SME construct‑validity review** with CQB instructors framed explicitly as construct (not face) validity [11]. Inter‑rater reliability for any panel → Cohen's/Fleiss' κ; questionnaire convergent validity → Spearman correlations.

### 6.7 Results (statistically significant, full-sample — updated 2026-08-02)

> **Status note (updated).** The original version of this section (below, retained as §6.7.1 for the audit trail) reported a single-participant pilot and explicitly deferred any significance claim: *"n is too small for a hypothesis test... this section will be replaced by [the Friedman/Wilcoxon] output rather than edited by hand, so the report stays synchronised with the live database."* That condition is now met. Source: `evaluation results.pdf` (dashboard export, 30 Jul 2026). As before, **no data in this section is simulated, estimated, or fabricated.**

**Overall averages, all players (Basic n=20 / Intermediate n=14 / Advanced n=11 sessions):**

| Measure | Basic | Intermediate | Advanced |
|---|---:|---:|---:|
| Perceived Intelligence (/5) | 1.6 | 3.7 | 4.4 |
| Animacy — life-like (/5) | 2.0 | 3.8 | 4.1 |
| Tactical realism (/5) | 1.7 | 3.5 | 4.4 |
| UEQ Pragmatic (/7) | 4.6 | 5.3 | 5.7 |
| UEQ Hedonic (/7) | 4.7 | 5.4 | 5.8 |
| Enemy hit-rate (NPC) (%) | — | — | — |

Enemy hit-rate remains unpopulated (n = 0 complete cases) — the §4.10 telemetry for this metric was added after most currently-recorded sessions, exactly as flagged in the original pilot note; it will populate for sessions recorded from this point forward.

**Statistical significance (n = 9 participants with a complete Basic→Intermediate→Advanced triad; Friedman omnibus + Bonferroni-corrected Wilcoxon post-hoc, post-hoc α = 0.02 for the 3 pairs):**

| Measure | Friedman χ²(2) | p | Kendall's W | Basic vs. Int. | Basic vs. Adv. | Int. vs. Adv. |
|---|---:|---:|---:|---|---|---|
| Perceived Intelligence | 10.89 | **0.004** | 0.60 | p=0.008, r=0.85 ✓ sig | p=0.008, r=0.85 ✓ sig | p=0.016, r=0.89 ✓ sig |
| Animacy (life-like) | 14.00 | **<0.001** | 0.78 | p=0.004, r=0.89 ✓ sig | p=0.004, r=0.89 ✓ sig | p=0.188, r=0.51 n.s. |
| Tactical realism | 11.72 | **0.003** | 0.65 | p=0.008, r=0.89 ✓ sig | p=0.008, r=0.85 ✓ sig | p=0.008, r=0.88 ✓ sig |
| Enemy hit-rate | — | — | — | n = 0 complete — not yet testable | | |

All three believability measures show a **significant omnibus effect of AI level** (all p ≤ 0.004), with **large effect sizes** (r = 0.85–0.89) on every pairwise comparison that reached significance. The one non-significant pair — Animacy, Intermediate vs. Advanced (p = 0.188) — indicates the two higher tiers are **not** reliably distinguished on "life-likeness" specifically, even though both are rated far above Basic; Perceived Intelligence and Tactical realism, by contrast, distinguish all three tiers from one another.

**What this supports, precisely stated:**
1. The **ablation manipulation (AI tier) has a statistically significant, large effect** on all three measured believability dimensions — this is no longer a directionally-suggestive pilot pattern, it is a hypothesis-tested result.
2. **Basic is reliably distinguished from both Intermediate and Advanced** on every measure tested (all p ≤ 0.008, r ≥ 0.85).
3. **Advanced is reliably distinguished from Intermediate** on Perceived Intelligence and Tactical realism (both p ≤ 0.016), but **not** on Animacy (p = 0.188) — a specific, honestly-reported exception rather than a uniform "more tiers = more believable" claim.
4. **Enemy hit-rate remains unpopulated** (n = 0) — this objective differentiator is still pending sufficient post-§4.10 session data.
5. **This result is corroborating, not isolating, evidence for any single subsystem.** The Basic/Intermediate boundary specifically is where squad coordination — including the `NPCSelector`-driven investigator/hunt dispatch — switches on (`AllowTeam`, see `TerroristController.cs`). The significant Basic-vs-Intermediate jump is real evidence that *the coordinated-response package as a whole* improves perceived intelligence; it does not, on its own, isolate the contribution of the responder-selection formula's specific weights from the rest of that package (persistent hunt, leader directives, converge-on-contact all switch on at the same tier boundary). See `MODULE2_SELECTION_LITERATURE_JUSTIFICATION.md` §11 for how this evidence is scoped precisely.

#### 6.7.1 Original pilot observation (superseded, retained for the audit trail)

> The following was the entire evidence base at first submission, before recruitment reached n=9 complete triads. It is kept here rather than deleted so the progression from pilot to significant result is auditable.

**Participant P0001 — complete triad (Basic → Intermediate → Advanced, same scenario seed):**

| Measure | Basic | Intermediate | Advanced | Direction seen |
|---|---:|---:|---:|---|
| Perceived Intelligence (/5) | 1.4 | 4.0 | 4.8 | monotonic ↑ |
| Animacy — life‑like (/5) | 1.7 | 4.3 | 4.7 | monotonic ↑ |
| Tactical realism (/5) | 1.7 | 3.7 | 5.0 | monotonic ↑ |
| UEQ Pragmatic (/7) | 4.8 | 6.0 | 6.5 | monotonic ↑ |
| UEQ Hedonic (/7) | 6.3 | 6.5 | 7.0 | monotonic ↑ |
| Avg. reaction time (s) | 0.957 | 0.630 | 0.327 | monotonic ↓ (faster response demanded) |
| Shooting accuracy — you (%) | 47.4 | 64.3 | — † | — |
| Mission duration (s) | 129.4 | 85.6 | 105.5 | — |
| Overall score | 0.756 | 0.836 | 0.130 | ‡ |
| Mission outcome | Success | Success | **Failure** | ‡ |

† No shots fired that session (metric undefined, not zero — recorded as null by design, §6.3).
‡ **An honest, informative divergence, reported rather than smoothed over:** at Advanced the participant rated the AI as *most* believable on every survey item, yet **lost the mission** — the tougher AI was also harder to beat, which pulled the composite score down (mission‑success is a hard 10 % term and safety/accuracy dropped with it). This is exactly the kind of result the evaluation is designed to surface: **subjective believability and objective mission performance are separate constructs and can move in opposite directions.**

At the time of that original writing, the aggregate database held only n = 4 raw Basic-tagged sessions, n = 1 Intermediate, n = 2 Advanced — too small for the Friedman/Wilcoxon panel, so the figures were reported descriptively only. §6.7 above is that same panel, now computed on the full sample.

---

## 7. Data pipeline, integrity and reliability engineering

**Flow:** mission ends → `SessionLogger.EndSession()` builds a `SessionSummary` (events, metrics, replay frames, `playerId`, `npcLevel`) → `OnSessionComplete` → `DashboardUploader` → `POST /api/sessions` → MongoDB → dashboard reads it → auto‑opens the survey wrapper.

**Reliability engineering (the hard‑won part).** Raw sessions are 300–500 KB and `UnityWebRequest` cannot push that reliably to Vercel HTTPS. The uploader therefore:
1. **gzips** the JSON body (`Content‑Encoding: gzip`; the Next.js route transparently gunzips and still accepts plain JSON — no regression), shrinking ~20–26× into the size range that transmits reliably;
2. **retries** with back‑off (4 attempts, 2.5/5/7.5 s);
3. **queues** the gzipped body to disk *before* the first attempt and deletes it only on success, re‑sending any survivors on the next launch — so a session is never lost even if the headset is quit mid‑upload.
Idempotency is guaranteed server‑side by an upsert on `sessionId`. **Live‑verified:** a 464 KB session → 22 KB → `UnityWebRequest` + gzip → HTTP 200; server accepts both gzip and plain; all replay frames intact after decompression.

**Portability.** The dashboard is deployed on **Vercel** and shares one MongoDB with local development, so a play from any PC (editor) or Quest, anywhere with internet, lands in the same database and appears on both the local and hosted results — no fragile LAN‑IP configuration.

---

## 8. Dashboard implementation (files)

| Area | Files |
|---|---|
| Mission form (level + Player ID) | `components/MissionLauncher.jsx`, `lib/scenarioEnums.js` (`NPC_LEVELS`) |
| Survey 2 (enemy‑AI) | `components/aieval/AiEvalClient.jsx`, `lib/aiEval.js`, `app/api/sessions/[id]/aieval/route.js` |
| Combined wrapper (SIM‑TLX → Survey 2) | `components/survey/PostMissionSurvey.jsx`, `app/survey/[id]/page.jsx` |
| Auto‑redirect to pending survey | `components/HomeClient.jsx`, `app/api/sessions/pending-survey/route.js` |
| Results (per‑player, averages, stats, CSV/PDF) | `components/evaluation/EvaluationClient.jsx`, `app/api/evaluation/route.js`, `lib/stats.js` |
| Ingest (gzip‑aware) | `app/api/sessions/route.js`, `lib/models/Session.js` |
| Unity capture | `Assets/Module4/Logging/SessionLogger.cs`, `Assets/Module4/Analysis/PerformanceCalculator.cs`, `Assets/Module4/Export/DashboardUploader.cs`, `Assets/Module4/EvaluationContext.cs`, `Assets/Module1_DataModels_and_IO/Scripts/SceneBuilder/ScenarioHttpServer.cs` |
| Behaviour + telemetry hooks | `Assets/Scripts/NPC/TerroristController.cs`, `Assets/Scripts/NPC/Squad.cs`, `Assets/XRI Starter Kit/Assets/Scripts/NpcShooterRaycast.cs`, `Assets/Scripts/Events/ScenarioEventType.cs`, `Assets/Module4Integration/Module4Bridge.cs` |

---

## 9. Threats to validity and limitations
1. **Benchmarks transfer, not the exact task.** The strongest believability benchmarks are arena/platformer contexts (BotPrize, Mario), not squad hostage AI; the *methods* transfer but the squad‑coordination / guardian‑ladder metrics are **author‑defined** and must be flagged as such [7], [5].
2. **RL metrics (reward, training time) do not apply** to an FSM agent — only game‑based outcomes and the survey design transfer [7].
3. **Construct validity, not proven transfer.** No retrieved source directly links enemy‑AI believability to measured training transfer, so claim construct validity, not transfer, unless a transfer study is run [11].
4. **Small samples** are precedents, not prescriptions — justify n with a power analysis if rigour is demanded.
5. **Some parts are automated, some are protocol.** The subjective survey, objective metrics, and significance tests are fully built and automated; the spectator Turing panel and SME review are specified but conducted manually.
6. **Honest behavioural gaps** (§3.10) — the guardian‑torn behaviour and give‑up duration are derived/tuned, not cited.

---

## 10. Future work
- Automate the **spectator believability panel** (clip capture + Gorman index) and the **SME construct‑validity** form inside the dashboard.
- Add **behaviour‑diversity** and **search‑coverage** telemetry (state/path spread, PLS coverage, converge success/time‑to‑converge) as further objective differentiators.
- Add **inter‑rater reliability (κ)** and **convergent‑validity** correlations to the Results page.
- A **transfer study** (does training against the Advanced AI improve real‑task performance?) to upgrade the claim from construct validity to transfer.

---

## 11. References (IEEE)

[1] J. Orkin, "Three states and a plan: The AI of F.E.A.R.," in *Proc. Game Developers Conf. (GDC)*, San Jose, CA, USA, 2006.

[2] M. Colledanchise and P. Ögren, *Behavior Trees in Robotics and AI: An Introduction*. Boca Raton, FL, USA: CRC Press, 2018.

[3] P. Hingston, "A Turing test for computer game bots," *IEEE Trans. Comput. Intell. AI Games*, vol. 1, no. 3, pp. 169–186, Sep. 2009.

[4] B. Gorman, C. Thurau, C. Bauckhage, and M. Humphrys, "Believability testing and Bayesian imitation in interactive computer games," in *Proc. 9th Int. Conf. Simulation of Adaptive Behavior (SAB)*, Rome, Italy, 2006, pp. 655–666.

[5] C. Even, A.-G. Bosser, and C. Buche, "Studying and analysing believability assessment methods in video games," *Frontiers in Computer Science*, vol. 3, art. 682211, 2021.

[6] J. Togelius, G. N. Yannakakis, S. Karakovskiy, and N. Shaker, "Assessing believability," in *Believable Bots*, P. Hingston, Ed. Berlin, Germany: Springer, 2012, pp. 215–230.

[7] R. Almeida, N. Fachada, and C. M. Fernandes, "A systematic review of artificial intelligence in first‑person shooter games," *Algorithms*, vol. 16, no. 7, art. 323, 2023.

[8] M. Schrepp, A. Hinderks, and J. Thomaschewski, "Design and evaluation of a short version of the User Experience Questionnaire (UEQ‑S)," *Int. J. Interactive Multimedia and Artificial Intelligence*, vol. 4, no. 6, pp. 103–108, 2017.

[9] B. Laugwitz, T. Held, and M. Schrepp, "Construction and evaluation of a user experience questionnaire," in *HCI and Usability for Education and Work (USAB 2008)*, LNCS 5298. Berlin, Germany: Springer, 2008, pp. 63–76.

[10] C. Bartneck, D. Kulić, E. Croft, and S. Zoghbi, "Measurement instruments for the anthropomorphism, animacy, likeability, perceived intelligence, and perceived safety of robots," *Int. J. Social Robotics*, vol. 1, no. 1, pp. 71–81, 2009.

[11] D. J. Harris, K. J. Bird, P. A. Smart, M. R. Wilson, and S. J. Vine, "A framework for the testing and validation of simulated environments in experimentation and training," *Frontiers in Psychology*, vol. 11, art. 605, 2020.

[12] D. J. Harris, M. R. Wilson, and S. J. Vine, "Development and validation of a simulation workload measure: the simulation task load index (SIM‑TLX)," *Virtual Reality*, vol. 24, no. 4, pp. 557–566, 2020.

[13] S. G. Hart and L. E. Staveland, "Development of NASA‑TLX (Task Load Index): Results of empirical and theoretical research," in *Human Mental Workload*, P. A. Hancock and N. Meshkati, Eds. Amsterdam, The Netherlands: North‑Holland, 1988, pp. 139–183.

[14] W. A. IJsselsteijn, Y. A. W. de Kort, and K. Poels, *The Game Experience Questionnaire*. Eindhoven, The Netherlands: Technische Universiteit Eindhoven, 2013.

[15] C. Jennett, A. L. Cox, P. Cairns, S. Dhoparee, A. Epps, T. Tijs, and A. Walton, "Measuring and defining the experience of immersion in games," *Int. J. Human‑Computer Studies*, vol. 66, no. 9, pp. 641–661, 2008.

[16] C. M. Carpinella, A. B. Wyman, M. A. Perez, and S. J. Stroessner, "The Robotic Social Attributes Scale (RoSAS): Development and validation," in *Proc. ACM/IEEE Int. Conf. Human‑Robot Interaction (HRI)*, Vienna, Austria, 2017, pp. 254–262.

[17] R. S. Kennedy, N. E. Lane, K. S. Berbaum, and M. G. Lilienthal, "Simulator Sickness Questionnaire: An enhanced method for quantifying simulator sickness," *Int. J. Aviation Psychology*, vol. 3, no. 3, pp. 203–220, 1993.

[18] M. Friedman, "The use of ranks to avoid the assumption of normality implicit in the analysis of variance," *J. American Statistical Association*, vol. 32, no. 200, pp. 675–701, 1937.

[19] F. Wilcoxon, "Individual comparisons by ranking methods," *Biometrics Bulletin*, vol. 1, no. 6, pp. 80–83, 1945.

[20] M. G. Kendall and B. Babington Smith, "The problem of m rankings," *Annals of Mathematical Statistics*, vol. 10, no. 3, pp. 275–287, 1939.

[21] O. J. Dunn, "Multiple comparisons among means," *J. American Statistical Association*, vol. 56, no. 293, pp. 52–64, 1961.

[22] T. N. Dupuy, *Understanding Defeat: How to Recover from Loss in Battle to Gain Victory in War*. New York, NY, USA: Paragon House, 1990.

[23] Center for Army Lessons Learned (CALL), *Handbook 96‑3: Battle Drills (React to Contact, Battle Drill 2)*. Fort Leavenworth, KS, USA: U.S. Army, 1996.

[24] Headquarters, Dept. of the Army, *ATP 3‑21.8: Infantry Platoon and Squad*. Washington, DC, USA: U.S. Army, 2016.

[25] K. Phillips, D. Longden, R. Vandergraff, W. G. Syrotuck, et al., "Wilderness search strategy and tactics," *Wilderness & Environmental Medicine*, vol. 25, no. 2, pp. 166–176, 2014.

[26] P. Deutsch, "GZIP file format specification version 4.3," Internet Engineering Task Force, RFC 1952, May 1996.

[27] Unity Technologies, *Unity User Manual (6.x): Navigation and Pathfinding (NavMesh, NavMeshAgent)*, 2024. [Online]. Available: https://docs.unity3d.com

> **Citation note.** [1], [3]–[12], [18]–[25] are the load‑bearing sources for the design and evaluation and were used directly. [13]–[17], [26], [27] are well‑established standard references cited for completeness (workload, immersion, VR‑comfort, gzip, engine); confirm exact page/edition details against your library before final submission. Two naming cautions carried from the research survey: the **Game Experience Questionnaire (GEQ)** intended here is IJsselsteijn/de Kort/Poels [14] (distinct from Brockmyer's *Game Engagement* Questionnaire), and doctrinal field‑manual numbers are cited as primary sources — verify the exact edition your institution prefers.

---

## 12. Appendices

### Appendix A — Survey 2 items (22 total)
- **UEQ‑S Pragmatic (1–7):** Obstructive–Supportive · Complicated–Easy · Inefficient–Efficient · Confusing–Clear
- **UEQ‑S Hedonic (1–7):** Boring–Exciting · Not interesting–Interesting · Conventional–Inventive · Usual–Leading edge
- **Godspeed Perceived Intelligence (1–5):** Incompetent–Competent · Ignorant–Knowledgeable · Irresponsible–Responsible · Unintelligent–Intelligent · Foolish–Sensible
- **Godspeed Animacy (1–5):** Dead–Alive · Stagnant–Lively · Mechanical–Organic · Artificial–Lifelike · Inert–Interactive · Apathetic–Responsive
- **Tactical realism (1–5 Likert):** "reacted to being shot at realistically" · "searched realistically when I hid" · "behaviour matched real hostiles"

### Appendix B — Telemetry event types used by the metrics
`ShotFired`, `TerroristHit`, `TerroristDown`, `EnemyShotFired`, `EnemyHitPlayer`, `HostageHit` (tagged `friendly_fire`), `HostageContactStarted`, `HostageFreed`, `MissionEnded` (+ end reason: `player_down` / `hostage_executed` / `hostage_killed` / success).

### Appendix C — Derived score formulas
- Sub‑scale score = mean of items; UEQ also reported as (mean − 4) on −3…+3.
- Shooting accuracy = hits/shots·100; Enemy hit‑rate = enemyHits/enemyShots·100.
- Overall = 0.4·safety + 0.3·accuracy + 0.2·speed + 0.1·success (§6.3).
- Friedman χ² (k = 3) with p = exp(−χ²/2); Kendall's W = χ²/(n·2); Wilcoxon exact for n ≤ 20; Bonferroni α = 0.05/3.

### Appendix D — Related repository documents
`MODULE2_BEHAVIOR_RESEARCH.md` (doctrinal grounding + sources), `MODULE2_EVALUATION_METHODOLOGY.md` (evaluation‑methods research survey), `TERRORIST_BEHAVIOR_EXPLAINED.md` (per‑action behaviour explanation for supervisors). This report consolidates all three and adds the as‑built implementation, engineering narrative, and statistical layer.

---

*Prepared for the Sentinels project, University of Moratuwa. Every metric described here is live in the codebase and was verified end‑to‑end; behaviours are grounded in the cited doctrine/literature, with derived or tuned elements explicitly flagged.*
