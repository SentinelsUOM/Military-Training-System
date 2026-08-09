# Module 2 — Complete Study Guide (NPC Behaviour & Coordination)

> Compiled from the actual codebase, the integration test suite, and
> `Assets/Docs/Literature_Review_Module2.docx`, for evaluation/viva prep.
> Last synced against the code as of 2026-08-09.

---

## 1. What Module 2 is, in one paragraph

Module 1 generates the building layout and decides where every NPC starts.
Module 2 is responsible for what those NPCs (the terrorists holding the
building, and the hostage) **do** once the mission is live — how they
perceive the trainee, how they react individually, and how they coordinate
as a group. Module 4 reads Module 2's telemetry to build the after-action
report. Module 2 is built from three layers: an **event bus** every system
talks through, **per-NPC finite state machines** (FSMs) that drive
individual behaviour, and a **squad coordination layer** that makes
terrorists in the same squad act as a group rather than independent bots.

---

## 2. Architecture map (files → role)

| Folder/File | Role |
|---|---|
| `Assets/Scripts/Events/EventManager.cs` | Central event bus — routes every `ScenarioEvent` |
| `Assets/Scripts/Events/ScenarioEvent.cs` | Immutable event data record |
| `Assets/Scripts/Events/ScenarioEventType.cs` | Enum of every event type the system knows |
| `Assets/Scripts/NPC/TerroristController.cs` | Terrorist FSM + combat + squad support brain (3,790 lines) |
| `Assets/Scripts/NPC/HostageController.cs` | Hostage FSM + follow/leverage logic |
| `Assets/Scripts/NPC/PerceptionController.cs` | Vision (FOV + raycast) and target-confirm timing for terrorists |
| `Assets/Scripts/NPC/HostageProfile.cs` | Weak/Normal/Brave personality trait system |
| `Assets/Scripts/NPC/AlertPropagator.cs` | 3-ring alert spreading (squad / proximity / none) |
| `Assets/Scripts/NPC/Squad.cs` | Squad membership, Leader directives, shared hunt/search logic |
| `Assets/Scripts/NPC/LeaderDirective.cs` | Converge / Hold / Flank command data |
| `Assets/Scripts/NPC/INPCResponder.cs` | Interface every NPC implements to plug into the event system |
| `Assets/Scripts/NPC/NPCRole.cs` | Guard / Roamer / Leader / Hostage role enum |
| `Assets/Scripts/NPC/TerroristState.cs`, `HostageState.cs` | FSM state enums |
| `Assets/Scripts/Selection/NPCSelector.cs` | Multi-factor scoring — decides *which* NPC responds to a targeted event |
| `Assets/Scripts/Telemetry/TelemetryLogger.cs` | Central logging sink → JSON files for Module 4 |
| `Assets/XRI Starter Kit/Assets/Scripts/PatrolLine.cs` | Multi-waypoint patrol route follower |
| `Assets/Scripts/Tests/Module2TestRunner.cs` | 13 automated integration tests (in-editor, Play Mode) |
| `Assets/Scripts/Tests/AblationExperimentRunner.cs` | Generates CSV evidence that the scoring formula's factors matter |
| `Assets/Scripts/Tests/Module2_SlideContent.md` | Ready-made presentation slide + viva talking points |
| `Assets/Docs/Literature_Review_Module2.docx` | The research grounding for every design decision below |

---

## 3. The event system — the nervous system

`EventManager` is a singleton. Anything that happens becomes a
`ScenarioEvent` (type, world position, instigator, timestamp, optional
room/target IDs) and is passed to `EventManager.Instance.Raise(e)`.

**Two routing modes:**

- **Broadcast** types — delivered to *every* eligible NPC:
  `GunshotHeard`, `TerroristDown`, `RoomCleared`, `StressSpike`.
- **Targeted** types — delivered to the *single best* NPC, chosen by
  `NPCSelector` (see §6): everything else (`RoomBreached`, `DoorOpened`,
  `AllyDownSeen`, `HostageContactStarted`, …).

**Event derivation** (automatic, inside `Raise`):
- `ShotFired` → always also raises `GunshotHeard`.
- 3+ `ShotFired` events within 2 seconds → automatically raises
  `StressSpike` (tracked with a rolling `Queue<float>` of recent shot
  timestamps in `EventManager`). This is what escalates hostages to Panic
  during sustained gunfire without any scripted trigger.

