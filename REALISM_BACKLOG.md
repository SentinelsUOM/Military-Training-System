# Realism Backlog — Sentinels VR Hostage-Rescue Trainer

**Branch:** `feature/npcMovement-2` · **Surveyed:** 13 July 2026

Everything still open, grouped by priority. Each item states what it is, why it matters,
where it lives in the code, and what would prove it done.

| Tier | Count | Note |
|---|---|---|
| **P0** | 7 open | Win/fail integrity — blocks release |
| **P1** | 4 | Fire & maneuver |
| **P2** | 3 | Perception & presence |
| **P3** | 8 | Reaction & polish |
| *Needs assets from you* | 3 | P0-7, P3-6, P3-7 |

---

## Two findings that change the plan

**The cover system is dead code.** It was scoped as P1, but it never runs today:
`TerroristState.TakeCover` is never entered from anywhere in the project, and zero
`CoverPoint` instances exist in a generated scene. So `CoverAndPeek` silently degrades to
"stand at a standoff distance". **Every terrorist you have ever fought has been fighting in
the open.**

**The PASS/FAIL banner can contradict the mission outcome.** The result screen does not read
the end reason — it recomputes success independently. Die *after* the hostage reaches the
extraction zone and you get "RESCUE COMPLETE" over the subtitle "You were killed in action."
That is squarely inside P0's win/fail integrity, not P1 polish.

---

# P0 — Win / fail integrity

The hostage-as-leverage mechanic (shield + execution) is **built and verified live**. What
remains in P0 is the other half you scoped: making the mission actually *resolve* correctly,
and proving it.

## P0-1 · Result screen can declare success on a failed mission
> **Type:** correctness bug

- **What** — `MissionResultUI` reads `summary.performance.missionSuccess`, which
  `PerformanceCalculator` computes on its own as `hostagesSaved >= 1`, completely ignoring
  the end reason.
- **Why** — A trainee who frees the hostage and is then shot dead sees **"RESCUE COMPLETE"**.
  The AAR then teaches the wrong lesson, which is worse than no AAR at all.
- **Where** — `Assets/Module4/Analysis/PerformanceCalculator.cs:58` ·
  `Assets/Module4Integration/MissionResultUI.cs:63`
- **Done when**
  - `player_down`, `hostage_executed`, `hostage_killed` force `missionSuccess = false`
    regardless of who was freed.
  - Banner and subtitle always agree.

## P0-2 · Shooting the hostage carries no scoring penalty
> **Type:** correctness bug

- **What** — `friendlyFireCount` is **always 0** in real play. `HostageHitBox.TakeDamage`
  raises no scenario event, and `Module4Bridge` creates every mission event with no tags — so
  nothing is ever tagged `friendly_fire`. The only place that tag exists is the test harness
  (`AARTestRunner`).
- **Why** — The `-0.2` safety penalty never fires. Hitting the person you came to rescue
  currently costs the trainee **nothing** unless it outright kills them.
- **Where** — `Assets/Scripts/NPC/HostageHitBox.cs:31-35` ·
  `Assets/Module4Integration/Module4Bridge.cs:73-80` ·
  `Assets/Module4/Analysis/PerformanceCalculator.cs:48-51`
- **Done when** — A non-fatal round into the hostage raises a tagged event, increments
  `friendlyFireCount`, and visibly drops `safetyScore` on the AAR.

## P0-3 · Player death → FAIL has never been watched happen
> **Type:** verification

- **What** — The chain exists and is statically sound: bullet → `PlayerHealth.TakeDamage` →
  `Update()` polls `health <= 0` → `EndSession("player_down")`. It has never been observed
  end-to-end.
- **Why** — This was the immortality bug's home ground. Two fragilities remain:
  `playerHealth` is a hand-assigned `[SerializeField]` with **no auto-lookup fallback**, and
  `PlayerHealth.OnPlayerDied` has **zero subscribers** despite its docs claiming
  `Module4SessionController` subscribes (it polls instead).
- **Where** — `Assets/Module4Integration/Module4SessionController.cs:150-154` ·
  `Assets/XRI Starter Kit/Assets/Scripts/PlayerHealth.cs:13`
- **Done when** — 10 hits at 10 damage drop health to 0, session ends `player_down`, FAIL
  renders. *I can drive this over MCP.*

## P0-4 · The success path has never been watched happen
> **Type:** verification

