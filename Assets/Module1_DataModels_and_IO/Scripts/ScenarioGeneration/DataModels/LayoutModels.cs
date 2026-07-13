// =============================================================================
// LayoutModels.cs
// Module 1 – Dynamic Scenario Generation
// Team Sentinels | University of Moratuwa | 2026
//
// Data models for the Scenario.json "layout" section: room graph, doors,
// entry points, and aggregate layout metadata. Rooms are modelled as graph
// nodes with doors as room attributes (not independent entities), following
// [20]'s finding that this significantly reduces generation difficulty.
// =============================================================================

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace TeamSentinels.ScenarioGeneration.DataModels
{
    // =========================================================================
    // Door Data
    // =========================================================================

    /// <summary>
    /// A door connecting two rooms. Modelled as an attribute of its parent room
    /// following the room-centric graph model [20]. Door positions are used by
    /// Module 2 (NPC Behaviour) for facing direction and alert detection.
    /// </summary>
    [Serializable]
    public class DoorData
    {
        /// <summary>Unique door identifier (e.g., "door_01_02").</summary>
        [JsonProperty("id")]
        public string id;

        /// <summary>Room ID on the other side of this door (graph edge target).</summary>
        [JsonProperty("connectsToRoomId")]
        public string connectsToRoomId;

        /// <summary>Door position in Unity world coordinates.</summary>
        [JsonProperty("position")]
        public SerializableVector3 position;

        /// <summary>Wall on which the door is placed (north/south/east/west).</summary>
        [JsonProperty("wallSide")]
        public WallSide wallSide;

        /// <summary>
        /// Initial state of the door at scene build time (open/closed/locked).
        /// Derived during layout generation from room type and seed. Both
        /// reciprocal door records for an edge always share the same state.
        /// Consumed by <c>SceneBuilder</c> to configure the interactive door
        /// prefab and by Module 2 for door-aware NPC navigation.
        /// </summary>
        [JsonProperty("state")]
        public DoorState state = DoorState.Closed;

        /// <summary>
        /// True if this door sits on the building's exterior shell rather than
        /// between two interior rooms. Reserved for future breach-point logic;
        /// interior connecting doors (the common case) are false.
        /// </summary>
        [JsonProperty("isExterior")]
        public bool isExterior;
    }

    // =========================================================================
    // Furniture Data
    // =========================================================================

    /// <summary>
    /// A single piece of furniture placed inside a room. Modelled as a room
    /// attribute (like <see cref="DoorData"/>) following the room-centric graph
    /// model [20]. Furniture is decorative-plus-tactical: it makes rooms read as
    /// real spaces and provides cover/obstacles that Module 2 NPCs path around
    /// (SceneBuilder bakes furniture into the NavMesh as obstacles). Placement is
    /// fully seed-reproducible and guaranteed to clear walls, doorways, entities,
    /// and other furniture by <c>FurniturePlacer</c>.
    /// </summary>
    [Serializable]
    public class FurnitureData
    {
        /// <summary>Unique furniture identifier (e.g., "furniture_room_02_01").</summary>
        [JsonProperty("id")]
        public string id;

        /// <summary>Furniture category, which fixes its canonical proportions.</summary>
        [JsonProperty("type")]
        public FurnitureType type;

        /// <summary>
        /// Furniture footprint centre in Unity world coordinates. Y is the floor
        /// level (0 for single-floor scenarios); SceneBuilder raises the mesh so
        /// it rests on the floor.
        /// </summary>
        [JsonProperty("position")]
        public SerializableVector3 position;

        /// <summary>
        /// Item dimensions in metres expressed in the item's LOCAL axes before
        /// rotation: x = width, y = height, z = depth. SceneBuilder scales the
        /// greybox box (or a mapped prefab) to exactly this size, then applies
        /// <see cref="rotationY"/>. Already scaled/clamped to fit the room.
        /// </summary>
        [JsonProperty("size")]
        public SerializableVector3 size;

        /// <summary>
        /// Yaw rotation in degrees about the Y axis (0/90/180/270). Wall-anchored
        /// items face into the room, so their "back" (local −Z) sits against the
        /// wall they were placed on.
        /// </summary>
        [JsonProperty("rotationY")]
        public float rotationY;

        /// <summary>
        /// Wall this item is anchored against (its back faces this wall). Recorded
        /// so Module 2 can reason about which sightlines the item blocks and
        /// Module 4 can render it consistently on the AAR map.
        /// </summary>
        [JsonProperty("againstWall")]
        public WallSide againstWall;
    }

    // =========================================================================
    // Room Data
    // =========================================================================

    /// <summary>
    /// A single room node in the room graph. Each room has a unique ID used as
    /// zone ID for location-tagged cognitive events (Module 3) and room labels
    /// for AAR reconstruction (Module 4).
    /// </summary>
    [Serializable]
    public class RoomData
    {
        /// <summary>
        /// Unique room identifier (e.g., "room_01"). Used as zone ID for
        /// location-tagged events and room labels across all downstream modules.
        /// </summary>
        [JsonProperty("id")]
        public string id;

        /// <summary>
        /// Room type derived from generation context: entry, corridor, standard,
        /// or hostage_room.
        /// </summary>
        [JsonProperty("type")]
        public RoomType type;

        /// <summary>
        /// Room centre in Unity world coordinates.
        /// Y is always 0 for single-floor scenarios.
        /// </summary>
        [JsonProperty("position")]
        public SerializableVector3 position;

        /// <summary>Room dimensions in metres (width, depth, height).</summary>
        [JsonProperty("size")]
        public RoomSize size;

        /// <summary>
        /// Room IDs connected via doors (graph edges). Defines the room
        /// adjacency graph used by all generators and navigation systems.
        /// </summary>
        [JsonProperty("connectedRoomIds")]
        public List<string> connectedRoomIds = new List<string>();

        /// <summary>
        /// Door objects as attributes of this room. Each door records which
        /// room it connects to, its position, and its wall side.
        /// </summary>
        [JsonProperty("doors")]
        public List<DoorData> doors = new List<DoorData>();

        /// <summary>
        /// Furniture placed inside this room. Populated by <c>FurniturePlacer</c>
        /// after entity placement so every item clears the entities, doorways, and
        /// walls. Empty for rooms the placer keeps clear (or when furniture is
        /// disabled). Consumed by SceneBuilder (rendering + NavMesh obstacles) and
        /// available to Modules 2/4 for cover reasoning and AAR map rendering.
        /// </summary>
        [JsonProperty("furniture")]
        public List<FurnitureData> furniture = new List<FurnitureData>();

        /// <summary>
        /// BFS depth from the entry room. Used for depth-based role
        /// assignment [18] and as cognitive context variable (deeper rooms =
        /// higher expected cognitive load).
        /// </summary>
        [JsonProperty("depth")]
        public int depth;

        /// <summary>
        /// Human-readable label (e.g., "Entry Hall", "East Corridor").
        /// Used by Module 3 (cognitive reports) and Module 4 (AAR timeline
        /// and map overlays).
        /// </summary>
        [JsonProperty("zoneLabel")]
        public string zoneLabel;
    }

    // =========================================================================
    // Entry Point Data
    // =========================================================================

    /// <summary>
    /// A building entry point. Single-entry scenarios have one; multiple-entry
    /// scenarios have 2–3. Consumed by Module 4 (AAR) to mark entry positions
    /// on the AAR map.
    /// </summary>
    [Serializable]
    public class EntryPointData
    {
        /// <summary>Unique entry point identifier (e.g., "entry_01").</summary>
        [JsonProperty("id")]
        public string id;

        /// <summary>Room ID of the entry room this point belongs to.</summary>
        [JsonProperty("roomId")]
        public string roomId;

        /// <summary>Entry position in Unity world coordinates.</summary>
        [JsonProperty("position")]
        public SerializableVector3 position;

        /// <summary>Direction the trainee faces upon entry.</summary>
        [JsonProperty("facingDirection")]
        public SerializableVector3 facingDirection;
    }

    // =========================================================================
    // Layout Metadata
    // =========================================================================

    /// <summary>
    /// Aggregate layout statistics for AAR perspective reconstruction (Module 4)
    /// and cognitive context analysis (Module 3).
    /// </summary>
    [Serializable]
    public class LayoutMetadata
    {
        /// <summary>Total number of rooms in the generated layout.</summary>
        [JsonProperty("totalRooms")]
        public int totalRooms;

        /// <summary>Room graph topology used (mirrors ScenarioConfig input).</summary>
        [JsonProperty("layoutType")]
        public LayoutType layoutType;

        /// <summary>Maximum BFS depth in the room graph.</summary>
        [JsonProperty("maxDepth")]
        public int maxDepth;

        /// <summary>
        /// Graph diameter: longest shortest path between any two rooms.
        /// Indicates overall layout traversal complexity.
        /// </summary>
        [JsonProperty("graphDiameter")]
        public int graphDiameter;

        /// <summary>Room size category used (mirrors ScenarioConfig input).</summary>
        [JsonProperty("roomSizeCategory")]
        public RoomSizeCategory roomSizeCategory;

        /// <summary>Entry type used (mirrors ScenarioConfig input).</summary>
        [JsonProperty("entryType")]
        public EntryType entryType;

        /// <summary>
        /// Axis-aligned bounding box enclosing the entire layout.
        /// Used by Module 4 (AAR) for camera bounds in overhead replay view.
        /// </summary>
        [JsonProperty("boundingBox")]
        public BoundingBox boundingBox;
    }

    // =========================================================================
    // Layout (Top-Level Section)
    // =========================================================================

    /// <summary>
    /// Complete room graph, entry points, and layout metadata.
    /// This is the primary spatial data structure consumed by all downstream
    /// modules for room-based operations.
    /// </summary>
    [Serializable]
    public class LayoutData
    {
        /// <summary>Array of room nodes in the room graph.</summary>
        [JsonProperty("rooms")]
        public List<RoomData> rooms = new List<RoomData>();

        /// <summary>
        /// Building entry points (1 for single entry, 2–3 for multiple).
        /// </summary>
        [JsonProperty("entryPoints")]
        public List<EntryPointData> entryPoints = new List<EntryPointData>();

        /// <summary>Aggregate layout statistics and bounding box.</summary>
        [JsonProperty("layoutMetadata")]
        public LayoutMetadata layoutMetadata;
    }
}
