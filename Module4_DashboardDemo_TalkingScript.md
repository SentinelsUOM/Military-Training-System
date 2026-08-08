# Talking Script — Module 4 AAR Dashboard Walkthrough

**Session used:** `bb4d2ac8-9418-436c-9dec-313cdf776ad4` · 07 Aug 2026, 09:04 · `VR_TRAINING` · 4:26 · **Overall 30% · MISSION FAIL**

**Source of the numbers:** every figure quoted below is read directly off the recorded dashboard for this session — not reconstructed. Scrub the clip once and nudge each timecode by a second or two where it drifts.

Read at a measured, briefing-room pace. This walkthrough exists to prove ONE claim about Module 4:

> **A single score cannot tell a trainee what went wrong. This session is the proof — the trainee shot at *expert level* and still failed the mission completely, and only a multi-axis, research-calibrated AAR can show why.**

Keep coming back to that. Every tab is another angle on the same failure.

---

## Part 1 — The session list (home)

**0:00**
"This is the Sentinels After-Action Review dashboard — the Module 4 deliverable. Every mission a trainee plays in VR uploads here automatically when it ends. No manual data entry, no instructor tagging: the headset finishes the mission and the review is waiting."

**~0:10**
"Across the top are aggregate statistics over all **205 recorded sessions** — average overall score 46%, mission success rate 42%, and the four component scores Module 4 computes: safety 40%, accuracy 45%, speed 60%, operator safety 32%. Average reaction time 0.42 seconds. These are the population baselines a single session gets read against."

**~0:22**
"Below that, every session, newest first — session ID, player ID, date, duration, and the scores at a glance, colour-coded so a failed run is obvious without opening it. I'm going to open one specific failure, because it makes the point better than a success would."

---

## Part 2 — Summary tab: the headline contradiction

**~0:32**
"Session header: four minutes twenty-six, overall score 30%, mission **FAIL**. The tab bar tells you the volume of evidence behind that — 182 timeline events, 6 flagged incidents, 1,279 movement samples."

**~0:42 — Performance Radar & Score Breakdown**
"Here's the whole story in one shape. Look at the radar: it collapses on almost every axis — but there's one long spike. That spike is **Accuracy at 100%**. Now read the breakdown beside it: Hostage Safety **0%**. Operator Safety **13%**. Speed **0%**. Accuracy **100%**."

**~0:55**
"Ten shots fired, seven hit, zero friendly fire, two terrorists down — and zero of one hostages saved. This trainee could shoot. He still failed completely. A system that reported one number would have told him almost nothing useful. The rest of this dashboard is Module 4 explaining *which* thing went wrong, and how badly, against real-world reference values."

**~1:08 — Hostage Safety box**
"Hostage Safety breaks into three research dimensions. **Outcome** — was the hostage rescued: zero of one extracted, so 0%. **Well-being** — how much distress did they endure: peak distress hit 75%, so this scores 25%. **Weapon safety** — no friendly fire, clean, 100%."

**~1:22**
"And at the bottom of every score box, the comparison an evaluator actually wants: against real-world expert values. Hostage survival — this run scores 0%, on a scale where a novice outcome is 40% and professional tactical rescues save 85%. Verdict: **Novice**. The source is RAND's hostage-rescue outcome data: professional operations save roughly 70 to 90% of hostages; below 40% mirrors a mishandled or abandoned incident."

**~1:38 — Operator Safety box**
"This is the axis most tactical AAR tools don't have, and it's Module 4's distinctive contribution. Hostage Safety asks *was the hostage protected*. Operator Safety asks a completely separate question: **did you conduct yourself safely?** You can rescue the hostage and still score badly here — which means you'd have died in reality."

**~1:52**
"Overall 13%. Survivability **0%** — final health zero HP; he was killed. Exposure Control **0%** — time exposed **245 seconds** out of a 266-second mission. He was inside an enemy's line of sight for ninety-two percent of the run. Weapon Discipline **0%** — eight negligent discharges, **80% wild shots**. Threat Response 53%."

**~2:10**
"That negligent-discharge metric is worth pausing on, because it's calibrated, not invented. A negligent discharge here means a round fired with no terrorist visible at all. The benchmark is from firearms qualification-failure studies: experts still do this on about 17% of shots, novices about 61%. So some wild shots under stress is *normal* — 80% is not."