- **What** — Extraction is fully wired — contact zone → `Follow` → `ExtractionZone` →
  `HostageFreed` → `EndSession("hostages_rescued")` — but no mission has ever been seen
  *ending in success*. Only failure branches have run.
- **Why** — It is the only win condition in the game. There is no "all terrorists eliminated"
  win anywhere.
- **Where** — `Assets/Module4Integration/ExtractionZone.cs:91-130` ·
  `Assets/Module4Integration/Module4SessionController.cs:282-299`
- **Note** — `AllHostagesFreed()` is lenient: it returns true on `freed >= 1`, despite the
  name. Fine while there is one hostage; revisit for multi-hostage scenarios.
- **Done when** — Hostage escorted into the safe zone → `hostages_rescued` → SUCCESS screen.

## P0-5 · The human shield has never run in a real firefight
> **Type:** verification

- **What** — You scoped leverage as "shield **and** execution". The execution ladder is now
  verified end-to-end. The **shield grab** — guardian pulls the hostage in front of him under
  pressure — is compiled and logic-checked but never observed.
- **Why** — Trigger conditions are narrow: `posture == CoverAndPeek` **or** wounded below
  `retreatHealthThreshold` **or** trainee within `shieldTriggerRange` (6 m) — all requiring
  live LOS. Note the first condition depends on `CoverAndPeek`, which is **currently degraded
  by P1-1**.
- **Where** — `TerroristController.GuardianPressureTick()`
- **Done when** — Guardian is seen taking the hostage as a shield, and de-escalating when you
  back off.

## P0-6 · Terrorists may not be able to path to the trainee's spawn
> **Type:** possible blocker — **not yet confirmed**

- **What** — Measured during verification: terrorist↔terrorist paths are all `PathComplete`
  (the corridor/door NavMeshLink fix is solid — all 4 links active, including
  `door_corridor_entry`), but every terrorist → **trainee spawn** path returns `PathPartial`,
  despite the spawn sitting only 0.08 m off the mesh. Generated doors are *carving*
  NavMeshObstacles.
- **Why** — If real, the entry room is a disconnected NavMesh island and nobody can reach you
  where you start — which would also make P0-3 and P0-5 fail for an unrelated reason.
- **Caveat** — I built the scene manually mid-Play-mode, which is **not** the normal startup
  path. This may be an artifact of the test harness.
- **Done when** — Confirmed or dismissed on a clean mission start.

## P0-7 · Warning shot and execution have no muzzle flash
> **Type:** needs an asset from you

- **What** — `muzzleFlash` is unassigned on `Assets/Prefabs/TerroristNPC.prefab`, so both
  shots are **audio-only**. The code path already calls `muzzleFlash.Play()` — the slot is
  simply empty.
- **Why** — The warning shot is the loudest rung of the escalation ladder and the trainee's
  cue to act. In a headset, a flash is most of that signal.
- **Needed** — A muzzle-flash `ParticleSystem`. Give me one and I will wire it.

---

# P1 — Fire & maneuver

Making a firefight read as a *squad* fighting, rather than several individuals shooting.
This is where the biggest realism gain per hour of work sits.

## P1-1 · Resurrect the cover system — nobody has ever taken cover
> **Type:** dead code

- **What** — Two independent breaks:
  - **(a)** `TerroristState.TakeCover` is never entered — there is no
    `TransitionTo(TakeCover, …)` call in the entire project, so the state handler and the
    whole `MoveToCover()` coroutine are unreachable.
  - **(b)** No `CoverPoint` exists in a generated scene — `SceneBuilder` never creates any, so
    `CoverPoint.SelectBest()` always returns `null`.
- **Why** — `CoverAndPeek`, the escalated posture the whole morale model builds toward,
  silently falls back to `agent.SetDestination(StandoffPoint(threat))`. Enemies stand in the
  open and trade shots. It also disarms one of the three shield-grab triggers (P0-5).
- **Where** — `Assets/Scripts/NPC/CoverPoint.cs` (complete, unused) ·
  `TerroristController.cs:641-665, 2171-2207` · `SceneBuilder.cs`
- **Done when**
  - `SceneBuilder` generates `CoverPoint`s from room geometry (walls, corners, furniture,
    door jambs).
  - Terrorists actually enter `TakeCover`, and peek-and-fire visibly works.

