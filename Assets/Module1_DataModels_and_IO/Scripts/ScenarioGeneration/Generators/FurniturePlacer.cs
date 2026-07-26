// =============================================================================
// FurniturePlacer.cs
// Module 1 – Dynamic Scenario Generation
// Team Sentinels | University of Moratuwa | 2026
//
// Populates each room with believable, seed-reproducible furniture. Runs as the
// final generation stage (after entities/roles/navigation) so it can read every
// actor position and guarantee that no item overlaps an entity, a doorway, a
// wall, or another item. Items are wall-anchored (furniture lines walls in real
// rooms) and their footprints are scaled to the room so a 4×4 store-room and an
// 8×8 hall both read at a realistic human scale. The open centre of the room is
// always preserved so the NavMesh stays traversable and the trainee/NPCs can
// cross every room and reach every doorway.
//
// Items are also GROUPED the way real rooms are furnished: chairs are never
// scattered along walls on their own — they spawn tucked in front of the desk
// or table they belong to, turned to face it; beds and sofas get a nightstand
// seated beside them against the same wall. Companions pass the exact same
// rejection checks (room bounds, door keep-outs, entity clearance, item gaps,
// floor-coverage cap) as primary items, so all navigability guarantees hold.
//
// Design notes:
//   * All randomness flows through the caller-supplied System.Random for full
//     seed reproducibility [22] — same seed ⇒ identical furniture.
//   * Placement is pure data: this class never touches Unity scene objects. The
//     resulting FurnitureData is serialised into Scenario.json and realised by
//     SceneBuilder (greybox boxes, or mapped prefabs, scaled to FurnitureData.size).
//   * Footprints are AABBs. Wall-anchored items only ever rotate by 0/90/180/270°,
//     so an item's world-space footprint is its local footprint (optionally with
//     width/depth swapped), keeping the overlap maths exact and cheap.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TeamSentinels.ScenarioGeneration.DataModels;

namespace TeamSentinels.ScenarioGeneration.Generators
{
    /// <summary>
    /// Places furniture inside the generated rooms. Plain C# class with no
    /// <see cref="MonoBehaviour"/> dependency; all randomness flows through the
    /// caller-supplied <see cref="System.Random"/> for seed reproducibility.
    /// Mutates each <see cref="RoomData.furniture"/> list in place.
    /// </summary>
    public class FurniturePlacer
    {
        // ── Geometry constants (metres) ──────────────────────────────────────

        /// <summary>Gap Module 1 leaves between adjacent room footprints. Must
        /// match <c>LayoutGenerator.CorridorGap</c>.</summary>
        private const float CorridorGap = 2.0f;

        /// <summary>Half of SceneBuilder's 0.12 m procedural wall thickness.</summary>
        private const float WallHalfThickness = 0.06f;

        /// <summary>
        /// How far the room's REAL interior extends beyond its nominal half-size
        /// on every side. Rooms are laid out <see cref="CorridorGap"/> apart and
        /// SceneBuilder builds each wall centred on the boundary plane halfway
        /// into that gap, with the floor slab extended to meet it. Anchoring
        /// furniture to THIS extent puts backs flush against the visible walls
        /// instead of floating a metre inside the room.
        /// </summary>
        private const float InteriorOutset = CorridorGap * 0.5f - WallHalfThickness;

        /// <summary>Gap left between an item's back and the wall it lines.</summary>
        private const float WallGap = 0.05f;

        /// <summary>Clear radius kept around every doorway / entry opening so the
        /// door swing and the NavMesh link across it are never blocked.</summary>
        private const float DoorKeepout = 1.35f;

        /// <summary>Minimum gap kept between an item's footprint and any entity
        /// (trainee, hostage, terrorist) so no actor spawns inside furniture.</summary>
        private const float EntityClearance = 0.6f;

        /// <summary>Minimum gap kept between two pieces of furniture.</summary>
        private const float FurnitureGap = 0.15f;

        /// <summary>Attempts to seat one item before giving up on it.</summary>
        private const int MaxAttemptsPerItem = 40;

        /// <summary>
        /// Never let furniture footprints consume more than this fraction of a
        /// room's floor area. A hard guard on navigability on top of the
        /// wall-anchoring and open-centre rules.
        /// </summary>
        private const float MaxFloorCoverage = 0.5f;

        // ── Furniture catalogue ──────────────────────────────────────────────