**~2:26 — Accuracy box**
"Now the one thing he did well, and this is where the research calibration earns its place. Accuracy overall: 100%. Raw hit rate: **70%** — seven of ten. Realism at range: 100%. Shot economy: 60% — five rounds per kill."

**~2:40 — Hits by engagement range**
"This table breaks his hits down by how far away the target was, against the NYPD SOP-9 field data. One hit under three metres — that's 14% of his hits, at a range where a real officer hits 50%. Six hits at three-to-seven metres — 86% of his hits, at a range where a real officer under stress hits just **13%**."

**~2:56**
"Read the honesty note under it: only hits record a distance — a miss never touches a target, so there's no range to measure. So this shows *where his successful shots came from*, not a hit-rate per band. We say that explicitly rather than implying a precision the data doesn't support."

**~3:08 — Accuracy vs expert**
"And the comparison: 70% against an expert benchmark of 13% at his measured average range of 3.3 metres. Verdict: **Expert**. That is not the system being generous — it's the system refusing to score him against a fantasy. Real officers under stress hit about 13% at this range. A 'low-looking' raw number can still be expert-level, and a naive accuracy score would have called this mediocre."

**~3:24 — Speed box**
"Speed: 0%. Completion pace — 266 seconds against a 125-second target for this scenario's scale: three rooms, four terrorists. Decisiveness — 56% of the mission actually moving."

**~3:36**
"Note the disclosure at the bottom: there is **no published expert time** for a hostage rescue, so Speed is deliberately shown *without* an expert benchmark, and the mission-time coefficients are stated as design defaults rather than research-sourced. Where Module 4 has a citation it shows it; where it doesn't, it says so. That distinction runs through the whole system."

**~3:50 — Cognitive metrics**
"Bottom left, the cognitive read: load high, stress high, average reaction time 0.49 seconds. Stability and Attention are marked **'est.'** — estimated — because Module 3's live cognitive feed isn't wired in during play, so these are derived from movement and reaction telemetry. Flagged as estimates rather than presented as measurements."

---

## Part 3 — Timeline tab

**~4:04**
"The Timeline tab. Activity density first — every shot, sighting, door and state change bucketed into ten-second slices. Tall peaks are the intense moments; flat stretches are searching, moving, or waiting. You can see the shape of the whole engagement without reading a single log line — the spike near 3:20 is the heaviest exchange of the mission."

**~4:18**
"Below it, the full event log — 182 events, filterable, with timestamp, type, actor, target and room. Watch the opening: at **0:21** terrorist_04 sees the player. Same second — enemy shot fired, gunshot heard, target confirmed. At **0:22**, EnemyHitPlayer. He was detected and taking rounds within twenty-two seconds of the mission starting, and as we saw, he never got out of contact after that."

---

## Part 4 — Incidents tab

**~4:36**
"Module 4 doesn't make a reviewer read 182 events to find the important ones. The Incidents tab auto-extracts notable moments and ranks them by severity — six here: one medium, five low, filterable across the top."

**~4:48**
"First Contact at 0:21. **AlertCascade at 0:21 — medium severity: four NPCs entered Alert state within five seconds.** That's the incident worth calling out, because it's Module 4 detecting a *Module 2* behaviour: the enemy squad sharing contact and responding as a coordinated group rather than four individuals. First shot at 0:22, terrorist_04 neutralised at 0:22, terrorist_02 at 4:23, and mission end at 4:26."

---

## Part 5 — Hostage tab

**~5:04**
"This is the part of Module 4 that's genuinely uncommon in tactical AAR tools. Most systems review the *operator's* tactics. Module 4 also reviews the **hostage's psychological experience** — because a rescue that technically succeeds can still traumatise the person you came for."

**~5:18**
"The distress index over time. It rises when something frightening happens and falls when they're calmed or freed. Every step in the line is a real recorded state change. Hover any point and you get three things: what the state means, why it ranks where it does on the scale, and the research basis for that ranking."

**~5:34**
"For this session: at 0:21 the hostage hears gunfire and becomes **Fearful**, 33%. At 0:40 a captor takes direct control — **Held**, 40% — and she stays there for most of the mission. At 4:05 the captor threatens her directly: **Threatened**, 75% — the peak. Then back to Held. She is never freed. This curve is the hostage's side of the same four minutes we just reviewed tactically."

