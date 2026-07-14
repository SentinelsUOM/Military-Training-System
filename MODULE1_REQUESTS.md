# What Module 4 / NPC-AI still needs from Module 1

**From:** Simulation & NPC-AI (Module 2/4)
**To:** Module 1 — Dynamic Scenario Generation
**Date:** 14 July 2026

Module 1 generates the scenario: rooms, doors, layout, NPC positions, roles and navigation.
SceneBuilder consumes that JSON and builds the playable level. This document lists what the
generator does **not** emit today but that the NPC AI needs, ordered by what it unblocks.

---

## What Module 1 already gives us (no change needed)

Credit where due — these all work and we depend on them:

- **Layout**: rooms with `id`, `position`, `size`, `connectedRoomIds`, `doors`, `depth`,
  `type` (`entry` / `corridor` / `standard` / `hostage_room`).
- **Reachability is guaranteed** — the validator BFS-checks that every room is reachable from
  the trainee spawn, and that a door-path exists from the trainee to the hostage.
- **Roles**: `NpcRole` includes **`hostage_guardian`**, and `RoleAssigner` guarantees exactly
  one whenever `terroristCount >= 1`. This is what drives the whole hostage-leverage mechanic.
  It works — thank you.
- **Navigation context** per terrorist: patrol routes, guard rooms, roaming rooms,
  `guardedEntityId` / `hostageRoomId` for the guardian.
- **Trainee spawn** and **entry points**.
- **Determinism** — seeded RNG, reproducible from `seedUsed`. Very valuable for debugging.

---

# The requests

## REQ-1 · Cover points — **highest priority**
> Unblocks: the entire cover system (P1-1)

**The problem.** Module 1 emits **no cover of any kind** — no cover points, no firing
positions, no overwatch positions. Because of this, `CoverPoint.SelectBest()` always returns
`null` at runtime, and the terrorists' `CoverAndPeek` combat posture silently degrades to
"walk to a standoff distance and stand still".

**The consequence:** *every terrorist in every mission so far has fought standing in the open.*
Nobody has ever taken cover. This is the single biggest realism gap in the project.

**What we need** — a list of cover positions per room:

```jsonc
"coverPoints": [
  {
    "id": "cover_01",
    "roomId": "room_02",
    "position":        { "x": 3.2, "y": 0, "z": 5.1 },  // where the NPC stands
    "shieldDirection": { "x": 0,   "y": 0, "z": -1 },   // the direction it protects FROM
    "quality": 0.8,        // 0-1: 1.0 = full hard cover, 0.4 = partial/soft
    "stance": "crouch"     // "crouch" | "stand"  (drives the crouch animation)
  }
]
```

`shieldDirection` and `quality` are the important ones — `CoverPoint.SelectBest()` already
scores candidates by how well their shield direction opposes the threat direction. It just has
nothing to score.

**Guidance:** cover should sit against walls, in corners, behind door jambs, and behind any
furniture (see REQ-2). Aim for roughly **2–4 per standard room**, more in the hostage room.

---

## REQ-2 · Interior geometry / furniture
> Unblocks: REQ-1 (cover needs something to hide behind), and general believability

**The problem.** Rooms are **completely empty boxes**. SceneBuilder instantiates a floor, four
walls, a ceiling, windows and doors — and nothing else. No desks, no crates, no pillars, no
shelving.

Two consequences: cover has nothing to be *behind*, and a "terrorist safehouse" that is a bare
white box does not read as a real building to a trainee in a headset.

**What we need** — props with enough data to place and collide them:

```jsonc
"props": [
  {
    "id": "prop_01",
    "roomId": "room_02",
    "type": "crate",            // crate | desk | cabinet | pillar | barrel | table | shelf
    "position": { "x": 2.0, "y": 0, "z": 4.5 },
    "rotationY": 45.0,
    "size":     { "width": 1.0, "depth": 1.0, "height": 1.2 },
    "providesCover": true       // if true, we generate cover behind it
  }
]
```

If `providesCover` is set, we can derive REQ-1's cover points ourselves — so **REQ-2 alone
could satisfy REQ-1**, if that is easier for you. Either approach works; we'd rather have
furniture with cover derived from it than floating invisible cover markers.

We also need to know the prop prefabs you expect us to instantiate, or we'll supply them.

---

## REQ-3 · Per-waypoint facing, dwell and scan
> Unblocks: patrol scanning (P2-1)

**The problem.** Patrol `waypoints` are **bare positions, and they are literally room centres**
(`NavigationContextBuilder` emits `roomMap[roomId].position`). There is:
- no per-waypoint **facing**,
- no **dwell / pause** time,
- no **scan direction or arc**,
- and **patrol and roaming NPCs get no `facingDirection` at all** (only the two stationary
  roles do).

**The consequence:** a patrolling guard only ever looks in the direction he is walking. He never
turns his head, never stops to check a corner. He is trivial to follow and sneak up on. He also
walks to the exact **middle** of each room, which is not how anyone patrols a building.