## P1-2 · Suppressive fire / overwatch — the covering NPC never shoots
> **Type:** missing

- **What** — The COVER branch of `SquadSupportRoutine` already sends an NPC to hold the
  doorway that best lines up with the threat. He aims at it — **and never fires**.
  `StartFiring()` is only ever called on entry to `Engage`.
- **Why** — Suppression is what makes a squad feel coordinated, and it is what pins a trainee
  down so that a flanker *means* something.
- **Tension to respect** — Firing without LOS is **deliberately forbidden** today:
  `CanRespond` drops `PlayerSeen`/`TargetConfirmed` from the bus so nobody engages on hearsay.
  Suppression needs a *controlled exception* (fire at a doorway / last-known position, wide
  spread, no damage to unseen targets) rather than lifting the rule.
- **Where** — `TerroristController.cs:2440-2504` (COVER branch) · `:1510-1518`

## P1-3 · Bounding: the flanker is never the shooter
> **Type:** partial

- **What** — A `Flank` directive exists and works. But `Squad.IssueDirective` **skips members
  already in `Engage`**, and `OnDirective` early-returns for `Engage`. So by construction the
  flanking NPC is not firing, and the firing NPC never flanks. There is no paired "you move,
  I shoot" bounding anywhere.
- **Why** — Real CQB movement is bounding overwatch. Right now movement and fire are mutually
  exclusive across the whole squad.
- **Where** — `Assets/Scripts/NPC/Squad.cs:91` · `TerroristController.cs:2954, 3034-3054`
- **Note** — `LeaderDirectiveType.Hold` is defined but **never issued**. It is the natural
  primitive for "hold and cover while he moves".

## P1-4 · Investigate under overwatch
> **Type:** missing

- **What** — One NPC searches a room or corridor while a second holds the entry and covers
  him. Today the investigator walks in alone and the others idle.
- **Why** — Sending a lone man through a door unsupported is the least realistic thing the
  squad currently does — you have flagged it twice in play-tests.
- **Depends on** — P1-2 (a covering NPC that can actually fire) and the unused `Hold`
  directive.

---

# P2 — Perception & presence

How the enemy behaves *before* contact — the part of the mission the trainee spends most of
their time in.

## P2-1 · Patrolling NPCs never look around
> **Type:** missing

- **What** — `PatrolLine` rotates an NPC **only toward its direction of travel**. No head
  turn, no pause-and-scan. `IdleMode.Wander` pauses at each point but does not look around
  during the pause. Head/eye IK only activates once the player has *already* been seen.
- **Why** — A patroller who only ever looks where his feet are going is trivially followed.
  Scanning is what makes the stealth approach a real decision.
- **Good news** — The machinery exists: `ScanRotation()` already does an N-direction scan, but
  is only called inside `InvestigateRoutine`. It needs to run in Idle too.
- **Where** — `Assets/XRI Starter Kit/Assets/Scripts/PatrolLine.cs:104-143` ·
  `TerroristController.cs:2649-2677, 2900-2929`

## P2-2 · Tactical spawns: zones exist, chokepoint scoring does not
> **Type:** partial

- **What** — Module 1's `EntityPlacer` is smarter than expected: real placement zones
  (`BehindDoor`, `SightlineBlind`, `Corner`) switch on at difficulty 4–5, it never spawns in
  the entry room, and it faces each terrorist toward his nearest door. But positions *within*
  a zone are RNG-sampled, never scored for sightline coverage or overlap.
- **Why** — Enemies are placed in plausible *kinds* of places, but nobody is deliberately
  covering the chokepoint. Firing positions and cover are not generated at all.
- **Where** — `Assets/Module1_DataModels_and_IO/Scripts/ScenarioGeneration/Generators/EntityPlacer.cs:378-548`
- **Pairs with** — P1-1: once `SceneBuilder` generates cover, spawns can be scored against it.

## P2-3 · Morale never breaks — no rout, no surrender
> **Type:** partial

- **What** — Escalation works: ally deaths, hits taken and sustained combat accumulate a
  stress score that flips posture `HoldAndShoot → CoverAndPeek`. But it is **one-way** (never
  decays, never de-escalates) and there is **no squad-strength check** — nothing counts how
  many are still alive.
- **Why** — The last man standing fights exactly as hard as a fresh four-man team. There is no
  `Flee` or `Surrender` member in `TerroristState` at all. Real hostage-takers break.
