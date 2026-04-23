# NPC Role Assignment & Navigation Context Algorithm Design

**Phase 1 – Prompt 1.5 Deliverable**
**Module 1 – Dynamic Scenario Generation**

Team Sentinels | University of Moratuwa | April 2026
VR-Based Hostage-Rescue Training System | Unity 2022.3 LTS | Meta Quest 3

---

## 1. Overview

This document defines the depth-based NPC role assignment and navigation context generation algorithms. These algorithms directly address **RQ2**: *How can NPC role assignments and navigation context be derived from the generated spatial structure in a semantically meaningful way?*

The algorithms consume the layout (from LayoutGenerator) and entity placements (from EntityPlacer) and produce the `roleAssignments` and `navigationContext` sections of Scenario.json — the primary data consumed by Module 2 (NPC Behaviour) at runtime.

**Literature-backed design decisions:**

- **Path DAG and BFS depth** determine role assignment — high-depth rooms are natural guard positions; shallow-depth rooms are natural patrol areas. [18]'s resistor network analog for flow direction.
- **Role assignments are spatially derived, not arbitrary** — this is the core of RQ2. [11]'s Space Syntax analysis: private rooms at deep nodes, public rooms at shallow nodes.
- **Navigation context must be traceable** to evaluator parameters so training evidence remains reliable. [22]'s evaluation framework requirement.
- **Difficulty level modulates role distribution** — higher difficulty increases the proportion of tactically challenging roles. [20]'s expected challenge density parameter M.

---

## 2. Role Type Definitions

| Role | Depth Zone | Behaviour | Tactical Purpose |
|------|-----------|-----------|------------------|
| **patrol** | Shallow (depth ≤ ⌈maxDepth/3⌉) | Moves continuously along a route through shallow rooms | Early warning system. Trainee encounters them first. Mobile — harder to predict. |
| **stationary_guard** | Deep (depth ≥ ⌈2×maxDepth/3⌉) | Holds fixed position in deep room, faces primary door | Blocks approach to hostage. Positioned for ambush per [3]. Stationary — rewards careful approach. |
| **roaming_guard** | Mid (depth between patrol and guard zones) | Moves between assigned room and adjacent rooms | Unpredictable middle layer. May flank trainee. Covers gaps between patrol and guard zones. |
| **hostage_guardian** | Same room as hostage | Stays near hostage, faces room entry | Final obstacle. Highest priority response. Creates dilemma: engage guardian without harming hostage. |

---

## 3. Role Assignment Decision Matrix

The matrix maps (depth zone × difficulty level × available NPC count) to role distribution. The hostage_guardian is always assigned first (mandatory when terroristCount ≥ 2), then remaining terrorists are distributed.

### 3.1 Base Role Distribution (after 1 hostage_guardian is assigned)

| Remaining NPCs | Difficulty 1–2 | Difficulty 3 | Difficulty 4–5 |
|----------------|----------------|--------------|----------------|
| 1 | 1 patrol | 1 roaming_guard | 1 stationary_guard |
| 2 | 1 patrol, 1 roaming_guard | 1 patrol, 1 stationary_guard | 1 roaming_guard, 1 stationary_guard |
| 3 | 2 patrol, 1 roaming_guard | 1 patrol, 1 roaming_guard, 1 stationary_guard | 1 patrol, 1 roaming_guard, 1 stationary_guard |
| 4 | 2 patrol, 1 roaming_guard, 1 stationary_guard | 1 patrol, 2 roaming_guard, 1 stationary_guard | 1 patrol, 1 roaming_guard, 2 stationary_guard |
| 5 | 2 patrol, 2 roaming_guard, 1 stationary_guard | 2 patrol, 1 roaming_guard, 2 stationary_guard | 1 patrol, 2 roaming_guard, 2 stationary_guard |
| 6 | 3 patrol, 2 roaming_guard, 1 stationary_guard | 2 patrol, 2 roaming_guard, 2 stationary_guard | 1 patrol, 2 roaming_guard, 3 stationary_guard |
| 7 | 3 patrol, 2 roaming_guard, 2 stationary_guard | 2 patrol, 3 roaming_guard, 2 stationary_guard | 1 patrol, 3 roaming_guard, 3 stationary_guard |