**What we need** — richer waypoints:

```jsonc
"waypoints": [
  {
    "position":  { "x": 4.0, "y": 0, "z": 2.0 },
    "facing":    { "x": 0,   "y": 0, "z": 1 },   // where he looks on ARRIVAL
    "dwellSeconds": 3.5,                          // how long he holds here
    "scanArcDegrees": 90                          // sweep ±45° around `facing` while dwelling
  }
]
```

And please route patrols **around the room** (near walls, past doors, covering entrances) rather
than through the dead centre.

*Note: this changes `waypoints` from `List<Vector3>` to a list of objects — a breaking schema
change. We're happy to support both shapes during migration.*

---

## REQ-4 · Exterior / breach doors are not expressible — and it caused a real bug
> Unblocks: removing a workaround we currently carry in SceneBuilder

**The problem.** `DoorData.isExterior` exists in the schema but is **never assigned anywhere** —
it always serialises `false`. Meanwhile `ScenarioValidator.CheckDoorConsistency` **requires**
every door to have:
- a non-empty `connectsToRoomId`,
- which resolves to a real interior room,
- **and** a reciprocal door in that room with the same `id` pointing back.

So a door leading **outside** or **into the perimeter corridor cannot be expressed at all** — it
would fail validation.

**What already broke because of this.** SceneBuilder has to *synthesise* the entry doors and the
perimeter-corridor door (`door_corridor_entry`) at build time, because they can't come from the
scenario. Our NavMeshLink code walked `scenario.layout.rooms[].doors` — which by definition only
ever contains room-to-room doors — so the corridor door **never got a NavMeshLink**. Measured
result: **0 out of 6 corridor points were reachable** by any terrorist. They physically could not
follow the trainee into the corridor.

We fixed it on our side (SceneBuilder now records every door it builds), but the schema is still
lying to us.

**What we need:**
- **Actually set `isExterior`**, and
- relax the validator so `connectsToRoomId` may be null/empty **when `isExterior == true`**
  (skip the reciprocal-door check for those), and
- emit the perimeter-corridor / entry doors as real `DoorData` records.

---

## REQ-5 · Chokepoint annotation
> Unblocks: tactical spawns and overwatch positions (P2-2, P1-2)

**The problem.** `LayoutGenerator` *knows* where the chokepoint is — there is a comment on
`GenerateHubAndSpokeTopology` saying *"Hub is the critical chokepoint"* — but **nothing is
emitted**. Downstream we can only re-derive it by counting `connectedRoomIds` degree.

**What we need** — say it out loud in the JSON:

```jsonc
"layoutMetadata": {
  "chokepointRoomIds": ["room_01"],       // rooms everything must pass through
  "chokepointDoorIds": ["door_01_02"]     // the doors that matter tactically
}
```

This lets us post a guard to actually *cover* the chokepoint, instead of placing him near it by
luck.

---

## REQ-6 · Per-NPC combat profile — difficulty currently changes nothing in a fight
> Unblocks: difficulty actually meaning something

**The problem.** An NPC entity carries **no health, no weapon, no accuracy, no detection range,
no reaction time**. All of that lives on the `TerroristNPC` prefab, identically for every enemy.

`difficultyLevel` (1–5) currently affects only **two** things: which intra-room placement zone is
used, and the role quota mix. **It does not change how dangerous an enemy is.** A difficulty-1
terrorist and a difficulty-5 terrorist shoot exactly the same, see exactly as far, and take
exactly as many bullets.

**What we need** — a combat profile per terrorist (or a single difficulty-derived profile we
apply to all):

```jsonc
"combatProfile": {
  "health": 80,
  "accuracySpreadDegrees": 5.0,   // higher = worse shot
  "detectionRange": 20.0,
  "reactionTimeSeconds": 0.8,     // delay between seeing and firing
  "aggression": 0.6               // 0-1: hangs back vs pushes the trainee
}
```

Our current tuning for reference (difficulty ~3): health 80, spread 5°, damage 10/round, 3-round
bursts. The trainee has 100 HP and dies in 10 hits; a terrorist dies in 3 hits.

---

## REQ-7 · Author the extraction / safe zone
> Unblocks: a possible NavMesh blocker (P0-6)

**The problem.** The extraction zone is **not in the schema**. SceneBuilder invents it at build
time and drops it on top of the trainee's spawn point. It is not authored, not validated, and
not reproducible from the JSON.

**Why it matters right now.** We measured something worrying: terrorists can path to *each other*
fine, but **no terrorist could compute a path to the trainee's spawn position**. If that holds
up, the entry room is a disconnected NavMesh island and the enemy can never reach the trainee
where the mission begins. We are still confirming whether this is real.

Since the extraction zone and the trainee spawn are the same point, and neither is validated for
NavMesh connectivity, this is a blind spot in the pipeline.

