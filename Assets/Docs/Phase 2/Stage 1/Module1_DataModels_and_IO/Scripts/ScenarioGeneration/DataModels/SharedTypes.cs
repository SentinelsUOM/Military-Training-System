// =============================================================================
// SharedTypes.cs
// Module 1 – Dynamic Scenario Generation
// Team Sentinels | University of Moratuwa | 2026
//
// Shared primitive data structures used by both ScenarioConfig and Scenario
// data models: serialisable Vector3 wrapper and room count range.
// =============================================================================

using System;
using Newtonsoft.Json;
using UnityEngine;

namespace TeamSentinels.ScenarioGeneration.DataModels
{
    /// <summary>
    /// JSON-serialisable 3D position/direction that converts to and from
    /// <see cref="UnityEngine.Vector3"/>. Used for all position, size, and
    /// direction fields throughout the scenario schemas.
    /// </summary>
    [Serializable]
    public class SerializableVector3
    {
        /// <summary>X component (Unity world units, metres).</summary>
        [JsonProperty("x")]
        public float x;

        /// <summary>Y component (Unity world units, metres). Always 0 for single-floor.</summary>
        [JsonProperty("y")]
        public float y;

        /// <summary>Z component (Unity world units, metres).</summary>
        [JsonProperty("z")]
        public float z;

        /// <summary>
        /// Default constructor required for deserialisation.
        /// </summary>
        public SerializableVector3() { }

        /// <summary>
        /// Constructs a serialisable vector from component values.
        /// </summary>
        public SerializableVector3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        /// <summary>
        /// Constructs from a Unity <see cref="Vector3"/>.
        /// </summary>
        public SerializableVector3(Vector3 v)
        {
            x = v.x;
            y = v.y;
            z = v.z;
        }

        /// <summary>
        /// Converts to a Unity <see cref="Vector3"/>.
        /// </summary>
        public Vector3 ToVector3() => new Vector3(x, y, z);

        /// <summary>
        /// Implicit conversion from <see cref="Vector3"/> for convenience.
        /// </summary>
        public static implicit operator SerializableVector3(Vector3 v) => new SerializableVector3(v);

        /// <summary>
        /// Implicit conversion to <see cref="Vector3"/> for convenience.
        /// </summary>
        public static implicit operator Vector3(SerializableVector3 sv) => sv.ToVector3();

        public override string ToString() => $"({x:F2}, {y:F2}, {z:F2})";
    }

    /// <summary>
    /// Room dimensions in metres used in the Scenario output schema.
    /// Distinct from <see cref="SerializableVector3"/> because the fields are
    /// named width/depth/height rather than x/y/z.
    /// </summary>
    [Serializable]
    public class RoomSize
    {
        /// <summary>Room width in metres along the X axis.</summary>
        [JsonProperty("width")]
        public float width;

        /// <summary>Room depth in metres along the Z axis.</summary>
        [JsonProperty("depth")]
        public float depth;

        /// <summary>Room height in metres along the Y axis.</summary>
        [JsonProperty("height")]
        public float height;

        public RoomSize() { }

        public RoomSize(float width, float depth, float height)
        {
            this.width = width;
            this.depth = depth;
            this.height = height;
        }
    }

    /// <summary>
    /// Min/max range for room count. Generator selects within range based on
    /// randomness level and layout type constraints.
    /// Schema constraint: min ∈ [3, 20], max ∈ [3, 20], min ≤ max.
    /// </summary>
    [Serializable]
    public class RoomCountRange
    {
        /// <summary>Minimum number of rooms to generate (3–20).</summary>
        [JsonProperty("min")]
        public int min = 5;

        /// <summary>Maximum number of rooms to generate (3–20).</summary>
        [JsonProperty("max")]
        public int max = 8;

        public RoomCountRange() { }

        public RoomCountRange(int min, int max)
        {
            this.min = min;
            this.max = max;
        }

        /// <summary>
        /// Validates that min ≤ max and both are within [3, 20].
        /// </summary>
        /// <param name="errorMessage">Describes the validation failure, if any.</param>
        /// <returns>True if valid.</returns>
        public bool Validate(out string errorMessage)
        {
            if (min < 3 || min > 20)
            {
                errorMessage = $"roomCount.min ({min}) must be in range [3, 20].";
                return false;
            }
            if (max < 3 || max > 20)
            {
                errorMessage = $"roomCount.max ({max}) must be in range [3, 20].";
                return false;
            }
            if (min > max)
            {
                errorMessage = $"roomCount.min ({min}) must not exceed roomCount.max ({max}).";
                return false;
            }
            errorMessage = null;
            return true;
        }
    }

    /// <summary>
    /// Axis-aligned bounding box for the entire layout, used by Module 4
    /// (AAR) for camera bounds in overhead replay view.
    /// </summary>
    [Serializable]
    public class BoundingBox
    {
        /// <summary>Minimum corner of the bounding box (world coordinates).</summary>
        [JsonProperty("min")]
        public SerializableVector3 min;

        /// <summary>Maximum corner of the bounding box (world coordinates).</summary>
        [JsonProperty("max")]
        public SerializableVector3 max;

        public BoundingBox() { }

        public BoundingBox(SerializableVector3 min, SerializableVector3 max)
        {
            this.min = min;
            this.max = max;
        }
    }
}
