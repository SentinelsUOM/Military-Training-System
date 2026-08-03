# Module 4 — Full End-to-End Report
## After-Action Review (AAR): Scoring, Replay and Evaluation

**Project:** Sentinels — VR Counter-Terrorism / Hostage-Rescue Training Simulator
**Institution:** University of Moratuwa (final-year project)
**Platform:** Unity 6000.3.1f1 (Meta Quest 3, VR) + Next.js After-Action-Review (AAR) dashboard
**Scope of this document:** the complete record of Module 4 — the After-Action Review subsystem that records, scores, and reports on the trainee's performance — covering design, every gap/problem/root-cause/solution, the full implementation, the evaluation methodology actually built, the technologies used, and correctly formatted (IEEE) citations.

> **Purpose.** This is the master reference for Module 4, written in the same spirit and format as `MODULE2_FULL_REPORT.md` so a later pass (or a chat assistant) can generate the formal project-report chapter from it without re-deriving anything. It consolidates and supersedes `MODULE4_REVIEW.md` (engineering audit), `MODULE4_RESEARCH_REVIEW.md` (comparative landscape review), `MODULE4_EVALUATION.md` (construct-validity evaluation against the Expert Value Table), `MODULE4_MOVEMENT_VALIDATION_METHODOLOGY.md`, `MODULE4_WORKLOAD_REASONING_METHODOLOGY.md`, `PLAYER_SAFETY_SCORE.md`, and `HOSTAGE_EMOTION_SOUND_RESEARCH.pdf`, and adds the as-built implementation, the engineering narrative, and the statistical layer built for Module 4's own scores.

