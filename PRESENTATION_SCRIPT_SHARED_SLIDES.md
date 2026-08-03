# Talking script — shared slides (everything except Modules 1, 3 and 4)

Covers slides 1–7 and 20–24 of the current deck. Module 2's three slides are in
[MODULE2_PRESENTATION_SCRIPT.md](MODULE2_PRESENTATION_SCRIPT.md).

Total for these twelve slides ≈ **6 minutes 20 seconds** at a normal pace.
`[ ]` = a stage direction, not something you say.

---

## Slide 1 — Title — 20 s

Good morning. We are Team Sentinels, and our project is **Dynamic Scenario Generation and
Evaluation for VR-Based Military Training**, supervised by Dr. Premaratne.

`[ names ]` I'm ___, and with me are ___, ___ and ___. Each of us owns one module, and each of
us will present our own.

---

## Slide 2 — Overview — 20 s

Here is how we will run through it. We start with the background and the problem we set out to
solve, then the aim, our proposed solution, and the system architecture. After that each of us
takes our module in turn — scenario generation, NPC behaviour, cognitive analysis, and the
after-action review. We finish with limitations and future work.

---

## Slide 3 — Introduction — 45 s

Military hostage-rescue training asks people to make decisions **under time pressure, with
incomplete information, and under very strict safety rules.**

In real life you practise that with live exercises — which are expensive, need a lot of people
and equipment, and are very hard to run twice under the same conditions.

VR is the obvious alternative. It is safe, it is repeatable, and — importantly for us — the
system can record objective evidence of what the trainee actually did.

But existing VR prototypes share three weaknesses: the scenario is fixed, the NPCs behave the
same way every time, and the feedback afterwards is shallow. **Our project addresses all three**,
inside one Unity VR prototype built from four cooperating modules.

---

## Slide 4 — Research Problem — 50 s

To state the problem precisely, there are four weaknesses, and they occur **together**.

One — real hostage-rescue training is risky, expensive, and hard to reset for another run.

Two — it is very hard to repeat the same scenario consistently, which makes fair evaluation
almost impossible.

Three — in existing VR systems the scenario is fixed, or only lightly varied.

Four — the NPCs are scripted, so they do not respond meaningfully to what the trainee actually
does.

`[ pause ]`

The result is that missions become monotonous, the evaluator does not get enough evidence to
judge performance, and the training value drops off quickly with repetition.

So the gap is this: **no existing system combines all four things** — parameter-driven scenario
generation, autonomous event-driven NPCs for both hostiles and civilians, cognitive analysis
from runtime evidence, and a structured post-mission review.

---

## Slide 5 — Aim & Objectives — 40 s

Our aim is to develop a VR training prototype for dynamic hostage-terrorist scenario generation
**and evaluation**, running on the Meta Quest 3.

We set six objectives: study the domain and the evaluation measures that already exist;
implement parameter-driven scenario generation with validation; implement event-driven NPC
behaviour with coordination; implement cognitive behaviour analysis from runtime evidence;
implement structured logging, after-action review and synchronised replay; and finally evaluate
all of it, through module tests, integration tests and user trials.

The middle four objectives map one-to-one onto our four modules.

---

## Slide 6 — Proposed Solution — 40 s

The prototype is built in Unity for the Meta Quest 3, and it is split into four modules.

**Module 1** turns evaluator parameters into a validated Scenario.json. **Module 2** reads that
and drives the NPC behaviour during the mission. **Module 3** observes the trainee at the same
time and produces cognitive indicators. **Module 4** takes the telemetry and turns it into a
score summary, a timeline and a replay.

What makes the split work is the **shared contract** at the bottom — one Scenario.json format and
one common event schema. Because we agreed on those first, all four modules could be built and
tested in parallel rather than one waiting on another.

---

## Slide 7 — System Architecture — 45 s

`[ point at the top of the diagram and work downwards ]`

At the top, the evaluator sets the parameters — layout, entity counts, difficulty. That goes into
Module 1, which produces Scenario.json **before** the mission starts.