**Special post-broadcast dispatch** baked into `EventManager.RouteToNPCs`:
- On `GunshotHeard`, after every terrorist in hearing range reacts, the
  *best-scored terrorist* (via `NPCSelector`) is additionally sent to
  physically **investigate** the shot's origin (`InvestigatePosition`).
- On `TerroristDown`, an Alert-state terrorist is picked the same way to
  go investigate the body.

---

## 4. Terrorist FSM (`TerroristController.cs`)

### 4.1 States
`Idle → Suspicious → Alert → Engage`, plus `TakeCover`, `Retreat`, `Down`.

### 4.2 Transition table

| From | To | Trigger |
|---|---|---|
| Idle | Suspicious | `GunshotHeard` within `hearingRange` (20m default) |
| Suspicious | Alert | `PlayerSeen` (own perception), `RoomBreached`, `DoorOpened`, another `GunshotHeard`, or `AllyDownSeen`/`TerroristDown` |
| Alert | Engage | `TargetConfirmed` — sustained LOS for `targetConfirmTime` (0.5s), from `PerceptionController` |
| Engage | Alert | `PlayerLost` — LOS broken for `lostSightDelay` (1.5s) |
| Engage | Retreat | `TakeHit()` drops health ≤ `retreatHealthThreshold` (40), only once per life, only if not a hostage guardian |
| any active | Down | health ≤ 20 (checked inside `TakeHit`) |
| Alert | TakeCover | manual cover-seeking logic (`MoveTocover`), falls back to Alert if no cover found |
| Retreat | Alert | automatic, after holding a fallback point for `retreatHoldTime` (4s) — re-arms perception so re-engagement is possible |
| Suspicious | Idle | `SuspiciousTimeout` coroutine, 6s of nothing new |
| Alert | Idle | `AlertGiveUpTimeout` coroutine, 12s with no re-acquisition — **but see §4.5, the give-up is now squad-gated** |

Central transition method: `TransitionTo(next, trigger)` — cleans up the
old state's coroutines (stop firing, cancel timers, release cover), logs
the change to `TelemetryLogger`, then runs new-state entry logic.

### 4.3 Perception is personal, not broadcast

`PlayerSeen`, `TargetConfirmed`, and `PlayerLost` are **not** routed
through the shared event bus's `CanRespond`/`RespondTo` path — that path
explicitly rejects them. Instead, `PerceptionController` (a sibling
component doing FOV cone + raycast checks every 0.2s) calls
`HandleDetection(e)` directly on its own `TerroristController`. This
matters: an NPC that never personally saw the player can never be handed
a squadmate's sighting and start shooting blind — only genuinely "shared"
information (see §7.4) is allowed to spread.

### 4.4 Idle / patrol movement (`idleMode`)
- **Patrol** — delegates to `PatrolLine`, which walks an ordered list of
  waypoints from Module 1's `navigationContext` (loop or ping-pong).
- **Wander** — random point within `wanderRadius` (12m), walk, pause
  or scan, repeat.
- **Static** — stays put, optionally faces a fixed direction, still
  idle-scans.
- **Idle scanning** — every ~5s, sweeps a 65° arc in glances (not a
  continuous spin), skipping directions blocked by a nearby wall.

### 4.5 The "no casual give-up" fix (this is the literature review payoff)

The literature review (§2–4) diagnosed a real bug: a terrorist who saw
the trainee, lost sight, and then just walked back to patrol — described
in the review as "doctrinally incoherent." The fix, now in code:

- Entering `Idle` is intercepted: if this NPC personally confirmed the
  player, **or** its squad is still actively hunting (`Squad.HuntActive`),
  it is bounced straight back to `Alert` instead of settling down.
- The only sanctioned way to stand down is `Squad.EndHunt()` — a
  **collective, squad-wide decision**, taken only when the shared hunt
  budget (45 seconds, re-armed by every fresh sighting) expires or the
  area is genuinely searched out. See §7.4.

### 4.6 Combat: health, damage, morale (escalation), positioning

- `maxHealth` = 100. `TakeHit(damage)` reduces it; ≤20 → `Down`.
- **Escalation / morale accumulator** (`_escalation`, float): rises from
  ally-down sightings (+2), stress spikes (+1), being hit (+1), and a
  periodic +tick every 8s of sustained combat. Once it crosses
  `escalationThreshold` (3), `CombatPosture` flips **one-way** from
  `HoldAndShoot` to `CoverAndPeek`.