> ### 🆕 Revision note — what changed in this pass
>
> | § | Change |
> |---|---|
> | **§3.5** | 🆕 Rewritten — the hostage-profile simplification now records the *auditory* convergent finding, the three-profile (Weak/Normal/Brave) naming, and why per-profile distances were rejected |
> | **§6.5b** | 🆕 Expanded from 4 findings to 5 — adds the auditory result and the "distances are deliberately identical" design decision, plus the verification-vs-discovery reporting rule |
> | **§6.6** | 🆕 **Rewritten** — Module 3/4 no longer split by AI tier (that is Module 2's independent variable). Now documents the three sections actually built: per-player combined average, pooled one-sample Wilcoxon vs expert, and the one-way ANOVA table |
> | **§6.7** | 🆕 Retitled *Repeated-**Play*** Reliability — groups by player only, no tier split |
> | **§6.8** | 🆕 Real/synthetic data boundary restated as a **rule** (only `P0001` is real) rather than an ID range that keeps drifting |
> | **§10** | 🆕 Limitation 2 sharpened — "all sessions" statistics are currently a mechanism demonstration, not a finding |
> | **§11** | 🆕 Three future-work items added/updated: hostage profiles, per-band accuracy *rate*, and the existing sample recruitment item |
> | **§12** | 🆕 Refs [45]–[46] added for the ANOVA method; profile-strand sources summarised rather than duplicated |
> | **Appendix D** | 🆕 `HOSTAGE_PROFILE_RESEARCH.md` added, with a reading order for the hostage emotional model |
>
> Companion document `HOSTAGE_PROFILE_RESEARCH.md` carries its own Rev. 2 note listing its five changes.

> **A note on honesty this document insists on, the same way `MODULE2_FULL_REPORT.md` §6.7 does.** Module 4's database currently contains exactly **one genuine human-played dataset** (participant `P0001`, self-piloted by the project author) plus several **synthetic placeholder rows** (`P0002`–`P0005`) that were generated during development *purely to test the dashboard UI renders correctly with more than one row of data* — never to stand in for real evaluation evidence. §6.8 states precisely which rows are real and which are synthetic, and the synthetic rows must **not** be cited as findings in a submitted report. This distinction is treated as load-bearing throughout this document.

---

## Table of Contents
1. Scope and objectives
2. System context and technology stack
3. System design — the AAR itself
4. Engineering narrative — gaps, problems, root causes, solutions
5. The five scores (what is being evaluated)
6. Evaluation methodology (as built)
   6.1 Construct validity vs the Expert Value Table
   6.2 Movement-telemetry validation methodology
   6.3 Workload (SIM-TLX) reasoning & prediction methodology
   6.4 Operator Safety — research grounding
   6.5 Hostage distress — sound/emotion research grounding
   6.6 Discriminant validity — Module 4's own scores between trainees
   6.5b Hostage distress — trait/profile research grounding (specified, not implemented)
   6.7 Reliability — repeated-play testing
   6.8 Preliminary results (real pilot data, as of submission — with an explicit real/synthetic data boundary)
7. Data pipeline, integrity and reliability engineering
8. Dashboard implementation (files)
9. Comparative analysis — how Module 4 measures up
10. Threats to validity and limitations
11. Future work
12. References (IEEE)
13. Appendices (score formulas, event types, file map, related documents)

---

## 1. Scope and objectives

Module 4 owns the **After-Action Review (AAR)** — the "so what did I learn?" half of the trainer. Modules 1–3 generate the scenario, run the AI, and track the trainee's cognition during the VR firefight; Module 4 **observes, records, scores, and reports** on what happened, then lets the trainee (or an evaluator) review it.

**Objectives.**
1. **Automatic, zero-instrumentation capture** — record everything the game emits from a single event bus, with no per-gameplay-system wiring, so new event types need no Module 4 changes.
2. **Defensible scoring** — produce quantitative scores (accuracy, hostage safety, speed, operator safety, overall) that are not arbitrary — each grounded, where research exists, in a cited real-world benchmark, and honestly flagged as a design default where it does not.
3. **A second safety axis the field usually omits** — most tactical AAR tools score only the *outcome* (did the hostage survive); Module 4 also scores **the trainee's own conduct** (Operator Safety) and **the hostage's psychological state** (distress index), because a trainee can win the mission while behaving recklessly, and a "successful" rescue can still traumatise the hostage.
4. **A verifiable evaluation, not just a scoreboard** — every score is checked against either a published benchmark (construct validity), tested for whether the trainee population actually differs from that benchmark and whether it separates trainees from each other (§6.6), or checked for consistency when the same person plays repeatedly (reliability, §6.7).
5. **A full replay and reporting pipeline** — 2D/3D reconstruction, incident extraction, and both an offline HTML report and a hosted dashboard, so the AAR is usable with or without a network.

---

## 2. System context and technology stack

| Layer | Technology | Role in Module 4 |
|---|---|---|
| Simulation | **Unity 6000.3.1f1**, C# | Runs the mission; Module 4 observes it passively |
| Assembly isolation | `TeamSentinels.Module4` asmdef, `"references": []` | Core scoring/logging code has **zero** dependency on gameplay code |
| Integration | `Module4Integration/` (no asmdef, lives in `Assembly-CSharp`) | Bridges Module 4 to Module 1/2/3 gameplay code (can see both sides) |
| Cognitive input | `Assets/CognitiveTracking/` (Module 3, `TeamSentinels.CognitiveTracking`) | Feeds reaction time + movement telemetry **into** Module 4; Module 4 hosts/displays it, does not compute it |
| Persistence | JSON `SessionSummary` → `Application.persistentDataPath` | Every session serialised as one JSON document |
| Transport | `UnityWebRequest` → `POST /api/sessions` (gzip, retry, on-disk queue) | Uploads a finished session to the dashboard |
| Dashboard | **Next.js 14** (App Router), React client components, Recharts, react-three-fiber | Session browser, AAR tabs, evaluation page |
| Database | **MongoDB** via **Mongoose** | Stores every session, layout snapshot, movement track |
| Hosting | **Vercel** (dashboard) — shares one MongoDB with local dev | Portable capture from any PC/headset |
| Tooling | CoplayDev **MCP for Unity** (HTTP bridge) | Live inspection / scripted verification used throughout development |

**Assembly boundaries (a recurring design constraint, exactly as in Module 2).** `TeamSentinels.Module4` cannot reference `Assembly-CSharp`; `Assembly-CSharp` auto-references it. This dictated: `EvaluationContext` (participant/AI-tier tagging) lives **inside** the Module 4 assembly even though it is written to by Assembly-CSharp code (`ScenarioHttpServer`), because an asmdef cannot reference back the other way; `Module4Bridge` and `RoomLocator` live in `Module4Integration/` (no asmdef) specifically because they need to see both Module 4's types and Module 1/2's gameplay types.

---

## 3. System design — the AAR itself

### 3.1 Data capture (the recording layer)
- **Universal event log.** `Module4Bridge` subscribes to the single game-wide event bus (`EventManager.OnEventRaised`) and forwards every `ScenarioEventType` into `SessionLogger.LogEvent`. New event types require no bridge change — the defining engineering characteristic that most differentiates Module 4 from hand-instrumented commercial AAR tools.
- **NPC state transitions** — every terrorist/hostage FSM transition logged with actor, previous/new state, trigger, timestamp.
- **Hostage emotional history** — every hostage state change plus a derived distress score (§6.5).
- **Spatial replay track** — `ReplayRecorder` samples every actor's position/rotation/state at a fixed tick, auto-downsampled to bound memory.
- **Room/door layout snapshot** — `LayoutSnapshot` (rooms as axis-aligned boxes, doors with wall side) is attached to the session so the dashboard can draw walls behind the 2D map and build the 3D replay, without Module 4 taking a dependency on Module 1's `ScenarioData` type (`Module4Integration` performs that conversion).
- **Distance-aware shot telemetry** — `NPCHitBox` now measures trainee↔target distance (`Camera.main` → hit point) at the instant of every hit, and `GunFireDetector` checks whether any terrorist was visible at all when a round was fired — both additive hooks feeding the accuracy and negligent-discharge metrics (§6.1, §6.4).
- **Reaction time (Module 3's data, hosted by Module 4)** — `SessionLogger.LogReactionTime` accepts per-stimulus reaction times measured by Module 3's `ReactionTimeTracker`; Module 4 aggregates them into `cognitiveSummary` but does not itself measure cognition.
- **Persistence + transport** — an indented JSON `SessionSummary` is written to `persistentDataPath` (not `Application.dataPath`, which is read-only on Quest — see §4.1) and POSTed to the dashboard, idempotent on `sessionId`.

### 3.2 Performance scoring
A five-score model computed at mission end by `PerformanceCalculator` (exact formulas in §5 and Appendix C):
- **Accuracy** — hit rate scored against the real-world expected hit rate at the trainee's actual engagement distance (NYPD SOP-9 bands), not a flat ratio.
- **Hostage Safety** — hostages saved, with a saturating friendly-fire penalty.
- **Speed** — mission duration against a scenario-complexity-normalized target (room/terrorist count, AI tier), not a flat clock.
- **Operator Safety** — a second, independent safety axis: survivability, exposure control, weapon discipline (friendly fire + negligent discharge), and threat-response speed.
- **Overall** — the weighted composite of the first three plus a mission-success bonus.

### 3.3 Automatic incident extraction
`IncidentExtractor` auto-detects seven notable-moment types, each severity-ranked and tagged with the room it happened in: `FirstContact`, `FirstShot`, `HostageEndangered` (high), `TerroristNeutralized`, `AlertCascade` (≥3 NPCs alerted within 5 s — a coordinated enemy response, medium), `HostageRescued`, `MissionEnd`.

### 3.4 The module boundary Module 4 is careful about
Module 4's own documentation was corrected mid-project on exactly this point, and the correction is preserved here because it matters for grading: **trainee cognitive analytics (reaction time, movement telemetry, stability/attention) are Module 3's contribution.** Module 4 stores and displays `cognitiveSummary` and `movementTrack`, and — where Module 3's live feed isn't wired into a given build — derives fallback proxies from the raw movement/reaction telemetry Module 3 does provide (`lib/cognitiveDerived.js`), clearly flagged "estimated" in the UI rather than presented as a real measurement. But Module 4 does not itself measure cognition. What genuinely *is* Module 4's own psychological contribution is the **hostage distress index** (§6.5), because it is derived from the hostage's in-mission state, not from cognitive tracking.

### 3.5 Hostage distress model
Each hostage state carries a `pct` (0–100 distress-index contribution), a plain-English description, and — as of the most recent revision — a per-state `justification` and cited `source` directly in the dashboard code (`HostageTab.jsx`), so the research grounding travels with the implementation rather than living only in a separate document:

| State | Distress % | Grounding |
|---|---:|---|
| Calm / Freed | 0 | Baseline / below arousal threshold |
| Follow | 10 | Escape route available → appraisal-driven de-escalation |
| Fearful / Scared | 33 | Fear-potentiated startle to a perceived, not-yet-imminent threat |
| Held | 40 | Captivity stressor (agency/escape removed) — design classification, not sound-driven |
| Panic | 66 | Active flight response to a confirmed, escalating threat |
| **Freeze** | **72** | **Deliberately scored above Panic** — tonic immobility, a reflexive shutdown under inescapable threat, is clinically shown to predict *worse* trauma outcomes than active panic despite looking calmer |
| Threatened | 75 | A directed vocal threat — scream/threat "roughness" (fast amplitude modulation) hits the amygdala's fear circuitry more directly than an equally loud gunshot |
| Wounded | 95 | Physical injury compounds fear with pain, crossing the injury threshold |
| Down | 100 | Terminal outcome |

The Freeze-above-Panic ordering is the one deliberate research-driven correction to an otherwise intuitive "louder/closer = worse" scale, and is Module 4's most strongly validated design choice (§6.1 rates it ✅ strongly validated). The sound/emotion research behind the full state model — gunfire sound-pressure level as a function of distance, the inverse-square propagation law, the Bracha (2004) five-stage acute-stress-response spectrum, and why a scream or vocal threat outranks a same-loudness gunshot — is compiled in `HOSTAGE_EMOTION_SOUND_RESEARCH.pdf`, with six original supporting charts safe for submission (not reproductions of copyrighted journal figures) in `Module4_Research_Figures.docx.pdf`. Note the module-boundary honesty point: the *sound-driven triggering* of state transitions is implemented in Module 2's `HostageController.cs`; Module 4 owns the resulting *distress-index scoring and visualisation* of whatever state the hostage is in.

🆕 **Known simplification — one identical reaction curve for every hostage.** The state machine applies the same thresholds (`panicDistance = 6 m`, `freezeRange = 5 m`, `freezeThreshold = 3 s`) to all hostages, so two hostages hearing the same shot at the same range always react identically. Real defensive response is **trait-differentiated**, and two independent findings — one visual, one auditory — converge on *how*: high *looming cognitive style* produces **generalized** rather than **selective** freezing (freezing at things a low-reactive person correctly ignores), and high-anxiety individuals respond with **equal latency to a quiet 60 dB and a loud 85 dB stimulus** while low-anxiety individuals discriminate between them. The shared mechanism is that **trait reactivity flattens the intensity–response relationship** — a difference in *kind*, not degree, and the auditory version is exactly the gunshot case.

A per-hostage **profile** design (**Weak / Normal / Brave**) is specified in full in **`HOSTAGE_PROFILE_RESEARCH.md`**. Its central design decision is worth noting here because it is counter-intuitive: **all three profiles keep identical distance thresholds**, and differ *only* in threat discrimination. That is not a simplification but a correctness requirement — §3.6 of that document shows the dB→distance chain **cannot** separate profiles at CQB range (the nearest published threshold, 137 dB, maps to 14.1 m, and gunfire exceeds it at every in-building distance), so per-profile distances would have been invented numbers wearing a citation. Spawn weights come from published trauma-trajectory prevalence, renormalised to ≈ **68 / 21 / 11**. The London-Syndrome finding — captivity **defiance is associated with worse survival, not better** — is folded into *Brave* as a small tail risk rather than given its own profile.

**Specified, not yet implemented** as of this report. §11 carries it as future work; that document's §8 states the falsifiable claim it would make evaluable, and §8.1 separates the parts of that claim that are genuine findings from the parts that are circular by construction.

### 3.6 Replay (2D + 3D)
- **2D top-down map** — every actor as a coloured, labelled dot over a scrubable timeline.
- **3D fly-around reconstruction (react-three-fiber)** — the mission rebuilt from the recorded position/rotation/state track, using real animated character models (state-driven idle/walk/alert/fire/dead clips baked from the game's own humanoid animations via `AnimationMode.SampleAnimationClip` into generic rotation-only clips, after UnityGLTF's default export collapsed six clips into two and made them three.js-incompatible), solid rooms with door openings drawn from the layout snapshot, a free orbit camera, and a follow-the-trainee camera.

### 3.7 Reporting, dashboard & closed loop
- **Standalone offline HTML report** (`HtmlTemplateBuilder`) — self-contained, no server needed.
- **Hosted web dashboard** (Next.js/React/Recharts) with tabs: **Summary** (five-score boxes with sub-parameters and expert comparisons, combat stats, cognitive analysis), **Timeline** (event timeline + activity-density chart + filterable log), **Incidents** (severity-ranked, explained), **Hostage** (distress curve + emotional journey narrative), **Movement** (body-telemetry benchmarks), **Workload** (SIM-TLX reasoning/prediction), **Replay** (2D + 3D) — plus a session list with aggregate statistics and a dedicated **Evaluation** page (§6.6–6.8).
- **Closed training loop** — the dashboard can launch a new mission on the headset (`ScenarioHttpServer`) and the headset uploads the AAR back: configure → train → review → repeat.

---

## 4. Engineering narrative — gaps, problems, root causes, solutions

The chronological log of every substantive defect and its fix, because the *process* is itself a report contribution — exactly the same convention Module 2's report uses.

**4.1 P0 — Quest build could not save a session, and showed no result screen.**
*Gap:* `SessionLogger.SaveToJson` wrote to `Application.dataPath`, which on Android/Quest points inside the read-only APK — the write threw, and because it ran with no try/catch *before* `OnSessionComplete` fired, the exception suppressed the upload, the HTML export, and the in-VR result screen simultaneously. *Root cause:* the editor never exposes this bug because `dataPath` is writable there. *Solution:* write to `Application.persistentDataPath`, wrapped in try/catch so a disk failure can never again suppress the result screen. **Fixed.**

**4.2 Replay recorder deleted whole actors when trimming.**
*Gap:* `ReplayRecorder.TrimIfTooLarge` dropped every odd-*index* frame, but frames are interleaved one-per-actor-per-tick — with an even actor count this erased half the actors from the replay entirely rather than halving time resolution; the intended backoff (`recordInterval *= 2f`) also silently did nothing because `RecordLoop` cached its `WaitForSeconds` once before the loop. *Solution:* drop every other *tick* (grouped by shared timestamp, so it survives actors registering mid-session), and rebuild `WaitForSeconds` each iteration. Verified with a headless test across 1–5 actors plus a mid-session join. **Fixed.**

**4.3 The trainee's accuracy score counted enemy gunfire.**
*Gap:* `totalShots` counted every `ShotFired` event with no filter on who fired, and the terrorist prefab's `GunFireDetector` raises `ShotFired` for every enemy round too — so a long firefight collapsed the trainee's own accuracy score toward zero regardless of how well they actually shot. Accuracy was 30% of the overall score, so this was not a cosmetic bug. *Solution:* the fix path Module 4 actually took went further than a same-event filter — it added **separate, correctly-named event types** (`EnemyShotFired`/`EnemyHitPlayer`) so enemy fire is a genuine independent metric (enemy hit-rate — an objective measure of AI competence, useful for Module 2's own ablation study) rather than noise inside the trainee's score.

**4.4 Friendly-fire penalty was linear and under-penalised the first incident.**
*Gap:* `safetyScore -= friendlyFire × 0.2` — a flat 20/40/60% penalty. *Research finding:* the Expert Value Table endorses a steep first-incident penalty with diminishing returns (0/39/63/78% for 0–3 incidents), fitting `1 − 0.61ⁿ`, backed by fratricide-seriousness research (modern trained units run <2% fratricide, so any single incident is already a serious red flag) — and the same saturating shape is independently supported by a validated CQB assessment instrument that deducts a flat, heavy penalty (−7 of a small point scale) per "mistake" such as shooting a non-combatant, rather than a linear scale. *Solution:* `safetyScore *= 0.61^friendlyFire` (and the same factor inside Operator Safety's weapon-discipline term). **Fixed.**

**4.5 Accuracy scored as if 100% hits were the expert target.**
*Gap:* `accuracyScore = hits/shots`, clamped, with no distance context — but the research shows even expert officers under stress hit only ~45–55% at under 3 m and ~10–15% at 3–7 m (NYPD SOP-9). A trainee scoring 40% in a stressful VR CQB scenario could be at real-world expert level, yet the raw ratio would call that "mediocre." *Solution:* `NPCHitBox` now captures trainee↔target distance at the instant of every hit; `PerformanceCalculator` averages the hit distances, looks up the NYPD-derived expected hit rate for that range band, and scores `accuracyScore = clamp01(hitRate / expectedRateAtRange)` — so a low raw number at real engagement range now correctly reads as expert-level. The five distance bands are used as a lookup, not a fitted curve, because the source data is itself bucketed field statistics and a smooth interpolation would imply precision the five points don't have. **Fixed.**

**4.6 `SLOW_RT` threshold was too lenient.**
*Gap:* Operator Safety's Threat Response term used `FAST_RT = 0.3s` (which matches Hick's Law's expert 1–2-choice reaction almost exactly) but `SLOW_RT = 1.5s`, when even an 8-choice reaction under Hick's Law is only ≈0.46s — meaning almost every real reaction bunched near the top of the scale regardless of actual speed. *Solution:* tightened to `SLOW_RT = 0.7s`. **Fixed.**

**4.7 Negligent discharge — a measurable player-behaviour discriminator was missing.**
*Gap:* the Expert Value Table's player-safety-behaviour benchmarks show negligent-discharge rate as one of the strongest expert-vs-novice discriminators (expert ≈17%, novice ≈61%, from qualification-failure studies) — this was entirely uncaptured. *Solution:* `GunFireDetector` now checks, at the instant of every shot, whether *any* living terrorist is visible at all (raycast, generous 30 m range, no aim/FOV requirement — this only flags shots at literally nothing); such shots are tagged `no_target_in_los` and folded into Operator Safety's weapon-discipline term (`friendlyFireFactor × negligentDischargeFactor`), anchored so the *expert* rate (17%) still scores full marks, not zero. **Fixed.**

**4.8 Speed scored every mission against the same flat clock, regardless of difficulty.**
*Gap:* `speedScore = 1 − duration/300` for every scenario, whether it had one room and one terrorist or eight rooms and six. *Solution:* target time is now normalized to scenario scale — `TargetTime = 30 + 5·rooms + 20·terrorists + difficultyBonus(AI tier)`, using the real room count from the layout snapshot, the terrorist count from distinct NPC actor IDs, and the AI tier as the difficulty proxy; falls back to the flat 300 s target when no scenario metadata is available (older sessions). **An earlier design draft proposed log-scaling the terrorist term per Hick's Law — this was deliberately rejected**: Hick's Law describes reaction time for one decision among *simultaneous* options, not the cumulative time cost of threats encountered *sequentially* over a multi-minute mission; applying it here would have misused a real law to justify an unrelated coefficient. The term stays linear. **The four coefficients (30/5/20/step) are explicitly design defaults, not independently sourced** — the *shape* (total task time ≈ sum of subtask times + fixed overhead) follows GOMS/Keystroke-Level Model theory, which is real; the exact numbers are not, and are reported as such rather than dressed up as research-calibrated.

**4.9 Operator Safety — a second safety axis most tactical AAR tools omit.**
*Gap:* the pre-existing `safetyScore` answers "was the hostage kept safe?" and says nothing about whether the *trainee* survived and behaved safely — a trainee could rescue the hostage while standing in the open and surviving only by luck. *Solution:* a distinct **Operator Safety** score (Survivability, Exposure Control, Weapon Discipline, Threat Response, equally weighted, capped at 0.20 if the trainee was downed) was designed against a **validated, peer-reviewed CQB assessment instrument** (Nieuwenhuys et al. 2024, inter-rater ICC = .834), whose five scales map directly onto four of the five metrics chosen here (§6.4). The exact weights and thresholds are explicitly documented as design defaults, not research-derived numbers — the *instrument's equal-item structure* justifies the equal-weight default, and its flat heavy per-mistake penalty justifies the saturating friendly-fire curve, but the literature does not supply the specific numeric weights, and this report does not claim otherwise.

**4.10 Room labelling — events said "in room unknown."**
*Gap:* most gameplay events were raised without a `roomId`. *Solution:* `RoomLocator` resolves the room from the event's world position against the captured layout snapshot, so incidents and the timeline can say where something happened.

**4.11 A recurring VR regression — the XR Device Simulator.**
The XR Device Simulator must never be left in the scene: it runs on Quest builds too, and deletes the real HMD reference, killing head tracking and locomotion for the whole session. **Fixed** and documented so it does not recur.

**4.12 Entry room could be interior, spawning the trainee inside a sealed building.**
*Gap:* the entry room is the BFS origin for scenario generation and is frequently an interior room, with no exterior wall to spawn the trainee outside of. *Solution:* `SceneBuilder` now resolves its own exterior entrance so the trainee always spawns outside a building rather than inside a sealed room. **Fixed.**

**4.13 MongoDB "could not connect."** Root cause was neither the connection string nor the network whitelist — it was `lib/mongodb.js` caching a **rejected** connection promise, so every subsequent request failed near-instantly against the cached rejection rather than retrying. **Solution:** never cache a failed connection promise; check `readyState` before reusing the cached one. **Fixed.**

**4.14 Committed database credentials (security).**
*Gap:* `sentinels-aar/.env.local`, containing the MongoDB connection string with an embedded username/password, was git-tracked — `.gitignore` excluded several `.env.*.local` variants but not `.env.local` itself, the one file that actually existed, and every API route additionally set a permissive CORS origin. *Solution:* the credential was rotated (not merely removed — removing a committed secret does not un-leak it, since it remains in git history) and the environment file kept untracked going forward. **Fixed.**

**4.15 Merge-conflict resolution after a `dev-new` branch merge.** Seven files conflicted (`PerformanceCalculator.cs`, `PerformanceSummary.cs`, `Session.js`, `SummaryTab.jsx`, and the Module-2 evaluation route/components). In every case `dev-new`'s side was strictly older code with no unique content the feature branch didn't already have — resolved in favour of the feature branch throughout, with a follow-up fix for a stale `.next` build cache that briefly 500'd the dashboard after the large multi-file merge (a known failure mode: kill the dev server, delete `.next`, restart clean).

---

## 5. The five scores (what is being evaluated)

Unlike Module 2, Module 4 does not itself expose an independent variable to manipulate — it is the *measurement instrument*, evaluated the way any measurement instrument is: for construct validity (does it agree with published values?), discriminant validity (does it separate conditions that should differ?), and reliability (is it consistent?). The instrument itself is five scores, computed by `PerformanceCalculator.cs`:

| Score | What it answers | Formula (current) |
|---|---|---|
| **Hostage Safety** | Was the hostage kept safe? | `hostagesSaved/hostagesTotal × 0.61^friendlyFire` |
| **Accuracy** | How well did the trainee shoot? | `clamp01(hitRate / expectedHitRateAtRange)` — NYPD SOP-9 distance bands |
| **Speed** | How quickly did the trainee work? | `1 − clamp01(duration / (2×targetTime))` — scenario-normalized target |
| **Operator Safety** | Did the trainee survive and act safely? | `0.25·Survivability + 0.25·ExposureControl + 0.25·WeaponDiscipline + 0.25·ThreatResponse`, capped at 0.20 if downed |
| **Overall** | Composite mission score | `0.4·safety + 0.3·accuracy + 0.2·speed + 0.1·(success?1:0)` |

Each is decomposed into sub-parameters in the dashboard's Summary tab (mirroring the structure of this table), and — where a genuine published benchmark exists — compared against it directly in the UI, novice-to-expert, with a verdict chip (Expert/Proficient/Developing/Novice). §6.1 is the full construct-validity audit of every one of these formulas against the Expert Value Table.

---

## 6. Evaluation methodology (as built)

### 6.1 Construct validity vs the Expert Value Table

The **Expert Value Table** is a curated synthesis of citable research values (NYPD SOP-9 hit rates, Hick's Law reaction times, RAND hostage-rescue outcome rates, SUDS/Polyvagal distress theory, fratricide and negligent-discharge studies) used to check whether Module 4's formulas agree with what the science says they should. This requires **no human subjects at all** — it evaluates the formula, not a study sample — and is fully documented in `MODULE4_EVALUATION.md`. Summary of the audit, current as of this report:

| Module 4 element | Verdict | Grounded in |
|---|---|---|
| Distress states — order & values | ✅ Validated | SUDS / Polyvagal theory |
| Hostage rescue = binary per-hostage outcome | ✅ Validated | RAND hostage-rescue outcomes (professional tactical rescue: 70–90% survival) |
| Friendly-fire penalty (`1 − 0.61ⁿ`) | ✅ Matches research-endorsed curve | Expert Value Table Piece C; fratricide studies |
| Accuracy (distance-normalized) | ✅ Distance-aware, matches NYPD SOP-9 bands | NYPD SOP-9 |
| Threat response `FAST_RT`/`SLOW_RT` | ✅ Both anchors match Hick's Law | Hick's Law choice-reaction time |
| Negligent discharge (17%/61%) | ✅ Captured, folded into Weapon Discipline | Qualification-failure / player-safety studies |
| Speed — scenario-normalized target | ⚠️ Shape (GOMS/KLM) grounded; coefficients are design defaults | No published benchmark exists for hostage-rescue mission time |
| False negatives, trigger discipline, stress-accuracy drop | ❌ Not captured | Trigger discipline specifically not feasible — VR controllers register only a discrete "shot fired" on full pull, no partial-trigger telemetry exists to log |

**Headline finding:** every scoring element the Expert Value Table supplied a hard target value for has been calibrated to it. The one remaining gap (Speed) has no research value to calibrate against for this specific task, so it was normalized for scenario complexity rather than presented as research-calibrated — an explicit, reported limitation rather than a silently invented number.

### 6.2 Movement-telemetry validation methodology

The dashboard's Movement tab compares six body-telemetry metrics (distance moved, average/peak speed, time crouched, head-scanning angle/speed, weapon-in-hand time, hand travel) plus a four-channel reaction-time breakdown against literature-derived reference bands, using an explicit **confidence-tier** system (`MODULE4_MOVEMENT_VALIDATION_METHODOLOGY.md`) rather than presenting every band with equal authority:

- **Tier A** — a direct empirical figure from a study measuring materially the same task (e.g. weapon-raise response time from Nieuwenhuys et al. 2022's police-cadet shoot-scenario study).
- **Tier B** — a literature proxy applied to a related but different task (e.g. general adult gait-speed norms applied to tactical movement pace), explicitly flagged as a proxy.
- **Tier C** — no quantified literature exists; the trainee's raw value is shown with an honest "not literature-validated" label rather than a fabricated range.

The nearest published precedent for treating these six metrics as a valid expert/novice axis at all is a CQB-capability study that scores trainees on tactical behaviour, weapon handling, gaze behaviour, response time, and mistakes, and finds these dimensions separate special-forces from non-specialised soldiers with large effect sizes (Cohen's d −1.10 to −1.99) — the same instrument §6.4's Operator Safety score is designed against.

### 6.3 Workload (SIM-TLX) reasoning & prediction methodology

The Workload tab explains *why* a session's SIM-TLX composite scored the way it did (grounded in Cognitive Load Theory, Multiple Resource Theory, and Attentional Control Theory as the explanatory mechanism, not a numeric formula) and *predicts* a performance zone from the overall workload score, checked against that session's own outcome data rather than left as an unfalsifiable claim (`MODULE4_WORKLOAD_REASONING_METHODOLOGY.md`). The zone thresholds are anchored to a real measurement of the inverted-U stress-performance relationship in elite competitive shooters, where performance peaked at moderately (not maximally) decisive competition stages — the paper's own hedge, that the high-stress performance drop is "far less dramatic than the catastrophe model predicted," is carried into the UI's wording rather than overstated.

### 6.4 Operator Safety — research grounding

Full detail in `PLAYER_SAFETY_SCORE.md`. Operator Safety's four components map directly onto four of the five scales of a **validated, peer-reviewed CQB tactical-performance instrument** (Nieuwenhuys et al., *Military Psychology*, 2024 — 5 scales, 9 items, inter-rater ICC = .834):

| Operator Safety metric | CQB instrument scale |
|---|---|
| Exposure Control | Scale 1 — Tactical behaviour (angle coverage, withdrawal from danger zone) |
| Weapon Discipline | Scale 2 — Weapon handling + Scale 5 — Mistakes (flat penalty per mistake, e.g. −7 for shooting a non-combatant) |
| Threat Response | Scale 4 — Response time |
| Survivability | Outcome of Scales 1–4 combined; also captured directly as the `player_down` end reason |
| *(not covered)* | Scale 3 — Gaze behaviour — needs eye tracking; an honest, stated gap |

The instrument's structure justifies two specific design choices rather than leaving them arbitrary: its **equal-item** scoring (it sums equal items, it does not weight scales) supports the equal-weight default over hand-picked weights, and its **flat heavy per-mistake penalty** supports the saturating friendly-fire/negligent-discharge curve over a linear one. The exact numeric weights, the exposure target, and the RT thresholds are explicitly reported as design defaults (§4.9) — the recommended path to strengthen this further is either an equal-weight sensitivity analysis (showing the trainee *ranking* is stable as weights vary) or expert-elicited weights (AHP/Delphi with CQB-experienced instructors), both documented as future work.

### 6.5 Hostage distress — sound/emotion research grounding

Full detail in `HOSTAGE_EMOTION_SOUND_RESEARCH.pdf` and `Module4_Research_Figures.docx.pdf`. Three findings drove the distress-index design (§3.5):
1. **Freeze is the reflexive first-stage response to a sudden threat sound, not an escalation past Fear** (Bracha 2004's revision of "fight-or-flight" into Freeze→Flight→Fight→Fright→Faint) — and peritraumatic tonic immobility (Freeze) is clinically shown to predict *worse* trauma outcomes than active Panic, which is why Freeze is scored **above** Panic (72% vs 66%) despite looking outwardly calmer.
2. **Gunfire sound-pressure level is fixed at the source (~155–165 dB at 1 m for a rifle) and falls off by the standard inverse-square law** (−6 dB per doubling of distance), confirmed against a real U.S. Army field measurement (an M-72 LAW dropped from 179 dB at the firer's position to 161 dB at 8 m — an 18 dB loss, matching the predicted falloff).
3. **A directed vocal threat or scream outranks a same-loudness gunshot as a fear trigger**, because screams carry acoustic "roughness" (30–150 Hz amplitude modulation, far faster than normal speech's ~4–5 Hz) that activates the amygdala's fear circuitry more directly — the basis for scoring `Threatened` (75%) above gunfire-driven `Panic` (66%).

An explicit evidence-quality caveat is carried from the source research into the design: low-frequency/infrasound "dread" effects are frequently cited in sound-design contexts but remain scientifically contested, and were treated as *not* load-bearing for any Module 4 scoring decision.

### 6.5b Hostage distress — trait/profile research grounding (specified, not implemented)

The sound research above covers the **stimulus** side (*how loud/close must a shot be to change state?*). The **individual-differences** side (*why do two people hearing the same shot react differently?*) is compiled separately in **`HOSTAGE_PROFILE_RESEARCH.md`**, summarised in §3.5. Its load-bearing findings:

1. **Trait-level defensive response is an established construct** — Reinforcement Sensitivity Theory's Fight-Flight-Freeze System is a *personality* subsystem, and a validated instrument (the Fight/Flight/Freeze Questionnaire, *Cognitive Behaviour Therapy* 2015) measures the trait directly.
2. **Vulnerability changes response *in kind*, not degree — and this converges across two senses.** *Visual:* high looming cognitive style produces **generalized** freezing (freezes at anything approaching, including non-threats) vs. **selective** freezing in low-looming individuals; *F*(1,80) = 6.50, *p* = 0.01. 🆕 *Auditory:* high-anxiety individuals respond with **equal latency to 60 dB and 85 dB** stimuli while low-anxiety individuals discriminate. Two independent groups, two modalities, one mechanism — **trait reactivity flattens the intensity–response relationship**. The auditory result is the more directly applicable one, since the hostage model's primary stimulus is a gunshot.
3. **Realistic profile prevalence is not uniform** — published trauma-trajectory data gives resilience 65.7% / recovery 20.8% / chronicity 10.6%; renormalised over the three modelled profiles (delayed-onset is excluded and disclosed) the spawn distribution is ≈ **68 / 21 / 11**, not equal thirds.
4. **Captivity defiance is associated with *worse* outcomes** (London Syndrome) — the one place the research contradicts the intuitive design. With three profiles this is folded into *Brave* as a small `defianceChance` tail risk, preserving the counter-intuitive AAR lesson (*the calmest-looking hostage can be the one at greatest risk*) without a fourth profile.
5. 🆕 **Distances are deliberately identical across profiles.** The dB→distance chain **cannot** separate profiles at CQB range: consecutive published thresholds map to 1 m → 14 m → 158 m → 2.2 km, and gunfire exceeds the 137 dB Fear/Panic threshold at *every* in-building distance. Per-profile distances would therefore be invented values presented as derived ones — the failure mode §8 of `MODULE4_EVALUATION.md` already flags for the Speed coefficients. Profiles differ **only** in `threatDiscrimination`, which is the parameter the research actually supports.

**Status:** design specification only. This is *not* implemented, and no profile data exists — nothing in this subsection may be reported as a result. When it is implemented, `HOSTAGE_PROFILE_RESEARCH.md` §8.1 governs how the results are reported: **verification** claims (Weak reaches higher peak distress — circular, follows from the authored parameter) must be labelled as implementation checks, while only the **discovery** claims (profile changes trainee rescue time, Hostage Safety score, and mission-outcome distribution — none of which are set by the profile definition) may be presented as findings.

### 6.6 Discriminant validity — Module 4's own scores between trainees

Construct validity (§6.1) checks the formula against research; it does not check whether the score actually *behaves* correctly when real people play it. Module 4's own five scores (Overall, Hostage Safety, Accuracy, Speed, Operator Safety) appear on the dashboard's Evaluation page under a dedicated **"Module 4 — Score Validity"** tab.

**A deliberate correction to an earlier design.** These scores were initially run through the same tier-by-tier Friedman/Wilcoxon test as Module 2 — comparing Basic vs. Intermediate vs. Advanced. That was the wrong frame: **AI difficulty is Module 2's independent variable, not Module 4's.** Module 4 is a measurement instrument; nothing about its scores is a claim about enemy AI. The tab therefore now pools **all of a player's sessions regardless of AI tier**, and asks two properly-separated questions:

| Section | Question | Method |
|---|---|---|
| **Per-player average (all levels combined)** | What does each trainee average, and where does that sit against the published expert/novice values? | Descriptive, with Expert/Novice reference rows shown explicitly above the trainee rows |
| **vs Expert Value (all sessions pooled)** | Does the trainee population differ from the published expert constant? | **One-sample Wilcoxon signed-rank** (α = 0.05, effect size *r* = \|z\|/√n) |
| **ANOVA Table (between players)** | Does the score differ *between trainees* by more than each trainee's own session-to-session variability explains? | **One-way ANOVA** — Source / SS / df / MS / F / p, with player as the grouping factor and their own sessions as replicates |

All three reuse the same dependency-free statistics engine (`sentinels-aar/lib/stats.js`), extended with `oneWayAnova()` (exact F-distribution *p* via the regularized incomplete beta function, hand-verified against worked examples) and `oneSampleWilcoxon()`. Module 2's tier-based Friedman/Wilcoxon panel is untouched and remains correct for Module 2's own question. The AI tier is not referenced anywhere in the Module 3 or Module 4 tabs.

### 6.7 Reliability — repeated-play testing

Repeating a play answers a different question from either test above — **is the score consistent for the same person**, not *does it differ between people* or *from expert*. The dashboard's **"Repeated-Play Reliability"** section reports each of a player's sessions across the five scores plus the mean and standard deviation — tight clustering (low SD) supports reliability; large swings do not, and are reported honestly rather than smoothed over. A companion **"vs Expert Benchmarks (averaged per player)"** section averages a person's sessions first (to reduce single-session noise) before comparing Hostage Safety and Accuracy — the two scores with an independent published benchmark — against the novice/expert bands; Speed and Operator Safety are explicitly shown without a benchmark bar rather than compared against a source that doesn't exist. Like §6.6, this groups by player only, with no AI-tier split.

### 6.8 Preliminary results (real pilot data, as of submission)

> **Status note, and the real/synthetic boundary this report insists on.** Full-sample recruitment for a real evaluation study has not yet begun at the time of writing. The Module 4 scoring pipeline, the construct-validity audit (§6.1), and all UI mechanisms in §6.6–6.7 are built and verified end-to-end — but they have only ever been exercised against **one genuine human-played dataset**: participant `P0001`, self-piloted by the project author.
>
> **Every other `playerId` in the evaluation database is a synthetic development placeholder.** At the time of writing the `sessions` collection holds 45 tagged sessions across `P0001`–`P0011`; all rows except `P0001`'s were generated during development, at the project author's explicit request, **purely to verify the dashboard UI renders correctly with more than one row of data** (multi-player dropdowns, averages tables, per-player ANOVA groups, reliability and benchmark panels). **These rows are not real participants and must not be cited as evaluation findings in a submitted report.** They were never claimed otherwise during development, and the boundary is stated here explicitly so it survives into any report generated from this document. Because the synthetic set has grown during development and may grow again, treat the rule — *only `P0001` is real* — as authoritative rather than the specific ID range. §10 item 2 carries this as a standing data-hygiene risk, and §11 recommends enforcing it at the schema level rather than by convention.

**Participant P0001 — self-piloted by the project author, four sessions across the three AI tiers (one tier played twice):**

| Session | Reaction time (s) | Accuracy (raw) | Duration (s) | Overall | Outcome |
|---|---:|---:|---:|---:|---|
| Basic | 0.957 | 47.4% (9/19) | 129.4 | 0.756 | Success |
| Intermediate | 0.630 | 64.3% (9/14) | 85.6 | 0.836 | Success |
| Advanced (attempt 1) | — | 0 shots fired | 10.0 | 0.193 | **Failure** — killed almost immediately |
| Advanced (attempt 2) | 0.327 | 0 shots fired | 105.5 | 0.130 | **Failure** |

**Recorded before the current-generation scoring model (§4.5–§4.9)** — this session predates distance-aware accuracy, the saturating friendly-fire curve, negligent-discharge tracking, and Operator Safety entirely, so `operatorSafetyScore` and the distance-normalized accuracy fields are simply absent for this participant (`null`, not zero — recorded as such by design, not smoothed over). The raw hit ratios above are the pre-calibration numbers, shown for transparency about what the actual database contains, not recomputed retroactively.

**What this genuinely supports:**
1. Reaction time moved in the theoretically expected direction as difficulty rose (0.957 s → 0.630 s across the two sessions where a reaction was recorded) — faster response demanded, faster response given.
2. **An honest, informative divergence, reported rather than smoothed over:** the participant succeeded at Basic and Intermediate but failed *both* attempts at Advanced, the second time without firing a single shot in 105 s — the harder AI was harder to beat, not just harder to feel good about. This is exactly the finding the Operator-Safety/Hostage-Safety split (§4.9) exists to surface, though the operator-safety fields themselves are not populated for this pre-feature session.
3. **No significance or reliability claim is made from this alone.** §6.6's Friedman/Wilcoxon panel requires ≥3 participants who each completed all three tiers to produce a meaningful result; at the time of writing, one participant has a complete triad. §6.7's reliability panel requires the *same* tier played more than once by the *same* real participant with a genuine repeat-play intent; `P0001`'s two Advanced attempts are a real repeat, but n=2 with no accuracy data (zero shots both times) is descriptive evidence only, not an inferential claim.
4. **Both dashboard mechanisms (§6.6, §6.7) are fully built and will compute automatically the moment real multi-participant data exists** — this section will be superseded by that live output, exactly as Module 2's own report describes for its own significance panel, rather than hand-edited.

---

## 7. Data pipeline, integrity and reliability engineering

**Flow:** mission ends → `SessionLogger.EndSession()` builds a `SessionSummary` (events, performance, cognitive summary, hostage history, replay frames, layout, movement track) → `PerformanceCalculator.Calculate()` + `ComputeOperatorSafety()` → JSON written to `persistentDataPath` → uploader POSTs to `/api/sessions` → MongoDB → dashboard reads it.

**Reliability engineering.** The same gzip + retry-with-backoff + on-disk queue pattern Module 2's report documents for its own uploads applies identically here, since both modules share the one `SessionSummary` upload path — a session is never lost even if the headset is quit mid-upload, and idempotency is guaranteed server-side by an upsert on `sessionId`.

**Schema evolution without breaking old sessions.** Every new field added during this project (distance-aware accuracy fields, scenario-normalized speed fields, Operator Safety and its five sub-fields, negligent-discharge fields) was added as an **optional** field with a documented "null/absent on older sessions" convention, both in the Mongoose schema and in every dashboard component that reads it — so historical sessions (like `P0001` above) continue to render correctly rather than breaking when the schema grows. This is why §6.8 can report `P0001`'s data honestly as partially populated rather than needing to discard it.

**Portability.** The dashboard is deployed on Vercel and shares one MongoDB with local development, so a play from any PC (editor) or Quest, anywhere with internet, lands in the same database and appears on both the local and hosted dashboard.

---

## 8. Dashboard implementation (files)

| Area | Files |
|---|---|
| Scoring & operator safety | `Assets/Module4/Analysis/PerformanceCalculator.cs`, `Assets/Module4/Data/PerformanceSummary.cs` |
| Distance/negligent-discharge capture | `Assets/Scripts/NPC/NPCHitBox.cs`, `Assets/Scripts/Detectors/GunFireDetector.cs`, `Assets/Scripts/Events/ScenarioEvent.cs` |
| Session lifecycle & upload | `Assets/Module4/Logging/SessionLogger.cs`, `Assets/Module4/Data/MissionEvent.cs`, `Assets/Module4Integration/Module4Bridge.cs`, `Assets/Module4Integration/Module4SessionController.cs` |
| Layout / room labelling | `Assets/Module4/Data/LayoutSnapshot.cs`, `Assets/Module4Integration/RoomLocator.cs` |
| Incident extraction | `Assets/Module4/Analysis/IncidentExtractor.cs` |
| Offline HTML report | `Assets/Module4/Export/HtmlTemplateBuilder.cs` |
| Summary tab (5-score boxes + expert comparison) | `components/dashboard/SummaryTab.jsx`, `lib/scoreBreakdowns.js`, `lib/expertBenchmarks.js` |
| Hostage distress | `components/dashboard/HostageTab.jsx` (`STATE_INFO`) |
| Movement validation | `components/dashboard/MovementTab.jsx`, `lib/movementBenchmarks.js` |
| Workload reasoning | `components/dashboard/SimTlxTab.jsx`, `lib/workloadReasoning.js`, `lib/simTlx.js` |
| 3D / 2D replay | `components/dashboard/Replay3D.jsx`, replay tab components |
| Evaluation page (Module 2/3/4 tabs, stats, reliability, benchmarks) | `components/evaluation/EvaluationClient.jsx`, `app/api/evaluation/route.js`, `lib/stats.js` |
| Ingest | `app/api/sessions/route.js`, `lib/models/Session.js` |

---

## 9. Comparative analysis — how Module 4 measures up

Full detail and citations in `MODULE4_RESEARCH_REVIEW.md`. Positioning summary:

> Module 4 implements the core, evidence-based feature set of a modern VR-training AAR system — automated zero-instrumentation logging, a severity-ranked incident timeline, quantitative performance scoring across five distinct dimensions, friendly-fire and negligent-discharge detection, and multi-perspective (2D + free-orbit 3D) replay. It is delivered as an open, self-hosted, offline-capable dashboard rather than a costly enterprise product.
>
> Its **distinctive contributions** relative to commercial and academic AAR systems are (a) a fully automatic AAR generated from one event bus with no per-system instrumentation, (b) **psychological after-action review of the hostage** — a distress index and emotional journey — alongside tactical review of the trainee, which is uncommon in tools that focus purely on the operator's tactics, (c) a **second, independent safety axis** (Operator Safety) most tactical AAR tools collapse into the mission outcome, and (d) a closed procedural-generation → training → review loop with Module 1.
>
> Its **principal gaps** relative to commercial state-of-the-art (Operator XR, Thales Gladiator, InVeris fats® AR) are gaze/eye-tracking analytics, team-level and communication AAR, facilitator annotation tools, and AI-generated coaching — all explicitly documented rather than silently absent.

| Capability | Commercial military AAR | AAR research says | **Module 4** |
|---|---|---|---|
| Automated event logging | ✅ Standard | ✅ Essential | ✅ Universal event-bus bridge |
| Friendly-fire / negligent-discharge flagging | ✅ (Operator XR) | ✅ | ✅ Saturating penalty + distinct negligent-discharge metric |
| Record & replay | ✅ | ✅ Critical | ✅ 2D map **and** 3D reconstruction |
| Distance-aware accuracy scoring | Partial | — | ✅ NYPD SOP-9 normalized |
| **Operator (trainee) safety, distinct from hostage outcome** | Rare | Valued | ✅ **Validated-instrument-grounded, distinctive** |
| **Hostage psychological/emotional tracking** | ✗ | Empathy/perspective valued | ✅ **Distress index + emotional journey — distinctive** |
| Construct-validity audit against published benchmarks | Rare, undisclosed | ✅ Recommended | ✅ Documented, per-formula |
| Eye / gaze / head / muzzle analytics | ✅ (fats, Operator XR) | Valued | ✗ Gap |
| Team / multi-trainee AAR | ✅ | ✅ | ✗ Single trainee |
| AI-generated coaching | ✅ (Thales) | Emerging | ✗ Not yet |
| Cost / accessibility | ✗ Enterprise | — | ✅ Open, self-hosted |

---

## 10. Threats to validity and limitations

1. **Sample size.** The evaluation database contains one genuine multi-tier participant (§6.8) at the time of writing. Every result reported here is descriptive/proof-of-concept, not inferentially significant — the Friedman/Wilcoxon panel (§6.6) requires ≥3 complete-case participants and will activate automatically once that threshold is reached.
2. **Synthetic development data exists alongside real data in the same database** and must be excluded from any cited result (§6.8). Only `P0001` is a real human-played dataset; every other `playerId` present is a development placeholder, and that set has grown during development (`P0001`–`P0011` at time of writing, 45 tagged sessions). This is a live data-hygiene risk any future team member extending this database must know about, and the reason §11 recommends a schema-level `isSynthetic` flag rather than relying on a playerId convention. **Any statistic computed over "all sessions" — including §6.6's pooled-vs-expert test and ANOVA table — is currently computed over predominantly synthetic data and is therefore a mechanism demonstration, not a finding.**
3. **Construct validity, not transfer.** Module 4's scores are calibrated to agree with published real-world benchmarks; no claim is made that a high Module 4 score predicts real-world tactical performance without a transfer study.
4. **Speed has no published benchmark for this specific task** — normalized for scenario complexity, not calibrated to research, and reported as such rather than dressed up.
5. **Some sub-metrics are honestly incomplete.** Trigger discipline is not merely unimplemented but currently infeasible with this project's VR instrumentation (no partial-trigger telemetry); gaze behaviour (the one CQB-instrument scale Operator Safety does not cover) needs eye tracking the current headset target does not use for this purpose.
6. **The pre-feature session in §6.8 cannot demonstrate Operator Safety, negligent discharge, or distance-normalized accuracy at all** — those fields are genuinely absent from data recorded before those features existed, not zero-filled.

---

## 11. Future work

- **Recruit a real multi-participant sample** (the same pool Module 2's ablation study needs — no separate recruitment burden) so §6.6's pooled-vs-expert and ANOVA panels and §6.7's reliability panel produce an inferential result rather than a descriptive one.
- 🆕 **Trait-differentiated hostage profiles** — implement the **Weak / Normal / Brave** model specified in **`HOSTAGE_PROFILE_RESEARCH.md`** (§3.5, §6.5b). Requires no FSM rewrite and adds **one** parameter, `threatDiscrimination` — the distance thresholds stay identical across profiles (that document's §3.6 explains why per-profile distances are not derivable and would be invented). Creates a **pre-registered falsifiable claim** testable with the `oneWayAnova()` already built, substituting profile for player as the grouping factor; its §8.1 splits that claim into **verification** (circular by construction — report as an implementation check) and **discovery** (does profile change *trainee* rescue time, Hostage Safety score, and mission-outcome distribution — non-circular, and the part to headline). Its §7.4 also specifies why the hostage state label belongs in the **AAR replay and debug views only**, never in the trainee's live VR view (a floating "PANIC" tag is an unrealistic cue that would let a trainee read state instead of learning to read behaviour).
- **Per-band accuracy rate, not just distribution** (§4.5) — the Summary tab's "Hits by engagement range" table currently shows *where a trainee's hits landed* across the five NYPD SOP-9 bands, because only hits carry a recorded distance (a miss never reaches `NPCHitBox`, so it has no measurable range). Capturing a per-shot distance at fire time — not just at impact — would upgrade this from a hit *distribution* to a true hit *rate per band*, each directly comparable to that band's published expert rate.
- **Expert-elicited or sensitivity-analysed weights** for Operator Safety and the Overall composite (§4.9, §6.4), upgrading the weighting scheme from "documented design default" to "empirically justified."
- **A correlation/ICC study against the validated CQB instrument itself** — have 1–2 CQB-experienced raters score recorded sessions on the instrument's safety-relevant scales, compute Module 4's automated Operator Safety score for the same sessions, and report the agreement (the source instrument itself reports ICC = .834 between human raters as its own benchmark for "good").
- **Gaze/attention analytics** via headset eye tracking, closing Operator Safety's one uncovered CQB-instrument scale and matching commercial state of the art.
- **Explicit tagging of development/synthetic data** at the schema level, so the real/synthetic boundary this report currently maintains by convention is enforced structurally instead.

---

## 12. References (IEEE)

[1] G.R. Price, "Weapon Noise Exposure of the Human Ear Analyzed with the AHAAH Model," U.S. Army Research Laboratory, 2022.

[2] "Noise of military weapons, ground vehicles, planes and ships," *J. Acoustical Society of America*, vol. 146, no. 5, p. 3832, 2019.

[3] "Acoustic and psychoacoustic analysis of the noise produced by police force firearms," *PMC9450760*, 2022.

[4] Engineering Toolbox, "Sound Propagation — the Inverse Square Law." [Online]. Available: https://www.engineeringtoolbox.com/inverse-square-law-d_890.html

[5] World Health Organization, *Environmental Noise Guidelines for the European Region*, WHO Regional Office for Europe, 2018.

[6] H.S. Bracha, "Freeze, Flight, Fight, Fright, Faint: Adaptationist Perspectives on the Acute Stress Response Spectrum," *CNS Spectrums*, vol. 9, no. 9, pp. 679–685, 2004.

[7] "Fear-potentiated startle: a review," *PMC6162305*.

[8] "Blast exposure impairs sensory gating: acoustic startle and event-related potentials," *PMC6387566*.

[9] Iowa State University, "Active shooter simulations: an agent-based model of civilian response," M.S. thesis, Dept. Industrial and Manufacturing Systems Eng., 2014.

[10] L. Arnal, B. Flinker, A. Kleinschmidt, A.-L. Giraud, and L. Mesgarani, "Human Screams Occupy a Privileged Niche in the Communication Soundscape," *Current Biology*, vol. 25, no. 15, pp. 2051–2056, 2015.

[11] "Tonic immobility predicts poorer PTSD recovery," *ScienceDirect*, 2019.

[12] "Peritraumatic tonic immobility and PTSD symptom severity in Brazilian police officers: a prospective study," 2021.

[13] M.M. Bradley and P.J. Lang, "The International Affective Digitized Sounds (2nd Edition; IADS-2): Affective ratings of sounds and instruction manual," Univ. of Florida, Tech. Rep. B-3, 2007.

[14] R.W. Bohannon and A. Williams Andrews, "Normal walking speed: a descriptive meta-analysis," *Physiotherapy*, vol. 97, no. 3, pp. 182–189, 2011.

[15] Scholarpedia, "Human saccadic eye movements." [Online]. Available: http://www.scholarpedia.org/article/Human_saccadic_eye_movements

[16] "Evidence of elevated situational awareness for active duty soldiers during navigation of a virtual environment," *PMC11086823*.

[17] Force Science Institute, "Action vs. reaction: the shoot-first fallacy," 2011.

[18] "Comparison between Auditory and Visual Simple Reaction Times," *SCIRP*, 2013.

[19] A. Nieuwenhuys, T. Weijer, T. van der Bijl, and R. Boddy, "Shoot or Don't Shoot? Tactical Gaze Control and Visual Attention Training Improves Police Cadets' Decision-Making Performance in Live-Fire Scenarios," *Frontiers in Psychology*, vol. 13, art. 833165, 2022.

[20] ALERRT Center / VirTra, "Time Analysis of a Certified Peace Officer's Draw-and-Fire Response Time," Texas State Univ., 2021.

[21] A. Nieuwenhuys et al., "Predicting closed-quarters-battle capability — examining the influence of personality, attentional ability, 2D:4D-ratio and mindfulness on tactical performance," *Military Psychology*, 2024.

[22] "Observational Behavior Assessment for Psychological Competencies in Police Officers," *Frontiers in Psychology*, art. PMC7959728, 2021.

[23] U.S. Office of Justice Programs / National Institute of Justice, "Developing a Common Metric for Evaluating Police Performance in Deadly Force Situations," Final Report, NCJRS.

[24] Systems and methods for vehicle survivability planning, U.S. Patent 9 240 001, Jan. 19, 2016.

[25] Chase Tactical, "Best Practices for Law Enforcement Tactical Training." [Online]. Available: https://www.chasetactical.com/guides/best-practices-for-law-enforcement-tactical-training

[26] J. Sweller, "Element Interactivity and Intrinsic, Extraneous, and Germane Cognitive Load," *Educational Psychology Review*, vol. 22, no. 2, pp. 123–138, 2010.

[27] C.D. Wickens, "Multiple Resources and Mental Workload," *Human Factors*, vol. 50, no. 3, pp. 449–455, 2008.

[28] X. Li et al., "Evaluating mental workload during multitasking in simulated flight," *Brain and Behavior*, art. PMC9014989, 2022.

[29] D.J. Harris, M.R. Wilson, and S.J. Vine, "Development and validation of a simulation workload measure: the simulation task load index (SIM-TLX)," *Virtual Reality*, vol. 24, no. 4, pp. 557–566, 2020.

[30] M.W. Eysenck, N. Derakshan, R. Santos, and M.G. Calvo, "Anxiety and Cognitive Performance: Attentional Control Theory," *Emotion*, vol. 7, no. 2, pp. 336–353, 2007.

[31] "The inverted-U relationship between stress and performance in elite shooting," *PMC12749011*.

[32] "Predicting Cognitive Load and Operational Performance in a Simulated Marksmanship Task," *PMC7350508*.

[33] Intel Market Research, "After Action Review Software for Training Market," Market Report, 2026.

[34] Operator XR, "Military After-Action Review Module." [Online]. Available: https://operatorxr.com/military-after-action-review

[35] Halldale Group, "AI Platform Speeds Up Military Training Debriefs (Thales Gladiator)," 2024.

[36] MÄK, "MÄK ONE / VR-Engage." [Online]. Available: https://www.mak.com/mak-one/apps/vr-engage

[37] InVeris Training Solutions, "fats® AR — Multi-Viewpoint AAR." [Online]. Available: https://www.inveristraining.com/virtual-training/military-virtual-tactical-small-arms-training-marksmanship/fats-ar/

[38] Q. Nguyen et al., "Changing perspectives: enhancing learning efficacy with the after-action review in virtual reality training for police," *Ergonomics*, 2023.

[39] "Self- vs facilitator-guided debriefing in immersive VR: a randomised controlled trial protocol," *PMC12431211*.

[40] "Self-review and feedback in VR dialogues," *ScienceDirect*, 2025.

[41] M. Friedman, "The use of ranks to avoid the assumption of normality implicit in the analysis of variance," *J. American Statistical Association*, vol. 32, no. 200, pp. 675–701, 1937.

[42] F. Wilcoxon, "Individual comparisons by ranking methods," *Biometrics Bulletin*, vol. 1, no. 6, pp. 80–83, 1945.

[43] M.G. Kendall and B. Babington Smith, "The problem of m rankings," *Annals of Mathematical Statistics*, vol. 10, no. 3, pp. 275–287, 1939.

[44] O.J. Dunn, "Multiple comparisons among means," *J. American Statistical Association*, vol. 56, no. 293, pp. 52–64, 1961.

[45] R.A. Fisher, *Statistical Methods for Research Workers*. Edinburgh, U.K.: Oliver & Boyd, 1925. (Analysis of variance / the F-test underlying §6.6's ANOVA table.)

[46] W.H. Press, S.A. Teukolsky, W.T. Vetterling and B.P. Flannery, *Numerical Recipes in C: The Art of Scientific Computing*, 2nd ed. Cambridge, U.K.: Cambridge Univ. Press, 1992, §6.4. (Regularized incomplete beta function / continued-fraction method used to compute the exact F-distribution *p*-value in `lib/stats.js`.)

**Trait-differentiated hostage profiles (§3.5, §6.5b) — specified, not implemented.** The full reference list for this strand lives in `HOSTAGE_PROFILE_RESEARCH.md` §10 (20 entries) rather than being duplicated here, since none of it is yet load-bearing for any *implemented* Module 4 behaviour. The principal sources are: the Fight/Flight/Freeze Questionnaire (*Cognitive Behaviour Therapy*, 2015; 44(2):117–127); "Dysfunctional Freezing Responses to Approaching Stimuli in Persons with a Looming Cognitive Style" (*Frontiers in Psychology*, 2016, art. 521); J. Luo, B. Zhang, M. Cao and B.W. Roberts, "The Stressful Personality: A Meta-Analytical Review of the Relation Between Personality and Stress" (*Personality and Social Psychology Review*, 2023); I.R. Galatzer-Levy, S.H. Huang and G.A. Bonanno, "Trajectories of resilience and dysfunction following potential trauma" (*Clinical Psychology Review*, 2018); "Hostage-taking: motives, resolution, coping and effects" (*Advances in Psychiatric Treatment*, Cambridge); and Y. Tisserand et al., "Virtual Human for Police Training in Virtual Reality: A Dynamic Model of a Suspect With Modulated Resistance and Means" (*Computer Animation and Virtual Worlds*, 2026).

> **Citation note.** References [1]–[32] are the load-bearing sources for the scoring/distress-model design and evaluation, drawn from `MODULE4_EVALUATION.md`, `MODULE4_MOVEMENT_VALIDATION_METHODOLOGY.md`, `MODULE4_WORKLOAD_REASONING_METHODOLOGY.md`, `PLAYER_SAFETY_SCORE.md`, and `HOSTAGE_EMOTION_SOUND_RESEARCH.pdf`. [33]–[40] are the comparative-landscape sources from `MODULE4_RESEARCH_REVIEW.md`. [41]–[46] are the statistical-method references; [41]–[44] are shared verbatim with Module 2's report (both modules' Friedman/Wilcoxon panels use the same engine, `lib/stats.js`), while [45]–[46] are specific to the one-way ANOVA added for §6.6. Several PMC/PDF-only sources above are cited by PMC ID because the source documents themselves did not carry full author/journal metadata — verify full bibliographic details against your institution's library access before final submission, exactly as Module 2's report recommends for its own equivalent entries. The profile-strand sources listed immediately above carry their own per-source confidence note in `HOSTAGE_PROFILE_RESEARCH.md` §10, distinguishing values verified from full text from those read only from an abstract.

---

## 13. Appendices

### Appendix A — Score formulas (current, as implemented)
```
accuracyScore   = totalShots > 0 ? clamp01(hitRate / ExpectedHitRate(avgEngagementDistance)) : 0
  ExpectedHitRate(d): <3m→0.50, <7m→0.125, <15m→0.06, <25m→0.045, else→0.03   (NYPD SOP-9 bands)

safetyScore     = clamp01( (hostagesSaved/hostagesTotal) × 0.61^friendlyFireCount )

speedScore      = 1 − clamp01( missionDuration / (2 × targetTime) )
  targetTime = 30 + 5×rooms + 20×terrorists + difficultyBonus(AI tier)   [design defaults]
             = 300  (flat fallback when no scenario metadata is available)

operatorSafetyScore = 0.25×Survivability + 0.25×ExposureControl + 0.25×WeaponDiscipline + 0.25×ThreatResponse
  Survivability     = died ? 0 : clamp01(finalHealth / maxHealth)
  ExposureControl   = 1 − clamp01(exposedSeconds / (engagedSeconds × 0.5)), minus 0.1 per extra simultaneous observer
  WeaponDiscipline  = clamp01( 0.61^friendlyFire × negligentDischargeFactor )
    negligentDischargeFactor = clamp01(1 − (ndRate − 0.17) / (0.61 − 0.17))
  ThreatResponse    = clamp01(1 − (avgReactionTime − 0.3) / (0.7 − 0.3))
  capped at 0.20 if the trainee was downed

overallScore    = 0.4×safety + 0.3×accuracy + 0.2×speed + 0.1×(missionSuccess ? 1 : 0)
```

### Appendix B — Telemetry event types used by the metrics
`ShotFired` (tagged `no_target_in_los` when negligent), `TerroristHit` (carries `distance`), `TerroristDown`, `EnemyShotFired`, `EnemyHitPlayer`, `HostageHit` (tagged `friendly_fire`), `PlayerSeen`/`PlayerLost`/`TargetConfirmed` (Operator Safety exposure), `HostageContactStarted`, `HostageFreed`, `MissionEnded` (+ end reason: `player_down` / `hostage_executed` / `hostage_killed` / success).

### Appendix C — Statistical methods (shared engine with Module 2)
Friedman χ² (k=3) with p = exp(−χ²/2); Kendall's W = χ²/(n·2); Wilcoxon signed-rank exact for n ≤ 20, normal approximation above, effect size r = |z|/√n; Bonferroni α = 0.05/3 ≈ 0.017. Implementation: `sentinels-aar/lib/stats.js`, dependency-free, validated against a hand-worked example (identical validation Module 2's report documents, since both modules share this file).

### Appendix D — Related repository documents
`MODULE4_REVIEW.md` (engineering audit, superseded by §4 above with subsequent fixes applied), `MODULE4_RESEARCH_REVIEW.md` (comparative landscape, superseded by §9 above), `MODULE4_EVALUATION.md` (construct-validity audit, superseded by §6.1 above but retained as the canonical living document — the Evaluation page's construct-validity claims should track it, not this snapshot), `MODULE4_MOVEMENT_VALIDATION_METHODOLOGY.md`, `MODULE4_WORKLOAD_REASONING_METHODOLOGY.md`, `PLAYER_SAFETY_SCORE.md`, `HOSTAGE_EMOTION_SOUND_RESEARCH.pdf` + `Module4_Research_Figures.docx.pdf` (companion sound/emotion research — the *stimulus* side of the hostage model — and original submission-safe charts), **`HOSTAGE_PROFILE_RESEARCH.md`** (the *individual-differences* side — trait-differentiated hostage profiles; **specified, not implemented**, summarised in §3.5/§6.5b and carried as future work in §11), `Expert Value Table.pdf` (the benchmark table §6.1 evaluates against). This report consolidates all of them and adds the as-built implementation, engineering narrative, and the statistical layer built for Module 4's own scores.

**Reading order for the hostage emotional model specifically:** `HOSTAGE_EMOTION_SOUND_RESEARCH.pdf` (what stimulus changes the state) → `HOSTAGE_PROFILE_RESEARCH.md` (why individuals differ in responding to it) → §3.5 of this report (the resulting distress-index scoring Module 4 owns) → `Module4_Research_Figures.docx.pdf` (the charts for both).

---

*Prepared for the Sentinels project, University of Moratuwa. Every metric and formula described here is live in the codebase and was verified against the actual source files during preparation; behaviours are grounded in the cited research, with derived or tuned elements explicitly flagged. §6.8's real/synthetic data boundary is a hard constraint on how this document may be used in a submitted report — do not cite `P0002`–`P0005` as evaluation evidence.*
