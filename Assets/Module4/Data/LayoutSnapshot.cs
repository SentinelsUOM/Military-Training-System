// Module 4 | Sentinels | University of Moratuwa | 2026

using System.Collections.Generic;
using Newtonsoft.Json;

namespace TeamSentinels.Module4.Data
{
    /// <summary>
    /// Compact top-down building layout attached to a <see cref="SessionSummary"/>
    /// so the web dashboard can draw the rooms and doors behind the replay — both
    /// the walls on the 2D dot map and the 3D fly-around replay.
    ///
    /// Coordinates are Unity world metres. The dashboard's horizontal axis is X and
    /// its vertical axis is Z (Y, the up axis, is kept only for 3D wall height).
    ///
    /// This is a pure DTO with NO reference to Module 1's ScenarioData, so the
    /// TeamSentinels.Module4 assembly keeps its zero-dependency boundary. The
    /// ScenarioData → LayoutSnapshot conversion lives in Module4Integration
    /// (Assembly-CSharp), which can see both sides.
    /// </summary>
    public class LayoutSnapshot
    {
        [JsonProperty("rooms")]
        public List<RoomBox> rooms = new List<RoomBox>();

        [JsonProperty("doors")]
        public List<DoorMark> doors = new List<DoorMark>();
    }

    /// <summary>One room as an axis-aligned box, centred at (centerX, centerZ).</summary>
    public class RoomBox
    {
        [JsonProperty("id")]      public string id;
        [JsonProperty("type")]    public string type;
        [JsonProperty("centerX")] public float centerX;   // world X of the room centre
        [JsonProperty("centerZ")] public float centerZ;   // world Z of the room centre
        [JsonProperty("width")]   public float width;      // extent along X
        [JsonProperty("depth")]   public float depth;      // extent along Z
        [JsonProperty("height")]  public float height;     // extent along Y (3D walls)
    }

    /// <summary>A door opening on a room wall, as a point plus the wall it sits on.</summary>
    public class DoorMark
    {
        [JsonProperty("x")]          public float x;
        [JsonProperty("z")]          public float z;
        [JsonProperty("wallSide")]   public string wallSide;   // North | South | East | West
        [JsonProperty("isExterior")] public bool isExterior;
    }
}
