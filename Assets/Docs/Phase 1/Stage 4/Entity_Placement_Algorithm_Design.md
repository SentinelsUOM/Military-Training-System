# Entity Placement Algorithm Design

**Phase 1 – Prompt 1.4 Deliverable**
**Module 1 – Dynamic Scenario Generation**

Team Sentinels | University of Moratuwa | April 2026
VR-Based Hostage-Rescue Training System | Unity 2022.3 LTS | Meta Quest 3

---

## 1. Overview

This document defines the entity placement algorithm for Module 1. The algorithm positions the trainee, hostage, and terrorists within the generated layout while respecting placement strategy, hostage-risk level distance constraints, and difficulty-aware positioning.

**Scope alignment:** hostageCount is fixed to 1 (per ScenarioConfig v1.0.0). The algorithm is designed to support 1–5 hostages for future expansion. The worked example uses 1 hostage and 4 terrorists in a 6-room branching layout.

**Literature-backed design decisions:**

- **Spatial proximity clustering** — not just individual positions. [5] showed spatial clustering creates compounding tactical effects.
- **Distance constraints between hostages and terrorists** — high-risk = small max distance, low-risk = large min distance. [16]'s WFC extension.
- **Difficulty-aware surprising positions** — behind doors, sightline intersections. [3]'s stressor rankings (stranger entering a room = highest stress, mean 41.45).
- **Entity placement respects room structure** — upper-layer elements restricted to valid lower-layer positions. [16]'s multi-layer model.
- **Hierarchical placement order** — layout first, then major entities, then fine-grained adjustments. [14]'s three-stage optimisation.

---

## 2. Constants and Configuration

```
CONSTANTS:
    WALL_MARGIN       = 0.8m    // minimum distance from entity centre to any wall
    MIN_CLEARANCE     = 1.5m    // minimum distance between any two entities
    DOOR_OFFSET       = 1.2m    // offset from door position for "behind door" placement
    MAX_PLACEMENT_ATTEMPTS = 100  // retries per entity before relaxing constraints
```

### 2.1 Room Size Placement Bounds

For a room at position (cx, cz) with size (w, d):
```
validMinX = cx - w/2 + WALL_MARGIN
validMaxX = cx + w/2 - WALL_MARGIN
validMinZ = cz - d/2 + WALL_MARGIN
validMaxZ = cz + d/2 - WALL_MARGIN
```

---

## 3. Placement Strategy Definitions

Each strategy defines how entities are distributed across rooms based on BFS depth. The strategy selects **which rooms** to use; fine-grained positioning within rooms is handled separately.

### 3.1 Room Selection by Strategy

| Strategy | Hostage Room Selection | Terrorist Room Selection | Tactical Character |
|---|---|---|---|
| **clustered** | Deepest dead-end room | Same room as hostage + adjacent rooms (depth ≥ maxDepth-1) | All entities concentrated together. Creates compounding tactical pressure per [5]. Forces trainee to handle multiple threats simultaneously. |
| **dispersed** | Deepest dead-end room | Spread across all depth levels (one per unique depth where possible) | Entities distributed evenly. Trainee encounters threats throughout the layout. Maximum spatial coverage. |
| **front_loaded** | Mid-depth room (depth ≈ maxDepth/2) | Shallow rooms (depth ≤ maxDepth/2), concentrated near entry | Threats encountered early. Hostage is deeper but terrorists guard the approach. Early-engagement pressure. |
| **deep** | Deepest dead-end room | Deep rooms (depth ≥ maxDepth/2), concentrated far from entry | All entities far from entry. Trainee must traverse empty rooms before encountering any NPC. Builds tension through emptiness. |

### 3.2 Formal Room Selection Logic