**~5:52 — the honesty note**
"And read the note under the chart, because an evaluator will ask. The **order** of the states is research-derived, and the zero-to-one-hundred scale follows the clinical SUDS convention. The **specific percentages are assigned for readability** — they're a calibration, not measured values. SUDS is self-reported, so no study assigns a number to a state. Each cited source justifies *where a state sits relative to the others* — not the figure itself."

**~6:10 — the counter-intuitive ranking**
"The clearest example of that ordering being research-driven: **Freeze is scored above Panic.** Intuitively, freezing looks calmer. But peritraumatic tonic immobility is clinically shown to predict *worse* long-term psychological outcomes than actively panicking — so on a severity scale it belongs higher. That's a non-obvious call the system makes because the literature says so, not because it looks right."

**~6:26**
"Below, the emotional journey bar — each state's duration to scale — and a plain-English 'what happened and why' table: time, state, distress, and the cause. 'Heard gunfire nearby → became Fearful.' No jargon, no interpretation needed."

---

## Part 6 — Replay tab

**~6:42**
"Finally, replay. Standard transport controls — play, scrub, half, normal and double speed — and a phase label so you know where you are in the mission structure."

**~6:52**
"Top-down positions first: every actor's real recorded position over time, with a live actor-state panel beside it and the events active within two seconds of the current timestamp. This is reconstructed from stored transforms and state snapshots, not rendered video — which is why you can scrub it freely and inspect any moment."

**~7:06**
"And the 3D reconstruction — orbit, zoom and pan freely, or lock the camera to follow the trainee. The rooms and doors are the actual generated scenario geometry from Module 1, and the characters are the game's own animated models driven by their recorded state. This matters for a specific evidence-based reason: replaying a scenario from **multiple perspectives** is the single AAR feature with the strongest published link to learning gains — a police-VR study found multi-viewpoint replay significantly outperformed bird's-eye alone."

---

## Close

**~7:24**
"So — one session, one failure, six views of it. Accuracy said expert. Operator Safety said he was exposed for ninety-two percent of the mission, fired eighty percent of his rounds at nothing, and died. Hostage Safety said she was never rescued and peaked at 75% distress while held at gunpoint. Overall: 30%, fail."

**~7:40**
"No single number gets you there. That's the argument for Module 4: automatic capture from one event bus, five research-calibrated scores, a hostage-psychology axis most systems don't have, honest labelling wherever a value is a design choice rather than a citation — and a replay that lets you go and watch the moment it went wrong."

---

## Notes for whoever records/narrates this

- **The load-bearing beat is 0:42–0:55** — the radar spike on Accuracy against a total collapse everywhere else. If a viewer only remembers one thing, it's *"he shot like an expert and still failed."* Don't rush it; that contradiction is the entire argument for multi-axis scoring.
- **Second most important: ~3:08**, accuracy 70% vs expert 13%. It sounds wrong until you explain the NYPD baseline. Say the benchmark number *before* the verdict, or it lands as the system flattering the trainee.
- **Do not describe the percentages as research values.** Say "calibrated", "assigned for readability", or "the ordering is research-derived". This is the claim most likely to be challenged, and the dashboard already states the honest version on screen — match it.
- **The Workload tab is empty for this session** (no post-mission assessment was completed). Either skip it, or show it and say one line: *"the SIM-TLX workload assessment opens automatically after a mission; it wasn't completed for this run."* Don't silently skip past a visible empty tab — it reads as a broken feature.
- **The Movement tab (1,279 samples) isn't in the current recording.** If you want it, it sits between Hostage and Workload and shows body-telemetry — distance, speed, crouch time, head scanning, weapon-in-hand — each against literature reference bands with an explicit confidence tier. Worth 20 seconds if the take allows.
- **Two cosmetic things a sharp evaluator may spot** — decide in advance whether to address or avoid:
  - The home page's workload card reads *"39 assessments"* while the trend panel says *"0 rated sessions"*. Check which is right before recording; on camera it looks like a bug.
  - Scenario Configuration reads *"No scenario config recorded"* for this session. It's a known gap (Unity doesn't populate that field yet) — if it's on screen, either say so plainly or scroll past.
- **Terminology consistency with the other module scripts:** Module 2's claim is *coordinated*, not *tougher*. When the AlertCascade incident comes up (~4:48), frame it as coordination being detected, not difficulty.
- If re-recorded with a different session, every figure in Parts 2–5 changes. The structure holds, but re-read the numbers off the new run — do not reuse these.
