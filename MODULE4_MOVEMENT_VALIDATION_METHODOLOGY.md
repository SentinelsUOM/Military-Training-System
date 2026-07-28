# Validating Module 4 Movement Telemetry Against Expert Benchmarks

**Sentinels VR Counter-Terrorism / Hostage-Rescue Trainer — University of Moratuwa**
How each per-session Movement-tab metric is compared against "what an expert would produce," what research backs each reference value, and what is honestly *not* backed by any literature.

---

## Why this exists

The dashboard's Movement tab (`sentinels-aar/components/dashboard/MovementTab.jsx`) records six per-session body-movement metrics — distance moved, average/peak speed, time crouched, head-scanning angle/speed, weapon-in-hand time, and hand travel — plus a reaction-time breakdown per threat stimulus (`ReactionTimeTracker.cs` → `movementTrack.reactionStats`). Until now these numbers were shown with no reference point: a trainee had no way to know whether "1.6 m/s average speed" or "0.9s reaction time" was good, bad, or ordinary.

There is no recorded dataset of expert/instructor sessions played through this simulator, so a literal "expert average" cannot be computed empirically. Instead, each metric is checked against a **reference band drawn from published research**, with an explicit confidence tier so a rough proxy is never presented with the same authority as a direct measurement.

The closest existing academic precedent for this whole approach is **Predicting closed quarters battle capability** (PMC12785213), which has expert raters score trainee CQB performance on five dimensions — tactical behavior, weapon handling, gaze behavior, response time, and mistakes — and finds these dimensions separate special-forces from non-specialized soldiers with large effect sizes (Cohen's d −1.099 to −1.993). Our six Movement-tab metrics plus reaction-time breakdown map onto essentially the same five dimensions, which is the justification for treating them as a valid axis for expert/novice comparison at all.

## Confidence tiers

| Tier | Meaning |
|---|---|
| **A** | Direct empirical reaction-time/performance figure from a study measuring materially the same task (perceive a threat cue → respond). |
| **B** | Literature proxy — the closest available published construct, applied to a different but related task (e.g. general gait-speed norms applied to tactical movement pace). Explicitly flagged as a proxy in the UI. |
| **C** | No quantified literature found. The trainee's value is shown; no expert range is claimed. Flagged "not literature-validated" rather than silently omitted, so a missing benchmark is never mistaken for "always fine." |

Implementation: `sentinels-aar/lib/movementBenchmarks.js` — `BENCHMARKS` holds the tier/range/citations per metric, `evaluateMovementAgainstExperts(session)` computes the trainee's value from `session.movementTrack` and returns a verdict (`below` / `within` / `above` / `unvalidated`) per metric, at render time, the same way `lib/cognitiveDerived.js` already derives fallback cognitive scores from telemetry.

## Metric-by-metric derivation

### Avg Speed — Tier B
**Reference band: 0.8–1.4 m/s.** Upper bound from Bohannon & Williams Andrews (2011), a meta-analysis of 41 studies (n=23,111) on normal adult walking speed, averaging 1.2–1.4 m/s on level ground. Lower bound reflects CQB movement doctrine (FM 3-06.11 / MCWP 3-11.3), which calls for deliberate, controlled movement during clearing — slower than a casual walk is expected, but a trainee who is essentially stationary (well under 0.8 m/s) is not moving with tactical purpose either. This is a proxy: the gait-speed literature was not collected in a CQB context, and no peer-reviewed study was found quantifying an "ideal" tactical clearing speed in m/s.

### Peak Speed — Tier C
No peer-reviewed source quantifies an expected peak/rush speed during CQB. Doctrine supports the qualitative expectation that peak speed should measurably exceed sustained average speed (brief bursts crossing exposed ground), but assigning a specific m/s figure to that burst would not be literature-backed, so no numeric range is claimed — the trainee's peak speed is shown alongside their average with no expert comparison.

### Time Crouched / Crouch Count — Tier C
Cover-and-concealment doctrine (FM 21-75 Ch.1; MCWP 3-11.3 §4001) establishes *that* using cover matters, but no source was found quantifying how much time an expert should spend crouched, or how many discrete crouch events are "correct" — this depends entirely on a given scenario's layout and threat placement, not a general constant. No numeric range is claimed for this metric.

### Head Scanning (peak angular speed) — Tier B
**Reference band: 30–100°/s.** This is a proxy at two removes: our system records head-yaw angular speed, not eye-gaze/saccade velocity, and the literature it's compared to measures the latter. Scholarpedia's summary of human saccadic eye movement gives a general saccade-velocity range of 30–100°/s. Separately, an active-duty-vs-civilian virtual environment study (PMC11086823) found active-duty soldiers scan with significantly faster peak saccade velocity and greater saccade magnitude than civilians (p<0.05), and fixate on more gaze-validated targets (median 15 vs 14, p=0.032) — directionally supporting "expert peak scanning speed should sit at or above a novice baseline," which is the qualitative claim actually being made here, not a precise head-yaw number.

### Weapon In Hand (% of mission) — Tier B
**Reference band: 90–100%.** Derived from CQB weapon-ready-posture doctrine (FM 3-06.11 / MCWP 3-11.3): the weapon should be presented and ready throughout active clearing, essentially continuously. This is a doctrine-derived quantification, not an empirical measurement of real operators' weapon-hold percentage — flagged tier B rather than A for that reason.

### Hand Travel (combined left+right, m) — Tier C
No study was found quantifying cumulative hand-travel distance for a VR/simulated weapon-handling task of this kind. Draw-stroke studies (see reaction-time section) measure *time*, not the cumulative distance a hand travels across a mission. No numeric range is claimed; the trainee's value is shown as-is.

### Reaction Time — per response channel
`ReactionTimeTracker.cs` records four independent response channels per stimulus: **head** (turned toward the threat), **hands** (raised weapon), **movement** (moved to cover), **trigger** (fired back). Reaction-time literature separates a similar pipeline — perception → decision → motor response — which maps unevenly onto these four channels:

- **Head/orienting response — Tier A, 0.25–0.40s.** Force Science's reaction-time breakdown puts simple threat-cue reaction at ~0.25s; the SCIRP visual-vs-auditory simple-reaction-time comparison gives a mean visual simple RT of ~331ms. Both measure "notice and begin to react," which is what the head channel captures.
- **Weapon-raise response — Tier A, 0.40–0.80s.** Nieuwenhuys et al. (2022, *Frontiers in Psychology*, PMC8905363) measured police cadets' shoot-scenario response time before and after gaze-control training: 1083ms pretest → 820ms posttest (trained group) vs 780ms → 743ms (control). The post-training/expert-leaning figure (~820ms) anchors the upper bound; the lower bound allows for faster-than-average performance.
- **Move-to-cover response — Tier B, 0.50–1.00s.** No study was found measuring reaction time specifically for "moved to cover" as a discrete response channel. This band is interpolated between the head-orienting figure and the full draw-and-fire figure below, since moving to cover is expected to follow initial detection but can precede a full weapon engagement.
- **Fire-back response — Tier A, 0.80–1.80s.** ALERRT/Texas State research and VirTra's "Time Analysis of a Certified Peace Officer" breakdown put average draw-and-fire response time at 1.43s–1.8s (range 0.93–2.4s across individual studies), matching the full perceive→decide→draw→fire pipeline this channel represents.
- **Overall average reaction time — Tier A, 0.40–1.00s.** Anchored on the same Nieuwenhuys et al. trained-cadet figures, since "average across all responded stimuli" in our data mixes channels the same way an aggregate live-fire scenario score would.

### Threats Responded (% of stimuli with any response) — Tier B
**Reference band: 90–100%.** No study reports a "response rate" percentage directly, but PMC11086823 found active-duty personnel fixate on significantly more gaze-validated targets than civilians (missing fewer), and PMC12785213 uses "mistakes" (including missed threats) as one of its five expert-discriminating CQB performance dimensions. Combined, these support a high (90–100%) expected response rate for expert-level performance, applied here as a proxy band rather than a directly measured percentage.

## Limitations

- **No expert baseline was recorded through this simulator.** Every band above is a literature reference, not a measurement of real experts playing this specific scenario set. The methodology explicitly recommends, as future work, recording instructor/SME sessions through the existing `CognitiveMovementRecorder` → `movementTrack` pipeline and replacing literature proxies with empirical ones where they diverge.
- **Head yaw ≠ eye gaze.** The head-scanning benchmark compares a head-tracking metric to eye-saccade literature; a trainee could hold their eyes still while yawing their head, or vice versa, in ways the source studies don't capture.
- **Several cited studies have modest sample sizes** (e.g. PMC12785213's special-forces group) or measure adjacent-but-not-identical populations (police officers vs. military CQB operators vs. this simulator's trainees) — treat all Tier B bands as directional guidance, not precision targets.
- **Tier C metrics (peak speed, crouch behavior, hand travel) have no expert comparison at all.** This is a deliberate choice over fabricating a number: the UI shows these as "not literature-validated" rather than implying a false level of rigor.

## References

1. Bohannon RW, Williams Andrews A (2011). Normal walking speed: a descriptive meta-analysis. *Physiotherapy*, 97(3), 182–189. https://www.sciencedirect.com/science/article/abs/pii/S0031940611000307
2. US Army FM 3-06.11 / USMC MCWP 3-11.3 — Combined Arms Operations in Urban Terrain (cover, concealment, and movement-technique doctrine). https://www.globalsecurity.org/military/library/policy/army/fm/21-75/Ch1.htm
3. Scholarpedia — Human saccadic eye movements. http://www.scholarpedia.org/article/Human_saccadic_eye_movements
4. Evidence of elevated situational awareness for active duty soldiers during navigation of a virtual environment. PMC11086823. https://pmc.ncbi.nlm.nih.gov/articles/PMC11086823/
5. Force Science Institute — Action vs. reaction: the shoot-first fallacy. https://www.police1.com/officer-shootings/articles/action-vs-reaction-the-shoot-first-fallacy-AxWyVoHO0jU9ByeJ/
6. Comparison between Auditory and Visual Simple Reaction Times. SCIRP. https://www.scirp.org/html/4-2400003_2689.htm
7. Nieuwenhuys A et al. (2022). Shoot or Don't Shoot? Tactical Gaze Control and Visual Attention Training Improves Police Cadets' Decision-Making Performance in Live-Fire Scenarios. *Frontiers in Psychology*, PMC8905363. https://www.ncbi.nlm.nih.gov/pmc/articles/PMC8905363/
8. ALERRT / Texas State University; VirTra — Time Analysis of a Certified Peace Officer's draw-and-fire response time. https://www.virtra.com/time-analysis-of-a-certified-peace-officers-blog/
9. Predicting closed quarters battle capability – Examining the influence of personality, attentional ability, 2D:4D-ratio and mindfulness on tactical performance. PMC12785213. https://pmc.ncbi.nlm.nih.gov/articles/PMC12785213/
