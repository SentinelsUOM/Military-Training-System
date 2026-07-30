# Reasoning & Prediction for Module 4 Workload (SIM-TLX) Scores

**Sentinels VR Counter-Terrorism / Hostage-Rescue Trainer — University of Moratuwa**
Why a session's SIM-TLX composite scores come out the way they do, and what those scores predict about how the trainee actually performed — both grounded in cited research, in the same spirit as `MODULE4_MOVEMENT_VALIDATION_METHODOLOGY.md`.

---

## Why this exists

The Workload tab (`sentinels-aar/components/dashboard/SimTlxTab.jsx`) shows SIM-TLX composite/subscale scores (`lib/simTlx.js`) per session, but until now only ever displayed the numbers — never *why* a session's Task Complexity & Situational Stress composite was high, or *what that predicts* about the trainee's actual performance. This document backs a new reasoning-and-prediction layer (`lib/workloadReasoning.js`) with the research that justifies each rule.

Two distinct things are being added, and they lean on different kinds of evidence:

1. **Reasoning** — for each composite, which specific facts about *this session* (scenario element count, missed threats, friendly fire, movement telemetry) plausibly explain why it scored the way it did. This leans on established **theory** (Cognitive Load Theory, Multiple Resource Theory, Attentional Control Theory) as the explanatory mechanism, not a numeric formula — theory tells you *why* more rooms/hostiles raises perceived complexity, not *how many points* it should add.
2. **Prediction** — mapping the overall workload score to a predicted performance zone (under-aroused / near-optimal / overload-risk), then checking that prediction against this session's own outcome data (accuracy, friendly fire, reaction time, threat-response rate). This leans on **direct empirical findings** (the inverted-U stress-performance relationship) and is checked, not just asserted — the UI always shows whether the prediction actually held up for that session.

## Reasoning: why a composite scores the way it does

### Mental & Physical Demands
- **Physical Demands** rises with actual measured movement (`movementTrack.stats.totalDistance`). Sweller's Cognitive Load Theory (below) covers mental load; for the physical side, a marksmanship-task study found high **cognitive** load itself measurably changes gait — cadence, time-on-ground — meaning our movement telemetry is an independent, objective corroboration of reported physical/mental demand, not just a self-report echo.
  - *Predicting Cognitive Load and Operational Performance in a Simulated Marksmanship Task*, PMC7350508.
- **Mental Demands** rises with the number of discrete threat events requiring perceive→decide→act cycles. Wickens' Multiple Resource Theory explains why: concurrent tasks that draw the same processing resource (visual threat scanning, motor aiming, spatial monitoring) interfere and cost more mental effort than the raw task count implies.
  - Wickens CD (2008), "Multiple Resources and Mental Workload," *Human Factors* 50(3), 449–455.

### Temporal Demands & Frustration
- **Frustration** rises with missed threats and friendly-fire incidents. This is closest to a direct construct match: SIM-TLX's Frustration item literally asks "how insecure, discouraged, irritated, stressed or annoyed were you?" — negative in-mission outcomes are exactly what that question is measuring, and the SIM-TLX validation study confirmed frustration-type manipulations move this subscale specifically (not a different one).
  - Harris D, Wilson M, Vine S (2020), "SIM-TLX," *Virtual Reality* 24, 557–566.
- **Temporal Demands** rises when threats arrive at a high rate relative to mission length. A controlled multitasking study found NASA-TLX-family workload scores rose with the number of concurrent subtasks — direct empirical support that event *rate*, not just event count, drives perceived time pressure.
  - Li et al. (2022), "Evaluating mental workload during multitasking in simulated flight," *Brain and Behavior*, PMC9014989.

### Task Complexity & Situational Stress
- **Task Complexity** rises with scenario "element interactivity" — rooms, hostiles, and hostages that must all be tracked simultaneously. This is the core claim of Sweller's Cognitive Load Theory: intrinsic cognitive load is a function of how many interacting components must be held in working memory at once, not the raw difficulty of any one component. Multiple concurrent objectives (neutralize hostiles *and* protect hostages) compound this further, since they can conflict.
  - Sweller J (2010), "Element Interactivity and Intrinsic, Extraneous, and Germane Cognitive Load," *Educational Psychology Review* 22(2), 123–138.
- **Situational Stress** rises after a missed or mishandled engagement under threat. Attentional Control Theory holds that anxiety impairs *processing efficiency* (how much effort a given output costs) more than *performance effectiveness* (the output itself) — which is also why a stressed trainee's accuracy can hold up for a while before it doesn't: effort compensates, up to a point.
  - Eysenck MW, Derakshan N, Santos R, Calvo MG (2007), "Anxiety and Cognitive Performance: Attentional Control Theory," *Emotion* 7(2), 336–353.

