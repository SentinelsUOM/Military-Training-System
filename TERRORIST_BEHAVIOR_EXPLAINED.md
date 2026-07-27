# Terrorist NPC Behaviour — How It Reacts to Everything You Do

**Module 2 (Enemy AI) — Sentinels VR Counter‑Terrorism Trainer**
A plain‑language, fully‑detailed walkthrough of *why* the terrorists do what they do, organised around the trainee's own actions. Every number quoted here is the actual value in the code.

---

## 1. The mental model (read this first)

Each terrorist runs three layers that stack on top of each other:

1. **Senses (Perception)** — a pair of "eyes" and "ears" that decide *what the terrorist knows*.
2. **Brain (a state machine)** — 7 states that decide *what the terrorist does* with what it knows.
3. **Squad brain (coordination)** — a shared layer above the individuals that lets the whole group act on one member's information (converge, flank, hunt, re‑elect a leader).

This mirrors the real‑world / game‑AI architecture documented in our literature review (`Assets/Docs/Literature_Review_Module2.docx`) — a per‑agent behaviour layer under a squad‑coordination layer, the same two‑layer design used by the game *F.E.A.R.*

The 7 states:

| State | Meaning |
|---|---|
| **Idle** | Patrolling / roaming / standing guard — calm, most responsive to new events |
| **Suspicious** | Heard something — stops and looks toward the sound, then goes to check |
| **Alert** | Knows a threat is near — weapon up, holding/searching, not yet shooting |
| **Engage** | Has eyes on you — actively firing |
| **TakeCover** | Moving to / fighting from a cover point |
| **Retreat** | Wounded — falling back (once per life) |
| **Down** | Dead — no further reactions |

Three **roles** change the flavour: **Leader** (issues squad orders, stands central), **Roamer** (wanders, prioritised to investigate noises), **Guard / Hostage‑Guardian** (stays put; the guardian never leaves the hostage).

---

## 2. How they SENSE you (Perception)

Every terrorist has a `PerceptionController` with human‑like limits — it does **not** magically know where you are.

- **Field of view:** 120° total (±60° from facing). Anything outside that cone is unseen — **you can flank or approach from behind.**
- **Range:** 20 m. Beyond that you're invisible to them.
- **Line of sight:** a ray from the eyes to your chest. **Walls and doors block it** — if there's a wall between you, they can't see you even inside range and cone.
- **"Seen" vs "Confirmed" (this is important):**
  - The **first frame** they get a clear look at you → **PlayerSeen** → they go **Alert** (weapon up, turning to you) but do **not** shoot yet.
  - After **0.5 seconds** of *continuous* line of sight → **TargetConfirmed** → they commit to **Engage** and open fire.
  - This 0.5 s is your reaction window — if you break their view within it, they never confirm.
- **Losing you:** once they lose line of sight, a timer runs; after **1.5 seconds** of no view → **PlayerLost** → they stop shooting and start hunting (Section 6).
- **Hearing:** separate from sight. A gunshot within **20 m** is heard even with no line of sight.

**Why it's built this way:** it forces the trainee to use real CQB skills — angles, cover, breaking the line of sight — instead of the enemy being an all‑seeing aimbot.

---

## 3. Before you're detected — what they do while idle

Depending on role, an idle terrorist:
- **Patrols** a fixed line (Guard), **wanders** random points within 12 m of spawn (Roamer), or **stands** at a post facing a set direction (Leader).
- **Scans while idle:** roughly every **5 seconds** it stops and sweeps its view **±65°** left/right, holding each look for **0.8 s**. This is deliberate — *a guard who only ever faces his direction of travel can be trivially followed*, so they periodically look around and can catch you sneaking.
- **Never stares at a wall:** an anti‑wall rule guarantees an idle terrorist always turns toward open space or a doorway, never faces a blank wall (it looked broken/robotic otherwise).

---

## 4. You make a noise (footstep zone, a distant shot, a door)

- A **GunshotHeard** within 20 m, a **door opening**, or a **room breach** flips a calm terrorist **Idle → Suspicious**.
- **Suspicious behaviour:** it *turns to face the sound first*, then **walks over to investigate** — it does **not** just stand and stare (an earlier bug). The path routes it *through doorways* to actually reach the spot.
- If it finds nothing after searching, and it never actually saw you, it gives up after **~6 seconds** (`suspiciousTimeout`) and returns to patrol.
- **Who goes:** the squad doesn't all pile onto one noise. A scoring system (`NPCSelector`) prefers the **calmest, closest, best‑suited** member (a Roamer is prioritised for gunshots) so one man investigates while others hold.

---

## 5. You come into view → they engage

1. **PlayerSeen** → **Alert**: weapon up, body/head turn to track you (head‑look IK), squad is told "contact!".
2. **0.5 s of continuous sight → TargetConfirmed → Engage**: they open fire.
   - **Shortcut:** if you appear **closer than 5 m** (`engageDistance`), they skip the wait and go straight to Engage — at knife‑fight range there's no luxury of a confirm delay.