        /// <summary>
        /// Canonical real-world proportions for a furniture type, in metres. Width
        /// runs along the item's local X, depth along local Z (the axis that faces
        /// into the room when wall-anchored), height along Y. <c>minRoomDim</c> is
        /// the smallest room side (width or depth) the item is allowed in, so bulky
        /// items (bed, sofa, table) never crowd a tiny store-room.
        /// </summary>
        private readonly struct FurnitureSpec
        {
            public readonly float width;
            public readonly float depth;
            public readonly float height;
            public readonly float minRoomDim;

            public FurnitureSpec(float width, float depth, float height, float minRoomDim)
            {
                this.width = width;
                this.depth = depth;
                this.height = height;
                this.minRoomDim = minRoomDim;
            }
        }

        private static readonly Dictionary<FurnitureType, FurnitureSpec> Catalogue =
            new Dictionary<FurnitureType, FurnitureSpec>
            {
                { FurnitureType.Table,     new FurnitureSpec(1.4f, 0.8f,  0.75f, 4.0f) },
                { FurnitureType.Desk,      new FurnitureSpec(1.2f, 0.6f,  0.75f, 3.5f) },
                { FurnitureType.Chair,     new FurnitureSpec(0.5f, 0.5f,  0.9f,  0.0f) },
                { FurnitureType.Crate,     new FurnitureSpec(0.8f, 0.8f,  0.8f,  0.0f) },
                { FurnitureType.Barrel,    new FurnitureSpec(0.6f, 0.6f,  0.9f,  0.0f) },
                { FurnitureType.Shelf,     new FurnitureSpec(1.0f, 0.4f,  1.8f,  0.0f) },
                { FurnitureType.Cabinet,   new FurnitureSpec(0.9f, 0.45f, 1.6f,  0.0f) },
                { FurnitureType.Bookshelf, new FurnitureSpec(1.2f, 0.35f, 1.8f,  3.5f) },
                { FurnitureType.Bed,       new FurnitureSpec(2.0f, 1.4f,  0.5f,  6.0f) },
                { FurnitureType.Sofa,      new FurnitureSpec(1.8f, 0.8f,  0.8f,  5.5f) },
                { FurnitureType.Locker,    new FurnitureSpec(0.9f, 0.5f,  1.9f,  0.0f) },
                { FurnitureType.SideTable, new FurnitureSpec(0.5f, 0.5f,  0.55f, 0.0f) },
                { FurnitureType.Stool,     new FurnitureSpec(0.4f, 0.4f,  0.5f,  0.0f) },
            };

