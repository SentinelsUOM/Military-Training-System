# Module 2 — talking script (2 min 12 s)

≈ 330 words. At a normal presenting pace (~150 words/minute) this lands at **2 min 12 s**,
leaving a little slack. Slide numbers refer to the current deck.

Bold = say it slightly slower, it is the point of that slide.
`[ ]` = a stage direction, not something you say.

---

## Slide 12 — NPC Behaviour and Coordination — 40 seconds

Module 2 controls **every NPC in the mission** — the terrorists who guard, patrol, hunt and hold
the hostage, and the hostage they are guarding. It is the part of the system that is alive while
the trainee plays.

`[ point at the research gap block ]`

The gap: existing VR prototypes use scripted or instructor-driven NPCs, treat enemies and
civilians as separate systems, and report only pass or fail. **Nobody tests whether the enemy AI
is actually perceived as intelligent.**

`[ move to the right column ]`

So: one shared event bus, a state machine per NPC with a squad layer above it, behaviour from
real military doctrine, three switchable intelligence levels, and everything logged for Module 4.

---

## Slide 13 — Methodology — 52 seconds

Five simple steps.

**First — everything is an event.** A shot. A door opening. A sighting. It goes onto one shared
bus, and every NPC hears it.

**Second — each NPC has a simple state machine.** `[ point at the diagram ]` It starts Idle.
Hears something, Suspicious. Confirms a threat, Alert. Sees the trainee, it Engages. Then cover,
retreat, or Down.

**Third — the squad layer sits on top.** One terrorist spots the trainee, and the whole squad
knows.

**Fourth, the key one** — lose sight of the trainee, and they don't go back to their post. They
hunt. And the search area grows the longer it takes. Our first version made them give up. Real
doctrine says the opposite.

**Fifth — the timings** come from US Army doctrine and the F.E.A.R. squad-AI model.

And all of it switches between three levels: Basic, Intermediate, Advanced.

---

## Slide 14 — Evaluation — 40 seconds

Each trainee played **the same mission three times** — once against each level. Same map, same
seed, so the AI was the only difference. After every play they rated the enemy on a 22-item
questionnaire. Eleven participants, forty-five sessions.

`[ point at the chart ]`

Every measure goes up from Basic to Advanced. Perceived intelligence: 1.6, 3.7, **4.4 out of
five**. That is significant for all three believability measures, and participants agreed
strongly on the ordering.

One honest finding — trainees rated the Advanced AI **most** believable, but **lost** more
missions against it. Believability and performance are two different things.

Thank you.

---

## If you are still running long

Cut these, in this order — each removes ~6 seconds:

1. Slide 13, step five (doctrine sources). Just say "grounded in real doctrine."
2. Slide 12, the last clause "and everything logged for Module 4."
3. Slide 14, "After every play they rated the enemy on a 22-item questionnaire."

**Do not cut** slide 13 step four (the persistent hunt) or the last paragraph of slide 14 — those
are the two things a panel is most likely to ask about.

---

## Before you present

Slide 14 currently reads **"21 participants"** on the slide. It should be **11** — Module 3's
chart says "11 players, 45 sessions", Module 4 says "11 individual trainees", and Table 7.6 of
the report says 11 participants / 45 sessions / 9 triads. The script above says eleven; fix the
slide to match.

---

## Likely questions

- **"Why a state machine and not a behaviour tree?"** — Simpler to author and audit, and every
  transition is a named event, which is what lets Module 4 log a whole session with no extra
  instrumentation.
- **"How do you know the three levels are really different?"** — We verified all seven gated
  behaviours tier by tier in live headset play, after finding and fixing a defect where an older
  code path bypassed the gate.
- **"Only eleven participants?"** — Nine completed all three levels, which is what the
  repeated-measures test needs; the tests are non-parametric precisely because the sample is small.