**Design rationale:** Low difficulty favours patrol (mobile, visible, easier to detect). High difficulty favours stationary_guard (ambush positions per [3]) and roaming_guard (unpredictable). This maps [20]'s challenge density parameter M onto role distribution.

### 3.2 Edge Cases

| Case | Handling |
|------|----------|
| terroristCount = 1 | Assign as hostage_guardian (no other roles) |
| More terrorists than rooms | Multiple terrorists per room allowed; each gets a distinct position via EntityPlacer's clearance check |
| Only 1 unique depth level (e.g., hub_and_spoke where all spokes = depth 1) | Treat all non-entry rooms as mid-depth → assign roaming_guard roles to fill patrol and guard slots |
| maxDepth = 1 (all rooms adjacent to entry) | patrol zone = depth 1, no stationary_guard zone → convert stationary_guard allocation to roaming_guard |

---

## 4. RoleAssigner Pseudocode

```
CLASS RoleAssigner

FUNCTION AssignRoles(layout: Layout, spawns: SpawnPoints, 
                     entities: List<EntityRecord>, config: ScenarioConfig) 
    -> List<RoleAssignment>

    difficulty ← config.executionControls.difficultyLevel
    terrorists ← entities WHERE type == "terrorist"
    hostageEntity ← entities WHERE type == "hostage" [first]
    hostageRoomId ← hostageEntity.assignedRoom
    maxDepth ← layout.layoutMetadata.maxDepth
    assignments ← []

    // ═══════════════════════════════════════════
    // STEP 1: Assign Hostage Guardian (mandatory)
    // ═══════════════════════════════════════════
    IF terrorists.Count >= 2 THEN
        // Pick terrorist closest to hostage (preferably same room)
        guardian ← SelectClosestTerrorist(terrorists, hostageEntity.position, 
                                          hostageRoomId)
        assignments.Add(RoleAssignment {
            entityId = guardian.id,
            role = "hostage_guardian",
            priorityLevel = 1,           // highest priority
            assignedRoomId = hostageRoomId,
            navigationContextId = "nav_" + guardian.id
        })
        terrorists.Remove(guardian)
    ELSE IF terrorists.Count == 1 THEN
        // Only 1 terrorist → must be hostage_guardian
        assignments.Add(RoleAssignment {
            entityId = terrorists[0].id,
            role = "hostage_guardian",
            priorityLevel = 1,
            assignedRoomId = hostageRoomId,
            navigationContextId = "nav_" + terrorists[0].id
        })
        RETURN assignments    // done — only one terrorist

    // ═══════════════════════════════════════════
    // STEP 2: Determine Role Quotas from Matrix
    // ═══════════════════════════════════════════
    remaining ← terrorists.Count
    (patrolCount, roamingCount, guardCount) ← GetRoleQuotas(remaining, difficulty)

    // ═══════════════════════════════════════════
    // STEP 3: Define Depth Zones
    // ═══════════════════════════════════════════
    shallowMax ← CEIL(maxDepth / 3.0)        // patrol zone
    deepMin ← CEIL(2.0 * maxDepth / 3.0)     // stationary guard zone
    // mid zone: shallowMax < depth < deepMin  // roaming guard zone

    // Handle degenerate case: maxDepth ≤ 1
    IF maxDepth <= 1 THEN
        shallowMax ← 1
        deepMin ← maxDepth + 1   // no deep zone → convert guards to roaming
        roamingCount ← roamingCount + guardCount
        guardCount ← 0

    // ═══════════════════════════════════════════
    // STEP 4: Sort Terrorists by Room Depth
    // ═══════════════════════════════════════════
    terrorists ← SortByRoomDepth(terrorists, layout)

    // ═══════════════════════════════════════════
    // STEP 5: Assign Roles by Depth Zone Matching
    // ═══════════════════════════════════════════
    patrolAssigned ← 0
    roamingAssigned ← 0
    guardAssigned ← 0

    FOR EACH terrorist IN terrorists:
        roomDepth ← GetRoomDepth(layout, terrorist.assignedRoom)
        role ← ""
        priority ← 0

        // Match to best-fit zone
        IF roomDepth <= shallowMax AND patrolAssigned < patrolCount THEN
            role ← "patrol"
            priority ← 3                // lowest priority (early warning, not critical)
            patrolAssigned ← patrolAssigned + 1

        ELSE IF roomDepth >= deepMin AND guardAssigned < guardCount THEN
            role ← "stationary_guard"
            priority ← 2
            guardAssigned ← guardAssigned + 1

        ELSE IF roamingAssigned < roamingCount THEN
            role ← "roaming_guard"
            priority ← 2
            roamingAssigned ← roamingAssigned + 1

        ELSE
            // Overflow: assign whatever slot remains
            IF patrolAssigned < patrolCount THEN
                role ← "patrol"; priority ← 3; patrolAssigned++
            ELSE IF roamingAssigned < roamingCount THEN
                role ← "roaming_guard"; priority ← 2; roamingAssigned++
            ELSE IF guardAssigned < guardCount THEN
                role ← "stationary_guard"; priority ← 2; guardAssigned++
            ELSE
                role ← "roaming_guard"; priority ← 2  // fallback

        assignments.Add(RoleAssignment {
            entityId = terrorist.id,
            role = role,
            priorityLevel = priority,
            assignedRoomId = terrorist.assignedRoom,
            navigationContextId = "nav_" + terrorist.id
        })

    RETURN assignments
END FUNCTION

// ─── Helper: Role Quotas from Decision Matrix ───
FUNCTION GetRoleQuotas(remaining: int, difficulty: int) -> (int, int, int)
    // Returns (patrolCount, roamingCount, guardCount)
    IF difficulty <= 2 THEN        // low difficulty → more patrols
        patrolCount ← CEIL(remaining * 0.5)
        guardCount ← FLOOR(remaining * 0.15)
        roamingCount ← remaining - patrolCount - guardCount
    ELSE IF difficulty == 3 THEN   // balanced
        patrolCount ← CEIL(remaining * 0.33)
        guardCount ← FLOOR(remaining * 0.33)
        roamingCount ← remaining - patrolCount - guardCount
    ELSE                           // high difficulty → more guards
        patrolCount ← MAX(1, FLOOR(remaining * 0.15))
        guardCount ← CEIL(remaining * 0.5)
        roamingCount ← remaining - patrolCount - guardCount
    
    // Ensure at least 1 patrol if remaining >= 2
    IF remaining >= 2 AND patrolCount == 0 THEN
        patrolCount ← 1
        roamingCount ← roamingCount - 1

    RETURN (patrolCount, MAX(0, roamingCount), MAX(0, guardCount))
END FUNCTION

// ─── Helper: Closest Terrorist to Hostage ───
FUNCTION SelectClosestTerrorist(terrorists, hostagePos, hostageRoomId) -> EntityRecord
    // Prefer terrorist in same room as hostage
    sameRoom ← terrorists WHERE assignedRoom == hostageRoomId
    IF sameRoom is not empty THEN RETURN sameRoom[0]
    // Otherwise closest by distance
    RETURN terrorists sorted by Distance(position, hostagePos) [first]
END FUNCTION

END CLASS
```