        // Furniture palette per room kind. The generator draws from these, in the
        // listed relative frequency (repeats bias the pick), then rejects any item
        // that can't fit the room. Entry/corridor rooms stay sparse and thin so the
        // breach path and passages read as circulation space, not storage.
        private static readonly Dictionary<RoomType, FurnitureType[]> PaletteByRoom =
            new Dictionary<RoomType, FurnitureType[]>
            {
                [RoomType.Entry] = new[]
                {
                    FurnitureType.Cabinet, FurnitureType.Shelf, FurnitureType.Crate,
                    FurnitureType.Locker, FurnitureType.Stool
                },
                [RoomType.Corridor] = new[]
                {
                    FurnitureType.Crate, FurnitureType.Shelf, FurnitureType.Cabinet,
                    FurnitureType.Barrel, FurnitureType.Locker
                },
                // No standalone chairs here: chairs only ever appear tucked at a
                // desk or table (see the companion pass), the way real rooms read.
                [RoomType.Standard] = new[]
                {
                    FurnitureType.Table, FurnitureType.Desk,
                    FurnitureType.Crate, FurnitureType.Shelf, FurnitureType.Cabinet,
                    FurnitureType.Bookshelf, FurnitureType.Barrel, FurnitureType.Sofa,
                    FurnitureType.Locker, FurnitureType.Stool
                },
                // Chairs stay standalone ONLY here: loose chairs along the walls of
                // a holding room read as hostage seating, which suits the scenario.
                [RoomType.HostageRoom] = new[]
                {
                    FurnitureType.Chair, FurnitureType.Chair, FurnitureType.Desk, FurnitureType.Table,
                    FurnitureType.Cabinet, FurnitureType.Shelf, FurnitureType.Bed,
                    FurnitureType.Stool
                },
            };

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Places furniture in every room of <paramref name="layout"/>, mutating
        /// each room's <see cref="RoomData.furniture"/> list. Entity positions are
        /// read from <paramref name="entities"/> so no item overlaps an actor.
        /// </summary>
        /// <param name="layout">Generated layout (rooms, doors, entry points).</param>
        /// <param name="entities">All placed entity records (trainee/hostage/terrorists).</param>
        /// <param name="config">Evaluator configuration (room size + randomness).</param>
        /// <param name="rng">Seeded random instance shared across the pipeline.</param>
        /// <returns>The flat list of every placed item (also stored on the rooms).</returns>
        public List<FurnitureData> Place(
            LayoutData layout, List<EntityRecord> entities, ScenarioConfig config, System.Random rng)
        {
            var all = new List<FurnitureData>();
            if (layout?.rooms == null) return all;

            RandomnessLevel randomness = config.executionControls.randomnessLevel;
            RoomSizeCategory sizeCategory =
                layout.layoutMetadata?.roomSizeCategory ?? config.missionStructure.roomSize;

            // Group entity positions by room once so per-room placement is cheap.
            var entitiesByRoom = new Dictionary<string, List<Vector3>>();
            if (entities != null)
            {
                foreach (EntityRecord e in entities)
                {
                    if (e?.assignedRoom == null || e.position == null) continue;
                    if (!entitiesByRoom.TryGetValue(e.assignedRoom, out List<Vector3> list))
                        entitiesByRoom[e.assignedRoom] = list = new List<Vector3>();
                    list.Add(e.position.ToVector3());
                }
            }

            // Entry openings are doors too — keep clear of them. Group by room.
            var entryByRoom = new Dictionary<string, List<Vector3>>();
            if (layout.entryPoints != null)
            {
                foreach (EntryPointData ep in layout.entryPoints)
                {
                    if (ep?.roomId == null || ep.position == null) continue;
                    if (!entryByRoom.TryGetValue(ep.roomId, out List<Vector3> list))
                        entryByRoom[ep.roomId] = list = new List<Vector3>();
                    list.Add(ep.position.ToVector3());
                }
            }

            // Process rooms in id order so the RNG stream (and therefore the
            // output) is deterministic regardless of the room list ordering.
            foreach (RoomData room in layout.rooms.OrderBy(r => r.id, StringComparer.Ordinal))
            {
                room.furniture ??= new List<FurnitureData>();

                entitiesByRoom.TryGetValue(room.id, out List<Vector3> roomEntities);
                entryByRoom.TryGetValue(room.id, out List<Vector3> roomEntries);

                PlaceInRoom(room, roomEntities, roomEntries, sizeCategory, randomness, rng);
                all.AddRange(room.furniture);
            }

            return all;
        }

        // ── Per-room placement ───────────────────────────────────────────────

