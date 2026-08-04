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
            Agent:Find distance-wise accuracy evaluation for Module 2
            IN
            This is a Unity-based military training simulation project (Module 2 relates to threat/decision behavior, likely FSM-based). The user wants to know: has "accuracy by distance" evaluation been done for Module 2 — i.e., calculating model/system accuracy as a function of distance (e.g., engagement distance, detection distance) and comparing those distance-bucketed accuracy values against expert-provided/expert-annotated reference values?
            
            I need you to research the codebase and docs (read-only, no edits) and report findings. Please:
            
            1. Search for any code, scripts, or reports that compute "accuracy" broken down "by distance" — look for terms like "distance", "accuracy", "expert", "ground truth", "AccuracyByDistance", "DistanceBucket", "expert value", "expert rating", "expert score" etc. Check especially:
               - Assets/Scripts/Tests/ (e.g. AblationExperimentRunner.cs, Module2TestRunner.cs, WeightSensitivityRunner.cs, HostageProfileTestRunner.cs)
               - Any Module2-related folders (Assets/Module2*, Module4/Data, etc.)
               - Root-level docs: MODULE2_ABLATION_STUDY_REPORT.md, MODULE2_WEIGHT_SENSITIVITY_REPORT.md, MODULE2_SELECTION_LITERATURE_JUSTIFICATION.md, MODULE_2_3_4_EXPANSION_CONTENT.md, MODULE2_PRESENTATION_SCRIPT.md, MODULE2_FSM_DIAGRAM_PROMPT.md, Module4_Documents/*.md, Final_Report_Team_Sentinels.docx (skip binary docx, just note existence), and any other Module2 related .md/.docx files at repo root.
               - Also check Assets/Docs/Literature_Review_Module2.docx (note existence, don't need to parse binary).
               - sentinels-aar/components/evaluation/EvaluationClient.jsx and any other evaluation-related JS/JSX files under sentinels-aar/.
            
            2. Determine specifically:
               - Is there existing code/logic that computes accuracy at different distance ranges/buckets (e.g., <5m, 5-10m, 10-20m etc.)?
               - Is there any comparison against "expert values" (i.e., expert-defined ground truth or expert ratings) at all, for Module 2, distance-wise or otherwise?
               - What metrics/evaluations ARE currently implemented for Module 2 (list them precisely, with file:line references)?
               - Are there any TODOs, comments, or report sections mentioning this planned-but-not-done, or is there literally nothing related?
            
            3. Report back concisely (under 500 words) with:
               - A clear yes/no on whether distance-wise accuracy vs expert values evaluation exists for Module 2
               - File:line references for anything relevant found
               - A short list of what evaluation/metrics DO currently exist for Module 2, for context
               - Any gaps you noticed (e.g., mentions in reports of "future work" for this)
            
            Do not modify any files. This is a research/discovery task only.
            Bash
            IN
            cd "c:\Users\navin\Downloads\Military-Training-System" && find Assets/Scripts/Tests -iname "*.cs" 2>/dev/null
            
            OUT
            Assets/Scripts/Tests/AblationExperimentRunner.cs
            Assets/Scripts/Tests/HostageProfileTestRunner.cs
            Assets/Scripts/Tests/Module2TestRunner.cs
            Assets/Scripts/Tests/WeightSensitivityRunner.cs
            Bash
            IN
            cd "c:\Users\navin\Downloads\Military-Training-System" && echo "--- grep expert ---" && grep -rniE "expert|ground.?truth" Assets/Scripts/Tests/AblationExperimentRunner.cs Assets/Scripts/Tests/HostageProfileTestRunner.cs Assets/Scripts/Tests/Module2TestRunner.cs Assets/Scripts/Tests/WeightSensitivityRunner.cs | head -80
            
            OUT
            --- grep expert ---
            Bash
            IN
            cd "c:\Users\navin\Downloads\Military-Training-System" && echo "--- repo-wide AccuracyByDistance / DistanceBucket ---" && git ls-files | xargs grep -lniE "AccuracyByDistance|DistanceBucket|accuracy.{0,20}distance|distance.{0,20}accuracy" 2>/dev/null
            
            OUT
            Exit code 123
            --- repo-wide AccuracyByDistance / DistanceBucket ---
            Assets/Module4/Analysis/PerformanceCalculator.cs
            Assets/Module4/Data/MissionEvent.cs
            Assets/Module4/Data/PerformanceSummary.cs
            Assets/Scripts/Events/ScenarioEvent.cs
            Module4_Documents/MODULE4_EVALUATION.md
            Module4_Documents/MODULE4_FULL_REPORT.md
            sentinels-aar/app/api/evaluation/route.js
            sentinels-aar/lib/expertBenchmarks.js
            sentinels-aar/lib/models/Session.js
            Bash
            IN
            cd "c:\Users\navin\Downloads\Military-Training-System" && grep -niE "module ?2|module2|distance" sentinels-aar/components/evaluation/EvaluationClient.jsx | head -40
            
            OUT
            13:// Measures the tier-by-tier significance test runs on — Module 2 ONLY. Module 2's whole
            19:  { key: 'perceivedIntelligence', label: 'Perceived Intelligence', source: 'aiEval',   group: 'Module 2 — AI Believability' },
            20:  { key: 'animacy',               label: 'Animacy (life-like)',    source: 'aiEval',   group: 'Module 2 — AI Believability' },
            21:  { key: 'realism',               label: 'Tactical realism',       source: 'aiEval',   group: 'Module 2 — AI Believability' },
            22:  { key: 'enemyAccuracy',         label: 'Enemy hit-rate (NPC)',   source: 'metrics',  group: 'Module 2 — AI Believability' },
            42:// itself a literature value. `accuracyScore` is Unity's real distance-normalized score
            44:// belongs to Module 2's own objective-telemetry table above.
            69:  { key: 'module2', label: 'Module 2 — AI Believability' },
            75:// All believability survey rows belong to Module 2 (the AI-behaviour survey).
            77:  { key: 'perceivedIntelligence', label: 'Perceived Intelligence', unit: '/5', better: 'up', module: 'module2' },
            78:  { key: 'animacy',               label: 'Animacy (life-like)',    unit: '/5', better: 'up', module: 'module2' },
            79:  { key: 'realism',               label: 'Tactical realism',       unit: '/5', better: 'up', module: 'module2' },
            80:  { key: 'ueqPragmatic',          label: 'UEQ — Pragmatic',        unit: '/7', better: 'up', module: 'module2' },
            81:  { key: 'ueqHedonic',            label: 'UEQ — Hedonic',          unit: '/7', better: 'up', module: 'module2' },
            87:  { key: 'enemyAccuracy',       label: 'Enemy hit-rate (NPC)',     unit: '%', better: 'up',   module: 'module2' },
            102:  const [activeTab, setActiveTab] = useState('module2')
            138:    a.href = url; a.download = 'module2-evaluation.csv'; a.click()
            152:            <div className={styles.kicker}>Module 2 · Ablation Study</div>
            194:                  {t.key === 'module2' && (
            196:                      {/* ── Per-player comparison, by AI tier — Module 2's own question ── */}
            225:                        complete-case participants — because AI difficulty IS Module 2's research question.
            234:                          Module 2's own question, above) — so a player's sessions are combined
            Bash
            IN
            cd "c:\Users\navin\Downloads\Military-Training-System" && grep -niE "distance" MODULE2_FULL_REPORT.md MODULE2_BEHAVIOR_RESEARCH.md MODULE2_EVALUATION_METHODOLOGY.md | head -60
            
            OUT
            MODULE2_EVALUATION_METHODOLOGY.md:69:- **Accuracy / hit‑rate** — shots hit ÷ fired; **engagement distance** distribution.
            Bash
            IN
            cd "c:\Users\navin\Downloads\Military-Training-System" && grep -niE "distance|enemyAccuracy" sentinels-aar/lib/scoreBreakdowns.js sentinels-aar/lib/models/Session.js | head -60
            
            
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