---

## 5. NavigationContextBuilder Pseudocode

```
CLASS NavigationContextBuilder

FUNCTION BuildContexts(layout: Layout, assignments: List<RoleAssignment>,
                        spawns: SpawnPoints, entities: List<EntityRecord>,
                        rng: Random) 
    -> Dictionary<string, NavigationContext>

    contexts ← new Dictionary<string, NavigationContext>()

    FOR EACH assignment IN assignments:
        entity ← FindEntity(entities, assignment.entityId)
        room ← FindRoom(layout.rooms, assignment.assignedRoomId)
        contextId ← assignment.navigationContextId

        SWITCH assignment.role:
            "patrol":
                contexts[contextId] ← BuildPatrolContext(layout, room, entity, rng)
            "stationary_guard":
                contexts[contextId] ← BuildStationaryGuardContext(room, entity, rng)
            "roaming_guard":
                contexts[contextId] ← BuildRoamingGuardContext(layout, room, entity, rng)
            "hostage_guardian":
                hostageSpawn ← spawns.hostages[0]
                contexts[contextId] ← BuildHostageGuardianContext(room, entity,
                                                                    hostageSpawn, rng)

    RETURN contexts
END FUNCTION
```

### 5.1 Patrol Context Builder

```
FUNCTION BuildPatrolContext(layout, assignedRoom, entity, rng) -> NavigationContext
    // Build patrol route through assigned room + connected shallow rooms
    route ← [assignedRoom.id]
    visited ← Set containing assignedRoom.id

    // Extend route to adjacent rooms at same or lower depth
    candidates ← GetNeighbours(layout, assignedRoom.id)
                  WHERE depth <= assignedRoom.depth + 1
                  AND depth > 0    // never patrol through entry
    
    FOR EACH candidate IN candidates:
        IF candidate.id NOT IN visited THEN
            route.Add(candidate.id)
            visited.Add(candidate.id)
            IF route.Count >= 3 THEN BREAK   // max 3 rooms in patrol circuit

    // Add return to start for loop
    route.Add(assignedRoom.id)

    // Generate waypoints: room centres + door midpoints
    waypoints ← []
    FOR i ← 0 TO route.Count - 1:
        room ← FindRoom(layout.rooms, route[i])
        waypoints.Add(room.position)
        
        // Add door waypoint between consecutive rooms
        IF i < route.Count - 1 THEN
            nextRoomId ← route[i + 1]
            door ← FindDoorBetween(room, nextRoomId)
            IF door != null THEN
                waypoints.Add(door.position)

    RETURN NavigationContext {
        type = "patrol",
        patrolRoute = route,
        waypoints = waypoints,
        looping = true
    }
END FUNCTION
```