```
FUNCTION SelectHostageRoom(rooms, strategy, rng) -> RoomNode
    maxDepth ← MAX(room.depth FOR room IN rooms)
    
    SWITCH strategy:
        "clustered":
            candidates ← rooms WHERE depth == maxDepth AND connectedRoomIds.Count == 1
            IF candidates is empty THEN candidates ← rooms WHERE depth == maxDepth
        "dispersed":
            candidates ← rooms WHERE depth == maxDepth AND connectedRoomIds.Count == 1
            IF candidates is empty THEN candidates ← rooms WHERE depth == maxDepth
        "front_loaded":
            targetDepth ← CEIL(maxDepth / 2)
            candidates ← rooms WHERE depth == targetDepth
        "deep":
            candidates ← rooms WHERE depth == maxDepth AND connectedRoomIds.Count == 1
            IF candidates is empty THEN candidates ← rooms WHERE depth == maxDepth

    RETURN candidates[rng.Next(candidates.Count)]
END FUNCTION

FUNCTION SelectTerroristRooms(rooms, strategy, terroristCount, hostageRoomId, rng) -> List<RoomNode>
    maxDepth ← MAX(room.depth FOR room IN rooms)
    availableRooms ← rooms WHERE depth > 0   // never place in entry room
    selectedRooms ← []

    SWITCH strategy:
        "clustered":
            // Prefer hostage room + adjacent rooms [5]
            hostageRoom ← FindRoom(rooms, hostageRoomId)
            clusterRooms ← [hostageRoom] + GetAdjacentRooms(rooms, hostageRoomId)
            clusterRooms ← clusterRooms filtered to depth > 0
            FOR i ← 0 TO terroristCount - 1:
                selectedRooms.Add(clusterRooms[i % clusterRooms.Count])

        "dispersed":
            // One terrorist per unique depth level, then cycle
            depthBuckets ← GroupByDepth(availableRooms)  // {depth → [rooms]}
            sortedDepths ← depthBuckets.Keys sorted ascending
            depthIndex ← 0
            FOR i ← 0 TO terroristCount - 1:
                depth ← sortedDepths[depthIndex % sortedDepths.Count]
                bucket ← depthBuckets[depth]
                room ← bucket[rng.Next(bucket.Count)]
                selectedRooms.Add(room)
                depthIndex ← depthIndex + 1

        "front_loaded":
            // Shallow rooms only (depth ≤ half max)
            shallowRooms ← availableRooms WHERE depth <= CEIL(maxDepth / 2)
            FOR i ← 0 TO terroristCount - 1:
                selectedRooms.Add(shallowRooms[rng.Next(shallowRooms.Count)])

        "deep":
            // Deep rooms only (depth ≥ half max)
            deepRooms ← availableRooms WHERE depth >= FLOOR(maxDepth / 2)
            FOR i ← 0 TO terroristCount - 1:
                selectedRooms.Add(deepRooms[rng.Next(deepRooms.Count)])

    RETURN selectedRooms
END FUNCTION
```

---

## 4. Hostage-Risk Level Distance Constraints

Following [16]'s distance constraint mechanism. These define the allowed distance range between the hostage and each terrorist, measured as Euclidean distance between world positions.

### 4.1 Distance Constraint Formulas

| Risk Level | Min Distance (hostage↔terrorist) | Max Distance (hostage↔terrorist) | Semantic Effect |
|---|---|---|---|
| **high** | 0.0m (same room allowed) | `roomWidth × 1.5` (e.g., 9.0m for medium rooms) | Hostage closely guarded. At least one terrorist must be within max distance. Creates immediate threat to hostage if trainee delays. |
| **medium** | `roomWidth × 0.5` (e.g., 3.0m) | `roomWidth × 3.0` (e.g., 18.0m) | Moderate spacing. Terrorists nearby but not in hostage's face. Standard tactical scenario. |
| **low** | `roomWidth × 2.0` (e.g., 12.0m) | No maximum | Hostage separated from threats. Terrorists and hostage are in different areas of the building. Easier rescue approach. |

### 4.2 Constraint Application

