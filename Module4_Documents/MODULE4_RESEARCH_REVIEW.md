# Module 4 (After-Action Review) — Research Review & Comparative Analysis

**Sentinels VR Hostage-Rescue Trainer · University of Moratuwa · 2026**
**Purpose:** position Module 4 against the existing AAR-dashboard landscape (commercial and academic), document exactly what it includes, and identify its contributions and gaps for the project write-up.

---

## 1. What Module 4 is

Module 4 is the **After-Action Review (AAR)** subsystem of the trainer. Modules 1–3 generate the scenario and run the VR firefight; Module 4 **observes, records, scores, and reports** on the trainee's performance. It is the "so what did I learn?" half of the system.

Architecturally it is two halves:

- **In-engine (Unity/C#):** a passive data-capture and analysis layer that logs everything, computes metrics, extracts notable incidents, and packages a `SessionSummary`.
- **Off-engine (web):** a standalone HTML report and a hosted **Next.js + MongoDB dashboard** (React/Recharts) that visualise the session, plus a closed-loop mission launcher.

The design intent — automatic AAR generation from a single gameplay event bus, with **zero manual instrumentation of individual gameplay systems** — is the defining engineering characteristic and is what most differentiates it from authored-scenario commercial tools.

---

## 2. Full feature inventory (what is actually in Module 4)

### 2.1 Data capture (the recording layer)
- **Universal event log.** `Module4Bridge` subscribes to the game's single event bus (`EventManager.OnEventRaised`) and forwards *every* `ScenarioEventType` (shots, sightings, door breaches, alerts, hostage events, mission lifecycle) into `SessionLogger`. New event types need no bridge changes.
- **NPC state transitions.** Every terrorist/guardian FSM transition (Idle → Alert → Engage → …) is logged with actor, previous/new state, trigger, and timestamp.
- **Hostage emotional history.** Each hostage state change (Calm, Fearful, Held, Follow, Freed, …) with its trigger event and a derived distress score.
- **Spatial replay track.** `ReplayRecorder` samples every actor's **position + rotation + FSM state every 0.5 s** (bounded, auto-downsampled), producing the data for full replay.
- **Room/door layout snapshot.** The played scenario's top-down geometry is captured so the dashboard can draw walls and place events in rooms.
- **Reaction-time capture.** Trainee response latency to threat stimuli (recent addition).
- **Persistence + transport.** Writes an indented JSON `SessionSummary` to `persistentDataPath` and POSTs it to the web backend (idempotent upsert by `sessionId`).

### 2.2 Performance scoring
A weighted composite computed at mission end:
- `accuracyScore` = hits / shots
- `safetyScore` = hostages saved / total, minus a penalty per friendly-fire event
- `speedScore` = inverse of mission duration against a benchmark
- `missionSuccess` = derived from the **actual end reason** (player_down / hostage_executed / hostage_killed force failure), not recomputed independently
- `overallScore` = `0.4·safety + 0.3·accuracy + 0.2·speed + 0.1·success`
- Plus raw counts: shots, hits, misses, friendly-fire, hostages saved/total, duration.

### 2.3 Automatic incident extraction
Seven auto-detected "notable moments," each severity-ranked (low / medium / high):
`FirstContact`, `FirstShot`, `HostageEndangered` (high), `TerroristNeutralized`, `AlertCascade` (≥3 NPCs alerted within 5 s — a coordinated enemy response, medium), `HostageRescued`, `MissionEnd`. Now tagged with the **room** the event occurred in.

### 2.4 Psychological state — mind the module boundary

> **Correction / clarification:** the trainee **cognitive-tracking function is NOT part of Module 4.** It is a separate subsystem — `Assets/CognitiveTracking/` (`TeamSentinels.CognitiveTracking`, the project's **Module 3**).

- **Trainee cognitive metrics = Module 3 (CognitiveTracking).** A dedicated module measures the trainee's **reaction time to threat stimuli** (a multi-channel head / hands / movement / trigger detector with validity rules — ignores motion already underway, an 80 ms human-floor, burst suppression) and records a **body-movement track** (head + hands, crouch, scanning, weapon-in-hand, health at 5 Hz), with an in-VR **mirror HUD** for live feedback. It *feeds* these into Module 4's `SessionLogger` (`LogReactionTime`, the movement track). **Module 4 only serialises and displays them** (`averageReactionTime` / `peakReactionTime`). The cognitive *analysis* is Module 3's; Module 4 is the host/visualiser.
- **Hostage distress index (this one IS Module 4's).** A moment-to-moment psychological-distress curve derived from the hostage's emotional-state changes, with a panic threshold line, a per-state colour-coded emotional timeline, and a plain-English "what happened and why" narrative for each transition. This belongs to Module 4 because it is derived from the hostage's in-mission state, not from the cognitive-tracking module.

### 2.5 Replay (the centrepiece)
- **2D top-down map:** all actors as coloured dots over a scrubable timeline, with state colours and per-actor labels.
- **3D fly-around reconstruction (react-three-fiber):** the mission rebuilt in 3D from the recorded tracks, with **real animated character models** (state-driven idle / walk / alert / fire / dead clips baked from the game's own animations), solid rooms with door openings, labelled corridors, coloured ground rings for role/state, a **free orbit camera**, and a **follow-the-trainee camera**.

### 2.6 Reporting, dashboard & closed loop
- **Standalone offline HTML report** (`WebReportExporter`) — self-contained, no server needed.
- **Hosted web dashboard** (Next.js/React/Recharts) with tabs: **Summary** (scores, combat stats, cognitive analysis), **Timeline** (event timeline + activity-density chart + filterable event log), **Incidents** (severity-ranked, explained), **Hostage** (distress + emotional journey), **Replay** (2D + 3D). Plus a **session list with aggregate statistics** across all runs.
- **Closed training loop:** the dashboard can **launch a new mission** on the headset (`ScenarioHttpServer`), and the headset uploads the AAR back — configure → train → review → repeat.

---

## 3. The existing AAR landscape

AAR-for-training is an established and growing category — the **AAR-software-for-training market was ~USD 0.85 B in 2025, projected to ~USD 1.78 B by 2034** ([Intel Market Research](https://www.intelmarketresearch.com/after-action-review-software-for-training-market-45196)). Two strands are relevant.

### 3.1 Commercial military/tactical VR-AAR systems
- **Operator XR — AAR Module:** timeline scrubbing, event markers, **auto-flagging of blue-on-blue (friendly-fire) incidents**, and analysis of field-of-view, weapon angles and eye movement ([Operator XR](https://operatorxr.com/military-after-action-review)).
- **Thales "Gladiator" Training Data Analytics:** an **AI-powered** platform that fuses data from exercise control, soldier/vehicle/drone sensors and infrastructure into structured AAR insights ([Halldale](https://www.halldale.com/defence/ai-platform-speeds-up-military-training-debriefs)).
- **MÄK VR-Engage / MÄK ONE:** AAR/debrief apps for record-and-replay of simulation exercises ([MAK](https://www.mak.com/mak-one/apps/vr-engage)).
- **InVeris fats® AR:** real-time monitoring and **immersive multi-viewpoint AAR** with **eye, head and muzzle tracking of every team member** ([InVeris](https://www.inveristraining.com/virtual-training/military-virtual-tactical-small-arms-training-marksmanship/fats-ar/)).

**Common commercial feature set:** automated data logging from the sim, synchronised video/replay, quantitative performance metrics (reaction time, accuracy, protocol adherence), interactive timelines with event markers, collaborative annotation, and increasingly AI-generated insight ([market overview](https://www.intelmarketresearch.com/after-action-review-software-for-training-market-45196)).

### 3.2 Academic findings on effective AAR/debrief
- **Multi-perspective replay improves learning.** In VR police training, replaying a scenario from **multiple viewpoints (bird's-eye + suspect perspective)** produced *significantly greater learning efficacy* than bird's-eye alone — the paper already in your repo ([Nguyen et al., *Ergonomics* 2023, "Changing Perspectives"](https://www.tandfonline.com/doi/full/10.1080/00140139.2023.2236819)).
- **Structured debriefing drives reflection.** Debriefing that guides learners to *recall, evaluate, and conceptualise* their actions is a critical strategy in simulation-based learning ([self- vs facilitator-guided debrief RCT](https://www.ncbi.nlm.nih.gov/pmc/articles/PMC12431211/)).
- **Record + replay + time-stamped logs** enable comprehensive qualitative post-analysis; self-review and feedback loops measurably change trainee behaviour ([ScienceDirect](https://www.sciencedirect.com/science/article/pii/S2949678025000169)).

---

## 4. Comparative analysis — how Module 4 measures up

| Capability | Commercial military AAR | AAR research says | **Module 4** |
|---|---|---|---|
| Automated event logging | ✅ Standard | ✅ Essential | ✅ Universal event-bus bridge (no per-system wiring) |
| Timeline + event markers | ✅ | ✅ | ✅ Event timeline + activity-density + filterable log |
| Performance metrics (accuracy, reaction, speed) | ✅ | ✅ | ✅ Weighted composite + reaction time |
| Friendly-fire / blue-on-blue flagging | ✅ (Operator XR) | ✅ | ✅ Friendly-fire penalty + safety score |
| Record & replay | ✅ | ✅ Critical | ✅ 2D map **and** 3D reconstruction |
| **Multi-perspective replay** | ✅ (fats AR) | ✅ **Proven to boost learning** | ✅ Top-down + free-orbit + follow-trainee |
| Incident auto-detection | Partial | — | ✅ 7 typed, severity-ranked, explained |
| **Hostage psychological/emotional tracking** | ✗ (tactical focus) | Empathy/perspective valued | ✅ **Distress index + emotional journey** *(distinctive, Module 4)* |
| Trainee reaction-time / cognitive analytics | ✅ | ✅ | ⚠️ Reaction time + movement track measured by **Module 3 (`CognitiveTracking`)**, only *displayed* by Module 4; higher-order cognitive-load/stress labels partial |
| Explainable / narrated AAR | Rare | ✅ Aids reflection | ✅ Plain-English "what & why" per event |
| Closed loop (dashboard launches scenario) | Rare | — | ✅ Configure→train→review→repeat |
| Procedurally-generated scenarios feeding AAR | Rare (authored) | — | ✅ Module 1 ↔ Module 4 loop *(distinctive)* |
| Eye / gaze / head / muzzle analytics | ✅ (fats, Operator XR) | Valued | ✗ **Gap** |
| Team / multi-trainee AAR | ✅ | ✅ | ✗ Single trainee |
| Facilitator annotation / collaborative debrief | ✅ | ✅ Emphasised | ✗ **Gap** |
| AI-generated insights & recommendations | ✅ (Thales) | Emerging | ✗ Not yet |
| Communication / comms-log analysis | ✅ | ✅ Emphasised | ✗ N/A (single trainee) |
| Cost / accessibility | ✗ Enterprise, costly | — | ✅ Open, self-hosted, offline-capable |

### 4.1 Where Module 4 is genuinely competitive
- **Zero-instrumentation, fully automatic AAR.** The single-event-bus bridge means the AAR captures *everything* the game emits without hand-wiring each subsystem — cleaner than most bolt-on AAR integrations.
- **Multi-perspective 3D replay.** This directly implements the strongest evidence-based AAR recommendation in the literature (the "Changing Perspectives" finding). Few *student/open* projects reach a free-camera, animated, model-accurate reconstruction.
- **Dual delivery.** An offline self-contained HTML report *and* a hosted dashboard — usable with or without infrastructure.

### 4.2 Where Module 4 is distinctive (potential research contribution)
- **Psychological AAR of the hostage, not just tactical AAR of the trainee.** Tracking the *hostage's* emotional trajectory (distress index, state journey, causes) and surfacing it in the debrief is uncommon in military AAR tools, which focus on the operator's tactics. It ties the AAR to the hostage-leverage mechanic and to the empathy/perspective themes the research values.
- **Closed generate → train → review loop with procedurally-generated scenarios.** Commercial AAR reviews *authored* exercises; here the scenario is generated (Module 1), trained, and reviewed, then a new one is generated — a full experiential-learning cycle in one system.
- **Explainable AAR.** Auto-narrated, plain-English causal explanations ("heard gunfire nearby → became Fearful") lower the need for a human facilitator, which speaks to the open question of *self-guided vs facilitator-guided* debriefing.

### 4.3 Honest gaps vs the state of the art
1. **Cognitive analytics belong to Module 3, and only partially surface in the AAR.** Reaction time and a body-movement track are genuinely measured by the `CognitiveTracking` module (Module 3) and shown on the Summary tab; the higher-order **cognitive-load / stress labels** and any **gaze/attention** analytics are the remaining depth gap. (This is a Module 3 capability question — Module 4 only hosts and displays whatever Module 3 feeds it.)
2. **No gaze/eye/head-tracking analytics** — commercial systems exploit headset eye tracking (Quest Pro supports it); Module 4 does not.
3. **Single-trainee only** — no team AAR, no communication-log analysis, both emphasised in research and commercial tools.
4. **No facilitator/collaborative annotation** — the debrief is self-guided by design, but there is no instructor tooling.
5. **No AI-generated coaching** — insights are rule-based, not model-generated (unlike Thales Gladiator).
6. **First-person trainee-POV video is reconstructed, not recorded** — the 3D replay approximates viewpoints rather than replaying the exact headset footage.
7. **Data-quality items** — e.g. accuracy counting should exclude enemy fire; some fields (room, cognitive) were only recently instrumented. (Tracked in `REALISM_BACKLOG.md` / `MODULE4_REVIEW.md`.)

---

## 5. Positioning statement (for the write-up)

> Module 4 implements the **core, evidence-based feature set of a modern VR-training AAR system** — automated logging, a severity-ranked incident timeline, quantitative performance scoring, friendly-fire detection, and **multi-perspective replay** (the single feature the peer-reviewed literature most strongly links to learning gains). It is delivered as an **open, self-hosted, offline-capable** dashboard rather than a costly enterprise product.
>
> Its **distinctive contributions** relative to existing systems are (a) a **fully automatic AAR generated from one event bus** with no per-system instrumentation, (b) **psychological after-action review of the hostage** (distress index and emotional journey) alongside tactical review of the trainee, and (c) a **closed procedural-generation → training → review loop**.
>
> Its **principal gaps** relative to commercial state-of-the-art (Operator XR, Thales Gladiator, InVeris fats® AR) are gaze/eye-tracking analytics, team-level and communication AAR, facilitator annotation tools, AI-generated coaching, and fully-instrumented cognitive-load measurement.

---

## 6. Suggested future work (research-relevant)
1. **Extend Module 3's cognitive layer into cognitive-load/stress estimates** (Module 3 already measures reaction latency and a movement track; the next step is deriving stability / hesitation / load–stress labels and surfacing them in Module 4's dashboard). This is a Module 3 research contribution that Module 4 would visualise.
2. **Add gaze/attention analytics** using Quest eye tracking — matches commercial state of the art.
3. **Trainee first-person replay** synchronised with the 3D reconstruction — realises the full "multiple perspectives" recommendation end-to-end.
4. **Empirical study:** does Module 4's *self-guided, explainable* AAR match *facilitator-guided* debrief on learning outcomes? This is an open question in the literature and your explainable-AAR design is a natural testbed.
5. **AI-generated debrief summary** (rule-based insights → LLM-generated coaching) to match Thales-class systems.

---

## References
- After Action Review Software for Training — market overview. Intel Market Research, 2026. https://www.intelmarketresearch.com/after-action-review-software-for-training-market-45196
- Operator XR — Military After-Action Review module. https://operatorxr.com/military-after-action-review
- Thales Gladiator Training Data Analytics (AI-powered AAR). Halldale Group. https://www.halldale.com/defence/ai-platform-speeds-up-military-training-debriefs
- MÄK VR-Engage / MÄK ONE AAR & debrief. https://www.mak.com/mak-one/apps/vr-engage
- InVeris fats® AR — multi-viewpoint AAR with eye/head/muzzle tracking. https://www.inveristraining.com/virtual-training/military-virtual-tactical-small-arms-training-marksmanship/fats-ar/
- Nguyen et al., "Changing perspectives: enhancing learning efficacy with the after-action review in virtual reality training for police," *Ergonomics*, 2023. https://www.tandfonline.com/doi/full/10.1080/00140139.2023.2236819 *(also in `Assets/Docs/Research Papers/`)*
- Self-guided vs facilitator-guided debriefing in immersive VR (RCT protocol). PMC. https://www.ncbi.nlm.nih.gov/pmc/articles/PMC12431211/
- Self-review and feedback in VR dialogues. ScienceDirect, 2025. https://www.sciencedirect.com/science/article/pii/S2949678025000169

*Companion documents: `MODULE4_REVIEW.md` (engineering audit & known issues), `REALISM_BACKLOG.md` (gameplay realism backlog).*
