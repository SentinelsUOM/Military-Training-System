# Hostage Personality Profiles — Research Basis for Trait-Differentiated Emotional Response

**Sentinels VR Counter-Terrorism / Hostage-Rescue Trainer — University of Moratuwa**
Research grounding for giving each hostage a **personality profile** (**Weak / Normal / Brave**) that changes *how* they react to the same threat, rather than every hostage sharing one identical reaction curve.

> ### 🆕 Revision note — Rev. 2 (what changed since first draft)
>
> This document was revised after checking whether the per-profile numbers could be **derived** rather than chosen. Three substantive changes, all marked 🆕 in the body:
>
> | § | Change |
> |---|---|
> | **§3.2** | 🆕 **New finding added** — an *auditory* counterpart to the Looming Cognitive Style result (startle-latency study: high-anxiety individuals reacted identically to 60 dB and 85 dB; low-anxiety individuals discriminated). This is the same mechanism in the same sensory channel as a gunshot, and it became the design's central parameter. |
> | **§3.6** | 🆕 **New section** — documents a derivation that was attempted and **failed**: the dB→distance chain cannot separate profiles at CQB range. Recorded so it is not retried. |
> | **§4** | 🆕 **Arithmetic correction** — spawn weights were previously stated as ≈13/22/65%; correct normalisation is **10.9 / 21.4 / 67.7%**. The dropped *delayed-onset* trajectory is now disclosed. |
> | **§5** | 🆕 **Reduced from four profiles to three** (Weak / Normal / Brave) at the project's direction — better statistical power per cell, and matches Module 2's 3-tier structure. The London-Syndrome finding is folded into *Brave* rather than given its own profile. |
> | **§7** | 🆕 **Design inverted.** Distance thresholds are now **identical across all three profiles**; profiles differ **only** in threat discrimination. This removes every invented number from the differentiating parameters. |
>
> **Net effect:** the profile differences are now traceable to cited findings rather than to scaled defaults.

> **Companion to `HOSTAGE_EMOTION_SOUND_RESEARCH.pdf`.** That document answers *"how loud/close must a gunshot be to change a hostage's state?"* — the **stimulus** side. This document answers *"why do two people hearing the same gunshot react differently?"* — the **individual-differences** side. Together they define the full input space of the hostage emotional model: `state = f(stimulus, profile)`.

**Status:** research review and design specification. **Not yet implemented** at the time of writing — `HostageController.cs` currently applies one identical threshold set (`panicDistance = 6 m`, `freezeRange = 5 m`, `freezeThreshold = 3 s`) to every hostage. §7 specifies the change; §8 states what it would let Module 4 evaluate.

---

## 1. Purpose & the question this answers

Module 4's distress index (`HostageTab.jsx` → `STATE_INFO`) scores *what state* a hostage is in and *how severe* that is, validated against SUDS/polyvagal research. But the state machine that produces those states (Module 2's `HostageController.cs`) currently treats all hostages as psychologically identical — the same gunshot at the same distance always produces the same transition.

That is the one clearly unrealistic simplification remaining in the hostage model, and it is also the most *researchable* one. This document establishes:

1. Whether "different people default to different defensive responses" is a real, measured psychological construct (§2) — **it is**;
2. Which specific findings support **profile-differentiated** rather than merely **scaled** behaviour (§3);
3. What realistic profile *proportions* should be (§4) — the research gives hard numbers;
4. A critical correction: **"brave" is not simply "better"** in captivity (§5);
5. Implementation precedent in comparable training simulations (§6);
6. The concrete design (§7) and what it makes evaluable (§8).

---

## 2. The theoretical anchor — this is a personality trait, not a design conceit

### 2.1 Reinforcement Sensitivity Theory (RST) — the direct framework

The revised **Reinforcement Sensitivity Theory** (Gray & McNaughton) models personality as three neurobiological subsystems, one of which is literally named for the defensive responses in question:

| RST subsystem | Governs | Trait association |
|---|---|---|
| **FFFS** — Fight-Flight-Freeze System | Behavioural reactions to *fear* | Avoidance, fear-proneness |
| **BIS** — Behavioural Inhibition System | Response to *conflict/uncertainty* | Anxious personality traits |
| **BAS** — Behavioural Activation System | Approach to reward | Impulsivity, extraversion |

This matters because it means "which defensive response does this individual default to, and how readily" is **an established trait dimension in mainstream personality psychology**. A hostage profile is therefore a model of a real construct, not an invented game parameter.

### 2.2 There is a validated instrument for exactly this

The **Fight, Flight, Freeze Questionnaire (FFFQ)** — *Cognitive Behaviour Therapy*, 2015; 44(2): 117–127 — was developed specifically to measure **trait-like response to fear**, i.e. individual differences in fight vs. flight vs. freeze tendency. The published abstract reports "strong, initial support for the factor structure, reliability, and construct validity" of the measure.