3. **How they shoot** (`NpcShooterRaycast`):
   - **Aimed at centre‑mass** (your capsule's middle), not your head — real doctrine, and it also fixed a bug where they shot at the floor when you had no headset on.
   - **Burst fire:** 3 rounds per burst at 8 rounds/sec, then a **~1.2 s pause** ("assess") before the next burst — humans fire in bursts, not a constant stream.
   - **Deliberate inaccuracy:** a spread cone (~**9°** on the tuned prefab) so they are *suppressive, not sniper‑perfect*. **This accuracy is a locked value — it is never changed without explicit sign‑off**, because it's the single biggest lever on difficulty/fairness.
   - Damage 10/round, effective range 80 m.
4. **Spacing (they don't run onto your muzzle):**
   - They keep a **hard minimum 3 m** gap and prefer to fire from a **3.5 m** ring (the "reactionary gap").
   - If they're **more than ~5.5 m** away, they **advance on you** to close the distance ("when he sees you, he comes at you"); if you crowd them, they **step back** to keep the gap.

---

## 6. You break line of sight / hide → the hunt (the big one)

This is the most important and most‑researched behaviour. When you disappear:

- After **1.5 s** of no sight → **PlayerLost**.
- The doctrine (from the literature review) is emphatic: **a confirmed sighting means you hunt, you do NOT wander back to your post.** So:

**a) The contact becomes SHARED, PERSISTENT squad knowledge.**
The instant *any* terrorist confirms you, your position is written to a **shared squad "blackboard"** (`Squad.PointLastSeen`) that **every** member reads — it is not private to the one who saw you. This is the core fix for the old bug where a man who never personally saw you would quit while his mate was still fighting.

**b) They converge and hunt from your Last‑Seen point.**
- The searchers **fan out** from where you were last seen:
  - **Wave 0 — chase:** everyone sweeps *forward along the direction you were running*, line‑abreast (covering the width of your escape). They chase where you *went*, not where you *were*.
  - **Wave 1+ — divide:** if the chase is empty, they split to distinct bearings (straight on, then ±60°, ±120°, then behind) to cover every approach, and the search radius **expands with time** (search theory: the area you could be in grows as time passes).
- **The nearest man takes the direct line** to the point; the rest fan around him.

**c) A fresh clue re‑anchors everyone.**
Any new gunshot or sighting **resets the shared last‑seen point and re‑converges the whole squad** on the new spot — fresh information beats a stale search.

**d) They only stop as a GROUP, deliberately.**
No individual quietly gives up. The squad presses the hunt for a **long, area‑based budget (~45 s, re‑armed by every fresh clue)**. Only when that's spent / the area is genuinely swept does the squad **stand down together** and everyone returns to patrol at once. (An individual 12 s "alert give‑up" timer still exists as a backstop, but it is suppressed while the squad hunt is live.)

**Why:** the old behaviour — see you, lose you, stroll back to the room — is doctrinally incoherent (breaking contact is a *deliberate, covered* decision, never a reflex). This makes them behave like a real force that commits to finding an intruder.

---

## 7. You free‑fire while hidden

- Your own gunshot raises a **GunshotHeard** at your position.
- Because it's *fresh contact intel*, it **re‑anchors the entire squad's hunt on the sound** and pulls everyone toward it — even a man mid‑search in a stale corner drops it and converges. So shooting while hidden gives your position away and draws them in, exactly as it should.

---

## 8. You kill a terrorist

Killing a member triggers a coordinated reaction (`AlertPropagator` + the hunt):

- **Squadmates with line of sight to the body** are pushed straight toward the fight (Ring 1). Nearby non‑squad enemies become Suspicious (Ring 2).
- **The kill location becomes a hunt anchor:** the body is treated as a fresh "last‑known point" (you were right there), so the survivors **sweep the spot where he died** as a shared, persistent hunt — instead of the old bug where the survivor walked onto the corpse and got *stuck standing on it*, ignoring your gunfire.
- **Terrorists don't friendly‑fire each other** — a terrorist's round can wound the hostage in crossfire but is ignored by other terrorists.
- **Morale takes the biggest hit** from seeing a mate die (see Section 10).
- **If the man you killed was the Leader → the squad re‑elects one.** A surviving member (a Roamer is preferred, the hostage‑guardian is never eligible) is promoted to Leader and resumes issuing squad orders. Killing the leader first is therefore not a permanent "decapitation" exploit.

---

## 9. You wound a terrorist (but don't kill him)

- The moment a hit drops him to **≤ 40 health** (`retreatHealthThreshold`), he **retreats once per life** — falls back ~**10 m** from you at a jog (1.7× walk speed), holds for ~**4 s**, then returns to Alert and re‑engages via his own perception.
- Taking hits also feeds his **morale/escalation** (Section 10), pushing him from standing‑and‑shooting toward using cover.
- (The **hostage guardian never retreats** — when wounded it instead reaches for the hostage as a human shield.)

---

## 10. Sustained pressure → morale decay (posture)

Each fighter carries a simple **morale model** (`CombatPosture`) that degrades under stress:

