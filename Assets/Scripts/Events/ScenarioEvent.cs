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

    static int _idCounter;

    public ScenarioEvent(
        ScenarioEventType type,
        Vector3           origin,
        GameObject        instigator    = null,
        string            roomId        = null,
        string            targetActorId = null)
    {
        EventId       = ++_idCounter;
        Type          = type;
        Origin        = origin;
        Instigator    = instigator;
        Timestamp     = Time.time;
        RoomId        = roomId;
        TargetActorId = targetActorId;
    }

    public override string ToString() =>
        $"[E{EventId}] {Type} | t={Timestamp:F2}s | origin={Origin:F1} | by={Instigator?.name ?? "—"}";
}