**What we need:**
- An explicit `extractionZone { roomId, position, radius }` in the schema.
- A validator rule: **the trainee spawn and the extraction zone must be on the same connected
  navigable region as every terrorist spawn.** Your reachability check is a *room-graph* BFS —
  it does not know about the baked NavMesh, so it cannot currently catch this class of bug.

---

# Bugs we found in Module 1

Not requests — things that look wrong:

**BUG-1 · Scenarios can have TWO hostage rooms.**
`LayoutGenerator.AssignRoomTypes` labels the deepest dead-end as `hostage_room`. Then
`EntityPlacer` **force-sets** `hostageRoom.type = HostageRoom` on a room it picked independently,
which may be a different room. Both stay labelled. Visible in the committed
`Scenario_056ee372-….json`, where `room_02` **and** `room_03` are both `"type":"hostage_room"`.

**BUG-2 · Scenarios that FAIL validation are still emitted.**
The retry loop tries 3 seeds; if validation never passes, it returns the last scenario anyway
with `validationResult.passed = false`. A broken scenario can therefore reach SceneBuilder and be
played. It should either throw, or SceneBuilder should refuse to build a scenario with
`passed == false`. Please confirm which you'd prefer.

**BUG-3 · `DoorData.isExterior` is dead.** Declared, never assigned. See REQ-4.

**BUG-4 · Every room in a scenario is the same size.** Room size comes from a single config enum
(small 4×4, medium 6×6, large 8×8), so all rooms are identical. Real buildings mix sizes. This
also makes cover and furniture placement monotonous.

**BUG-5 · Doors are placed at the midpoint between two room centres** — i.e. floating in the 2 m
gap between rooms, not on a wall plane. It works because SceneBuilder re-derives the wall
position, but the JSON coordinate is not the door's real location.

---

# Priority order

| # | Request | Unblocks | Priority |
|---|---|---|---|
| REQ-1 / REQ-2 | Cover points + furniture | Cover system — the biggest realism gap | **P1 — critical** |
| REQ-4 | Exterior doors expressible | Removes a SceneBuilder workaround | **High** |
| REQ-7 | Authored extraction zone + NavMesh validation | Possible mission-breaking blocker | **High** |
| REQ-3 | Waypoint facing / dwell / scan | Patrol scanning | Medium |
| REQ-5 | Chokepoint annotation | Tactical spawns, overwatch | Medium |
| REQ-6 | Per-NPC combat profile | Difficulty means something | Medium |
| BUG-1…5 | — | Correctness | As convenient |

---

# Open questions for Module 1

1. **Cover — furniture-derived or explicit markers?** We'd prefer **REQ-2 (furniture) with cover
   derived from it**, since it solves believability at the same time. Which is easier for you?
2. **Who supplies the prop prefabs** — you, or us? We're happy to provide crates/desks/pillars
   and a naming convention you can emit against.
3. **Is `hostageCount` staying at 1?** It's currently a hard constant. Our scoring assumes one
   hostage; if multi-hostage is coming, we need to know before we finalise the win condition.
4. **REQ-3 is a breaking schema change** (`waypoints` becomes a list of objects). Are you OK with
   that, or should we add a parallel `waypointsDetailed` field instead?
5. **BUG-2** — should a scenario that fails validation throw, or should SceneBuilder refuse it?

---

## Reference — the schema as it stands

For your convenience, this is what a scenario contains today:

```
ScenarioData
├── scenarioId, missionType
├── layout
│   ├── rooms[]        { id, type, position, size, connectedRoomIds, doors[], depth, zoneLabel }
│   │   └── doors[]    { id, connectsToRoomId, position, wallSide, state, isExterior(dead) }
│   ├── entryPoints[]  { id, roomId, position, facingDirection }
│   └── layoutMetadata { totalRooms, layoutType, maxDepth, graphDiameter, roomSizeCategory,
│                        entryType, boundingBox }
├── spawnPoints
│   ├── trainee        { roomId, position, facingDirection }
│   ├── hostages[]     { entityId, roomId, position, facingDirection }
│   └── terrorists[]   { entityId, roomId, position, facingDirection }
├── entities[]         { id, type, assignedRoom, position, metadata{ initialState } }
├── roleAssignments[]  { entityId, role, priorityLevel, assignedRoomId, navigationContextId }
├── navigationContext  { <navId>: { type, patrolRoute[], looping, guardRoomId, roamingRoomIds[],
│                        anchorRoomId, guardedEntityId, hostageRoomId, waypoints[],
│                        guardPosition, facingDirection } }
└── configurationMetadata { scenarioConfig, generationTimestamp, seedUsed, generatorVersion,
                            validationResult }
```

**Missing:** cover points · furniture/props · firing & overwatch positions · chokepoint
annotation · extraction zone · NPC combat stats · per-waypoint facing/dwell/scan · exterior door
records · room *function* semantics · per-room size variation.