- **Where** — `TerroristController.AddEscalation():596-610` ·
  `Assets/Scripts/NPC/TerroristState.cs`
- **Counter-rule to preserve** — An NPC that has personally confirmed the player can never
  stand down. Rout must not undo "never give up on me".

---

# P3 — Reaction & polish

Lower leverage individually, but several are small and directly answer things you flagged in
play-tests.

## P3-1 · Nobody reacts to a corpse they didn't see fall
- **What** — `AllyDownSeen` fires **exactly once, at the moment of death**, and only to
  squadmates who had LOS right then. `PerceptionController` only ever ray-casts to the
  player — an NPC who later walks past the body sees nothing.
- **Bug worth fixing regardless** — Ordinary alert propagation *also* raises `AllyDownSeen`
  when nobody is down, so the event is not a trustworthy "someone died" signal.
- **Where** — `Assets/Scripts/NPC/AlertPropagator.cs:91-116` ·
  `Assets/Scripts/NPC/PerceptionController.cs`

## P3-2 · The squad leader is never replaced
- **What** — `SceneBuilder` elects exactly one leader at spawn. If he dies, no one is
  re-elected — `Converge` and `Flank` directives stop being issued for the rest of the
  mission.
- **Why** — Killing the leader first silently switches the whole squad's coordination off.
  That is an exploit, not a tactic.
- **Where** — `SceneBuilder.cs:1485-1500`

## P3-3 · Guardian steps to the door, checks, returns to the hostage
- From your original guard brief. Today he searches on a leash around the hostage but never
  advances to the doorway to look, then falls back.
- **Constraint** — He must never break the leash. The hostage is never left alone, for any
  reason.

## P3-4 · Converge on a repeatedly-confirmed position
- If a spotter confirms the trainee's position two or more times, the others should commit and
  converge — not keep sweeping their own stale search areas.
- **Partially addressed already** — fresh contact now abandons a stale search, and gunshots
  re-task the sweep. The 2+ confirmation escalation itself is not built.

## P3-5 · Don't enter the corridor until the contact is confirmed
- Deferred from an earlier session. Terrorists should hold the room and cover the doorway
  rather than filing into an open corridor on a single unconfirmed noise.

## P3-6 · Stressed / agitated guard idle animation
> **Needs an asset from you**
- Under pressure the guardian holds the same composed alert idle as when relaxed. A tense
  variant would telegraph escalation before he ever speaks.
- **Needed** — An agitated rifle idle clip. None supplied yet.

## P3-7 · Hostage distress audio
> **Needs an asset from you**
- The hostage is silent throughout — including while held at gunpoint and while being
  threatened. The guardian has four VO lines; the hostage has none.
- **Needed** — A few distress / pleading clips, same treatment as the guardian VO.

## P3-8 · Barrel-on-player aim during normal firing
> **Deferred by you**
- The damped multi-pass CCD aim solver is built and now proven: it puts the barrel on a
  kneeling hostage's head to **0.09°** with the grip intact. It is switched **off** for normal
  combat (`useProceduralAim = false`); terrorists shoot from the firing animation's fixed pose.
- You said: *"they're not pointing the gun at me when shooting, but it's OK for now."* The
  machinery is ready whenever you want it on.

---

# Shipped & verified

Verified by driving the running game over MCP and **measuring**, not by reading the code:

- Four-stage escalation ladder (seize → threat → warning shot → execute) with split VO
- Hostage kneel with **no blinking** (`Held` stable, `Scared` cleared)
- The rifle **stays in the guardian's fist** while aiming at the hostage's head — barrel-to-head **0.09°**, `GunAimPivot` deviation `0.00°`
- **Audible execution shot** (was completely silent)
- `hostage_executed` propagating to the session as a FAIL
- Guard **turns off the doorway onto a visible trainee** — body yaw `56.3° → 0.0°`, gaze error `0.00 m`
- Burst fire + trigger discipline (`shotsPerBurst = 3`, `burstPause = 1.2`)
- Corridor NavMeshLinks — all terrorist↔terrorist paths `PathComplete`, 4 links incl. `door_corridor_entry`
- Squad leader directives (`Converge` / `Flank`), 3-ring alert propagation
- Hostage escort + extraction wired end-to-end
- A **single** `PlayerHealth`, on `PlayerHitbox` (the immortality fix)