### 5.2 Stationary Guard Context Builder

```
FUNCTION BuildStationaryGuardContext(room, entity, rng) -> NavigationContext
    // Guard position: near door facing entry, or in corner
    // Uses entity's already-placed position from EntityPlacer
    guardPos ← entity.position

    // Facing direction: toward the primary (first) door
    IF room.doors.Count > 0 THEN
        primaryDoor ← room.doors[0]
        facingDir ← Normalize(primaryDoor.position - guardPos)
    ELSE
        facingDir ← Vector3(0, 0, -1)   // default: face south

    RETURN NavigationContext {
        type = "stationary_guard",
        guardRoomId = room.id,
        guardPosition = guardPos,
        facingDirection = facingDir
    }
END FUNCTION
```

### 5.3 Roaming Guard Context Builder

```
FUNCTION BuildRoamingGuardContext(layout, assignedRoom, entity, rng) -> NavigationContext
    // Roaming area: assigned room + all adjacent rooms (excluding entry)
    roamingIds ← [assignedRoom.id]
    neighbours ← GetNeighbours(layout, assignedRoom.id) WHERE depth > 0
    
    FOR EACH neighbour IN neighbours:
        roamingIds.Add(neighbour.id)
        IF roamingIds.Count >= 3 THEN BREAK   // max 3 rooms in roaming area

    // Waypoints: positions within each roaming room
    waypoints ← []
    FOR EACH roomId IN roamingIds:
        room ← FindRoom(layout.rooms, roomId)
        waypoints.Add(room.position)    // room centre as anchor

    // Add return waypoint to assigned room
    waypoints.Add(assignedRoom.position)

    RETURN NavigationContext {
        type = "roaming_guard",
        roamingRoomIds = roamingIds,
        waypoints = waypoints,
        anchorRoomId = assignedRoom.id
    }
END FUNCTION
```

