using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Scores a list of NPC candidates against a scenario event and returns the best match.
///
/// Scoring formula (from design spec):
///   score = (1 / distance) × 2.0  +  roleBonus[role][eventType]  +  stateReadiness
///
/// roleBonus table:
///   Guard  + RoomBreached  → 3.0
///   Roamer + GunshotHeard  → 2.0
///   Leader + AllyDownSeen  → 1.5
///   default                → 0.0
///
/// stateReadiness (from INPCResponder.StateScore, range 0.0–1.0):
///   Idle = 1.0 | Suspicious = 0.8 | Alert = 0.5 | TakeCover = 0.2 | Engage = 0.0
///   Down → hard-filtered before scoring
///
/// Hard filters (applied in INPCResponder.CanRespond before candidates reach here):
///   • State is Down / terminal → CanRespond returns false
///   • Already Engage targeting a different threat → CanRespond returns false
///   • Within cooldown window for this event type → CanRespond returns false
///   • Distance > MaxRange → excluded here (second-pass safety net)
/// </summary>
public static class NPCSelector
{
    /// NPCs beyond this distance are never selected.
    public const float MaxRange = 30f;

    /// <summary>
    /// Score all candidates, optionally log the breakdown to TelemetryLogger, and return the winner.
    /// Returns null when no candidate is within MaxRange.
    /// </summary>
    public static INPCResponder SelectBest(
        IReadOnlyList<INPCResponder> candidates,
        ScenarioEvent                e,
        bool                         log = true)
    {
        if (candidates == null || candidates.Count == 0) return null;

        var sb = log ? new StringBuilder() : null;
        sb?.AppendLine($"[NPCSelector] {e.Type} (E{e.EventId}) → {candidates.Count} candidate(s):");

        INPCResponder best      = null;
        float         bestScore = float.MinValue;

        // Accumulate a compact score summary for telemetry.
        var scoreLines = log ? new List<string>() : null;

        foreach (var npc in candidates)
        {
            float dist = Vector3.Distance(npc.Position, e.Origin);

            // Second-pass range guard (first pass is CanRespond).
            if (dist > MaxRange)
            {
                sb?.AppendLine($"  ✗ {npc.NPCId,-20}  dist={dist:F1}m > MaxRange — skipped");
                continue;
            }

            float distScore      = dist > 0.001f ? (1f / dist) * 2f : 2f; // cap to avoid ÷0
            float roleBonus      = GetRoleBonus(npc.Role, e.Type);
            float stateReadiness = npc.StateScore; // 0.0–1.0 per spec

            float total = distScore + roleBonus + stateReadiness;

            string line = $"{npc.NPCId}:dist={distScore:F2}+role={roleBonus:F1}+state={stateReadiness:F2}={total:F2}";
            scoreLines?.Add(line);

            sb?.AppendLine(
                $"  • {npc.NPCId,-20}  dist={dist,5:F1}m  " +
                $"distScore={distScore:F3}  role={roleBonus:F1}  state={stateReadiness:F2}  → {total:F2}");

            if (total > bestScore)
            {
                bestScore = total;
                best      = npc;
            }
        }

        if (best != null)
        {
            sb?.Append($"  ✓ Selected: {best.NPCId} ({bestScore:F2} pts)");

            // Log to telemetry
            string summary = scoreLines != null ? string.Join(" | ", scoreLines) : "";
            var candidateIds = new List<string>();
            foreach (var c in candidates) candidateIds.Add(c.NPCId);

            TelemetryLogger.Instance?.LogDecision(
                e.EventId,
                e.Type.ToString(),
                candidateIds,
                best.NPCId,
                summary);
        }
        else
        {
            sb?.Append("  ✗ No valid candidate within range");

            TelemetryLogger.Instance?.LogDecision(
                e.EventId, e.Type.ToString(),
                new List<string>(), null, "all out of range");
        }

        if (log) Debug.Log(sb?.ToString());
        return best;
    }

    // ── Role bonus table ──────────────────────────────────────────────────────

    static float GetRoleBonus(NPCRole role, ScenarioEventType type)
    {
        return (role, type) switch
        {
            (NPCRole.Guard,  ScenarioEventType.RoomBreached) => 3.0f,
            (NPCRole.Roamer, ScenarioEventType.GunshotHeard) => 2.0f,
            (NPCRole.Leader, ScenarioEventType.AllyDownSeen) => 1.5f,
            _ => 0.0f,
        };
    }
}
