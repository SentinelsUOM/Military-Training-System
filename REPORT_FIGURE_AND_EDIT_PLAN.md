# Final Report — Screenshot Placement & Edit Plan

**Target document:** `Final_Report_Team_Sentinels.docx` (663 paragraphs, 8 chapters + Appendix A/B)
**Prepared:** 2026-07-30
**Sources cross-checked:** `evaluation results.pdf` (dashboard export, 16 pp.), `MODULE2_FULL_REPORT.md`, `Module4_Documents/MODULE4_FULL_REPORT.md`, `Module1_Report_Handover_Brief.md`, `Module3_Report_Handover_Brief.md`, `Module4_Documents/HOSTAGE_PROFILE_RESEARCH.md`, and the Unity + `sentinels-aar` source trees.

**How to find an anchor in Word:** every anchor below quotes the first few words of the paragraph. Use `Ctrl+F` and paste the quoted words. The `[P0xxx]` index is the paragraph number in document order (body paragraphs + tables), given so you can tell two similar paragraphs apart.

---

## 0. Current state — what the report already has

| Asset | Status |
|---|---|
| Architecture / pipeline diagrams | ✅ **7 present** (Fig. 4.1, 5.1–5.6), embedded inside single-cell tables that act as figure frames |
| Screenshots of the running system | ❌ **Zero** |
| Screenshots of the dashboard | ❌ **Zero** |
| Code listings / code screenshots | ❌ **Zero** |
| Evaluation-result figures | ❌ **Zero** — Chapter 7 is entirely tables |

Chapter 6 (Implementation) and Chapter 7 (Evaluation) are the two chapters that a Level-4 examiner expects to be visually evidenced, and both currently have **no images at all**. That is the single biggest presentational gap.

---

# PART 1 — THINGS TO FIX BEFORE YOU ADD ANY SCREENSHOT

These are not optional polish. Two of them change what Chapter 7 is allowed to claim.

## 1.1 🔴 BLOCKING — the real-vs-synthetic participant boundary

**What the report says (§7.4, [P0444]):**
> "At the time of writing, eleven trainee participants had taken part, of whom seven completed all three tiers (a complete case), enabling the repeated-measures tests below."

**What `MODULE4_FULL_REPORT.md` §6.8 says (revised today):**
> "the `sessions` collection holds 45 tagged sessions across `P0001`–`P0011`; all rows except `P0001`'s were generated during development … **These rows are not real participants and must not be cited as evaluation findings in a submitted report.** … treat the rule — *only `P0001` is real* — as authoritative."

**And §10 item 2 of the same document:**
> "**Any statistic computed over 'all sessions' — including §6.6's pooled-vs-expert test and ANOVA table — is currently computed over predominantly synthetic data and is therefore a mechanism demonstration, not a finding.**"

`MODULE2_FULL_REPORT.md` §6.7 agrees: one participant (`P0001`) has a complete triad; `P0002` was partial; no significance claim is made.

The attached `evaluation results.pdf` is a render of exactly that database — P0001 through P0011, 45 sessions.

**You must pick one of two branches before doing anything else.** Everything in Part 3 and Part 4 depends on which.

### Branch A — real participants **were** recruited since those documents were written
Then the module documents are stale, not the report. Do this:
1. Update `MODULE4_FULL_REPORT.md` §6.8 and §10.2, and `MODULE2_FULL_REPORT.md` §6.7, to record that real recruitment happened and which IDs are real. Otherwise your own supporting documents contradict your thesis, and an examiner who reads the repo will find it.
2. Apply every edit in **Part 3** and **Part 4** as written.
3. Add an ethics/consent sentence to §7.2 (see §3.2 below) — a study with eleven human participants needs one.

### Branch B — the data is still predominantly synthetic
Then Chapter 7 as written is not defensible and the dashboard screenshots must not be captioned as results. Minimum changes:
1. §7.4 [P0444] — replace the participant sentence with: *"At the time of writing one participant (P0001) has completed all three tiers; the remaining rows in the evaluation database are synthetic development records generated to verify that the dashboard's multi-participant panels render correctly, and are reported here as a demonstration that the analysis mechanism works end-to-end, not as a finding about trainee perception."*
2. Delete Tables 7.6 and 7.7's inferential columns, or retitle both tables **"mechanism demonstration — synthetic dataset"**.
3. Every Chapter 7 dashboard screenshot caption must end with: *"— rendered from the development dataset; the panel computes automatically from live data once real sessions are recorded."*
4. §7.7 Cross-Module Synthesis [P0473] and the Abstract [P0046] both currently assert the Module 2 believability result as a finding. Both need softening.
5. Remove "colleagues and peers who volunteered as trainee participants for the evaluation study reported in Chapter 7" from the Acknowledgement [P0049].

> The rest of this document is written for **Branch A**, because you attached the evaluation PDF as the source of truth. If you are on Branch B, use it for figure *placement* and ignore the numeric updates in Part 3.

## 1.2 🔴 Chapter 7's numbers are stale — the dashboard has moved on

The evaluation PDF disagrees with Tables 7.6, 7.7 and §7.6 in ways that **strengthen** your results, so this is worth doing. Full replacement text is in **Part 3**.

| Report says | PDF says | Impact |
|---|---|---|
| Basic n=20, Intermediate n=12, Advanced n=9 | Basic n=20, Intermediate n=**14**, Advanced n=**11** | Table 7.6 header |
| n = 7 complete-case | n = **9** complete | Table 7.7 title + §7.4 narrative |
| Perceived Intelligence: no pair survives Bonferroni | **All three pairs significant** (p=0.008/0.008/0.016) | Removes a whole limitation paragraph |
| Tactical realism: no pair survives Bonferroni | **All three pairs significant** (p=0.008/0.008/0.008) | Same |
| Module 4 scores tested **across AI tiers** (§7.6.2) | Design deliberately abandoned; now per-player pooling + one-sample Wilcoxon + one-way ANOVA | §7.6.2 must be **rewritten**, not patched |
| §7.6.3 "no data to report on" | Repeated-Play Reliability panel is **fully populated**, 11 players | §7.6.3 must be **rewritten** |
| §7.5 "N/A (methodology audit)" | Module 3 tab now has a 45-session Wilcoxon + ANOVA | §7.5 needs a **new subsection** |

## 1.3 🟠 Hostage personality profiles are implemented but absent from the report

`Assets/Scripts/NPC/HostageProfile.cs` exists on your working tree (untracked), `HostageController.cs` has +104 lines wiring it in, and `Module4Bridge.cs` / `SessionLogger.cs` / `HostageStateEntry.cs` were changed to log it. `Module4_Documents/HOSTAGE_PROFILE_RESEARCH.md` (47 KB, untracked) is the research backing.

The final report mentions this **nowhere**. It is a genuine, research-grounded contribution — three profiles (Weak / Normal / Brave), spawn weights renormalised from published trauma-trajectory prevalence (≈68/21/11), and a counter-intuitive London-Syndrome defiance risk on the *Brave* profile. See **Part 5** for the paragraphs to add.

Note the module report still says "specified, not yet implemented" — update it, or the report and the repo disagree.

## 1.4 🟡 Broken cross-references (all confirmed by reading the document)

| Where | Says | Should be |
|---|---|---|
| §5.6 [P0350] "given in full in Section 6.7 and **Appendix C**" | Appendix C does not exist | `Appendix B` — **or** create Appendix C for the code listings (recommended, see Part 2 §2.7) |
| §2.5 [P0227] "…a session's SIM-TLX outcome (**Section 6.6**)" | 6.6 is Module 4 | `Section 6.5` |
| §3.2 [P0248] "…hand-pose assets (Module 3, **Section 6.6**)" | 6.6 is Module 4 | `Section 6.5` |
| §3.3 [P0250] "…differentiates it from hand-instrumented AAR tooling (**Section 6.7**)" | 6.7 is Score Formulas | `Section 6.6` |
| App. B.1 [P0641] "…corrected to 0.7 s … (**Section 6.4**)" | 6.4 is Module 2 | `Section 6.6, Table 6.3 row 6` |
| App. B.2 [P0643] "…incident timeline (**Section 5.4**)" | 5.4 is Module 2 Design | `Section 5.6` |
| App. B.2 [P0643] "…Module 2 EventManager (**Section 4.3**)" | 4.3 is Users | `Section 5.4` |
| App. B.2 [P0650] "…distress index (**Section 5.5**)" | 5.5 is Module 3 | `Section 5.6` |
| App. B.2 [P0647] "…(**Section 6.4** records this as a fixed defect)" | | `Section 6.6, Table 6.3 row 3` |
| List of Figures [P0133] | Fig 4.1 and Fig 5.1 are **merged into one paragraph** | Split into two lines |