- **HoldAndShoot**: holds a firing-ring standoff distance (steps back if
  the player gets within 3m, advances if the player is beyond ~3.5m —
  i.e. it visibly presses the attack).
- **CoverAndPeek**: claims the best nearby `CoverPoint`, holds fire there
  ~3.5s, then relocates — classic peek-and-bound suppression behaviour.
- **Death** (`EnterDownState`): stops shooting, drops any held hostage,
  cancels all coroutines, disables `PatrolLine`/`NavMeshAgent`, snaps the
  body to the floor, disables hitboxes and (optionally) non-trigger
  colliders so living NPCs can walk over corpses, plays the death
  animation, raises `TerroristDown`.

### 4.7 AI ablation tiers

`AILevel` (Basic / Intermediate / Advanced) gates whole feature groups on
or off (`AllowTeam`, `AllowCoverMorale`, `AllowRetreat`, `AllowIdleScan`,
`AllowGuardianLadder`) — this is a design-level ablation independent of
the `NPCSelector` scoring ablation described in §6.

### 4.8 Key tunables (defaults)

`hearingRange`=20m · `wanderRadius`=12m · `retreatHealthThreshold`=40 ·
`retreatHoldTime`=4s · `flankOffset`=5m · `responseCooldown`=4s ·
`minStandoffDistance`=3m · `preferredStandoffDistance`=3.5m ·
`escalationThreshold`=3 · `coverHoldTime`=3.5s · `suspiciousTimeout`=6s ·
`alertGiveUpTime`=12s.

---

## 5. Hostage FSM (`HostageController.cs`)

### 5.1 States
`Calm, Fearful, Freeze, Panic, Follow, Held, Freed, Down`.

### 5.2 Transition table

| From | To | Trigger |
|---|---|---|
| Calm | Fearful | `GunshotHeard` beyond `panicDistance` (6m) |
| Calm | Panic | `GunshotHeard` within `panicDistance`, or a misread of a distant shot, or `StressSpike` |
| Fearful | Panic | `TerroristEnteredRoom`, or `StressSpike` |
| Fearful | Freeze | `GunshotHeard` within `freezeRange` (5m) |
| Panic | Freeze | `Panic` sustained ≥ `freezeThreshold` (3s), via `SustainedThreatCheck` |
| Panic | Fearful | `TerroristDown` (threat eliminated) — unless misread |
| Follow | Fearful ("regression") | close `GunshotHeard` during escort — flagged `[REGRESSION]` in telemetry |
| any (except Freed/Follow) | Calm | `RoomCleared` — unless misread |
| any non-terminal | Follow | `HostageContactStarted` — unless misread as a threat (→ Freeze instead) |
| — | Freed | `HostageFreed` event, or auto-detected when the runaway controller reports `IsHiding` |
| — | Held | `SeizeAsLeverage(captor)` by a guardian terrorist |
| Held | Fearful | `ReleaseFromHold()` |
| — | Down | `TakeHit` to 0 health, or guardian `Execute()` — terminal, raises `OnKilled` (ends mission as FAIL) |

### 5.3 Personality profiles (`HostageProfile.cs`)

Three profiles: `Weak`, `Normal`, `Brave`. **All three use identical
distance thresholds** — the code comments explain why: published
dB-to-distance research shows gunfire exceeds the fear/panic loudness
threshold at *every* realistic in-building range, so distance literally
cannot discriminate between personalities at close quarters. The only
differentiator is `ThreatDiscrimination` (0–1):

| Profile | Discrimination | Spawn weight |
|---|---|---|
| Weak | 0.35 | 10.9% |
| Normal | 0.85 | 21.4% |
| Brave | 1.00 | 67.7% |

`MisreadsNonThreat()` rolls `Random.value > ThreatDiscrimination` at four
decision points (grading a distant shot, calming down after the threat
is gone, calming down on `RoomCleared`, recognising an approaching
rescuer). Lower discrimination → more overreaction/underreaction, which
naturally makes a Weak hostage slower to extract without any hardcoded
delay. Spawn weighting matches published trauma-trajectory prevalence
(Galatzer-Levy, Huang & Bonanno 2018), renormalised over 3 profiles.
`Brave` also carries a 15% "London Syndrome" defiance chance (captor
tail-risk).