        private void PlaceInRoom(
            RoomData room,
            List<Vector3> entityPositions,
            List<Vector3> entryPositions,
            RoomSizeCategory sizeCategory,
            RandomnessLevel randomness,
            System.Random rng)
        {
            float width  = room.size != null && room.size.width > 0f ? room.size.width : 6f;
            float depth  = room.size != null && room.size.depth > 0f ? room.size.depth : 6f;
            float minDim = Mathf.Min(width, depth);

            int budget = FurnitureBudget(room.type, width, depth, randomness, rng);
            if (budget <= 0) return;

            float sizeScale = SizeScale(sizeCategory);
            float floorArea = width * depth;
            float usedArea  = 0f;

            // Door + entry keep-out centres (layout coordinates, same space as
            // room/furniture positions — the world build offset cancels).
            var keepouts = new List<Vector3>();
            if (room.doors != null)
                foreach (DoorData d in room.doors)
                    if (d?.position != null) keepouts.Add(d.position.ToVector3());
            if (entryPositions != null) keepouts.AddRange(entryPositions);

            FurnitureType[] palette = PaletteByRoom.TryGetValue(room.type, out FurnitureType[] p)
                ? p : PaletteByRoom[RoomType.Standard];

            var placedRects = new List<Rect>();     // footprints already seated (X/Z)
            int seated = 0;

            for (int i = 0; i < budget; i++)
            {
                bool placed = false;

                for (int attempt = 0; attempt < MaxAttemptsPerItem && !placed; attempt++)
                {
                    FurnitureType type = palette[rng.Next(palette.Length)];
                    FurnitureSpec spec = Catalogue[type];

                    // Gate bulky items out of rooms too small to carry them.
                    if (minDim < spec.minRoomDim) continue;

                    // Local footprint, scaled to the room's size category with a
                    // little per-item jitter for variety (deterministic via rng).
                    float jitter = randomness == RandomnessLevel.Low
                        ? 1.0f
                        : 1.0f + (float)(rng.NextDouble() - 0.5) * 0.12f; // ±6 %
                    float w = spec.width  * sizeScale * jitter;
                    float d = spec.depth  * sizeScale * jitter;
                    float h = spec.height * sizeScale;

                    WallSide wall = (WallSide)rng.Next(4);
                    if (!TrySeatAgainstWall(room, wall, w, d, keepouts, entityPositions,
                                            placedRects, out Vector3 centre, out float rotationY,
                                            out Rect footprint, rng))
                        continue;

                    // Global navigability guard: cap total floor coverage.
                    float area = footprint.width * footprint.height;
                    if ((usedArea + area) / floorArea > MaxFloorCoverage) continue;

                    seated++;
                    usedArea += area;
                    placedRects.Add(footprint);

                    var item = new FurnitureData
                    {
                        id          = $"furniture_{room.id}_{seated:00}",
                        type        = type,
                        position    = new SerializableVector3(centre.x, 0f, centre.z),
                        size        = new SerializableVector3(w, h, d),
                        rotationY   = rotationY,
                        againstWall = wall
                    };
                    room.furniture.Add(item);
                    placed = true;

                    // Companion pass: dress the item the way real rooms are
                    // furnished. Chairs belong to desks/tables; beds and sofas
                    // get a nightstand beside them against the same wall.
                    switch (type)
                    {
                        case FurnitureType.Desk:
                            PlaceChairsAtParent(room, item, 1, sizeScale, randomness,
                                keepouts, entityPositions, placedRects,
                                ref usedArea, floorArea, ref seated, rng);
                            break;
                        case FurnitureType.Table:
                            PlaceChairsAtParent(room, item, 1 + rng.Next(2), sizeScale, randomness,
                                keepouts, entityPositions, placedRects,
                                ref usedArea, floorArea, ref seated, rng);
                            break;
                        case FurnitureType.Bed:
                        case FurnitureType.Sofa:
                            PlaceSideTableBeside(room, item, sizeScale, randomness,
                                keepouts, entityPositions, placedRects,
                                ref usedArea, floorArea, ref seated, rng);
                            break;
                    }
                }
            }
        }

        // ── Companion placement ──────────────────────────────────────────────

        /// <summary>
        /// Seats up to <paramref name="count"/> chairs tucked in front of a
        /// wall-anchored desk/table, pulled out slightly (as if in use) and turned
        /// to face it. Chair yaw stays axis-aligned (parent + 180°), so footprint
        /// maths remain exact AABBs. Chairs that fail any clearance check are
        /// silently skipped — a desk without a chair is still believable.
        /// </summary>
        private void PlaceChairsAtParent(
            RoomData room, FurnitureData parent, int count,
            float sizeScale, RandomnessLevel randomness,
            List<Vector3> keepouts, List<Vector3> entityPositions, List<Rect> placedRects,
            ref float usedArea, float floorArea, ref int seated, System.Random rng)
        {
            FurnitureSpec spec = Catalogue[FurnitureType.Chair];
            WallAxes(parent.againstWall, out Vector3 inward, out Vector3 tangent);
            bool alongX = parent.againstWall == WallSide.North || parent.againstWall == WallSide.South;

            // Lateral slots across the parent's front edge: centred for one chair,
            // spread towards the ends for two. Width always runs along the wall.
            float[] slots = count <= 1 ? new[] { 0f } : new[] { -0.27f, 0.27f };

            for (int i = 0; i < count && i < slots.Length; i++)
            {
                float jitter = randomness == RandomnessLevel.Low
                    ? 1.0f
                    : 1.0f + (float)(rng.NextDouble() - 0.5) * 0.12f;
                float w = spec.width  * sizeScale * jitter;
                float d = spec.depth  * sizeScale * jitter;
                float h = spec.height * sizeScale;

                // Pulled-out gap between the parent's front edge and the chair.
                // Kept above FurnitureGap so the no-overlap invariant holds.
                float pull = 0.2f + (float)rng.NextDouble() * 0.15f;

                Vector3 centre = parent.position.ToVector3()
                               + inward  * (parent.size.z * 0.5f + d * 0.5f + pull)
                               + tangent * (slots[i] * parent.size.x);

                float halfFx = alongX ? w * 0.5f : d * 0.5f;
                float halfFz = alongX ? d * 0.5f : w * 0.5f;
                var rect = new Rect(centre.x - halfFx, centre.z - halfFz, halfFx * 2f, halfFz * 2f);

                if (!TryCommitCompanion(room, FurnitureType.Chair, centre, rect,
                        new Vector3(w, h, d), (parent.rotationY + 180f) % 360f, parent.againstWall,
                        keepouts, entityPositions, placedRects, ref usedArea, floorArea, ref seated))
                    continue;
            }
        }

