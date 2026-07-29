using UnityEngine;

/// <summary>
/// Immutable data record for a single scenario event.
/// Created by a detector and passed to EventManager.Raise().
/// No MonoBehaviour — just a plain data object.
/// </summary>
public class ScenarioEvent
{
    /// Monotonically-increasing identifier — used to correlate telemetry records.
    public readonly int EventId;

    public readonly ScenarioEventType Type;

    /// World-space position where the event originated (gun muzzle, door, zone centre, etc.)
    public readonly Vector3 Origin;

    /// The GameObject that caused this event. May be null for timer-based events.
    public readonly GameObject Instigator;

    /// Time.time when the event was created.
    public readonly float Timestamp;

    /// Optional room identifier from Scenario JSON (set by detectors that know their room).
    public readonly string RoomId;

    /// Optional target actor identifier (e.g. which NPC was seen or downed).
    public readonly string TargetActorId;

    /// Optional world-space distance relevant to this event (e.g. shooter↔target range
    /// at the moment of a hit). -1 means "not captured". Module 4 uses this for
    /// distance-aware accuracy scoring — see PerformanceCalculator.
    public readonly float Distance;

    /// Optional detector-supplied tags (e.g. "no_target_in_los" for a negligent
    /// discharge). Merged into Module4Bridge's own tag derivation.
    public readonly System.Collections.Generic.List<string> Tags;

    static int _idCounter;

    public ScenarioEvent(
        ScenarioEventType type,
        Vector3           origin,
        GameObject        instigator    = null,
        string            roomId        = null,
        string            targetActorId = null,
        float             distance      = -1f,
        System.Collections.Generic.List<string> tags = null)
    {
        EventId       = ++_idCounter;
        Type          = type;
        Origin        = origin;
        Instigator    = instigator;
        Timestamp     = Time.time;
        RoomId        = roomId;
        TargetActorId = targetActorId;
        Distance      = distance;
        Tags          = tags;
    }

    public override string ToString() =>
        $"[E{EventId}] {Type} | t={Timestamp:F2}s | origin={Origin:F1} | by={Instigator?.name ?? "—"}";
}