### 5.4 Hostage Guardian Context Builder

```
FUNCTION BuildHostageGuardianContext(room, entity, hostageSpawn, rng) -> NavigationContext
    // Position: use entity's placed position (already near hostage from EntityPlacer)
    guardPos ← entity.position

    // Facing: toward the room's primary entry door
    IF room.doors.Count > 0 THEN
        primaryDoor ← room.doors[0]
        facingDir ← Normalize(primaryDoor.position - guardPos)
    ELSE
        facingDir ← Vector3(-1, 0, 0)   // default: face toward entry direction

    RETURN NavigationContext {
        type = "hostage_guardian",
        guardedEntityId = hostageSpawn.entityId,
        hostageRoomId = room.id,
        guardPosition = guardPos,
        facingDirection = facingDir
    }
END FUNCTION
```

---

## 6. Navigation Context Data Structures

These match the frozen Scenario.json schema exactly (Section 7 of the Scenario Output Schema Design).

```
NavigationContext (patrol) {
    type:         "patrol"
    patrolRoute:  List<string>     // ordered room IDs, e.g. ["room_02","room_01","room_02"]
    waypoints:    List<Vector3>    // world positions visited in order
    looping:      bool             // true = loop back to start; false = reverse at ends
}

NavigationContext (stationary_guard) {
    type:              "stationary_guard"
    guardRoomId:       string          // room where guard is stationed
    guardPosition:     Vector3         // exact position within room
    facingDirection:   Vector3         // direction guard faces
}

NavigationContext (roaming_guard) {
    type:              "roaming_guard"
    roamingRoomIds:    List<string>    // set of rooms defining roaming area
    waypoints:         List<Vector3>   // movement anchor positions
    anchorRoomId:      string          // primary room (returns here when idle)
}

NavigationContext (hostage_guardian) {
    type:              "hostage_guardian"
    guardedEntityId:   string          // entity ID of hostage
    hostageRoomId:     string          // room containing hostage
    guardPosition:     Vector3         // position near hostage
    facingDirection:   Vector3         // faces toward room entry door
}
```

---

## 7. Worked Example

**Input:** 6-room branching layout, 4 terrorists, difficulty = 3.

### 7.1 Layout with Depths

```
room_01 (Entry Hall)     depth=0  connections=[room_02, room_03]
room_02 (East Corridor)  depth=1  connections=[room_01, room_04]
room_03 (North Wing)     depth=1  connections=[room_01, room_05]
room_04 (Storage Room)   depth=2  connections=[room_02, room_06]
room_05 (North Dead End) depth=2  connections=[room_03]
room_06 (Back Office)    depth=3  connections=[room_04]  ← hostage here
```

Depths: [0, 1, 1, 2, 2, 3], maxDepth = 3.

### 7.2 Entity Positions (from EntityPlacer)

| Entity | Room | Position |
|--------|------|----------|
| hostage_01 | room_06 | (24.2, 0, 0.3) |
| terrorist_01 | room_02 | (9.5, 0, -1.8) |
| terrorist_02 | room_03 | (1.2, 0, 9.5) |
| terrorist_03 | room_04 | (17.0, 0, 1.5) |
| terrorist_04 | room_06 | (22.5, 0, -2.0) |

### 7.3 Step 1: Assign Hostage Guardian

- Hostage is in room_06
- Terrorists in room_06: terrorist_04 (same room)
- **terrorist_04 → hostage_guardian**, priority = 1
- Remaining terrorists: [terrorist_01, terrorist_02, terrorist_03]