        /// <summary>
        /// Seats one side table (nightstand) directly beside a wall-anchored bed
        /// or sofa, back against the same wall, sharing its orientation. Tries the
        /// randomly-picked side first, then the other; gives up quietly if neither
        /// end has room.
        /// </summary>
        private void PlaceSideTableBeside(
            RoomData room, FurnitureData parent,
            float sizeScale, RandomnessLevel randomness,
            List<Vector3> keepouts, List<Vector3> entityPositions, List<Rect> placedRects,
            ref float usedArea, float floorArea, ref int seated, System.Random rng)
        {
            FurnitureSpec spec = Catalogue[FurnitureType.SideTable];
            WallAxes(parent.againstWall, out Vector3 inward, out Vector3 tangent);
            bool alongX = parent.againstWall == WallSide.North || parent.againstWall == WallSide.South;

            float jitter = randomness == RandomnessLevel.Low
                ? 1.0f
                : 1.0f + (float)(rng.NextDouble() - 0.5) * 0.12f;
            float w = spec.width  * sizeScale * jitter;
            float d = spec.depth  * sizeScale * jitter;
            float h = spec.height * sizeScale;

            float firstSide = rng.Next(2) == 0 ? 1f : -1f;
            foreach (float side in new[] { firstSide, -firstSide })
            {
                // Along the wall: parent half-width + gap + own half-width (local
                // width always runs along the anchored wall). Across: shift so the
                // table's back sits on the wall plane despite the depth difference.
                Vector3 centre = parent.position.ToVector3()
                               + tangent * (side * (parent.size.x * 0.5f + FurnitureGap + 0.05f + w * 0.5f))
                               + inward  * ((d - parent.size.z) * 0.5f);

                float halfFx = alongX ? w * 0.5f : d * 0.5f;
                float halfFz = alongX ? d * 0.5f : w * 0.5f;
                var rect = new Rect(centre.x - halfFx, centre.z - halfFz, halfFx * 2f, halfFz * 2f);

                if (TryCommitCompanion(room, FurnitureType.SideTable, centre, rect,
                        new Vector3(w, h, d), parent.rotationY, parent.againstWall,
                        keepouts, entityPositions, placedRects, ref usedArea, floorArea, ref seated))
                    return;
            }
        }

        /// <summary>
        /// Validates a companion footprint against every invariant a primary item
        /// obeys (floor bounds, door keep-outs, entity clearance, item gaps and the
        /// floor-coverage cap) and, if it passes, records the item on the room.
        /// </summary>
        private bool TryCommitCompanion(
            RoomData room, FurnitureType type, Vector3 centre, Rect rect, Vector3 size,
            float rotationY, WallSide wall,
            List<Vector3> keepouts, List<Vector3> entityPositions, List<Rect> placedRects,
            ref float usedArea, float floorArea, ref int seated)
        {
            float area = rect.width * rect.height;
            if ((usedArea + area) / floorArea > MaxFloorCoverage) return false;
            if (!FootprintIsValid(room, rect, keepouts, entityPositions, placedRects)) return false;

            seated++;
            usedArea += area;
            placedRects.Add(rect);
            room.furniture.Add(new FurnitureData
            {
                id          = $"furniture_{room.id}_{seated:00}",
                type        = type,
                position    = new SerializableVector3(centre.x, 0f, centre.z),
                size        = new SerializableVector3(size.x, size.y, size.z),
                rotationY   = rotationY,
                againstWall = wall
            });
            return true;
        }