```
FUNCTION GetDistanceConstraints(riskLevel, roomWidth) -> (float minDist, float maxDist)
    SWITCH riskLevel:
        "high":
            RETURN (0.0, roomWidth * 1.5)
        "medium":
            RETURN (roomWidth * 0.5, roomWidth * 3.0)
        "low":
            RETURN (roomWidth * 2.0, float.MaxValue)
END FUNCTION

FUNCTION SatisfiesRiskConstraint(terroristPos, hostagePos, minDist, maxDist) -> bool
    dist ← EuclideanDistance(terroristPos, hostagePos)
    RETURN dist >= minDist AND dist <= maxDist
END FUNCTION
```

**Constraint enforcement priority:**
1. At least one terrorist MUST satisfy the distance constraint (hard constraint)
2. Remaining terrorists SHOULD satisfy the constraint (soft constraint, relaxed after MAX_PLACEMENT_ATTEMPTS/2)
3. All terrorists must be within valid room boundaries (hard constraint)
4. No two entities may overlap within MIN_CLEARANCE (hard constraint)

---

## 5. Difficulty-Aware Position Selection

Following [3]'s finding that a stranger suddenly entering a room ranked highest for stress (mean 41.45). Higher difficulty levels place NPCs in positions that are harder to detect.

### 5.1 Position Selection Zones Within a Room

```
FUNCTION SelectPositionInRoom(room, difficultyLevel, rng, existingPositions) -> Vector3
    // Define placement zones based on difficulty [3]
    zones ← GetPlacementZones(room, difficultyLevel)
    
    FOR attempt ← 0 TO MAX_PLACEMENT_ATTEMPTS:
        zone ← zones[attempt % zones.Count]
        candidate ← GeneratePositionInZone(room, zone, rng)
        
        IF IsWithinBounds(candidate, room) 
           AND HasClearance(candidate, existingPositions, MIN_CLEARANCE) THEN
            RETURN candidate
    
    // Fallback: room centre (always valid)
    RETURN room.position
END FUNCTION

FUNCTION GetPlacementZones(room, difficultyLevel) -> List<Zone>
    // Difficulty 1-2: open positions (room centre, visible areas)
    // Difficulty 3:   mixed positions
    // Difficulty 4-5: tactical positions (behind doors, corners, sightline intersections)
    
    zones ← []
    
    IF difficultyLevel <= 2 THEN
        zones.Add(ZONE_CENTRE)          // centre of room — easy to spot
        zones.Add(ZONE_OPEN_AREA)       // open area away from walls
    
    IF difficultyLevel == 3 THEN
        zones.Add(ZONE_CENTRE)
        zones.Add(ZONE_NEAR_WALL)       // near a wall but visible
        zones.Add(ZONE_CORNER)          // in a corner

    IF difficultyLevel >= 4 THEN
        zones.Add(ZONE_BEHIND_DOOR)     // behind a door [3] — highest stress trigger
        zones.Add(ZONE_CORNER)          // corner with limited sightline
        zones.Add(ZONE_SIGHTLINE_BLIND) // blind spot from primary door entry

    RETURN zones
END FUNCTION
```

### 5.2 Zone Position Generation