## 1.5 🟡 Bonferroni α mismatch

Appendix B.4 [P0663] and §7.4 [P0448] state α = 0.05/3 ≈ **0.0167**. The dashboard prints **α = 0.02** (PDF pp. 2–3). Several PDF p-values are 0.016 — which passes at 0.0167 but only just.

Verify which the engine actually uses (`sentinels-aar/lib/stats.js`) and make the report, the dashboard and Appendix B.4 agree. If the dashboard is only rounding for display, add a footnote to Table 7.7: *"The dashboard displays the corrected threshold rounded to 0.02; the test is applied at the exact value 0.05/3 = 0.0167."*

---

# PART 2 — WHERE TO PUT SCREENSHOTS

## Numbering convention used below

- **Figures** continue each chapter's existing sequence. Chapter 5 already uses 5.1–5.6, so nothing is renumbered — every number below is new and free.
- **Listings** (code screenshots) are numbered separately: `Listing 6.1`, `Listing 6.2`, … so they don't collide with figures, and they get their own *List of Listings* after the List of Tables.

## How to capture (do this once, then everything matches)

**Dashboard (browser):** Chrome at 1440 px width, **light theme** (there's a theme toggle — light prints better), browser zoom 100 %, `F12 → Ctrl+Shift+P → "Capture node screenshot"` on the card element so you get a clean card with no chrome. Fall back to Windows `Win+Shift+S` if that's fiddly. Crop out the browser UI either way.

**Unity Editor:** maximise the relevant panel (`Shift+Space` over it), then `Win+Shift+S`. For the Scene view, use the top-down 2D orthographic view for layout figures — it reads far better in print than a perspective shot.

**In-headset:** Quest capture via `Meta Quest Link` or the headset's own screenshot (right controller → Oculus button + trigger), then pull from `Internal storage/Oculus/Screenshots/`.

**Code:** VS Code, **light theme**, font size ~15, minimap off, word-wrap on, line numbers on. Select the exact line range, then either screenshot or use the `CodeSnap` / `Polacode` extension for a clean framed image. **Do not** screenshot the dark theme — it renders as a black block on paper.

**Consistency rules:** one width for all dashboard figures, one width for all Unity figures, one width for all listings. Caption below the image, italic, 10 pt, matching the existing Figure 4.1 caption style.

---

## Chapter 3 — Technology Adapted

Two figures. This chapter is currently pure prose and reads thin.

### Figure 3.1 — Unity Editor with the project open
- **Anchor:** §3.2, after [P0248] *"The OpenXR runtime, together with the Unity XR Interaction Toolkit…"*
- **Capture:** Full Unity Editor — Hierarchy, Scene view (`BasicScene`), Project window expanded to show `Module1_DataModels_and_IO`, `Module4`, `CognitiveTracking`, `Scripts`, and Inspector on a selected NPC.
- **Caption:** *Figure 3.1: The Unity project as configured for development, showing the four modules' assembly-separated source trees, the XR Interaction Toolkit rig, and the baseline VR vertical slice all four modules build against.*
- **Why this earns its place:** §3.3 and §3.5 make a lot of claims about assembly-definition isolation. One picture of the Project window proves it.

### Figure 3.2 — The batch generation menu
- **Anchor:** §3.4, after [P0253] *"The Unity Editor on Windows is the primary IDE…"* — specifically after the sentence "…without entering Play mode, which was essential for Module 1's 2000-scenario evaluation study (Section 7.3)."
- **Capture:** Unity menu bar open at `Tools ▸ Scenario Generator`, showing the five entries (`Generate Default Scenario`, `Generate Minimal (Easy)`, `Generate Max Difficulty`, `Generate from Custom Config...`, `Open Output Folder`). Source: `ScenarioGeneratorEditor.cs:46–77`.
- **Caption:** *Figure 3.2: Editor tooling under Tools ▸ Scenario Generator, which runs the full generation-and-validation pipeline headless. This is the entry point used to produce the 2000-scenario evaluation dataset reported in Section 7.3.*

---

## Chapter 4 — Our Approach

### Figure 4.2 — The mission-launch form
- **Anchor:** §4.4, immediately after Table 4.2 [P0284] and before §4.5.
- **Capture:** `sentinels-aar` mission launcher (`components/MissionLauncher.jsx`) with **every field filled in**: Mission Type, Room Count, Room Size, Layout Type, Entry Type, Hostage Count, Terrorist Count, Placement Strategy, **Player ID**, **NPC Level**, Seed.
- **Caption:** *Figure 4.2: The evaluator's mission-launch form. The Player ID and NPC Level fields (highlighted) are the mechanism that tags each session for the within-subjects ablation study of Section 7.4, so no separate data-collection tooling was required.*
- **Why this is important:** §3.6 [P0264], §4.5 [P0288] and §7.4 [P0444] all rest on the claim that tagging happens on this form. This figure is the evidence, and it is one of the highest-value single screenshots in the whole report.
- **Text change:** none required, but see Part 4 §4.1 for a one-sentence addition that references the figure.

---

## Chapter 5 — Analysis and Design

**Leave as-is.** All six design diagrams are present and correct, and a design chapter should not carry implementation screenshots. The one exception is if you add hostage profiles (Part 5) — that would need a small table, not a figure.

---

## Chapter 6 — Implementation ⭐ **highest priority chapter**

This chapter is 100 % prose and tables today. It describes eight defects, three state machines, four pipelines and two dashboards without showing any of them.

### §6.3 — Module 1

| # | Figure | Anchor | What to capture |
|---|---|---|---|
| **6.1** | Generated scenario in the Unity Scene view | After Table 6.1 [P0367], before [P0369] *"A single System.Random is threaded…"* | Top-down 2D orthographic Scene view of a built scenario: rooms, doors, procedural furniture, trainee spawn, hostage, terrorists. Turn gizmos on so spawn points are visible. |
| **6.2** | `Scenario.json` output | After [P0369] *"A single System.Random is threaded through every stage…"* | VS Code with `Scenario.json` open, JSON folded to top level so you can see `rooms`, `entities`, `roleAssignments`, `navigationContext`, `furniture`, `validationResult`, **`seedUsed`** — then one section expanded (`rooms[0]` is a good choice). |
| **6.3** | 21 automated tests passing | After [P0369], adjacent to 6.2 | Unity Console after running `ScenarioGenerationTests` via its Inspector context menu (`Run All Tests`). Shows the five categories and the pass count. |
| **6.4** | In-VR evaluator config panel | Table 6.1 stage 8 area, or §6.3 end | In-headset (or Game view) capture of `EvaluatorConfigPanel` with its Mission Structure / Entity Configuration / Execution Controls groups visible. |

**Captions:**
- *Figure 6.1: A generated four-room scenario realised by SceneBuilder — room graph, reciprocal doors, procedural cover furniture, and the trainee, hostage and terrorist spawns placed by EntityPlacer and RoleAssigner. The hostage room is the deepest dead-end node, as described in Section 5.3.*
- *Figure 6.2: The exported Scenario.json data contract consumed by Modules 2, 3 and 4. The `seedUsed` field records the seed that actually produced the output after any retry, which is what makes the reproducibility claims of Section 7.3 checkable.*
- *Figure 6.3: The 21 automated Module 1 tests passing across all five categories (configuration loading, layout generation, entity placement, role assignment, end-to-end).*
- *Figure 6.4: The in-VR EvaluatorConfigPanel, which lets an evaluator configure and regenerate a scenario without leaving the headset.*

**Listings for §6.3:**

| # | File & lines | Anchor | Why |
|---|---|---|---|
| **Listing 6.1** | `LayoutGenerator.cs:633–660` (`AssignRoomTypes`) | §6.3, near [P0369] | This is the BFS-depth room typing that §5.3 [P0318] and the Space-Syntax citation [12] rest on. ~25 lines — the deepest dead-end becoming the hostage room is visible in the code itself. |
| **Listing 6.2** | `ScenarioValidator.cs:76–95` (the `RunCheck` block) | §6.3, after Listing 6.1 | The two-tier architecture (4 local checks then 5 global checks) is the design claim in §5.3 [P0321]; those twenty lines show it literally as two labelled tiers. |

- *Listing 6.1: BFS depth-based room typing in LayoutGenerator. The single hostage room is selected as the deepest dead-end node (maximum depth, exactly one connection), implementing the Space-Syntax accessibility precedent discussed in Section 5.3.*
- *Listing 6.2: The two-tier constraint architecture in ScenarioValidator — four local (per-room / per-entity) checks followed by five global (whole-scenario) checks, with reachability enforced separately from placement as argued in Section 5.3.*

---

### §6.4 — Module 2

| # | Figure | Anchor | What to capture |
|---|---|---|---|
| **6.5** | Terrorist FSM in play | After [P0373] *"Module 2's runtime data types…"* | Unity in Play mode: Inspector on a `TerroristController` showing current state + `aiLevel`, alongside the Console showing the `ScenarioEvent` stream. Split-screen it. |
| **6.6** | Persistent hunt in action | Inside "The persistent hunt fix (P0)" — after [P0375] | Scene view during play with the squad hunting: draw/enable gizmos for `PointLastSeen` and the growing search radius, several terrorists converging. **This is the money shot for your most consequential defect fix.** |
| **6.7** | Hostage-guardian escalation | Inside "Hostage animation and interaction", after [P0379] | In-headset: guardian holding the hostage at gunpoint during the warn → warning-shot → execute ladder. (Your notes flag that the held-at-gunpoint clip needed work — capture whatever the current build actually shows, and if it isn't right yet, drop this figure rather than showing a T-pose.) |

**Captions:**
- *Figure 6.5: Module 2's terrorist finite-state machine observable at runtime. Every transition is driven by a named, timestamped ScenarioEvent on the shared EventManager bus, which is what allows Module 4 to log the session with no Module 2-specific instrumentation.*
- *Figure 6.6: The persistent hunt system after the P0 fix. A confirmed contact is written to the shared squad blackboard and anchored on the Point-Last-Seen position; the search radius grows with elapsed time and the whole squad converges, rather than each terrorist independently withdrawing to its post.*
- *Figure 6.7: The hostage guardian's escalation ladder at the Advanced intelligence tier — verbal warning, audible warning shot, then execution.*

**Listings for §6.4:**

| # | File & lines | Anchor | Why |
|---|---|---|---|
| **Listing 6.3** | `TerroristController.cs:126–141` | Inside "The AI-intelligence-tier gating gap", after [P0377] | Five one-line `Allow*` predicates — `AllowTeam`, `AllowCoverMorale`, `AllowRetreat`, `AllowIdleScan`, `AllowGuardianLadder`. This *is* Table 5.2 expressed as code, and it makes the gating-gap defect story land: the defect was that one call path skipped these. |
| **Listing 6.4** | `Squad.cs:306–330` (`ReportConfirmedContact` + `BeginHunt`) | Inside "The persistent hunt fix (P0)", after [P0375] | The shared-blackboard write that replaced the private per-NPC flag. Pair it with Figure 6.6. |

- *Listing 6.3: The AI intelligence tier as five gating predicates on TerroristController. Table 5.2's capability matrix is enforced by these five expressions; the defect described in this section was an older code path that reached the gated behaviour without consulting them.*
- *Listing 6.4: The shared squad blackboard. A single member's confirmed sighting is written to squad-wide state and anchored on the Point-Last-Seen position, implementing the react-to-contact principle in place of the private per-NPC flag it replaced.*

---

### §6.5 — Module 3

| # | Figure | Anchor | What to capture |
|---|---|---|---|
| **6.8** | In-headset cognitive mirror HUD | After Table 6.2 [P0383], before [P0385] | Quest capture of `CognitiveMirrorHud` — the translucent proprioceptive-feedback panels showing live movement/reaction readouts. |
| **6.9** | Dashboard — Movement tab | After [P0385] *"CognitiveMovementRecorder samples at 5 Hz…"* | Session page → **Movement** tab. Capture the metrics grid **with the A/B/C confidence chips visible**, plus the "Expert Benchmark Sources" card (`MovementTab.jsx:214`). |
| **6.10** | Dashboard — Reaction time by channel | Same anchor, next to 6.9 | Movement tab → "Reaction Time by Channel vs. Expert Benchmark" chart (`MovementTab.jsx:412`) — the four channels against their bands. |
| **6.11** | SIM-TLX questionnaire (trainee view) | §6.5, near the Table 6.2 stage-3 row | `components/simtlx/SimTlxClient.jsx` — the questionnaire as the trainee fills it in after a mission. |
| **6.12** | Dashboard — Workload tab | After [P0385] | Session → **Workload** tab: Subscale Ratings, **"Why This Session's Workload"** (`SimTlxTab.jsx:182`) and **"Predicted Performance Zone"** (`:215`). The reasoning card is the distinctive part — make sure it's legible. |

**Captions:**
- *Figure 6.8: The in-headset cognitive mirror HUD, built at runtime on translucent panels, giving the trainee proprioceptive feedback without any additional hardware.*
- *Figure 6.9: Module 3's movement-benchmark interpretation in the dashboard. Each metric carries its A/B/C confidence tier explicitly, and tier-C metrics are shown as plain values with no expert range attached rather than compared against a source that does not exist.*
- *Figure 6.10: Multi-channel reaction time against literature-derived reference bands. The four channels — head orientation, weapon raise, body movement and trigger pull — are measured independently, and a stimulus with no response inside the window is recorded as an explicit miss rather than silently dropped.*
- *Figure 6.11: The SIM-TLX subjective workload questionnaire administered to the trainee after every mission.*
- *Figure 6.12: The workload-reasoning layer, which explains a session's composite SIM-TLX score from that session's own recorded facts and cross-checks the predicted performance zone against the same session's outcome data.*

**Listing for §6.5:**

| # | File & lines | Anchor | Why |
|---|---|---|---|
| **Listing 6.5** | `ReactionTimeTracker.cs:110–160` | After [P0385] | Shows all three false-positive validity rules in one block — already-above-threshold disqualification, the 80 ms floor, and the response-window/cooldown pair. §5.5 [P0336] describes these in prose; this is the proof. |

- *Listing 6.5: The three validity rules that prevent a naive threshold-crossing design from recording false reaction times — motion already underway is disqualified, the first 80 ms is never counted, and a cooldown suppresses duplicate samples from burst stimuli such as automatic fire.*

---

### §6.6 — Module 4

This section describes eight defects, two replay systems, two report targets and a five-score model without a single image. It needs the most.

| # | Figure | Anchor | What to capture |
|---|---|---|---|
| **6.13** | Dashboard — Summary tab | After [P0389] *"Module 4's universal event-bus logging…"*, before Table 6.3 | Session → **Summary**: Performance Radar, Score Breakdown, and the **"Operator Safety — how safely you conducted yourself"** card with its four sub-terms. One tall screenshot, or split into 6.13a/6.13b. |
| **6.14** | Dashboard — Timeline tab | After Table 6.3 [P0391] | Event Timeline + Activity Density (10 s buckets) + a few rows of the Event Log. |
| **6.15** | Dashboard — Incidents tab | Next to 6.14 | The severity-ranked incident list, ideally with a high-severity incident at the top. |
| **6.16** | Dashboard — Hostage tab | Next to 6.15 | "Distress Index Over Time" chart plus the per-hostage state journey. |
| **6.17** | Dashboard — 2D replay | Before the "Reliability engineering" heading [P0394] | Replay tab: "Positions (top-down)" map + scrub controls + "Actor States" + "Active Events (±2 s)". |
| **6.18** | Dashboard — 3D replay | Next to 6.17 | The react-three-fiber free-orbit reconstruction with animated character models and rooms drawn from the layout snapshot. **This is your most visually impressive asset — give it a full-width figure.** |
| **6.19** | Offline HTML report | Next to 6.18 | The `WebReportExporter` output opened in a browser, so the "works with or without a network" claim is evidenced. |
| **6.20** | In-VR mission result screen | Anywhere in §6.6 | `MissionResultUI` as the trainee sees it at mission end. Ties into Table 6.3 defect #1 (the Quest build that could not show a result screen). |

**Captions:**
- *Figure 6.13: The session summary. Five scores are shown alongside the Operator Safety sub-term breakdown — survivability, exposure control, weapon discipline and threat response — the second, independent safety axis described in Section 6.6.*
- *Figure 6.14: The event timeline and activity-density view, reconstructed entirely from the universal event bus with no per-gameplay-system instrumentation.*
- *Figure 6.15: Severity-ranked incident extraction. Every incident references a concrete event, timestamp and replay position, following the evidence-linked review principle of Section 2.6.*
- *Figure 6.16: The hostage psychological distress index over the course of a mission — a dimension most tactical AAR tools omit entirely.*
- *Figure 6.17: The 2D top-down replay. Because replay is stored as transforms and discrete state snapshots rather than rendered video, the reviewer can scrub, change speed and jump directly to any extracted incident.*
- *Figure 6.18: The free-orbit 3D replay reconstruction, rebuilt from the recorded position/rotation/state track with the generated room geometry drawn from the layout snapshot.*
- *Figure 6.19: The offline HTML after-action report, produced alongside the hosted dashboard so a session can be reviewed with no network connection.*
- *Figure 6.20: The in-headset mission result screen shown to the trainee at mission end.*

**Listings for §6.6:**

| # | File & lines | Anchor | Why |
|---|---|---|---|
| **Listing 6.6** | `PerformanceCalculator.cs:261–275` (`ExpectedHitRate`) | §6.7 Score Formulas, after [P0398] | The NYPD SOP-9 distance bands as literal code. §6.7 already states the bands in prose — showing them in code proves the formula and the citation agree. |
| **Listing 6.7** | `PerformanceCalculator.cs:215–240` (weapon discipline + threat response) | §6.7, after [P0401] | Contains the `0.61^n` saturating curve, the negligent-discharge rescaling between the published expert (0.17) and novice (0.61) rates, and the `FAST_RT`/`SLOW_RT` Hick's Law anchors — three separate Table 6.3 defect fixes in one screenshot. |
| **Listing 6.8** | `Module4Bridge.cs:44–55` | §6.6, right after [P0389] | Two lines of `+=` subscription. This is the entire "zero-instrumentation" claim, and its brevity *is* the argument. |
| **Listing 6.9** | `DashboardUploader.cs:155–195` | Inside "Reliability engineering", after [P0395] | gzip → queue-to-disk-before-first-attempt → retry with 2.5/5/7.5 s backoff. Shows the "never lost even if the headset is quit mid-upload" claim as code. |

- *Listing 6.6: The distance-normalised expected hit rate, implementing the NYPD SOP-9 hit-rate-by-range bands. Scoring a trainee against 100 % regardless of engagement distance was defect 5 in Table 6.3.*
- *Listing 6.7: Weapon discipline and threat response as implemented. The 0.61ⁿ friendly-fire curve, the negligent-discharge rate rescaled between the published expert and novice qualification-failure rates, and the two Hick's Law reaction-time anchors are all visible; the 0.7 s slow-reaction anchor replaced an earlier, too-lenient 1.5 s value (Table 6.3, defect 6).*
- *Listing 6.8: The whole of Module 4's instrumentation surface. Because the bridge subscribes to one game-wide event bus, a new gameplay event type requires no Module 4 code change at all.*
- *Listing 6.9: The upload pipeline. The gzipped body is written to the on-disk queue before the first network attempt and deleted only on success, so a session survives a mid-upload quit; retries use exponential back-off.*

**Optional but strong — before/after defect evidence.** Table 6.3 lists eight defects. If you can still reproduce any of them, a paired figure is unusually persuasive to an examiner:
- Defect 2 (replay recorder dropping half the actors) — a before/after 2D replay frame showing missing vs. present actors.
- Defect 5 (distance-blind accuracy) — the same session scored under the old flat model and the current distance-aware one.

Only do this if the old behaviour is genuinely reproducible. Do **not** stage it.

---

## Chapter 7 — Evaluation and Results ⭐ **second priority**

Every figure here comes straight out of `evaluation results.pdf`. Crop each card individually rather than pasting whole pages — whole pages will be unreadable at report scale.

### §7.3 — Module 1

| # | Figure | Anchor | Source |
|---|---|---|---|
| **7.1** | Batch evaluation output | After [P0419] *"Four methodological safeguards were applied…"* | The 39-column per-scenario metrics CSV open in Excel, or the Unity Console at the end of a batch run. Shows the measurement instrument, not just its output. |
| **7.2** | *(chart)* Variability by metric | After Table 7.3 [P0423] | Horizontal bar chart of the CV % column from Table 7.3. You must generate this — Excel is fine. |
| **7.3** | *(chart)* Parameter → metric effect sizes | After Table 7.4 [P0429] | Bar chart of ε² per parameter/metric pair. |
| **7.4** | *(chart)* Validation pass rate by scope | After Table 7.5 [P0435] | Stacked bar: form-reachable 43/43 vs extended 373/457, with the two failure categories (EntityBounds 90.5 %, EntityOverlap 9.5 %) broken out. |

Figures 7.2–7.4 are optional. They add no new information over the tables — but four consecutive dense numeric tables with no visual break is hard reading, and 7.4 in particular makes the "100 % within the range an evaluator can actually reach" point far faster than the table does. **If you only make one, make 7.4.**

- *Figure 7.1: The batch evaluation runner and its 39-column per-scenario metric extractor, the measurement instrument built to produce the 2000-scenario dataset. This tooling was itself audited across eight checks before any figure derived from it was treated as citable.*
- *Figure 7.4: Scenario validation pass rate by parameter scope. All 84 failures fall outside the range the evaluator's mission-launch form can produce; within that range the pass rate is 100 % (n = 43).*

### §7.4 — Module 2

| # | Figure | Anchor | Source in the PDF |
|---|---|---|---|
| **7.5** | Per-player comparison, by AI tier | After [P0444] *"A within-subjects ablation study compared…"* | **p. 1**, "Per-player comparison" card (P0001 selected) |
| **7.6** | Overall averages, all players | Replace or accompany Table 7.6 | **pp. 1–2**, "Overall averages (all players)" |
| **7.7** | Statistical significance panel | After Table 7.7 [P0450] | **p. 2**, all three cards: Perceived Intelligence, Animacy, Tactical realism. Crop as one tall figure. |
| **7.8** | Enemy-AI believability survey (trainee view) | After [P0444] | `components/aieval/AiEvalClient.jsx` — the questionnaire the trainee completes after each play |

- *Figure 7.5: Per-player comparison across the three AI intelligence tiers for a single participant, with the same scenario seed played at each tier.*
- *Figure 7.6: Overall averages across all recorded sessions by AI tier. Every believability measure increases monotonically from Basic through Advanced.*
- *Figure 7.7: Friedman omnibus and Bonferroni-corrected Wilcoxon signed-rank post-hoc results, computed live by the dashboard from the same statistics engine documented in Appendix B.4.*
- *Figure 7.8: The post-mission Enemy-AI believability questionnaire, combining Godspeed Perceived Intelligence and Animacy sub-scales, UEQ-S, and an author-defined tactical-realism scale.*

### §7.5 — Module 3

| # | Figure | Anchor | Source |
|---|---|---|---|
| **7.9** | Per-player reaction time vs published reference values | New §7.5.2 (see Part 3 §3.5) | **p. 3**, "Per-player average (all AI levels combined)" with the Expert 0.30 s / Novice 0.50 s reference rows |
| **7.10** | Reaction time vs expert + ANOVA | Same subsection | **p. 4**, "vs Expert Value" card **and** the "ANOVA Table (between players)" card |

- *Figure 7.9: Per-player mean reaction time against the published expert (0.30 s) and novice (0.50 s) reference values. Sessions are pooled across AI tiers, since reaction time is not a function of which tier was played.*
- *Figure 7.10: One-sample Wilcoxon comparison of the pooled trainee reaction-time distribution against the published expert benchmark, and the one-way ANOVA testing whether trainees differ from each other by more than their own session-to-session variability explains.*

### §7.6 — Module 4

| # | Figure | Anchor | Source |
|---|---|---|---|
| **7.11** | Per-player five-score average vs reference values | New §7.6.2 (rewritten — see Part 3 §3.6) | **p. 5**, "Per-player average (all AI levels combined)" with Expert/Novice rows and the NOVICE/DEVELOPING/PROFICIENT/EXPERT badges |
| **7.12** | Hostage Safety & Accuracy vs expert | Same subsection | **p. 6**, both "vs Expert Value" cards |
| **7.13** | ANOVA tables, five scores | Same subsection | **pp. 6–7**, the five ANOVA cards. Crop as one figure, or split into 7.13a (Overall + Hostage Safety, both n.s.) and 7.13b (Accuracy + Speed + Operator Safety, all significant). |
| **7.14** | Repeated-play reliability | Rewritten §7.6.3 (see Part 3 §3.7) | **pp. 7–11**. Do **not** paste all eleven players. Pick **two contrasting ones**: `P0004` (Hostage Safety ±0 pp, Overall ±4 pp — tight) and `P0001` (Hostage Safety ±58 pp — wide). The contrast is the finding. |
| **7.15** | Per-player benchmark bands | End of §7.6 | **pp. 11–16**, two or three player cards showing the NOVICE / DEVELOPING / PROFICIENT / EXPERT classification, plus the "No independent published benchmark exists for this composite score" note on Speed and Operator Safety |

- *Figure 7.11: Each trainee's five-score average with the published expert and novice reference values shown explicitly above the trainee rows. Speed and Operator Safety carry no reference row because no independent published benchmark exists for them.*
- *Figure 7.12: One-sample Wilcoxon tests of the pooled trainee population against the two scores that have an independent published benchmark — Hostage Safety (RAND hostage-rescue outcomes) and Accuracy (NYPD SOP-9, normalised to engagement range).*
- *Figure 7.13: One-way ANOVA between players for each of the five scores, with each player as a group and their own sessions as replicates.*
- *Figure 7.14: Repeated-play reliability for two contrasting participants. Speed and Accuracy cluster tightly across a participant's sessions; Hostage Safety does not, because it is close to binary within a single mission — reported as it stands rather than smoothed.*
- *Figure 7.15: Per-player comparison against published benchmark bands, averaging a participant's sessions before classification to reduce single-session noise. Speed and Operator Safety are shown without a band rather than compared against a source that does not exist.*

---

## Chapter 8 — Discussion

Optional, one figure:

### Figure 8.1 — Capability comparison
- **Anchor:** replace or accompany Table 8.1 [P0484]
- **Capture:** a matrix/heatmap rendering of Table 8.1's twelve rows across the three columns (green = yes, amber = partial, red = no). You'd build this in PowerPoint or Excel.
- **Verdict:** genuinely optional. Table 8.1 is already clear. Skip it if you're short on time.

---

## 2.7 Recommended: a new Appendix C for long code listings

Keep listings in Chapter 6 short (≤ 25 lines, one idea each — every listing above satisfies this). Anything longer belongs in an appendix.

Create **Appendix C — Selected Source Listings** after Appendix B, containing full-method listings for:
- `ScenarioGenerator.Generate()` — the orchestrator with the seed+1 retry loop
- `PerformanceCalculator.Calculate()` — all five scores end to end
- `Squad` hunt lifecycle (`ReportConfirmedContact` → `BeginHunt` → `HuntShouldContinue` → `EndHunt`)
- `sentinels-aar/lib/stats.js` — `friedman()`, `wilcoxonSignedRank()`, `oneWayAnova()`, `oneSampleWilcoxon()`

**This also fixes the broken cross-reference at §5.6 [P0350]**, which already promises an Appendix C that does not exist.

For an appendix, prefer **monospaced text with a light-grey background** over screenshots — it stays selectable and searchable in the PDF, and it survives page breaks. Reserve screenshots for the short inline listings in Chapter 6, where syntax colouring genuinely helps.

---

# PART 3 — REQUIRED TEXT CHANGES (Chapter 7)

Every replacement below is written to be pasted directly. Numbers come from `evaluation results.pdf`.

## 3.1 Table 7.1 — Evaluation methodology by module [P0412]

Rows 2, 3 and 4 are all wrong now.

| Module | Method | Sample |
|---|---|---|
| 1 | *(unchanged)* | 2000 generated scenarios across six experiments |
| 2 | Within-subjects ablation across three AI tiers; validated believability questionnaire (UEQ-S, Godspeed); Friedman + Wilcoxon signed-rank | 11 trainee participants (**9** completing all three tiers) |
| 3 | Construct-validity audit of each benchmark band against its source literature; **one-sample Wilcoxon of pooled trainee reaction time against the published expert value; one-way ANOVA between participants** | **N/A for the audit; 45 recorded sessions across 11 participants for the statistical comparison** |
| 4 | Construct-validity audit of each score formula against a published benchmark; **one-sample Wilcoxon against published expert values; one-way ANOVA between participants; repeated-play reliability** | 11 trainee participants, 45 sessions (**35 for Operator Safety, which is absent on sessions recorded before that score existed**) |

## 3.2 §7.2 — add an ethics sentence (Branch A only)

Insert after Table 7.1 and before §7.3:

> All trainee participants took part voluntarily and were briefed on the purpose of the study, the data recorded (mission telemetry, movement and reaction-time tracking, and post-mission questionnaire responses) and their right to withdraw, before their first session. No personally identifying information is stored: each participant is referenced throughout this chapter only by an anonymised participant identifier (P0001–P0011) assigned on the mission-launch form.

*(If any of that is not accurate, adjust it — but a chapter reporting eleven human participants needs an ethics sentence, and an examiner will look for it.)*

## 3.3 §7.4 — Table 7.6 replacement [P0446]

**Table 7.6: Overall averages across all recorded sessions, by AI tier**

| Measure | Basic (n=20) | Intermediate (n=14) | Advanced (n=11) |
|---|---|---|---|
| Perceived Intelligence (/5) | 1.6 | 3.7 | 4.4 |
| Animacy — life-like (/5) | 2.0 | 3.8 | 4.1 |
| Tactical realism (/5) | 1.7 | 3.5 | 4.4 |
| UEQ Pragmatic (/7) | 4.6 | 5.3 | 5.7 |
| UEQ Hedonic (/7) | 4.7 | 5.4 | 5.8 |
| Enemy hit-rate — NPC (%) | — | — | — |

⚠️ **Rows you must delete or re-source.** The current Table 7.6 also carries *Reaction time*, *Shooting accuracy*, *Mission duration*, *Overall score (Module 4)* and *Workload (SIM-TLX)*. Those are **no longer on the Module 2 tab** — they moved to the Module 3 and Module 4 tabs when the dashboard was restructured (`MODULE4_FULL_REPORT.md` §6.6: "AI difficulty is Module 2's independent variable, not Module 4's"). Either:
- **(a)** delete those five rows from Table 7.6 and let §7.5/§7.6 carry them (recommended — it matches the tool and the argument), or
- **(b)** keep them, but add a footnote: *"Reaction time, accuracy, duration, Overall score and workload are reported here for context only; they are not analysed by AI tier, for the reason given in Section 7.6.2."*

Add a footnote either way for the dash row: *"Enemy hit-rate is shown as unavailable because the telemetry that produces it was added after these sessions were recorded; it populates automatically for all sessions recorded from that point forward."*

## 3.4 §7.4 — Table 7.7 replacement [P0450]

**Table 7.7: Friedman and Wilcoxon signed-rank results across AI tiers (n = 9 complete-case participants)**

| Measure | Friedman χ²(2) | p | Kendall's W | Significant pairs (Bonferroni-corrected) |
|---|---|---|---|---|
| Perceived Intelligence | 10.89 | 0.004 | 0.60 | **All three**: Basic–Intermediate (p=0.008, r=0.85); Basic–Advanced (p=0.008, r=0.85); Intermediate–Advanced (p=0.016, r=0.89) |
| Animacy | 14.00 | < 0.001 | 0.78 | Basic–Intermediate (p=0.004, r=0.89); Basic–Advanced (p=0.004, r=0.89). Intermediate–Advanced n.s. (p=0.188, r=0.51) |
| Tactical realism | 11.72 | 0.003 | 0.65 | **All three**: Basic–Intermediate (p=0.008, r=0.89); Basic–Advanced (p=0.008, r=0.85); Intermediate–Advanced (p=0.008, r=0.88) |
| Enemy hit-rate (NPC) | — | — | — | Not enough complete data (n = 0) |

**Delete these three rows entirely:** *Reaction time*, *Overall score (M4)*, *Hostage Safety (M4)*, *Speed (M4)*. Reaction time moves to §7.5.2; the Module 4 scores move to the rewritten §7.6.2 under a different test.

## 3.5 §7.4 — narrative paragraph replacement [P0452]

Replace the whole paragraph beginning *"The omnibus Friedman test is significant (p < 0.05) for Perceived Intelligence, Animacy, Tactical realism, and Reaction time…"* with:

> The omnibus Friedman test is significant for all three believability constructs — Perceived Intelligence (χ²(2) = 10.89, p = 0.004, W = 0.60), Animacy (χ²(2) = 14.00, p < 0.001, W = 0.78) and Tactical realism (χ²(2) = 11.72, p = 0.003, W = 0.65) — so the AI intelligence tier measurably changes all three. The Bonferroni-corrected pairwise tests are stronger still: Perceived Intelligence and Tactical realism separate **every** pair of tiers, including the harder Intermediate-versus-Advanced comparison, with large effect sizes throughout (r = 0.85–0.89). Animacy separates Basic from both higher tiers decisively (p = 0.004, r = 0.89 for both) but does not distinguish Intermediate from Advanced (p = 0.188, r = 0.51) — an interpretable rather than a disappointing result, since the behaviours the Advanced tier adds over Intermediate (posture escalation, retreat when wounded, idle scanning, the hostage-guardian ladder) are tactical refinements rather than changes to how *alive* the NPC appears, whereas both Perceived Intelligence and Tactical realism ask about exactly the kind of competence those behaviours add. Kendall's W of 0.60–0.78 indicates substantial agreement among participants about the ordering of the three tiers, not merely that a difference exists somewhere.

Then replace the closing sentence [P0453] with:

> Interpreted together, these results support the conclusion that Module 2's three AI intelligence tiers are perceived by trainees as genuinely and measurably different in intelligence, life-likeness and tactical realism — the central claim the ablation design was built to test — with Perceived Intelligence and Tactical realism discriminating all three tiers from one another and Animacy discriminating the presence of squad-level coordination rather than its degree.

**What this removes:** the old paragraph's long defence of why nothing survived Bonferroni at n = 7. That limitation no longer applies at n = 9, so delete it rather than carrying it. Its counterpart in §8.3 (Module 2 limitations) should be checked too.

## 3.6 §7.5 — add a new subsection 7.5.2

Restructure §7.5:
- Renumber the existing content as **§7.5.1 Construct validity of the benchmark bands** (Table 7.8 stays as-is; it is still accurate).
- Add **§7.5.2** after it:

> ### 7.5.2 Trainee reaction time against the published expert value
>
> Section 7.5.1 audits whether Module 3's reference bands match their sources. A second, complementary question is whether the trainee population Module 3 actually measured differs from the published expert value it is compared against — a question the recorded sessions can answer directly. Because reaction time does not depend on which AI tier a participant played, each participant's sessions are pooled across tiers.
>
> Across N = 45 recorded sessions from 11 participants, mean reaction time was **0.65 s**, against a published expert value of **0.30 s** (Hick's Law choice reaction time [38]). A one-sample Wilcoxon signed-rank test finds the trainee distribution significantly different from the expert constant (p < 0.001, r = 0.67, α = 0.05) — the expected result for a non-expert sample, and confirmation that the measurement instrument separates this population from the published expert value rather than returning expert-level numbers for everyone.
>
> A one-way ANOVA with each participant as a group and their own sessions as replicates finds **no** significant difference between participants (F(10, 34) = 1.22, p = 0.316; between-participants SS = 1.94, within-participants SS = 5.43, grand mean 0.65 s). Session-to-session variation within a participant is therefore larger than the variation between participants at this sample size — consistent with reaction time being a state-dependent measure sensitive to the particular encounter rather than a stable trait separating one trainee from another over four to six sessions.
>
> **Table 7.8b: Module 3 reaction time — population comparison**
>
> | Test | Result | Interpretation |
> |---|---|---|
> | One-sample Wilcoxon vs expert (0.30 s) | Trainee mean 0.65 s, p < 0.001, r = 0.67, n = 45 sessions | Significantly slower than the published expert value |
> | One-way ANOVA between participants | F(10, 34) = 1.22, p = 0.316, k = 11 | No significant between-participant difference at this sample size |

Insert **Figures 7.9 and 7.10** in this subsection.

## 3.7 §7.6.2 — full rewrite [P0468, P0469]

The existing heading *"Discriminant validity — do Module 4's own scores change with AI difficulty"* and its paragraph describe a design that was **deliberately abandoned**. Replace the whole subsection:

> ### 7.6.2 Score behaviour across the trainee population
>
> Construct validity (Section 7.6.1) checks each formula against research; it does not check whether the score behaves correctly when real people play. An earlier version of this evaluation ran Module 4's scores through the same tier-by-tier Friedman test as Module 2's ablation study. That framing was subsequently corrected: AI difficulty is Module 2's independent variable, not Module 4's. Module 4 is a measurement instrument, and nothing about its scores is a claim about enemy AI. The analysis therefore pools each participant's sessions across all tiers and asks two properly separated questions — does the trainee population differ from the published expert value, and do trainees differ from one another by more than their own session-to-session variability explains?
>
> **Against the published expert values.** The two scores with an independent published benchmark were tested with a one-sample Wilcoxon signed-rank test over all 45 recorded sessions. Hostage Safety averaged **52 %** against an expert value of **85 %** (RAND hostage-rescue outcomes [60]; p < 0.001, r = 0.49). Accuracy averaged **68 %** against an expert value of **100 %** (NYPD SOP-9, normalised to each session's engagement range [59]; p < 0.001, r = 0.88). Both scores place this non-expert sample significantly below the published expert value, in the expected direction and with a moderate-to-large effect size — the behaviour a correctly calibrated instrument should show. Speed and Operator Safety are not tested this way, because no independent published benchmark exists for either, and are reported as plain values rather than compared against a source that does not exist.
>
> **Between participants.** A one-way ANOVA, with each participant as a group and their own sessions as replicates, was run per score:
>
> **Table 7.9b: One-way ANOVA between participants, Module 4 scores**
>
> | Score | F | df | p | Grand mean | Verdict |
> |---|---|---|---|---|---|
> | Overall | 0.71 | (10, 34) | 0.709 | 59 % | n.s. |
> | Hostage Safety | 0.76 | (10, 34) | 0.666 | 52 % | n.s. |
> | Accuracy | 2.87 | (10, 34) | **0.011** | 68 % | **significant** |
> | Speed | 2.26 | (10, 34) | **0.037** | 61 % | **significant** |
> | Operator Safety | 4.22 | (7, 27) | **0.003** | 30 % | **significant** |
>
> Three of the five scores separate participants from one another significantly, and the pattern is informative rather than arbitrary. **Operator Safety discriminates most strongly** (F(7, 27) = 4.22, p = 0.003) — which is precisely the design intent of adding it as a second, independent safety axis, since it measures how the trainee conducted themselves rather than whether the mission was won. **Accuracy and Speed also discriminate**, both being continuous, skill-linked measures. **Overall and Hostage Safety do not.** Hostage Safety is close to binary within a single mission — the hostage is either rescued or not, so a participant's score is largely a run of 0 % and 100 % results whose between-participant variance cannot exceed its within-participant variance at this sample size. Overall inherits that behaviour, since Hostage Safety carries the largest weight (0.4) in the composite. This is reported as it stands rather than reframed: a composite dominated by a near-binary term is a real property of the current weighting, and Section 8.4 records rebalancing it as future work.
>
> Operator Safety is computed over k = 8 participants and N = 35 sessions rather than 11 and 45, because the score did not exist when the earliest sessions were recorded and is stored as `null` rather than zero on those sessions by design — an absent measurement is not a score of zero.

**Note the knock-on effects:** §7.7 [P0473] and §8.3 Module 4 [P0500] both assert the *old* "no significant difference across AI tier" result. Both must be updated — see Part 4.

## 3.8 §7.6.3 — full rewrite [P0470, P0471]

The current text says the reliability panel "has no data to report on at the time of writing." It now has eleven participants' worth. Replace:

> ### 7.6.3 Repeated-play reliability
>
> A participant playing more than once answers a different question from either test above — not whether the score discriminates, but whether it is **consistent** for the same person. The dashboard's repeated-play reliability panel reports every one of a participant's sessions across the five scores, with the mean and standard deviation across those sessions; tight clustering supports reliability, and wide swings do not.
>
> Across the eleven participants (three to six sessions each), the five scores separate clearly into two reliability regimes:
>
> **Table 7.10: Repeated-play reliability — representative participants**
>
> | Participant | Sessions | Overall | Hostage Safety | Accuracy | Speed | Operator Safety |
> |---|---|---|---|---|---|---|
> | P0004 | 3 | 76 % ± 4 pp | 100 % ± 0 pp | 43 % ± 14 pp | 65 % ± 2 pp | — |
> | P0005 | 5 | 75 % ± 24 pp | 72 % ± 44 pp | 68 % ± 17 pp | 87 % ± 1 pp | 60 % ± 4 pp |
> | P0009 | 6 | 68 % ± 32 pp | 60 % ± 49 pp | 83 % ± 41 pp | 63 % ± 16 pp | 22 % ± 10 pp |
> | P0001 | 4 | 48 % ± 37 pp | 50 % ± 58 pp | 28 % ± 33 pp | 72 % ± 17 pp | — |
>
> **Speed and Operator Safety are the most reliable scores.** Speed's standard deviation is 1–2 pp for participants who played consistently (P0005 87 % ± 1 pp, P0004 65 % ± 2 pp), and Operator Safety clusters within 4–19 pp for every participant who has it. Both are continuous, per-second measures accumulated across a whole mission, so a single event cannot move them far.
>
> **Hostage Safety is the least reliable, structurally rather than accidentally.** Its per-session standard deviations range from ±0 pp to ±58 pp, and the wide ones are all cases where a participant scored 100 % in some sessions and 0 % in others. With one hostage per scenario, the score is close to binary within a single mission, so a standard deviation near ±50 pp is the *arithmetic consequence* of an even split rather than evidence of a noisy instrument. The correct reading is that Hostage Safety is a reliable measure of a single mission's outcome and an unreliable estimate of a trainee's underlying skill from a small number of plays — which is a statement about how many sessions are needed, not about the score's construction. Accuracy sits between the two regimes: ±0 pp for participants who consistently engaged at the same range, and up to ±49 pp for participants with at least one zero-shot session.
>
> Operator Safety is absent for three participants (P0001, P0003, P0004), whose sessions predate the score's introduction and store it as `null` rather than zero.

Insert **Figure 7.14** here.

Add a matching future-work item to §8.4:

> Record more repeat plays per participant, since Section 7.6.3 shows that Hostage Safety — the highest-weighted term in the Overall composite — needs substantially more sessions per trainee than the current three to six before it becomes a stable estimate of individual skill rather than of a single mission's outcome.

---

# PART 4 — KNOCK-ON TEXT CHANGES (other chapters)

## 4.1 Abstract [P0046]

Two changes:

1. Find *"…statistically significant increase in perceived believability and behavioural competence across eleven trainee participants"* → change to *"…statistically significant increase in perceived believability and behavioural competence across eleven trainee participants, with Perceived Intelligence and Tactical realism separating all three tiers from one another under Bonferroni correction"*.
2. Find *"…and that Module 4's scoring formulas agree with every published benchmark value available to check them against."* → extend to *"…and that Module 4's scoring formulas agree with every published benchmark value available to check them against, place the trainee population significantly below the published expert values for both benchmark-anchored scores, and separate individual trainees from one another most strongly on Operator Safety — the second safety axis the module adds."*

## 4.2 §4.4 [P0284] — reference the new figure

After Table 4.2, add: *"Figure 4.2 shows the mission-launch form as the evaluator sees it; the Player ID and NPC Level fields it exposes are what tag each session for the evaluation study reported in Chapter 7."*

## 4.3 §7.7 Cross-Module Synthesis [P0473]

Replace the Module 4 clause. Find:
> "…while honestly reporting that its own composite scores do not (yet, at this sample size) discriminate across AI difficulty the way the believability measures do — a genuinely informative finding about what a harder AI tier does and does not change, rather than a gap papered over."

Replace with:
> "…and that its scores behave as a measurement instrument should when applied to real trainees: the two benchmark-anchored scores place a non-expert sample significantly below the published expert value, and three of the five scores — led by Operator Safety, the second safety axis the module adds — separate individual trainees from one another. The two that do not, Hostage Safety and the Overall composite it dominates, fail to do so for a structural reason the analysis identifies explicitly rather than leaves implicit: with one hostage per scenario, Hostage Safety is close to binary within a single mission."

## 4.4 §8.3 Threats to Validity — Module 4 [P0500]

Replace the first bullet. Find:
> "The evaluation sample (Chapter 7) is real but still modest by the standards of a fully-powered study; Module 4's own discriminant-validity result (no significant score change across AI tier) should be read as 'not yet detected at this sample size,' not as a claim that no such effect exists."

Replace with:
> "The evaluation sample (Chapter 7) is real but still modest by the standards of a fully-powered study. Where a Module 4 score does not separate participants — Hostage Safety and the Overall composite it dominates — the correct reading is 'not detectable at this sample size with a near-binary per-mission outcome,' not that no such difference exists. Operator Safety's between-participant result additionally rests on eight participants rather than eleven, because the score postdates the earliest recorded sessions."

## 4.5 §8.3 Threats to Validity — Module 2 [P0493]

Check whether any surviving sentence still carries the old n = 7 / Bonferroni limitation. If so, replace with:
> "The believability result now separates all three tiers on two of the three constructs under Bonferroni correction at n = 9 complete cases; Animacy's Intermediate-versus-Advanced comparison remains non-significant, and should be read as evidence about which construct the Advanced tier's additional behaviours affect rather than as a power limitation alone."

## 4.6 §8.4 Future Work [P0506]

The first item says the larger sample is needed *"to strengthen the significance results reported in Chapter 7 and give Module 4's discriminant-validity and reliability panels enough repeated trials to activate."* Both panels have now activated. Rewrite:

> Recruit a larger, fully-powered trainee sample (the same pool already serving Module 2's and Module 4's shared evaluation, so no separate recruitment effort is required) to increase the number of complete-case triads behind Section 7.4's significance results, and to give Section 7.6.3's reliability analysis enough repeat plays per participant for Hostage Safety to stabilise.

## 4.7 List of Figures [P0132–P0138]

- **Fix the merge bug:** [P0133] currently contains the Figure 4.1 entry and the Figure 5.1 entry in one paragraph. Split them.
- Add every new figure (3.1–3.2, 4.2, 6.1–6.20, 7.1–7.15, optionally 8.1).
- **Strong recommendation:** convert this list to a Word field. Give every caption Word's `Caption` style, insert with `References ▸ Insert Caption` (Label = "Figure", numbering = "Include chapter number"), then replace the manual list with `References ▸ Insert Table of Figures`. With ~40 figures, maintaining the list by hand will not survive one more editing pass, and it fixes the page-number placeholder note at [P0132] at the same time.

## 4.8 New: List of Listings

Add after the List of Tables [P0162]. Same approach — use a second caption label ("Listing") and a second Table of Figures field filtered to it.

## 4.9 List of Tables [P0141–P0162]

Add the new tables: **7.8b** (Module 3 population comparison), **7.9b** (Module 4 ANOVA), **7.10** (repeated-play reliability). Renumber if you'd rather they run in sequence — but 7.8b/7.9b avoid renumbering Table 8.1 and everything that references it, so the suffix form is the lower-risk choice.

---

# PART 5 — NEW CONTENT: HOSTAGE PERSONALITY PROFILES

`HostageProfile.cs`, the `HostageController` wiring, and `HOSTAGE_PROFILE_RESEARCH.md` are all on your working tree and absent from the report. This is a research-grounded feature with a genuinely counter-intuitive design decision, which makes it good report material.

**Before writing it up, confirm one thing:** is it live in the build you are submitting, and does the AAR display the profile? `Module4Bridge.cs` and `HostageStateEntry.cs` were modified to log it, so it probably is — but check the dashboard actually surfaces it. That determines whether this goes in Chapter 5/6 as implemented work or in §8.4 as future work.

### If implemented — add to §5.6, after [P0346]

> Hostages additionally carry a trait-differentiated response profile, because a model in which every hostage reacts identically to the same stimulus is the one clearly unrealistic simplification remaining in the distress model. Two independent findings converge on how real defensive response varies: visually, a high looming cognitive style produces generalised rather than selective freezing (F(1,80) = 6.50, p = 0.01), and auditorily, high-anxiety individuals respond with equal latency to a quiet 60 dB and a loud 85 dB stimulus while low-anxiety individuals discriminate between them. The shared mechanism is that trait reactivity flattens the intensity–response relationship, and the auditory result is the directly applicable one here, since the hostage model's primary stimulus is gunfire.
>
> Three profiles are modelled — Weak, Normal and Brave — and the design decision worth stating explicitly is that **all three keep identical distance thresholds**, differing only in threat discrimination. This is a correctness requirement rather than a simplification: the sound-pressure-to-distance chain cannot separate profiles at close-quarters range, since consecutive published thresholds map to 1 m, 14 m, 158 m and 2.2 km, and gunfire exceeds the 137 dB fear/panic threshold at every in-building distance. Per-profile distances would therefore have been invented values presented as research-derived ones — the exact failure mode this project flags elsewhere for the Speed coefficients (Section 7.6.1). Spawn weights are renormalised from published trauma-trajectory prevalence (resilience 65.7 %, recovery 20.8 %, chronicity 10.6 %) to approximately 68 / 21 / 11, so a trainee meets a composed hostage far more often than a fragile one. The Brave profile additionally carries a small captor-defiance risk drawn from the London Syndrome finding that captivity defiance is associated with *worse* survival outcomes, not better — preserving the counter-intuitive after-action lesson that the calmest-looking hostage can be the one at greatest risk, without requiring a fourth profile.

**Table 5.5: Hostage response profiles**

| Profile | Threat discrimination | Spawn weight | Behavioural signature |
|---|---|---|---|
| Weak | 0.35 | ≈ 11 % | Cannot grade the threat — reacts to a distant shot as though point-blank; may stay frozen after the threat has passed; slow to extract |
| Normal | 0.85 | ≈ 21 % | Population-typical; grades the threat correctly with occasional lapses. Preserves the project's original single behaviour as a regression baseline |
| Brave | 1.00 | ≈ 68 % | Grades the threat correctly, low peak distress, follows the rescuer quickly — carries a 15 % captor-defiance risk (London Syndrome) |

**Add to §6.4** (Module 2 implementation, since `HostageController` owns it): one paragraph noting that `assignRandomProfile` is on by default for training variety and turned off for controlled evaluation runs, and that the active profile is logged into the session record so Module 4 can attribute a distress trajectory to it in the AAR.

**Add a screenshot:** *Figure 6.7b — the AAR hostage panel showing the active profile alongside the distress trajectory.*

**Add an honesty note** (this matters — `HostageProfile.cs`'s own doc comment insists on it):
> The mechanism and its direction are research-grounded; the three discrimination values themselves are design calibrations, since the literature supplies no numeric probability. They carry the same standing as the Speed target-time coefficients (Section 7.6.1) and are reported as such.

**Add to §8.3 (Module 4 or Module 2 limitations):**
> `HOSTAGE_PROFILE_RESEARCH.md` §8.1 separates the claims this feature can and cannot support. That a Weak-profile hostage reaches higher peak distress is a **verification** result — it follows by construction from the authored discrimination parameter and must be reported as an implementation check. Only the **discovery** claims — that profile changes trainee rescue time, Hostage Safety score, and mission-outcome distribution, none of which are set by the profile definition — can be presented as findings, and none has been evaluated at the time of writing.

### If not yet live in the submission build — add to §8.4 instead

> Extend the hostage model with trait-differentiated response profiles. A three-profile design (Weak / Normal / Brave) is specified in full, grounded in convergent visual and auditory evidence that trait reactivity flattens the intensity–response relationship, with spawn weights renormalised from published trauma-trajectory prevalence and a London-Syndrome defiance risk attached to the least-fearful profile. Its central design constraint is already established: profiles must differ in threat discrimination rather than in distance thresholds, because the sound-pressure-to-distance chain cannot separate them at close-quarters range.

### Also update `MODULE4_FULL_REPORT.md`

§3.5 and §6.5b both say "specified, not yet implemented". `HostageProfile.cs` now exists. Fix that before submission, or the report and the repository contradict each other on a point an examiner can check in thirty seconds.

---

# PART 6 — CAPTURE CHECKLIST

Print this and tick as you go. **P1** = must have, **P2** = strongly recommended, **P3** = optional.

### Dashboard — browser, light theme, 1440 px
- [ ] **P1** 4.2 Mission-launch form, all fields filled
- [ ] **P1** 6.13 Session Summary — radar + score breakdown + Operator Safety card
- [ ] **P1** 6.18 3D replay reconstruction *(most impressive single asset)*
- [ ] **P1** 7.5 Module 2 tab — Per-player comparison *(PDF p. 1)*
- [ ] **P1** 7.6 Module 2 tab — Overall averages *(PDF pp. 1–2)*
- [ ] **P1** 7.7 Module 2 tab — Statistical significance, all three cards *(PDF p. 2)*
- [ ] **P1** 7.11 Module 4 tab — Per-player average vs reference values *(PDF p. 5)*
- [ ] **P1** 7.12 Module 4 tab — vs Expert Value, both cards *(PDF p. 6)*
- [ ] **P1** 7.13 Module 4 tab — ANOVA tables *(PDF pp. 6–7)*
- [ ] **P2** 6.9 Movement tab — metrics with A/B/C chips + benchmark sources
- [ ] **P2** 6.10 Movement tab — reaction time by channel vs benchmark
- [ ] **P2** 6.12 Workload tab — subscales + reasoning + predicted zone
- [ ] **P2** 6.14 Timeline tab
- [ ] **P2** 6.15 Incidents tab
- [ ] **P2** 6.16 Hostage tab — distress index over time
- [ ] **P2** 6.17 2D replay map + controls
- [ ] **P2** 7.9 Module 3 tab — per-player reaction time *(PDF p. 3)*
- [ ] **P2** 7.10 Module 3 tab — vs Expert + ANOVA *(PDF p. 4)*
- [ ] **P2** 7.14 Repeated-play reliability — **P0004 and P0001 only** *(PDF pp. 7–11)*
- [ ] **P2** 7.8 Enemy-AI believability survey (trainee view)
- [ ] **P3** 6.11 SIM-TLX questionnaire (trainee view)
- [ ] **P3** 6.19 Offline HTML report
- [ ] **P3** 7.15 Per-player benchmark bands, 2–3 cards *(PDF pp. 11–16)*

### Unity Editor
- [ ] **P1** 6.1 Generated scenario, top-down 2D Scene view
- [ ] **P1** 6.3 21 automated tests passing (Console)
- [ ] **P2** 3.1 Full Editor with project open
- [ ] **P2** 3.2 `Tools ▸ Scenario Generator` menu
- [ ] **P2** 6.5 Terrorist FSM — Inspector + Console event stream
- [ ] **P2** 6.6 Persistent hunt — Scene view with PLS gizmos
- [ ] **P3** 7.1 Batch evaluation runner / 39-column CSV

### In-headset (Quest)
- [ ] **P2** 6.4 In-VR evaluator config panel
- [ ] **P2** 6.8 Cognitive mirror HUD
- [ ] **P2** 6.20 Mission result screen
- [ ] **P3** 6.7 Hostage-guardian escalation *(skip if the pose still isn't right)*

### Code (VS Code, **light theme**, ~15 pt)
- [ ] **P1** L6.6 `PerformanceCalculator.cs:261–275` — NYPD SOP-9 bands
- [ ] **P1** L6.7 `PerformanceCalculator.cs:215–240` — weapon discipline + threat response
- [ ] **P1** L6.8 `Module4Bridge.cs:44–55` — zero-instrumentation subscription
- [ ] **P2** L6.3 `TerroristController.cs:126–141` — AI tier gating predicates
- [ ] **P2** L6.4 `Squad.cs:306–330` — shared contact blackboard
- [ ] **P2** L6.5 `ReactionTimeTracker.cs:110–160` — three validity rules
- [ ] **P2** L6.2 `ScenarioValidator.cs:76–95` — two-tier check list
- [ ] **P2** L6.1 `LayoutGenerator.cs:633–660` — BFS room typing
- [ ] **P2** L6.9 `DashboardUploader.cs:155–195` — gzip + retry + queue
- [ ] **P3** 6.2 `Scenario.json` in VS Code, folded

### Charts you must generate (not screenshots)
- [ ] **P2** 7.4 Validation pass rate by scope
- [ ] **P3** 7.2 Variability (CV %) by metric
- [ ] **P3** 7.3 Parameter → metric effect sizes
- [ ] **P3** 8.1 Capability comparison matrix

---

# PART 7 — SUGGESTED ORDER OF WORK

1. **Decide Branch A or Branch B** (§1.1). Everything downstream depends on it.
2. **Apply all Part 3 numeric updates** — Tables 7.1, 7.6, 7.7; new §7.5.2; rewritten §7.6.2 and §7.6.3. Do this *before* capturing evaluation screenshots, so the text and the figures are describing the same dashboard state.
3. **Apply Part 4 knock-on edits** (Abstract, §7.7, §8.3, §8.4).
4. **Fix all cross-references** (§1.4) — ten minutes, and each one is a soft mark an examiner can dock.
5. **Capture all P1 screenshots** in one sitting, so the theme, zoom and crop stay consistent.
6. **Insert P1 figures with proper Word captions** (`References ▸ Insert Caption`, chapter-numbered).
7. **Decide on the hostage-profile section** (Part 5) — it needs the implementation-status check first.
8. **Capture P2 screenshots**, insert.
9. **Convert the List of Figures / Tables to Word fields**, add the List of Listings, press F9.
10. **Create Appendix C** for the long listings, and fix the §5.6 cross-reference that already points at it.
11. **P3 items** only if time allows.

> A backup already exists at `Final_Report_Team_Sentinels.BACKUP-20260730-124318.docx`. Take a fresh one before you start editing.