        /// <summary>Room-inward normal and along-wall tangent for a wall side.</summary>
        private static void WallAxes(WallSide wall, out Vector3 inward, out Vector3 tangent)
        {
            switch (wall)
            {
                case WallSide.North: inward = new Vector3(0f, 0f, -1f); break;
                case WallSide.South: inward = new Vector3(0f, 0f,  1f); break;
                case WallSide.East:  inward = new Vector3(-1f, 0f, 0f); break;
                default:             inward = new Vector3( 1f, 0f, 0f); break; // West
            }
            tangent = (wall == WallSide.North || wall == WallSide.South)
                ? new Vector3(1f, 0f, 0f)
                : new Vector3(0f, 0f, 1f);
        }

        /// <summary>
        /// The full rejection suite shared by companion items: footprint inside the
        /// inset floor bounds, clear of doorway keep-outs, clear of entities, and
        /// clear of every already-seated item.
        /// </summary>
        private bool FootprintIsValid(
            RoomData room, Rect rect,
            List<Vector3> keepouts, List<Vector3> entityPositions, List<Rect> placedRects)
        {
            const float eps = 1e-3f;
            float cx = room.position.x;
            float cz = room.position.z;
            // Half-extents of the REAL interior: out to each wall's inner face,
            // not just the room's nominal footprint (see InteriorOutset).
            float halfW = room.size.width * 0.5f + InteriorOutset;
            float halfD = room.size.depth * 0.5f + InteriorOutset;
            if (rect.xMin < cx - halfW - eps || rect.xMax > cx + halfW + eps ||
                rect.yMin < cz - halfD - eps || rect.yMax > cz + halfD + eps)
                return false;

            foreach (Vector3 k in keepouts)
                if (RectOverlapsCircle(rect, k.x, k.z, DoorKeepout))
                    return false;

            if (entityPositions != null)
                foreach (Vector3 e in entityPositions)
                    if (PointInRect(e.x, e.z, rect, EntityClearance))
                        return false;

            foreach (Rect other in placedRects)
                if (RectsOverlap(rect, other, FurnitureGap))
                    return false;

            return true;
        }

        /// <summary>
        /// Tries to seat an item of footprint <paramref name="w"/>×<paramref name="d"/>
        /// (local width × depth) against <paramref name="wall"/>, its back to the
        /// wall and its face turned into the room. Picks a random position along
        /// the wall and rejects it if the footprint leaves the floor, or overlaps a
        /// doorway keep-out, an entity, or an already-placed item. On success
        /// returns the world footprint centre, the yaw rotation, and the X/Z
        /// footprint rect.
        /// </summary>
        private bool TrySeatAgainstWall(
            RoomData room, WallSide wall, float w, float d,
            List<Vector3> keepouts, List<Vector3> entityPositions, List<Rect> placedRects,
            out Vector3 centre, out float rotationY, out Rect footprint, System.Random rng)
        {
            centre = Vector3.zero;
            rotationY = 0f;
            footprint = default;

            float cx = room.position.x;
            float cz = room.position.z;
            // Half-extents of the REAL interior: out to each wall's inner face,
            // not just the room's nominal footprint (see InteriorOutset).
            float halfW = room.size.width * 0.5f + InteriorOutset;
            float halfD = room.size.depth * 0.5f + InteriorOutset;

            // World footprint half-extents: for east/west walls the item is turned
            // 90°, so its depth runs along X and width along Z.
            bool alongX = wall == WallSide.North || wall == WallSide.South; // wall runs along X
            float halfFx = alongX ? w * 0.5f : d * 0.5f;
            float halfFz = alongX ? d * 0.5f : w * 0.5f;

            // The item must fit between the two perpendicular walls with a margin.
            if (halfFx > halfW || halfFz > halfD) return false;

            float px, pz;
            switch (wall)
            {
                case WallSide.North: // +Z wall, face south, back to north
                    pz = cz + halfD - halfFz - WallGap;
                    px = cx + RandRange(-(halfW - halfFx), halfW - halfFx, rng);
                    rotationY = 180f;
                    break;
                case WallSide.South: // -Z wall, face north
                    pz = cz - halfD + halfFz + WallGap;
                    px = cx + RandRange(-(halfW - halfFx), halfW - halfFx, rng);
                    rotationY = 0f;
                    break;
                case WallSide.East:  // +X wall, face west
                    px = cx + halfW - halfFx - WallGap;
                    pz = cz + RandRange(-(halfD - halfFz), halfD - halfFz, rng);
                    rotationY = 270f;
                    break;
                default:             // West: -X wall, face east
                    px = cx - halfW + halfFx + WallGap;
                    pz = cz + RandRange(-(halfD - halfFz), halfD - halfFz, rng);
                    rotationY = 90f;
                    break;
            }

            var rect = new Rect(px - halfFx, pz - halfFz, halfFx * 2f, halfFz * 2f);

            // Reject: too close to a doorway / entry opening.
            foreach (Vector3 k in keepouts)
                if (RectOverlapsCircle(rect, k.x, k.z, DoorKeepout))
                    return false;

            // Reject: overlaps an entity (expand footprint by the clearance).
            if (entityPositions != null)
                foreach (Vector3 e in entityPositions)
                    if (PointInRect(e.x, e.z, rect, EntityClearance))
                        return false;

            // Reject: overlaps another item (expand by the inter-item gap).
            foreach (Rect other in placedRects)
                if (RectsOverlap(rect, other, FurnitureGap))
                    return false;

            centre = new Vector3(px, 0f, pz);
            footprint = rect;
            return true;
        }