### 7.4 Step 2: Role Quotas (difficulty = 3, remaining = 3)

- `GetRoleQuotas(3, 3)`:
  - patrolCount = ⌈3 × 0.33⌉ = 1
  - guardCount = ⌊3 × 0.33⌋ = 1
  - roamingCount = 3 - 1 - 1 = 1
- **Quotas: 1 patrol, 1 roaming_guard, 1 stationary_guard**

### 7.5 Step 3: Depth Zones

- maxDepth = 3
- shallowMax = ⌈3/3⌉ = 1 → patrol zone: depth ≤ 1
- deepMin = ⌈2×3/3⌉ = 2 → guard zone: depth ≥ 2
- mid zone: depth between 1 and 2 (exclusive) → **empty in this layout**

### 7.6 Step 4: Sort by Depth, Step 5: Assign

| Terrorist | Room | Depth | Zone Match | Assigned Role | Priority |
|-----------|------|-------|------------|---------------|----------|
| terrorist_01 | room_02 | 1 | shallow (≤1) | **patrol** | 3 |
| terrorist_02 | room_03 | 1 | shallow (≤1), but patrol quota filled | overflow → **roaming_guard** | 2 |
| terrorist_03 | room_04 | 2 | deep (≥2) | **stationary_guard** | 2 |
| terrorist_04 | room_06 | 3 | — (pre-assigned) | **hostage_guardian** | 1 |

### 7.7 Navigation Context Generation

