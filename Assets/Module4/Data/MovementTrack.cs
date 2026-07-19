// Module 4 | Sentinels | University of Moratuwa | 2026

using System.Collections.Generic;
using Newtonsoft.Json;

namespace TeamSentinels.Module4.Data
{
    /// <summary>
    /// One timestamped snapshot of the trainee's tracked body state, sampled at a
    /// fixed rate (~5 Hz) by CognitiveMovementRecorder while a session is active.
    /// Positions are world-space so the dashboard can overlay the path onto the
    /// scenario layout, exactly like replayFrames.
    /// </summary>
    public class MovementSample
    {
        /// <summary>Seconds since StartSession — same clock as events/replayFrames.</summary>
        [JsonProperty("t")] public float t;

        [JsonProperty("head")]  public Vector3Serializable head;
        [JsonProperty("lHand")] public Vector3Serializable lHand;
        [JsonProperty("rHand")] public Vector3Serializable rHand;

        /// <summary>Head yaw in degrees (world heading, 0 = +Z).</summary>
        [JsonProperty("yaw")] public float yaw;
        /// <summary>Head pitch in degrees (positive = looking up).</summary>
        [JsonProperty("pitch")] public float pitch;

        /// <summary>Horizontal head speed in m/s over the last sample interval.</summary>
        [JsonProperty("speed")] public float speed;
        /// <summary>Head yaw angular speed in deg/s (scanning behaviour).</summary>
        [JsonProperty("angSpeed")] public float angSpeed;

        /// <summary>Crouch estimate 0 (standing) → 1 (fully crouched).</summary>
        [JsonProperty("crouch")] public float crouch;

        /// <summary>Weapon-in-hand bitmask: 1 = left, 2 = right.</summary>
        [JsonProperty("gun")] public int gun;

        /// <summary>Player health fraction 0–1 at sample time.</summary>
        [JsonProperty("hp")] public float hp;
    }

    /// <summary>
    /// Aggregates over the whole session, updated incrementally on every sample so
    /// they are always current when EndSession() serialises the summary.
    /// </summary>
    public class MovementStats
    {
        [JsonProperty("sampleRateHz")]     public float sampleRateHz;
        [JsonProperty("totalDistance")]    public float totalDistance;     // m, horizontal head travel
        [JsonProperty("avgSpeed")]         public float avgSpeed;          // m/s while session active
        [JsonProperty("maxSpeed")]         public float maxSpeed;          // m/s
        [JsonProperty("timeMoving")]       public float timeMoving;        // s with speed > 0.15 m/s
        [JsonProperty("timeStill")]        public float timeStill;         // s with speed <= 0.15 m/s
        [JsonProperty("timeCrouched")]     public float timeCrouched;      // s with crouch > 0.35
        [JsonProperty("crouchCount")]      public int   crouchCount;       // distinct crouch events
        [JsonProperty("avgHeadHeight")]    public float avgHeadHeight;     // m above rig floor
        [JsonProperty("minHeadHeight")]    public float minHeadHeight;     // m
        [JsonProperty("totalHeadYawDeg")]  public float totalHeadYawDeg;   // accumulated |yaw delta|
        [JsonProperty("avgAngSpeed")]      public float avgAngSpeed;       // deg/s
        [JsonProperty("peakAngSpeed")]     public float peakAngSpeed;      // deg/s
        [JsonProperty("leftHandDistance")] public float leftHandDistance;  // m
        [JsonProperty("rightHandDistance")]public float rightHandDistance; // m
        [JsonProperty("timeWeaponHeld")]   public float timeWeaponHeld;    // s
    }

    /// <summary>
    /// Full body-movement record of a session: per-sample track plus aggregates.
    /// Attached to SessionSummary as "movementTrack" and stored by the AAR
    /// dashboard alongside the rest of the session document.
    /// </summary>
    public class MovementTrack
    {
        [JsonProperty("stats")]   public MovementStats stats = new MovementStats();
        [JsonProperty("samples")] public List<MovementSample> samples = new List<MovementSample>();
    }
}
