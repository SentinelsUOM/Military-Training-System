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
    /// One stimulus → response measurement, produced by ReactionTimeTracker.
    /// A stimulus is an enemy-caused event (spotted, shot at, engaged); the
    /// response is the trainee's first movement reaction to it.
    /// </summary>
    public class ReactionSample
    {
        /// <summary>Stimulus time — seconds since StartSession (same clock as events).</summary>
        [JsonProperty("t")] public float t;

        /// <summary>Stimulus event type: PlayerSeen, GunshotHeard, TargetConfirmed.</summary>
        [JsonProperty("stimulus")] public string stimulus;

        /// <summary>Actor that caused the stimulus (e.g. terrorist_02).</summary>
        [JsonProperty("actor")] public string actor;

        /// <summary>Angle between trainee gaze and the threat at stimulus time (deg).
        /// 0 = already looking straight at it, 180 = directly behind.</summary>
        [JsonProperty("angleToThreat")] public float angleToThreat;

        /// <summary>Seconds from stimulus to first response. -1 = no response in window.</summary>
        [JsonProperty("reactionTime")] public float reactionTime;

        /// <summary>Which channel responded first: head, hands, movement, trigger, none.</summary>
        [JsonProperty("channel")] public string channel;
    }

    /// <summary>Aggregates over all reaction measurements in the session.</summary>
    public class ReactionStats
    {
        [JsonProperty("stimulusCount")]  public int   stimulusCount;
        [JsonProperty("respondedCount")] public int   respondedCount;
        [JsonProperty("missedCount")]    public int   missedCount;
        [JsonProperty("avgReactionTime")]    public float avgReactionTime;
        [JsonProperty("medianReactionTime")] public float medianReactionTime;
        [JsonProperty("bestReactionTime")]   public float bestReactionTime;
        [JsonProperty("worstReactionTime")]  public float worstReactionTime;
        [JsonProperty("avgAngleToThreat")]   public float avgAngleToThreat;
        [JsonProperty("headResponses")]      public int   headResponses;
        [JsonProperty("handResponses")]      public int   handResponses;
        [JsonProperty("moveResponses")]      public int   moveResponses;
        [JsonProperty("triggerResponses")]   public int   triggerResponses;
    }

    /// <summary>
    /// Full body-movement record of a session: per-sample track plus aggregates,
    /// and the stimulus→response reaction-time measurements taken during play.
    /// Attached to SessionSummary as "movementTrack" and stored by the AAR
    /// dashboard alongside the rest of the session document.
    /// </summary>
    public class MovementTrack
    {
        [JsonProperty("stats")]   public MovementStats stats = new MovementStats();
        [JsonProperty("samples")] public List<MovementSample> samples = new List<MovementSample>();

        [JsonProperty("reactionStats")] public ReactionStats reactionStats = new ReactionStats();
        [JsonProperty("reactions")]     public List<ReactionSample> reactions = new List<ReactionSample>();
    }
}
