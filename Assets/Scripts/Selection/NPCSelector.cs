using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Scores a list of NPC candidates against a scenario event and returns the best match.
///
/// Scoring formula (multi-factor, weighted — supports ablation):
///   score = (1/distance) × DistanceWeight
///         + roleBonus[role][eventType] × RoleWeight
///         + stateReadiness              × StateWeight
///         + losBonus                    × LosWeight
///
/// Defaults reproduce the design-spec formula:
///   DistanceWeight = 2.0 | RoleWeight = 1.0 | StateWeight = 1.0 | LosWeight = 1.0
///
/// Set any weight to 0 to ablate that term (used for evaluation experiments —
/// e.g. SetAblation(distance:false) → DistanceWeight=0 to measure how much
/// the distance factor contributes to selection quality).
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
/// losBonus (perception term — added 2026-05-25):
///   Raycast from NPC eye-height to event origin against LosObstacleLayers.
///   Clear LOS → 1.0 | Blocked → 0.0
///   Models the intuition that an NPC who can SEE the event source is a better
///   responder than one who only knows about it via squad propagation.
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

    // ── Scoring weights (mutable for ablation experiments) ────────────────────

    /// Weight applied to (1/distance). Default 2.0 matches the original design spec.
    public static float DistanceWeight = 2.0f;

    /// Weight applied to the role-bonus lookup. Default 1.0.
    public static float RoleWeight = 1.0f;

    /// Weight applied to stateReadiness (INPCResponder.StateScore). Default 1.0.
    public static float StateWeight = 1.0f;

    /// Weight applied to the LOS bonus (1.0 if visible, 0.0 if blocked). Default 1.0.
    public static float LosWeight = 1.0f;

    // ── LOS (perception) configuration ────────────────────────────────────────

    /// Layers that block line-of-sight (walls, doors, large props).
    /// Defaults to all layers (~0). Override in scene-setup code if you have a dedicated
    /// "Obstacle" layer — e.g. NPCSelector.LosObstacleLayers = LayerMask.GetMask("Default", "Walls");
    public static LayerMask LosObstacleLayers = ~0;

    /// Y-offset from NPC root and event origin used for LOS raycasts (~standing eye height).
    public static float EyeHeight = 1.5f;

    /// Turn the LOS check off entirely (raycast skipped, losBonus always 0).
    /// Set false for ablation studies where perception should not contribute to scoring.
    public static bool LosEnabled = true;

    /// <summary>
    /// One-call helper for ablation experiments. Pass false to disable a term.
    /// Restore defaults by calling ResetWeights().
    /// </summary>
    public static void SetAblation(bool distance = true, bool role = true,
                                   bool state = true, bool los = true)
    {
        DistanceWeight = distance ? 2.0f : 0f;
        RoleWeight     = role     ? 1.0f : 0f;
        StateWeight    = state    ? 1.0f : 0f;
        LosWeight      = los      ? 1.0f : 0f;
        LosEnabled     = los;
    }

    /// <summary>Restore the design-spec defaults.</summary>
    public static void ResetWeights() => SetAblation();

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

            // Raw factors (0..N)
            float distRaw  = dist > 0.001f ? (1f / dist) : 1f;
            float roleRaw  = GetRoleBonus(npc.Role, e.Type);
            float stateRaw = npc.StateScore; // 0.0–1.0
            float losRaw   = (LosEnabled && HasLineOfSight(npc.Position, e.Origin)) ? 1f : 0f;

            // Weighted (set any *Weight to 0 to ablate that term)
            float distScore  = distRaw  * DistanceWeight;
            float roleScore  = roleRaw  * RoleWeight;
            float stateScore = stateRaw * StateWeight;
            float losScore   = losRaw   * LosWeight;
            float total      = distScore + roleScore + stateScore + losScore;

            string line = $"{npc.NPCId}:d={distScore:F2}+r={roleScore:F1}+s={stateScore:F2}+los={losScore:F1}={total:F2}";
            scoreLines?.Add(line);

            sb?.AppendLine(
                $"  • {npc.NPCId,-20}  dist={dist,5:F1}m  " +
                $"d={distScore:F3}  r={roleScore:F1}  s={stateScore:F2}  los={losScore:F1}  → {total:F2}");

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

    // ── Line-of-sight check ───────────────────────────────────────────────────

    /// <summary>
    /// Returns true if a raycast from the NPC's eye height to the event origin
    /// is not blocked by anything in LosObstacleLayers. Both endpoints are lifted
    /// by EyeHeight so floor geometry doesn't interfere.
    /// </summary>
    static bool HasLineOfSight(Vector3 npcPos, Vector3 eventOrigin)
    {
        Vector3 from = npcPos       + Vector3.up * EyeHeight;
        Vector3 to   = eventOrigin  + Vector3.up * EyeHeight;
        Vector3 dir  = to - from;
        float   dist = dir.magnitude;

        if (dist < 0.01f) return true; // co-located

        return !Physics.Raycast(from, dir.normalized, dist,
                                LosObstacleLayers, QueryTriggerInteraction.Ignore);
    }
}