```
FUNCTION GeneratePositionInZone(room, zone, rng) -> Vector3
    cx ← room.position.x
    cz ← room.position.z
    hw ← room.size.width / 2 - WALL_MARGIN    // half-width within margin
    hd ← room.size.depth / 2 - WALL_MARGIN    // half-depth within margin

    SWITCH zone:
        ZONE_CENTRE:
            jitter ← 0.5
            RETURN Vector3(cx + RandRange(-jitter, jitter, rng),
                           0, cz + RandRange(-jitter, jitter, rng))

        ZONE_OPEN_AREA:
            RETURN Vector3(cx + RandRange(-hw * 0.5, hw * 0.5, rng),
                           0, cz + RandRange(-hd * 0.5, hd * 0.5, rng))

        ZONE_NEAR_WALL:
            wall ← rng.Next(4)  // 0=north, 1=south, 2=east, 3=west
            IF wall == 0 THEN RETURN Vector3(cx + RandRange(-hw*0.5, hw*0.5, rng), 0, cz + hd)
            IF wall == 1 THEN RETURN Vector3(cx + RandRange(-hw*0.5, hw*0.5, rng), 0, cz - hd)
            IF wall == 2 THEN RETURN Vector3(cx + hw, 0, cz + RandRange(-hd*0.5, hd*0.5, rng))
            IF wall == 3 THEN RETURN Vector3(cx - hw, 0, cz + RandRange(-hd*0.5, hd*0.5, rng))

        ZONE_CORNER:
            cornerX ← (rng.Next(2) == 0) ? (cx - hw) : (cx + hw)
            cornerZ ← (rng.Next(2) == 0) ? (cz - hd) : (cz + hd)
            RETURN Vector3(cornerX, 0, cornerZ)

        ZONE_BEHIND_DOOR:
            IF room.doors.Count > 0 THEN
                door ← room.doors[rng.Next(room.doors.Count)]
                // Position behind the door swing (offset perpendicular to wall)
                offset ← GetBehindDoorOffset(door.wallSide, DOOR_OFFSET)
                RETURN Vector3(door.position.x + offset.x, 0, door.position.z + offset.z)
            ELSE
                RETURN GeneratePositionInZone(room, ZONE_CORNER, rng)

        ZONE_SIGHTLINE_BLIND:
            // Position that is NOT visible from the primary entry door
            IF room.doors.Count > 0 THEN
                primaryDoor ← room.doors[0]
                // Place on the same wall as the door but offset to the side
                RETURN GetBlindSpotPosition(room, primaryDoor, rng)
            ELSE
                RETURN GeneratePositionInZone(room, ZONE_CORNER, rng)
END FUNCTION
```

---

## 6. Complete EntityPlacer Pseudocode

