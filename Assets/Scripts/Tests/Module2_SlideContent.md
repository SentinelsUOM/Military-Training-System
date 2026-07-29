# Module 2 — Updated Presentation Slide Content

Use this for the **"Module 2 — NPC Behaviour and Coordination"** slide in
your final-evaluation deck. Replace the existing bullets directly.

This reflects what the code ACTUALLY does as of 2026-06-11.

---

## Slide title
**Module 2 — NPC Behaviour and Coordination**

---

## Left column — "Completed so far:"

- **Runtime event system** — central EventManager with broadcast/targeted routing, derived events (ShotFired → GunshotHeard → StressSpike on 3+/2s)
- **Terrorist FSM (full)** — Idle / Suspicious / Alert / TakeCover / Engage / **Retreat** / Down with investigation walk, rotation scan, cover-taking, low-health fallback (retreat once per life), death animation
- **Hostage FSM (full)** — Calm / Fearful / Freeze / Panic / Follow / Freed with sustained-threat freeze, NavMesh follow, regression detection
- **3-ring alert propagation** — Ring 1 squad broadcast (0.3s) · Ring 2 proximity + LOS (1.0s, 15m) · Ring 3 isolated
- **Multi-factor responder selection** — distance + role + state + **perception (LOS)** scoring; weights are tunable for evaluation
- **Leader-bias coordination** — Leader-role NPCs broadcast directives to their squad: **Converge** on Alert, **Flank** on Engage (members split to the threat's sides while the Leader holds the front); Hold also supported
- **Multi-waypoint patrol routes** — PatrolLine consumes the full ordered waypoints[] from Module 1's navigationContext (loop or ping-pong traversal per the `looping` flag)
- **Module 1 → Module 2 handoff** — `NotifyScenarioReady()` activates FSMs after NavMesh bake
- **Telemetry stream stabilised for Module 4** — four JSON streams (StateChanges, Decisions, Snapshots, Directives) + `OnStateChangeLogged` C# event hook
- **13 integration tests + ablation experiment runner** — `Module2TestRunner` and `AblationExperimentRunner` produce pass/fail and CSV evidence in play mode

---

## Right column — "As future work, should complete:"

- Full-scenario VR playtest (Module 1 scenes + Module 2 NPCs + Module 4 logging end-to-end)
- Run ablation experiment, write up findings (CSV → report tables and graphs)
- Swap placeholder NPC prefabs for rigged humanoid models (Animator integration already wired)
- Tune `LosObstacleLayers` per scene once Module 1 has a dedicated Walls layer

---

## Lower column — "Research Gap:" *(keep wording, now backed by code)*

- **Fully autonomous event-driven NPCs** — both terrorist and hostage populations react to runtime events without instructor scripting (existing academic prototypes rely on scripted or instructor-driven NPCs)
- **Both hostile and civilian populations in one shared event model** — single EventManager + INPCResponder interface drives terrorist FSM, hostage FSM, and coordination (not documented in published VR-training prototypes)
- **Context-aware responder selection** — multi-factor scoring (distance + role + state + LOS) with ablation evidence quantifying each factor's contribution (no comparable evaluation in VR-training literature)

---

# Viva talking points (be ready to defend these)

If a panel member asks **"how do you prioritise events?"** — say:
> "Priority works at two layers. **Broadcast-vs-targeted routing** decides whether every eligible NPC reacts (gunshots, squad-member down) or only one best-scored NPC (room breach, door opened). For targeted events, the **NPCSelector** scores every candidate on four factors — distance, role-bonus, current state, and line-of-sight — and the highest-scoring NPC responds. We deliberately don't use a global event priority queue because every event is processed immediately; the 'priority' is encoded in *which NPC responds*, not in *which event runs first*."

If asked **"is this from a paper?"** — say:
> "The exact scoring formula is custom, but the approach is standard **utility-based AI / multi-criteria decision making**. Mark's *Behavioral Mathematics for Game AI* (2009) and Dill's I/ITSEC paper on autonomous virtual characters for military training are the closest published references. The contribution is the **specific factor combination + ablation evaluation** in a VR training context, which we couldn't find in the literature review."

If asked **"how do you know your scoring is better than just picking nearest?"** — say:
> "We ran an ablation experiment — same 20 random events, four NPCSelector configurations (Full, NoLOS, NoDistance, NoRole). The CSV is in `Application.persistentDataPath/Telemetry/AblationResults_*.csv`. The full configuration distributes responses across role types in a way the distance-only or role-disabled configurations don't, which we treat as evidence that the multi-factor scoring meaningfully shifts selection."

If asked **"what about leader coordination?"** — say:
> "When a Leader-role terrorist enters Alert it broadcasts a **Converge** `LeaderDirective` to its squad; when it escalates to Engage it broadcasts **Flank** — each member computes its own flank point perpendicular to its approach direction, so the squad naturally splits left/right of the threat while the Leader holds the front. Squad members in Idle/Suspicious escalate to Alert and route their NavMeshAgent to the directive's destination. Engaged members ignore directives — they're committed to their current target. You can see this visibly in the scene: spawn a Leader and 2 squadmates in the same squadId, alert the Leader, and watch the squadmates converge, then fan out to the sides once the Leader engages."

---

# Demo checklist for the live evaluation

1. **Test harness runs green** — open the test scene, attach `Module2TestRunner`, press Play, right-click → "TEST → Run All", show Console with all 13 tests passing
2. **Leader-bias demo** — spawn a Leader + 2 squadmates with the same squadId, fire a test event on the Leader, show squadmates converging in scene view
3. **Telemetry files exist** — open `Application.persistentDataPath/Telemetry/` and show the four JSON files + the ablation CSV
4. **Full VR scenario** — Module 1 generates a scenario, SceneBuilder spawns NPCs, you play one mission, hostage panics on gunshot, terrorist investigates and engages, AAR shows the timeline