Profile is assigned in `Awake()`: random-weighted by default, or pinned
via Inspector for controlled evaluation runs (this is exactly what
`Module2TestRunner` does — pins `Brave` so its distance-threshold tests
stay deterministic).

### 5.4 Following / regression

`FollowRoutine()` tracks `Camera.main.transform` (VR-correct — the player
rig can stay at world origin while only the head moves) or a captured
`_followTarget`. Re-paths only when the target moves >0.5m to avoid path
churn. `followSpeed` = 2.2 (overrides the NavMeshAgent default of 3.5).

### 5.5 Key tunables (defaults)
`panicDistance`=6m · `freezeRange`=5m · `freezeThreshold`=3s ·
`followSpeed`=2.2 · `maxHealth`=90 (three 30-dmg hits = death, matching
enemy fragility) · `injuredThreshold`=45.

---

## 6. NPCSelector — "who responds?" (`Selection/NPCSelector.cs`)

For every **targeted** event, all eligible candidates (already filtered
by `CanRespond` — not Down, not committed to another target, not on
cooldown) are scored:

```
score =  (1 / distance)            × DistanceWeight   (2.0)
       +  roleBonus[role][evtType] × RoleWeight        (1.0)
       +  stateReadiness           × StateWeight       (1.0)
       +  losBonus                 × LosWeight         (1.0)
```

- **Distance term**: closer = higher score (inverse distance).
- **Role bonus table**: Guard+RoomBreached = 3.0, Roamer+GunshotHeard =
  2.0, Leader+AllyDownSeen = 1.5, else 0.
- **State readiness** (0–1, from `INPCResponder.StateScore`): Idle=1.0,
  Suspicious=0.8, Alert=0.5, TakeCover=0.2, Engage=0.0 — idle NPCs are
  preferred responders.
- **LOS bonus**: a raycast from the candidate's eye height to the event
  origin — 1.0 if clear, 0.0 if blocked. Models "an NPC who can literally
  see the event is a better responder than one who only heard about it
  secondhand."
- Candidates beyond `MaxRange` (30m) are excluded outright.

`SetAblation(distance, role, state, los)` zeroes out any subset of
weights — this is the exact mechanism the `AblationExperimentRunner`
(see §9) drives to produce evidence for the evaluation report. Every
decision is logged via `TelemetryLogger.LogDecision`.

This is a standard **utility-based AI** technique (weighted multi-factor
scoring, pick the max) — see §10 for the literature grounding.

---

## 7. Squad coordination

### 7.1 Alert propagation — 3 rings (`AlertPropagator.cs`)

| Ring | Scope | Delay | Requires LOS? | Effect |
|---|---|---|---|---|
| 1 | Same squad | 0.3s | No | Idle/Suspicious members → Alert. On a member's death, LOS members → Engage immediately (0 delay) |
| 2 | Any NPC within `alertPropagationRadius` (15m) | 1.0s | Yes | Idle members → Suspicious |
| 3 | Everyone else | — | — | No effect |

### 7.2 Leader directives (`Squad.cs` + `LeaderDirective.cs`)

- A **Leader**-role terrorist entering `Alert` issues **Converge**
  (squad approaches but stops on a standoff ring — never walks onto the
  threat).
- The same Leader entering `Engage` issues **Flank** — each member
  computes its own flank point (`ComputeFlankPoint`, offset ± 5m
  perpendicular to the approach line, on whichever side it's already
  closer to) so the squad naturally splits left/right of the threat while
  the Leader holds the front. No explicit slot assignment needed.
- **Hold** stops a member in place (used for overlapping fields of fire).
- Members already `Engage` or `Down` ignore directives — they're
  committed. Hostage guardians *always* ignore directives — they never
  leave the hostage.

### 7.3 Leader succession

If the Leader dies, `Squad.PromoteNewLeaderIfNeeded` automatically
promotes a replacement (prefers a `Roamer`, excludes the hostage
guardian) so the squad doesn't lose coordination for the rest of the
mission just because the trainee killed the leader first.

### 7.4 Shared confirmed-contact blackboard & persistent hunt