        // ── Budget + scaling ─────────────────────────────────────────────────

        /// <summary>
        /// How many items to attempt in a room. Scales with floor area and
        /// randomness, then multiplies by a per-room-kind factor so entries and
        /// corridors stay sparse. Result is clamped so no room is ever crammed.
        /// </summary>
        private static int FurnitureBudget(
            RoomType type, float width, float depth, RandomnessLevel randomness, System.Random rng)
        {
            float density = randomness switch
            {
                RandomnessLevel.Low  => 0.09f,
                RandomnessLevel.High => 0.18f,
                _                    => 0.13f,
            };

            float area = width * depth;
            float roomFactor = type switch
            {
                RoomType.Entry       => 0.5f,
                RoomType.Corridor    => 0.45f,
                RoomType.HostageRoom => 0.9f,
                _                    => 1.2f,
            };

            int hardCap = type switch
            {
                RoomType.Entry    => 3,
                RoomType.Corridor => 3,
                _                 => width >= 7.5f ? 9 : width >= 5.5f ? 6 : 3,
            };

            float expected = area * density * roomFactor;
            int baseline = Mathf.FloorToInt(expected);

            // Fractional remainder becomes a probabilistic extra item, so small
            // rooms still occasionally get a piece without ever exceeding the cap.
            if (rng.NextDouble() < expected - baseline) baseline++;

            return Mathf.Clamp(baseline, 0, hardCap);
        }

        /// <summary>Mild footprint multiplier by room-size category so items in a
        /// large hall read a touch bigger than those in a cramped store-room,
        /// while staying within believable human scale.</summary>
        private static float SizeScale(RoomSizeCategory category) => category switch
        {
            RoomSizeCategory.Small => 0.9f,
            RoomSizeCategory.Large => 1.08f,
            _                      => 1.0f,
        };

        // ── Geometry helpers (X/Z plane) ─────────────────────────────────────

        private static bool RectsOverlap(Rect a, Rect b, float pad)
        {
            return a.xMin - pad < b.xMax && a.xMax + pad > b.xMin
                && a.yMin - pad < b.yMax && a.yMax + pad > b.yMin;
        }

        private static bool PointInRect(float x, float z, Rect r, float pad)
        {
            return x >= r.xMin - pad && x <= r.xMax + pad
                && z >= r.yMin - pad && z <= r.yMax + pad;
        }

        /// <summary>True when the axis-aligned rect comes within
        /// <paramref name="radius"/> of the point (cx, cz).</summary>
        private static bool RectOverlapsCircle(Rect r, float cx, float cz, float radius)
        {
            float nx = Mathf.Clamp(cx, r.xMin, r.xMax);
            float nz = Mathf.Clamp(cz, r.yMin, r.yMax);
            float dx = cx - nx;
            float dz = cz - nz;
            return dx * dx + dz * dz < radius * radius;
        }

        private static float RandRange(float min, float max, System.Random rng)
        {
            if (max <= min) return (min + max) * 0.5f;
            return min + (float)rng.NextDouble() * (max - min);
        }
    }
}