```
CLASS EntityPlacer

FUNCTION PlaceEntities(layout: Layout, config: ScenarioConfig, rng: Random)
    -> (SpawnPoints, List<EntityRecord>)

    allPositions ← []   // tracks all placed positions for clearance checks
    entities ← []
    strategy ← config.entityConfiguration.placementStrategy
    riskLevel ← config.entityConfiguration.hostageRiskLevel
    difficulty ← config.executionControls.difficultyLevel
    roomWidth ← GetRoomWidth(config.missionStructure.roomSize)

    // ══════════════════════════════════════════════
    // STAGE 1: Place Trainee at Entry Room
    // (Hierarchical order: trainee first [14])
    // ══════════════════════════════════════════════
    entryRoom ← layout.rooms WHERE depth == 0 [first]
    traineePos ← Vector3(entryRoom.position.x, 0, entryRoom.position.z)
    traineeFacing ← CalculateFacingIntoRoom(entryRoom)
    allPositions.Add(traineePos)

    traineeEntity ← EntityRecord {
        id = "trainee_01", type = "trainee",
        assignedRoom = entryRoom.id, position = traineePos,
        metadata = {}
    }
    entities.Add(traineeEntity)

    // ══════════════════════════════════════════════
    // STAGE 2: Place Hostage(s) by Strategy
    // (Major entities before fine-grained [14])
    // ══════════════════════════════════════════════
    hostageRoom ← SelectHostageRoom(layout.rooms, strategy, rng)
    hostagePos ← SelectPositionInRoom(hostageRoom, 1, rng, allPositions)
        // Hostage always at difficulty=1 zone (centre/open — visible, not hidden)
    hostageFacing ← RandomFacingDirection(rng)
    allPositions.Add(hostagePos)

    hostageEntity ← EntityRecord {
        id = "hostage_01", type = "hostage",
        assignedRoom = hostageRoom.id, position = hostagePos,
        metadata = { "initialState": "calm" }
    }
    entities.Add(hostageEntity)

    hostageSpawns ← [{ entityId="hostage_01", roomId=hostageRoom.id,
                        position=hostagePos, facingDirection=hostageFacing }]

    // ══════════════════════════════════════════════
    // STAGE 3: Place Terrorists by Strategy + Risk Constraints
    // ══════════════════════════════════════════════
    terroristCount ← config.entityConfiguration.terroristCount
    terroristRooms ← SelectTerroristRooms(layout.rooms, strategy,
                                           terroristCount, hostageRoom.id, rng)
    (minDist, maxDist) ← GetDistanceConstraints(riskLevel, roomWidth)

    terroristSpawns ← []
    riskSatisfied ← false    // at least one terrorist must satisfy risk constraint

    FOR i ← 0 TO terroristCount - 1:
        room ← terroristRooms[i]
        entityId ← "terrorist_" + PadZero(i + 1)
        
        placed ← false
        FOR attempt ← 0 TO MAX_PLACEMENT_ATTEMPTS:
            candidatePos ← SelectPositionInRoom(room, difficulty, rng, allPositions)

            // Check clearance against all existing positions
            IF NOT HasClearance(candidatePos, allPositions, MIN_CLEARANCE) THEN
                CONTINUE

            // Check risk-level distance constraint to hostage
            satisfiesRisk ← SatisfiesRiskConstraint(candidatePos, hostagePos,
                                                     minDist, maxDist)

            // First terrorist with risk satisfaction is mandatory
            IF NOT riskSatisfied AND NOT satisfiesRisk THEN
                // For the first N/2 attempts, enforce strictly
                IF attempt < MAX_PLACEMENT_ATTEMPTS / 2 THEN CONTINUE
                // After half attempts, relax for non-first terrorists

            IF satisfiesRisk THEN riskSatisfied ← true

            // Position accepted
            allPositions.Add(candidatePos)
            facing ← CalculateFacingTowardDoor(room, rng)

            entities.Add(EntityRecord {
                id = entityId, type = "terrorist",
                assignedRoom = room.id, position = candidatePos,
                metadata = { "initialState": "idle" }
            })
            terroristSpawns.Add({
                entityId = entityId, roomId = room.id,
                position = candidatePos, facingDirection = facing
            })
            placed ← true
            BREAK

        // Fallback: if placement failed after all attempts
        IF NOT placed THEN
            fallbackPos ← room.position  // room centre
            allPositions.Add(fallbackPos)
            entities.Add(EntityRecord {
                id = entityId, type = "terrorist",
                assignedRoom = room.id, position = fallbackPos,
                metadata = { "initialState": "idle" }
            })
            terroristSpawns.Add({
                entityId = entityId, roomId = room.id,
                position = fallbackPos,
                facingDirection = CalculateFacingTowardDoor(room, rng)
            })

    // ══════════════════════════════════════════════
    // STAGE 4: Assemble SpawnPoints
    // ══════════════════════════════════════════════
    spawnPoints ← SpawnPoints {
        trainee = { roomId=entryRoom.id, position=traineePos,
                    facingDirection=traineeFacing },
        hostages = hostageSpawns,
        terrorists = terroristSpawns
    }

    RETURN (spawnPoints, entities)
END FUNCTION

END CLASS
```

---

## 7. Helper Functions

### 7.1 Clearance Check

```
FUNCTION HasClearance(candidate: Vector3, existing: List<Vector3>, minDist: float) -> bool
    FOR EACH pos IN existing:
        IF EuclideanDistance(candidate, pos) < minDist THEN
            RETURN false
    RETURN true
END FUNCTION
```

### 7.2 Bounds Check

```
FUNCTION IsWithinBounds(pos: Vector3, room: RoomNode) -> bool
    halfW ← room.size.width / 2 - WALL_MARGIN
    halfD ← room.size.depth / 2 - WALL_MARGIN
    RETURN ABS(pos.x - room.position.x) <= halfW
       AND ABS(pos.z - room.position.z) <= halfD
END FUNCTION
```

### 7.3 Behind-Door Offset