This is the **direct implementation of the literature review's central
finding** (Sections 2–4: "a confirmed sighting is a trigger for
coordinated pursuit, not withdrawal").

- `Squad.ReportConfirmedContact(pos, heading, reporter)` — called by
  *any* member who personally confirms the trainee. Writes a **shared**
  anchor (`PointLastSeen`, `LastSeenHeading`), (re)starts the hunt, and
  collapses the search back to converging on the fresh sighting. This is
  what makes one NPC's sighting authoritative for the *whole* squad.
- `HuntActive` stays true, and the hunt is pressed, for up to
  `HuntBudgetSeconds` = 45s (re-armed by every fresh sighting) — a
  deliberately long, engineering-judgement number, because (as the
  review states explicitly) doctrine gives no exact figure for how long
  a lost contact should be pursued.
- The search radius **grows with elapsed time** (`SearchRadiusMin`=5m up
  to `SearchRadiusMax`=18m, growing at 0.6m/s) — a direct implementation
  of the search-and-rescue "expanding circle from the Point Last Seen"
  model cited in the review (§4).
- `FanOutSearch` distributes searchers along the escape direction on
  wave 0 (a forward chase, line-abreast), then on later waves fans them
  out to cover every approach angle (front, sides, behind) — modelling
  systematic clearing (§5 of the review).
- The **only** sanctioned way out of a hunt is `Squad.EndHunt()` — every
  member stands down together, never one NPC alone.

### 7.5 Squad support brain (independent of Leader directives)

Non-guardian, non-engaged members also run a lightweight local decision
each tick: **HELP** an injured/dead ally, **SUPPORT** an ally who
currently has eyes on the player, or **SWEEP** (trigger `FanOutSearch`
so searchers spread rather than clump).

---

## 8. Telemetry (`TelemetryLogger.cs`)

Four record types, all logged in real time and written to JSON at
session end (`Application.persistentDataPath/Telemetry/`):

1. **StateChanges** — every FSM transition (actor, old/new state,
   triggering event).
2. **Decisions** — every `NPCSelector.SelectBest` call (candidates,
   winner, score breakdown).
3. **Snapshots** — periodic position/state samples from
   `PerceptionController`.
4. **Directives** — every Leader Converge/Flank/Hold command.

`OnStateChangeLogged` (C# event) is the hook Module 4's AAR/dashboard
subscribes to, so the timeline can be rebuilt without Module 2 needing to
know Module 4 exists.

---

## 9. Testing & evaluation evidence

### 9.1 `Module2TestRunner.cs` — 13 integration tests

Attach to any GameObject in Play Mode, right-click → "TEST → Run All".
Covers: full terrorist FSM chain (Idle→Suspicious→Alert→Engage→Down),
retreat-once-per-life, alert give-up timeout, both hostage transitions
above, Leader Converge issuance, Leader Flank issuance, `PatrolLine`
multi-waypoint traversal with loop wrap, `NPCSelector` LOS term
(clear-sight NPC beats blocked-sight NPC), and `NPCSelector` ablation
(disabling distance still returns a valid winner).

### 9.2 `AblationExperimentRunner.cs` — quantitative evidence generator

Purpose: prove the 4-factor scoring formula isn't decorative — each
factor measurably changes who gets picked.

- Spawns 8 terrorists in a 30×30 arena (seeded RNG for reproducibility),
  round-robin across roles and states so `StateScore` genuinely varies,
  plus a centre wall so the LOS term has something real to block.
- Fires 20 random `GunshotHeard` events.
- For every event, runs selection under **5** configurations: `Full`,
  `NoLOS`, `NoDistance`, `NoRole`, `NoState` (100 decision rows total).
- Writes a CSV (`AblationResults_<timestamp>.csv`) with event id, mode,
  event position, selected NPC/role, distance, LOS, state score — plus a
  console summary table (pick counts per role per mode).
- The postscript of the literature review reports actual results from
  this kind of run: factor influence ranked **Role > State > LOS >
  Distance**, with LOS flagged as the most weight-sensitive and worth
  future re-tuning (full detail in `MODULE2_ABLATION_STUDY_REPORT` /
  `MODULE2_WEIGHT_SENSITIVITY_REPORT`, referenced by the review).

---

## 10. Research grounding — what to say if asked "is this evidence-based?"

`Literature_Review_Module2.docx` (July/Aug 2026) is the justification
document. Key points to be able to cite:

- **Central principle**: US Army battle-drill doctrine (Battle Drill 2 —
  "React to Contact") says a unit under contact returns fire and holds,
  never just walks away; "Break Contact" is a *separate*, deliberate
  drill executed under covering fire, not a reflex. This directly
  motivated the shared-hunt fix in §7.4.
- **Shared situational awareness**: doctrine treats a sighting as group
  property (ADDRAC fire-command format, leader converging reinforcements
  on contact) — modelled as the squad blackboard.
- **Search theory**: Point-Last-Seen anchor + time-expanding search
  radius (Phillips et al. 2014; NIST) — modelled directly in
  `Squad.NextSearchPoint`'s growing radius.
- **Clearing doctrine + F.E.A.R.'s GOAP**: base-of-fire + manoeuvre
  element, systematic clearing, and — most directly applicable — the
  **two-layer** architecture (autonomous per-agent behaviour beneath a
  squad coordination layer) from Orkin's 2006 GDC talk on F.E.A.R.'s AI.
  This is explicitly named as the closest computational precedent to
  `TerroristController` + `Squad`.
- **Morale**: Dupuy Institute operations-research on unit breakpoints —
  cohesion collapse driven by leadership loss, casualties, surprise, not
  a raw casualty count. Modelled as the `_escalation` accumulator +
  `CombatPosture` flip.
- **NPCSelector's utility formula**: standard utility-based/multi-factor
  scoring (Mark, *Behavioral Mathematics for Game AI*, 2009; Dill, GDC
  2010) — and notably Dill & Martin's I/ITSEC papers apply the *exact
  same technique to autonomous virtual characters for military training
  simulators*, the same domain as this project.
- **Honest gaps** (say these out loud if asked — it's a strength, not a
  weakness): no literature source exists for hostage-guard behaviour
  specifically (it's an engineering derivation from neighbouring
  principles); no literature-fixed number for lost-contact persistence
  duration (45s is a tuning decision); the exact 4-factor
  NPCSelector combination and its weights are engineering design,
  validated empirically via the ablation/sensitivity studies rather than
  pulled from a paper.

---

## 11. Likely viva questions — quick answers

**"How do you prioritise events?"**
Two layers: broadcast-vs-targeted routing decides *whether* everyone
reacts or one NPC is chosen; for targeted events, `NPCSelector` scores
every candidate on distance/role/state/LOS and the highest scorer
responds. There's no global priority queue — "priority" is encoded in
*who* responds, not in *event ordering*.

**"Is this from a paper?"**
The formula is custom, but the *method* (utility-based multi-criteria
scoring) is standard and has direct precedent in published military
training-simulator AI (Dill & Martin, I/ITSEC 2011/2012). The
contribution is the specific factor combination plus the ablation study
proving each factor matters — not found combined like this in the VR
training literature reviewed.

**"How do you know your scoring is better than nearest-only?"**
Ablation experiment: 20 identical events, 5 configurations, compared
which role gets picked. The full configuration distributes responses
across role types differently than distance-only or role-disabled
configurations — evidence the multi-factor scoring changes outcomes
meaningfully, not just cosmetically.

**"What about leader coordination?"**
Leader → Alert issues Converge; Leader → Engage issues Flank (each member
computes its own flank side, so the squad splits naturally around the
threat while the leader holds the front). Demonstrable live: spawn a
Leader + 2 squadmates on the same `squadId`, alert the Leader, watch
convergence, then engagement → fan-out.

**"What's the actual research contribution here, not just engineering?"**
Three things the review states aren't documented elsewhere: (1) fully
autonomous event-driven NPCs — both hostile and civilian — with no
instructor scripting; (2) a single shared event model driving both
populations plus coordination; (3) context-aware multi-factor responder
selection with ablation evidence quantifying each factor's contribution
— none of which the literature review found a comparable evaluation of
in existing VR-training research.

---

## 12. Demo checklist (if you need to show it live)

1. Open a test scene, attach `Module2TestRunner`, Play, right-click →
   "TEST → Run All" — show all 13 green in the Console.
2. Spawn a Leader + 2 squadmates (same `squadId`), fire a test event on
   the Leader, show squadmates converging in Scene view.
3. Open `Application.persistentDataPath/Telemetry/` — show the four
   JSON files plus the ablation CSV.
4. Full run: Module 1 generates a scenario → SceneBuilder spawns NPCs →
   play the mission → hostage panics on gunshot → terrorist investigates
   and engages → AAR shows the timeline reconstructed from telemetry.
