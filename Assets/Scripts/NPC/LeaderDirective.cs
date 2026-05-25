using UnityEngine;

/// <summary>
/// Command issued by a Leader-role terrorist to its squad.
///
/// Lifecycle:
///   1. Leader (NPCRole.Leader) transitions to Alert or Engage in TerroristController.
///   2. TerroristController constructs a LeaderDirective with the trigger's origin
///      as TargetPosition.
///   3. Squad.IssueDirective(directive) broadcasts to non-engaged squad members.
///   4. Each member's OnDirective() handler escalates them to Alert and routes
///      their NavMeshAgent toward TargetPosition.
///   5. TelemetryLogger.LogDirective() emits a record so Module 4 AAR can show
///      "Leader X → Converge on (3.5, 0, 8.2)" in the timeline.
///
/// Members already in Engage ignore directives — they're committed to their target.
/// Members in Down also ignore — terminal state.
/// </summary>
public enum LeaderDirectiveType
{
    /// Move toward TargetPosition and hold there (default leader response).
    Converge,

    /// Stop in place and stand ready (used when leader wants to set up overlapping fire).
    Hold,
}

public class LeaderDirective
{
    public readonly LeaderDirectiveType Type;
    public readonly Vector3             TargetPosition;
    public readonly TerroristController Source;
    public readonly float               Timestamp;
    public readonly string              Reason;

    public LeaderDirective(LeaderDirectiveType type, Vector3 target,
                           TerroristController source, string reason)
    {
        Type           = type;
        TargetPosition = target;
        Source         = source;
        Timestamp      = Time.time;
        Reason         = reason ?? "";
    }

    public override string ToString() =>
        $"[Directive {Type}] from {(Source != null ? Source.NPCId : "?")} → {TargetPosition:F1} (reason: {Reason})";
}