> **Honest limitation:** the abstract does not expose the subscale names, sample size, or reliability coefficients (Cronbach's α), and full text was not accessible during this review. Cite the *existence and purpose* of a validated trait-fear instrument confidently; do **not** cite specific psychometric values without obtaining the full paper.

**Why this is the strongest single justification for the whole idea:** if a validated questionnaire exists to *measure* a person's trait fight/flight/freeze tendency, then modelling that tendency as a per-hostage profile is operationalising a measurable individual difference — the same logical move as scoring accuracy against NYPD hit rates rather than inventing a target.

---

## 3. Findings that support *profile-differentiated*, not merely *scaled*, behaviour

This is the crucial design distinction. A naive implementation would give the "vulnerable" hostage a larger `panicDistance` — the same curve, shifted. The research supports something better: **different profiles fail in different ways.**

### 3.1 The key paper — Looming Cognitive Style (adaptive vs. dysfunctional freezing)

**"Dysfunctional Freezing Responses to Approaching Stimuli in Persons with a Looming Cognitive Style for Physical Threats"** — *Frontiers in Psychology*, 2016, art. 521.

**Looming Cognitive Style (LCS)** is the internalised tendency to overestimate a threat's approach — to habitually perceive ambiguous threats as rapidly closing and escalating in danger. The study split participants by LCS score and found the two groups differed **qualitatively**:

| Group | Freezing response | Interpretation |
|---|---|---|
| **Low** physical looming | **Adaptive / selective** freezing — slowed only to *threatening* animals that were *approaching*; normal response to non-threatening or receding stimuli | Intact threat discrimination |
| **High** physical looming | **Dysfunctional / generalized** freezing — slowed to *anything* approaching, regardless of actual threat value | **Impaired** threat discrimination |

**Statistics:** n = 100 undergraduates (57 female), 84 in final analysis. Significant three-way interaction of motion direction × valence × physical looming, **F(1, 80) = 6.50, p = 0.01**. Physical-looming score negatively correlated with selective freezing, **r = −0.230, p = 0.035**.

The authors' conclusion — *"cognitive vulnerability to anxiety impairs the normal flexibility of defensive responses"* — is precisely the design principle to implement.

> **Design implication (load-bearing):** a **vulnerable** hostage should not simply freeze *sooner*. It should freeze at things a resilient hostage correctly ignores — e.g. a distant/receding gunshot, an approaching *trainee* (a rescuer, not a threat), a door opening. That is a materially more interesting and more defensible behaviour than a shifted threshold.

### 3.2 🆕 The auditory counterpart — the same mechanism, in the sense that actually matters here

§3.1's finding is **visual** (approaching stimuli). Because the hostage model's primary trigger is a **gunshot**, an auditory demonstration of the same mechanism matters far more — and one exists.

**"Social Anxiety and Latency of Response to Startle Stimuli"** — *Journal of Research in Personality* (ScienceDirect S0092656683710019):

> Individuals **high** in state anxiety responded with **equal latency to both lower (60 dB) and higher (85 dB) stimulus intensity levels**, whereas those **low** in state anxiety showed **differential responding based on stimulus intensity**.

In plain terms: **a high-anxiety person reacts the same way to a quiet sound and a loud one. A low-anxiety person reacts differently to each.**

Converging evidence across two sensory modalities:

| Modality | Study | Low-reactive individual | High-reactive individual |
|---|---|---|---|
| **Visual** | Looming Cognitive Style (§3.1) | **Selective** freezing — discriminates genuine approach-threat | **Generalized** freezing — cannot discriminate |
| **Auditory** | Startle latency (this section) | Response **scales with intensity** (60 dB ≠ 85 dB) | Response **flat across intensity** (60 dB = 85 dB) |

Two independent research groups, two different senses, one mechanism: **trait reactivity impairs the ability to grade a response to the size of the threat.** That convergence is what justifies making discrimination — rather than distance — the parameter that separates the profiles (§7).

> **Honest limits on this source.** It is an older study; it measures **state** (not trait) anxiety; it measures response **latency** (not a detection threshold); and it used 60/85 dB laboratory tones, far below gunfire SPL. It therefore transfers as a **directional mechanism** — *"high reactivity flattens the intensity-response relationship"* — and **not** as a magnitude. Do not convert its dB values into simulation distances (see §3.6 for why that conversion fails in general).

### 3.3 Trait anxiety predicts freezing — and correlates with a physiological marker

**"Human defensive freezing is associated with acute threat coping, long term hair cortisol levels and trait anxiety"** (bioRxiv preprint). Links threat-induced freezing to **trait anxiety** and to long-term hair cortisol, i.e. the tendency is both dispositional and physiologically corroborated rather than purely self-reported.

Trait anxiety is itself defined as a *basic personality trait* — a consistent tendency toward approach or avoidance in uncertain situations, with high scorers experiencing negative emotions more frequently and intensely. It is directly associated with hypervigilance and enhanced threat responding including freezing.

### 3.4 Freezing is trait-like and *stable across events* — which is what a "profile" means

Research on **peritraumatic tonic immobility (TI)** — the sustained, inescapable-threat freeze that Module 4 already scores at 72% distress (above Panic) — establishes both its triggers and its stability:

- **Necessary conditions for TI:** peritraumatic **physical restraint**, **high fear**, and **perceived inescapability**. *All three are structurally present in a hostage scenario* — which is why TI is a doctrinally appropriate state for this simulation in the first place.
- **Risk factors (who):** previous trauma and psychiatric history.
- **Stability (the key point):** **TI during a previous trauma was associated with elevated TI during current laboratory stress.** The tendency persists across separate events.

That cross-event stability is exactly the claim a persistent per-hostage profile encodes. Without it, a profile would be arbitrary; with it, a profile is modelling a documented dispositional tendency.

### 3.5 Big Five meta-analysis — direction of effect, with an important caveat

**"The Stressful Personality: A Meta-Analytical Review of the Relation Between Personality and Stress"** — Luo, Zhang, Cao & Roberts (2023), *Personality and Social Psychology Review*. **1,575 effect sizes from 298 samples.**

| Trait | Relation to stress |
|---|---|
| **Neuroticism** | **Positive** (more stress) — strongest predictor of both stressor exposure and perceived stress |
| Extraversion | Negative (less stress) |
| Agreeableness | Negative |
| Conscientiousness | Negative |
| Openness | Negative |

Also reported: higher neuroticism → higher **anticipatory** stress vulnerability; higher conscientiousness and extraversion → lower.

> ⚠️ **Critical caveat, from the same meta-analysis — state this in the report.** All five traits were significantly associated with **psychological stress *perception***, but the traits showed **weak to null associations with *physiological* stress response**. Personality predicts *subjective* distress far better than objective arousal.
>
> **Why this is favourable for Module 4 specifically:** the distress index is an ordinal *subjective-distress* analogue calibrated to **SUDS** (Subjective Units of Distress) — the exact construct personality *does* predict well. So profile-differentiated distress is defensible. What must **not** be claimed is that profiles would shift physiological measures; the meta-analysis says they largely wouldn't.

*(Note: the abstract does not report effect-size magnitudes — cite direction and the 1,575/298 scale, not specific r or d values, unless full text is obtained.)*

### 3.6 🆕 A derivation that does **not** work — recorded so it is not retried

An obvious and attractive idea is to **derive** each profile's distance thresholds from the dB→state table in `HOSTAGE_EMOTION_SOUND_RESEARCH.pdf`, by assigning each profile a different published dB threshold and inverting the propagation law. **This was attempted and it fails.** The arithmetic is recorded here because the idea will otherwise recur.

Inverting the companion document's own law, `SPL(d) = 160 − 20·log₁₀(d)` ⟹ `d = 10^((160 − SPL)/20)`:

| Published dB threshold | Source | Implied distance |
|---|---|---|
| 160 dB — pain / injury threshold | PMC9450760 | **1 m** |
| 137 dB — occupational SPL ceiling; Fear → Panic if inescapable | JASA | **14.1 m** |
| 116 dB — standard startle-chamber stimulus | startle methodology | **158 m** |
| 93 dB — acoustic startle threshold | companion doc §5 | **2,238 m** |

**Three reasons this cannot separate profiles:**

1. **The thresholds are decades apart in distance.** Consecutive published thresholds map to 1 m → 14 m → 158 m → 2.2 km. A CQB building is ~20–40 m across, so there is no room *between* thresholds to place three profiles.
2. **Gunfire saturates the scale indoors.** At every in-building range, gunfire exceeds 137 dB — so by the research's own table, *every* hostage is in the Fear/Panic band at *every* in-building distance, regardless of profile.
3. **Free-field inverse-square is the wrong propagation model indoors.** The companion document states the law assumes "free field, no reflective surfaces," and separately notes indoor reverberation extends the disturbance to ~100+ ms. Real indoor SPL therefore falls off *more slowly* than the law predicts, widening rather than narrowing the problem.

**Corollary — a correction to a plausible assumption about the existing values.** The current `panicDistance = 6 m` / `freezeRange = 5 m` are sometimes assumed to have been computed from a dB threshold. They were not, and cannot have been: the nearest relevant threshold (137 dB) maps to **14.1 m**, not 5–6 m. What they *are* is **consistent with** the companion document's §7 band recommendation — *"Close ≤ 10 m → Panic/Fear (always)"* — which is a research-derived **band**, not a computed point value. That is a legitimate and citable grounding; it simply does not uniquely determine 5 or 6, and it certainly cannot generate three distinct per-profile values.

**Consequence for the design:** distance is not a defensible axis on which to separate profiles. §3.2's discrimination mechanism is — which is why §7 holds the distances constant and varies only discrimination.

---

## 4. Realistic profile proportions — the research supplies hard numbers

An equal 33/33/33 split would be convenient but wrong. The canonical trajectory review gives real population proportions:

**Galatzer-Levy, Huang & Bonanno (2018), "Trajectories of resilience and dysfunction following potential trauma: A review and statistical evaluation"** — *Clinical Psychology Review*.

| Trajectory | Prevalence (average across populations) |
|---|---|
| **Resilience** | **65.7%** — the *modal* response |
| **Recovery** | **20.8%** |
| **Chronicity** | **10.6%** |
| *(Delayed onset)* | *reported in the 0–15% range across individual studies* |

The four prototypical trajectories consistently identified are **resilience, recovery, chronicity, and delayed onset** — with resilience by far the most common, a finding Bonanno's programme has emphasised repeatedly (roughly two-thirds of people).

### 4.1 🆕 Normalising three trajectories onto three profiles (and disclosing what was dropped)

The three trajectories used here sum to **97.1%**, not 100%. The remainder is the **delayed-onset** trajectory, which is deliberately **not modelled**: it describes symptoms emerging *weeks to months after* the event, which has no meaningful counterpart inside a single ~5-minute mission. Dropping it is defensible, but must be disclosed rather than silently absorbed.

Renormalising the remaining three to 100%:

| Profile | Source trajectory | Raw | **Normalised spawn weight** |
|---|---|---:|---:|
| **Brave** | Resilience | 65.7% | **67.7%** |
| **Normal** | Recovery | 20.8% | **21.4%** |
| **Weak** | Chronicity | 10.6% | **10.9%** |
| *(not modelled)* | *Delayed onset* | *~2.9% residual* | — |

> **Design implication:** the default spawn distribution is ≈ **68 / 21 / 11**, not equal thirds. This is directly citable calibration — functionally the same kind of grounding the NYPD SOP-9 bands give the accuracy score. A scenario where *most* hostages fall apart would be the unrealistic configuration, and a trainee should meet a composed hostage far more often than a fragile one.
>
> 🆕 **Correction to Rev. 1:** an earlier draft of this document stated ≈13 / 22 / 65%. Those figures were eyeballed to sum to 100 rather than computed; **10.9 / 21.4 / 67.7** is the correct normalisation. The Rev. 1 figures overstated the Weak profile by ~2 percentage points.
>
> **Note on evaluation runs:** these weights govern **random/training mode only**. Evaluation runs force a specific profile (§7.2), so prevalence never confounds a controlled comparison.

---

## 5. The important correction — "brave" is **not** simply "better" in captivity

This is the finding most likely to change the design, and it is worth foregrounding in the report because it is counter-intuitive.

### 5.1 The four-response typology includes **fawn**, and fawn is the captivity-typical response

The trauma-response typology is **fight / flight / freeze / fawn**. In a hostage or otherwise inescapable environment, *fight and flight are unavailable* — so the response that emerges is **fawn**: appeasement and compliance directed at the captor. Hostages learn that survival requires becoming attuned to their captors and developing compliant, dependent behaviour.

Module 4's existing distress model already encodes something close to this: **`Follow` = 10%** distress, justified as *"contact with a rescuer provides a visible escape route — appraisal of an available escape sharply lowers arousal"*, which is the same appraisal mechanism (Bracha 2004) running in the *positive* direction.

### 5.2 London Syndrome — defiance is associated with being killed

Captivity psychology names three phenomena, and the *third* is the relevant one:

| Phenomenon | Description | Prevalence / outcome |
|---|---|---|
| **Stockholm Syndrome** | Hostage develops positive feelings toward captor | Only **~8%** of hostages |
| **Lima Syndrome** | *Captor* develops feelings for hostage | — |
| **London Syndrome** | Hostage becomes **aggressively defiant**, argues with/challenges captors | **Historically the hostages who get executed** (the Iranian Embassy siege is the origin case) |

Source: *Hostage-taking: motives, resolution, coping and effects* (**Advances in Psychiatric Treatment**, Cambridge) and Stockholm-syndrome research overviews.

### 5.3 🆕 Consequence for the profile scheme — three profiles, with defiance folded into *Brave*

The §5.2 finding means "brave" conflates two behaviours with *opposite* survival outcomes: staying **functional** (good) and being **defiant** (bad). An earlier draft resolved this by adding a fourth *Defiant* profile.

**The project settled on three profiles** — Weak / Normal / Brave — for two sound reasons: with a limited participant pool, three cells give materially better statistical power per cell than four; and three matches Module 2's existing 3-tier ablation structure, so the same statistical machinery applies unchanged.

The London-Syndrome finding is therefore **retained inside the Brave profile** as a secondary trait rather than discarded:

| Profile | Trait grounding | Behaviour in scenario | Outcome |
|---|---|---|---|
| **Weak** | High neuroticism / trait anxiety / **high looming** (§3.1) + **flat intensity-response** (§3.2); chronicity trajectory | **Cannot grade the threat** — reacts to a distant shot as though it were point-blank; may freeze at receding gunfire or at the *approaching rescuer*; slow to extract | ❌ **Poor** |
| **Normal** | Population-typical; recovery trajectory | Grades the threat correctly most of the time, with occasional lapses — the **current, unchanged** behaviour | ➖ Baseline |
| **Brave** | Low neuroticism, low looming (§3.1), **intensity-graded response** (§3.2); resilience trajectory · **plus** a small London-Syndrome defiance tendency (§5.2) | Grades the threat correctly; low peak distress; recognises and follows the rescuer quickly — **but occasionally defies the captor**, which raises execution risk | ✅ Good, ⚠️ **with a tail risk** |

**Why keeping defiance inside Brave matters pedagogically.** It preserves the counter-intuitive lesson at zero structural cost: in the AAR, the hostage who read as *calmest and most composed* can turn out to be the one who got executed. A trainee learns that low observed distress is **not** the same as low risk — a genuine, research-grounded insight the current single-profile model cannot produce, and one that fits Module 4's distinctive angle (hostage *psychological* AAR, not merely tactical AAR of the trainee).

*Implementation note:* this is a single `defianceChance` float on the Brave profile only, gated to captor-proximity events. If the project prefers Brave to be unambiguously positive, setting it to `0` disables the behaviour without any structural change — but §9 then carries the omission as a stated limitation.

---

## 6. Implementation precedent in comparable systems

### 6.1 The standard technique — personality → appraisal → emotion

The established architecture for personality-driven NPC affect is **Big Five/OCEAN for the trait layer + OCC (Ortony, Clore & Collins) for the appraisal/emotion layer**. Personality values are typically defined per-NPC in a normalised range and mapped onto the five factors, then OCC appraisal rules convert events into emotional responses filtered by those traits. The open-source **FAtiMA Toolkit** implements exactly this (OCC-based appraisal for socioemotional agents) and is the reference implementation in the literature.

*Relevance:* this confirms the `state = f(stimulus, profile)` shape is the field-standard approach, not a bespoke invention. Sentinels' existing FSM + distance thresholds is a simpler variant of the same pattern; a profile layer is the natural extension.

### 6.2 Closest published match — and it is very recent

**"Virtual Human for Police Training in Virtual Reality: A Dynamic Model of a Suspect With Modulated Resistance and Means"** — *Computer Animation and Virtual Worlds*, 2026 (Tisserand et al.).

This modulates a virtual human's **behavioural parameters** to generate *differentiated threat perceptions* in the trainee — finding that resistance primarily influenced perceived danger and hostile intent, while other parameters affected perceived capacity and opportunity. **Same technique, same domain (VR police/tactical training), applied to a suspect rather than a hostage.**

*Relevance:* this is the strongest "someone credible does this" citation, published in a peer-reviewed venue, in the same year, for the same class of trainer. It positions Sentinels' hostage profiles as an application of a current, active research direction rather than an untested idea.

### 6.3 Perspective-taking evidence supports revealing profile in the AAR

VR training research reports that **perspective-taking increases officer empathy with effects lasting 8+ weeks** and improves de-escalation. Combined with the multi-perspective-replay finding Module 4 already cites (Nguyen et al., *Ergonomics* 2023 — replaying from multiple viewpoints produced significantly greater learning efficacy than bird's-eye alone), this supports **revealing the hostage's profile and internal state in the after-action review**, where it becomes a reflection aid.

---

## 7. Design specification

### 7.1 🆕 Distances stay **identical**; profiles differ **only** in discrimination

This inverts the Rev. 1 design, and the inversion is the single most important decision in this document.

**Rev. 1 (superseded)** gave each profile its own `panicDistance` / `freezeRange` / `freezeThreshold`. §3.6 shows those distances cannot be derived from any published threshold — so all six of the non-default values would have been **invented numbers wearing a research citation**. That is precisely the failure mode `MODULE4_EVALUATION.md` §8 warns about for the Speed coefficients.

**Rev. 2 (adopted):** all three profiles keep the **existing, unchanged** distance values, which are already research-*consistent* via the companion document's "Close ≤ 10 m" band (§3.6):

| Field | Weak | Normal | Brave | Status |
|---|:--:|:--:|:--:|---|
| `panicDistance` | 6 m | 6 m | 6 m | **unchanged for all three** |
| `freezeRange` | 5 m | 5 m | 5 m | **unchanged for all three** |
| `freezeThreshold` | 3 s | 3 s | 3 s | **unchanged for all three** |
| **`threatDiscrimination`** | **low** | **mid** | **high** | 🆕 **the only differentiator** |

**Why this is strictly stronger:**

1. **No invented numbers on the differentiating axis.** Every profile difference traces to §3.1 + §3.2.
2. **One parameter to sensitivity-test instead of four** (§9).
3. **`Normal` is bit-identical to current behaviour**, so "did profiles break the existing hostage?" is answerable by regression-testing Normal alone.
4. **Behaviourally more interesting.** Weak does not get a bigger panic bubble — it *misreads the situation*, which is visible to the trainee and produces the emergent effects §8 measures.

### 7.2 🆕 What `threatDiscrimination` actually does

It is the probability that a **non-threatening** stimulus is correctly recognised as non-threatening. It implements §3.2's finding directly: *does the hostage's reaction scale with the intensity of the stimulus, or is it flat?*

```
HostageProfile (ScriptableObject)
  ├─ profileName           : Weak | Normal | Brave
  ├─ threatDiscrimination  : 0..1   ← the ONLY differentiating parameter
  │     high = response GRADES with stimulus intensity/proximity (§3.2 low-anxiety pattern)
  │     low  = response is FLAT — a distant shot is treated like a point-blank one
  └─ defianceChance        : 0..1   ← Brave only; London-Syndrome tail risk (§5.3)
                                      set to 0 to disable, see §9
  (panicDistance / freezeRange / freezeThreshold are NOT per-profile — see §7.1)
```

Decision logic, in essence:

```
onThreatStimulus(stimulus):
    if stimulus.isGenuineThreat:
        respond(scaledTo: stimulus.intensity)      // all profiles react
    else:                                          // distant / receding shot, or the RESCUER
        if Random.value > threatDiscrimination:
            respond(asMaximumThreat)               // MISREAD — the Weak signature
        else:
            ignoreOrCalm()                         // correctly discounted
```

The same four events, by profile — this table is the design in one view, and is what §8 tests:

| Event | **Brave** (high discrimination) | **Normal** (mid) | **Weak** (low) |
|---|---|---|---|
| Gunshot **20 m away**, other room | Fearful *(mild — correctly graded)* | Fearful | **Panic** *(misread as point-blank)* |
| Gunshot **3 m away**, same room | **Panic** *(correct — genuine threat)* | Panic | Panic |
| Gunfire **receding / getting quieter** | **Calms** *(detects it leaving)* | Mostly calms | **Stays frozen** *(cannot detect it leaving)* |
| **Rescuer approaches** | **Follow** *(recognises help)* | Follow | **Freeze** *(cannot distinguish rescuer from captor)* |

Read the **columns**: Brave's varies with the situation (graded); Weak's is nearly constant (flat). That contrast *is* the §3.2 finding, and the bottom row is what drives the emergent trainee-facing effects in §8.

### 7.3 Spawn distribution

Weight profile assignment to the §4.1 normalised proportions — ≈ **68% Brave / 21% Normal / 11% Weak** — rather than uniformly. Expose the weights, and expose a **force-profile** override so an evaluator can run a controlled comparison (§8). Random weighting applies to training mode only; evaluation runs always force the profile.

### 7.4 State label above the hostage's head — recommended placement

Technically trivial (world-space UI patterns already exist in `MissionResultUI`, `SafeZoneBeacon`, `PlayerDamageFeedback`). **But it should not be visible in the trainee's live VR view.**

| Context | Show label? | Reason |
|---|---|---|
| **Dev/debug toggle** in Editor | ✅ Yes | Essential for verifying profile behaviour during development |
| **Dashboard 3D replay** (AAR) | ✅ Yes | Post-hoc analysis; supported by the perspective-taking / multi-perspective-replay evidence (§6.3) |
| **Instructor / spectator view** | ✅ Optional | Same rationale as replay |
| **Trainee's live VR HMD view** | ❌ **No** | A real operator cannot see a floating "PANIC" tag. Showing it live is an **unrealistic cue** that would (a) undermine the construct validity Module 4's evaluation rests on, and (b) let the trainee *read* the hostage's state instead of learning to *read behaviour* — which is the actual skill being trained |

This is a deliberate, stated design decision rather than a missing feature, and should be documented as such.

---

## 8. What this makes evaluable (Module 4 relevance)

Implementing profiles creates a **new falsifiable claim** testable with infrastructure Module 4 already has:

> *Do hostage distress trajectories differ by profile in the direction and shape the research predicts?*

- **Predicted:** Weak > Normal > Brave in peak distress and in time-to-Freeze; Weak additionally shows freeze events at stimuli the other profiles ignore (the §3.1/§3.2 discrimination signature); Brave shows *low* distress but a small elevated `hostage_executed` rate (§5.3 defiance tail).
- **Test:** the **one-way ANOVA** already built in `sentinels-aar/lib/stats.js` (`oneWayAnova`), with **profile as the grouping factor** instead of player — exactly the substitution the existing `AnovaPanel` supports.
- **Aggregate check:** rescue-survival rate by profile should remain inside RAND's 70–90% professional band overall while differing between profiles.

### 8.1 🆕 Separating **verification** from **discovery** — the critical distinction

Not every profile comparison is a real finding, and conflating the two would undermine the whole evaluation. Because the profile parameters are *authored*, some results are **circular by construction** and must be labelled as such.

| Claim | Type | Circular? | Report as |
|---|---|---|---|
| Weak reaches higher peak distress than Brave | **Verification** | ✅ Yes — follows from the authored discrimination value | "implementation behaves as specified" |
| Weak's freeze events include stimuli the others ignore | **Verification** of the §3.1/§3.2 signature | Partly | "reproduces the documented discrimination pattern" |
| Distress-trajectory *shape* matches the research-predicted pattern | Verification | Partly | "faithful to the source model" |
| **Profile changes trainee rescue time and Hostage Safety score** | 🎯 **Discovery** | ❌ **No** | **headline finding** |
| **Profile changes the mission-outcome distribution** | 🎯 **Discovery** | ❌ No | **headline finding** |
| **Brave's defiance tail raises `hostage_executed` rate** | 🎯 **Discovery** — emerges from guardian-AI interaction | ❌ No | **headline finding** |

**Why the discovery rows are genuinely non-circular:** nothing in the profile definition sets *how long a trainee takes to extract a frozen hostage*, or *how the guardian AI responds to a defiant one*. Those emerge from the interaction of the profile with the trainee's behaviour and Module 2's AI. A Weak hostage that freezes at the approaching rescuer (§7.2, bottom row) makes extraction slower **in fact**, not by fiat — which then moves the trainee's Speed and Hostage Safety scores.

**Recommended framing for the report:** lead with the discovery rows; present the verification rows as a *correctness check on the implementation*, explicitly labelled non-inferential. A `p < 0.001` on a circular claim is worse than no claim at all, because an examiner who spots the circularity will discount every other number in the section.

### 8.2 🆕 Study design — mirror Module 2's ablation

The cleanest design reuses the shape Module 2 already validates:

| | Module 2 (existing) | Hostage profile (proposed) |
|---|---|---|
| Independent variable | AI level (Basic / Inter / Advanced) | **Hostage profile** (Weak / Normal / Brave) |
| Design | Within-subjects, same scenario seed | **Same** — one trainee, same seed, profile forced |
| Statistics | Friedman + Wilcoxon post-hoc | **Same engine**, or one-way ANOVA + η² |

Same participant, same seed, only the profile varies — a proper experimental manipulation rather than an observational comparison, and it requires no new statistical code.

**Suggested reporting set:** one-way ANOVA with **η² effect size** (matching Module 1's existing convention); a **chi-square test of independence** for the categorical outcome (profile × mission-end-reason), which is the correctly-chosen test for that data type rather than reusing ANOVA; and a **distress-trajectory overlay chart** (mean curve per profile with the predicted band shaded behind).

This is a *stronger* evaluation claim than the current hostage model permits, because it is a **directional prediction derived from published research before the data is collected**, rather than a post-hoc description.

> **Sample-size reality check.** Three profiles need meaningful *n* per cell. As of writing the project has **one** genuine human participant (`MODULE4_FULL_REPORT.md` §6.8), so this design currently demonstrates the *mechanism* only. Plan the run schedule alongside the implementation.

---

## 9. Limitations & honest gaps

1. **No profile data exists yet.** Nothing in §8 has been measured; the predictions are pre-registered expectations, not results.
2. 🆕 **The three `threatDiscrimination` values remain design calibrations.** Rev. 2 removed the invented *distance* values (§7.1), but the low/mid/high discrimination levels themselves are still chosen, not derived — the research supplies the **mechanism and its direction** (§3.1, §3.2), not a numeric probability. **Mitigation, and the recommended one:** a **sensitivity analysis** showing the qualitative ordering (Weak > Normal > Brave in peak distress) is stable across a range of discrimination values, so the exact numbers demonstrably do not drive the conclusion. This is exactly the remedy `PLAYER_SAFETY_SCORE.md` §4.6 prescribes for the operator-safety weights, and it is now cheaper here because there is **one** parameter to sweep rather than four. Do not present the discrimination values themselves as research-derived.
3. 🆕 **The §3.2 source is directional only.** It is an older study measuring **state** anxiety and response **latency** (not a trait-level detection threshold), using 60/85 dB laboratory tones far below gunfire SPL. It supports *"high reactivity flattens the intensity–response relationship"*; it does **not** license converting its dB values into simulation distances (§3.6).
4. **Personality predicts subjective, not physiological, stress** (§3.5). Constrain claims to the distress index; do not claim profile effects on physiological arousal.
5. **The FFFQ's internals were not obtained** (§2.2) — cite the instrument's existence and purpose, not its psychometrics, without full-text access.
6. **Looming Cognitive Style was measured on undergraduates** (n=84 analysed) in a laboratory approach-avoidance task, not on hostages in captivity. The mechanism transfers as a *directional* principle; the effect size does not transfer to this population.
7. **London Syndrome is a case-derived clinical observation**, not a controlled-study effect with a prevalence figure (unlike Stockholm's ~8%). Treat the "defiance raises execution risk" link as historically-grounded and clinically described, not statistically quantified. 🆕 If `defianceChance` is set to 0 (§5.3), record the omission here — the model then deliberately excludes a documented captivity phenomenon.
8. 🆕 **The delayed-onset trajectory is not modelled** (§4.1). Spawn weights are a renormalisation of three of the four published trajectories; the ~2.9% residual is dropped because delayed onset has no counterpart inside a single mission. Disclose this rather than presenting 68/21/11 as raw published values.
9. **Profiles are not the same as the sound model.** Where a state transition is driven by SPL/distance, `HOSTAGE_EMOTION_SOUND_RESEARCH.pdf` governs; profiles modulate only the **discrimination** applied to that stimulus (§7.1 — distances are no longer per-profile). Keep the two documents' claims distinct.

---

## 10. References

1. Gray, J.A. & McNaughton, N. — revised **Reinforcement Sensitivity Theory** (BAS / BIS / **FFFS**: Fight-Flight-Freeze System). Foundational personality framework for trait-level defensive response.
2. **Fight, Flight, Freeze Questionnaire (FFFQ)** — development and psychometric investigation of an inventory to assess fight, flight and freeze tendencies. *Cognitive Behaviour Therapy*, 2015; 44(2): 117–127. https://pubmed.ncbi.nlm.nih.gov/25365751/
3. **Dysfunctional Freezing Responses to Approaching Stimuli in Persons with a Looming Cognitive Style for Physical Threats.** *Frontiers in Psychology*, 2016, art. 521. https://www.frontiersin.org/journals/psychology/articles/10.3389/fpsyg.2016.00521/full
3b. 🆕 **Social Anxiety and Latency of Response to Startle Stimuli.** *Journal of Research in Personality.* — the **auditory** counterpart to [3]: high state-anxiety individuals responded with equal latency to 60 dB and 85 dB stimuli, low state-anxiety individuals discriminated between them. The load-bearing source for `threatDiscrimination` (§3.2, §7.2). https://www.sciencedirect.com/science/article/abs/pii/S0092656683710019

3c. 🆕 **Greater general startle reflex is associated with greater anxiety levels: a correlational study on 111 young women.** *Frontiers in Behavioral Neuroscience*, 2015. (Supporting: startle magnitude scales with anxiety level; the general startle reflex is a stable individual trait.) https://www.frontiersin.org/journals/behavioral-neuroscience/articles/10.3389/fnbeh.2015.00010/full

4. **Human defensive freezing is associated with acute threat coping, long term hair cortisol levels and trait anxiety.** bioRxiv preprint. https://www.biorxiv.org/content/10.1101/554840.full.pdf
5. Luo, J., Zhang, B., Cao, M. & Roberts, B.W. (2023). **The Stressful Personality: A Meta-Analytical Review of the Relation Between Personality and Stress.** *Personality and Social Psychology Review.* (1,575 effect sizes, 298 samples.) https://journals.sagepub.com/doi/abs/10.1177/10888683221104002 · https://pubmed.ncbi.nlm.nih.gov/35801622/
6. Galatzer-Levy, I.R., Huang, S.H. & Bonanno, G.A. (2018). **Trajectories of resilience and dysfunction following potential trauma: A review and statistical evaluation.** *Clinical Psychology Review.* (Resilience 65.7% / Recovery 20.8% / Chronicity 10.6%.) https://www.sciencedirect.com/science/article/abs/pii/S0272735818300539
7. Bonanno, G.A. — **Resilience in the Face of Potential Trauma** (four prototypical trajectories). https://dhstraumaresourcelibrary.alleghenycounty.us/wp-content/uploads/2019/02/Resilience-in-the-Face-of-Potential-Trauma.pdf
8. **Hostage-taking: motives, resolution, coping and effects.** *Advances in Psychiatric Treatment*, Cambridge University Press. (Stockholm / Lima / **London** syndromes; hostage coping.) https://www.cambridge.org/core/journals/advances-in-psychiatric-treatment/article/hostagetaking-motives-resolution-coping-and-effects/1EC7C6FE7B502B95234C5BE2C1E9995F
9. **Stockholm Syndrome — research overview** (≈8% prevalence; London Syndrome as aggressive defiance). EBSCO Research Starters. https://www.ebsco.com/research-starters/social-sciences-and-humanities/stockholm-syndrome
10. **Exposure to trauma-relevant pictures is associated with tachycardia in victims who had experienced an intense peritraumatic defensive response: tonic immobility.** PMC4274794. (TI stability across events.) https://www.ncbi.nlm.nih.gov/pmc/articles/PMC4274794/
11. **Tonic immobility predicts poorer recovery from posttraumatic stress disorder.** *Journal of Affective Disorders* / ScienceDirect. (Already cited by Module 4 for the Freeze > Panic ordering.) https://www.sciencedirect.com/science/article/abs/pii/S0165032718329689
12. **Sexual trauma is more strongly associated with tonic immobility than other types of trauma — a population-based study.** ScienceDirect. (TI risk factors.) https://www.sciencedirect.com/science/article/abs/pii/S0165032716317220
13. **Fight, flight, and freeze: Threat sensitivity and emotion dysregulation in survivors of chronic childhood maltreatment.** ScienceDirect. https://www.sciencedirect.com/science/article/abs/pii/S019188691400289X
14. Nierman et al. (2017). **Individual differences in defensive stress-responses.** EPAN Lab. https://www.epanlab.nl/wp-content/uploads/2017/03/Nierman-et-al-2017.pdf
15. Bracha, H.S. (2004). **Freeze, Flight, Fight, Fright, Faint: Adaptationist Perspectives on the Acute Stress Response Spectrum.** *CNS Spectrums.* (Already central to `HOSTAGE_EMOTION_SOUND_RESEARCH.pdf`; the appraisal mechanism profiles modulate.)
16. Tisserand, Y. et al. (2026). **Virtual Human for Police Training in Virtual Reality: A Dynamic Model of a Suspect With Modulated Resistance and Means.** *Computer Animation and Virtual Worlds.* https://onlinelibrary.wiley.com/doi/10.1002/cav.70129
17. **Start Your EM(otion En)gine: Towards Computational Models of Emotion for Improving the Believability of Video Game Non-Player Characters.** arXiv. (OCC / FAtiMA / OCEAN survey.) https://arxiv.org/pdf/2307.10031
18. **FAtiMA Toolkit** — open-source OCC-based appraisal architecture for socioemotional agents.
19. Nguyen, Q. et al. (2023). **Changing perspectives: enhancing learning efficacy with the after-action review in virtual reality training for police.** *Ergonomics.* (Multi-perspective replay → greater learning efficacy; supports §6.3/§7.3.) https://www.tandfonline.com/doi/full/10.1080/00140139.2023.2236819
20. **From simulations to real-world operations: Virtual reality training for reducing racialized police violence.** *Industrial and Organizational Psychology*, Cambridge. (VR perspective-taking, empathy durability.) https://www.cambridge.org/core/journals/industrial-and-organizational-psychology/article/from-simulations-to-realworld-operations-virtual-reality-training-for-reducing-racialized-police-violence/3CFCF21F35219DB3851EA7BEB473B3E6

> **Citation confidence note** (same convention as `MODULE2_FULL_REPORT.md` §11). **Verified during this review:** the Looming Cognitive Style statistics (F, r, p, n — read from full text); the Luo et al. effect-size/sample counts and trait directions; the Galatzer-Levy/Huang/Bonanno trajectory proportions; the FFFQ journal/volume/pages; the Tisserand et al. title and journal; 🆕 the 60 dB/85 dB equal-latency result in [3b]. **Not verified — confirm before final submission:** FFFQ author list and psychometric values; Looming Cognitive Style author list; exact effect-size magnitudes in Luo et al.; full author list for Tisserand et al.; 🆕 the author list, exact year and sample size for [3b] (retrieved via abstract only — and note it is an older paper, so confirm it is still the best available source for this effect before making it load-bearing); the primary-source Gray & McNaughton RST edition your institution prefers. Several sources are cited by PMC ID or preprint URL because full bibliographic metadata was not exposed — resolve these against library access, exactly as the Module 2 report recommends for its own equivalent entries.
>
> 🆕 **On numbers that are *not* citations.** Three quantities in this document are **design calibrations, not research values**, and are flagged at each use: the three `threatDiscrimination` levels (§7.2, §9 item 2), `defianceChance` (§5.3), and the decision to drop delayed-onset before renormalising the spawn weights (§4.1, §9 item 8). Everything else numeric — the trajectory proportions, the dB thresholds, the LCS statistics, the 60/85 dB result — is quoted from a source. §3.6 documents one derivation that was **attempted and rejected**, to prevent a plausible-looking but unsupported number entering later.

---

*Prepared for the Sentinels project, University of Moratuwa. Companion to `HOSTAGE_EMOTION_SOUND_RESEARCH.pdf` (stimulus side) and `MODULE4_FULL_REPORT.md` §3.5/§6.5 (the distress model this would feed). Profile behaviour is **specified but not yet implemented** — §7 is the design, §8 the evaluation it would enable, §9 the honest limits.*