**terrorist_01 (patrol):**
- Assigned room: room_02 (depth 1)
- Adjacent rooms at depth ≤ 2: room_01 (depth 0 → skip, it's entry), room_04 (depth 2)
- Route: `["room_02", "room_04", "room_02"]` (patrol into storage and back)
- Waypoints: room_02 centre → door_02_04 → room_04 centre → door_02_04 → room_02 centre

```json
{
  "type": "patrol",
  "patrolRoute": ["room_02", "room_04", "room_02"],
  "waypoints": [
    {"x": 9.5, "y": 0, "z": -1.8},
    {"x": 12.0, "y": 0, "z": 0.0},
    {"x": 16.0, "y": 0, "z": 0.0},
    {"x": 12.0, "y": 0, "z": 0.0},
    {"x": 9.5, "y": 0, "z": -1.8}
  ],
  "looping": true
}
```

**terrorist_02 (roaming_guard):**
- Assigned room: room_03 (depth 1)
- Adjacent non-entry rooms: room_05 (depth 2)
- Roaming area: `["room_03", "room_05"]`
- Waypoints: room_03 centre → room_05 centre → room_03 centre

```json
{
  "type": "roaming_guard",
  "roamingRoomIds": ["room_03", "room_05"],
  "waypoints": [
    {"x": 1.2, "y": 0, "z": 9.5},
    {"x": 0.0, "y": 0, "z": 16.0},
    {"x": 1.2, "y": 0, "z": 9.5}
  ],
  "anchorRoomId": "room_03"
}
```

**terrorist_03 (stationary_guard):**
- Assigned room: room_04 (depth 2)
- Guard position: entity's placed position (17.0, 0, 1.5)
- Facing: toward door_04_02 (west wall) = direction (-1, 0, 0)

```json
{
  "type": "stationary_guard",
  "guardRoomId": "room_04",
  "guardPosition": {"x": 17.0, "y": 0, "z": 1.5},
  "facingDirection": {"x": -1.0, "y": 0, "z": 0.0}
}
```

**terrorist_04 (hostage_guardian):**
- Guards hostage_01 in room_06
- Guard position: entity's placed position (22.5, 0, -2.0)
- Facing: toward door_06_04 (west wall) = direction (-1, 0, 0)

```json
{
  "type": "hostage_guardian",
  "guardedEntityId": "hostage_01",
  "hostageRoomId": "room_06",
  "guardPosition": {"x": 22.5, "y": 0, "z": -2.0},
  "facingDirection": {"x": -1.0, "y": 0, "z": 0.0}
}
```

### 7.8 Final Role Assignments Array

```json
[
  { "entityId": "terrorist_01", "role": "patrol",           "priorityLevel": 3, "assignedRoomId": "room_02", "navigationContextId": "nav_terrorist_01" },
  { "entityId": "terrorist_02", "role": "roaming_guard",    "priorityLevel": 2, "assignedRoomId": "room_03", "navigationContextId": "nav_terrorist_02" },
  { "entityId": "terrorist_03", "role": "stationary_guard", "priorityLevel": 2, "assignedRoomId": "room_04", "navigationContextId": "nav_terrorist_03" },
  { "entityId": "terrorist_04", "role": "hostage_guardian", "priorityLevel": 1, "assignedRoomId": "room_06", "navigationContextId": "nav_terrorist_04" }
]
```

### 7.9 Verification

- All 4 terrorists have role assignments ✓
- All 4 have navigation context entries ✓
- hostage_guardian exists for the single hostage ✓
- Roles are spatially derived: patrol at depth 1, roaming at depth 1–2, guard at depth 2, guardian at depth 3 ✓
- Priority levels: guardian=1 (highest), guards/roaming=2, patrol=3 (lowest) ✓
- Navigation context references only existing room IDs ✓
- All data traceable to evaluator parameters (difficulty=3 drove balanced quotas, spatial structure drove zone assignment) per [22] ✓

---

## 8. How Difficulty Affects Role Assignment

| Aspect | Difficulty 1–2 | Difficulty 3 | Difficulty 4–5 |
|--------|----------------|--------------|----------------|
| Patrol ratio | ~50% of NPCs | ~33% | ~15% (minimum 1) |
| Stationary guard ratio | ~15% | ~33% | ~50% |
| Roaming guard ratio | ~35% | ~33% | ~35% |
| Tactical effect | More mobile, visible threats; easier to detect | Balanced encounter types | More ambush positions; fewer predictable patrols |
| Trainee experience | Gradual difficulty; encounters are telegraphed | Standard tactical challenge | High stress; threats from unexpected positions per [3] |

---

## 9. Traceability Chain (Evaluator Parameter → Role Assignment)

Following [22]'s requirement that generated content is traceable:

```
evaluator sets difficultyLevel = 4
  → GetRoleQuotas selects high-guard distribution (50% stationary)
    → terrorists in deep rooms assigned stationary_guard
      → BuildStationaryGuardContext places them facing doors
        → Module 2 reads facingDirection and drives ambush behaviour
          → Module 4 logs "stationary_guard at room_04, facing west"
            → evaluator can verify: difficulty=4 produced expected guard placement ✓
```

Every field in `roleAssignments` and `navigationContext` can be traced back through this chain to the evaluator's original ScenarioConfig parameters, satisfying the traceability requirement.

---

## 10. Design Decisions Summary with Literature References

| Decision | Justification |
|----------|---------------|
| BFS depth determines role zones (shallow=patrol, deep=guard) | [18]'s path DAG and resistor network analog for flow direction |
| Roles are spatially derived, not random | Core of RQ2; [11]'s Space Syntax (deep=private/guarded, shallow=public/patrol) |
| Difficulty modulates role distribution ratios | [20]'s expected challenge density parameter M |
| hostage_guardian is mandatory (highest priority) | [16]'s distance constraint — hostage must have proximate threat |
| Navigation context is traceable to evaluator parameters | [22]'s automated evaluation framework requirement |
| Guard faces primary door | [3]'s finding that stranger entering room = highest stress trigger |
| Patrol covers 2–3 rooms in a loop | [5]'s temporal/spatial clustering — mobile threats create uncertainty |

---

*End of NPC Role Assignment & Navigation Context Algorithm Design | Phase 1, Prompt 1.5 | Module 1 – Dynamic Scenario Generation*