Inside the green box is the live mission. Module 2 reads the scenario and drives the NPCs;
Module 3 watches the trainee in parallel. Both feed the same runtime telemetry stream — events,
NPC state changes, and cognitive indicators.

That telemetry goes down to Module 4, which combines it with the original Scenario.json to build
the after-action review, and the evaluator reviews the mission afterwards.

So the whole flow is: **parameters in, one mission played, structured evidence out.**

---

## Slide 20 — Inter-Module Integration — 40 s

Everything is centred on Scenario.json. Module 1 produces it; Modules 2, 3 and 4 all consume it
— Module 2 for roles and navigation context, Module 3 for room and zone identifiers, Module 4
for layout metadata.

During the mission, runtime events travel on the shared EventManager bus, and Modules 3 and 4
both observe it. After the mission we produce three artefacts: SessionSummary, EventTimeline and
ReplayData.

And because the contracts are explicit, **each module is independently testable** — Module 1 with
21 automated generation tests, Module 2 with scripted event sequences checked against expected
NPC states, Module 3 against pilot recordings, and Module 4 by checking replay synchronisation
against recorded sessions.

---

## Slide 21 — Limitations — 35 s

We should be honest about the limits.

The layout generator handles a **single floor** only. There is **one hostage** per scenario — no
multi-hostage logic yet. The Quest 3 is a consumer headset, so body tracking is approximate
rather than exact. Our participant pool is around twenty, which is small for the cognitive
validation. We deliberately did **not** add automated tactical recommendations, because that
needs real expert judgment we are not qualified to encode. And the environments are greybox
ProBuilder geometry, not production art.

---

## Slide 22 — Future Work — 30 s

For future work: complete the remaining implementation; extend the layout generator to multiple
floors; add richer NPC coordination and behaviour variety; run a larger SIM-TLX study with
fifteen to thirty participants across low and high difficulty; expand the after-action review
with comparative views across sessions; and finally, run a larger evaluation with **actually
trained personnel**, to measure whether the training transfers to the real task.

---

## Slide 23 — References — 10 s

These are the main sources behind the work — VR skill training, XR in the military, scenario
generation, after-action review, and procedural content generation. Full citations are in the
report.

---

## Slide 24 — Thank You — 15 s

That is our project. Thank you for listening — we are happy to take your questions.

---

## Timing summary

| Section | Time |
|---|---|
| Slides 1–7 (intro through architecture) | ≈ 4 min 00 s |
| Module 1 · Module 2 · Module 3 · Module 4 | ≈ 3 min each = 12 min |
| Slides 20–24 (integration, limitations, close) | ≈ 2 min 10 s |
| **Total** | **≈ 18 min** |

If you need to save time, the two cheapest cuts are slide 2 (Overview — you can skip it entirely
and go straight into the Introduction) and slide 23 (References — just say "full citations are in
the report" while advancing).

---

## Six things to fix in the deck before you present

Found while reading the current PDF. The first one matters most — a panel member can catch it by
comparing two of your own slides.

1. **Slide 13 says "21 participants".** Every other source says **11** — Module 3's chart says
   "11 players, 45 sessions", Module 4 says "11 individual trainees", and Table 7.6 of the report
   says 11 participants / 45 sessions / 9 triads. Change 21 → **11**.
2. **Slide 11: "Eisting"** should be "Existing".
3. **Slide 13: the caption overlaps the chart.** "Average ratings by AI tier…" is sitting on top
   of the x-axis labels. Drag it down, or delete it — the chart reads fine without it.
4. **Slide 6 says "Unity 2022.3 LTS"**, but the report says Unity 6.3. Pick one and make both
   agree; if a panel member asks the version, you want a single answer.
5. **Slide 17 (Module 4): "Identified Problem" and "The research gap" list the same three
   bullets.** One of the two blocks needs different content or should be removed.
6. **Slide 21 is titled "Limitations & Future Works"** but contains only limitations — slide 22
   is Future Work. Retitle slide 21 to just "Limitations".