```
FUNCTION GetBehindDoorOffset(wallSide: string, offset: float) -> Vector2
    // Returns offset that places entity behind the door swing
    SWITCH wallSide:
        "north" → RETURN (offset, -offset)   // behind door on east side
        "south" → RETURN (-offset, offset)
        "east"  → RETURN (-offset, -offset)
        "west"  → RETURN (offset, offset)
END FUNCTION
```

### 7.4 Facing Direction Toward Door

```
FUNCTION CalculateFacingTowardDoor(room: RoomNode, rng: Random) -> Vector3
    IF room.doors.Count == 0 THEN
        RETURN RandomFacingDirection(rng)
    
    // Face toward the first (primary) door
    door ← room.doors[0]
    direction ← Normalize(door.position - room.position)
    RETURN direction
END FUNCTION
```

---

## 8. Worked Example

**Input:** 6-room branching layout, 1 hostage, 4 terrorists, `dispersed` strategy, `medium` risk level, difficulty = 3, medium rooms (6×6m).

### 8.1 Layout (from Prompt 1.3 algorithm, extended to 6 rooms)

```
room_01 (Entry Hall)     depth=0  pos=(0,0,0)
room_02 (East Corridor)  depth=1  pos=(8,0,0)     connected to room_01
room_03 (North Wing)     depth=1  pos=(0,0,8)     connected to room_01
room_04 (Storage Room)   depth=2  pos=(16,0,0)    connected to room_02
room_05 (North Dead End) depth=2  pos=(0,0,16)    connected to room_03
room_06 (Back Office)    depth=3  pos=(24,0,0)    connected to room_04  ← deepest dead-end
```

### 8.2 Stage 1: Trainee Placement

- Entry room = room_01 at (0, 0, 0)
- Trainee position = (0, 0, 0) — room centre
- Facing direction = (1, 0, 0) — facing east into the building
- allPositions = [(0, 0, 0)]

### 8.3 Stage 2: Hostage Placement

- Strategy = dispersed → hostage in deepest dead-end
- room_06 (depth=3, 1 connection) selected
- Hostage placed at difficulty=1 zone (ZONE_CENTRE): (24.2, 0, 0.3) — slight jitter from centre
- allPositions = [(0,0,0), (24.2,0,0.3)]

### 8.4 Stage 3: Terrorist Placement

**Distance constraints (medium risk, roomWidth=6.0m):**
- minDist = 6.0 × 0.5 = 3.0m
- maxDist = 6.0 × 3.0 = 18.0m

**Room selection (dispersed strategy):**
Distribute across depth levels. Available depths: {1, 2, 3}. Cycle through:
- terrorist_01 → depth 1 → room_02 (East Corridor)
- terrorist_02 → depth 2 → room_04 (Storage Room)
- terrorist_03 → depth 3 → room_06 (Back Office) — same room as hostage
- terrorist_04 → depth 1 → room_03 (North Wing)

**Placement with difficulty=3 zones (ZONE_CENTRE, ZONE_NEAR_WALL, ZONE_CORNER):**

| Entity | Room | Position | Dist to Hostage | Risk OK? | Placement Zone |
|--------|------|----------|-----------------|----------|----------------|
| terrorist_01 | room_02 | (9.5, 0, -1.8) | 18.3m | ✗ (>18m, marginal) | ZONE_NEAR_WALL |
| terrorist_02 | room_04 | (17.0, 0, 1.5) | 7.4m | ✓ (3-18m) | ZONE_CENTRE |
| terrorist_03 | room_06 | (22.5, 0, -1.2) | 2.2m | ✗ (<3m min) → retry | ZONE_CORNER |
| | | (22.0, 0, 2.0) | 2.7m | ✗ → retry | ZONE_NEAR_WALL |
| | | (25.8, 0, -2.2) | 2.9m | ✗ → retry | ZONE_CORNER |
| | | (26.0, 0, 2.5) | 2.7m | ✗ → after 50 attempts, relax (riskSatisfied=true from terrorist_02) | ZONE_CENTRE |
| | | (22.5, 0, -2.0) | placed at (22.5,0,-2.0) with relaxed constraint | 2.7m | ZONE_CORNER |
| terrorist_04 | room_03 | (1.2, 0, 9.5) | 25.6m | ✗ (>18m) → relaxed (riskSatisfied=true) | ZONE_CENTRE |