**Every reasoning rule only fires when its condition is actually true for that session** (`lib/workloadReasoning.js`'s `REASONING_RULES`) — there is no generic "task complexity is high because scenarios are complex" filler text; each shown factor cites a real number from that session's `performance` or `movementTrack`, and scenario scale (room count, hostage count, hostile count) — from `layout.rooms.length`, `performance.hostagesTotal`, and distinct terrorist actors in `npcStateChanges`, not the `scenarioConfig` schema field, which is declared but never actually populated by the current Unity build.

## Prediction: what the workload score predicts about performance

The overall workload score is mapped to one of three zones, using the exact thresholds `lib/simTlx.js`'s `workloadLevel()` already uses (so the two never disagree):

| Overall workload | Zone | Prediction |
|---|---|---|
| < 25 | **Under-Aroused** | Vigilance/attention may lag rather than sharpen — the inverted-U model predicts underperformance at low arousal too, not only at high arousal. |
| 25–74 | **Likely Near-Optimal** | This band matches where elite shooters posted their best scores in a real inverted-U study — moderate workload is where performance is predicted to peak. |
| ≥ 75 | **Overload Risk** | Matches the range where the inverted-U model predicts a performance decline — though the source study found the drop is usually a moderate dip, not a collapse. |

This mapping is grounded directly in a real measurement of the inverted-U stress-performance relationship in a task closely analogous to ours:

- **"The inverted-U relationship between stress and performance in elite shooting"** (PMC12749011): using competition stage as an objective stress proxy, rifle shooters scored ~0.34 points higher (95% CI 0.278–0.393, p<0.0001) at the moderately-decisive stage 5 versus the least-decisive stage 1, and pistol shooters ~0.47 points higher at stage 4 versus stage 1 (95% CI 0.351–0.593, p<0.0001). Performance declined again at the most-decisive final stage, but "far less dramatically than the catastrophe model predicted" — the UI's Overload-Risk wording reflects that hedge rather than claiming a collapse.

**The prediction is always checked against the session's own outcome data**, not left as an unfalsifiable claim (`lib/workloadReasoning.js`'s `checkPrediction()`):
- **Overload Risk** is checked against friendly-fire count, accuracy score, and (reused from `lib/movementBenchmarks.js`) the session's reaction-time and threat-response-rate verdicts.
- **Under-Aroused** is checked against the same reaction-time/response-rate signals, looking for a vigilance dip despite low reported demand.
- **Near-Optimal** is checked against mission success and accuracy for confirmation.

If none of the expected evidence shows up, the UI says so explicitly (e.g. "performance held up despite the predicted overload risk — possibly compensatory effort") rather than forcing a match.

## Limitations

- **Self-report, not physiological measurement.** SIM-TLX ratings are subjective; the reasoning layer explains plausible drivers, it does not prove causation for any individual session.
- **The elite-shooter inverted-U study used competition stage as a stress proxy**, not a validated psychometric or physiological stress measure — the authors themselves note this as a limitation of their own study. The zone thresholds inherit that same uncertainty.
- **Attentional Control Theory's efficiency/effectiveness distinction is a theoretical mechanism**, not a numeric prediction — it explains *why* stress and steady accuracy can coexist temporarily, not *how long* that holds.
- **Reasoning rules are deliberately conservative**: a factor is only surfaced when a concrete session number crosses a stated threshold, but the thresholds themselves (e.g. "≥3 rooms and ≥2 hostiles") are reasonable engineering judgment calibrated to this project's scenario scale, not values taken from a published study.

## References

1. Sweller J (2010). Element Interactivity and Intrinsic, Extraneous, and Germane Cognitive Load. *Educational Psychology Review*, 22(2), 123–138. https://link.springer.com/article/10.1007/s10648-010-9128-5
2. Wickens CD (2008). Multiple Resources and Mental Workload. *Human Factors*, 50(3), 449–455. https://journals.sagepub.com/doi/10.1518/001872008X288394
3. Li et al. (2022). Evaluating mental workload during multitasking in simulated flight. *Brain and Behavior*, PMC9014989. https://www.ncbi.nlm.nih.gov/pmc/articles/PMC9014989/
4. Harris D, Wilson M, Vine S (2020). Development and validation of a simulation workload measure: the simulation task load index (SIM-TLX). *Virtual Reality*, 24, 557–566. https://doi.org/10.1007/s10055-019-00422-9
5. Eysenck MW, Derakshan N, Santos R, Calvo MG (2007). Anxiety and Cognitive Performance: Attentional Control Theory. *Emotion*, 7(2), 336–353. https://pubmed.ncbi.nlm.nih.gov/17516812/
6. The inverted-U relationship between stress and performance in elite shooting. PMC12749011. https://pmc.ncbi.nlm.nih.gov/articles/PMC12749011/
7. Predicting Cognitive Load and Operational Performance in a Simulated Marksmanship Task. PMC7350508. https://pmc.ncbi.nlm.nih.gov/articles/PMC7350508/