- Starts at **HoldAndShoot** — stand at the standoff ring and return fire (confident).
- Accumulates **escalation points**: **+2** when a mate goes down (strongest cue), **+1** per stress‑spike (your rapid gunfire nearby), **+1** each time you hit him, **+1** for every 8 s of continuous combat.
- At **3 points** it flips to **CoverAndPeek** — it now **relocates between cover points, peeks, and fires**, holding each cover spot ~3.5 s before moving. This models a soldier getting more cautious as the fight turns against him.

This is grounded in the operations‑research finding that units don't break on casualties alone but on *accumulating* casualties, stress and lost confidence.

---

## 11. You go for the hostage (the Guardian's escalation ladder)

The **hostage guardian** is special: it **never leaves the hostage**. Its reactions form a deliberate, *observable* threat ladder (based on real hostage‑negotiation "triggering points", not a hidden timer):

1. **Relaxed:** patrols gently within **2.5 m** of the hostage, watching the door.
2. **Under pressure** (a mate is down / fighting nearby): it can't leave, so it **paces between the doorway (watching for you) and the hostage (checking it hasn't fled)** — torn, but leashed (never strays past **6 m**).
3. **You close within 3.5 m of the hostage → THREAT:** it turns and **aims its rifle at the hostage's head** and shouts *"Back off! Stay back or he dies!"* Back away and it **de‑escalates**.
4. **You close within 2.5 m → WARNING SHOT:** it fires an audible shot ("*I'm not joking!*" → BANG → "*Get back!*") — the unmissable cue that you've pushed too far.
5. **Execution** happens only on a **discrete, earned trigger**:
   - You push in to within **1.5 m** of the hostage after the warning shot, **or**
   - You neither back off nor kill the guardian within **60 s** of the warning shot (the ultimatum).
   The execution is a scripted point‑blank shot (with gunshot report and blood).
6. **Release:** if you back off and the guardian loses contact for **6 continuous seconds**, it releases the hostage so you can then make contact and escort them out.

**Why the ladder:** it gives the trainee a *readable* sequence with clear off‑ramps — the failure is always earned and telegraphed, never a surprise from a silent countdown.

---

## 12. You save the hostage / clear the area

- Reaching the hostage lets you **escort** them (they stand up and follow you out).
- If **no terrorist has contact and the hunt budget expires**, the surviving squad **stands down together** and returns to patrol — the mission's threat state genuinely resets rather than leaving everyone frozen on alert.

---

## 13. Quick reference — the numbers

| Behaviour | Value | Field |
|---|---|---|
| Field of view | 120° (±60°) | `fovHalfAngle` |
| Sight range | 20 m | `detectionRange` |
| Time to confirm & open fire | 0.5 s continuous LOS | `targetConfirmTime` |
| Time to declare "lost" | 1.5 s no LOS | `lostSightDelay` |
| Hearing range | 20 m | `hearingRange` |
| Idle scan | every ~5 s, ±65°, hold 0.8 s | `idleScan*` |
| See‑at‑close → straight to fire | < 5 m | `engageDistance` |
| Combat spacing | min 3 m / prefer 3.5 m / advance if > ~5.5 m | `minStandoffDistance` etc. |
| Burst fire | 3 rounds @ 8/s, 1.2 s pause | `shotsPerBurst`, `fireRate`, `burstPause` |
| Spread (accuracy) | ~9° (locked) | `spreadDegrees` |
| Retreat when wounded | ≤ 40 HP, fall back 10 m, once per life | `retreatHealthThreshold` |
| Help a hurt mate | mate ≤ 60 HP or Down | `allySupportHealthThreshold` |
| Morale flip to cover | 3 pts (ally‑down +2, hit/stress/8 s +1) | `escalation*` |
| Suspicious give‑up | ~6 s | `suspiciousTimeout` |
| Squad hunt budget | ~45 s, re‑armed by fresh clues | `HuntBudgetSeconds` |
| Guardian threat / warning‑shot / execute range | 3.5 m / 2.5 m / 1.5 m | `warningRange` etc. |
| Guardian ultimatum after warning shot | 60 s | `executeAfterWarningShot` |

---

## 14. What's grounded in research vs. an engineering choice

- **Research‑grounded** (see `Assets/Docs/Literature_Review_Module2.docx`): react‑to‑contact (fight, don't stroll off); shared contact reporting; converge‑on‑contact; last‑seen‑point + expanding search; fire‑and‑manoeuvre / cover; morale driven by casualties + leadership; the two‑layer squad architecture.
- **Engineering / tuning choices** (reasonable, but not directly cited): the exact give‑up budget (~45 s), the guardian's door↔hostage pacing, and all the specific numeric thresholds above — these were tuned for game pacing and are honestly flagged as such in the review.

---

*Every behaviour above is verified running in the live scenario, not just written in code. Source files: `TerroristController.cs`, `PerceptionController.cs`, `Squad.cs`, `AlertPropagator.cs`, `NpcShooterRaycast.cs`, `NPCSelector.cs`.*