**Risk constraint summary:**
- terrorist_02 satisfies medium risk (7.4m is within 3.0–18.0m range) ✓
- At least one terrorist satisfies the constraint → riskSatisfied = true
- Remaining terrorists placed with relaxed constraint (distance only enforced as soft preference)

### 8.5 Final Entity List

| Entity | Type | Room | Position | Initial State |
|--------|------|------|----------|---------------|
| trainee_01 | trainee | room_01 | (0.0, 0, 0.0) | — |
| hostage_01 | hostage | room_06 | (24.2, 0, 0.3) | calm |
| terrorist_01 | terrorist | room_02 | (9.5, 0, -1.8) | idle |
| terrorist_02 | terrorist | room_04 | (17.0, 0, 1.5) | idle |
| terrorist_03 | terrorist | room_06 | (22.5, 0, -2.0) | idle |
| terrorist_04 | terrorist | room_03 | (1.2, 0, 9.5) | idle |

### 8.6 Spatial Distribution Verification

- **Dispersed strategy check:** Terrorists span depths 1, 2, 3, 1 — all non-entry depths covered ✓
- **No overlap:** All pairwise distances > 1.5m ✓
- **All within bounds:** All positions respect 0.8m wall margin ✓
- **Risk constraint:** At least one terrorist (terrorist_02) within 3.0–18.0m of hostage ✓
- **Hostage reachable:** BFS path exists trainee→room_01→room_02→room_04→room_06 ✓

---

## 9. Strategy Comparison Summary

How the same 6-room layout with 4 terrorists would differ across strategies:

| Aspect | Clustered | Dispersed | Front-Loaded | Deep |
|--------|-----------|-----------|--------------|------|
| Hostage room | room_06 (depth 3) | room_06 (depth 3) | room_04 (depth 2) | room_06 (depth 3) |
| Terrorist rooms | room_06, room_06, room_04, room_04 | room_02, room_04, room_06, room_03 | room_02, room_03, room_02, room_03 | room_04, room_05, room_06, room_06 |
| Depth spread | 2–3 (narrow) | 1–3 (full range) | 1 (shallow only) | 2–3 (deep only) |
| Empty rooms | 3 (rooms 01,02,03) | 1 (room 05) | 3 (rooms 04,05,06) | 2 (rooms 01,02) |
| First contact depth | 2 | 1 | 1 | 2 |
| Tactical feel | Concentrated ambush | Progressive encounters | Early gauntlet | Deep fortress |

---

## 10. Design Decisions Summary with Literature References

| Decision | Justification |
|----------|---------------|
| Proximity clustering in "clustered" strategy | [5]'s finding that spatial proximity creates compounding tactical effects |
| Distance constraints for hostage-risk level | [16]'s WFC extension with distance constraints between element types |
| Behind-door and sightline-blind placement at high difficulty | [3]'s stressor rankings — stranger entering room = highest stress (mean 41.45) |
| Entity positions restricted to valid room bounds | [16]'s multi-layer model — upper-layer elements on valid lower-layer positions |
| Hierarchical placement order: trainee → hostage → terrorists | [14]'s three-stage optimisation: large objects, then medium, then small |
| Retry logic with constraint relaxation | [8]'s parameter bounding — constrain to valid ranges to ensure plausibility by construction |
| At least one terrorist satisfying risk constraint (hard) | [18]'s cardinality constraint mechanism for entity count parameters |
| Single hostage in deepest dead-end for most strategies | [11]'s Space Syntax: private rooms at deep nodes of accessibility graph |

---

*End of Entity Placement Algorithm Design | Phase 1, Prompt 1.4 | Module 1 – Dynamic Scenario Generation*
